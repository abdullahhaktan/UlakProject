using System;

namespace Ulak.Mobile.Services;

/// <summary>
/// Persists the API session tokens. Primary store is <see cref="SecureStorage"/>
/// (Keystore-backed); a <see cref="Preferences"/> mirror is kept because
/// SecureStorage silently fails to read back on some Android devices
/// (Keystore / auto-backup quirks) — losing the mirror only downgrades
/// at-rest protection on an already app-private file, but losing the session
/// on every cold start makes the app unusable offline.
/// </summary>
public sealed class TokenStore
{
    private const string AccessKey = "ll_access_token";
    private const string RefreshKey = "ll_refresh_token";
    private const string UserNameKey = "ll_user_name";
    private const string UserIdKey = "ll_user_id";

    public async Task SaveAsync(string accessToken, string refreshToken, int userId, string userName)
    {
        await SetAsync(AccessKey, accessToken);
        await SetAsync(RefreshKey, refreshToken);
        await SetAsync(UserNameKey, userName);
        await SetAsync(UserIdKey, userId.ToString());
    }

    public Task<string?> GetAccessTokenAsync() => GetAsync(AccessKey);

    public Task<string?> GetRefreshTokenAsync() => GetAsync(RefreshKey);

    public Task<string?> GetUserNameAsync() => GetAsync(UserNameKey);

    public async Task<int> GetUserIdAsync() =>
        int.TryParse(await GetAsync(UserIdKey), out var id) ? id : 0;

    public async Task<bool> HasSessionAsync() =>
        !string.IsNullOrEmpty(await GetRefreshTokenAsync());

    public void Clear()
    {
        foreach (var key in new[] { AccessKey, RefreshKey, UserNameKey, UserIdKey })
        {
            try { SecureStorage.Remove(key); } catch (Exception ex) { Log(nameof(Clear), key, ex); }
            Preferences.Remove(key);
        }
    }

    private static async Task SetAsync(string key, string value)
    {
        try
        {
            await SecureStorage.SetAsync(key, value);
        }
        catch (Exception ex)
        {
            Log(nameof(SetAsync), key, ex);
        }

        // Mirror unconditionally: a successful SecureStorage write that can't be
        // read back later (the failure mode we're guarding against) is invisible here.
        Preferences.Set(key, value);
    }

    private static async Task<string?> GetAsync(string key)
    {
        try
        {
            var secure = await SecureStorage.GetAsync(key);
            if (!string.IsNullOrEmpty(secure))
            {
                return secure;
            }
        }
        catch (Exception ex)
        {
            Log(nameof(GetAsync), key, ex);
        }

        var mirrored = Preferences.Get(key, null);
        if (!string.IsNullOrEmpty(mirrored))
        {
            Console.WriteLine($"[TokenStore] {key}: served from Preferences mirror (SecureStorage empty)");
        }

        return mirrored;
    }

    private static void Log(string op, string key, Exception ex) =>
        Console.WriteLine($"[TokenStore] {op}({key}) SecureStorage failed: {ex.GetType().Name}: {ex.Message}");
}
