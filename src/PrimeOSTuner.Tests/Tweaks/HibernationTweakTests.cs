using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class HibernationTweakTests
{
    [Fact]
    public async Task ProbeAsync_returns_Applied_when_HiberbootEnabled_is_zero()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.ReadDword(Microsoft.Win32.RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled")).Returns(0);
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new HibernationTweak(reg.Object, ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ApplyAsync_runs_powercfg_h_off()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.ReadDword(Microsoft.Win32.RegistryHive.LocalMachine,
            "SYSTEM\\CurrentControlSet\\Control\\Power", "HibernateEnabled")).Returns(1);
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new HibernationTweak(reg.Object, ppc.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        ppc.Verify(p => p.RunPowercfg("/h off"), Times.Once);
        result.UndoData.Should().Contain("1");
    }

    [Fact]
    public async Task RevertAsync_runs_powercfg_h_on_when_previously_enabled()
    {
        var reg = new Mock<IRegistryClient>();
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new HibernationTweak(reg.Object, ppc.Object);
        await tweak.RevertAsync("1");
        ppc.Verify(p => p.RunPowercfg("/h on"), Times.Once);
    }
}
