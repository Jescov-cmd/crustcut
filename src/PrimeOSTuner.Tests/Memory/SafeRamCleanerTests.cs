using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

public class SafeRamCleanerTests
{
    [Fact]
    public async Task RunAsync_skips_launching_pid_and_protected_pids()
    {
        var trimmer = new Mock<IWorkingSetTrimmer>();
        trimmer.Setup(t => t.Snapshot()).Returns(new[]
        {
            new ProcessSnapshot(100, "cs2",       500_000_000),  // launching app
            new ProcessSnapshot(200, "discord",   400_000_000),  // protected
            new ProcessSnapshot(300, "chrome",    600_000_000),  // background heavy
            new ProcessSnapshot(400, "smol",        50_000_000), // below 100MB threshold
            new ProcessSnapshot(4,   "System",   1_000_000_000), // system pid
            new ProcessSnapshot(500, "explorer",   300_000_000), // shell — never trim
        });

        var cleaner = new SafeRamCleaner(trimmer.Object);
        await cleaner.RunAsync(launchingPid: 100, protectedPids: new[] { 200 });

        // Only chrome (300) is eligible.
        trimmer.Verify(t => t.TrimWorkingSet(300), Times.Once);
        trimmer.Verify(t => t.TrimWorkingSet(100), Times.Never);
        trimmer.Verify(t => t.TrimWorkingSet(200), Times.Never);
        trimmer.Verify(t => t.TrimWorkingSet(400), Times.Never);
        trimmer.Verify(t => t.TrimWorkingSet(4),   Times.Never);
        trimmer.Verify(t => t.TrimWorkingSet(500), Times.Never);
        trimmer.Verify(t => t.FlushFileCache(), Times.Once);
    }
}
