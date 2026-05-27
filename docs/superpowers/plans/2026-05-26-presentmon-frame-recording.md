# Performance / PresentMon Frame-Time Recording Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture real frame-time data via Intel's bundled PresentMon while a Library game runs, compute summary stats (avg FPS / 1% low / 0.1% low / max frame-time / duration) on game exit, and display them as cards in a new PERFORMANCE section on the Dashboard.

**Architecture:** Pure data records + pure parser + pure stats computer in `PrimeOSTuner.Core.Performance`; subprocess runner in `PrimeOSTuner.Win.Performance`; orchestrating service hooks the existing `ProfileLifecycleService.GameStarted/Stopped` events the same way Sentinel and the suspender do. PresentMon-x64.exe is bundled in `Assets/PresentMon/` and copied to output.

**Tech Stack:** .NET 9 / C# / WPF, xUnit + FluentAssertions + Moq, CommunityToolkit.Mvvm, `System.Diagnostics.Process` for subprocess management, `System.Text.Json` for storage.

**Spec:** [docs/superpowers/specs/2026-05-26-presentmon-frame-recording-design.md](../specs/2026-05-26-presentmon-frame-recording-design.md)

---

## File map

**Create:**

- `src/PrimeOSTuner.Core/Performance/FrameSessionStats.cs` — computed stats record
- `src/PrimeOSTuner.Core/Performance/FrameSession.cs` — persistent session record
- `src/PrimeOSTuner.Core/Performance/FrameTimeStatsCalculator.cs` — pure stats computer
- `src/PrimeOSTuner.Core/Performance/FrameTimeParser.cs` — pure CSV parser
- `src/PrimeOSTuner.Core/Performance/IPresentMonRunner.cs` — subprocess runner abstraction
- `src/PrimeOSTuner.Core/Performance/IFrameSessionStore.cs` — store abstraction
- `src/PrimeOSTuner.Core/Performance/FrameSessionStore.cs` — JSON-backed store
- `src/PrimeOSTuner.Core/Performance/FrameRecordingService.cs` — orchestrator
- `src/PrimeOSTuner.Win/Performance/PresentMonRunner.cs` — `Process.Start`-based runner
- `src/PrimeOSTuner.UI/ViewModels/FrameSessionVm.cs` — view-model for a single card
- `src/PrimeOSTuner.UI/Assets/PresentMon/PresentMon-x64.exe` — bundled binary (downloaded in Task 9)
- `src/PrimeOSTuner.Tests/Performance/FrameTimeStatsCalculatorTests.cs`
- `src/PrimeOSTuner.Tests/Performance/FrameTimeParserTests.cs`
- `src/PrimeOSTuner.Tests/Performance/FrameSessionStoreTests.cs`
- `src/PrimeOSTuner.Tests/Performance/FrameRecordingServiceTests.cs`
- `src/PrimeOSTuner.Tests/Fixtures/presentmon/valid-short.csv`
- `src/PrimeOSTuner.Tests/Fixtures/presentmon/empty.csv`
- `src/PrimeOSTuner.Tests/Fixtures/presentmon/header-only.csv`
- `src/PrimeOSTuner.Tests/Fixtures/presentmon/with-zero-rows.csv`

**Modify:**

- `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs` — add optional `FrameRecordingService? recorder = null` ctor parameter, call its `OnGameStarted` / `OnGameStopped` from existing event handlers.
- `src/PrimeOSTuner.UI/App.xaml.cs` — DI registrations, bundle-path resolution, orphan-CSV cleanup on startup.
- `src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs` — add `ObservableCollection<FrameSessionVm> RecentSessions`.
- `src/PrimeOSTuner.UI/Views/DashboardView.xaml` — new PERFORMANCE section below the tile grid.
- `src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj` — copy `Assets/PresentMon/*` to output.
- `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj` — include presentmon fixture CSVs as content.

---

## Task 1: FrameSessionStats + FrameSession records

Pure data types — no behaviour. The next task adds the computer.

**Files:**
- Create: `src/PrimeOSTuner.Core/Performance/FrameSessionStats.cs`
- Create: `src/PrimeOSTuner.Core/Performance/FrameSession.cs`

- [ ] **Step 1.1: Create FrameSessionStats**

`src/PrimeOSTuner.Core/Performance/FrameSessionStats.cs`:

```csharp
namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Summary statistics computed from a session's raw frame-time samples.
/// All frame-time values are in milliseconds; FPS values are derived.
/// </summary>
public sealed record FrameSessionStats(
    double AvgFps,
    double OnePctLowFps,       // = 1000 / P99FrameTimeMs
    double ZeroPointOnePctLowFps, // = 1000 / P999FrameTimeMs
    double P50FrameTimeMs,
    double P99FrameTimeMs,
    double P999FrameTimeMs,
    double MaxFrameTimeMs,
    int SampleCount);
```

- [ ] **Step 1.2: Create FrameSession**

`src/PrimeOSTuner.Core/Performance/FrameSession.cs`:

```csharp
namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// A single completed game session's frame-time recording, persisted across
/// app restarts.
/// </summary>
public sealed record FrameSession(
    string GameId,
    string GameName,
    DateTime StartedAt,
    TimeSpan Duration,
    FrameSessionStats Stats);
```

- [ ] **Step 1.3: Verify build**

```powershell
dotnet build src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj --nologo -v minimal
```

Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 1.4: Commit**

```powershell
git add -- src/PrimeOSTuner.Core/Performance/FrameSessionStats.cs `
            src/PrimeOSTuner.Core/Performance/FrameSession.cs
git commit -m "feat(perf): FrameSession + FrameSessionStats records" -- `
    src/PrimeOSTuner.Core/Performance/FrameSessionStats.cs `
    src/PrimeOSTuner.Core/Performance/FrameSession.cs
