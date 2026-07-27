using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmosphereSunSource")]
    public enum AtmosphereSunSource
    {
        MainLight = 0,
        CustomDirection = 1
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmosphereAerialPerspectivePlacement")]

    public enum AtmosphereAerialPerspectivePlacement
    {
        AfterOpaqueBeforeSky = 0,
        AfterSkyBeforeSSR = 1,
        BeforeTransparent = 2
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmosphereFogInteraction")]

    public enum AtmosphereFogInteraction
    {
        Additive = 0,
        AerialDominatesDistance = 1,
        FogOnly = 2,
        AerialOnly = 3
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmospherePlanetTransformMode")]
    public enum AtmospherePlanetTransformMode
    {
        PlanetTopAtAbsoluteWorldOrigin = 0,
        PlanetTopAtAnchorWorld = 1,
        PlanetCenterAtAnchorWorld = 2,
        ExplicitPlanetCenterWorld = 3
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmosphereSunSourceParameter")]
    public sealed class AtmosphereSunSourceParameter : VolumeParameter<AtmosphereSunSource>
    {
        public AtmosphereSunSourceParameter(AtmosphereSunSource value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmosphereAerialPerspectivePlacementParameter")]
    public sealed class AtmosphereAerialPerspectivePlacementParameter : VolumeParameter<AtmosphereAerialPerspectivePlacement>
    {
        public AtmosphereAerialPerspectivePlacementParameter(AtmosphereAerialPerspectivePlacement value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmosphereFogInteractionParameter")]
    public sealed class AtmosphereFogInteractionParameter : VolumeParameter<AtmosphereFogInteraction>
    {
        public AtmosphereFogInteractionParameter(AtmosphereFogInteraction value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmospherePlanetTransformModeParameter")]
    public sealed class AtmospherePlanetTransformModeParameter : VolumeParameter<AtmospherePlanetTransformMode>
    {
        public AtmospherePlanetTransformModeParameter(AtmospherePlanetTransformMode value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("Rendering/Atmosphere Scattering")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAtmosphereVolumeComponent")]
    public sealed class AtmosphereVolumeComponent : VolumeComponent
    {
        [Title("BurtRP Atmosphere")]
        [InfoBox("Single-scattering sky background. Disabled by default; when enabled it replaces the camera skybox pass for Skybox clear mode.")]
        public BoolParameter enabled = new BoolParameter(false);

        [Title("Scattering")]
        public ClampedFloatParameter rayleighIntensity = new ClampedFloatParameter(1.0f, 0f, 8f);
        public ClampedFloatParameter mieIntensity = new ClampedFloatParameter(0.12f, 0f, 8f);
        public ClampedFloatParameter mieAnisotropy = new ClampedFloatParameter(0.76f, -0.95f, 0.95f);
        [InfoBox("Physical coefficients in km^-1. Defaults are XRender's Earth profile; the existing intensity controls remain multiplicative for backwards compatibility.")]
        public ColorParameter rayleighScatteringCoefficient = new ColorParameter(new Color(0.005802f, 0.013558f, 0.033100f, 1f), true, false, true);
        public ColorParameter mieScatteringCoefficient = new ColorParameter(new Color(0.003996f, 0.003996f, 0.003996f, 1f), true, false, true);
        public ColorParameter mieAbsorptionCoefficient = new ColorParameter(new Color(0.000444f, 0.000444f, 0.000444f, 1f), true, false, true);
        [InfoBox("Triangular ozone absorption layer. Defaults match XRender's Earth-like profile in kilometer space.")]
        public ClampedFloatParameter ozoneAbsorptionIntensity = new ClampedFloatParameter(1.0f, 0f, 8f);
        public ColorParameter ozoneAbsorptionCoefficient = new ColorParameter(new Color(0.000650f, 0.001881f, 0.000085f, 1f), true, false, true);
        public ClampedFloatParameter ozoneLayerCenter = new ClampedFloatParameter(25f, 0f, 100f);
        public ClampedFloatParameter ozoneLayerThickness = new ClampedFloatParameter(15f, 0.1f, 100f);
        [InfoBox("Scales the geometric-series multiple-scattering solution; 1 matches the physical LUT default.")]
        public ClampedFloatParameter multipleScatteringIntensity = new ClampedFloatParameter(1.0f, 0f, 4f);
        [InfoBox("Matches XRender Trace Sample Count Scale. Higher values rebuild more accurate LUTs at a proportionally higher cost.")]
        public ClampedFloatParameter traceSampleCountScale = new ClampedFloatParameter(1.0f, 0.25f, 8f);
        public ClampedFloatParameter sunIntensity = new ClampedFloatParameter(0.6f, 0f, 64f);
        [Title("Sun Disk (XRender)")]
        [InfoBox("Full solar angular diameter in degrees. BRP converts it to a half-angle in radians and derives disk luminance from directional-light illuminance and the corresponding solid angle, as XRender does. Earth's apparent solar diameter is approximately 0.5 degrees.")]
        public ClampedFloatParameter sunDiskSize = new ClampedFloatParameter(0.5f, 0.05f, 20f);
        [InfoBox("Additional BRP art-direction multiplier applied after XRender's illuminance-to-disk-luminance conversion.")]
        public ClampedFloatParameter sunDiskIntensity = new ClampedFloatParameter(1.2f, 0f, 16f);
        [Title("Sun Halo (BRP)")]
        public ClampedFloatParameter sunHaloSize = new ClampedFloatParameter(1.0f, 0.05f, 8f);
        public ClampedFloatParameter sunHaloIntensity = new ClampedFloatParameter(1.0f, 0f, 16f);
        public AtmosphereSunSourceParameter sunSource = new AtmosphereSunSourceParameter(AtmosphereSunSource.MainLight);
        public Vector3Parameter customSunDirection = new Vector3Parameter(new Vector3(0.3f, 0.8f, 0.4f));

        [Title("Main Light Coupling (XRender)")]
        [InfoBox("Applies XRender's 15-sample ground-level optical-depth transmittance to scene main-light shading. A value of one is XRender parity; zero keeps the original unattenuated BRP main light. Sky and atmosphere integration always use the unattenuated outer-space light color.")]
        public ClampedFloatParameter mainLightTransmittanceStrength = new ClampedFloatParameter(1.0f, 0f, 1f);
        [InfoBox("XRender-style environment occlusion shared by surface main-light shading, physical sky, aerial perspective, sun disk and atmosphere-driven fog. It is applied at consumption time and never baked into atmosphere LUTs.")]
        public ClampedFloatParameter mainLightOcclusion = new ClampedFloatParameter(1.0f, 0f, 1f);

        [Title("Shape")]
        public ClampedFloatParameter planetRadius = new ClampedFloatParameter(6371f, 100f, 100000f);
        public ClampedFloatParameter atmosphereHeight = new ClampedFloatParameter(80f, 1f, 1000f);
        public ClampedFloatParameter rayleighScaleHeight = new ClampedFloatParameter(8f, 0.1f, 128f);
        public ClampedFloatParameter mieScaleHeight = new ClampedFloatParameter(1.2f, 0.1f, 64f);
        [InfoBox("Matches XRender's planet transform options. Volume components have no Transform, so Anchor World is an explicit world-space replacement for the XRender component transform.")]
        public AtmospherePlanetTransformModeParameter planetTransformMode = new AtmospherePlanetTransformModeParameter(AtmospherePlanetTransformMode.PlanetTopAtAbsoluteWorldOrigin);
        [InfoBox("Used by the two Anchor World modes. In Planet Top mode this position is on the ground; in Planet Center mode it is the center of the planet.")]
        public Vector3Parameter planetAnchorWorld = new Vector3Parameter(Vector3.zero);
        [InfoBox("Used only by Explicit Planet Center World. The default keeps world origin at ground level for the default 6371km planet.")]
        public Vector3Parameter planetCenterWorld = new Vector3Parameter(new Vector3(0f, -6371000f, 0f));
        [InfoBox("Converts BRP world units to physical kilometers. Use 0.001 for meter-based worlds.")]
        public ClampedFloatParameter worldToKilometers = new ClampedFloatParameter(0.001f, 0.000001f, 10f);
        [InfoBox("Physical planet ground albedo for the atmosphere multiple-scattering LUT. Matches XRender's Earth default (170/256); it is intentionally independent of the artistic Ground Color below.")]
        public ColorParameter groundAlbedo = new ColorParameter(new Color(0.6666667f, 0.6666667f, 0.6666667f, 1f), true, false, true);

        [Title("Art Direction")]
        public ColorParameter groundColor = new ColorParameter(new Color(0.18f, 0.20f, 0.18f, 1f), true, false, true);
        public ColorParameter skyTint = new ColorParameter(new Color(0.65f, 0.78f, 1f, 1f), true, false, true);
        [InfoBox("Matches XRender Sky Luminance Factor. This RGB multiplier grades physical sky radiance at sampling time, so changing it does not rebuild atmosphere LUTs and does not tint the direct sun disk.")]
        public ColorParameter skyLuminanceFactor = new ColorParameter(Color.white, true, false, true);
        public ColorParameter horizonColor = new ColorParameter(new Color(0.48f, 0.66f, 0.92f, 1f), true, false, true);
        public ColorParameter horizonSunsetColor = new ColorParameter(new Color(0.95f, 0.82f, 0.58f, 1f), true, false, true);
        public ClampedFloatParameter horizonIntensity = new ClampedFloatParameter(1.0f, 0f, 4f);
        public ClampedFloatParameter horizonFalloff = new ClampedFloatParameter(0.65f, 0.1f, 4f);
        public ClampedFloatParameter horizonSunsetInfluence = new ClampedFloatParameter(0.35f, 0f, 1f);
        public ClampedFloatParameter groundContribution = new ClampedFloatParameter(0.22f, 0f, 2f);
        public ClampedFloatParameter groundBlendStart = new ClampedFloatParameter(-0.02f, -1f, 1f);
        public ClampedFloatParameter groundBlendEnd = new ClampedFloatParameter(-0.20f, -1f, 1f);
        public ClampedFloatParameter exposureCompensation = new ClampedFloatParameter(0.0f, -8f, 8f);
        [InfoBox("Soft-clamps the analytic sky and authored halo controls before post tonemapping. The physical disk follows XRender's separate 64000 luminance safety clamp.")]
        public ClampedFloatParameter tonemapSafeSunIntensity = new ClampedFloatParameter(4.0f, 0.1f, 32f);

        [Title("Stylized Sky (XRender)")]
        [InfoBox("Blends the physical atmosphere background toward XRender's authored day/dawn/night sky colors. The direct sun disk remains a separate physical term. A value of zero preserves the existing BRP sky exactly.")]
        public ClampedFloatParameter stylizedSkyBlend = new ClampedFloatParameter(0f, 0f, 1f);
        public ColorParameter stylizedBaseSkyColorDay = new ColorParameter(new Color(0.0838f, 0.1645f, 0.8716f, 1f), true, false, true);
        public ColorParameter stylizedBaseSkyColorDawnDusk = new ColorParameter(new Color(0.1651f, 0.1946f, 0.3662f, 1f), true, false, true);
        public ColorParameter stylizedBaseSkyColorNight = new ColorParameter(new Color(0.0166f, 0.0265f, 0.1245f, 1f), true, false, true);
        public ColorParameter stylizedHorizonSkyColorDay = new ColorParameter(new Color(0.55f, 0.66f, 1.92f, 1f), true, false, true);
        public ColorParameter stylizedHorizonSkyColorDawnDusk = new ColorParameter(new Color(0.4735f, 0.1844f, 0.1274f, 1f), true, false, true);
        public ColorParameter stylizedHorizonSkyColorNight = new ColorParameter(new Color(0.3132f, 0.2110f, 0.1672f, 1f), true, false, true);
        public ClampedFloatParameter stylizedHorizonBrightness = new ClampedFloatParameter(1.5f, 0f, 100f);
        public ClampedFloatParameter stylizedHorizonFalloff = new ClampedFloatParameter(10f, 0.1f, 100f);
        public ColorParameter stylizedSunDiskColorScale = new ColorParameter(Color.white, true, false, true);
        public ColorParameter stylizedSunGlowColor = new ColorParameter(Color.white, true, false, true);
        [InfoBox("XRender stores this interval as -60..7 and divides by 100 before shading; BRP exposes the normalized sun-elevation interval directly.")]
        public ClampedFloatParameter stylizedSunRiseBlendMin = new ClampedFloatParameter(-0.6f, -1f, 1f);
        public ClampedFloatParameter stylizedSunRiseBlendMax = new ClampedFloatParameter(0.07f, -1f, 1f);
        public ClampedFloatParameter stylizedSunGlowScale = new ClampedFloatParameter(3.5f, 0f, 5f);

        [Title("Moon (XRender)")]
        [InfoBox("XRender-compatible textured moon disk. Rotation is applied to the default forward/up/right basis; the visible moon direction is the negated rotated forward axis, matching XRender's _MoonRotation upload.")]
        public BoolParameter moonEnabled = new BoolParameter(false);
        public TextureParameter moonSurfaceTexture = new TextureParameter(null);
        public Vector3Parameter moonRotationEuler = new Vector3Parameter(Vector3.zero);
        [InfoBox("Moon illuminance converted to disk luminance from the authored angular diameter using XRender's solid-angle formula.")]
        public ClampedFloatParameter moonIntensity = new ClampedFloatParameter(0f, 0f, 130000f);
        public ClampedFloatParameter moonAngularDiameter = new ClampedFloatParameter(6f, 0.05f, 90f);
        public ColorParameter moonSurfaceTint = new ColorParameter(new Color(0.06f, 0.06f, 0.14f, 1f), true, false, true);
        public ClampedFloatParameter moonPhase = new ClampedFloatParameter(0.6f, 0f, 1f);
        public ClampedFloatParameter moonPhaseRotation = new ClampedFloatParameter(300f, 0f, 360f);
        [InfoBox("Normalized earthshine added to the analytic moon phase. XRender authors this value magnified by 1000; BRP exposes the shader-space value directly, so 0.01 corresponds to XRender's authored value 10.")]
        public ClampedFloatParameter moonEarthshine = new ClampedFloatParameter(0.01f, 0f, 0.5f);
        public ClampedFloatParameter moonFlareSize = new ClampedFloatParameter(0f, 0f, 5f);
        public ClampedFloatParameter moonFlareFalloff = new ClampedFloatParameter(50f, 1f, 100f);
        public ColorParameter moonFlareTint = new ColorParameter(Color.white, true, false, true);
        [InfoBox("Moon elevation visibility interval. Defaults match XRender's authored 10..75 interval after its division by 100.")]
        public ClampedFloatParameter moonRiseBlendMin = new ClampedFloatParameter(0.1f, -1f, 1f);
        public ClampedFloatParameter moonRiseBlendMax = new ClampedFloatParameter(0.75f, -1f, 1f);

        [Title("Aerial Perspective")]
        public BoolParameter aerialPerspective = new BoolParameter(false);
        [InfoBox("Matches XRender Atmosphere Fog Density Scale. It scales physical extinction in the aerial LUT, so it changes both scene transmittance and in-scattering. Intensity below remains the independent luminance scale for backwards compatibility.")]
        public ClampedFloatParameter aerialPerspectiveDensityScale = new ClampedFloatParameter(1.0f, 0f, 21f);
        [InfoBox("Matches XRender Atmosphere Fog Luminance Scale. It only scales in-scattering after LUT sampling; it does not alter scene transmittance.")]
        public ClampedFloatParameter aerialPerspectiveLuminanceScale = new ClampedFloatParameter(1.0f, 0f, 20f);
        [InfoBox("Matches XRender Atmosphere Fog Sampling Distance Scale. It remaps camera distance before the fog-LUT lookup without rebuilding the LUT.")]
        public ClampedFloatParameter aerialPerspectiveSamplingDistanceScale = new ClampedFloatParameter(1.0f, 0f, 20f);
        [InfoBox("Legacy BRP analytic-fallback intensity. It does not scale or disable the physical XRender Fog LUT path.")]
        public ClampedFloatParameter aerialPerspectiveIntensity = new ClampedFloatParameter(0.35f, 0f, 4f);
        [InfoBox("Legacy BRP analytic-fallback distance. The physical Fog LUT always uses XRender's fixed 96 km coverage.")]
        public ClampedFloatParameter aerialPerspectiveDistance = new ClampedFloatParameter(250f, 1f, 100000f);
        [InfoBox("Optional BRP post-sampling height fade. Zero is the neutral XRender-compatible value.")]
        public ClampedFloatParameter aerialPerspectiveHeightFalloff = new ClampedFloatParameter(0.0f, 0f, 2f);
        [InfoBox("Optional BRP sampling-side tint. White is neutral and matches XRender.")]
        public ColorParameter aerialPerspectiveTint = new ColorParameter(Color.white, true, false, true);
        [InfoBox("Atmosphere Fog start distance in BRP world units. The 100-unit default equals XRender's 0.1 km default in a meter-based world.")]
        public ClampedFloatParameter aerialPerspectiveNearFadeStart = new ClampedFloatParameter(100f, 0f, 100000f);
        [InfoBox("Optional BRP smooth-start endpoint. The near-equal default preserves XRender's hard start; the LUT supplies its own first-slice fade.")]
        public ClampedFloatParameter aerialPerspectiveNearFadeEnd = new ClampedFloatParameter(100.001f, 0f, 100000f);
        [InfoBox("Optional BRP opacity cap. One is neutral and matches XRender.")]
        public ClampedFloatParameter aerialPerspectiveMaxOpacity = new ClampedFloatParameter(1f, 0f, 1f);
        public AtmosphereAerialPerspectivePlacementParameter aerialPerspectivePlacement = new AtmosphereAerialPerspectivePlacementParameter(AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky);
        public AtmosphereFogInteractionParameter aerialFogInteraction = new AtmosphereFogInteractionParameter(AtmosphereFogInteraction.Additive);

        public bool IsEnabled()
        {
            return active && enabled.value && sunIntensity.value > 0.0001f && (rayleighIntensity.value > 0.0001f || mieIntensity.value > 0.0001f);
        }

        public bool IsAerialPerspectiveEnabled()
        {
            return IsEnabled()
                && aerialPerspective.value
                && aerialPerspectiveDensityScale.value > 0.0001f
                && aerialPerspectiveLuminanceScale.value > 0.0001f
                && aerialPerspectiveSamplingDistanceScale.value > 0.0001f;
        }
    }
}
