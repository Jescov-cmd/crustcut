using FluentAssertions;
using PrimeOSTuner.Core.Windows.Platform;
using Xunit;

namespace PrimeOSTuner.Tests.Platform;

/// <summary>
/// The StartupApproved mark encoding, which must match Task Manager's exactly: first
/// byte even = enabled, odd = disabled; a missing mark means enabled. Getting this wrong
/// silently flips the meaning of every toggle on the Cleanup page.
/// </summary>
public class StartupAppsClientTests
{
    [Fact]
    public void Missing_or_empty_mark_means_enabled()
    {
        StartupAppsClient.IsEnabledMark(null).Should().BeTrue();
        StartupAppsClient.IsEnabledMark(Array.Empty<byte>()).Should().BeTrue();
    }

    [Fact]
    public void Even_first_byte_is_enabled_odd_is_disabled()
    {
        StartupAppsClient.IsEnabledMark(new byte[] { 0x02, 0, 0, 0 }).Should().BeTrue();
        StartupAppsClient.IsEnabledMark(new byte[] { 0x06, 0, 0, 0 }).Should().BeTrue();
        StartupAppsClient.IsEnabledMark(new byte[] { 0x03, 0, 0, 0 }).Should().BeFalse();
        // Real disabled marks carry a FILETIME after the flag; only byte 0 matters.
        var realDisabled = new byte[] { 0x03, 0, 0, 0, 0x50, 0x1A, 0x2B, 0x3C, 0x4D, 0x5E, 0x6F, 0x70 };
        StartupAppsClient.IsEnabledMark(realDisabled).Should().BeFalse();
    }

    [Fact]
    public void Marks_round_trip_through_their_own_reader()
    {
        StartupAppsClient.IsEnabledMark(StartupAppsClient.MakeMark(true)).Should().BeTrue();
        StartupAppsClient.IsEnabledMark(StartupAppsClient.MakeMark(false)).Should().BeFalse();
        StartupAppsClient.MakeMark(true).Length.Should().Be(12);   // Windows' expected size
    }
}
