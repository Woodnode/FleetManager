using System.Text;
using System.Text.Json;
using AutoNexusMobile.Core.Api;

namespace AutoNexusMobile.Core.Auth;

/// <summary>Session ouverte sur l'appareil : identité, rôle et paire de jetons.</summary>
public sealed record AuthSession(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    UserRole Role,
    Guid? StoreId,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt)
{
    public string FullName => $"{FirstName} {LastName}".Trim();

    public string Initials =>
        string.Concat(new[] { FirstName, LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => char.ToUpperInvariant(part.Trim()[0])));

    public static AuthSession From(SessionResponse response) => new(
        response.UserId,
        response.FirstName,
        response.LastName,
        response.Email,
        response.Role,
        response.StoreId,
        response.AccessToken,
        response.RefreshToken,
        JwtExpiry.Read(response.AccessToken) ?? DateTimeOffset.UtcNow.AddMinutes(15));
}

/// <summary>Lit la date d'expiration (« exp ») d'un JWT sans le valider : le serveur s'en charge.</summary>
public static class JwtExpiry
{
    public static DateTimeOffset? Read(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            return document.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return null;
        }
    }
}

/// <summary>Stockage sécurisé de la session (SecureStorage dans l'application).</summary>
public interface ITokenStore
{
    Task<AuthSession?> LoadAsync();
    Task SaveAsync(AuthSession session);
    Task ClearAsync();
}
