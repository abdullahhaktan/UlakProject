using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LinkLogistics.Mobile.Services;
using LinkLogistics.Mobile.Views;
using LinkLogistics.Shared.Deliveries;

namespace LinkLogistics.Mobile.ViewModels;

public sealed partial class DeliveryListViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly TokenStore _tokenStore;
    private readonly ProofQueue _queue;
    private readonly ProofSyncService _sync;

    public DeliveryListViewModel(
        ApiClient api, TokenStore tokenStore, ProofQueue queue, ProofSyncService sync)
    {
        _api = api;
        _tokenStore = tokenStore;
        _queue = queue;
        _sync = sync;
        _queue.Changed += async (_, _) => await MainThread.InvokeOnMainThreadAsync(RefreshQueueBadgeAsync);
    }

    public ObservableCollection<DeliveryListItem> Deliveries { get; } = [];

    [ObservableProperty]
    private string _driverName = string.Empty;

    [ObservableProperty]
    private int _queuedCount;

    [ObservableProperty]
    private int _failedCount;

    [ObservableProperty]
    private bool _isRefreshing;

    public bool HasQueued => QueuedCount > 0;
    public bool HasFailed => FailedCount > 0;

    partial void OnQueuedCountChanged(int value) => OnPropertyChanged(nameof(HasQueued));

    partial void OnFailedCountChanged(int value) => OnPropertyChanged(nameof(HasFailed));

    [RelayCommand]
    public async Task LoadAsync()
    {
        ClearError();
        DriverName = await _tokenStore.GetUserNameAsync() ?? string.Empty;
        await RefreshQueueBadgeAsync();

        try
        {
            IsBusy = true;
            var items = await _api.GetTodayDeliveriesAsync(CancellationToken.None);
            Deliveries.Clear();
            foreach (var item in items)
            {
                Deliveries.Add(item);
            }
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Çevrimdışı — teslimat listesi güncellenemedi.";
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }

    private async Task RefreshQueueBadgeAsync()
    {
        QueuedCount = await _queue.GetPendingCountAsync();
        FailedCount = await _queue.GetFailedCountAsync();
    }

    [RelayCommand]
    private Task SyncNowAsync() => _sync.RunOnceAsync();

    [RelayCommand]
    private async Task OpenDeliveryAsync(DeliveryListItem? delivery)
    {
        if (delivery is not null)
        {
            await Shell.Current.GoToAsync($"{nameof(DeliveryDetailPage)}?id={delivery.Id}");
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        _tokenStore.Clear();
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }
}
