using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class VehicleDetailPage : PageBase
{
    public VehicleDetailPage(VehicleDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
