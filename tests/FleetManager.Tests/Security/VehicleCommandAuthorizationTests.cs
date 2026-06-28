using FleetManager.Application.Interfaces;
using FleetManager.Application.Services;
using FleetManager.Application.Validators;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using FluentValidation.TestHelper;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur les commandes Vehicles.
///
/// FAILLES CORRIGÉES :
///   - Create : un non-Admin pouvait spécifier n'importe quel storeId dans le corps de la requête.
///   - Update : un non-Admin pouvait modifier un véhicule d'une autre enseigne.
///   - Update : un non-Admin pouvait réaffecter un véhicule à une autre enseigne (mass assignment).
///   - Delete : un StoreManager pouvait supprimer un véhicule d'une autre enseigne.
///   - ChangeStatus : un non-Admin pouvait changer le statut d'un véhicule d'une autre enseigne.
/// </summary>
public class VehicleCommandAuthorizationTests
{
    private readonly Mock<IVehicleRepository>      _vehicleRepoMock      = new();
    private readonly Mock<IStoreRepository>        _storeRepoMock        = new();
    private readonly Mock<IInterventionRepository> _interventionRepoMock = new();
    private readonly Mock<IUnitOfWork>             _unitOfWorkMock       = new();
    private readonly Mock<ICurrentUserService>     _currentUserMock      = new();
    private readonly IStoreAuthorizationService    _authService          = new StoreAuthorizationService();

    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();

