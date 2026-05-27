using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.UI.ViewModels;

public partial class MemoryPriorityViewModel : ObservableObject
{
    private readonly PriorityRuleStore _store;
    private readonly PriorityRuleEngine _engine;
    private readonly GameRegistry _games;

    public ObservableCollection<PriorityRuleVm> Rules { get; } = new();

    [ObservableProperty] private string _activeFilter = "all"; // all | games | apps

    [ObservableProperty] private bool _multiSelectMode;

    public MemoryPriorityViewModel(
        PriorityRuleStore store, PriorityRuleEngine engine, GameRegistry games)
    {
        _store = store;
        _engine = engine;
        _games = games;
    }

    public async Task LoadAsync()
    {
        var rules = await _store.LoadAsync();
        Rules.Clear();
        foreach (var r in rules) Rules.Add(new PriorityRuleVm(r));

        // First launch (or after the user wiped the rules file): auto-populate with
        // detected games + currently-running user apps so the tab isn't empty.
        if (Rules.Count == 0)
        {
            await AutoPopulateAsync();
        }

        await SyncEngineAsync();
    }

    /// <summary>
    /// Seeds the rule list with detected games (from GameLibrary) + currently-running
    /// user-installed apps. All rows start with default settings — user customizes
    /// from there. Persists the seeded list so it's stable across launches.
    /// </summary>
    public async Task AutoPopulateAsync()
    {
        var existingPaths = new HashSet<string>(
            Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);
        var added = 0;

        // 1. Games from the library.
        var games = await _games.GetAllAsync();
        foreach (var game in games)
        {
            if (string.IsNullOrEmpty(game.InstallPath)) continue;
            var exePath = ResolveLaunchExe(game.InstallPath);
            if (exePath is null) continue;
            if (!existingPaths.Add(exePath)) continue;

            Rules.Add(new PriorityRuleVm(new PriorityRule(
                ExePath: exePath,
                DisplayName: game.DisplayName,
                Priority: PriorityLevel.Normal,
                ProtectFromRamCleanup: false,
                GameBooster: false,
                IsGame: true)));
            added++;
        }

        // 2. Currently-running user-installed apps.
        foreach (var (path, name) in EnumerateRunningUserApps())
        {
            if (!existingPaths.Add(path)) continue;
            Rules.Add(new PriorityRuleVm(new PriorityRule(
                ExePath: path,
                DisplayName: name,
                Priority: PriorityLevel.Normal,
                ProtectFromRamCleanup: false,
                GameBooster: false,
                IsGame: false)));
            added++;
        }

        // 3. Installed-but-not-running user apps (Program Files / LocalAppData\Programs).
        foreach (var (path, name) in EnumerateInstalledUserApps())
        {
            if (!existingPaths.Add(path)) continue;
            Rules.Add(new PriorityRuleVm(new PriorityRule(
                ExePath: path,
                DisplayName: name,
                Priority: PriorityLevel.Normal,
                ProtectFromRamCleanup: false,
                GameBooster: false,
                IsGame: false)));
            added++;
        }

        if (added > 0) await PersistAsync();
    }

    /// <summary>
    /// Re-scan everything (running + installed) and add any new user-installed apps
    /// not already in the rules list. Called from the "Re-scan apps" button.
    /// </summary>
    public async Task<int> RescanRunningAppsAsync()
    {
        var existingPaths = new HashSet<string>(
            Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var source in new[] { EnumerateRunningUserApps(), EnumerateInstalledUserApps() })
        {
            foreach (var (path, name) in source)
            {
                if (!existingPaths.Add(path)) continue;
                Rules.Add(new PriorityRuleVm(new PriorityRule(
                    ExePath: path,
                    DisplayName: name,
                    Priority: PriorityLevel.Normal,
                    ProtectFromRamCleanup: false,
                    GameBooster: false,
                    IsGame: false)));
                added++;
            }
        }
        if (added > 0) await PersistAsync();
        return added;
    }

