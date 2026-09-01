using Ulak.Mobile.ViewModels;

namespace Ulak.Mobile.Views;

public partial class DeliveryDetailPage : ContentPage
{
    public DeliveryDetailPage(DeliveryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    private async void OnBack(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
