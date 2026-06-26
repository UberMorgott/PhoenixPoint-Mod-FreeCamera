using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Base.Cameras;
using Base.Core;
using Base.Input;
using PhoenixPoint.Modding;
using UnityEngine;

namespace Morgott.FreeCamera
{
    /// <summary>
    /// Per-frame glue between Unity input and the live tactical camera. Holds the active
    /// <see cref="PlanarScrollCamera"/> for the current mission and, while the middle mouse button is
    /// dragged (and the stock Q/E rotation is idle), tilts pitch via the public
    /// <c>VerticalAngle</c> field and rotates yaw by writing the private <c>_heading</c> field. All
    /// numeric decisions are delegated to <see cref="OrbitInputMath"/>; this class is thin glue only.
    /// </summary>
    public class FreeOrbitController : MonoBehaviour
    {
        private const int MiddleMouseButton = 2;
        private const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        // Cached reflection handles (resolved once; the type never changes at runtime).
        private static FieldInfo _headingField;        // PlanarScrollCamera._heading           (float)
        private static FieldInfo _rotationInputField;  // PlanarScrollCamera._rotationInputData (private struct)
        private static FieldInfo _directionField;      // RotationInputData.Direction           (private enum, value None == 0)

        // One-shot guard so the read-only wheel-binding diagnostic dump runs once per session.
        private static bool _wheelBindingChecked;

        // Pristine, pre-strip deep copies of the two floor actions, captured the first time the wheel
        // key is stripped so the strip is fully reversible (mod disable / Floors mode). Captured once;
        // never overwritten with an already-stripped action.
        private static InputAction _origAscend;
        private static InputAction _origDescend;

        // True while the scroll-wheel key is currently stripped from the floor actions, so apply/restore
        // stay balanced: no double-strip, no restore-when-nothing-stripped.
        private static bool _stripApplied;

        private PlanarScrollCamera _camera;
        private bool _active;
        private bool _dragging;
        private Vector3 _lastMousePos;

        /// <summary>Begin watching for orbit input this mission; the camera is resolved lazily in Update.</summary>
        public void Activate()
        {
            _active = true;
            _camera = null;
            _dragging = false;
        }

        /// <summary>Stop orbiting and drop the camera reference (mission ended / mod disabled).</summary>
        public void Deactivate()
        {
            _active = false;
            _camera = null;
            _dragging = false;
        }

        /// <summary>Push the configured zoom range onto the bound camera's public zoom-limit fields.</summary>
        public void ApplyZoomLimits()
        {
            FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
            if (cfg == null || _camera == null)
            {
                return;
            }
            float min = cfg.ZoomMin;
            float max = cfg.ZoomMax;
            OrbitInputMath.SanitizeZoomLimits(ref min, ref max);
            _camera.MaxZoomInLimit = min;   // closest distance
            _camera.MaxZoomOutLimit = max;  // farthest distance
        }

        /// <summary>
        /// Once per session, log the live scroll-wheel / floor input bindings (the one fact that can
        /// only be confirmed in-game). Read-only diagnostic; the actual scroll-strip is applied (and
        /// reverted) by <see cref="SyncFloorActionStrip"/>. Best-effort: never throws out of Update.
        /// </summary>
        private void RunWheelBindingDiagnosticsOnce()
        {
            if (_wheelBindingChecked)
            {
                return;
            }
            _wheelBindingChecked = true;

            try
            {
                InputController input = GameUtl.GameComponent<InputController>();
                if (input == null)
                {
                    _wheelBindingChecked = false; // not ready yet; retry on a later frame
                    return;
                }
                ModLogger log = FreeCameraMain.Instance?.Logger;

                // DIAGNOSTIC: surface the real wheel binding in the game log.
                if (log != null)
                {
                    log.LogInfo("[FreeCamera] Wheel/floor input bindings (active):");
                    DumpActionBinding(log, input, "Change Level Ascend");
                    DumpActionBinding(log, input, "Change Level Descend");
                    DumpActionBinding(log, input, "Discrete Zoom In");
                    DumpActionBinding(log, input, "Discrete Zoom Out");
                }
            }
            catch
            {
                // Diagnostics must never break the input loop.
            }
        }

