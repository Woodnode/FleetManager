using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class InterventionDetailPage : PageBase
{
    public InterventionDetailPage(InterventionDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
