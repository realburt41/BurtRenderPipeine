using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Rendering/Light Shaft")]
    public sealed class LightShaftVolumeComponent : VolumeComponent
    {
        [Title("Desktop Light Shaft Occlusion")]
        [InfoBox("XRender-compatible desktop screen-space occlusion applied to opaque atmosphere, height-fog, and volumetric-fog scattering.")]
        public BoolParameter enabled = new BoolParameter(true);

        public ClampedFloatParameter occlusionMaskDarkness =
            new ClampedFloatParameter(0.05f, 0f, 1f);

        public ClampedFloatParameter occlusionDepthRange =
            new ClampedFloatParameter(1000f, 1f, 5000f);

        [Title("Desktop Light Shaft Bloom")]
        [InfoBox("Half-resolution HDR setup, three radial blur passes, and additive scene-color composition matching XRender's desktop post-process path.")]
        public BoolParameter bloomEnabled = new BoolParameter(false);
        public ClampedFloatParameter bloomScale = new ClampedFloatParameter(0.2f, 0f, 1f);
        public ClampedFloatParameter bloomThreshold = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter bloomMaxBrightness = new ClampedFloatParameter(100f, 0f, 100f);
        public ColorParameter bloomTint = new ColorParameter(Color.white, true, false, true);

        public bool IsOcclusionEnabled()
        {
            return active &&
                HasAnyOcclusionOverride() &&
                enabled.value &&
                occlusionDepthRange.value > 0.0001f;
        }

        public bool IsBloomEnabled()
        {
            return active &&
                HasAnyBloomOverride() &&
                bloomEnabled.value &&
                bloomScale.value > 0.000001f &&
                bloomMaxBrightness.value > 0.000001f &&
                occlusionDepthRange.value > 0.0001f;
        }

        private bool HasAnyOcclusionOverride()
        {
            return enabled.overrideState ||
                occlusionMaskDarkness.overrideState ||
                occlusionDepthRange.overrideState;
        }

        private bool HasAnyBloomOverride()
        {
            return bloomEnabled.overrideState ||
                bloomScale.overrideState ||
                bloomThreshold.overrideState ||
                bloomMaxBrightness.overrideState ||
                bloomTint.overrideState ||
                occlusionDepthRange.overrideState;
        }
    }
}
