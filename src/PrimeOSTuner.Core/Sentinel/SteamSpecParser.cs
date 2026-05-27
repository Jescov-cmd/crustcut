using System.Text.RegularExpressions;

namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Pure parser for the HTML blobs Steam returns under
/// <c>pc_requirements.minimum</c> / <c>pc_requirements.recommended</c>.
/// Tolerant by design: any field we cannot extract stays null so the
/// detection rules silently skip that axis instead of firing on bad data.
/// </summary>
public static class SteamSpecParser
{
    /// <summary>Parse the "minimum" HTML blob.</summary>
    public static SteamPcRequirements ParseMinimum(string html)
        => new(MinRamMb: ExtractRamMb(html), RecRamMb: null,
               MinVramMb: ExtractVramMb(html), RecVramMb: null);

    /// <summary>Parse the "recommended" HTML blob.</summary>
    public static SteamPcRequirements ParseRecommended(string html)
        => new(MinRamMb: null, RecRamMb: ExtractRamMb(html),
               MinVramMb: null, RecVramMb: ExtractVramMb(html));

    /// <summary>Combine a min spec and a rec spec into a single record.</summary>
    public static SteamPcRequirements Merge(SteamPcRequirements min, SteamPcRequirements rec)
        => new(MinRamMb: min.MinRamMb, RecRamMb: rec.RecRamMb,
               MinVramMb: min.MinVramMb, RecVramMb: rec.RecVramMb);

    /// <summary>Parse a size string like "8 GB" / "8GB" / "16 gigabytes" into MB.</summary>
    public static int? ParseSizeMb(string text)
    {
        var m = Regex.Match(text, @"(\d+)\s*(gb|gigabytes?)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        if (!int.TryParse(m.Groups[1].Value, out var gb)) return null;
        return gb * 1024;
    }

    // RAM lives on a "Memory:" line.
    private static int? ExtractRamMb(string html)
    {
        var m = Regex.Match(html,
            @"Memory:?\s*</strong>?\s*([^<]+)",
            RegexOptions.IgnoreCase);
        return m.Success ? ParseSizeMb(m.Groups[1].Value) : null;
    }

    // VRAM is usually a "Graphics:" line ending in "<size> GB".
    private static int? ExtractVramMb(string html)
    {
        var m = Regex.Match(html,
            @"Graphics:?\s*</strong>?\s*([^<]+)",
            RegexOptions.IgnoreCase);
        return m.Success ? ParseSizeMb(m.Groups[1].Value) : null;
    }
}
