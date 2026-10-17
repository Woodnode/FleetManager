using Android.App;
using Android.Runtime;

namespace AutoNexusMobile;

// En Debug, la configuration réseau autorise l'HTTP vers l'API locale (10.0.2.2). Elle passe par
// cet attribut : MAUI impose son propre manifeste et un manifeste Debug séparé serait ignoré.
// En Release, rien n'est ajouté : l'app n'accepte que le HTTPS.
#if DEBUG
[Application(NetworkSecurityConfig = "@xml/network_security_config")]
#else
[Application]
#endif
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
