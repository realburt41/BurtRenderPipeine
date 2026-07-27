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
        [InfoBox("XRender-style 3D integrated volumetric fog shared by opaque and transparent rendering, including conservative-depth slice culling, translucency-volume GI and direction-dependent sky SH fallback. Unsupported platforms and extended debug views fall back to the screen-space raymarch. Disabled by default.")]
        public BoolParameter enabled = new BoolParameter(false);

        [Title("Integration")]
        public ClampedFloatParameter visibleDistance = new ClampedFloatParameter(300f, 1f, 5000f);
        public ClampedFloatParameter startDistance = new ClampedFloatParameter(0f, 0f, 100000f);
        [Tooltip("Legacy screen-space fallback and density-debug step count. The shared 3D integration uses 256 logarithmic depth slices.")]
        public ClampedIntParameter stepCount = new ClampedIntParameter(24, 4, 96);
        public BoolParameter jitter = new BoolParameter(true);

        [Title("Density")]
        public FloatParameter height = new FloatParameter(0f);
        public ClampedFloatParameter density = new ClampedFloatParameter(0.01f, 0f, 1f);
        public ClampedFloatParameter heightFalloff = new ClampedFloatParameter(0.15f, 0.001f, 4f);
        [Tooltip("XRender second-layer height relative to the first layer height.")]
        public FloatParameter secondLayerHeightOffset = new FloatParameter(0f);
        public ClampedFloatParameter secondLayerDensity = new ClampedFloatParameter(0f, 0f, 1f);
        [Tooltip("0 creates a uniform second layer, matching XRender.")]
        public ClampedFloatParameter secondLayerHeightFalloff = new ClampedFloatParameter(0f, 0f, 4f);
        public ClampedFloatParameter extinctionScale = new ClampedFloatParameter(1f, 0.01f, 10f);
        public ClampedFloatParameter maxOpacity = new ClampedFloatParameter(0.75f, 0f, 1f);

        [Title("Fog Map")]
        [InfoBox("XRender encoding: R = relative fog height, G = falloff distance over 0-300 m, B = density multiplied by 10, A unused. World XZ is remapped and saturated to the 0-1 map domain.")]
        public BoolParameter useFogMap = new BoolParameter(false);
        [Tooltip("Import this texture as linear data (sRGB disabled) so encoded height, falloff and density remain numerically correct.")]
        public TextureParameter fogMap = new TextureParameter(null);
        public Vector2Parameter fogMapCenterXZ = new Vector2Parameter(Vector2.zero);
        public Vector2Parameter fogMapCoverageXZ = new Vector2Parameter(new Vector2(4096f, 4096f));
        public FloatParameter fogMapMinAltitude = new FloatParameter(-200f);
        public FloatParameter fogMapMaxAltitude = new FloatParameter(500f);

        [Title("Scattering")]
        public ColorParameter albedo = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter anisotropy = new ClampedFloatParameter(0.2f, -0.9f, 0.9f);
        public ClampedFloatParameter directIntensity = new ClampedFloatParameter(1f, 0f, 8f);
        [Tooltip("Scales XRender-style ambient scattering. Deferred TGI uses its current filtered SH2 volume; other paths use BurtRP's packed sky SH, with flat white fallback before SH globals are available.")]
        public ClampedFloatParameter ambientIntensity = new ClampedFloatParameter(0.35f, 0f, 8f);

        [Title("Atmosphere Horizontal Scattering")]
        [InfoBox("Blends from the legacy lighting model to XRender-style horizontal scattering between 130 m and 150 m. Rayleigh and Mie remain phase-dependent; multiple scattering is phase-independent.")]
        public BoolParameter useAtmosphereHorizontalScattering = new BoolParameter(true);
        public ColorParameter atmosphereRayleighTint = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter atmosphereRayleighScale = new ClampedFloatParameter(1f, 0f, 10f);
        public ColorParameter atmosphereMieTint = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter atmosphereMieScale = new ClampedFloatParameter(1f, 0f, 10f);
        public ColorParameter atmosphereMultipleScatteringTint = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter atmosphereMultipleScatteringScale = new ClampedFloatParameter(1f, 0f, 10f);

        public bool IsEnabled()
        {
            return active &&
                enabled.value &&
                visibleDistance.value > 0.001f &&
                (density.value > 0.000001f || secondLayerDensity.value > 0.000001f ||
                    (useFogMap.value && fogMap.value is Texture2D)) &&
                extinctionScale.value > 0.000001f &&
                maxOpacity.value > 0.000001f;
        }
    }
}
