namespace PrimeOSTuner.Core.Profiles;

public static class BuiltInProfiles
{
    public static readonly ModeProfile Basic = new(
        Id: "basic",
        DisplayName: "Basic Mode",
        Description: "Lightweight gaming preset: enables Game Mode, disables mouse acceleration, switches to high performance power plan, optimizes visual effects. Safe and reversible on every PC.",
        TweakIds: new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "core.visual-effects"
        });

    public static readonly ModeProfile Performance = new(
        Id: "performance",
        DisplayName: "Performance Mode",
        Description: "Maximum gaming preset: everything in Basic, plus 0.5 ms timer resolution, hardware GPU scheduling, Nagle's algorithm disabled, network throttling removed, multimedia thread responsiveness maxed. Some tweaks require admin and a reboot.",
        TweakIds: new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "core.visual-effects",
            "game.timer-resolution",
            "game.hw-gpu-scheduling",
            "game.nagle-algorithm",
            "game.network-throttling",
            "game.system-responsiveness",
            "game.per-app-gpu-pref"
        });

    public static readonly IReadOnlyList<ModeProfile> All = new[] { Basic, Performance };
}
