using AutoNexusMobile.Views;

namespace AutoNexusMobile.Services;

/// <summary>
/// Confirmation brève en bas de l'écran (react-hot-toast du web), dessinée par l'app à la place
/// du Toast gris d'Android.
/// </summary>
public static class Toasts
{
    public static void Show(string message, bool error = false)
    {
        if (Shell.Current?.CurrentPage is PageBase page)
            page.ShowToast(message, error);
    }
}
