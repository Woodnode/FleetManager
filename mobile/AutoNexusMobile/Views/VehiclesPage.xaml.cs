using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class VehiclesPage : PageBase
{
    public VehiclesPage(VehiclesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
