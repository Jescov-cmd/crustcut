using FluentAssertions;
using PrimeOSTuner.Core.Games;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class GameProfileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gp-{Guid.NewGuid()}.json");
    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task GetProfileFor_returns_null_when_not_assigned()
    {
        var store = new GameProfileStore(_path);
        (await store.GetProfileForAsync("steam.440")).Should().BeNull();
    }

    [Fact]
    public async Task SetProfileFor_then_GetProfileFor_round_trips()
    {
        var store = new GameProfileStore(_path);
        await store.SetProfileForAsync("steam.440", "performance");

        var p = await store.GetProfileForAsync("steam.440");
        p.Should().Be("performance");
    }

    [Fact]
    public async Task SetProfileFor_overwrites_existing_assignment()
    {
        var store = new GameProfileStore(_path);
        await store.SetProfileForAsync("steam.440", "basic");
        await store.SetProfileForAsync("steam.440", "performance");

        (await store.GetProfileForAsync("steam.440")).Should().Be("performance");
    }

    [Fact]
    public async Task ClearProfileFor_removes_assignment()
    {
        var store = new GameProfileStore(_path);
        await store.SetProfileForAsync("steam.440", "basic");
        await store.ClearProfileForAsync("steam.440");

        (await store.GetProfileForAsync("steam.440")).Should().BeNull();
    }
}
