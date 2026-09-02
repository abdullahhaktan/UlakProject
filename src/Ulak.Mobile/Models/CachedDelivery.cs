using SQLite;
using Ulak.Shared.Deliveries;

namespace Ulak.Mobile.Models;

/// <summary>
/// A delivery mirrored to the device from the last successful list fetch, so
/// the working list (and a delivery's detail, for proof capture) still opens
/// when the driver is offline. The whole set is replaced on every fetch;
/// <see cref="CachedAtUtc"/> is the same for every row and drives the
/// "last updated HH:mm" note.
/// </summary>
[Table("CachedDelivery")]
public sealed class CachedDelivery
{
    [PrimaryKey]
    public int Id { get; set; }

    public string OrderRef { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientPhone { get; set; }
    public string AddressText { get; set; } = string.Empty;
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; }
    public bool HasProof { get; set; }
    public bool IsMine { get; set; }
    public DateTimeOffset CachedAtUtc { get; set; }

    public static CachedDelivery From(DeliveryListItem d, DateTimeOffset cachedAt) => new()
    {
        Id = d.Id,
        OrderRef = d.OrderRef,
        RecipientName = d.RecipientName,
        RecipientPhone = d.RecipientPhone,
        AddressText = d.AddressText,
        Lat = (double?)d.Lat,
        Lng = (double?)d.Lng,
        Note = d.Note,
        Status = d.Status,
        CreatedAtUtc = d.CreatedAtUtc,
        HasProof = d.HasProof,
        IsMine = d.IsMine,
        CachedAtUtc = cachedAt,
    };

    public DeliveryListItem ToListItem() => new(
        Id, OrderRef, RecipientName, RecipientPhone, AddressText,
        (decimal?)Lat, (decimal?)Lng, Note, Status, CreatedAtUtc, HasProof, IsMine);

    public DeliveryDetail ToDetail() => new(
        Id, OrderRef, RecipientName, RecipientPhone, AddressText,
        (decimal?)Lat, (decimal?)Lng, Note, null, null, Status, CreatedAtUtc);
}
