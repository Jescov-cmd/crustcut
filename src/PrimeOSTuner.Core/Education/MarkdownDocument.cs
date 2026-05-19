using System.Text;

namespace PrimeOSTuner.Core.Education;

public enum MarkdownBlockKind { Heading, Paragraph, BulletList, NumberedList }

/// <summary>A run of inline text, optionally bold (<c>**like this**</c>).</summary>
public sealed record MarkdownSpan(string Text, bool Bold);

/// <summary>
/// One parsed block of guide markdown. <see cref="Items"/> holds one entry for a
/// heading or paragraph, and one entry per item for a list.
/// </summary>
public sealed record MarkdownBlock(
    MarkdownBlockKind Kind,
    int HeadingLevel,
    IReadOnlyList<string> Items);

/// <summary>
/// A deliberately small markdown parser for "Optimization 101" guide bodies.
/// Supports headings (#/##/###), paragraphs, bullet lists (-/*), numbered lists,
/// and inline bold (**). Anything fancier is out of scope by design.
/// </summary>
public static class MarkdownDocument
{
    public static IReadOnlyList<MarkdownBlock> Parse(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

        int i = 0;
        while (i < lines.Length)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) { i++; continue; }

            var trimmed = lines[i].TrimStart();

            if (TryHeadingLevel(trimmed, out var level))
            {
                blocks.Add(new MarkdownBlock(
                    MarkdownBlockKind.Heading, level, new[] { trimmed[(level + 1)..].Trim() }));
                i++;
                continue;
            }

            if (IsBullet(trimmed))
            {
                var items = new List<string>();
                while (i < lines.Length && IsBullet(lines[i].TrimStart()))
                {
                    items.Add(lines[i].TrimStart()[2..].Trim());
                    i++;
                }
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.BulletList, 0, items));
                continue;
            }

            if (TryNumbered(trimmed, out _))
            {
                var items = new List<string>();
                while (i < lines.Length && TryNumbered(lines[i].TrimStart(), out var content))
                {
                    items.Add(content);
                    i++;
                }
                blocks.Add(new MarkdownBlock(MarkdownBlockKind.NumberedList, 0, items));
                continue;
            }

            // Paragraph: consecutive non-blank lines that aren't a heading or list.
            var paragraph = new List<string>();
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                var t = lines[i].TrimStart();
                if (TryHeadingLevel(t, out _) || IsBullet(t) || TryNumbered(t, out _)) break;
                paragraph.Add(lines[i].Trim());
                i++;
            }
            blocks.Add(new MarkdownBlock(
                MarkdownBlockKind.Paragraph, 0, new[] { string.Join(" ", paragraph) }));
        }

        return blocks;
    }

    public static IReadOnlyList<MarkdownSpan> ParseInline(string text)
    {
        var spans = new List<MarkdownSpan>();
        if (string.IsNullOrEmpty(text)) return spans;

        var buffer = new StringBuilder();
        var bold = false;
        var i = 0;
        while (i < text.Length)
        {
            if (i + 1 < text.Length && text[i] == '*' && text[i + 1] == '*')
            {
                if (buffer.Length > 0)
                {
                    spans.Add(new MarkdownSpan(buffer.ToString(), bold));
                    buffer.Clear();
                }
                bold = !bold;
                i += 2;
            }
            else
            {
                buffer.Append(text[i]);
                i++;
            }
        }
        if (buffer.Length > 0)
            spans.Add(new MarkdownSpan(buffer.ToString(), bold));

        return spans;
    }

    private static bool TryHeadingLevel(string trimmed, out int level)
    {
        level = 0;
        while (level < trimmed.Length && trimmed[level] == '#') level++;
        if (level is >= 1 and <= 6 && level < trimmed.Length && trimmed[level] == ' ')
            return true;
        level = 0;
        return false;
    }

    private static bool IsBullet(string trimmed)
        => trimmed.StartsWith("- ", StringComparison.Ordinal)
        || trimmed.StartsWith("* ", StringComparison.Ordinal);

    private static bool TryNumbered(string trimmed, out string content)
    {
        content = string.Empty;
        var d = 0;
        while (d < trimmed.Length && char.IsDigit(trimmed[d])) d++;
        if (d == 0 || d + 1 >= trimmed.Length || trimmed[d] != '.' || trimmed[d + 1] != ' ')
            return false;
        content = trimmed[(d + 2)..].Trim();
        return true;
    }
}
