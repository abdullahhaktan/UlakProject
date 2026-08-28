namespace LinkLogistics.Mobile.Services;

/// <summary>Persists the API tokens in the platform secure store.</summary>
public sealed class TokenStore
{
    private const string AccessKey = "ll_access_token";
    private const string RefreshKey = "ll_refresh_token";
    private const string UserNameKey = "ll_user_name";
    private const string UserIdKey = "ll_user_id";

    public async Task SaveAsync(string accessToken, string refreshToken, int userId, string userName)
    {
        await SecureStorage.SetAsync(AccessKey, accessToken);
        await SecureStorage.SetAsync(RefreshKey, refreshToken);
        await SecureStorage.SetAsync(UserNameKey, userName);
        await SecureStorage.SetAsync(UserIdKey, userId.ToString());
    }

    public Task<string?> GetAccessTokenAsync() => SecureStorage.GetAsync(AccessKey);

    public Task<string?> GetRefreshTokenAsync() => SecureStorage.GetAsync(RefreshKey);

    public async Task<string?> GetUserNameAsync() => await SecureStorage.GetAsync(UserNameKey);

    public async Task<int> GetUserIdAsync() =>
        int.TryParse(await SecureStorage.GetAsync(UserIdKey), out var id) ? id : 0;

    public async Task<bool> HasSessionAsync() =>
        !string.IsNullOrEmpty(await GetRefreshTokenAsync());

    public void Clear()
    {
        SecureStorage.Remove(AccessKey);
        SecureStorage.Remove(RefreshKey);
        SecureStorage.Remove(UserNameKey);
        SecureStorage.Remove(UserIdKey);
    }
}
