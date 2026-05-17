using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtAtmosphereSunSource
    {
        MainLight = 0,
        CustomDirection = 1
    }

    [Serializable]
    public sealed class BurtAtmosphereSunSourceParameter : VolumeParameter<BurtAtmosphereSunSource>
    {
        public BurtAtmosphereSunSourceParameter(BurtAtmosphereSunSource value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("BurtRP/Rendering/Atmosphere Scattering")]
    public sealed class BurtAtmosphereVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Atmosphere")]
        [InfoBox("Single-scattering sky background. Disabled by default; when enabled it replaces the camera skybox pass for Skybox clear mode.")]
        public BoolParameter enabled = new BoolParameter(false);

        [Title("Scattering")]
        public ClampedFloatParameter rayleighIntensity = new ClampedFloatParameter(1.0f, 0f, 8f);
        public ClampedFloatParameter mieIntensity = new ClampedFloatParameter(0.12f, 0f, 8f);
        public ClampedFloatParameter mieAnisotropy = new ClampedFloatParameter(0.76f, -0.95f, 0.95f);
        public ClampedFloatParameter sunIntensity = new ClampedFloatParameter(0.6f, 0f, 64f);
        public BurtAtmosphereSunSourceParameter sunSource = new BurtAtmosphereSunSourceParameter(BurtAtmosphereSunSource.MainLight);
        public Vector3Parameter customSunDirection = new Vector3Parameter(new Vector3(0.3f, 0.8f, 0.4f));

        [Title("Shape")]
        public ClampedFloatParameter planetRadius = new ClampedFloatParameter(6371f, 100f, 100000f);
        public ClampedFloatParameter atmosphereHeight = new ClampedFloatParameter(80f, 1f, 1000f);
        public ClampedFloatParameter rayleighScaleHeight = new ClampedFloatParameter(8f, 0.1f, 128f);
        public ClampedFloatParameter mieScaleHeight = new ClampedFloatParameter(1.2f, 0.1f, 64f);

        [Title("Art Direction")]
        public ColorParameter groundColor = new ColorParameter(new Color(0.18f, 0.20f, 0.18f, 1f), true, false, true);
        public ColorParameter skyTint = new ColorParameter(new Color(0.65f, 0.78f, 1f, 1f), true, false, true);
        public ClampedFloatParameter horizonIntensity = new ClampedFloatParameter(1.0f, 0f, 4f);
        public ClampedFloatParameter horizonFalloff = new ClampedFloatParameter(0.65f, 0.1f, 4f);
        public ClampedFloatParameter groundContribution = new ClampedFloatParameter(0.22f, 0f, 2f);
        public ClampedFloatParameter exposureCompensation = new ClampedFloatParameter(0.0f, -8f, 8f);
        [InfoBox("Soft-clamps the sky sun/halo contribution before post tonemapping. Keep this modest when auto exposure is enabled.")]
        public ClampedFloatParameter tonemapSafeSunIntensity = new ClampedFloatParameter(4.0f, 0.1f, 32f);

        [Title("Aerial Perspective")]
        public BoolParameter aerialPerspective = new BoolParameter(false);
        public ClampedFloatParameter aerialPerspectiveIntensity = new ClampedFloatParameter(0.35f, 0f, 4f);
        public ClampedFloatParameter aerialPerspectiveDistance = new ClampedFloatParameter(750f, 1f, 100000f);
        public ClampedFloatParameter aerialPerspectiveHeightFalloff = new ClampedFloatParameter(0.001f, 0f, 2f);
        public ColorParameter aerialPerspectiveTint = new ColorParameter(new Color(0.70f, 0.82f, 1.0f, 1f), true, false, true);
        public ClampedFloatParameter aerialPerspectiveNearFadeStart = new ClampedFloatParameter(0f, 0f, 100000f);
        public ClampedFloatParameter aerialPerspectiveNearFadeEnd = new ClampedFloatParameter(50f, 0f, 100000f);
        public ClampedFloatParameter aerialPerspectiveMaxOpacity = new ClampedFloatParameter(0.65f, 0f, 1f);

        public bool IsEnabled()
        {
            return active && enabled.value && sunIntensity.value > 0.0001f && (rayleighIntensity.value > 0.0001f || mieIntensity.value > 0.0001f);
        }

        public bool IsAerialPerspectiveEnabled()
        {
            return IsEnabled() && aerialPerspective.value && aerialPerspectiveIntensity.value > 0.0001f && aerialPerspectiveDistance.value > 0.0001f;
        }
    }
}
