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

    /// <summary>
    /// Steam installs these like games, but they aren't: runtimes, redistributables,
    /// soundtracks, servers, SDKs. They cluttered the Games tab ("Steamworks Common
    /// Redistributables", "Lords of the Fallen Soundtrack").
    /// </summary>
    public static bool IsNotAGame(string name)
    {
        string[] markers =
        {
            "Redistributable", "Soundtrack", "Dedicated Server", " SDK", "Steamworks Common",
            "Steam Linux Runtime", "Proton ", "Proton EasyAntiCheat", "Proton BattlEye",
            // Tools that live in the tray or run beside games. Listed as "games" they made
            // Crustcut think a game was running whenever they were open.
            "Wallpaper Engine", "Lossless Scaling", "Crosshair X", "Mouse X", "Steam 360 Video Player",
        };
        if (markers.Any(m => name.Contains(m, StringComparison.OrdinalIgnoreCase))) return true;
        // "OST" as a word only — never "Ghost" or "Host".
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"\bOST\b");
    }

    /// <summary>
    /// The exes that could be the running game, best guess first. The first *.exe in the
    /// folder used to be taken as "the game" — for Terraria that was the bundled .NET
    /// installer (dotNetFx40_Full_setup.exe), so Terraria was never detected. Installers,
    /// redistributables and crash reporters are dropped; an exe named like the game or its
    /// folder ranks first; up to 8 are kept, so a launcher-then-game pair is covered too.
    /// </summary>
    public static IReadOnlyList<string> PickExecutables(IEnumerable<string> exePaths, string installDir, string gameName)
    {
        string[] junk =
        {
            "setup", "install", "unins", "redist", "vcredist", "vc_redist", "dxsetup", "dotnet",
            "directx", "crash", "report", "easyanticheat_setup", "eac_setup", "be_setup",
            "unitycrashhandler", "cefsharp", "cef_", "cefclient", "cefsimple", "helper", "updater",
            "prereq", "uplay_", "busybox", "quickeditdisable", "elevate", "createdump", "linkshandler",
            "steamsvc",
        };
        static string Squash(string s) => new(s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        var dirKey = Squash(installDir);
        var nameKey = Squash(gameName);

        return exePaths
            .Where(p => !junk.Any(j => Path.GetFileNameWithoutExtension(p).Contains(j, StringComparison.OrdinalIgnoreCase)))
            .Select(p =>
            {
                var key = Squash(Path.GetFileNameWithoutExtension(p));
                var score = 0;
                if (key.Length > 0 && (dirKey.Contains(key) || key.Contains(dirKey) || nameKey.Contains(key) || key.Contains(nameKey))) score += 10;
                if (key.Contains("shipping") || key.Contains("win64")) score += 5;     // Unreal's real game exe
                if (key.Contains("launcher")) score -= 3;
                long size = 0;
                try { size = new FileInfo(p).Length; } catch { }
                return (Path: p, Score: score, Size: size);
            })
            .OrderByDescending(x => x.Score).ThenByDescending(x => x.Size)
            .Take(8)
            .Select(x => x.Path)
            .ToList();
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
            if (IsNotAGame(name)) return null;

            IReadOnlyList<string> exes = Array.Empty<string>();
            var commonInstall = Path.Combine(libraryPath, "steamapps", "common", installDir);
            if (Directory.Exists(commonInstall))
            {
                // A few levels deep: Unreal games keep the real exe in Binaries\Win64, and the
                // top-level exe is often just a bootstrapper that launches it and quits.
                var found = Directory.EnumerateFiles(commonInstall, "*.exe", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    MaxRecursionDepth = 3,
                    IgnoreInaccessible = true,
                });
                exes = PickExecutables(found, installDir, name);
            }

            return new SteamGame(appId, name, installDir, libraryPath, exes.FirstOrDefault(), exes);
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
