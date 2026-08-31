using Ulak.Core.Abstractions;

namespace Ulak.Infrastructure.Persistence;

public sealed class DashboardRepository : IDashboardRepository
{
    private readonly IDbConnectionFactory _factory;

    public DashboardRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct)
    {
        using var connection = _factory.Create();
        using var multi = await connection.QueryMultipleProcAsync("dbo.usp_Dashboard_Summary", null, ct);

        var counts = await multi.ReadSingleAsync<CountsRow>();
        var trend = (await multi.ReadAsync<TrendRow>())
            .Select(r => new DashboardTrendPoint(DateOnly.FromDateTime(r.Day), r.Delivered, r.Failed))
            .ToList();

        return new DashboardSummary(
            counts.PendingCount, counts.DeliveredCount, counts.FailedCount, counts.UnassignedCount, trend);
    }

    private sealed record CountsRow(int PendingCount, int DeliveredCount, int FailedCount, int UnassignedCount);

    private sealed record TrendRow(DateTime Day, int Delivered, int Failed);
}
