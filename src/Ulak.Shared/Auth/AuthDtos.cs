namespace Ulak.Shared.Auth;

public sealed record LoginRequest(string Phone, string Password);

public sealed record RefreshRequest(string RefreshToken);

/// <summary>Self-service company sign-up: new tenant + its first Admin.</summary>
public sealed record SignUpRequest(string CompanyName, string AdminName, string Phone, string Password);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    UserInfo User);

public sealed record UserInfo(int Id, string Name, string Phone, string Role, bool MustChangePassword);