    private static Vehicle BuildVehicle(Guid storeId)
        => Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 0, storeId);

    private void SetCurrentUser(UserRole role, Guid? storeId)
    {
        _currentUserMock.Setup(s => s.Role).Returns(role);
        _currentUserMock.Setup(s => s.StoreId).Returns(storeId);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(role == UserRole.Admin);
    }

    // ── CreateVehicleCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task Create_NonAdmin_PourAutreEnseigne_RetourneEchec()
    {
        //Étant donné — un Technicien de StoreA essaie de créer un véhicule pour StoreB
        SetCurrentUser(UserRole.Technician, StoreA);
        var handler = new CreateVehicleCommandHandler(_vehicleRepoMock.Object, _storeRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);
        var command = new CreateVehicleCommand("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 0, StoreB);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors — refusé avant même de contacter le repository
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("autre enseigne");
        _vehicleRepoMock.Verify(r => r.ExistsByVinAsync(It.IsAny<string>(), default), Times.Never);
        _vehicleRepoMock.Verify(r => r.AddAsync(It.IsAny<Vehicle>(), default), Times.Never);
    }

    [Fact]
    public async Task Create_NonAdmin_PourSaPropresEnseigne_EstAutorise()
    {
        //Étant donné
        SetCurrentUser(UserRole.StoreManager, StoreA);
        _vehicleRepoMock.Setup(r => r.ExistsByVinAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreA, default)).ReturnsAsync(true);
        _vehicleRepoMock.Setup(r => r.AddAsync(It.IsAny<Vehicle>(), default)).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new CreateVehicleCommandHandler(_vehicleRepoMock.Object, _storeRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);
        var command = new CreateVehicleCommand("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 0, StoreA);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Create_Admin_PeutCreerPourNimporteQuelleEnseigne()
    {
        //Étant donné — Admin de StoreA crée un véhicule pour StoreB
        SetCurrentUser(UserRole.Admin, StoreA);
        _vehicleRepoMock.Setup(r => r.ExistsByVinAsync(It.IsAny<string>(), default)).ReturnsAsync(false);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreB, default)).ReturnsAsync(true);
        _vehicleRepoMock.Setup(r => r.AddAsync(It.IsAny<Vehicle>(), default)).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new CreateVehicleCommandHandler(_vehicleRepoMock.Object, _storeRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);
        var command = new CreateVehicleCommand("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 0, StoreB);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
    }

    // ── UpdateVehicleCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task Update_NonAdmin_ModifieVehiculeAutreEnseigne_RetourneEchec()
    {
        //Étant donné — un StoreManager de StoreA essaie de modifier un véhicule de StoreB
        SetCurrentUser(UserRole.StoreManager, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);

        var handler = new UpdateVehicleCommandHandler(_vehicleRepoMock.Object, _storeRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);
        var command = new UpdateVehicleCommand(vehicleStoreB.Id, "Honda", "Civic", 2021, 0, StoreB);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("autre enseigne");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Update_NonAdmin_NePeutPasReaffecterVehiculeAAutreEnseigne()
    {
        //Étant donné — un StoreManager de StoreA essaie de réaffecter son véhicule vers StoreB (mass assignment)
        SetCurrentUser(UserRole.StoreManager, StoreA);
        var vehicleStoreA = BuildVehicle(StoreA);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreA.Id, default)).ReturnsAsync(vehicleStoreA);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreA, default)).ReturnsAsync(true);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new UpdateVehicleCommandHandler(_vehicleRepoMock.Object, _storeRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);

        // L'appelant envoie StoreB dans le corps de la requête mais son JWT indique StoreA
        var command = new UpdateVehicleCommand(vehicleStoreA.Id, "Honda", "Civic", 2021, 0, StoreB);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors — la commande réussit mais le véhicule reste dans StoreA (storeId ignoré pour non-Admin)
        result.IsSuccess.Should().BeTrue();
        result.Value!.StoreId.Should().Be(StoreA);
        _storeRepoMock.Verify(r => r.ExistsAsync(StoreB, default), Times.Never);
    }

    // ── DeleteVehicleCommand ──────────────────────────────────────────────────

    [Fact]
    public async Task Delete_StoreManager_SupprimerVehiculeAutreEnseigne_RetourneEchec()
    {
        //Étant donné — un StoreManager de StoreA essaie de supprimer un véhicule de StoreB
        SetCurrentUser(UserRole.StoreManager, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);

        var handler = new DeleteVehicleCommandHandler(_vehicleRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteVehicleCommand(vehicleStoreB.Id);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("autre enseigne");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Fact]
    public async Task Delete_Admin_PeutSupprimerNimporteQuelVehicule()
    {
        //Étant donné — Admin de StoreA supprime un véhicule de StoreB
        SetCurrentUser(UserRole.Admin, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);
        _vehicleRepoMock.Setup(r => r.Update(vehicleStoreB));
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new DeleteVehicleCommandHandler(_vehicleRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);
        var command = new DeleteVehicleCommand(vehicleStoreB.Id);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors
        result.IsSuccess.Should().BeTrue();
    }

    // ── ChangeVehicleStatusCommand ────────────────────────────────────────────

    [Fact]
    public async Task ChangeStatus_NonAdmin_VehiculeAutreEnseigne_RetourneEchec()
    {
        //Étant donné
        SetCurrentUser(UserRole.Technician, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);

        var handler = new ChangeVehicleStatusCommandHandler(_vehicleRepoMock.Object, _authService, _currentUserMock.Object, _unitOfWorkMock.Object);
        var command = new ChangeVehicleStatusCommand(vehicleStoreB.Id, VehicleStatus.Available);

        //Quand
        var result = await handler.Handle(command, default);

        //Alors
        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("autre enseigne");
    }

    // ── ChangeVehicleStatusCommandValidator (faille 3 : enum non validé) ──────

    [Fact]
    public void ChangeStatusVehicleValidator_StatutInvalide_RetourneErreur()
    {
        //Étant donné — valeur hors de l'enum VehicleStatus
        var validator = new ChangeVehicleStatusCommandValidator();
        var command = new ChangeVehicleStatusCommand(Guid.NewGuid(), (VehicleStatus)999);

        //Quand / Alors
        validator.TestValidate(command)
                 .ShouldHaveValidationErrorFor(x => x.NewStatus)
                 .WithErrorMessage("Le statut fourni est invalide.");
    }

    [Fact]
    public void ChangeStatusVehicleValidator_StatutValide_AucuneErreur()
    {
        var validator = new ChangeVehicleStatusCommandValidator();
        var command = new ChangeVehicleStatusCommand(Guid.NewGuid(), VehicleStatus.Available);

        validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }
}
