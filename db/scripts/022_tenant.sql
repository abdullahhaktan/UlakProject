/* =====================================================================
   022_tenant.sql  --  Multi-tenant + self-service onboarding.

   - Role 'Ops' -> 'Admin' (firm owner: web panel + mobile, manages drivers).
   - AppUser.MustChangePassword for invited drivers.
   - dbo.CompanySettings (per-tenant config).
   - Delivery.CustomerName / AgreedPrice (solo-courier scenario).
   - New procs: usp_Company_SignUp, usp_AppUser_CreateDriver, usp_Auth_ChangePassword.
   - Every delivery/proof/dashboard proc now takes @CompanyId and filters on it.
     Tenant scope is enforced in the app layer: the API always passes the caller's
     company_id claim; the client can never choose it. (SQL Server RLS is a later
     defence-in-depth step.)

   New script (not an edit to 020/021) so DbUp applies it to existing databases.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---------- AppUser: MustChangePassword + role migration ---------- */
IF COL_LENGTH(N'dbo.AppUser', N'MustChangePassword') IS NULL
    ALTER TABLE dbo.AppUser
        ADD MustChangePassword BIT NOT NULL
            CONSTRAINT DF_AppUser_MustChangePassword DEFAULT 0;
GO

/* drop the old CHECK first — it still forbids 'Admin', so the UPDATE below
   would fail against it on a database that already has 'Ops' rows. */
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_AppUser_Role')
    ALTER TABLE dbo.AppUser DROP CONSTRAINT CK_AppUser_Role;
GO

UPDATE dbo.AppUser SET Role = 'Admin' WHERE Role = 'Ops';
GO

ALTER TABLE dbo.AppUser
    ADD CONSTRAINT CK_AppUser_Role CHECK (Role IN ('Driver','Admin'));
GO

/* ---------- CompanySettings (one row per tenant) ---------------- */
IF OBJECT_ID(N'dbo.CompanySettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CompanySettings
    (
        CompanyId        INT           NOT NULL
                         CONSTRAINT PK_CompanySettings PRIMARY KEY
                         CONSTRAINT FK_CompanySettings_Company REFERENCES dbo.Company(Id),
        DisplayName      NVARCHAR(200) NOT NULL,
        PricingModel     VARCHAR(10)   NOT NULL CONSTRAINT DF_CompanySettings_PricingModel DEFAULT 'Flat',
        FlatRate         DECIMAL(10,2) NULL,
        PerKmRate        DECIMAL(10,2) NULL,
        Currency         CHAR(3)       NOT NULL CONSTRAINT DF_CompanySettings_Currency DEFAULT 'TRY',
        RequirePhoto     BIT           NOT NULL CONSTRAINT DF_CompanySettings_RequirePhoto DEFAULT 1,
        RequireSignature BIT           NOT NULL CONSTRAINT DF_CompanySettings_RequireSignature DEFAULT 0,
        LogoObjectKey    NVARCHAR(400) NULL,
        CreatedAtUtc     DATETIME2(0)  NOT NULL CONSTRAINT DF_CompanySettings_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT CK_CompanySettings_PricingModel CHECK (PricingModel IN ('Flat','PerKm'))
    );
END
GO

/* backfill settings for companies created before this script */
INSERT dbo.CompanySettings (CompanyId, DisplayName)
SELECT c.Id, c.Name
FROM   dbo.Company c
WHERE  NOT EXISTS (SELECT 1 FROM dbo.CompanySettings s WHERE s.CompanyId = c.Id);
GO

/* ---------- Delivery: customer + agreed price ------------------- */
IF COL_LENGTH(N'dbo.Delivery', N'CustomerName') IS NULL
    ALTER TABLE dbo.Delivery ADD CustomerName NVARCHAR(150) NULL;
GO
IF COL_LENGTH(N'dbo.Delivery', N'AgreedPrice') IS NULL
    ALTER TABLE dbo.Delivery ADD AgreedPrice DECIMAL(10,2) NULL;
GO

/* =====================================================================
   ONBOARDING PROCS
   ===================================================================== */

/* Self-service sign-up: new company + its first Admin + default settings. */
CREATE OR ALTER PROCEDURE dbo.usp_Company_SignUp
    @CompanyName  NVARCHAR(200),
    @AdminName    NVARCHAR(120),
    @Phone        VARCHAR(20),
    @PasswordHash VARCHAR(300)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM dbo.AppUser WHERE Phone = @Phone)
        THROW 50030, 'Bu telefon numarasi zaten kayitli.', 1;

    BEGIN TRAN;

    INSERT dbo.Company (Name) VALUES (@CompanyName);
    DECLARE @companyId INT = SCOPE_IDENTITY();

    INSERT dbo.AppUser (CompanyId, Phone, PasswordHash, Name, Role, MustChangePassword)
    VALUES (@companyId, @Phone, @PasswordHash, @AdminName, 'Admin', 0);
    DECLARE @userId INT = SCOPE_IDENTITY();

    INSERT dbo.CompanySettings (CompanyId, DisplayName)
    VALUES (@companyId, @CompanyName);

    COMMIT;

    SELECT Id, CompanyId, Phone, Name, Role, IsActive
    FROM   dbo.AppUser WHERE Id = @userId;
