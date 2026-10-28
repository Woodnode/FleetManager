using FleetManager.Domain.Enums;

namespace FleetManager.Application.Interfaces;

public interface ICurrentUserService
{
    Guid?     UserId    { get; }
    Guid?     StoreId   { get; }
    UserRole? Role      { get; }
    bool      IsAdmin   { get; }
    string?   FirstName { get; }
    string?   LastName  { get; }
    string?   Jti       { get; }

    /// <summary>
    /// Expiration du jeton d'accès courant (claim exp, à défaut la durée de vie configurée).
    /// Sert à garder un JTI révoqué en liste noire tant que le jeton reste valide.
    /// </summary>
    DateTimeOffset? TokenExpiresAt { get; }
}
