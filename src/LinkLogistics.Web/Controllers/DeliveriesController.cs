using LinkLogistics.Shared.Deliveries;
using LinkLogistics.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkLogistics.Web.Controllers;

[Authorize]
public sealed class DeliveriesController : Controller
{
    private readonly LinkLogisticsApiClient _api;

    public DeliveriesController(LinkLogisticsApiClient api) => _api = api;

    public IActionResult Index() => View();

    /// <summary>JSON feed for the DevExtreme DataGrid CustomStore.</summary>
    [HttpGet]
    public async Task<IActionResult> Data(
        int skip = 0, int take = 20, string? sort = null, bool desc = true,
        string? search = null, string? status = null, int? driverId = null,
        string? from = null, string? to = null, CancellationToken ct = default)
    {
        var query = new GridQuery(
            Search: search, Status: status, DriverId: driverId, From: from, To: to,
            Sort: string.IsNullOrWhiteSpace(sort) ? "CreatedAtUtc" : sort,
            Dir: desc ? "DESC" : "ASC", Skip: skip, Take: take);

        var page = await _api.GetDeliveriesAsync(query, ct);
        return Json(new { data = page?.Items ?? [], totalCount = page?.TotalCount ?? 0 });
    }

    [HttpGet]
    public async Task<IActionResult> Drivers(CancellationToken ct) =>
        Json(await _api.GetDriversAsync(ct) ?? []);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryRequest request, CancellationToken ct)
    {
        try
        {
            await _api.CreateDeliveryAsync(request, ct);
            return Ok();
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignDeliveryRequest request, CancellationToken ct)
    {
        try
        {
            await _api.AssignDeliveryAsync(id, request.DriverId, ct);
            return Ok();
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
