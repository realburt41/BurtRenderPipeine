using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Rendering/Screen Space Ambient Occlusion")]
    public sealed class BurtScreenSpaceAmbientOcclusionVolumeComponent : VolumeComponent
    {
        private const float Epsilon = 0.0001f;

        [Title("BurtRP Screen Space Ambient Occlusion")]
        [InfoBox("Deferred SSAO reads CameraDepth + GBuffer normal and outputs a screen-space AO texture. Default is off.")]
        public BoolParameter enabled = new BoolParameter(false);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(2f, 0f, 4f);
        public ClampedFloatParameter radius = new ClampedFloatParameter(1f, 0.01f, 5f);
        public ClampedIntParameter sampleCount = new ClampedIntParameter(16, 1, 32);
        public BoolParameter horizonSearch = new BoolParameter(true);
        public ClampedIntParameter directionCount = new ClampedIntParameter(2, 1, 8);
        public ClampedFloatParameter bias = new ClampedFloatParameter(0.02f, 0f, 0.2f);
        public ClampedFloatParameter power = new ClampedFloatParameter(1f, 0.25f, 4f);
        public BoolParameter halfResolution = new BoolParameter(true);
        public BoolParameter blur = new BoolParameter(true);
        public BoolParameter spatialDenoise = new BoolParameter(true);
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
