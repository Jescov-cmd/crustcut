using System.Diagnostics;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Runs <c>ipconfig /flushdns</c>. One-shot, no state, no undo.
/// Useful when DNS records are stale (e.g. a site moved IPs and you can't reach it).
/// </summary>
public sealed class DnsFlushTweak : ITweak
{
    public string Id => "core.dns-flush";
    public string DisplayName => "Flush DNS cache";
    public string Description => "Clears stale DNS lookups.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied); // always actionable

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        try
        {
            var psi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var proc = Process.Start(psi)
                             ?? throw new InvalidOperationException("Could not start ipconfig.");
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0)
                return TweakResult.Failure($"ipconfig exited with code {proc.ExitCode}.");
            return TweakResult.Success(message: "DNS cache cleared.");
        }
        catch (Exception ex)
        {
            return TweakResult.Failure(ex.Message);
        }
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("DNS flush cannot be reverted."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will run 'ipconfig /flushdns' to clear DNS resolver cache.");
}
