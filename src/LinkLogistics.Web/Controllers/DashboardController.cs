using LinkLogistics.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkLogistics.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly LinkLogisticsApiClient _api;

    public DashboardController(LinkLogisticsApiClient api) => _api = api;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var summary = await _api.GetDashboardAsync(ct);
        return View(summary);
    }
}