    /// <summary>
    /// Walks the top two levels of Program Files / Program Files (x86) / LocalAppData\Programs
    /// and picks the largest .exe in each app's folder. Heuristic but catches the vast
    /// majority of user-installed apps without needing COM or registry access.
    /// </summary>
    private static IEnumerable<(string Path, string Name)> EnumerateInstalledUserApps()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        }.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p));

        // Folder names we never want to surface as "apps."
        var excludeFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Common Files", "Internet Explorer", "Windows Defender",
            "Windows Defender Advanced Threat Protection", "Windows Mail",
            "Windows Media Player", "Windows NT", "Windows Photo Viewer",
            "Windows Portable Devices", "Windows Sidebar", "WindowsApps",
            "WindowsPowerShell", "ModifiableWindowsApps", "Microsoft",
            "Reference Assemblies", "MSBuild", "dotnet"
        };

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var cap = 75;  // soft cap so the list doesn't bloat to hundreds

        foreach (var root in roots)
        {
            DirectoryInfo[] children;
            try { children = new DirectoryInfo(root).GetDirectories(); }
            catch { continue; }

            foreach (var dir in children)
            {
                if (seen.Count >= cap) break;
                if (excludeFolders.Contains(dir.Name)) continue;
                try
                {
                    // Pick the largest top-level .exe in this folder. Most apps have a
                    // clear "main exe" at the install root; for the rest this is best-effort.
                    var exe = dir.EnumerateFiles("*.exe", SearchOption.TopDirectoryOnly)
                        .OrderByDescending(f => f.Length)
                        .FirstOrDefault();
                    if (exe is null) continue;
                    if (exe.Length < 100_000) continue;  // skip tiny utility .exes
                    seen.TryAdd(exe.FullName, dir.Name);
                }
                catch { /* access denied — skip */ }
            }
        }
        return seen.Select(kv => (kv.Key, kv.Value));
    }

    /// <summary>
    /// Enumerates running processes that look like real user-installed apps (has a
    /// MainWindowHandle, lives under a normal install path, not a system process).
    /// </summary>
    private static IEnumerable<(string Path, string Name)> EnumerateRunningUserApps()
    {
        var systemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "explorer", "dwm", "csrss", "wininit", "services", "lsass", "winlogon",
            "smss", "audiodg", "fontdrvhost", "RuntimeBroker", "sihost", "taskhostw",
            "SearchHost", "StartMenuExperienceHost", "ShellExperienceHost",
            "ApplicationFrameHost", "TextInputHost", "ctfmon", "conhost",
            "svchost", "spoolsv", "WmiPrvSE", "MsMpEng", "SecurityHealthService",
            "MoUsoCoreWorker", "PrimeOSTuner.UI"
        };
        var userInstallPrefixes = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        }.Where(p => !string.IsNullOrEmpty(p)).ToArray();

        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (systemNames.Contains(p.ProcessName)) continue;
                if (p.MainWindowHandle == IntPtr.Zero) continue;
                var path = p.MainModule?.FileName;
                if (string.IsNullOrEmpty(path)) continue;
                if (path.StartsWith(@"C:\Windows\", StringComparison.OrdinalIgnoreCase)) continue;
                if (!userInstallPrefixes.Any(prefix =>
                        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) continue;
                seen.TryAdd(path, p.ProcessName);
            }
            catch { /* access denied on system processes — skip */ }
            finally { p.Dispose(); }
        }
        return seen.Select(kv => (kv.Key, kv.Value));
    }

    public async Task AddAsync(PriorityRule rule)
    {
        Rules.Add(new PriorityRuleVm(rule));
        await PersistAsync();
    }

    public async Task RemoveAsync(PriorityRuleVm vm)
    {
        Rules.Remove(vm);
        await PersistAsync();
    }

    public async Task UpdateRuleAsync(PriorityRuleVm vm)
    {
        await PersistAsync();
    }

    public async Task<(int Added, int Updated)> ApplyRecommendedToAllGamesAsync()
    {
        var games = (await _games.GetAllAsync())
            .Where(g => !string.IsNullOrEmpty(g.InstallPath))
            .ToList();

        var added = 0;
        var updated = 0;
        foreach (var game in games)
        {
            // Each game has potentially multiple EXEs; use the launch executable
            // (best-effort: the first .exe under InstallPath whose name matches the game name).
            var exePath = ResolveLaunchExe(game.InstallPath!);
            if (exePath is null) continue;

            var existing = Rules.FirstOrDefault(r =>
                string.Equals(r.ExePath, exePath, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                // Re-apply recommended settings — High priority + Protect + Booster — if any drift from recommended.
                var changed = existing.Priority != PriorityLevel.High
                           || !existing.ProtectFromRamCleanup
                           || !existing.GameBooster;
                if (!changed) continue;
                existing.Priority = PriorityLevel.High;
                existing.ProtectFromRamCleanup = true;
                existing.GameBooster = true;
                updated++;
                continue;
            }

            var rule = new PriorityRule(
                ExePath: exePath,
                DisplayName: game.DisplayName,
                Priority: PriorityLevel.High,
                ProtectFromRamCleanup: true,
                GameBooster: true,
                IsGame: true);
            Rules.Add(new PriorityRuleVm(rule));
            added++;
        }

        if (added > 0 || updated > 0) await PersistAsync();
        return (added, updated);
    }

    private static string? ResolveLaunchExe(string installPath)
    {
        try
        {
            // installPath may be a direct EXE path (e.g. from Steam scanner) or a folder.
            if (File.Exists(installPath) &&
                string.Equals(Path.GetExtension(installPath), ".exe", StringComparison.OrdinalIgnoreCase))
                return installPath;

            // Pick the largest .exe in the root install folder. Most games' launcher
            // is the largest binary by far.
            var dir = new DirectoryInfo(installPath);
            if (!dir.Exists) return null;
            return dir.EnumerateFiles("*.exe")
                .OrderByDescending(f => f.Length)
                .FirstOrDefault()?.FullName;
        }
        catch
        {
            return null;
        }
    }

    private async Task PersistAsync()
    {
        var rules = Rules.Select(vm => vm.ToRule()).ToList();
        await _store.SaveAsync(rules);
        await SyncEngineAsync();
    }

    private async Task SyncEngineAsync()
    {
        await _engine.ReloadAsync(Rules.Select(vm => vm.ToRule()));
    }
}
