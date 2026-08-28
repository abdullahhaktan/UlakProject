using LinkLogistics.Api.Auth;
using LinkLogistics.Core.Abstractions;
using LinkLogistics.Core.Domain;
using LinkLogistics.Infrastructure;
using LinkLogistics.Shared.Uploads;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LinkLogistics.Api.Controllers;

[ApiController]
[Route("uploads")]
[Authorize(Roles = UserRoles.Driver)]
public sealed class UploadsController : ControllerBase
{
    private static readonly Dictionary<string, string> AllowedContentTypes = new()
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
    };

    private readonly IObjectStorage _storage;
    private readonly ICurrentUser _currentUser;
    private readonly StorageOptions _options;

    public UploadsController(IObjectStorage storage, ICurrentUser currentUser, IOptions<StorageOptions> options)
    {
        _storage = storage;
        _currentUser = currentUser;
        _options = options.Value;
    }

    /// <summary>
    /// Returns a short-lived presigned PUT URL. The driver app uploads the
    /// photo/signature bytes straight to object storage, then submits the
    /// returned <see cref="PresignResponse.ObjectKey"/> on the proof.
    /// </summary>
    [HttpPost("presign")]
    [ProducesResponseType<PresignResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Presign([FromBody] PresignRequest request)
    {
        if (!AllowedContentTypes.TryGetValue(request.ContentType, out var extension))
        {
            return Problem(detail: "contentType must be image/jpeg or image/png.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var kind = request.Kind?.ToLowerInvariant() switch
        {
            "photo" => "photos",
            "signature" => "signatures",
            _ => null,
        };
        if (kind is null)
        {
            return Problem(detail: "kind must be 'photo' or 'signature'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var now = DateTime.UtcNow;
        var objectKey = $"{kind}/{now:yyyy/MM}/driver-{_currentUser.Id}/{Guid.NewGuid():N}.{extension}";
        var ttl = TimeSpan.FromMinutes(_options.PresignTtlMinutes);

        var upload = _storage.CreateUploadUrl(objectKey, request.ContentType, ttl);
        return Ok(new PresignResponse(
            upload.UploadUrl, upload.PublicUrl, upload.ObjectKey, upload.ExpiresInSeconds));
    }
}
