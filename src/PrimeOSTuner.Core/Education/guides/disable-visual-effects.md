---
id: disable-visual-effects
title: Trim Windows visual effects
category: Windows
difficulty: Beginner
risk: Low
time: 3 minutes
---
## What this does

Windows draws animations, window shadows, and transparency effects. Turning the heaviest ones off frees a small amount of CPU and GPU work and can make the desktop feel more responsive.

## Expected benefit

- Honest: the effect is tiny on a modern gaming PC
- More noticeable on low-end hardware, older PCs, and laptops
- This is mostly a "feels snappier" change, not an FPS change

## Prerequisites

- None.

## Steps

1. Press **Windows key + R**, type `sysdm.cpl`, and press Enter.
2. Go to the **Advanced** tab.
3. Under Performance, click **Settings**.
4. Choose **Adjust for best performance** to turn everything off, or choose **Custom** and untick the effects you do not want.
5. If you used "best performance", tick **Smooth edges of screen fonts** back on - text looks rough without it.
6. Click Apply, then OK.

## How to verify it worked

- Window minimise/maximise animations are gone, and the change stays after a reboot.

## How to revert

- Open the same dialog and choose **Let Windows choose what's best for my computer**, or **Adjust for best appearance**.

## Common pitfalls

- "Adjust for best performance" also disables font smoothing. Always re-tick "Smooth edges of screen fonts" or text becomes unpleasant to read.
- On a capable gaming PC the performance gain is very small - do not expect more FPS from this.
