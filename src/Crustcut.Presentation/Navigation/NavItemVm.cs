using CommunityToolkit.Mvvm.ComponentModel;

namespace Crustcut.Presentation.Navigation;

/// <summary>
/// A nav entry plus its selected state. <see cref="NavItem"/> stays a plain immutable
/// record; selection is view state, so it lives here.
/// </summary>
public partial class NavItemVm : ObservableObject
{
    public NavItemVm(NavItem model) => Model = model;

    public NavItem Model { get; }

    public string Id => Model.Id;
    public string Label => Model.Label;
    public string Group => Model.Group;

    /// <summary>
    /// True only for the first item of a group, so the section heading is drawn once above
    /// the group rather than repeated above every row.
    /// </summary>
    public bool ShowGroupHeader { get; init; }

    [ObservableProperty] private bool _isActive;
}
