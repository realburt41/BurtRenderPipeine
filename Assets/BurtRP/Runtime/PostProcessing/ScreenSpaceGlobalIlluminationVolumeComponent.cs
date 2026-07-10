using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationQuality")]
    public enum ScreenSpaceGlobalIlluminationQuality
    {
        Custom = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationResolution")]

    public enum ScreenSpaceGlobalIlluminationResolution
    {
        Full = 0,
        Half = 1
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationFinalGather")]
    public enum ScreenSpaceGlobalIlluminationFinalGather
    {
        ScreenProbe = 0,
        IrradianceField = 1
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationQualityParameter")]
    public sealed class ScreenSpaceGlobalIlluminationQualityParameter : VolumeParameter<ScreenSpaceGlobalIlluminationQuality>, IEquatable<ScreenSpaceGlobalIlluminationQualityParameter>
    {
        public ScreenSpaceGlobalIlluminationQualityParameter(ScreenSpaceGlobalIlluminationQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenSpaceGlobalIlluminationQualityParameter lhs, ScreenSpaceGlobalIlluminationQuality rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenSpaceGlobalIlluminationQualityParameter lhs, ScreenSpaceGlobalIlluminationQuality rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenSpaceGlobalIlluminationQualityParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenSpaceGlobalIlluminationQualityParameter);
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationResolutionParameter")]
    public sealed class ScreenSpaceGlobalIlluminationResolutionParameter : VolumeParameter<ScreenSpaceGlobalIlluminationResolution>, IEquatable<ScreenSpaceGlobalIlluminationResolutionParameter>
    {
        public ScreenSpaceGlobalIlluminationResolutionParameter(ScreenSpaceGlobalIlluminationResolution value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenSpaceGlobalIlluminationResolutionParameter lhs, ScreenSpaceGlobalIlluminationResolution rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenSpaceGlobalIlluminationResolutionParameter lhs, ScreenSpaceGlobalIlluminationResolution rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenSpaceGlobalIlluminationResolutionParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenSpaceGlobalIlluminationResolutionParameter);
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationFinalGatherParameter")]
    public sealed class ScreenSpaceGlobalIlluminationFinalGatherParameter : VolumeParameter<ScreenSpaceGlobalIlluminationFinalGather>, IEquatable<ScreenSpaceGlobalIlluminationFinalGatherParameter>
    {
        public ScreenSpaceGlobalIlluminationFinalGatherParameter(ScreenSpaceGlobalIlluminationFinalGather value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenSpaceGlobalIlluminationFinalGatherParameter lhs, ScreenSpaceGlobalIlluminationFinalGather rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenSpaceGlobalIlluminationFinalGatherParameter lhs, ScreenSpaceGlobalIlluminationFinalGather rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenSpaceGlobalIlluminationFinalGatherParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenSpaceGlobalIlluminationFinalGatherParameter);
        }
    }

    [Serializable]
    [VolumeComponentMenu("Rendering/Screen Space Global Illumination")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationVolumeComponent")]
    public sealed class ScreenSpaceGlobalIlluminationVolumeComponent : VolumeComponent
    {
        private const float Epsilon = 0.0001f;

        [Title("BurtRP BurtGI")]
        [InfoBox("Deferred v2.2 diffuse GI. It traces against the current screen color, temporally denoises the result, and guards thin edges against sky/surface leaks.")]
        public BoolParameter enabled = new BoolParameter(false);
        [InfoBox("Custom keeps the manual values below. Low/Medium/High apply conservative presets for resolution, trace and blur.")]
        public ScreenSpaceGlobalIlluminationQualityParameter quality = new ScreenSpaceGlobalIlluminationQualityParameter(ScreenSpaceGlobalIlluminationQuality.Medium);
        [InfoBox("Used by Custom. Low and Medium force Half for performance; High forces Full for quality.")]
        public ScreenSpaceGlobalIlluminationResolutionParameter resolution = new ScreenSpaceGlobalIlluminationResolutionParameter(ScreenSpaceGlobalIlluminationResolution.Half);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.6f, 0f, 4f);
        public ClampedFloatParameter radius = new ClampedFloatParameter(2f, 0.05f, 20f);
        public ClampedIntParameter sampleCount = new ClampedIntParameter(12, 1, 32);
        public ClampedIntParameter maxSteps = new ClampedIntParameter(8, 1, 64);
        public ClampedFloatParameter thickness = new ClampedFloatParameter(0.35f, 0.01f, 3f);
        public ClampedFloatParameter skyFallback = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter radianceClamp = new ClampedFloatParameter(8f, 0.1f, 64f);
        public ClampedFloatParameter normalWeight = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter distanceFade = new ClampedFloatParameter(80f, 1f, 800f);
        [Title("Spatial Denoise")]
        public BoolParameter blur = new BoolParameter(true);
        public ClampedFloatParameter blurSharpness = new ClampedFloatParameter(0.18f, 0f, 1f);
        public ClampedFloatParameter spatialDenoiseRadius = new ClampedFloatParameter(1.25f, 0.5f, 3f);
        public ClampedFloatParameter spatialDenoiseStrength = new ClampedFloatParameter(0.75f, 0f, 1f);
        [Title("Leak Guard")]
        public ClampedFloatParameter leakGuardStrength = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter edgeFadeStrength = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter normalConeTightness = new ClampedFloatParameter(0.55f, 0f, 1f);
        public ClampedFloatParameter skyEdgeSuppression = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Title("Temporal Denoise")]
        public BoolParameter temporalAccumulation = new BoolParameter(true);
        public ClampedFloatParameter temporalFeedback = new ClampedFloatParameter(0.86f, 0f, 0.98f);
        public ClampedFloatParameter temporalDepthRejection = new ClampedFloatParameter(0.02f, 0.001f, 0.2f);
        public ClampedFloatParameter temporalNormalRejection = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter temporalClamp = new ClampedFloatParameter(1f, 0.25f, 4f);
        public ClampedFloatParameter temporalVarianceClamp = new ClampedFloatParameter(1.25f, 0f, 4f);
        public ClampedFloatParameter temporalHitRejection = new ClampedFloatParameter(0.55f, 0f, 1f);

        [Title("ScreenProbe Lite")]
        [InfoBox("XGI-style ScreenProbe path. It gathers a low-resolution probe grid, traces a compact octahedral atlas, and blends the resulting irradiance into BurtGI when Apply Strength is above zero.")]
        public BoolParameter screenProbeLite = new BoolParameter(false);
        public ClampedIntParameter screenProbeSpacingPixels = new ClampedIntParameter(16, 4, 64);
        public ClampedFloatParameter screenProbeTraceDistance = new ClampedFloatParameter(12f, 0.5f, 80f);
        public ClampedIntParameter screenProbeSampleCount = new ClampedIntParameter(8, 1, 32);
        public ClampedFloatParameter screenProbeTemporalFeedback = new ClampedFloatParameter(0.9f, 0f, 0.98f);
        public ClampedFloatParameter screenProbeApplyStrength = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedIntParameter screenProbeSpatialFilterPasses = new ClampedIntParameter(3, 0, 4);
        public ClampedIntParameter screenProbeSpatialFilterHalfKernelSize = new ClampedIntParameter(1, 0, 2);
        public BoolParameter screenProbeFixupBorders = new BoolParameter(true);

        [Title("XGI Final Gather")]
        [InfoBox("ScreenProbe gathers the filtered probe atlas. IrradianceField directly interpolates the Radiance Cache ClipMap onto the full-resolution GBuffer, matching XRender's alternate final-gather path.")]
        public ScreenSpaceGlobalIlluminationFinalGatherParameter finalGather = new ScreenSpaceGlobalIlluminationFinalGatherParameter(ScreenSpaceGlobalIlluminationFinalGather.ScreenProbe);
        public ClampedFloatParameter irradianceFieldStrength = new ClampedFloatParameter(1f, 0f, 2f);

        [Title("Short Range AO")]
        [InfoBox("XGI-style near-field occlusion for indirect lighting, evaluated from the ScreenProbe trace-atlas bent-normal texture and its temporal history.")]
        public BoolParameter shortRangeAO = new BoolParameter(false);
        public ClampedFloatParameter shortRangeAOWeight = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter shortRangeAOSlopeCompareToleranceScale = new ClampedFloatParameter(0.5f, 0f, 2f);

        public bool IsEnabled()
        {
            return active && enabled.value && intensity.value > Epsilon && radius.value > Epsilon && sampleCount.value > 0;
        }
    }
}
