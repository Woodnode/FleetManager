using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

public partial class TodayPage : PageBase
{
    public TodayPage(TodayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
