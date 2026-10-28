using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

namespace FleetManager.Api.Infrastructure;

/// <summary>
/// Adresse IP du client derrière le proxy inverse, et limiteur de connexion partitionné par cette adresse.
///
/// Les en-têtes X-Forwarded-For et X-Forwarded-Proto ne sont crus que s'ils viennent d'un proxy de
/// confiance : le loopback par défaut, plus les adresses de <c>ForwardedHeaders:KnownProxies</c>.
/// Sinon n'importe quel client changerait d'adresse à chaque requête pour contourner le limiteur.
/// </summary>
public static class ForwardedHeadersSetup
{
    public const string LoginPolicy = "login";

    /// <summary>Lit la liste des proxys ; une adresse invalide arrête le démarrage plutôt que d'être ignorée.</summary>
    public static void Configure(ForwardedHeadersOptions options, IEnumerable<string>? knownProxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        foreach (var value in knownProxies ?? [])
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;
            if (!IPAddress.TryParse(value.Trim(), out var address))
                throw new InvalidOperationException(
                    $"ForwardedHeaders:KnownProxies contient « {value} », qui n'est pas une adresse IP valide.");
            options.KnownProxies.Add(address);
        }
    }

    /// <summary>5 tentatives par minute et par adresse IP : un attaquant ne bloque plus la connexion des autres.</summary>
    public static RateLimitPartition<string> LoginPartition(HttpContext context) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "inconnue",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 5,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0,
            });
}
