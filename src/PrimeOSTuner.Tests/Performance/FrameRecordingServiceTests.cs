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

    /// <summary>
    /// Capture that ends while the game is still running (the game moved to a new process,
    /// or the recorder died) must reattach — not leave the rest of the session unrecorded.
    /// </summary>
    [Fact]
    public async Task Capture_that_drops_mid_game_reattaches_to_the_games_current_process()
    {
        var pids = new List<int>();
        var runner = new Mock<IPresentMonRunner>();
        runner.Setup(r => r.StreamAsync(It.IsAny<int>(), It.IsAny<Action<double>>(), It.IsAny<CancellationToken>()))
              .Callback<int, Action<double>, CancellationToken>((pid, _, _) => { lock (pids) pids.Add(pid); })
              .Returns(Task.CompletedTask);   // ends at once, as if the recorder dropped out
        var log = new List<string>();
        var svc = new FrameRecordingService(runner.Object, new FrameSessionStore(_storePath), _tempDir,
            resolvePid: _ => 5678, log: s => { lock (log) log.Add(s); });

        svc.OnGameStarted(Game(), pid: 1234);
        await Task.Delay(2600);
        await svc.OnGameStoppedAsync();

        lock (pids) pids.Take(2).Should().Equal(1234, 5678);
        lock (log) log.Should().Contain(l => l.Contains("reattached"));
    }

    /// <summary>2026-09-30: attached to a Java process that never draws (Minecraft's
    /// launcher), a whole evening recorded 0 frames. Silence on one process while the game
    /// resolves to another: move the capture there.</summary>
    [Fact]
    public async Task A_silent_capture_moves_to_the_process_actually_drawing()
    {
        var pids = new List<int>();
        var runner = new Mock<IPresentMonRunner>();
        runner.Setup(r => r.StreamAsync(It.IsAny<int>(), It.IsAny<Action<double>>(), It.IsAny<CancellationToken>()))
              .Returns<int, Action<double>, CancellationToken>(async (pid, onFrame, ct) =>
              {
                  lock (pids) pids.Add(pid);
                  if (pid == 2) { for (var i = 0; i < 20; i++) onFrame(16.7); }
                  try { await Task.Delay(Timeout.Infinite, ct); } catch (OperationCanceledException) { }
              });
        var log = new List<string>();
        var svc = new FrameRecordingService(runner.Object, new FrameSessionStore(_storePath), _tempDir,
            resolvePid: _ => 2, log: s => { lock (log) log.Add(s); })
        { SilenceLimit = TimeSpan.FromMilliseconds(100) };

        svc.OnGameStarted(Game(name: "Minecraft"), pid: 1);
        await Task.Delay(2800);
        await svc.OnGameStoppedAsync();

        lock (pids) pids.Take(2).Should().Equal(1, 2);
        lock (log) log.Should().Contain(l => l.Contains("switching to pid 2"));
        new FrameSessionStore(_storePath).Load().Should().ContainSingle(s => s.Stats.SampleCount == 20);
    }

    /// <summary>A second game starting mid-session: the first session is saved on its own
    /// (not mixed with the new game's frames) and recording moves to the new game.</summary>
    [Fact]
    public async Task A_second_game_saves_the_first_session_and_takes_over_recording()
    {
        var store = new FrameSessionStore(_storePath);
        var svc = new FrameRecordingService(StreamingRunner(Enumerable.Repeat(16.67, 20).ToArray()).Object,
            store, _tempDir);

        svc.OnGameStarted(Game("mc", "Minecraft"), pid: 1);
        await Task.Delay(50);
        svc.OnGameStarted(Game("rbx", "Roblox"), pid: 2);
        await Task.Delay(50);
        await svc.OnGameStoppedAsync(Game("rbx", "Roblox"));

        store.Load().Select(s => s.GameName).Should().BeEquivalentTo("Minecraft", "Roblox");
        store.Load().Should().OnlyContain(s => s.Stats.SampleCount == 20);   // nothing mixed
    }

    /// <summary>The OTHER game closing must not end the session still being played.</summary>
    [Fact]
    public async Task A_different_game_closing_does_not_end_the_recorded_session()
    {
        var store = new FrameSessionStore(_storePath);
        var svc = new FrameRecordingService(StreamingRunner(Enumerable.Repeat(16.67, 20).ToArray()).Object,
            store, _tempDir);

        svc.OnGameStarted(Game("rbx", "Roblox"), pid: 2);
        await Task.Delay(50);
        await svc.OnGameStoppedAsync(Game("other", "Something else"));

        store.Load().Should().BeEmpty();                    // Roblox still being recorded
        await svc.OnGameStoppedAsync(Game("rbx", "Roblox"));
        store.Load().Should().ContainSingle(s => s.GameName == "Roblox");
    }

    [Fact]
    public async Task A_session_too_short_to_keep_says_why_instead_of_vanishing()
    {
        var log = new List<string>();
        var svc = new FrameRecordingService(StreamingRunner(16.7, 16.7).Object,
            new FrameSessionStore(_storePath), _tempDir, log: s => { lock (log) log.Add(s); });

        svc.OnGameStarted(Game(name: "Roblox"), pid: 1);
        await Task.Yield();
        await svc.OnGameStoppedAsync();

        lock (log) log.Should().Contain(l => l.Contains("Roblox") && l.Contains("not saved"));
    }
}
