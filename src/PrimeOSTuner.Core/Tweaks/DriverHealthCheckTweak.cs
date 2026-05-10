using System.Management;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Read-only scan: queries Win32_PnPEntity for any device whose ConfigManagerErrorCode != 0
/// (the yellow-exclamation-mark category in Device Manager).
///
/// "Apply" doesn't fix anything — it just reports the count and names. Marked non-destructive
/// so it's eligible for one-click runs (the run cost is a single WMI query).
/// </summary>
public sealed class DriverHealthCheckTweak : ITweak
{
    public string Id => "core.driver-health";
    public string DisplayName => "Check driver health";
    public string Description => "Lists devices with driver errors.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied); // always actionable as a check

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var problems = QueryProblems(ct);
            if (problems.Count == 0)
                return Task.FromResult(TweakResult.Success(message: "No driver problems detected."));

            // Surface the first few in the message; the full list goes into UndoData
            // so a future "details" view can show everything.
            var preview = string.Join(", ", problems.Take(3).Select(p => p.name));
            var more = problems.Count > 3 ? $" (+{problems.Count - 3} more)" : "";
            var msg = $"{problems.Count} device(s) with errors: {preview}{more}.";
            var details = string.Join(Environment.NewLine,
                problems.Select(p => $"{p.name} — code {p.errorCode}"));
            return Task.FromResult(TweakResult.Success(undoData: details, message: msg));
        }
        catch (Exception ex)
        {
            return Task.FromResult(TweakResult.Failure(ex.Message));
        }
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("Diagnostic check has nothing to revert."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will scan Device Manager for devices with driver errors.");

    private static List<(string name, uint errorCode)> QueryProblems(CancellationToken ct)
    {
        var results = new List<(string, uint)>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT Name, ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");
        foreach (var raw in searcher.Get())
        {
            ct.ThrowIfCancellationRequested();
            using var mo = (ManagementObject)raw;
            var name = mo["Name"] as string ?? "(unnamed)";
            var code = mo["ConfigManagerErrorCode"] is uint c ? c : 0u;
            results.Add((name, code));
        }
        return results;
    }
}
