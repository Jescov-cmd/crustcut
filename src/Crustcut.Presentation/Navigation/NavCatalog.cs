namespace Crustcut.Presentation.Navigation;

/// <summary>
/// The repositioned information architecture. Gaming is one section of a performance tool
/// rather than the frame of the whole product. Tabs whose feature surface only exists on
/// Windows (registry tweaks, WMI diagnosis, Appx cleanup, PresentMon sessions…) carry
/// WindowsOnly and disappear from the rail on other platforms instead of rendering broken.
/// </summary>
public static class NavCatalog
{
    public const string Performance = "Performance";
    public const string Gaming      = "Gaming";
    public const string Learn       = "Learn";

    private static readonly IReadOnlyList<NavItem> PrimaryAll = new[]
    {
        new NavItem("Overview", "Overview", "", Icon: "\uE80F"),

        new NavItem("Optimize", "Optimize", Performance, WindowsOnly: true, Icon: "\uEC4A"),
        new NavItem("Diagnosis", "Diagnosis", Performance, WindowsOnly: true, Icon: "\uE9D9"),
        new NavItem("Cleanup", "Cleanup", Performance, WindowsOnly: true, Icon: "\uEA99"),   // was "Bloatware"
        new NavItem("Memory", "Memory", Performance, WindowsOnly: true, Icon: "\uEEA0"),
        new NavItem("Fans", "Fans", Performance, WindowsOnly: true, Icon: "\uE9CA"),

        new NavItem("Games", "Games", Gaming, Icon: "\uE7FC"),        // was "Library"
        new NavItem("Sessions", "Sessions", Gaming, WindowsOnly: true, Icon: "\uE9D2"),        // was "Sentinel"
        new NavItem("GameBoost", "Game boost", Gaming, WindowsOnly: true, Icon: "\uE945"),

        // Every current guide is PC-hardware/Windows content (BIOS, NVIDIA panel, XMP…).
        // Windows-only until mac guides are written.
        new NavItem("Guides", "Guides", Learn, WindowsOnly: true, Icon: "\uE8F1"),
        new NavItem("WhatsNew", "What's new", Learn, Icon: "\uE734"),
    };

    private static readonly IReadOnlyList<NavItem> BottomAll = new[]
    {
        new NavItem("History", "History", "", WindowsOnly: true, Icon: "\uE81C"),
        new NavItem("Settings", "Settings", "", Icon: "\uE713"),
    };

    public static readonly IReadOnlyList<NavItem> Primary = Filter(PrimaryAll);
    public static readonly IReadOnlyList<NavItem> Bottom = Filter(BottomAll);

    private static IReadOnlyList<NavItem> Filter(IReadOnlyList<NavItem> items) =>
        OperatingSystem.IsWindows() ? items : items.Where(i => !i.WindowsOnly).ToList();
}
