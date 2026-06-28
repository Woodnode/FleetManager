using FleetManager.Application.Interfaces;
using FleetManager.Application.Services;
using FleetManager.Application.Vehicles.Queries;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité IDOR (Insecure Direct Object Reference) sur les queries Vehicles.
/// </summary>
public class IdorVehicleQueryTests
{
    private readonly Mock<IVehicleRepository>   _vehicleRepoMock = new();
    private readonly Mock<IStoreRepository>     _storeRepoMock   = new();
    private readonly Mock<ICurrentUserService>  _currentUserMock = new();
    private readonly IStoreAuthorizationService _authService     = new StoreAuthorizationService();

    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();

    private static Vehicle BuildVehicle(Guid storeId)
        => Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 0, storeId);

    private void SetCurrentUser(UserRole? role, Guid? storeId)
    {
        _currentUserMock.Setup(s => s.Role).Returns(role);
        _currentUserMock.Setup(s => s.StoreId).Returns(storeId);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(role == UserRole.Admin);
    }

    // ── GetAllVehiclesQuery ───────────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Admin_RetourneTousLesVehicules()
    {
        SetCurrentUser(UserRole.Admin, null);
        var all = new List<Vehicle> { BuildVehicle(StoreA), BuildVehicle(StoreB) };
        _vehicleRepoMock
            .Setup(r => r.GetPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(((IReadOnlyList<Vehicle>)all, all.Count));
        var handler = new GetAllVehiclesQueryHandler(_vehicleRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllVehiclesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        _vehicleRepoMock.Verify(r => r.GetPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Once);
    }

    [Fact]
    public async Task GetAll_NonAdmin_RetourneSeulementSonEnseigne()
    {
        SetCurrentUser(UserRole.Technician, StoreA);
        var storeAVehicles = new List<Vehicle> { BuildVehicle(StoreA) };
        _vehicleRepoMock
            .Setup(r => r.GetPagedAsync(StoreA, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(((IReadOnlyList<Vehicle>)storeAVehicles, storeAVehicles.Count));
        var handler = new GetAllVehiclesQueryHandler(_vehicleRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllVehiclesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        _vehicleRepoMock.Verify(r => r.GetPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAll_SansEnseigne_RetourneListeVide()
    {
        SetCurrentUser(UserRole.Technician, null);
        var handler = new GetAllVehiclesQueryHandler(_vehicleRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllVehiclesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        _vehicleRepoMock.Verify(r => r.GetPagedAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAll_RoleNull_RetourneListeVide()
    {
        //Étant donné — rôle absent du JWT : aucune donnée ne doit être retournée
        SetCurrentUser(null, StoreA);
        var handler = new GetAllVehiclesQueryHandler(_vehicleRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllVehiclesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        _vehicleRepoMock.Verify(r => r.GetPagedAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    // ── GetVehicleByIdQuery ───────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Admin_PeutAccederANimporteQuelVehicule()
    {
        SetCurrentUser(UserRole.Admin, StoreA);
        var vehicle = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicle.Id, default)).ReturnsAsync(vehicle);
        var handler = new GetVehicleByIdQueryHandler(_vehicleRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetVehicleByIdQuery(vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_NonAdmin_PeutAccederAuxVehiculesDeSonEnseigne()
    {
        SetCurrentUser(UserRole.Technician, StoreA);
        var vehicle = BuildVehicle(StoreA);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicle.Id, default)).ReturnsAsync(vehicle);
        var handler = new GetVehicleByIdQueryHandler(_vehicleRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetVehicleByIdQuery(vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_NonAdmin_NeVoitPasLesVehiculesDesAutresEnseignes()
    {
        SetCurrentUser(UserRole.Technician, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);
        var handler = new GetVehicleByIdQueryHandler(_vehicleRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetVehicleByIdQuery(vehicleStoreB.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetById_RoleNull_MemeEnseigne_AccesRefuse()
    {
        //Étant donné — rôle absent mais storeId correspondant : doit quand même être refusé
        SetCurrentUser(null, StoreA);
        var vehicle = BuildVehicle(StoreA);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicle.Id, default)).ReturnsAsync(vehicle);
        var handler = new GetVehicleByIdQueryHandler(_vehicleRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetVehicleByIdQuery(vehicle.Id), default);

        result.IsFailure.Should().BeTrue();
    }

    // ── GetVehiclesByStoreQuery ───────────────────────────────────────────────

    [Fact]
    public async Task GetByStore_NonAdmin_NePeutPasInterrogerAutreEnseigne()
    {
        SetCurrentUser(UserRole.StoreManager, StoreA);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreB, default)).ReturnsAsync(true);
        var handler = new GetVehiclesByStoreQueryHandler(_vehicleRepoMock.Object, _storeRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetVehiclesByStoreQuery(StoreB), default);

        result.IsFailure.Should().BeTrue();
        _vehicleRepoMock.Verify(r => r.GetByStoreIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task GetByStore_Admin_PeutInterrogerNimporteQuelleEnseigne()
    {
        SetCurrentUser(UserRole.Admin, StoreA);
        _storeRepoMock.Setup(r => r.ExistsAsync(StoreB, default)).ReturnsAsync(true);
        _vehicleRepoMock.Setup(r => r.GetByStoreIdAsync(StoreB, default)).ReturnsAsync([BuildVehicle(StoreB)]);
        var handler = new GetVehiclesByStoreQueryHandler(_vehicleRepoMock.Object, _storeRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetVehiclesByStoreQuery(StoreB), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
    }
}
