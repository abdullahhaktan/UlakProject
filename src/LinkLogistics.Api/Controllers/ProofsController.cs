using LinkLogistics.Api.Auth;
using LinkLogistics.Core.Abstractions;
using LinkLogistics.Core.Domain;
using LinkLogistics.Shared.Proofs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LinkLogistics.Api.Controllers;

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
            request.FailureReason,
            request.RecipientSignedName,
            request.SignatureUrl,
            request.CapturedLat,
            request.CapturedLng,
            request.CapturedAt.UtcDateTime,
            photos);

        var result = await _proofs.CreateAsync(proof, ct);
        var response = new CreateProofResponse(result.Id, result.DeliveryId, result.Status, result.WasDuplicate);

        return result.WasDuplicate
            ? Ok(response)
            : CreatedAtRoute("GetById", new { id = result.Id }, response);
    }
}
