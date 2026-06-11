using System.Collections.Generic;
using FluentAssertions;
using PrimeOSTuner.Core.Bloatware;
using Xunit;

namespace PrimeOSTuner.Tests.Bloatware;

public class DesktopBloatwareDetectorTests
{
    private static DesktopBloatwareCatalogEntry Entry(
        string id, string nameContains, string? publisherContains = null) =>
        new(id, id, nameContains, publisherContains, "desktop", SafetyTier.Safe, null);

    private static DesktopProgram Prog(string name, string? publisher = null) =>
        new(name, publisher, "C:\\uninst.exe", null, null);

    [Fact]
    public void Matches_on_name_substring_case_insensitive()
    {
        var hits = DesktopBloatwareDetector.Match(
            new[] { Prog("McAfee LiveSafe") },
            new[] { Entry("mcafee", "mcafee") });
        hits.Should().ContainSingle().Which.Program.DisplayName.Should().Be("McAfee LiveSafe");
    }

    [Fact]
    public void Publisher_filter_blocks_name_only_match()
    {
        var hits = DesktopBloatwareDetector.Match(
            new[] { Prog("HP Support Assistant", "SomeoneElse Inc") },
            new[] { Entry("hp-sa", "HP Support Assistant", "HP") });
        hits.Should().BeEmpty();
    }

    [Fact]
    public void Publisher_filter_passes_when_publisher_matches()
    {
        var hits = DesktopBloatwareDetector.Match(
            new[] { Prog("HP Support Assistant", "HP Inc.") },
            new[] { Entry("hp-sa", "HP Support Assistant", "HP") });
        hits.Should().ContainSingle();
    }

    [Fact]
    public void Same_program_not_reported_twice_for_overlapping_entries()
    {
        var hits = DesktopBloatwareDetector.Match(
            new[] { Prog("Norton 360") },
            new[] { Entry("norton-360", "Norton 360"), Entry("norton", "Norton") });
        hits.Should().ContainSingle("first catalog match wins per program");
    }
}
