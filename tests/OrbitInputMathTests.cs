using Morgott.FreeCamera;
using Xunit;

namespace Morgott.FreeCamera.Tests
{
    /// <summary>
    /// Unit tests for the pure orbit math. No Unity types are involved, so these run under net8
    /// against the linked production source (<c>..\src\OrbitInputMath.cs</c>).
    /// </summary>
    public class OrbitInputMathTests
    {
        private const float Eps = 1e-4f;

        // ---- PitchDelta -------------------------------------------------------------------

        [Fact]
        public void PitchDelta_NoInvert_KeepsSign()
        {
            float d = OrbitInputMath.PitchDelta(10f, 1f, invertY: false);
            Assert.Equal(10f * OrbitInputMath.BaseDegreesPerPixel, d, 4);
            Assert.True(d > 0f);
        }

        [Fact]
        public void PitchDelta_Invert_FlipsSign()
        {
            float normal = OrbitInputMath.PitchDelta(10f, 1f, invertY: false);
            float inverted = OrbitInputMath.PitchDelta(10f, 1f, invertY: true);
            Assert.Equal(-normal, inverted, 4);
        }

        [Fact]
        public void PitchDelta_ScalesWithSensitivity()
        {
            float low = OrbitInputMath.PitchDelta(10f, 1f, false);
            float high = OrbitInputMath.PitchDelta(10f, 2f, false);
            Assert.Equal(low * 2f, high, 4);
        }

        // ---- YawDelta ---------------------------------------------------------------------

        [Fact]
        public void YawDelta_ScalesWithSensitivityAndDelta()
        {
            Assert.Equal(20f * OrbitInputMath.BaseDegreesPerPixel, OrbitInputMath.YawDelta(20f, 1f), 4);
            Assert.Equal(20f * OrbitInputMath.BaseDegreesPerPixel * 1.5f, OrbitInputMath.YawDelta(20f, 1.5f), 4);
        }

        [Fact]
        public void YawDelta_NegativeDelta_NegativeResult()
        {
            Assert.True(OrbitInputMath.YawDelta(-5f, 1f) < 0f);
        }

        // ---- ClampPitch -------------------------------------------------------------------

        [Theory]
        [InlineData(45f, 5f, 85f, 45f)]   // inside
        [InlineData(2f, 5f, 85f, 5f)]     // below min
        [InlineData(90f, 5f, 85f, 85f)]   // above max
        [InlineData(5f, 5f, 85f, 5f)]     // at min (inclusive)
        [InlineData(85f, 5f, 85f, 85f)]   // at max (inclusive)
        [InlineData(120f, 5f, 85f, 85f)]  // far beyond
        public void ClampPitch_RespectsBounds(float pitch, float min, float max, float expected)
        {
            Assert.Equal(expected, OrbitInputMath.ClampPitch(pitch, min, max), 4);
        }

        [Fact]
        public void ClampPitch_SwappedBounds_StillClamps()
        {
            // min/max passed in reversed must behave as if ordered.
            Assert.Equal(85f, OrbitInputMath.ClampPitch(120f, 85f, 5f), 4);
            Assert.Equal(5f, OrbitInputMath.ClampPitch(-3f, 85f, 5f), 4);
        }

        // ---- WrapHeading ------------------------------------------------------------------

        [Theory]
        [InlineData(10f, 10f)]
        [InlineData(370f, 10f)]
        [InlineData(-10f, 350f)]
        [InlineData(720f, 0f)]
        public void WrapHeading_NormalizesTo0To360(float input, float expected)
        {
            Assert.Equal(expected, OrbitInputMath.WrapHeading(input), 3);
        }

        // ---- SanitizeSensitivity ----------------------------------------------------------

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void SanitizeSensitivity_Invalid_ResetsToDefault(float bad)
        {
            Assert.Equal(OrbitInputMath.DefaultSensitivity, OrbitInputMath.SanitizeSensitivity(bad), 4);
        }

        [Fact]
        public void SanitizeSensitivity_Valid_Unchanged()
        {
            Assert.Equal(2.5f, OrbitInputMath.SanitizeSensitivity(2.5f), 4);
        }

        // ---- ComputeProportionalZoomStep --------------------------------------------------

