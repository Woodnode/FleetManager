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
/// Tests de sécurité IDOR sur les archives : un gérant ne doit jamais voir, ni deviner,
/// les véhicules archivés d'une autre enseigne.
/// </summary>
public class IdorArchivedVehicleQueryTests
{
    private readonly Mock<IVehicleRepository>      _vehicleRepoMock      = new();
    private readonly Mock<IInterventionRepository> _interventionRepoMock = new();
    private readonly Mock<ICurrentUserService>     _currentUserMock      = new();
    private readonly IStoreAuthorizationService    _authService          = new StoreAuthorizationService();

    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();

    public IdorArchivedVehicleQueryTests()
    {
        _interventionRepoMock
            .Setup(r => r.CountByArchivedVehicleIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), default))
            .ReturnsAsync(new Dictionary<Guid, int>());
        _interventionRepoMock
            .Setup(r => r.GetArchivedVehicleHistoryAsync(It.IsAny<Guid>(), default))
            .ReturnsAsync(Array.Empty<Intervention>());
    }

    private static Vehicle BuildArchivedVehicle(Guid storeId)
    {
        var vehicle = Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 0, storeId);
        vehicle.SoftDelete();
        return vehicle;
    }

    private void SetCurrentUser(UserRole? role, Guid? storeId)
    {
        _currentUserMock.Setup(s => s.Role).Returns(role);
        _currentUserMock.Setup(s => s.StoreId).Returns(storeId);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(role == UserRole.Admin);
    }

    private GetArchivedVehiclesQueryHandler ListHandler() =>
        new(_vehicleRepoMock.Object, _interventionRepoMock.Object, _currentUserMock.Object);

    private GetArchivedVehicleHistoryQueryHandler HistoryHandler() =>
        new(_vehicleRepoMock.Object, _interventionRepoMock.Object, _authService, _currentUserMock.Object);

    // ── GetArchivedVehiclesQuery ──────────────────────────────────────────────

    [Fact]
    public async Task Liste_Admin_InterrogeToutesLesEnseignes()
    {
        SetCurrentUser(UserRole.Admin, null);
        _vehicleRepoMock
            .Setup(r => r.GetArchivedPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default))
            .ReturnsAsync(((IReadOnlyList<Vehicle>)new[] { BuildArchivedVehicle(StoreA), BuildArchivedVehicle(StoreB) }, 2));

        var result = await ListHandler().Handle(new GetArchivedVehiclesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Liste_Gerant_InterrogeSeulementSonEnseigne()
    {
        SetCurrentUser(UserRole.StoreManager, StoreA);
        _vehicleRepoMock
            .Setup(r => r.GetArchivedPagedAsync(StoreA, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default))
            .ReturnsAsync(((IReadOnlyList<Vehicle>)new[] { BuildArchivedVehicle(StoreA) }, 1));

        var result = await ListHandler().Handle(new GetArchivedVehiclesQuery(), default);

        result.Value!.Items.Should().ContainSingle(v => v.StoreId == StoreA);
        _vehicleRepoMock.Verify(r => r.GetArchivedPagedAsync(StoreA, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Once);
        _vehicleRepoMock.Verify(r => r.GetArchivedPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task Liste_SansEnseigneDansLeJeton_RetourneVideSansInterrogerLaBase()
    {
        SetCurrentUser(UserRole.StoreManager, null);

        var result = await ListHandler().Handle(new GetArchivedVehiclesQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        _vehicleRepoMock.Verify(r => r.GetArchivedPagedAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), default), Times.Never);
    }

    // ── GetArchivedVehicleHistoryQuery ────────────────────────────────────────

    [Fact]
    public async Task Historique_GerantAutreEnseigne_Retourne404SansChargerLHistorique()
    {
        SetCurrentUser(UserRole.StoreManager, StoreA);
        var vehicleOfB = BuildArchivedVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetArchivedByIdAsync(vehicleOfB.Id, default)).ReturnsAsync(vehicleOfB);

        var result = await HistoryHandler().Handle(new GetArchivedVehicleHistoryQuery(vehicleOfB.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("NOT_FOUND");
        _interventionRepoMock.Verify(r => r.GetArchivedVehicleHistoryAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task Historique_GerantMemeEnseigne_RetourneLHistorique()
    {
        SetCurrentUser(UserRole.StoreManager, StoreA);
        var vehicle = BuildArchivedVehicle(StoreA);
        _vehicleRepoMock.Setup(r => r.GetArchivedByIdAsync(vehicle.Id, default)).ReturnsAsync(vehicle);

        var result = await HistoryHandler().Handle(new GetArchivedVehicleHistoryQuery(vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Vehicle.Id.Should().Be(vehicle.Id);
        result.Value.Vehicle.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Historique_VehiculeNonArchive_Retourne404()
    {
        SetCurrentUser(UserRole.Admin, null);
        _vehicleRepoMock.Setup(r => r.GetArchivedByIdAsync(It.IsAny<Guid>(), default)).ReturnsAsync((Vehicle?)null);

        var result = await HistoryHandler().Handle(new GetArchivedVehicleHistoryQuery(Guid.NewGuid()), default);

        result.Error!.Code.Should().Be("NOT_FOUND");
    }
}
