using FluentAssertions;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

[Trait("Category", "Integration")]
public class HardwareClientTests
{
    [Fact]
    public void Snapshot_returns_plausible_values()
    {
        using var client = new HardwareClient();
        var snap = client.Snapshot();

        snap.CpuPercent.Should().BeInRange(0, 100);
        snap.RamUsedBytes.Should().BeGreaterThan(0);
        snap.RamTotalBytes.Should().BeGreaterThan(snap.RamUsedBytes);
    }
}