END
GO

/* Admin adds a driver to their own company. Caller passes a generated
   temp password hash; the driver must change it on first login. */
CREATE OR ALTER PROCEDURE dbo.usp_AppUser_CreateDriver
    @CompanyId    INT,
    @Name         NVARCHAR(120),
    @Phone        VARCHAR(20),
    @PasswordHash VARCHAR(300)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.AppUser WHERE Phone = @Phone)
        THROW 50031, 'Bu telefon numarasi zaten kayitli.', 1;

    INSERT dbo.AppUser (CompanyId, Phone, PasswordHash, Name, Role, MustChangePassword)
    VALUES (@CompanyId, @Phone, @PasswordHash, @Name, 'Driver', 1);

    SELECT Id, CompanyId, Phone, Name, Role, IsActive
    FROM   dbo.AppUser WHERE Id = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Auth_ChangePassword
    @UserId          INT,
    @NewPasswordHash VARCHAR(300)
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.AppUser
    SET    PasswordHash = @NewPasswordHash,
           MustChangePassword = 0
    WHERE  Id = @UserId;
END
GO

/* usp_Auth_GetUserByPhone also returns MustChangePassword now. */
CREATE OR ALTER PROCEDURE dbo.usp_Auth_GetUserByPhone
    @Phone VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, CompanyId, Phone, PasswordHash, Name, Role, IsActive, MustChangePassword
    FROM   dbo.AppUser
    WHERE  Phone = @Phone;
END
GO

/* =====================================================================
   TENANT-SCOPED PROCS  (every one now takes @CompanyId)
   ===================================================================== */

/* Driver's deliveries: the WHOLE company's open list, each row flagged
   IsMine. Teammates' rows are read-only (enforced on writes elsewhere). */
