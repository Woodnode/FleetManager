using AutoNexusMobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace AutoNexusMobile.Controls;

/// <summary>Confirmation d'une action (suppression, restauration), à la place de DisplayAlert.</summary>
public sealed class ConfirmSheet : BottomSheet
{
    private ConfirmSheet(string title, string message, string confirmLabel, bool destructive)
    {
        var confirm = new Button { Text = confirmLabel };
        if (destructive)
            confirm.Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources["DestructiveButton"];
        confirm.Clicked += async (_, _) => await CloseAsync(true);

        var cancel = new Button { Text = "Annuler", Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources["SecondaryButton"] };
        cancel.Clicked += async (_, _) => await CloseAsync(false);

        var buttons = new Grid { ColumnDefinitions = Columns.Define(Star, Star), ColumnSpacing = 10, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Add(cancel, 0);
        buttons.Add(confirm, 1);

        SheetContent = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = title, Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources["TextTitle"], FontSize = 18 },
                new Label { Text = message, Style = (Style)Microsoft.Maui.Controls.Application.Current!.Resources["TextBody"] },
                buttons,
            },
        };
    }

    public static async Task<bool> AskAsync(string title, string message, string confirmLabel, bool destructive = false) =>
        await ShowAsync<bool>(new ConfirmSheet(title, message, confirmLabel, destructive));

    private static GridLength Star => GridLength.Star;

    private static class Columns
    {
        public static ColumnDefinitionCollection Define(params GridLength[] widths) =>
            new(widths.Select(w => new ColumnDefinition(w)).ToArray());
    }
}

/// <summary>Une option d'une <see cref="OptionsSheet"/>.</summary>
public sealed record SheetOption(string Label, object Value, string? Icon = null, string? Detail = null, bool IsCurrent = false, string? Tone = null);

/// <summary>Liste de choix (nouveau statut d'un véhicule…), à la place du Picker natif d'Android.</summary>
public sealed class OptionsSheet : BottomSheet
{
    private OptionsSheet(string title, string? subtitle, IEnumerable<SheetOption> options)
    {
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        var list = new VerticalStackLayout { Spacing = 8 };
        list.Add(new Label { Text = title, Style = (Style)resources["TextTitle"], FontSize = 18 });
        if (subtitle is not null)
            list.Add(new Label { Text = subtitle, Style = (Style)resources["TextMeta"], Margin = new Thickness(0, -4, 0, 4) });

        foreach (var option in options)
            list.Add(Row(option, resources));

        SheetContent = list;
    }

    public static Task<T?> PickAsync<T>(string title, string? subtitle, IEnumerable<SheetOption> options) =>
        ShowAsync<T>(new OptionsSheet(title, subtitle, options));

    private View Row(SheetOption option, ResourceDictionary resources)
    {
        var grid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 12,
            MinimumHeightRequest = 52,
        };

        if (option.Tone is not null)
        {
            var dot = new Ellipse { WidthRequest = 10, HeightRequest = 10, Fill = ThemeColors.Get(option.Tone + "Dot"), VerticalOptions = LayoutOptions.Center, Margin = new Thickness(4, 0) };
            grid.Add(dot, 0);
        }
        else if (option.Icon is not null)
        {
            grid.Add(new Label { Text = option.Icon, Style = (Style)resources["IconAccent"], WidthRequest = 24 }, 0);
        }

        var texts = new VerticalStackLayout { VerticalOptions = LayoutOptions.Center, Spacing = 1 };
        texts.Add(new Label { Text = option.Label, Style = (Style)resources["TextStrong"] });
        if (option.Detail is not null)
            texts.Add(new Label { Text = option.Detail, Style = (Style)resources["TextMeta"] });
        grid.Add(texts, 1);

        if (option.IsCurrent)
            grid.Add(new Label { Text = "Actuel", Style = (Style)resources["TextMeta"], VerticalOptions = LayoutOptions.Center }, 2);

        var row = new Border
        {
            Content = grid,
            Padding = new Thickness(12, 4),
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            StrokeThickness = 1,
            IsEnabled = !option.IsCurrent,
            Opacity = option.IsCurrent ? 0.6 : 1,
        };
        row.SetAppThemeColor(Border.StrokeProperty, ThemeColors.Get("Line", false), ThemeColors.Get("Line", true));
        row.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.Get("Page", false), ThemeColors.Get("Sunken", true));
        SemanticProperties.SetDescription(row, option.Label);
        row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await CloseAsync(option.Value)) });
        return row;
    }
}
