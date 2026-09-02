using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ulak.Mobile.Models;
using Ulak.Mobile.Services;
using Ulak.Shared.Proofs;

namespace Ulak.Mobile.ViewModels;

public sealed partial class CapturedPhoto : ObservableObject
{
    public required byte[] Bytes { get; init; }
    public required ImageSource Preview { get; init; }
}

public sealed partial class ProofCaptureViewModel : BaseViewModel, IQueryAttributable
{
    private const int MaxPhotos = 5;

    private readonly ApiClient _api;
    private readonly PhotoService _photoService;
    private readonly LocationService _locationService;
    private readonly ProofQueue _queue;

    public ProofCaptureViewModel(
        ApiClient api, PhotoService photoService, LocationService locationService, ProofQueue queue)
    {
        _api = api;
        _photoService = photoService;
        _locationService = locationService;
        _queue = queue;
    }

    /// <summary>Set by the page so the view model can pull the rendered signature at submit time.</summary>
    public Func<byte[]?>? SignatureProvider { get; set; }

    public ObservableCollection<CapturedPhoto> Photos { get; } = [];

    [ObservableProperty]
    private int _deliveryId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPickup))]
    [NotifyPropertyChangedFor(nameof(ScreenTitle))]
    [NotifyPropertyChangedFor(nameof(RecipientFieldLabel))]
    [NotifyPropertyChangedFor(nameof(FailedToggleLabel))]
    private string _proofType = "Delivery";

    public bool IsPickup => ProofType == "Pickup";

    public string ScreenTitle => IsPickup ? "Teslim alma kanıtı" : "Teslim etme kanıtı";

    public string RecipientFieldLabel => IsPickup ? "TESLİM ALINAN YER / KİŞİ" : "TESLİM ALAN KİŞİ";

    public string FailedToggleLabel => IsPickup ? "Teslim alınamadı" : "Teslim edilemedi";

    [ObservableProperty]
    private string _orderRef = string.Empty;

    [ObservableProperty]
    private string _recipientName = string.Empty;

    [ObservableProperty]
    private string _recipientSignedName = string.Empty;

    [ObservableProperty]
    private bool _isFailed;

    [ObservableProperty]
    private string _failureReason = string.Empty;

    [ObservableProperty]
    private bool _canAddPhoto = true;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("type", out var typeRaw) && typeRaw?.ToString() == "Pickup")
        {
            ProofType = "Pickup";
        }

        if (query.TryGetValue("id", out var raw) && int.TryParse(raw?.ToString(), out var id))
        {
            DeliveryId = id;
            _ = LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            var delivery = await _api.GetDeliveryAsync(DeliveryId, CancellationToken.None);
            if (delivery is not null)
            {
                OrderRef = delivery.OrderRef;
                RecipientName = delivery.RecipientName;
            }
        }
        catch
        {
            // detail is best-effort; the id is enough to submit
        }
    }

    [RelayCommand]
    private async Task AddPhotoAsync()
    {
        if (Photos.Count >= MaxPhotos)
        {
            return;
        }

        try
        {
            var bytes = await _photoService.CaptureCompressedAsync();
            if (bytes is null)
            {
                return;
            }

            Photos.Add(new CapturedPhoto
            {
                Bytes = bytes,
                Preview = ImageSource.FromStream(() => new MemoryStream(bytes)),
            });
            CanAddPhoto = Photos.Count < MaxPhotos;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Fotoğraf alınamadı: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemovePhoto(CapturedPhoto? photo)
    {
        if (photo is not null)
        {
            Photos.Remove(photo);
            CanAddPhoto = Photos.Count < MaxPhotos;
        }
    }

    [RelayCommand]
    private async Task SubmitAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();

        if (Photos.Count == 0 && !IsFailed)
        {
            ErrorMessage = "En az bir fotoğraf ekleyin.";
            return;
        }

        if (IsFailed && string.IsNullOrWhiteSpace(FailureReason))
        {
            ErrorMessage = "Başarısızlık nedeni gerekli.";
            return;
        }

        var status = IsFailed ? "Failed" : (IsPickup ? "PickedUp" : "Delivered");

        try
        {
            IsBusy = true;

            var location = await _locationService.TryGetAsync(CancellationToken.None);
            var signature = SignatureProvider?.Invoke();

            var pending = new PendingProof
            {
                ClientUuid = Guid.NewGuid(),
                DeliveryId = DeliveryId,
                OrderRef = OrderRef,
                ProofType = ProofType,
                Status = status,
                FailureReason = IsFailed ? FailureReason.Trim() : null,
                RecipientSignedName = string.IsNullOrWhiteSpace(RecipientSignedName) ? null : RecipientSignedName.Trim(),
                CapturedLat = location?.Latitude,
                CapturedLng = location?.Longitude,
                CapturedAt = DateTimeOffset.UtcNow,
            };

            await _queue.EnqueueAsync(pending, Photos.Select(p => p.Bytes).ToList(), signature);

            await Shell.Current.DisplayAlert(
                "Kaydedildi",
                IsPickup
                    ? "Teslim alma kanıtı kaydedildi ve gönderilmek üzere sıraya alındı."
                    : "Teslim etme kanıtı kaydedildi ve gönderilmek üzere sıraya alındı.",
                "Tamam");

            await Shell.Current.GoToAsync("../..");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Kaydedilemedi: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
