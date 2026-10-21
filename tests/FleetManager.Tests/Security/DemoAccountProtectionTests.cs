using FleetManager.Application.Interfaces;
using FleetManager.Application.Users.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FleetManager.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Comptes de démonstration publics : un visiteur connecté en Admin désactivait les autres comptes
/// de démo (ou l'Admin de démo depuis un second Admin), et la démo restait fermée jusqu'à la nuit.
/// </summary>
public class DemoAccountProtectionTests
{
    private readonly Mock<IUserRepository>         _userRepoMock    = new();
    private readonly Mock<IRefreshTokenRepository> _refreshMock     = new();
    private readonly Mock<ICurrentUserService>     _currentUserMock = new();
    private readonly Mock<IUnitOfWork>             _unitOfWorkMock  = new();

    private static DemoAccountPolicy Policy(bool demoEnabled) => new(
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Demo:ResetEnabled"] = demoEnabled.ToString() })
            .Build());

    private SetUserActiveCommandHandler Handler(IDemoAccountPolicy policy)
    {
        _currentUserMock.Setup(s => s.IsAdmin).Returns(true);
        _currentUserMock.Setup(s => s.UserId).Returns(Guid.NewGuid());
        return new SetUserActiveCommandHandler(_userRepoMock.Object, _refreshMock.Object, _currentUserMock.Object, _unitOfWorkMock.Object, policy);
    }

    private User SeedUser(string email)
    {
        var user = User.Create("Lucas", "Moreau", email, "$2b$12$hash", UserRole.Technician, Guid.NewGuid());
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, default)).ReturnsAsync(user);
        return user;
    }

    [Theory]
    [InlineData("tech1.paris@fleetmanager.fr", true, true)]
    [InlineData("ADMIN@FLEETMANAGER.FR", true, true)]
    [InlineData("tech1.paris@fleetmanager.fr", false, false)]
    [InlineData("nouveau@garage.fr", true, false)]
    public void EtantDonneCourriel_QuandEvaluerPolitique_AlorsProtegeSeulementLesComptesDeDemoEnModeDemo(string email, bool demo, bool expected)
    {
        Policy(demo).IsProtected(email).Should().Be(expected);
    }

    [Fact]
    public async Task EtantDonneDemoActive_QuandDesactiverUnCompteDeDemo_AlorsRefuseSansToucherAuCompte()
    {
        var user = SeedUser("tech1.paris@fleetmanager.fr");

        var result = await Handler(Policy(demoEnabled: true)).Handle(new SetUserActiveCommand(user.Id, false), default);

        result.IsFailure.Should().BeTrue();
        user.IsActive.Should().BeTrue();
        _refreshMock.Verify(r => r.RevokeAllForUserAsync(It.IsAny<Guid>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task EtantDonneDemoActive_QuandDesactiverUnCompteCreeParUnVisiteur_AlorsAutorise()
    {
        var user = SeedUser("nouveau@garage.fr");

        var result = await Handler(Policy(demoEnabled: true)).Handle(new SetUserActiveCommand(user.Id, false), default);

        result.IsSuccess.Should().BeTrue();
        user.IsActive.Should().BeFalse();
    }
}
