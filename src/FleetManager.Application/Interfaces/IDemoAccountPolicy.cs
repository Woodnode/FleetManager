namespace FleetManager.Application.Interfaces;

/// <summary>
/// Comptes de démonstration publics du site vitrine. Un visiteur connecté en Admin ne doit pas
/// pouvoir les désactiver : la démo serait fermée à tous jusqu'à la remise à zéro de la nuit.
/// </summary>
public interface IDemoAccountPolicy
{
    bool IsProtected(string email);
}
