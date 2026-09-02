using Ulak.Mobile.ViewModels;

namespace Ulak.Mobile.Views;

public partial class ChangePasswordPage : ContentPage
{
    public ChangePasswordPage(ChangePasswordViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.Prime();
    }
}
