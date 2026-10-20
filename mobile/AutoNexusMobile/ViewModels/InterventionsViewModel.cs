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
/// Liste des interventions (Interventions.tsx) : filtres de statut et de type, cartes avec
/// l'action suivante, création pour l'administrateur et le gérant.
/// </summary>
public sealed partial class InterventionsViewModel : ViewModelBase
{
    private const int PageSize = 20;

    private readonly FleetApi _api;
    private readonly AppSession _session;
    private readonly InterventionFlow _flow;
    private int _page;
    private bool _hasMore;
    private InterventionStatus? _status;
    private InterventionType? _type;

    public InterventionsViewModel(FleetApi api, AppSession session, InterventionFlow flow)
    {
        _api = api;
        _session = session;
        _flow = flow;

        StatusChoices = Choice.Group(
            new (string, InterventionStatus?)[]
            {
                ("Tous les statuts", null),
                (FleetFormat.Label(InterventionStatus.Planned), InterventionStatus.Planned),
                (FleetFormat.Label(InterventionStatus.InProgress), InterventionStatus.InProgress),
                (FleetFormat.Label(InterventionStatus.Completed), InterventionStatus.Completed),
                (FleetFormat.Label(InterventionStatus.Cancelled), InterventionStatus.Cancelled),
            }, null, async status => { _status = status; await ReloadAsync(showSkeleton: true); });

        TypeChoices = Choice.Group(
            new (string, InterventionType?)[]
            {
                ("Tous les types", null),
                (FleetFormat.Label(InterventionType.Maintenance), InterventionType.Maintenance),
                (FleetFormat.Label(InterventionType.Repair), InterventionType.Repair),
                (FleetFormat.Label(InterventionType.Inspection), InterventionType.Inspection),
                (FleetFormat.Label(InterventionType.Other), InterventionType.Other),
            }, null, async type => { _type = type; await ReloadAsync(showSkeleton: true); });
    }

    protected override FleetChange Watches => FleetChange.Interventions | FleetChange.Stores;

    public ObservableCollection<InterventionItem> Interventions { get; } = [];

    public List<Choice> StatusChoices { get; }

    public List<Choice> TypeChoices { get; }

    public bool CanCreate => RolePermissions.CanPlanInterventions(_session.Role);

    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isLoadingMore;

    protected override async Task LoadAsync()
    {
        var result = await _api.GetInterventionsAsync(1, PageSize, _status, _type);
        _page = 1;
        _hasMore = result.HasNextPage;
        var now = DateTime.Now;
        Interventions.Clear();
        foreach (var intervention in result.Items)
            Interventions.Add(new InterventionItem(intervention, now, _session.CanActOn(intervention)));

        Subtitle = $"{FleetFormat.Count(result.TotalCount, "intervention enregistrée", "interventions enregistrées")}";
        IsEmpty = Interventions.Count == 0;
        OnPropertyChanged(nameof(CanCreate));
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_hasMore || IsLoadingMore || IsLoading)
            return;

        IsLoadingMore = true;
        try
        {
            var result = await _api.GetInterventionsAsync(_page + 1, PageSize, _status, _type);
            _page = result.Page;
            _hasMore = result.HasNextPage;
            var now = DateTime.Now;
            foreach (var intervention in result.Items.Where(i => Interventions.All(existing => existing.Id != i.Id)))
                Interventions.Add(new InterventionItem(intervention, now, _session.CanActOn(intervention)));
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingMore = false;
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

    [RelayCommand]
    private Task CreateAsync() => Shell.Current.GoToAsync("interventionform");
}
