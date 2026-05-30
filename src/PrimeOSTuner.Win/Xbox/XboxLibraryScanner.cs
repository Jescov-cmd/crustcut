using System.Xml.Linq;

namespace PrimeOSTuner.Win.Xbox;

/// <summary>
/// Discovers Xbox app / Game Pass (PC) games. They install to <c>&lt;drive&gt;:\XboxGames\
/// &lt;Game&gt;\Content\</c>, and each carries a <c>MicrosoftGame.config</c> whose
/// <c>&lt;Executable Name="..."/&gt;</c> names the real game exe — the authoritative way to
/// find the process to watch / set a per-app GPU preference on.
/// </summary>
public sealed class XboxLibraryScanner : IXboxLibraryScanner
{
    // exes that ship alongside games but aren't the game itself.
    private static readonly string[] NonGameExeHints =
    {
        "crashpad", "crashhandler", "crashreport", "unitycrash", "vc_redist", "vcredist",
        "dxsetup", "dotnet", "unrealcefsubprocess", "eac", "easyanticheat", "battleye",
        "be_service", "installer", "setup", "uninstall", "prereq", "redist",
    };

    public IReadOnlyList<XboxGame> ScanInstalledGames()
    {
        var games = new List<XboxGame>();
        foreach (var root in EnumerateXboxRoots())
        {
            IEnumerable<string> gameDirs;
            try { gameDirs = Directory.EnumerateDirectories(root); }
            catch { continue; }

            foreach (var gameDir in gameDirs)
            {
                var content = Path.Combine(gameDir, "Content");
                if (!Directory.Exists(content)) continue;   // skips non-game folders like "GameSave"

                var name = Path.GetFileName(gameDir);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var exe = ResolveExecutable(content);
                games.Add(new XboxGame($"xbox.{name}", name, gameDir, exe));
            }
        }
        return games;
    }

    private static IEnumerable<string> EnumerateXboxRoots()
    {
        DriveInfo[] drives;
        try { drives = DriveInfo.GetDrives(); }
        catch { yield break; }

        foreach (var d in drives)
        {
            string root;
            try
            {
                if (!d.IsReady) continue;
                root = Path.Combine(d.RootDirectory.FullName, "XboxGames");
            }
            catch { continue; }
            if (Directory.Exists(root)) yield return root;
        }
    }

    /// <summary>Authoritative exe from MicrosoftGame.config; falls back to a best-guess exe.</summary>
    public static string? ResolveExecutable(string contentDir)
    {
        var named = ParseExecutableName(Path.Combine(contentDir, "MicrosoftGame.config"));
        if (named is not null)
        {
            var direct = Path.Combine(contentDir, named);
            if (File.Exists(direct)) return direct;

            // The Name may include a sub-path, or the layout may differ — search by filename.
            var found = SafeFindByName(contentDir, Path.GetFileName(named));
            if (found is not null) return found;
        }
        return FallbackExe(contentDir);
    }

    /// <summary>Reads &lt;ExecutableList&gt;&lt;Executable Name="Game.exe"/&gt; from the config.</summary>
    public static string? ParseExecutableName(string configPath)
    {
        if (!File.Exists(configPath)) return null;
        try
        {
            var doc = XDocument.Load(configPath);
            // Element names are case-sensitive in the config; match defensively on local name.
            var name = doc.Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "Executable", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Attribute("Name")?.Value)
                .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeFindByName(string dir, string fileName)
    {
        try
        {
            return Directory.EnumerateFiles(dir, fileName, new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            }).FirstOrDefault();
        }
        catch { return null; }
    }

    private static string? FallbackExe(string contentDir)
    {
        try
        {
            var exes = Directory.EnumerateFiles(contentDir, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(p => !IsLikelyNonGame(Path.GetFileName(p)))
                .ToList();
            if (exes.Count == 0) return null;
            // Largest exe at the top level is almost always the game.
            return exes.OrderByDescending(SafeLength).First();
        }
        catch { return null; }
    }

    private static bool IsLikelyNonGame(string fileName)
    {
        var lower = fileName.ToLowerInvariant();
        return NonGameExeHints.Any(h => lower.Contains(h));
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; } catch { return 0; }
    }
}
