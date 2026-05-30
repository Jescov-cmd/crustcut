using Microsoft.Win32;

namespace PrimeOSTuner.Win.Launchers;

/// <summary>
/// Ubisoft Connect. Each installed game registers under
/// <c>HKLM\SOFTWARE\[WOW6432Node\]Ubisoft\Launcher\Installs\&lt;id&gt;\InstallDir</c>.
/// The folder name is the game name; the exe is found heuristically in the install dir.
/// </summary>
public sealed class UbisoftGameScanner : IExternalGameScanner
{
    public GameLauncher Launcher => GameLauncher.Ubisoft;

    public IReadOnlyList<ExternalGame> Scan()
    {
        var games = new List<ExternalGame>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var installs = baseKey.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs");
                if (installs is null) continue;

                foreach (var id in installs.GetSubKeyNames())
                {
                    if (!seen.Add(id)) continue;
                    try
                    {
                        using var k = installs.OpenSubKey(id);
                        var dir = k?.GetValue("InstallDir") as string;
                        var g = BuildGame(id, dir);
                        if (g is not null) games.Add(g);
                    }
                    catch { /* skip a malformed entry */ }
                }
            }
            catch { /* Ubisoft not installed under this view */ }
        }
        return games;
    }

    public static ExternalGame? BuildGame(string id, string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir)) return null;
        var dir = installDir.Replace('/', '\\').TrimEnd('\\');
        var name = Path.GetFileName(dir);
        if (string.IsNullOrWhiteSpace(name)) return null;

        var exe = LauncherExe.FindPrimary(dir);
        return new ExternalGame($"ubisoft.{id}", name, exe, GameLauncher.Ubisoft);
    }
}
