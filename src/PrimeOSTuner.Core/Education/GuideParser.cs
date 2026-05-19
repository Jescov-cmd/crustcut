namespace PrimeOSTuner.Core.Education;

/// <summary>
/// Parses an "Optimization 101" guide from markdown text with a frontmatter header:
///
///   ---
///   id: enable-resizable-bar
///   title: Enable Resizable BAR
///   category: BIOS / UEFI
///   difficulty: Intermediate
///   risk: Medium
///   time: 10-15 minutes, requires restart
///   ---
///   (markdown body...)
/// </summary>
public static class GuideParser
{
    public static Guide Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var text = markdown.Replace("\r\n", "\n").TrimStart('\n', ' ');
        if (!text.StartsWith("---\n", StringComparison.Ordinal))
            throw new FormatException("Guide is missing its '---' frontmatter header.");

        // The closing delimiter is the first "\n---" after the opening "---\n".
        var close = text.IndexOf("\n---", 4, StringComparison.Ordinal);
        if (close < 0)
            throw new FormatException("Guide frontmatter header is not closed with '---'.");

        var frontmatter = text.Substring(4, close - 4);
        var body = text[(close + 4)..].TrimStart('\n');
        var fields = ParseFields(frontmatter);

        return new Guide(
            Id: Required(fields, "id"),
            Title: Required(fields, "title"),
            Category: Required(fields, "category"),
            Difficulty: ParseEnum<GuideDifficulty>(Required(fields, "difficulty"), "difficulty"),
            Risk: ParseEnum<GuideRisk>(Required(fields, "risk"), "risk"),
            EstimatedTime: Required(fields, "time"),
            MarkdownBody: body);
    }

    private static Dictionary<string, string> ParseFields(string frontmatter)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in frontmatter.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            var colon = line.IndexOf(':');
            if (colon <= 0)
                throw new FormatException($"Malformed guide frontmatter line: '{raw}'.");

            fields[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }
        return fields;
    }

    private static string Required(IReadOnlyDictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var value) && value.Length > 0
            ? value
            : throw new FormatException($"Guide frontmatter is missing required field '{key}'.");

    private static T ParseEnum<T>(string value, string field) where T : struct, Enum
        => Enum.TryParse<T>(value, ignoreCase: true, out var result)
            ? result
            : throw new FormatException($"Guide frontmatter field '{field}' has invalid value '{value}'.");
}
