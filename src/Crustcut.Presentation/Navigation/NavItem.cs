namespace Crustcut.Presentation.Navigation;

/// <summary>One entry in the navigation rail.</summary>
/// <param name="Id">Stable key used for routing. Never shown to the user.</param>
/// <param name="Label">Display text.</param>
/// <param name="Group">Section heading, or empty for an ungrouped item.</param>
/// <param name="WindowsOnly">True when the tab's feature surface exists only on Windows.</param>
public sealed record NavItem(string Id, string Label, string Group, bool WindowsOnly = false);
