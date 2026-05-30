using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Settings;

namespace PrimeOSTuner.UI.ViewModels;

/// <summary>
/// Drives the in-game performance overlay. Subscribes to the live <see cref="SystemSampler"/>
/// (the same stream Sentinel/the dashboard use) plus the live FPS counter, and exposes
/// pre-formatted, minimalistic metric strings + per-metric visibility.
/// </summary>
public partial class OverlayViewModel : ObservableObject, IDisposable
{
    private readonly SystemSampler _sampler;
    private readonly FrameRecordingService _frames;

    [ObservableProperty] private string _fpsText = "FPS --";
    [ObservableProperty] private string _cpuText = "CPU --%";
    [ObservableProperty] private string _gpuText = "GPU --%";
    [ObservableProperty] private string _ramText = "RAM --";
    [ObservableProperty] private string _vramText = "VRAM --";
    [ObservableProperty] private string _netText = "NET --";

    [ObservableProperty] private bool _showFps = true;
    [ObservableProperty] private bool _showCpu = true;
    [ObservableProperty] private bool _showGpu = true;
    [ObservableProperty] private bool _showRam = true;
    [ObservableProperty] private bool _showVram = true;
    [ObservableProperty] private bool _showNet;
    [ObservableProperty] private bool _hasVram = true;

    [ObservableProperty] private double _fontSize = 16;
    [ObservableProperty] private bool _editMode;

    public OverlayViewModel(SystemSampler sampler, FrameRecordingService frames)
    {
        _sampler = sampler;
        _frames = frames;
        _sampler.Sampled += OnSampled;
        _frames.FpsChanged += OnFpsChanged;
    }

    public void ApplySettings(AppSettings s)
    {
        ShowFps = s.OverlayShowFps;
        ShowCpu = s.OverlayShowCpu;
        ShowGpu = s.OverlayShowGpu;
        ShowRam = s.OverlayShowRam;
        ShowVram = s.OverlayShowVram;
        ShowNet = s.OverlayShowNet;
        FontSize = 16 * Math.Clamp(s.OverlayScale, 0.7, 2.0);
    }

    private void OnFpsChanged(object? sender, EventArgs e)
    {
        var fps = _frames.CurrentFps;
        var text = fps >= 1 ? $"FPS {fps:F0}" : "FPS --";
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) FpsText = text;
        else dispatcher.BeginInvoke(() => FpsText = text);
    }

    private void OnSampled(object? sender, SystemSample s)
    {
        var dispatcher = Application.Current?.Dispatcher;
        var f = Format(s);
        Action update = () =>
        {
            CpuText = f.Cpu;
            GpuText = f.Gpu;
            RamText = f.Ram;
            HasVram = f.HasVram;
            VramText = f.Vram;
            NetText = f.Net;
        };
        if (dispatcher is null || dispatcher.CheckAccess()) update();
        else dispatcher.BeginInvoke(update);
    }

    public readonly record struct Formatted(string Cpu, string Gpu, string Ram, string Vram, string Net, bool HasVram);

    /// <summary>Pure, testable formatting of a live sample into the OSD rows.</summary>
    public static Formatted Format(SystemSample s)
    {
        var hasVram = s.VramTotalBytes > 0;
        return new Formatted(
            Cpu: $"CPU {Clamp(s.CpuPercent):F0}%",
            Gpu: $"GPU {Clamp(s.GpuPercent):F0}%",
            Ram: $"RAM {Gb(s.RamUsedBytes):F1}/{Gb(s.RamTotalBytes):F0} GB",
            Vram: hasVram ? $"VRAM {Gb(s.VramUsedBytes):F1}/{Gb(s.VramTotalBytes):F0} GB" : "VRAM n/a",
            Net: $"NET ↓{Mbps(s.NetworkDownBps):F1} ↑{Mbps(s.NetworkUpBps):F1} Mb/s",
            HasVram: hasVram);
    }

    private static double Clamp(double v) => v < 0 ? 0 : (v > 100 ? 100 : v);
    private static double Gb(long bytes) => bytes / 1024.0 / 1024.0 / 1024.0;
    private static double Mbps(long bytesPerSec) => bytesPerSec * 8.0 / 1_000_000.0;

    public void Dispose()
    {
        _sampler.Sampled -= OnSampled;
        _frames.FpsChanged -= OnFpsChanged;
    }
}
