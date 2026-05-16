using System;
using Sirenix.OdinInspector;
using UnityEngine;
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
        public ClampedFloatParameter threshold = new ClampedFloatParameter(BurtBloomSettings.DefaultThreshold, -1f, 10f);
        public ClampedFloatParameter softKnee = new ClampedFloatParameter(BurtBloomSettings.DefaultSoftKnee, 0f, 1f);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 10f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(BurtBloomSettings.DefaultScatter, 0f, 1f);
        public ClampedFloatParameter sizeScale = new ClampedFloatParameter(BurtBloomSettings.DefaultSizeScale, 0f, 64f);
        public BurtBloomQualityParameter quality = new BurtBloomQualityParameter(BurtBloomSettings.DefaultQuality);
        [LabelText("Max Iterations Cap")]
        public ClampedIntParameter maxIterations = new ClampedIntParameter(BurtBloomSettings.DefaultMaxMipCount, 1, 8);
        public BoolParameter bloomAlphaChannel = new BoolParameter(false);
        public BurtBloomDebugViewParameter debugView = new BurtBloomDebugViewParameter(BurtBloomSettings.DefaultDebugView);

        [Title("XRender PC Bloom Stages")]
        public ClampedFloatParameter filter1Size = new ClampedFloatParameter(BurtBloomSettings.DefaultFilter1Size, 0f, 4f);
        public ClampedFloatParameter filter2Size = new ClampedFloatParameter(BurtBloomSettings.DefaultFilter2Size, 0f, 8f);
        public ClampedFloatParameter filter3Size = new ClampedFloatParameter(BurtBloomSettings.DefaultFilter3Size, 0f, 16f);
        public ClampedFloatParameter filter4Size = new ClampedFloatParameter(BurtBloomSettings.DefaultFilter4Size, 0f, 32f);
        public ClampedFloatParameter filter5Size = new ClampedFloatParameter(BurtBloomSettings.DefaultFilter5Size, 0f, 64f);
        public ClampedFloatParameter filter6Size = new ClampedFloatParameter(BurtBloomSettings.DefaultFilter6Size, 0f, 128f);
        public ColorParameter filter1Tint = new ColorParameter(Color.white, true, false, false);
        public ColorParameter filter2Tint = new ColorParameter(Color.white, true, false, false);
        public ColorParameter filter3Tint = new ColorParameter(Color.white, true, false, false);
        public ColorParameter filter4Tint = new ColorParameter(Color.white, true, false, false);
        public ColorParameter filter5Tint = new ColorParameter(Color.white, true, false, false);
        public ColorParameter filter6Tint = new ColorParameter(Color.white, true, false, false);

        public bool IsEnabled()
        {
            return active && BurtBloomSettings.IsQualityEnabled(quality.value) && intensity.value > IntensityEpsilon;
        }
    }
}
