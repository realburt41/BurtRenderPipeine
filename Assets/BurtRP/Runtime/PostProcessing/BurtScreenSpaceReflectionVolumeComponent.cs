using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtScreenSpaceReflectionQuality
    {
        Custom = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    [Serializable]
    public sealed class BurtScreenSpaceReflectionQualityParameter : VolumeParameter<BurtScreenSpaceReflectionQuality>, IEquatable<BurtScreenSpaceReflectionQualityParameter>
    {
        public BurtScreenSpaceReflectionQualityParameter(BurtScreenSpaceReflectionQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(BurtScreenSpaceReflectionQualityParameter lhs, BurtScreenSpaceReflectionQuality rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(BurtScreenSpaceReflectionQualityParameter lhs, BurtScreenSpaceReflectionQuality rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(BurtScreenSpaceReflectionQualityParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BurtScreenSpaceReflectionQualityParameter);
        }
    }

    [Serializable]
    [VolumeComponentMenu("BurtRP/Rendering/Screen Space Reflections")]
    public sealed class BurtScreenSpaceReflectionVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Screen Space Reflections")]
        [InfoBox("Deferred SSR normally uses the stable mip0 path. HiZ trace is an explicit experimental path; debug views live in the Burt Shading Debug overlay.")]
        public BoolParameter enabled = new BoolParameter(false);
        [InfoBox("Custom keeps the manual values below. Low/Medium/High apply XRender-style presets for trace and temporal quality without enabling experimental HiZ automatically.")]
        public BurtScreenSpaceReflectionQualityParameter quality = new BurtScreenSpaceReflectionQualityParameter(BurtScreenSpaceReflectionQuality.Custom);
        public ClampedIntParameter maxSteps = new ClampedIntParameter(48, 1, 512);
        public ClampedFloatParameter maxDistance = new ClampedFloatParameter(30f, 0.01f, 200f);
        public ClampedFloatParameter thickness = new ClampedFloatParameter(0.35f, 0.0001f, 5f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter roughnessFade = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Title("Experimental")]
        [InfoBox("Default off. When enabled, production SSR reads the HiZ pyramid and uses the guarded HiZ trace candidate.")]
        public BoolParameter experimentalHiZTrace = new BoolParameter(false);

        [Title("Temporal Denoise")]
        public BoolParameter temporalAccumulation = new BoolParameter(true);
        public ClampedFloatParameter temporalFeedback = new ClampedFloatParameter(0.86f, 0f, 0.98f);
        public ClampedFloatParameter temporalDepthRejection = new ClampedFloatParameter(0.02f, 0.001f, 0.2f);
        public ClampedFloatParameter temporalClamp = new ClampedFloatParameter(1f, 0.25f, 4f);

        public bool IsEnabled()
        {
            return active && enabled.value && intensity.value > 0.0001f;
        }
    }
}
