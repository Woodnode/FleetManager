using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FluentAssertions;

namespace FleetManager.Tests.Domain;

public class InterventionTests
{
    private static readonly Guid VehicleId = Guid.NewGuid();
    private static readonly Guid StoreId   = Guid.NewGuid();
    private static readonly DateTime Start  = DateTime.UtcNow.AddDays(1);
    private static readonly DateTime End    = DateTime.UtcNow.AddDays(2);

    private static User BuildTechnician()
        => User.Create("Lucas", "Moreau", "tech@fleet.fr",
                       "$2b$12$FakeHash", UserRole.Technician, StoreId);

    [Fact]
    public void Create_WithValidData_ShouldHavePlannedStatus()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(), InterventionType.Maintenance, Start, End);

        intervention.Status.Should().Be(InterventionStatus.Planned);
        intervention.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithEndBeforeStart_ShouldThrowDomainException()
    {
        var act = () => Intervention.Create(VehicleId, StoreId, BuildTechnician(), InterventionType.Maintenance, End, Start);

        act.Should().Throw<DomainException>().WithMessage("*end date*");
    }

    [Fact]
    public void Start_FromPlanned_ShouldSetInProgress()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(), InterventionType.Maintenance, Start, End);

        intervention.Start();

        intervention.Status.Should().Be(InterventionStatus.InProgress);
    }

    [Fact]
    public void Start_FromCompleted_ShouldThrowDomainException()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(), InterventionType.Maintenance, Start, End);
        intervention.Start();
        intervention.Complete();

        var act = () => intervention.Start();

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Complete_FromInProgress_ShouldSetCompletedAndActualEndDate()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(), InterventionType.Maintenance, Start, End);
        intervention.Start();

        intervention.Complete("All good");

        intervention.Status.Should().Be(InterventionStatus.Completed);
        intervention.ActualEndDate.Should().NotBeNull();
        intervention.Comment.Should().Be("All good");
    }

    [Fact]
    public void Cancel_WithoutReason_ShouldThrowDomainException()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(), InterventionType.Maintenance, Start, End);

        var act = () => intervention.Cancel("");

        act.Should().Throw<DomainException>().WithMessage("*reason*");
    }

    [Fact]
    public void Cancel_AfterCompleted_ShouldThrowDomainException()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(), InterventionType.Maintenance, Start, End);
        intervention.Start();
        intervention.Complete();

        var act = () => intervention.Cancel("Too late");

        act.Should().Throw<DomainException>().WithMessage("*completed*");
    }

    // ── Invariant d'autorisation : seul un technicien peut être assigné ──────

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.StoreManager)]
    public void Create_AvecNonTechnicien_LanceDomainException(UserRole role)
    {
        // Un Admin ou StoreManager ne peut pas être le technicien d'une intervention.
        // Cette règle est un invariant d'autorisation du domaine — toute tentative
        // de contourner le validator applicatif en passant directement une commande
        // doit quand même être bloquée par le domaine.
        var nonTechnicien = User.Create("Chef", "Store", "manager@fleet.fr",
                                        "$2b$12$FakeHash", role, StoreId);

        var act = () => Intervention.Create(VehicleId, StoreId, nonTechnicien,
                                            InterventionType.Maintenance, Start, End);

        act.Should().Throw<DomainException>().WithMessage("*technicien*");
    }

    // ── Limite de longueur du commentaire ────────────────────────────────────

    [Fact]
    public void Create_AvecCommentaireTropLong_LanceDomainException()
    {
        var commentaireTropLong = new string('A', 1001);

        var act = () => Intervention.Create(VehicleId, StoreId, BuildTechnician(),
                                            InterventionType.Maintenance, Start, End,
                                            commentaireTropLong);

        act.Should().Throw<DomainException>().WithMessage("*1000*");
    }

    [Fact]
    public void Complete_AvecCommentaireTropLong_LanceDomainException()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(),
                                               InterventionType.Maintenance, Start, End);
        intervention.Start();
        var commentaireTropLong = new string('A', 1001);

        var act = () => intervention.Complete(commentaireTropLong);

        act.Should().Throw<DomainException>().WithMessage("*1000*");
    }

    [Fact]
    public void Cancel_AvecCommentaireTropLong_LanceDomainException()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(),
                                               InterventionType.Maintenance, Start, End);
        var commentaireTropLong = new string('A', 1001);

        var act = () => intervention.Cancel(commentaireTropLong);

        act.Should().Throw<DomainException>().WithMessage("*1000*");
    }

    // ── UpdateDetails : uniquement en statut Planned ─────────────────────────

    [Fact]
    public void UpdateDetails_QuandEnCours_LanceDomainException()
    {
        var intervention = Intervention.Create(VehicleId, StoreId, BuildTechnician(),
                                               InterventionType.Maintenance, Start, End);
        intervention.Start();

        var act = () => intervention.UpdateDetails(InterventionType.Repair, Start, End, null);

        act.Should().Throw<DomainException>().WithMessage("*planned*");
    }
}
