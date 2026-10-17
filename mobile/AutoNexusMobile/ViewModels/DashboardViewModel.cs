using System.Collections.ObjectModel;
using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Indicateur du tableau de bord (KpiCard du web).</summary>
public sealed record KpiItem(string Title, string Value, string Sub, string Icon, string Tone, string? Delta, bool DeltaPositive)
{
    public bool HasDelta => Delta is not null;
}

/// <summary>Intervention récente (liste mobile du tableau de bord du web).</summary>
public sealed record RecentItem(RecentInterventionDto Dto)
{
    public string Vehicle => FleetFormat.Vehicle(Dto.VehicleBrand, Dto.VehicleModel);
    public string Date => FleetFormat.Date(Dto.PlannedStartDate);
    public string People => $"{Dto.TechnicianFullName ?? "Non assigné"} · {Dto.StoreName}";
}

/// <summary>Ligne de légende d'un graphique.</summary>
public sealed record LegendItem(string Label, string Value, Color Color);

public sealed partial class DashboardViewModel : ViewModelBase
{
    // Couleurs de Dashboard.tsx.
    private static readonly Color AvailableColor = Color.FromArgb("#10B981");
    private static readonly Color InInterventionColor = Color.FromArgb("#4C6EF5");
    private static readonly Color SoldColor = Color.FromArgb("#94A3B8");
    private static readonly Color OutOfServiceColor = Color.FromArgb("#EF4444");

    private readonly FleetApi _api;

    public DashboardViewModel(FleetApi api) => _api = api;

    protected override FleetChange Watches => FleetChange.Vehicles | FleetChange.Interventions | FleetChange.Stores;

    public ObservableCollection<KpiItem> Kpis { get; } = [];

    public ObservableCollection<RecentItem> Recent { get; } = [];

    public ObservableCollection<LegendItem> FleetLegend { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<ChartSlice> _fleetSlices = [];

    [ObservableProperty]
    private IReadOnlyList<ChartSlice> _typeSlices = [];

    [ObservableProperty]
    private string _fleetTotal = "0";

    [ObservableProperty]
    private string _fleetCaption = "";

    [ObservableProperty]
    private string _typesCaption = "";

    [ObservableProperty]
    private bool _hasRecent;

    protected override async Task LoadAsync()
    {
        var summary = await _api.GetDashboardAsync();
        var v = summary.Vehicles;
        var i = summary.Interventions;
        var availPct = v.Total > 0 ? (int)Math.Round(v.Available * 100.0 / v.Total) : 0;

        Kpis.Clear();
        Kpis.Add(new("Total véhicules", FleetFormat.Number(v.Total), $"{v.InIntervention} en intervention", Resources.Icons.Car, "Slate",
            v.Sold > 0 ? $"{v.Sold} vendu{(v.Sold > 1 ? "s" : "")}" : null, false));
        Kpis.Add(new("Disponibles", FleetFormat.Number(v.Available), "Prêts à l'affectation", Resources.Icons.CircleCheck, "Emerald",
            $"{availPct} % du parc", availPct >= 60));
        Kpis.Add(new("Interventions prévues", FleetFormat.Number(i.Planned), $"{i.InProgress} en cours actuellement", Resources.Icons.Clock, "Amber",
            i.InProgress > 0 ? $"{i.InProgress} active{(i.InProgress > 1 ? "s" : "")}" : null, true));
        Kpis.Add(new("Taux de dispo.", $"{availPct} %", "Véhicules disponibles", Resources.Icons.TrendingUp, "Blue",
            availPct >= 70 ? "Bon niveau" : availPct >= 50 ? "Moyen" : "Faible", availPct >= 70));

        FleetSlices =
        [
            new("Disponible", v.Available, AvailableColor),
            new("En intervention", v.InIntervention, InInterventionColor),
            new("Vendu", v.Sold, SoldColor),
            new("Hors service", v.OutOfService, OutOfServiceColor),
        ];
        FleetLegend.Clear();
        foreach (var slice in FleetSlices.Where(s => s.Value > 0))
            FleetLegend.Add(new(slice.Label, FleetFormat.Number(slice.Value), slice.Color));
        FleetTotal = FleetFormat.Number(v.Total);
        FleetCaption = $"Statuts des {v.Total} véhicules";

        TypeSlices =
        [
            new("Maintenance", i.Maintenance, Color.FromArgb("#7C3AED")),
            new("Réparation", i.Repair, Color.FromArgb("#EA580C")),
            new("Inspection", i.Inspection, Color.FromArgb("#0D9488")),
        ];
        TypesCaption = $"Total : {i.Total} interventions";

        Recent.Clear();
        foreach (var recent in summary.RecentInterventions)
            Recent.Add(new RecentItem(recent));
        HasRecent = Recent.Count > 0;
    }

    [RelayCommand]
    private Task OpenRecentAsync(RecentItem item) =>
        Shell.Current.GoToAsync("intervention", new Dictionary<string, object> { ["id"] = item.Dto.Id });
}
