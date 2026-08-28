using SQLite;

namespace LinkLogistics.Mobile.Models;

public enum SyncState
{
    Pending = 0,
    Syncing = 1,
    Synced = 2,
    Failed = 3,   // exhausted retries; visible to the driver, never deleted
}

/// <summary>
/// A proof captured on the device and waiting to reach the API. Rows are
/// removed only after a confirmed sync; a failed sync keeps the row and its
/// local files so nothing is ever lost silently.
/// </summary>
[Table("PendingProof")]
public sealed class PendingProof
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed, Unique]
    public Guid ClientUuid { get; set; }

    public int DeliveryId { get; set; }

    public string OrderRef { get; set; } = string.Empty;

    public string Status { get; set; } = "Delivered";

    public string? FailureReason { get; set; }

    public string? RecipientSignedName { get; set; }

    public decimal? CapturedLat { get; set; }

    public decimal? CapturedLng { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    public string? SignatureLocalPath { get; set; }

    public string? SignatureRemoteKey { get; set; }

    public SyncState State { get; set; } = SyncState.Pending;

    public int Attempts { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset NextAttemptUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("PendingPhoto")]
public sealed class PendingPhoto
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int PendingProofId { get; set; }

    public string LocalPath { get; set; } = string.Empty;

    public int OrderIndex { get; set; }

    public string? RemoteKey { get; set; }
}
