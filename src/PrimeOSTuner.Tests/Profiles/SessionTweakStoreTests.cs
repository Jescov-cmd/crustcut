using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class SessionTweakStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"session-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }

    [Fact]
    public async Task Load_returns_empty_when_no_file()
    {
        var store = new SessionTweakStore(_path);
        (await store.LoadAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Add_then_Load_round_trips()
    {
        var store = new SessionTweakStore(_path);
        await store.AddAsync("game.timer-resolution");

        (await store.LoadAsync()).Should().ContainSingle().Which.Should().Be("game.timer-resolution");
    }

    [Fact]
    public async Task Add_is_idempotent()
    {
        var store = new SessionTweakStore(_path);
        await store.AddAsync("game.timer-resolution");
        await store.AddAsync("game.timer-resolution");

        (await store.LoadAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Remove_drops_the_id()
    {
        var store = new SessionTweakStore(_path);
        await store.AddAsync("game.timer-resolution");
        await store.RemoveAsync("game.timer-resolution");

        (await store.LoadAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AddMany_backfills_and_merges_without_clobbering()
    {
        var store = new SessionTweakStore(_path);
        await store.AddAsync("core.hibernation-disable");
        await store.AddManyAsync(new[] { "game.game-mode", "game.hw-gpu-scheduling", "core.hibernation-disable" });

        (await store.LoadAsync()).Should()
            .BeEquivalentTo("core.hibernation-disable", "game.game-mode", "game.hw-gpu-scheduling");
    }

    [Fact]
    public void Timer_resolution_is_session_scoped_and_registry_tweaks_are_not()
    {
        SessionScopedTweaks.IsSessionScoped("game.timer-resolution").Should().BeTrue();
        SessionScopedTweaks.IsSessionScoped("game.game-mode").Should().BeFalse();
        SessionScopedTweaks.IsSessionScoped("core.power-plan").Should().BeFalse();
    }
}
