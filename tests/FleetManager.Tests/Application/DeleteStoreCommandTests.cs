using FleetManager.Application.Interfaces;
using FleetManager.Application.Stores.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Application;

public class DeleteStoreCommandTests
{
    private static readonly Guid StoreId = Guid.NewGuid();

    private readonly Mock<IStoreRepository>   _storeRepo   = new();
    private readonly Mock<IVehicleRepository> _vehicleRepo = new();
    private readonly Mock<IUnitOfWork>        _unitOfWork  = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private DeleteStoreCommandHandler CreateHandler() => new(
        _storeRepo.Object,
        _vehicleRepo.Object,
        _unitOfWork.Object,
        _currentUser.Object);

    [Fact]
    public async Task Handle_AsAdmin_WithEmptyStore_ShouldSucceed()
    {
        // Arrange
        _currentUser.Setup(u => u.IsAdmin).Returns(true);
        var store = Store.Create("Test Store", "1 rue Test", "75001", "Paris");
        _storeRepo.Setup(r => r.GetByIdAsync(StoreId, default)).ReturnsAsync(store);
        _vehicleRepo.Setup(r => r.GetByStoreIdAsync(StoreId, default))
            .ReturnsAsync(Array.Empty<Vehicle>());
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        // Act
        var result = await CreateHandler().Handle(new DeleteStoreCommand(StoreId), default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _storeRepo.Verify(r => r.Remove(store), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task Handle_AsAdmin_WhenStoreHasVehicles_ShouldReturnConflict()
    {
        // Arrange
        _currentUser.Setup(u => u.IsAdmin).Returns(true);
        var store   = Store.Create("Test Store", "", "", "Paris");
        var vehicle = Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2022, 0, StoreId);
        _storeRepo.Setup(r => r.GetByIdAsync(StoreId, default)).ReturnsAsync(store);
        _vehicleRepo.Setup(r => r.GetByStoreIdAsync(StoreId, default))
            .ReturnsAsync(new[] { vehicle });

        // Act
        var result = await CreateHandler().Handle(new DeleteStoreCommand(StoreId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task Handle_AsNonAdmin_ShouldReturnForbidden()
    {
        // Arrange
        _currentUser.Setup(u => u.IsAdmin).Returns(false);

        // Act
        var result = await CreateHandler().Handle(new DeleteStoreCommand(StoreId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task Handle_WhenStoreNotFound_ShouldReturnNotFound()
    {
        // Arrange
        _currentUser.Setup(u => u.IsAdmin).Returns(true);
        _storeRepo.Setup(r => r.GetByIdAsync(StoreId, default)).ReturnsAsync((Store?)null);

        // Act
        var result = await CreateHandler().Handle(new DeleteStoreCommand(StoreId), default);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
    }
}
