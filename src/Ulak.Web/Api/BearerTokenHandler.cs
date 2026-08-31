using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Ulak.Shared.Auth;

namespace Ulak.Web.Api;

/// <summary>
/// Attaches the signed-in user's API access token to every outgoing request.
/// When the token is within two minutes of expiry (or a call comes back 401),
/// it silently rotates the refresh token and re-issues the auth cookie.
/// </summary>
public sealed class BearerTokenHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<BearerTokenHandler> _logger;

    public BearerTokenHandler(
        IHttpContextAccessor httpContextAccessor,
        IHttpClientFactory httpClientFactory,
        ILogger<BearerTokenHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var accessToken = await GetValidAccessTokenAsync(context, cancellationToken);
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // one retry after a forced refresh
        var refreshed = await RefreshAsync(context, cancellationToken);
        if (refreshed is null)
        {
            return response;
        }

        response.Dispose();
        var retry = await CloneAsync(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task<string?> GetValidAccessTokenAsync(HttpContext context, CancellationToken ct)
    {
        var accessToken = await context.GetTokenAsync(TokenNames.AccessToken);
        var expiresAtRaw = await context.GetTokenAsync(TokenNames.ExpiresAt);

        if (DateTimeOffset.TryParse(expiresAtRaw, out var expiresAt)
            && expiresAt - DateTimeOffset.UtcNow > TimeSpan.FromMinutes(2))
        {
            return accessToken;
        }

        return await RefreshAsync(context, ct) ?? accessToken;
    }

    private async Task<string?> RefreshAsync(HttpContext context, CancellationToken ct)
    {
        var refreshToken = await context.GetTokenAsync(TokenNames.RefreshToken);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return null;
        }

        try
        {
            using var client = _httpClientFactory.CreateClient(nameof(BearerTokenHandler));
            var response = await client.PostAsJsonAsync("auth/refresh", new RefreshRequest(refreshToken), ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token refresh failed with {Status}", response.StatusCode);
                return null;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
            if (auth is null)
            {
                return null;
            }

            var authResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            if (authResult.Succeeded)
            {
                var props = authResult.Properties!;
                props.UpdateTokenValue(TokenNames.AccessToken, auth.AccessToken);
                props.UpdateTokenValue(TokenNames.RefreshToken, auth.RefreshToken);
                props.UpdateTokenValue(TokenNames.ExpiresAt,
                    DateTimeOffset.UtcNow.AddSeconds(auth.ExpiresInSeconds).ToString("o"));
                await context.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme, authResult.Principal!, props);
            }

            return auth.AccessToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token refresh threw");
            return null;
        }
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
