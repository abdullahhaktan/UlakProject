/* =====================================================================
   021_more_procedures.sql
   Added after 020 shipped: driver lookup for the ops panel's
   "assign to driver" control. New script (not an edit to 020) so DbUp
   applies it to databases that already ran 020.
   ===================================================================== */
GO

CREATE OR ALTER PROCEDURE dbo.usp_AppUser_ListDrivers
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Phone
    FROM   dbo.AppUser
    WHERE  Role = 'Driver' AND IsActive = 1
    ORDER  BY Name;
END
GO
