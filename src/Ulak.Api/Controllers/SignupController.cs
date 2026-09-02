using Ulak.Api.Services;
using Ulak.Core.Abstractions;
using Ulak.Shared.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ulak.Api.Controllers;

/// <summary>Self-service company sign-up: creates a new tenant + its first Admin
/// and returns a session, so the caller is signed in immediately.</summary>
[ApiController]
[Route("signup")]
[AllowAnonymous]
public sealed class SignupController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthAppService _auth;

    public SignupController(IUserRepository users, IPasswordHasher passwordHasher, AuthAppService auth)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _auth = auth;
    }

    [HttpPost]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SignUp([FromBody] SignUpRequest request, CancellationToken ct)
    {
        var admin = await _users.SignUpCompanyAsync(
            request.CompanyName.Trim(),
            request.AdminName.Trim(),
            Ulak.Shared.PhoneNumber.Normalize(request.Phone) ?? request.Phone.Trim(),
            _passwordHasher.Hash(request.Password),
            ct);

        return Ok(await _auth.IssueForNewUserAsync(admin, ct));
    }
}
