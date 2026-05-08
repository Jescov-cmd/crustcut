using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Monitoring;

public class SystemSamplerTests
{
    [Fact]
    public async Task Sampler_emits_events_using_hardware_client_data()
    {
        var hw = new Mock<IHardwareClient>();
        hw.Setup(h => h.Snapshot()).Returns(new HardwareSnapshot(
            42, 8L * 1024 * 1024 * 1024, 16L * 1024 * 1024 * 1024, 30, 60, 100, 50));

        using var sampler = new SystemSampler(hw.Object, intervalMs: 50);
        var samples = new List<SystemSample>();
        sampler.Sampled += (_, s) => samples.Add(s);

        sampler.Start();
        await Task.Delay(200);
        sampler.Stop();

        samples.Should().NotBeEmpty();
        samples[0].CpuPercent.Should().Be(42);
        samples[0].RamPercent.Should().BeApproximately(50.0, 0.1);
    }
}
