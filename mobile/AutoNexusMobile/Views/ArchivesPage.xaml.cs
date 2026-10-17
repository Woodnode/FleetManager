using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class ArchivesPage : PageBase
{
    public ArchivesPage(ArchivesViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
