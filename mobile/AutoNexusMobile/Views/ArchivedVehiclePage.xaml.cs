using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class ArchivedVehiclePage : PageBase
{
    public ArchivedVehiclePage(ArchivedVehicleViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