CREATE OR ALTER PROCEDURE dbo.usp_Delivery_ListForDriver
    @CompanyId INT,
    @DriverId  INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  d.Id, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
            d.Lat, d.Lng, d.Note, d.Status, d.CreatedAtUtc,
            CAST(CASE WHEN p.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS HasProof,
            CAST(CASE WHEN d.AssignedDriverId = @DriverId THEN 1 ELSE 0 END AS BIT) AS IsMine
    FROM    dbo.Delivery d
    LEFT    JOIN dbo.Proof p ON p.DeliveryId = d.Id
    WHERE   d.CompanyId = @CompanyId
      AND   (d.AssignedDriverId = @DriverId OR d.Status = 'Pending' OR p.Id IS NULL)
    ORDER   BY
        CASE WHEN d.AssignedDriverId = @DriverId THEN 0 ELSE 1 END,
        CASE d.Status WHEN 'Pending' THEN 0 WHEN 'Failed' THEN 1 ELSE 2 END,
        d.CreatedAtUtc DESC;
END
GO

/* Any user in the tenant may read any delivery in the tenant. */
CREATE OR ALTER PROCEDURE dbo.usp_Delivery_GetById
    @CompanyId INT,
    @Id        INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT  d.Id, d.CompanyId, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
            d.Lat, d.Lng, d.Note, d.AssignedDriverId, drv.Name AS AssignedDriverName,
            d.Status, d.CreatedAtUtc
    FROM    dbo.Delivery d
    LEFT    JOIN dbo.AppUser drv ON drv.Id = d.AssignedDriverId
    WHERE   d.Id = @Id AND d.CompanyId = @CompanyId;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Delivery_Create
    @CompanyId        INT,
    @OrderRef         VARCHAR(40),
    @RecipientName    NVARCHAR(150),
    @RecipientPhone   VARCHAR(20)   = NULL,
    @AddressText      NVARCHAR(400),
    @Lat              DECIMAL(9,6)  = NULL,
    @Lng              DECIMAL(9,6)  = NULL,
    @Note             NVARCHAR(500) = NULL,
    @AssignedDriverId INT           = NULL,
    @CustomerName     NVARCHAR(150) = NULL,
    @AgreedPrice      DECIMAL(10,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Delivery WHERE CompanyId = @CompanyId AND OrderRef = @OrderRef)
        THROW 50010, 'A delivery with this order reference already exists.', 1;

    IF @AssignedDriverId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.AppUser
                       WHERE Id = @AssignedDriverId AND Role = 'Driver'
                         AND IsActive = 1 AND CompanyId = @CompanyId)
        THROW 50011, 'Assigned driver is not a valid active driver in this company.', 1;

    INSERT dbo.Delivery (CompanyId, OrderRef, RecipientName, RecipientPhone, AddressText,
                         Lat, Lng, Note, AssignedDriverId, CustomerName, AgreedPrice)
    VALUES (@CompanyId, @OrderRef, @RecipientName, @RecipientPhone, @AddressText,
            @Lat, @Lng, @Note, @AssignedDriverId, @CustomerName, @AgreedPrice);

    SELECT SCOPE_IDENTITY() AS Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Delivery_Assign
    @CompanyId  INT,
    @DeliveryId INT,
    @DriverId   INT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Delivery WHERE Id = @DeliveryId AND CompanyId = @CompanyId)
        THROW 50012, 'Delivery not found.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser
                   WHERE Id = @DriverId AND Role = 'Driver'
                     AND IsActive = 1 AND CompanyId = @CompanyId)
        THROW 50011, 'Assigned driver is not a valid active driver in this company.', 1;

    IF EXISTS (SELECT 1 FROM dbo.Delivery WHERE Id = @DeliveryId AND Status <> 'Pending')
        THROW 50013, 'Only pending deliveries can be reassigned.', 1;

    UPDATE dbo.Delivery SET AssignedDriverId = @DriverId WHERE Id = @DeliveryId;

    SELECT Id, AssignedDriverId FROM dbo.Delivery WHERE Id = @DeliveryId;
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
                CAST(CASE WHEN p.Id IS NULL THEN 0 ELSE 1 END AS BIT) AS HasProof
        FROM    dbo.Delivery dl
        LEFT    JOIN dbo.AppUser drv ON drv.Id = dl.AssignedDriverId
        LEFT    JOIN dbo.Proof   p   ON p.DeliveryId = dl.Id
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

