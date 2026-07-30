using System.Text.RegularExpressions;
using PrimeOSTuner.Win.Steam;
using PrimeOSTuner.Win.Xbox;

namespace PrimeOSTuner.Mac;

/// <summary>
/// Steam-on-macOS library scan. Same on-disk layout as Windows —
/// `~/Library/Application Support/Steam/steamapps` with `libraryfolders.vdf` naming any
/// extra libraries and one `appmanifest_&lt;appid&gt;.acf` per installed game. Parsed with
/// regex instead of a VDF library: we only need three scalar fields.
/// UNTESTED ON REAL HARDWARE — built on Windows; returns empty on any surprise.
/// </summary>
public sealed class MacSteamLibraryScanner : ISteamLibraryScanner
{
    private static readonly Regex PathRx = new("\"path\"\\s+\"([^\"]+)\"", RegexOptions.Compiled);
    private static readonly Regex FieldRx = new("\"(appid|name|installdir)\"\\s+\"([^\"]+)\"", RegexOptions.Compiled);

    public IReadOnlyList<SteamGame> ScanInstalledGames()
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var root = Path.Combine(home, "Library", "Application Support", "Steam", "steamapps");
            if (!Directory.Exists(root)) return Array.Empty<SteamGame>();

            var libraries = new List<string> { root };
            var vdf = Path.Combine(root, "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in PathRx.Matches(File.ReadAllText(vdf)))
                {
                    var extra = Path.Combine(m.Groups[1].Value, "steamapps");
                    if (Directory.Exists(extra) && !libraries.Contains(extra)) libraries.Add(extra);
                }

            var games = new List<SteamGame>();
            var seen = new HashSet<string>();
            foreach (var lib in libraries)
                foreach (var acf in Directory.EnumerateFiles(lib, "appmanifest_*.acf"))
                {
                    var fields = new Dictionary<string, string>();
                    foreach (Match m in FieldRx.Matches(File.ReadAllText(acf)))
                        fields.TryAdd(m.Groups[1].Value, m.Groups[2].Value);

                    if (!fields.TryGetValue("appid", out var id) ||
                        !fields.TryGetValue("name", out var name) ||
                        !fields.TryGetValue("installdir", out var dir)) continue;
                    if (!seen.Add(id)) continue;

                    var installPath = Path.Combine(lib, "common", dir);
                    games.Add(new SteamGame(id, name, dir, lib, FindExecutable(installPath)));
                }
            return games;
        }
        catch
        {
            return Array.Empty<SteamGame>();
        }
    }

    /// <summary>Mac games ship as .app bundles — the real binary is Contents/MacOS/&lt;name&gt;.</summary>
    private static string? FindExecutable(string installPath)
    {
        try
        {
            if (!Directory.Exists(installPath)) return null;
            var bundle = Directory.EnumerateDirectories(installPath, "*.app").FirstOrDefault();
            if (bundle is null) return null;
            var macos = Path.Combine(bundle, "Contents", "MacOS");
            return Directory.Exists(macos) ? Directory.EnumerateFiles(macos).FirstOrDefault() : null;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>There is no Xbox app on macOS; the registry always gets an empty scan.</summary>
public sealed class NullXboxLibraryScanner : IXboxLibraryScanner
{
    public IReadOnlyList<XboxGame> ScanInstalledGames() => Array.Empty<XboxGame>();
}
