using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Rendering/Volumetric Fog")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtVolumetricFogVolumeComponent")]
    public sealed class VolumetricFogVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Volumetric Fog")]
        [InfoBox("Lightweight screen-space raymarch volumetric fog inspired by XRender volumetric fog parameters. Disabled by default.")]
        public BoolParameter enabled = new BoolParameter(false);

        [Title("Marching")]
        public ClampedFloatParameter visibleDistance = new ClampedFloatParameter(300f, 1f, 5000f);
        public ClampedFloatParameter startDistance = new ClampedFloatParameter(0f, 0f, 100000f);
        public ClampedIntParameter stepCount = new ClampedIntParameter(24, 4, 96);
        public BoolParameter jitter = new BoolParameter(true);

        [Title("Density")]
        public FloatParameter height = new FloatParameter(0f);
        public ClampedFloatParameter density = new ClampedFloatParameter(0.01f, 0f, 1f);
        public ClampedFloatParameter heightFalloff = new ClampedFloatParameter(0.15f, 0.001f, 4f);
        public ClampedFloatParameter extinctionScale = new ClampedFloatParameter(1f, 0.01f, 10f);
        public ClampedFloatParameter maxOpacity = new ClampedFloatParameter(0.75f, 0f, 1f);

        [Title("Scattering")]
        public ColorParameter albedo = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.2f, -0.9f, 0.9f);
        public ClampedFloatParameter directIntensity = new ClampedFloatParameter(1f, 0f, 8f);
        public ClampedFloatParameter ambientIntensity = new ClampedFloatParameter(0.35f, 0f, 8f);

        public bool IsEnabled()
        {
            return active &&
                enabled.value &&
                visibleDistance.value > 0.001f &&
                density.value > 0.000001f &&
                extinctionScale.value > 0.000001f &&
                maxOpacity.value > 0.000001f;
        }
    }
}
