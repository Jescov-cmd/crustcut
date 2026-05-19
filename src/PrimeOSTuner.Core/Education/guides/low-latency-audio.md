---
id: low-latency-audio
title: Set up audio for low latency
category: Audio
difficulty: Beginner
risk: Low
time: 5 minutes
---
## What this does

Windows applies "audio enhancements" to your output device, and the playback sample rate is not always matched to what games and media actually use. Turning enhancements off and setting a sensible sample rate gives cleaner audio with slightly less processing delay.

## Expected benefit

- A small reduction in audio latency, and sometimes cleaner sound
- Most noticeable in competitive games and for music - it is not an FPS tweak
- Effect varies by hardware; some onboard audio barely changes

## Prerequisites

- None.

## Steps

1. Right-click the speaker icon in the taskbar and choose **Sound settings**.
2. Click your output device to open its properties.
3. Turn **Audio enhancements** off. On older Windows builds this is an **Enhancements** tab with a "Disable all enhancements" box.
4. Set the **Format** (sample rate) to **24-bit, 48000 Hz** - a safe match for most games, video, and music.
5. Apply the changes.

## How to verify it worked

- Play any audio - it should still work normally, and the settings stay after a reboot.

## How to revert

- Turn Audio enhancements back on, and set the Format back to its previous value if you changed it.

## Common pitfalls

- Some gaming headsets use an enhancement for virtual surround sound. If you lose a feature you actually wanted, turn just that one back on.
- "Exclusive mode" options let one app take full control of the device. Leave them alone unless you specifically know you need them - they can stop other apps from playing sound.
