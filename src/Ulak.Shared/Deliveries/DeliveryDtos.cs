namespace Ulak.Shared.Deliveries;

/// <summary>A delivery as seen by a driver on the mobile app. The list covers the
/// whole company; <see cref="IsMine"/> marks the ones assigned to the caller.</summary>
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
    bool HasProof,
    bool HasPickupProof,
    bool IsMine);

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

/// <summary>Admin creates a single delivery.</summary>
public sealed record CreateDeliveryRequest(
    string OrderRef,
    string RecipientName,
    string? RecipientPhone,
    string AddressText,
    decimal? Lat,
    decimal? Lng,
    string? Note,
    int? AssignedDriverId,
    string? CustomerName = null,
    decimal? AgreedPrice = null);

public sealed record AssignDeliveryRequest(int DriverId);
