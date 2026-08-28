namespace LinkLogistics.Shared.Admin;

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record AdminDeliveryRow(
    int Id,
    string OrderRef,
    string RecipientName,
    string AddressText,
    string Status,
    DateTime CreatedAtUtc,
    int? AssignedDriverId,
    string? AssignedDriverName,
    bool HasProof);

public sealed record DriverOption(int Id, string Name, string Phone);

public sealed record DashboardSummaryDto(
    int PendingCount,
    int DeliveredCount,
    int FailedCount,
    int UnassignedCount,
    IReadOnlyList<DashboardTrendPointDto> Last7Days);

public sealed record DashboardTrendPointDto(string Day, int Delivered, int Failed);
