using System.IO;
using FluentAssertions;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

public class PriorityRuleStoreTests
{
    [Fact]
    public async Task LoadAsync_returns_empty_when_file_does_not_exist()
    {
        var path = Path.Combine(Path.GetTempPath(), $"primeos-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new PriorityRuleStore(path);
            var rules = await store.LoadAsync();
            rules.Should().BeEmpty();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_salvages_a_torn_file_instead_of_returning_empty()
    {
        // A process killed mid-write leaves the file truncated mid-object. Returning empty
        // here is what destroyed real user data: the caller saw "no rules" and
        // auto-populate overwrote 53 tuned rules with defaults.
        var dir = Path.Combine(Path.GetTempPath(), $"primeos-torn-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "rules.json");
        try
        {
            var store = new PriorityRuleStore(path);
            await store.SaveAsync(new[]
            {
                new PriorityRule(@"C:\a.exe", "A", PriorityLevel.High, true, true, true),
                new PriorityRule(@"C:\b.exe", "B", PriorityLevel.Normal, false, false, false),
                new PriorityRule(@"C:\c.exe", "C", PriorityLevel.AboveNormal, false, false, false),
            });

            // Tear the file the way a hard kill does: truncate mid-way through the last object.
            var raw = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, raw[..(raw.LastIndexOf("\"DisplayName\"") + 8)]);

            var loaded = await store.LoadAsync();

            loaded.Should().HaveCount(2, "the two complete objects are salvageable");
            loaded[0].DisplayName.Should().Be("A");
            loaded[1].DisplayName.Should().Be("B");
            Directory.GetFiles(dir, "*.corrupt-*").Should().NotBeEmpty(
                "the torn original must be backed up, not silently discarded");

            // The salvage must also persist, so the next load doesn't re-salvage.
            (await store.LoadAsync()).Should().HaveCount(2);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task LoadAsync_of_unsalvageable_garbage_backs_up_and_returns_empty()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"primeos-garbage-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "rules.json");
        try
        {
            await File.WriteAllTextAsync(path, "not json at all");
            var store = new PriorityRuleStore(path);

            (await store.LoadAsync()).Should().BeEmpty();
            Directory.GetFiles(dir, "*.corrupt-*").Should().NotBeEmpty();
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SaveAsync_leaves_no_temp_file_behind()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"primeos-tmp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "rules.json");
        try
        {
            var store = new PriorityRuleStore(path);
            await store.SaveAsync(new[]
            {
                new PriorityRule(@"C:\a.exe", "A", PriorityLevel.Normal, false, false, false),
            });

            Directory.GetFiles(dir).Should().ContainSingle()
                .Which.Should().EndWith("rules.json");
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public async Task SaveAsync_then_LoadAsync_round_trips_rules()
    {
        var path = Path.Combine(Path.GetTempPath(), $"primeos-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new PriorityRuleStore(path);
            var rules = new[]
            {
                new PriorityRule(@"C:\Games\cs2.exe", "Counter-Strike 2", PriorityLevel.High, true, true, true),
                new PriorityRule(@"C:\Games\valorant.exe", "VALORANT", PriorityLevel.AboveNormal, false, false, true),
            };
            await store.SaveAsync(rules);

            var loaded = await store.LoadAsync();
            loaded.Should().HaveCount(2);
            loaded[0].ExePath.Should().Be(@"C:\Games\cs2.exe");
            loaded[0].Priority.Should().Be(PriorityLevel.High);
            loaded[0].ProtectFromRamCleanup.Should().BeTrue();
            loaded[1].DisplayName.Should().Be("VALORANT");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task LoadAsync_returns_empty_on_malformed_json_without_throwing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"primeos-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not valid json");
            var store = new PriorityRuleStore(path);
            var rules = await store.LoadAsync();
            rules.Should().BeEmpty();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [Fact]
    public async Task Concurrent_saves_and_loads_do_not_throw_file_in_use()
    {
        // Regression: the UI saving while another save/load raced threw
        // "The process cannot access the file ... because it is being used by another
        // process." The store now serializes access, so hammering it concurrently is safe.
        var path = Path.Combine(Path.GetTempPath(), $"primeos-test-{Guid.NewGuid():N}.json");
        try
        {
            var store = new PriorityRuleStore(path);
            var rule = new PriorityRule(@"C:\Games\cs2.exe", "CS2", PriorityLevel.High, true, true, true);

            var tasks = new List<Task>();
            for (int i = 0; i < 40; i++)
            {
                tasks.Add(store.SaveAsync(new[] { rule }));
                tasks.Add(store.LoadAsync());
            }

            // Must complete without an IOException surfacing.
            var act = async () => await Task.WhenAll(tasks);
            await act.Should().NotThrowAsync();
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
