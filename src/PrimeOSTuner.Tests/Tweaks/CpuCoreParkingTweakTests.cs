using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Tweaks;
using PrimeOSTuner.Win;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class CpuCoreParkingTweakTests
{
    [Fact]
    public async Task Apply_calls_setacvalueindex_with_SUB_PROCESSOR_CPMINCORES_100()
    {
        var client = new Mock<IPowerPlanClient>();
        client.Setup(c => c.GetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES")).Returns(0);

        var tweak = new CpuCoreParkingTweak(client.Object);
        var result = await tweak.ApplyAsync();

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES", 100), Times.Once);
        result.UndoData.Should().Contain("0");
    }

    [Fact]
    public async Task Revert_restores_previous_index()
    {
        var client = new Mock<IPowerPlanClient>();
        var tweak = new CpuCoreParkingTweak(client.Object);

        var result = await tweak.RevertAsync("25");

        result.Succeeded.Should().BeTrue();
        client.Verify(c => c.SetActiveAcValueIndex("SUB_PROCESSOR", "CPMINCORES", 25), Times.Once);
    }
}
