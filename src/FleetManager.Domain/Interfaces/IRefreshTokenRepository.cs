using FleetManager.Domain.Entities;

namespace FleetManager.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    /// <summary>Retrouve un jeton à partir de sa valeur en clair (comparée par empreinte).</summary>
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Révoque le jeton seulement s'il est encore actif, en une instruction atomique. Renvoie false
    /// si une autre requête l'a consommé entre-temps : un jeton ne sert qu'une fois.
    /// </summary>
    Task<bool> TryConsumeAsync(Guid tokenId, CancellationToken cancellationToken = default);

    /// <summary>Révoque tous les jetons d'une famille (réutilisation d'un jeton consommé).</summary>
    Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
