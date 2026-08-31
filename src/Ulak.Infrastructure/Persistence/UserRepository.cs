using Ulak.Core.Abstractions;
using Ulak.Core.Domain;

namespace Ulak.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;

    public UserRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<AppUserWithHash?> GetByPhoneAsync(string phone, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<AppUserWithHash>(
            "dbo.usp_Auth_GetUserByPhone", new { Phone = phone }, ct);
        return rows.SingleOrDefault();
    }

    public async Task<IReadOnlyList<DriverLookup>> ListDriversAsync(CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<DriverLookup>("dbo.usp_AppUser_ListDrivers", null, ct);
        return rows.ToList();
    }
}

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _factory;

    public RefreshTokenRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task StoreAsync(int userId, string tokenHash, DateTime expiresAtUtc, CancellationToken ct)
    {
        using var connection = _factory.Create();
        await connection.ExecuteProcAsync(
            "dbo.usp_Auth_StoreRefreshToken",
            new { UserId = userId, TokenHash = tokenHash, ExpiresAtUtc = expiresAtUtc },
            ct);
    }

    public async Task<AppUser?> ValidateAsync(string tokenHash, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<AppUser>(
            "dbo.usp_Auth_ValidateRefreshToken", new { TokenHash = tokenHash }, ct);
        return rows.SingleOrDefault();
    }

    public async Task RevokeAsync(string tokenHash, CancellationToken ct)
    {
        using var connection = _factory.Create();
        await connection.ExecuteProcAsync(
            "dbo.usp_Auth_RevokeRefreshToken", new { TokenHash = tokenHash }, ct);
    }
}