        [Fact]
        public void ProportionalZoom_ScalesWithDistance()
        {
            // In the un-clamped mid band, doubling the distance doubles the net step.
            float near = OrbitInputMath.ComputeProportionalZoomStep(20f, 0.12f, 0.3f, 8f, doubleApplied: false);
            float far = OrbitInputMath.ComputeProportionalZoomStep(40f, 0.12f, 0.3f, 8f, doubleApplied: false);
            Assert.Equal(20f * 0.12f, near, 4);
            Assert.Equal(40f * 0.12f, far, 4);
            Assert.Equal(near * 2f, far, 4);
        }

        [Fact]
        public void ProportionalZoom_NearZero_ClampedToMinStep()
        {
            // distance * factor would be ~0; the floor keeps the zoom from crawling.
            float step = OrbitInputMath.ComputeProportionalZoomStep(0.5f, 0.12f, 0.3f, 8f, doubleApplied: false);
            Assert.Equal(0.3f, step, 4);
        }

        [Fact]
        public void ProportionalZoom_VeryFar_CappedToMaxStep()
        {
            // distance * factor = 1000 * 0.12 = 120; the cap prevents a teleport.
            float step = OrbitInputMath.ComputeProportionalZoomStep(1000f, 0.12f, 0.3f, 8f, doubleApplied: false);
            Assert.Equal(8f, step, 4);
        }

        [Fact]
        public void ProportionalZoom_DoubleApplied_HalvesPerApply()
        {
            // The engine applies the increment twice, so the per-apply value is half the net step.
            float net = OrbitInputMath.ComputeProportionalZoomStep(20f, 0.12f, 0.3f, 8f, doubleApplied: false);
            float perApply = OrbitInputMath.ComputeProportionalZoomStep(20f, 0.12f, 0.3f, 8f, doubleApplied: true);
            Assert.Equal(net * 0.5f, perApply, 4);
            Assert.Equal(20f * 0.12f * 0.5f, perApply, 4);
        }

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        [InlineData(-50f)]
        public void ProportionalZoom_InvalidDistance_TreatedAsZero_FloorsToMinStep(float badDistance)
        {
            float step = OrbitInputMath.ComputeProportionalZoomStep(badDistance, 0.12f, 0.3f, 8f, doubleApplied: false);
            Assert.Equal(0.3f, step, 4);
        }

        [Fact]
        public void ProportionalZoom_InvalidTuning_UsesDefaults()
        {
            // factor<=0, minStep<=0, maxStep<=0 are all replaced by their defaults inside the computation.
            float step = OrbitInputMath.ComputeProportionalZoomStep(20f, -1f, 0f, float.NaN, doubleApplied: false);
            Assert.Equal(20f * OrbitInputMath.DefaultZoomFactor, step, 4);
        }

        // ---- SanitizeProportionalZoom -----------------------------------------------------

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void SanitizeProportionalZoom_InvalidFactor_ResetsToDefault(float badFactor)
        {
            float factor = badFactor, minStep = 0.3f, maxStep = 8f;
            OrbitInputMath.SanitizeProportionalZoom(ref factor, ref minStep, ref maxStep);
            Assert.Equal(OrbitInputMath.DefaultZoomFactor, factor, 4);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-2f)]
        [InlineData(float.NaN)]
        public void SanitizeProportionalZoom_InvalidSteps_ResetToDefaults(float bad)
        {
            float factor = 0.12f, minStep = bad, maxStep = bad;
            OrbitInputMath.SanitizeProportionalZoom(ref factor, ref minStep, ref maxStep);
            Assert.Equal(OrbitInputMath.DefaultMinZoomStep, minStep, 4);
            Assert.Equal(OrbitInputMath.DefaultMaxZoomStep, maxStep, 4);
        }

        [Fact]
        public void SanitizeProportionalZoom_SwappedSteps_Ordered()
        {
            float factor = 0.12f, minStep = 9f, maxStep = 2f;
            OrbitInputMath.SanitizeProportionalZoom(ref factor, ref minStep, ref maxStep);
            Assert.Equal(2f, minStep, 4);
            Assert.Equal(9f, maxStep, 4);
        }

