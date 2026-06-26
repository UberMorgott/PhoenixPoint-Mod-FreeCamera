using System.IO;
using System.Reflection;
using System.Text;
using Base.Levels;
using HarmonyLib;
using I2.Loc;
using PhoenixPoint.Modding;
using UnityEngine;

namespace Morgott.FreeCamera
{
    /// <summary>
    /// Mod entry point. Installs the Harmony patches, attaches the per-frame orbit controller to the
    /// mod GameObject, and switches the controller on/off as tactical missions start and end.
    /// </summary>
    public class FreeCameraMain : ModMain
    {
        public static new FreeCameraMain Instance { get; private set; }

        /// <summary>Strongly-typed view of the mod's config (declared public fields auto-exposed in-game).</summary>
        public new FreeCameraConfig Config => (FreeCameraConfig)base.Config;

        public override bool CanSafelyDisable => true;

        private FreeOrbitController _controller;

        /// <summary>
        /// Runtime read of the master orbit toggle. Defaults to false when the config is unavailable,
        /// so a missing config leaves the stock camera untouched. Read by the controller and the
        /// MMB-suppress patch.
        /// </summary>
        public static bool OrbitEnabled
        {
            get
            {
                FreeCameraMain inst = Instance;
                FreeCameraConfig cfg = inst != null ? inst.Config : null;
                return cfg != null && cfg.EnableOrbit;
            }
        }

        /// <summary>
        /// Whether to swallow the stock middle-mouse "Mouse Scroll Zoom Out" action. Only while orbit
        /// is enabled — disabling orbit restores vanilla MMB behaviour.
        /// </summary>
        public static bool SuppressMmbZoom => OrbitEnabled;

        public override void OnModEnabled()
        {
            Instance = this;
            ((Harmony)HarmonyInstance).PatchAll(Assembly.GetExecutingAssembly());
            LoadLocalization();
            SanitizeConfig();
            if (ModGO != null)
            {
                _controller = ModGO.GetComponent<FreeOrbitController>() ?? ModGO.AddComponent<FreeOrbitController>();
            }
        }

        public override void OnModDisabled()
        {
            if (_controller != null)
            {
                _controller.Deactivate();
                UnityEngine.Object.Destroy(_controller);
                _controller = null;
            }
            Instance = null;
        }

        public override void OnConfigChanged()
        {
            SanitizeConfig();
            _controller?.ApplyZoomLimits();
        }

        public override void OnLevelStateChanged(Level level, Level.State prevState, Level.State state)
        {
            // PlanarScrollCamera only exists/plays in tactical missions; the controller resolves it
            // lazily once active and no-ops if none is present.
            if (state == Level.State.Playing)
            {
                _controller?.Activate();
            }
            else if (prevState == Level.State.Playing)
            {
                _controller?.Deactivate();
            }
        }

        public override void OnLevelEnd(Level level)
        {
            _controller?.Deactivate();
        }

        /// <summary>
        /// Import the mod's localization terms from <c>Assets/Localization/FreeCamera_Localization.csv</c>
        /// (UTF-8) into I2's primary language source, so the in-game options labels/descriptions show in
        /// the active language (mirrors Oracle's / TFTV's AddLocalizationFromCSV). Fully guarded and
        /// fail-silent: a missing file or any import error simply leaves the UI on its English literal
        /// fallback (see <see cref="Loc"/>) rather than throwing out of OnModEnabled.
        /// </summary>
        private void LoadLocalization()
        {
            try
            {
                // base.Instance is the framework ModInstance (its Entry.Directory is the install dir);
                // the static FreeCameraMain.Instance shadows it, so qualify with base.
                string dir = base.Instance.Entry.Directory;
                string path = Path.Combine(dir, "Assets", "Localization", "FreeCamera_Localization.csv");
                if (!File.Exists(path))
                {
                    return;
                }

                string csv = File.ReadAllText(path, Encoding.UTF8);
                if (!csv.EndsWith("\n"))
                {
                    csv += "\n";
                }

                if (LocalizationManager.Sources == null || LocalizationManager.Sources.Count == 0)
                {
                    return;
                }

                LocalizationManager.Sources[0].Import_CSV(string.Empty, csv, eSpreadsheetUpdateMode.AddNewTerms, ',');
                LocalizationManager.LocalizeAll(true);
            }
            catch
            {
                // Localization is cosmetic: never let an import failure break mod enable.
            }
        }

        /// <summary>Clamp the user-typed config values into legal ranges (no sliders exist in-game).</summary>
        private void SanitizeConfig()
        {
            FreeCameraConfig cfg = Config;
            if (cfg == null)
            {
                return;
            }
            cfg.SensitivityX = OrbitInputMath.SanitizeSensitivity(cfg.SensitivityX);
            cfg.SensitivityY = OrbitInputMath.SanitizeSensitivity(cfg.SensitivityY);
            OrbitInputMath.SanitizePitchLimits(ref cfg.PitchMin, ref cfg.PitchMax);
            OrbitInputMath.SanitizeZoomLimits(ref cfg.ZoomMin, ref cfg.ZoomMax);
            OrbitInputMath.SanitizeProportionalZoom(ref cfg.ZoomFactor, ref cfg.MinZoomStep, ref cfg.MaxZoomStep);
        }
    }
}
