using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Post Processing/Bloom")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtBloomVolumeComponent")]
    public sealed class BloomVolumeComponent : VolumeComponent
    {
        private const float IntensityEpsilon = 0.0001f;

        [Title("BurtRP Bloom")]
        [InfoBox("Requests a temporary mip chain inside Burt post-processing and composites Bloom back before tonemapping.")]
        public ClampedFloatParameter threshold = new ClampedFloatParameter(BloomSettings.DefaultThreshold, -1f, 10f);
        public ClampedFloatParameter softKnee = new ClampedFloatParameter(BloomSettings.DefaultSoftKnee, 0f, 1f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 10f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(BloomSettings.DefaultScatter, 0f, 1f);
        public ClampedFloatParameter sizeScale = new ClampedFloatParameter(BloomSettings.DefaultSizeScale, 0f, 64f);
        public BloomQualityParameter quality = new BloomQualityParameter(BloomSettings.DefaultQuality);
        [LabelText("Max Iterations Cap")]
        public ClampedIntParameter maxIterations = new ClampedIntParameter(BloomSettings.DefaultMaxMipCount, 1, 8);
        public BoolParameter bloomAlphaChannel = new BoolParameter(false);
        public BloomDebugViewParameter debugView = new BloomDebugViewParameter(BloomSettings.DefaultDebugView);

        [Title("XRender PC Bloom Stages")]
        public ClampedFloatParameter filter1Size = new ClampedFloatParameter(BloomSettings.DefaultFilter1Size, 0f, 4f);
        public ClampedFloatParameter filter2Size = new ClampedFloatParameter(BloomSettings.DefaultFilter2Size, 0f, 8f);
        public ClampedFloatParameter filter3Size = new ClampedFloatParameter(BloomSettings.DefaultFilter3Size, 0f, 16f);
        public ClampedFloatParameter filter4Size = new ClampedFloatParameter(BloomSettings.DefaultFilter4Size, 0f, 32f);
        public ClampedFloatParameter filter5Size = new ClampedFloatParameter(BloomSettings.DefaultFilter5Size, 0f, 64f);
        public ClampedFloatParameter filter6Size = new ClampedFloatParameter(BloomSettings.DefaultFilter6Size, 0f, 128f);
        public ColorParameter filter1Tint = new ColorParameter(BloomSettings.DefaultFilter1Tint, true, false, false);
        public ColorParameter filter2Tint = new ColorParameter(BloomSettings.DefaultFilter2Tint, true, false, false);
        public ColorParameter filter3Tint = new ColorParameter(BloomSettings.DefaultFilter3Tint, true, false, false);
        public ColorParameter filter4Tint = new ColorParameter(BloomSettings.DefaultFilter4Tint, true, false, false);
        public ColorParameter filter5Tint = new ColorParameter(BloomSettings.DefaultFilter5Tint, true, false, false);
        public ColorParameter filter6Tint = new ColorParameter(BloomSettings.DefaultFilter6Tint, true, false, false);

        public bool IsEnabled()
        {
            return active && BloomSettings.IsQualityEnabled(quality.value) && intensity.value > IntensityEpsilon;
        }
    }
}
