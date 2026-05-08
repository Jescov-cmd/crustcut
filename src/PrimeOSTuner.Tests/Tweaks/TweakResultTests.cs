using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class TweakResultTests
{
    [Fact]
    public void Success_factory_returns_succeeded_result_with_undo_data()
    {
        var result = TweakResult.Success("undo-payload");

        result.Succeeded.Should().BeTrue();
        result.UndoData.Should().Be("undo-payload");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Failure_factory_returns_failed_result_with_error_message()
    {
        var result = TweakResult.Failure("boom");

        result.Succeeded.Should().BeFalse();
        result.Error.Should().Be("boom");
        result.UndoData.Should().BeNull();
    }
}
