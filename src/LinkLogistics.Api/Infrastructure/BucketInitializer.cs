using LinkLogistics.Core.Abstractions;

namespace LinkLogistics.Api.Infrastructure;

/// <summary>
/// Creates the object-storage bucket on startup. Best effort: it never
/// throws out of <see cref="StartAsync"/> so a slow/absent MinIO cannot
/// stop the API from starting.
/// </summary>
public sealed class BucketInitializer : IHostedService
{
    private readonly IObjectStorage _storage;
    private readonly ILogger<BucketInitializer> _logger;

    public BucketInitializer(IObjectStorage storage, ILogger<BucketInitializer> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // fire and forget: don't block or break host startup
        _ = Task.Run(() => InitialiseAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitialiseAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            try
            {
                await _storage.EnsureBucketAsync(ct);
                _logger.LogInformation("Object storage bucket is ready.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Object storage not ready (attempt {Attempt}/10): {Message}", attempt, ex.Message);
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(3), ct);
                }
                catch (TaskCanceledException)
                {
                    return;
                }
            }
        }

        _logger.LogError("Object storage bucket could not be initialised; uploads will fail until it is.");
    }
}
