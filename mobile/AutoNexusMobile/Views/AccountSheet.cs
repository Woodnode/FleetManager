using AutoNexusMobile.Controls;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace AutoNexusMobile.Views;

/// <summary>
/// Feuille du compte, l'équivalent du bloc utilisateur de la barre latérale du web : avatar,
/// nom, rôle, rattachement, état du temps réel, thème (système, clair, sombre) et déconnexion.
/// </summary>
public sealed class AccountSheet : BottomSheet
{
    private readonly List<(ThemeChoice Choice, Border Segment, Label Text)> _segments = [];

    private AccountSheet(AppSession session)
    {
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;
        var user = session.Current;

        var avatar = new Border
        {
            WidthRequest = 52,
            HeightRequest = 52,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 999 },
            Background = (Brush)resources["AvatarGradient"],
            VerticalOptions = LayoutOptions.Center,
            Content = new Label { Text = user?.Initials, FontFamily = "InterBold", FontSize = 18, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
        };

        var rolePill = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 6 },
            StrokeThickness = 0,
            Padding = new Thickness(8, 2),
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 0, 0),
            Content = new Label { Text = user is null ? "" : RolePermissions.RoleLabel(user.Role), FontFamily = "InterMedium", FontSize = 12 },
        };
        rolePill.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.Get("AccentSoft", false), ThemeColors.Get("AccentSoft", true));
        ((Label)rolePill.Content).SetAppThemeColor(Label.TextColorProperty, ThemeColors.Get("AccentInk", false), ThemeColors.Get("AccentInk", true));

        var identity = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = user?.FullName, Style = (Style)resources["TextStrong"], FontSize = 17 },
                new Label { Text = user?.Email, Style = (Style)resources["TextMeta"], LineBreakMode = LineBreakMode.TailTruncation },
                rolePill,
            },
        };

        var close = new Button { Text = AutoNexusMobile.Resources.Icons.Close, Style = (Style)resources["IconButton"], BorderWidth = 0, Background = Brush.Transparent, VerticalOptions = LayoutOptions.Start };
        SemanticProperties.SetDescription(close, "Fermer");
        close.Clicked += async (_, _) => await CloseAsync();

        var header = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 14 };
        header.Add(avatar, 0);
        header.Add(identity, 1);
        header.Add(close, 2);

        var scope = new Label
        {
            Text = user?.StoreId is null ? "Admin global : toutes les enseignes" : "En enseigne : vous voyez les données de votre enseigne",
            Style = (Style)resources["TextMeta"],
        };

        // Thème : contrôle segmenté à trois choix.
        var segments = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 4 };
        var options = new[] { (ThemeChoice.System, "Système", AutoNexusMobile.Resources.Icons.SunMoon), (ThemeChoice.Light, "Clair", AutoNexusMobile.Resources.Icons.Sun), (ThemeChoice.Dark, "Sombre", AutoNexusMobile.Resources.Icons.Moon) };
        for (var i = 0; i < options.Length; i++)
        {
            var (choice, label, icon) = options[i];
            var text = new Label { Text = label, FontFamily = "InterMedium", FontSize = 13, VerticalOptions = LayoutOptions.Center };
            var glyph = new Label { Text = icon, FontFamily = "Lucide", FontSize = 15, VerticalOptions = LayoutOptions.Center };
            var segment = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 7 },
                StrokeThickness = 0,
                HeightRequest = 40,
                Content = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Center, Children = { glyph, text } },
            };
            glyph.SetBinding(Label.TextColorProperty, new Binding(nameof(Label.TextColor), source: text));
            segment.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => Select(choice)) });
            SemanticProperties.SetDescription(segment, $"Thème {label.ToLowerInvariant()}");
            segments.Add(segment, i);
            _segments.Add((choice, segment, text));
        }
        var themeTrack = new Border { StrokeShape = new RoundRectangle { CornerRadius = 10 }, StrokeThickness = 0, Padding = 4, Content = segments };
        themeTrack.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.Get("Sunken", false), ThemeColors.Get("Sunken", true));

        var logout = new Button
        {
            Text = "Déconnexion",
            Style = (Style)resources["DangerButton"],
            ImageSource = new FontImageSource { Glyph = AutoNexusMobile.Resources.Icons.LogOut, FontFamily = "Lucide", Size = 16, Color = ThemeColors.Get("RedDot") },
            ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 8),
        };
        logout.Clicked += async (_, _) =>
        {
            await CloseAsync();
            await session.SignOutAsync();
        };

        SheetContent = new VerticalStackLayout
        {
            Spacing = 16,
            Children =
            {
                header,
                scope,
                new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, Children = { new Label { Text = "Temps réel", Style = (Style)resources["TextStrong"], VerticalOptions = LayoutOptions.Center } } }
                    .WithChild(new RealtimeBadge(), 1),
                new Label { Text = "Thème", Style = (Style)resources["TextOverline"], Margin = new Thickness(0, 4, 0, -8) },
                themeTrack,
                logout,
            },
        };

        Refresh(ThemeSettings.Current);
    }

    public static Task OpenAsync()
    {
        var session = IPlatformApplication.Current!.Services.GetRequiredService<AppSession>();
        return ShowAsync<object>(new AccountSheet(session));
    }

    private void Select(ThemeChoice choice)
    {
        ThemeSettings.Set(choice);
        Refresh(choice);
    }

    private void Refresh(ThemeChoice current)
    {
        foreach (var (choice, segment, text) in _segments)
        {
            var selected = choice == current;
            if (selected)
            {
                segment.SetAppThemeColor(Border.BackgroundColorProperty, ThemeColors.Get("Surface", false), ThemeColors.Get("LineStrong", true));
                text.SetAppThemeColor(Label.TextColorProperty, ThemeColors.Get("Ink", false), ThemeColors.Get("Ink", true));
                segment.Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(0, 1), Radius = 3, Opacity = 0.08f };
            }
            else
            {
                segment.BackgroundColor = Colors.Transparent;
                text.SetAppThemeColor(Label.TextColorProperty, ThemeColors.Get("Muted", false), ThemeColors.Get("Muted", true));
                segment.Shadow = null!;
            }
        }
    }
}

internal static class GridExtensions
{
    public static Grid WithChild(this Grid grid, View child, int column)
    {
        grid.Add(child, column);
        return grid;
    }
}
