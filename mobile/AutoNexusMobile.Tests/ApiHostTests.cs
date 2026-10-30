using AutoNexusMobile.Core.Api;
using FluentAssertions;

namespace AutoNexusMobile.Tests;

/// <summary>Une version publiée ne doit jamais parler à l'API en HTTP : les jetons partiraient en clair.</summary>
public class ApiHostTests
{
    [Theory]
    [InlineData("https://fleetmanager-jbarret.pages.dev", "https://fleetmanager-jbarret.pages.dev")]
    [InlineData(" https://fleetmanager-jbarret.pages.dev/ ", "https://fleetmanager-jbarret.pages.dev")]
    public void EtantDonneHoteHttps_QuandValider_AlorsHoteNormalise(string host, string expected)
    {
        ApiHost.RequireHttps(host).Should().Be(expected);
    }

    [Theory]
    [InlineData("http://fleetmanager-jbarret.pages.dev")]
    [InlineData("http://10.0.2.2:5290")]
    [InlineData("fleetmanager-jbarret.pages.dev")]
    [InlineData("ftp://fleetmanager-jbarret.pages.dev")]
    public void EtantDonneHoteNonHttps_QuandValider_AlorsRefuse(string host)
    {
        var act = () => ApiHost.RequireHttps(host);

        act.Should().Throw<InvalidOperationException>().WithMessage("*HTTPS*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EtantDonneHoteAbsent_QuandValider_AlorsRefuse(string? host)
    {
        var act = () => ApiHost.RequireHttps(host);

        act.Should().Throw<InvalidOperationException>().WithMessage("*AutoNexusApiHost*");
    }
}
