using CommunityToolkit.Mvvm.ComponentModel;

namespace PrimeOSTuner.UI.ViewModels;

public partial class SentinelRowViewModel : ObservableObject
{
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _current = "—";
    [ObservableProperty] private string _recommended = "—";
    [ObservableProperty] private bool _isProblem;
    [ObservableProperty] private string _explanation = "";
}
