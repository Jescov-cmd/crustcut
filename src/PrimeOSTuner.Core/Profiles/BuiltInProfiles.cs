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

    public static readonly ModeProfile Performance = new(
        Id: "performance",
        DisplayName: "Performance Mode",
        Description: "Maximum gaming preset: everything in Basic, plus 0.5 ms timer resolution, hardware GPU scheduling, Nagle's algorithm disabled, network throttling removed, multimedia thread responsiveness maxed. Some tweaks require admin and a reboot.",
        TweakIds: new[]
        {
            "game.game-mode",
            "game.mouse-accel",
            "core.power-plan",
            "game.timer-resolution",
            "game.hw-gpu-scheduling",
            "game.nagle-algorithm",
            "game.network-throttling",
            "game.system-responsiveness",
            "game.per-app-gpu-pref",
            "core.win32-priority-separation",
            "core.startup-delay",
            "core.tcp-ack-frequency",
            "core.tcp-delivery-acceleration",
            "core.qos-bandwidth",
            "core.netbios-disable",
            "core.werror-reporting",
            "core.game-dvr-disable",
            "core.fullscreen-optimizations",
            "core.sysmain-disable",
            "core.connected-user-experiences",
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
        });

    public static readonly ModeProfile Aggressive = new(
        Id: "aggressive",
        DisplayName: "Aggressive",
        Description: "Performance plus advanced tweaks. Disable IPv6, Cortana, search indexing.",
        TweakIds: new List<string>(Performance.TweakIds)
        {
            "core.ipv6-disable",
            "core.cortana-disable",
            "core.search-indexing-tune",
            "core.modern-standby-disable",
            "core.hibernation-disable",
        });

    public static readonly IReadOnlyList<ModeProfile> All = new[] { Basic, Performance, Aggressive };
}
