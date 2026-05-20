using FluentAssertions;
using PrimeOSTuner.Win.Suspension;
using Xunit;

namespace PrimeOSTuner.Tests.Win;

public class BackgroundSuspendListTests
{
    [Fact]
    public void Default_includes_common_cloud_sync_and_media_apps()
    {
        BackgroundSuspendList.Default.Should().Contain(new[]
        {
            "OneDrive", "Dropbox", "Spotify",
        });
    }

    [Fact]
    public void Default_excludes_Steam_because_it_must_be_running_to_launch_games()
    {
        BackgroundSuspendList.Default.Should().NotContain(n =>
            string.Equals(n, "Steam", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Default_excludes_browsers_because_their_child_process_trees_are_too_messy()
    {
        BackgroundSuspendList.Default.Should().NotContain(n =>
            string.Equals(n, "chrome", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "msedge", StringComparison.OrdinalIgnoreCase)
            || string.Equals(n, "firefox", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Default_entries_have_no_exe_extension()
    {
        BackgroundSuspendList.Default.Should().AllSatisfy(n =>
            n.Should().NotEndWith(".exe"));
    }

    [Fact]
    public void Default_entries_are_unique()
    {
        BackgroundSuspendList.Default.Should().OnlyHaveUniqueItems();
    }
}
