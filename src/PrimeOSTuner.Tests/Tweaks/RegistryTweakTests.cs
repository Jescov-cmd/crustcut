using FluentAssertions;
using Microsoft.Win32;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class RegistryTweakTests
{
    private static RegistryTweakDefinition DwordDef(string applied = "1") => new(
        Id: "test.dword-tweak",
        DisplayName: "Test DWORD",
        Description: "x",
        Category: "system",
        RequiresElevation: true,
        RequiresReboot: false,
        Hive: "LocalMachine",
        Key: @"SOFTWARE\Test",
        ValueName: "Foo",
        ValueKind: "DWord",
        AppliedData: applied,
        RiskNote: null
    );

    private static RegistryTweakDefinition StringDef(string applied = "0") => new(
        Id: "test.string-tweak",
        DisplayName: "Test STRING",
        Description: "x",
        Category: "system",
        RequiresElevation: true,
        RequiresReboot: false,
        Hive: "CurrentUser",
        Key: @"Control Panel\Test",
        ValueName: "Bar",
        ValueKind: "String",
        AppliedData: applied,
        RiskNote: null
    );

    [Fact]
    public async Task ProbeAsync_returns_Applied_when_dword_value_matches()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo"))
                .Returns(1);
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ProbeAsync_returns_NotApplied_when_dword_value_differs()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo"))
                .Returns(0);
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.NotApplied);
    }

    [Fact]
    public async Task ProbeAsync_returns_Applied_when_string_value_matches()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.ReadString(RegistryHive.CurrentUser, @"Control Panel\Test", "Bar"))
                .Returns("0");
        var tweak = new RegistryTweak(StringDef(), registry.Object);
        (await tweak.ProbeAsync()).Should().Be(TweakState.Applied);
    }

    [Fact]
    public async Task ApplyAsync_writes_dword_and_returns_serializable_undo()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo", 1))
                .Returns(new RegistryBackup(
                    RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo",
                    PreviousString: null, PreviousDword: 0, PreviousKind: RegistryValueKind.DWord));
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().NotBeNull().And.Contain("Foo");
    }

    [Fact]
    public async Task ApplyAsync_writes_string_and_returns_serializable_undo()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteString(RegistryHive.CurrentUser, @"Control Panel\Test", "Bar", "0"))
                .Returns(new RegistryBackup(
                    RegistryHive.CurrentUser, @"Control Panel\Test", "Bar", "1"));
        var tweak = new RegistryTweak(StringDef(), registry.Object);
        var result = await tweak.ApplyAsync();
        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().NotBeNull().And.Contain("Bar");
    }

    [Fact]
    public async Task RevertAsync_restores_backup()
    {
        var registry = new Mock<IRegistryClient>();
        registry.Setup(r => r.WriteDword(RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo", 1))
                .Returns(new RegistryBackup(
                    RegistryHive.LocalMachine, @"SOFTWARE\Test", "Foo",
                    PreviousString: null, PreviousDword: 0, PreviousKind: RegistryValueKind.DWord));
        var tweak = new RegistryTweak(DwordDef(), registry.Object);
        var apply = await tweak.ApplyAsync();
        var revert = await tweak.RevertAsync(apply.UndoData!);
        revert.Succeeded.Should().BeTrue();
        registry.Verify(r => r.RestoreFromBackup(It.IsAny<RegistryBackup>()), Times.Once);
    }

    [Fact]
    public void Identity_fields_come_from_definition()
    {
        var tweak = new RegistryTweak(DwordDef(), new Mock<IRegistryClient>().Object);
        tweak.Id.Should().Be("test.dword-tweak");
        tweak.DisplayName.Should().Be("Test DWORD");
        tweak.RequiresElevation.Should().BeTrue();
        tweak.IsDestructive.Should().BeFalse();
    }
}
