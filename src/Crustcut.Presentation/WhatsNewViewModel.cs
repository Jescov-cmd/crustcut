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
        new WhatsNewRelease("0.9.5", "Simpler, lighter, and a real Game mode", "November 26, 2026",
            new[]
            {
                I("A simpler Crustcut: four pages instead of thirteen. Home tells you in one sentence how your PC is doing and lists anything worth fixing. Boost has five switches instead of forty. Games and Settings hold the rest, and every older page is still one click away."),
                I("A new look: true black and white, easy on the eyes and on OLED screens."),
                I("Game mode (Boost page): when any game starts, Crustcut switches to the fastest power plan, sharpens the system timer, puts the game first in line for the CPU and pauses background apps. Everything goes back the moment you quit. Settings you had already turned on yourself are left alone."),
                I("Your devices are off-limits: software that runs a mouse, keyboard, headset, lights or Stream Deck (Logitech G HUB, SignalRGB, Elgato, Razer, Corsair, SteelSeries, Realtek audio and more) is never paused, trimmed, capped or lowered in priority, and its startup entry can't be switched off by accident."),
                I("Stronger heat protection: Cool-down assist keeps working with fan control off, follows whichever power plan is active, and switches on by itself while Game mode is running a game."),
                I("Fixed: building a Minecraft mod no longer counts as playing Minecraft, and long pages scroll all the way to the bottom."),
                I("A new tray popup: click the tray icon for temperature, memory, the Game mode switch and Free up memory without opening the window."),
                I("Much lighter: Crustcut now runs on .NET 10, and closing it to the tray releases the window entirely. Sitting in the tray it uses a fraction of the memory it did before, and long lists no longer freeze the window for a second when you open a tab."),
                I("Finds more games, faster: games are spotted the moment they start, and Crustcut now recognises games it has never heard of — anything that fills the screen and keeps the graphics card busy is added to your Games list on its own."),
                I("Sleep apps you're not using (Game boost page): game helpers like Medal, overlays and crosshair apps sleep when you're not playing and wake the moment a game starts. Mouse, keyboard, headset and lighting software is never put to sleep."),
                I("Game sessions now say why a game stuttered — for example Windows pulling memory back off the disk while another app held gigabytes — not just that it did."),
                I("Less lag online (Optimize page, opt-in): a network card fix for drivers that ship with tiny buffers. It undoes exactly, and puts itself back if a driver update resets it. Diagnosis also now spots when a driver is causing lag."),
                I("Steadier fans: the CPU temperature is averaged over a few seconds, so short boost spikes no longer make the reading or the fans jump around. The 90 °C safety limit still reacts instantly. Auto also waits 3 minutes before stepping the fans down, so they stop speeding up and slowing down every couple of minutes."),
                I("One \"Fix readings\" button on the Fans page replaces the row of per-fan RPM buttons, for when a fan's speed doesn't match your BIOS."),
                I("Automatic cache purging removed: it made the memory number in Task Manager look better but freed nothing real, and made recently used files load slower afterwards. Clearing the cache is now only ever a button you press."),
                I("Fixed: settings could be lost if two parts of the app saved at once, or if the settings file couldn't be read. Saves now take turns, and Crustcut never writes defaults over your settings."),
                I("Crustcut now writes a crash log instead of silently disappearing from the tray, and one bad click can no longer take the whole app down."),
                I("Fixed: on narrow windows, cards on the Fans, Settings and What's new pages could run past the right edge. Pages now fit the window, and content stays centred on wide screens."),
            }),

        new WhatsNewRelease("0.9.4", "Cooler, quieter, more honest", "August 14, 2026",
            new[]
            {
                I("Startup manager (Cleanup tab): see everything that launches with Windows and switch off what you don't need — the single biggest everyday speed win. Nothing is uninstalled, and Task Manager shows the same state."),
                I("Minecraft is finally detected — including modded, through Modrinth, CurseForge and Lunar. Crustcut used to be blind to Java games and would treat your machine as idle while you played."),
                I("Pause background apps while gaming (Game Boost tab): RGB, wallpaper, sync and launcher apps are frozen in place when any game starts and wake untouched when you quit. Microphone, capture and streaming tools are never paused."),
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
