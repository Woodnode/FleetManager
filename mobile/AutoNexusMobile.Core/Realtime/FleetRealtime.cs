using AutoNexusMobile.Core.Auth;
using Microsoft.AspNetCore.SignalR.Client;

namespace AutoNexusMobile.Core.Realtime;

public enum RealtimeStatus { Offline, Connecting, Connected, Reconnecting }

/// <summary>Ensembles de données que le serveur signale comme modifiés (événement « changed »).</summary>
[Flags]
public enum FleetChange
{
    None = 0,
    Vehicles = 1,
    Interventions = 2,
    Stores = 4,
}

/// <summary>
/// Canal temps réel du hub /api/v1/hubs/fleet, comme le RealtimeProvider du web : le serveur
/// n'envoie que « ceci a changé », les écrans rechargent leurs données par l'API, qui applique
/// ses contrôles d'accès. Ouvert à la connexion, fermé à la déconnexion.
/// </summary>
public sealed class FleetRealtime : IAsyncDisposable
{
    public const string ChangedEvent = "changed";

    // Après un échec de démarrage (serveur arrêté), nouvelle tentative au bout de 10 s, comme le web.
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

    private readonly string _hubUrl;
    private readonly SessionManager _session;
    private HubConnection? _connection;
    private CancellationTokenSource? _lifetime;

    public FleetRealtime(string hubUrl, SessionManager session)
    {
        _hubUrl = hubUrl;
        _session = session;
    }

    public RealtimeStatus Status { get; private set; } = RealtimeStatus.Offline;

    public event EventHandler<RealtimeStatus>? StatusChanged;

    public event EventHandler<FleetChange>? Changed;

    /// <summary>Traduit la charge utile du serveur (<c>{ entities: ["vehicles", ...] }</c>).</summary>
    public static FleetChange Parse(IEnumerable<string>? entities)
    {
        var change = FleetChange.None;
        foreach (var entity in entities ?? [])
        {
            change |= entity?.Trim().ToLowerInvariant() switch
            {
                "vehicles" => FleetChange.Vehicles,
                "interventions" => FleetChange.Interventions,
                // Une enseigne modifiée change aussi les noms affichés dans les listes.
                "stores" => FleetChange.Stores | FleetChange.Vehicles | FleetChange.Interventions,
                _ => FleetChange.None,
            };
        }

        return change;
    }

    public async Task StartAsync()
    {
        if (_connection is not null)
            return;

        _lifetime = new CancellationTokenSource();
        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => options.AccessTokenProvider = () => _session.GetAccessTokenAsync())
            .WithAutomaticReconnect()
            .Build();

        _connection.On<ChangedPayload>(ChangedEvent, payload =>
        {
            var change = Parse(payload.Entities);
            if (change != FleetChange.None)
                Changed?.Invoke(this, change);
        });
        _connection.Reconnecting += _ =>
        {
            SetStatus(RealtimeStatus.Reconnecting);
            return Task.CompletedTask;
        };
        _connection.Reconnected += _ =>
        {
            SetStatus(RealtimeStatus.Connected);
            // Des événements ont pu être manqués pendant la coupure : tout recharger.
            Changed?.Invoke(this, FleetChange.Vehicles | FleetChange.Interventions | FleetChange.Stores);
            return Task.CompletedTask;
        };
        // Le serveur ferme la connexion à l'expiration du jeton : on rouvre avec un jeton renouvelé.
        _connection.Closed += error =>
        {
            SetStatus(RealtimeStatus.Offline);
            _ = ConnectLoopAsync(_lifetime.Token);
            return Task.CompletedTask;
        };

        _ = ConnectLoopAsync(_lifetime.Token);
        await Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        _lifetime?.Cancel();
        if (_connection is { } connection)
        {
            _connection = null;
            try
            {
                await connection.DisposeAsync();
            }
            catch (Exception)
            {
                // Fermeture au mieux : la connexion peut déjà être tombée.
            }
        }

        SetStatus(RealtimeStatus.Offline);
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    private async Task ConnectLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _connection is { State: HubConnectionState.Disconnected } connection && _session.Current is not null)
        {
            SetStatus(RealtimeStatus.Connecting);
            try
            {
                await connection.StartAsync(ct);
                SetStatus(RealtimeStatus.Connected);
                return;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                SetStatus(RealtimeStatus.Offline);
                try
                {
                    await Task.Delay(RetryDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private void SetStatus(RealtimeStatus status)
    {
        if (Status != status)
        {
            Status = status;
            StatusChanged?.Invoke(this, status);
        }
    }

    private sealed record ChangedPayload(string[]? Entities);
}
