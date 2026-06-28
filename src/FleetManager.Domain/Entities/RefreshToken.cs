using System.Security.Cryptography;

namespace FleetManager.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }

    private RefreshToken() { }

    public static RefreshToken Create(Guid userId, int expiryDays = 7)
    {
        return new RefreshToken
        {
            Id        = Guid.NewGuid(),
            Token     = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            UserId    = userId,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            IsRevoked = false
        };
    }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsActive  => !IsRevoked && !IsExpired;

    public void Revoke() => IsRevoked = true;
}
