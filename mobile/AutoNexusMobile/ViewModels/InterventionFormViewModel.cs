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
/// Planification d'une intervention (modale « Nouvelle intervention » du web). Le véhicule est
/// choisi parmi les disponibles, ce qui fixe l'enseigne et la liste des techniciens. Les dates
/// passent par des puces (jour, heure, durée) plutôt que par les calendriers natifs d'Android.
/// </summary>
public sealed partial class InterventionFormViewModel : ViewModelBase, IQueryAttributable
{
    private static readonly (string Label, TimeSpan Duration)[] Durations =
    [
        ("1 h", TimeSpan.FromHours(1)),
        ("2 h", TimeSpan.FromHours(2)),
        ("4 h", TimeSpan.FromHours(4)),
        ("1 jour", TimeSpan.FromHours(9)),
        ("2 jours", TimeSpan.FromDays(1) + TimeSpan.FromHours(9)),
        ("3 jours", TimeSpan.FromDays(2) + TimeSpan.FromHours(9)),
    ];

    private readonly FleetApi _api;
    private CancellationTokenSource? _searchDelay;
    private Guid? _technicianId;
    private InterventionType _type = InterventionType.Maintenance;
    private DateTime _day = DateTime.Today;
    private TimeSpan _time = TimeSpan.FromHours(9);
    private TimeSpan _duration = TimeSpan.FromHours(2);

    public InterventionFormViewModel(FleetApi api)
    {
        _api = api;

        TypeChoices = Choice.Group(
            Enum.GetValues<InterventionType>().Select(t => (FleetFormat.Label(t), t)), _type, t => _type = t);
        DayChoices = Choice.Group(
            Enumerable.Range(0, 14).Select(offset =>
            {
                var day = DateTime.Today.AddDays(offset);
                var label = offset switch { 0 => "Aujourd'hui", 1 => "Demain", _ => day.ToString("ddd d MMM", FleetFormat.Culture) };
                return (label, day);
            }), _day, day => { _day = day; UpdateSummary(); });
        TimeChoices = Choice.Group(
            Enumerable.Range(14, 21).Select(half => TimeSpan.FromMinutes(half * 30)).Select(t => (t.ToString(@"hh\:mm"), t)),
            _time, time => { _time = time; UpdateSummary(); });
        DurationChoices = Choice.Group(Durations.Select(d => (d.Label, d.Duration)), _duration, d => { _duration = d; UpdateSummary(); });
        UpdateSummary();
    }

    public List<Choice> TypeChoices { get; }
    public List<Choice> DayChoices { get; }
    public List<Choice> TimeChoices { get; }
    public List<Choice> DurationChoices { get; }

    public ObservableCollection<VehicleItem> VehicleResults { get; } = [];

    [ObservableProperty] private List<Choice> _technicianChoices = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVehicle), nameof(VehicleName))]
    private VehicleDto? _vehicle;

    public bool HasVehicle => Vehicle is not null;

    public string VehicleName => Vehicle is null ? "" : FleetFormat.Vehicle(Vehicle.Brand, Vehicle.Model);

    [ObservableProperty] private string _vehicleSearch = "";
    [ObservableProperty] private string _comment = "";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private bool _hasNoTechnician;

    [ObservableProperty] private string? _vehicleError;
    [ObservableProperty] private string? _technicianError;
    [ObservableProperty] private string? _dateError;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("vehicle", out var value) && value is VehicleDto vehicle)
            Vehicle = vehicle;
    }

    protected override async Task LoadAsync()
    {
        if (Vehicle is not null)
            await LoadTechniciansAsync();
        else
            await SearchVehiclesAsync();
    }

    partial void OnVehicleSearchChanged(string value)
    {
        _searchDelay?.Cancel();
        _searchDelay = new CancellationTokenSource();
        var token = _searchDelay.Token;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                await SearchVehiclesAsync();
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private async Task SearchVehiclesAsync()
    {
        try
        {
            var result = await _api.GetVehiclesAsync(1, 10, VehicleSearch, VehicleStatus.Available);
            VehicleResults.Clear();
            foreach (var vehicle in result.Items)
                VehicleResults.Add(new VehicleItem(vehicle));
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task PickVehicleAsync(VehicleItem item)
    {
        Vehicle = item.Dto;
        VehicleError = null;
        await LoadTechniciansAsync();
    }

    [RelayCommand]
    private async Task ChangeVehicleAsync()
    {
        Vehicle = null;
        TechnicianChoices = [];
        _technicianId = null;
        await SearchVehiclesAsync();
    }

    private async Task LoadTechniciansAsync()
    {
        if (Vehicle is null) return;

        try
        {
            var technicians = await _api.GetTechniciansAsync(Vehicle.StoreId);
            _technicianId = technicians.Count == 1 ? technicians[0].Id : null;
            TechnicianChoices = Choice.Group(technicians.Select(t => (t.FullName, (Guid?)t.Id)), _technicianId, id =>
            {
                _technicianId = id;
                TechnicianError = null;
            });
            HasNoTechnician = technicians.Count == 0;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void UpdateSummary()
    {
        var start = _day.Date + _time;
        var end = start + _duration;
        Summary = FleetFormat.Window(start, end);
        DateError = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var start = DateTime.SpecifyKind(_day.Date + _time, DateTimeKind.Unspecified);
        var end = start + _duration;

        VehicleError = Vehicle is null ? "Veuillez sélectionner un véhicule" : null;
        TechnicianError = _technicianId is null ? "Veuillez sélectionner un technicien" : null;
        DateError = end <= start ? "La date de fin doit être après la date de début" : null;
        if (VehicleError is not null || TechnicianError is not null || DateError is not null)
            return;

        InterventionDto? created = null;
        var ok = await RunAsync(async () => created = await _api.CreateInterventionAsync(new CreateInterventionRequest(
            Vehicle!.Id, Vehicle.StoreId, _technicianId!.Value, _type, start, end,
            string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim())));

        if (!ok || created is null)
            return;

        Broadcast(FleetChange.Interventions | FleetChange.Vehicles);
        await Shell.Current.GoToAsync("..");
        Toasts.Show("Intervention créée");
    }
}
