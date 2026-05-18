using System;
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.UI.Services;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class DashboardView : UserControl
{
    private readonly OneClickOptimizer _optimizer;
    private readonly TrayIconService _tray;
    private readonly SettingsViewModel _settings;

    public DashboardView(
        DashboardViewModel vm,
        OneClickOptimizer optimizer,
        TrayIconService tray,
        SettingsViewModel settings)
    {
        InitializeComponent();
        DataContext = vm;
        _optimizer = optimizer;
        _tray = tray;
        _settings = settings;
    }

    private void Notify(string title, string message)
    {
        if (_settings.NotificationsEnabled) _tray.ShowNotification(title, message);
    }

    private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
        OptimizeButton.IsEnabled = false;
        OptimizeButton.Content = "Working…";
        try
        {
            var report = await _optimizer.RunAsync();
            var msg = $"Optimization complete: {report.SuccessCount} succeeded, {report.FailureCount} failed.";
            OptimizeStatus.Text = msg;
            Notify("PrimeOS Tuner", msg);
            if (DataContext is DashboardViewModel vm) await vm.RefreshBoostScoreAsync();
        }
        catch (Exception ex)
        {
            OptimizeStatus.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            OptimizeButton.IsEnabled = true;
            OptimizeButton.Content = "⚡ OPTIMIZE NOW";
        }
    }
}
