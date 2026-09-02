using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ulak.Mobile.Services;
using Ulak.Mobile.Views;

namespace Ulak.Mobile.ViewModels;

public sealed partial class ChangePasswordViewModel : BaseViewModel
{
    private readonly ApiClient _api;
    private readonly TokenStore _tokenStore;
    private readonly PendingCredential _pending;

    public ChangePasswordViewModel(ApiClient api, TokenStore tokenStore, PendingCredential pending)
    {
        _api = api;
        _tokenStore = tokenStore;
        _pending = pending;
    }

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    /// <summary>True when we carried the temp password over from login — the field is then hidden.</summary>
    [ObservableProperty]
    private bool _currentKnown;

    public bool CurrentEditable => !CurrentKnown;

    partial void OnCurrentKnownChanged(bool value) => OnPropertyChanged(nameof(CurrentEditable));

    /// <summary>Called by the page on creation: pre-fills the current password if login handed it over.</summary>
    public void Prime()
    {
        if (!string.IsNullOrEmpty(_pending.CurrentPassword))
        {
            CurrentPassword = _pending.CurrentPassword!;
            CurrentKnown = true;
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

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "Mevcut şifre gerekli.";
            return;
        }

        if (NewPassword.Length < 6)
        {
            ErrorMessage = "Yeni şifre en az 6 karakter olmalı.";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "Yeni şifreler eşleşmiyor.";
            return;
        }

        try
        {
            IsBusy = true;
            await _api.ChangePasswordAsync(CurrentPassword, NewPassword, CancellationToken.None);
            await _tokenStore.ClearMustChangePasswordAsync();
            _pending.Clear();
            await Shell.Current.GoToAsync($"//{nameof(DeliveryListPage)}");
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.StatusCode == 400 ? "Mevcut şifre hatalı." : ex.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Sunucuya ulaşılamıyor.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        _tokenStore.Clear();
        _pending.Clear();
        await Shell.Current.GoToAsync($"//{nameof(LoginPage)}");
    }
}
