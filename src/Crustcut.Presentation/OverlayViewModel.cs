using CommunityToolkit.Mvvm.ComponentModel;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Settings;

namespace Crustcut.Presentation;

/// <summary>
/// Drives the in-game performance overlay. Subscribes to the live <see cref="SystemSampler"/>
/// (the same stream Sentinel/the dashboard use) plus the live FPS counter, and exposes
/// pre-formatted, minimalistic metric strings + per-metric visibility.
/// </summary>
public partial class OverlayViewModel : ObservableObject, IDisposable
{
    private readonly SystemSampler _sampler;
    private readonly FrameRecordingService _frames;

    // Values only — the row LABELS live in the view, the same way every other panel in
    // the app pairs a static caption with a monospace value.
    [ObservableProperty] private string _fpsText = "--";
    [ObservableProperty] private string _cpuText = "--%";
    [ObservableProperty] private string _gpuText = "--%";
    [ObservableProperty] private string _ramText = "--";
    [ObservableProperty] private string _vramText = "--";
    [ObservableProperty] private string _netText = "--";

    [ObservableProperty] private bool _showFps = true;
    [ObservableProperty] private bool _showCpu = true;
    [ObservableProperty] private bool _showGpu = true;
    [ObservableProperty] private bool _showRam = true;
    [ObservableProperty] private bool _showVram = true;
    [ObservableProperty] private bool _showNet;
    [ObservableProperty] private bool _hasVram = true;

    [ObservableProperty] private double _fontSize = 16;
    [ObservableProperty] private bool _editMode;

    /// <summary>Captions sit a little under the values, and FPS a little over them, so the
    /// readout has the same hierarchy as the app's panels at every overlay scale.</summary>
    public double LabelFontSize => Math.Round(FontSize * 0.72, 1);
    public double FpsFontSize => Math.Round(FontSize * 1.25, 1);

    partial void OnFontSizeChanged(double value)
    {
        OnPropertyChanged(nameof(LabelFontSize));
        OnPropertyChanged(nameof(FpsFontSize));
    }

    private readonly IUiDispatcher _ui;

    public OverlayViewModel(SystemSampler sampler, FrameRecordingService frames, IUiDispatcher ui)
    {
        _sampler = sampler;
        _frames = frames;
        _ui = ui;
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
        var text = fps >= 1 ? $"{fps:F0}" : "--";
        _ui.Post(() => FpsText = text);
    }

    private void OnSampled(object? sender, SystemSample s)
    {
        var f = Format(s);
        _ui.Post(() =>
        {
            CpuText = f.Cpu;
            GpuText = f.Gpu;
            RamText = f.Ram;
            HasVram = f.HasVram;
            VramText = f.Vram;
            NetText = f.Net;
        });
    }

    public readonly record struct Formatted(string Cpu, string Gpu, string Ram, string Vram, string Net, bool HasVram);

    /// <summary>
    /// Pure, testable formatting of a live sample into OSD values. Labels are the view's
    /// job; these are the numbers only, kept short so the readout stays narrow over a game.
    /// </summary>
    public static Formatted Format(SystemSample s)
    {
        var hasVram = s.VramTotalBytes > 0;
        return new Formatted(
            Cpu: $"{Clamp(s.CpuPercent):F0}%",
            Gpu: $"{Clamp(s.GpuPercent):F0}%",
            Ram: $"{Gb(s.RamUsedBytes):F1}/{Gb(s.RamTotalBytes):F0} GB",
            Vram: hasVram ? $"{Gb(s.VramUsedBytes):F1}/{Gb(s.VramTotalBytes):F0} GB" : "n/a",
            Net: $"↓{Mbps(s.NetworkDownBps):F1} ↑{Mbps(s.NetworkUpBps):F1}",
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
