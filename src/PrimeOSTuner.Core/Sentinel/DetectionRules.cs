namespace PrimeOSTuner.Core.Sentinel;

/// <summary>
/// Pure detection rules. Given a snapshot, the parsed spec, and a rolling
/// CPU history, return the list of currently-active problems.
///
/// "Silent on uncertainty" is the prime directive — when a field is null
/// (unknown spec) or negative (unknown sample), the affected rule does
/// nothing. Never a false alarm.
/// </summary>
public static class DetectionRules
{
    // VRAM rule: usage > 95% of card AND game's recommended <= 50% of card.
    private const double VramHighWatermark = 0.95;
    private const double VramRecCardRatio  = 0.50;

    // RAM rule: usage > 95% of total AND game's recommended <= 75% of total.
    private const double RamHighWatermark = 0.95;
    private const double RamRecTotalRatio = 0.75;

    // CPU rule: every sample in trailing 30s window > 90%.
    private const double CpuHighWatermark = 90.0;
    private static readonly TimeSpan CpuWindow = TimeSpan.FromSeconds(30);

    public static IReadOnlyList<Problem> Evaluate(
        MetricsSnapshot snap,
        SteamPcRequirements spec,
        Queue<(DateTime At, double Percent)> rollingCpuWindow)
    {
        var problems = new List<Problem>();

        if (TryVramOverhead(snap, spec, out var vram)) problems.Add(vram!);
        if (TryRamPressure(snap, spec, out var ram))   problems.Add(ram!);
        if (TryCpuSaturated(snap, rollingCpuWindow, out var cpu)) problems.Add(cpu!);

        return problems;
    }

    private static bool TryVramOverhead(MetricsSnapshot snap, SteamPcRequirements spec, out Problem? p)
    {
        p = null;
        if (snap.VramUsedBytes < 0 || snap.VramTotalBytes <= 0) return false;   // unknown sample
        if (spec.RecVramMb is not int recMb) return false;                       // unknown spec

        var cardMb = (int)(snap.VramTotalBytes / 1024 / 1024);
        var usedMb = (int)(snap.VramUsedBytes / 1024 / 1024);

        var usageRatio = (double)usedMb / cardMb;
        var recRatio   = (double)recMb / cardMb;

        if (usageRatio <= VramHighWatermark) return false;
        if (recRatio   >  VramRecCardRatio)  return false;

        p = new Problem(
            ProblemKind.VramOverhead,
            $"VRAM is {usedMb} MB of {cardMb} MB — game's recommended is only {recMb} MB.",
            snap.At);
        return true;
    }

    private static bool TryRamPressure(MetricsSnapshot snap, SteamPcRequirements spec, out Problem? p)
    {
        p = null;
        if (snap.RamUsedBytes < 0 || snap.RamTotalBytes <= 0) return false;
        if (spec.RecRamMb is not int recMb) return false;

        var totalMb = (int)(snap.RamTotalBytes / 1024 / 1024);
        var usedMb  = (int)(snap.RamUsedBytes / 1024 / 1024);

        var usageRatio = (double)usedMb / totalMb;
        var recRatio   = (double)recMb / totalMb;

        if (usageRatio <= RamHighWatermark) return false;
        if (recRatio   >  RamRecTotalRatio) return false;

        p = new Problem(
            ProblemKind.RamPressure,
            $"System RAM is {usedMb} MB of {totalMb} MB — game's recommended is only {recMb} MB.",
            snap.At);
        return true;
    }

    private static bool TryCpuSaturated(
        MetricsSnapshot snap,
        Queue<(DateTime At, double Percent)> window,
        out Problem? p)
    {
        p = null;
        if (snap.SystemCpuPercent < 0) return false;
        if (window.Count == 0) return false;

        // Need a full 30s of history.
        var span = snap.At - window.Peek().At;
        if (span < CpuWindow) return false;

        // Every sample in the window AND the current snapshot must exceed the watermark.
        if (snap.SystemCpuPercent <= CpuHighWatermark) return false;
        if (window.Any(s => s.Percent <= CpuHighWatermark)) return false;

        p = new Problem(
            ProblemKind.CpuSaturated,
            $"System CPU has been above {CpuHighWatermark:F0}% for the last {CpuWindow.TotalSeconds:F0} s.",
            snap.At);
        return true;
    }
}
