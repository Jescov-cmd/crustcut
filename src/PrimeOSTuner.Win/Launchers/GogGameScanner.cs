using Microsoft.Win32;

namespace PrimeOSTuner.Win.Launchers;

/// <summary>
/// GOG Galaxy. Installed games register under
/// <c>HKLM\SOFTWARE\[WOW6432Node\]GOG.com\Games\&lt;id&gt;</c> with <c>gameName</c>,
/// <c>path</c>, and <c>exe</c> values.
/// </summary>
public sealed class GogGameScanner : IExternalGameScanner
{
    public GameLauncher Launcher => GameLauncher.Gog;

    public IReadOnlyList<ExternalGame> Scan()
    {
        var games = new List<ExternalGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var gamesKey = baseKey.OpenSubKey(@"SOFTWARE\GOG.com\Games");
                if (gamesKey is null) continue;

                foreach (var id in gamesKey.GetSubKeyNames())
                {
                    if (!seen.Add(id)) continue;
                    try
                    {
                        using var k = gamesKey.OpenSubKey(id);
                        var g = BuildGame(
                            id,
                            k?.GetValue("gameName") as string,
                            k?.GetValue("path") as string,
                            k?.GetValue("exe") as string);
                        if (g is not null) games.Add(g);
                    }
                    catch { /* skip malformed entry */ }
                }
            }
            catch { /* GOG not installed under this view */ }
        }
        return games;
    }

    public static ExternalGame? BuildGame(string id, string? gameName, string? path, string? exe)
    {
        var name = gameName;
        if (string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(path))
            name = Path.GetFileName(path!.Replace('/', '\\').TrimEnd('\\'));
        if (string.IsNullOrWhiteSpace(name)) return null;

        string? exePath = null;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            exePath = Path.IsPathRooted(exe) ? exe
                : (!string.IsNullOrWhiteSpace(path) ? Path.Combine(path!, exe!) : null);
            if (exePath is null || !File.Exists(exePath))
                exePath = !string.IsNullOrWhiteSpace(path) ? LauncherExe.FindPrimary(path!) : null;
        }
        else if (!string.IsNullOrWhiteSpace(path))
        {
            exePath = LauncherExe.FindPrimary(path!);
        }

        return new ExternalGame($"gog.{id}", name!.Trim(), exePath, GameLauncher.Gog);
    }
}
