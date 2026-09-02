/* =====================================================================
   024_pickup_proof.sql  --  Pickup proof (Step 4).

   A delivery now carries up to two proofs:
     - Pickup   : the driver collected the parcel from the sender
     - Delivery : the driver handed it to the recipient  (the original proof)

   Lifecycle:  Pending --pickup--> PickedUp --delivery--> Delivered | Failed
   A failed pickup (sender did not hand the parcel over) sets the delivery
   straight to Failed. The delivery proof is rejected until a pickup proof
   exists (THROW 50025).

   Schema changes:
     - dbo.Proof.ProofType  VARCHAR(10)  ('Pickup' | 'Delivery'), default 'Delivery'
       so every existing proof back-fills as a delivery proof.
     - UQ_Proof_Delivery (one proof per delivery)  ->  one proof per (delivery, type)
     - Proof.Status  + 'PickedUp'
     - Delivery.Status  + 'PickedUp'

   New THROWs: 50025 pickup-required, 50026 invalid status/type, 50027 wrong state.

   New script (not an edit to 020/022) so DbUp applies it to existing databases.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---------- Proof.ProofType ------------------------------------- */
IF COL_LENGTH(N'dbo.Proof', N'ProofType') IS NULL
    ALTER TABLE dbo.Proof
        ADD ProofType VARCHAR(10) NOT NULL
            CONSTRAINT DF_Proof_ProofType DEFAULT 'Delivery';
GO

/* ---------- one proof per (delivery, type) --------------------- */
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Proof_Delivery')
    ALTER TABLE dbo.Proof DROP CONSTRAINT UQ_Proof_Delivery;
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Proof_Delivery_Type')
    ALTER TABLE dbo.Proof
        ADD CONSTRAINT UQ_Proof_Delivery_Type UNIQUE (DeliveryId, ProofType);
GO

/* ---------- widened CHECK constraints -------------------------- */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Proof_ProofType')
    ALTER TABLE dbo.Proof DROP CONSTRAINT CK_Proof_ProofType;
GO
ALTER TABLE dbo.Proof
    ADD CONSTRAINT CK_Proof_ProofType CHECK (ProofType IN ('Pickup','Delivery'));
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Proof_Status')
    ALTER TABLE dbo.Proof DROP CONSTRAINT CK_Proof_Status;
GO
ALTER TABLE dbo.Proof
    ADD CONSTRAINT CK_Proof_Status CHECK (Status IN ('Delivered','Failed','PickedUp'));
GO

/* status must match the proof type */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Proof_Type_Status')
    ALTER TABLE dbo.Proof DROP CONSTRAINT CK_Proof_Type_Status;
GO
ALTER TABLE dbo.Proof
    ADD CONSTRAINT CK_Proof_Type_Status CHECK
    (
        (ProofType = 'Pickup'   AND Status IN ('PickedUp','Failed')) OR
        (ProofType = 'Delivery' AND Status IN ('Delivered','Failed'))
    );
GO

IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Delivery_Status')
    ALTER TABLE dbo.Delivery DROP CONSTRAINT CK_Delivery_Status;
GO
ALTER TABLE dbo.Delivery
    ADD CONSTRAINT CK_Delivery_Status CHECK (Status IN ('Pending','PickedUp','Delivered','Failed'));
GO

/* =====================================================================
   PROOF CAPTURE  --  now type-aware and enforces the lifecycle.
   ===================================================================== */
