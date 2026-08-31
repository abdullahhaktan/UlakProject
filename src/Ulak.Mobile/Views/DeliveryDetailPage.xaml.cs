using Ulak.Mobile.ViewModels;

namespace Ulak.Mobile.Views;

public partial class DeliveryDetailPage : ContentPage
{
    public DeliveryDetailPage(DeliveryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
