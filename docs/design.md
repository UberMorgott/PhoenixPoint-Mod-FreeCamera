# Free Camera — design / spec

Baldur's-Gate-style free-orbit tactical camera for Phoenix Point. Tactical combat only (v1).

## Requirements

- **MMB-drag free orbit.** Hold the middle mouse button and drag to orbit: `dx → yaw`, `dy → pitch (tilt)`.
- **Zoom-to-centre, extended range.** Keep the stock distance-only zoom toward screen centre (no cursor
  raycast); only widen the allowed range from config.
- **Leave Q/E untouched.** The stock keyboard free-rotation must keep working; orbit only drives while
  the QE rotation input is idle.
- **Config (in-game Mods UI).** Invert-Y, per-axis sensitivity, pitch limits, zoom range. No sliders exist
  in that UI → floats are text boxes → all clamped in code.

## Approach A — thin MonoBehaviour + one small Harmony prefix

Do NOT replace the camera. Drive the existing `Base.Cameras.PlanarScrollCamera` (a pure orbit) by writing
its angle state each frame, and swallow one stock input event. Minimal surface, no custom camera.

## Verified engine facts (decompile `Base.Cameras.PlanarScrollCamera`)

- Camera is an orbit: pivot = private `DampedVector3 _target`, yaw = private `float _heading`
  (public getter `Heading`), radius = private `DampedFloat _distanceToTarget`, pitch =
  **public `float VerticalAngle = 45f`**.
- `VerticalAngle` is **public, read every frame** by `GetCameraParams()` (`PlanarScrollCamera.cs:339-342`)
  → `CamUtl.GetCameraParams(Target, Heading, DistanceToTarget, VerticalAngle, FOV)`, and is **never written
  or clamped** by the engine. Writing it tilts the camera — the core mechanism.
- Per-frame anim: `BehaviorUpdate` (in `CameraBehavior`) calls `UpdateParams()` then
  `CameraAnimation(time)`. Steady-state `CameraAnimation == DefaultCameraAnimation` → `GetCameraParams()`,
  so a direct write to `_heading` / `VerticalAngle` shows next frame. `SetRotationTransition` (QE) swaps in a
  temporary animation and restores `DefaultCameraAnimation` when done.
- Stock MMB zoom: action name **`"Mouse Scroll Zoom Out"`**, handled in private
  `bool HandleZoomRotateSelect(InputEvent ie)` (`PlanarScrollCamera.cs:585`): Pressed `+= MaxZoomOutDistance`,
  Released `-= MaxZoomOutDistance`. This is what yanks the view on MMB and must be swallowed.
- Zoom range = public `MaxZoomInLimit (10)` / `MaxZoomOutLimit (25)`; `_distanceToTarget` already damped.
  ZoomMin → `MaxZoomInLimit` (closest), ZoomMax → `MaxZoomOutLimit` (farthest).
- QE-rotation state = private struct field `_rotationInputData` with public `Direction` (enum
  `None/Left/Right`, `None == 0`). Idle ⇔ `Direction == None`.
- Live instance (mirrors `TacticalLevelController.cs:684`):
  `GameUtl.GameComponent<CameraManager>().CameraBehaviors.OfType<PlanarScrollCamera>().FirstOrDefault()`.
  (TFTV alt: `CameraManager.CurrentBehavior as PlanarScrollCamera`.) NonPublic reflection works at runtime.
- `InputEvent` = struct in `Base.Input` with public `string Name`, `InputEventType Type`, `InputType InputType`.
- `ConfigFieldAttribute(text, description)` drives the in-game label/description (`ModConfigField.cs:46-54`).

## Component map

| File | Role | Touches |
|---|---|---|
| `src/OrbitInputMath.cs` | Pure, engine-free math (TDD core) | nothing — float math only |
| `src/FreeCameraConfig.cs` | `ModConfig` subclass, `[ConfigField]` fields | modding UI |
| `src/FreeCameraMain.cs` | `ModMain`: PatchAll, attach controller, level on/off, sanitize config | `HarmonyInstance`, `ModGO`, `Level.State` |
| `src/FreeOrbitController.cs` | `MonoBehaviour`: per-frame MMB orbit glue | `PlanarScrollCamera.VerticalAngle` (write), `_heading`/`_rotationInputData` (reflection), `MaxZoom*Limit`, `UnityEngine.Input` |
| `src/WheelRouterPatch.cs` | Harmony Prefix on `HandleInput` — wheel zoom/floor routing + `OnActivate` zoom-limit postfix | `PlanarScrollCamera.HandleInput` (`"Change Level"` axis), `MaxZoom*Limit` |
| `src/MmbZoomSuppressPatch.cs` | Harmony Prefix swallowing `"Mouse Scroll Zoom Out"` | `PlanarScrollCamera.HandleZoomRotateSelect` |

