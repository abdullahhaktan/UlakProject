using Ulak.Core.Abstractions;
using Ulak.Core.Domain;
using Ulak.Shared.Auth;

namespace Ulak.Api.Services;

public sealed class AuthResult
{
    public AuthResponse? Response { get; private init; }
    public string? Error { get; private init; }
    public bool Succeeded => Response is not null;

    public static AuthResult Ok(AuthResponse response) => new() { Response = response };
    public static AuthResult Fail(string error) => new() { Error = error };
}

public sealed class AuthAppService
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthAppService> _logger;

    public AuthAppService(
        IUserRepository users,
        IRefreshTokenRepository refreshTokens,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<AuthAppService> logger)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await _users.GetByPhoneAsync(request.Phone.Trim(), ct);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for {Phone}", request.Phone);
            return AuthResult.Fail("Invalid phone or password.");
        }

        var principal = new AppUser(user.Id, user.CompanyId, user.Phone, user.Name, user.Role, user.IsActive);
        return AuthResult.Ok(await IssueTokensAsync(principal, ct));
    }

    public async Task<AuthResult> RefreshAsync(RefreshRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return AuthResult.Fail("Missing refresh token.");
        }

        var hash = _tokenService.HashRefreshToken(request.RefreshToken);
        var user = await _refreshTokens.ValidateAsync(hash, ct);
        if (user is null)
        {
            return AuthResult.Fail("Refresh token is invalid or expired.");
        }

        // rotate: revoke the presented token, issue a fresh pair
        await _refreshTokens.RevokeAsync(hash, ct);
        return AuthResult.Ok(await IssueTokensAsync(user, ct));
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, CancellationToken ct)
    {
        var access = _tokenService.CreateAccessToken(user);
        var (refreshToken, refreshHash, refreshExpiry) = _tokenService.CreateRefreshToken();
        await _refreshTokens.StoreAsync(user.Id, refreshHash, refreshExpiry, ct);

        return new AuthResponse(
            access.Value,
            refreshToken,
            access.ExpiresInSeconds,
            new UserInfo(user.Id, user.Name, user.Phone, user.Role));
    }
}
