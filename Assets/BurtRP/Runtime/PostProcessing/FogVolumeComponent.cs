using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [Serializable]
    [VolumeComponentMenu("Rendering/Fog")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtFogVolumeComponent")]
    public sealed class FogVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Fog")]
        [InfoBox("Lightweight screen-space exponential height fog inspired by XRender GlobalFog. Disabled by default.")]
        public BoolParameter enabled = new BoolParameter(false);

        [Title("Shape")]
        public FloatParameter height = new FloatParameter(0f);
        public ClampedFloatParameter density = new ClampedFloatParameter(0.02f, 0f, 0.5f);
        public ClampedFloatParameter heightFalloff = new ClampedFloatParameter(0.2f, 0.001f, 4f);
        public ClampedFloatParameter startDistance = new ClampedFloatParameter(0f, 0f, 100000f);
        public ClampedFloatParameter cutoffDistance = new ClampedFloatParameter(0f, 0f, 200000f);
        public ClampedFloatParameter maxOpacity = new ClampedFloatParameter(0.85f, 0f, 1f);

        [Title("Scattering")]
        public ColorParameter albedo = new ColorParameter(new Color(0.72f, 0.80f, 0.90f, 1f), true, false, true);
        public ClampedFloatParameter directionalIntensity = new ClampedFloatParameter(0.25f, 0f, 4f);
        public ClampedFloatParameter ambientIntensity = new ClampedFloatParameter(0.75f, 0f, 4f);
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.2f, -0.9f, 0.9f);

        public bool IsEnabled()
        {
            return active && enabled.value && density.value > 0.000001f && maxOpacity.value > 0.000001f;
        }
    }
}
