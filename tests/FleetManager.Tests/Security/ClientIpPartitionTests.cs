using System.Net;
using FleetManager.Api.Infrastructure;
using FluentAssertions;

namespace FleetManager.Tests.Security;

/// <summary>
/// Clé des limites de débit. En IPv6, un script tirait une adresse neuve de son /64 à chaque
/// requête et obtenait une fenêtre vierge : la limite doit porter sur le préfixe.
/// </summary>
public class ClientIpPartitionTests
{
    [Fact]
    public void EtantDonneDeuxAdressesDuMemeSlash64_QuandPartitionner_AlorsMemeCle()
    {
        var first  = ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:2:aaaa:bbbb:cccc:1"));
        var second = ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:2:1234:5678:9abc:def0"));

        first.Should().Be(second).And.Be("2001:db8:1:2::/64");
    }

    [Fact]
    public void EtantDonneDeuxSlash64Differents_QuandPartitionner_AlorsClesDifferentes()
    {
        ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:2::1"))
            .Should().NotBe(ClientIpForwarding.PartitionKey(IPAddress.Parse("2001:db8:1:3::1")));
    }

    [Theory]
    [InlineData("203.0.113.7", "203.0.113.7")]
    [InlineData("::ffff:203.0.113.7", "203.0.113.7")]
    public void EtantDonneIPv4_QuandPartitionner_AlorsAdresseSeule(string address, string expected)
    {
        ClientIpForwarding.PartitionKey(IPAddress.Parse(address)).Should().Be(expected);
    }

    [Fact]
    public void EtantDonneAdresseInconnue_QuandPartitionner_AlorsCleCommune()
    {
        ClientIpForwarding.PartitionKey((IPAddress?)null).Should().Be("inconnue");
    }
}
