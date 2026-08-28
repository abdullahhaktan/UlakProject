namespace LinkLogistics.Mobile;

/// <summary>
/// Static configuration for the driver app.
/// <para>
/// The Android emulator reaches the host machine at <c>10.0.2.2</c>, so the
/// default points at the compose API published on the host. On a physical
/// device, override this on the login screen with the machine's LAN IP.
/// </para>
/// </summary>
public static class AppConfig
{
    public const string DefaultApiBaseUrl = "http://10.0.2.2:8080";

    public const string ApiBaseUrlKey = "api_base_url";

    public static string ApiBaseUrl =>
        Preferences.Get(ApiBaseUrlKey, DefaultApiBaseUrl);

    public static void SetApiBaseUrl(string value) =>
        Preferences.Set(ApiBaseUrlKey, string.IsNullOrWhiteSpace(value) ? DefaultApiBaseUrl : value.Trim());
}
