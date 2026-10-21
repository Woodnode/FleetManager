using FleetManager.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FleetManager.Infrastructure.Services;

/// <summary>
/// Protège les comptes semés par <see cref="Persistence.DatabaseSeeder"/> (domaine
/// <c>@fleetmanager.fr</c>), seulement quand la démo publique est active : <c>Demo:PublicAccounts</c>
/// (site vitrine dont la remise à zéro est faite hors de l'API, par une tâche planifiée) ou
/// <c>Demo:ResetEnabled</c> (remise à zéro par l'API elle-même). Hors démo, un administrateur garde
/// la main sur tous les comptes.
/// </summary>
public sealed class DemoAccountPolicy : IDemoAccountPolicy
{
    public const string DemoDomain = "@fleetmanager.fr";

    private readonly bool _demoEnabled;

    public DemoAccountPolicy(IConfiguration configuration) =>
        _demoEnabled = configuration.GetValue<bool>("Demo:PublicAccounts")
                       || configuration.GetValue<bool>("Demo:ResetEnabled");

    public bool IsProtected(string email) =>
        _demoEnabled && email.EndsWith(DemoDomain, StringComparison.OrdinalIgnoreCase);
}
