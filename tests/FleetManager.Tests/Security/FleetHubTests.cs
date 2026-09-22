using System.Security.Claims;
using FleetManager.Api.Realtime;
using FleetManager.Application.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Cloisonnement du temps réel : chaque connexion ne rejoint que le groupe de son enseigne,
/// et chaque signal ne part que vers les enseignes concernées (plus les admins).
/// </summary>
public class FleetHubTests
{
    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();

    private static (FleetHub Hub, Mock<IGroupManager> Groups) CreateHub(params Claim[] claims)
    {
        var context = new Mock<HubCallerContext>();
        context.SetupGet(c => c.ConnectionId).Returns("connexion-1");
        context.SetupGet(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims, "test")));
        var groups = new Mock<IGroupManager>();
        return (new FleetHub { Context = context.Object, Groups = groups.Object }, groups);
    }

    [Fact]
    public async Task Admin_RejointLeGroupeDesAdmins()
    {
        var (hub, groups) = CreateHub(new Claim(ClaimTypes.Role, "Admin"));

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync("connexion-1", FleetHubGroups.Admins, It.IsAny<CancellationToken>()), Times.Once);
        groups.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("StoreManager")]
    [InlineData("Technician")]
    public async Task UtilisateurDEnseigne_RejointSeulementSonEnseigne(string role)
    {
        var (hub, groups) = CreateHub(new Claim(ClaimTypes.Role, role), new Claim("storeId", StoreA.ToString()));

        await hub.OnConnectedAsync();

        groups.Verify(g => g.AddToGroupAsync("connexion-1", FleetHubGroups.Store(StoreA), It.IsAny<CancellationToken>()), Times.Once);
        groups.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UtilisateurSansEnseigne_NeRejointAucunGroupe()
    {
        var (hub, groups) = CreateHub(new Claim(ClaimTypes.Role, "Technician"));

        await hub.OnConnectedAsync();

        groups.VerifyNoOtherCalls();
    }

    private static (SignalRRealtimeNotifier Notifier, Mock<IHubClients> Clients, Mock<IClientProxy> Proxy) CreateNotifier()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.SetupGet(c => c.All).Returns(proxy.Object);
        clients.Setup(c => c.Groups(It.IsAny<IReadOnlyList<string>>())).Returns(proxy.Object);
        var hub = new Mock<IHubContext<FleetHub>>();
        hub.SetupGet(h => h.Clients).Returns(clients.Object);
        return (new SignalRRealtimeNotifier(hub.Object), clients, proxy);
    }

    [Fact]
    public async Task Notification_VersLesEnseignesConcerneesEtLesAdmins()
    {
        var (notifier, clients, proxy) = CreateNotifier();

        await notifier.NotifyAsync(new RealtimeChange(
            new HashSet<RealtimeEntity> { RealtimeEntity.Vehicles },
            new HashSet<Guid> { StoreA, StoreB },
            Global: false));

        clients.Verify(c => c.Groups(It.Is<IReadOnlyList<string>>(g =>
            g.Count == 3 && g.Contains(FleetHubGroups.Store(StoreA)) && g.Contains(FleetHubGroups.Store(StoreB)) && g.Contains(FleetHubGroups.Admins))), Times.Once);
        clients.VerifyGet(c => c.All, Times.Never);
        proxy.Verify(p => p.SendCoreAsync(FleetHub.ChangedEvent, It.Is<object?[]>(a => a.Length == 1), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NotificationGlobale_VersTousLesClients()
    {
        var (notifier, clients, _) = CreateNotifier();

        await notifier.NotifyAsync(new RealtimeChange(
            new HashSet<RealtimeEntity> { RealtimeEntity.Stores },
            new HashSet<Guid>(),
            Global: true));

        clients.VerifyGet(c => c.All, Times.Once);
        clients.Verify(c => c.Groups(It.IsAny<IReadOnlyList<string>>()), Times.Never);
    }
}
