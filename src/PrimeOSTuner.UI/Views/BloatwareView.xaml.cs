using System;
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.Bloatware;
using PrimeOSTuner.UI.Dialogs;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class BloatwareView : UserControl
{
    private readonly BloatwareViewModel _vm;
    private readonly BloatwareDisableService _disableSvc;
    private readonly BloatwareUninstallService _uninstallSvc;

    public BloatwareView(
        BloatwareViewModel vm,
        BloatwareDisableService disableSvc,
        BloatwareUninstallService uninstallSvc)
    {
        InitializeComponent();
        _vm = vm;
        _disableSvc = disableSvc;
        _uninstallSvc = uninstallSvc;
        DataContext = vm;
        // Auto-scan when the tab is first shown.
        Loaded += async (_, _) => { if (_vm.Items.Count == 0) await _vm.RefreshAsync(); };
    }

    private async void RefreshClick(object sender, RoutedEventArgs e)
    {
        await _vm.RefreshAsync();
    }

    private async void DisableClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not BloatwareItemRowVm row) return;
        btn.IsEnabled = false;
        try
        {
            await _disableSvc.DisableAsync(row.Item);
            row.StatusText = "Disabled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Disable failed: {ex.Message}", row.DisplayName,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void UninstallClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not BloatwareItemRowVm row) return;

        var dlg = new BloatwareUninstallDialog { Owner = Window.GetWindow(this) };
        dlg.Configure(row.Item);
        dlg.ShowDialog();
        if (!dlg.Confirmed) return;

        btn.IsEnabled = false;
        try
        {
            await _uninstallSvc.UninstallAsync(row.Item);
            row.StatusText = "Uninstalled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Uninstall failed: {ex.Message}", row.DisplayName,
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }
}
