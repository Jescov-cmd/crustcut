namespace Crustcut.Presentation.Navigation;

/// <summary>
/// Four pages in the rail — Home, Boost, Games, Settings. The thirteen-page rail made
/// people hunt; every older page still exists but is reached from inside one of the four
/// (a "Show all settings" row on Boost, a "Back to Boost" bar at the top), so nothing was
/// removed, it moved one level down. Tabs whose feature surface only exists on Windows
/// carry WindowsOnly and disappear on other platforms instead of rendering broken.
/// </summary>
public static class NavCatalog
{
    private static readonly IReadOnlyList<NavItem> PrimaryAll = new[]
    {
        new NavItem("Home", "Home", "", Icon: ""),
        new NavItem("Boost", "Boost", "", WindowsOnly: true, Icon: ""),
        new NavItem("Games", "Games", "", Icon: ""),
        new NavItem("Settings", "Settings", "", Icon: ""),
    };

    /// <summary>Pages reached from inside a rail page. Parent = the rail page that stays
    /// highlighted while one is open, and where its back bar returns to.</summary>
    private static readonly IReadOnlyList<SubPage> SubPagesAll = new[]
    {
        new SubPage("Optimize", "All settings", "Boost", WindowsOnly: true),
        new SubPage("Fans", "Fans", "Boost", WindowsOnly: true),
        new SubPage("GameBoost", "Game presets and sleeping apps", "Boost", WindowsOnly: true),
        new SubPage("Memory", "Memory", "Home", WindowsOnly: true),
        new SubPage("Cleanup", "Clean up apps", "Home", WindowsOnly: true),
        new SubPage("Diagnosis", "Diagnosis", "Home", WindowsOnly: true),
        new SubPage("Overview", "Live details", "Home"),
        new SubPage("Sessions", "Game sessions", "Games", WindowsOnly: true),
        new SubPage("History", "History", "Settings", WindowsOnly: true),
        new SubPage("Guides", "Guides", "Settings", WindowsOnly: true),
        new SubPage("WhatsNew", "What's new", "Settings"),
    };

    public static readonly IReadOnlyList<NavItem> Primary = Filter(PrimaryAll);
    public static readonly IReadOnlyList<NavItem> Bottom = Array.Empty<NavItem>();
    public static readonly IReadOnlyList<SubPage> SubPages =
        OperatingSystem.IsWindows() ? SubPagesAll : SubPagesAll.Where(p => !p.WindowsOnly).ToList();

    public static SubPage? SubPageFor(string id) => SubPages.FirstOrDefault(p => p.Id == id);

    public static bool IsKnown(string id) =>
        Primary.Any(i => i.Id == id) || SubPages.Any(p => p.Id == id);

    public static string LabelFor(string id) =>
        Primary.FirstOrDefault(i => i.Id == id)?.Label ?? SubPageFor(id)?.Label ?? id;

    private static IReadOnlyList<NavItem> Filter(IReadOnlyList<NavItem> items) =>
        OperatingSystem.IsWindows() ? items : items.Where(i => !i.WindowsOnly).ToList();
}

/// <summary>A page that isn't in the rail; opened from inside <paramref name="Parent"/>.</summary>
public sealed record SubPage(string Id, string Label, string Parent, bool WindowsOnly = false);
