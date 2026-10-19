using AutoNexusMobile.ViewModels;
using ZXing.Net.Maui;

namespace AutoNexusMobile.Views;

public partial class ScanPage : PageBase
{
    private readonly ScanViewModel _viewModel;

    public ScanPage(ScanViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        // Formats des NIV : Code 39 (étiquettes nord-américaines), Code 128, Data Matrix et QR
        // (pare-brise, certificats d'immatriculation).
        BarcodeView.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormat.Code39 | BarcodeFormat.Code128 | BarcodeFormat.DataMatrix | BarcodeFormat.QrCode | BarcodeFormat.Pdf417,
            TryHarder = true,
            AutoRotate = true,
            Multiple = false,
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.EnsureCameraPermissionAsync();
    }

    // Le clavier de la saisie manuelle ne suit pas l'utilisateur sur la fiche ouverte.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ManualEntry.Unfocus();
        _ = ManualEntry.HideSoftInputAsync(CancellationToken.None);
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;

        // Le rappel arrive sur le fil de la caméra : retour sur le fil de l'interface.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_viewModel.DetectedCommand.CanExecute(value))
                _viewModel.DetectedCommand.Execute(value);
        });
    }
}
