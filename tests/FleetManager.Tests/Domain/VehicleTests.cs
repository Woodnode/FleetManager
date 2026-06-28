using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FluentAssertions;

namespace FleetManager.Tests.Domain;

public class VehicleTests
{
    private static readonly Guid StoreId = Guid.NewGuid();
    private const string ValidVin = "1HGBH41JXMN109186";

    [Fact]
    public void Create_WithValidData_ShouldSucceed()
    {
        var vehicle = Vehicle.Create(ValidVin, "Toyota", "Corolla", 2022, 15000, StoreId);

        vehicle.Vin.Value.Should().Be(ValidVin);
        vehicle.Brand.Should().Be("Toyota");
        vehicle.Model.Should().Be("Corolla");
        vehicle.Year.Should().Be(2022);
        vehicle.Mileage.Should().Be(15000);
        vehicle.Status.Should().Be(VehicleStatus.Available);
        vehicle.StoreId.Should().Be(StoreId);
        vehicle.Id.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_WithEmptyBrand_ShouldThrowDomainException(string brand)
    {
        var act = () => Vehicle.Create(ValidVin, brand, "Corolla", 2022, 0, StoreId);
        act.Should().Throw<DomainException>().WithMessage("*Brand*");
    }

    [Fact]
    public void Create_WithNegativeMileage_ShouldThrowDomainException()
    {
        var act = () => Vehicle.Create(ValidVin, "Toyota", "Corolla", 2022, -1, StoreId);
        act.Should().Throw<DomainException>().WithMessage("*Mileage*");
    }

    [Fact]
    public void Create_WithInvalidVin_ShouldThrowDomainException()
    {
        var act = () => Vehicle.Create("SHORTVIN", "Toyota", "Corolla", 2022, 0, StoreId);
        act.Should().Throw<DomainException>().WithMessage("*17*");
    }

    [Fact]
    public void ChangeStatus_FromAvailableToInIntervention_ShouldSucceed()
    {
        var vehicle = Vehicle.Create(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);

        vehicle.ChangeStatus(VehicleStatus.InIntervention);

        vehicle.Status.Should().Be(VehicleStatus.InIntervention);
    }

    [Fact]
    public void ChangeStatus_FromSold_ShouldThrowDomainException()
    {
        var vehicle = Vehicle.Create(ValidVin, "Toyota", "Corolla", 2022, 0, StoreId);
        vehicle.ChangeStatus(VehicleStatus.Sold);

        var act = () => vehicle.ChangeStatus(VehicleStatus.Available);

        act.Should().Throw<DomainException>().WithMessage("*sold*");
    }

    [Fact]
    public void Update_WithDecreasingMileage_ShouldThrowDomainException()
    {
        var vehicle = Vehicle.Create(ValidVin, "Toyota", "Corolla", 2022, 10000, StoreId);

        var act = () => vehicle.Update("Toyota", "Corolla", 2022, 5000, StoreId);

        act.Should().Throw<DomainException>().WithMessage("*Mileage*");
    }
}
