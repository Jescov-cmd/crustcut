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
        new NavItem("Overview",  "OVERVIEW",   ""),

        new NavItem("Optimize",  "OPTIMIZE",   Performance),
        new NavItem("Diagnosis", "DIAGNOSIS",  Performance),
        new NavItem("Cleanup",   "CLEANUP",    Performance),   // was "Bloatware"
        new NavItem("Memory",    "MEMORY",     Performance),

        new NavItem("Games",     "GAMES",      Gaming),        // was "Library"
        new NavItem("Sessions",  "SESSIONS",   Gaming),        // was "Sentinel"
        new NavItem("GameBoost", "GAME BOOST", Gaming),

        new NavItem("Guides",    "GUIDES",     Learn),
    };

    public static readonly IReadOnlyList<NavItem> Bottom = new[]
    {
        new NavItem("History",  "HISTORY",  ""),
        new NavItem("Settings", "SETTINGS", ""),
    };
}
