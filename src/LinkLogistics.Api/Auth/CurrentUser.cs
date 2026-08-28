using System.Security.Claims;
using LinkLogistics.Core.Domain;
using LinkLogistics.Infrastructure.Security;

namespace LinkLogistics.Api.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    int Id { get; }
    int CompanyId { get; }
    string Role { get; }
    string Name { get; }
    AppUser ToAppUser();
}

public sealed class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }

    public bool IsAuthenticated => _principal.Identity?.IsAuthenticated == true;

    public int Id => GetInt(ClaimTypes.NameIdentifier, "sub");

    public int CompanyId => GetInt(TokenService.CompanyIdClaim);

    public string Role => _principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;

    public string Name => _principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;

    public AppUser ToAppUser() => new(
        Id, CompanyId,
        _principal.FindFirstValue(ClaimTypes.MobilePhone) ?? string.Empty,
        Name, Role, IsActive: true);

    private int GetInt(params string[] claimTypes)
    {
        foreach (var type in claimTypes)
        {
            var raw = _principal.FindFirstValue(type);
            if (int.TryParse(raw, out var value))
            {
                return value;
            }
        }

        return 0;
    }
}
