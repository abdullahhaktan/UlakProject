/* =====================================================================
   020_stored_procedures.sql
   Every business operation is a stored procedure, called from the Dapper
   repositories. CREATE OR ALTER keeps them re-runnable; later changes
   ship as new numbered scripts (021_, 022_, ...).
   ===================================================================== */
GO

/* =======================  AUTH  ==================================== */

CREATE OR ALTER PROCEDURE dbo.usp_Auth_GetUserByPhone
    @Phone VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, CompanyId, Phone, PasswordHash, Name, Role, IsActive
    FROM   dbo.AppUser
    WHERE  Phone = @Phone;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_StoreRefreshToken
    @UserId       INT,
    @TokenHash    CHAR(64),
    @ExpiresAtUtc DATETIME2(0)
AS
BEGIN
    SET NOCOUNT ON;
    INSERT dbo.RefreshToken (UserId, TokenHash, ExpiresAtUtc)
    VALUES (@UserId, @TokenHash, @ExpiresAtUtc);
END
GO

/* Returns the owning user only when the token is live (found, not
   revoked, not expired). Empty result set otherwise. */
CREATE OR ALTER PROCEDURE dbo.usp_Auth_ValidateRefreshToken
    @TokenHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Id, u.CompanyId, u.Phone, u.Name, u.Role, u.IsActive
    FROM   dbo.RefreshToken t
    JOIN   dbo.AppUser      u ON u.Id = t.UserId
    WHERE  t.TokenHash    = @TokenHash
      AND  t.RevokedAtUtc IS NULL
      AND  t.ExpiresAtUtc > SYSUTCDATETIME()
      AND  u.IsActive      = 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_RevokeRefreshToken
    @TokenHash CHAR(64)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.RefreshToken
    SET    RevokedAtUtc = SYSUTCDATETIME()
    WHERE  TokenHash = @TokenHash AND RevokedAtUtc IS NULL;
END
GO

/* =======================  DELIVERIES  ============================= */

/* Driver's own deliveries for a given day (defaults to today, UTC). */
CREATE OR ALTER PROCEDURE dbo.usp_Delivery_ListForDriver
    @DriverId INT,
    @Date     DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @Date = COALESCE(@Date, CAST(SYSUTCDATETIME() AS DATE));

    SELECT  d.Id, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
            d.Lat, d.Lng, d.Note, d.Status, d.CreatedAtUtc,
            CAST(CASE WHEN p.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS HasProof
    FROM    dbo.Delivery d
    LEFT    JOIN dbo.Proof p ON p.DeliveryId = d.Id
    WHERE   d.AssignedDriverId = @DriverId
      AND   CAST(d.CreatedAtUtc AS DATE) = @Date
    ORDER   BY d.CreatedAtUtc;
END
GO

/* Ops sees any delivery; a driver only sees one assigned to them.
   Unauthorised access returns an empty result set (404 at the API). */
CREATE OR ALTER PROCEDURE dbo.usp_Delivery_GetById
    @Id               INT,
    @RequestingUserId INT,
    @Role             VARCHAR(10)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  d.Id, d.CompanyId, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
            d.Lat, d.Lng, d.Note, d.AssignedDriverId, drv.Name AS AssignedDriverName,
            d.Status, d.CreatedAtUtc
    FROM    dbo.Delivery d
    LEFT    JOIN dbo.AppUser drv ON drv.Id = d.AssignedDriverId
    WHERE   d.Id = @Id
      AND  (@Role = 'Ops' OR d.AssignedDriverId = @RequestingUserId);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Delivery_Create
    @CompanyId       INT,
    @OrderRef        VARCHAR(40),
    @RecipientName   NVARCHAR(150),
    @RecipientPhone  VARCHAR(20)   = NULL,
    @AddressText     NVARCHAR(400),
    @Lat             DECIMAL(9,6)  = NULL,
    @Lng             DECIMAL(9,6)  = NULL,
    @Note            NVARCHAR(500) = NULL,
    @AssignedDriverId INT          = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Delivery WHERE CompanyId = @CompanyId AND OrderRef = @OrderRef)
        THROW 50010, 'A delivery with this order reference already exists.', 1;

    IF @AssignedDriverId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE Id = @AssignedDriverId AND Role = 'Driver' AND IsActive = 1)
        THROW 50011, 'Assigned driver is not a valid active driver.', 1;

    INSERT dbo.Delivery (CompanyId, OrderRef, RecipientName, RecipientPhone, AddressText,
                         Lat, Lng, Note, AssignedDriverId)
    VALUES (@CompanyId, @OrderRef, @RecipientName, @RecipientPhone, @AddressText,
            @Lat, @Lng, @Note, @AssignedDriverId);

    SELECT SCOPE_IDENTITY() AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Delivery_Assign
    @DeliveryId INT,
    @DriverId   INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Delivery WHERE Id = @DeliveryId)
        THROW 50012, 'Delivery not found.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser WHERE Id = @DriverId AND Role = 'Driver' AND IsActive = 1)
        THROW 50011, 'Assigned driver is not a valid active driver.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Delivery WHERE Id = @DeliveryId AND Status <> 'Pending')
        THROW 50013, 'Only pending deliveries can be reassigned.', 1;

    UPDATE dbo.Delivery SET AssignedDriverId = @DriverId WHERE Id = @DeliveryId;

    SELECT Id, AssignedDriverId FROM dbo.Delivery WHERE Id = @DeliveryId;
