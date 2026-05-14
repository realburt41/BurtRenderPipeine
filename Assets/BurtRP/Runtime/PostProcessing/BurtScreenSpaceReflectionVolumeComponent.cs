using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Rendering/Screen Space Reflections")]
    public sealed class BurtScreenSpaceReflectionVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Screen Space Reflections")]
        [InfoBox("Deferred SSR normally uses the stable mip0 path. HiZ trace is an explicit experimental path; debug views live in the Burt Shading Debug overlay.")]
        public BoolParameter enabled = new BoolParameter(false);
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
