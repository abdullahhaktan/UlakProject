using Ulak.Shared.Admin;
using Ulak.Web.Api;
using Ulak.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Web.Controllers;

[Authorize]
public sealed class DriversController : Controller
{
    private readonly UlakApiClient _api;

    public DriversController(UlakApiClient api) => _api = api;

    public async Task<IActionResult> Index(bool showInactive = false, CancellationToken ct = default)
    {
        var drivers = await _api.GetDriversAsync(showInactive, ct) ?? [];
        return View(new DriversViewModel { Drivers = drivers, ShowInactive = showInactive });
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateDriverRequest request, CancellationToken ct)
    {
        try
        {
            var driver = await _api.UpdateDriverAsync(id, request, ct);
            return Json(new { ok = true, driver.Id, driver.Name, driver.Phone, driver.IsActive });
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, new { ok = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool active, CancellationToken ct)
    {
        try
        {
            await _api.SetDriverActiveAsync(id, active, ct);
            return Json(new { ok = true });
        }
        catch (ApiException ex)
        {
            return StatusCode(ex.StatusCode, new { ok = false, message = ex.Message });
        }
    }
}
