using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>
/// Fiche d'une intervention, le cœur du parcours technicien : véhicule, type, statut, créneau,
/// technicien, commentaire ; boutons Commencer, Terminer (commentaire et kilométrage) et Annuler
/// selon le statut. Mise à jour en direct si quelqu'un d'autre la modifie.
/// </summary>
public sealed partial class InterventionDetailViewModel : ViewModelBase, IQueryAttributable
{
    private readonly FleetApi _api;
    private readonly InterventionFlow _flow;
    private Guid _id;

    public InterventionDetailViewModel(FleetApi api, InterventionFlow flow)
    {
        _api = api;
        _flow = flow;
    }

    protected override FleetChange Watches => FleetChange.Interventions;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIntervention), nameof(Vehicle), nameof(Window), nameof(ActualEnd), nameof(HasActualEnd),
        nameof(CanStart), nameof(CanComplete), nameof(CanCancel), nameof(IsOverdue), nameof(Comment), nameof(HasComment))]
    private InterventionDto? _intervention;

    public bool HasIntervention => Intervention is not null;

    public string Vehicle => Intervention is null ? "" : FleetFormat.Vehicle(Intervention.VehicleBrand, Intervention.VehicleModel);

    public string Window => Intervention is null ? "" : FleetFormat.Window(Intervention.PlannedStartDate, Intervention.PlannedEndDate);

    public string ActualEnd => Intervention?.ActualEndDate is { } end ? $"{FleetFormat.Date(end)} {FleetFormat.Time(end)}" : "";

    public bool HasActualEnd => Intervention?.ActualEndDate is not null;

    public string? Comment => string.IsNullOrWhiteSpace(Intervention?.Comment) ? null : Intervention.Comment;

    public bool HasComment => Comment is not null;

    public bool IsOverdue => Intervention is { Status: InterventionStatus.Planned or InterventionStatus.InProgress } i && i.PlannedEndDate < DateTime.Now;

    public bool CanStart => Intervention is { Status: InterventionStatus.Planned };

    public bool CanComplete => Intervention is { Status: InterventionStatus.InProgress };

    public bool CanCancel => Intervention is { Status: InterventionStatus.Planned or InterventionStatus.InProgress };

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var id) && id is Guid guid)
            _id = guid;
    }

    protected override async Task LoadAsync() => Intervention = await _api.GetInterventionAsync(_id);

    [RelayCommand]
    private Task StartAsync() => ChangeAsync(InterventionStatus.InProgress);

    [RelayCommand]
    private Task CompleteAsync() => ChangeAsync(InterventionStatus.Completed);

    [RelayCommand]
    private Task CancelAsync() => ChangeAsync(InterventionStatus.Cancelled);

    [RelayCommand]
    private Task OpenVehicleAsync() =>
        Intervention is null ? Task.CompletedTask : Shell.Current.GoToAsync("vehicle", new Dictionary<string, object> { ["id"] = Intervention.VehicleId });

    private async Task ChangeAsync(InterventionStatus target)
    {
        if (Intervention is null || IsBusy) return;

        IsBusy = true;
        try
        {
            if (await _flow.ChangeStatusAsync(Intervention, target) is { } updated)
                Intervention = updated;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
