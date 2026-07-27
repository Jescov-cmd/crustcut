# Crustcut idle resource baseline

**Date:** 2026-07-26
**Build:** fresh Release publish from `main` post memory-safety fix (`publish/v0.8a-test`)
**Sampling:** 120 samples over 10 minutes, 5-second interval, app idle in tray, PID 11712
**Status:** ✅ **Measured — Crustcut is not the cause of system slowdown**

## Results

| Metric | Average | Maximum | Start → End | Verdict |
| --- | --- | --- | --- | --- |
| CPU % | **0.48** | 4.31 | — | Healthy — well under the 2% threshold |
| Private MB | 199.1 | 221.4 | 72.6 → 207.8 | Warm-up then flat — no leak |
| Handles | 1307.8 | 1371 | 993 → 1328 | Oscillating, not climbing — no leak |
| Threads | 22.8 | 33 | 25 → 22 | Stable |

## Reading

**CPU is a non-issue.** Averaging under half a percent at idle, peaking at 4.3% on a single
5-second sample. Crustcut is not consuming meaningful CPU.

**The memory growth is warm-up, not a leak.** Private bytes climb from 72 MB to ~208 MB
during the first ~75 seconds, then sit flat for the remaining nine minutes:

```
sample   1:  72.59 MB,  993 handles
sample  16: 208.06 MB, 1356 handles
sample  31: 208.37 MB, 1359 handles
sample  46: 207.63 MB, 1328 handles
sample  61: 208.61 MB, 1332 handles
sample  76: 208.53 MB, 1319 handles
sample  91: 207.60 MB, 1318 handles
sample 106: 207.84 MB, 1328 handles
```

A leak produces a steady upward slope across the whole run. This is a step to a plateau —
the shape of one-time initialisation (performance counters, the game-library scan cache,
cover-art loading) reaching steady state. Handles behave identically: a rise to ~1330, then
oscillation within a narrow band rather than monotonic growth.

~208 MB resident is on the heavy side for a tray utility and worth revisiting, but it is
**stable**, and stable memory does not slow a machine down.

## Suspects — all closed

| Suspect | Verdict |
| --- | --- |
| `HardwareClient.SampleGpuPercent` re-enumerating GPU counters every sample | **Closed.** Would show as sustained CPU. Measured 0.48% average. |
| `WmiProcessWatcher` using `Win32_ProcessStartTrace` | **Closed.** No CPU cost and no handle growth over 10 minutes. |
| `GameProcessWatcher` 2-second poll | **Closed.** Already cached and re-entrancy guarded; invisible in the numbers. |

No code will be changed on the strength of these hypotheses. The measurements exonerate
all three.

## What was actually causing the slowdown

The RAM cleaner, not the app's own footprint. Three findings, all fixed on `main`:

1. **`RamAutoOptimizeOnInterval: true` with `RamAutoIntervalMinutes: 2`** in the user's
   settings — the cleaner was running **every two minutes, continuously**, roughly 30 times
   an hour. This was the dominant cause, and it was not visible from the code alone.
2. **`TrimAllUserProcesses()`** trimmed every process on the system with no protection at
   all, so the manual tile was more damaging than the automatic path.
3. **`EmptyStandbyList()` / `FlushFileCache()`** purged the machine-wide standby list and
   file cache on every run, forcing every process to re-read from storage.

Additionally, `ProcessClientTests` invoked `TrimAllUserProcesses()` for real, so **every run
of the test suite** trimmed every process on the developer machine.

## How to re-run

```powershell
.\scripts\measure-idle.ps1 -Minutes 10
```

Thresholds: CPU sustained >2% warrants investigation and >5% is a real problem; private
bytes sloping upward across the whole run indicates a leak, whereas a plateau does not;
handle or thread growth is a leak regardless of absolute value.
