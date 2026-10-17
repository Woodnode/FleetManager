using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Queries;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using Moq;

namespace FleetManager.Tests.Application;

public class GetMyInterventionsQueryTests
{
    private readonly Mock<IInterventionRepository> _repo        = new();
    private readonly Mock<ICurrentUserService>     _currentUser = new();

    private static readonly Guid StoreId = Guid.NewGuid();
    private static readonly Guid UserId  = Guid.NewGuid();

    private GetMyInterventionsQueryHandler BuildHandler() => new(_repo.Object, _currentUser.Object);

    private void ActAs(UserRole role)
    {
        _currentUser.Setup(c => c.Role).Returns(role);
        _currentUser.Setup(c => c.UserId).Returns(UserId);
        _currentUser.Setup(c => c.StoreId).Returns(StoreId);
        _currentUser.Setup(c => c.IsAdmin).Returns(role == UserRole.Admin);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.StoreManager)]
    public async Task EtantDonneNonTechnicien_QuandMesInterventions_AlorsRefuseSansLireLaBase(UserRole role)
    {
        ActAs(role);

        var result = await BuildHandler().Handle(new GetMyInterventionsQuery(), default);

        result.IsFailure.Should().BeTrue();
        _repo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EtantDonneTechnicienSansPeriode_QuandMesInterventions_AlorsJourUtcCourantPourLuiEtSonEnseigne()
    {
        ActAs(UserRole.Technician);
        var today = DateTime.UtcNow.Date;
        _repo.Setup(r => r.GetTechnicianAgendaAsync(UserId, StoreId, today, today.AddDays(1), default))
             .ReturnsAsync(Array.Empty<Intervention>());

        var result = await BuildHandler().Handle(new GetMyInterventionsQuery(), default);

        result.IsSuccess.Should().BeTrue();
        _repo.Verify(r => r.GetTechnicianAgendaAsync(UserId, StoreId, today, today.AddDays(1), default), Times.Once);
    }

    [Fact]
    public async Task EtantDonnePeriodeLocale_QuandMesInterventions_AlorsBornesTransmises()
    {
        ActAs(UserRole.Technician);
        // Journée à Montréal (UTC-4) : de 04:00 UTC au lendemain 04:00 UTC.
        var from = new DateTime(2026, 10, 17, 4, 0, 0, DateTimeKind.Utc);
        var to   = from.AddDays(1);
        var tech = User.Create("Lucas", "Moreau", "tech@fleet.fr", "$2b$12$FakeHash", UserRole.Technician, StoreId);
        var intervention = Intervention.Create(Guid.NewGuid(), StoreId, tech, InterventionType.Repair, from.AddHours(9), from.AddHours(11));
        _repo.Setup(r => r.GetTechnicianAgendaAsync(UserId, StoreId, from, to, default))
             .ReturnsAsync(new[] { intervention });

        var result = await BuildHandler().Handle(new GetMyInterventionsQuery(from, to), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Id.Should().Be(intervention.Id);
    }

    [Fact]
    public async Task EtantDonneBornesSansFuseau_QuandMesInterventions_AlorsCompareesTellesQuelles()
    {
        // Le mobile envoie le début et la fin de sa journée en heure murale, comme les dates stockées.
        ActAs(UserRole.Technician);
        var from = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Unspecified);
        var to   = from.AddDays(1);
        _repo.Setup(r => r.GetTechnicianAgendaAsync(UserId, StoreId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), default))
             .ReturnsAsync(Array.Empty<Intervention>());

        await BuildHandler().Handle(new GetMyInterventionsQuery(from, to), default);

        _repo.Verify(r => r.GetTechnicianAgendaAsync(UserId, StoreId,
            It.Is<DateTime>(d => d == new DateTime(2026, 10, 17, 0, 0, 0)),
            It.Is<DateTime>(d => d == new DateTime(2026, 10, 18, 0, 0, 0)), default), Times.Once);
    }

    [Fact]
    public async Task EtantDonneFinAvantDebut_QuandMesInterventions_AlorsErreurDeValidation()
    {
        ActAs(UserRole.Technician);
        var from = DateTime.UtcNow;

        var result = await BuildHandler().Handle(new GetMyInterventionsQuery(from, from.AddMinutes(-1)), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("VALIDATION");
    }

    [Fact]
    public async Task EtantDonnePeriodeDePlusDeSeptJours_QuandMesInterventions_AlorsErreurDeValidation()
    {
        ActAs(UserRole.Technician);
        var from = DateTime.UtcNow;

        var result = await BuildHandler().Handle(new GetMyInterventionsQuery(from, from.AddDays(8)), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("VALIDATION");
        _repo.VerifyNoOtherCalls();
    }
}