        [Fact]
        public void SanitizeProportionalZoom_EqualSteps_MadeDistinct()
        {
            float factor = 0.12f, minStep = 4f, maxStep = 4f;
            OrbitInputMath.SanitizeProportionalZoom(ref factor, ref minStep, ref maxStep);
            Assert.True(minStep < maxStep);
        }

        [Fact]
        public void SanitizeProportionalZoom_ValidDefaults_Unchanged()
        {
            float factor = OrbitInputMath.DefaultZoomFactor,
                  minStep = OrbitInputMath.DefaultMinZoomStep,
                  maxStep = OrbitInputMath.DefaultMaxZoomStep;
            OrbitInputMath.SanitizeProportionalZoom(ref factor, ref minStep, ref maxStep);
            Assert.Equal(OrbitInputMath.DefaultZoomFactor, factor, 4);
            Assert.Equal(OrbitInputMath.DefaultMinZoomStep, minStep, 4);
            Assert.Equal(OrbitInputMath.DefaultMaxZoomStep, maxStep, 4);
        }

        [Fact]
        public void ProportionalZoom_DefaultIsSmoother_ThanStockPrefabStep()
        {
            // Stock prefab ships DiscreteZoomInIncrement = 3 (sharp). At the prefab's initial distance
            // (17) our default per-apply step must stay well below it for a gentler feel.
            float perApply = OrbitInputMath.ComputeProportionalZoomStep(
                17f, OrbitInputMath.DefaultZoomFactor, OrbitInputMath.DefaultMinZoomStep,
                OrbitInputMath.DefaultMaxZoomStep, doubleApplied: true);
            Assert.True(perApply > 0f);
            Assert.True(perApply < 3f);
        }

        // ---- SanitizePitchLimits ----------------------------------------------------------

        [Fact]
        public void SanitizePitchLimits_OrdersAscending()
        {
            float min = 85f, max = 5f;
            OrbitInputMath.SanitizePitchLimits(ref min, ref max);
            Assert.Equal(5f, min, 4);
            Assert.Equal(85f, max, 4);
        }

        [Fact]
        public void SanitizePitchLimits_ClampsToHardBand()
        {
            float min = -200f, max = 200f;
            OrbitInputMath.SanitizePitchLimits(ref min, ref max);
            Assert.Equal(OrbitInputMath.PitchHardMin, min, 4);
            Assert.Equal(OrbitInputMath.PitchHardMax, max, 4);
        }

        [Fact]
        public void SanitizePitchLimits_DegenerateEqual_FallsBackToFullBand()
        {
            float min = 40f, max = 40f;
            OrbitInputMath.SanitizePitchLimits(ref min, ref max);
            Assert.True(min < max);
            Assert.Equal(OrbitInputMath.PitchHardMin, min, 4);
            Assert.Equal(OrbitInputMath.PitchHardMax, max, 4);
        }

        // ---- SanitizeZoomLimits -----------------------------------------------------------

