using System;
using System.Windows;
using System.Windows.Controls;
using PrimeOSTuner.Core.Pipeline;
using PrimeOSTuner.UI.ViewModels;

namespace PrimeOSTuner.UI.Views;

public partial class DashboardView : UserControl
{
    private readonly OneClickOptimizer _optimizer;

    public DashboardView(DashboardViewModel vm, OneClickOptimizer optimizer)
    {
        InitializeComponent();
        DataContext = vm;
        _optimizer = optimizer;
    }

    private async void OptimizeButton_Click(object sender, RoutedEventArgs e)
    {
        OptimizeButton.IsEnabled = false;
        OptimizeButton.Content = "Working…";
        try
        {
            var report = await _optimizer.RunAsync();
            ActivityPlaceholder.Text =
                $"Optimization complete: {report.SuccessCount} succeeded, {report.FailureCount} failed.";
        }
        catch (Exception ex)
        {
            ActivityPlaceholder.Text = $"Failed: {ex.Message}";
        }
        finally
        {
            OptimizeButton.IsEnabled = true;
            OptimizeButton.Content = "⚡ OPTIMIZE NOW";
        }
    }
}
