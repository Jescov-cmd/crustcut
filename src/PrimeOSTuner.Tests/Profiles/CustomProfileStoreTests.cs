using FluentAssertions;
using PrimeOSTuner.Core.Profiles;
using Xunit;

namespace PrimeOSTuner.Tests.Profiles;

public class CustomProfileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"custom-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task LoadAsync_returns_default_empty_profile_when_file_missing()
    {
        var store = new CustomProfileStore(_path);
        var profile = await store.LoadAsync();
        profile.Id.Should().Be("custom");
        profile.TweakIds.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_tweak_ids()
    {
        var store = new CustomProfileStore(_path);
        await store.SaveAsync(new[] { "game.game-mode", "game.timer-resolution" });

        var loaded = await store.LoadAsync();

        loaded.TweakIds.Should().BeEquivalentTo(new[] { "game.game-mode", "game.timer-resolution" });
    }

    [Fact]
    public async Task SaveAsync_creates_parent_directory()
    {
        var nested = Path.Combine(Path.GetTempPath(), $"primeos-{Guid.NewGuid()}", "sub", "custom.json");
        var store = new CustomProfileStore(nested);
        try
        {
            await store.SaveAsync(new[] { "x" });
            File.Exists(nested).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(Path.GetDirectoryName(nested)!))
                Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(nested)!)!, true);
        }
    }
}
