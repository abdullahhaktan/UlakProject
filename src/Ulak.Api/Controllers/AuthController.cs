using System.Security.Claims;
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

    /// <summary>Change the signed-in user's password (also clears the "must change" flag).</summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = int.Parse(
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "0");
        var phone = User.FindFirstValue(ClaimTypes.MobilePhone) ?? string.Empty;

        var ok = await _auth.ChangePasswordAsync(userId, phone, request, ct);
        return ok
            ? NoContent()
            : Problem(detail: "Mevcut şifre hatalı.", statusCode: StatusCodes.Status400BadRequest);
    }
}
