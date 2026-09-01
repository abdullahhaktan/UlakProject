using Ulak.Api.Auth;
using Ulak.Core.Abstractions;
using Ulak.Core.Domain;
using Ulak.Shared.Deliveries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Api.Controllers;

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

    /// <summary>
    /// The signed-in driver's working list: the whole company's open deliveries,
    /// each flagged <c>isMine</c>. Teammates' rows are read-only.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = UserRoles.Driver)]
    [ProducesResponseType<IReadOnlyList<DeliveryListItem>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListForDriver(CancellationToken ct)
    {
        var rows = await _deliveries.ListForDriverAsync(_currentUser.CompanyId, _currentUser.Id, ct);
        return Ok(rows.Select(ToListItem));
    }

    /// <summary>A single delivery. Any user in the tenant can read any of its deliveries.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<DeliveryDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var delivery = await _deliveries.GetByIdAsync(_currentUser.CompanyId, id, ct);
        return delivery is null ? NotFound() : Ok(ToDetail(delivery));
    }

    /// <summary>Admin creates a delivery.</summary>
    [HttpPost]
    [Authorize(Roles = UserRoles.Admin)]
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
            request.AssignedDriverId,
            request.CustomerName,
            request.AgreedPrice), ct);

        var created = await _deliveries.GetByIdAsync(_currentUser.CompanyId, id, ct);
        return CreatedAtAction(nameof(GetById), new { id }, created is null ? null : ToDetail(created));
    }

    /// <summary>Admin assigns / reassigns a pending delivery to a driver.</summary>
    [HttpPatch("{id:int}/assign")]
    [Authorize(Roles = UserRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignDeliveryRequest request, CancellationToken ct)
    {
        await _deliveries.AssignAsync(_currentUser.CompanyId, id, request.DriverId, ct);
        return NoContent();
    }

    private static DeliveryListItem ToListItem(DriverDelivery d) => new(
        d.Id, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
        d.Lat, d.Lng, d.Note, d.Status, d.CreatedAtUtc, d.HasProof, d.IsMine);

    private static DeliveryDetail ToDetail(Delivery d) => new(
        d.Id, d.OrderRef, d.RecipientName, d.RecipientPhone, d.AddressText,
        d.Lat, d.Lng, d.Note, d.AssignedDriverId, d.AssignedDriverName, d.Status, d.CreatedAtUtc);
}
