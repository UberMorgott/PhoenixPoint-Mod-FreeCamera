using PhoenixPoint.Modding;

namespace Morgott.FreeCamera
{
    /// <summary>
    /// Held keyboard modifier that swaps the scroll wheel's meaning (zoom &lt;-&gt; floor). Rendered
    /// in-game as an arrow-picker. Read at runtime by the wheel-router patch via
    /// <c>UnityEngine.Input.GetKey</c>.
    /// </summary>
    public enum FloorModifier
    {
        /// <summary>Left or right Ctrl. (Default.)</summary>
        Ctrl,

        /// <summary>Left or right Alt.</summary>
        Alt,

        /// <summary>Left or right Shift.</summary>
        Shift,
    }


    /// <summary>
    /// In-game mod settings for Free Camera. The game auto-discovers every public instance field
    /// (see <see cref="ModConfig.GetConfigFields"/>) and surfaces it in the mod-options UI, using the
    /// <see cref="ConfigFieldAttribute"/> for the label/description. There are NO sliders in that UI,
    /// so float fields render as free text boxes — values are clamped in code (see
    /// <see cref="OrbitInputMath"/> sanitizers, applied from <see cref="FreeCameraMain"/>).
    /// Read at runtime via <c>FreeCameraMain.Instance.Config</c>.
    /// </summary>
    public class FreeCameraConfig : ModConfig
    {
        /// <summary>Master toggle for the free-orbit feature. Off restores stock camera behaviour
        /// (including the vanilla middle-mouse "zoom all the way out").</summary>
        [ConfigField("Enable free orbit",
            "Hold the middle mouse button and drag to freely orbit the tactical camera (look around / tilt). Off restores the stock middle-mouse zoom-out.")]
        public bool EnableOrbit = true;

        /// <summary>Invert the vertical (pitch) drag direction.</summary>
        [ConfigField("Invert Y axis",
            "Flip the up/down drag direction when tilting the camera.")]
        public bool InvertY = false;

        /// <summary>What the bare scroll wheel does; the floor modifier swaps to the other meaning.</summary>
        [ConfigField("Scroll wheel mode",
            "Zoom: wheel zooms, modifier+wheel changes floor. Floors: wheel changes floor, modifier+wheel zooms.")]
        public WheelMode Wheel = WheelMode.Zoom;

        /// <summary>Held key that swaps the wheel between zoom and floor-change.</summary>
        [ConfigField("Floor modifier key",
            "Hold this key to swap the scroll wheel's meaning (zoom <-> change floor).")]
        public FloorModifier FloorKey = FloorModifier.Ctrl;

        /// <summary>Flip the wheel zoom direction (which way scrolls in vs out).</summary>
        [ConfigField("Invert wheel zoom",
            "Flip which scroll direction zooms in versus out.")]
        public bool InvertZoom = false;

        /// <summary>Flip which wheel direction goes up versus down a floor.</summary>
        [ConfigField("Invert wheel floor",
            "Flip which scroll direction climbs a floor versus descends one.")]
        public bool InvertFloor = false;

        /// <summary>Horizontal (yaw) drag sensitivity multiplier. Clamped to &gt; 0 in code.</summary>
        [ConfigField("Horizontal sensitivity",
            "Mouse sensitivity for left/right orbit (yaw). 1.0 = default. Must be greater than 0.")]
        public float SensitivityX = 1f;

        /// <summary>Vertical (pitch) drag sensitivity multiplier. Clamped to &gt; 0 in code.</summary>
        [ConfigField("Vertical sensitivity",
            "Mouse sensitivity for up/down tilt (pitch). 1.0 = default. Must be greater than 0.")]
        public float SensitivityY = 1f;

        /// <summary>Lowest pitch (camera angle, degrees) the orbit allows. Clamped into (-89, 89), below PitchMax.</summary>
        [ConfigField("Min pitch angle",
            "Lowest camera tilt angle in degrees (closer to horizontal). Kept within -89..89 and below the max.")]
        public float PitchMin = 5f;

        /// <summary>Highest pitch (camera angle, degrees) the orbit allows. Clamped into (-89, 89), above PitchMin.</summary>
        [ConfigField("Max pitch angle",
            "Highest camera tilt angle in degrees (closer to top-down). Kept within -89..89 and above the min.")]
        public float PitchMax = 85f;

        /// <summary>Closest zoom distance (maps to the camera's MaxZoomInLimit). Clamped to 0 &lt; ZoomMin &lt; ZoomMax.</summary>
        [ConfigField("Min zoom distance",
            "Closest the camera may zoom in. Lower = closer to the ground. Must be greater than 0 and below the max.")]
        public float ZoomMin = 3f;

        /// <summary>Farthest zoom distance (maps to the camera's MaxZoomOutLimit). Clamped above ZoomMin.</summary>
        [ConfigField("Max zoom distance",
            "Farthest the camera may zoom out. Higher = wider battlefield view. Must be above the min.")]
        public float ZoomMax = 55f;
    }
}
