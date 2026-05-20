namespace PrimeOSTuner.Win.Suspension;

/// <summary>
/// Classifies whether a process is safe to suspend based on its owner.
/// Built-in service accounts (SYSTEM, LOCAL SERVICE, NETWORK SERVICE,
/// TrustedInstaller) are never safe — freezing them can lock up the OS.
/// </summary>
public static class ProcessSuspendSafety
{
    public static bool IsSafeToSuspend(string? processOwner)
    {
        if (string.IsNullOrWhiteSpace(processOwner)) return false;

        var upper = processOwner.ToUpperInvariant();
        return !upper.Contains("SYSTEM")
            && !upper.Contains("LOCAL SERVICE")
            && !upper.Contains("NETWORK SERVICE")
            && !upper.Contains("TRUSTEDINSTALLER");
    }
}