## Scroll-wheel routing — verified ground truth (extracted input map + decompile, 2026-06-26)

The earlier model was wrong (3 failed fixes); corrected from `extracted/GameData/input/inputmap.md`
and `Base.Cameras.PlanarScrollCamera`:

- The mouse wheel is bound to TWO axis actions: **`"Scroll Zoom"`** and **`"Change Level"`** (both
  `Mouse ScrollWheel : Axis`). It does **NOT** fire the discrete `"Discrete Zoom In/Out"` (t/g) or
  `"Change Level Ascend/Descend"` (z/c) actions — those are **keyboard-only**.
- In the **tactical** camera `"Scroll Zoom"` is **dead** — `PlanarScrollCamera.HandleInput` never
  reads it (only `FirstPersonCamera`/`GeoscapeCamera` do). The **only** wheel event with a side
  effect is `"Change Level"` (AxisUpdate) at `PlanarScrollCamera.cs:513`: moves the floor by
  `_floorHeight * sign(AxisValue)` while `MoveCursorWithJoyStick == true` (the KB+M default).
- `MoveCursorWithJoyStick` (toggled by `"Joystick Toggle Tactical Camera Scroll Mode"`, gamepad
  button 8, `:524`) selects joystick-cursor-vs-floor — **not** zoom. So there is **no native
  tactical wheel-zoom and no mode field that produces one**: a bare-wheel zoom must be synthesized.
- `Ctrl + wheel` is natively `"OverwatchSpreadAxis"` (overwatch cone), so the floor modifier
  defaults to **Alt** (Ctrl/Shift selectable; Ctrl flagged as colliding).

**Implementation (`WheelRouterPatch`):** one prefix on `HandleInput` intercepts the `"Change Level"`
axis event, resolves zoom-vs-floor (`OrbitInputMath.ResolveWheelAction`), then mutates the by-ref
`InputEvent` and lets the original run — Zoom rewrites it to a native `"Discrete Zoom In/Out"`
`Pressed` event (drives `_distanceToTarget`, clamped by `MaxZoomInLimit/Out`, floor branch skipped);
Floor flips `AxisValue` for `InvertFloor` and takes exactly one native floor step. No re-dispatch,
no keybind-stripping, no reflection.

## Defaults

- Pitch: `PitchMin 5`, `PitchMax 85`; hard band `(-89, 89)`. Sensitivity `1.0` each axis; gain
  `BaseDegreesPerPixel = 0.2`. Zoom: `ZoomMin 3`, `ZoomMax 55` (vanilla 10 / 25).
- Yaw written to `_heading` (direct, frame-immediate) — chosen over `SetRotationTransition`, which would
  lerp over `RotationDuration` and make a drag feel laggy.

## Known risks (verify in-game)

- **Cinemachine Composer / re-aim.** The virtual camera may re-aim rotation independently of `_heading`;
  if yaw fights back, route yaw differently. Pitch via `VerticalAngle` is confirmed read every frame.
- **QE-idle timing.** The engine resets `_rotationInputData.Direction` to `None` at the end of its own
  per-frame pass, so the idle check is best-effort against simultaneous MMB + Q/E, not a hard interlock
  (fail-open by design).
- **CameraAnimation steady state.** Direct `_heading` writes assume `CameraAnimation == DefaultCameraAnimation`
  (true unless a QE rotation transition is mid-flight).

## InvertScroll decision (superseded 2026-06-26)

Originally omitted on the assumption that no zoom event is intercepted. That assumption no longer
holds: `WheelRouterPatch` now intercepts the wheel's `"Change Level"` axis event to route zoom-vs-floor
(see "Scroll-wheel routing" above). Direction is therefore configurable via **`InvertZoom`** /
**`InvertFloor`** (`FreeCameraConfig`), resolved by `OrbitInputMath.ResolveWheelAction`. Zoom distance
is still clamped/damped natively (we only widen `MaxZoomInLimit`/`MaxZoomOutLimit`).
