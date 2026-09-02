namespace Ulak.Shared.Admin;

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

/// <summary>A driver as shown on the panel's Drivers page.</summary>
public sealed record DriverListItem(int Id, string Name, string Phone, bool IsActive, int OpenDeliveries);

public sealed record CreateDriverRequest(string Name, string Phone);

public sealed record UpdateDriverRequest(string Name, string Phone);

public sealed record SetDriverActiveRequest(bool IsActive);

/// <summary>The temp password is returned exactly once; the driver must change it on first login.</summary>
public sealed record CreateDriverResponse(int Id, string Name, string Phone, string TempPassword);

public sealed record CompanyConfigDto(
    int CompanyId,
    string DisplayName,
    string PricingModel,
    decimal? FlatRate,
    decimal? PerKmRate,
    string Currency,
    bool RequirePhoto,
    bool RequireSignature,
    string? LogoUrl);

public sealed record DashboardSummaryDto(
    int PendingCount,
    int DeliveredCount,
    int FailedCount,
    int UnassignedCount,
    IReadOnlyList<DashboardTrendPointDto> Last7Days);

public sealed record DashboardTrendPointDto(string Day, int Delivered, int Failed);
