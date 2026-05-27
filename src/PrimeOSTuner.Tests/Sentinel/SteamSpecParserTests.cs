using FluentAssertions;
using PrimeOSTuner.Core.Sentinel;
using Xunit;

namespace PrimeOSTuner.Tests.Sentinel;

public class SteamSpecParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "steam-specs", name);

    [Fact]
    public void Valve_style_minimum_extracts_8GB_ram_and_3GB_vram()
    {
        var html = File.ReadAllText(FixturePath("valve-style.html"));

        var spec = SteamSpecParser.ParseMinimum(html);

        spec.MinRamMb.Should().Be(8192);
        spec.MinVramMb.Should().Be(3072);
    }

    [Fact]
    public void Bethesda_style_recommended_extracts_16GB_ram_and_8GB_vram()
    {
        var html = File.ReadAllText(FixturePath("bethesda-style.html"));

        var spec = SteamSpecParser.ParseRecommended(html);

        spec.RecRamMb.Should().Be(16384);
        spec.RecVramMb.Should().Be(8192);
    }

    [Fact]
    public void Indie_minimal_extracts_4GB_ram_and_leaves_vram_null()
    {
        var html = File.ReadAllText(FixturePath("indie-minimal.html"));

        var spec = SteamSpecParser.ParseMinimum(html);

        spec.MinRamMb.Should().Be(4096);
        spec.MinVramMb.Should().BeNull();
    }

    [Fact]
    public void Empty_html_returns_all_nulls()
    {
        var spec = SteamSpecParser.ParseMinimum("");

        spec.MinRamMb.Should().BeNull();
        spec.MinVramMb.Should().BeNull();
    }

    [Fact]
    public void Garbage_html_does_not_throw_and_returns_all_nulls()
    {
        var spec = SteamSpecParser.ParseMinimum("<not><real>html");

        spec.MinRamMb.Should().BeNull();
        spec.MinVramMb.Should().BeNull();
    }

    [Theory]
    [InlineData("8 GB",  8192)]
    [InlineData("8GB",   8192)]
    [InlineData("16 gb", 16384)]
    [InlineData("16 Gigabytes", 16384)]
    public void ParseSizeMb_handles_common_size_spellings(string input, int expectedMb)
    {
        SteamSpecParser.ParseSizeMb(input).Should().Be(expectedMb);
    }
}