```

---

## Task 2: FrameTimeStatsCalculator (pure)

Pure function: given a list of frame-time samples (ms), compute the `FrameSessionStats`.

**Files:**
- Create: `src/PrimeOSTuner.Core/Performance/FrameTimeStatsCalculator.cs`
- Create: `src/PrimeOSTuner.Tests/Performance/FrameTimeStatsCalculatorTests.cs`

- [ ] **Step 2.1: Write the failing tests**

`src/PrimeOSTuner.Tests/Performance/FrameTimeStatsCalculatorTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class FrameTimeStatsCalculatorTests
{
    [Fact]
    public void Compute_returns_60fps_when_every_frame_is_exactly_16_67ms()
    {
        var samples = Enumerable.Repeat(1000.0 / 60.0, 1000).ToList();

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.AvgFps.Should().BeApproximately(60.0, 0.01);
        stats.OnePctLowFps.Should().BeApproximately(60.0, 0.01);
        stats.SampleCount.Should().Be(1000);
    }

    [Fact]
    public void Compute_one_percent_low_reflects_the_worst_one_percent_of_frames()
    {
        // 990 frames at 60fps (16.67ms) + 10 frames at 30fps (33.33ms).
        // The 99th percentile sits in the 30fps tail, so 1% low FPS ≈ 30.
        var samples = Enumerable.Repeat(16.67, 990)
            .Concat(Enumerable.Repeat(33.33, 10))
            .ToList();

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.AvgFps.Should().BeApproximately(60.0, 1.0);   // average ~60
        stats.OnePctLowFps.Should().BeLessThan(35);          // 1% low is in the slow tail
        stats.OnePctLowFps.Should().BeGreaterThan(28);
    }

    [Fact]
    public void Compute_returns_zero_stats_for_an_empty_sample_set()
    {
        var stats = FrameTimeStatsCalculator.Compute(new List<double>());

        stats.SampleCount.Should().Be(0);
        stats.AvgFps.Should().Be(0);
        stats.OnePctLowFps.Should().Be(0);
        stats.MaxFrameTimeMs.Should().Be(0);
    }

    [Fact]
    public void Compute_handles_a_single_sample_without_throwing()
    {
        var stats = FrameTimeStatsCalculator.Compute(new List<double> { 16.67 });

        stats.SampleCount.Should().Be(1);
        stats.AvgFps.Should().BeApproximately(60.0, 0.1);
        stats.MaxFrameTimeMs.Should().BeApproximately(16.67, 0.001);
    }

    [Fact]
    public void Compute_ignores_zero_and_negative_samples()
    {
        // PresentMon's first row often has msBetweenPresents = 0; some malformed rows
        // could also produce negatives. Both should be filtered out.
        var samples = new List<double> { 0.0, -5.0, 16.67, 16.67, 16.67 };

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.SampleCount.Should().Be(3);   // only the three positive samples
        stats.AvgFps.Should().BeApproximately(60.0, 0.1);
    }

    [Fact]
    public void Compute_sets_MaxFrameTimeMs_to_the_largest_sample()
    {
        var samples = new List<double> { 16.67, 16.67, 200.0, 16.67 };

        var stats = FrameTimeStatsCalculator.Compute(samples);

        stats.MaxFrameTimeMs.Should().BeApproximately(200.0, 0.001);
    }
}
```

- [ ] **Step 2.2: Run tests to verify they fail**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameTimeStatsCalculatorTests --nologo -v minimal
```

Expected: build failure ("`FrameTimeStatsCalculator` does not exist").

- [ ] **Step 2.3: Implement the calculator**

`src/PrimeOSTuner.Core/Performance/FrameTimeStatsCalculator.cs`:

```csharp
namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Pure stats computer over raw frame-time samples in milliseconds.
/// Negative and zero samples are filtered out — they represent either the
/// first-row sentinel PresentMon writes or malformed rows.
/// </summary>
public static class FrameTimeStatsCalculator
{
    public static FrameSessionStats Compute(IReadOnlyList<double> samples)
    {
        var valid = samples.Where(s => s > 0).ToArray();
        if (valid.Length == 0)
            return new FrameSessionStats(0, 0, 0, 0, 0, 0, 0, 0);

        Array.Sort(valid);

        var avgMs = valid.Average();
        var avgFps = 1000.0 / avgMs;

        var p50  = Percentile(valid, 0.50);
        var p99  = Percentile(valid, 0.99);
        var p999 = Percentile(valid, 0.999);
        var max  = valid[^1];

        return new FrameSessionStats(
            AvgFps: avgFps,
            OnePctLowFps: 1000.0 / p99,
            ZeroPointOnePctLowFps: 1000.0 / p999,
            P50FrameTimeMs: p50,
            P99FrameTimeMs: p99,
            P999FrameTimeMs: p999,
            MaxFrameTimeMs: max,
            SampleCount: valid.Length);
    }

    // Nearest-rank percentile on a pre-sorted ascending array.
    private static double Percentile(double[] sorted, double pct)
    {
        if (sorted.Length == 0) return 0;
        var rank = (int)Math.Ceiling(pct * sorted.Length) - 1;
        if (rank < 0) rank = 0;
        if (rank >= sorted.Length) rank = sorted.Length - 1;
        return sorted[rank];
    }
}
```

- [ ] **Step 2.4: Run tests to verify they pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameTimeStatsCalculatorTests --nologo -v minimal
```

Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 2.5: Commit**

```powershell
git add -- src/PrimeOSTuner.Core/Performance/FrameTimeStatsCalculator.cs `
            src/PrimeOSTuner.Tests/Performance/FrameTimeStatsCalculatorTests.cs
git commit -m "feat(perf): frame-time stats calculator (avg FPS / 1% low / 0.1% low / max)" -- `
    src/PrimeOSTuner.Core/Performance/FrameTimeStatsCalculator.cs `
    src/PrimeOSTuner.Tests/Performance/FrameTimeStatsCalculatorTests.cs
```

---

## Task 3: FrameTimeParser + fixture CSVs

Pure parser. PresentMon 2.x writes one CSV row per frame. The column we want is `msBetweenPresents` — frame-to-frame interval in milliseconds.

**Files:**
- Create: `src/PrimeOSTuner.Core/Performance/FrameTimeParser.cs`
- Create: `src/PrimeOSTuner.Tests/Performance/FrameTimeParserTests.cs`
- Create: `src/PrimeOSTuner.Tests/Fixtures/presentmon/valid-short.csv`
- Create: `src/PrimeOSTuner.Tests/Fixtures/presentmon/empty.csv`
- Create: `src/PrimeOSTuner.Tests/Fixtures/presentmon/header-only.csv`
- Create: `src/PrimeOSTuner.Tests/Fixtures/presentmon/with-zero-rows.csv`
- Modify: `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj`

- [ ] **Step 3.1: Create the fixture CSVs**

`src/PrimeOSTuner.Tests/Fixtures/presentmon/valid-short.csv`:

```
Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,AllowsTearing,PresentMode,Dropped,TimeInSeconds,msBetweenPresents,msBetweenDisplayChange,msInPresentAPI,msUntilRenderComplete,msUntilDisplayed
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.000,0.0,16.7,0.5,8.2,16.7
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.017,16.7,16.7,0.5,8.2,16.7
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.034,16.7,16.7,0.5,8.2,16.7
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.100,66.7,66.7,0.5,8.2,66.7
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.117,16.7,16.7,0.5,8.2,16.7
```

`src/PrimeOSTuner.Tests/Fixtures/presentmon/empty.csv`:

```
```

(literally an empty file — leave it empty after creation)

`src/PrimeOSTuner.Tests/Fixtures/presentmon/header-only.csv`:

```
Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,AllowsTearing,PresentMode,Dropped,TimeInSeconds,msBetweenPresents,msBetweenDisplayChange,msInPresentAPI,msUntilRenderComplete,msUntilDisplayed
```

`src/PrimeOSTuner.Tests/Fixtures/presentmon/with-zero-rows.csv`:

