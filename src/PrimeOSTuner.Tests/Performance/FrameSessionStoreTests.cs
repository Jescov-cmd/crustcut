using FluentAssertions;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class FrameSessionStoreTests : IDisposable
{
    private readonly string _tempPath;

    public FrameSessionStoreTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"primeos-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
        if (File.Exists(_tempPath + ".tmp")) File.Delete(_tempPath + ".tmp");
    }

    private static FrameSession MakeSession(string gameName, DateTime at) =>
        new(GameId: gameName.ToLowerInvariant(),
            GameName: gameName,
            StartedAt: at,
            Duration: TimeSpan.FromMinutes(10),
            Stats: new FrameSessionStats(60, 45, 30, 16.7, 22.0, 33.0, 100.0, 36000));

    [Fact]
    public void Save_then_Load_round_trips_a_session()
    {
        var store = new FrameSessionStore(_tempPath);
        var session = MakeSession("Cyberpunk", DateTime.UtcNow);

        store.Save(session);
        var loaded = new FrameSessionStore(_tempPath).Load();

        loaded.Should().HaveCount(1);
        loaded[0].GameName.Should().Be("Cyberpunk");
        loaded[0].Stats.AvgFps.Should().Be(60);
    }

    [Fact]
    public void Save_orders_newest_first()
    {
        var store = new FrameSessionStore(_tempPath);
        var older = MakeSession("Older", DateTime.UtcNow.AddHours(-2));
        var newer = MakeSession("Newer", DateTime.UtcNow.AddHours(-1));

        store.Save(older);
        store.Save(newer);

        store.Load().Select(s => s.GameName).Should().Equal(new[] { "Newer", "Older" });
    }

    [Fact]
    public void Save_caps_the_list_at_fifty_entries()
    {
        var store = new FrameSessionStore(_tempPath);
        for (int i = 0; i < 60; i++)
            store.Save(MakeSession($"Game{i}", DateTime.UtcNow.AddSeconds(i)));

        store.Load().Should().HaveCount(50);
        store.Load()[0].GameName.Should().Be("Game59");   // newest
    }

    [Fact]
    public void Load_drops_sessions_older_than_24h()
    {
        var store = new FrameSessionStore(_tempPath);
        store.Save(MakeSession("Fresh", DateTime.UtcNow.AddHours(-1)));
        store.Save(MakeSession("Stale", DateTime.UtcNow.AddHours(-30)));   // older than 24h

        store.Load().Select(s => s.GameName).Should().ContainSingle().Which.Should().Be("Fresh");
    }

    [Fact]
    public void Load_returns_empty_when_file_does_not_exist()
    {
        var store = new FrameSessionStore(_tempPath);

        store.Load().Should().BeEmpty();
    }

    [Fact]
    public void Save_raises_Updated_event()
    {
        var store = new FrameSessionStore(_tempPath);
        var fired = 0;
        store.Updated += (_, _) => fired++;

        store.Save(MakeSession("A", DateTime.UtcNow));
        store.Save(MakeSession("B", DateTime.UtcNow));

        fired.Should().Be(2);
    }
}
