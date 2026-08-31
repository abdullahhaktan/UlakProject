using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Ulak.Shared.Auth;
using Ulak.Shared.Deliveries;
using Ulak.Shared.Proofs;
using Ulak.Shared.Uploads;

namespace Ulak.Mobile.Services;

public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Talks to the Ulak REST API. All URIs are absolute so the base URL can change at runtime.</summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;      // carries the AuthHandler
    private readonly TokenStore _tokenStore;

    public ApiClient(HttpClient http, TokenStore tokenStore)
    {
        _http = http;
        _tokenStore = tokenStore;
    }

    private static Uri Url(string path) => new($"{AppConfig.ApiBaseUrl.TrimEnd('/')}/{path}");

    public async Task<AuthResponse> LoginAsync(string phone, string password, CancellationToken ct)
    {
        using var plain = new HttpClient();
        var response = await plain.PostAsJsonAsync(Url("auth/login"), new LoginRequest(phone, password), ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ApiException(401, "Telefon veya şifre hatalı.");
        }

        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct)
                   ?? throw new ApiException(500, "Sunucu yanıtı boş.");
        await _tokenStore.SaveAsync(auth.AccessToken, auth.RefreshToken, auth.User.Id, auth.User.Name);
        return auth;
    }

    public async Task<IReadOnlyList<DeliveryListItem>> GetTodayDeliveriesAsync(CancellationToken ct)
    {
        var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var result = await _http.GetFromJsonAsync<List<DeliveryListItem>>(Url($"deliveries?date={date}"), ct);
        return result ?? [];
    }

    public Task<DeliveryDetail?> GetDeliveryAsync(int id, CancellationToken ct) =>
        _http.GetFromJsonAsync<DeliveryDetail>(Url($"deliveries/{id}"), ct);

    public async Task<PresignResponse> PresignAsync(string contentType, string kind, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(Url("uploads/presign"), new PresignRequest(contentType, kind), ct);
        await ThrowIfProblem(response);
        return (await response.Content.ReadFromJsonAsync<PresignResponse>(cancellationToken: ct))!;
    }

    public async Task UploadAsync(string uploadUrl, Stream content, string contentType, CancellationToken ct)
    {
        // A presigned S3/MinIO URL carries its own auth in the query string.
        // Sending it through _http would add the API's "Authorization: Bearer"
        // header too, and MinIO rejects requests that mix both auth mechanisms.
        using var plain = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        using var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await plain.PutAsync(uploadUrl, streamContent, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ApiException((int)response.StatusCode,
                $"Dosya yüklenemedi ({(int)response.StatusCode}). {body}".Trim());
        }
    }

    public async Task<CreateProofResponse> SubmitProofAsync(CreateProofRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(Url("proofs"), request, ct);
        await ThrowIfProblem(response);
        return (await response.Content.ReadFromJsonAsync<CreateProofResponse>(cancellationToken: ct))!;
    }

    private static async Task ThrowIfProblem(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new ApiException((int)response.StatusCode, string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Hata" : body);
    }
}
