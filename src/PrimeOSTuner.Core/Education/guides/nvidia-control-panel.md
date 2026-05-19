---
id: nvidia-control-panel
title: Configure the NVIDIA Control Panel for gaming
category: GPU Driver
difficulty: Beginner
risk: Low
time: 10 minutes, no restart
---
## What this does

The NVIDIA driver has global 3D settings that affect every game. The defaults are conservative and are not tuned for gaming latency or performance. A few key changes give real, measurable improvements.

## Expected benefit

- Lower input latency, roughly 1-5 ms depending on the game
- Slightly higher and more consistent FPS in some titles
- Better behaviour on G-Sync displays

## Prerequisites

- An NVIDIA GPU with current drivers
- Open the NVIDIA Control Panel by right-clicking the desktop and choosing NVIDIA Control Panel

## Steps

1. Go to Manage 3D Settings, then the Global Settings tab.
2. Set **Low Latency Mode** to Ultra (or On for older drivers). This forces frames to render just in time and shortens the render queue.
3. Set **Power management mode** to Prefer maximum performance. This keeps the GPU at higher clocks and removes clock-ramp delay.
4. Set **Texture filtering quality** to High performance. A minor visual difference for a small FPS gain.
5. Set **Vertical sync** to Off. You handle this per-game with G-Sync instead.
6. Leave **Threaded optimization** on Auto. Do not force it on or off globally - some games dislike it being forced.
7. Click Apply.
8. Set up G-Sync. In the left sidebar open Display, then Set up G-SYNC. Enable G-SYNC and check "Enable for windowed and full screen mode".
9. Optional but recommended: for competitive titles, use the Program Settings tab, add the game's exe, override Low Latency Mode to Ultra, and set Max Frame Rate to your refresh rate minus 3 so G-Sync stays engaged.

## How to verify it worked

- The settings should persist after you close and reopen the Control Panel.
- Low Latency Mode mostly shows up as snappier input rather than a number.
- For G-Sync, turn on the indicator under Display, then "G-SYNC Compatible Indicator" - an overlay appears in-game when G-Sync is active.

## How to revert

- In Manage 3D Settings there is a Restore button at the bottom right of the Global Settings page. It resets everything to driver defaults.

## Common pitfalls

- Prefer maximum performance keeps the GPU at higher idle clocks and uses slightly more power. Trivial on desktops, more noticeable on laptops.
- Low Latency Mode Ultra can rarely cause stutters in CPU-bottlenecked games. If you see new stutters, drop it to On or Off for that game.
- Do not enable Image Sharpening globally - it can affect HUD and UI text quality. Use it per-game instead.
