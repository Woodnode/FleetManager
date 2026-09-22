using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FleetManager.Infrastructure.Realtime;

/// <summary>
/// Repère ce qui change à chaque SaveChanges (véhicules, interventions, enseignes) et publie un
/// signal temps réel une fois l'enregistrement réussi. Centralisé ici plutôt que dans chaque
/// handler : aucune commande ne peut oublier de prévenir les clients.
/// </summary>
public sealed class RealtimeChangeInterceptor : SaveChangesInterceptor
{
    private readonly IRealtimeNotifier _notifier;
    private readonly ILogger<RealtimeChangeInterceptor> _logger;
    private RealtimeChange? _pending;

    public RealtimeChangeInterceptor(IRealtimeNotifier notifier, ILogger<RealtimeChangeInterceptor> logger)
    {
        _notifier = notifier;
        _logger   = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        _pending = Collect(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        _pending = Collect(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        PublishAsync(CancellationToken.None).GetAwaiter().GetResult();
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await PublishAsync(cancellationToken);
        return result;
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData) => _pending = null;

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending = null;
        return Task.CompletedTask;
    }

    internal static RealtimeChange? Collect(DbContext? context)
    {
        if (context is null) return null;

        var entities = new HashSet<RealtimeEntity>();
        var storeIds = new HashSet<Guid>();
        var global   = false;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
                continue;

            switch (entry.Entity)
            {
                case Vehicle vehicle:
                    entities.Add(RealtimeEntity.Vehicles);
                    AddStores(entry, vehicle.StoreId, nameof(Vehicle.StoreId), storeIds);
                    break;
                case Intervention intervention:
                    entities.Add(RealtimeEntity.Interventions);
                    AddStores(entry, intervention.StoreId, nameof(Intervention.StoreId), storeIds);
                    break;
                case Store:
                    entities.Add(RealtimeEntity.Stores);
                    global = true;
                    break;
            }
        }

        return entities.Count == 0 ? null : new RealtimeChange(entities, storeIds, global);
    }

    // Un transfert d'enseigne concerne l'ancienne et la nouvelle : les deux doivent recharger.
    private static void AddStores(EntityEntry entry, Guid current, string property, HashSet<Guid> storeIds)
    {
        storeIds.Add(current);
        if (entry.State == EntityState.Modified && entry.OriginalValues[property] is Guid original)
            storeIds.Add(original);
    }

    private async Task PublishAsync(CancellationToken cancellationToken)
    {
        var change = _pending;
        _pending = null;
        if (change is null) return;

        // Le temps réel est un confort : son échec ne doit jamais faire échouer la requête métier,
        // déjà enregistrée à ce stade.
        try
        {
            await _notifier.NotifyAsync(change, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Échec de la notification temps réel ({Entities}).", string.Join(", ", change.Entities));
        }
    }
}

/// <summary>Implémentation par défaut (tests, outils) : l'API la remplace par SignalR.</summary>
public sealed class NoOpRealtimeNotifier : IRealtimeNotifier
{
    public Task NotifyAsync(RealtimeChange change, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