        /// <summary>Log one action's chord/key bindings as <c>[Name/InputSource]</c> tokens.</summary>
        private static void DumpActionBinding(ModLogger log, InputController input, string actionName)
        {
            InputAction action = input.GetActiveAction(actionName);
            if (action == null || action.Chords == null)
            {
                log.LogInfo("  " + actionName + ": <none>");
                return;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("  ").Append(actionName).Append(": ");
            foreach (InputChord chord in action.Chords)
            {
                if (chord?.Keys == null)
                {
                    continue;
                }
                foreach (InputKey key in chord.Keys)
                {
                    if (key == null)
                    {
                        continue;
                    }
                    sb.Append('[').Append(key.Name).Append('/').Append(key.InputSource).Append(']');
                }
            }
            log.LogInfo(sb.ToString());
        }

        /// <summary>
        /// Remove any scroll-wheel key from a floor action via the native rebind API, capturing the
        /// pristine pre-strip action into <paramref name="snapshot"/> (once) so the change can be
        /// reverted later. Deep-copies the active action, filters the copy, and only applies if a scroll
        /// key was actually found (idempotent no-op otherwise). Returns true when the action was live
        /// (resolvable) this frame, so the caller knows the strip was actually evaluated. A "scroll" key
        /// is detected by name (the exact name is what the diagnostic dump reveals).
        /// </summary>
        private static bool StripScrollFromFloorAction(ModLogger log, InputController input, string actionName, ref InputAction snapshot)
        {
            InputAction active = input.GetActiveAction(actionName);
            if (active == null || active.Chords == null)
            {
                return false; // not active (e.g. off-mission) -> caller should retry later
            }
            // Snapshot the pristine original ONCE (independent deep copy: fresh chord+key arrays), BEFORE
            // any key is removed, so the strip stays fully reversible. Never overwrite a real snapshot
            // with an already-stripped action on a later re-strip.
            if (snapshot == null)
            {
                snapshot = new InputAction(active);
            }
            InputAction modified = new InputAction(active); // independent deep copy (chords + keys)
            List<InputChord> keptChords = new List<InputChord>();
            bool removedAny = false;
            foreach (InputChord chord in modified.Chords)
            {
                if (chord?.Keys == null)
                {
                    if (chord != null)
                    {
                        keptChords.Add(chord);
                    }
                    continue;
                }
                List<InputKey> keptKeys = new List<InputKey>();
                foreach (InputKey key in chord.Keys)
                {
                    if (key != null && IsScrollWheelKey(key.Name))
                    {
                        removedAny = true;
                        continue;
                    }
                    keptKeys.Add(key);
                }
                if (keptKeys.Count > 0)
                {
                    chord.Keys = keptKeys.ToArray();
                    keptChords.Add(chord);
                }
            }
            if (!removedAny)
            {
                return true; // wheel not bound here -> nothing to strip, but the action was live
            }
            modified.Chords = keptChords.ToArray();
            input.ApplyKeybinding(modified);
            log?.LogInfo("[FreeCamera] Stripped scroll-wheel key from \"" + actionName + "\" (Zoom mode).");
            return true;
        }

        /// <summary>
        /// Apply or revert the scroll-wheel strip to match the current wheel mode, keeping the player's
        /// live keybindings reversible. Strips the wheel key from the floor actions when orbit is on and
        /// <see cref="WheelMode.Zoom"/> is selected (so the bare wheel is zoom-only); otherwise restores
        /// the captured originals. Idempotent and balanced via <see cref="_stripApplied"/> — never
        /// double-strips, never restores when nothing is stripped. Best-effort: never throws.
        /// </summary>
        internal static void SyncFloorActionStrip()
        {
            try
            {
                FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
                bool wantStrip = cfg != null && cfg.EnableOrbit && cfg.Wheel == WheelMode.Zoom;
                if (wantStrip == _stripApplied)
                {
                    return; // already in the desired state
                }
                InputController input = GameUtl.GameComponent<InputController>();
                if (input == null)
                {
                    return; // input not ready (e.g. no mission yet); a later call retries
                }
                ModLogger log = FreeCameraMain.Instance?.Logger;
                if (wantStrip)
                {
                    bool liveA = StripScrollFromFloorAction(log, input, "Change Level Ascend", ref _origAscend);
                    bool liveD = StripScrollFromFloorAction(log, input, "Change Level Descend", ref _origDescend);
                    if (liveA || liveD)
                    {
                        _stripApplied = true; // only latch once the actions were actually resolvable
                    }
                }
                else
                {
                    RestoreFloorAction(log, input, _origAscend, "Change Level Ascend");
                    RestoreFloorAction(log, input, _origDescend, "Change Level Descend");
                    _stripApplied = false;
                }
            }
            catch
            {
                // Rebinding is best-effort: never break the input loop or mod lifecycle.
            }
        }

        /// <summary>
        /// Force-restore the original floor bindings and clear the strip state, regardless of config.
        /// Called on mod disable so the player's session-live keybindings are fully reverted; also resets
        /// the one-shot diagnostic guard so a disable -> enable cycle re-runs cleanly. Best-effort.
        /// </summary>
        internal static void RestoreFloorActionStrip()
        {
            try
            {
                if (_stripApplied)
                {
                    InputController input = GameUtl.GameComponent<InputController>();
                    if (input != null)
                    {
                        ModLogger log = FreeCameraMain.Instance?.Logger;
                        RestoreFloorAction(log, input, _origAscend, "Change Level Ascend");
                        RestoreFloorAction(log, input, _origDescend, "Change Level Descend");
                    }
                }
            }
            catch
            {
                // Best-effort restore: never throw out of the mod lifecycle.
            }
            finally
            {
                _stripApplied = false;
                _wheelBindingChecked = false; // disable -> enable re-runs diagnostics + strip cleanly
            }
        }

        /// <summary>
        /// Re-apply a captured pristine floor action through the native rebind API, restoring the
        /// player's original binding. Re-caches a fresh deep copy of the snapshot so the stored original
        /// can never be mutated by a later strip. No-op when nothing was captured for this action.
        /// </summary>
        private static void RestoreFloorAction(ModLogger log, InputController input, InputAction snapshot, string actionName)
        {
            if (snapshot == null)
            {
                return; // nothing was ever stripped for this action
            }
            input.ApplyKeybinding(new InputAction(snapshot));
            log?.LogInfo("[FreeCamera] Restored original scroll/floor binding for \"" + actionName + "\".");
        }

        /// <summary>Heuristic: a mouse-scroll key carries "scroll" or "wheel" in its name.</summary>
        private static bool IsScrollWheelKey(string keyName)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                return false;
            }
            string lower = keyName.ToLowerInvariant();
            return lower.Contains("scroll") || lower.Contains("wheel");
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }
            FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
            if (cfg == null || !cfg.EnableOrbit)
            {
                _dragging = false;
                return;
            }

