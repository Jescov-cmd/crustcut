using FluentAssertions;
using PrimeOSTuner.Core.Games;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class StaticGameCatalogTests
{
    [Fact]
    public void Catalog_includes_valorant_with_correct_executable()
    {
        var match = StaticGameCatalog.All.FirstOrDefault(g => g.Id == "static.valorant");
        match.Should().NotBeNull();
        match!.ExecutableNames.Should().Contain("VALORANT-Win64-Shipping.exe");
    }

    [Fact]
    public void Catalog_includes_league_of_legends()
    {
        var match = StaticGameCatalog.All.FirstOrDefault(g => g.Id == "static.league-of-legends");
        match.Should().NotBeNull();
        match!.ExecutableNames.Should().Contain("League of Legends.exe");
    }

    [Fact]
    public void Catalog_includes_fortnite_epic()
    {
        var match = StaticGameCatalog.All.FirstOrDefault(g => g.Id == "static.fortnite");
        match.Should().NotBeNull();
        match!.ExecutableNames.Should().Contain("FortniteClient-Win64-Shipping.exe");
    }

    [Fact]
    public void Roblox_is_recognised_however_it_was_launched()
    {
        var match = StaticGameCatalog.All.FirstOrDefault(g => g.Id == "static.roblox");
        match.Should().NotBeNull();
        match!.ExecutableNames.Should().Contain("RobloxPlayerBeta.exe");   // Roblox / Bloxstrap / Fishstrap
        match.ExecutableNames.Should().NotContain("RobloxStudioBeta.exe"); // Studio is a dev tool
    }

    /// <summary>javaw.exe is every Java program. Matching it by name turned each Gradle
    /// build into a "Minecraft session"; JavaGameDetector handles Java Minecraft instead.</summary>
    [Fact]
    public void No_entry_matches_a_generic_java_runtime()
    {
        StaticGameCatalog.All.SelectMany(g => g.ExecutableNames)
            .Should().NotContain(n => n.Equals("javaw.exe", StringComparison.OrdinalIgnoreCase)
                                   || n.Equals("java.exe", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_entry_uses_StaticCatalog_source()
    {
        StaticGameCatalog.All.Should().OnlyContain(g => g.Source == KnownGameSource.StaticCatalog);
    }
}
