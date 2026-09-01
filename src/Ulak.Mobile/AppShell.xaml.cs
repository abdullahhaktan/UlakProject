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
        try
        {
            hasSession = await _tokenStore.HasSessionAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppShell] HasSessionAsync threw: {ex.GetType().Name}: {ex.Message}");
        }

        var target = hasSession ? nameof(DeliveryListPage) : nameof(LoginPage);
        Console.WriteLine($"[AppShell] landing -> {target} (hasSession={hasSession})");
        await GoToAsync($"//{target}");
    }
}
