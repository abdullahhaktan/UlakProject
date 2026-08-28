using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LinkLogistics.Mobile.Services;
using LinkLogistics.Mobile.Views;

namespace LinkLogistics.Mobile.ViewModels;

public sealed partial class LoginViewModel : BaseViewModel
{
    private readonly ApiClient _api;

    public LoginViewModel(ApiClient api)
    {
        _api = api;
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
            await _api.LoginAsync(Phone.Trim(), Password, CancellationToken.None);
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
