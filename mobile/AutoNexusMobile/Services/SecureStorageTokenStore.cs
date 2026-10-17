using System.Text.Json;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Auth;

namespace AutoNexusMobile.Services;

/// <summary>Session gardée dans le stockage chiffré du système (Keystore sur Android).</summary>
public sealed class SecureStorageTokenStore : ITokenStore
{
    private const string Key = "autonexus-session";

    public async Task<AuthSession?> LoadAsync()
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync(Key);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<AuthSession>(json, ApiJson.Options);
        }
        catch (Exception)
        {
            // Clé du Keystore perdue (réinstallation, sauvegarde restaurée) : on repart sans session.
            SecureStorage.Default.Remove(Key);
            return null;
        }
    }

    public Task SaveAsync(AuthSession session) =>
        SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(session, ApiJson.Options));

    public Task ClearAsync()
    {
        SecureStorage.Default.Remove(Key);
        return Task.CompletedTask;
    }
}
