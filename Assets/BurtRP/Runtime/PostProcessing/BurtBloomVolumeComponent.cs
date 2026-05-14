using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Post Processing/Bloom")]
    public sealed class BurtBloomVolumeComponent : VolumeComponent
    {
        private const float IntensityEpsilon = 0.0001f;

        [Title("BurtRP Bloom")]
        [InfoBox("Requests a temporary mip chain inside Burt post-processing and composites Bloom back before tonemapping.")]
        public ClampedFloatParameter threshold = new ClampedFloatParameter(BurtBloomSettings.DefaultThreshold, 0f, 10f);
        public ClampedFloatParameter softKnee = new ClampedFloatParameter(BurtBloomSettings.DefaultSoftKnee, 0f, 1f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 10f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(BurtBloomSettings.DefaultScatter, 0f, 1f);
        public ClampedIntParameter maxIterations = new ClampedIntParameter(BurtBloomSettings.DefaultMaxMipCount, 1, 8);

        public bool IsEnabled()
        {
            return active && intensity.value > IntensityEpsilon;
        }
    }
}
