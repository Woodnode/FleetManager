using FleetManager.Api.Infrastructure;
using FluentAssertions;

namespace FleetManager.Tests.Security;

public class JwtSecretValidatorTests
{
    private const string Placeholder = "CHANGE_THIS_TO_A_REAL_SECRET_IN_PRODUCTION_!!";
    private const string StrongSecret = "k3Fz9QvT0pWm7XbN2cRa5LhY8sJd4GuE1iOx6VnB";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenMissing_ShouldThrowInEveryEnvironment(string? secret)
    {
        var inDev  = () => JwtSecretValidator.Validate(secret, isDevelopment: true);
        var inProd = () => JwtSecretValidator.Validate(secret, isDevelopment: false);

        inDev.Should().Throw<InvalidOperationException>().WithMessage("*not configured*");
        inProd.Should().Throw<InvalidOperationException>().WithMessage("*not configured*");
    }

    [Fact]
    public void Validate_WithPlaceholder_InDevelopment_ShouldBeAccepted()
    {
        var secret = JwtSecretValidator.Validate(Placeholder, isDevelopment: true);

        secret.Should().Be(Placeholder);
    }

    [Theory]
    [InlineData(Placeholder)]
    [InlineData("change-me-please-this-is-still-the-example")]
    public void Validate_WithPlaceholder_OutsideDevelopment_ShouldThrow(string secret)
    {
        var act = () => JwtSecretValidator.Validate(secret, isDevelopment: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*example value*");
    }

    [Fact]
    public void Validate_WithShortSecret_OutsideDevelopment_ShouldThrow()
    {
        var act = () => JwtSecretValidator.Validate("too-short-31-characters-secret!", isDevelopment: false);

        act.Should().Throw<InvalidOperationException>().WithMessage("*at least 32*");
    }

    [Fact]
    public void Validate_WithStrongSecret_OutsideDevelopment_ShouldBeAccepted()
    {
        var secret = JwtSecretValidator.Validate(StrongSecret, isDevelopment: false);

        secret.Should().Be(StrongSecret);
    }
}
