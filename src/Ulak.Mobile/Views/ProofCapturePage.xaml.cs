using Ulak.Mobile.ViewModels;

namespace Ulak.Mobile.Views;

public partial class ProofCapturePage : ContentPage
{
    public ProofCapturePage(ProofCaptureViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // let the view model pull the rendered signature at submit time
        viewModel.SignatureProvider = () => SignaturePad.ExportPng();
    }

    private void OnClearSignature(object? sender, EventArgs e) => SignaturePad.Clear();

    private async void OnBack(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
