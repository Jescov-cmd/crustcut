using System.IO;
using FluentAssertions;
using PrimeOSTuner.Core.Platform;
using PrimeOSTuner.Core.Settings;
using Xunit;

namespace PrimeOSTuner.Tests.Settings;

public class AppSettingsUpdateTests
{
    [Fact]
    public void Update_keeps_fields_it_did_not_touch()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");
        try
        {
            var store = new AppSettingsStore(path);
            store.Save(new AppSettings { StartMinimized = true, FanMode = "Silent" });

            store.Update(s => s.FanMode = "Auto");

            var loaded = store.Load();
            loaded.FanMode.Should().Be("Auto");
            loaded.StartMinimized.Should().BeTrue();
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Update_writes_nothing_when_the_file_cannot_be_read()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, "{ this is not json");
            var store = new AppSettingsStore(path);

            var result = store.Update(s => s.FanMode = "Auto");

            result.Should().BeNull();
            File.ReadAllText(path).Should().Be("{ this is not json");   // not replaced by defaults
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Update_creates_the_file_when_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");
        try
        {
            new AppSettingsStore(path).Update(s => s.FanMode = "Auto").Should().NotBeNull();
            new AppSettingsStore(path).Load().FanMode.Should().Be("Auto");
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Process_snapshot_includes_this_test_process()
    {
        var self = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
        ProcessNames.Snapshot().Should().Contain(n =>
            Path.GetFileNameWithoutExtension(n).Equals(self, StringComparison.OrdinalIgnoreCase));
    }
}
