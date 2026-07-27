namespace Crustcut.Presentation.Navigation;

/// <summary>
/// The repositioned information architecture. Gaming is one section of a performance tool
/// rather than the frame of the whole product.
/// </summary>
public static class NavCatalog
{
    public const string Performance = "PERFORMANCE";
    public const string Gaming      = "GAMING";
    public const string Learn       = "LEARN";

    public static readonly IReadOnlyList<NavItem> Primary = new[]
    {
        new NavItem("Overview",  "Overview",   ""),

        new NavItem("Optimize",  "Optimize",   Performance),
        new NavItem("Diagnosis", "Diagnosis",  Performance),
        new NavItem("Cleanup",   "Cleanup",    Performance),   // was "Bloatware"
        new NavItem("Memory",    "Memory",     Performance),

        new NavItem("Games",     "Games",      Gaming),        // was "Library"
        new NavItem("Sessions",  "Sessions",   Gaming),        // was "Sentinel"
        new NavItem("GameBoost", "Game Boost", Gaming),

        new NavItem("Guides",    "Guides",     Learn),
    };

    public static readonly IReadOnlyList<NavItem> Bottom = new[]
    {
        new NavItem("History",  "History",  ""),
        new NavItem("Settings", "Settings", ""),
    };
}
