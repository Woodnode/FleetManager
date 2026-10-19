using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class InterventionFormPage : PageBase
{
    public InterventionFormPage(InterventionFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
