using FluentAssertions;
using PrimeOSTuner.Win.Network;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class NetworkInterfaceClientTests
{
    [Fact]
    public void EnumerateActiveInterfaceGuids_returns_at_least_one_on_a_connected_machine()
    {
        var client = new NetworkInterfaceClient();
        var guids = client.EnumerateActiveInterfaceGuids();
        guids.Should().NotBeNull();
    }
}
