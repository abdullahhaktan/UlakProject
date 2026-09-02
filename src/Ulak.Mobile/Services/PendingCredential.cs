namespace Ulak.Mobile.Services;

/// <summary>
/// Carries the phone + password the driver just typed at login across the
/// one navigation into <c>ChangePasswordPage</c>, so a first-login password
/// change doesn't ask them to re-type the temp password. In-memory only,
/// never persisted, cleared once used.
/// </summary>
public sealed class PendingCredential
{
    public string? Phone { get; private set; }
    public string? CurrentPassword { get; private set; }

    public void Set(string phone, string currentPassword)
    {
        Phone = phone;
        CurrentPassword = currentPassword;
    }

    public void Clear()
    {
        Phone = null;
        CurrentPassword = null;
    }
}
