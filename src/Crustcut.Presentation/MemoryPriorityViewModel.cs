using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Memory;
using PrimeOSTuner.Core.Tweaks;

namespace Crustcut.Presentation;

public partial class MemoryPriorityViewModel : ObservableObject
{
    private readonly PriorityRuleStore _store;
    private readonly PriorityRuleEngine _engine;
    private readonly GameRegistry _games;
    private readonly IPriorityClient? _priority;
    private readonly ITweak? _ramCleaner;

    public ObservableCollection<PriorityRuleVm> Rules { get; } = new();

    /// <summary>Levels offered in the priority dropdown. Realtime is intentionally absent.</summary>
    public static IReadOnlyList<PriorityLevel> PriorityLevels { get; } = Enum.GetValues<PriorityLevel>();

    /// <summary>Preset caps for the per-app memory-limit dropdown (view binds via DataContext).</summary>
    public static IReadOnlyList<MemoryLimitOption> MemoryLimitOptions => PriorityRuleVm.MemoryLimitOptions;

    [ObservableProperty] private string _activeFilter = "all"; // all | games | apps

    [ObservableProperty] private bool _multiSelectMode;

    public MemoryPriorityViewModel(
        PriorityRuleStore store, PriorityRuleEngine engine, GameRegistry games,
        IPriorityClient? priority = null, ITweak? ramCleaner = null)
    {
        _store = store;
        _engine = engine;
        _games = games;
        _priority = priority;
        _ramCleaner = ramCleaner;
    }

    /// <summary>"Last cleanup: 4 min ago — freed 210 MB." Empty when none has run yet.
    /// The proof-of-life line that makes automatic cleanup visibly alive or visibly dead.</summary>
    public static string LastCleanupText()
    {
        var info = RamCleanLog.TryRead();
        if (info is null) return "";
        var age = DateTime.UtcNow - info.UtcWhen;
        var when = age.TotalMinutes < 1 ? "just now"
                 : age.TotalMinutes < 60 ? $"{(int)age.TotalMinutes} min ago"
                 : age.TotalHours < 24 ? $"{(int)age.TotalHours} h ago"
                 : "over a day ago";
        return info.Trimmed == 0
            ? $"Last cleanup: {when} — nothing needed cleaning."
            : $"Last cleanup: {when} — freed {info.FreedMb} MB from {info.Trimmed} app(s).";
    }

