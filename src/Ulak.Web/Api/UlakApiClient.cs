using System.Net;
using System.Net.Http.Json;
using System.Web;
using Ulak.Shared.Admin;
using Ulak.Shared.Auth;
using Ulak.Shared.Deliveries;
using Ulak.Shared.Proofs;

namespace Ulak.Web.Api;

public sealed record GridQuery(
    string? Search = null,
    string? Status = null,
    int? DriverId = null,
    string? From = null,
    string? To = null,
    string? ProofType = null,
    string Sort = "CreatedAtUtc",
    string Dir = "DESC",
    int Skip = 0,
    int Take = 20);

public sealed record PagedProofs(IReadOnlyList<ProofListItem> Items, int TotalCount);

public sealed class UlakApiClient
{
    private readonly HttpClient _http;

    public UlakApiClient(HttpClient http) => _http = http;

    public async Task<AuthResponse?> LoginAsync(string phone, string password, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("auth/login", new LoginRequest(phone, password), ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct);
    }

    /// <summary>Self-service company sign-up. Returns a session, or throws <see cref="ApiException"/> (e.g. 409).</summary>
    public async Task<AuthResponse> SignUpAsync(SignUpRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("signup", request, ct);
        await ThrowIfProblem(response);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken: ct))!;
    }

    public async Task<CreateDriverResponse> CreateDriverAsync(CreateDriverRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("admin/drivers", request, ct);
        await ThrowIfProblem(response);
        return (await response.Content.ReadFromJsonAsync<CreateDriverResponse>(cancellationToken: ct))!;
    }

    public Task<PagedResponse<AdminDeliveryRow>?> GetDeliveriesAsync(GridQuery query, CancellationToken ct) =>
        _http.GetFromJsonAsync<PagedResponse<AdminDeliveryRow>>(
            "admin/deliveries" + ToQueryString(query), ct);

    public Task<IReadOnlyList<DriverListItem>?> GetDriversAsync(bool includeInactive, CancellationToken ct) =>
        _http.GetFromJsonAsync<IReadOnlyList<DriverListItem>>(
            "admin/drivers" + (includeInactive ? "?include_inactive=true" : ""), ct);

    public async Task<DriverListItem> UpdateDriverAsync(int id, UpdateDriverRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync($"admin/drivers/{id}", request, ct);
        await ThrowIfProblem(response);
        return (await response.Content.ReadFromJsonAsync<DriverListItem>(cancellationToken: ct))!;
    }

    public async Task SetDriverActiveAsync(int id, bool isActive, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync(
            $"admin/drivers/{id}/active", new SetDriverActiveRequest(isActive), ct);
        await ThrowIfProblem(response);
    }

    public Task<DashboardSummaryDto?> GetDashboardAsync(CancellationToken ct) =>
        _http.GetFromJsonAsync<DashboardSummaryDto>("admin/dashboard", ct);

    public Task<CompanyConfigDto?> GetSettingsAsync(CancellationToken ct) =>
        _http.GetFromJsonAsync<CompanyConfigDto>("admin/settings", ct);

    public async Task<CompanyConfigDto> UpdateSettingsAsync(UpdateCompanySettingsRequest request, CancellationToken ct)
    {
        var response = await _http.PutAsJsonAsync("admin/settings", request, ct);
        await ThrowIfProblem(response);
        return (await response.Content.ReadFromJsonAsync<CompanyConfigDto>(cancellationToken: ct))!;
    }

    public Task<PagedProofs?> GetProofsAsync(GridQuery query, CancellationToken ct) =>
        _http.GetFromJsonAsync<PagedProofs>("admin/proofs" + ToQueryString(query, "CapturedAtUtc"), ct);

    public Task<ProofDetail?> GetProofAsync(long id, CancellationToken ct) =>
        _http.GetFromJsonAsync<ProofDetail>($"admin/proofs/{id}", ct);

    public async Task<(byte[] Bytes, string ContentType)?> GetProofPdfAsync(long id, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"admin/proofs/{id}/pdf", ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return (await response.Content.ReadAsByteArrayAsync(ct),
            response.Content.Headers.ContentType?.MediaType ?? "application/pdf");
    }

    public async Task<byte[]> GetProofsExcelAsync(GridQuery query, CancellationToken ct)
    {
        using var response = await _http.GetAsync("admin/proofs/export" + ToQueryString(query, "CapturedAtUtc"), ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task CreateDeliveryAsync(CreateDeliveryRequest request, CancellationToken ct)
    {
        var response = await _http.PostAsJsonAsync("deliveries", request, ct);
        await ThrowIfProblem(response);
    }

    public async Task AssignDeliveryAsync(int id, int driverId, CancellationToken ct)
    {
        var response = await _http.PatchAsJsonAsync(
            $"deliveries/{id}/assign", new AssignDeliveryRequest(driverId), ct);
        await ThrowIfProblem(response);
    }

    private static string ToQueryString(GridQuery q, string? defaultSort = null)
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add($"{key}={HttpUtility.UrlEncode(value)}");
            }
        }

        Add("search", q.Search);
        Add("status", q.Status);
        Add("proof_type", q.ProofType);
        if (q.DriverId is > 0) parts.Add($"driver_id={q.DriverId}");
        Add("from", q.From);
        Add("to", q.To);
        parts.Add($"sort={HttpUtility.UrlEncode(q.Sort == "CreatedAtUtc" && defaultSort is not null ? defaultSort : q.Sort)}");
        parts.Add($"dir={q.Dir}");
        parts.Add($"skip={q.Skip}");
        parts.Add($"take={q.Take}");
        return "?" + string.Join('&', parts);
    }

    private static async Task ThrowIfProblem(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var detail = body;
        try
        {
            var problem = System.Text.Json.JsonSerializer.Deserialize<Microsoft.AspNetCore.Mvc.ProblemDetails>(body);
            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                detail = problem.Detail;
            }
        }
        catch
        {
            // keep raw body
        }

        throw new ApiException((int)response.StatusCode, detail);
    }
}
