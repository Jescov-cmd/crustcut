using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Win32;

namespace PrimeOSTuner.Win.Launchers;

/// <summary>
/// EA app / Origin. Scans the standard EA / Origin install roots. The game exe usually
/// lives in the game's folder, but EA can install the playable files elsewhere and leave
/// only an <c>__Installer</c> stub here — so when no exe is found in the folder we resolve
/// the real path from <c>__Installer\installerdata.xml</c> (which references a registry
/// "Install Dir"). Games we can't resolve to a real exe are skipped (not actually installed).
/// </summary>
public sealed class EaGameScanner : IExternalGameScanner
{
    private readonly IReadOnlyList<string> _roots;

    public EaGameScanner(IReadOnlyList<string>? roots = null)
    {
        _roots = roots ?? DefaultRoots();
    }

    public GameLauncher Launcher => GameLauncher.Ea;

    private static IReadOnlyList<string> DefaultRoots()
    {
        var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        return new[]
        {
            Path.Combine(pf, "EA Games"),
            Path.Combine(pf86, "EA Games"),
            Path.Combine(pf86, "Origin Games"),
        };
    }

    public IReadOnlyList<ExternalGame> Scan()
    {
        var games = new List<ExternalGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in _roots)
        {
            if (!Directory.Exists(root)) continue;

            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(root); }
            catch { continue; }

            foreach (var gameDir in dirs)
            {
                var name = Path.GetFileName(gameDir);
                if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) continue;

                // Game files in the folder; otherwise resolve the real install via the manifest.
                var exe = LauncherExe.FindPrimary(gameDir) ?? ResolveViaInstallerData(gameDir);
                if (exe is null) continue;   // only an installer stub — not actually installed

                games.Add(new ExternalGame($"ea.{name}", name, exe, GameLauncher.Ea));
            }
        }
        return games;
    }

    /// <summary>
    /// Parses <c>__Installer\installerdata.xml</c> for a launcher filePath like
    /// <c>[HKEY_LOCAL_MACHINE\SOFTWARE\EA Games\&lt;Game&gt;\Install Dir]game.exe</c> and
    /// resolves it against the live registry. Returns null if it can't find a real exe.
    /// </summary>
    public static string? ResolveViaInstallerData(string gameDir)
    {
        try
        {
            var xmlPath = Path.Combine(gameDir, "__Installer", "installerdata.xml");
            if (!File.Exists(xmlPath)) return null;

            var doc = XDocument.Load(xmlPath);
            var filePaths = doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("filePath", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                // Prefer the real game over a "trial" launcher entry.
                .OrderBy(v => v.Contains("trial", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ToList();

            foreach (var fp in filePaths)
            {
                var resolved = ResolveRegistryFilePath(fp);
                if (resolved is not null && File.Exists(resolved)) return resolved;
            }
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveRegistryFilePath(string filePath)
    {
        var m = Regex.Match(filePath, @"^\[(HKEY[^\]]+)\](.*)$");
        if (!m.Success)
            return File.Exists(filePath) ? filePath : null;

        var regRef = m.Groups[1].Value;
        var suffix = m.Groups[2].Value.Replace('/', '\\').TrimStart('\\');

        var slash = regRef.LastIndexOf('\\');
        if (slash < 0) return null;
        var fullKey = regRef.Substring(0, slash);
        var valueName = regRef.Substring(slash + 1);

        const string hklm = "HKEY_LOCAL_MACHINE\\";
        if (!fullKey.StartsWith(hklm, StringComparison.OrdinalIgnoreCase)) return null;
        var subKey = fullKey.Substring(hklm.Length);

        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var k = baseKey.OpenSubKey(subKey);
                if (k?.GetValue(valueName) is string dir && !string.IsNullOrWhiteSpace(dir))
                {
                    var full = Path.Combine(dir, suffix);
                    if (File.Exists(full)) return full;
                }
            }
            catch { /* try the other view */ }
        }
        return null;
    }
}
