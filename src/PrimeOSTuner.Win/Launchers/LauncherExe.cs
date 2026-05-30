namespace PrimeOSTuner.Win.Launchers;

/// <summary>
/// Shared heuristic for picking a game's main executable out of its install folder when
/// the launcher doesn't tell us directly (Ubisoft, EA). Skips the obvious non-game exes
/// (crash handlers, redistributables, launchers, anti-cheat services) and prefers the
/// largest real exe — which is almost always the game.
/// </summary>
public static class LauncherExe
{
    private static readonly string[] NonGameHints =
    {
        "crashpad", "crashhandler", "crashreport", "unitycrash", "vc_redist", "vcredist",
        "dxsetup", "directx", "dotnet", "unrealcefsubprocess", "cefsubprocess",
        "easyanticheat", "battleye", "be_service", "anticheat", "eac_",
        "installer", "setup", "uninstall", "prereq", "redist", "bootstrapper",
        "helper", "service", "cleanup", "touchup", "activation", "support",
        "report", "diag", "update", "register", "config",
    };

    public static string? FindPrimary(string installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir) || !Directory.Exists(installDir)) return null;

        // Prefer a real exe at the top level first (most games put it there).
        var top = LargestNonHelper(installDir, recurse: false, maxDepth: 1);
        if (top is not null) return top;

        // Otherwise look a few levels deep (e.g. Binaries\Win64\Game.exe).
        return LargestNonHelper(installDir, recurse: true, maxDepth: 4);
    }

    private static string? LargestNonHelper(string dir, bool recurse, int maxDepth)
    {
        try
        {
            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = recurse,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
                MaxRecursionDepth = maxDepth,
            };
            return Directory.EnumerateFiles(dir, "*.exe", opts)
                .Where(p => !IsLikelyNonGame(Path.GetFileName(p)))
                .OrderByDescending(SafeLength)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    public static bool IsLikelyNonGame(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return NonGameHints.Any(h => lower.Contains(h));
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }
}
