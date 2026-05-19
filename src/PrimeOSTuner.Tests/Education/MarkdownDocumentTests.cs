using FluentAssertions;
using PrimeOSTuner.Core.Education;
using Xunit;

namespace PrimeOSTuner.Tests.Education;

public class MarkdownDocumentTests
{
    [Fact]
    public void Parse_reads_a_heading_with_its_level()
    {
        var blocks = MarkdownDocument.Parse("## Steps");

        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(MarkdownBlockKind.Heading);
        blocks[0].HeadingLevel.Should().Be(2);
        blocks[0].Items.Single().Should().Be("Steps");
    }

    [Fact]
    public void Parse_joins_consecutive_lines_into_one_paragraph()
    {
        var blocks = MarkdownDocument.Parse("This is\na paragraph.");

        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(MarkdownBlockKind.Paragraph);
        blocks[0].Items.Single().Should().Be("This is a paragraph.");
    }

    [Fact]
    public void Parse_groups_bullet_lines_into_one_list()
    {
        var blocks = MarkdownDocument.Parse("- first\n- second\n- third");

        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(MarkdownBlockKind.BulletList);
        blocks[0].Items.Should().Equal("first", "second", "third");
    }

    [Fact]
    public void Parse_groups_numbered_lines_into_one_list()
    {
        var blocks = MarkdownDocument.Parse("1. do this\n2. then this");

        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(MarkdownBlockKind.NumberedList);
        blocks[0].Items.Should().Equal("do this", "then this");
    }

    [Fact]
    public void Parse_separates_blocks_on_blank_lines()
    {
        var blocks = MarkdownDocument.Parse("## Title\n\nSome text.\n\n- a\n- b");

        blocks.Select(b => b.Kind).Should().Equal(
            MarkdownBlockKind.Heading,
            MarkdownBlockKind.Paragraph,
            MarkdownBlockKind.BulletList);
    }

    [Fact]
    public void Parse_starts_a_new_block_at_a_heading_with_no_blank_line_before_it()
    {
        var blocks = MarkdownDocument.Parse("Some text.\n## Heading");

        blocks.Should().HaveCount(2);
        blocks[0].Kind.Should().Be(MarkdownBlockKind.Paragraph);
        blocks[1].Kind.Should().Be(MarkdownBlockKind.Heading);
    }

    [Fact]
    public void ParseInline_returns_a_single_plain_span_for_text_without_bold()
    {
        var spans = MarkdownDocument.ParseInline("just plain text");

        spans.Should().ContainSingle();
        spans[0].Should().Be(new MarkdownSpan("just plain text", false));
    }

    [Fact]
    public void ParseInline_splits_bold_runs_marked_with_double_asterisks()
    {
        var spans = MarkdownDocument.ParseInline("a **b** c");

        spans.Should().Equal(
            new MarkdownSpan("a ", false),
            new MarkdownSpan("b", true),
            new MarkdownSpan(" c", false));
    }
}
