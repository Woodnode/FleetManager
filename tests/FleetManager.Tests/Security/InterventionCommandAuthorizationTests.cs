using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Commands;
using FleetManager.Application.Services;
using FleetManager.Application.Validators;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using FluentValidation.TestHelper;
using MediatR;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur les commandes Interventions.
///
/// FAILLES CORRIGÉES :
///   - Create : un non-Admin pouvait créer une intervention pour n'importe quelle enseigne.
///   - Create : le technicien n'était pas vérifié comme appartenant à l'enseigne de l'intervention.
///   - ChangeStatus : le commentaire et le statut n'étaient pas validés (validator manquant).
/// </summary>
public class InterventionCommandAuthorizationTests
{
    private readonly Mock<IInterventionRepository> _interventionRepoMock = new();
    private readonly Mock<IVehicleRepository>      _vehicleRepoMock      = new();
    private readonly Mock<IStoreRepository>        _storeRepoMock        = new();
    private readonly Mock<IUserRepository>         _userRepoMock         = new();
    private readonly Mock<IUnitOfWork>             _unitOfWorkMock       = new();
    private readonly Mock<ICurrentUserService>     _currentUserMock      = new();
    private readonly IStoreAuthorizationService    _authService          = new StoreAuthorizationService();

    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();

    private static readonly DateTime Start = DateTime.UtcNow.AddDays(1);
    private static readonly DateTime End   = DateTime.UtcNow.AddDays(2);

    private void SetCurrentUser(UserRole? role, Guid? storeId)
    {
        _currentUserMock.Setup(s => s.Role).Returns(role);
        _currentUserMock.Setup(s => s.StoreId).Returns(storeId);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(role == UserRole.Admin);
    }

    private CreateInterventionCommandHandler BuildHandler()
        => new(_interventionRepoMock.Object, _vehicleRepoMock.Object,
               _storeRepoMock.Object, _userRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);

    private static User BuildTechnician(Guid storeId)
        => User.Create("Lucas", "Moreau", $"tech+{storeId}@fleet.fr", "$2b$12$FakeHash", UserRole.Technician, storeId);

