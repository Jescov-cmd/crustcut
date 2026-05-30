using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Monitoring;
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Core.Settings;
using PrimeOSTuner.UI.ViewModels;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.ViewModels;

public class OverlayViewModelTests
{
    private static SystemSample Sample(
        double cpu = 0, double gpu = 0, long ramUsed = 0, long ramTotal = 0,
        long vramUsed = 0, long vramTotal = 0, long down = 0, long up = 0) =>
        new(DateTime.UtcNow, cpu, 0, ramUsed, ramTotal, gpu, 0, down, up, vramUsed, vramTotal);

    private const long GB = 1024L * 1024 * 1024;

    [Fact]
    public void Format_produces_clean_rows()
    {
        var f = OverlayViewModel.Format(Sample(
            cpu: 42.6, gpu: 78.2, ramUsed: 12L * GB + GB / 2, ramTotal: 32L * GB,
            vramUsed: 6L * GB, vramTotal: 8L * GB));

        f.Cpu.Should().Be("CPU 43%");
        f.Gpu.Should().Be("GPU 78%");
        f.Ram.Should().Be("RAM 12.5/32 GB");
        f.Vram.Should().Be("VRAM 6.0/8 GB");
        f.HasVram.Should().BeTrue();
    }

    [Fact]
    public void Format_shows_vram_na_when_counter_unavailable()
    {
        var f = OverlayViewModel.Format(Sample(vramTotal: 0));
        f.HasVram.Should().BeFalse();
        f.Vram.Should().Be("VRAM n/a");
    }

    [Fact]
    public void Format_clamps_out_of_range_percentages()
    {
        OverlayViewModel.Format(Sample(cpu: -1)).Cpu.Should().Be("CPU 0%");
        OverlayViewModel.Format(Sample(gpu: 150)).Gpu.Should().Be("GPU 100%");
    }

    [Fact]
    public void ApplySettings_drives_visibility_and_scale()
    {
        var frames = new FrameRecordingService(Mock.Of<IPresentMonRunner>(), Mock.Of<IFrameSessionStore>(), "");
        var vm = new OverlayViewModel(new SystemSampler(Mock.Of<IHardwareClient>()), frames);
        var s = new AppSettings
        {
            OverlayShowCpu = true, OverlayShowGpu = false, OverlayShowRam = true,
            OverlayShowVram = false, OverlayShowNet = true, OverlayScale = 1.5
        };

        vm.ApplySettings(s);

        vm.ShowCpu.Should().BeTrue();
        vm.ShowGpu.Should().BeFalse();
        vm.ShowVram.Should().BeFalse();
        vm.ShowNet.Should().BeTrue();
        vm.FontSize.Should().BeApproximately(16 * 1.5, 0.01);
    }
}
