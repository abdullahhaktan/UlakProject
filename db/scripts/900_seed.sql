/* =====================================================================
   900_seed.sql  --  Demo data: 1 company, 2 drivers + 1 ops user,
   10 deliveries with mixed status / assignment.

   Credentials (phone / password):
     Ops    :  +905550001122 / Ops12345!
     Driver1:  +905551112233 / Driver123!
     Driver2:  +905554445566 / Driver123!

   Password hashes are PBKDF2-SHA256, 100000 iterations, produced by the
   same scheme as Infrastructure/Security/PasswordHasher.cs.
   Idempotent so it can also be applied to an already-seeded database.
   ===================================================================== */
SET NOCOUNT ON;
GO

DECLARE @companyId INT;

IF NOT EXISTS (SELECT 1 FROM dbo.Company WHERE Name = N'Ulak Demo')
    INSERT dbo.Company (Name) VALUES (N'Ulak Demo');

SELECT @companyId = Id FROM dbo.Company WHERE Name = N'Ulak Demo';

/* ---- Users ---- */
MERGE dbo.AppUser AS tgt
USING (VALUES
    (@companyId, '+905550001122', N'Operasyon Kullanicisi',
        'pbkdf2$sha256$100000$Qk7QyCH6WD397x2NC32ghg==$GcER5iRA5QR9YYm5CovUUY1n1bZBhj4h0SN0mlkNhOw=', 'Ops'),
    (@companyId, '+905551112233', N'Ahmet Yilmaz (Surucu)',
        'pbkdf2$sha256$100000$uvzObMCS0ayoIZm2Tb/3TQ==$SZf+vrKU90Oc6FFO3LYY4bH18cbL0ee877jGst8ccx0=', 'Driver'),
    (@companyId, '+905554445566', N'Mehmet Demir (Surucu)',
        'pbkdf2$sha256$100000$qYL2al5BzBezIuBLGvpf4w==$di6zxZ7frcCj+cxfOaRTNAQyTz0zPxqI9YX6rSjwVnM=', 'Driver')
) AS src (CompanyId, Phone, Name, PasswordHash, Role)
   ON tgt.Phone = src.Phone
WHEN NOT MATCHED THEN
    INSERT (CompanyId, Phone, Name, PasswordHash, Role)
    VALUES (src.CompanyId, src.Phone, src.Name, src.PasswordHash, src.Role);

DECLARE @driver1 INT = (SELECT Id FROM dbo.AppUser WHERE Phone = '+905551112233');
DECLARE @driver2 INT = (SELECT Id FROM dbo.AppUser WHERE Phone = '+905554445566');

/* ---- Deliveries (idempotent on OrderRef) ---- */
MERGE dbo.Delivery AS tgt
USING (VALUES
    ('ORD-24001', N'Ayse Kaya',       '+905321110001', N'Bagdat Cad. No:12 D:4, Kadikoy/Istanbul',        40.982100, 29.062700, N'Kapida odeme yok',            @driver1, 'Pending'),
    ('ORD-24002', N'Can Ozturk',      '+905321110002', N'Barbaros Bulvari No:45, Besiktas/Istanbul',        41.045800, 29.007300, NULL,                          @driver1, 'Pending'),
    ('ORD-24003', N'Elif Sahin',      '+905321110003', N'Nispetiye Cad. No:8 D:2, Etiler/Istanbul',         41.081200, 29.032900, N'Zili calmayin, arayin',      @driver1, 'Pending'),
    ('ORD-24004', N'Burak Aydin',     '+905321110004', N'Istiklal Cad. No:100, Beyoglu/Istanbul',           41.033500, 28.977600, NULL,                          @driver2, 'Pending'),
    ('ORD-24005', N'Zeynep Arslan',   '+905321110005', N'Halaskargazi Cad. No:210, Sisli/Istanbul',         41.055900, 28.987400, N'Ofis girisi arkada',         @driver2, 'Pending'),
    ('ORD-24006', N'Deniz Yildiz',    '+905321110006', N'Feneryolu Mah. Sair Nesimi Sok. No:3, Kadikoy',    40.987700, 29.049800, NULL,                          @driver2, 'Pending'),
    ('ORD-24007', N'Kemal Celik',     '+905321110007', N'Ataturk Mah. Ertugrul Gazi Sok. No:20, Atasehir',  40.984500, 29.107800, N'Site 4, blok B',            NULL,     'Pending'),
    ('ORD-24008', N'Selin Kurt',      '+905321110008', N'Caferaga Mah. Moda Cad. No:55, Kadikoy/Istanbul',  40.985300, 29.026100, NULL,                          NULL,     'Pending'),
    ('ORD-24009', N'Onur Guler',      '+905321110009', N'Kozyatagi Mah. Bayar Cad. No:78, Kadikoy',         40.975600, 29.093400, N'Kargo dolabina birakilabilir', @driver1, 'Pending'),
    ('ORD-24010', N'Merve Dogan',     '+905321110010', N'Acibadem Mah. Cecen Sok. No:11, Uskudar/Istanbul', 41.001200, 29.043700, NULL,                          @driver2, 'Pending')
) AS src (OrderRef, RecipientName, RecipientPhone, AddressText, Lat, Lng, Note, AssignedDriverId, Status)
   ON tgt.CompanyId = @companyId AND tgt.OrderRef = src.OrderRef
WHEN NOT MATCHED THEN
    INSERT (CompanyId, OrderRef, RecipientName, RecipientPhone, AddressText, Lat, Lng, Note, AssignedDriverId, Status)
    VALUES (@companyId, src.OrderRef, src.RecipientName, src.RecipientPhone, src.AddressText,
            src.Lat, src.Lng, src.Note, src.AssignedDriverId, src.Status);
GO
