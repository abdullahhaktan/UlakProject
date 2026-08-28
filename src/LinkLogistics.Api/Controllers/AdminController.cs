using System.Globalization;
using LinkLogistics.Core.Abstractions;
using LinkLogistics.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dto = LinkLogistics.Shared.Admin;

namespace LinkLogistics.Api.Controllers;

[ApiController]
[Route("admin")]
[Authorize(Roles = UserRoles.Ops)]
public sealed class AdminController : ControllerBase
{
    private static readonly string[] DeliverySortColumns =
        ["OrderRef", "RecipientName", "Status", "CreatedAtUtc"];

    private readonly IDeliveryRepository _deliveries;
    private readonly IUserRepository _users;
    private readonly IDashboardRepository _dashboard;

    public AdminController(
        IDeliveryRepository deliveries, IUserRepository users, IDashboardRepository dashboard)
    {
        _deliveries = deliveries;
        _users = users;
        _dashboard = dashboard;
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

        var page = await _deliveries.AdminSearchAsync(query, ct);
        var items = page.Items.Select(r => new Dto.AdminDeliveryRow(
            r.Id, r.OrderRef, r.RecipientName, r.AddressText, r.Status, r.CreatedAtUtc,
            r.AssignedDriverId, r.AssignedDriverName, r.HasProof)).ToList();

        return Ok(new Dto.PagedResponse<Dto.AdminDeliveryRow>(items, page.TotalCount));
    }

    [HttpGet("drivers")]
    [ProducesResponseType<IReadOnlyList<Dto.DriverOption>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Drivers(CancellationToken ct)
    {
        var drivers = await _users.ListDriversAsync(ct);
        return Ok(drivers.Select(d => new Dto.DriverOption(d.Id, d.Name, d.Phone)));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType<Dto.DashboardSummaryDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var summary = await _dashboard.GetSummaryAsync(ct);
        return Ok(new Dto.DashboardSummaryDto(
            summary.PendingCount, summary.DeliveredCount, summary.FailedCount, summary.UnassignedCount,
            summary.Last7Days
                .Select(p => new Dto.DashboardTrendPointDto(
                    p.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.Delivered, p.Failed))
                .ToList()));
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
