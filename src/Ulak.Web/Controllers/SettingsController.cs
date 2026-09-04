using Ulak.Shared.Admin;
using Ulak.Web.Api;
using Ulak.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Web.Controllers;

[Authorize]
public sealed class SettingsController : Controller
{
    private readonly UlakApiClient _api;

    public SettingsController(UlakApiClient api) => _api = api;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var config = await _api.GetSettingsAsync(ct);
        return View(ToViewModel(config));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var saved = await _api.UpdateSettingsAsync(
                new UpdateCompanySettingsRequest(
                    model.DisplayName.Trim(), model.RequirePhoto, model.RequireSignature),
                ct);

            var vm = ToViewModel(saved);
            vm.Saved = true;
            return View(vm);
        }
        catch (ApiException ex)
        {
            model.Error = ex.Message;
            return View(model);
        }
    }

    private static SettingsViewModel ToViewModel(CompanyConfigDto? config) => new()
    {
        DisplayName = config?.DisplayName ?? string.Empty,
        RequirePhoto = config?.RequirePhoto ?? true,
        RequireSignature = config?.RequireSignature ?? false,
    };
}
