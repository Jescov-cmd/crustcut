using PrimeOSTuner.Win;

namespace PrimeOSTuner.Core.Tweaks;

public sealed class TimerResolutionTweak : ITweak
{
    private const uint TargetHundredNs = 5000;
    private readonly ITimerResolutionClient _client;

    public string Id => "game.timer-resolution";
    public string DisplayName => "Speed up the system timer";
    public string Description => "Lowers input latency. Slight CPU cost.";
    public bool RequiresElevation => false;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public TimerResolutionTweak(ITimerResolutionClient client) { _client = client; }

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var current = _client.GetCurrentResolution();
            return Task.FromResult(Math.Abs((int)current - (int)TargetHundredNs) <= 500
                ? TweakState.Applied
                : TweakState.NotApplied);
        }
        catch { return Task.FromResult(TweakState.Unknown); }
    }

    public Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        _client.SetResolution(TargetHundredNs);
        return Task.FromResult(TweakResult.Success(TargetHundredNs.ToString()));
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
    {
        if (!uint.TryParse(undoData, out var hundredNs))
            return Task.FromResult(TweakResult.Failure("Invalid undo data"));
        _client.ClearResolution(hundredNs);
        return Task.FromResult(TweakResult.Success());
    }

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult($"Will set system timer resolution to 0.5 ms ({TargetHundredNs} × 100 ns).");
}
