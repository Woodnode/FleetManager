using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace AutoNexusMobile.Controls;

/// <summary>
/// Badge de statut ou de type (components/ui/Badge.tsx du web) : point de couleur, libellé,
/// fond teinté, rayon 6. La teinte suit la valeur, les couleurs suivent le thème.
/// </summary>
public sealed class StatusBadge : Border
{
    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(object), typeof(StatusBadge), propertyChanged: (b, _, _) => ((StatusBadge)b).Update());

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(StatusBadge), propertyChanged: (b, _, _) => ((StatusBadge)b).Update());

    private readonly Ellipse _dot = new() { WidthRequest = 6, HeightRequest = 6, VerticalOptions = LayoutOptions.Center };
    private readonly Label _label = new() { FontFamily = "InterMedium", FontSize = 12, LineBreakMode = LineBreakMode.NoWrap, VerticalOptions = LayoutOptions.Center };

    public StatusBadge()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 6 };
        StrokeThickness = 1;
        Padding = new Thickness(8, 3);
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;
        Content = new HorizontalStackLayout { Spacing = 6, Children = { _dot, _label } };
        Update();
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Teinte de Badge.tsx pour une valeur d'énumération de l'API.</summary>
    public static string Tone(object? value) => value switch
    {
        VehicleStatus.Available or InterventionStatus.Completed => "Emerald",
        VehicleStatus.InIntervention or InterventionStatus.InProgress => "Blue",
        InterventionStatus.Planned => "Amber",
        VehicleStatus.OutOfService => "Red",
        InterventionType.Maintenance => "Violet",
        InterventionType.Repair => "Orange",
        InterventionType.Inspection => "Teal",
        _ => "Slate",
    };

    private void Update()
    {
        var tone = Tone(Value);
        _label.Text = Text ?? Value?.ToString();
        _dot.Fill = ThemeColors.Get(tone + "Dot");
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColors.Get(tone + "Bg", false), ThemeColors.Get(tone + "Bg", true));
        this.SetAppThemeColor(StrokeProperty, ThemeColors.Get(tone + "Bg", false), ThemeColors.Get(tone + "Bg", true));
        _label.SetAppThemeColor(Label.TextColorProperty, ThemeColors.Get(tone + "Ink", false), ThemeColors.Get(tone + "Ink", true));
        SemanticProperties.SetDescription(this, _label.Text);
    }
}
