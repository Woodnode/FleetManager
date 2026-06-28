using FleetManager.Domain.Enums;

namespace FleetManager.Application.Interfaces;

/// <summary>
/// Centralise la politique d'accès aux ressources rattachées à une enseigne.
/// Admin → accès universel.
/// Rôle absent (null) → accès refusé, même si le storeId correspond.
/// Autre rôle → accès limité à sa propre enseigne.
/// </summary>
public interface IStoreAuthorizationService
{
    bool CanAccessStore(UserRole? callerRole, Guid? callerStoreId, Guid resourceStoreId);
}
