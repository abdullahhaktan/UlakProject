using LinkLogistics.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkLogistics.Web.Controllers;

[Authorize]
public sealed class ProofsController : Controller
{
    private readonly LinkLogisticsApiClient _api;

    public ProofsController(LinkLogisticsApiClient api) => _api = api;

    public IActionResult Index() => View();

    public async Task<IActionResult> Detail(long id, CancellationToken ct)
    {
        var proof = await _api.GetProofAsync(id, ct);
        return proof is null ? NotFound() : View(proof);
    }

    [HttpGet]
    public async Task<IActionResult> Data(
        int skip = 0, int take = 20, string? sort = null, bool desc = true,
        string? search = null, string? status = null, int? driverId = null,
        string? from = null, string? to = null, CancellationToken ct = default)
    {
        var query = new GridQuery(
            Search: search, Status: status, DriverId: driverId, From: from, To: to,
            Sort: string.IsNullOrWhiteSpace(sort) ? "CapturedAtUtc" : sort,
            Dir: desc ? "DESC" : "ASC", Skip: skip, Take: take);

        var page = await _api.GetProofsAsync(query, ct);
        return Json(new { data = page?.Items ?? [], totalCount = page?.TotalCount ?? 0 });
    }

    [HttpGet]
    public async Task<IActionResult> Drivers(CancellationToken ct) =>
        Json(await _api.GetDriversAsync(ct) ?? []);

    [HttpGet]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        var pdf = await _api.GetProofPdfAsync(id, ct);
        return pdf is null ? NotFound() : File(pdf.Value.Bytes, pdf.Value.ContentType, $"proof-{id}.pdf");
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        string? search = null, string? status = null, int? driverId = null,
        string? from = null, string? to = null, CancellationToken ct = default)
    {
        var query = new GridQuery(
            Search: search, Status: status, DriverId: driverId, From: from, To: to,
            Sort: "CapturedAtUtc", Dir: "DESC", Skip: 0, Take: 5000);

        var bytes = await _api.GetProofsExcelAsync(query, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"proofs-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }
}
