namespace PrimeOSTuner.Core.Profiles;

public static class BuiltInProfiles
{
    public static readonly ModeProfile Basic = new(
        Id: "basic",
        DisplayName: "Basic Mode",
        Description: "Lightweight gaming preset: enables Game Mode, disables mouse acceleration, switches to high performance power plan. Safe and reversible on every PC.",
        TweakIds: new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "core.win32-priority-separation",
            "core.startup-delay",
            "core.werror-reporting",
            "core.activity-history",
            "core.advertising-id",
        });

    // NOTE: opt-in tweaks (hardware GPU scheduling, disable-animations, Quiet CPU, MPO)
    // must never appear in these lists — and ProfileApplier filters them even if one
    // sneaks in. They're the tweaks whose failure mode is visible on the wrong machine.
    public static readonly ModeProfile Performance = new(
        Id: "performance",
        DisplayName: "Performance Mode",
        Description: "Maximum gaming preset: everything in Basic, plus 0.5 ms timer resolution, multimedia thread responsiveness maxed, and the Ultimate Performance power plan. Some tweaks require admin and a reboot.",
        TweakIds: new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "game.timer-resolution",
            "game.system-responsiveness",
            "game.per-app-gpu-pref",
            "core.win32-priority-separation",
            "core.startup-delay",
            "core.werror-reporting",
            "core.game-dvr-disable",
            "core.fullscreen-optimizations",
            "core.ceip-disable",
            "core.activity-history",
            "core.advertising-id",
            "core.location-tracking",
            "core.feedback-diagnostics",
            "core.typing-personalization",
            "core.telemetry-disable",
            "core.usb-selective-suspend",
            "core.power-throttling-disable",
            "core.ultimate-performance",
            "game.mmcss-games-priority",
            "core.snappy-ui",
            "core.widgets-disable",
            "core.ndu-disable",
            "core.sticky-keys-shortcuts",
        });

    public static readonly ModeProfile Aggressive = new(
        Id: "aggressive",
        DisplayName: "Aggressive",
        Description: "Performance plus advanced power tweaks: disables Cortana, Modern Standby, and hibernation. May affect sleep/resume on some laptops.",
        TweakIds: new List<string>(Performance.TweakIds)
        {
            "core.cortana-disable",
            "core.modern-standby-disable",
            "core.hibernation-disable",
        });

    // Automatic "Game mode": applied to every detected game without a per-game profile and
    // reverted when it closes. Temporary, safe tweaks only. Deliberately NOT in All — All
    // feeds the Game boost preset list and the opt-in safety test, and that list stays fixed.
    // ("core.power-throttling-disable" and "game.system-responsiveness" were wanted here but
    // have no ITweak implementation yet, so they're left out rather than failing every launch.)
    public static readonly ModeProfile GameSession = new(
        Id: "game-session",
        DisplayName: "Game mode",
        Description: "Temporary performance set applied while any game runs, put back when it closes.",
        TweakIds: new[]
        {
            "core.ultimate-performance",   // makes sure the plan exists
            "core.power-plan",             // switches to it (put back on exit unless it was already on)
            "game.timer-resolution",
            "game.mmcss-games-priority",
        });

    public static readonly IReadOnlyList<ModeProfile> All = new[] { Basic, Performance, Aggressive };
}
