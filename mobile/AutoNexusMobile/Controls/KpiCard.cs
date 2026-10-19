using AutoNexusMobile.Services;
using AutoNexusMobile.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace AutoNexusMobile.Controls;

/// <summary>
/// Indicateur du tableau de bord (KpiCard de Dashboard.tsx) : filet de couleur en haut
/// (fm-kpi-*), pastille d'icône teintée, intitulé en petites capitales, chiffre, détail et
/// tendance.
/// </summary>
public sealed class KpiCard : ContentView
{
    // Couleurs des classes fm-kpi-* du web.
    private static readonly Dictionary<string, Color> Accents = new()
    {
        ["Slate"] = Color.FromArgb("#64748B"),
        ["Emerald"] = Color.FromArgb("#10B981"),
        ["Amber"] = Color.FromArgb("#F59E0B"),
        ["Blue"] = Color.FromArgb("#4C6EF5"),
    };

    public KpiCard()
    {
        BindingContextChanged += (_, _) => Build();
    }

    private void Build()
    {
        if (BindingContext is not KpiItem item)
            return;

        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        var accent = Accents.GetValueOrDefault(item.Tone, Accents["Slate"]);

        var tile = new Border
        {
            WidthRequest = 36,
            HeightRequest = 36,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            BackgroundColor = accent.WithAlpha(0.12f),
            Content = new Label { Text = item.Icon, FontFamily = "Lucide", FontSize = 18, TextColor = accent, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
        };

        var top = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        top.Add(tile, 0);
        if (item.Delta is { } delta)
        {
            var tone = item.DeltaPositive ? "Emerald" : "Red";
            var pill = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                StrokeThickness = 0,
                Padding = new Thickness(7, 2),
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Content = new Label { Text = (item.DeltaPositive ? "↑ " : "↓ ") + delta, FontFamily = "InterSemiBold", FontSize = 10, LineBreakMode = LineBreakMode.NoWrap },
            };
            pill.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.Get(tone + "Bg", false), ThemeColors.Get(tone + "Bg", true));
            ((Label)pill.Content).SetAppThemeColor(Label.TextColorProperty, ThemeColors.Get(tone + "Ink", false), ThemeColors.Get(tone + "Ink", true));
            top.Add(pill, 1);
        }

        var body = new VerticalStackLayout
        {
            Spacing = 0,
            Padding = new Thickness(14, 12, 14, 14),
            Children =
            {
                top,
                new Label { Text = item.Title, Style = (Style)resources["TextOverline"], Margin = new Thickness(0, 12, 0, 4), LineBreakMode = LineBreakMode.TailTruncation },
                new Label { Text = item.Value, Style = (Style)resources["TextKpi"] },
                new Label { Text = item.Sub, Style = (Style)resources["TextMeta"], FontSize = 12, Margin = new Thickness(0, 4, 0, 0), MaxLines = 2 },
            },
        };

        var card = new Border { Style = (Style)resources["Card"], Padding = 0 };
        card.Content = new Grid
        {
            RowDefinitions = { new(3), new(GridLength.Star) },
            Children = { new BoxView { Color = accent }, body },
        };
        Grid.SetRow(body, 1);
        SemanticProperties.SetDescription(card, $"{item.Title} : {item.Value}. {item.Sub}");
        Content = card;
    }
}
