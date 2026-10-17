using AutoNexusMobile.ViewModels;

namespace AutoNexusMobile.Views;

/// <summary>
/// Page de l'app : active son ViewModel tant qu'elle est affichée et porte la couche des toasts,
/// posée par-dessus le contenu au premier affichage.
/// </summary>
public class PageBase : ContentPage
{
    private Grid? _host;
    private Border? _toast;
    private CancellationTokenSource? _toastTimer;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        EnsureHost();
        if (BindingContext is ViewModelBase viewModel)
            await viewModel.ActivateAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        (BindingContext as ViewModelBase)?.Deactivate();
    }

    private readonly List<Controls.BottomSheet> _sheets = [];

    /// <summary>Pose une feuille du bas par-dessus le contenu de la page.</summary>
    public void ShowOverlay(Controls.BottomSheet sheet)
    {
        EnsureHost();
        _sheets.Add(sheet);
        _host?.Add(sheet);
    }

    public void RemoveOverlay(Controls.BottomSheet sheet)
    {
        _sheets.Remove(sheet);
        _host?.Remove(sheet);
    }

    // Le bouton retour ferme d'abord la feuille ouverte.
    protected override bool OnBackButtonPressed()
    {
        if (_sheets.Count > 0)
        {
            _ = _sheets[^1].CloseAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void EnsureHost()
    {
        if (_host is not null || Content is null)
            return;

        var content = Content;
        Content = null;
        _host = new Grid { content };
        Content = _host;
    }

    public async void ShowToast(string message, bool error)
    {
        EnsureHost();
        if (_host is null)
            return;

        _toastTimer?.Cancel();
        _toastTimer = new CancellationTokenSource();
        var token = _toastTimer.Token;

        if (_toast is not null)
            _host.Remove(_toast);

        var tone = error ? "Red" : "Emerald";
        var icon = new Label
        {
            Text = error ? AutoNexusMobile.Resources.Icons.CircleAlert : AutoNexusMobile.Resources.Icons.CircleCheck,
            FontFamily = "Lucide",
            FontSize = 18,
            VerticalOptions = LayoutOptions.Center,
        };
        icon.SetAppThemeColor(Label.TextColorProperty, Services.ThemeColors.Get(tone + "Ink", false), Services.ThemeColors.Get(tone + "Ink", true));

        var text = new Label { Text = message, FontFamily = "InterMedium", FontSize = 14, VerticalOptions = LayoutOptions.Center };
        text.SetAppThemeColor(Label.TextColorProperty, Services.ThemeColors.Get("Ink", false), Services.ThemeColors.Get("Ink", true));

        _toast = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            StrokeThickness = 1,
            Padding = new Thickness(14, 12),
            Margin = new Thickness(16, 0, 16, 20),
            VerticalOptions = LayoutOptions.End,
            Opacity = 0,
            TranslationY = 24,
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 8), Radius = 24, Opacity = 0.14f },
            Content = new HorizontalStackLayout { Spacing = 10, Children = { icon, text } },
        };
        _toast.SetAppThemeColor(Border.BackgroundColorProperty, Services.ThemeColors.Get("Surface", false), Services.ThemeColors.Get("Surface", true));
        _toast.SetAppThemeColor(Border.StrokeProperty, Services.ThemeColors.Get("Line", false), Services.ThemeColors.Get("LineStrong", true));
        SemanticProperties.SetDescription(_toast, message);

        var toast = _toast;
        _host.Add(toast);
        await Task.WhenAll(toast.FadeToAsync(1, 180, Easing.CubicOut), toast.TranslateToAsync(0, 0, 220, Easing.CubicOut));
        try
        {
            await Task.Delay(error ? 4000 : 2600, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await Task.WhenAll(toast.FadeToAsync(0, 180, Easing.CubicIn), toast.TranslateToAsync(0, 24, 180, Easing.CubicIn));
        _host.Remove(toast);
        if (_toast == toast)
            _toast = null;
    }
}
