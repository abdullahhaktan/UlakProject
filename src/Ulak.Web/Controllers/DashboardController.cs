using Ulak.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly UlakApiClient _api;

    public DashboardController(UlakApiClient api) => _api = api;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var summary = await _api.GetDashboardAsync(ct);
        return View(summary);
    }
}