END
GO

/* Paged/filtered list for the ops panel grid (server-side paging). */
CREATE OR ALTER PROCEDURE dbo.usp_Delivery_AdminSearch
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
                CAST(CASE WHEN p.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS HasProof
        FROM    dbo.Delivery dl
        LEFT    JOIN dbo.AppUser drv ON drv.Id = dl.AssignedDriverId
        LEFT    JOIN dbo.Proof   p   ON p.DeliveryId = dl.Id
        WHERE  (@Status   IS NULL OR dl.Status = @Status)
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
    WHERE (@Status   IS NULL OR dl.Status = @Status)
      AND (@DriverId IS NULL OR dl.AssignedDriverId = @DriverId)
      AND (@FromDate IS NULL OR dl.CreatedAtUtc >= @FromDate)
      AND (@ToDate   IS NULL OR dl.CreatedAtUtc <  DATEADD(DAY, 1, @ToDate))
      AND (@Search   IS NULL OR dl.OrderRef LIKE '%' + @Search + '%'
                             OR dl.RecipientName LIKE '%' + @Search + '%');
END
GO

/* =======================  PROOFS  ================================= */

/* Idempotent proof capture. Re-sending the same ClientUuid returns the
   existing proof unchanged (the offline queue may POST twice). */
CREATE OR ALTER PROCEDURE dbo.usp_Proof_Create
    @ClientUuid          UNIQUEIDENTIFIER,
    @DeliveryId          INT,
    @DriverId            INT,
    @Status              VARCHAR(10),
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

    /* ---- idempotency short-circuit ---- */
    DECLARE @existingId BIGINT = (SELECT Id FROM dbo.Proof WHERE ClientUuid = @ClientUuid);
    IF @existingId IS NOT NULL
    BEGIN
        SELECT Id, DeliveryId, Status, CAST(1 AS BIT) AS WasDuplicate FROM dbo.Proof WHERE Id = @existingId;
        RETURN;
    END

    /* ---- authorisation: driver must own this delivery ---- */
    DECLARE @assigned INT, @deliveryStatus VARCHAR(10);
    SELECT @assigned = AssignedDriverId, @deliveryStatus = Status
    FROM   dbo.Delivery WHERE Id = @DeliveryId;

    IF @deliveryStatus IS NULL
        THROW 50020, 'Delivery not found.', 1;
    IF @assigned IS NULL OR @assigned <> @DriverId
        THROW 50021, 'Delivery is not assigned to this driver.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Proof WHERE DeliveryId = @DeliveryId)
        THROW 50022, 'This delivery already has a proof.', 1;

    IF @Status = 'Failed' AND (@FailureReason IS NULL OR LTRIM(RTRIM(@FailureReason)) = '')
        THROW 50023, 'A failure reason is required when the delivery failed.', 1;

    IF (SELECT COUNT(*) FROM @Photos) > 5
        THROW 50024, 'At most 5 photos are allowed.', 1;

    BEGIN TRAN;

    INSERT dbo.Proof (DeliveryId, DriverId, Status, FailureReason, RecipientSignedName, SignatureUrl,
                      CapturedLat, CapturedLng, CapturedAtUtc, ClientUuid)
    VALUES (@DeliveryId, @DriverId, @Status, @FailureReason, @RecipientSignedName, @SignatureUrl,
            @CapturedLat, @CapturedLng, @CapturedAtUtc, @ClientUuid);

    DECLARE @proofId BIGINT = SCOPE_IDENTITY();

    INSERT dbo.ProofPhoto (ProofId, Url, OrderIndex)
    SELECT @proofId, Url, OrderIndex FROM @Photos;

    UPDATE dbo.Delivery
    SET    Status = CASE WHEN @Status = 'Delivered' THEN 'Delivered' ELSE 'Failed' END
    WHERE  Id = @DeliveryId;

    COMMIT;

    SELECT @proofId AS Id, @DeliveryId AS DeliveryId, @Status AS Status, CAST(0 AS BIT) AS WasDuplicate;
END
GO

/* Paged/filtered proof list for the ops panel. */
CREATE OR ALTER PROCEDURE dbo.usp_Admin_ProofSearch
    @FromDate      DATE        = NULL,
    @ToDate        DATE        = NULL,
    @DriverId      INT         = NULL,
    @Status        VARCHAR(10) = NULL,
    @Search        NVARCHAR(80) = NULL,   -- OrderRef
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
                p.Status, p.FailureReason, p.RecipientSignedName,
                p.CapturedLat, p.CapturedLng, p.CapturedAtUtc, p.SyncedAtUtc,
                drv.Id AS DriverId, drv.Name AS DriverName,
                (SELECT COUNT(*) FROM dbo.ProofPhoto ph WHERE ph.ProofId = p.Id) AS PhotoCount
        FROM    dbo.Proof    p
        JOIN    dbo.Delivery d   ON d.Id  = p.DeliveryId
        JOIN    dbo.AppUser  drv ON drv.Id = p.DriverId
        WHERE  (@FromDate IS NULL OR p.CapturedAtUtc >= @FromDate)
          AND  (@ToDate   IS NULL OR p.CapturedAtUtc <  DATEADD(DAY, 1, @ToDate))
          AND  (@DriverId IS NULL OR p.DriverId = @DriverId)
          AND  (@Status   IS NULL OR p.Status = @Status)
          AND  (@Search   IS NULL OR d.OrderRef LIKE '%' + @Search + '%')
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
    WHERE (@FromDate IS NULL OR p.CapturedAtUtc >= @FromDate)
      AND (@ToDate   IS NULL OR p.CapturedAtUtc <  DATEADD(DAY, 1, @ToDate))
      AND (@DriverId IS NULL OR p.DriverId = @DriverId)
      AND (@Status   IS NULL OR p.Status = @Status)
      AND (@Search   IS NULL OR d.OrderRef LIKE '%' + @Search + '%');
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Admin_ProofGetById
    @Id BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  p.Id, p.DeliveryId, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
            d.Lat AS DeliveryLat, d.Lng AS DeliveryLng,
            p.Status, p.FailureReason, p.RecipientSignedName, p.SignatureUrl,
            p.CapturedLat, p.CapturedLng, p.CapturedAtUtc, p.SyncedAtUtc,
            drv.Id AS DriverId, drv.Name AS DriverName, drv.Phone AS DriverPhone
    FROM    dbo.Proof    p
    JOIN    dbo.Delivery d   ON d.Id   = p.DeliveryId
    JOIN    dbo.AppUser  drv ON drv.Id = p.DriverId
    WHERE   p.Id = @Id;

    SELECT Id, Url, OrderIndex
    FROM   dbo.ProofPhoto
    WHERE  ProofId = @Id
    ORDER  BY OrderIndex;
END
GO

/* =======================  DASHBOARD  ============================== */

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Summary
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(*) FROM dbo.Delivery WHERE Status = 'Pending')                        AS PendingCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE Status = 'Delivered')                      AS DeliveredCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE Status = 'Failed')                         AS FailedCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE Status = 'Pending' AND AssignedDriverId IS NULL) AS UnassignedCount;

    SELECT CAST(p.CapturedAtUtc AS DATE) AS Day,
           SUM(CASE WHEN p.Status = 'Delivered' THEN 1 ELSE 0 END) AS Delivered,
           SUM(CASE WHEN p.Status = 'Failed'    THEN 1 ELSE 0 END) AS Failed
    FROM   dbo.Proof p
    WHERE  p.CapturedAtUtc >= DATEADD(DAY, -7, CAST(SYSUTCDATETIME() AS DATE))
    GROUP  BY CAST(p.CapturedAtUtc AS DATE)
    ORDER  BY Day;
END
GO
