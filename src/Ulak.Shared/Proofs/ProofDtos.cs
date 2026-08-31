namespace Ulak.Shared.Proofs;

/// <summary>
/// Proof of delivery submitted by a driver. Photos and the signature are
/// uploaded to object storage first (via <c>/uploads/presign</c>); only
/// their keys/URLs are sent here. <see cref="ClientUuid"/> makes the call
/// idempotent — the offline queue may POST the same proof more than once.
/// </summary>
public sealed record CreateProofRequest(
    Guid ClientUuid,
    int DeliveryId,
    string Status,               // Delivered | Failed
    string? FailureReason,
    string? RecipientSignedName,
    string? SignatureUrl,
    IReadOnlyList<string> PhotoUrls,
    decimal? CapturedLat,
    decimal? CapturedLng,
    DateTimeOffset CapturedAt);

public sealed record CreateProofResponse(
    long Id,
    int DeliveryId,
    string Status,
    bool WasDuplicate);

// --- ops panel views ---

public sealed record ProofListItem(
    long Id,
    int DeliveryId,
    string OrderRef,
    string RecipientName,
    string Status,
    string? FailureReason,
    int DriverId,
    string DriverName,
    int PhotoCount,
    DateTime CapturedAtUtc,
    DateTime SyncedAtUtc);

public sealed record ProofDetail(
    long Id,
    int DeliveryId,
    string OrderRef,
    string RecipientName,
    string? RecipientPhone,
    string AddressText,
    decimal? DeliveryLat,
    decimal? DeliveryLng,
    string Status,
    string? FailureReason,
    string? RecipientSignedName,
    string? SignatureUrl,
    decimal? CapturedLat,
    decimal? CapturedLng,
    DateTime CapturedAtUtc,
    DateTime SyncedAtUtc,
    int DriverId,
    string DriverName,
    string DriverPhone,
    IReadOnlyList<ProofPhotoDto> Photos);

public sealed record ProofPhotoDto(long Id, string Url, int OrderIndex);
