namespace PrimeOSTuner.Core.Diagnosis;

public sealed record ProcSample(string Name, double CpuPercent, long WorkingSetBytes);

/// <summary>
/// Pure evaluation rules for the on-demand Diagnosis scan. Mirrors Sentinel's prime
/// directive: silent on uncertainty — a null measurement yields a null Finding (the
/// check is simply not reported) rather than a guess.
/// </summary>
public static class DiagnosisRules
{
    public static Finding? EvaluateCpuThrottle(double? avgPerfPercent)
    {
        if (avgPerfPercent is not double perf) return null;
        if (perf < 80)
            return new Finding("cpu-throttle", FindingSeverity.Problem, "CPU is being slowed down",
                $"Under load your CPU ran at {perf:F0}% of its rated speed — Windows or the hardware is holding it back (heat, power limits, or battery mode).",
                "Check cooling/dust, plug in the charger, and use the fastest power plan on the Optimize tab.",
                "Optimize");
        return new Finding("cpu-throttle", FindingSeverity.Passed, "CPU runs at full speed",
            $"Under load the CPU reached {perf:F0}% of its rated speed.");
    }

    public static Finding EvaluateBackgroundHogs(IReadOnlyList<ProcSample> top)
    {
        var hogs = top.Where(p => p.CpuPercent > 15 || p.WorkingSetBytes > 1_500L * 1024 * 1024).ToList();
        if (hogs.Count == 0)
            return new Finding("bg-hogs", FindingSeverity.Passed, "No background hogs",
                "No background app is using significant CPU or memory right now.");
        var list = string.Join(", ", hogs.Take(4).Select(h =>
            h.CpuPercent > 15 ? $"{h.Name} ({h.CpuPercent:F0}% CPU)"
                              : $"{h.Name} ({h.WorkingSetBytes / 1024 / 1024 / 1024.0:F1} GB RAM)"));
        return new Finding("bg-hogs", FindingSeverity.Warning, "Background apps are using resources",
            $"Busy right now: {list}.",
            "Close what you don't need before gaming, or remove it from startup (Task Manager → Startup apps).");
    }

    /// <summary>
    /// A driver that holds a processor core for long stretches makes everything on that core
    /// wait — mouse input, the game's frames, network packets. It feels like lag, and CPU%,
    /// GPU% and RAM all look normal, which is why it goes unnoticed. A healthy idle core
    /// spends well under 2% of its time in drivers. Real case (2026-09-25): a network card
    /// whose buffers a driver update reset held core 0 at 12% average, 22% peak.
    /// </summary>
    public static Finding EvaluateDriverLatency(double busiestCoreAvgPercent, double peakPercent)
    {
        if (busiestCoreAvgPercent >= 5 || peakPercent >= 15)
            return new Finding("driver-latency", FindingSeverity.Problem, "A driver is causing lag",
                $"One processor core is spending {busiestCoreAvgPercent:F0}% of its time inside drivers " +
                $"(peaks of {peakPercent:F0}%) while the PC is idle. Input, frames and network all wait " +
                "behind it — this is the kind of lag you feel but can't see in Task Manager.",
                "The usual cause is the network card. Turn on \"Network card latency fix\" on the Optimize tab.",
                "Optimize");
        return new Finding("driver-latency", FindingSeverity.Passed, "No driver lag",
            $"Drivers use {busiestCoreAvgPercent:F1}% of the busiest core — healthy.");
    }

    public static Finding EvaluateRam(double usedPercent)
        => usedPercent > 85
            ? new Finding("ram", FindingSeverity.Warning, "RAM is nearly full",
                $"{usedPercent:F0}% of RAM is in use while idle — games will hit the slow page file.",
                "Close apps you're not using. On Game Boost, \"Sleep game apps when you're not playing\" hands back what clip recorders and overlays hold.")
            : new Finding("ram", FindingSeverity.Passed, "RAM headroom is fine",
                $"{usedPercent:F0}% of RAM is in use.");

    public static Finding EvaluateDisk(long freeBytes, long totalBytes)
    {
        var freePct = totalBytes > 0 ? 100.0 * freeBytes / totalBytes : 100;
        return freePct < 15
            ? new Finding("disk", FindingSeverity.Warning, "System drive is nearly full",
                $"Only {freePct:F0}% free — Windows and shader caches need headroom, and a full drive slows everything.",
                "Free space: uninstall unused games/apps, run Maintenance cleanups, empty the recycle bin.")
            : new Finding("disk", FindingSeverity.Passed, "Disk space is fine",
                $"{freePct:F0}% of the system drive is free.");
    }

    public static Finding EvaluatePowerPlan(string? activePlanName)
    {
        if (string.IsNullOrWhiteSpace(activePlanName))
            return new Finding("power-plan", FindingSeverity.Passed, "Power plan", "Could not read the active plan.");
        return activePlanName.Contains("saver", StringComparison.OrdinalIgnoreCase)
            ? new Finding("power-plan", FindingSeverity.Problem, "Power saver plan is active",
                "Windows is deliberately slowing the PC to save power.",
                "Switch to the fastest power plan on the Optimize tab.", "Optimize")
            : new Finding("power-plan", FindingSeverity.Passed, "Power plan",
                $"Active plan: {activePlanName}.");
    }

    public static Finding EvaluateGpuDriver(DateTime driverDateUtc, DateTime nowUtc)
        => (nowUtc - driverDateUtc) > TimeSpan.FromDays(365)
            ? new Finding("gpu-driver", FindingSeverity.Warning, "GPU driver is old",
                $"Display driver dates from {driverDateUtc:MMMM yyyy} — over a year old; new drivers often add game-specific performance fixes.",
                "Update from NVIDIA GeForce Experience / AMD Software / Intel Arc, or the maker's website.")
            : new Finding("gpu-driver", FindingSeverity.Passed, "GPU driver is recent",
                $"Display driver dates from {driverDateUtc:MMMM yyyy}.");
}
