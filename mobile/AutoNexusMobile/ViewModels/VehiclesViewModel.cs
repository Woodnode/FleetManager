using System.Collections.ObjectModel;
using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Véhicule mis en forme pour la liste (VehicleCard du web).</summary>
public sealed record VehicleItem(VehicleDto Dto)
{
    public string Name => FleetFormat.Vehicle(Dto.Brand, Dto.Model);
    public string Details => Dto.StoreName is { } store
        ? $"{Dto.Year} · {FleetFormat.Kilometers(Dto.Mileage)} · {store}"
        : $"{Dto.Year} · {FleetFormat.Kilometers(Dto.Mileage)}";
}

/// <summary>
/// Liste des véhicules (Vehicles.tsx) : recherche par NIV, marque ou modèle, filtre de statut en
/// pilules, pages de 20 chargées au défilement.
/// </summary>
public sealed partial class VehiclesViewModel : ViewModelBase
{
    private const int PageSize = 20;

    private readonly FleetApi _api;
    private readonly AppSession _session;
    private int _page;
    private bool _hasMore;
    private VehicleStatus? _status;
    private CancellationTokenSource? _searchDelay;

    public VehiclesViewModel(FleetApi api, AppSession session)
    {
        _api = api;
        _session = session;
        StatusChoices = Choice.Group(
            new (string, VehicleStatus?)[]
            {
                (FleetFormat.FilterLabel(null), null),
                (FleetFormat.FilterLabel(VehicleStatus.Available), VehicleStatus.Available),
                (FleetFormat.FilterLabel(VehicleStatus.InIntervention), VehicleStatus.InIntervention),
                (FleetFormat.FilterLabel(VehicleStatus.Sold), VehicleStatus.Sold),
                (FleetFormat.FilterLabel(VehicleStatus.OutOfService), VehicleStatus.OutOfService),
            },
            null,
            async status =>
            {
                _status = status;
                await ReloadAsync(showSkeleton: true);
            });
    }

    protected override FleetChange Watches => FleetChange.Vehicles | FleetChange.Stores;

    public ObservableCollection<VehicleItem> Vehicles { get; } = [];

    public List<Choice> StatusChoices { get; }

    public bool CanCreate => RolePermissions.CanManageVehicles(_session.Role);

    [ObservableProperty]
    private string _search = string.Empty;

    [ObservableProperty]
    private string _subtitle = "";

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private string _emptyText = "";

    [ObservableProperty]
    private bool _isLoadingMore;

    protected override async Task LoadAsync()
    {
        var result = await _api.GetVehiclesAsync(1, PageSize, Search, _status);
        _page = 1;
        _hasMore = result.HasNextPage;
        Vehicles.Clear();
        foreach (var vehicle in result.Items)
            Vehicles.Add(new VehicleItem(vehicle));

        Subtitle = $"{FleetFormat.Count(result.TotalCount, "véhicule", "véhicules")} dans le parc";
        IsEmpty = Vehicles.Count == 0;
        EmptyText = string.IsNullOrWhiteSpace(Search) && _status is null ? "Aucun véhicule enregistré" : "Aucun résultat trouvé";
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
            var result = await _api.GetVehiclesAsync(_page + 1, PageSize, Search, _status);
            _page = result.Page;
            _hasMore = result.HasNextPage;
            foreach (var vehicle in result.Items.Where(v => Vehicles.All(existing => existing.Dto.Id != v.Id)))
                Vehicles.Add(new VehicleItem(vehicle));
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

    // Recherche au fil de la frappe, après une courte pause (comme le debounce du web).
    partial void OnSearchChanged(string value)
    {
        _searchDelay?.Cancel();
        _searchDelay = new CancellationTokenSource();
        var token = _searchDelay.Token;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                await ReloadAsync(showSkeleton: false);
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    [RelayCommand]
    private Task OpenAsync(VehicleItem item) =>
        Shell.Current.GoToAsync("vehicle", new Dictionary<string, object> { ["id"] = item.Dto.Id });

    [RelayCommand]
    private Task CreateAsync() => Shell.Current.GoToAsync("vehicleform");

    [RelayCommand]
    private Task ScanAsync() => Shell.Current.GoToAsync("scan");
}
