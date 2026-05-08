using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class ActiveTweaksStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"active-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task LoadAsync_returns_null_when_file_missing()
    {
        var store = new ActiveTweaksStore(_path);
        (await store.LoadAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Save_then_Load_round_trips()
    {
        var store = new ActiveTweaksStore(_path);
        var record = new ActiveTweaksRecord(
            "valorant",
            "performance",
            DateTime.UtcNow,
            new[]
            {
                new ProfileTweakOutcome("game.game-mode", true, "undo-1", null),
                new ProfileTweakOutcome("game.mouse-accel", true, "undo-2", null),
            });

        await store.SaveAsync(record);
        var loaded = await store.LoadAsync();

        loaded.Should().NotBeNull();
        loaded!.GameId.Should().Be("valorant");
        loaded.Outcomes.Should().HaveCount(2);
    }

    [Fact]
    public async Task Clear_removes_the_file()
    {
        var store = new ActiveTweaksStore(_path);
        await store.SaveAsync(new ActiveTweaksRecord("g", "p", DateTime.UtcNow, Array.Empty<ProfileTweakOutcome>()));
        File.Exists(_path).Should().BeTrue();

        await store.ClearAsync();

        File.Exists(_path).Should().BeFalse();
    }
}