CREATE OR ALTER PROCEDURE dbo.usp_Proof_Create
    @CompanyId           INT,
    @ClientUuid          UNIQUEIDENTIFIER,
    @DeliveryId          INT,
    @DriverId            INT,
    @Status              VARCHAR(10),
    @ProofType           VARCHAR(10)   = 'Delivery',
    @FailureReason       NVARCHAR(300) = NULL,
    @RecipientSignedName NVARCHAR(150) = NULL,
    @SignatureUrl        NVARCHAR(400) = NULL,
    @CapturedLat         DECIMAL(9,6)  = NULL,
    @CapturedLng         DECIMAL(9,6)  = NULL,
    @CapturedAtUtc       DATETIME2(0),
    @Photos              dbo.ProofPhotoType READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    /* ---- idempotency short-circuit (offline queue may POST twice) ---- */
    DECLARE @existingId BIGINT = (SELECT Id FROM dbo.Proof WHERE ClientUuid = @ClientUuid);
    IF @existingId IS NOT NULL
    BEGIN
        SELECT Id, DeliveryId, Status, CAST(1 AS BIT) AS WasDuplicate, ProofType
        FROM   dbo.Proof WHERE Id = @existingId;
        RETURN;
    END

    IF @ProofType NOT IN ('Pickup','Delivery')
        THROW 50026, 'Invalid proof type.', 1;

    /* ---- delivery must exist in this tenant and be the driver's ---- */
    DECLARE @assigned INT, @deliveryStatus VARCHAR(10), @deliveryCompany INT;
    SELECT @assigned = AssignedDriverId, @deliveryStatus = Status, @deliveryCompany = CompanyId
    FROM   dbo.Delivery WHERE Id = @DeliveryId;

    IF @deliveryStatus IS NULL OR @deliveryCompany <> @CompanyId
        THROW 50020, 'Delivery not found.', 1;
    IF @assigned IS NULL OR @assigned <> @DriverId
        THROW 50021, 'Delivery is not assigned to this driver.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Proof WHERE DeliveryId = @DeliveryId AND ProofType = @ProofType)
        THROW 50022, 'This delivery already has this proof.', 1;

    /* ---- lifecycle rules ---- */
    IF @ProofType = 'Pickup'
    BEGIN
        IF @deliveryStatus <> 'Pending'
            THROW 50027, 'A pickup proof can only be captured for a pending delivery.', 1;
        IF @Status NOT IN ('PickedUp','Failed')
            THROW 50026, 'Invalid status for a pickup proof.', 1;
    END
    ELSE
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM dbo.Proof WHERE DeliveryId = @DeliveryId AND ProofType = 'Pickup')
            THROW 50025, 'A pickup proof is required before the delivery proof.', 1;
        IF @deliveryStatus <> 'PickedUp'
            THROW 50027, 'The delivery is not in a deliverable state.', 1;
        IF @Status NOT IN ('Delivered','Failed')
            THROW 50026, 'Invalid status for a delivery proof.', 1;
    END

    IF @Status = 'Failed' AND (@FailureReason IS NULL OR LTRIM(RTRIM(@FailureReason)) = '')
        THROW 50023, 'A failure reason is required when it failed.', 1;

    IF (SELECT COUNT(*) FROM @Photos) > 5
        THROW 50024, 'At most 5 photos are allowed.', 1;

    BEGIN TRAN;

    INSERT dbo.Proof (DeliveryId, DriverId, ProofType, Status, FailureReason, RecipientSignedName,
                      SignatureUrl, CapturedLat, CapturedLng, CapturedAtUtc, ClientUuid)
    VALUES (@DeliveryId, @DriverId, @ProofType, @Status, @FailureReason, @RecipientSignedName,
            @SignatureUrl, @CapturedLat, @CapturedLng, @CapturedAtUtc, @ClientUuid);

    DECLARE @proofId BIGINT = SCOPE_IDENTITY();

    INSERT dbo.ProofPhoto (ProofId, Url, OrderIndex)
    SELECT @proofId, Url, OrderIndex FROM @Photos;

    UPDATE dbo.Delivery
    SET    Status = CASE
                        WHEN @Status = 'Failed'      THEN 'Failed'
                        WHEN @ProofType = 'Pickup'   THEN 'PickedUp'
                        ELSE 'Delivered'
                    END
    WHERE  Id = @DeliveryId;

    COMMIT;

    SELECT @proofId AS Id, @DeliveryId AS DeliveryId, @Status AS Status,
           CAST(0 AS BIT) AS WasDuplicate, @ProofType AS ProofType;
