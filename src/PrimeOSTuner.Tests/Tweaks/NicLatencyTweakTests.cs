using FluentAssertions;
using PrimeOSTuner.Core.Tweaks;
using Xunit;

namespace PrimeOSTuner.Tests.Tweaks;

public class NicLatencyTweakTests
{
    private static NicProperty Numeric(string keyword, string value, int min, int max) =>
        new("Ethernet 3", keyword, value, Array.Empty<string>(), min, max);

    private static NicProperty Enum(string keyword, string value, params string[] valid) =>
        new("Ethernet 3", keyword, value, valid, null, null);

    /// <summary>The user's Realtek on 2026-09-25, exactly as Windows reported it after a
    /// driver update reset the buffers: 12% average DPC time on core 0 at idle.</summary>
    [Fact]
    public void Reset_realtek_gets_its_buffers_raised_to_the_measured_fix()
    {
        var changes = NicLatencyTweak.Plan(new[]
        {
            Enum("*FlowControl", "0", "0", "3"),
            Enum("*InterruptModeration", "0", "0", "1"),
            Numeric("*ReceiveBuffers", "32", 32, 512),
            Numeric("*TransmitBuffers", "32", 32, 128),
        });

        changes.Should().BeEquivalentTo(new[]
        {
            new NicChange("Ethernet 3", "*ReceiveBuffers", "32", "512"),
            new NicChange("Ethernet 3", "*TransmitBuffers", "32", "128"),
        });
    }

    [Fact]
    public void Stock_card_also_gets_moderation_and_flow_control_off()
    {
        var changes = NicLatencyTweak.Plan(new[]
        {
            Enum("*FlowControl", "3", "0", "3"),
            Enum("*InterruptModeration", "1", "0", "1"),
        });

        changes.Select(c => (c.Keyword, c.To)).Should().BeEquivalentTo(new[]
        {
            ("*FlowControl", "0"), ("*InterruptModeration", "0"),
        });
    }

    [Fact]
    public void Already_tuned_card_needs_nothing()
    {
        NicLatencyTweak.Plan(new[]
        {
            Enum("*FlowControl", "0", "0", "3"),
            Enum("*InterruptModeration", "0", "0", "1"),
            Numeric("*ReceiveBuffers", "512", 32, 512),
            Numeric("*TransmitBuffers", "128", 32, 128),
        }).Should().BeEmpty();
    }

    [Fact]
    public void Never_lowers_a_bigger_buffer_or_exceeds_what_the_driver_allows()
    {
        NicLatencyTweak.Plan(new[] { Numeric("*ReceiveBuffers", "2048", 80, 4096) }).Should().BeEmpty();

        NicLatencyTweak.Plan(new[] { Numeric("*ReceiveBuffers", "64", 32, 256) })
            .Single().To.Should().Be("256");   // capped at the card's maximum
    }

    [Fact]
    public void Enumerated_buffers_pick_the_largest_allowed_step()
    {
        NicLatencyTweak.Plan(new[] { Enum("*ReceiveBuffers", "64", "64", "128", "256", "1024") })
            .Single().To.Should().Be("256");
    }

    [Fact]
    public void Leaves_a_setting_alone_when_disabled_is_not_an_allowed_value()
    {
        NicLatencyTweak.Plan(new[] { Enum("*InterruptModeration", "1", "1", "2") }).Should().BeEmpty();
    }

    [Fact]
    public void Is_opt_in_and_never_part_of_a_bundle()
    {
        var t = new NicLatencyTweak();
        t.OptIn.Should().BeTrue();
        t.RequiresElevation.Should().BeTrue();
    }
}
