using System.Text.Json;

namespace PrimeOSTuner.Win.Launchers;

/// <summary>
/// Epic Games Store. Reads the JSON manifests the launcher writes to
/// <c>%ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item</c>. Each manifest names
/// the DisplayName, InstallLocation, and LaunchExecutable — the authoritative game exe.
/// </summary>
public sealed class EpicGameScanner : IExternalGameScanner
{
    private readonly string _manifestsDir;

    public EpicGameScanner(string? manifestsDir = null)
    {
        _manifestsDir = manifestsDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Epic", "EpicGamesLauncher", "Data", "Manifests");
    }

    public GameLauncher Launcher => GameLauncher.Epic;

    public IReadOnlyList<ExternalGame> Scan()
    {
        var games = new List<ExternalGame>();
        if (!Directory.Exists(_manifestsDir)) return games;

        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(_manifestsDir, "*.item"); }
        catch { return games; }

        foreach (var f in files)
        {
            var g = ParseManifest(f);
            if (g is not null) games.Add(g);
        }
        return games;
    }

    public static ExternalGame? ParseManifest(string itemPath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(itemPath));
            var root = doc.RootElement;

            var name = GetString(root, "DisplayName");
            var install = GetString(root, "InstallLocation");
            var launch = GetString(root, "LaunchExecutable");
            var appName = GetString(root, "AppName");

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(install)) return null;

            string? exe = null;
            if (!string.IsNullOrWhiteSpace(launch))
            {
                var combined = Path.Combine(install!, launch!.Replace('/', Path.DirectorySeparatorChar));
                var fileName = Path.GetFileName(combined);
                // Some titles (e.g. Rocket League) point LaunchExecutable at a generic
                // "Launcher.exe" redirector — prefer the real game exe in that case.
                if (File.Exists(combined) && !LauncherExe.IsLikelyNonGame(fileName))
                    exe = Path.GetFullPath(combined);
                else
                    exe = LauncherExe.FindPrimary(install!)
                          ?? (File.Exists(combined) ? Path.GetFullPath(combined) : null);
            }
            else
            {
                exe = LauncherExe.FindPrimary(install!);
            }

            var id = $"epic.{(string.IsNullOrWhiteSpace(appName) ? name : appName)}";
            return new ExternalGame(id, name!.Trim(), exe, GameLauncher.Epic);
        }
        catch
        {
            return null;
        }
    }

    private static string? GetString(JsonElement obj, string prop)
        => obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
