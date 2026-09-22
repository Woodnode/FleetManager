namespace FleetManager.Api.Infrastructure;

/// <summary>
/// Refuse de démarrer avec une clé de signature JWT faible ou laissée à sa valeur d'exemple.
/// </summary>
/// <remarks>
/// appsettings.json contient une valeur d'exemple (préfixe "CHANGE") pour que l'API démarre
/// en développement sans configuration. Hors développement, la vraie clé doit venir de la
/// variable d'environnement JwtSettings__Secret : si elle manque, l'API utiliserait sinon la
/// valeur d'exemple, publique dans le dépôt, et n'importe qui pourrait forger des jetons.
/// </remarks>
public static class JwtSecretValidator
{
    /// <summary>HMAC-SHA256 exige une clé d'au moins 256 bits.</summary>
    public const int MinimumLength = 32;

    private const string PlaceholderPrefix = "CHANGE";

    public static string Validate(string? secret, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JwtSettings:Secret is not configured.");

        if (isDevelopment)
            return secret;

        if (secret.StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "JwtSettings:Secret still has its example value. Set the JwtSettings__Secret environment variable.");

        if (secret.Length < MinimumLength)
            throw new InvalidOperationException(
                $"JwtSettings:Secret must be at least {MinimumLength} characters long.");

        return secret;
    }
}
