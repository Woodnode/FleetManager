using System.Globalization;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Auth;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using AutoNexusMobile.Services;
using AutoNexusMobile.ViewModels;
using AutoNexusMobile.Views;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using ZXing.Net.Maui.Controls;

namespace AutoNexusMobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseBarcodeReader()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("Inter-Regular.ttf", "Inter");
				fonts.AddFont("Inter-Medium.ttf", "InterMedium");
				fonts.AddFont("Inter-SemiBold.ttf", "InterSemiBold");
				fonts.AddFont("Inter-Bold.ttf", "InterBold");
				fonts.AddFont("Lucide.ttf", "Lucide");
			})
			.ConfigureMauiHandlers(handlers =>
			{
#if ANDROID
				handlers.AddHandler<Shell, TabBarShellRenderer>();
#endif
			});

		// L'application n'existe qu'en français : dates et nombres suivent le français de France,
		// comme le web (toLocaleString('fr-FR')), quelle que soit la langue de l'appareil.
		CultureInfo.DefaultThreadCurrentCulture = FleetFormat.Culture;
		CultureInfo.DefaultThreadCurrentUICulture = FleetFormat.Culture;
		CultureInfo.CurrentCulture = FleetFormat.Culture;
		CultureInfo.CurrentUICulture = FleetFormat.Culture;

		RemoveNativeFieldChrome();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Session : un client HTTP sans jeton pour l'authentification, un autre pour l'API qui
		// joint le jeton et le renouvelle sur un 401.
		builder.Services.AddSingleton<ITokenStore, SecureStorageTokenStore>();
		builder.Services.AddSingleton(sp => new SessionManager(
			new HttpClient { BaseAddress = ApiConfig.BaseUrl, Timeout = TimeSpan.FromSeconds(20) },
			sp.GetRequiredService<ITokenStore>()));
		builder.Services.AddSingleton(sp => new FleetApi(
			new HttpClient(new AuthHeaderHandler(sp.GetRequiredService<SessionManager>()) { InnerHandler = new HttpClientHandler() })
			{
				BaseAddress = ApiConfig.BaseUrl,
				Timeout = TimeSpan.FromSeconds(20),
			}));
		builder.Services.AddSingleton(sp => new FleetRealtime(ApiConfig.HubUrl, sp.GetRequiredService<SessionManager>()));
		builder.Services.AddSingleton<RealtimeState>();
		builder.Services.AddSingleton<AppSession>();
		builder.Services.AddSingleton<InterventionFlow>();
		builder.Services.AddSingleton<AppShell>();

		builder.Services.AddTransient<LoginViewModel>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<TodayViewModel>();
		builder.Services.AddTransient<TodayPage>();
		builder.Services.AddTransient<DashboardViewModel>();
		builder.Services.AddTransient<DashboardPage>();
		builder.Services.AddTransient<VehiclesViewModel>();
		builder.Services.AddTransient<VehiclesPage>();
		builder.Services.AddTransient<VehicleDetailViewModel>();
		builder.Services.AddTransient<VehicleDetailPage>();
		builder.Services.AddTransient<VehicleFormViewModel>();
		builder.Services.AddTransient<VehicleFormPage>();
		builder.Services.AddTransient<InterventionsViewModel>();
		builder.Services.AddTransient<InterventionsPage>();
		builder.Services.AddTransient<InterventionDetailViewModel>();
		builder.Services.AddTransient<InterventionDetailPage>();
		builder.Services.AddTransient<InterventionFormViewModel>();
		builder.Services.AddTransient<InterventionFormPage>();
		builder.Services.AddTransient<StoresViewModel>();
		builder.Services.AddTransient<StoresPage>();
		builder.Services.AddTransient<StoreFormViewModel>();
		builder.Services.AddTransient<StoreFormPage>();
		builder.Services.AddTransient<ArchivesViewModel>();
		builder.Services.AddTransient<ArchivesPage>();
		builder.Services.AddTransient<ArchivedVehicleViewModel>();
		builder.Services.AddTransient<ArchivedVehiclePage>();
		builder.Services.AddTransient<ScanViewModel>();
		builder.Services.AddTransient<ScanPage>();

		return builder.Build();
	}

	// Les champs portent leur bordure arrondie (style Field) : on retire le soulignement natif
	// d'Android, qui se superposait au cadre.
	private static void RemoveNativeFieldChrome()
	{
#if ANDROID
		static void ClearUnderline(Android.Views.View view) =>
			view.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);

		EntryHandler.Mapper.AppendToMapping("SansSoulignement", (handler, _) => ClearUnderline(handler.PlatformView));
		EditorHandler.Mapper.AppendToMapping("SansSoulignement", (handler, _) => ClearUnderline(handler.PlatformView));

		// Piste de la barre de disponibilité des enseignes : celle du web (slate-100), pas le gris d'Android.
		ProgressBarHandler.Mapper.AppendToMapping("PisteDuWeb", (handler, _) =>
			handler.PlatformView.ProgressBackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(
				Microsoft.Maui.Platform.ColorExtensions.ToPlatform(Services.ThemeColors.Get("Track"))));
#endif
	}
}
