using Ulak.Api.Services;
using Ulak.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Api.Controllers;

[ApiController]
[Route("auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly AuthAppService _auth;

    public AuthController(AuthAppService auth) => _auth = auth;

    /// <summary>Exchange phone + password for an access token and a refresh token.</summary>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(request, ct);
        return result.Succeeded
            ? Ok(result.Response)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);
    }

    /// <summary>Rotate a refresh token for a new access + refresh pair.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(request, ct);
        return result.Succeeded
            ? Ok(result.Response)
            : Problem(detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);
    }
}
