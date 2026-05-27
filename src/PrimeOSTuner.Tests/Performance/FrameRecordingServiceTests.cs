using FluentAssertions;
using Moq;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class FrameRecordingServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _storePath;

    public FrameRecordingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"primeos-frec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _storePath = Path.Combine(_tempDir, "store.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static KnownGame Game(string id = "g", string name = "Test Game") =>
        new(id, name, new[] { "test.exe" }, "12345", "C:\\Games\\Test", KnownGameSource.Steam);

    [Fact]
    public async Task OnGameStarted_calls_runner_StartAsync_with_the_pid()
    {
        var runner = new Mock<IPresentMonRunner>();
        runner.Setup(r => r.StartAsync(1234, It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync("dummy.csv");
        var store = new FrameSessionStore(_storePath);

        var svc = new FrameRecordingService(runner.Object, store, _tempDir);
        svc.OnGameStarted(Game(), pid: 1234);
        await Task.Yield();

        runner.Verify(r => r.StartAsync(1234, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGameStopped_stops_the_runner_parses_csv_saves_a_session_and_deletes_the_csv()
    {
        // Pre-stage a valid CSV at a known path so the service can parse it.
        var csvPath = Path.Combine(_tempDir, "session.csv");
        File.WriteAllText(csvPath,
            "msBetweenPresents\n16.67\n16.67\n16.67\n16.67\n16.67\n16.67\n16.67\n16.67\n16.67\n16.67\n16.67\n");

        var runner = new Mock<IPresentMonRunner>();
        runner.Setup(r => r.StartAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(csvPath);
        var store = new FrameSessionStore(_storePath);

        var svc = new FrameRecordingService(runner.Object, store, _tempDir);
        svc.OnGameStarted(Game(name: "Cyberpunk"), pid: 1234);
        await Task.Yield();
        await svc.OnGameStoppedAsync();

        runner.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        var sessions = store.Load();
        sessions.Should().HaveCount(1);
        sessions[0].GameName.Should().Be("Cyberpunk");
        sessions[0].Stats.AvgFps.Should().BeApproximately(60.0, 0.5);
        File.Exists(csvPath).Should().BeFalse();   // cleaned up
    }

    [Fact]
    public async Task OnGameStopped_with_no_in_flight_recording_is_a_noop()
    {
        var runner = new Mock<IPresentMonRunner>();
        var store = new FrameSessionStore(_storePath);
        var svc = new FrameRecordingService(runner.Object, store, _tempDir);

        await svc.OnGameStoppedAsync();

        store.Load().Should().BeEmpty();
        runner.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.AtMostOnce);
    }

    [Fact]
    public async Task OnGameStopped_does_not_save_a_session_when_the_csv_is_empty()
    {
        var csvPath = Path.Combine(_tempDir, "empty.csv");
        File.WriteAllText(csvPath, "msBetweenPresents\n");   // header only

        var runner = new Mock<IPresentMonRunner>();
        runner.Setup(r => r.StartAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync(csvPath);
        var store = new FrameSessionStore(_storePath);

        var svc = new FrameRecordingService(runner.Object, store, _tempDir);
        svc.OnGameStarted(Game(), pid: 1234);
        await Task.Yield();
        await svc.OnGameStoppedAsync();

        store.Load().Should().BeEmpty();
    }

    [Fact]
    public async Task OnGameStarted_with_runner_returning_null_does_not_throw()
    {
        var runner = new Mock<IPresentMonRunner>();
        runner.Setup(r => r.StartAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
              .ReturnsAsync((string?)null);
        var store = new FrameSessionStore(_storePath);

        var svc = new FrameRecordingService(runner.Object, store, _tempDir);
        svc.OnGameStarted(Game(), pid: 1234);
        await Task.Yield();
        await svc.OnGameStoppedAsync();

        store.Load().Should().BeEmpty();
    }
}
