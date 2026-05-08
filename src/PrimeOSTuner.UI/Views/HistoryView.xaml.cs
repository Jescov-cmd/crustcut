using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class HistoryView : UserControl
{
    public HistoryView(HistoryViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
