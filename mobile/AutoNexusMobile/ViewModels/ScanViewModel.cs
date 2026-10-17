using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>
/// Scan du NIV (code-barres du montant de porte ou du pare-brise, QR de la carte grise) : le code
/// lu ouvre la fiche du véhicule. Saisie manuelle pour une étiquette illisible ou une caméra
/// refusée. Ouvert depuis le formulaire d'ajout, le scan remplit le champ NIV et revient.
/// </summary>
public sealed partial class ScanViewModel : ViewModelBase, IQueryAttributable
{
    private readonly FleetApi _api;
    private readonly Services.AppSession _session;
    private bool _fillMode;
    private string? _lastCode;

    public ScanViewModel(FleetApi api, Services.AppSession session)
    {
        _api = api;
        _session = session;
    }

    [ObservableProperty] private string _manualVin = "";
    [ObservableProperty] private bool _isCameraAllowed = true;
    [ObservableProperty] private bool _isDetecting = true;

    /// <summary>NIV valide mais inconnu du parc : l'administrateur et le gérant peuvent l'ajouter.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnknownVin), nameof(CanAddUnknown))]
    private string? _unknownVin;

    public bool HasUnknownVin => UnknownVin is not null;

    public bool CanAddUnknown => HasUnknownVin && RolePermissions.CanManageVehicles(_session.Role);

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _fillMode = query.TryGetValue("mode", out var mode) && mode as string == "fill";

    protected override Task LoadAsync() => Task.CompletedTask;

    public async Task EnsureCameraPermissionAsync()
    {
        var status = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<Microsoft.Maui.ApplicationModel.Permissions.Camera>();
        if (status == PermissionStatus.Granted)
        {
            IsCameraAllowed = true;
            return;
        }

        status = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<Microsoft.Maui.ApplicationModel.Permissions.Camera>();
        // La vue caméra a tenté de démarrer avant l'autorisation : on la recrée pour qu'elle s'ouvre.
        IsCameraAllowed = false;
        await Task.Delay(100);
        IsCameraAllowed = status == PermissionStatus.Granted;
    }

    [RelayCommand]
    private async Task DetectedAsync(string raw)
    {
        // La caméra relit le même code plusieurs fois par seconde : un seul traitement par code.
        if (IsBusy || raw == _lastCode) return;
        _lastCode = raw;

        var vin = VinReader.FromScan(raw);
        if (vin is null)
        {
            ErrorMessage = "Ce code ne contient pas de NIV. Visez l'étiquette du montant de porte ou le pare-brise.";
            return;
        }

        await OpenAsync(vin);
    }

    [RelayCommand]
    private async Task SearchManualAsync()
    {
        var vin = VinReader.Normalize(ManualVin);
        if (!VinReader.IsValid(vin))
        {
            ErrorMessage = "Le VIN doit faire 17 caractères, lettres et chiffres sauf I, O et Q.";
            return;
        }

        await OpenAsync(vin);
    }

    [RelayCommand]
    private async Task AddUnknownAsync()
    {
        if (UnknownVin is null) return;
        await Shell.Current.GoToAsync("../vehicleform", new Dictionary<string, object> { ["vin"] = UnknownVin });
    }

    [RelayCommand]
    private void ScanAgain()
    {
        UnknownVin = null;
        ErrorMessage = null;
        _lastCode = null;
        IsDetecting = true;
    }

    [RelayCommand]
    private static void OpenSettings() => AppInfo.Current.ShowSettingsUI();

    private async Task OpenAsync(string vin)
    {
        UnknownVin = null;

        if (_fillMode)
        {
            IsDetecting = false;
            var stack = Shell.Current.Navigation.NavigationStack;
            if (stack.Count >= 2 && stack[^2]?.BindingContext is VehicleFormViewModel form)
                form.UseScannedVin(vin);
            await Shell.Current.GoToAsync("..");
            return;
        }

        VehicleDto? vehicle = null;
        if (!await RunAsync(async () => vehicle = await _api.FindVehicleByVinAsync(vin)))
        {
            _lastCode = null;
            return;
        }

        if (vehicle is null)
        {
            UnknownVin = vin;
            IsDetecting = false;
            return;
        }

        IsDetecting = false;
        // Petit retour au toucher quand le NIV est reconnu ; sans vibreur, on s'en passe.
        try { HapticFeedback.Default.Perform(HapticFeedbackType.Click); } catch (Exception) { }
        await Shell.Current.GoToAsync("../vehicle", new Dictionary<string, object> { ["id"] = vehicle.Id });
    }
}
