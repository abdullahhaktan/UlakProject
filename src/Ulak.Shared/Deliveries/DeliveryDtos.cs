namespace Ulak.Shared.Deliveries;

/// <summary>A delivery as seen by the assigned driver on the mobile app.</summary>
public sealed record DeliveryListItem(
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
    bool HasProof);

public sealed record DeliveryDetail(
    int Id,
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

/// <summary>Ops creates a single delivery.</summary>
public sealed record CreateDeliveryRequest(
    string OrderRef,
    string RecipientName,
    string? RecipientPhone,
    string AddressText,
    decimal? Lat,
    decimal? Lng,
    string? Note,
    int? AssignedDriverId);

public sealed record AssignDeliveryRequest(int DriverId);
