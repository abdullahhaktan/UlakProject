using System.Data;
using Dapper;
using Ulak.Core.Abstractions;
using Ulak.Core.Domain;

namespace Ulak.Infrastructure.Persistence;

public sealed class ProofRepository : IProofRepository
{
    private readonly IDbConnectionFactory _factory;

    public ProofRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<ProofCreateResult> CreateAsync(int companyId, NewProof proof, CancellationToken ct)
    {
        using var connection = _factory.Create();

        var parameters = new DynamicParameters(new
        {
            CompanyId = companyId,
            proof.ClientUuid,
            proof.DeliveryId,
            proof.DriverId,
            proof.Status,
            proof.FailureReason,
            proof.RecipientSignedName,
            proof.SignatureUrl,
            proof.CapturedLat,
            proof.CapturedLng,
            proof.CapturedAtUtc,
        });
        parameters.Add("Photos", BuildPhotoTable(proof.Photos).AsTableValuedParameter("dbo.ProofPhotoType"));

        var row = await connection.QuerySingleAsync<ProofCreateResult>(new CommandDefinition(
            "dbo.usp_Proof_Create", parameters,
            commandType: CommandType.StoredProcedure, cancellationToken: ct));

        return row;
    }

    public async Task<PagedResult<AdminProofRow>> AdminSearchAsync(
        int companyId, ProofSearchQuery query, CancellationToken ct)
    {
        using var connection = _factory.Create();
        using var multi = await connection.QueryMultipleProcAsync(
            "dbo.usp_Admin_ProofSearch",
            new
            {
                CompanyId = companyId,
                FromDate = query.FromDate?.ToDateTime(TimeOnly.MinValue),
                ToDate = query.ToDate?.ToDateTime(TimeOnly.MinValue),
                query.DriverId,
                query.Status,
                query.Search,
                query.SortColumn,
                query.SortDirection,
                query.Skip,
                query.Take,
            },
            ct);

        var items = (await multi.ReadAsync<AdminProofRow>()).ToList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AdminProofRow>(items, total);
    }

    public async Task<AdminProofDetail?> GetByIdAsync(int companyId, long id, CancellationToken ct)
    {
        using var connection = _factory.Create();
        using var multi = await connection.QueryMultipleProcAsync(
            "dbo.usp_Admin_ProofGetById", new { CompanyId = companyId, Id = id }, ct);

        var header = await multi.ReadSingleOrDefaultAsync<ProofHeaderRow>();
        if (header is null)
        {
            return null;
        }

        var photos = (await multi.ReadAsync<ProofPhotoRow>()).ToList();

        return new AdminProofDetail(
            header.Id, header.DeliveryId, header.OrderRef, header.RecipientName, header.RecipientPhone,
            header.AddressText, header.DeliveryLat, header.DeliveryLng, header.Status, header.FailureReason,
            header.RecipientSignedName, header.SignatureUrl, header.CapturedLat, header.CapturedLng,
            header.CapturedAtUtc, header.SyncedAtUtc, header.DriverId, header.DriverName, header.DriverPhone,
            photos);
    }

    private static DataTable BuildPhotoTable(IReadOnlyList<ProofPhotoInput> photos)
    {
        var table = new DataTable();
        table.Columns.Add("Url", typeof(string));
        table.Columns.Add("OrderIndex", typeof(int));
        foreach (var photo in photos)
        {
            table.Rows.Add(photo.Url, photo.OrderIndex);
        }

        return table;
    }

    private sealed record ProofHeaderRow(
        long Id, int DeliveryId, string OrderRef, string RecipientName, string? RecipientPhone,
        string AddressText, decimal? DeliveryLat, decimal? DeliveryLng, string Status, string? FailureReason,
        string? RecipientSignedName, string? SignatureUrl, decimal? CapturedLat, decimal? CapturedLng,
        DateTime CapturedAtUtc, DateTime SyncedAtUtc, int DriverId, string DriverName, string DriverPhone);
}
