# Free Camera

> A free-orbit tactical camera for Phoenix Point. Hold the middle mouse button and drag to look around the battlefield freely.

Free Camera replaces the stock "middle-mouse = snap zoom out" with a proper free orbit, the way an isometric RPG camera feels. It changes no game content — only how the tactical camera moves. Works standalone on the base game and is fully compatible with TFTV.

## What it does

- **MMB-drag free orbit.** Hold the **middle mouse button** and drag: left/right rotates (yaw), up/down tilts (pitch).
- **Proportional wheel zoom, extended range.** The mouse wheel zooms toward the screen centre with a distance-proportional feel (fast when far, gentle when near) over a wider, configurable range that reaches much closer than vanilla.
- **Alt + wheel changes floors.** Slice building storeys without leaving the wheel; the bare wheel stays on zoom (both directions and the modifier key are configurable).
- **Q/E untouched.** The stock keyboard rotation keeps working exactly as before.
- **Tactical missions only.** The geoscape camera is not affected.

## Controls

| Control | Action |
|---|---|
| **Middle mouse + drag** | Orbit — left/right rotates (yaw), up/down tilts (pitch) |
| **Mouse wheel** | Zoom in / out (distance-proportional, extended close range) |
| **Alt + mouse wheel** | Change building floor (slice up/down) |
| **Q / E** | Stock keyboard rotation (unchanged) |
| **t / g** | Stock keyboard zoom (now distance-proportional too) |

Defaults assume `Scroll wheel mode = Zoom` and `Floor modifier key = Alt`; both are configurable (see below).

## Configuration (in-game Mods menu)

| Setting | Default | Effect |
|---|---|---|
| `Enable free orbit` | `true` | Master toggle. Off restores the vanilla middle-mouse zoom-out. |
| `Invert Y axis` | `false` | Flip the up/down tilt direction. |
| `Scroll wheel mode` | `Zoom` | `Zoom`: wheel zooms, modifier+wheel changes floor. `Floors`: the mirror. |
| `Floor modifier key` | `Alt` | Held key that swaps the wheel's meaning. **Avoid `Ctrl`** — Ctrl+wheel is the game's overwatch-cone control. |
| `Invert wheel zoom` | `false` | Flip which scroll direction zooms in vs out. |
| `Invert wheel floor` | `false` | Flip which scroll direction climbs vs descends a floor. |
| `Horizontal sensitivity` | `1.0` | Yaw drag sensitivity (must be > 0). |
| `Vertical sensitivity` | `1.0` | Pitch drag sensitivity (must be > 0). |
| `Min pitch angle` | `5` | Lowest tilt angle, degrees (kept within −89..89, below max). |
| `Max pitch angle` | `85` | Highest tilt angle, degrees (kept within −89..89, above min). |
| `Min zoom distance` | `3` | Closest the camera may zoom in (vanilla 10). |
| `Max zoom distance` | `55` | Farthest the camera may zoom out (vanilla 25). |
| `Wheel zoom factor` | `0.12` | Fraction of the current distance moved per notch (higher = bigger jumps). |
| `Min zoom step` | `0.3` | Floor on the per-notch step so close-in zoom never crawls. |
| `Max zoom step` | `8` | Cap on the per-notch step so a far view never jumps the whole range at once. |

There are no sliders in the mod-options UI, so numeric fields are text boxes; out-of-range values are clamped automatically.

## Requirements

- **Phoenix Point** (base game). No dependencies.
- **Terror From The Void (TFTV)** is optional and fully compatible — Free Camera only drives the camera, so it does not touch any content either mod changes.

## Supported languages

The in-game mod settings (labels and descriptions) and the mod name are localized in:
English, 简体中文, Français, Deutsch, Italiano, Polski, Русский, Español.

## Installation

Copy the `FreeCamera` folder — `FreeCamera.dll`, `meta.json`, and the `Assets/` folder — into `…\Phoenix Point\Mods\`, then enable **Free Camera** in the in-game mod manager. The final path should be `Phoenix Point\Mods\FreeCamera\meta.json`. (The `Assets/` folder ships the localization CSV; without it the UI simply falls back to English.)

## Building from source

Requires the .NET SDK and a Phoenix Point install (the project references the game's managed assemblies).

```powershell
# build the mod assembly in Release
dotnet build -c Release

# run the unit tests (pure orbit math)
dotnet test tests/FreeCamera.Tests.csproj
```

## License

Free Camera © 2026 Morgott. Licensed under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to use and modify for non-commercial purposes with attribution.

## Credits

- Built by **Morgott**.
- Compatible with, but not dependent on, the **TFTV** overhaul by Voland163 and contributors.
- Phoenix Point © Snapshot Games.
