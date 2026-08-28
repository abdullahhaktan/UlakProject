using LinkLogistics.Mobile.ViewModels;

namespace LinkLogistics.Mobile.Views;

public partial class DeliveryDetailPage : ContentPage
{
    public DeliveryDetailPage(DeliveryDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
