using FleetManager.Application.Auth.Commands;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le handler de connexion.
/// Vérifie : authentification correcte, rejet des mauvais identifiants,
/// message d'erreur générique (protection contre l'énumération d'utilisateurs),
/// protection anti-timing (Verify toujours appelé même si user absent),
/// et intégration des dépendances de sécurité.
/// </summary>
public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository>         _userRepoMock           = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepoMock   = new();
    private readonly Mock<IPasswordHasher>         _hasherMock             = new();
    private readonly Mock<IJwtTokenGenerator>      _jwtGeneratorMock       = new();
    private readonly Mock<IUnitOfWork>             _unitOfWorkMock         = new();
    private readonly LoginCommandHandler           _handler;

    private static readonly string MockToken = "eyJ.mock.token";
    private static readonly string ValidHash = "$2b$12$ValidStoredHash";

    public LoginCommandHandlerTests()
    {
        _refreshTokenRepoMock
            .Setup(r => r.AddAsync(It.IsAny<RefreshToken>(), default))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(default))
            .ReturnsAsync(1);

        _handler = new LoginCommandHandler(
            _userRepoMock.Object,
            _refreshTokenRepoMock.Object,
            _hasherMock.Object,
            _jwtGeneratorMock.Object,
            _unitOfWorkMock.Object);
    }

    private User BuildUser(UserRole role = UserRole.Admin)
        => User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, role);

    // ── Authentification réussie ──────────────────────────────────────────────

    [Fact]
    public async Task Handle_IdentifiantsValides_RetourneSuccessAvecToken()
    {
        //Étant donné
        var user = BuildUser();
        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@fleet.fr", default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify("Fleet@2024", ValidHash)).Returns(true);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user)).Returns(MockToken);

        var command = new LoginCommand("admin@fleet.fr", "Fleet@2024");

        //Quand
        var result = await _handler.Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be(MockToken);
    }

    [Fact]
    public async Task Handle_IdentifiantsValides_RetourneEmailEtRoleCorrects()
    {
        //Étant donné
        var user = BuildUser(UserRole.Admin);
        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@fleet.fr", default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), ValidHash)).Returns(true);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user)).Returns(MockToken);

        //Quand
        var result = await _handler.Handle(new LoginCommand("admin@fleet.fr", "Fleet@2024"), default);

        //Alors
        result.Value!.Email.Should().Be("admin@fleet.fr");
        result.Value.Role.Should().Be(UserRole.Admin);
    }

    // ── Rejet : email inconnu ─────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmailInconnu_RetourneEchec()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync((User?)null);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        //Quand
        var result = await _handler.Handle(new LoginCommand("inconnu@fleet.fr", "Fleet@2024"), default);

        //Alors
        result.IsFailure.Should().BeTrue();
    }

    // ── Rejet : mauvais mot de passe ─────────────────────────────────────────

    [Fact]
    public async Task Handle_MauvaisMotDePasse_RetourneEchec()
    {
        //Étant donné
        var user = BuildUser();
        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@fleet.fr", default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify("mauvaisMotDePasse", ValidHash)).Returns(false);

        //Quand
        var result = await _handler.Handle(new LoginCommand("admin@fleet.fr", "mauvaisMotDePasse"), default);

        //Alors
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MotDePasseVide_RetourneEchec()
    {
        //Étant donné — défense en profondeur : le handler rejette même sans le validator
        var user = BuildUser();
        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@fleet.fr", default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify("", ValidHash)).Returns(false);

        //Quand
        var result = await _handler.Handle(new LoginCommand("admin@fleet.fr", ""), default);

        //Alors
        result.IsFailure.Should().BeTrue();
    }

    // ── Message générique (protection énumération d'utilisateurs) ─────────────

    [Fact]
    public async Task Handle_EmailInconnu_RetourneMemeMessageQueMotDePasseIncorrect()
    {
        //Étant donné — pour éviter l'énumération d'utilisateurs, le message doit
        // être identique qu'il s'agisse d'un email inconnu ou d'un mauvais MDP
        _userRepoMock.Setup(r => r.GetByEmailAsync("inconnu@fleet.fr", default))
                     .ReturnsAsync((User?)null);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var user = BuildUser();
        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@fleet.fr", default)).ReturnsAsync(user);

        //Quand
        var echecEmailInconnu = await _handler.Handle(new LoginCommand("inconnu@fleet.fr", "Fleet@2024"), default);
        var echecMauvaisMDP   = await _handler.Handle(new LoginCommand("admin@fleet.fr", "mauvais"), default);

        //Alors
        echecEmailInconnu.Error.Should().Be(echecMauvaisMDP.Error);
    }

    // ── Protection contre le timing attack ───────────────────────────────────

    [Fact]
    public async Task Handle_EmailInconnu_AppelleVerifyPourProtectionTimingAttack()
    {
        //Étant donné — quand l'email est inconnu, Verify DOIT quand même être appelé
        // pour éviter qu'un attaquant distingue "email inconnu" (rapide) de
        // "mauvais mot de passe" (lent = BCrypt) par le temps de réponse.
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync((User?)null);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        //Quand
        await _handler.Handle(new LoginCommand("inconnu@fleet.fr", "Fleet@2024"), default);

        //Alors — Verify doit avoir été appelé exactement une fois même sans user
        _hasherMock.Verify(h => h.Verify(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // ── Le handler ne génère pas de token en cas d'échec ──────────────────────

    [Fact]
    public async Task Handle_EmailInconnu_NAppellePasLaGenerationDeToken()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
                     .ReturnsAsync((User?)null);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        //Quand
        await _handler.Handle(new LoginCommand("inconnu@fleet.fr", "Fleet@2024"), default);

        //Alors
        _jwtGeneratorMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MauvaisMotDePasse_NAppellePasLaGenerationDeToken()
    {
        //Étant donné
        var user = BuildUser();
        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@fleet.fr", default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify(It.IsAny<string>(), ValidHash)).Returns(false);

        //Quand
        await _handler.Handle(new LoginCommand("admin@fleet.fr", "mauvais"), default);

        //Alors
        _jwtGeneratorMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    // ── Vérification que le hash est bien comparé (pas le mot de passe brut) ──

    [Fact]
    public async Task Handle_IdentifiantsValides_VerifieLeHashEtNonLeMotDePasseBrut()
    {
        //Étant donné
        var user = BuildUser();
        _userRepoMock.Setup(r => r.GetByEmailAsync("admin@fleet.fr", default)).ReturnsAsync(user);
        _hasherMock.Setup(h => h.Verify("Fleet@2024", ValidHash)).Returns(true);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user)).Returns(MockToken);

        //Quand
        await _handler.Handle(new LoginCommand("admin@fleet.fr", "Fleet@2024"), default);

        //Alors — Verify doit avoir été appelé avec le hash stocké, pas avec le MDP brut en second argument
        _hasherMock.Verify(h => h.Verify("Fleet@2024", ValidHash), Times.Once);
    }
}
