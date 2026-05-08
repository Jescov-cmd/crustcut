using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Profiles;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class DashboardViewModelTests
{
    [Fact]
    public void OnSampled_updates_observable_properties()
    {
        var hw = new Mock<IHardwareClient>();
        hw.Setup(h => h.Snapshot()).Returns(new HardwareSnapshot(50, 4_000_000_000, 16_000_000_000, 25, 70, 100, 50));
        using var sampler = new SystemSampler(hw.Object, 50);
        var activeStore = new ActiveTweaksStore(Path.Combine(Path.GetTempPath(), $"active-{Guid.NewGuid()}.json"));

        var vm = new DashboardViewModel(sampler, activeStore);
        sampler.Start();
        Thread.Sleep(250);
        sampler.Stop();

        vm.CpuPercent.Should().BeGreaterThan(0);
        vm.RamPercent.Should().BeApproximately(25.0, 0.5);
    }
}
