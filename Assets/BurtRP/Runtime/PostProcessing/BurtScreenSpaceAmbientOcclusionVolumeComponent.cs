using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtScreenSpaceAmbientOcclusionQuality
    {
        Custom = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum BurtScreenSpaceAmbientOcclusionAlgorithm
    {
        SSAO = 0,
        GTAO = 1,
        HBAO = 2
    }

    [Serializable]
    public sealed class BurtScreenSpaceAmbientOcclusionQualityParameter : VolumeParameter<BurtScreenSpaceAmbientOcclusionQuality>, IEquatable<BurtScreenSpaceAmbientOcclusionQualityParameter>
    {
        public BurtScreenSpaceAmbientOcclusionQualityParameter(BurtScreenSpaceAmbientOcclusionQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BurtScreenSpaceAmbientOcclusionQualityParameter lhs, BurtScreenSpaceAmbientOcclusionQuality rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BurtScreenSpaceAmbientOcclusionQualityParameter lhs, BurtScreenSpaceAmbientOcclusionQuality rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BurtScreenSpaceAmbientOcclusionQualityParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BurtScreenSpaceAmbientOcclusionQualityParameter);
        }
    }

    [Serializable]
    public sealed class BurtScreenSpaceAmbientOcclusionAlgorithmParameter : VolumeParameter<BurtScreenSpaceAmbientOcclusionAlgorithm>, IEquatable<BurtScreenSpaceAmbientOcclusionAlgorithmParameter>
    {
        public BurtScreenSpaceAmbientOcclusionAlgorithmParameter(BurtScreenSpaceAmbientOcclusionAlgorithm value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BurtScreenSpaceAmbientOcclusionAlgorithmParameter lhs, BurtScreenSpaceAmbientOcclusionAlgorithm rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BurtScreenSpaceAmbientOcclusionAlgorithmParameter lhs, BurtScreenSpaceAmbientOcclusionAlgorithm rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BurtScreenSpaceAmbientOcclusionAlgorithmParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BurtScreenSpaceAmbientOcclusionAlgorithmParameter);
        }
    }

    [Serializable]
    [VolumeComponentMenu("BurtRP/Rendering/Screen Space Ambient Occlusion")]
    public sealed class BurtScreenSpaceAmbientOcclusionVolumeComponent : VolumeComponent
    {
        private const float Epsilon = 0.0001f;

        [Title("BurtRP Screen Space Ambient Occlusion")]
        [InfoBox("Deferred SSAO reads CameraDepth + GBuffer normal and outputs a screen-space AO texture. Default is off.")]
        public BoolParameter enabled = new BoolParameter(false);
        [InfoBox("Custom keeps the manual values below. Low/Medium/High apply conservative algorithm-aware trace, denoise and temporal presets.")]
        public BurtScreenSpaceAmbientOcclusionQualityParameter quality = new BurtScreenSpaceAmbientOcclusionQualityParameter(BurtScreenSpaceAmbientOcclusionQuality.Medium);
        [InfoBox("SSAO preserves the existing trace path. GTAO and HBAO are optional experimental trace variants that reuse the same denoise and temporal resolve.")]
        public BurtScreenSpaceAmbientOcclusionAlgorithmParameter algorithm = new BurtScreenSpaceAmbientOcclusionAlgorithmParameter(BurtScreenSpaceAmbientOcclusionAlgorithm.SSAO);
        public ClampedFloatParameter gtaoStrength = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter hbaoStrength = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0.5f, 0f, 4f);
        public ClampedFloatParameter radius = new ClampedFloatParameter(2f, 0.01f, 8f);
        public ClampedIntParameter sampleCount = new ClampedIntParameter(16, 1, 32);
        public BoolParameter horizonSearch = new BoolParameter(true);
        public ClampedIntParameter directionCount = new ClampedIntParameter(2, 1, 8);
        public ClampedFloatParameter bias = new ClampedFloatParameter(0.003f, 0f, 0.2f);
        public ClampedFloatParameter power = new ClampedFloatParameter(2f, 0.1f, 16f);
        public BoolParameter halfResolution = new BoolParameter(true);
        public BoolParameter blur = new BoolParameter(true);
        public BoolParameter spatialDenoise = new BoolParameter(true);
        public BoolParameter temporalAccumulation = new BoolParameter(true);
        public ClampedFloatParameter temporalFeedback = new ClampedFloatParameter(0.78f, 0f, 0.98f);
        public ClampedFloatParameter temporalDepthRejection = new ClampedFloatParameter(0.012f, 0.001f, 0.2f);
        public ClampedFloatParameter temporalClamp = new ClampedFloatParameter(0.75f, 0f, 4f);
        public ClampedFloatParameter blurSharpness = new ClampedFloatParameter(0.12f, 0f, 1f);
        public ClampedFloatParameter thickness = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter fadeRadius = new ClampedFloatParameter(50f, 0.01f, 200f);
        public ClampedFloatParameter fadeDistance = new ClampedFloatParameter(80f, 0.01f, 800f);

        public bool IsEnabled()
        {
            return active && enabled.value && intensity.value > Epsilon && radius.value > Epsilon && sampleCount.value > 0;
        }
    }
}
