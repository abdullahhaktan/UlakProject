using Ulak.Mobile.Services;
using Ulak.Mobile.Views;

namespace Ulak.Mobile;

public partial class AppShell : Shell
{
    private readonly TokenStore _tokenStore;
    private bool _landingDone;

    public AppShell(TokenStore tokenStore)
    {
        InitializeComponent();
        _tokenStore = tokenStore;

        Routing.RegisterRoute(nameof(DeliveryDetailPage), typeof(DeliveryDetailPage));
        Routing.RegisterRoute(nameof(ProofCapturePage), typeof(ProofCapturePage));

        Loaded += OnShellLoaded;
    }

    private async void OnShellLoaded(object? sender, EventArgs e)
    {
        if (_landingDone)
        {
            return;
        }

        _landingDone = true;

        var hasSession = false;
        var mustChangePassword = false;
        try
        {
            hasSession = await _tokenStore.HasSessionAsync();
            if (hasSession)
            {
                mustChangePassword = await _tokenStore.MustChangePasswordAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] session check threw: {ex.GetType().Name}: {ex.Message}");
        }

        var target = !hasSession
            ? nameof(LoginPage)
            : mustChangePassword ? nameof(ChangePasswordPage) : nameof(DeliveryListPage);
        Console.WriteLine($"[AppShell] landing -> {target} (hasSession={hasSession}, mustChangePassword={mustChangePassword})");
        await GoToAsync($"//{target}");
    }
}
