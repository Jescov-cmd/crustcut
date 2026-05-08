using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class RamCleanerTweakTests
{
    [Fact]
    public async Task Apply_calls_TrimAllUserProcesses()
    {
        var client = new Mock<IProcessClient>();
        client.Setup(c => c.TrimAllUserProcesses()).Returns(123);

        var tweak = new RamCleanerTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("123");
        client.Verify(c => c.TrimAllUserProcesses(), Times.Once);
    }

    [Fact]
    public async Task Probe_always_returns_NotApplied_since_RAM_refills()
    {
        var tweak = new RamCleanerTweak(Mock.Of<IProcessClient>());
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }
}
