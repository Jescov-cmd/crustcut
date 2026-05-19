namespace PrimeOSTuner.Core.Education;

/// <summary>How hard a guide is to follow.</summary>
public enum GuideDifficulty { Beginner, Intermediate, Advanced }

/// <summary>How much damage a mistake while following the guide could do.</summary>
public enum GuideRisk { Low, Medium, High }

/// <summary>
/// One "Optimization 101" guide — a manual tweak the app teaches the user to do
/// themselves but deliberately does NOT automate (BIOS settings, driver panels, etc.).
///
/// Guides are authored as markdown files with a '---' frontmatter metadata header;
/// see <see cref="GuideParser"/>.
/// </summary>
public sealed record Guide(
    string Id,
    string Title,
    string Category,
    GuideDifficulty Difficulty,
    GuideRisk Risk,
    string EstimatedTime,
    string MarkdownBody);
