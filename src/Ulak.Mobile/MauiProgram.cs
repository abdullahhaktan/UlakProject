using Ulak.Mobile.Services;
using Ulak.Mobile.ViewModels;
using Ulak.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace Ulak.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("Inter-Regular.ttf", "Inter");
                fonts.AddFont("Inter-Medium.ttf", "InterMedium");
                fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
                fonts.AddFont("Inter-Bold.ttf", "InterBold");
                fonts.AddFont("Inter-ExtraBold.ttf", "InterExtraBold");
                fonts.AddFont("Phosphor.ttf", "Phosphor");
            });

        // AddDebug surfaces ILogger output in logcat on Android, in Release too —
        // the offline-sync path is hard to diagnose on a device without it.
        builder.Logging.AddDebug();

        // --- platform services ---
        builder.Services.AddSingleton(Connectivity.Current);

        // --- app services ---
        builder.Services.AddSingleton<TokenStore>();
        builder.Services.AddSingleton<AuthHandler>();
        builder.Services.AddSingleton(sp =>
        {
            var handler = sp.GetRequiredService<AuthHandler>();
            handler.SessionExpired += (_, _) =>
                MainThread.BeginInvokeOnMainThread(async () =>
                    await Shell.Current.GoToAsync($"//{nameof(LoginPage)}"));
            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        });
        builder.Services.AddSingleton<ApiClient>();
        builder.Services.AddSingleton<PhotoService>();
        builder.Services.AddSingleton<LocationService>();
        builder.Services.AddSingleton<LocalDatabase>();
        builder.Services.AddSingleton<ProofQueue>();
        builder.Services.AddSingleton<ProofSyncService>();

        // --- shell + pages + view models ---
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddTransient<DeliveryListPage>();
        builder.Services.AddTransient<DeliveryListViewModel>();
        builder.Services.AddTransient<DeliveryDetailPage>();
        builder.Services.AddTransient<DeliveryDetailViewModel>();
        builder.Services.AddTransient<ProofCapturePage>();
        builder.Services.AddTransient<ProofCaptureViewModel>();

        var app = builder.Build();

        // start draining the offline queue
        app.Services.GetRequiredService<ProofSyncService>().Start();

        return app;
    }
}