END
GO

/* =====================================================================
   DRIVER / ADMIN read procs  --  rewritten to use EXISTS subqueries
   instead of a LEFT JOIN to dbo.Proof (which now fans a delivery out to
   two rows when it has both a pickup and a delivery proof).
   ===================================================================== */
CREATE OR ALTER PROCEDURE dbo.usp_Delivery_ListForDriver
    @CompanyId INT,
    @DriverId  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  d.Id, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
            d.Lat, d.Lng, d.Note, d.Status, d.CreatedAtUtc,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Proof p
                                   WHERE p.DeliveryId = d.Id AND p.ProofType = 'Delivery')
                      THEN 1 ELSE 0 END AS BIT) AS HasProof,
            CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Proof p
                                   WHERE p.DeliveryId = d.Id AND p.ProofType = 'Pickup')
                      THEN 1 ELSE 0 END AS BIT) AS HasPickupProof,
            CAST(CASE WHEN d.AssignedDriverId = @DriverId THEN 1 ELSE 0 END AS BIT) AS IsMine
    FROM    dbo.Delivery d
    WHERE   d.CompanyId = @CompanyId
      AND   (d.AssignedDriverId = @DriverId
             OR d.Status IN ('Pending','PickedUp')
             OR NOT EXISTS (SELECT 1 FROM dbo.Proof p WHERE p.DeliveryId = d.Id))
    ORDER   BY
        CASE WHEN d.AssignedDriverId = @DriverId THEN 0 ELSE 1 END,
        CASE d.Status WHEN 'Pending' THEN 0 WHEN 'PickedUp' THEN 1 WHEN 'Failed' THEN 2 ELSE 3 END,
        d.CreatedAtUtc DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Delivery_AdminSearch
    @CompanyId     INT,
    @Search        NVARCHAR(80) = NULL,
    @Status        VARCHAR(10)  = NULL,
    @DriverId      INT          = NULL,
    @FromDate      DATE         = NULL,
    @ToDate        DATE         = NULL,
    @SortColumn    VARCHAR(30)  = 'CreatedAtUtc',
    @SortDirection VARCHAR(4)   = 'DESC',
    @Skip          INT          = 0,
    @Take          INT          = 20
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH d AS
    (
        SELECT  dl.Id, dl.OrderRef, dl.RecipientName, dl.AddressText, dl.Status,
                dl.CreatedAtUtc, dl.AssignedDriverId, drv.Name AS AssignedDriverName,
                CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.Proof p
                                       WHERE p.DeliveryId = dl.Id AND p.ProofType = 'Delivery')
                          THEN 1 ELSE 0 END AS BIT) AS HasProof
        FROM    dbo.Delivery dl
        LEFT    JOIN dbo.AppUser drv ON drv.Id = dl.AssignedDriverId
        WHERE  dl.CompanyId = @CompanyId
          AND  (@Status   IS NULL OR dl.Status = @Status)
          AND  (@DriverId IS NULL OR dl.AssignedDriverId = @DriverId)
          AND  (@FromDate IS NULL OR dl.CreatedAtUtc >= @FromDate)
          AND  (@ToDate   IS NULL OR dl.CreatedAtUtc <  DATEADD(DAY, 1, @ToDate))
          AND  (@Search   IS NULL OR dl.OrderRef LIKE '%' + @Search + '%'
                                  OR dl.RecipientName LIKE '%' + @Search + '%')
    )
    SELECT *
    FROM   d
    ORDER  BY
        CASE WHEN @SortColumn = 'OrderRef'      AND @SortDirection = 'ASC'  THEN d.OrderRef      END ASC,
        CASE WHEN @SortColumn = 'OrderRef'      AND @SortDirection = 'DESC' THEN d.OrderRef      END DESC,
        CASE WHEN @SortColumn = 'RecipientName' AND @SortDirection = 'ASC'  THEN d.RecipientName END ASC,
        CASE WHEN @SortColumn = 'RecipientName' AND @SortDirection = 'DESC' THEN d.RecipientName END DESC,
        CASE WHEN @SortColumn = 'Status'        AND @SortDirection = 'ASC'  THEN d.Status        END ASC,
        CASE WHEN @SortColumn = 'Status'        AND @SortDirection = 'DESC' THEN d.Status        END DESC,
        CASE WHEN @SortColumn = 'CreatedAtUtc'  AND @SortDirection = 'ASC'  THEN d.CreatedAtUtc  END ASC,
        d.CreatedAtUtc DESC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM   dbo.Delivery dl
    WHERE dl.CompanyId = @CompanyId
      AND (@Status   IS NULL OR dl.Status = @Status)
      AND (@DriverId IS NULL OR dl.AssignedDriverId = @DriverId)
      AND (@FromDate IS NULL OR dl.CreatedAtUtc >= @FromDate)
      AND (@ToDate   IS NULL OR dl.CreatedAtUtc <  DATEADD(DAY, 1, @ToDate))
      AND (@Search   IS NULL OR dl.OrderRef LIKE '%' + @Search + '%'
                             OR dl.RecipientName LIKE '%' + @Search + '%');
