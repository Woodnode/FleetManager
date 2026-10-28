using System.Reflection;
using AutoNexusMobile.Core.Api;

namespace AutoNexusMobile.Services;

/// <summary>
/// Adresse de l'API. En Debug, l'API locale en HTTP (profil « http » de FleetManager.Api, port
/// 5290), jointe par 10.0.2.2 depuis l'émulateur Android. En Release, l'hôte HTTPS fixé à la
/// compilation : dotnet publish -p:AutoNexusApiHost=https://... (une variable d'environnement
/// n'existe pas pour une app installée sur un téléphone).
/// </summary>
public static class ApiConfig
{
#if DEBUG
    private static string ResolveHost() => DeviceInfo.Platform == DevicePlatform.Android
        ? "http://10.0.2.2:5290"
        : "http://localhost:5290";
#else
    private static string ResolveHost()
    {
        var host = Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == "AutoNexusApiHost")?.Value;

        // Refuse un hôte absent ou en HTTP : les jetons ne doivent jamais circuler en clair.
        return ApiHost.RequireHttps(host);
    }
#endif

    private static string Host { get; } = ResolveHost();

    public static Uri BaseUrl => new($"{Host}/api/v1/");

    // Le hub est servi sous /api/v1, comme le reste de l'API : un seul relais couvre les deux.
    public static string HubUrl => $"{Host}/api/v1/hubs/fleet";
}
