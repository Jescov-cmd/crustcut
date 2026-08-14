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
    public async Task Applies_memory_limit_when_rule_has_one()
    {
        var watcher = new TestWatcher();
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExe(@"C:\Apps\edge.exe")).Returns(new[] { 4321 });
        var engine = new PriorityRuleEngine(watcher, priority.Object, new Mock<IGameBooster>().Object);
        await engine.ReloadAsync(new[]
        {
            Rule(@"C:\Apps\edge.exe", PriorityLevel.Normal) with { MemoryLimitMb = 1024 }
        });
        engine.Start();

        watcher.RaiseStarted(4321, "edge.exe");

        // hard: true — the user chose this cap by hand, so it gets a real ceiling.
        priority.Verify(p => p.TrySetMemoryLimit(4321, 1024, true), Times.Once);
    }

    /// <summary>
    /// A cap Crustcut assigned by itself must be SOFT. A hard ceiling forces Windows to
    /// evict the instant the process reaches it, and on anything that draws, the evicted
    /// pages are its render surfaces — black rectangles and blurry text. The app is only
    /// allowed to make that trade when a human asked for it.
    /// </summary>
    [Fact]
    public async Task Auto_assigned_limits_are_soft_not_hard()
    {
        var watcher = new TestWatcher();
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExe(@"C:\Apps\spotify.exe")).Returns(new[] { 777 });
        var engine = new PriorityRuleEngine(watcher, priority.Object, new Mock<IGameBooster>().Object);
        await engine.ReloadAsync(new[]
        {
            Rule(@"C:\Apps\spotify.exe", PriorityLevel.Normal)
                with { MemoryLimitMb = 512, LimitAutoAssigned = true }
        });
        engine.Start();

        watcher.RaiseStarted(777, "spotify.exe");

        priority.Verify(p => p.TrySetMemoryLimit(777, 512, false), Times.Once);
        priority.Verify(p => p.TrySetMemoryLimit(777, 512, true), Times.Never);
    }

    [Fact]
    public async Task Does_not_touch_memory_limit_when_rule_has_none()
    {
        var watcher = new TestWatcher();
        var priority = new Mock<IPriorityClient>();
        priority.Setup(p => p.FindPidsForExe(@"C:\Games\cs2.exe")).Returns(new[] { 1234 });
        var engine = new PriorityRuleEngine(watcher, priority.Object, new Mock<IGameBooster>().Object);
        await engine.ReloadAsync(new[] { Rule(@"C:\Games\cs2.exe") });
        engine.Start();

        watcher.RaiseStarted(1234, "cs2.exe");

        priority.Verify(p => p.TrySetMemoryLimit(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
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