    private static Vehicle BuildVehicle(Guid storeId)
        => Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 50000, storeId);

    // ── Isolation par enseigne ────────────────────────────────────────────────

    [Fact]
    public async Task Create_NonAdmin_PourAutreEnseigne_RetourneEchec()
    {
        //Étant donné — un StoreManager de StoreA essaie de créer une intervention pour StoreB
        SetCurrentUser(UserRole.StoreManager, StoreA);
        var command = new CreateInterventionCommand(
            Guid.NewGuid(), StoreB, Guid.NewGuid(),
            InterventionType.Maintenance, Start, End, null);

        //Quand
        var result = await BuildHandler().Handle(command, default);

        //Alors — refusé avant de contacter la base de données
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("autre enseigne");
        _vehicleRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_Admin_PeutCreerPourNimporteQuelleEnseigne()
    {
        //Étant donné — Admin de StoreA crée une intervention pour StoreB avec un tech de StoreB
        SetCurrentUser(UserRole.Admin, StoreA);
        var techStoreB    = BuildTechnician(StoreB);
        var vehicleStoreB = BuildVehicle(StoreB);

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreB, default)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(techStoreB.Id, default)).ReturnsAsync(techStoreB);
        _interventionRepoMock.Setup(r => r.AddAsync(It.IsAny<Intervention>(), default)).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new CreateInterventionCommand(
            vehicleStoreB.Id, StoreB, techStoreB.Id,
            InterventionType.Maintenance, Start, End, null);

        //Quand
        var result = await BuildHandler().Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
    }

    // ── Technicien inter-enseigne ─────────────────────────────────────────────

    [Fact]
    public async Task Create_TechnicienDUneAutreEnseigne_RetourneEchec()
    {
        //Étant donné — on essaie d'assigner un technicien de StoreB à une intervention de StoreA
        SetCurrentUser(UserRole.Admin, null);
        var techStoreB    = BuildTechnician(StoreB);
        var vehicleStoreA = BuildVehicle(StoreA);

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreA.Id, default)).ReturnsAsync(vehicleStoreA);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreA, default)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(techStoreB.Id, default)).ReturnsAsync(techStoreB);

        var command = new CreateInterventionCommand(
            vehicleStoreA.Id, StoreA, techStoreB.Id,
            InterventionType.Maintenance, Start, End, null);

        //Quand
        var result = await BuildHandler().Handle(command, default);

        //Alors — le technicien doit appartenir à l'enseigne de l'intervention
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("enseigne");
        _interventionRepoMock.Verify(r => r.AddAsync(It.IsAny<Intervention>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_TechnicienMemeEnseigne_EstAutorise()
    {
        //Étant donné — technicien et intervention dans la même enseigne
        SetCurrentUser(UserRole.Admin, null);
        var techStoreA    = BuildTechnician(StoreA);
        var vehicleStoreA = BuildVehicle(StoreA);

        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreA.Id, default)).ReturnsAsync(vehicleStoreA);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreA, default)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.GetByIdAsync(techStoreA.Id, default)).ReturnsAsync(techStoreA);
        _interventionRepoMock.Setup(r => r.AddAsync(It.IsAny<Intervention>(), default)).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new CreateInterventionCommand(
            vehicleStoreA.Id, StoreA, techStoreA.Id,
            InterventionType.Maintenance, Start, End, null);

        //Quand
        var result = await BuildHandler().Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
    }

    // ── ChangeInterventionStatusCommandValidator ──────────────────────────────

    [Fact]
    public void ChangeStatusValidator_CommentaireTropLong_RetourneErreur()
    {
        //Étant donné — commentaire de 1001 caractères
        var validator = new ChangeInterventionStatusCommandValidator();
        var command = new ChangeInterventionStatusCommand(
            Guid.NewGuid(), InterventionStatus.Completed,
            Comment: new string('A', 1001));

        //Quand / Alors
        validator.TestValidate(command)
                 .ShouldHaveValidationErrorFor(x => x.Comment)
                 .WithErrorMessage("Le commentaire ne peut pas dépasser 1000 caractères.");
    }

    [Fact]
    public void ChangeStatusValidator_StatutInvalide_RetourneErreur()
    {
        //Étant donné — valeur entière hors de l'enum InterventionStatus
        var validator = new ChangeInterventionStatusCommandValidator();
        var command = new ChangeInterventionStatusCommand(
            Guid.NewGuid(), (InterventionStatus)999);

        //Quand / Alors
        validator.TestValidate(command)
                 .ShouldHaveValidationErrorFor(x => x.NewStatus);
    }

    [Fact]
    public void ChangeStatusValidator_CommandeValide_AucuneErreur()
    {
        //Étant donné
        var validator = new ChangeInterventionStatusCommandValidator();
        var command = new ChangeInterventionStatusCommand(
            Guid.NewGuid(), InterventionStatus.Completed,
            Comment: "Intervention terminée.");

        //Quand / Alors
        validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    // ── Faille 1 : véhicule d'une autre enseigne (IDOR CreateIntervention) ───

    [Fact]
    public async Task Create_VehiculeAutreEnseigne_RetourneEchec()
    {
        //Étant donné — StoreManager de StoreA crée une intervention dans StoreA
        //              mais le VehicleId pointe vers un véhicule de StoreB
        SetCurrentUser(UserRole.StoreManager, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreA, default)).ReturnsAsync(true);

        var command = new CreateInterventionCommand(
            vehicleStoreB.Id, StoreA, Guid.NewGuid(),
            InterventionType.Maintenance, Start, End, null);

        //Quand
        var result = await BuildHandler().Handle(command, default);

        //Alors — refusé avec le même message que "véhicule inexistant" (défense en profondeur)
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("introuvable");
        _userRepoMock.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_Admin_VehiculeAutreEnseigne_RetourneEchec()
    {
        //Étant donné — même règle pour l'Admin : un véhicule doit appartenir au store de l'intervention
        SetCurrentUser(UserRole.Admin, null);
        var vehicleStoreA = BuildVehicle(StoreA);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreA.Id, default)).ReturnsAsync(vehicleStoreA);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreB, default)).ReturnsAsync(true);

        var command = new CreateInterventionCommand(
            vehicleStoreA.Id, StoreB, Guid.NewGuid(),
            InterventionType.Maintenance, Start, End, null);

        //Quand
        var result = await BuildHandler().Handle(command, default);

        //Alors
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("introuvable");
    }

    // ── Faille 2 : ChangeInterventionStatus — 404 au lieu de 403 ─────────────

    [Fact]
    public async Task ChangeStatus_InterventionAutreEnseigne_Retourne404PasAutre()
    {
        //Étant donné — une intervention de StoreB, accédée par un user de StoreA
        var interventionRepo = new Mock<IInterventionRepository>();
        var authService      = new StoreAuthorizationService();
        var unitOfWork       = new Mock<IUnitOfWork>();
        var publisher        = new Mock<IPublisher>();
        var currentUserMock  = new Mock<ICurrentUserService>();
        currentUserMock.Setup(s => s.Role).Returns(UserRole.Technician);
        currentUserMock.Setup(s => s.StoreId).Returns(StoreA);
        currentUserMock.Setup(s => s.IsAdmin).Returns(false);

        var tech = User.Create("L", "M", "t@t.fr", "$2b$12$FakeHash", UserRole.Technician, StoreB);
        var intervention = Intervention.Create(Guid.NewGuid(), StoreB, tech, InterventionType.Maintenance, Start, End);
        interventionRepo.Setup(r => r.GetByIdAsync(intervention.Id, default)).ReturnsAsync(intervention);

        var vehicleRepo = new Mock<IVehicleRepository>();
        var handler = new ChangeInterventionStatusCommandHandler(
            interventionRepo.Object, vehicleRepo.Object, authService, currentUserMock.Object, unitOfWork.Object, publisher.Object);

        var result = await handler.Handle(
            new ChangeInterventionStatusCommand(intervention.Id, InterventionStatus.InProgress),
            default);

        //Alors — message identique à "non trouvé" pour ne pas révéler l'existence
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("introuvable");
        result.Error.Message.Should().NotContain("autorisé");
        unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }
}
