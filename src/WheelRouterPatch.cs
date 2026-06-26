using Base.Cameras;
using Base.Input;
using HarmonyLib;
using UnityEngine;

namespace Morgott.FreeCamera
{
    /// <summary>
    /// Routes the mouse scroll wheel between camera zoom and building-floor slicing on the tactical
    /// <see cref="PlanarScrollCamera"/>, via a single Harmony prefix on the private
    /// <c>PlanarScrollCamera.HandleInput(InputEvent)</c>.
    ///
    /// GROUND TRUTH (extracted input map + decompile, 2026-06-26 — supersedes the earlier wrong model):
    ///  - The wheel is bound to the two AXIS actions "Scroll Zoom" and "Change Level" (both
    ///    <c>Mouse ScrollWheel : Axis</c>). It does NOT fire the discrete "Discrete Zoom In/Out" (t/g)
    ///    or "Change Level Ascend/Descend" (z/c) actions — those are keyboard-only. (Prior fixes failed
    ///    because they targeted those discrete events the wheel never produces.)
    ///  - In the TACTICAL camera "Scroll Zoom" is DEAD: <c>PlanarScrollCamera.HandleInput</c> never
    ///    reads it (only FirstPersonCamera / GeoscapeCamera handle "Scroll Zoom"). The only wheel event
    ///    with a side effect is "Change Level" (AxisUpdate), handled at <c>PlanarScrollCamera.cs:513</c>
    ///    — it moves the floor by <c>_floorHeight * sign(AxisValue)</c> when <c>MoveCursorWithJoyStick</c>
    ///    is true (the keyboard+mouse default). There is therefore NO native tactical wheel-zoom and no
    ///    scroll-mode field that produces one, so a bare-wheel ZOOM must be synthesized here.
    ///
    /// Mechanism: intercept the wheel's "Change Level" AxisUpdate event, resolve zoom-vs-floor via
    /// <see cref="OrbitInputMath.ResolveWheelAction"/>, then mutate the by-ref event in place and let
    /// the original method run (no re-dispatch, no reflection):
    ///  - Zoom: rewrite the event to a native "Discrete Zoom In/Out" Pressed event ⇒ the original drives
    ///    <c>_distanceToTarget</c> through the native zoom path (clamped by MaxZoomInLimit/MaxZoomOutLimit,
    ///    our close-zoom override applies) and the AxisUpdate floor branch is skipped (no floor move).
    ///  - Floor: honour InvertFloor by flipping <c>AxisValue</c>, then let the original run ⇒ exactly one
    ///    native floor step (OffsetCamera + bounds clamp + OnCameraChangeLevel, no zoom).
    /// Active only while orbit is enabled (matches <see cref="FreeCameraMain.OrbitEnabled"/>); disabled
    /// restores the stock wheel (native floor-slice).
    /// </summary>
    [HarmonyPatch(typeof(PlanarScrollCamera), "HandleInput")]
    internal static class WheelRouterPatch
    {
        // The wheel's only side-effecting event on the tactical camera (PlanarScrollCamera.cs:513).
        private const string WheelFloorAxisAction = "Change Level";

        // Native discrete-zoom actions the original method already handles (PlanarScrollCamera.cs:529/599
        // zoom-closer, :537/606 zoom-farther), clamped by MaxZoomInLimit / MaxZoomOutLimit.
        private const string DiscreteZoomInAction = "Discrete Zoom In";
        private const string DiscreteZoomOutAction = "Discrete Zoom Out";

        // The original parameter is named "ie"; Harmony injects it by name. Taken by ref so the in-place
        // zoom rewrite / floor-direction flip is seen by the original method.
        private static bool Prefix(ref InputEvent ie)
        {
            FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
            if (cfg == null || !cfg.EnableOrbit)
            {
                return true; // mod off -> stock wheel (native floor-slice)
            }

            // The mouse wheel reaches the tactical camera only as the "Change Level" axis event; every
            // other input event (keyboard t/g/z/c, Q/E, Select, gamepad, ...) passes straight through.
            if (ie.Type != InputEventType.AxisUpdate || ie.Name != WheelFloorAxisAction || ie.AxisValue == 0f)
            {
                return true;
            }

            int wheelDir = ie.AxisValue > 0f ? 1 : -1;
            bool modifierHeld = IsFloorModifierHeld(cfg.FloorKey);
            WheelResolution res = OrbitInputMath.ResolveWheelAction(
                cfg.Wheel, modifierHeld, wheelDir, cfg.InvertZoom, cfg.InvertFloor);

            if (res.Action == WheelAction.Zoom)
            {
                // Rewrite the notch into a native discrete-zoom press: the original method then zooms via
                // _distanceToTarget (MaxZoomInLimit/Out clamp) and, being no longer an AxisUpdate, never
                // takes the floor branch -> bare wheel zooms only.
                ie.Name = res.EffectiveDir > 0 ? DiscreteZoomInAction : DiscreteZoomOutAction;
                ie.Type = InputEventType.Pressed;
                ie.AxisChanged = false; // match a keyboard discrete-zoom press (no StopCameraChase)
                return true;
            }

            // Floor: the native "Change Level" branch moves by sign(AxisValue); flip it for InvertFloor so
            // the resolved direction wins, then let the original perform exactly one floor step.
            if ((ie.AxisValue > 0f) != (res.EffectiveDir > 0))
            {
                ie.AxisValue = -ie.AxisValue;
            }
            return true;
        }

        /// <summary>True while the configured floor-swap modifier key is held (either left/right variant).</summary>
        private static bool IsFloorModifierHeld(FloorModifier modifier)
        {
            switch (modifier)
            {
                case FloorModifier.Ctrl:
                    return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                case FloorModifier.Shift:
                    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case FloorModifier.Alt:
                default:
                    return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            }
        }
    }

    /// <summary>
    /// Re-applies the configured zoom-distance limits every time the tactical camera activates. The
    /// camera instance is re-created per mission, so a one-time field override would be lost; pushing
    /// <c>MaxZoomInLimit</c>/<c>MaxZoomOutLimit</c> from a postfix on
    /// <c>PlanarScrollCamera.OnActivate(bool)</c> survives that re-creation. (The live controller also
    /// pushes them on config change; this covers fresh missions.)
    /// </summary>
    [HarmonyPatch(typeof(PlanarScrollCamera), "OnActivate")]
    internal static class ZoomLimitReapplyPatch
    {
        private static void Postfix(PlanarScrollCamera __instance, bool activate)
        {
            if (!activate)
            {
                return;
            }
            FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
            if (cfg == null || !cfg.EnableOrbit)
            {
                return;
            }
            float min = cfg.ZoomMin;
            float max = cfg.ZoomMax;
            OrbitInputMath.SanitizeZoomLimits(ref min, ref max);
            __instance.MaxZoomInLimit = min;   // closest distance
            __instance.MaxZoomOutLimit = max;  // farthest distance
        }
    }
}
