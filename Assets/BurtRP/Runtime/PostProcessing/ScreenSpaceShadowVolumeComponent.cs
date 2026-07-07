using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Rendering/Screen Space Shadow")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceShadowVolumeComponent")]
    public sealed class ScreenSpaceShadowVolumeComponent : VolumeComponent
    {
        private const float Epsilon = 0.0001f;

        [Title("BurtRP Screen Space Shadow")]
        [InfoBox("Deferred screen-space main-light shadow. This first Burt port traces CameraDepth along the projected main-light direction; foliage can blend it by material screen-space shadow intensity.")]
        public BoolParameter enabled = new BoolParameter(false);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedIntParameter sampleCount = new ClampedIntParameter(32, 1, 64);
        public ClampedFloatParameter maxDistance = new ClampedFloatParameter(5f, 0.01f, 50f);

        [Title("Environment")]
        public ClampedFloatParameter depthOffset = new ClampedFloatParameter(0.5f, 0f, 2f);

        [Title("Cast Contrast")]
        public ClampedFloatParameter grassContrast = new ClampedFloatParameter(0.8f, 0f, 2f);
        public ClampedFloatParameter detailContrast = new ClampedFloatParameter(0.8f, 0f, 2f);
        public ClampedFloatParameter foliageContrast = new ClampedFloatParameter(0.8f, 0f, 2f);
        public ClampedFloatParameter characterContrast = new ClampedFloatParameter(0.8f, 0f, 2f);

        [Title("Advanced")]
        public ClampedFloatParameter thickness = new ClampedFloatParameter(0.5f, 0f, 10f);
        public ClampedFloatParameter bilinearThreshold = new ClampedFloatParameter(2f, 0f, 10f);
        public BoolParameter bilinearSamplingOffset = new BoolParameter(true);
        public ClampedFloatParameter fadeDistance = new ClampedFloatParameter(80f, 0.01f, 800f);
        public ClampedFloatParameter fadeRadius = new ClampedFloatParameter(50f, 0.01f, 200f);

        [Title("Quality")]
        public BoolParameter halfResolution = new BoolParameter(false);
        public BoolParameter quarterResolution = new BoolParameter(false);

        public bool IsEnabled()
        {
            return active && enabled.value && intensity.value > Epsilon && sampleCount.value > 0 && maxDistance.value > Epsilon;
        }
    }
}
