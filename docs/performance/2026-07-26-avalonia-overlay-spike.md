# Avalonia overlay spike — findings

**Date:** 2026-07-26
**Purpose:** De-risk the v0.8b migration before committing to it. The click-through
performance overlay was the one component where Avalonia might genuinely have blocked us.

## Result: PASSED

```
PASS  1. HWND obtained: 0x7820E68 (descriptor 'HWND')
      exstyle before=0x00200008 after=0x082800A8
PASS  2/3. Click-through styles applied and read back intact
PASS  3b. Click-through removable (edit mode works)
PASS  4. RegisterHotKey(Ctrl+Shift+O) succeeded on the Avalonia HWND

RESULT: SPIKE PASSED — Avalonia can host the overlay.
```

## What was verified

| # | Question | Answer |
| --- | --- | --- |
| 1 | Does an Avalonia `Window` expose a real Win32 `HWND`? | Yes — `TryGetPlatformHandle()` returns a handle with descriptor `HWND`. |
| 2 | Do `WS_EX_TRANSPARENT \| LAYERED \| TOOLWINDOW \| NOACTIVATE` apply? | Yes — `SetWindowLong(GWL_EXSTYLE, …)` succeeds. |
| 3 | Do those styles actually stick? | Yes — read back via `GetWindowLong`, all four bits present (`0x082800A8`). |
| 3b | Can click-through be cleared again for overlay edit mode? | Yes — clearing `WS_EX_TRANSPARENT` works, so drag-to-reposition is achievable. |
| 4 | Can a global hotkey be registered on that HWND? | Yes — `RegisterHotKey(Ctrl+Shift+O)` succeeded. |

Both Windows-only platform services the migration depends on — the click-through overlay
and the global hotkey — work on an Avalonia window using the same interop the WPF build
already uses. The interop code ports across essentially unchanged.

## Environment notes for the plan

- **SDK:** 9.0.313. The `dotnet new avalonia.app` template scaffolds `net10.0`, which this
  SDK cannot build. The new project must be retargeted to `net9.0-windows` manually.
- **Avalonia version:** 12.1.0 (current on nuget.org).
- **`Avalonia.Templates`** was not installed; it is now.
- **Template extras to drop:** the scaffold references `AvaloniaUI.DiagnosticsSupport`
  (`.WithDeveloperTools()`) and `Avalonia.Fonts.Inter` (`.WithInterFont()`). Removing either
  package without removing its `AppBuilder` call is a compile error.
- **`Window.SystemDecorations` is obsolete** in Avalonia 12 — use `WindowDecorations`.

## Still unverified

The spike proves the window *accepts* click-through; it does not prove the overlay is
visually correct over a running game. That needs a real game and a human, same as the WPF
build did.

## Reproduction

The spike project lives outside the repo (scratchpad, disposable). To recreate: an Avalonia
app targeting `net9.0-windows`, a borderless transparent topmost window, and the interop in
`MainWindow.OnOpened` shown above.
