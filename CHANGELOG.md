# Changelog

All notable changes to Free Camera are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres
to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.0] - 2026-06-26

### Added
- **MMB-drag free orbit** for the tactical camera: hold the middle mouse button and drag to
  rotate (yaw) and tilt (pitch), Baldur's-Gate style.
- **Extended zoom range**, configurable via `Min/Max zoom distance` (defaults 3 / 55, vanilla 10 / 25).
- **In-game settings**: enable toggle, invert-Y, per-axis sensitivity, pitch limits, zoom range.
- Stock middle-mouse "zoom all the way out" is suppressed while orbit is enabled so MMB no longer
  yanks the camera; the keyboard Q/E rotation is left untouched.
