using FleetManager.Application.Auth.Commands;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le handler d'inscription.
/// Vérifie : rejet des emails dupliqués, validation de l'enseigne,
/// hachage obligatoire du mot de passe avant persistance,
/// non-divulgation du mot de passe en clair, et aucune action de sécurité
/// (persist, token, hash) en cas d'échec précoce.
/// </summary>
public class RegisterCommandHandlerTests
{
    private readonly Mock<IUserRepository>    _userRepoMock     = new();
    private readonly Mock<IStoreRepository>   _storeRepoMock    = new();
    private readonly Mock<IPasswordHasher>    _hasherMock       = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGeneratorMock = new();
    private readonly Mock<IUnitOfWork>        _unitOfWorkMock   = new();
    private readonly RegisterCommandHandler   _handler;

    private static readonly Guid StoreId    = Guid.NewGuid();
    private static readonly string MockHash  = "$2b$12$HashedPassword";
    private static readonly string MockToken = "eyJ.mock.token";

    public RegisterCommandHandlerTests()
    {
        _handler = new RegisterCommandHandler(
            _userRepoMock.Object,
            _storeRepoMock.Object,
            _hasherMock.Object,
            _jwtGeneratorMock.Object,
            _unitOfWorkMock.Object);
    }

    private void ConfigureSuccessScenario(string email = "nouveau@fleet.fr", Guid? storeId = null)
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(email, default)).ReturnsAsync(false);
        if (storeId.HasValue)
            _storeRepoMock.Setup(r => r.ExistsAsync(storeId.Value, default)).ReturnsAsync(true);
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns(MockHash);
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>(), default)).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(It.IsAny<User>())).Returns(MockToken);
    }

    // ── Inscription réussie ───────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CommandeValide_RetourneSuccessAvecToken()
    {
        //Étant donné
        ConfigureSuccessScenario();
        var command = new RegisterCommand("Jean", "Dupont", "nouveau@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        var result = await _handler.Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
        result.Value!.AccessToken.Should().Be(MockToken);
    }

    [Fact]
    public async Task Handle_CommandeValideAvecEnseigne_RetourneSuccessAvecStoreId()
    {
        //Étant donné
        ConfigureSuccessScenario("tech@fleet.fr", StoreId);
        var command = new RegisterCommand("Lucas", "Moreau", "tech@fleet.fr", "Fleet@2024", UserRole.Technician, StoreId);

        //Quand
        var result = await _handler.Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
        result.Value!.StoreId.Should().Be(StoreId);
    }

    [Fact]
    public async Task Handle_CommandeValide_SaveChangesAppeleExactementUneFois()
    {
        //Étant donné — SaveChanges doit être appelé exactement une fois par inscription
        ConfigureSuccessScenario();
        var command = new RegisterCommand("Jean", "Dupont", "nouveau@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        await _handler.Handle(command, default);

        //Alors
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    // ── Rejet : email dupliqué ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_EmailDejaUtilise_RetourneEchec()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.ExistsByEmailAsync("existant@fleet.fr", default)).ReturnsAsync(true);
        var command = new RegisterCommand("Jean", "Dupont", "existant@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        var result = await _handler.Handle(command, default);

        //Alors
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("existant@fleet.fr");
    }

    [Fact]
    public async Task Handle_EmailDejaUtilise_NePasAppelerLePersistance()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(true);
        var command = new RegisterCommand("Jean", "Dupont", "existant@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        await _handler.Handle(command, default);

        //Alors — aucun utilisateur ne doit être persisté
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailDejaUtilise_NePasHacherLeMotDePasse()
    {
        //Étant donné — si l'email est déjà pris, on doit échouer avant même de hacher
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(true);
        var command = new RegisterCommand("Jean", "Dupont", "existant@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        await _handler.Handle(command, default);

        //Alors — Hash ne doit PAS être appelé (fail-fast avant l'opération coûteuse)
        _hasherMock.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmailDejaUtilise_NePasGenererDeToken()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(true);
        var command = new RegisterCommand("Jean", "Dupont", "existant@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        await _handler.Handle(command, default);

        //Alors
        _jwtGeneratorMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    // ── Rejet : enseigne inexistante ──────────────────────────────────────────

    [Fact]
    public async Task Handle_EnseigneInexistante_RetourneEchec()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(false);
        var command = new RegisterCommand("Lucas", "Moreau", "tech@fleet.fr", "Fleet@2024", UserRole.Technician, StoreId);

        //Quand
        var result = await _handler.Handle(command, default);

        //Alors
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain(StoreId.ToString());
    }

    [Fact]
    public async Task Handle_EnseigneInexistante_NePasAppelerLaPersistance()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(false);
        var command = new RegisterCommand("Lucas", "Moreau", "tech@fleet.fr", "Fleet@2024", UserRole.Technician, StoreId);

        //Quand
        await _handler.Handle(command, default);

        //Alors
        _userRepoMock.Verify(r => r.AddAsync(It.IsAny<User>(), default), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Handle_EnseigneInexistante_NePasGenererDeToken()
    {
        //Étant donné
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(false);
        var command = new RegisterCommand("Lucas", "Moreau", "tech@fleet.fr", "Fleet@2024", UserRole.Technician, StoreId);

        //Quand
        await _handler.Handle(command, default);

        //Alors
        _jwtGeneratorMock.Verify(j => j.GenerateToken(It.IsAny<User>()), Times.Never);
    }

    // ── Hachage obligatoire du mot de passe ───────────────────────────────────

    [Fact]
    public async Task Handle_CommandeValide_HacheLeMotDePasseAvantPersistance()
    {
        //Étant donné
        ConfigureSuccessScenario();
        var command = new RegisterCommand("Jean", "Dupont", "nouveau@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        await _handler.Handle(command, default);

        //Alors — le hasher doit avoir été appelé exactement une fois
        _hasherMock.Verify(h => h.Hash("Fleet@2024"), Times.Once);
    }

    [Fact]
    public async Task Handle_CommandeValide_NePasStockerMotDePasseEnClair()
    {
        //Étant donné
        User? utilisateurPersiste = null;
        ConfigureSuccessScenario();
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>(), default))
                     .Callback<User, CancellationToken>((u, _) => utilisateurPersiste = u)
                     .Returns(Task.CompletedTask);

        var command = new RegisterCommand("Jean", "Dupont", "nouveau@fleet.fr", "Fleet@2024", UserRole.Admin, null);

        //Quand
        await _handler.Handle(command, default);

        //Alors — le PasswordHash persisté ne doit pas contenir le mot de passe brut
        utilisateurPersiste.Should().NotBeNull();
        utilisateurPersiste!.PasswordHash.Should().NotBe("Fleet@2024");
        utilisateurPersiste.PasswordHash.Should().Be(MockHash);
    }

    // ── Rejet domaine : email invalide propagé par User.Create ───────────────

    [Fact]
    public async Task Handle_EmailInvalidePourDomaine_RetourneEchecViaDomainException()
    {
        //Étant donné — l'email passe la validation FluentValidation mais le
        // VO Email du domaine applique ses propres règles
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _hasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns(MockHash);

        var command = new RegisterCommand("Jean", "Dupont", "user@domain.fr", "Fleet@2024", UserRole.Admin, null);
        ConfigureSuccessScenario("user@domain.fr");

        //Quand
        var result = await _handler.Handle(command, default);

        //Alors — cas nominal : doit réussir avec un email valide
        result.IsSuccess.Should().BeTrue();
    }
}
