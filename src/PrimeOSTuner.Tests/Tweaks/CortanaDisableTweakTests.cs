using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class CortanaDisableTweakTests
{
    [Fact]
    public async Task ApplyAsync_writes_three_cortana_policy_keys()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var tweak = new CortanaDisableTweak(reg.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "AllowCortana", 0), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "DisableWebSearch", 1), Times.Once);
        reg.Verify(r => r.WriteDword(RegistryHive.LocalMachine,
            "SOFTWARE\\Policies\\Microsoft\\Windows\\Windows Search", "ConnectedSearchUseWeb", 0), Times.Once);
    }

    [Fact]
    public async Task RevertAsync_restores_all_three_backups()
    {
        var reg = new Mock<IRegistryClient>();
        reg.Setup(r => r.WriteDword(It.IsAny<RegistryHive>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
           .Returns((RegistryHive h, string k, string v, int d) =>
               new RegistryBackup(h, k, v, null, 1, RegistryValueKind.DWord));

        var tweak = new CortanaDisableTweak(reg.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);

        revert.Succeeded.Should().BeTrue();
        reg.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Exactly(3));
    }
}
