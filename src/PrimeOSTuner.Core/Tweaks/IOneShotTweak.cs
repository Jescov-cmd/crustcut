namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// A tweak that performs a one-time ACTION (flush a cache, run a check) rather than
/// holding a persistent setting. Its probe is always NotApplied because there is no "on"
/// state to hold — the UI must render it as a run-button, never a toggle, and score
/// calculators must exclude it (it can never count as "applied").
/// </summary>
public interface IOneShotTweak : ITweak
{
}
