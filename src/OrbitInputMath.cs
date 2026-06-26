namespace Morgott.FreeCamera
{
    /// <summary>
    /// Pure, engine-free math for the free-orbit camera. Deliberately has NO UnityEngine
    /// dependency so it links into the net8 test project and is unit-tested in isolation. All
    /// runtime glue (reading Unity input, writing the camera) lives in <see cref="FreeOrbitController"/>;
    /// every value-producing decision is computed here.
    /// </summary>
    public static class OrbitInputMath
    {
        /// <summary>Sensitivity used when the configured value is invalid (&lt;= 0 / NaN / Inf).</summary>
        public const float DefaultSensitivity = 1f;

        /// <summary>Base angular gain: degrees of rotation per pixel of mouse movement at sensitivity 1.</summary>
        public const float BaseDegreesPerPixel = 0.2f;

        /// <summary>Hard pitch (VerticalAngle) band the camera is never driven outside of.</summary>
        public const float PitchHardMin = -89f;
        public const float PitchHardMax = 89f;

        /// <summary>
        /// Pitch (VerticalAngle) delta in degrees from a vertical mouse delta. Dragging the mouse up
        /// (positive dy) tilts toward a more top-down angle by default; <paramref name="invertY"/> flips it.
        /// </summary>
        public static float PitchDelta(float mouseDy, float sensitivityY, bool invertY)
        {
            float delta = mouseDy * sensitivityY * BaseDegreesPerPixel;
            return invertY ? -delta : delta;
        }

        /// <summary>Yaw (heading) delta in degrees from a horizontal mouse delta.</summary>
        public static float YawDelta(float mouseDx, float sensitivityX)
        {
            return mouseDx * sensitivityX * BaseDegreesPerPixel;
        }

        /// <summary>Clamp a pitch value into [min, max]; tolerates swapped bounds.</summary>
        public static float ClampPitch(float pitch, float min, float max)
        {
            if (min > max)
            {
                float tmp = min; min = max; max = tmp;
            }
            if (pitch < min) return min;
            if (pitch > max) return max;
            return pitch;
        }

        /// <summary>Normalize a heading to the [0, 360) range (engine treats heading as free degrees).</summary>
        public static float WrapHeading(float heading)
        {
            float h = heading % 360f;
            if (h < 0f) h += 360f;
            return h;
        }

        /// <summary>A mouse sensitivity must be strictly positive and finite; otherwise reset to the default.</summary>
        public static float SanitizeSensitivity(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return DefaultSensitivity;
            }
            return value;
        }

        /// <summary>
        /// Force the pitch limits into the legal band and guarantee a non-degenerate min &lt; max.
        /// In-game floats are text boxes (no slider), so a user can type anything.
        /// </summary>
        public static void SanitizePitchLimits(ref float min, ref float max)
        {
            if (float.IsNaN(min) || float.IsInfinity(min)) min = 5f;
            if (float.IsNaN(max) || float.IsInfinity(max)) max = 85f;
            if (min < PitchHardMin) min = PitchHardMin;
            if (max > PitchHardMax) max = PitchHardMax;
            if (min < PitchHardMin) min = PitchHardMin;
            if (max > PitchHardMax) max = PitchHardMax;
            if (min > max)
            {
                float tmp = min; min = max; max = tmp;
            }
            if (min >= max)
            {
                // Degenerate band: fall back to the full legal range.
                min = PitchHardMin;
                max = PitchHardMax;
            }
        }

        /// <summary>
        /// Force the zoom-distance limits to satisfy 0 &lt; min &lt; max. ZoomMin maps to the camera's
        /// closest distance (MaxZoomInLimit), ZoomMax to its farthest (MaxZoomOutLimit).
        /// </summary>
        public static void SanitizeZoomLimits(ref float min, ref float max)
        {
            const float floor = 0.1f;
            if (float.IsNaN(min) || float.IsInfinity(min) || min < floor) min = floor;
            if (float.IsNaN(max) || float.IsInfinity(max) || max < floor) max = floor;
            if (min > max)
            {
                float tmp = min; min = max; max = tmp;
            }
            if (min >= max)
            {
                max = min + 1f;
            }
        }
    }
}
