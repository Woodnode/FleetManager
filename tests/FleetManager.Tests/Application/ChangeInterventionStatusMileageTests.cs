using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Commands;
using FleetManager.Application.Services;
using FleetManager.Application.Validators;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using FluentAssertions;
using FluentValidation.TestHelper;
using MediatR;
using Moq;

namespace FleetManager.Tests.Application;

/// <summary>Relevé du compteur à la clôture d'une intervention (parcours technicien mobile).</summary>
public class ChangeInterventionStatusMileageTests
{
    private readonly Mock<IInterventionRepository> _interventionRepo = new();
    private readonly Mock<IVehicleRepository>      _vehicleRepo      = new();
    private readonly Mock<ICurrentUserService>     _currentUser      = new();
    private readonly Mock<IUnitOfWork>             _unitOfWork       = new();
    private readonly Mock<IPublisher>              _publisher        = new();

    private static readonly Guid StoreId = Guid.NewGuid();

    private readonly Vehicle _vehicle = Vehicle.Create("1HGBH41JXMN109186", "Renault", "Clio", 2021, 42_000, StoreId);
    private readonly Intervention _intervention;

    public ChangeInterventionStatusMileageTests()
    {
        var tech = User.Create("Lucas", "Moreau", "tech@fleet.fr", "$2b$12$FakeHash", UserRole.Technician, StoreId);
        _intervention = Intervention.Create(_vehicle.Id, StoreId, tech, InterventionType.Maintenance,
            DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(2));
        _intervention.Start();
        _vehicle.ChangeStatus(VehicleStatus.InIntervention);

        _currentUser.Setup(c => c.Role).Returns(UserRole.Technician);
        _currentUser.Setup(c => c.StoreId).Returns(StoreId);
        _interventionRepo.Setup(r => r.GetByIdAsync(_intervention.Id, default)).ReturnsAsync(_intervention);
        _vehicleRepo.Setup(r => r.GetByIdAsync(_vehicle.Id, default)).ReturnsAsync(_vehicle);
        _unitOfWork.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);
    }

    private ChangeInterventionStatusCommandHandler BuildHandler() =>
        new(_interventionRepo.Object, _vehicleRepo.Object, new StoreAuthorizationService(),
            _currentUser.Object, _unitOfWork.Object, _publisher.Object);

    [Fact]
    public async Task EtantDonneReleveValide_QuandTerminer_AlorsKilometrageDuVehiculeMisAJour()
    {
        var command = new ChangeInterventionStatusCommand(_intervention.Id, InterventionStatus.Completed, "Vidange faite", 42_350);

        var result = await BuildHandler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _vehicle.Mileage.Should().Be(42_350);
        _vehicle.Status.Should().Be(VehicleStatus.Available);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Once);
    }

    [Fact]
    public async Task EtantDonneSansReleve_QuandTerminer_AlorsKilometrageInchange()
    {
        var command = new ChangeInterventionStatusCommand(_intervention.Id, InterventionStatus.Completed, null);

        var result = await BuildHandler().Handle(command, default);

        result.IsSuccess.Should().BeTrue();
        _vehicle.Mileage.Should().Be(42_000);
    }

    [Fact]
    public async Task EtantDonneReleveInferieur_QuandTerminer_AlorsEchecSansEnregistrement()
    {
        var command = new ChangeInterventionStatusCommand(_intervention.Id, InterventionStatus.Completed, null, 41_000);

        var result = await BuildHandler().Handle(command, default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("VALIDATION");
        _vehicle.Mileage.Should().Be(42_000);
        _unitOfWork.Verify(u => u.SaveChangesAsync(default), Times.Never);
    }

    [Theory]
    [InlineData(InterventionStatus.InProgress)]
    [InlineData(InterventionStatus.Cancelled)]
    public void EtantDonneReleveHorsCloture_QuandValider_AlorsErreur(InterventionStatus status)
    {
        var command = new ChangeInterventionStatusCommand(Guid.NewGuid(), status, "Raison", 10_000);

        new ChangeInterventionStatusCommandValidator().TestValidate(command)
            .ShouldHaveValidationErrorFor(c => c.Mileage);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2_000_001)]
    public void EtantDonneReleveHorsBornes_QuandValider_AlorsErreur(int mileage)
    {
        var command = new ChangeInterventionStatusCommand(Guid.NewGuid(), InterventionStatus.Completed, null, mileage);

        new ChangeInterventionStatusCommandValidator().TestValidate(command)
            .ShouldHaveValidationErrorFor(c => c.Mileage);
    }

    [Fact]
    public void EtantDonneReleveAlaCloture_QuandValider_AlorsAucuneErreur()
    {
        var command = new ChangeInterventionStatusCommand(Guid.NewGuid(), InterventionStatus.Completed, null, 120_000);

        new ChangeInterventionStatusCommandValidator().TestValidate(command)
            .ShouldNotHaveValidationErrorFor(c => c.Mileage);
    }
}