```
Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags,AllowsTearing,PresentMode,Dropped,TimeInSeconds,msBetweenPresents,msBetweenDisplayChange,msInPresentAPI,msUntilRenderComplete,msUntilDisplayed
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.000,0.0,16.7,0.5,8.2,16.7
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.017,16.7,16.7,0.5,8.2,16.7
game.exe,1234,0x1,DXGI,0,0,0,Hardware,0,1.034,0.0,16.7,0.5,8.2,16.7
```

- [ ] **Step 3.2: Wire fixtures into the test project**

Edit `src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj`. Add a new `<ItemGroup>` near the existing fixture copy block (the one for `steam-specs`):

```xml
<ItemGroup>
  <None Update="Fixtures\presentmon\*.csv">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 3.3: Write the failing parser tests**

`src/PrimeOSTuner.Tests/Performance/FrameTimeParserTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class FrameTimeParserTests
{
    private static string FixturePath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "presentmon", name);

    [Fact]
    public void Parse_valid_csv_returns_five_samples()
    {
        var path = FixturePath("valid-short.csv");

        var samples = FrameTimeParser.ParseFile(path);

        samples.Should().HaveCount(5);
        samples[0].Should().BeApproximately(0.0, 0.001);
        samples[3].Should().BeApproximately(66.7, 0.001);
    }

    [Fact]
    public void Parse_empty_file_returns_an_empty_list()
    {
        var samples = FrameTimeParser.ParseFile(FixturePath("empty.csv"));

        samples.Should().BeEmpty();
    }

    [Fact]
    public void Parse_header_only_csv_returns_an_empty_list()
    {
        var samples = FrameTimeParser.ParseFile(FixturePath("header-only.csv"));

        samples.Should().BeEmpty();
    }

    [Fact]
    public void Parse_missing_file_returns_an_empty_list_without_throwing()
    {
        var samples = FrameTimeParser.ParseFile(@"C:\does\not\exist.csv");

        samples.Should().BeEmpty();
    }

    [Fact]
    public void Parse_preserves_zero_rows_so_the_stats_calculator_can_filter_them()
    {
        var samples = FrameTimeParser.ParseFile(FixturePath("with-zero-rows.csv"));

        samples.Should().HaveCount(3);
        samples[0].Should().Be(0.0);    // raw value, stats calculator filters
        samples[1].Should().BeApproximately(16.7, 0.001);
        samples[2].Should().Be(0.0);
    }

    [Fact]
    public void Parse_csv_without_msBetweenPresents_column_returns_empty()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, "Application,ProcessID\ngame.exe,1234\n");

            var samples = FrameTimeParser.ParseFile(tmp);

            samples.Should().BeEmpty();
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
```

- [ ] **Step 3.4: Run tests to verify they fail**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameTimeParserTests --nologo -v minimal
```

Expected: build failure ("`FrameTimeParser` does not exist").

- [ ] **Step 3.5: Implement the parser**

`src/PrimeOSTuner.Core/Performance/FrameTimeParser.cs`:

```csharp
using System.Globalization;

namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Pure parser for PresentMon's CSV output. Looks up the
/// <c>msBetweenPresents</c> column by header name and extracts its value
/// from every data row. Tolerant of missing files and malformed rows —
/// anything that fails parsing is silently skipped so a partial CSV from
/// a crashed PresentMon still yields whatever samples it captured.
/// </summary>
public static class FrameTimeParser
{
    private const string TargetColumn = "msBetweenPresents";

    public static IReadOnlyList<double> ParseFile(string path)
    {
        if (!File.Exists(path)) return Array.Empty<double>();

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { return Array.Empty<double>(); }

        if (lines.Length < 2) return Array.Empty<double>();

        var headers = lines[0].Split(',');
        var columnIndex = Array.FindIndex(headers,
            h => string.Equals(h.Trim(), TargetColumn, StringComparison.OrdinalIgnoreCase));
        if (columnIndex < 0) return Array.Empty<double>();

        var samples = new List<double>(lines.Length - 1);
        for (int i = 1; i < lines.Length; i++)
        {
            var cells = lines[i].Split(',');
            if (cells.Length <= columnIndex) continue;
            if (double.TryParse(cells[columnIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var ms))
                samples.Add(ms);
        }
        return samples;
    }
}
```

- [ ] **Step 3.6: Run tests to verify they pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameTimeParserTests --nologo -v minimal
```

Expected: `Passed: 6, Failed: 0`.

- [ ] **Step 3.7: Commit**

```powershell
git add -- src/PrimeOSTuner.Core/Performance/FrameTimeParser.cs `
            src/PrimeOSTuner.Tests/Performance/FrameTimeParserTests.cs `
            src/PrimeOSTuner.Tests/Fixtures/presentmon/ `
            src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
git commit -m "feat(perf): tolerant PresentMon CSV parser" -- `
    src/PrimeOSTuner.Core/Performance/FrameTimeParser.cs `
    src/PrimeOSTuner.Tests/Performance/FrameTimeParserTests.cs `
    src/PrimeOSTuner.Tests/Fixtures/presentmon/ `
    src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj
```

---

## Task 4: IPresentMonRunner abstraction

Interface so `FrameRecordingService` can be unit-tested without spawning a real process.

**Files:**
- Create: `src/PrimeOSTuner.Core/Performance/IPresentMonRunner.cs`

- [ ] **Step 4.1: Create the interface**

`src/PrimeOSTuner.Core/Performance/IPresentMonRunner.cs`:

```csharp
namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Starts and stops the PresentMon subprocess. Implementations swallow
/// all failures and return null on Start so the recording service can
/// degrade silently — frame-time capture must never break a game launch.
/// </summary>
public interface IPresentMonRunner
{
    /// <summary>
    /// Spawn PresentMon targeting the given pid. Returns the CSV path it
    /// is writing to, or null if PresentMon could not be started.
    /// </summary>
    Task<string?> StartAsync(int gamePid, string outputCsvPath, CancellationToken ct = default);

    /// <summary>Stop the in-flight PresentMon process, if any. Safe to call when nothing is running.</summary>
    Task StopAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4.2: Verify build**

```powershell
dotnet build src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj --nologo -v minimal
```

- [ ] **Step 4.3: Commit**

```powershell
git add -- src/PrimeOSTuner.Core/Performance/IPresentMonRunner.cs
git commit -m "feat(perf): IPresentMonRunner abstraction" -- src/PrimeOSTuner.Core/Performance/IPresentMonRunner.cs
```

---

## Task 5: PresentMonRunner concrete implementation

Spawns the bundled `PresentMon-x64.exe` via `Process.Start`. The binary itself is added in Task 9 — this task only writes the runner code.

**Files:**
- Create: `src/PrimeOSTuner.Win/Performance/PresentMonRunner.cs`

- [ ] **Step 5.1: Implement the runner**

`src/PrimeOSTuner.Win/Performance/PresentMonRunner.cs`:

```csharp
using System.Diagnostics;
using PrimeOSTuner.Core.Performance;

