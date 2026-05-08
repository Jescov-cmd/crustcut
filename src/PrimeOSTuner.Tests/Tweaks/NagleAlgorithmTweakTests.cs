using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using PrimeOSTuner.Win.Network;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class NagleAlgorithmTweakTests
{
    [Fact]
    public async Task Apply_writes_TcpAckFrequency_and_TCPNoDelay_for_each_interface()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(
                RegistryHive.LocalMachine, It.IsAny<string>(), It.IsAny<string>(), "1"))
            .Returns((RegistryHive h, string s, string n, string v) => new RegistryBackup(h, s, n, null));

        var nics = new Mock<INetworkInterfaceClient>();
        nics.Setup(n => n.EnumerateActiveInterfaceGuids())
            .Returns(new[] { "{AAAAAAAA-1111-2222-3333-444444444444}" });

        var tweak = new NagleAlgorithmTweak(registry.Object, nics.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        var expectedKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{AAAAAAAA-1111-2222-3333-444444444444}";
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, expectedKey, "TcpAckFrequency", "1"), Times.Once);
        registry.Verify(r => r.WriteString(RegistryHive.LocalMachine, expectedKey, "TCPNoDelay", "1"), Times.Once);
    }

    [Fact]
    public void Tweak_requires_elevation()
    {
        var tweak = new NagleAlgorithmTweak(Mock.Of<IRegistryClient>(), Mock.Of<INetworkInterfaceClient>());
        tweak.RequiresElevation.Should().BeTrue();
    }
}
