using System.Collections.ObjectModel;
using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>
/// Fiche d'un véhicule (propre au mobile, ouverte par la liste ou le scan du NIV) : identité,
/// statut, kilométrage, enseigne, et ses interventions. Les actions de la carte du web (changer le
/// statut, modifier, supprimer) y sont regroupées, avec la planification d'une intervention.
/// </summary>
public sealed partial class VehicleDetailViewModel : ViewModelBase, IQueryAttributable
{
    private readonly FleetApi _api;
    private readonly AppSession _session;
    private readonly InterventionFlow _flow;
    private Guid _id;

    public VehicleDetailViewModel(FleetApi api, AppSession session, InterventionFlow flow)
    {
        _api = api;
        _session = session;
        _flow = flow;
    }

    protected override FleetChange Watches => FleetChange.Vehicles | FleetChange.Interventions;

    public ObservableCollection<InterventionItem> Interventions { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name), nameof(Mileage), nameof(Year), nameof(HasVehicle))]
    private VehicleDto? _vehicle;

    public bool HasVehicle => Vehicle is not null;

    public string Name => Vehicle is null ? "" : FleetFormat.Vehicle(Vehicle.Brand, Vehicle.Model);

    public string Mileage => Vehicle is null ? "" : FleetFormat.Kilometers(Vehicle.Mileage);

    public string Year => Vehicle?.Year.ToString() ?? "";

    public bool CanManage => RolePermissions.CanManageVehicles(_session.Role);

    public bool CanPlan => RolePermissions.CanPlanInterventions(_session.Role) && Vehicle?.Status is VehicleStatus.Available;

    [ObservableProperty]
    private bool _hasInterventions;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid guid)
            _id = guid;
    }

    protected override async Task LoadAsync()
    {
        var vehicleTask = _api.GetVehicleAsync(_id);
        var historyTask = _api.GetVehicleInterventionsAsync(_id);
        Vehicle = await vehicleTask;

        var now = DateTime.Now;
        Interventions.Clear();
        foreach (var intervention in (await historyTask).OrderByDescending(i => i.PlannedStartDate))
            Interventions.Add(new InterventionItem(intervention, now, _session.CanActOn(intervention)));
        HasInterventions = Interventions.Count > 0;
        OnPropertyChanged(nameof(CanManage));
        OnPropertyChanged(nameof(CanPlan));
    }

    [RelayCommand]
    private async Task ChangeStatusAsync()
    {
        if (Vehicle is null) return;

        var options = Enum.GetValues<VehicleStatus>().Select(status => new SheetOption(
            FleetFormat.Label(status), status, Tone: StatusBadge.Tone(status), IsCurrent: status == Vehicle.Status,
            Detail: status == VehicleStatus.Sold ? "Définitif : un véhicule vendu ne change plus de statut" : null));
        var picked = await OptionsSheet.PickAsync<VehicleStatus?>("Changer le statut", Name, options);
        if (picked is not { } status) return;

        if (await RunAsync(async () => Vehicle = await _api.ChangeVehicleStatusAsync(Vehicle.Id, status)))
        {
            Toasts.Show("Statut mis à jour");
            Broadcast(FleetChange.Vehicles);
            OnPropertyChanged(nameof(CanPlan));
        }
        else
        {
            Toasts.Show(ErrorMessage ?? "Changement de statut refusé", error: true);
        }
    }

    [RelayCommand]
    private Task EditAsync() =>
        Vehicle is null ? Task.CompletedTask : Shell.Current.GoToAsync("vehicleform", new Dictionary<string, object> { ["vehicle"] = Vehicle });

    [RelayCommand]
    private Task PlanAsync() =>
        Vehicle is null ? Task.CompletedTask : Shell.Current.GoToAsync("interventionform", new Dictionary<string, object> { ["vehicle"] = Vehicle });

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Vehicle is null) return;

        var confirmed = await ConfirmSheet.AskAsync("Supprimer le véhicule",
            $"{Name} ({Vehicle.Vin}) sera retiré du parc et placé dans les archives, avec son historique d'interventions.",
            "Supprimer", destructive: true);
        if (!confirmed) return;

        if (await RunAsync(() => _api.DeleteVehicleAsync(Vehicle.Id)))
        {
            Broadcast(FleetChange.Vehicles);
            await Shell.Current.GoToAsync("..");
            Toasts.Show("Véhicule supprimé et placé dans les archives");
        }
        else
        {
            Toasts.Show(ErrorMessage ?? "Erreur lors de la suppression", error: true);
        }
    }

    [RelayCommand]
    private Task OpenAsync(InterventionItem item) =>
        Shell.Current.GoToAsync("intervention", new Dictionary<string, object> { ["id"] = item.Id });

    [RelayCommand]
    private async Task QuickActionAsync(InterventionItem item)
    {
        if (item.NextAction is { } target && await _flow.ChangeStatusAsync(item.Dto, target) is not null)
            await ReloadAsync(showSkeleton: false);
    }
}
