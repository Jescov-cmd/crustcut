using System.Text.Json;

namespace PrimeOSTuner.Core.Memory;

/// <summary>Record of the most recent cleanup, for "last cleanup: N min ago — freed X MB"
/// in the UI. Proof-of-life: without it, automatic cleanup is indistinguishable from a
/// feature that silently died.</summary>
public sealed record LastCleanInfo(DateTime UtcWhen, int Trimmed, long FreedMb);

public static class RamCleanLog
{
    public static string DefaultPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PrimeOSTuner", "last-ram-clean.json");

    public static void TryWrite(RamCleanReport report)
    {
        try
        {
            var info = new LastCleanInfo(DateTime.UtcNow, report.Trimmed, report.FreedBytes / (1024 * 1024));
            var path = DefaultPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(info));
        }
        catch { /* best-effort breadcrumb */ }
    }

    public static LastCleanInfo? TryRead()
    {
        try
        {
            var path = DefaultPath();
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<LastCleanInfo>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }
}
