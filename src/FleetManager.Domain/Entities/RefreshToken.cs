using System.Security.Cryptography;
using System.Text;

namespace FleetManager.Domain.Entities;

/// <summary>
/// Refresh token. Seule son empreinte SHA-256 est enregistrée : une fuite de la table ne donne
/// aucun jeton utilisable. Les jetons issus d'une même connexion partagent une famille, révoquée
/// en bloc si un jeton déjà consommé revient (signe qu'il a été volé).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    private RefreshToken() { }

    /// <summary>Crée un jeton et renvoie sa valeur en clair, seule copie qui quitte le serveur.</summary>
    public static (RefreshToken Entity, string Token) Create(Guid userId, Guid? familyId = null, int expiryDays = 7)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var entity = new RefreshToken
        {
            Id        = Guid.NewGuid(),
            TokenHash = Hash(token),
            FamilyId  = familyId ?? Guid.NewGuid(),
            UserId    = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IsRevoked = false
        };
        return (entity, token);
    }

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive  => !IsRevoked && !IsExpired;

    public void Revoke() => IsRevoked = true;
}
