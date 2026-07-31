using PrimeOSTuner.Core.Memory;

namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// One-shot standby-cache purge (the ISLC feature, minus ISLC's window). The description
/// is deliberately blunt that this is a stutter fix for full-RAM gaming, not a routine
/// cleanup — the cache being purged normally makes the system FASTER.
/// </summary>
public sealed class StandbyPurgeTweak : IOneShotTweak
{
    private readonly IStandbyListClient _standby;

    public string Id => "core.standby-purge";
    public string DisplayName => "Clear standby cache";
    public string Description =>
        "Empties Windows' cached-file list (the ISLC trick). Useful when games stutter " +
        "with RAM nearly full — Windows reclaiming cache mid-game causes hitches. Don't run " +
        "it habitually: the cache normally speeds things up. Settings has an automatic mode " +
        "that fires only when memory is genuinely tight.";

    public bool RequiresElevation => true;
    public bool IsDestructive => false;
    public bool RequiresReboot => false;

    public StandbyPurgeTweak(IStandbyListClient standby) => _standby = standby;

    public Task<TweakState> ProbeAsync(CancellationToken ct = default)
        => Task.FromResult(TweakState.NotApplied);   // one-shot: always actionable

    public async Task<TweakResult> ApplyAsync(IProgress<int>? progress = null, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var before = _standby.GetStandbyBytes();
            if (!_standby.TryPurge())
                return TweakResult.Failure("Windows refused the purge (needs administrator).");
            var after = _standby.GetStandbyBytes();
            var clearedMb = Math.Max(0, before - after) / (1024 * 1024);
            return TweakResult.Success(message: clearedMb == 0
                ? "Standby cache was already empty."
                : $"Cleared {clearedMb} MB of standby cache.");
        }, ct);
    }

    public Task<TweakResult> RevertAsync(string undoData, CancellationToken ct = default)
        => Task.FromResult(TweakResult.Failure("The cache refills itself as files are read — nothing to revert."));

    public Task<string> PreviewAsync(CancellationToken ct = default)
        => Task.FromResult("Will ask Windows to drop the standby (cached-file) memory lists.");
}
