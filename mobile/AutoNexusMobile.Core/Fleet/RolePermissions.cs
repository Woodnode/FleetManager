using AutoNexusMobile.Core.Api;

namespace AutoNexusMobile.Core.Fleet;

/// <summary>
/// Ce que chaque rôle peut faire, aligné sur les attributs [Authorize] de l'API : l'application
/// masque une action que le serveur refuserait au lieu de laisser l'erreur arriver.
/// </summary>
public static class RolePermissions
{
    public static bool IsAdmin(UserRole? role) => role == UserRole.Admin;

    public static bool IsTechnician(UserRole? role) => role == UserRole.Technician;

    /// <summary>Créer, modifier, supprimer un véhicule ; consulter et restaurer les archives.</summary>
    public static bool CanManageVehicles(UserRole? role) => role is UserRole.Admin or UserRole.StoreManager;

    public static bool CanSeeArchives(UserRole? role) => CanManageVehicles(role);

    /// <summary>Changer le statut d'un véhicule de son enseigne : tous les rôles.</summary>
    public static bool CanChangeVehicleStatus(UserRole? role) => role is not null;

    public static bool CanPlanInterventions(UserRole? role) => CanManageVehicles(role);

    public static bool CanManageStores(UserRole? role) => IsAdmin(role);

    /// <summary>Choisir l'enseigne d'un véhicule ou d'une intervention : l'API impose la sienne aux autres rôles.</summary>
    public static bool CanPickStore(UserRole? role) => IsAdmin(role);

    public static string RoleLabel(UserRole role) => role switch
    {
        UserRole.Admin => "Administrateur",
        UserRole.StoreManager => "Gérant d'enseigne",
        UserRole.Technician => "Technicien",
        _ => role.ToString(),
    };

    /// <summary>Transitions offertes depuis un statut, comme les boutons du web.</summary>
    public static IReadOnlyList<InterventionStatus> NextStatuses(InterventionStatus status) => status switch
    {
        InterventionStatus.Planned => [InterventionStatus.InProgress, InterventionStatus.Cancelled],
        InterventionStatus.InProgress => [InterventionStatus.Completed, InterventionStatus.Cancelled],
        _ => [],
    };
}
