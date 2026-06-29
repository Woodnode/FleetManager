using FleetManager.Application.Interfaces;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Application;

public class CreateVehicleCommandTests
{
    private static readonly Guid   AdminUserId = Guid.NewGuid();
    private static readonly Guid   StoreId     = Guid.NewGuid();
    private const           string ValidVin    = "1HGBH41JXMN109186";

    private readonly Mock<IVehicleRepository>          _vehicleRepo     = new();
    private readonly Mock<IStoreRepository>            _storeRepo       = new();
    private readonly Mock<IStoreAuthorizationService>  _authService     = new();
    private readonly Mock<ICurrentUserService>         _currentUser     = new();
    private readonly Mock<IUnitOfWork>                 _unitOfWork      = new();

    private CreateVehicleCommandHandler CreateHandler() => new(
        _vehicleRepo.Object,
        _storeRepo.Object,
        _authService.Object,
        _currentUser.Object,
        _unitOfWork.Object);

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithVehicleDto()
    {
        // Arrange
        _currentUser.Setup(u => u.IsAdmin).Returns(true);
        _currentUser.Setup(u => u.Role).Returns(UserRole.Admin);
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(true);
        _vehicleRepo.Setup(r => r.ExistsByVinAsync(ValidVin, default)).ReturnsAsync(false);
        _storeRepo.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(true);
        _vehicleRepo.Setup(r => r.AddAsync(It.IsAny<FleetManager.Domain.Entities.Vehicle>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Vin.Should().Be(ValidVin);
        result.Value.Brand.Should().Be("Toyota");
        result.Value.Model.Should().Be("Corolla");
    }

    [Fact]
    public async Task Handle_WhenVinAlreadyExists_ShouldReturnConflictFailure()
    {
        // Arrange
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(true);
        _vehicleRepo.Setup(r => r.ExistsByVinAsync(ValidVin, default)).ReturnsAsync(true);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task Handle_WhenStoreDoesNotExist_ShouldReturnNotFoundFailure()
    {
        // Arrange
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(true);
        _vehicleRepo.Setup(r => r.ExistsByVinAsync(ValidVin, default)).ReturnsAsync(false);
        _storeRepo.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(false);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task Handle_WhenUserNotAuthorizedForStore_ShouldReturnForbiddenFailure()
    {
        // Arrange
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(false);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);

        // Act
        var result = await CreateHandler().Handle(command, default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_WhenVehicleCreated_ShouldCallSaveChanges()
    {
        // Arrange
        _authService.Setup(a => a.CanAccessStore(It.IsAny<UserRole?>(), It.IsAny<Guid?>(), StoreId)).Returns(true);
        _vehicleRepo.Setup(r => r.ExistsByVinAsync(ValidVin, default)).ReturnsAsync(false);
        _storeRepo.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(true);
        _vehicleRepo.Setup(r => r.AddAsync(It.IsAny<FleetManager.Domain.Entities.Vehicle>(), default)).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);

        // Act
        await CreateHandler().Handle(command, default);

        // Assert
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }
}
