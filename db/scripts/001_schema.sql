/* =====================================================================
   001_schema.sql  --  Proof of Delivery ("Teslimat Kaniti") schema.

   Drivers capture photo + signature + GPS + timestamp at delivery time;
   an ops panel views/exports the records.

   Run once by DbUp (journaled in dbo.SchemaVersions). IF NOT EXISTS
   guards keep the script safe to re-run against an existing database.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

/* ---------- Company (single tenant for the MVP) -------------------- */
IF OBJECT_ID(N'dbo.Company', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Company
    (
        Id           INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Company PRIMARY KEY,
        Name         NVARCHAR(200) NOT NULL,
        CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_Company_CreatedAtUtc DEFAULT SYSUTCDATETIME()
    );
END
GO

/* ---------- Users: drivers and ops staff -------------------------- */
IF OBJECT_ID(N'dbo.AppUser', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AppUser
    (
        Id           INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_AppUser PRIMARY KEY,
        CompanyId    INT           NOT NULL CONSTRAINT FK_AppUser_Company REFERENCES dbo.Company(Id),
        Phone        VARCHAR(20)   NOT NULL,
        PasswordHash VARCHAR(300)  NOT NULL,   -- pbkdf2$sha256$<iter>$<salt>$<hash>  (see PasswordHasher)
        Name         NVARCHAR(120) NOT NULL,
        Role         VARCHAR(10)   NOT NULL,   -- Driver | Ops
        IsActive     BIT           NOT NULL CONSTRAINT DF_AppUser_IsActive DEFAULT 1,
        CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_AppUser_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_AppUser_Phone UNIQUE (Phone),
        CONSTRAINT CK_AppUser_Role  CHECK (Role IN ('Driver','Ops'))
    );
END
GO

/* ---------- Refresh tokens (rotating) ----------------------------- */
IF OBJECT_ID(N'dbo.RefreshToken', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RefreshToken
    (
        Id           BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_RefreshToken PRIMARY KEY,
        UserId       INT           NOT NULL CONSTRAINT FK_RefreshToken_User REFERENCES dbo.AppUser(Id),
        TokenHash    CHAR(64)      NOT NULL,   -- SHA-256 hex of the opaque token
        ExpiresAtUtc DATETIME2(0)  NOT NULL,
        RevokedAtUtc DATETIME2(0)  NULL,
        CreatedAtUtc DATETIME2(0)  NOT NULL CONSTRAINT DF_RefreshToken_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_RefreshToken_Hash UNIQUE (TokenHash)
    );
    CREATE INDEX IX_RefreshToken_UserId ON dbo.RefreshToken(UserId);
END
GO

/* ---------- Deliveries ------------------------------------------- */
IF OBJECT_ID(N'dbo.Delivery', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Delivery
    (
        Id               INT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Delivery PRIMARY KEY,
        CompanyId        INT           NOT NULL CONSTRAINT FK_Delivery_Company REFERENCES dbo.Company(Id),
        OrderRef         VARCHAR(40)   NOT NULL,
        RecipientName    NVARCHAR(150) NOT NULL,
        RecipientPhone   VARCHAR(20)   NULL,
        AddressText      NVARCHAR(400) NOT NULL,
        Lat              DECIMAL(9,6)  NULL,
        Lng              DECIMAL(9,6)  NULL,
        Note             NVARCHAR(500) NULL,
        AssignedDriverId INT           NULL CONSTRAINT FK_Delivery_Driver REFERENCES dbo.AppUser(Id),
        Status           VARCHAR(10)   NOT NULL CONSTRAINT DF_Delivery_Status DEFAULT 'Pending', -- Pending|Delivered|Failed
        CreatedAtUtc     DATETIME2(0)  NOT NULL CONSTRAINT DF_Delivery_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_Delivery_OrderRef UNIQUE (CompanyId, OrderRef),
        CONSTRAINT CK_Delivery_Status   CHECK (Status IN ('Pending','Delivered','Failed'))
    );
    CREATE INDEX IX_Delivery_Driver_Created ON dbo.Delivery(AssignedDriverId, CreatedAtUtc);
END
GO

/* ---------- Proofs (one active per delivery) -------------------- */
IF OBJECT_ID(N'dbo.Proof', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Proof
    (
        Id                  BIGINT           IDENTITY(1,1) NOT NULL CONSTRAINT PK_Proof PRIMARY KEY,
        DeliveryId          INT              NOT NULL CONSTRAINT FK_Proof_Delivery REFERENCES dbo.Delivery(Id),
        DriverId            INT              NOT NULL CONSTRAINT FK_Proof_Driver   REFERENCES dbo.AppUser(Id),
        Status              VARCHAR(10)      NOT NULL,   -- Delivered | Failed
        FailureReason       NVARCHAR(300)    NULL,
        RecipientSignedName NVARCHAR(150)    NULL,
        SignatureUrl        NVARCHAR(400)    NULL,
        CapturedLat         DECIMAL(9,6)     NULL,
        CapturedLng         DECIMAL(9,6)     NULL,
        CapturedAtUtc       DATETIME2(0)     NOT NULL,
        SyncedAtUtc         DATETIME2(0)     NOT NULL CONSTRAINT DF_Proof_SyncedAtUtc DEFAULT SYSUTCDATETIME(),
        ClientUuid          UNIQUEIDENTIFIER NOT NULL,   -- idempotency key from the offline queue
        CONSTRAINT UQ_Proof_ClientUuid UNIQUE (ClientUuid),
        CONSTRAINT UQ_Proof_Delivery   UNIQUE (DeliveryId),   -- exactly one proof per delivery
        CONSTRAINT CK_Proof_Status     CHECK (Status IN ('Delivered','Failed')),
        CONSTRAINT CK_Proof_Failure    CHECK (Status <> 'Failed' OR FailureReason IS NOT NULL)
    );
    CREATE INDEX IX_Proof_CapturedAtUtc ON dbo.Proof(CapturedAtUtc);
    CREATE INDEX IX_Proof_DriverId      ON dbo.Proof(DriverId);
END
GO

/* ---------- Proof photos --------------------------------------- */
IF OBJECT_ID(N'dbo.ProofPhoto', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProofPhoto
    (
        Id         BIGINT        IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProofPhoto PRIMARY KEY,
        ProofId    BIGINT        NOT NULL CONSTRAINT FK_ProofPhoto_Proof
                                 REFERENCES dbo.Proof(Id) ON DELETE CASCADE,
        Url        NVARCHAR(400) NOT NULL,
        OrderIndex INT           NOT NULL CONSTRAINT DF_ProofPhoto_OrderIndex DEFAULT 0,
        CONSTRAINT CK_ProofPhoto_OrderIndex CHECK (OrderIndex BETWEEN 0 AND 4)   -- max 5 photos
    );
    CREATE INDEX IX_ProofPhoto_ProofId ON dbo.ProofPhoto(ProofId, OrderIndex);
END
GO
