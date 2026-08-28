using LinkLogistics.Shared.Offline;

namespace LinkLogistics.UnitTests;

public sealed class OfflineQueueRetryPolicyTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 8)]
    [InlineData(4, 16)]
    [InlineData(5, 32)]
    public void Backoff_grows_exponentially(int attempts, int expectedSeconds) =>
        OfflineQueueRetryPolicy.Backoff(attempts).TotalSeconds.ShouldBe(expectedSeconds);

    [Fact]
    public void Backoff_is_capped_at_five_minutes() =>
        OfflineQueueRetryPolicy.Backoff(20).ShouldBe(TimeSpan.FromMinutes(5));

    [Fact]
    public void Backoff_treats_zero_attempts_as_one() =>
        OfflineQueueRetryPolicy.Backoff(0).ShouldBe(TimeSpan.FromSeconds(2));

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void IsExhausted_after_five_attempts(int attempts, bool exhausted) =>
        OfflineQueueRetryPolicy.IsExhausted(attempts).ShouldBe(exhausted);

    [Theory]
    [InlineData(400, true)]
    [InlineData(403, true)]
    [InlineData(409, true)]
    [InlineData(422, true)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    [InlineData(408, false)]
    public void Only_client_errors_are_permanent(int status, bool permanent) =>
        OfflineQueueRetryPolicy.IsPermanentFailure(status).ShouldBe(permanent);
}
