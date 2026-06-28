using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Queries;
using FleetManager.Application.Services;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité IDOR sur les queries Interventions.
/// </summary>
public class IdorInterventionQueryTests
{
    private readonly Mock<IInterventionRepository> _interventionRepoMock = new();
    private readonly Mock<IVehicleRepository>      _vehicleRepoMock      = new();
    private readonly Mock<ICurrentUserService>     _currentUserMock      = new();
    private readonly IStoreAuthorizationService    _authService          = new StoreAuthorizationService();

    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();
    private static readonly DateTime Start = DateTime.UtcNow.AddDays(1);
    private static readonly DateTime End   = DateTime.UtcNow.AddDays(2);

    private static Intervention BuildIntervention(Guid storeId)
    {
        var tech = User.Create("Lucas", "Moreau", "tech@fleet.fr", "$2b$12$FakeHash", UserRole.Technician, storeId);
        return Intervention.Create(Guid.NewGuid(), storeId, tech, InterventionType.Maintenance, Start, End);
    }

    private static Vehicle BuildVehicle(Guid storeId)
        => Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, 0, storeId);

    private void SetCurrentUser(UserRole? role, Guid? storeId)
    {
        _currentUserMock.Setup(s => s.Role).Returns(role);
        _currentUserMock.Setup(s => s.StoreId).Returns(storeId);
        _currentUserMock.Setup(s => s.IsAdmin).Returns(role == UserRole.Admin);
    }

    // ── GetAllInterventionsQuery ──────────────────────────────────────────────

    [Fact]
    public async Task GetAll_Admin_RetourneToutesLesInterventions()
    {
        SetCurrentUser(UserRole.Admin, null);
        var all = new List<Intervention> { BuildIntervention(StoreA), BuildIntervention(StoreB) };
        _interventionRepoMock
            .Setup(r => r.GetPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(((IReadOnlyList<Intervention>)all, all.Count));
        var handler = new GetAllInterventionsQueryHandler(_interventionRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllInterventionsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(2);
        _interventionRepoMock.Verify(r => r.GetPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Once);
    }

    [Fact]
    public async Task GetAll_NonAdmin_RetourneSeulementSonEnseigne()
    {
        SetCurrentUser(UserRole.Technician, StoreA);
        var storeAInterventions = new List<Intervention> { BuildIntervention(StoreA) };
        _interventionRepoMock
            .Setup(r => r.GetPagedAsync(StoreA, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default))
            .ReturnsAsync(((IReadOnlyList<Intervention>)storeAInterventions, storeAInterventions.Count));
        var handler = new GetAllInterventionsQueryHandler(_interventionRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllInterventionsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().HaveCount(1);
        _interventionRepoMock.Verify(r => r.GetPagedAsync(null, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAll_SansEnseigne_RetourneListeVide()
    {
        SetCurrentUser(UserRole.StoreManager, null);
        var handler = new GetAllInterventionsQueryHandler(_interventionRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllInterventionsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        _interventionRepoMock.Verify(r => r.GetPagedAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    [Fact]
    public async Task GetAll_RoleNull_RetourneListeVide()
    {
        //Étant donné — rôle absent du JWT avec storeId correspondant : aucune donnée retournée
        SetCurrentUser(null, StoreA);
        var handler = new GetAllInterventionsQueryHandler(_interventionRepoMock.Object, _currentUserMock.Object);

        var result = await handler.Handle(new GetAllInterventionsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Items.Should().BeEmpty();
        _interventionRepoMock.Verify(r => r.GetPagedAsync(It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(), It.IsAny<string?>(), default), Times.Never);
    }

    // ── GetInterventionByIdQuery ──────────────────────────────────────────────

    [Fact]
    public async Task GetById_Admin_PeutAccederANimporteQuelleIntervention()
    {
        SetCurrentUser(UserRole.Admin, StoreA);
        var intervention = BuildIntervention(StoreB);
        _interventionRepoMock.Setup(r => r.GetByIdAsync(intervention.Id, default)).ReturnsAsync(intervention);
        var handler = new GetInterventionByIdQueryHandler(_interventionRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetInterventionByIdQuery(intervention.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_NonAdmin_PeutAccederAuxInterventionsDeSonEnseigne()
    {
        SetCurrentUser(UserRole.Technician, StoreA);
        var intervention = BuildIntervention(StoreA);
        _interventionRepoMock.Setup(r => r.GetByIdAsync(intervention.Id, default)).ReturnsAsync(intervention);
        var handler = new GetInterventionByIdQueryHandler(_interventionRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetInterventionByIdQuery(intervention.Id), default);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_NonAdmin_NeVoitPasLesInterventionsDesAutresEnseignes()
    {
        SetCurrentUser(UserRole.Technician, StoreA);
        var interventionStoreB = BuildIntervention(StoreB);
        _interventionRepoMock.Setup(r => r.GetByIdAsync(interventionStoreB.Id, default)).ReturnsAsync(interventionStoreB);
        var handler = new GetInterventionByIdQueryHandler(_interventionRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetInterventionByIdQuery(interventionStoreB.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task GetById_RoleNull_MemeEnseigne_AccesRefuse()
    {
        //Étant donné — rôle absent mais storeId correspondant
        SetCurrentUser(null, StoreA);
        var intervention = BuildIntervention(StoreA);
        _interventionRepoMock.Setup(r => r.GetByIdAsync(intervention.Id, default)).ReturnsAsync(intervention);
        var handler = new GetInterventionByIdQueryHandler(_interventionRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetInterventionByIdQuery(intervention.Id), default);

        result.IsFailure.Should().BeTrue();
    }

    // ── GetInterventionsByVehicleQuery (IDOR) ─────────────────────────────────

    [Fact]
    public async Task GetByVehicle_NonAdmin_NeVoitPasInterventionsVehiculeAutreEnseigne()
    {
        //Étant donné — un Technicien de StoreA tente d'accéder aux interventions d'un véhicule de StoreB
        SetCurrentUser(UserRole.Technician, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);
        var handler = new GetInterventionsByVehicleQueryHandler(_interventionRepoMock.Object, _vehicleRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetInterventionsByVehicleQuery(vehicleStoreB.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("not found");
        _interventionRepoMock.Verify(r => r.GetByVehicleIdAsync(It.IsAny<Guid>(), default), Times.Never);
    }

    [Fact]
    public async Task GetByVehicle_Admin_PeutVoirInterventionsNimporteQuelVehicule()
    {
        //Étant donné
        SetCurrentUser(UserRole.Admin, StoreA);
        var vehicleStoreB = BuildVehicle(StoreB);
        _vehicleRepoMock.Setup(r => r.GetByIdAsync(vehicleStoreB.Id, default)).ReturnsAsync(vehicleStoreB);
        _interventionRepoMock.Setup(r => r.GetByVehicleIdAsync(vehicleStoreB.Id, default))
                             .ReturnsAsync([BuildIntervention(StoreB)]);
        var handler = new GetInterventionsByVehicleQueryHandler(_interventionRepoMock.Object, _vehicleRepoMock.Object, _authService, _currentUserMock.Object);

        var result = await handler.Handle(new GetInterventionsByVehicleQuery(vehicleStoreB.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Should().HaveCount(1);
    }
}
