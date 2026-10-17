using FleetManager.Domain.Entities;
using FleetManager.Domain.Exceptions;
using FluentAssertions;

namespace FleetManager.Tests.Domain;

public class VehicleMileageTests
{
    private static Vehicle BuildVehicle(int mileage)
        => Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2020, mileage, Guid.NewGuid());

    [Fact]
    public void EtantDonneReleveSuperieur_QuandRecordMileage_AlorsKilometrageMisAJour()
    {
        var vehicle = BuildVehicle(50_000);

        vehicle.RecordMileage(50_420);

        vehicle.Mileage.Should().Be(50_420);
    }

    [Fact]
    public void EtantDonneReleveIdentique_QuandRecordMileage_AlorsAccepte()
    {
        var vehicle = BuildVehicle(50_000);

        vehicle.RecordMileage(50_000);

        vehicle.Mileage.Should().Be(50_000);
    }

    [Fact]
    public void EtantDonneReleveInferieur_QuandRecordMileage_AlorsExceptionEtKilometrageInchange()
    {
        var vehicle = BuildVehicle(50_000);

        var act = () => vehicle.RecordMileage(49_999);

        act.Should().Throw<DomainException>().WithMessage("*inférieur*");
        vehicle.Mileage.Should().Be(50_000);
    }
}
