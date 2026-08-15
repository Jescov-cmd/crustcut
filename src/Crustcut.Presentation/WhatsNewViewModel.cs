namespace Crustcut.Presentation;

public sealed record WhatsNewItem(string Text);

public sealed record WhatsNewRelease(string Version, string Title, string When,
    IReadOnlyList<WhatsNewItem> Items);

/// <summary>
/// The user-facing changelog, newest first. Curated by hand, not generated: users want
/// "what changed for ME", not commit messages. Keep entries short, plain and honest —
/// including the fixes, because pretending bugs never existed reads as dishonest to
/// anyone who hit one.
/// </summary>
public static class WhatsNewCatalog
{
    private static WhatsNewItem I(string text) => new(text);

    public static readonly IReadOnlyList<WhatsNewRelease> Releases = new[]
    {
        new WhatsNewRelease("0.9.4", "Cooler, quieter, more honest", "in progress",
            new[]
            {
                I("Cool-Down Assist (Fans tab): if the CPU stays hot for 20 seconds, its turbo boost is trimmed until it cools. The safe form of heat-based power management."),
                I("Fan curves retuned for CPUs that boost: quiet when the machine is actually cool, real airflow when it's genuinely hot, and no more chasing temperatures the CPU maintains on purpose."),
                I("Fixed: the automatic memory cleaner was trimming apps minimised to the tray, which made them come back blurry. It now recognises tray apps as apps."),
                I("Fixed: power optimizations only applied to the active power plan, so switching plans in Windows silently undid them — and made a 'lower' plan run hotter. They now apply to every plan."),
                I("Fixed: the 'runs hotter' toggles (Ultimate Performance, fastest power plan, core parking) could refuse to turn off. All three now genuinely disable."),
                I("Memory tab shows what's using the most memory right now, live."),
                I("History tab shows what Crustcut did on its own — cleanups by name, fan decisions, automatic repairs."),
                I("The optimization score no longer docks points for declining risky opt-in tweaks."),
                I("Square buttons and a cleaner window — custom title bar, no more mismatched Windows chrome."),
                I("One-click bundles and Game Boost profiles never apply machine-specific tweaks (GPU scheduling, desktop restyle, Quiet CPU) — those are yours to choose. Upgrading undoes any a bundle applied earlier."),
            }),

        new WhatsNewRelease("0.9.3", "Fan control arrives", "August 12, 2026",
            new[]
            {
                I("Crustcut can run your fans: Auto follows load and temperature; Silent, Balanced and Performance pin a curve; calibration measures your hardware's real minimum speeds automatically."),
                I("Quiet CPU (Optimize tab): turn off turbo boost for a dramatically cooler, quieter machine at a small cost in burst speed."),
                I("Update notices: Crustcut tells you when a new version is out and installs it in one click."),
                I("Repairs three v0.9.2 problems automatically on first launch (black rectangles around hover effects, blurry rendering, throttled background apps)."),
            }),

        new WhatsNewRelease("0.9.2", "The adaptive memory engine", "August 3, 2026",
            new[]
            {
                I("Memory cleanup that watches pressure and decides for itself when and how hard to clean — including standing down when cleaning would do harm."),
                I("Deep Clean button for maximum reclaim, standby-cache purge for the game-stutter case, and automatic memory limits for known background hogs."),
                I("Optimizations Windows quietly reverts are re-applied and logged."),
            }),

        new WhatsNewRelease("0.9.1", "Per-app memory limits", "July 30, 2026",
            new[]
            {
                I("Cap how much RAM any app may keep (Memory tab), like Edge's resource controls but for everything."),
                I("Optimizer toggles no longer freeze the window; one-shot actions (cache flushes) became RUN buttons."),
            }),

        new WhatsNewRelease("0.9.0", "Windows + macOS", "July 30, 2026",
            new[]
            {
                I("First macOS preview build (Apple Silicon): live monitoring, Steam library and settings."),
                I("One codebase, two platforms; separate downloads on the releases page."),
            }),

        new WhatsNewRelease("0.8.0", "The control-panel redesign", "July 29, 2026",
            new[]
            {
                I("Complete rebuild: the dense control-panel look, the 8-bit wordmark, instant navigation, and a live overview."),
            }),
    };
}
