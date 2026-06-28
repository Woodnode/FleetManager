using FleetManager.Application.Interfaces;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Application;

public class ChangeVehicleStatusCommandTests
{
    private static readonly Guid   StoreId  = Guid.NewGuid();
    private static readonly Guid   VehicleId = Guid.NewGuid();
    private const           string ValidVin  = "1HGBH41JXMN109186";

    private readonly Mock<IVehicleRepository>         _vehicleRepo  = new();
    private readonly Mock<IStoreAuthorizationService> _authService  = new();
    private readonly Mock<ICurrentUserService>        _currentUser  = new();
    private readonly Mock<IUnitOfWork>                _unitOfWork   = new();

    private ChangeVehicleStatusCommandHandler CreateHandler() => new(
        _vehicleRepo.Object,
        _authService.Object,
        _currentUser.Object,
        _unitOfWork.Object);

    private static Vehicle BuildAvailableVehicle()
        => Vehicle.Create(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);

    [Fact]
    public async Task Handle_WhenVehicleExists_ShouldUpdateStatusAndReturnSuccess()
    {
        // Arrange
        var vehicle = BuildAvailableVehicle();
        _vehicleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(vehicle);
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(true);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new ChangeVehicleStatusCommand(VehicleId, VehicleStatus.InIntervention);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("InIntervention");
    }

    [Fact]
    public async Task Handle_WhenVehicleNotFound_ShouldReturnNotFoundFailure()
    {
        // Arrange
        _vehicleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Vehicle?)null);

        var command = new ChangeVehicleStatusCommand(VehicleId, VehicleStatus.Available);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenUserCannotAccessStore_ShouldReturnForbiddenFailure()
    {
        // Arrange
        var vehicle = BuildAvailableVehicle();
        _vehicleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(vehicle);
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(false);

        var command = new ChangeVehicleStatusCommand(VehicleId, VehicleStatus.Available);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_WhenVehicleIsSold_ShouldReturnValidationFailure()
    {
        // Arrange
        var vehicle = BuildAvailableVehicle();
        vehicle.ChangeStatus(VehicleStatus.Sold);

        _vehicleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(vehicle);
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(true);

        var command = new ChangeVehicleStatusCommand(VehicleId, VehicleStatus.Available);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task Handle_WhenStatusChangedSuccessfully_ShouldCallUpdateAndSave()
    {
        // Arrange
        var vehicle = BuildAvailableVehicle();
        _vehicleRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync(vehicle);
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(true);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new ChangeVehicleStatusCommand(VehicleId, VehicleStatus.OutOfService);

        // Act
        await CreateHandler().Handle(command, default);

        // Assert
        _vehicleRepo.Verify(r => r.Update(vehicle), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
