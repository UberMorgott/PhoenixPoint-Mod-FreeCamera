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
| `src/MmbZoomSuppressPatch.cs` | Harmony Prefix swallowing `"Mouse Scroll Zoom Out"` | `PlanarScrollCamera.HandleZoomRotateSelect` |

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

## InvertScroll decision

**Omitted.** Zoom stays fully native (we only widen `MaxZoomInLimit`/`MaxZoomOutLimit`; the engine clamps
and damps it). Since no zoom event is intercepted, an `InvertScroll` toggle would be dead — so it is not
shipped (and `OrbitInputMath` has no zoom-direction helper, only the `SanitizeZoomLimits` range guard,
which is used at runtime).
