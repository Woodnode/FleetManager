using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>
/// Ajout ou modification d'un véhicule (modales du web), avec les règles de schemas/vehicle.ts.
/// À l'ajout, le NIV peut venir du scan ; s'il appartient à un véhicule archivé, l'API le
/// signale et la fiche propose de le restaurer, comme le web.
/// </summary>
public sealed partial class VehicleFormViewModel : ViewModelBase, IQueryAttributable
{
    private static readonly int MaxYear = DateTime.Now.Year + 1;

    private readonly FleetApi _api;
    private readonly AppSession _session;
    private VehicleDto? _editing;
    private Guid? _storeId;

    public VehicleFormViewModel(FleetApi api, AppSession session)
    {
        _api = api;
        _session = session;
    }

    public bool IsEdit => _editing is not null;

    public string Title => IsEdit ? $"Modifier : {FleetFormat.Vehicle(_editing!.Brand, _editing.Model)}" : "Ajouter un véhicule";

    public string SubmitLabel => IsEdit ? "Enregistrer" : "Ajouter le véhicule";

    public bool CanPickStore => RolePermissions.CanPickStore(_session.Role);

    [ObservableProperty] private string _vin = "";
    [ObservableProperty] private string _brand = "";
    [ObservableProperty] private string _model = "";
    [ObservableProperty] private string _year = DateTime.Now.Year.ToString();
    [ObservableProperty] private string _mileage = "0";
    [ObservableProperty] private string _storeName = "";
    [ObservableProperty] private List<Choice> _storeChoices = [];

    [ObservableProperty] private string? _vinError;
    [ObservableProperty] private string? _brandError;
    [ObservableProperty] private string? _modelError;
    [ObservableProperty] private string? _yearError;
    [ObservableProperty] private string? _mileageError;
    [ObservableProperty] private string? _storeError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConflict))]
    private ArchivedVinConflict? _conflict;

    public bool HasConflict => Conflict is not null;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("vehicle", out var value) && value is VehicleDto vehicle)
        {
            _editing = vehicle;
            Vin = vehicle.Vin;
            Brand = vehicle.Brand;
            Model = vehicle.Model;
            Year = vehicle.Year.ToString();
            Mileage = vehicle.Mileage.ToString();
            _storeId = vehicle.StoreId;
            StoreName = vehicle.StoreName ?? "";
        }
        else if (query.TryGetValue("vin", out var vin) && vin is string scanned)
        {
            Vin = scanned;
        }

        OnPropertyChanged(nameof(IsEdit));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SubmitLabel));
    }

    protected override async Task LoadAsync()
    {
        var stores = await _api.GetStoresAsync();
        _storeId ??= _session.Current?.StoreId;
        StoreName = stores.FirstOrDefault(s => s.Id == _storeId)?.Name ?? StoreName;
        StoreChoices = Choice.Group(stores.Select(s => (s.Name, (Guid?)s.Id)), _storeId, id =>
        {
            _storeId = id;
            StoreError = null;
        });
        OnPropertyChanged(nameof(CanPickStore));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Validate(out var year, out var mileage))
            return;

        Conflict = null;
        VehicleDto? saved = null;
        var ok = await RunAsync(async () =>
        {
            try
            {
                saved = IsEdit
                    ? await _api.UpdateVehicleAsync(_editing!.Id, new UpdateVehicleRequest(Brand.Trim(), Model.Trim(), year, mileage, _storeId!.Value))
                    : await _api.CreateVehicleAsync(new CreateVehicleRequest(VinReader.Normalize(Vin), Brand.Trim(), Model.Trim(), year, mileage, _storeId!.Value));
            }
            catch (ApiException ex) when (ex.ArchivedVinConflict is not null)
            {
                Conflict = ex.ArchivedVinConflict;
                throw;
            }
        });

        if (!ok || saved is null)
        {
            if (HasConflict) ErrorMessage = null;
            return;
        }

        Broadcast(FleetChange.Vehicles);
        await Shell.Current.GoToAsync("..");
        Toasts.Show(IsEdit ? "Véhicule modifié" : "Véhicule ajouté");
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (Conflict is null) return;

        VehicleDto? restored = null;
        if (await RunAsync(async () => restored = await _api.RestoreVehicleAsync(Conflict.ArchivedVehicleId)) && restored is not null)
        {
            Broadcast(FleetChange.Vehicles);
            await Shell.Current.GoToAsync("..");
            await Shell.Current.GoToAsync("vehicle", new Dictionary<string, object> { ["id"] = restored.Id });
            Toasts.Show("Véhicule restauré avec son historique");
        }
    }

    [RelayCommand]
    private Task ScanAsync() => Shell.Current.GoToAsync("scan", new Dictionary<string, object> { ["mode"] = "fill" });

    /// <summary>NIV rendu par le scan lancé depuis ce formulaire.</summary>
    public void UseScannedVin(string vin) => Vin = vin;

    private bool Validate(out int year, out int mileage)
    {
        var vin = VinReader.Normalize(Vin);
        VinError = IsEdit ? null
            : vin.Length != VinReader.Length ? "Le VIN doit faire exactement 17 caractères"
            : !VinReader.IsValid(vin) ? "Le VIN ne peut contenir que des lettres et chiffres (sauf I, O, Q)"
            : null;
        BrandError = string.IsNullOrWhiteSpace(Brand) ? "Marque requise" : Brand.Trim().Length > 100 ? "La marque ne peut pas dépasser 100 caractères" : null;
        ModelError = string.IsNullOrWhiteSpace(Model) ? "Modèle requis" : Model.Trim().Length > 100 ? "Le modèle ne peut pas dépasser 100 caractères" : null;

        YearError = !int.TryParse(Year, out year) ? "Année invalide"
            : year < 1990 ? "Année minimum: 1990"
            : year > MaxYear ? $"Année maximum: {MaxYear}"
            : null;
        MileageError = !int.TryParse(Mileage.Replace(" ", "").Replace(" ", ""), out mileage) || mileage < 0 ? "Kilométrage invalide"
            : IsEdit && mileage < _editing!.Mileage ? $"Le kilométrage ne peut pas descendre sous {FleetFormat.Kilometers(_editing.Mileage)}"
            : null;
        StoreError = _storeId is null ? "Veuillez sélectionner une enseigne" : null;

        return VinError is null && BrandError is null && ModelError is null && YearError is null && MileageError is null && StoreError is null;
    }
}
