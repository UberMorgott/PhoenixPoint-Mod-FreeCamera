using System.Collections.Generic;
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
        /// <summary>Left or right Ctrl. WARNING: Ctrl+wheel is the game's native overwatch-cone spread
        /// control ("OverwatchSpreadAxis"), so choosing Ctrl makes the floor modifier collide with it.</summary>
        Ctrl,

        /// <summary>Left or right Alt. (Default — no scroll-wheel collision.)</summary>
        Alt,

        /// <summary>Left or right Shift.</summary>
        Shift,
    }


    /// <summary>
    /// In-game mod settings for Free Camera. The game auto-discovers every public instance field
    /// (see <see cref="ModConfig.GetConfigFields"/>) and surfaces it in the mod-options UI. There are NO
    /// sliders in that UI, so float fields render as free text boxes — values are clamped in code (see
    /// <see cref="OrbitInputMath"/> sanitizers, applied from <see cref="FreeCameraMain"/>). Read at runtime
    /// via <c>FreeCameraMain.Instance.Config</c>. Labels/descriptions are localized in
    /// <see cref="GetConfigFields"/> with the English literals as fallback.
    /// </summary>
    public class FreeCameraConfig : ModConfig
    {
        /// <summary>Master toggle for the free-orbit feature. Off restores stock camera behaviour
        /// (including the vanilla middle-mouse "zoom all the way out").</summary>
        public bool EnableOrbit = true;

        /// <summary>Invert the vertical (pitch) drag direction.</summary>
        public bool InvertY = false;

        /// <summary>What the bare scroll wheel does; the floor modifier swaps to the other meaning.</summary>
        public WheelMode Wheel = WheelMode.Zoom;

        /// <summary>Held key that swaps the wheel between zoom and floor-change. Defaults to Alt;
        /// Ctrl is avoided because Ctrl+wheel is the game's native overwatch-cone control.</summary>
        public FloorModifier FloorKey = FloorModifier.Alt;

        /// <summary>Flip the wheel zoom direction (which way scrolls in vs out).</summary>
        public bool InvertZoom = false;

        /// <summary>Flip which wheel direction goes up versus down a floor.</summary>
        public bool InvertFloor = false;

        /// <summary>Horizontal (yaw) drag sensitivity multiplier. Clamped to &gt; 0 in code.</summary>
        public float SensitivityX = 1f;

        /// <summary>Vertical (pitch) drag sensitivity multiplier. Clamped to &gt; 0 in code.</summary>
        public float SensitivityY = 1f;

        /// <summary>Lowest pitch (camera angle, degrees) the orbit allows. Clamped into (-89, 89), below PitchMax.</summary>
        public float PitchMin = 5f;

        /// <summary>Highest pitch (camera angle, degrees) the orbit allows. Clamped into (-89, 89), above PitchMin.</summary>
        public float PitchMax = 85f;

        /// <summary>Closest zoom distance (maps to the camera's MaxZoomInLimit). Clamped to 0 &lt; ZoomMin &lt; ZoomMax.</summary>
        public float ZoomMin = 3f;

        /// <summary>Farthest zoom distance (maps to the camera's MaxZoomOutLimit). Clamped above ZoomMin.</summary>
        public float ZoomMax = 55f;

        /// <summary>Fraction of the CURRENT zoom distance moved per wheel notch — distance-proportional
        /// (multiplicative) zoom: bigger steps when far, gentler when close. Clamped to &gt; 0 in code.</summary>
        public float ZoomFactor = 0.12f;

        /// <summary>Floor on the per-notch step so the proportional zoom never crawls near the closest
        /// distance. Clamped to 0 &lt; MinZoomStep &lt; MaxZoomStep in code.</summary>
        public float MinZoomStep = 0.3f;

        /// <summary>Cap on the per-notch step so a very large far distance cannot teleport the camera the
        /// whole range in one notch. Clamped above MinZoomStep in code.</summary>
        public float MaxZoomStep = 8f;

        /// <summary>
        /// Localize the in-game options UI. The base implementation builds one
        /// <see cref="ModConfigField"/> per public field; we keep those (so value get/set still work) and
        /// only override the <c>GetText</c>/<c>GetDescription</c> delegates to read the current-language
        /// strings via <see cref="Loc"/>. English literals are the fallback (and the source column of the
        /// CSV). Enum value names (ZOOM/FLOORS, CTRL/ALT/SHIFT) are rendered by the engine as the raw
        /// member name and are not localizable through this API — their meaning is spelled out in the
        /// localized description text instead. Mirrors TFTV's / Oracle's GetConfigFields override pattern.
        /// </summary>
        public override List<ModConfigField> GetConfigFields()
        {
            List<ModConfigField> fields = base.GetConfigFields();
            foreach (ModConfigField field in fields)
            {
                switch (field.ID)
                {
                    case nameof(EnableOrbit):
                        field.GetText = () => Loc.Get("FREECAM_EnableOrbit", "Enable free orbit");
                        field.GetDescription = () => Loc.Get("FREECAM_EnableOrbit_DESCRIPTION",
                            "Hold the middle mouse button and drag to freely orbit the tactical camera (look around / tilt). Off restores the stock middle-mouse zoom-out.");
                        break;
                    case nameof(InvertY):
                        field.GetText = () => Loc.Get("FREECAM_InvertY", "Invert Y axis");
                        field.GetDescription = () => Loc.Get("FREECAM_InvertY_DESCRIPTION",
                            "Flip the up/down drag direction when tilting the camera.");
                        break;
                    case nameof(Wheel):
                        field.GetText = () => Loc.Get("FREECAM_Wheel", "Scroll wheel mode");
                        field.GetDescription = () => Loc.Get("FREECAM_Wheel_DESCRIPTION",
                            "Zoom: wheel zooms, modifier+wheel changes floor. Floors: wheel changes floor, modifier+wheel zooms.");
                        break;
                    case nameof(FloorKey):
                        field.GetText = () => Loc.Get("FREECAM_FloorKey", "Floor modifier key");
                        field.GetDescription = () => Loc.Get("FREECAM_FloorKey_DESCRIPTION",
                            "Hold this key while scrolling to swap the wheel's meaning (zoom <-> change floor). Default Alt. Avoid Ctrl: Ctrl+wheel is the game's overwatch-cone spread control and will collide.");
                        break;
                    case nameof(InvertZoom):
                        field.GetText = () => Loc.Get("FREECAM_InvertZoom", "Invert wheel zoom");
                        field.GetDescription = () => Loc.Get("FREECAM_InvertZoom_DESCRIPTION",
                            "Flip which scroll direction zooms in versus out.");
                        break;
                    case nameof(InvertFloor):
                        field.GetText = () => Loc.Get("FREECAM_InvertFloor", "Invert wheel floor");
                        field.GetDescription = () => Loc.Get("FREECAM_InvertFloor_DESCRIPTION",
                            "Flip which scroll direction climbs a floor versus descends one.");
                        break;
                    case nameof(SensitivityX):
                        field.GetText = () => Loc.Get("FREECAM_SensitivityX", "Horizontal sensitivity");
                        field.GetDescription = () => Loc.Get("FREECAM_SensitivityX_DESCRIPTION",
                            "Mouse sensitivity for left/right orbit (yaw). 1.0 = default. Must be greater than 0.");
                        break;
                    case nameof(SensitivityY):
                        field.GetText = () => Loc.Get("FREECAM_SensitivityY", "Vertical sensitivity");
                        field.GetDescription = () => Loc.Get("FREECAM_SensitivityY_DESCRIPTION",
                            "Mouse sensitivity for up/down tilt (pitch). 1.0 = default. Must be greater than 0.");
                        break;
                    case nameof(PitchMin):
                        field.GetText = () => Loc.Get("FREECAM_PitchMin", "Min pitch angle");
                        field.GetDescription = () => Loc.Get("FREECAM_PitchMin_DESCRIPTION",
                            "Lowest camera tilt angle in degrees (closer to horizontal). Kept within -89..89 and below the max.");
                        break;
                    case nameof(PitchMax):
                        field.GetText = () => Loc.Get("FREECAM_PitchMax", "Max pitch angle");
                        field.GetDescription = () => Loc.Get("FREECAM_PitchMax_DESCRIPTION",
                            "Highest camera tilt angle in degrees (closer to top-down). Kept within -89..89 and above the min.");
                        break;
                    case nameof(ZoomMin):
                        field.GetText = () => Loc.Get("FREECAM_ZoomMin", "Min zoom distance");
                        field.GetDescription = () => Loc.Get("FREECAM_ZoomMin_DESCRIPTION",
                            "Closest the camera may zoom in. Lower = closer to the ground. Must be greater than 0 and below the max.");
                        break;
                    case nameof(ZoomMax):
                        field.GetText = () => Loc.Get("FREECAM_ZoomMax", "Max zoom distance");
                        field.GetDescription = () => Loc.Get("FREECAM_ZoomMax_DESCRIPTION",
                            "Farthest the camera may zoom out. Higher = wider battlefield view. Must be above the min.");
                        break;
                    case nameof(ZoomFactor):
                        field.GetText = () => Loc.Get("FREECAM_ZoomFactor", "Wheel zoom factor");
                        field.GetDescription = () => Loc.Get("FREECAM_ZoomFactor_DESCRIPTION",
                            "Distance-proportional zoom: each scroll-wheel notch (and keyboard t/g) moves this fraction of the current distance, so the camera zooms fast when far and gently when near. ~0.12 = smooth. Higher = bigger jumps. Must be greater than 0.");
                        break;
                    case nameof(MinZoomStep):
                        field.GetText = () => Loc.Get("FREECAM_MinZoomStep", "Min zoom step");
                        field.GetDescription = () => Loc.Get("FREECAM_MinZoomStep_DESCRIPTION",
                            "Smallest distance one scroll notch may move, so the zoom never crawls when very close in. Must be greater than 0 and below the max step.");
                        break;
                    case nameof(MaxZoomStep):
                        field.GetText = () => Loc.Get("FREECAM_MaxZoomStep", "Max zoom step");
                        field.GetDescription = () => Loc.Get("FREECAM_MaxZoomStep_DESCRIPTION",
                            "Largest distance one scroll notch may move, so a far view does not jump the whole range in a single notch. Must be above the min step.");
                        break;
                }
            }
            return fields;
        }
    }
}
