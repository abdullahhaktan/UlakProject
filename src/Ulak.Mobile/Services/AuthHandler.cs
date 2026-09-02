using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ulak.Shared.Auth;

namespace Ulak.Mobile.Services;

/// <summary>
/// Attaches the stored access token to every request and, on a 401,
/// rotates the refresh token once before retrying. If the refresh fails
/// the session is cleared and <see cref="SessionExpired"/> is raised.
/// </summary>
public sealed class AuthHandler : DelegatingHandler
{
    private readonly TokenStore _tokenStore;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public AuthHandler(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        InnerHandler = new HttpClientHandler();
    }

    public event EventHandler? SessionExpired;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await _tokenStore.GetAccessTokenAsync();
        if (!string.IsNullOrEmpty(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var newToken = await TryRefreshAsync(cancellationToken);
        if (newToken is null)
        {
            SessionExpired?.Invoke(this, EventArgs.Empty);
            return response;
        }

        response.Dispose();
        var retry = await CloneAsync(request);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    private async Task<string?> TryRefreshAsync(CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct);
        try
        {
            var refreshToken = await _tokenStore.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
            {
                return null;
            }

            using var client = new HttpClient { BaseAddress = new Uri(AppConfig.ApiBaseUrl) };
            var response = await client.PostAsJsonAsync("auth/refresh", new RefreshRequest(refreshToken), ct);
            if (!response.IsSuccessStatusCode)
            {
                _tokenStore.Clear();
                return null;
            }

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
            if (auth is null)
            {
                return null;
            }

            await _tokenStore.SaveAsync(
                auth.AccessToken, auth.RefreshToken, auth.User.Id, auth.User.Name, auth.User.MustChangePassword);
            return auth.AccessToken;
        }
        catch
        {
            return null;
        }
        finally
        {
            _refreshLock.Release();
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

        return clone;
    }
}
