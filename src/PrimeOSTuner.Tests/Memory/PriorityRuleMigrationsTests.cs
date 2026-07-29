using FluentAssertions;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Memory;

public class PriorityRuleMigrationsTests
{
    private static PriorityRule Rule(
        string name, PriorityLevel priority,
        bool isGame = false, bool booster = false) =>
        new($@"C:\apps\{name}.exe", name, priority, false, booster, isGame);

    [Fact]
    public void Legacy_belownormal_app_rules_reset_to_normal()
    {
        var rules = new List<PriorityRule>
        {
            Rule("Code", PriorityLevel.BelowNormal),
            Rule("obs", PriorityLevel.BelowNormal),
        };

        var changed = PriorityRuleMigrations.NormalizeLegacyBelowNormal(rules);

        changed.Should().Be(2);
        rules.Should().OnlyContain(r => r.Priority == PriorityLevel.Normal);
    }

    [Fact]
    public void Games_boosters_and_hand_set_priorities_are_untouched()
    {
        var rules = new List<PriorityRule>
        {
            Rule("game", PriorityLevel.BelowNormal, isGame: true),      // game — user's call
            Rule("boosted", PriorityLevel.BelowNormal, booster: true),  // booster — configured
            Rule("editor", PriorityLevel.High),                          // not BelowNormal
            Rule("bg", PriorityLevel.AboveNormal),
        };

        var changed = PriorityRuleMigrations.NormalizeLegacyBelowNormal(rules);

        changed.Should().Be(0);
        rules[0].Priority.Should().Be(PriorityLevel.BelowNormal);
        rules[1].Priority.Should().Be(PriorityLevel.BelowNormal);
        rules[2].Priority.Should().Be(PriorityLevel.High);
        rules[3].Priority.Should().Be(PriorityLevel.AboveNormal);
    }

    [Fact]
    public void Migration_is_idempotent()
    {
        var rules = new List<PriorityRule> { Rule("Code", PriorityLevel.BelowNormal) };

        PriorityRuleMigrations.NormalizeLegacyBelowNormal(rules).Should().Be(1);
        PriorityRuleMigrations.NormalizeLegacyBelowNormal(rules).Should().Be(0);
    }

    [Fact]
    public async Task RunOnce_rewrites_the_store_then_never_again()
    {
        var dir = Path.Combine(Path.GetTempPath(), "crustcut-mig-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var store = new PriorityRuleStore(Path.Combine(dir, "rules.json"));
            var marker = Path.Combine(dir, "migrated");
            await store.SaveAsync(new[] { Rule("Code", PriorityLevel.BelowNormal) });

            await PriorityRuleMigrations.RunOnceAsync(store, marker);
            (await store.LoadAsync())[0].Priority.Should().Be(PriorityLevel.Normal);
            File.Exists(marker).Should().BeTrue();

            // A BelowNormal the user sets AFTER the migration must survive restarts.
            await store.SaveAsync(new[] { Rule("Code", PriorityLevel.BelowNormal) });
            await PriorityRuleMigrations.RunOnceAsync(store, marker);
            (await store.LoadAsync())[0].Priority.Should().Be(PriorityLevel.BelowNormal);
        }
        finally
        {
            try { Directory.Delete(dir, true); } catch { }
        }
    }
}
