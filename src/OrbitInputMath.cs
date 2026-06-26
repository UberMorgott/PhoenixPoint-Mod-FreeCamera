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

        /// <summary>
        /// Default fraction of the CURRENT zoom distance moved per wheel notch (and per keyboard t/g
        /// press). Distance-proportional zoom: far from the target a notch covers more ground, close in
        /// it steps gently — the multiplicative feel of a 3D editor. Net travel per notch is
        /// <c>distance × ZoomFactor</c>, clamped to [<see cref="DefaultMinZoomStep"/>,
        /// <see cref="DefaultMaxZoomStep"/>]. Used when the configured value is invalid.
        /// </summary>
        public const float DefaultZoomFactor = 0.12f;

        /// <summary>Default floor on the net per-notch step so the zoom never crawls to a halt near the
        /// closest distance (where <c>distance × ZoomFactor</c> shrinks toward zero).</summary>
        public const float DefaultMinZoomStep = 0.3f;

        /// <summary>Default cap on the net per-notch step so a very large far distance cannot teleport
        /// the camera the whole range in a single notch.</summary>
        public const float DefaultMaxZoomStep = 8f;

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
        /// Force the proportional-zoom tuning into a sane band: every value strictly positive and finite
        /// (invalid ⇒ its default), and <paramref name="minStep"/> &lt; <paramref name="maxStep"/>
        /// (swapped if reversed, made distinct if equal). Applied at config load and defensively inside
        /// <see cref="ComputeProportionalZoomStep"/>.
        /// </summary>
        public static void SanitizeProportionalZoom(ref float factor, ref float minStep, ref float maxStep)
        {
            if (float.IsNaN(factor) || float.IsInfinity(factor) || factor <= 0f) factor = DefaultZoomFactor;
            if (float.IsNaN(minStep) || float.IsInfinity(minStep) || minStep <= 0f) minStep = DefaultMinZoomStep;
            if (float.IsNaN(maxStep) || float.IsInfinity(maxStep) || maxStep <= 0f) maxStep = DefaultMaxZoomStep;
            if (minStep > maxStep)
            {
                float tmp = minStep; minStep = maxStep; maxStep = tmp;
            }
            if (minStep >= maxStep)
            {
                maxStep = minStep + 1f;
            }
        }

        /// <summary>
        /// Distance-proportional per-notch zoom step. The intended NET travel for one wheel notch is
        /// <c>distance × factor</c>, clamped to [<paramref name="minStep"/>, <paramref name="maxStep"/>]
        /// — so the camera moves fast when far from the target and gently when near. Tuning inputs are
        /// sanitized internally (see <see cref="SanitizeProportionalZoom"/>); a NaN/Inf/negative distance
        /// is treated as 0 (⇒ the min-step floor). When <paramref name="doubleApplied"/> is true the
        /// engine applies the camera's increment twice per discrete-zoom event
        /// (PlanarScrollCamera.HandleZoomRotateSelect at cs:603 AND the HandleInput Pressed branch at
        /// cs:533), so the value returned — what is written to <c>DiscreteZoomIn/OutIncrement</c> — is
        /// HALF the net step, making the two applies sum to the intended proportional amount.
        /// </summary>
        public static float ComputeProportionalZoomStep(float distance, float factor, float minStep, float maxStep, bool doubleApplied)
        {
            SanitizeProportionalZoom(ref factor, ref minStep, ref maxStep);
            float d = (float.IsNaN(distance) || float.IsInfinity(distance) || distance < 0f) ? 0f : distance;
            float net = d * factor;
            if (net < minStep) net = minStep;
            if (net > maxStep) net = maxStep;
            return doubleApplied ? net * 0.5f : net;
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

        /// <summary>
        /// Decide whether a single scroll-wheel notch should zoom or change floor, and in which
        /// direction. Pure (no engine types) so it unit-tests in isolation; the runtime patch feeds
        /// it the live state: <paramref name="modifierHeld"/> = is the floor-swap modifier key down,
        /// <paramref name="wheelDir"/> = the raw notch (+1 for a "Discrete Zoom In" event, -1 for
        /// "Discrete Zoom Out"). Mode + modifier select the action; the matching invert flag
        /// (<paramref name="invertZoom"/> for Zoom, <paramref name="invertFloor"/> for Floor) may flip
        /// the effective direction. Returns the action and the post-invert direction.
        /// </summary>
        public static WheelResolution ResolveWheelAction(WheelMode mode, bool modifierHeld, int wheelDir, bool invertZoom, bool invertFloor)
        {
            // Zoom mode: bare wheel zooms, modifier swaps to floor. Floors mode: the mirror.
            WheelAction action;
            if (mode == WheelMode.Zoom)
            {
                action = modifierHeld ? WheelAction.Floor : WheelAction.Zoom;
            }
            else
            {
                action = modifierHeld ? WheelAction.Zoom : WheelAction.Floor;
            }

            int dir = wheelDir > 0 ? 1 : (wheelDir < 0 ? -1 : 0);
            bool invert = action == WheelAction.Zoom ? invertZoom : invertFloor;
            int effectiveDir = invert ? -dir : dir;
            return new WheelResolution(action, effectiveDir);
        }
    }

    /// <summary>
    /// What the scroll wheel does with no modifier held. Surfaced in-game as an arrow-picker
    /// (<see cref="FreeCameraConfig.Wheel"/>); the held floor modifier swaps to the other meaning.
    /// </summary>
    public enum WheelMode
    {
        /// <summary>Bare wheel zooms the camera; floor-modifier + wheel changes floor. (Default.)</summary>
        Zoom,

        /// <summary>Bare wheel changes floor (native feel); floor-modifier + wheel zooms.</summary>
        Floors,
    }

    /// <summary>The resolved meaning of one wheel notch.</summary>
    public enum WheelAction
    {
        /// <summary>Move the camera closer/farther.</summary>
        Zoom,

        /// <summary>Step the visible storey up/down (native "Change Level Ascend/Descend").</summary>
        Floor,
    }

    /// <summary>
    /// Output of <see cref="OrbitInputMath.ResolveWheelAction"/>: which action a wheel notch maps to,
    /// and its direction after the invert flags. <see cref="EffectiveDir"/> is +1 for zoom-in /
    /// floor-ascend, -1 for zoom-out / floor-descend.
    /// </summary>
    public readonly struct WheelResolution
    {
        public readonly WheelAction Action;
        public readonly int EffectiveDir;

        public WheelResolution(WheelAction action, int effectiveDir)
        {
            Action = action;
            EffectiveDir = effectiveDir;
        }
    }
}
