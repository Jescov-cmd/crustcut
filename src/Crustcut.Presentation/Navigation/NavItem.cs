namespace Crustcut.Presentation.Navigation;

/// <summary>One entry in the navigation rail.</summary>
/// <param name="Id">Stable key used for routing. Never shown to the user.</param>
/// <param name="Label">Display text.</param>
/// <param name="Group">Section heading, or empty for an ungrouped item.</param>
public sealed record NavItem(string Id, string Label, string Group);
