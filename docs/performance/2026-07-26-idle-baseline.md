# Crustcut idle resource baseline

**Date:** 2026-07-26
**Build:** v0.8a branch (`v0.8a-memory-safety`), post memory-safety fix
**Status:** ⏳ **Awaiting measurement** — see "How to run" below

## Why this exists

The user reported believing Crustcut itself degrades system performance. This document
settles that with numbers rather than guesses. No code will be "optimized" on the strength
of a hunch: if the measurements exonerate a suspect, that suspect is closed.

## How to run

Crustcut must be running and elevated (it self-elevates, so launching it raises a UAC
prompt). Leave it on the Overview tab and do not interact with it while sampling.

```powershell
.\scripts\measure-idle.ps1 -Minutes 10
```

The script samples every 5 seconds and writes `idle-samples.csv`, then prints the average
and maximum for each metric.

## Thresholds

| Metric | Reading | Verdict |
| --- | --- | --- |
| CPU | sustained > 2% at idle | investigate |
| CPU | sustained > 5% at idle | real problem |
| Private bytes | steady upward slope across the run | leak |
| Private bytes | plateau | healthy |
| Handles / threads | monotonic growth | leak, regardless of absolute value |

## Results

| Metric | Average | Maximum | Verdict |
| --- | --- | --- | --- |
| CPU % | _not yet measured_ | | |
| Private MB | _not yet measured_ | | |
| Working set MB | _not yet measured_ | | |
| Handles | _not yet measured_ | | |
| Threads | _not yet measured_ | | |

**Reading:** _to be written once the run completes._

## Suspects, in priority order

These are hypotheses to check **against the measurements**, not defects. Each is closed
explicitly if the numbers do not support it.

1. **`HardwareClient.SampleGpuPercent`** — re-enumerates every GPU performance-counter
   instance on every sample. This was a deliberate fix (counters for processes launched
   after startup were being missed), but it is the most expensive thing on the sampling
   path. Expect this to dominate CPU if anything does.
2. **`WmiProcessWatcher`** — uses `Win32_ProcessStartTrace`, a known-expensive WMI event
   source. Historically it has also thrown `ManagementException: Access denied` on
   restricted systems.
3. **`GameProcessWatcher`** — polls on a two-second timer. It already has a 20-second scan
   cache and a re-entrancy guard, so it is the least likely culprit of the three.

## Related finding (already fixed on this branch)

While implementing the memory-safety work, `ProcessClientTests` was found to call
`ProcessClient.TrimAllUserProcesses()` for real. **Every run of the test suite trimmed the
working set of every process on the machine, VS Code included.** If the user ran the suite
during development, that alone would have produced exactly the stalls they reported. The
test and the underlying method have both been deleted.
