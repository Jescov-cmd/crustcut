using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class UltimatePerformanceTweakTests
{
    [Fact]
    public async Task ProbeAsync_returns_Applied_when_powercfg_list_contains_ultimate_guid()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/list"))
           .Returns("Power Scheme GUID: e9a42b02-d5df-448d-aa00-03f14749eb61  (Ultimate Performance) *");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ProbeAsync_returns_NotApplied_when_ultimate_not_in_list()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/list"))
           .Returns("Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task ApplyAsync_runs_duplicatescheme_with_ultimate_guid()
    {
        var ppc = new Mock<IPowerPlanClient>();
        ppc.Setup(p => p.RunPowercfg("/duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61"))
           .Returns("Power Scheme GUID: 11111111-1111-1111-1111-111111111111  (Ultimate Performance)");
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("11111111-1111-1111-1111-111111111111");
    }

    [Fact]
    public async Task RevertAsync_deletes_the_duplicated_plan()
    {
        var ppc = new Mock<IPowerPlanClient>();
        var tweak = new UltimatePerformanceTweak(ppc.Object);
        await tweak.RevertAsync("\"11111111-1111-1111-1111-111111111111\"");
        ppc.Verify(p => p.RunPowercfg("/delete 11111111-1111-1111-1111-111111111111"), Times.Once);
    }
}
