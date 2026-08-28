namespace LinkLogistics.Shared.Offline;

/// <summary>
/// The retry rules for the mobile app's offline proof queue. Kept here (not
/// in the Android-only project) so it can be unit-tested directly.
/// </summary>
public static class OfflineQueueRetryPolicy
{
    /// <summary>After this many failed attempts the proof is surfaced as a visible error and no longer auto-retried.</summary>
    public const int MaxAttempts = 5;

    private const int MaxBackoffSeconds = 300;

    /// <summary>Exponential backoff before the next attempt: 2, 4, 8, 16, 32 s ... capped at 5 minutes.</summary>
    public static TimeSpan Backoff(int attempts)
    {
        var clamped = Math.Max(1, attempts);
        var seconds = Math.Min(MaxBackoffSeconds, Math.Pow(2, clamped));
        return TimeSpan.FromSeconds(seconds);
    }

    public static bool IsExhausted(int attempts) => attempts >= MaxAttempts;

    /// <summary>
    /// 4xx client errors (bad request / forbidden / conflict) will never
    /// succeed on retry, so the queue stops retrying and shows the error.
    /// </summary>
    public static bool IsPermanentFailure(int httpStatusCode) =>
        httpStatusCode is 400 or 403 or 409 or 422;
}
