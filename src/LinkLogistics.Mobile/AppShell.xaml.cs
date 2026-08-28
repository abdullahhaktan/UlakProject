using LinkLogistics.Mobile.Services;
using LinkLogistics.Mobile.Views;

namespace LinkLogistics.Mobile;

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
        var target = await _tokenStore.HasSessionAsync() ? nameof(DeliveryListPage) : nameof(LoginPage);
        await GoToAsync($"//{target}");
    }
}
