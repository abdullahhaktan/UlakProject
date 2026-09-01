using Ulak.Mobile.Theme;
using Ulak.Mobile.ViewModels;

namespace Ulak.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private void OnToggleAdvanced(object? sender, TappedEventArgs e)
    {
        AdvancedSection.IsVisible = !AdvancedSection.IsVisible;
        AdvancedCaret.Text = AdvancedSection.IsVisible ? Icon.CaretUp : Icon.CaretDown;
    }
}
