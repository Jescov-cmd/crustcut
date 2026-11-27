using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PrimeOSTuner.Core.Fans;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.App;

/// <summary>
/// The tray icon's left-click popup: live temperature and RAM, the game-mode switch, a
/// memory cleanup and the way into the app. Built when it opens and thrown away when it
/// closes, exactly like the main window — nothing about it stays alive in the tray.
/// </summary>
public partial class TrayPopup : Window
{
    private const double MarginDip = 12;

    private readonly Composition? _composition;
    private readonly Action _open;
    private readonly Action _quit;
    private readonly bool _closeOnDeactivate;
    private readonly AppSettingsStore _settings = new(AppSettingsStore.DefaultPath());
    private SystemSampler? _sampler;
    private bool _loadingSettings;

    // XAML loader / previewer only.
    public TrayPopup() : this(null, () => { }, () => { }) { }

    public TrayPopup(Composition? composition, Action open, Action quit, bool closeOnDeactivate = true)
    {
        _composition = composition;
        _open = open;
        _quit = quit;
        _closeOnDeactivate = closeOnDeactivate;
        InitializeComponent();

        _loadingSettings = true;
        GameModeSwitch.IsChecked = _settings.Load().GameModeAuto;
        _loadingSettings = false;
        UpdateFans();

        Opacity = 0;   // shown once it's been placed, so it never flashes at the screen origin
        Opened += OnOpened;
        Closed += OnClosed;
        Deactivated += OnDeactivated;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Place();
        Opacity = 1;
        Activate();

        _sampler = _composition?.Sampler;
        if (_sampler is not null) _sampler.Sampled += OnSampled;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (_sampler is not null) _sampler.Sampled -= OnSampled;
        _sampler = null;
        Opened -= OnOpened;
        Closed -= OnClosed;
        Deactivated -= OnDeactivated;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_closeOnDeactivate) Close();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { e.Handled = true; Close(); return; }
        base.OnKeyDown(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        // The result line makes it taller: keep the bottom edge pinned above the taskbar.
        if (change.Property == ClientSizeProperty && IsVisible) Place();
    }

    // ── Live line ────────────────────────────────────────────────────────────────────

    private void OnSampled(object? sender, SystemSample s)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_sampler is null) return;   // closed while the post was queued
            var temp = _composition?.FanService?.Status().TempC;
            LiveText.Text = temp is double t
                ? $"{t:F0}° · {s.RamPercent:F0}% RAM"
                : $"{s.RamPercent:F0}% RAM";
            UpdateFans();
        });
    }

    private void UpdateFans()
    {
        var fans = _composition?.FanService;
        FansText.Text = fans is { IsSupported: true, Enabled: true }
            ? $"Fans: {(fans.Mode == FanMode.Auto ? "auto" : fans.Mode.ToString().ToLowerInvariant())}"
            : "Fans: off";
    }

    // ── Rows ─────────────────────────────────────────────────────────────────────────

    private void GameModeRowClick(object? sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        var on = GameModeSwitch.IsChecked != true;
        // Update reads fresh and writes atomically; null means the file couldn't be read
        // and nothing was written — leave the switch where the file says it is.
        if (_settings.Update(s => s.GameModeAuto = on) is not null)
            GameModeSwitch.IsChecked = on;
    }

    private async void FreeMemoryClick(object? sender, RoutedEventArgs e)
    {
        var memory = _composition?.Memory;
        if (memory is null) { ShowResult("Memory cleanup isn't available on this platform."); return; }
        FreeMemoryRow.IsEnabled = false;
        ShowResult("Freeing up memory…");
        try { ShowResult(await memory.DeepCleanAsync()); }
        catch (Exception ex) { ShowResult($"Cleanup failed: {ex.Message}"); }
        finally { FreeMemoryRow.IsEnabled = true; }
    }

    private void ShowResult(string text)
    {
        ResultText.Text = text;
        ResultText.IsVisible = true;
    }

    private void OpenClick(object? sender, RoutedEventArgs e)
    {
        Close();
        _open();
    }

    private void QuitClick(object? sender, RoutedEventArgs e)
    {
        Close();
        _quit();
    }

    // ── Placement ────────────────────────────────────────────────────────────────────

    /// <summary>Bottom-right of the working area of the screen under the cursor (where the
    /// tray icon was just clicked), 12 px in from each edge.</summary>
    private void Place()
    {
        var cursor = CursorPosition();
        var screen = (cursor is { } p ? Screens.ScreenFromPoint(p) : null) ?? Screens.Primary;
        if (screen is null) return;

        var scale = screen.Scaling;
        var area = screen.WorkingArea;
        var w = (int)Math.Ceiling(Bounds.Width * scale);
        var h = (int)Math.Ceiling(Bounds.Height * scale);
        var m = (int)Math.Round(MarginDip * scale);
        Position = new PixelPoint(area.Right - w - m, area.Bottom - h - m);
    }

    private static PixelPoint? CursorPosition()
    {
        if (!OperatingSystem.IsWindows()) return null;
        try { return GetCursorPos(out var pt) ? new PixelPoint(pt.X, pt.Y) : null; }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);
}
