using Ulak.Mobile.ViewModels;

namespace Ulak.Mobile.Views;

public partial class DeliveryListPage : ContentPage
{
    private readonly DeliveryListViewModel _viewModel;

    public DeliveryListPage(DeliveryListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
