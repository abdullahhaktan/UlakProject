using LinkLogistics.Mobile.Models;

namespace LinkLogistics.Mobile.Services;

/// <summary>
/// The write side of the offline queue. Persisting a proof always means:
/// write the photo/signature bytes to disk, then insert the rows. The
/// driver is told "kaydedildi" the moment this returns; delivery to the
/// API is the sync service's job.
/// </summary>
public sealed class ProofQueue
{
    private readonly LocalDatabase _db;

    public ProofQueue(LocalDatabase db) => _db = db;

    /// <summary>Raised whenever the queue size changes (enqueue or a sync outcome).</summary>
    public event EventHandler? Changed;

    public void NotifyChanged() => Changed?.Invoke(this, EventArgs.Empty);

    public Task<int> GetPendingCountAsync() => _db.CountUnsyncedAsync();

    public Task<int> GetFailedCountAsync() => _db.CountByStateAsync(SyncState.Failed);

    public async Task EnqueueAsync(PendingProof proof, IReadOnlyList<byte[]> photos, byte[]? signature)
    {
        if (signature is not null)
        {
            proof.SignatureLocalPath = await WriteFileAsync(proof.ClientUuid, "signature.png", signature);
        }

        var proofId = await _db.InsertProofAsync(proof);

        for (var i = 0; i < photos.Count; i++)
        {
            var path = await WriteFileAsync(proof.ClientUuid, $"photo-{i}.jpg", photos[i]);
            await _db.InsertPhotoAsync(new PendingPhoto
            {
                PendingProofId = proofId,
                LocalPath = path,
                OrderIndex = i,
            });
        }

        NotifyChanged();
    }

    private async Task<string> WriteFileAsync(Guid clientUuid, string name, byte[] bytes)
    {
        var dir = Path.Combine(_db.FilesDirectory, clientUuid.ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }
}
