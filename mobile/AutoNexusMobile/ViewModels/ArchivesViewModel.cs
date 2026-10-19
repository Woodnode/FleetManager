using System.Collections.ObjectModel;
using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Véhicule archivé mis en forme (Archives.tsx, vue mobile).</summary>
public sealed record ArchivedItem(ArchivedVehicleDto Dto)
{
    public string Name => FleetFormat.Vehicle(Dto.Brand, Dto.Model);
    public string Details =>
        $"{Dto.StoreName} · {Dto.Year} · {FleetFormat.Kilometers(Dto.Mileage)} · {Dto.InterventionCount} intervention{(Dto.InterventionCount > 1 ? "s" : "")}";
    public string DeletedAt => $"Supprimé le {FleetFormat.Date(Dto.DeletedAt)}";
}

/// <summary>Archives : véhicules supprimés, consultables et restaurables (administrateur et gérant).</summary>
public sealed partial class ArchivesViewModel : ViewModelBase
{
    private const int PageSize = 20;

    private readonly FleetApi _api;
    private CancellationTokenSource? _searchDelay;
    private int _page;
    private bool _hasMore;

    public ArchivesViewModel(FleetApi api) => _api = api;

    protected override FleetChange Watches => FleetChange.Vehicles;

    public ObservableCollection<ArchivedItem> Vehicles { get; } = [];

    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private bool _isLoadingMore;

    protected override async Task LoadAsync()
    {
        var result = await _api.GetArchivedVehiclesAsync(1, PageSize, Search);
        _page = 1;
        _hasMore = result.HasNextPage;
        Vehicles.Clear();
        foreach (var vehicle in result.Items)
            Vehicles.Add(new ArchivedItem(vehicle));

        Subtitle = $"{FleetFormat.Count(result.TotalCount, "véhicule archivé", "véhicules archivés")}. Historique conservé pour les rapports.";
        IsEmpty = Vehicles.Count == 0;
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_hasMore || IsLoadingMore || IsLoading) return;

        IsLoadingMore = true;
        try
        {
            var result = await _api.GetArchivedVehiclesAsync(_page + 1, PageSize, Search);
            _page = result.Page;
            _hasMore = result.HasNextPage;
            foreach (var vehicle in result.Items.Where(v => Vehicles.All(e => e.Dto.Id != v.Id)))
                Vehicles.Add(new ArchivedItem(vehicle));
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
    private Task HistoryAsync(ArchivedItem item) =>
        Shell.Current.GoToAsync("archived", new Dictionary<string, object> { ["id"] = item.Dto.Id });

    [RelayCommand]
    private async Task RestoreAsync(ArchivedItem item)
    {
        if (await ArchiveRestore.RunAsync(_api, item.Dto) is not null)
            await ReloadAsync(showSkeleton: false);
    }
}

/// <summary>Restauration d'un véhicule archivé, partagée par la liste et l'historique.</summary>
public static class ArchiveRestore
{
    public static async Task<VehicleDto?> RunAsync(FleetApi api, ArchivedVehicleDto vehicle)
    {
        var name = FleetFormat.Vehicle(vehicle.Brand, vehicle.Model);
        var history = vehicle.InterventionCount > 0
            ? $" avec ses {vehicle.InterventionCount} intervention{(vehicle.InterventionCount > 1 ? "s" : "")} d'historique"
            : "";
        var confirmed = await ConfirmSheet.AskAsync("Restaurer le véhicule",
            $"{name} ({vehicle.Vin}) reviendra dans le parc de {vehicle.StoreName}{history}.", "Restaurer");
        if (!confirmed) return null;

        try
        {
            var restored = await api.RestoreVehicleAsync(vehicle.Id);
            CommunityToolkit.Mvvm.Messaging.IMessengerExtensions.Send(CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default, new FleetChangedMessage(FleetChange.Vehicles | FleetChange.Interventions));
            Toasts.Show("Véhicule restauré");
            return restored;
        }
        catch (ApiException ex)
        {
            Toasts.Show(ex.Message, error: true);
            return null;
        }
    }
}
