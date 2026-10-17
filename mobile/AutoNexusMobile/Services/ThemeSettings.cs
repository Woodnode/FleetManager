namespace AutoNexusMobile.Services;

public enum ThemeChoice { System, Light, Dark }

/// <summary>Thème choisi dans la feuille du compte (système, clair ou sombre), gardé entre deux lancements.</summary>
public static class ThemeSettings
{
    private const string Key = "autonexus-theme";

    public static ThemeChoice Current => Preferences.Default.Get(Key, string.Empty) switch
    {
        "light" => ThemeChoice.Light,
        "dark" => ThemeChoice.Dark,
        _ => ThemeChoice.System,
    };

    public static void Restore() => Apply(Current);

    public static void Set(ThemeChoice choice)
    {
        Preferences.Default.Set(Key, choice switch
        {
            ThemeChoice.Light => "light",
            ThemeChoice.Dark => "dark",
            _ => string.Empty,
        });
        Apply(choice);
    }

    private static void Apply(ThemeChoice choice)
    {
        if (Microsoft.Maui.Controls.Application.Current is not { } app)
            return;

        app.UserAppTheme = choice switch
        {
            ThemeChoice.Light => AppTheme.Light,
            ThemeChoice.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

#if ANDROID
        (Platform.CurrentActivity as MainActivity)?.ApplySystemBarAppearance(app.RequestedTheme == AppTheme.Dark);
#endif
    }
}
