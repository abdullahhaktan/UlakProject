using Ulak.Mobile.Models;
using SQLite;

namespace Ulak.Mobile.Services;

/// <summary>The on-device SQLite store that backs the offline proof queue.</summary>
public sealed class LocalDatabase
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private SQLiteAsyncConnection? _connection;

    public string FilesDirectory { get; } =
        Path.Combine(FileSystem.AppDataDirectory, "proof-files");

    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_connection is null)
            {
                Directory.CreateDirectory(FilesDirectory);
                var path = Path.Combine(FileSystem.AppDataDirectory, "ulak.db3");
                var connection = new SQLiteAsyncConnection(path,
                    SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
                await connection.CreateTableAsync<PendingProof>();
                await connection.CreateTableAsync<PendingPhoto>();
                await connection.CreateTableAsync<CachedDelivery>();

                // Publish the connection only once every table exists. Assigning
                // _connection earlier lets a concurrent caller (the sync loop)
                // take it via the fast path above and hit "no such table".
                _connection = connection;
            }
        }
        finally
        {
            _initLock.Release();
        }

        return _connection;
    }

    public async Task<int> InsertProofAsync(PendingProof proof)
    {
        var db = await GetConnectionAsync();
        await db.InsertAsync(proof);
        return proof.Id;
    }

    public async Task InsertPhotoAsync(PendingPhoto photo)
    {
        var db = await GetConnectionAsync();
        await db.InsertAsync(photo);
    }

    public async Task UpdateProofAsync(PendingProof proof)
    {
        var db = await GetConnectionAsync();
        await db.UpdateAsync(proof);
    }

    public async Task UpdatePhotoAsync(PendingPhoto photo)
    {
        var db = await GetConnectionAsync();
        await db.UpdateAsync(photo);
    }

    public async Task<List<PendingProof>> GetDueAsync(DateTimeOffset now)
    {
        var db = await GetConnectionAsync();
        return await db.Table<PendingProof>()
            .Where(p => (p.State == SyncState.Pending || p.State == SyncState.Syncing) && p.NextAttemptUtc <= now)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<PendingPhoto>> GetPhotosAsync(int pendingProofId)
    {
        var db = await GetConnectionAsync();
        return await db.Table<PendingPhoto>()
            .Where(p => p.PendingProofId == pendingProofId)
            .OrderBy(p => p.OrderIndex)
            .ToListAsync();
    }

    public async Task<int> CountUnsyncedAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<PendingProof>().Where(p => p.State != SyncState.Synced).CountAsync();
    }

    public async Task<int> CountByStateAsync(SyncState state)
    {
        var db = await GetConnectionAsync();
        return await db.Table<PendingProof>().Where(p => p.State == state).CountAsync();
    }

    /// <summary>
    /// Re-arms every <see cref="SyncState.Failed"/> proof for another sync pass:
    /// back to Pending, attempts cleared, due now. Used by the manual "send now"
    /// action so a driver can retry uploads that exhausted their automatic retries.
    /// </summary>
    public async Task<int> RetryFailedAsync()
    {
        var db = await GetConnectionAsync();
        var failed = await db.Table<PendingProof>().Where(p => p.State == SyncState.Failed).ToListAsync();
        foreach (var proof in failed)
        {
            proof.State = SyncState.Pending;
            proof.Attempts = 0;
            proof.LastError = null;
            proof.NextAttemptUtc = DateTimeOffset.UtcNow;
            await db.UpdateAsync(proof);
        }

        return failed.Count;
    }

    // --- offline delivery cache ---

    /// <summary>Replaces the whole cached list with the rows from the latest fetch.</summary>
    public async Task ReplaceCachedDeliveriesAsync(IReadOnlyList<CachedDelivery> deliveries)
    {
        var db = await GetConnectionAsync();
        await db.RunInTransactionAsync(tx =>
        {
            tx.DeleteAll<CachedDelivery>();
            if (deliveries.Count > 0)
            {
                tx.InsertAll(deliveries);
            }
        });
    }

    public async Task<List<CachedDelivery>> GetCachedDeliveriesAsync()
    {
        var db = await GetConnectionAsync();
        return await db.Table<CachedDelivery>().OrderByDescending(d => d.CreatedAtUtc).ToListAsync();
    }

    public async Task<CachedDelivery?> GetCachedDeliveryAsync(int id)
    {
        var db = await GetConnectionAsync();
        return await db.Table<CachedDelivery>().Where(d => d.Id == id).FirstOrDefaultAsync();
    }

    public async Task<DateTimeOffset?> GetCacheTimestampAsync()
    {
        var db = await GetConnectionAsync();
        var row = await db.Table<CachedDelivery>().FirstOrDefaultAsync();
        return row?.CachedAtUtc;
    }

    public async Task DeleteProofAsync(PendingProof proof)
    {
        var db = await GetConnectionAsync();
        var photos = await GetPhotosAsync(proof.Id);
        foreach (var photo in photos)
        {
            await db.DeleteAsync(photo);
        }

        await db.DeleteAsync(proof);
    }
}
