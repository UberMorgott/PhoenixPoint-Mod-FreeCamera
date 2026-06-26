using System.Reflection;
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

        // The engine applies DiscreteZoomIn/OutIncrement TWICE per discrete-zoom event (once in
        // HandleZoomRotateSelect at cs:603, once in the HandleInput Pressed branch at cs:533), so the
        // per-apply increment we write is HALF the intended net per-notch step.
        private const bool DoubleApplied = true;

        // Reflection handles for reading the live zoom distance (PlanarScrollCamera._distanceToTarget is a
        // private DampedFloat struct; its public Target field is the engine's stepping value). Resolved
        // once; the types never change at runtime.
        private static FieldInfo _distanceToTargetField; // PlanarScrollCamera._distanceToTarget (DampedFloat)
        private static FieldInfo _dampedTargetField;     // DampedFloat.Target (float)

        // The original parameter is named "ie"; Harmony injects it by name. Taken by ref so the in-place
        // zoom rewrite / floor-direction flip is seen by the original method. __instance is the live
        // camera (its current distance drives the proportional zoom step).
        private static bool Prefix(PlanarScrollCamera __instance, ref InputEvent ie)
        {
            FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
            if (cfg == null || !cfg.EnableOrbit)
            {
                return true; // mod off -> stock wheel (native floor-slice)
            }

            // Case B: a native keyboard t/g discrete-zoom press. The wheel routes through Case A below;
            // t/g arrive directly as these events. Make t/g distance-proportional too (shared feel) by
            // setting the per-notch increment from the current distance before the original applies it.
            if (ie.Type == InputEventType.Pressed
                && (ie.Name == DiscreteZoomInAction || ie.Name == DiscreteZoomOutAction))
            {
                ApplyProportionalZoomStep(__instance, cfg);
                return true;
            }

            // Case A: the mouse wheel reaches the tactical camera only as the "Change Level" axis event;
            // every other input event (Q/E, Select, gamepad, ...) passes straight through.
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
                // Set the distance-proportional per-notch increment BEFORE the rewrite, so the original's
                // double-apply (cs:603 + cs:533) sums to the intended proportional step for THIS notch.
                ApplyProportionalZoomStep(__instance, cfg);
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

        /// <summary>
        /// Write the distance-proportional per-notch increment onto the camera's
        /// <c>DiscreteZoomIn/OutIncrement</c> for the discrete-zoom event about to run. Net travel is
        /// <c>distance × ZoomFactor</c> clamped to [MinZoomStep, MaxZoomStep]; the value written is half
        /// that (the engine applies it twice). Both increments are set to the same value so zoom-in and
        /// zoom-out share the step (the engine's zoom-out path also reads <c>DiscreteZoomInIncrement</c>).
        /// </summary>
        private static void ApplyProportionalZoomStep(PlanarScrollCamera cam, FreeCameraConfig cfg)
        {
            float distance = ReadCurrentDistance(cam);
            float perApply = OrbitInputMath.ComputeProportionalZoomStep(
                distance, cfg.ZoomFactor, cfg.MinZoomStep, cfg.MaxZoomStep, DoubleApplied);
            cam.DiscreteZoomInIncrement = perApply;
            cam.DiscreteZoomOutIncrement = perApply;
        }

        /// <summary>
        /// Read the camera's live zoom distance (<c>_distanceToTarget.Target</c> — the engine's own
        /// stepping value, matching the clamp checks at cs:531/601). Reflection is cached. Fail-open: any
        /// failure falls back to the public <c>DistanceToTarget</c> getter (the damped current distance),
        /// so the proportional feel still holds.
        /// </summary>
        private static float ReadCurrentDistance(PlanarScrollCamera cam)
        {
            try
            {
                if (_distanceToTargetField == null)
                {
                    _distanceToTargetField = typeof(PlanarScrollCamera)
                        .GetField("_distanceToTarget", BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_distanceToTargetField != null)
                {
                    object boxed = _distanceToTargetField.GetValue(cam); // boxed DampedFloat struct
                    if (boxed != null)
                    {
                        if (_dampedTargetField == null)
                        {
                            _dampedTargetField = boxed.GetType().GetField("Target"); // public float field
                        }
                        if (_dampedTargetField != null)
                        {
                            return (float)_dampedTargetField.GetValue(boxed);
                        }
                    }
                }
            }
            catch
            {
                // fall through to the public getter
            }
            return cam.DistanceToTarget;
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
    /// <c>PlanarScrollCamera.OnActivate(bool)</c> survives that re-creation. The per-notch
    /// <c>DiscreteZoomIn/OutIncrement</c> is NOT set here — it is distance-proportional and recomputed on
    /// every notch / t-g press in <see cref="WheelRouterPatch"/>. (The live controller also pushes the
    /// range on config change; this covers fresh missions.)
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

    /// <summary>
    /// Keeps the user's configured close zoom (<c>ZoomMin</c>) reachable across new turns.
    ///
    /// RCA (decompile, 2026-06-26): min zoom is NOT tied to floor height. <c>_floorHeight</c> only ever
    /// moves the look-at target / camera bounds (PlanarScrollCamera.cs:517/553/566/924/928 and :368-370),
    /// never <c>_distanceToTarget</c> — so raising the floor does not move the near-zoom bound. What
    /// actually "resets" the close zoom is the tactical UI: every new turn,
    /// <c>TacticalView.OnNewTurn</c> calls
    /// <c>PlanarScrollCamera.ClampCameraDistance(InitialDistanceToTarget, +Inf)</c>
    /// (TacticalView.cs:1164), forcing the minimum distance up to the prefab's InitialDistanceToTarget
    /// (17), which overrides our <c>MaxZoomInLimit = ZoomMin</c> and yanks the camera back out.
    ///
    /// Fix: a prefix that lowers that forced minimum to the user's ZoomMin (never raising it), so the
    /// native <c>Clamp</c> keeps whatever close distance the camera already had (≥ ZoomMin) instead of
    /// snapping out to 17. Zoom-out and floor behaviour are untouched. Active only while orbit is on.
    /// </summary>
    [HarmonyPatch(typeof(PlanarScrollCamera), "ClampCameraDistance", new[] { typeof(float), typeof(float) })]
    internal static class DistanceClampMinPatch
    {
        private static void Prefix(ref float minDistance)
        {
            FreeCameraConfig cfg = FreeCameraMain.Instance?.Config;
            if (cfg == null || !cfg.EnableOrbit)
            {
                return;
            }
            float min = cfg.ZoomMin;
            float max = cfg.ZoomMax;
            OrbitInputMath.SanitizeZoomLimits(ref min, ref max);
            // Only ever lower the enforced floor toward ZoomMin; never raise a caller's tighter bound.
            if (minDistance > min)
            {
                minDistance = min;
            }
        }
    }
}
