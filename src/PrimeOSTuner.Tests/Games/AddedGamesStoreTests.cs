using FluentAssertions;
using PrimeOSTuner.Core.Games;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class AddedGamesStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"added-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public async Task LoadAsync_returns_empty_when_file_missing()
    {
        var store = new AddedGamesStore(_path);
        (await store.LoadAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task AddAsync_persists_game_with_UserAdded_source()
    {
        var store = new AddedGamesStore(_path);
        await store.AddAsync(new KnownGame(
            "user.test", "Test Game", new[] { "test.exe" }, null, @"C:\Games\Test", KnownGameSource.UserAdded));

        var loaded = await store.LoadAsync();
        loaded.Should().HaveCount(1);
        loaded[0].DisplayName.Should().Be("Test Game");
        loaded[0].Source.Should().Be(KnownGameSource.UserAdded);
    }

    [Fact]
    public async Task RemoveAsync_drops_matching_id()
    {
        var store = new AddedGamesStore(_path);
        await store.AddAsync(new KnownGame("a", "A", new[] { "a.exe" }, null, null, KnownGameSource.UserAdded));
        await store.AddAsync(new KnownGame("b", "B", new[] { "b.exe" }, null, null, KnownGameSource.UserAdded));

        await store.RemoveAsync("a");

        (await store.LoadAsync()).Select(g => g.Id).Should().BeEquivalentTo(new[] { "b" });
    }
}
