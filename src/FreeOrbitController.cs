using System;
using System.Linq;
using System.Reflection;
using Base.Cameras;
using Base.Core;
using UnityEngine;

namespace Morgott.FreeCamera
{
    /// <summary>
    /// Per-frame glue between Unity input and the live tactical camera. Holds the active
    /// <see cref="PlanarScrollCamera"/> for the current mission and, while the middle mouse button is
    /// dragged (and the stock Q/E rotation is idle), tilts pitch via the public
    /// <c>VerticalAngle</c> field and rotates yaw by writing the private <c>_heading</c> field. All
    /// numeric decisions are delegated to <see cref="OrbitInputMath"/>; this class is thin glue only.
    /// (Scroll-wheel zoom/floor routing lives entirely in <see cref="WheelRouterPatch"/>.)
    /// </summary>
    public class FreeOrbitController : MonoBehaviour
    {
        private const int MiddleMouseButton = 2;
        private const BindingFlags NonPublicInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        // Cached reflection handles (resolved once; the type never changes at runtime).
        private static FieldInfo _headingField;        // PlanarScrollCamera._heading           (float)
        private static FieldInfo _rotationInputField;  // PlanarScrollCamera._rotationInputData (private struct)
        private static FieldInfo _directionField;      // RotationInputData.Direction           (private enum, value None == 0)

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

        /// <summary>Push the configured zoom range (close/far clamps) onto the bound camera's public
        /// fields. The per-notch discrete-zoom increment is NOT set here: it is now distance-proportional
        /// and recomputed on every wheel notch / keyboard t-g press in <see cref="WheelRouterPatch"/>.</summary>
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
