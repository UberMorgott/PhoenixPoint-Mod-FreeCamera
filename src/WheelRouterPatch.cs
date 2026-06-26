using System.Reflection;
using Base.Cameras;
using Base.Input;
using HarmonyLib;
using UnityEngine;

namespace Morgott.FreeCamera
{
    /// <summary>
    /// Splits the scroll wheel between camera zoom and floor-slicing. A single Harmony prefix on
    /// <c>PlanarScrollCamera.HandleInput(InputEvent)</c> is the cleanest interception point: the wheel
    /// produces "Discrete Zoom In/Out" Pressed events, and <c>HandleInput</c> is the ONE method that
    /// processes them — it processes a single notch in TWO places (its own zoom block at ~:529-543
    /// AND the <c>HandleZoomRotateSelect</c> call it makes at ~:599-609). Patching <c>HandleInput</c>
    /// therefore dominates both sites with one prefix, avoiding the double-zoom leak.
    ///
    /// Per <see cref="OrbitInputMath.ResolveWheelAction"/> the notch resolves to Zoom or Floor:
    ///  - Floor: synthesize the native "Change Level Ascend/Descend" event and re-dispatch it through
    ///    <c>HandleInput</c> (reusing every native side-effect: OffsetCamera, bounds clamp,
    ///    OnCameraChangeLevel), then SKIP the original so neither zoom site fires.
    ///  - Zoom: optionally swap the event name (InvertZoom) and let the original run; the swap is seen
    ///    by both zoom sites because the event struct is passed by ref.
    /// Active only while orbit is enabled (matches <see cref="FreeCameraMain.OrbitEnabled"/>).
    /// </summary>
    [HarmonyPatch(typeof(PlanarScrollCamera), "HandleInput")]
    internal static class WheelRouterPatch
    {
        private const string ZoomInAction = "Discrete Zoom In";
        private const string ZoomOutAction = "Discrete Zoom Out";
        private const string FloorAscendAction = "Change Level Ascend";
        private const string FloorDescendAction = "Change Level Descend";
        private const string FloorAxisAction = "Change Level"; // gamepad/axis floor (PlanarScrollCamera.cs:513)

        // Cached handle to the private PlanarScrollCamera.HandleInput so a synthesized floor event can
        // be pushed back through the full native pipeline (resolved once; the type never changes).
        private static MethodInfo _handleInputMethod;

        // Re-entrancy guard: true only while DispatchHandleInput re-pushes our OWN synthesized floor
        // event. The floor-swallow branch consults it so our event reaches the native floor logic
        // (exactly one step) instead of being suppressed like a stray native wheel-driven floor event.
        private static bool _dispatchingFloor;

        // Time.frameCount of the most recent wheel notch (a Discrete Zoom In/Out event) the prefix saw.
        // A native "Change Level" floor event fired by the same physical notch is recognised as
        // wheel-driven even if the input pump dispatches it BEFORE the zoom event in that frame.
        private static int _lastWheelNotchFrame = -1;

