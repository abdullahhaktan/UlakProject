namespace LinkLogistics.Web.Api;

public sealed class ApiClientOptions
{
    public const string SectionName = "ApiClient";

    public string BaseUrl { get; set; } = "http://localhost:8080";
}

/// <summary>Names used for the tokens stored in the auth cookie.</summary>
public static class TokenNames
{
    public const string AccessToken = "access_token";
    public const string RefreshToken = "refresh_token";
    public const string ExpiresAt = "expires_at";
}

public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string message) : base(message) => StatusCode = statusCode;

    public int StatusCode { get; }
}