/* Proof capture — now also checks the delivery belongs to the caller's company. */
CREATE OR ALTER PROCEDURE dbo.usp_Proof_Create
    @CompanyId           INT,
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

    DECLARE @existingId BIGINT = (SELECT Id FROM dbo.Proof WHERE ClientUuid = @ClientUuid);
    IF @existingId IS NOT NULL
    BEGIN
        SELECT Id, DeliveryId, Status, CAST(1 AS BIT) AS WasDuplicate FROM dbo.Proof WHERE Id = @existingId;
        RETURN;
    END

    DECLARE @assigned INT, @deliveryStatus VARCHAR(10), @deliveryCompany INT;
    SELECT @assigned = AssignedDriverId, @deliveryStatus = Status, @deliveryCompany = CompanyId
    FROM   dbo.Delivery WHERE Id = @DeliveryId;

    IF @deliveryStatus IS NULL OR @deliveryCompany <> @CompanyId
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

CREATE OR ALTER PROCEDURE dbo.usp_Admin_ProofSearch
    @CompanyId     INT,
    @FromDate      DATE        = NULL,
    @ToDate        DATE        = NULL,
    @DriverId      INT         = NULL,
    @Status        VARCHAR(10) = NULL,
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
                p.Status, p.FailureReason, p.RecipientSignedName,
                p.CapturedLat, p.CapturedLng, p.CapturedAtUtc, p.SyncedAtUtc,
                drv.Id AS DriverId, drv.Name AS DriverName,
                (SELECT COUNT(*) FROM dbo.ProofPhoto ph WHERE ph.ProofId = p.Id) AS PhotoCount
        FROM    dbo.Proof    p
        JOIN    dbo.Delivery d   ON d.Id  = p.DeliveryId
        JOIN    dbo.AppUser  drv ON drv.Id = p.DriverId
        WHERE  d.CompanyId = @CompanyId
          AND  (@FromDate IS NULL OR p.CapturedAtUtc >= @FromDate)
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
    WHERE d.CompanyId = @CompanyId
      AND (@FromDate IS NULL OR p.CapturedAtUtc >= @FromDate)
      AND (@ToDate   IS NULL OR p.CapturedAtUtc <  DATEADD(DAY, 1, @ToDate))
      AND (@DriverId IS NULL OR p.DriverId = @DriverId)
      AND (@Status   IS NULL OR p.Status = @Status)
      AND (@Search   IS NULL OR d.OrderRef LIKE '%' + @Search + '%');
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
            p.Status, p.FailureReason, p.RecipientSignedName, p.SignatureUrl,
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

CREATE OR ALTER PROCEDURE dbo.usp_Dashboard_Summary
    @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status = 'Pending')   AS PendingCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status = 'Delivered') AS DeliveredCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status = 'Failed')    AS FailedCount,
        (SELECT COUNT(*) FROM dbo.Delivery WHERE CompanyId = @CompanyId AND Status = 'Pending' AND AssignedDriverId IS NULL) AS UnassignedCount;

    SELECT CAST(p.CapturedAtUtc AS DATE) AS Day,
           SUM(CASE WHEN p.Status = 'Delivered' THEN 1 ELSE 0 END) AS Delivered,
           SUM(CASE WHEN p.Status = 'Failed'    THEN 1 ELSE 0 END) AS Failed
    FROM   dbo.Proof    p
    JOIN   dbo.Delivery d ON d.Id = p.DeliveryId
    WHERE  d.CompanyId = @CompanyId
      AND  p.CapturedAtUtc >= DATEADD(DAY, -7, CAST(SYSUTCDATETIME() AS DATE))
    GROUP  BY CAST(p.CapturedAtUtc AS DATE)
    ORDER  BY Day;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_AppUser_ListDrivers
    @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Phone
    FROM   dbo.AppUser
    WHERE  CompanyId = @CompanyId AND Role = 'Driver' AND IsActive = 1
    ORDER  BY Name;
END
GO

/* Per-tenant config read. */
CREATE OR ALTER PROCEDURE dbo.usp_Company_GetSettings
    @CompanyId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CompanyId, DisplayName, PricingModel, FlatRate, PerKmRate, Currency,
           RequirePhoto, RequireSignature, LogoObjectKey
    FROM   dbo.CompanySettings
    WHERE  CompanyId = @CompanyId;
END
GO
