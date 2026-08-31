using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Ulak.Core.Abstractions;
using Ulak.Core.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ulak.Infrastructure.Security;

public sealed class TokenService : ITokenService
{
    public const string CompanyIdClaim = "company_id";

    private readonly JwtOptions _options;
    private readonly SigningCredentials _signingCredentials;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(AppUser user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.MobilePhone, user.Phone),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(CompanyIdClaim, user.CompanyId.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: _signingCredentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(value, _options.AccessTokenMinutes * 60, expires);
    }

    public (string Token, string TokenHash, DateTime ExpiresAtUtc) CreateRefreshToken()
    {
        var raw = RandomNumberGenerator.GetBytes(32);
        var token = Base64UrlEncoder.Encode(raw);
        return (token, HashRefreshToken(token), DateTime.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