    /// <summary>
    /// Runs the safe RAM cleanup on demand and returns a human-readable result line
    /// ("Freed 240 MB from 6 background app(s)."). This is THE missing piece that made
    /// the optimizer feel dead: cleanup previously only ran from background schedules,
    /// with no button and no feedback.
    /// </summary>
    public async Task<string> CleanNowAsync()
    {
        if (_ramCleaner is null) return "RAM cleanup isn't available on this platform.";
        try
        {
            var result = await _ramCleaner.ApplyAsync();   // Task.Runs internally
            return result.Succeeded
                ? result.Message ?? "Done."
                : $"Cleanup failed: {result.Error}";
        }
        catch (Exception ex)
        {
            return $"Cleanup failed: {ex.Message}";
        }
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
    /// user-installed apps. All rows start with default settings â€” user customizes
    /// from there. Persists the seeded list so it's stable across launches.
    /// </summary>
    public async Task AutoPopulateAsync()
    {
        var existingPaths = new HashSet<string>(
            Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);

        // Candidate gathering walks game install dirs and Program Files — all of it stays
        // off the UI thread; only the row adds below run on it.
        var candidates = await Task.Run(async () =>
        {
            var list = new List<PriorityRule>();

            // 1. Games from the library.
            foreach (var game in await _games.GetAllAsync())
            {
                if (string.IsNullOrEmpty(game.InstallPath)) continue;
                var exePath = ResolveLaunchExe(game.InstallPath);
                if (exePath is null) continue;
                list.Add(new PriorityRule(exePath, game.DisplayName,
                    PriorityLevel.Normal, false, false, IsGame: true));
            }

            // 2 + 3. Running, then installed-but-not-running user apps.
            foreach (var (path, name) in EnumerateRunningUserApps().Concat(EnumerateInstalledUserApps()))
                list.Add(new PriorityRule(path, name,
                    PriorityLevel.Normal, false, false, IsGame: false));

            return list;
        });

        var added = 0;
        foreach (var rule in candidates)
        {
            if (!existingPaths.Add(rule.ExePath)) continue;
            Rules.Add(new PriorityRuleVm(rule));
            added++;
        }

        if (added > 0) await PersistAsync();
    }

    /// <summary>
    /// Called every time the Memory Priority tab is opened: reloads from disk, DROPS rules
    /// whose target .exe no longer exists (uninstalled/deleted software), then re-scans for
    /// newly installed/running apps. Keeps the list honest instead of showing stale entries.
    /// </summary>
    public async Task RefreshAsync()
    {
        var stored = await _store.LoadAsync();
        Rules.Clear();
        foreach (var r in stored)
            if (TargetExists(r.ExePath)) Rules.Add(new PriorityRuleVm(r));

        if (Rules.Count == 0)
        {
            await AutoPopulateAsync();      // persists internally
        }
        else
        {
            await RescanRunningAppsAsync();  // add new apps (persists if any added)
            await PersistAsync();            // persist the pruning + resync the engine
        }
    }

    // A rule's target is considered gone if it has a path that no longer points to a file.
    private static bool TargetExists(string? exePath) =>
        !string.IsNullOrWhiteSpace(exePath) && File.Exists(exePath);

    /// <summary>
    /// Re-scan everything (running + installed) and add any new user-installed apps
    /// not already in the rules list. Called from the "Re-scan apps" button.
    /// </summary>
    public async Task<int> RescanRunningAppsAsync()
    {
        var existingPaths = new HashSet<string>(
            Rules.Select(r => r.ExePath), StringComparer.OrdinalIgnoreCase);

        // The Program Files walk touches hundreds of directories — off the UI thread, or
        // the tab freezes while it runs.
        var found = await Task.Run(() =>
            EnumerateRunningUserApps().Concat(EnumerateInstalledUserApps()).ToList());

        var added = 0;
        foreach (var (path, name) in found)
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
                catch { /* access denied â€” skip */ }
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
            catch { /* access denied on system processes â€” skip */ }
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
        // Removing a rule stops future enforcement but doesn't touch the running process —
        // restore Normal and lift any memory cap so the app isn't stuck until restart.
        await Task.Run(() => ApplyToRunningProcesses(vm.ExePath, PriorityLevel.Normal, memoryLimitMb: null));
        await PersistAsync();
        await SyncEngineAsync();
    }

    private CancellationTokenSource? _updateDebounce;

    /// <summary>
    /// Persist + enforce rule edits, DEBOUNCED. ComboBox bindings fire SelectionChanged
    /// with transient values while a view (re)binds — a crash mid-bind once persisted
    /// those transients and bulk-rewrote every rule to BelowNormal. Waiting for the UI to
    /// settle, then comparing every row against what's on disk, means only values that
    /// still differ after the dust clears are treated as real user edits.
    /// </summary>
    public async Task UpdateRuleAsync(PriorityRuleVm vm)
    {
        _updateDebounce?.Cancel();
        var cts = new CancellationTokenSource();
        _updateDebounce = cts;
        try { await Task.Delay(400, cts.Token); }
        catch (TaskCanceledException) { return; }

        // Settle-time snapshot: every row that genuinely differs from its persisted state.
        var dirty = Rules.Where(r => r.ToRule() != r.LastSaved).ToList();
        if (dirty.Count == 0) return;

        // The engine only acts on process START, so without this a priority change would
        // do nothing until the app was next relaunched. Scanning pids walks every running
        // process — worker thread, never the UI's.
        await Task.Run(() =>
        {
            foreach (var r in dirty)
                ApplyToRunningProcesses(r.ExePath, r.Priority, r.MemoryLimit?.Mb);
        });
        await PersistAsync();   // refreshes LastSaved for every row
        await SyncEngineAsync();
    }

    private void ApplyToRunningProcesses(string exePath, PriorityLevel level, int? memoryLimitMb)
    {
        if (_priority is null) return;
        try
        {
            foreach (var pid in _priority.FindPidsForExe(exePath))
            {
                _priority.TrySetPriority(pid, level);
                if (memoryLimitMb is int mb) _priority.TrySetMemoryLimit(pid, mb);
                else _priority.TryClearMemoryLimit(pid);
            }
        }
        catch { /* enforcement is best-effort; the rule itself is already saved */ }
    }

    /// <summary>
    /// Sets a measured, meaningful cap on every running non-game app: the smallest preset
    /// that still gives the app's LARGEST process ~25% headroom over what it uses right
    /// now (floor 512 MB). That lets the app run normally today but stops it ballooning
    /// tomorrow. Caps are per process — for multi-process apps (browsers, editors) a cap
    /// far above any single process would never bite, which is why "just pick 1 GB" felt
    /// dead. Games are never capped.
    /// </summary>
    public async Task<string> ApplyRecommendedLimitsAsync()
    {
        if (_priority is null) return "Not available on this platform.";

        var candidates = Rules.Where(r => !r.IsGame).ToList();
        var set = 0;

        var planned = await Task.Run(() =>
        {
            var result = new List<(PriorityRuleVm Vm, MemoryLimitOption Option)>();
            foreach (var vm in candidates)
            {
                long largest = 0;
                foreach (var pid in _priority.FindPidsForExe(vm.ExePath))
                {
                    try { using var p = Process.GetProcessById(pid); largest = Math.Max(largest, p.WorkingSet64); }
                    catch { /* exited between scan and read */ }
                }
                if (largest == 0) continue;   // not running — no measurement to base a cap on

                var wantMb = Math.Max(512, (long)(largest * 1.25 / (1024 * 1024)));
                var option = PriorityRuleVm.MemoryLimitOptions
                    .Where(o => o.Mb is int mb && mb >= wantMb)
                    .OrderBy(o => o.Mb)
                    .FirstOrDefault();
                if (option is null) continue;  // app legitimately needs more than the largest preset
                result.Add((vm, option));
            }
            return result;
        });

        foreach (var (vm, option) in planned)
        {
            if (vm.MemoryLimit?.Mb == option.Mb) continue;
            vm.MemoryLimit = option;
            set++;
        }

        if (set == 0)
            return "No changes — running apps are already capped sensibly or not running.";

        // Persist first (refreshes LastSaved so the ComboBox-change debounce sees nothing
        // dirty), then enforce on the running processes.
        await PersistAsync();
        await Task.Run(() =>
        {
            foreach (var (vm, option) in planned)
                ApplyToRunningProcesses(vm.ExePath, vm.Priority, option.Mb);
        });
        return $"Set limits on {set} app(s), based on what each uses right now plus headroom.";
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
                // Re-apply recommended settings â€” High priority + Protect + Booster â€” if any drift from recommended.
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
        var rules = new List<PriorityRule>(Rules.Count);
        foreach (var vm in Rules)
        {
            var rule = vm.ToRule();
            vm.LastSaved = rule;   // keep the phantom-change detector in step with disk
            rules.Add(rule);
        }
        await _store.SaveAsync(rules);
        await SyncEngineAsync();
    }

    private async Task SyncEngineAsync()
    {
        await _engine.ReloadAsync(Rules.Select(vm => vm.ToRule()));
    }
}

