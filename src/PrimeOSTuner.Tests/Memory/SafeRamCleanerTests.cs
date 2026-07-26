using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

public class SafeRamCleanerTests
{
    private static Mock<IWorkingSetTrimmer> TrimmerWith(
        IEnumerable<ProcessSnapshot> processes, int foregroundPid = 0)
    {
        var trimmer = new Mock<IWorkingSetTrimmer>();
        trimmer.Setup(t => t.Snapshot()).Returns(processes.ToList());
        trimmer.Setup(t => t.ForegroundPid()).Returns(foregroundPid);
        return trimmer;
    }

    [Fact]
    public async Task Never_trims_a_windowed_app_or_any_of_its_children()
    {
        // The reported bug: VS Code owns a window, but its renderers, extension host and
        // language servers do not. Trimming those is what causes the freeze, so the whole
        // process tree has to be protected — not just the process with the window.
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(100, "Code", 300_000_000, ParentPid: 1, HasVisibleWindow: true),
            new ProcessSnapshot(101, "Code", 400_000_000, ParentPid: 100),  // renderer
            new ProcessSnapshot(102, "Code", 250_000_000, ParentPid: 100),  // extension host
            new ProcessSnapshot(103, "node", 500_000_000, ParentPid: 102),  // language server (grandchild)
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(launchingPid: 9999, protectedPids: Array.Empty<int>());

        trimmed.Should().Be(0);
        trimmer.Verify(t => t.TrimWorkingSet(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task Trims_an_idle_windowless_background_process()
    {
        // The feature still has to do something useful, or there is no point keeping it.
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(300, "BackgroundSync", 600_000_000, ParentPid: 1),
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(launchingPid: 9999, protectedPids: Array.Empty<int>());

        trimmed.Should().Be(1);
        trimmer.Verify(t => t.TrimWorkingSet(300), Times.Once);
    }

    [Fact]
    public async Task Never_trims_the_foreground_process_or_its_tree()
    {
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(200, "SomeGame", 800_000_000, ParentPid: 1),
            new ProcessSnapshot(201, "SomeGameHelper", 300_000_000, ParentPid: 200),
        }, foregroundPid: 200);

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(launchingPid: 9999, protectedPids: Array.Empty<int>());

        trimmed.Should().Be(0);
    }

    [Fact]
    public async Task Protects_other_instances_sharing_a_protected_executable_name()
    {
        // A second Code.exe with no window of its own is still VS Code.
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(100, "Code", 300_000_000, ParentPid: 1, HasVisibleWindow: true),
            new ProcessSnapshot(500, "Code", 400_000_000, ParentPid: 1),
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        await cleaner.RunAsync(launchingPid: 9999, protectedPids: Array.Empty<int>());

        trimmer.Verify(t => t.TrimWorkingSet(500), Times.Never);
    }

    [Fact]
    public async Task Honours_launching_pid_explicit_protect_list_system_names_and_threshold()
    {
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(100, "cs2",      500_000_000, ParentPid: 1),  // launching app
            new ProcessSnapshot(200, "discord",  400_000_000, ParentPid: 1),  // explicit protect list
            new ProcessSnapshot(300, "indexer",  600_000_000, ParentPid: 1),  // eligible
            new ProcessSnapshot(400, "smol",      50_000_000, ParentPid: 1),  // below threshold
            new ProcessSnapshot(500, "explorer", 300_000_000, ParentPid: 1),  // shell
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(launchingPid: 100, protectedPids: new[] { 200 });

        trimmed.Should().Be(1);
        trimmer.Verify(t => t.TrimWorkingSet(300), Times.Once);
        trimmer.Verify(t => t.TrimWorkingSet(100), Times.Never);
        trimmer.Verify(t => t.TrimWorkingSet(200), Times.Never);
        trimmer.Verify(t => t.TrimWorkingSet(400), Times.Never);
        trimmer.Verify(t => t.TrimWorkingSet(500), Times.Never);
    }

    [Fact]
    public async Task Never_trims_crustcut_itself()
    {
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(Environment.ProcessId, "Crustcut", 900_000_000, ParentPid: 1),
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(launchingPid: 9999, protectedPids: Array.Empty<int>());

        trimmed.Should().Be(0);
    }

    [Fact]
    public async Task Orphans_are_not_protected_by_sharing_parent_pid_zero()
    {
        // Processes whose parent has exited report ParentPid 0. Pid 0 (System Idle) must
        // never seed the tree walk, or every orphan on the machine becomes protected.
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(0, "Idle",    1_000_000_000, ParentPid: 0),
            new ProcessSnapshot(700, "orphan",  500_000_000, ParentPid: 0),
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(launchingPid: 9999, protectedPids: Array.Empty<int>());

        trimmed.Should().Be(1);
        trimmer.Verify(t => t.TrimWorkingSet(700), Times.Once);
        trimmer.Verify(t => t.TrimWorkingSet(0), Times.Never);
    }

    [Fact]
    public async Task Terminates_on_cyclic_parent_data()
    {
        // Recycled pids can produce a cycle. The walk must not hang.
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(800, "a", 300_000_000, ParentPid: 801, HasVisibleWindow: true),
            new ProcessSnapshot(801, "b", 300_000_000, ParentPid: 800),
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(launchingPid: 9999, protectedPids: Array.Empty<int>());

        trimmed.Should().Be(0);
    }

    [Fact]
    public async Task Respects_cancellation()
    {
        var trimmer = TrimmerWith(new[]
        {
            new ProcessSnapshot(300, "indexer", 600_000_000, ParentPid: 1),
        });
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var cleaner = new SafeRamCleaner(trimmer.Object);
        var trimmed = await cleaner.RunAsync(9999, Array.Empty<int>(), cts.Token);

        trimmed.Should().Be(0);
        trimmer.Verify(t => t.TrimWorkingSet(It.IsAny<int>()), Times.Never);
    }
}
