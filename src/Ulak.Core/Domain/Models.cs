namespace Ulak.Core.Domain;

/// <summary>Authenticated principal loaded from the database (never carries the hash outside auth).</summary>
public sealed record AppUser(
    int Id,
    int CompanyId,
    string Phone,
    string Name,
    string Role,
    bool IsActive)
{
    public bool IsDriver => Role == UserRoles.Driver;
    public bool IsAdmin => Role == UserRoles.Admin;
}

/// <summary>Row returned by <c>usp_Auth_GetUserByPhone</c>, including the stored hash.</summary>
public sealed record AppUserWithHash(
    int Id,
    int CompanyId,
    string Phone,
    string PasswordHash,
    string Name,
    string Role,
    bool IsActive,
    bool MustChangePassword = false);

public sealed record Delivery(
    int Id,
    int CompanyId,
    string OrderRef,
    string RecipientName,
    string? RecipientPhone,
    string AddressText,
    decimal? Lat,
    decimal? Lng,
    string? Note,
    int? AssignedDriverId,
    string? AssignedDriverName,
    string Status,
    DateTime CreatedAtUtc);

public sealed record DriverDelivery(
    int Id,
    string OrderRef,
    string RecipientName,
    string? RecipientPhone,
    string AddressText,
    decimal? Lat,
    decimal? Lng,
    string? Note,
    string Status,
    DateTime CreatedAtUtc,
    bool HasProof,
    bool IsMine);

/// <summary>Per-tenant configuration (<c>usp_Company_GetSettings</c>).</summary>
public sealed record CompanySettings(
    int CompanyId,
    string DisplayName,
    string PricingModel,
    decimal? FlatRate,
    decimal? PerKmRate,
    string Currency,
    bool RequirePhoto,
    bool RequireSignature,
    string? LogoObjectKey);

public sealed record ProofPhotoInput(string Url, int OrderIndex);

public sealed record NewProof(
    Guid ClientUuid,
    int DeliveryId,
    int DriverId,
    string Status,
    string? FailureReason,
    string? RecipientSignedName,
    string? SignatureUrl,
    decimal? CapturedLat,
    decimal? CapturedLng,
    DateTime CapturedAtUtc,
    IReadOnlyList<ProofPhotoInput> Photos);

public sealed record ProofCreateResult(long Id, int DeliveryId, string Status, bool WasDuplicate);
