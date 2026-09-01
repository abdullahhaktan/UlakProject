using Ulak.Core.Abstractions;
using Ulak.Core.Domain;

namespace Ulak.Infrastructure.Persistence;

public sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly IDbConnectionFactory _factory;

    public DeliveryRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IReadOnlyList<DriverDelivery>> ListForDriverAsync(
        int companyId, int driverId, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<DriverDelivery>(
            "dbo.usp_Delivery_ListForDriver",
            new { CompanyId = companyId, DriverId = driverId },
            ct);
        return rows.ToList();
    }

    public async Task<Delivery?> GetByIdAsync(int companyId, int id, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<Delivery>(
            "dbo.usp_Delivery_GetById",
            new { CompanyId = companyId, Id = id },
            ct);
        return rows.SingleOrDefault();
    }

    public async Task<int> CreateAsync(CreateDeliveryInput input, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<decimal>(
            "dbo.usp_Delivery_Create",
            new
            {
                input.CompanyId,
                input.OrderRef,
                input.RecipientName,
                input.RecipientPhone,
                input.AddressText,
                input.Lat,
                input.Lng,
                input.Note,
                input.AssignedDriverId,
                input.CustomerName,
                input.AgreedPrice,
            },
            ct);
        return (int)rows.Single();
    }

    public async Task AssignAsync(int companyId, int deliveryId, int driverId, CancellationToken ct)
    {
        using var connection = _factory.Create();
        await connection.ExecuteProcAsync(
            "dbo.usp_Delivery_Assign",
            new { CompanyId = companyId, DeliveryId = deliveryId, DriverId = driverId },
            ct);
    }

    public async Task<PagedResult<AdminDeliveryRow>> AdminSearchAsync(
        int companyId, DeliverySearchQuery query, CancellationToken ct)
    {
        using var connection = _factory.Create();
        using var multi = await connection.QueryMultipleProcAsync(
            "dbo.usp_Delivery_AdminSearch",
            new
            {
                CompanyId = companyId,
                query.Search,
                query.Status,
                query.DriverId,
                FromDate = ToDate(query.FromDate),
                ToDate = ToDate(query.ToDate),
                query.SortColumn,
                query.SortDirection,
                query.Skip,
                query.Take,
            },
            ct);

        var items = (await multi.ReadAsync<AdminDeliveryRow>()).ToList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AdminDeliveryRow>(items, total);
    }

    private static DateTime? ToDate(DateOnly? value) =>
        value?.ToDateTime(TimeOnly.MinValue);
}
