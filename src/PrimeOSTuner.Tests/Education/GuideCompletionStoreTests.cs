using FluentAssertions;
using PrimeOSTuner.Core.Education;
using Xunit;

namespace PrimeOSTuner.Tests.Education;

public class GuideCompletionStoreTests
{
    private static string TempPath()
        => Path.Combine(Path.GetTempPath(), "guide-completion-" + Guid.NewGuid().ToString("N") + ".json");

    [Fact]
    public async Task LoadAsync_returns_empty_when_no_file_exists()
    {
        var store = new GuideCompletionStore(TempPath());

        (await store.LoadAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_the_completed_ids()
    {
        var path = TempPath();
        try
        {
            var store = new GuideCompletionStore(path);
            await store.SaveAsync(new[] { "enable-rebar", "enable-xmp" });

            var loaded = await store.LoadAsync();

            loaded.Should().BeEquivalentTo(new[] { "enable-rebar", "enable-xmp" });
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task SaveAsync_replaces_the_previously_saved_set()
    {
        var path = TempPath();
        try
        {
            var store = new GuideCompletionStore(path);
            await store.SaveAsync(new[] { "a", "b" });
            await store.SaveAsync(new[] { "a" });

            (await store.LoadAsync()).Should().BeEquivalentTo(new[] { "a" });
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
