using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Views;

namespace AutoNexusMobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("vehicle", typeof(VehicleDetailPage));
        Routing.RegisterRoute("vehicleform", typeof(VehicleFormPage));
        Routing.RegisterRoute("intervention", typeof(InterventionDetailPage));
        Routing.RegisterRoute("interventionform", typeof(InterventionFormPage));
        Routing.RegisterRoute("storeform", typeof(StoreFormPage));
        Routing.RegisterRoute("archived", typeof(ArchivedVehiclePage));
        Routing.RegisterRoute("scan", typeof(ScanPage));
    }

    /// <summary>
    /// Onglets du rôle (cinq au plus, la limite de la barre Android) : la journée pour le
    /// technicien, les archives pour l'administrateur et le gérant, comme le menu du web.
    /// </summary>
    public void ConfigureFor(UserRole role)
    {
        TodayTab.IsVisible = RolePermissions.IsTechnician(role);
        ArchivesTab.IsVisible = RolePermissions.CanSeeArchives(role);
    }
}
