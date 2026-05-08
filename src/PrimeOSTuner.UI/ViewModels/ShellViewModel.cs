using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrimeOSTuner.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private static readonly string[] TabOrder =
    {
        "Dashboard", "Optimize", "GameBoost", "GameLibrary", "CustomMode", "History"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTabIndex))]
    private string _activeTab = "Dashboard";

    public int SelectedTabIndex
    {
        get
        {
            var idx = Array.IndexOf(TabOrder, ActiveTab);
            return idx < 0 ? 0 : idx;
        }
    }

    [RelayCommand]
    private void Navigate(string tab) => ActiveTab = tab;
}
