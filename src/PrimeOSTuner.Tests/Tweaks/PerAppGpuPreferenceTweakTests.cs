using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class PerAppGpuPreferenceTweakTests
{
    private const string SubKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

    [Fact]
    public async Task Apply_writes_GpuPreference_2_for_each_exe_path()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, SubKey, It.IsAny<string>(), "GpuPreference=2;"))
                .Returns((RegistryHive h, string s, string n, string v) => new RegistryBackup(h, s, n, null));

        var paths = new[]
        {
            @"C:\Games\Valorant\VALORANT-Win64-Shipping.exe",
            @"C:\Riot Games\League of Legends\League of Legends.exe"
        };
        var tweak = new PerAppGpuPreferenceTweak(registry.Object, paths);

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, paths[0], "GpuPreference=2;"), Times.Once);
        registry.Verify(r => r.WriteString(RegistryHive.CurrentUser, SubKey, paths[1], "GpuPreference=2;"), Times.Once);
    }

    [Fact]
    public async Task Apply_with_empty_path_list_succeeds_with_empty_undo()
    {
        var registry = new Mock<IRegistryClient>();
        var tweak = new PerAppGpuPreferenceTweak(registry.Object, Array.Empty<string>());

        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("[]");
    }
}
