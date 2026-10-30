using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FleetManager.Application.Auth.Commands;
using FleetManager.Application.Dashboard.Queries;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Stores.Queries;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FleetManager.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Durcissements de session relevés par l'audit.
///
/// FAILLES CORRIGÉES :
///   - Logout : le JTI n'était révoqué que 15 minutes, quelle que soit la durée de vie du jeton.
///   - Utilisateur sans enseigne : tableau de bord et liste des enseignes renvoyaient tout le parc.
/// </summary>
public class SessionHardeningTests
{
    // ── LogoutCommand ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_RevoqueLeJtiJusquaLExpirationDuJeton()
    {
        //Étant donné — un jeton qui expire dans une heure
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.Jti).Returns("jti-1");
        currentUser.Setup(u => u.AccessTokenExpiresAt).Returns(DateTimeOffset.UtcNow.AddMinutes(60));
        var blacklist = new Mock<ITokenBlacklist>();
        var handler = new LogoutCommandHandler(currentUser.Object, blacklist.Object,
            Mock.Of<IRefreshTokenRepository>(), Mock.Of<IUnitOfWork>());

        //Quand
        await handler.Handle(new LogoutCommand(), default);

        //Alors — révoqué pour toute la durée restante (plus la marge d'horloge), pas 15 minutes
        blacklist.Verify(b => b.Revoke("jti-1",
            It.Is<TimeSpan>(t => t > TimeSpan.FromMinutes(60) && t <= TimeSpan.FromMinutes(61))), Times.Once);
    }

    [Fact]
    public async Task Logout_JetonDejaExpire_RevoqueSeulementLaMargeDHorloge()
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.Jti).Returns("jti-2");
        currentUser.Setup(u => u.AccessTokenExpiresAt).Returns(DateTimeOffset.UtcNow.AddMinutes(-5));
        var blacklist = new Mock<ITokenBlacklist>();
        var handler = new LogoutCommandHandler(currentUser.Object, blacklist.Object,
            Mock.Of<IRefreshTokenRepository>(), Mock.Of<IUnitOfWork>());

        await handler.Handle(new LogoutCommand(), default);

        //Alors — le jeton reste refusé le temps de la tolérance d'horloge, sans plus
        blacklist.Verify(b => b.Revoke("jti-2", TimeSpan.FromMinutes(1)), Times.Once);
    }

    // ── CurrentUserService.AccessTokenExpiresAt ───────────────────────────────

    private static CurrentUserService CurrentUserFor(params Claim[] claims)
    {
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer")) };
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(context);
        return new CurrentUserService(accessor.Object);
    }

    [Fact]
    public void AccessTokenExpiresAt_LuDepuisLeClaimExp()
    {
        var exp = DateTimeOffset.UtcNow.AddMinutes(42).ToUnixTimeSeconds();

        var user = CurrentUserFor(
            new Claim(JwtRegisteredClaimNames.Jti, "jti"),
            new Claim(JwtRegisteredClaimNames.Exp, exp.ToString()));

        user.AccessTokenExpiresAt.Should().Be(DateTimeOffset.FromUnixTimeSeconds(exp));
    }

    [Fact]
    public async Task Logout_SansClaimExp_RevoqueAuMoinsLaDureeDeVieMaximale()
    {
        //Étant donné — un jeton dont l'expiration est illisible
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.Jti).Returns("jti-3");
        currentUser.Setup(u => u.AccessTokenExpiresAt).Returns((DateTimeOffset?)null);
        var blacklist = new Mock<ITokenBlacklist>();
        var handler = new LogoutCommandHandler(currentUser.Object, blacklist.Object,
            Mock.Of<IRefreshTokenRepository>(), Mock.Of<IUnitOfWork>());

        //Quand
        await handler.Handle(new LogoutCommand(), default);

        //Alors — une journée, plus que toute durée de vie configurée
        blacklist.Verify(b => b.Revoke("jti-3", TimeSpan.FromDays(1)), Times.Once);
    }

    // ── Utilisateur sans enseigne ─────────────────────────────────────────────

    private static Mock<ICurrentUserService> OrphanManager()
    {
        var user = new Mock<ICurrentUserService>();
        user.Setup(u => u.Role).Returns(UserRole.StoreManager);
        user.Setup(u => u.StoreId).Returns((Guid?)null);
        user.Setup(u => u.IsAdmin).Returns(false);
        return user;
    }

    [Fact]
    public async Task Enseignes_NonAdminSansEnseigne_ListeVide()
    {
        var stores = new Mock<IStoreRepository>();
        stores.Setup(r => r.GetAllAsync(default))
              .ReturnsAsync(new[] { Store.Create("Paris", "1 rue A", "75001", "Paris") });

        var result = await new GetAllStoresQueryHandler(stores.Object, OrphanManager().Object)
            .Handle(new GetAllStoresQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task TableauDeBord_NonAdminSansEnseigne_RienNEstLuNiRenvoye()
    {
        var vehicles = new Mock<IVehicleRepository>(MockBehavior.Strict);
        var interventions = new Mock<IInterventionRepository>(MockBehavior.Strict);

        var result = await new GetDashboardSummaryQueryHandler(vehicles.Object, interventions.Object, OrphanManager().Object)
            .Handle(new GetDashboardSummaryQuery(), default);

        //Alors — aucun dépôt interrogé avec storeId null (qui voudrait dire « toutes les enseignes »)
        result.IsSuccess.Should().BeTrue();
        result.Value!.Vehicles.Total.Should().Be(0);
        result.Value.RecentInterventions.Should().BeEmpty();
    }
}
