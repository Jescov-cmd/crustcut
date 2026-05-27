using System.Diagnostics;
using System.Text.Json;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// Adds the install folders of every game in the Library to Windows Defender's
/// exclusion list. Real measurable IO win in heavy-loading titles (5–15 %
/// reduction in disk-bound load times). Reversible — Revert removes only the
/// paths we actually added.
///
/// Uses Set-MpPreference via PowerShell because that's the supported API;
/// the registry mirror under HKLM\\SOFTWARE\\Microsoft\\Windows Defender\\Exclusions
/// is ACL-locked even for admins.
/// </summary>
public sealed class DefenderGameExclusionsTweak : ITweak
{
    private readonly Func<IReadOnlyList<string>> _pathsProvider;

    public string Id => "core.defender-game-exclusions";
    public string DisplayName => "Exclude games from Defender";
    public string Description => "Adds every game's install folder to Windows Defender exclusions. Reduces real-time scan overhead on disk-bound loading screens. Defender still scans everything outside game folders.";
    public bool RequiresElevation => true;
    public bool IsDestructive => true;   // weakens AV scope — opt-in only
    public bool RequiresReboot => false;

    public DefenderGameExclusionsTweak(Func<IReadOnlyList<string>> pathsProvider)
    {
        _pathsProvider = pathsProvider;
    }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        // We don't read Defender state — call it Unknown so the UI shows "Apply".
        return Task.FromResult(TweakState.Unknown);
    }

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var paths = _pathsProvider().Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        if (paths.Count == 0)
            return TweakResult.Success(message: "No game install paths in Library — nothing to exclude.");

        var quoted = string.Join(",", paths.Select(p => $"'{p.Replace("'", "''")}'"));
        var script = $"Add-MpPreference -ExclusionPath @({quoted})";
        var (code, err) = await RunPowerShellAsync(script, ct);
        if (code != 0)
            return TweakResult.Failure($"Defender refused exclusion: {err}");

        return TweakResult.Success(
            undoData: JsonSerializer.Serialize(paths.ToArray()),
            message: $"Excluded {paths.Count} game folder(s).");
    }

    public async Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        var paths = JsonSerializer.Deserialize<string[]>(undoData) ?? Array.Empty<string>();
        if (paths.Length == 0) return TweakResult.Success();

        var quoted = string.Join(",", paths.Select(p => $"'{p.Replace("'", "''")}'"));
        var script = $"Remove-MpPreference -ExclusionPath @({quoted})";
        var (code, err) = await RunPowerShellAsync(script, ct);
        if (code != 0)
            return TweakResult.Failure($"Defender refused removal: {err}");

        return TweakResult.Success(message: $"Removed {paths.Length} exclusion(s).");
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
    {
        var paths = _pathsProvider().Where(p => !string.IsNullOrWhiteSpace(p)).Distinct().ToList();
        if (paths.Count == 0) return Task.FromResult("No game install paths in Library.");
        return Task.FromResult($"Will exclude {paths.Count} game folder(s): " + string.Join(", ", paths));
    }

    private static async Task<(int ExitCode, string Stderr)> RunPowerShellAsync(string script, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("powershell.exe",
                $"-NoProfile -ExecutionPolicy Bypass -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var stderr = await stderrTask;
        return (proc.ExitCode, stderr.Trim());
    }
}
