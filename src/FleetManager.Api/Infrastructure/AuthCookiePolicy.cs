namespace FleetManager.Api.Infrastructure;

/// <summary>
/// Politique des cookies d'authentification.
///
/// En production, le front et l'API sont servis sous la même origine (le relais transmet /api au
/// serveur) : <c>SameSite=Lax</c> suffit et protège mieux contre le CSRF. En développement, le front
/// (port 5173) et l'API (autre port) sont deux origines : seul <c>SameSite=None</c> laisse passer
/// le cookie. D'où le réglage <c>Auth:CookieSameSite</c>.
/// </summary>
public sealed class AuthCookiePolicy
{
    public const string AccessTokenCookie  = "access_token";
    public const string RefreshTokenCookie = "refresh_token";
    public const string RefreshTokenPath   = "/api/v1/auth/refresh";

    public SameSiteMode SameSite { get; }

    public AuthCookiePolicy(SameSiteMode sameSite) => SameSite = sameSite;

    /// <summary>Lit la configuration ; une valeur invalide arrête le démarrage plutôt que d'affaiblir les cookies en silence.</summary>
    public static AuthCookiePolicy FromConfiguration(string? configuredValue) =>
        new(configuredValue?.Trim().ToLowerInvariant() switch
        {
            null or "" => SameSiteMode.None,
            "none"     => SameSiteMode.None,
            "lax"      => SameSiteMode.Lax,
            "strict"   => SameSiteMode.Strict,
            var other  => throw new InvalidOperationException(
                $"Auth:CookieSameSite = '{other}' est invalide. Valeurs acceptées : None, Lax, Strict."),
        });

    public CookieOptions Build(DateTimeOffset? expires = null, string? path = null)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            // Toujours Secure : SameSite=None l'exige, et la production est en HTTPS.
            Secure   = true,
            SameSite = SameSite,
        };
        if (expires is not null) options.Expires = expires;
        if (path is not null) options.Path = path;
        return options;
    }
}
