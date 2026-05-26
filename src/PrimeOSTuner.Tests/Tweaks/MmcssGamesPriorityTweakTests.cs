using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class MmcssGamesPriorityTweakTests
{
    private const string SubKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";

    [Fact]
    public async Task Apply_writes_priority_scheduling_category_and_sfio_priority()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, SubKey, "Priority", 6))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "Priority", null, 2, RegistryValueKind.DWord));
        registry.Setup(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "Scheduling Category", "High"))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "Scheduling Category", "Medium"));
        registry.Setup(r => r.WriteString(RegistryHive.LocalMachine, SubKey, "SFIO Priority", "High"))
                .Returns(new RegistryBackup(RegistryHive.LocalMachine, SubKey, "SFIO Priority", "Normal"));

        var tweak = new MmcssGamesPriorityTweak(registry.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Contain("Priority");
        result.UndoData.Should().Contain("Scheduling Category");
        result.UndoData.Should().Contain("SFIO Priority");
    }

    [Fact]
    public async Task Probe_returns_Applied_when_all_three_values_match_targets()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, SubKey, "Priority")).Returns(6);
        registry.Setup(r => r.ReadString(RegistryHive.LocalMachine, SubKey, "Scheduling Category")).Returns("High");
        registry.Setup(r => r.ReadString(RegistryHive.LocalMachine, SubKey, "SFIO Priority")).Returns("High");

        var tweak = new MmcssGamesPriorityTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task Probe_returns_NotApplied_when_priority_is_default()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, SubKey, "Priority")).Returns(2);
        registry.Setup(r => r.ReadString(RegistryHive.LocalMachine, SubKey, "Scheduling Category")).Returns("High");
        registry.Setup(r => r.ReadString(RegistryHive.LocalMachine, SubKey, "SFIO Priority")).Returns("High");

        var tweak = new MmcssGamesPriorityTweak(registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task RequiresElevation_is_true_because_the_key_lives_under_HKLM()
    {
        var registry = new Mock<IRegistryClient>();
        var tweak = new MmcssGamesPriorityTweak(registry.Object);
        await Task.CompletedTask;
        tweak.RequiresElevation.Should().BeTrue();
    }
}
