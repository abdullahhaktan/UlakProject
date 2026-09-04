using System.Globalization;
using System.Security.Cryptography;
using Ulak.Api.Auth;
using Ulak.Core.Abstractions;
using Ulak.Core.Domain;
using Ulak.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Dto = Ulak.Shared.Admin;

namespace Ulak.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = UserRoles.Admin)]
public sealed class AdminController : ControllerBase
{
    private static readonly string[] DeliverySortColumns =
        ["OrderRef", "RecipientName", "Status", "CreatedAtUtc"];

    private readonly IDeliveryRepository _deliveries;
    private readonly IUserRepository _users;
    private readonly IDashboardRepository _dashboard;
    private readonly ICompanyRepository _companies;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUser _currentUser;
    private readonly ISmsSender _sms;
    private readonly SmsOptions _smsOptions;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IDeliveryRepository deliveries, IUserRepository users, IDashboardRepository dashboard,
        ICompanyRepository companies, IPasswordHasher passwordHasher, ICurrentUser currentUser,
        ISmsSender sms, IOptions<SmsOptions> smsOptions, ILogger<AdminController> logger)
    {
        _deliveries = deliveries;
        _users = users;
        _dashboard = dashboard;
        _companies = companies;
        _passwordHasher = passwordHasher;
        _currentUser = currentUser;
        _sms = sms;
        _smsOptions = smsOptions.Value;
        _logger = logger;
    }

    [HttpGet("deliveries")]
    [ProducesResponseType<Dto.PagedResponse<Dto.AdminDeliveryRow>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Deliveries(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery(Name = "driver_id")] int? driverId,
        [FromQuery(Name = "from")] string? from,
        [FromQuery(Name = "to")] string? to,
        [FromQuery] string sort = "CreatedAtUtc",
        [FromQuery] string dir = "DESC",
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var query = new DeliverySearchQuery(
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            NormalizeDeliveryStatus(status),
            driverId,
            ParseDate(from),
            ParseDate(to),
            DeliverySortColumns.Contains(sort) ? sort : "CreatedAtUtc",
            dir.Equals("ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC",
            Math.Max(0, skip),
            Math.Clamp(take, 1, 200));

        var page = await _deliveries.AdminSearchAsync(_currentUser.CompanyId, query, ct);
        var items = page.Items.Select(r => new Dto.AdminDeliveryRow(
            r.Id, r.OrderRef, r.RecipientName, r.AddressText, r.Status, r.CreatedAtUtc,
            r.AssignedDriverId, r.AssignedDriverName, r.HasProof)).ToList();

        return Ok(new Dto.PagedResponse<Dto.AdminDeliveryRow>(items, page.TotalCount));
    }

    [HttpGet("drivers")]
    [ProducesResponseType<IReadOnlyList<Dto.DriverListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Drivers(
        [FromQuery(Name = "include_inactive")] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var drivers = await _users.ListDriversAsync(_currentUser.CompanyId, includeInactive, ct);
        return Ok(drivers.Select(d => new Dto.DriverListItem(d.Id, d.Name, d.Phone, d.IsActive, d.OpenDeliveries)));
    }

    /// <summary>Admin adds a driver to their own company. The temp password is returned once.</summary>
    [HttpPost("drivers")]
    [ProducesResponseType<Dto.CreateDriverResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateDriver([FromBody] Dto.CreateDriverRequest request, CancellationToken ct)
    {
        var tempPassword = GenerateTempPassword();
        var driver = await _users.CreateDriverAsync(
            _currentUser.CompanyId,
            request.Name.Trim(),
            Ulak.Shared.PhoneNumber.Normalize(request.Phone) ?? request.Phone.Trim(),
            _passwordHasher.Hash(tempPassword),
            ct);

        await SendInviteSmsAsync(driver.Phone, tempPassword, ct);

        return CreatedAtAction(nameof(Drivers), null,
            new Dto.CreateDriverResponse(driver.Id, driver.Name, driver.Phone, tempPassword));
    }

    /// <summary>Admin edits one of their drivers (name + phone).</summary>
    [HttpPut("drivers/{id:int}")]
    [ProducesResponseType<Dto.DriverListItem>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateDriver(int id, [FromBody] Dto.UpdateDriverRequest request, CancellationToken ct)
    {
        var driver = await _users.UpdateDriverAsync(
            _currentUser.CompanyId,
            id,
            request.Name.Trim(),
            Ulak.Shared.PhoneNumber.Normalize(request.Phone) ?? request.Phone.Trim(),
            ct);

        return Ok(new Dto.DriverListItem(driver.Id, driver.Name, driver.Phone, driver.IsActive, 0));
    }

    /// <summary>Admin activates / deactivates one of their drivers.</summary>
    [HttpPost("drivers/{id:int}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetDriverActive(int id, [FromBody] Dto.SetDriverActiveRequest request, CancellationToken ct)
    {
        await _users.SetDriverActiveAsync(_currentUser.CompanyId, id, request.IsActive, ct);
        return NoContent();
    }

    [HttpGet("settings")]
    [ProducesResponseType<Dto.CompanyConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
    {
        var s = await _companies.GetSettingsAsync(_currentUser.CompanyId, ct);
        return s is null ? NotFound() : Ok(ToConfigDto(s));
    }

    [HttpPut("settings")]
    [ProducesResponseType<Dto.CompanyConfigDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] Dto.UpdateCompanySettingsRequest request, CancellationToken ct)
    {
        var saved = await _companies.UpdateSettingsAsync(
            _currentUser.CompanyId, request.DisplayName.Trim(),
            request.RequirePhoto, request.RequireSignature, ct);
        return Ok(ToConfigDto(saved));
    }

    private static Dto.CompanyConfigDto ToConfigDto(Ulak.Core.Domain.CompanySettings s) =>
        new(s.CompanyId, s.DisplayName, s.PricingModel, s.FlatRate, s.PerKmRate,
            s.Currency, s.RequirePhoto, s.RequireSignature, LogoUrl: null);

    [HttpGet("dashboard")]
    [ProducesResponseType<Dto.DashboardSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var summary = await _dashboard.GetSummaryAsync(_currentUser.CompanyId, ct);
        return Ok(new Dto.DashboardSummaryDto(
            summary.PendingCount, summary.DeliveredCount, summary.FailedCount, summary.UnassignedCount,
            summary.Last7Days
                .Select(p => new Dto.DashboardTrendPointDto(
                    p.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.Delivered, p.Failed))
                .ToList()));
    }

    /// <summary>Best-effort invite SMS. A send failure never fails driver creation —
    /// the admin still gets the temp password in the response as a fallback.</summary>
    private async Task SendInviteSmsAsync(string phone, string tempPassword, CancellationToken ct)
    {
        var body =
            $"Ulak surucu hesabiniz olusturuldu. Gecici sifre: {tempPassword} " +
            $"Uygulama: {_smsOptions.AppDownloadUrl} Ilk giriste sifrenizi degistireceksiniz.";

        try
        {
            await _sms.SendAsync(phone, body, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Driver invite SMS to {Phone} failed", phone);
        }
    }

    private static string GenerateTempPassword()
    {
        // 8 chars, no ambiguous 0/O/1/l, readable to dictate over the phone.
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
        Span<char> chars = stackalloc char[8];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        }

        return new string(chars);
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static string? NormalizeDeliveryStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "pending" => DeliveryStatuses.Pending,
        "delivered" => DeliveryStatuses.Delivered,
        "failed" => DeliveryStatuses.Failed,
        _ => null,
    };
}
