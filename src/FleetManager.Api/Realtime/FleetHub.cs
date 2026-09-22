using FleetManager.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FleetManager.Api.Realtime;

public static class FleetHubGroups
{
    public const string Admins = "admins";
    public static string Store(Guid storeId) => $"store:{storeId}";
}

/// <summary>
/// Canal temps réel : le serveur prévient les clients qu'un ensemble de données a changé.
/// Chaque connexion rejoint le groupe de son enseigne (ou celui des admins) : un utilisateur
/// n'est jamais prévenu des changements d'une autre enseigne.
/// </summary>
[Authorize]
public sealed class FleetHub : Hub
{
    public const string ChangedEvent = "changed";

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        if (user?.IsInRole("Admin") == true)
            await Groups.AddToGroupAsync(Context.ConnectionId, FleetHubGroups.Admins);
        else if (Guid.TryParse(user?.FindFirst("storeId")?.Value, out var storeId))
            await Groups.AddToGroupAsync(Context.ConnectionId, FleetHubGroups.Store(storeId));

        await base.OnConnectedAsync();
    }
}

public sealed class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<FleetHub> _hub;

    public SignalRRealtimeNotifier(IHubContext<FleetHub> hub) => _hub = hub;

    public Task NotifyAsync(RealtimeChange change, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            entities = change.Entities
                .Select(e => e.ToString().ToLowerInvariant())
                .OrderBy(e => e)
                .ToArray(),
        };

        if (change.Global)
            return _hub.Clients.All.SendAsync(FleetHub.ChangedEvent, payload, cancellationToken);

        var groups = change.StoreIds.Select(FleetHubGroups.Store).Append(FleetHubGroups.Admins).ToList();
        return _hub.Clients.Groups(groups).SendAsync(FleetHub.ChangedEvent, payload, cancellationToken);
    }
}
