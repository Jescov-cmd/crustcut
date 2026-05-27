using System.Globalization;

namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Pure parser for PresentMon's CSV output. Looks up the
/// <c>msBetweenPresents</c> column by header name and extracts its value
/// from every data row. Tolerant of missing files and malformed rows —
/// anything that fails parsing is silently skipped so a partial CSV from
/// a crashed PresentMon still yields whatever samples it captured.
/// </summary>
public static class FrameTimeParser
{
    private const string TargetColumn = "msBetweenPresents";

    public static IReadOnlyList<double> ParseFile(string path)
    {
        if (!File.Exists(path)) return Array.Empty<double>();

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { return Array.Empty<double>(); }

        if (lines.Length < 2) return Array.Empty<double>();

        var headers = lines[0].Split(',');
        var columnIndex = Array.FindIndex(headers,
            h => string.Equals(h.Trim(), TargetColumn, StringComparison.OrdinalIgnoreCase));
        if (columnIndex < 0) return Array.Empty<double>();

        var samples = new List<double>(lines.Length - 1);
        for (int i = 1; i < lines.Length; i++)
        {
            var cells = lines[i].Split(',');
            if (cells.Length <= columnIndex) continue;
            if (double.TryParse(cells[columnIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
                samples.Add(ms);
        }
        return samples;
    }
}
