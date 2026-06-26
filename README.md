# Free Camera

> A Baldur's-Gate-style free-orbit tactical camera for Phoenix Point. Hold the middle mouse button and drag to look around the battlefield freely.

Free Camera replaces the stock "middle-mouse = snap zoom out" with a proper free orbit, the way an isometric RPG camera feels. It changes no game content — only how the tactical camera moves.

## What it does

- **MMB-drag free orbit.** Hold the **middle mouse button** and drag: left/right rotates (yaw), up/down tilts (pitch).
- **Extended zoom.** The mouse wheel still zooms toward the screen centre, now over a wider configurable range.
- **Q/E untouched.** The stock keyboard rotation keeps working exactly as before.
- **Tactical missions only.** The geoscape camera is not affected.

## Configuration (in-game Mods menu)

| Setting | Default | Effect |
|---|---|---|
| `Enable free orbit` | `true` | Master toggle. Off restores the vanilla middle-mouse zoom-out. |
| `Invert Y axis` | `false` | Flip the up/down tilt direction. |
| `Horizontal sensitivity` | `1.0` | Yaw drag sensitivity (must be > 0). |
| `Vertical sensitivity` | `1.0` | Pitch drag sensitivity (must be > 0). |
| `Min pitch angle` | `5` | Lowest tilt angle, degrees (kept within −89..89, below max). |
| `Max pitch angle` | `85` | Highest tilt angle, degrees (kept within −89..89, above min). |
| `Min zoom distance` | `3` | Closest the camera may zoom in. |
| `Max zoom distance` | `55` | Farthest the camera may zoom out. |

There are no sliders in the mod-options UI, so numeric fields are text boxes; out-of-range values are clamped automatically.

## Requirements

- **Phoenix Point** (base game). No dependencies. Compatible with TFTV.

## Installation

Copy the `FreeCamera` folder (`FreeCamera.dll` + `meta.json`) into `…\Phoenix Point\Mods\`, then enable **Free Camera** in the in-game mod manager. The final path should be `Phoenix Point\Mods\FreeCamera\meta.json`.

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
- Phoenix Point © Snapshot Games.
