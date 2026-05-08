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
}
