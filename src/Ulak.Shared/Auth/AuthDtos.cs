namespace Ulak.Shared.Auth;

public sealed record LoginRequest(string Phone, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresInSeconds,
    UserInfo User);

public sealed record UserInfo(int Id, string Name, string Phone, string Role);
