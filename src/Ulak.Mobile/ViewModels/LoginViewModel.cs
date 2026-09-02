using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ulak.Mobile.Services;
using Ulak.Mobile.Views;

namespace Ulak.Mobile.ViewModels;

public sealed partial class LoginViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly PendingCredential _pending;

    public LoginViewModel(ApiClient api, PendingCredential pending)
    {
        _api = api;
        _pending = pending;
        _apiBaseUrl = AppConfig.ApiBaseUrl;
    }

    [ObservableProperty]
    private string _phone = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _apiBaseUrl;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (IsBusy)
        {
            return;
        }

        ClearError();

        if (string.IsNullOrWhiteSpace(Phone) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Telefon ve şifre gerekli.";
            return;
        }

        AppConfig.SetApiBaseUrl(ApiBaseUrl);

        try
        {
            IsBusy = true;
            var auth = await _api.LoginAsync(Phone.Trim(), Password, CancellationToken.None);

            if (auth.User.MustChangePassword)
            {
                _pending.Set(Phone.Trim(), Password);
                await Shell.Current.GoToAsync($"//{nameof(ChangePasswordPage)}");
                return;
            }

            await Shell.Current.GoToAsync($"//{nameof(DeliveryListPage)}");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Sunucuya ulaşılamıyor. API adresini kontrol edin.";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
