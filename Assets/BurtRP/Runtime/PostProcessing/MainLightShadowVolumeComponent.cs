using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtMainLightShadowFilterMode
    {
        UseLightSettings = 0,
        Hard = 1,
        PCF3 = 3,
        PCF5 = 5,
        PCF7 = 7
    }

    [Serializable]
    public sealed class BurtMainLightShadowFilterModeParameter : VolumeParameter<BurtMainLightShadowFilterMode>
    {
        public BurtMainLightShadowFilterModeParameter(BurtMainLightShadowFilterMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("Rendering/Main Light Shadows")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtMainLightShadowVolumeComponent")]
    public sealed class MainLightShadowVolumeComponent : VolumeComponent
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

        [Title("Filtering")]
        [InfoBox("Use Light Settings follows the Directional Light: Hard uses one comparison sample and Soft uses XRender's optimized PCF 5x5 filter. Explicit PCF modes override the Light shadow type.")]
        public BurtMainLightShadowFilterModeParameter filterMode = new BurtMainLightShadowFilterModeParameter(BurtMainLightShadowFilterMode.UseLightSettings);

        [Title("Legacy PCSS Compatibility")]
        [InfoBox("The active XRender main-light path is fixed-kernel PCF, not variable-radius PCSS. When Filter Mode is Use Light Settings, legacy PCSS Enabled maps to PCF 5x5; explicit Filter Mode takes priority.")]
        public BoolParameter pcssEnabled = new BoolParameter(false);
        public ClampedFloatParameter pcssLightSize = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowPCSSLightSize, 0f, 64f);
        public ClampedFloatParameter pcssBlockerSearchRadius = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowPCSSBlockerSearchRadius, 0f, 64f);
        public ClampedFloatParameter pcssMaxFilterRadius = new ClampedFloatParameter(BurtShadowData.DefaultMainLightShadowPCSSMaxFilterRadius, 0f, 128f);

        public bool HasAnyOverride()
        {
            return active && (enabled.overrideState || resolution.overrideState || distance.overrideState || cascadeCount.overrideState || cascadeSplit1.overrideState || cascadeSplit2.overrideState || cascadeSplit3.overrideState || cascadeBlendDistance.overrideState || shadowFadeDistance.overrideState || depthBias.overrideState || normalBias.overrideState || sampleBias.overrideState || filterMode.overrideState || pcssEnabled.overrideState || pcssLightSize.overrideState || pcssBlockerSearchRadius.overrideState || pcssMaxFilterRadius.overrideState);
        }
    }
}
