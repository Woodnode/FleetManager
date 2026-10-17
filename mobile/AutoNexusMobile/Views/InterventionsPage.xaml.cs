using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class InterventionsPage : PageBase
{
    public InterventionsPage(InterventionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