        [Fact]
        public void SanitizeZoomLimits_OrdersAscending()
        {
            float min = 55f, max = 3f;
            OrbitInputMath.SanitizeZoomLimits(ref min, ref max);
            Assert.Equal(3f, min, 4);
            Assert.Equal(55f, max, 4);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-5f)]
        [InlineData(float.NaN)]
        public void SanitizeZoomLimits_NonPositiveMin_Floored(float badMin)
        {
            float min = badMin, max = 55f;
            OrbitInputMath.SanitizeZoomLimits(ref min, ref max);
            Assert.True(min > 0f);
            Assert.True(min < max);
        }

        [Fact]
        public void SanitizeZoomLimits_EqualValues_MadeDistinct()
        {
            float min = 20f, max = 20f;
            OrbitInputMath.SanitizeZoomLimits(ref min, ref max);
            Assert.True(min < max);
        }

        [Fact]
        public void SanitizeZoomLimits_DefaultConfig_Unchanged()
        {
            float min = 3f, max = 55f;
            OrbitInputMath.SanitizeZoomLimits(ref min, ref max);
            Assert.Equal(3f, min, 4);
            Assert.Equal(55f, max, 4);
        }

        // ---- ResolveWheelAction: action truth table --------------------------------------

        [Theory]
        // Zoom mode: bare wheel = Zoom, modifier+wheel = Floor.
        [InlineData(WheelMode.Zoom, false, WheelAction.Zoom)]
        [InlineData(WheelMode.Zoom, true, WheelAction.Floor)]
        // Floors mode: the mirror.
        [InlineData(WheelMode.Floors, false, WheelAction.Floor)]
        [InlineData(WheelMode.Floors, true, WheelAction.Zoom)]
        public void ResolveWheelAction_ActionFromModeAndModifier(WheelMode mode, bool modifierHeld, WheelAction expected)
        {
            WheelResolution res = OrbitInputMath.ResolveWheelAction(mode, modifierHeld, wheelDir: 1, invertZoom: false, invertFloor: false);
            Assert.Equal(expected, res.Action);
        }

        // ---- ResolveWheelAction: direction passthrough (no invert) ------------------------

        [Theory]
        [InlineData(1, 1)]
        [InlineData(-1, -1)]
        public void ResolveWheelAction_NoInvert_KeepsDirection(int wheelDir, int expectedDir)
        {
            // Zoom action.
            WheelResolution zoom = OrbitInputMath.ResolveWheelAction(WheelMode.Zoom, false, wheelDir, invertZoom: false, invertFloor: false);
            Assert.Equal(WheelAction.Zoom, zoom.Action);
            Assert.Equal(expectedDir, zoom.EffectiveDir);

            // Floor action.
            WheelResolution floor = OrbitInputMath.ResolveWheelAction(WheelMode.Floors, false, wheelDir, invertZoom: false, invertFloor: false);
            Assert.Equal(WheelAction.Floor, floor.Action);
            Assert.Equal(expectedDir, floor.EffectiveDir);
        }

        // ---- ResolveWheelAction: invert flags flip only their own action ------------------

        [Theory]
        [InlineData(1)]
        [InlineData(-1)]
        public void ResolveWheelAction_InvertZoom_FlipsZoomOnly(int wheelDir)
        {
            // Zoom action: InvertZoom flips the effective direction.
            WheelResolution zoom = OrbitInputMath.ResolveWheelAction(WheelMode.Zoom, false, wheelDir, invertZoom: true, invertFloor: false);
            Assert.Equal(WheelAction.Zoom, zoom.Action);
            Assert.Equal(-wheelDir, zoom.EffectiveDir);

            // Floor action: InvertZoom must NOT affect it.
            WheelResolution floor = OrbitInputMath.ResolveWheelAction(WheelMode.Floors, false, wheelDir, invertZoom: true, invertFloor: false);
            Assert.Equal(WheelAction.Floor, floor.Action);
            Assert.Equal(wheelDir, floor.EffectiveDir);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(-1)]
        public void ResolveWheelAction_InvertFloor_FlipsFloorOnly(int wheelDir)
        {
            // Floor action: InvertFloor flips the effective direction.
            WheelResolution floor = OrbitInputMath.ResolveWheelAction(WheelMode.Floors, false, wheelDir, invertZoom: false, invertFloor: true);
            Assert.Equal(WheelAction.Floor, floor.Action);
            Assert.Equal(-wheelDir, floor.EffectiveDir);

            // Zoom action: InvertFloor must NOT affect it.
            WheelResolution zoom = OrbitInputMath.ResolveWheelAction(WheelMode.Zoom, false, wheelDir, invertZoom: false, invertFloor: true);
            Assert.Equal(WheelAction.Zoom, zoom.Action);
            Assert.Equal(wheelDir, zoom.EffectiveDir);
        }

        [Fact]
        public void ResolveWheelAction_ModifierSwap_UsesSwappedActionsInvert()
        {
            // Zoom mode + modifier held -> Floor action, so InvertFloor (not InvertZoom) applies.
            WheelResolution res = OrbitInputMath.ResolveWheelAction(WheelMode.Zoom, modifierHeld: true, wheelDir: 1, invertZoom: true, invertFloor: true);
            Assert.Equal(WheelAction.Floor, res.Action);
            Assert.Equal(-1, res.EffectiveDir); // flipped by invertFloor, unaffected by invertZoom
        }
    }
}
