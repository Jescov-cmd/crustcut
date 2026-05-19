using FluentAssertions;
using PrimeOSTuner.Core.Education;
using Xunit;

namespace PrimeOSTuner.Tests.Education;

public class GuideParserTests
{
    private const string SampleGuide =
        """
        ---
        id: enable-resizable-bar
        title: Enable Resizable BAR
        category: BIOS / UEFI
        difficulty: Intermediate
        risk: Medium
        time: 10-15 minutes, requires restart
        ---
        ## What this does
        Lets your CPU access all of your GPU's VRAM at once.
        """;

    [Fact]
    public void Parse_reads_all_frontmatter_fields()
    {
        var guide = GuideParser.Parse(SampleGuide);

        guide.Id.Should().Be("enable-resizable-bar");
        guide.Title.Should().Be("Enable Resizable BAR");
        guide.Category.Should().Be("BIOS / UEFI");
        guide.Difficulty.Should().Be(GuideDifficulty.Intermediate);
        guide.Risk.Should().Be(GuideRisk.Medium);
        guide.EstimatedTime.Should().Be("10-15 minutes, requires restart");
    }

    [Fact]
    public void Parse_keeps_the_body_and_drops_the_frontmatter()
    {
        var guide = GuideParser.Parse(SampleGuide);

        guide.MarkdownBody.Should().Contain("## What this does");
        guide.MarkdownBody.Should().Contain("Lets your CPU access");
        guide.MarkdownBody.Should().NotContain("difficulty:");
        guide.MarkdownBody.Should().NotStartWith("---");
    }

    [Fact]
    public void Parse_throws_when_the_frontmatter_header_is_missing()
    {
        var act = () => GuideParser.Parse("## Just a body, no header");
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Parse_throws_when_a_required_field_is_missing()
    {
        var noTitle =
            """
            ---
            id: x
            category: Windows
            difficulty: Beginner
            risk: Low
            time: 5 minutes
            ---
            body
            """;
        var act = () => GuideParser.Parse(noTitle);
        act.Should().Throw<FormatException>().WithMessage("*title*");
    }

    [Fact]
    public void Parse_accepts_difficulty_and_risk_case_insensitively()
    {
        var lower =
            """
            ---
            id: x
            title: X
            category: Windows
            difficulty: beginner
            risk: low
            time: 5 minutes
            ---
            body
            """;
        var guide = GuideParser.Parse(lower);

        guide.Difficulty.Should().Be(GuideDifficulty.Beginner);
        guide.Risk.Should().Be(GuideRisk.Low);
    }

    [Fact]
    public void Parse_throws_when_difficulty_value_is_not_recognized()
    {
        var bad =
            """
            ---
            id: x
            title: X
            category: Windows
            difficulty: Wizard
            risk: Low
            time: 5 minutes
            ---
            body
            """;
        var act = () => GuideParser.Parse(bad);
        act.Should().Throw<FormatException>().WithMessage("*difficulty*");
    }
}
