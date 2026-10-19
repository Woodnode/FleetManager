using System.Collections.ObjectModel;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Historique complet d'un véhicule archivé (modale Historique du web), avec restauration.</summary>
public sealed partial class ArchivedVehicleViewModel : ViewModelBase, IQueryAttributable
{
    private readonly FleetApi _api;
    private Guid _id;

    public ArchivedVehicleViewModel(FleetApi api) => _api = api;

    protected override FleetChange Watches => FleetChange.Vehicles;

    public ObservableCollection<InterventionItem> Interventions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Item), nameof(HasVehicle))]
    private ArchivedVehicleDto? _vehicle;

    public ArchivedItem? Item => Vehicle is null ? null : new ArchivedItem(Vehicle);

    public bool HasVehicle => Vehicle is not null;

    [ObservableProperty] private bool _hasInterventions;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid guid)
            _id = guid;
    }

    protected override async Task LoadAsync()
    {
        var history = await _api.GetArchivedHistoryAsync(_id);
        Vehicle = history.Vehicle;
        Interventions.Clear();
        foreach (var intervention in history.Interventions)
            Interventions.Add(new InterventionItem(intervention));
        HasInterventions = Interventions.Count > 0;
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (Vehicle is null) return;

        if (await ArchiveRestore.RunAsync(_api, Vehicle) is { } restored)
        {
            await Shell.Current.GoToAsync("..");
            await Shell.Current.GoToAsync("//main/vehicles/vehicle", new Dictionary<string, object> { ["id"] = restored.Id });
        }
    }

    // Les cartes d'intervention partagées appellent ces commandes ; un véhicule archivé est en lecture seule.
    [RelayCommand]
    private Task OpenAsync(InterventionItem item) => Task.CompletedTask;

    [RelayCommand]
    private Task QuickActionAsync(InterventionItem item) => Task.CompletedTask;
}
