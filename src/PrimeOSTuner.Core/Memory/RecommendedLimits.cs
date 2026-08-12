using System.Diagnostics;

namespace PrimeOSTuner.Core.Memory;

/// <summary>
/// The recommendation brain for per-app memory caps, shared by the Memory tab's button
/// and the engine's automatic assignment. Running apps get a measured cap (largest
/// process +25% headroom, floor 512 MB); known hogs get a curated default even when not
/// running, so the cap is waiting for them at next launch.
/// </summary>
public static class RecommendedLimits
{
    /// <summary>Must stay aligned with the UI dropdown's presets.</summary>
    public static readonly int[] PresetsMb = { 256, 512, 1024, 2048, 4096 };

    /// <summary>Curated per-process caps for well-known background memory hogs.</summary>
    public static readonly IReadOnlyDictionary<string, int> KnownAppLimitsMb =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["steamwebhelper"] = 512, ["steam"] = 1024,
            ["Discord"] = 512, ["Spotify"] = 512, ["AppleMusic"] = 512,
            ["chrome"] = 1024, ["msedge"] = 1024, ["firefox"] = 1024, ["brave"] = 1024, ["opera"] = 1024,
            ["OneDrive"] = 256, ["Dropbox"] = 512,
            ["EpicGamesLauncher"] = 512, ["upc"] = 512, ["EADesktop"] = 512, ["GalaxyClient"] = 512,
            ["Slack"] = 1024, ["Teams"] = 1024, ["ms-teams"] = 1024,
            ["Telegram"] = 512, ["WhatsApp"] = 512,
            ["SignalRgb"] = 512, ["wallpaper32"] = 512, ["wallpaper64"] = 512,
            ["Overwolf"] = 512, ["CurseForge"] = 512,
        };

    /// <summary>Smallest preset that fits, or null when the ask exceeds every preset.</summary>
    public static int? SnapToPreset(long wantMb)
    {
        foreach (var p in PresetsMb)
            if (p >= wantMb) return p;
        return null;
    }

    /// <summary>Headroom over measured usage. Generous on purpose: a cap that sits close
    /// to real usage makes Windows evict pages constantly, which is felt, not just measured.</summary>
    private const double HeadroomFactor = 1.5;

    /// <summary>
    /// True when any instance owns a visible window. Hard working-set caps force eviction
    /// the moment a process reaches the ceiling, and for a windowed (GPU-accelerated) app
    /// the evicted pages include render surfaces — the user sees black rectangles and
    /// blurry text. Automatic capping therefore applies to background processes only.
    /// </summary>
    public static bool HasVisibleWindow(string exePath, IPriorityClient priority)
    {
        if (!OperatingSystem.IsWindows()) return false;
        foreach (var pid in priority.FindPidsForExe(exePath))
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                if (p.MainWindowHandle != IntPtr.Zero) return true;
            }
            catch { /* exited between scan and read */ }
        }
        return false;
    }

    /// <summary>
    /// Recommended cap for one rule, or null when there's no honest basis for one
    /// (not running AND not in the catalog, needs more than the largest preset, or the app
    /// has a window and must not be capped automatically).
    /// </summary>
    public static int? RecommendMb(string exePath, IPriorityClient priority)
    {
        if (HasVisibleWindow(exePath, priority)) return null;

        long largest = 0;
        foreach (var pid in priority.FindPidsForExe(exePath))
        {
            try { using var p = Process.GetProcessById(pid); largest = Math.Max(largest, p.WorkingSet64); }
            catch { /* exited between scan and read */ }
        }

        if (largest > 0)
            return SnapToPreset(Math.Max(512, (long)(largest * HeadroomFactor / (1024 * 1024))));

        var exeName = Path.GetFileNameWithoutExtension(exePath);
        return KnownAppLimitsMb.TryGetValue(exeName, out var mb) ? SnapToPreset(mb) : null;
    }
}