END
GO

/* =====================================================================
   OPS PANEL proof list / detail  --  expose ProofType + filter by it.
   ===================================================================== */
CREATE OR ALTER PROCEDURE dbo.usp_Admin_ProofSearch
    @CompanyId     INT,
    @FromDate      DATE        = NULL,
    @ToDate        DATE        = NULL,
    @DriverId      INT         = NULL,
    @Status        VARCHAR(10) = NULL,
    @ProofType     VARCHAR(10) = NULL,
    @Search        NVARCHAR(80) = NULL,
    @SortColumn    VARCHAR(30) = 'CapturedAtUtc',
    @SortDirection VARCHAR(4)  = 'DESC',
    @Skip          INT         = 0,
    @Take          INT         = 20
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH pr AS
    (
        SELECT  p.Id, p.DeliveryId, d.OrderRef, d.RecipientName, d.AddressText,
                p.ProofType, p.Status, p.FailureReason, p.RecipientSignedName,
                p.CapturedLat, p.CapturedLng, p.CapturedAtUtc, p.SyncedAtUtc,
                drv.Id AS DriverId, drv.Name AS DriverName,
                (SELECT COUNT(*) FROM dbo.ProofPhoto ph WHERE ph.ProofId = p.Id) AS PhotoCount
        FROM    dbo.Proof    p
        JOIN    dbo.Delivery d   ON d.Id  = p.DeliveryId
        JOIN    dbo.AppUser  drv ON drv.Id = p.DriverId
        WHERE  d.CompanyId = @CompanyId
          AND  (@FromDate  IS NULL OR p.CapturedAtUtc >= @FromDate)
          AND  (@ToDate    IS NULL OR p.CapturedAtUtc <  DATEADD(DAY, 1, @ToDate))
          AND  (@DriverId  IS NULL OR p.DriverId = @DriverId)
          AND  (@Status    IS NULL OR p.Status = @Status)
          AND  (@ProofType IS NULL OR p.ProofType = @ProofType)
          AND  (@Search    IS NULL OR d.OrderRef LIKE '%' + @Search + '%')
    )
    SELECT *
    FROM   pr
    ORDER  BY
        CASE WHEN @SortColumn = 'OrderRef'      AND @SortDirection = 'ASC'  THEN pr.OrderRef      END ASC,
        CASE WHEN @SortColumn = 'OrderRef'      AND @SortDirection = 'DESC' THEN pr.OrderRef      END DESC,
        CASE WHEN @SortColumn = 'DriverName'    AND @SortDirection = 'ASC'  THEN pr.DriverName    END ASC,
        CASE WHEN @SortColumn = 'DriverName'    AND @SortDirection = 'DESC' THEN pr.DriverName    END DESC,
        CASE WHEN @SortColumn = 'Status'        AND @SortDirection = 'ASC'  THEN pr.Status        END ASC,
        CASE WHEN @SortColumn = 'Status'        AND @SortDirection = 'DESC' THEN pr.Status        END DESC,
        CASE WHEN @SortColumn = 'CapturedAtUtc' AND @SortDirection = 'ASC'  THEN pr.CapturedAtUtc END ASC,
        pr.CapturedAtUtc DESC
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;

    SELECT COUNT(*) AS TotalCount
    FROM   dbo.Proof p
    JOIN   dbo.Delivery d ON d.Id = p.DeliveryId
    WHERE d.CompanyId = @CompanyId
      AND (@FromDate  IS NULL OR p.CapturedAtUtc >= @FromDate)
      AND (@ToDate    IS NULL OR p.CapturedAtUtc <  DATEADD(DAY, 1, @ToDate))
      AND (@DriverId  IS NULL OR p.DriverId = @DriverId)
      AND (@Status    IS NULL OR p.Status = @Status)
      AND (@ProofType IS NULL OR p.ProofType = @ProofType)
      AND (@Search    IS NULL OR d.OrderRef LIKE '%' + @Search + '%');
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Admin_ProofGetById
    @CompanyId INT,
    @Id        BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  p.Id, p.DeliveryId, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
            d.Lat AS DeliveryLat, d.Lng AS DeliveryLng,
            p.ProofType, p.Status, p.FailureReason, p.RecipientSignedName, p.SignatureUrl,
            p.CapturedLat, p.CapturedLng, p.CapturedAtUtc, p.SyncedAtUtc,
            drv.Id AS DriverId, drv.Name AS DriverName, drv.Phone AS DriverPhone
    FROM    dbo.Proof    p
    JOIN    dbo.Delivery d   ON d.Id   = p.DeliveryId
    JOIN    dbo.AppUser  drv ON drv.Id = p.DriverId
    WHERE   p.Id = @Id AND d.CompanyId = @CompanyId;

    SELECT ph.Id, ph.Url, ph.OrderIndex
    FROM   dbo.ProofPhoto ph
    JOIN   dbo.Proof p    ON p.Id = ph.ProofId
    JOIN   dbo.Delivery d ON d.Id = p.DeliveryId
    WHERE  ph.ProofId = @Id AND d.CompanyId = @CompanyId
    ORDER  BY ph.OrderIndex;
END
GO

/* Dashboard trend must count delivery proofs only — pickup proofs would
   otherwise skew "delivered vs failed". */
CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Summary
    @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status IN ('Pending','PickedUp')) AS PendingCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status = 'Delivered')             AS DeliveredCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status = 'Failed')                AS FailedCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status = 'Pending' AND AssignedDriverId IS NULL) AS UnassignedCount;

    SELECT CAST(p.CapturedAtUtc AS DATE) AS Day,
           SUM(CASE WHEN p.Status = 'Delivered' THEN 1 ELSE 0 END) AS Delivered,
           SUM(CASE WHEN p.Status = 'Failed'    THEN 1 ELSE 0 END) AS Failed
    FROM   dbo.Proof    p
    JOIN   dbo.Delivery d ON d.Id = p.DeliveryId
    WHERE  d.CompanyId = @CompanyId
      AND  p.ProofType = 'Delivery'
      AND  p.CapturedAtUtc >= DATEADD(DAY, -7, CAST(SYSUTCDATETIME() AS DATE))
    GROUP  BY CAST(p.CapturedAtUtc AS DATE)
    ORDER  BY Day;
END
GO
