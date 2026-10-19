using AutoNexusMobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace AutoNexusMobile.Controls;

/// <summary>
/// État réel de la connexion temps réel (RealtimeBadge du web) : « En direct » quand les
/// modifications des autres utilisateurs arrivent instantanément, sinon « Hors ligne ».
/// </summary>
public sealed class RealtimeBadge : Border
{
    private readonly Ellipse _dot = new() { WidthRequest = 6, HeightRequest = 6, VerticalOptions = LayoutOptions.Center };
    private readonly Label _label = new() { FontFamily = "InterMedium", FontSize = 12, LineBreakMode = LineBreakMode.NoWrap, VerticalOptions = LayoutOptions.Center };
    private RealtimeState? _state;
    private bool _pulsing;

    public RealtimeBadge()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 999 };
        StrokeThickness = 1;
        Padding = new Thickness(12, 6);
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;
        Content = new HorizontalStackLayout { Spacing = 8, Children = { _dot, _label } };
        SemanticProperties.SetDescription(this, "État du temps réel");

        Loaded += (_, _) =>
        {
            _state = IPlatformApplication.Current?.Services.GetService<RealtimeState>();
            if (_state is null) return;
            _state.PropertyChanged += OnStateChanged;
            Update();
        };
        Unloaded += (_, _) =>
        {
            if (_state is not null) _state.PropertyChanged -= OnStateChanged;
            _pulsing = false;
        };
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        MainThread.BeginInvokeOnMainThread(Update);

    private void Update()
    {
        if (_state is null) return;

        var tone = _state.Tone;
        _label.Text = _state.Label;
        _dot.Fill = ThemeColors.Get(tone + "Dot");
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColors.Get(tone + "Bg", false), ThemeColors.Get(tone + "Bg", true));
        this.SetAppThemeColor(StrokeProperty, ThemeColors.Get("Line", false), ThemeColors.Get("Line", true));
        _label.SetAppThemeColor(Label.TextColorProperty, ThemeColors.Get(tone + "Ink", false), ThemeColors.Get(tone + "Ink", true));
        SemanticProperties.SetDescription(this, $"Temps réel : {_state.Label}");

        if (_state.Pulses && !_pulsing)
            _ = PulseAsync();
    }

    // animate-pulse du web : le point s'estompe et revient, tant que la connexion est vivante.
    private async Task PulseAsync()
    {
        _pulsing = true;
        while (_pulsing && _state?.Pulses == true)
        {
            await _dot.FadeToAsync(0.35, 900, Easing.SinInOut);
            await _dot.FadeToAsync(1, 900, Easing.SinInOut);
        }
        _dot.Opacity = 1;
        _pulsing = false;
    }
}
