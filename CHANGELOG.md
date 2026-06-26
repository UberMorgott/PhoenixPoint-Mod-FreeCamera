# Changelog

All notable changes to Free Camera are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-06-26

### Added
- **MMB-drag free orbit** for the tactical camera: hold the middle mouse button and drag to
  rotate (yaw) and tilt (pitch), Baldur's-Gate style. The stock keyboard Q/E rotation is left
  untouched, and orbit only drives while that rotation is idle.
- **Distance-proportional wheel zoom with an extended range.** The mouse wheel (and keyboard
  t/g) zooms toward the screen centre by a fraction of the current distance — fast when far,
  gentle when near — over a configurable range that reaches much closer than vanilla
  (`Min/Max zoom distance` default 3 / 55, vanilla 10 / 25).
- **Alt + wheel changes building floors.** The wheel routes between zoom and floor-slicing; the
  modifier key and both scroll directions are configurable.
- **Full in-game settings**: enable toggle, invert-Y, scroll-wheel mode, floor modifier key,
  invert wheel zoom/floor, per-axis sensitivity, pitch limits, zoom range, and the
  proportional-zoom tuning (factor, min/max step).
- **Eight-language localization** of the in-game settings (labels and descriptions) and the mod
  name: English, Simplified Chinese, French, German, Italian, Polish, Russian, Spanish.
- The stock middle-mouse "zoom all the way out" is suppressed while orbit is enabled, so holding
  MMB to orbit no longer yanks the camera.

[1.0.0]: https://github.com/UberMorgott/PhoenixPoint-Mod-FreeCamera/releases/tag/v1.0.0
