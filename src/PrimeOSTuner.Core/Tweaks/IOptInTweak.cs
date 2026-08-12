namespace PrimeOSTuner.Core.Tweaks;

/// <summary>
/// A tweak that must never be applied by a bundle ("Optimize now", a profile) — only by
/// the user deliberately toggling it. For settings whose benefit is genuinely
/// system-dependent and whose downside is visible: e.g. disabling Multi-Plane Overlay
/// fixes flicker on some machines and CAUSES black rectangles on others, so no automatic
/// action gets to make that bet on the user's behalf.
/// </summary>
public interface IOptInTweak
{
    bool OptIn { get; }
}
