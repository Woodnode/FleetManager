using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using AndroidX.Core.View;

namespace AutoNexusMobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    // L'application n'existe qu'en français : les fenêtres natives (demande d'accès à la caméra)
    // suivent le français de France, comme le web, même sur un appareil réglé en anglais.
    protected override void AttachBaseContext(Context? @base)
    {
        if (@base?.Resources?.Configuration is { } current)
        {
            var locale = Java.Util.Locale.ForLanguageTag("fr-FR");
            Java.Util.Locale.Default = locale;
            var configuration = new Configuration(current);
            configuration.SetLocale(locale);
            @base = @base.CreateConfigurationContext(configuration);
        }

        base.AttachBaseContext(@base);
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplySystemBarAppearance();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        ApplySystemBarAppearance();
    }

    /// <summary>
    /// Barre d'état : icônes claires sur l'en-tête sombre, dans les deux thèmes. Barre de
    /// navigation : celle de la barre d'onglets, claire ou sombre selon le thème de l'app.
    /// </summary>
    public void ApplySystemBarAppearance(bool? dark = null)
    {
        if (Window is null || WindowCompat.GetInsetsController(Window, Window.DecorView) is not { } controller)
            return;

        var isDark = dark ?? (Resources?.Configuration?.UiMode & UiMode.NightMask) == UiMode.NightYes;
        controller.AppearanceLightStatusBars = false;
        controller.AppearanceLightNavigationBars = !isDark;
    }
}