namespace PrimeOSTuner.Win.Performance;

/// <summary>
/// Spawns the bundled PresentMon.exe as a subprocess. PresentMon is told to
/// self-terminate when the target game exits via <c>-terminate_on_proc_exit</c>
/// and to skip writing CSVs for processes that never present a frame (a
/// launcher rather than a real game).
/// </summary>
public sealed class PresentMonRunner : IPresentMonRunner
{
    private readonly string _binaryPath;
    private Process? _current;
    private readonly object _gate = new();

    public PresentMonRunner(string binaryPath)
    {
        _binaryPath = binaryPath;
    }

    public Task<string?> StartAsync(int gamePid, string outputCsvPath, CancellationToken ct = default)
    {
        if (!File.Exists(_binaryPath)) return Task.FromResult<string?>(null);

        try
        {
            var psi = new ProcessStartInfo(_binaryPath)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            psi.ArgumentList.Add("-process_id");
            psi.ArgumentList.Add(gamePid.ToString());
            psi.ArgumentList.Add("-output_file");
            psi.ArgumentList.Add(outputCsvPath);
            psi.ArgumentList.Add("-no_csv_for_processes_with_no_present_events");
            psi.ArgumentList.Add("-terminate_on_proc_exit");
            psi.ArgumentList.Add("-stop_existing_session");

            Directory.CreateDirectory(Path.GetDirectoryName(outputCsvPath)!);

            var proc = Process.Start(psi);
            if (proc is null) return Task.FromResult<string?>(null);

            lock (_gate) _current = proc;
            return Task.FromResult<string?>(outputCsvPath);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        Process? toStop;
        lock (_gate) { toStop = _current; _current = null; }
        if (toStop is null) return;

        try
        {
            if (!toStop.HasExited) toStop.Kill(entireProcessTree: true);
            // Wait briefly so PresentMon flushes its CSV before we try to parse it.
            await toStop.WaitForExitAsync(ct);
        }
        catch { /* best effort */ }
        finally
        {
            try { toStop.Dispose(); } catch { }
        }
    }
}
```

- [ ] **Step 5.2: Verify build**

```powershell
dotnet build src/PrimeOSTuner.Win/PrimeOSTuner.Win.csproj --nologo -v minimal
```

- [ ] **Step 5.3: Commit**

```powershell
git add -- src/PrimeOSTuner.Win/Performance/PresentMonRunner.cs
git commit -m "feat(perf): PresentMonRunner subprocess wrapper" -- src/PrimeOSTuner.Win/Performance/PresentMonRunner.cs
```

---

## Task 6: FrameSessionStore (JSON-backed)

Persistent list of recent N sessions, capped at 50, atomic write.

**Files:**
- Create: `src/PrimeOSTuner.Core/Performance/IFrameSessionStore.cs`
- Create: `src/PrimeOSTuner.Core/Performance/FrameSessionStore.cs`
- Create: `src/PrimeOSTuner.Tests/Performance/FrameSessionStoreTests.cs`

- [ ] **Step 6.1: Create the interface**

`src/PrimeOSTuner.Core/Performance/IFrameSessionStore.cs`:

```csharp
namespace PrimeOSTuner.Core.Performance;

public interface IFrameSessionStore
{
    /// <summary>Sessions ordered newest-first.</summary>
    IReadOnlyList<FrameSession> Load();

    /// <summary>Append a session. Older entries past the cap are dropped on save.</summary>
    void Save(FrameSession session);

    /// <summary>Raised after Save() succeeds.</summary>
    event EventHandler? Updated;
}
```

- [ ] **Step 6.2: Write the failing tests**

`src/PrimeOSTuner.Tests/Performance/FrameSessionStoreTests.cs`:

```csharp
using FluentAssertions;
using PrimeOSTuner.Core.Performance;
using Xunit;

namespace PrimeOSTuner.Tests.Performance;

public class FrameSessionStoreTests : IDisposable
{
    private readonly string _tempPath;

    public FrameSessionStoreTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"primeos-test-{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
        if (File.Exists(_tempPath + ".tmp")) File.Delete(_tempPath + ".tmp");
    }

    private static FrameSession MakeSession(string gameName, DateTime at) =>
        new(GameId: gameName.ToLowerInvariant(),
            GameName: gameName,
            StartedAt: at,
            Duration: TimeSpan.FromMinutes(10),
            Stats: new FrameSessionStats(60, 45, 30, 16.7, 22.0, 33.0, 100.0, 36000));

    [Fact]
    public void Save_then_Load_round_trips_a_session()
    {
        var store = new FrameSessionStore(_tempPath);
        var session = MakeSession("Cyberpunk", new DateTime(2026, 5, 26, 12, 0, 0));

        store.Save(session);
        var loaded = new FrameSessionStore(_tempPath).Load();

        loaded.Should().HaveCount(1);
        loaded[0].GameName.Should().Be("Cyberpunk");
        loaded[0].Stats.AvgFps.Should().Be(60);
    }

    [Fact]
    public void Save_orders_newest_first()
    {
        var store = new FrameSessionStore(_tempPath);
        var older = MakeSession("Older", new DateTime(2026, 5, 25, 10, 0, 0));
        var newer = MakeSession("Newer", new DateTime(2026, 5, 26, 10, 0, 0));

        store.Save(older);
        store.Save(newer);

        store.Load().Select(s => s.GameName).Should().Equal(new[] { "Newer", "Older" });
    }

    [Fact]
    public void Save_caps_the_list_at_fifty_entries()
    {
        var store = new FrameSessionStore(_tempPath);
        for (int i = 0; i < 60; i++)
            store.Save(MakeSession($"Game{i}", new DateTime(2026, 1, 1).AddMinutes(i)));

        store.Load().Should().HaveCount(50);
        store.Load()[0].GameName.Should().Be("Game59");   // newest
    }

    [Fact]
    public void Load_returns_empty_when_file_does_not_exist()
    {
        var store = new FrameSessionStore(_tempPath);

        store.Load().Should().BeEmpty();
    }

    [Fact]
    public void Save_raises_Updated_event()
    {
        var store = new FrameSessionStore(_tempPath);
        var fired = 0;
        store.Updated += (_, _) => fired++;

        store.Save(MakeSession("A", DateTime.UtcNow));
        store.Save(MakeSession("B", DateTime.UtcNow));

        fired.Should().Be(2);
    }
}
```

- [ ] **Step 6.3: Run tests to verify they fail**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameSessionStoreTests --nologo -v minimal
```

Expected: build failure ("`FrameSessionStore` does not exist").

- [ ] **Step 6.4: Implement the store**

`src/PrimeOSTuner.Core/Performance/FrameSessionStore.cs`:

