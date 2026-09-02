/* =====================================================================
   023_driver_admin.sql
   Panel: edit a driver (name / phone) and activate / deactivate them.
   A deactivated driver can't log in (usp_Auth already checks IsActive)
   and can't be assigned new deliveries (usp_Delivery_Assign checks it).
   New THROWs: 50032 duplicate phone, 50033 driver not found.
   ===================================================================== */

CREATE OR ALTER PROCEDURE dbo.usp_AppUser_ListDrivers
    @CompanyId       INT,
    @IncludeInactive BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT u.Id, u.Name, u.Phone, u.IsActive,
           OpenDeliveries = (SELECT COUNT(*) FROM dbo.Delivery d
                             WHERE d.AssignedDriverId = u.Id AND d.Status = 'Pending')
    FROM   dbo.AppUser u
    WHERE  u.CompanyId = @CompanyId AND u.Role = 'Driver'
      AND  (@IncludeInactive = 1 OR u.IsActive = 1)
    ORDER  BY u.IsActive DESC, u.Name;
END
GO

/* Edit a driver's name / phone (same company, role Driver only). */
CREATE OR ALTER PROCEDURE dbo.usp_AppUser_UpdateDriver
    @CompanyId INT,
    @Id        INT,
    @Name      NVARCHAR(120),
    @Phone     VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser
                   WHERE Id = @Id AND CompanyId = @CompanyId AND Role = 'Driver')
        THROW 50033, 'Surucu bulunamadi.', 1;

    IF EXISTS (SELECT 1 FROM dbo.AppUser WHERE Phone = @Phone AND Id <> @Id)
        THROW 50032, 'Bu telefon numarasi zaten kayitli.', 1;

    UPDATE dbo.AppUser SET Name = @Name, Phone = @Phone WHERE Id = @Id;

    SELECT Id, CompanyId, Phone, Name, Role, IsActive
    FROM   dbo.AppUser WHERE Id = @Id;
END
GO

/* Activate / deactivate a driver. */
CREATE OR ALTER PROCEDURE dbo.usp_AppUser_SetDriverActive
    @CompanyId INT,
    @Id        INT,
    @IsActive  BIT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.AppUser
                   WHERE Id = @Id AND CompanyId = @CompanyId AND Role = 'Driver')
        THROW 50033, 'Surucu bulunamadi.', 1;

    UPDATE dbo.AppUser SET IsActive = @IsActive WHERE Id = @Id;
END
GO
