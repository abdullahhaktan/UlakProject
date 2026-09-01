using Ulak.Api.Auth;
using Ulak.Core.Abstractions;
using Ulak.Infrastructure;
using Ulak.Shared.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ulak.Api.Controllers;

[ApiController]
[Route("me")]
[Authorize]
public sealed class MeController : ControllerBase
{
    private readonly ICompanyRepository _companies;
    private readonly IObjectStorage _storage;
    private readonly StorageOptions _storageOptions;
    private readonly ICurrentUser _currentUser;

    public MeController(
        ICompanyRepository companies, IObjectStorage storage,
        IOptions<StorageOptions> storageOptions, ICurrentUser currentUser)
    {
        _companies = companies;
        _storage = storage;
        _storageOptions = storageOptions.Value;
        _currentUser = currentUser;
    }

    /// <summary>Per-tenant config for the signed-in user's company.</summary>
    [HttpGet("config")]
    [ProducesResponseType<CompanyConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Config(CancellationToken ct)
    {
        var s = await _companies.GetSettingsAsync(_currentUser.CompanyId, ct);
        if (s is null)
        {
            return NotFound();
        }

        var logoUrl = string.IsNullOrEmpty(s.LogoObjectKey)
            ? null
            : _storage.CreateReadUrl(s.LogoObjectKey, TimeSpan.FromMinutes(_storageOptions.PresignTtlMinutes));

        return Ok(new CompanyConfigDto(
            s.CompanyId, s.DisplayName, s.PricingModel, s.FlatRate, s.PerKmRate,
            s.Currency, s.RequirePhoto, s.RequireSignature, logoUrl));
    }
}
