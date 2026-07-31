namespace PrimeOSTuner.Core.Memory;

/// <summary>Inputs the adaptive policy reads each tick.</summary>
/// <param name="UsedPercent">Physical RAM in use, 0-100.</param>
/// <param name="FreeBytes">Truly free/zeroed memory (not standby).</param>
/// <param name="StandbyBytes">Cached-file standby lists.</param>
/// <param name="PageInputPerSec">Hard-fault read rate — how hard the system is paging in.</param>
public sealed record RamPressureSnapshot(
    double UsedPercent, long FreeBytes, long StandbyBytes, double PageInputPerSec);

/// <summary>What the policy chose and why. Reason strings feed the engine log.</summary>
public sealed record AdaptiveDecision(bool Clean, RamCleanMode Mode, bool PurgeStandby, string Reason)
{
    public static AdaptiveDecision None(string reason) => new(false, RamCleanMode.Normal, false, reason);
}

/// <summary>
/// Pressure-proportional cleanup: nothing while memory is comfortable, measured responses
/// as pressure rises, escalation to Deep only when a Normal clean has already proven
/// ineffective, and a thrash guard that BACKS OFF when the system is mid paging-storm —
/// evicting more memory while Windows is faulting pages back in makes stutter worse,
/// which is the mistake every dumb-timer cleaner makes.
/// </summary>
public static class AdaptiveRamPolicy
{
    private const double ComfortablePercent = 70;
    private const double HighPercent = 85;
    private const double CriticalPercent = 92;
    private const long CriticalFreeBytes = 800L * 1024 * 1024;
    private const double PagingStormFaultsPerSec = 400;
    private const long IneffectiveCleanBytes = 150L * 1024 * 1024;
    private const long PurgeFreeFloorBytes = 1024L * 1024 * 1024;
    private const long PurgeCacheNormalBytes = 1536L * 1024 * 1024;
    private const long PurgeCacheCriticalBytes = 512L * 1024 * 1024;

    public static AdaptiveDecision Decide(
        RamPressureSnapshot s, TimeSpan sinceLastClean, long lastCleanFreedBytes)
    {
        if (s.PageInputPerSec > PagingStormFaultsPerSec)
            return AdaptiveDecision.None($"backoff — paging storm ({s.PageInputPerSec:F0} faults/s)");

        var critical = s.UsedPercent >= CriticalPercent || s.FreeBytes < CriticalFreeBytes;
        var high = s.UsedPercent >= HighPercent;

        if (!critical && !high && s.UsedPercent < ComfortablePercent)
            return AdaptiveDecision.None($"comfortable ({s.UsedPercent:F0}%)");

        // Standby purge rides along whenever free memory is starved while the cache
        // hoards; under critical pressure a smaller hoard already qualifies.
        var purge = s.FreeBytes < PurgeFreeFloorBytes
                    && s.StandbyBytes > (critical ? PurgeCacheCriticalBytes : PurgeCacheNormalBytes);

        var cooldown = TimeSpan.FromMinutes(critical ? 4 : high ? 6 : 10);
        if (sinceLastClean < cooldown)
            return purge
                ? new AdaptiveDecision(false, RamCleanMode.Normal, true, "cooling down — purge only")
                : AdaptiveDecision.None($"cooling down ({cooldown.TotalMinutes:F0}m at this pressure)");

        // Deep is earned, not scheduled: only when critical AND the last Normal clean
        // demonstrably didn't help.
        var mode = critical && lastCleanFreedBytes >= 0 && lastCleanFreedBytes < IneffectiveCleanBytes
            ? RamCleanMode.Deep
            : RamCleanMode.Normal;

        var level = critical ? "critical" : high ? "high" : "elevated";
        return new AdaptiveDecision(true, mode, purge,
            $"{level} ({s.UsedPercent:F0}%, {s.FreeBytes / (1024 * 1024)} MB free)");
    }
}
