using System.IO;
using FluentAssertions;
using PrimeOSTuner.Core.Bloatware;
using Xunit;

namespace PrimeOSTuner.Tests.Bloatware;

public class BloatwareCatalogTests
{
    [Fact]
    public void Load_returns_empty_when_json_has_no_items()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{ \"items\": [] }");
        try
        {
            var entries = BloatwareCatalog.LoadFromFile(path);
            entries.Should().BeEmpty();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_parses_one_entry_with_all_fields()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            {
              "items": [
                {
                  "appxName": "Microsoft.XboxGamingOverlay",
                  "displayName": "Xbox Game Bar",
                  "category": "gaming",
                  "tier": "Risky",
                  "riskNote": "Xbox Game Bar provides the in-game overlay for some games."
                }
              ]
            }
            """);
        try
        {
            var entries = BloatwareCatalog.LoadFromFile(path);
            entries.Should().HaveCount(1);
            entries[0].AppxName.Should().Be("Microsoft.XboxGamingOverlay");
            entries[0].Tier.Should().Be(SafetyTier.Risky);
            entries[0].RiskNote.Should().StartWith("Xbox Game Bar");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Load_throws_on_duplicate_appxName()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, """
            {
              "items": [
                { "appxName": "x", "displayName": "A", "category": "preinstalled", "tier": "Safe", "riskNote": null },
                { "appxName": "x", "displayName": "B", "category": "preinstalled", "tier": "Safe", "riskNote": null }
              ]
            }
            """);
        try
        {
            var act = () => BloatwareCatalog.LoadFromFile(path);
            act.Should().Throw<InvalidOperationException>().WithMessage("*duplicate*x*");
        }
        finally { File.Delete(path); }
    }
}
