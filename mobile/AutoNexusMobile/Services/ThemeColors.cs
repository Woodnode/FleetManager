namespace AutoNexusMobile.Services;

/// <summary>
/// Jetons de Colors.xaml lus depuis le code (graphiques, badges, barre d'onglets native). Les vues
/// XAML passent par AppThemeBinding.
/// </summary>
public static class ThemeColors
{
    public static bool IsDark => Microsoft.Maui.Controls.Application.Current?.RequestedTheme == AppTheme.Dark;

    /// <summary>Couleur du thème courant : « Ink » donne InkLight ou InkDark, sinon la clé seule.</summary>
    public static Color Get(string token) => Get(token, IsDark);

    public static Color Get(string token, bool dark)
    {
        var resources = Microsoft.Maui.Controls.Application.Current?.Resources;
        if (resources?.TryGetValue(token + (dark ? "Dark" : "Light"), out var themed) == true && themed is Color color)
            return color;

        return resources?.TryGetValue(token, out var plain) == true && plain is Color single ? single : Colors.Gray;
    }

    /// <summary>Couleur qui suit le thème : à poser avec SetAppThemeColor.</summary>
    public static (Color Light, Color Dark) Pair(string token) => (Get(token, false), Get(token, true));
}