```csharp
using System.Text.Json;

namespace PrimeOSTuner.Core.Performance;

public sealed class FrameSessionStore : IFrameSessionStore
{
    private const int MaxEntries = 50;
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    private readonly string _path;
    private readonly object _gate = new();

    public event EventHandler? Updated;

    public FrameSessionStore(string path) { _path = path; }

    public static string DefaultPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrimeOSTuner", "frame-sessions.json");

    public IReadOnlyList<FrameSession> Load()
    {
        lock (_gate)
        {
            if (!File.Exists(_path)) return Array.Empty<FrameSession>();
            try
            {
                var json = File.ReadAllText(_path);
                var list = JsonSerializer.Deserialize<List<FrameSession>>(json) ?? new();
                return list;
            }
            catch
            {
                return Array.Empty<FrameSession>();
            }
        }
    }

    public void Save(FrameSession session)
    {
        lock (_gate)
        {
            var existing = LoadInternalLocked();
            existing.Insert(0, session);
            while (existing.Count > MaxEntries) existing.RemoveAt(existing.Count - 1);
            WriteAtomicLocked(existing);
        }
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private List<FrameSession> LoadInternalLocked()
    {
        if (!File.Exists(_path)) return new List<FrameSession>();
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<FrameSession>>(json) ?? new();
        }
        catch
        {
            return new List<FrameSession>();
        }
    }

    private void WriteAtomicLocked(List<FrameSession> list)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(list, JsonOpts));
        File.Move(tmp, _path, overwrite: true);
    }
}
```

- [ ] **Step 6.5: Run tests to verify they pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameSessionStoreTests --nologo -v minimal
```

Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 6.6: Commit**

```powershell
git add -- src/PrimeOSTuner.Core/Performance/IFrameSessionStore.cs `
            src/PrimeOSTuner.Core/Performance/FrameSessionStore.cs `
            src/PrimeOSTuner.Tests/Performance/FrameSessionStoreTests.cs
git commit -m "feat(perf): JSON-backed FrameSessionStore (cap 50, atomic write)" -- `
    src/PrimeOSTuner.Core/Performance/IFrameSessionStore.cs `
    src/PrimeOSTuner.Core/Performance/FrameSessionStore.cs `
    src/PrimeOSTuner.Tests/Performance/FrameSessionStoreTests.cs
```

---

## Task 7: FrameRecordingService (orchestrator)

Hooks the runner + parser + stats + store together. Receives game-lifecycle calls from `ProfileLifecycleService` (wired in Task 8).

**Files:**
- Create: `src/PrimeOSTuner.Core/Performance/FrameRecordingService.cs`
- Create: `src/PrimeOSTuner.Tests/Performance/FrameRecordingServiceTests.cs`

- [ ] **Step 7.1: Write the failing tests**

`src/PrimeOSTuner.Tests/Performance/FrameRecordingServiceTests.cs`:

```csharp
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
            "msBetweenPresents\n16.67\n16.67\n16.67\n");

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
```

- [ ] **Step 7.2: Run tests to verify they fail**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameRecordingServiceTests --nologo -v minimal
```

Expected: build failure ("`FrameRecordingService` does not exist").

- [ ] **Step 7.3: Implement the service**

`src/PrimeOSTuner.Core/Performance/FrameRecordingService.cs`:

```csharp
using PrimeOSTuner.Core.Games;

namespace PrimeOSTuner.Core.Performance;

/// <summary>
/// Glue: on GameStarted, spawn PresentMon into a per-session CSV path.
/// On GameStopped, kill it, parse the CSV, persist a FrameSession, delete
/// the CSV. Every entry point is failure-isolated — recording must never
/// break a game launch.
/// </summary>
public sealed class FrameRecordingService
{
    private readonly IPresentMonRunner _runner;
    private readonly IFrameSessionStore _store;
    private readonly string _framesDir;
    private readonly object _gate = new();

    private DateTime _startedAt;
    private KnownGame? _game;
    private string? _csvPath;

    public FrameRecordingService(IPresentMonRunner runner, IFrameSessionStore store, string framesDir)
    {
        _runner = runner;
        _store = store;
        _framesDir = framesDir;
    }

