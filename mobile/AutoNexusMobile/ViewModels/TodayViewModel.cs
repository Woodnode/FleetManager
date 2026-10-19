using System.Collections.ObjectModel;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Section de la journée (en retard, en cours, à faire, terminées).</summary>
public sealed class AgendaGroup(string title, bool isOverdue, IEnumerable<InterventionItem> items) : List<InterventionItem>(items)
{
    public string Title { get; } = title;

    public bool IsOverdue { get; } = isOverdue;

    public string CountText => Count.ToString();
}

/// <summary>
/// « Ma journée », l'écran d'accueil du technicien : ses interventions du jour et celles restées
/// ouvertes, dans l'ordre où il les traite, avec Commencer / Terminer sur chaque carte.
/// </summary>
public sealed partial class TodayViewModel : ViewModelBase
{
    private readonly FleetApi _api;
    private readonly AppSession _session;
    private readonly InterventionFlow _flow;

    public TodayViewModel(FleetApi api, AppSession session, InterventionFlow flow)
    {
        _api = api;
        _session = session;
        _flow = flow;
    }

    protected override FleetChange Watches => FleetChange.Interventions;

    public ObservableCollection<AgendaGroup> Groups { get; } = [];

    [ObservableProperty]
    private string _greeting = "";

    [ObservableProperty]
    private string _dayTitle = "";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private bool _isEmpty;

    protected override async Task LoadAsync()
    {
        var now = DateTime.Now;
        var (from, to) = DayAgenda.Bounds(now);
        var interventions = await _api.GetMyInterventionsAsync(from, to);
        var agenda = DayAgenda.Build(interventions, now);

        Greeting = $"Bonjour {_session.Current?.FirstName}";
        var day = FleetFormat.LongDay(now);
        DayTitle = char.ToUpper(day[0]) + day[1..];
        Summary = (agenda.OpenCount, agenda.DoneCount) switch
        {
            (0, 0) => "Aucune intervention prévue",
            (0, var done) => $"Tout est traité · {FleetFormat.Count(done, "terminée", "terminées")}",
            (var open, 0) => FleetFormat.Count(open, "intervention à traiter", "interventions à traiter"),
            (var open, var done) => $"{FleetFormat.Count(open, "intervention à traiter", "interventions à traiter")} · {FleetFormat.Count(done, "terminée", "terminées")}",
        };

        Groups.Clear();
        foreach (var (section, items) in agenda.Sections)
            Groups.Add(new AgendaGroup(DayAgenda.Title(section), section == AgendaSection.Overdue, items.Select(i => new InterventionItem(i, now))));
        IsEmpty = Groups.Count == 0;
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

    [RelayCommand]
    private Task ScanAsync() => Shell.Current.GoToAsync("scan");
}
