using Base.Cameras;
using Base.Input;
using HarmonyLib;

namespace Morgott.FreeCamera
{
    /// <summary>
    /// Suppresses the stock middle-mouse "zoom all the way out" so holding MMB to orbit no longer yanks
    /// the camera back. Surgical: a Prefix on <c>PlanarScrollCamera.HandleZoomRotateSelect(InputEvent)</c>
    /// that swallows ONLY the <c>"Mouse Scroll Zoom Out"</c> action (both Pressed and Released, so the
    /// net distance change is zero); every other input event falls through to the original.
    /// Active only while orbit is enabled (<see cref="FreeCameraMain.SuppressMmbZoom"/>).
    /// </summary>
    [HarmonyPatch(typeof(PlanarScrollCamera), "HandleZoomRotateSelect")]
    internal static class MmbZoomSuppressPatch
    {
        // The original parameter is named "ie"; Harmony injects it by name.
        private static bool Prefix(InputEvent ie, ref bool __result)
        {
            if (FreeCameraMain.SuppressMmbZoom && ie.Name == "Mouse Scroll Zoom Out")
            {
                __result = true;   // mark the event consumed so nothing else reacts to it
                return false;      // skip the original method (no distance change)
            }
            return true;           // run the original for all other events
        }
    }
}
