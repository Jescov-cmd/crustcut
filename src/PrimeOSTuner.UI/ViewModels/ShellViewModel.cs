using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PrimeOSTuner.UI.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty] private string _activeTab = "Dashboard";

    [RelayCommand]
    private void Navigate(string tab) => ActiveTab = tab;
}
