using System.Globalization;
using Ulak.Api.Auth;
using Ulak.Core.Abstractions;
using Ulak.Core.Domain;
using Ulak.Infrastructure;
using Ulak.Shared.Proofs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ulak.Api.Controllers;

[ApiController]
[Route("admin/proofs")]
[Authorize(Roles = UserRoles.Admin)]
public sealed class AdminProofsController : ControllerBase
{
    private static readonly string[] SortableColumns =
        ["OrderRef", "DriverName", "Status", "CapturedAtUtc"];

    private readonly IProofRepository _proofs;
    private readonly IObjectStorage _storage;
    private readonly IProofDocumentService _documents;
    private readonly StorageOptions _storageOptions;
    private readonly ICurrentUser _currentUser;

    public AdminProofsController(
        IProofRepository proofs,
        IObjectStorage storage,
        IProofDocumentService documents,
        IOptions<StorageOptions> storageOptions,
        ICurrentUser currentUser)
    {
        _proofs = proofs;
        _storage = storage;
        _documents = documents;
        _storageOptions = storageOptions.Value;
        _currentUser = currentUser;
    }

    /// <summary>Paged / filtered proof list for the ops panel grid.</summary>
    [HttpGet]
    [ProducesResponseType<PagedProofs>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "from")] string? from,
        [FromQuery(Name = "to")] string? to,
        [FromQuery(Name = "driver_id")] int? driverId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string sort = "CapturedAtUtc",
        [FromQuery] string dir = "DESC",
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var query = new ProofSearchQuery(
            ParseDate(from),
            ParseDate(to),
            driverId,
            NormalizeStatus(status),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            SortableColumns.Contains(sort) ? sort : "CapturedAtUtc",
            dir.Equals("ASC", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC",
            Math.Max(0, skip),
            Math.Clamp(take, 1, 200));

        var page = await _proofs.AdminSearchAsync(_currentUser.CompanyId, query, ct);
        var items = page.Items.Select(r => new ProofListItem(
            r.Id, r.DeliveryId, r.OrderRef, r.RecipientName, r.Status, r.FailureReason,
            r.DriverId, r.DriverName, r.PhotoCount, r.CapturedAtUtc, r.SyncedAtUtc)).ToList();

        return Ok(new PagedProofs(items, page.TotalCount));
    }

    [HttpGet("{id:long}", Name = "GetById")]
    [ProducesResponseType<ProofDetail>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct)
    {
        var detail = await _proofs.GetByIdAsync(_currentUser.CompanyId, id, ct);
        if (detail is null)
        {
            return NotFound();
        }

        var ttl = TimeSpan.FromMinutes(_storageOptions.PresignTtlMinutes);
        var photos = detail.Photos
            .Select(p => new ProofPhotoDto(p.Id, _storage.CreateReadUrl(p.Url, ttl), p.OrderIndex))
            .ToList();

        var signatureUrl = string.IsNullOrEmpty(detail.SignatureUrl)
            ? null
            : _storage.CreateReadUrl(detail.SignatureUrl, ttl);

        return Ok(new ProofDetail(
            detail.Id, detail.DeliveryId, detail.OrderRef, detail.RecipientName, detail.RecipientPhone,
            detail.AddressText, detail.DeliveryLat, detail.DeliveryLng, detail.Status, detail.FailureReason,
            detail.RecipientSignedName, signatureUrl, detail.CapturedLat, detail.CapturedLng,
            detail.CapturedAtUtc, detail.SyncedAtUtc, detail.DriverId, detail.DriverName, detail.DriverPhone,
            photos));
    }

    [HttpGet("{id:long}/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(long id, CancellationToken ct)
    {
        var pdf = await _documents.RenderProofPdfAsync(_currentUser.CompanyId, id, ct);
        return pdf is null ? NotFound() : File(pdf, "application/pdf", $"proof-{id}.pdf");
    }

    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery(Name = "from")] string? from,
        [FromQuery(Name = "to")] string? to,
        [FromQuery(Name = "driver_id")] int? driverId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        CancellationToken ct = default)
    {
        var query = new ProofSearchQuery(
            ParseDate(from), ParseDate(to), driverId, NormalizeStatus(status),
            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            "CapturedAtUtc", "DESC", 0, 5000);

        var bytes = await _documents.ExportProofsXlsxAsync(_currentUser.CompanyId, query, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"proofs-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static string? NormalizeStatus(string? status) => status?.ToLowerInvariant() switch
    {
        "delivered" => ProofStatuses.Delivered,
        "failed" => ProofStatuses.Failed,
        _ => null,
    };

    public sealed record PagedProofs(IReadOnlyList<ProofListItem> Items, int TotalCount);
}
