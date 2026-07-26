using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class RamCleanerTweakTests
{
    private static SafeRamCleaner CleanerOver(params ProcessSnapshot[] processes)
    {
        var trimmer = new Mock<IWorkingSetTrimmer>();
        trimmer.Setup(t => t.Snapshot()).Returns(processes.ToList());
        trimmer.Setup(t => t.ForegroundPid()).Returns(0);
        return new SafeRamCleaner(trimmer.Object);
    }

    [Fact]
    public async Task Apply_trims_idle_processes_and_reports_the_count()
    {
        var cleaner = CleanerOver(
            new ProcessSnapshot(300, "indexer", 600_000_000, ParentPid: 1),
            new ProcessSnapshot(301, "updater", 400_000_000, ParentPid: 1));

        var protectList = new Mock<IRamCleanerProtectList>();
        protectList.Setup(p => p.Get()).Returns(Array.Empty<string>());
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExes(It.IsAny<IEnumerable<string>>()))
                .Returns(Array.Empty<int>());

        var tweak = new RamCleanerTweak(cleaner, protectList.Object, priority.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("2");
    }

    [Fact]
    public async Task Apply_never_trims_a_windowed_app_even_from_the_manual_button()
    {
        // The manual tile used to call TrimAllUserProcesses(), which protected nothing.
        var trimmer = new Mock<IWorkingSetTrimmer>();
        trimmer.Setup(t => t.Snapshot()).Returns(new[]
        {
            new ProcessSnapshot(100, "Code", 300_000_000, ParentPid: 1, HasVisibleWindow: true),
            new ProcessSnapshot(101, "Code", 400_000_000, ParentPid: 100),
        });
        trimmer.Setup(t => t.ForegroundPid()).Returns(0);

        var protectList = new Mock<IRamCleanerProtectList>();
        protectList.Setup(p => p.Get()).Returns(Array.Empty<string>());
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExes(It.IsAny<IEnumerable<string>>()))
                .Returns(Array.Empty<int>());

        var tweak = new RamCleanerTweak(new SafeRamCleaner(trimmer.Object), protectList.Object, priority.Object);
        await tweak.ApplyAsync();

        trimmer.Verify(t => t.TrimWorkingSet(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Apply_resolves_the_user_protect_list_to_pids()
    {
        var trimmer = new Mock<IWorkingSetTrimmer>();
        trimmer.Setup(t => t.Snapshot()).Returns(new[]
        {
            new ProcessSnapshot(900, "Discord", 500_000_000, ParentPid: 1),
        });
        trimmer.Setup(t => t.ForegroundPid()).Returns(0);

        var protectList = new Mock<IRamCleanerProtectList>();
        protectList.Setup(p => p.Get()).Returns(new[] { @"C:\Discord\Discord.exe" });
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExes(It.Is<IEnumerable<string>>(
                    paths => paths.Contains(@"C:\Discord\Discord.exe"))))
                .Returns(new[] { 900 });

        var tweak = new RamCleanerTweak(new SafeRamCleaner(trimmer.Object), protectList.Object, priority.Object);
        await tweak.ApplyAsync();

        trimmer.Verify(t => t.TrimWorkingSet(900), Times.Never);
    }

    [Fact]
    public async Task Probe_always_returns_NotApplied_since_RAM_refills()
    {
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExes(It.IsAny<IEnumerable<string>>()))
                .Returns(Array.Empty<int>());
        var protectList = new Mock<IRamCleanerProtectList>();
        protectList.Setup(p => p.Get()).Returns(Array.Empty<string>());

        var tweak = new RamCleanerTweak(CleanerOver(), protectList.Object, priority.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public void Description_does_not_claim_the_operation_is_safe()
    {
        var priority = new Mock<IPriorityClient>();
        var protectList = new Mock<IRamCleanerProtectList>();

        var tweak = new RamCleanerTweak(CleanerOver(), protectList.Object, priority.Object);

        tweak.Description.Should().NotContain("safe");
        tweak.Description.Should().NotContain("standby");
    }
}
