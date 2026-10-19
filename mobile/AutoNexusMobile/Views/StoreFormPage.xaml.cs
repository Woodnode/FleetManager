using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class StoreFormPage : PageBase
{
    public StoreFormPage(StoreFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
