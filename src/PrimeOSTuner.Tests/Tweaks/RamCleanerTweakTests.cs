using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class RamCleanerTweakTests
{
    [Fact]
    public async Task Apply_calls_TrimAllUserProcesses_when_protect_list_is_empty()
    {
        var client = new Mock<IProcessClient>();
        client.Setup(c => c.TrimAllUserProcesses()).Returns(123);

        var trimmer = new Mock<IWorkingSetTrimmer>();
        var tweak = new RamCleanerTweak(client.Object, new EmptyRamCleanerProtectList(), trimmer.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("123");
        client.Verify(c => c.TrimAllUserProcesses(), Times.Once);
        client.Verify(c => c.TrimUserProcessesExcept(It.IsAny<IReadOnlyCollection<string>>()), Times.Never);
        trimmer.Verify(t => t.EmptyStandbyList(), Times.Once);
    }

    [Fact]
    public async Task Probe_always_returns_NotApplied_since_RAM_refills()
    {
        var tweak = new RamCleanerTweak(Mock.Of<IProcessClient>(), new EmptyRamCleanerProtectList(), Mock.Of<IWorkingSetTrimmer>());
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task Apply_routes_through_TrimUserProcessesExcept_when_protect_list_has_entries()
    {
        var client = new Mock<IProcessClient>();
        client.Setup(c => c.TrimUserProcessesExcept(It.IsAny<IReadOnlyCollection<string>>())).Returns(50);
        var protectList = new Mock<IRamCleanerProtectList>();
        protectList.Setup(p => p.Get()).Returns(new[] { @"C:\Discord\Discord.exe" });

        var tweak = new RamCleanerTweak(client.Object, protectList.Object, Mock.Of<IWorkingSetTrimmer>());
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("50");
        client.Verify(c => c.TrimUserProcessesExcept(It.Is<IReadOnlyCollection<string>>(
            paths => paths.Contains(@"C:\Discord\Discord.exe"))), Times.Once);
        client.Verify(c => c.TrimAllUserProcesses(), Times.Never);
    }
}
