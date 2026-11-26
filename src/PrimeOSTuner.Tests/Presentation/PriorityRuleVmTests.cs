using Crustcut.Presentation;
using FluentAssertions;
using PrimeOSTuner.Core.Memory;
using Xunit;

namespace PrimeOSTuner.Tests.Presentation;

public class PriorityRuleVmTests
{
    private static PriorityRuleVm Rule(int? limitMb) => new(new PriorityRule(
        @"C:\Apps\discord.exe", "Discord", PriorityLevel.Normal,
        ProtectFromRamCleanup: false, GameBooster: false, IsGame: false, MemoryLimitMb: limitMb));

    /// <summary>
    /// A re-binding ComboBox (tab re-shown, list row reused while scrolling) pushes a
    /// transient null through its TwoWay binding. Null used to be read as "No limit", so
    /// the next save of ANY row persisted a silently erased cap.
    /// </summary>
    [Fact]
    public void Transient_null_selection_never_erases_a_memory_limit()
    {
        var vm = Rule(512);

        vm.MemoryLimit = null;

        vm.MemoryLimit!.Mb.Should().Be(512);
        vm.ToRule().MemoryLimitMb.Should().Be(512);
    }

    [Fact]
    public void Choosing_no_limit_explicitly_still_clears_the_cap()
    {
        var vm = Rule(512);

        vm.MemoryLimit = PriorityRuleVm.MemoryLimitOptions.First(o => o.Mb is null);

        vm.ToRule().MemoryLimitMb.Should().BeNull();
    }
}
