using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ulak.Mobile.Models;
using Ulak.Mobile.Services;
using Ulak.Mobile.Views;
using Ulak.Shared.Deliveries;

namespace Ulak.Mobile.ViewModels;

public sealed partial class DeliveryListViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly TokenStore _tokenStore;
    private readonly ProofQueue _queue;
    private readonly ProofSyncService _sync;
    private readonly LocalDatabase _db;

    public DeliveryListViewModel(
        ApiClient api, TokenStore tokenStore, ProofQueue queue, ProofSyncService sync, LocalDatabase db)
    {
        _api = api;
        _tokenStore = tokenStore;
        _queue = queue;
        _sync = sync;
        _db = db;
        _queue.Changed += async (_, _) => await MainThread.InvokeOnMainThreadAsync(RefreshQueueBadgeAsync);
    }

    private List<DeliveryListItem> _all = [];

    public ObservableCollection<DeliveryListItem> Deliveries { get; } = [];

    /// <summary>false = only the deliveries assigned to me, true = the whole team's.</summary>
    [ObservableProperty]
    private bool _teamView;

    [ObservableProperty]
    private int _mineCount;

    [ObservableProperty]
    private int _teamCount;

    partial void OnTeamViewChanged(bool value) => ApplyFilter();

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

        // on a cold open, paint the last cached list first so it's never blank;
        // on pull-to-refresh the list is already filled, so skip the flicker
        var hadCache = Deliveries.Count > 0 || await ShowCachedAsync();

        try
        {
            IsBusy = !hadCache;
            var items = await _api.GetTodayDeliveriesAsync(CancellationToken.None);
            Fill(items);

            var now = DateTimeOffset.Now;
            await _db.ReplaceCachedDeliveriesAsync(
                items.Select(i => CachedDelivery.From(i, now)).ToList());
        }
        catch (HttpRequestException)
        {
            var stamp = await _db.GetCacheTimestampAsync();
            ErrorMessage = stamp is { } t
                ? $"Çevrimdışı — liste {t.LocalDateTime:d MMM HH:mm} itibarıyla"
                : "Çevrimdışı — kayıtlı teslimat yok.";
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

    private async Task<bool> ShowCachedAsync()
    {
        var cached = await _db.GetCachedDeliveriesAsync();
        if (cached.Count == 0)
        {
            return false;
        }

        Fill(cached.Select(c => c.ToListItem()));
        return true;
    }

    private void Fill(IEnumerable<DeliveryListItem> items)
    {
        _all = items.ToList();
        MineCount = _all.Count(d => d.IsMine);
        TeamCount = _all.Count;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var shown = TeamView ? _all : _all.Where(d => d.IsMine);
        Deliveries.Clear();
        foreach (var item in shown)
        {
            Deliveries.Add(item);
        }
    }

    [RelayCommand]
    private void SetView(string mode) => TeamView = mode == "team";

    private async Task RefreshQueueBadgeAsync()
    {
        QueuedCount = await _queue.GetPendingCountAsync();
        FailedCount = await _queue.GetFailedCountAsync();
    }

    [RelayCommand]
    private Task SyncNowAsync() => _sync.RetryAllAsync();

    [RelayCommand]
    private async Task OpenDeliveryAsync(DeliveryListItem? delivery)
    {
        if (delivery is not null)
        {
            await Shell.Current.GoToAsync(
                $"{nameof(DeliveryDetailPage)}?id={delivery.Id}&mine={delivery.IsMine}");
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        _tokenStore.Clear();
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }
}
