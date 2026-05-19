---
id: enable-xmp-expo
title: Enable XMP / EXPO memory profile
category: BIOS / UEFI
difficulty: Beginner
risk: Low
time: 5 minutes, requires restart
---
## What this does

RAM ships running at slow, conservative speeds (the JEDEC standard). Your kit's advertised speed - for example DDR5-6000 - only happens when you enable its **XMP** profile (Intel branding) or **EXPO** profile (AMD branding) in the BIOS. Most people never do this and silently lose 20-30% of their memory performance.

## Expected benefit

- 5-15% FPS gain in many games - memory bandwidth matters for games
- Larger gains in 1% lows than in average framerate
- A bigger benefit on Ryzen than on Intel, since Ryzen is more memory-sensitive

## Prerequisites

- A RAM kit that advertises a speed above the JEDEC base (almost any gaming RAM)
- A motherboard that supports that speed - check the QVL list if you run a fast kit

## Steps

1. Check your current speed first. Open Task Manager, then the Performance tab, then Memory. If "Speed" shows roughly 2133, 2400, or 4800, XMP/EXPO is not enabled yet.
2. Restart and enter the BIOS (the Delete key on MSI boards).
3. Find the memory or overclocking section. On MSI it is "OC"; on ASUS it is "Ai Tweaker" or "Extreme Tweaker"; on Gigabyte it is "Tweaker".
4. Find the setting named **XMP**, **EXPO**, or **Memory Profile**, and set it to Profile 1 - the advertised speed. Profile 2 is usually a slightly slower fallback.
5. Save and exit, usually with F10.

## How to verify it worked

- Boot into Windows. In Task Manager, Performance, Memory, the "Speed" value should now match your kit's advertised speed.
- Or use CPU-Z (free): the Memory tab shows DRAM Frequency - double it for the effective speed.

## How to revert

- Go back into the BIOS, set XMP/EXPO to Disabled or Auto, then save and exit.

## Common pitfalls

- If your PC will not boot after enabling XMP, the motherboard usually retries at lower speeds automatically after a few failed boots. If not, clear CMOS to reset.
- Running four sticks of RAM often means you cannot hit the advertised speed of a two-stick kit. Drop to the next profile or run at JEDEC.
- AM5 (Ryzen 7000/9000) EXPO kits sometimes need a manual SOC voltage adjustment to be stable. That is an advanced topic.
