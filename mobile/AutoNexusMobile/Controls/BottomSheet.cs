using AutoNexusMobile.Views;
using Microsoft.Maui.Controls.Shapes;

namespace AutoNexusMobile.Controls;

/// <summary>
/// Feuille qui monte du bas, à la place des boîtes de dialogue d'Android (alerte, liste de choix) :
/// même typographie, mêmes surfaces et mêmes rayons que le reste de l'app. Elle est posée en calque
/// dans la page courante (pas de fenêtre modale, qui laissait un voile sur la page à la fermeture).
/// Le fond s'assombrit ; toucher le fond, le bouton retour ou <see cref="CloseAsync"/> la referme.
/// Les feuilles dérivées déclarent leur contenu en XAML et rendent un résultat à l'appelant.
/// </summary>
[ContentProperty(nameof(SheetContent))]
public class BottomSheet : ContentView
{
    private const uint Duration = 220;

    public static readonly BindableProperty SheetContentProperty = BindableProperty.Create(
        nameof(SheetContent), typeof(View), typeof(BottomSheet), propertyChanged: (b, _, n) => ((BottomSheet)b)._body.Content = (View?)n);

    private readonly BoxView _scrim;
    private readonly Border _sheet;
    private readonly ContentView _body = new();
    private readonly TaskCompletionSource<object?> _result = new();
    private PageBase? _host;
    private bool _closing;

    public BottomSheet()
    {
        _scrim = new BoxView { Color = Color.FromArgb("#0C1020"), Opacity = 0 };
        _scrim.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await CloseAsync()) });

        var handle = new BoxView { WidthRequest = 40, HeightRequest = 4, CornerRadius = 2, HorizontalOptions = LayoutOptions.Center };
        handle.SetAppThemeColor(BoxView.ColorProperty, Services.ThemeColors.Get("LineStrong", false), Services.ThemeColors.Get("LineStrong", true));

        _sheet = new Border
        {
            VerticalOptions = LayoutOptions.End,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20, 20, 0, 0) },
            Padding = new Thickness(20, 10, 20, 24),
            TranslationY = 900,
            Content = new VerticalStackLayout { Spacing = 14, Children = { handle, _body } },
        };
        _sheet.SetAppThemeColor(Border.BackgroundColorProperty, Services.ThemeColors.Get("Surface", false), Services.ThemeColors.Get("Surface", true));

        Content = new Grid { Children = { _scrim, _sheet } };
    }

    public View? SheetContent
    {
        get => (View?)GetValue(SheetContentProperty);
        set => SetValue(SheetContentProperty, value);
    }

    /// <summary>Ouvre la feuille par-dessus la page courante et attend son résultat.</summary>
    public static async Task<T?> ShowAsync<T>(BottomSheet sheet)
    {
        if (Shell.Current?.CurrentPage is not PageBase page)
            return default;

        sheet._host = page;
        page.ShowOverlay(sheet);
        await sheet.OpenAsync();
        return await sheet._result.Task is T value ? value : default;
    }

    private async Task OpenAsync()
    {
        // La hauteur n'est connue qu'après la mise en page : la feuille part du bas de l'écran.
        await Task.Yield();
        _sheet.TranslationY = Math.Max(_sheet.Height, 600);
        await Task.WhenAll(
            _scrim.FadeToAsync(0.5, Duration, Easing.CubicOut),
            _sheet.TranslateToAsync(0, 0, Duration, Easing.CubicOut));
    }

    public async Task CloseAsync(object? result = null)
    {
        if (_closing)
            return;
        _closing = true;

        await Task.WhenAll(
            _scrim.FadeToAsync(0, Duration, Easing.CubicIn),
            _sheet.TranslateToAsync(0, _sheet.Height, Duration, Easing.CubicIn));
        _host?.RemoveOverlay(this);
        _result.TrySetResult(result);
    }
}
