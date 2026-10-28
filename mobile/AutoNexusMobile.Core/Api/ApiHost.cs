namespace AutoNexusMobile.Core.Api;

/// <summary>
/// Hôte de l'API d'une version publiée : seul HTTPS est accepté. Les jetons partent dans chaque
/// requête ; une compilation avec un hôte en HTTP les enverrait en clair.
/// </summary>
public static class ApiHost
{
    public static string RequireHttps(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException(
                "Fournir l'hôte de l'API à la compilation : dotnet publish -p:AutoNexusApiHost=https://...");

        var trimmed = host.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException(
                $"L'hôte de l'API doit être en HTTPS en Release (reçu « {trimmed} »).");

        return trimmed;
    }
}
