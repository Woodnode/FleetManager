using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class VehicleFormPage : PageBase
{
    public VehicleFormPage(VehicleFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
