using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Lifecycle;
using Xunit;

namespace PrimeOSTuner.Tests.Lifecycle;

public class GameProcessWatcherTests
{
    private static KnownGame Tf2 = new(
        "steam.440", "Team Fortress 2",
        new[] { "hl2.exe" }, "440", null, KnownGameSource.Steam);

    [Fact]
    public async Task Tick_raises_GameStarted_when_known_executable_appears()
    {
        var snapshot = new List<string> { "explorer.exe", "chrome.exe" };
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 50);

        KnownGame? started = null;
        watcher.GameStarted += (_, g) => started = g;

        await watcher.TickAsync();
        snapshot.Add("hl2.exe");
        await watcher.TickAsync();

        started.Should().NotBeNull();
        started!.Id.Should().Be("steam.440");
    }

    [Fact]
    public async Task Tick_raises_GameStopped_when_executable_disappears()
    {
        var snapshot = new List<string> { "hl2.exe" };
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 50);

        KnownGame? stopped = null;
        watcher.GameStopped += (_, e) => stopped = e.Game;

        await watcher.TickAsync();
        snapshot.Clear();
        await watcher.TickAsync();

        stopped.Should().NotBeNull();
        stopped!.Id.Should().Be("steam.440");
    }

    /// <summary>2026-09-30: one Minecraft session became nine — the game was declared
    /// closed on a single bad look at the process list and "started" again two seconds
    /// later. A game is over only when consecutive checks agree.</summary>
    [Fact]
    public async Task One_missed_check_does_not_end_a_game()
    {
        var snapshot = new List<string> { "hl2.exe" };
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 60_000, exitConfirmTicks: 2);
        var started = 0; var stopped = 0;
        watcher.GameStarted += (_, _) => started++;
        watcher.GameStopped += (_, _) => stopped++;

        await watcher.TickAsync();
        snapshot.Clear();
        await watcher.TickAsync();          // one bad look
        snapshot.Add("hl2.exe");
        await watcher.TickAsync();          // still there

        started.Should().Be(1);
        stopped.Should().Be(0);

        snapshot.Clear();
        await watcher.TickAsync();
        await watcher.TickAsync();          // gone twice in a row: really closed
        stopped.Should().Be(1);
    }

    [Fact]
    public async Task An_unreadable_process_list_ends_nothing()
    {
        var fail = false;
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => fail ? throw new InvalidOperationException() : new[] { "hl2.exe" },
            pollIntervalMs: 60_000);
        var stopped = 0;
        watcher.GameStopped += (_, _) => stopped++;

        await watcher.TickAsync();
        fail = true;
        var act = () => watcher.TickAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();   // the guarded tick swallows it
        stopped.Should().Be(0);
    }

    [Fact]
    public async Task Tick_does_not_raise_GameStarted_twice_for_same_game()
    {
        var snapshot = new List<string> { "hl2.exe" };
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 50);

        var startCount = 0;
        watcher.GameStarted += (_, _) => startCount++;

        await watcher.TickAsync();
        await watcher.TickAsync();
        await watcher.TickAsync();

        startCount.Should().Be(1);
    }

    /// <summary>"Did it even notice my game?" has to be answerable from the History tab.</summary>
    [Fact]
    public async Task Starts_and_stops_are_logged()
    {
        var snapshot = new List<string>();
        var log = new List<string>();
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            log: log.Add);

        snapshot.Add("hl2.exe");
        await watcher.TickAsync();
        snapshot.Clear();
        await watcher.TickAsync();

        log.Should().HaveCount(2);
        log[0].Should().Contain("Team Fortress 2 started");
        log[1].Should().Contain("Team Fortress 2 closed after");
    }

    /// <summary>A launch event for a game's exe must trigger a check straight away, not
    /// whenever the next poll comes round.</summary>
    [Fact]
    public async Task A_game_launch_event_triggers_an_immediate_check()
    {
        var snapshot = new List<string>();
        var launches = new FakeProcessWatcher();
        var started = new TaskCompletionSource<KnownGame>();
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(new[] { Tf2 }),
            processSnapshotProvider: () => snapshot.ToArray(),
            pollIntervalMs: 600_000,          // effectively no polling: only the event can find it
            processStarts: launches);
        watcher.GameStarted += (_, g) => started.TrySetResult(g);
        watcher.Start();
        await Task.Delay(200);                // let the start-up check finish (nothing running yet)

        snapshot.Add("hl2.exe");
        launches.Raise(4242, "hl2.exe");

        var winner = await Task.WhenAny(started.Task, Task.Delay(3000));
        winner.Should().BeSameAs(started.Task, "the launch event should have been enough on its own");
        watcher.Dispose();
    }

    private sealed class FakeProcessWatcher : PrimeOSTuner.Core.Memory.IProcessWatcher
    {
        public event EventHandler<PrimeOSTuner.Core.Memory.ProcessStartedEvent>? ProcessStarted;
        public event EventHandler<PrimeOSTuner.Core.Memory.ProcessStoppedEvent>? ProcessStopped { add { } remove { } }
        public void Raise(int pid, string name) =>
            ProcessStarted?.Invoke(this, new PrimeOSTuner.Core.Memory.ProcessStartedEvent(pid, name));
        public void Start() { }
        public void Stop() { }
        public void Dispose() { }
    }
}
