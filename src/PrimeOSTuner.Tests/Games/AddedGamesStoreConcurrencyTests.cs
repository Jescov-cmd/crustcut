using System.IO;
using FluentAssertions;
using PrimeOSTuner.Core.Games;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class AddedGamesStoreConcurrencyTests
{
    private static KnownGame Game(int n) =>
        new($"user.{n}", $"Game {n}", new[] { $"game{n}.exe" }, null, $@"C:\Games\game{n}.exe", KnownGameSource.UserAdded);

    [Fact]
    public async Task Concurrent_adds_do_not_throw_and_do_not_lose_entries()
    {
        // Regression for the file-lock crash class + the read-modify-write race: adding many
        // games in parallel must neither throw "file in use" nor silently drop entries.
        var path = Path.Combine(Path.GetTempPath(), $"added-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AddedGamesStore(path);
            var tasks = Enumerable.Range(0, 30).Select(i => store.AddAsync(Game(i)));

            var act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync();

            var all = await store.LoadAsync();
            all.Should().HaveCount(30, "the RMW lock must prevent concurrent adds from clobbering each other");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Concurrent_adds_and_loads_are_safe()
    {
        var path = Path.Combine(Path.GetTempPath(), $"added-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AddedGamesStore(path);
            var tasks = new List<Task>();
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(store.AddAsync(Game(i)));
                tasks.Add(store.LoadAsync());
            }

            var act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
