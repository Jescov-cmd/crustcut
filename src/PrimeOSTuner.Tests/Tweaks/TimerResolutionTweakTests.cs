using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class TimerResolutionTweakTests
{
    [Fact]
    public async Task Apply_calls_SetResolution_with_5000_hundred_ns_units()
    {
        var client = new Mock<ITimerResolutionClient>();
        client.Setup(c => c.SetResolution(5000)).Returns(5000u);

        var tweak = new TimerResolutionTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetResolution(5000), Times.Once);
    }

    [Fact]
    public async Task Revert_calls_ClearResolution_with_5000()
    {
        var client = new Mock<ITimerResolutionClient>();
        var tweak = new TimerResolutionTweak(client.Object);

        var result = await tweak.RevertAsync("5000");

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.ClearResolution(5000), Times.Once);
    }

    [Fact]
    public async Task Probe_returns_Applied_when_current_resolution_within_tolerance()
    {
        var client = new Mock<ITimerResolutionClient>();
        client.Setup(c => c.GetCurrentResolution()).Returns(5005u);

        var tweak = new TimerResolutionTweak(client.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }
}
