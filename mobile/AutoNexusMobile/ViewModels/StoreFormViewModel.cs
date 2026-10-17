using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Ajout ou modification d'une enseigne (administrateur), règles de schemas/store.ts.</summary>
public sealed partial class StoreFormViewModel : ViewModelBase, IQueryAttributable
{
    private readonly FleetApi _api;
    private StoreDto? _editing;

    public StoreFormViewModel(FleetApi api) => _api = api;

    public string Title => _editing is null ? "Ajouter une enseigne" : $"Modifier : {_editing.Name}";

    public string SubmitLabel => _editing is null ? "Ajouter l'enseigne" : "Enregistrer";

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _address = "";
    [ObservableProperty] private string _postalCode = "";
    [ObservableProperty] private string _city = "";

    [ObservableProperty] private string? _nameError;
    [ObservableProperty] private string? _addressError;
    [ObservableProperty] private string? _postalCodeError;
    [ObservableProperty] private string? _cityError;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("store", out var value) && value is StoreDto store)
        {
            _editing = store;
            Name = store.Name;
            Address = store.Address;
            PostalCode = store.PostalCode;
            City = store.City;
        }

        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(SubmitLabel));
    }

    protected override Task LoadAsync() => Task.CompletedTask;

    [RelayCommand]
    private async Task SaveAsync()
    {
        NameError = string.IsNullOrWhiteSpace(Name) ? "Le nom est requis" : Name.Trim().Length > 100 ? "Le nom est trop long" : null;
        AddressError = Address.Trim().Length > 200 ? "L'adresse est trop longue" : null;
        PostalCodeError = PostalCode.Trim().Length > 10 ? "Le code postal est trop long" : null;
        CityError = string.IsNullOrWhiteSpace(City) ? "La ville est requise" : City.Trim().Length > 100 ? "La ville est trop longue" : null;
        if (NameError is not null || AddressError is not null || PostalCodeError is not null || CityError is not null)
            return;

        var request = new StoreRequest(Name.Trim(), Address.Trim(), PostalCode.Trim(), City.Trim());
        var ok = await RunAsync(async () =>
        {
            if (_editing is null)
                await _api.CreateStoreAsync(request);
            else
                await _api.UpdateStoreAsync(_editing.Id, request);
        });
        if (!ok) return;

        Broadcast(FleetChange.Stores);
        await Shell.Current.GoToAsync("..");
        Toasts.Show(_editing is null ? "Enseigne ajoutée" : "Enseigne modifiée");
    }
}
