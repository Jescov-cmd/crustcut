using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class RamCleanerTweak : IOneShotTweak
{
    private readonly SafeRamCleaner _cleaner;
    private readonly IRamCleanerProtectList _protectList;
    private readonly IPriorityClient _priority;
    private readonly RamCleanMode _mode;

    public string Id => _mode == RamCleanMode.Deep ? "core.ram-cleaner-deep" : "core.ram-cleaner";
    public string DisplayName => _mode == RamCleanMode.Deep ? "Deep clean RAM" : "Free up RAM now";

    // Deliberately plain about the trade-off. The previous copy called this "safe" while it
    // was purging the machine-wide standby list, which made the whole system slower.
    public string Description => _mode == RamCleanMode.Deep
        ? "Maximum cleanup: minimized apps and small background programs are trimmed, and " +
          "Windows' caches (standby, dirty pages, system file cache) are cleared. Biggest visible drop; recently-used files reload from disk a beat " +
          "slower afterwards. Focused, on-screen and PROTECT-ed apps are never touched."
        : "Releases memory held by idle background programs. Apps you're using — anything with " +
          "an open window, whatever's in focus, and their helper processes — are left alone. " +
          "Trimmed programs may pause briefly the next time you switch to them while Windows " +
          "reloads what they need.";

    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    private readonly IStandbyListClient? _standby;

    public RamCleanerTweak(SafeRamCleaner cleaner, IRamCleanerProtectList protectList,
        IPriorityClient priority, RamCleanMode mode = RamCleanMode.Normal,
        IStandbyListClient? standby = null)
    {
        _cleaner = cleaner;
        _protectList = protectList;
        _priority = priority;
        _mode = mode;
        _standby = standby;
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied);

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        // Enumerating every process and trimming is heavy synchronous work; keep it off the
        // UI thread so the window stays responsive.
        var report = await Task.Run(() =>
        {
            var protectedPids = _priority.FindPidsForExes(_protectList.Get());
            // launchingPid 0 == nothing extra to protect beyond the structural rules.
            return _cleaner.RunAsync(0, protectedPids, _mode, ct);
        }, ct);

        // NO Efficiency Mode here. Deep clean used to push trimmed processes into EcoQoS
        // (efficiency cores + reduced clocks); it caught shell and UI helper processes and
        // wrecked rendering — laggy hover states, black rectangles, fuzzy text — and the
        // flag PERSISTS until the process restarts, so it never healed on its own.
        // Memory reclamation must not silently change how the machine feels. The
        // capability stays on IPriorityClient for explicit, targeted use only.

        // Deep also pulls Mem Reduct's system-level levers (user's explicit choice —
        // maximum visible reclaim over cache-warmth purity): flush the dirty page list,
        // purge the standby cache, trim the OS's own file-cache working set.
        long cacheClearedMb = 0;
        if (_mode == RamCleanMode.Deep && _standby is not null)
        {
            // Order matters: trimming working sets and the system cache pushes pages ONTO
            // the standby list, so the purge has to run last or it purges a stale, small
            // list and leaves the freshly-evicted pages sitting there (measured: standby
            // grew 155 -> 1859 MB when the purge ran first).
            var cacheBefore = _standby.GetStandbyBytes();
            _standby.TryFlushModified();
            _standby.TryTrimSystemCache();
            _standby.TryPurge();
            var cacheAfter = _standby.GetStandbyBytes();
            cacheClearedMb = Math.Max(0, cacheBefore - cacheAfter) / (1024 * 1024);
        }

        // The message is the whole point — a silent cleanup is indistinguishable from a
        // broken one. Freed is a real measurement (working-set delta), not an estimate.
        // NAMES are included because "3 background app(s)" is undiagnosable: when the
        // cleaner trimmed a tray GUI app for weeks (the WaveLink blur bug), the log had
        // no way to show it. Who got trimmed is the single most useful fact in this line.
        RamCleanLog.TryWrite(report);
        var freedMb = report.FreedBytes / (1024 * 1024);
        var message = report.Trimmed == 0 && cacheClearedMb == 0
            ? "Nothing to clean — everything running is either in use or already lean."
            : $"Freed {freedMb} MB from {report.Trimmed} background app(s){NamesFor(report)}." +
              (cacheClearedMb > 0 ? $" Cleared {cacheClearedMb} MB of system cache." : "");
        return TweakResult.Success($"{{\"trimmed\":{report.Trimmed}}}", message);
    }

    /// <summary>" (node ×3, OneDrive)" — the trimmed exes, grouped, best-effort.</summary>
    private static string NamesFor(RamCleanReport report)
    {
        if (report.TrimmedPids is not { Count: > 0 } pids) return "";
        var names = new List<string>();
        foreach (var pid in pids)
        {
            try { using var p = System.Diagnostics.Process.GetProcessById(pid); names.Add(p.ProcessName); }
            catch { /* already exited — its name is lost, not worth failing the message */ }
        }
        if (names.Count == 0) return "";
        var grouped = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Count() > 1 ? $"{g.Key} ×{g.Count()}" : g.Key);
        return $" ({string.Join(", ", grouped)})";
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("RAM trim is not revertible — Windows repopulates working sets as processes resume work."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will trim idle background programs, leaving apps you're using untouched.");
}
