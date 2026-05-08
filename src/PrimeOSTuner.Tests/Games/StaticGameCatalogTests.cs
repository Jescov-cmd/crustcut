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
    public void Every_entry_uses_StaticCatalog_source()
    {
        StaticGameCatalog.All.Should().OnlyContain(g => g.Source == KnownGameSource.StaticCatalog);
    }
}
