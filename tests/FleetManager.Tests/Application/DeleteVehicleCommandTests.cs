using FleetManager.Application.Interfaces;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Application;

public class DeleteVehicleCommandTests
{
    private static readonly Guid StoreId = Guid.NewGuid();

    private readonly Mock<IVehicleRepository>         _vehicleRepo   = new();
    private readonly Mock<IStoreAuthorizationService> _authorization = new();
    private readonly Mock<ICurrentUserService>        _currentUser   = new();
    private readonly Mock<IUnitOfWork>                _unitOfWork    = new();
    private readonly Mock<IInterventionRepository>    _interventionRepo = new();

    public DeleteVehicleCommandTests()
    {
        _currentUser.Setup(u => u.Role).Returns(UserRole.Admin);
        _authorization
            .Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), It.IsAny<Guid>()))
            .Returns(true);
    }

    private DeleteVehicleCommandHandler CreateHandler() => new(
        _vehicleRepo.Object,
        _interventionRepo.Object,
        _authorization.Object,
        _currentUser.Object,
        _unitOfWork.Object);

    [Fact]
    public async Task Handle_WhenVehicleAvailable_ShouldSoftDeleteAndSave()
    {
        // Arrange
        var vehicle = Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2022, 0, StoreId);
        _vehicleRepo.Setup(r => r.GetByIdAsync(vehicle.Id, default)).ReturnsAsync(vehicle);

        // Act
        var result = await CreateHandler().Handle(new DeleteVehicleCommand(vehicle.Id), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        vehicle.IsDeleted.Should().BeTrue();
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenVehicleInIntervention_ShouldReturnConflictAndNotSave()
    {
        // Arrange
        var vehicle = Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2022, 0, StoreId);
        vehicle.ChangeStatus(VehicleStatus.InIntervention);
        _vehicleRepo.Setup(r => r.GetByIdAsync(vehicle.Id, default)).ReturnsAsync(vehicle);

        // Act
        var result = await CreateHandler().Handle(new DeleteVehicleCommand(vehicle.Id), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONFLICT");
        vehicle.IsDeleted.Should().BeFalse();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenVehicleAvailableButHasPlannedIntervention_ShouldReturnConflictAndNotSave()
    {
        // Arrange : le statut du véhicule ne suffit pas (données de démo, import, incohérence) :
        // c'est l'existence d'une intervention active qui fait foi.
        var vehicle = Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2022, 0, StoreId);
        _vehicleRepo.Setup(r => r.GetByIdAsync(vehicle.Id, default)).ReturnsAsync(vehicle);
        _interventionRepo.Setup(r => r.HasActiveForVehicleAsync(vehicle.Id, default)).ReturnsAsync(true);

        // Act
        var result = await CreateHandler().Handle(new DeleteVehicleCommand(vehicle.Id), default);

        // Assert
        result.Error!.Code.Should().Be("CONFLICT");
        vehicle.IsDeleted.Should().BeFalse();
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
