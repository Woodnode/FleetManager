using AutoNexusMobile.Services;
using Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific;

namespace AutoNexusMobile;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;

        // Le clavier réduit la fenêtre au lieu de la recouvrir : les champs des feuilles du bas
        // restent visibles pendant la saisie.
        On<Microsoft.Maui.Controls.PlatformConfiguration.Android>().UseWindowSoftInputModeAdjust(WindowSoftInputModeAdjust.Resize);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        ThemeSettings.Restore();

        var shell = _services.GetRequiredService<AppShell>();
        var window = new Window(shell);
        // La session est restaurée une fois le Shell affiché : la navigation a besoin de lui.
        shell.Loaded += async (_, _) => await _services.GetRequiredService<AppSession>().StartAsync();
        return window;
    }
}
