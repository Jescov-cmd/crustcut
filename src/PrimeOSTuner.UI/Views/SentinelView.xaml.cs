using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class SentinelView : UserControl
{
    private readonly SentinelViewModel _vm;

    public SentinelView(SentinelViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        Loaded += (_, _) => _vm.AcknowledgeDot();
    }
}
