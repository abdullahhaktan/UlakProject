using LinkLogistics.Mobile.Models;
using LinkLogistics.Shared.Offline;
using LinkLogistics.Shared.Proofs;
using Microsoft.Extensions.Logging;

namespace LinkLogistics.Mobile.Services;

/// <summary>
/// Drains the offline proof queue to the API. Runs on a periodic timer and
/// whenever connectivity returns. Guarantees:
/// <list type="bullet">
///   <item>a proof row is deleted only after a confirmed POST /proofs;</item>
///   <item>a failed attempt keeps the row and its files, backs off exponentially,
///         and after <see cref="MaxAttempts"/> tries surfaces a visible error;</item>
///   <item>re-sending is safe — the API dedupes on ClientUuid.</item>
/// </list>
/// </summary>
public sealed class ProofSyncService
{
    public const int MaxAttempts = OfflineQueueRetryPolicy.MaxAttempts;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(20);

    private readonly LocalDatabase _db;
    private readonly ProofQueue _queue;
    private readonly ApiClient _api;
    private readonly IConnectivity _connectivity;
    private readonly ILogger<ProofSyncService> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);
    private CancellationTokenSource? _cts;

    public ProofSyncService(
        LocalDatabase db,
        ProofQueue queue,
        ApiClient api,
        IConnectivity connectivity,
        ILogger<ProofSyncService> logger)
    {
        _db = db;
        _queue = queue;
        _api = api;
        _connectivity = connectivity;
        _logger = logger;
    }

    public void Start()
    {
        if (_cts is not null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        _connectivity.ConnectivityChanged += OnConnectivityChanged;
        _queue.Changed += (_, _) => _ = RunOnceAsync();
        _ = LoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _connectivity.ConnectivityChanged -= OnConnectivityChanged;
        _cts?.Cancel();
        _cts = null;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        if (e.NetworkAccess == NetworkAccess.Internet)
        {
            _ = RunOnceAsync();
        }
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PollInterval);
        try
        {
            do
            {
                await RunOnceAsync();
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>
    /// Driver-triggered "send now": re-arms failed proofs and drains the queue
    /// even if <see cref="IConnectivity"/> is unsure about the network (its report
    /// is unreliable on some devices/emulators, and the driver just asked).
    /// </summary>
    public async Task RetryAllAsync()
    {
        await _db.RetryFailedAsync();
        _queue.NotifyChanged();
        await RunOnceAsync(force: true);
    }

    public async Task RunOnceAsync(bool force = false)
    {
        if (!force && _connectivity.NetworkAccess != NetworkAccess.Internet)
        {
            return;
        }

        if (!await _runLock.WaitAsync(0))
        {
            return; // already draining
        }

        try
        {
            var due = await _db.GetDueAsync(DateTimeOffset.UtcNow);
            _logger.LogInformation("Queue drain: {Count} proof(s) due (force={Force})", due.Count, force);
            foreach (var proof in due)
            {
                await ProcessAsync(proof);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Queue drain failed");
        }
        finally
        {
            _runLock.Release();
        }
    }

    private async Task ProcessAsync(PendingProof proof)
    {
        proof.State = SyncState.Syncing;
        await _db.UpdateProofAsync(proof);

        try
        {
            var photos = await _db.GetPhotosAsync(proof.Id);

            foreach (var photo in photos.Where(p => string.IsNullOrEmpty(p.RemoteKey)))
            {
                photo.RemoteKey = await UploadAsync(photo.LocalPath, "image/jpeg", "photo");
                await _db.UpdatePhotoAsync(photo);
            }

            if (!string.IsNullOrEmpty(proof.SignatureLocalPath) && string.IsNullOrEmpty(proof.SignatureRemoteKey))
            {
                proof.SignatureRemoteKey = await UploadAsync(proof.SignatureLocalPath, "image/png", "signature");
                await _db.UpdateProofAsync(proof);
            }

            var request = new CreateProofRequest(
                proof.ClientUuid,
                proof.DeliveryId,
                proof.Status,
                proof.FailureReason,
                proof.RecipientSignedName,
                proof.SignatureRemoteKey,
                photos.OrderBy(p => p.OrderIndex).Select(p => p.RemoteKey!).ToList(),
                proof.CapturedLat,
                proof.CapturedLng,
                proof.CapturedAt);

            var result = await _api.SubmitProofAsync(request, CancellationToken.None);
            _logger.LogInformation("Proof {ClientUuid} synced (duplicate={Dup})", proof.ClientUuid, result.WasDuplicate);

            DeleteLocalFiles(proof, photos);
            await _db.DeleteProofAsync(proof);
        }
        catch (ApiException apiEx) when (OfflineQueueRetryPolicy.IsPermanentFailure(apiEx.StatusCode))
        {
            // a permanent rejection — retrying won't help; surface it and stop
            proof.Attempts = MaxAttempts;
            proof.State = SyncState.Failed;
            proof.LastError = $"{apiEx.StatusCode}: {apiEx.Message}";
            await _db.UpdateProofAsync(proof);
            _logger.LogWarning("Proof {ClientUuid} permanently rejected: {Error}", proof.ClientUuid, proof.LastError);
        }
        catch (Exception ex)
        {
            proof.Attempts++;
            proof.LastError = ex.Message;

            if (OfflineQueueRetryPolicy.IsExhausted(proof.Attempts))
            {
                proof.State = SyncState.Failed;
                _logger.LogWarning("Proof {ClientUuid} failed after {Attempts} attempts", proof.ClientUuid, proof.Attempts);
            }
            else
            {
                proof.State = SyncState.Pending;
                proof.NextAttemptUtc = DateTimeOffset.UtcNow.Add(OfflineQueueRetryPolicy.Backoff(proof.Attempts));
            }

            await _db.UpdateProofAsync(proof);
        }
        finally
        {
            _queue.NotifyChanged();
        }
    }

    private async Task<string> UploadAsync(string localPath, string contentType, string kind)
    {
        var presign = await _api.PresignAsync(contentType, kind, CancellationToken.None);
        await using var stream = File.OpenRead(localPath);
        await _api.UploadAsync(presign.UploadUrl, stream, contentType, CancellationToken.None);
        return presign.ObjectKey;
    }

    private void DeleteLocalFiles(PendingProof proof, IEnumerable<PendingPhoto> photos)
    {
        try
        {
            foreach (var photo in photos)
            {
                if (File.Exists(photo.LocalPath))
                {
                    File.Delete(photo.LocalPath);
                }
            }

            if (!string.IsNullOrEmpty(proof.SignatureLocalPath) && File.Exists(proof.SignatureLocalPath))
            {
                File.Delete(proof.SignatureLocalPath);
            }

            var dir = Path.Combine(_db.FilesDirectory, proof.ClientUuid.ToString("N"));
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not clean local files for {ClientUuid}", proof.ClientUuid);
        }
    }
}
