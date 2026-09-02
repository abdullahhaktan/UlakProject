using Ulak.Api.Auth;
using Ulak.Core.Abstractions;
using Ulak.Core.Domain;
using Ulak.Shared.Proofs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Api.Controllers;

[ApiController]
[Route("proofs")]
[Authorize(Roles = UserRoles.Driver)]
public sealed class ProofsController : ControllerBase
{
    private readonly IProofRepository _proofs;
    private readonly ICurrentUser _currentUser;

    public ProofsController(IProofRepository proofs, ICurrentUser currentUser)
    {
        _proofs = proofs;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Submit a proof of delivery. Idempotent on <see cref="CreateProofRequest.ClientUuid"/> —
    /// the offline queue may POST the same proof more than once; the second call
    /// returns the existing proof with <c>wasDuplicate = true</c> and HTTP 200.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<CreateProofResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<CreateProofResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateProofRequest request, CancellationToken ct)
    {
        var photos = request.PhotoUrls
            .Select((url, index) => new ProofPhotoInput(url, index))
            .ToList();

        var proof = new NewProof(
            request.ClientUuid,
            request.DeliveryId,
            _currentUser.Id,
            request.Status,
            request.ProofType,
            request.FailureReason,
            request.RecipientSignedName,
            request.SignatureUrl,
            request.CapturedLat,
            request.CapturedLng,
            request.CapturedAt.UtcDateTime,
            photos);

        var result = await _proofs.CreateAsync(_currentUser.CompanyId, proof, ct);
        var response = new CreateProofResponse(
            result.Id, result.DeliveryId, result.Status, result.WasDuplicate, result.ProofType);

        return result.WasDuplicate
            ? Ok(response)
            : CreatedAtRoute("GetById", new { id = result.Id }, response);
    }
}
