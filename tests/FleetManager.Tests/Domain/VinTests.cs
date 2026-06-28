using FleetManager.Domain.Exceptions;
using FleetManager.Domain.ValueObjects;
using FluentAssertions;

namespace FleetManager.Tests.Domain;

public class VinTests
{
    [Fact]
    public void Create_WithValid17CharVin_ShouldSucceed()
    {
        var vin = Vin.Create("1HGBH41JXMN109186");

        vin.Value.Should().Be("1HGBH41JXMN109186");
    }

    [Fact]
    public void Create_ShouldNormalizeToUpperCase()
    {
        var vin = Vin.Create("1hgbh41jxmn109186");

        vin.Value.Should().Be("1HGBH41JXMN109186");
    }

    [Fact]
    public void Create_WithEmptyString_ShouldThrowDomainException()
    {
        var act = () => Vin.Create("");
        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }

    [Theory]
    [InlineData("SHORT")]
    [InlineData("TOOLONGVINWITHMORETHAN17CHARS")]
    public void Create_WithInvalidLength_ShouldThrowDomainException(string value)
    {
        var act = () => Vin.Create(value);
        act.Should().Throw<DomainException>().WithMessage("*17*");
    }

    [Fact]
    public void TwoVinsWithSameValue_ShouldBeEqual()
    {
        var vin1 = Vin.Create("1HGBH41JXMN109186");
        var vin2 = Vin.Create("1hgbh41jxmn109186");

        vin1.Should().Be(vin2);
    }
}
