namespace Crustcut.Presentation.Navigation;

/// <summary>
/// The repositioned information architecture. Gaming is one section of a performance tool
/// rather than the frame of the whole product. Tabs whose feature surface only exists on
/// Windows (registry tweaks, WMI diagnosis, Appx cleanup, PresentMon sessions…) carry
/// WindowsOnly and disappear from the rail on other platforms instead of rendering broken.
/// </summary>
public static class NavCatalog
{
    public const string Performance = "PERFORMANCE";
    public const string Gaming      = "GAMING";
    public const string Learn       = "LEARN";

    private static readonly IReadOnlyList<NavItem> PrimaryAll = new[]
    {
        new NavItem("Overview",  "OVERVIEW",   ""),

        new NavItem("Optimize",  "OPTIMIZE",   Performance, WindowsOnly: true),
        new NavItem("Diagnosis", "DIAGNOSIS",  Performance, WindowsOnly: true),
        new NavItem("Cleanup",   "CLEANUP",    Performance, WindowsOnly: true),   // was "Bloatware"
        new NavItem("Memory",    "MEMORY",     Performance, WindowsOnly: true),
        new NavItem("Fans",      "FANS",       Performance, WindowsOnly: true),

        new NavItem("Games",     "GAMES",      Gaming),        // was "Library"
        new NavItem("Sessions",  "SESSIONS",   Gaming, WindowsOnly: true),        // was "Sentinel"
        new NavItem("GameBoost", "GAME BOOST", Gaming, WindowsOnly: true),

        // Every current guide is PC-hardware/Windows content (BIOS, NVIDIA panel, XMP…).
        // Windows-only until mac guides are written.
        new NavItem("Guides",    "GUIDES",     Learn, WindowsOnly: true),
        new NavItem("WhatsNew",  "WHAT'S NEW", Learn),
    };

    private static readonly IReadOnlyList<NavItem> BottomAll = new[]
    {
        new NavItem("History",  "HISTORY",  "", WindowsOnly: true),
        new NavItem("Settings", "SETTINGS", ""),
    };

    public static readonly IReadOnlyList<NavItem> Primary = Filter(PrimaryAll);
    public static readonly IReadOnlyList<NavItem> Bottom = Filter(BottomAll);

    private static IReadOnlyList<NavItem> Filter(IReadOnlyList<NavItem> items) =>
        OperatingSystem.IsWindows() ? items : items.Where(i => !i.WindowsOnly).ToList();
}