        // The original parameter is named "ie"; Harmony injects it by name. Taken by ref so an
        // InvertZoom name-swap is visible to the original method (both zoom sites read ie.Name).
        private static bool Prefix(PlanarScrollCamera __instance, ref InputEvent ie, ref bool __result)
        {
            FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
            if (cfg == null || !cfg.EnableOrbit)
            {
                return true; // mod off -> stock wheel behaviour
            }

            // --- Floor events: a bare wheel notch must NEVER slice floors regardless of how the wheel is
            // bound. This prefix sees every event the camera processes, so it is binding-independent: any
            // native "Change Level" floor event that is wheel-driven this frame is swallowed (the router
            // owns wheel->floor and synthesizes the step itself). A keyboard/gamepad floor key (no wheel
            // notch this frame) and our own re-dispatched synthetic event pass straight through.
            bool isFloorPress = ie.Type == InputEventType.Pressed
                && (ie.Name == FloorAscendAction || ie.Name == FloorDescendAction);
            bool isFloorAxis = ie.Type == InputEventType.AxisUpdate && ie.Name == FloorAxisAction;
            if (isFloorPress || isFloorAxis)
            {
                if (OrbitInputMath.ShouldSwallowFloorEvent(WheelNotchActiveThisFrame(), _dispatchingFloor))
                {
                    __result = true; // consume so nothing else slices the floor
                    return false;    // skip native floor for a bare/zoom wheel notch
                }
                return true; // keyboard/gamepad floor key, or our own synthesized floor step -> native floor
            }

            if (ie.Type != InputEventType.Pressed)
            {
                return true; // only the discrete press carries a wheel notch
            }

            bool isZoomIn = ie.Name == ZoomInAction;
            bool isZoomOut = ie.Name == ZoomOutAction;
            if (!isZoomIn && !isZoomOut)
            {
                return true; // not a wheel notch (e.g. our own re-dispatched floor event) -> run original
            }

            _lastWheelNotchFrame = Time.frameCount; // mark: a wheel notch occurred this frame

            int wheelDir = isZoomIn ? 1 : -1;
            bool modifierHeld = IsFloorModifierHeld(cfg.FloorKey);
            WheelResolution res = OrbitInputMath.ResolveWheelAction(
                cfg.Wheel, modifierHeld, wheelDir, cfg.InvertZoom, cfg.InvertFloor);

            if (res.Action == WheelAction.Floor)
            {
                string floorName = res.EffectiveDir >= 0 ? FloorAscendAction : FloorDescendAction;
                InputEvent floorEvent = default(InputEvent);
                floorEvent.Name = floorName;
                floorEvent.Type = InputEventType.Pressed;
                floorEvent.InputType = InputType.KeyboardMouse;
                DispatchHandleInput(__instance, floorEvent); // one floor step; any native wheel floor is swallowed above

                __result = true; // mark the wheel notch consumed
                return false;    // skip the original -> neither native zoom site runs
            }

            // Zoom: apply InvertZoom by rewriting the notch direction, then let the original proceed.
            if (res.EffectiveDir != wheelDir)
            {
                ie.Name = res.EffectiveDir >= 0 ? ZoomInAction : ZoomOutAction;
            }
            return true;
        }

        /// <summary>
        /// True when a physical mouse-wheel notch is happening in the current frame, used to recognise a
        /// wheel-driven native floor event. Ordering-independent: matches a Discrete-Zoom event already
        /// seen this frame, OR a live mouse-scroll delta (covers a native floor event dispatched before
        /// the zoom event). A frame with no scroll (a keyboard floor key) yields false.
        /// </summary>
        private static bool WheelNotchActiveThisFrame()
        {
            if (_lastWheelNotchFrame == Time.frameCount)
            {
                return true;
            }
            return Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f;
        }

        /// <summary>True while the configured floor-swap modifier key is held (either left/right variant).</summary>
        private static bool IsFloorModifierHeld(FloorModifier modifier)
        {
            switch (modifier)
            {
                case FloorModifier.Alt:
                    return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                case FloorModifier.Shift:
                    return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                case FloorModifier.Ctrl:
                default:
                    return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            }
        }

        /// <summary>
        /// Re-dispatch a synthesized event through the camera's private <c>HandleInput</c>. The patched
        /// method runs again, but the event is a floor event (not a wheel notch) so this prefix passes
        /// it straight through to the native floor logic -> no recursion. Never throws out of input.
        /// </summary>
        private static void DispatchHandleInput(PlanarScrollCamera camera, InputEvent ev)
        {
            try
            {
                if (_handleInputMethod == null)
                {
                    _handleInputMethod = AccessTools.Method(typeof(PlanarScrollCamera), "HandleInput");
                }
                _dispatchingFloor = true; // mark our re-entry so the floor-swallow branch lets this event through
                _handleInputMethod?.Invoke(camera, new object[] { ev });
            }
            catch
            {
                // Reflection guard: a failed dispatch must not break the input pump.
            }
            finally
            {
                _dispatchingFloor = false;
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
