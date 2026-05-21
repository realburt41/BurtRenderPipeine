using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtScreenSpaceGlobalIlluminationQuality
    {
        Custom = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum BurtScreenSpaceGlobalIlluminationResolution
    {
        Full = 0,
        Half = 1
    }

    [Serializable]
    public sealed class BurtScreenSpaceGlobalIlluminationQualityParameter : VolumeParameter<BurtScreenSpaceGlobalIlluminationQuality>, IEquatable<BurtScreenSpaceGlobalIlluminationQualityParameter>
    {
        public BurtScreenSpaceGlobalIlluminationQualityParameter(BurtScreenSpaceGlobalIlluminationQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BurtScreenSpaceGlobalIlluminationQualityParameter lhs, BurtScreenSpaceGlobalIlluminationQuality rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BurtScreenSpaceGlobalIlluminationQualityParameter lhs, BurtScreenSpaceGlobalIlluminationQuality rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BurtScreenSpaceGlobalIlluminationQualityParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BurtScreenSpaceGlobalIlluminationQualityParameter);
        }
    }

    [Serializable]
    public sealed class BurtScreenSpaceGlobalIlluminationResolutionParameter : VolumeParameter<BurtScreenSpaceGlobalIlluminationResolution>, IEquatable<BurtScreenSpaceGlobalIlluminationResolutionParameter>
    {
        public BurtScreenSpaceGlobalIlluminationResolutionParameter(BurtScreenSpaceGlobalIlluminationResolution value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BurtScreenSpaceGlobalIlluminationResolutionParameter lhs, BurtScreenSpaceGlobalIlluminationResolution rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BurtScreenSpaceGlobalIlluminationResolutionParameter lhs, BurtScreenSpaceGlobalIlluminationResolution rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BurtScreenSpaceGlobalIlluminationResolutionParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BurtScreenSpaceGlobalIlluminationResolutionParameter);
        }
    }

    [Serializable]
    [VolumeComponentMenu("BurtRP/Rendering/BurtGI")]
    public sealed class BurtScreenSpaceGlobalIlluminationVolumeComponent : VolumeComponent
    {
        private const float Epsilon = 0.0001f;

        [Title("BurtRP BurtGI")]
        [InfoBox("Deferred v2.2 diffuse GI. It traces against the current screen color, temporally denoises the result, and guards thin edges against sky/surface leaks.")]
        public BoolParameter enabled = new BoolParameter(false);
        [InfoBox("Custom keeps the manual values below. Low/Medium/High apply conservative presets for resolution, trace and blur.")]
        public BurtScreenSpaceGlobalIlluminationQualityParameter quality = new BurtScreenSpaceGlobalIlluminationQualityParameter(BurtScreenSpaceGlobalIlluminationQuality.Medium);
        [InfoBox("Used by Custom. Low and Medium force Half for performance; High forces Full for quality.")]
        public BurtScreenSpaceGlobalIlluminationResolutionParameter resolution = new BurtScreenSpaceGlobalIlluminationResolutionParameter(BurtScreenSpaceGlobalIlluminationResolution.Half);
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

        public bool IsEnabled()
        {
            return active && enabled.value && intensity.value > Epsilon && radius.value > Epsilon && sampleCount.value > 0;
        }
    }
}
