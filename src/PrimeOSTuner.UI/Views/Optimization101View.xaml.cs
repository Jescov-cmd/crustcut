using System.Windows.Controls;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class Optimization101View : UserControl
{
    public Optimization101View(Optimization101ViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
