using System.Data;
using Ulak.Core.Domain;

namespace Ulak.Core.Abstractions;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}

public interface IUserRepository
{
    Task<AppUserWithHash?> GetByPhoneAsync(string phone, CancellationToken ct);

    Task<IReadOnlyList<DriverLookup>> ListDriversAsync(CancellationToken ct);
}

public sealed record DriverLookup(int Id, string Name, string Phone);

public interface IRefreshTokenRepository
{
    Task StoreAsync(int userId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct);

    /// <summary>Returns the owning user only if the token is live (found, not revoked, not expired).</summary>
    Task<AppUser?> ValidateAsync(string tokenHash, CancellationToken ct);

    Task RevokeAsync(string tokenHash, CancellationToken ct);
}

public interface IDeliveryRepository
{
    Task<IReadOnlyList<DriverDelivery>> ListForDriverAsync(int driverId, DateOnly? date, CancellationToken ct);

    Task<Delivery?> GetByIdAsync(int id, int requestingUserId, string role, CancellationToken ct);

    Task<int> CreateAsync(CreateDeliveryInput input, CancellationToken ct);

    Task AssignAsync(int deliveryId, int driverId, CancellationToken ct);

    Task<PagedResult<AdminDeliveryRow>> AdminSearchAsync(DeliverySearchQuery query, CancellationToken ct);
}

public interface IProofRepository
{
    Task<ProofCreateResult> CreateAsync(NewProof proof, CancellationToken ct);

    Task<PagedResult<AdminProofRow>> AdminSearchAsync(ProofSearchQuery query, CancellationToken ct);

    Task<AdminProofDetail?> GetByIdAsync(long id, CancellationToken ct);
}

public interface IDashboardRepository
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct);
}

public sealed record CreateDeliveryInput(
    int CompanyId,
    string OrderRef,
    string RecipientName,
    string? RecipientPhone,
    string AddressText,
    decimal? Lat,
    decimal? Lng,
    string? Note,
    int? AssignedDriverId);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);

public sealed record DeliverySearchQuery(
    string? Search,
    string? Status,
    int? DriverId,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string SortColumn,
    string SortDirection,
    int Skip,
    int Take);

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

public sealed record ProofSearchQuery(
    DateOnly? FromDate,
    DateOnly? ToDate,
    int? DriverId,
    string? Status,
    string? Search,
    string SortColumn,
    string SortDirection,
    int Skip,
    int Take);

public sealed record AdminProofRow(
    long Id,
    int DeliveryId,
    string OrderRef,
    string RecipientName,
    string AddressText,
    string Status,
    string? FailureReason,
    string? RecipientSignedName,
    decimal? CapturedLat,
    decimal? CapturedLng,
    DateTime CapturedAtUtc,
    DateTime SyncedAtUtc,
    int DriverId,
    string DriverName,
    int PhotoCount);

public sealed record AdminProofDetail(
    long Id,
    int DeliveryId,
    string OrderRef,
    string RecipientName,
    string? RecipientPhone,
    string AddressText,
    decimal? DeliveryLat,
    decimal? DeliveryLng,
    string Status,
    string? FailureReason,
    string? RecipientSignedName,
    string? SignatureUrl,
    decimal? CapturedLat,
    decimal? CapturedLng,
    DateTime CapturedAtUtc,
    DateTime SyncedAtUtc,
    int DriverId,
    string DriverName,
    string DriverPhone,
    IReadOnlyList<ProofPhotoRow> Photos);

public sealed record ProofPhotoRow(long Id, string Url, int OrderIndex);

public sealed record DashboardSummary(
    int PendingCount,
    int DeliveredCount,
    int FailedCount,
    int UnassignedCount,
    IReadOnlyList<DashboardTrendPoint> Last7Days);

public sealed record DashboardTrendPoint(DateOnly Day, int Delivered, int Failed);
