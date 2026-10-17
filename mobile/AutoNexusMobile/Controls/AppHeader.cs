using AutoNexusMobile.Services;
using AutoNexusMobile.Views;

namespace AutoNexusMobile.Controls;

/// <summary>
/// En-tête des onglets, repris de la barre supérieure du web mobile : fond de la barre latérale,
/// logo et marque « AutoNexus / Gestion du parc auto » à gauche ; à droite le scan du NIV
/// (le geste principal du technicien) et l'avatar qui ouvre la feuille du compte.
/// </summary>
public sealed class AppHeader : ContentView
{
    private readonly Label _initials;

    public AppHeader()
    {
        var resources = Microsoft.Maui.Controls.Application.Current!.Resources;

        var logo = new Image { Source = "logo.png", WidthRequest = 30, HeightRequest = 34, VerticalOptions = LayoutOptions.Center };
        var brand = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Spacing = 0,
            Children =
            {
                new Label { Text = "AutoNexus", FontFamily = "InterSemiBold", FontSize = 15, TextColor = (Color)resources["SidebarInk"], CharacterSpacing = -0.2 },
                new Label { Text = "Gestion du parc auto", FontFamily = "Inter", FontSize = 12, TextColor = (Color)resources["SidebarMuted"] },
            },
        };
        SemanticProperties.SetHeadingLevel(brand, SemanticHeadingLevel.Level1);

        var scan = new Button { Text = AutoNexusMobile.Resources.Icons.ScanBarcode, Style = (Style)resources["HeaderIconButton"] };
        SemanticProperties.SetDescription(scan, "Scanner le NIV d'un véhicule");
        scan.Clicked += async (_, _) => await Shell.Current.GoToAsync("scan");

        _initials = new Label
        {
            FontFamily = "InterBold",
            FontSize = 12,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };
        var avatar = new Border
        {
            WidthRequest = 34,
            HeightRequest = 34,
            StrokeThickness = 0,
            StrokeShape = (Microsoft.Maui.Controls.Shapes.Shape)resources["ShapePill"],
            Background = (Brush)resources["AvatarGradient"],
            Content = _initials,
        };
        var account = new Grid { WidthRequest = 44, HeightRequest = 44, Children = { avatar } };
        account.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await AccountSheet.OpenAsync()) });
        SemanticProperties.SetDescription(account, "Compte, thème et déconnexion");

        var grid = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) },
            ColumnSpacing = 10,
            Padding = new Thickness(16, 6, 8, 6),
            MinimumHeightRequest = 56,
            BackgroundColor = (Color)resources["Sidebar"],
        };
        grid.Add(logo, 0);
        grid.Add(brand, 1);
        grid.Add(scan, 2);
        grid.Add(account, 3);
        var line = new BoxView { HeightRequest = 1, Color = (Color)resources["SidebarLine"], VerticalOptions = LayoutOptions.End, Margin = new Thickness(-16, 0, -8, -6) };
        grid.Add(line, 0);
        Grid.SetColumnSpan(line, 4);

        Content = grid;
        Loaded += (_, _) => _initials.Text = IPlatformApplication.Current?.Services.GetService<AppSession>()?.Current?.Initials ?? "";
    }
}
