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
}
