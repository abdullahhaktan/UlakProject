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

    public async Task<IReadOnlyList<DriverLookup>> ListDriversAsync(
        int companyId, bool includeInactive, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<DriverLookup>(
            "dbo.usp_AppUser_ListDrivers",
            new { CompanyId = companyId, IncludeInactive = includeInactive },
            ct);
        return rows.ToList();
    }

    public async Task<AppUser> UpdateDriverAsync(
        int companyId, int driverId, string name, string phone, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<AppUser>(
            "dbo.usp_AppUser_UpdateDriver",
            new { CompanyId = companyId, Id = driverId, Name = name, Phone = phone },
            ct);
        return rows.Single();
    }

    public async Task SetDriverActiveAsync(int companyId, int driverId, bool isActive, CancellationToken ct)
    {
        using var connection = _factory.Create();
        await connection.ExecuteProcAsync(
            "dbo.usp_AppUser_SetDriverActive",
            new { CompanyId = companyId, Id = driverId, IsActive = isActive },
            ct);
    }

    public async Task<AppUser> SignUpCompanyAsync(
        string companyName, string adminName, string phone, string passwordHash, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<AppUser>(
            "dbo.usp_Company_SignUp",
            new { CompanyName = companyName, AdminName = adminName, Phone = phone, PasswordHash = passwordHash },
            ct);
        return rows.Single();
    }

    public async Task<AppUser> CreateDriverAsync(
        int companyId, string name, string phone, string passwordHash, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<AppUser>(
            "dbo.usp_AppUser_CreateDriver",
            new { CompanyId = companyId, Name = name, Phone = phone, PasswordHash = passwordHash },
            ct);
        return rows.Single();
    }

    public async Task ChangePasswordAsync(int userId, string newPasswordHash, CancellationToken ct)
    {
        using var connection = _factory.Create();
        await connection.ExecuteProcAsync(
            "dbo.usp_Auth_ChangePassword",
            new { UserId = userId, NewPasswordHash = newPasswordHash },
            ct);
    }
}

public sealed class CompanyRepository : ICompanyRepository
{
    private readonly IDbConnectionFactory _factory;

    public CompanyRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<CompanySettings?> GetSettingsAsync(int companyId, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<CompanySettings>(
            "dbo.usp_Company_GetSettings", new { CompanyId = companyId }, ct);
        return rows.SingleOrDefault();
    }

    public async Task<CompanySettings> UpdateSettingsAsync(
        int companyId, string displayName, bool requirePhoto, bool requireSignature, CancellationToken ct)
    {
        using var connection = _factory.Create();
        var rows = await connection.QueryProcAsync<CompanySettings>(
            "dbo.usp_Company_UpdateSettings",
            new
            {
                CompanyId = companyId,
                DisplayName = displayName,
                RequirePhoto = requirePhoto,
                RequireSignature = requireSignature,
            },
            ct);
        return rows.Single();
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
