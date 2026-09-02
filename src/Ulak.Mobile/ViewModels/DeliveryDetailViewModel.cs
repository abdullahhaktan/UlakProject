using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ulak.Mobile.Services;
using Ulak.Shared.Deliveries;

namespace Ulak.Mobile.ViewModels;

public sealed partial class DeliveryDetailViewModel : BaseViewModel, IQueryAttributable
{
    private readonly ApiClient _api;
    private readonly LocalDatabase _db;

    public DeliveryDetailViewModel(ApiClient api, LocalDatabase db)
    {
        _api = api;
        _db = db;
    }

    [ObservableProperty]
    private int _deliveryId;

    [ObservableProperty]
    private DeliveryDetail? _delivery;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var raw) && int.TryParse(raw?.ToString(), out var id))
        {
            DeliveryId = id;
            _ = LoadAsync();
        }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (DeliveryId <= 0)
        {
            return;
        }

        ClearError();
        try
        {
            IsBusy = true;
            Delivery = await _api.GetDeliveryAsync(DeliveryId, CancellationToken.None);
        }
        catch (HttpRequestException)
        {
            var cached = await _db.GetCachedDeliveryAsync(DeliveryId);
            if (cached is not null)
            {
                Delivery = cached.ToDetail();
                ErrorMessage = "Çevrimdışı — kayıtlı bilgiler gösteriliyor.";
            }
            else
            {
                ErrorMessage = "Çevrimdışı — teslimat detayı alınamadı.";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CaptureProofAsync()
    {
        if (Delivery is null)
        {
            return;
        }

        // ProofCapturePage is added in step 7.
        await Shell.Current.GoToAsync($"ProofCapturePage?id={Delivery.Id}");
    }

    [RelayCommand]
    private async Task CallRecipientAsync()
    {
        if (!string.IsNullOrWhiteSpace(Delivery?.RecipientPhone))
        {
            try
            {
                PhoneDialer.Open(Delivery.RecipientPhone);
            }
            catch
            {
                await Shell.Current.DisplayAlert("Arama", "Bu cihazda arama yapılamıyor.", "Tamam");
            }
        }
    }

    [RelayCommand]
    private async Task OpenMapAsync()
    {
        if (Delivery is { Lat: { } lat, Lng: { } lng })
        {
            await Map.OpenAsync(
                (double)lat, (double)lng,
                new MapLaunchOptions { Name = Delivery.RecipientName });
        }
    }
}
