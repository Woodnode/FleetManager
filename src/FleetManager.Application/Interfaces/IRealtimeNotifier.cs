namespace FleetManager.Application.Interfaces;

/// <summary>Ensembles de données qu'un client doit recharger après une modification.</summary>
public enum RealtimeEntity
{
    Vehicles,
    Interventions,
    Stores,
}

/// <summary>
/// Signal « ces données ont changé » : aucun contenu métier n'est transmis, le client recharge
/// via l'API, qui applique ses propres contrôles d'accès.
/// </summary>
/// <param name="Entities">Ensembles de données modifiés.</param>
/// <param name="StoreIds">Enseignes concernées : seuls leurs utilisateurs (et les admins) sont prévenus.</param>
/// <param name="Global">Vrai quand la modification concerne tout le monde (liste des enseignes).</param>
public sealed record RealtimeChange(
    IReadOnlySet<RealtimeEntity> Entities,
    IReadOnlySet<Guid> StoreIds,
    bool Global);

public interface IRealtimeNotifier
{
    Task NotifyAsync(RealtimeChange change, CancellationToken cancellationToken = default);
}
