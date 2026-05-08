using FluentAssertions;
using PrimeOSTuner.Win.SteamGridDb;
using Xunit;

namespace PrimeOSTuner.Tests.SteamGridDb;

public class SteamGridDbSettingsTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid()}.json");

    public void Dispose() { if (File.Exists(_path)) File.Delete(_path); }

    [Fact]
    public void Load_returns_empty_settings_when_file_missing()
    {
        var s = SteamGridDbSettings.Load(_path);
        s.SteamGridDbApiKey.Should().BeNull();
    }

    [Fact]
    public void Load_reads_api_key_from_file()
    {
        File.WriteAllText(_path, "{\"SteamGridDbApiKey\":\"abc123\"}");
        var s = SteamGridDbSettings.Load(_path);
        s.SteamGridDbApiKey.Should().Be("abc123");
    }

    [Fact]
    public void Load_returns_empty_settings_when_file_is_invalid_json()
    {
        File.WriteAllText(_path, "{this is not json");
        var s = SteamGridDbSettings.Load(_path);
        s.SteamGridDbApiKey.Should().BeNull();
    }
}
