# Performance — PresentMon frame-time recording

**Status:** approved — ready for implementation plan
**Date:** 2026-05-26
**Author:** Claude (working under PrimeOS Tuner v0.4 honest-optimization rules)

## Goal

While a Library game is running, **silently record real frame-time data**
using Intel's PresentMon. When the game exits, compute a summary
(average FPS, 1% low, 0.1% low, max frame-time, duration) and surface it
as a card in a new **PERFORMANCE** section on the Dashboard. Users apply
a tweak, play, compare the resulting session card to a previous one, and
see actual evidence of whether the tweak helped.

## Non-goals (v1)

- **No live chart** while a game is running. Post-session summary only.
- **No formal compare UI.** Side-by-side cards on the Dashboard is enough —
  users eyeball it.
- **No raw CSV retention.** Parse PresentMon's output once on game exit,
  store the computed stats, delete the CSV.
- **No new nav tab.** The user explicitly didn't want another tab. The
  feature lives as a new section on the Dashboard.
- **No per-process attribution.** PresentMon already targets the
  specific game pid; we don't decompose by sub-process.
- **No GPU / CPU stats** — Sentinel already covers those.

## Bundling PresentMon

Intel's PresentMon (https://github.com/GameTechDev/PresentMon) ships
under the MIT license — safe to redistribute. We bundle the latest
stable `PresentMon-2.x.x-x64.exe` in the repo at:

```
src/PrimeOSTuner.UI/Assets/PresentMon/PresentMon-x64.exe
```

The `.csproj` copies it to the publish output (`PreserveNewest`). At
runtime we resolve the binary path via `AppContext.BaseDirectory`.

PresentMon needs admin privileges to read ETW events. The app already
runs as administrator, so this is free.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│  Dashboard view                                                      │
│  ┌─ Existing tile grid (Boost Score, Optimize Now, …) ──┐           │
│  └───────────────────────────────────────────────────────┘           │
│  ┌─ PERFORMANCE section (new) ──────────────────────────┐           │
│  │  [card] [card] [card] [card] …  newest first         │           │
│  └───────────────────────────────────────────────────────┘           │
└─────────────────────────────────────────────────────────────────────┘
                         ▲ binds to
                ┌────────┴──────────────────┐
                │ DashboardViewModel        │  ← +RecentSessions collection
                └────────┬──────────────────┘
                         │ subscribes to Updated event
                ┌────────┴──────────────────┐
                │ FrameSessionStore         │  ← JSON list of recent N sessions
                └────────┬──────────────────┘
                         ▲ saves
                ┌────────┴──────────────────┐
                │ FrameRecordingService     │  ← orchestrator
                └────┬──────────────┬───────┘
                     │              │
            ┌────────┴────┐  ┌──────┴────────┐
            │ IPresentMon-│  │ FrameTime-    │
            │   Runner    │  │   Parser      │  (pure CSV → samples)
            └────────┬────┘  └───────────────┘
                     │
            PresentMon-x64.exe (bundled)
                     │
            CSV in %LocalAppData%\PrimeOSTuner\frames\<game>-<timestamp>.csv
            (deleted after successful parse)
