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

    [ObservableProperty] private bool _isActive;
}
