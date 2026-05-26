using FluentAssertions;
using Moq;
using PrimeOSTuner.Win.Suspension;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

public class BackgroundSuspenderServiceTests
{
    private static BackgroundSuspenderService Build(
        Mock<IProcessSuspender> suspender,
        Dictionary<string, int[]> processIdsByName,
        params string[] targetNames)
    {
        return new BackgroundSuspenderService(
            suspender.Object,
            targetNames.Length > 0 ? targetNames : new[] { "Spotify", "OneDrive" },
            name => processIdsByName.TryGetValue(name, out var pids) ? pids : Array.Empty<int>());
    }

    [Fact]
    public void Suspend_calls_IProcessSuspender_for_every_matched_pid_and_records_it()
    {
        var suspender = new Mock<IProcessSuspender>();
        var service = Build(suspender, new()
        {
            ["Spotify"] = new[] { 1001 },
            ["OneDrive"] = new[] { 2001, 2002 },
        });

        var newly = service.SuspendBackgroundApps();

        suspender.Verify(s => s.Suspend(1001), Times.Once);
        suspender.Verify(s => s.Suspend(2001), Times.Once);
        suspender.Verify(s => s.Suspend(2002), Times.Once);
        newly.Should().HaveCount(3);
        service.Currently.Should().HaveCount(3);
    }

    [Fact]
    public void Suspend_does_not_double_suspend_a_pid_that_is_already_recorded()
    {
        var suspender = new Mock<IProcessSuspender>();
        var service = Build(suspender, new() { ["Spotify"] = new[] { 1001 } });

        service.SuspendBackgroundApps();
        suspender.Verify(s => s.Suspend(1001), Times.Once);

        var newly = service.SuspendBackgroundApps();
        suspender.Verify(s => s.Suspend(1001), Times.Once);
        newly.Should().BeEmpty();
    }

    [Fact]
    public void Resume_calls_IProcessSuspender_for_every_recorded_pid_and_clears_state()
    {
        var suspender = new Mock<IProcessSuspender>();
        var service = Build(suspender, new()
        {
            ["Spotify"] = new[] { 1001 },
            ["OneDrive"] = new[] { 2001 },
        });
        service.SuspendBackgroundApps();

        service.ResumeAll();

        suspender.Verify(s => s.Resume(1001), Times.Once);
        suspender.Verify(s => s.Resume(2001), Times.Once);
        service.Currently.Should().BeEmpty();
    }

    [Fact]
    public void Changed_is_raised_when_pids_actually_change()
    {
        var suspender = new Mock<IProcessSuspender>();
        var service = Build(suspender, new() { ["Spotify"] = new[] { 1001 } });

        var changedCount = 0;
        service.Changed += (_, _) => changedCount++;

        service.SuspendBackgroundApps();
        service.SuspendBackgroundApps(); // no-op, already suspended
        service.ResumeAll();
        service.ResumeAll();             // no-op, nothing to resume

        changedCount.Should().Be(2);     // one Suspend with new pids + one Resume with state
    }
}
