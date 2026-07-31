namespace PrimeOSTuner.Core.Memory;

/// <summary>Inputs the adaptive policy reads each tick.</summary>
/// <param name="UsedPercent">Physical RAM in use, 0-100.</param>
/// <param name="FreeBytes">Truly free/zeroed memory (not standby).</param>
/// <param name="StandbyBytes">Cached-file standby lists.</param>
/// <param name="PageInputPerSec">Hard-fault read rate — how hard the system is paging in.</param>
/// <param name="GameRunning">A detected game is running — pressure hurts most right now.</param>
/// <param name="TrendPercentPerMin">RAM usage slope; positive = climbing.</param>
public sealed record RamPressureSnapshot(
    double UsedPercent, long FreeBytes, long StandbyBytes, double PageInputPerSec,
    bool GameRunning = false, double TrendPercentPerMin = 0);

/// <summary>What the policy chose and why. Reason strings feed the engine log.</summary>
public sealed record AdaptiveDecision(bool Clean, RamCleanMode Mode, bool PurgeStandby, string Reason)
{
    public static AdaptiveDecision None(string reason) => new(false, RamCleanMode.Normal, false, reason);
}

/// <summary>
/// Pressure-proportional cleanup with four kinds of smarts a dumb timer can't have:
/// (1) game-aware — while a game runs, cooldowns halve, Deep is allowed at high pressure
/// and the purge gate loosens, because in-game is when pressure costs frames;
/// (2) predictive — a fast upward RAM trend bumps the pressure level so cleanup lands
/// BEFORE critical, not after; (3) evidence-based escalation — Deep only after a Normal
/// clean measurably failed; (4) futility backoff — consecutive ineffective cleans stretch
/// the cooldown instead of churning a machine whose memory is all genuinely active.
/// The thrash guard outranks everything: never evict during a paging storm.
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
    private const long PurgeCacheInGameBytes = 768L * 1024 * 1024;
    private const double RisingFastPercentPerMin = 4;
    private const double TrendBumpFloorPercent = 75;

    public static AdaptiveDecision Decide(
        RamPressureSnapshot s, TimeSpan sinceLastClean, long lastCleanFreedBytes,
        int consecutiveIneffectiveCleans = 0)
    {
        if (s.PageInputPerSec > PagingStormFaultsPerSec)
            return AdaptiveDecision.None($"backoff — paging storm ({s.PageInputPerSec:F0} faults/s)");

        var critical = s.UsedPercent >= CriticalPercent || s.FreeBytes < CriticalFreeBytes;
        var high = s.UsedPercent >= HighPercent;
        var rising = s.TrendPercentPerMin >= RisingFastPercentPerMin
                     && s.UsedPercent >= TrendBumpFloorPercent;

        // Predictive bump: climbing fast is treated one level worse than it looks.
        if (rising && !critical && high) critical = true;
        else if (rising && !high) high = true;

        if (!critical && !high && s.UsedPercent < ComfortablePercent)
            return AdaptiveDecision.None($"comfortable ({s.UsedPercent:F0}%)");

        // Standby purge rides along whenever free memory is starved while the cache
        // hoards. In-game the bar drops — reclaiming cache is exactly the ISLC use case.
        var cacheTrigger = critical ? PurgeCacheCriticalBytes
                         : s.GameRunning ? PurgeCacheInGameBytes
                         : PurgeCacheNormalBytes;
        var purge = s.FreeBytes < PurgeFreeFloorBytes && s.StandbyBytes > cacheTrigger;

        var cooldownMin = critical ? 4.0 : high ? 6.0 : 10.0;
        if (s.GameRunning) cooldownMin = Math.Max(2, cooldownMin / 2);
        // Futility backoff: each consecutive no-yield clean stretches the wait, up to 4x.
        cooldownMin *= 1 + Math.Min(consecutiveIneffectiveCleans, 3);

        if (sinceLastClean < TimeSpan.FromMinutes(cooldownMin))
            return purge
                ? new AdaptiveDecision(false, RamCleanMode.Normal, true, "cooling down — purge only")
                : AdaptiveDecision.None($"cooling down ({cooldownMin:F0}m at this pressure)");

        // Deep is earned or game-justified, never routine: (a) critical with a proven-
        // ineffective Normal clean; (b) in-game at high+ pressure, where trimming
        // minimized apps is exactly what the player wants.
        var deepEarned = critical && lastCleanFreedBytes >= 0 && lastCleanFreedBytes < IneffectiveCleanBytes;
        var deepForGame = s.GameRunning && (critical || high);
        var mode = deepEarned || deepForGame ? RamCleanMode.Deep : RamCleanMode.Normal;

        var level = critical ? "critical" : high ? "high" : "elevated";
        var context = s.GameRunning ? ", in game" : rising ? ", rising fast" : "";
        return new AdaptiveDecision(true, mode, purge,
            $"{level} ({s.UsedPercent:F0}%, {s.FreeBytes / (1024 * 1024)} MB free{context})");
    }
}
