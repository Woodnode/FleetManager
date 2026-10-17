using System.Collections.ObjectModel;
using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AutoNexusMobile.ViewModels;

/// <summary>Enseigne et ses chiffres (StoreCard du web).</summary>
public sealed class StoreItem(StoreDto dto, int total, int available, int inIntervention)
{
    public StoreDto Dto { get; } = dto;
    public string Address => string.IsNullOrWhiteSpace(Dto.Address) ? "Adresse non renseignée" : Dto.Address;
    public string City => $"{Dto.PostalCode} {Dto.City}".Trim();
    public string VehicleCount => FleetFormat.Count(total, "véhicule", "véhicules");
    public string AvailableText => $"{available} dispo";
    public string InInterventionText => $"{inIntervention} en cours";
    public bool HasInIntervention => inIntervention > 0;
    public int AvailabilityPercent => total > 0 ? (int)Math.Round(available * 100.0 / total) : 0;
    public double AvailabilityRatio => AvailabilityPercent / 100.0;
    public string AvailabilityText => $"{AvailabilityPercent} % disponibilité";

    /// <summary>Vert au-dessus de 70 %, ambre au-dessus de 40 %, rouge en dessous, comme le web.</summary>
    public Color BarColor => AvailabilityPercent >= 70 ? Color.FromArgb("#10B981")
        : AvailabilityPercent >= 40 ? Color.FromArgb("#F59E0B")
        : Color.FromArgb("#EF4444");
}

public sealed partial class StoresViewModel : ViewModelBase
{
    private readonly FleetApi _api;
    private readonly AppSession _session;

    public StoresViewModel(FleetApi api, AppSession session)
    {
        _api = api;
        _session = session;
    }

    protected override FleetChange Watches => FleetChange.Stores | FleetChange.Vehicles;

    public ObservableCollection<StoreItem> Stores { get; } = [];

    public bool IsAdmin => RolePermissions.CanManageStores(_session.Role);

    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private bool _isEmpty;

    protected override async Task LoadAsync()
    {
        var storesTask = _api.GetStoresAsync();
        var vehicles = await AllVehiclesAsync();
        var stores = await storesTask;

        Stores.Clear();
        foreach (var store in stores.OrderBy(s => s.Name))
        {
            var mine = vehicles.Where(v => v.StoreId == store.Id).ToList();
            Stores.Add(new StoreItem(store, mine.Count,
                mine.Count(v => v.Status == VehicleStatus.Available),
                mine.Count(v => v.Status == VehicleStatus.InIntervention)));
        }

        Subtitle = $"{FleetFormat.Count(stores.Count, "enseigne", "enseignes")} dans le réseau";
        IsEmpty = Stores.Count == 0;
        OnPropertyChanged(nameof(IsAdmin));
    }

    // Les chiffres par enseigne viennent de la liste des véhicules, comme sur le web (pages de 100).
    private async Task<List<VehicleDto>> AllVehiclesAsync()
    {
        var all = new List<VehicleDto>();
        for (var page = 1; ; page++)
        {
            var result = await _api.GetVehiclesAsync(page, 100);
            all.AddRange(result.Items);
            if (!result.HasNextPage || page >= 20)
                return all;
        }
    }

    [RelayCommand]
    private Task CreateAsync() => Shell.Current.GoToAsync("storeform");

    [RelayCommand]
    private Task EditAsync(StoreItem item) =>
        Shell.Current.GoToAsync("storeform", new Dictionary<string, object> { ["store"] = item.Dto });

    [RelayCommand]
    private async Task DeleteAsync(StoreItem item)
    {
        var confirmed = await ConfirmSheet.AskAsync("Supprimer l'enseigne",
            $"L'enseigne « {item.Dto.Name} » sera supprimée. Une enseigne qui compte encore des véhicules ne peut pas l'être.",
            "Supprimer", destructive: true);
        if (!confirmed) return;

        if (await RunAsync(() => _api.DeleteStoreAsync(item.Dto.Id)))
        {
            Stores.Remove(item);
            Broadcast(FleetChange.Stores);
            Toasts.Show("Enseigne supprimée");
        }
        else
        {
            Toasts.Show(ErrorMessage ?? "Impossible de supprimer : l'enseigne contient des véhicules", error: true);
            ErrorMessage = null;
        }
    }
}