```

### Lifecycle wire-up

`FrameRecordingService` subscribes to the same
`ProfileLifecycleService.GameStarted` / `GameStopped` events that
`ISentinelService` consumes (commit `bb25a7e`). On `GameStarted`: spawn
PresentMon targeting the game's pid → CSV path. On `GameStopped`: kill
the PresentMon process, parse the CSV, compute stats, persist a
`FrameSession`, delete the CSV.

Recording is **independent of Sentinel** — both observe the same
lifecycle events but neither depends on the other.

## Components

### `PrimeOSTuner.Core.Performance`

| Type | Responsibility |
| --- | --- |
| `FrameSession` | Persistent record: `string GameId`, `string GameName`, `DateTime StartedAt`, `TimeSpan Duration`, `FrameSessionStats Stats`. |
| `FrameSessionStats` | Computed stats: `double AvgFps`, `double P50FrameTimeMs`, `double P99FrameTimeMs`, `double P999FrameTimeMs`, `double MaxFrameTimeMs`, `int SampleCount`. |
| `FrameTimeParser` (static) | Pure parser. Input: path to PresentMon CSV. Output: `IReadOnlyList<double>` of frame-time samples in milliseconds. Reads the `msBetweenPresents` (or equivalent) column; tolerant of header variations across PresentMon versions. |
| `FrameSessionStats.Compute(IReadOnlyList<double>)` | Pure: takes raw frame-time samples, returns a `FrameSessionStats`. |
| `IPresentMonRunner` | `Task<string?> StartAsync(int pid, string outputCsvPath, CancellationToken)` — spawns PresentMon, returns the CSV path it's writing to (or null on failure). `Task StopAsync(CancellationToken)` — kills the subprocess. |
| `FrameSessionStore` | JSON-backed list (`%LocalAppData%\PrimeOSTuner\frame-sessions.json`). Capped at 50 most-recent entries. Atomic write via temp + Move. Raises `Updated` event so the VM can refresh. |
| `FrameRecordingService` | Orchestration. Hooks `ProfileLifecycleService` events, owns the per-session CSV path, calls runner + parser + store. Failure-isolated (recording must never break a game launch or exit). |

### `PrimeOSTuner.Win.Performance`

| Type | Responsibility |
| --- | --- |
| `PresentMonRunner` | Concrete `IPresentMonRunner`. Launches the bundled `PresentMon-x64.exe` with: `-process_id <pid> -output_file <csv> -no_csv_for_processes_with_no_present_events -terminate_on_proc_exit -stop_existing_session`. On `StopAsync`, sends Ctrl+C / kills the process gracefully. |

### `PrimeOSTuner.UI`

| Type | Responsibility |
| --- | --- |
| `DashboardViewModel` | Gains `ObservableCollection<FrameSessionVm> RecentSessions`. Subscribes to `FrameSessionStore.Updated` (dispatched onto the WPF dispatcher). |
| `FrameSessionVm` | View-model wrapper around `FrameSession` with display strings: `"87 FPS avg"`, `"1% low: 54 FPS"`, `"played 32 min"`. |
| `DashboardView.xaml` | New `PERFORMANCE` section below the existing tile grid. `ItemsControl` of `FrameSessionVm` rendered as horizontal-wrapping cards (220 × 120). Newest first. Empty state: "No game sessions recorded yet — launch a game from your Library." |

## Data flow walkthrough

User launches Cyberpunk 2077 from the Library:

1. `GameProcessWatcher` detects the process → `ProfileLifecycleService.GameStarted` fires.
2. `ProfileLifecycleService` applies the profile, fires the suspender, calls `ISentinelService.OnGameStarted`, AND (new) calls `FrameRecordingService.OnGameStarted(game, pid)`.
3. `FrameRecordingService` builds an output path: `%LocalAppData%\PrimeOSTuner\frames\cyberpunk-2026-05-26T13-04-22.csv`. Calls `IPresentMonRunner.StartAsync(pid, path, ct)`.
4. `PresentMonRunner` spawns `PresentMon-x64.exe` as a subprocess. PresentMon begins writing one row per frame to the CSV.
5. (User plays the game for 32 minutes.)
6. User exits the game → `GameProcessWatcher` fires `GameStopped`.
7. `ProfileLifecycleService` reverts tweaks, resumes background apps, tells Sentinel, AND calls `FrameRecordingService.OnGameStopped(game)`.
8. `FrameRecordingService` calls `IPresentMonRunner.StopAsync()` to terminate PresentMon (PresentMon also self-terminates on game exit thanks to `-terminate_on_proc_exit`).
9. `FrameTimeParser` reads the CSV. If empty or unparseable → log warning, skip the session (no card appears).
10. `FrameSessionStats.Compute` turns samples → stats.
11. `FrameRecordingService` builds a `FrameSession` and calls `FrameSessionStore.SaveAsync`. Deletes the CSV.
12. Store raises `Updated` → VM re-reads → new card appears on the Dashboard.

## Failure handling

| Failure | Behavior |
| --- | --- |
| PresentMon binary missing from output | `PresentMonRunner.StartAsync` returns `null`, FrameRecordingService logs warning, does nothing. App keeps running. |
| PresentMon exits with non-zero code | Treat as "no recording made." Don't save a session card. |
| CSV is empty or has < 10 samples | The game was too short or PresentMon didn't capture anything. Don't save. |
| CSV is malformed (header mismatch, unparseable rows) | Parser returns empty list; service skips the session. Log a one-line warning. |
| Game lifecycle fires `GameStopped` for a game we never started recording | Service no-ops. |
| App crashes mid-recording | Orphan CSV sits in `%LocalAppData%\PrimeOSTuner\frames\`. On next app start, `FrameRecordingService` scans and deletes any CSVs older than 24 h. |
| User has Sentinel disabled | Has no effect on recording. The two features are independent. |

Recording's prime directive: **never break a game launch or exit**. Every entry point is wrapped in try/catch — same pattern as the existing Sentinel + suspender wiring.

## Storage

- Recent-sessions JSON at `%LocalAppData%\PrimeOSTuner\frame-sessions.json`.
- Capped at **50 most-recent** sessions. Older entries are dropped on save.
- Atomic write: serialize → `.tmp` file → `File.Move(tmp, real, overwrite: true)`.
- Format: a top-level array of `FrameSession` objects.

## Testing strategy

| Layer | How tested |
| --- | --- |
| `FrameTimeParser` | TDD with fixture CSVs in `tests/Fixtures/presentmon/` covering: PresentMon 2.x default format, an empty file, a file with only the header, malformed rows mixed with good rows. |
| `FrameSessionStats.Compute` | Pure function, parameterized xUnit tests. Includes the edge case of a single-sample input. |
| `FrameSessionStore` | Tested with a temp directory: save → load round-trip, capping at 50, atomic write doesn't corrupt on simulated I/O failure. |
| `FrameRecordingService` | Mocked `IPresentMonRunner` + `FrameSessionStore`; verifies the GameStarted → Start path, GameStopped → Stop+Parse+Save+Delete path, and that failures stay silent. |
| `PresentMonRunner` | Smoke test on a dev box; not unit-tested. |
| `DashboardViewModel` | Verifies it subscribes to `Updated`, re-reads the store, marshals to the dispatcher. |

Target: ~15 new passing tests, taking the suite from 249 to ~264.

## Files

**Create:**
- `src/PrimeOSTuner.Core/Performance/FrameSession.cs`
- `src/PrimeOSTuner.Core/Performance/FrameSessionStats.cs`
- `src/PrimeOSTuner.Core/Performance/FrameTimeParser.cs`
- `src/PrimeOSTuner.Core/Performance/FrameSessionStore.cs`
- `src/PrimeOSTuner.Core/Performance/IPresentMonRunner.cs`
- `src/PrimeOSTuner.Core/Performance/FrameRecordingService.cs`
- `src/PrimeOSTuner.Win/Performance/PresentMonRunner.cs`
- `src/PrimeOSTuner.UI/ViewModels/FrameSessionVm.cs`
- `src/PrimeOSTuner.UI/Assets/PresentMon/PresentMon-x64.exe` (bundled binary)
- `src/PrimeOSTuner.Tests/Performance/FrameTimeParserTests.cs`
- `src/PrimeOSTuner.Tests/Performance/FrameSessionStatsTests.cs`
- `src/PrimeOSTuner.Tests/Performance/FrameSessionStoreTests.cs`
- `src/PrimeOSTuner.Tests/Performance/FrameRecordingServiceTests.cs`
- `src/PrimeOSTuner.Tests/Fixtures/presentmon/*.csv`

**Modify:**
- `src/PrimeOSTuner.UI/Views/DashboardView.xaml` — add the PERFORMANCE section.
- `src/PrimeOSTuner.UI/ViewModels/DashboardViewModel.cs` — add `RecentSessions`.
- `src/PrimeOSTuner.Core/Lifecycle/ProfileLifecycleService.cs` — add an optional `FrameRecordingService? recorder = null` ctor parameter, call its `OnGameStarted`/`OnGameStopped` alongside the suspender + Sentinel calls.
- `src/PrimeOSTuner.UI/App.xaml.cs` — DI registrations + bundle path resolution + crash-cleanup of orphan CSVs at startup.
- `src/PrimeOSTuner.UI/PrimeOSTuner.UI.csproj` — copy `Assets/PresentMon/*` to output.

## Out of scope for v1

- Live chart while a game is running
- A formal "select two sessions, compare in detail" UI
- Frame-time histograms or scatter plots
- Filtering session list by game / date
- Long-term retention beyond 50 sessions
- Replacing PresentMon with a custom implementation
