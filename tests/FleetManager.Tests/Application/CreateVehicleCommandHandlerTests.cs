using FleetManager.Application.Interfaces;
using FleetManager.Application.Services;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Application;

public class CreateVehicleCommandHandlerTests
{
    private readonly Mock<IVehicleRepository> _vehicleRepoMock = new();
    private readonly Mock<IStoreRepository>   _storeRepoMock   = new();
    private readonly Mock<IUnitOfWork>        _unitOfWorkMock  = new();
    private readonly Mock<ICurrentUserService> _currentUserMock = new();

    private static readonly Guid StoreId = Guid.NewGuid();
    private const string ValidVin = "1HGBH41JXMN109186";

    private CreateVehicleCommandHandler BuildHandler()
    {
        return new CreateVehicleCommandHandler(
            _vehicleRepoMock.Object, _storeRepoMock.Object,
            new StoreAuthorizationService(), _currentUserMock.Object, _unitOfWorkMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_ShouldReturnSuccessWithVehicleDto()
    {
        _currentUserMock.Setup(s => s.Role).Returns(UserRole.Admin);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(true);
        _vehicleRepoMock.Setup(r => r.ExistsByVinAsync(ValidVin, default)).ReturnsAsync(false);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(true);
        _vehicleRepoMock.Setup(r => r.AddAsync(It.IsAny<Vehicle>(), default)).Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 10000, StoreId);
        var result = await BuildHandler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Vin.Should().Be(ValidVin);
        result.Value.Brand.Should().Be("Toyota");
    }

    [Fact]
    public async Task Handle_WithDuplicateVin_ShouldReturnFailure()
    {
        _currentUserMock.Setup(s => s.Role).Returns(UserRole.Admin);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(true);
        _vehicleRepoMock.Setup(r => r.ExistsByVinAsync(ValidVin, default)).ReturnsAsync(true);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);
        var result = await BuildHandler().Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain(ValidVin);
    }

    [Fact]
    public async Task Handle_WithNonExistentStore_ShouldReturnFailure()
    {
        _currentUserMock.Setup(s => s.Role).Returns(UserRole.Admin);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(true);
        _vehicleRepoMock.Setup(r => r.ExistsByVinAsync(ValidVin, default)).ReturnsAsync(false);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreId, default)).ReturnsAsync(false);

        var command = new CreateVehicleCommand(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);
        var result = await BuildHandler().Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("Store");
    }
}
