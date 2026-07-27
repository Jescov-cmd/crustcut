using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Crustcut.Presentation;

namespace Crustcut.App.Services;

/// <summary>
/// Avalonia has no built-in message box, so this builds a small themed modal. Kept behind
/// <see cref="IDialogService"/> so view-models stay testable.
/// </summary>
public sealed class AvaloniaDialogService : IDialogService
{
    public Task ShowAsync(string title, string message, DialogKind kind = DialogKind.Info)
        => Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = (Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            var accent = kind switch
            {
                DialogKind.Error => Color.Parse("#FF6B6B"),
                DialogKind.Warning => Color.Parse("#FFB84D"),
                _ => Color.Parse("#E8C088"),
            };

            var ok = new Button
            {
                Content = "OK",
                Height = 32,
                Padding = new Avalonia.Thickness(22, 0),
                CornerRadius = new Avalonia.CornerRadius(16),
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = new SolidColorBrush(accent),
                Foreground = new SolidColorBrush(Color.Parse("#26190A")),
                FontWeight = FontWeight.SemiBold,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };

            var dialog = new Window
            {
                Title = title,
                Width = 460,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = owner is null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#141312")),
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(24),
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 15,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(accent),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 12.5,
                            Foreground = new SolidColorBrush(Color.Parse("#C9C3BA")),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        ok,
                    }
                }
            };

            ok.Click += (_, _) => dialog.Close();

            if (owner is not null) await dialog.ShowDialog(owner);
            else dialog.Show();
        });

    public Task<bool> ConfirmAsync(string title, string message, string confirmLabel)
        => Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var owner = (Avalonia.Application.Current?.ApplicationLifetime
                as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

            var confirmed = false;

            var cancel = new Button
            {
                Content = "Cancel",
                Height = 32,
                Padding = new Avalonia.Thickness(18, 0),
                CornerRadius = new Avalonia.CornerRadius(16),
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.Parse("#22FFFFFF")),
                BorderThickness = new Avalonia.Thickness(1),
                Foreground = new SolidColorBrush(Color.Parse("#9A948C")),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };

            var confirm = new Button
            {
                Content = confirmLabel,
                Height = 32,
                Padding = new Avalonia.Thickness(20, 0),
                CornerRadius = new Avalonia.CornerRadius(16),
                Background = new SolidColorBrush(Color.Parse("#FF6B6B")),
                Foreground = new SolidColorBrush(Color.Parse("#2A0F0F")),
                FontWeight = FontWeight.SemiBold,
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };

            var dialog = new Window
            {
                Title = title,
                Width = 460,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = owner is null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Background = new SolidColorBrush(Color.Parse("#141312")),
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(24),
                    Spacing = 14,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 15,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(Color.Parse("#F9F0E1")),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new TextBlock
                        {
                            Text = message,
                            FontSize = 12.5,
                            Foreground = new SolidColorBrush(Color.Parse("#C9C3BA")),
                            TextWrapping = TextWrapping.Wrap,
                        },
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Spacing = 9,
                            Children = { cancel, confirm },
                        }
                    }
                }
            };

            // Default is "no" — closing the window any other way must not count as consent.
            cancel.Click += (_, _) => dialog.Close();
            confirm.Click += (_, _) => { confirmed = true; dialog.Close(); };

            if (owner is not null) await dialog.ShowDialog(owner);
            else dialog.Show();

            return confirmed;
        });
}
