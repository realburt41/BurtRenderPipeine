using System;
using Sirenix.OdinInspector;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Rendering/Light Shaft Occlusion")]
    public sealed class LightShaftVolumeComponent : VolumeComponent
    {
        [Title("Desktop Light Shaft Occlusion")]
        [InfoBox("XRender-compatible desktop screen-space occlusion applied to opaque atmosphere, height-fog, and volumetric-fog scattering.")]
        public BoolParameter enabled = new BoolParameter(true);

        public ClampedFloatParameter occlusionMaskDarkness =
            new ClampedFloatParameter(0.05f, 0f, 1f);

        public ClampedFloatParameter occlusionDepthRange =
            new ClampedFloatParameter(1000f, 1f, 5000f);

        public bool IsEnabled()
        {
            return active &&
                HasAnyOverride() &&
                enabled.value &&
                occlusionDepthRange.value > 0.0001f;
        }

        private bool HasAnyOverride()
        {
            return enabled.overrideState ||
                occlusionMaskDarkness.overrideState ||
                occlusionDepthRange.overrideState;
        }
    }
}
