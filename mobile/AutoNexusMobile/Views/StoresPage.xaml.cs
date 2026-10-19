using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class StoresPage : PageBase
{
    public StoresPage(StoresViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
