using Ulak.Shared.Admin;
using Ulak.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Web.Controllers;

[Authorize]
public sealed class DriversController : Controller
{
    private readonly UlakApiClient _api;

    public DriversController(UlakApiClient api) => _api = api;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var drivers = await _api.GetDriversAsync(ct) ?? [];
        return View(drivers);
    }

    /// <summary>JSON: create a driver, returns the one-time temp password.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] CreateDriverRequest request, CancellationToken ct)
    {
        try
        {
            var created = await _api.CreateDriverAsync(request, ct);
            return Json(new { ok = true, created.Id, created.Name, created.Phone, created.TempPassword });
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, new { ok = false, message = ex.Message });
        }
    }
}
