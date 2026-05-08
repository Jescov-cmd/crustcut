using Gameloop.Vdf;
using Gameloop.Vdf.Linq;
using Microsoft.Win32;

namespace PrimeOSTuner.Win.Steam;

public sealed class SteamLibraryScanner : ISteamLibraryScanner
{
    public IReadOnlyList<SteamGame> ScanInstalledGames()
    {
        var steamRoot = ReadSteamPath();
        if (steamRoot is null || !Directory.Exists(steamRoot))
            return Array.Empty<SteamGame>();

        var libraryFoldersVdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        var libraryPaths = ParseLibraryFolders(libraryFoldersVdf);
        if (libraryPaths.Count == 0) libraryPaths = new[] { steamRoot };

        var games = new List<SteamGame>();
        foreach (var libRoot in libraryPaths)
        {
            var steamapps = Path.Combine(libRoot, "steamapps");
            if (!Directory.Exists(steamapps)) continue;

            foreach (var manifest in Directory.EnumerateFiles(steamapps, "appmanifest_*.acf"))
            {
                var g = ParseAppManifest(manifest, libRoot);
                if (g is not null) games.Add(g);
            }
        }
        return games;
    }

    public static IReadOnlyList<string> ParseLibraryFolders(string libraryFoldersVdfPath)
    {
        if (!File.Exists(libraryFoldersVdfPath)) return Array.Empty<string>();
        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(libraryFoldersVdfPath));
            var result = new List<string>();
            foreach (var child in (VObject)root.Value)
            {
                if (child.Value is VObject obj && obj.TryGetValue("path", out var pathToken))
                {
                    var p = pathToken.ToString().Replace(@"\\", @"\");
                    if (!string.IsNullOrWhiteSpace(p)) result.Add(p);
                }
            }
            return result;
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public static SteamGame? ParseAppManifest(string acfPath, string libraryPath)
    {
        if (!File.Exists(acfPath)) return null;
        try
        {
            var root = VdfConvert.Deserialize(File.ReadAllText(acfPath));
            var state = (VObject)root.Value;
            var appId = state["appid"]?.ToString() ?? "";
            var name = state["name"]?.ToString() ?? "";
            var installDir = state["installdir"]?.ToString() ?? "";
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(name)) return null;

            string? exePath = null;
            var commonInstall = Path.Combine(libraryPath, "steamapps", "common", installDir);
            if (Directory.Exists(commonInstall))
            {
                exePath = Directory.EnumerateFiles(commonInstall, "*.exe", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
            }

            return new SteamGame(appId, name, installDir, libraryPath, exePath);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
            return key?.GetValue("SteamPath") as string;
        }
        catch
        {
            return null;
        }
    }

    private static class VObjectExtensions { }
}

internal static class VObjectAccess
{
    public static bool TryGetValue(this VObject obj, string key, out VToken? token)
    {
        if (obj.ContainsKey(key))
        {
            token = obj[key];
            return true;
        }
        token = null;
        return false;
    }
}