    public void OnGameStarted(KnownGame game, int pid)
    {
        var now = DateTime.UtcNow;
        var safeId = string.Concat(game.Id.Where(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_'));
        var path = Path.Combine(_framesDir, $"{safeId}-{now:yyyyMMddTHHmmss}.csv");

        lock (_gate)
        {
            _startedAt = now;
            _game = game;
            _csvPath = path;
        }

        // Fire-and-forget — Start runs synchronously enough but we don't want any await contract here.
        _ = StartAsync(pid, path);
    }

    private async Task StartAsync(int pid, string path)
    {
        try { await _runner.StartAsync(pid, path); }
        catch { /* swallow — recording must never break a game launch */ }
    }

    public async Task OnGameStoppedAsync()
    {
        KnownGame? game;
        string? path;
        DateTime startedAt;
        lock (_gate)
        {
            game = _game;
            path = _csvPath;
            startedAt = _startedAt;
            _game = null;
            _csvPath = null;
        }
        if (game is null || path is null) return;

        try { await _runner.StopAsync(); }
        catch { /* best effort */ }

        try
        {
            var samples = FrameTimeParser.ParseFile(path);
            var stats = FrameTimeStatsCalculator.Compute(samples);

            if (stats.SampleCount < 10) return;   // too short to be useful

            var session = new FrameSession(
                GameId: game.Id,
                GameName: game.DisplayName,
                StartedAt: startedAt,
                Duration: DateTime.UtcNow - startedAt,
                Stats: stats);

            _store.Save(session);
        }
        catch { /* never break the game-stopped path */ }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
```

- [ ] **Step 7.4: Run tests to verify they pass**

```powershell
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --filter FrameRecordingServiceTests --nologo -v minimal
```

Expected: `Passed: 5, Failed: 0`.

- [ ] **Step 7.5: Commit**

```powershell
git add -- src/PrimeOSTuner.Core/Performance/FrameRecordingService.cs `
            src/PrimeOSTuner.Tests/Performance/FrameRecordingServiceTests.cs
git commit -m "feat(perf): FrameRecordingService orchestrator" -- `
    src/PrimeOSTuner.Core/Performance/FrameRecordingService.cs `
    src/PrimeOSTuner.Tests/Performance/FrameRecordingServiceTests.cs
```

---

## Task 8: Wire FrameRecordingService into ProfileLifecycleService

Add an optional `FrameRecordingService?` ctor parameter, call its hooks alongside the existing Sentinel and suspender calls. Same pattern as commit `4e65080`.

**Files:**
- Modify: `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs`

- [ ] **Step 8.1: Add the ctor parameter + field**

In `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs`, add `using PrimeOSTuner.Core.Performance;` near the existing usings. Then update fields + ctor:

```csharp
    private readonly IBackgroundSuspenderService? _suspender;
    private readonly ISentinelService? _sentinel;
    private readonly FrameRecordingService? _recorder;

    public ProfileLifecycleService(
        IGameProcessWatcher watcher,
        GameProfileStore profiles,
        ActiveTweaksStore active,
        IReadOnlyDictionary<string, ModeProfile> profileLookup,
        ProfileApplier applier,
        IBackgroundSuspenderService? suspender = null,
        ISentinelService? sentinel = null,
        FrameRecordingService? recorder = null)
    {
        _watcher = watcher;
        _profiles = profiles;
        _active = active;
        _profileLookup = profileLookup;
        _applier = applier;
        _suspender = suspender;
        _sentinel = sentinel;
        _recorder = recorder;
    }
```

- [ ] **Step 8.2: Call OnGameStarted from the watcher event handler**

In the same file, find the existing Sentinel block inside `OnGameStarted` (it ends with `catch { /* Sentinel must never break a game launch */ }`). Immediately **after** that catch block, add:

```csharp
            try { _recorder?.OnGameStarted(game, pid); }
            catch { /* recording must never break a game launch */ }
```

`pid` is already in scope from the existing Sentinel block (it comes from `ResolvePid(game)`).

- [ ] **Step 8.3: Call OnGameStoppedAsync from the stop handler**

In `OnGameStopped`, find the existing Sentinel-stop call (currently:
`try { _sentinel?.OnGameStopped(); } catch { /* never trap on Sentinel teardown */ }`).
Immediately **after** it, add:

```csharp
            try { if (_recorder is not null) await _recorder.OnGameStoppedAsync(); }
            catch { /* recording must never break game-exit teardown */ }
```

(The enclosing method `OnGameStopped` is already `async void` so `await` is valid.)

- [ ] **Step 8.4: Verify build + tests**

```powershell
dotnet build src/PrimeOSTuner.Core/PrimeOSTuner.Core.csproj --nologo -v minimal
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --nologo -v minimal
```

Expected: build green, all tests pass (no new tests yet in this task — lifecycle wiring is a pass-through).

- [ ] **Step 8.5: Commit**

```powershell
git add -- src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs
git commit -m "feat(lifecycle): wire FrameRecordingService into game lifecycle" -- `
    src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs
```

---

## Task 9: Bundle PresentMon binary + csproj copy

Download the latest stable PresentMon and check it in. Configure the UI csproj to copy it to `bin\Debug\.../Assets/PresentMon/`.

**Files:**
- Create: `src/PrimeOSTuner.UI/Assets/PresentMon/PresentMon-x64.exe` (binary)
- Modify: `src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj`

- [ ] **Step 9.1: Download the latest stable PresentMon release**

From PowerShell at the repo root:

```powershell
$dest = "src\PrimeOSTuner.UI\Assets\PresentMon\PresentMon-x64.exe"
New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null

# Latest stable release as of the time of writing. If a newer release exists,
# substitute its asset URL — the file name must remain PresentMon-x64.exe so
# the rest of the plan resolves the path consistently.
$url = "https://github.com/GameTechDev/PresentMon/releases/download/v2.3.0/PresentMon-2.3.0-x64.exe"
Invoke-WebRequest -Uri $url -OutFile $dest
```

Verify it downloaded and isn't zero-byte:

```powershell
Get-Item $dest | Select-Object Name, Length
```

Expected: file size around 600–800 KB (varies by release).

- [ ] **Step 9.2: Confirm the .exe runs (sanity check)**

```powershell
& src\PrimeOSTuner.UI\Assets\PresentMon\PresentMon-x64.exe --help
```

Expected: a usage screen prints. If you see "this app needs admin", that's fine — PresentMon does, but `--help` should work either way.

- [ ] **Step 9.3: Wire the copy into the csproj**

Edit `src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj`. Add a new `<ItemGroup>` near the end (before the closing `</Project>` tag):

```xml
<ItemGroup>
  <None Update="Assets\PresentMon\*.exe">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

- [ ] **Step 9.4: Build + confirm the .exe lands in bin**

```powershell
dotnet build src\PrimeOSTuner.UI\PrimeOSTuner.UI.csproj --nologo -v minimal
Test-Path "src\PrimeOSTuner.UI\bin\Debug\net9.0-windows\Assets\PresentMon\PresentMon-x64.exe"
```

Expected: `True`.

- [ ] **Step 9.5: Commit**

```powershell
git add -- src/PrimeOSTuner.UI/Assets/PresentMon/PresentMon-x64.exe `
            src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj
git commit -m "chore(perf): bundle PresentMon-x64.exe + copy to output" -- `
    src/PrimeOSTuner.UI/Assets/PresentMon/PresentMon-x64.exe `
    src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj
```

---

## Task 10: DI registration + orphan-CSV cleanup at startup

Wire `IPresentMonRunner`, `IFrameSessionStore`, `FrameRecordingService` into the host container; resolve the bundled binary's path; clean up orphan CSVs from prior crashed sessions.

**Files:**
- Modify: `src/PrimeOSTuner.UI/App.xaml.cs`

- [ ] **Step 10.1: Add using directives**

At the top of `src/PrimeOSTuner.UI/App.xaml.cs`, add:

```csharp
using PrimeOSTuner.Core.Performance;
using PrimeOSTuner.Win.Performance;
```

- [ ] **Step 10.2: Register the services**

Find the existing `// Sentinel — passive performance watcher` block in `ConfigureServices`. Immediately **after** it (before the `// Lifecycle` comment), insert:

```csharp
                // PresentMon frame-time recording
                var presentMonBinaryPath = Path.Combine(
                    AppContext.BaseDirectory, "Assets", "PresentMon", "PresentMon-x64.exe");
                var framesDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "PrimeOSTuner", "frames");

                s.AddSingleton<IPresentMonRunner>(_ => new PresentMonRunner(presentMonBinaryPath));
                s.AddSingleton<IFrameSessionStore>(_ => new FrameSessionStore(FrameSessionStore.DefaultPath()));
                s.AddSingleton<FrameRecordingService>(sp =>
                    new FrameRecordingService(
                        sp.GetRequiredService<IPresentMonRunner>(),
                        sp.GetRequiredService<IFrameSessionStore>(),
                        framesDir));
```

- [ ] **Step 10.3: Pass the recorder into the ProfileLifecycleService factory**

Find the existing `ProfileLifecycleService` factory registration. Update the `return new ProfileLifecycleService(...)` call's argument list to include the recorder as the 8th argument:

```csharp
                    return new ProfileLifecycleService(
                        sp.GetRequiredService<IGameProcessWatcher>(),
                        sp.GetRequiredService<GameProfileStore>(),
                        sp.GetRequiredService<ActiveTweaksStore>(),
                        dict,
                        sp.GetRequiredService<ProfileApplier>(),
                        sp.GetRequiredService<IBackgroundSuspenderService>(),
                        sp.GetRequiredService<ISentinelService>(),
                        sp.GetRequiredService<FrameRecordingService>());
```

- [ ] **Step 10.4: Orphan-CSV cleanup at startup**

In `OnStartup`, **after** `Host.Start();` (so DI is alive) and **before** `lifecycle.Start();`, add:

```csharp
        TryCleanupOrphanFrameCsvs();
```

Then add the method to the App class (anywhere in the body, e.g. after `OnExit`):

```csharp
    private static void TryCleanupOrphanFrameCsvs()
    {
        try
        {
            var framesDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PrimeOSTuner", "frames");
            if (!Directory.Exists(framesDir)) return;

            var cutoff = DateTime.UtcNow.AddHours(-24);
            foreach (var file in Directory.EnumerateFiles(framesDir, "*.csv"))
            {
                try
                {
                    if (File.GetLastWriteTimeUtc(file) < cutoff) File.Delete(file);
                }
                catch { /* best effort */ }
            }
        }
        catch { /* never block app startup */ }
    }
```

- [ ] **Step 10.5: Build + tests**

```powershell
dotnet build PrimeOSTuner.sln --nologo -v minimal
dotnet test src/PrimeOSTuner.Tests/PrimeOSTuner.Tests.csproj --nologo -v minimal
```

Expected: build clean; all tests pass.

- [ ] **Step 10.6: Commit**

```powershell
git add -- src/PrimeOSTuner.UI/App.xaml.cs
git commit -m "feat(perf): DI registration + orphan-CSV cleanup at startup" -- `
    src/PrimeOSTuner.UI/App.xaml.cs
```

---

## Task 11: FrameSessionVm + DashboardViewModel extension

Add a per-session view-model and an `ObservableCollection<FrameSessionVm>` on the dashboard VM, refreshed via the store's `Updated` event.

**Files:**
- Create: `src/PrimeOSTuner.UI/ViewModels/FrameSessionVm.cs`
- Modify: `src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs`

- [ ] **Step 11.1: Create FrameSessionVm**

`src/PrimeOSTuner.UI/ViewModels/FrameSessionVm.cs`:

```csharp
using PrimeOSTuner.Core.Performance;

namespace PrimeOSTuner.UI.ViewModels;

public sealed class FrameSessionVm
{
    private readonly FrameSession _model;

    public FrameSessionVm(FrameSession model) { _model = model; }

    public string GameName => _model.GameName;
    public string StartedAtDisplay => _model.StartedAt.ToLocalTime().ToString("MMM d, h:mm tt");
    public string DurationDisplay => _model.Duration switch
    {
        var d when d.TotalHours >= 1 => $"{(int)d.TotalHours}h {d.Minutes}m",
        var d when d.TotalMinutes >= 1 => $"{(int)d.TotalMinutes} min",
        _ => $"{(int)_model.Duration.TotalSeconds} sec",
    };
    public string AvgFpsDisplay        => $"{_model.Stats.AvgFps:F0} FPS avg";
    public string OnePctLowDisplay     => $"1% low: {_model.Stats.OnePctLowFps:F0} FPS";
    public string ZeroPointOnePctDisplay => $"0.1% low: {_model.Stats.ZeroPointOnePctLowFps:F0} FPS";
}
```

- [ ] **Step 11.2: Locate DashboardViewModel**

First read `src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs` to see the existing shape (constructor parameters, fields, observable properties). You'll be adding:

- A new field `private readonly IFrameSessionStore _frameStore;`
- A new constructor parameter `IFrameSessionStore frameStore` (add it to the end of the existing parameter list)
- A new public `ObservableCollection<FrameSessionVm> RecentSessions { get; } = new();`
- A `RefreshSessions()` private method that clears + repopulates from `_frameStore.Load()`
- In the ctor body, subscribe: `_frameStore.Updated += (_, _) => Application.Current?.Dispatcher.BeginInvoke(RefreshSessions);`
- Call `RefreshSessions()` once at the end of the ctor so the initial state is populated.

- [ ] **Step 11.3: Apply the DashboardViewModel changes**

Add the using directives at the top of the file:

```csharp
using System.Collections.ObjectModel;
using System.Windows;
using PrimeOSTuner.Core.Performance;
```

Add the field, ctor parameter, property, and helper. Where the existing ctor body sets fields, append `_frameStore = frameStore;`. At the end of the ctor body (after existing initialization), append:

```csharp
        _frameStore.Updated += (_, _) => Application.Current?.Dispatcher.BeginInvoke(RefreshSessions);
        RefreshSessions();
```

Add the property next to other observable members:

```csharp
    public ObservableCollection<FrameSessionVm> RecentSessions { get; } = new();
```

Add the helper somewhere in the class body:

```csharp
    private void RefreshSessions()
    {
        RecentSessions.Clear();
        foreach (var s in _frameStore.Load()) RecentSessions.Add(new FrameSessionVm(s));
    }
```

- [ ] **Step 11.4: Verify build**

```powershell
dotnet build src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj --nologo -v minimal
```

Expected: build clean. (DI for `IFrameSessionStore` is already registered in Task 10; resolver will inject it into `DashboardViewModel` automatically.)

- [ ] **Step 11.5: Commit**

```powershell
git add -- src/PrimeOSTuner.UI/ViewModels/FrameSessionVm.cs `
            src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs
git commit -m "feat(perf): DashboardViewModel exposes RecentSessions backed by FrameSessionStore" -- `
    src/PrimeOSTuner.UI/ViewModels/FrameSessionVm.cs `
    src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs
```

---

## Task 12: DashboardView — PERFORMANCE section

A new section below the existing tile grid. `ItemsControl` of `FrameSessionVm` rendered as horizontal-wrapping cards (220 × 120). Empty state: "No game sessions recorded yet — launch a game from your Library."

**Files:**
- Modify: `src/PrimeOSTuner.UI/Views/DashboardView.xaml`

- [ ] **Step 12.1: Read the Dashboard XAML**

Read the file to identify the end of the tile-grid `WrapPanel` (closing `</WrapPanel>` tag inside the outer `StackPanel`). The new section goes immediately after it, still inside the outer `StackPanel`.

- [ ] **Step 12.2: Add the PERFORMANCE section**

Append this block to `src/PrimeOSTuner.UI/Views/DashboardView.xaml`, **inside the outer `<StackPanel>`** and **after the existing tile-grid `</WrapPanel>`** (and after any sibling `Border` blocks that follow it, just before the closing `</StackPanel>`):

```xml
            <!-- PERFORMANCE — recent frame-time recordings -->
            <TextBlock Text="PERFORMANCE"
                       Style="{StaticResource SectionLabel}"
                       Margin="0,24,0,8"/>
            <TextBlock Text="Recent gaming sessions captured by PresentMon. Apply a tweak, play again, compare the cards."
                       Foreground="{StaticResource Text3Brush}"
                       FontSize="11" Margin="0,0,0,12"
                       TextWrapping="Wrap"/>

            <!-- Empty state -->
            <TextBlock Text="No game sessions recorded yet — launch a game from your Library."
                       Foreground="{StaticResource Text3Brush}"
                       FontStyle="Italic" FontSize="12">
                <TextBlock.Style>
                    <Style TargetType="TextBlock">
                        <Setter Property="Visibility" Value="Collapsed"/>
                        <Style.Triggers>
                            <DataTrigger Binding="{Binding RecentSessions.Count}" Value="0">
                                <Setter Property="Visibility" Value="Visible"/>
                            </DataTrigger>
                        </Style.Triggers>
                    </Style>
                </TextBlock.Style>
            </TextBlock>

            <ItemsControl ItemsSource="{Binding RecentSessions}">
                <ItemsControl.ItemsPanel>
                    <ItemsPanelTemplate>
                        <WrapPanel/>
                    </ItemsPanelTemplate>
                </ItemsControl.ItemsPanel>
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Style="{StaticResource CardBorder}"
                                Width="220" Height="120" Margin="0,0,12,12">
                            <Grid>
                                <Grid.RowDefinitions>
                                    <RowDefinition Height="Auto"/>
                                    <RowDefinition Height="*"/>
                                    <RowDefinition Height="Auto"/>
                                </Grid.RowDefinitions>

                                <TextBlock Grid.Row="0"
                                           Text="{Binding GameName}"
                                           FontWeight="SemiBold" FontSize="13"
                                           Foreground="{StaticResource Text0Brush}"
                                           TextTrimming="CharacterEllipsis"/>

                                <StackPanel Grid.Row="1" VerticalAlignment="Center" Margin="0,4">
                                    <TextBlock Text="{Binding AvgFpsDisplay}"
                                               FontFamily="Segoe UI Variable Display, Segoe UI"
                                               FontSize="22" FontWeight="Bold"
                                               Foreground="{StaticResource AccentBrush}"/>
                                    <TextBlock Text="{Binding OnePctLowDisplay}"
                                               Foreground="{StaticResource Text2Brush}"
                                               FontSize="11"/>
                                    <TextBlock Text="{Binding ZeroPointOnePctDisplay}"
                                               Foreground="{StaticResource Text3Brush}"
                                               FontSize="11"/>
                                </StackPanel>

                                <Grid Grid.Row="2">
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width="*"/>
                                        <ColumnDefinition Width="Auto"/>
                                    </Grid.ColumnDefinitions>
                                    <TextBlock Grid.Column="0"
                                               Text="{Binding StartedAtDisplay}"
                                               Foreground="{StaticResource Text3Brush}"
                                               FontSize="10"/>
                                    <TextBlock Grid.Column="1"
                                               Text="{Binding DurationDisplay}"
                                               Foreground="{StaticResource Text3Brush}"
                                               FontSize="10"/>
                                </Grid>
                            </Grid>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
```

- [ ] **Step 12.3: Build**

```powershell
dotnet build src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj --nologo -v minimal
```

Expected: build clean.

- [ ] **Step 12.4: Commit**

```powershell
git add -- src/PrimeOSTuner.UI/Views/DashboardView.xaml
git commit -m "feat(perf): PERFORMANCE section on Dashboard" -- src/PrimeOSTuner.UI/Views/DashboardView.xaml
```

---

## Task 13: Manual verification

No automated test can prove that PresentMon actually records frame times against a real game. Run the app, launch a Library game briefly, exit, and confirm a card appears.

**Files:** none modified.

- [ ] **Step 13.1: Build + launch**

```powershell
Stop-Process -Name "PrimeOSTuner.UI" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 600
dotnet build src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj --nologo -v minimal
Start-Process "src\PrimeOSTuner.UI\bin\Debug\net9.0-windows\PrimeOSTuner.UI.exe"
```

- [ ] **Step 13.2: Confirm the empty-state copy on first launch**

Open the Dashboard. Scroll past the tile grid. Expected:
- A "PERFORMANCE" section header
- The explanatory subtitle
- The empty-state message: "No game sessions recorded yet — launch a game from your Library."

- [ ] **Step 13.3: Run a known-good game**

Launch a Library game that you know presents frames (any actual game — not just a launcher). Play for **at least 30 seconds** so PresentMon captures enough samples to pass the `SampleCount < 10` floor in `FrameRecordingService.OnGameStoppedAsync`.

- [ ] **Step 13.4: Confirm the session card appears**

Close the game. Within ~5 seconds, return to the PrimeOS Tuner Dashboard. Expected:
- The empty-state message is gone.
- A new card has appeared in the PERFORMANCE section showing the game's name, an avg-FPS number in the accent color, 1% low and 0.1% low values, the timestamp, and the duration.

- [ ] **Step 13.5: Confirm the CSV was cleaned up**

```powershell
Get-ChildItem "$env:LOCALAPPDATA\PrimeOSTuner\frames" -ErrorAction SilentlyContinue
```

Expected: empty (or no folder at all). The per-session CSV should have been deleted in `FrameRecordingService.OnGameStoppedAsync`'s finally block.

- [ ] **Step 13.6: Confirm the JSON store has the session**

```powershell
Get-Content "$env:LOCALAPPDATA\PrimeOSTuner\frame-sessions.json"
```

Expected: a JSON array with one object containing `GameId`, `GameName`, `StartedAt`, `Duration`, and a `Stats` block.

---

## Spec coverage check

| Spec requirement | Covered by |
| --- | --- |
| Goal: capture frame-time data via PresentMon, show summary on Dashboard | Tasks 5, 7, 11, 12 |
| Bundle PresentMon-x64.exe, MIT license, MSBuild copies to output | Task 9 |
| `FrameSession` / `FrameSessionStats` records (Core.Performance namespace) | Task 1 |
| `FrameTimeStatsCalculator.Compute` (pure, tested) | Task 2 |
| `FrameTimeParser` (tolerant, tested with fixtures) | Task 3 |
| `IPresentMonRunner` abstraction + `PresentMonRunner` concrete | Tasks 4, 5 |
| `FrameSessionStore` (JSON, capped at 50, atomic write, Updated event) | Task 6 |
| `FrameRecordingService` orchestrator with failure isolation | Task 7 |
| Lifecycle wire-up (`ProfileLifecycleService` calls recorder.OnGameStarted/Stopped) | Task 8 |
| DI registrations + orphan-CSV cleanup at startup | Task 10 |
| `DashboardViewModel.RecentSessions` + `FrameSessionVm` | Task 11 |
| `DashboardView.xaml` PERFORMANCE section with empty state | Task 12 |
| Failure handling: missing binary, empty CSV, malformed rows, crash recovery | Tasks 5, 7, 10 |
| Storage: `%LocalAppData%/PrimeOSTuner/frame-sessions.json`, atomic, capped | Task 6 |
| Manual verification | Task 13 |
| Out-of-scope: live chart, formal compare UI, raw CSV retention | (nothing implemented — correct) |