            if (_camera == null)
            {
                _camera = ResolveCamera();
                if (_camera == null)
                {
                    _dragging = false;
                    return;
                }
                ApplyZoomLimits();
                RunWheelBindingDiagnosticsOnce();
                SyncFloorActionStrip();
            }

            if (!Input.GetMouseButton(MiddleMouseButton))
            {
                _dragging = false;
                return;
            }

            Vector3 mouse = Input.mousePosition;
            if (!_dragging)
            {
                // First frame of the hold: capture the baseline so there is no initial jump.
                _lastMousePos = mouse;
                _dragging = true;
                return;
            }

            float dx = mouse.x - _lastMousePos.x;
            float dy = mouse.y - _lastMousePos.y;
            _lastMousePos = mouse;

            // Do not fight the stock Q/E free-rotation: only drive while the rotation input is idle.
            if (!IsRotationIdle(_camera))
            {
                return;
            }
            if (dx == 0f && dy == 0f)
            {
                return;
            }

            // Pitch: VerticalAngle is public and read every frame by GetCameraParams -> direct write.
            float pitchDelta = OrbitInputMath.PitchDelta(dy, cfg.SensitivityY, cfg.InvertY);
            _camera.VerticalAngle = OrbitInputMath.ClampPitch(_camera.VerticalAngle + pitchDelta, cfg.PitchMin, cfg.PitchMax);

            // Yaw: _heading is private; written via reflection. In steady state CameraAnimation is
            // DefaultCameraAnimation, which reads Heading (_heading) each frame, so the change shows immediately.
            float yawDelta = OrbitInputMath.YawDelta(dx, cfg.SensitivityX);
            ApplyYaw(_camera, yawDelta);
        }

        /// <summary>
        /// Resolve the tactical orbit camera the same way the engine does
        /// (TacticalLevelController: <c>CameraManager.CameraBehaviors.OfType&lt;PlanarScrollCamera&gt;().FirstOrDefault()</c>).
        /// Returns null off the tactical layer.
        /// </summary>
        private static PlanarScrollCamera ResolveCamera()
        {
            CameraManager cm = GameUtl.GameComponent<CameraManager>();
            if (cm == null)
            {
                return null;
            }
            return cm.CameraBehaviors?.OfType<PlanarScrollCamera>().FirstOrDefault();
        }

        private static void ApplyYaw(PlanarScrollCamera cam, float yawDelta)
        {
            try
            {
                if (_headingField == null)
                {
                    _headingField = typeof(PlanarScrollCamera).GetField("_heading", NonPublicInstance);
                }
                if (_headingField == null)
                {
                    return;
                }
                float heading = cam.Heading; // public getter; avoids per-frame boxing of the private _heading read
                _headingField.SetValue(cam, OrbitInputMath.WrapHeading(heading + yawDelta));
            }
            catch
            {
                // Reflection guard: never throw out of Update.
            }
        }

        /// <summary>
        /// True when the camera's QE rotation input is idle (<c>_rotationInputData.Direction == None</c>).
        /// Fail-open: any reflection failure returns true so orbit still works. NOTE: the engine resets
        /// Direction to None at the end of its own per-frame pass, so this is a best-effort guard against
        /// simultaneous MMB + Q/E, not a hard interlock.
        /// </summary>
        private static bool IsRotationIdle(PlanarScrollCamera cam)
        {
            try
            {
                if (_rotationInputField == null)
                {
                    _rotationInputField = typeof(PlanarScrollCamera).GetField("_rotationInputData", NonPublicInstance);
                }
                if (_rotationInputField == null)
                {
                    return true;
                }
                object rot = _rotationInputField.GetValue(cam); // boxed RotationInputData struct
                if (rot == null)
                {
                    return true;
                }
                if (_directionField == null)
                {
                    _directionField = rot.GetType().GetField("Direction"); // public field of the private struct
                }
                if (_directionField == null)
                {
                    return true;
                }
                object dir = _directionField.GetValue(rot);
                return Convert.ToInt32(dir) == 0; // DirectionType.None
            }
            catch
            {
                return true;
            }
        }
    }
}
