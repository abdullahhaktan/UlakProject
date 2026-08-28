using System.Globalization;
using LinkLogistics.Api.Auth;
using LinkLogistics.Core.Abstractions;
using LinkLogistics.Core.Domain;
using LinkLogistics.Shared.Deliveries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkLogistics.Api.Controllers;

[ApiController]
[Route("deliveries")]
[Authorize]
public sealed class DeliveriesController : ControllerBase
{
    private readonly IDeliveryRepository _deliveries;
    private readonly ICurrentUser _currentUser;

    public DeliveriesController(IDeliveryRepository deliveries, ICurrentUser currentUser)
    {
        _deliveries = deliveries;
        _currentUser = currentUser;
    }

    /// <summary>The signed-in driver's deliveries for a day (defaults to today, UTC).</summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.Driver)]
    [ProducesResponseType<IReadOnlyList<DeliveryListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListForDriver([FromQuery] string? date, CancellationToken ct)
    {
        DateOnly? day = null;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return Problem(detail: "date must be in YYYY-MM-DD format.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            day = parsed;
        }

        var rows = await _deliveries.ListForDriverAsync(_currentUser.Id, day, ct);
        return Ok(rows.Select(ToListItem));
    }

    /// <summary>A single delivery. Drivers only see deliveries assigned to them.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<DeliveryDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var delivery = await _deliveries.GetByIdAsync(id, _currentUser.Id, _currentUser.Role, ct);
        return delivery is null ? NotFound() : Ok(ToDetail(delivery));
    }

    /// <summary>Ops creates a delivery.</summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.Ops)]
    [ProducesResponseType<DeliveryDetail>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryRequest request, CancellationToken ct)
    {
        var id = await _deliveries.CreateAsync(new CreateDeliveryInput(
            _currentUser.CompanyId,
            request.OrderRef.Trim(),
            request.RecipientName.Trim(),
            request.RecipientPhone,
            request.AddressText.Trim(),
            request.Lat,
            request.Lng,
            request.Note,
            request.AssignedDriverId), ct);

        var created = await _deliveries.GetByIdAsync(id, _currentUser.Id, _currentUser.Role, ct);
        return CreatedAtAction(nameof(GetById), new { id }, created is null ? null : ToDetail(created));
    }

    /// <summary>Ops assigns / reassigns a pending delivery to a driver.</summary>
    [HttpPatch("{id:int}/assign")]
    [Authorize(Roles = UserRoles.Ops)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignDeliveryRequest request, CancellationToken ct)
    {
        await _deliveries.AssignAsync(id, request.DriverId, ct);
        return NoContent();
    }

    private static DeliveryListItem ToListItem(DriverDelivery d) => new(
        d.Id, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
        d.Lat, d.Lng, d.Note, d.Status, d.CreatedAtUtc, d.HasProof);

    private static DeliveryDetail ToDetail(Delivery d) => new(
        d.Id, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
        d.Lat, d.Lng, d.Note, d.AssignedDriverId, d.AssignedDriverName, d.Status, d.CreatedAtUtc);
}
