/* =====================================================================
   025_company_settings.sql  --  Make per-tenant capture rules real.

   - usp_Company_UpdateSettings: an Admin edits DisplayName + the two
     capture toggles (RequirePhoto / RequireSignature).
   - usp_Proof_Create: enforces those toggles. A photo/signature that the
     tenant marks required is now rejected server-side when missing
     (THROW 50028 / 50029). Skipped for a Failed proof (it carries its
     own required FailureReason); RequireSignature applies to Delivery
     proofs only (a pickup has no recipient to sign).

   New script (not an edit to 022/024) so DbUp applies it to existing DBs.
   ===================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.usp_Company_UpdateSettings
    @CompanyId        INT,
    @DisplayName      NVARCHAR(200),
    @RequirePhoto     BIT,
    @RequireSignature BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF @DisplayName IS NULL OR LTRIM(RTRIM(@DisplayName)) = ''
        THROW 50035, 'Display name is required.', 1;

    UPDATE dbo.CompanySettings
    SET    DisplayName       = @DisplayName,
           RequirePhoto      = @RequirePhoto,
           RequireSignature  = @RequireSignature
    WHERE  CompanyId = @CompanyId;

    IF @@ROWCOUNT = 0
        THROW 50034, 'Company settings not found.', 1;

    SELECT CompanyId, DisplayName, PricingModel, FlatRate, PerKmRate, Currency,
           RequirePhoto, RequireSignature, LogoObjectKey
    FROM   dbo.CompanySettings
    WHERE  CompanyId = @CompanyId;
END
GO

/* =====================================================================
   PROOF CAPTURE  --  024 version + per-tenant RequirePhoto/RequireSignature.
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

    /* ---- per-tenant capture requirements (a failed proof is exempt) ---- */
    IF @Status <> 'Failed'
    BEGIN
        DECLARE @requirePhoto BIT, @requireSignature BIT;
        SELECT @requirePhoto = RequirePhoto, @requireSignature = RequireSignature
        FROM   dbo.CompanySettings WHERE CompanyId = @CompanyId;

        IF @requirePhoto = 1 AND (SELECT COUNT(*) FROM @Photos) = 0
            THROW 50028, 'A photo is required for this company.', 1;

        IF @requireSignature = 1 AND @ProofType = 'Delivery'
           AND (@SignatureUrl IS NULL OR LTRIM(RTRIM(@SignatureUrl)) = '')
            THROW 50029, 'A signature is required for this company.', 1;
    END

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
