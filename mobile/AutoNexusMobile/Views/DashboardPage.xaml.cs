using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class DashboardPage : PageBase
{
    public DashboardPage(DashboardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Total du parc au centre de l'anneau, comme le chiffre de la carte.
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(DashboardViewModel.FleetTotal))
                FleetChart.SetCenter(viewModel.FleetTotal, "véhicules");
        };
    }
}
