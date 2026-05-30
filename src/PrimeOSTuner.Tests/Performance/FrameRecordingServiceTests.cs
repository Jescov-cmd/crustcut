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

    // Stream mock that synchronously feeds the given frame times to the service's callback.
    private static Mock<IPresentMonRunner> StreamingRunner(params double[] frameTimesMs)
    {
        var runner = new Mock<IPresentMonRunner>();
        runner.Setup(r => r.StreamAsync(It.IsAny<int>(), It.IsAny<Action<double>>(), It.IsAny<CancellationToken>()))
              .Callback<int, Action<double>, CancellationToken>((_, onFrame, _) =>
              {
                  foreach (var ms in frameTimesMs) onFrame(ms);
              })
              .Returns(Task.CompletedTask);
        return runner;
    }

    [Fact]
    public async Task OnGameStarted_streams_for_the_given_pid()
    {
        var runner = StreamingRunner();
        var svc = new FrameRecordingService(runner.Object, new FrameSessionStore(_storePath), _tempDir);

        svc.OnGameStarted(Game(), pid: 1234);
        await Task.Yield();

        runner.Verify(r => r.StreamAsync(1234, It.IsAny<Action<double>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Streamed_frames_drive_live_fps_and_a_saved_session()
    {
        // 11 frames at 16.67 ms ≈ 60 FPS.
        var runner = StreamingRunner(Enumerable.Repeat(16.67, 11).ToArray());
        var store = new FrameSessionStore(_storePath);
        var svc = new FrameRecordingService(runner.Object, store, _tempDir);

        svc.OnGameStarted(Game(name: "Cyberpunk"), pid: 1234);
        await Task.Yield();

        svc.CurrentFps.Should().BeApproximately(60.0, 1.0, "live FPS comes from the rolling window");

        await svc.OnGameStoppedAsync();

        runner.Verify(r => r.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        var sessions = store.Load();
        sessions.Should().HaveCount(1);
        sessions[0].GameName.Should().Be("Cyberpunk");
        sessions[0].Stats.AvgFps.Should().BeApproximately(60.0, 0.5);
        sessions[0].Stats.MaxFps.Should().BeApproximately(60.0, 0.5);
        svc.CurrentFps.Should().Be(0, "the live counter resets when the game stops");
    }

    [Fact]
    public async Task OnGameStopped_with_no_recording_is_a_noop()
    {
        var svc = new FrameRecordingService(new Mock<IPresentMonRunner>().Object, new FrameSessionStore(_storePath), _tempDir);
        await svc.OnGameStoppedAsync();
        new FrameSessionStore(_storePath).Load().Should().BeEmpty();
    }

    [Fact]
    public async Task Too_few_frames_saves_nothing()
    {
        var runner = StreamingRunner(16.67, 16.67, 16.67);   // only 3 frames
        var store = new FrameSessionStore(_storePath);
        var svc = new FrameRecordingService(runner.Object, store, _tempDir);

        svc.OnGameStarted(Game(), pid: 1234);
        await Task.Yield();
        await svc.OnGameStoppedAsync();

        store.Load().Should().BeEmpty();
    }
}
