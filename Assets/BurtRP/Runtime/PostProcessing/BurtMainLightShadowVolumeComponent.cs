using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("BurtRP/Rendering/Main Light Shadows")]
    public sealed class BurtMainLightShadowVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Main Light Shadows")]
        [InfoBox("Global Volume controls BurtRP shadow quality. Light shadow type, strength, near plane, and custom resolution still come from the main Directional Light.")]
        public BoolParameter enabled = new BoolParameter(true);
        public ClampedIntParameter resolution = new ClampedIntParameter(BurtShadowData.DefaultMainLightShadowResolution, 16, 8192);
        public ClampedFloatParameter distance = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowDistance, 0f, 1000f);

        [Title("Cascades")]
        [InfoBox("Cascade count uses 1, 2, or 4 cascades. Split values are normalized camera shadow-distance ratios.")]
        public ClampedIntParameter cascadeCount = new ClampedIntParameter(BurtShadowData.DefaultMainLightShadowCascadeCount, 1, BurtShadowData.MaxMainLightShadowCascadeCount);
        public ClampedFloatParameter cascadeSplit1 = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowCascadeSplit1, 0.01f, 0.99f);
        public ClampedFloatParameter cascadeSplit2 = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowCascadeSplit2, 0.02f, 0.995f);
        public ClampedFloatParameter cascadeSplit3 = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowCascadeSplit3, 0.03f, 0.999f);
        public ClampedFloatParameter cascadeBlendDistance = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowCascadeBlendDistance, 0f, 0.5f);
        public ClampedFloatParameter shadowFadeDistance = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowFadeDistance, 0f, 100f);

        [Title("Bias")]
        public ClampedFloatParameter depthBias = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowDepthBias, 0f, 10f);
        public ClampedFloatParameter normalBias = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowNormalBias, 0f, 10f);
        public ClampedFloatParameter sampleBias = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowSampleBias, 0f, 0.1f);

        [Title("PCSS")]
        [InfoBox("PCSS replaces BurtRP hard shadow sampling with contact-hardening filtering. Set the main Directional Light to Hard Shadows, then enable PCSS here.")]
        public BoolParameter pcssEnabled = new BoolParameter(false);
        public ClampedFloatParameter pcssLightSize = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowPCSSLightSize, 0f, 64f);
        public ClampedFloatParameter pcssBlockerSearchRadius = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowPCSSBlockerSearchRadius, 0f, 64f);
        public ClampedFloatParameter pcssMaxFilterRadius = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowPCSSMaxFilterRadius, 0f, 128f);

        public bool HasAnyOverride()
        {
            return active && (enabled.overrideState || resolution.overrideState || distance.overrideState || cascadeCount.overrideState || cascadeSplit1.overrideState || cascadeSplit2.overrideState || cascadeSplit3.overrideState || cascadeBlendDistance.overrideState || shadowFadeDistance.overrideState || depthBias.overrideState || normalBias.overrideState || sampleBias.overrideState || pcssEnabled.overrideState || pcssLightSize.overrideState || pcssBlockerSearchRadius.overrideState || pcssMaxFilterRadius.overrideState);
        }
    }
}
