using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

public class PriorityRuleEngineTests
{
    private static PriorityRule Rule(string path, PriorityLevel lvl = PriorityLevel.High,
                                     bool protect = false, bool booster = false)
        => new(path, Path.GetFileName(path), lvl, protect, booster, false);

    [Fact]
    public async Task Applies_priority_when_matching_process_starts()
    {
        var watcher = new TestWatcher();
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExe(@"C:\Games\cs2.exe"))
                .Returns(new[] { 1234 });
        var booster = new Mock<IGameBooster>();
        var engine = new PriorityRuleEngine(watcher, priority.Object, booster.Object);
        await engine.ReloadAsync(new[] { Rule(@"C:\Games\cs2.exe") });
        engine.Start();

        watcher.RaiseStarted(1234, "cs2.exe");

        priority.Verify(p => p.TrySetPriority(1234, PriorityLevel.High), Times.Once);
        booster.Verify(b => b.QueueAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Fires_GameBooster_when_rule_has_booster_enabled()
    {
        var watcher = new TestWatcher();
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExe(@"C:\Games\cs2.exe")).Returns(new[] { 1234 });
        priority.Setup(p => p.FindPidsForExes(It.IsAny<IEnumerable<string>>())).Returns(Array.Empty<int>());
        var booster = new Mock<IGameBooster>();
        var engine = new PriorityRuleEngine(watcher, priority.Object, booster.Object);
        await engine.ReloadAsync(new[] { Rule(@"C:\Games\cs2.exe", booster: true) });
        engine.Start();

        watcher.RaiseStarted(1234, "cs2.exe");

        booster.Verify(b => b.QueueAsync(1234, It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ignores_unmatched_process_starts()
    {
        var watcher = new TestWatcher();
        var priority = new Mock<IPriorityClient>();
        var booster = new Mock<IGameBooster>();
        var engine = new PriorityRuleEngine(watcher, priority.Object, booster.Object);
        await engine.ReloadAsync(new[] { Rule(@"C:\Games\cs2.exe") });
        engine.Start();

        watcher.RaiseStarted(9999, "notepad.exe");

        priority.Verify(p => p.TrySetPriority(It.IsAny<int>(), It.IsAny<PriorityLevel>()), Times.Never);
        booster.Verify(b => b.QueueAsync(It.IsAny<int>(), It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class TestWatcher : IProcessWatcher
    {
        public event EventHandler<ProcessStartedEvent>? ProcessStarted;
        public event EventHandler<ProcessStoppedEvent>? ProcessStopped;
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
        public void RaiseStarted(int pid, string name) => ProcessStarted?.Invoke(this, new ProcessStartedEvent(pid, name));
        public void RaiseStopped(int pid, string name) => ProcessStopped?.Invoke(this, new ProcessStoppedEvent(pid, name));
    }
}
