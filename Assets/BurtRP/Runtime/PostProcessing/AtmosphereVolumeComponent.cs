using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

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
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "AtmosphereSkyMeshParameter")]
    public sealed class AtmosphereSkyMeshParameter : VolumeParameter<Mesh>
    {
        public AtmosphereSkyMeshParameter(Mesh value, bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "AtmosphereSkyMaterialParameter")]
    public sealed class AtmosphereSkyMaterialParameter : VolumeParameter<Material>
    {
        public AtmosphereSkyMaterialParameter(Material value, bool overrideState = false)
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
        [InfoBox("XRender-compatible physical atmosphere. It requires a visible main directional light. LUT, fog, aerial-perspective and reflection consumers remain independent of camera clear mode; only the visible sky replacement requires Skybox clear mode.")]
        public BoolParameter enabled = new BoolParameter(false);

        [Title("Scattering")]
        public ClampedFloatParameter rayleighIntensity = new ClampedFloatParameter(1.0f, 0f, 8f);
        public ClampedFloatParameter mieIntensity = new ClampedFloatParameter(0.12f, 0f, 8f);
        public ClampedFloatParameter mieAnisotropy = new ClampedFloatParameter(0.8f, 0f, 0.999f);
        [InfoBox("Physical coefficients in km^-1. Defaults are XRender's Earth profile; the existing intensity controls remain multiplicative for backwards compatibility.")]
        public ColorParameter rayleighScatteringCoefficient = new ColorParameter(new Color(0.005802f, 0.013558f, 0.033100f, 1f), true, false, true);
        public ColorParameter mieScatteringCoefficient = new ColorParameter(new Color(0.003996f, 0.003996f, 0.003996f, 1f), true, false, true);
        public ColorParameter mieAbsorptionCoefficient = new ColorParameter(new Color(0.000444f, 0.000444f, 0.000444f, 1f), true, false, true);
        [InfoBox("Triangular ozone absorption layer. Layer Thickness is XRender's one-sided width (half-width) around the center altitude, in kilometers.")]
        public ClampedFloatParameter ozoneAbsorptionIntensity = new ClampedFloatParameter(1.0f, 0f, 8f);
        public ColorParameter ozoneAbsorptionCoefficient = new ColorParameter(new Color(0.000650f, 0.001881f, 0.000085f, 1f), true, false, true);
        public ClampedFloatParameter ozoneLayerCenter = new ClampedFloatParameter(25f, 0f, 60f);
        public ClampedFloatParameter ozoneLayerThickness = new ClampedFloatParameter(15f, 0.01f, 20f);
        [InfoBox("Scales the geometric-series multiple-scattering solution; 1 matches the physical LUT default.")]
        public ClampedFloatParameter multipleScatteringIntensity = new ClampedFloatParameter(1.0f, 0f, 2f);
        [InfoBox("Matches XRender Trace Sample Count Scale. Higher values rebuild more accurate LUTs at a proportionally higher cost.")]
        public ClampedFloatParameter traceSampleCountScale = new ClampedFloatParameter(1.0f, 0.25f, 8f);
        [InfoBox("Legacy BRP analytic/fog intensity. The PhysicalSky SkyView and hard sun disk use XRender's outer-space main-light illuminance directly.")]
        public ClampedFloatParameter sunIntensity = new ClampedFloatParameter(0.6f, 0f, 64f);
        [Title("Sun Disk (PhysicalSky)")]
        [InfoBox("Full solar angular diameter in degrees. XRender authors this over 0..90 degrees and defaults to 6 degrees; Earth's approximately 0.5-degree disk remains available as an authored value. BRP converts the diameter to a half-angle and derives luminance from directional-light illuminance and solid angle.")]
        public ClampedFloatParameter sunDiskSize = new ClampedFloatParameter(6f, 0f, 90f);
        [InfoBox("Legacy analytic-fallback multiplier. PhysicalSky's active LUT path instead uses Stylized Sky / Sun Disk Color Scale, matching m_SunDiskColorScale.")]
        public ClampedFloatParameter sunDiskIntensity = new ClampedFloatParameter(1.2f, 0f, 16f);
        [Title("Sun Halo (BRP)")]
        public ClampedFloatParameter sunHaloSize = new ClampedFloatParameter(1.0f, 0.05f, 8f);
        public ClampedFloatParameter sunHaloIntensity = new ClampedFloatParameter(1.0f, 0f, 16f);
        public AtmosphereSunSourceParameter sunSource = new AtmosphereSunSourceParameter(AtmosphereSunSource.MainLight);
        public Vector3Parameter customSunDirection = new Vector3Parameter(new Vector3(0.3f, 0.8f, 0.4f));

        [Title("Main Light Coupling (XRender)")]
        [InfoBox("Applies XRender's 15-sample ground-level optical-depth transmittance to scene main-light shading. A value of one is XRender parity; zero keeps the original unattenuated BRP main light. Sky and atmosphere integration always use the unattenuated outer-space light color.")]
        public ClampedFloatParameter mainLightTransmittanceStrength = new ClampedFloatParameter(1.0f, 0f, 1f);
        [InfoBox("XRender-style environment occlusion shared by surface main-light shading, integrated physical sky, aerial perspective and atmosphere-driven fog. The project's custom PhysicalSky hard sun disk deliberately bypasses this factor.")]
        public ClampedFloatParameter mainLightOcclusion = new ClampedFloatParameter(1.0f, 0f, 1f);

        [Title("Shape")]
        public ClampedFloatParameter planetRadius = new ClampedFloatParameter(6360f, 0.1f, 10000f);
        public ClampedFloatParameter atmosphereHeight = new ClampedFloatParameter(60f, 0.1f, 200f);
        public ClampedFloatParameter rayleighScaleHeight = new ClampedFloatParameter(8f, 0.001f, 20f);
        public ClampedFloatParameter mieScaleHeight = new ClampedFloatParameter(1.2f, 0.001f, 10f);
        [InfoBox("Matches XRender's planet transform options. Volume components have no Transform, so Anchor World is an explicit world-space replacement for the XRender component transform.")]
        public AtmospherePlanetTransformModeParameter planetTransformMode = new AtmospherePlanetTransformModeParameter(AtmospherePlanetTransformMode.PlanetTopAtAbsoluteWorldOrigin);
        [InfoBox("Used by the two Anchor World modes. In Planet Top mode this position is on the ground; in Planet Center mode it is the center of the planet.")]
        public Vector3Parameter planetAnchorWorld = new Vector3Parameter(Vector3.zero);
        [InfoBox("Used only by Explicit Planet Center World. The default keeps world origin at ground level for XRender's 6360km planet.")]
        public Vector3Parameter planetCenterWorld = new Vector3Parameter(new Vector3(0f, -6360000f, 0f));
        [InfoBox("Converts BRP world units to physical kilometers. Use 0.001 for meter-based worlds.")]
        public ClampedFloatParameter worldToKilometers = new ClampedFloatParameter(0.001f, 0.000001f, 10f);
        [InfoBox("Physical planet ground albedo for the atmosphere multiple-scattering LUT. Matches XRender's authored Earth default (170/256), which its RenderProxy explicitly converts from gamma to linear before LUT generation. It is intentionally independent of the artistic Ground Color below.")]
        public ColorParameter groundAlbedo = new ColorParameter(new Color(0.6666667f, 0.6666667f, 0.6666667f, 1f), false, false, true);

        [Title("Art Direction")]
        public ColorParameter groundColor = new ColorParameter(new Color(0.18f, 0.20f, 0.18f, 1f), true, false, true);
        [InfoBox("Legacy BRP analytic-fallback control. XRender's project PhysicalSky material declares SkyTint but never reads it, and its LUT sky consumer does not apply this tint.")]
        public ColorParameter skyTint = new ColorParameter(new Color(0.65f, 0.78f, 1f, 1f), true, false, true);
        [InfoBox("Matches XRender Sky Luminance Factor. This RGB multiplier grades physical sky radiance at sampling time, so changing it does not rebuild atmosphere LUTs and does not tint the direct sun disk.")]
        public ColorParameter skyLuminanceFactor = new ColorParameter(Color.white, true, false, true);
        [InfoBox("Horizon colors, falloff, ground blend and the tonemap-safe analytic sun are legacy BRP analytic-fallback controls. The LUT PhysicalSky path uses integrated SkyView radiance instead.")]
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

        [Title("Physical Sky Mesh (XRender)")]
        [InfoBox("Like XRender, a non-null custom material replaces the built-in PhysicalSky material and uses pass 0. It must implement BRP's atmosphere globals and the two XRender permutation keywords.")]
        public AtmosphereSkyMaterialParameter physicalSkyMaterial = new AtmosphereSkyMaterialParameter(null);
        [InfoBox("Supplies XRender PhysicalSky's authored MeshUv0/MeshUv1. Null first uses the migrated DefaultSkyMesh; if that resource is unavailable, BRP follows XRender's three-vertex procedural fallback. MeshUv0 drives weather, stars, galaxy and moon masking; MeshUv1 drives panoramic clouds. Rotation and scale are intentionally ignored like XRender, while World Position replaces the source AtmosphereComponent transform translation.")]
        public AtmosphereSkyMeshParameter physicalSkyMesh = new AtmosphereSkyMeshParameter(null);
        public Vector3Parameter physicalSkyMeshWorldPosition = new Vector3Parameter(Vector3.zero);

        [Title("Physical Sky Time Of Day (XRender)")]
        [InfoBox("Exact replacement for PhysicalSky's _TodCurve. XRender supplies 0 through the day and a 0..1 sine arc through the night: values above 0 hide the sun disk, while values above 0.5 select the night permutation for moon, stars, weather-cloud luminance and panoramic-cloud UV/luminance. Animate this independently from the active main-light direction because XRender changes that light to the moon trajectory at night.")]
        public ClampedFloatParameter physicalSkyTimeOfDayCurve = new ClampedFloatParameter(0f, 0f, 1f);

        [Title("Stylized Sky (Legacy Analytic Fallback)")]
        [InfoBox("XRender uploads Blend, Base/Horizon colors, SunGlow, SunRise and falloff fields, but the project's active PhysicalSky.hlsl never reads them. BRP preserves their former effect only when LUTs are unavailable. Sun Disk Color Scale remains active in the LUT path because XRender consumes that value on the CPU when deriving physical disk luminance.")]
        public ClampedFloatParameter stylizedSkyBlend = new ClampedFloatParameter(0f, 0f, 1f);
        public ColorParameter stylizedBaseSkyColorDay = new ColorParameter(new Color(0.0838f, 0.1645f, 0.8716f, 1f), true, false, true);
        public ColorParameter stylizedBaseSkyColorDawnDusk = new ColorParameter(new Color(0.1651f, 0.1946f, 0.3662f, 1f), true, false, true);
        public ColorParameter stylizedBaseSkyColorNight = new ColorParameter(new Color(0.0166f, 0.0265f, 0.1245f, 1f), true, false, true);
        public ColorParameter stylizedHorizonSkyColorDay = new ColorParameter(new Color(0.55f, 0.66f, 1.92f, 1f), true, false, true);
        public ColorParameter stylizedHorizonSkyColorDawnDusk = new ColorParameter(new Color(0.4735f, 0.1844f, 0.1274f, 1f), true, false, true);
        public ColorParameter stylizedHorizonSkyColorNight = new ColorParameter(new Color(0.3132f, 0.2110f, 0.1672f, 1f), true, false, true);
        public ClampedFloatParameter stylizedHorizonBrightness = new ClampedFloatParameter(1.5f, 0f, 100f);
        public ClampedFloatParameter stylizedHorizonFalloff = new ClampedFloatParameter(10f, 0f, 100f);
        public ColorParameter stylizedSunDiskColorScale = new ColorParameter(Color.white, true, true, true);
        public ColorParameter stylizedSunGlowColor = new ColorParameter(Color.white, true, true, true);
        [InfoBox("Legacy analytic-fallback interval. XRender stores it as -60..7 and uploads the divided -0.6..0.07 range, but the active project PhysicalSky shader does not read it.")]
        public ClampedFloatParameter stylizedSunRiseBlendMin = new ClampedFloatParameter(-0.6f, -1f, 1f);
        public ClampedFloatParameter stylizedSunRiseBlendMax = new ClampedFloatParameter(0.07f, -1f, 1f);
        public ClampedFloatParameter stylizedSunGlowScale = new ClampedFloatParameter(3.5f, 0f, 5f);

        [Title("Moon (XRender)")]
        [InfoBox("XRender-compatible textured moon disk. Rotation is applied to the default forward/up/right basis; the visible moon direction is the negated rotated forward axis. On Mobile, the project shader expects a packed texture with surface in G, glow in B and mask in A, and uses its fixed low-cost projection.")]
        [InfoBox("XRender has no separate moon enable flag; this compatibility switch defaults on. A zero Moon Intensity still produces no moon.")]
        public BoolParameter moonEnabled = new BoolParameter(true);
        public TextureParameter moonSurfaceTexture = new TextureParameter(null);
        [InfoBox("PC always uses XRender PhysicalSky's moon-phase normal path and phased surface UVs. When null, XRender binds a white dummy texture; Mobile deliberately ignores this texture and the authored phase/earthshine/bloom controls.")]
        public TextureParameter moonPhaseNormalTexture = new TextureParameter(null);
        public Vector3Parameter moonRotationEuler = new Vector3Parameter(Vector3.zero);
        [InfoBox("Moon illuminance converted to disk luminance from the authored angular diameter using XRender's solid-angle formula.")]
        public ClampedFloatParameter moonIntensity = new ClampedFloatParameter(0f, 0f, 130000f);
        [InfoBox("The low-level AtmosphereComponent initializes this to 6 degrees, but the active XRender EnvSky layer always overrides it with its effective 2-degree project default and 0.1..20 range.")]
        public ClampedFloatParameter moonAngularDiameter = new ClampedFloatParameter(2f, 0.1f, 20f);
        public ColorParameter moonSurfaceTint = new ColorParameter(new Color(0.06f, 0.06f, 0.14f, 1f), true, false, true);
        [InfoBox("Matches PhysicalSky material _AddMoonTint. XRender applies it only to the textured moon disk, before the shared surface tint and disk luminance.")]
        public ColorParameter moonAdditionalTint = new ColorParameter(Color.white, true, true, true);
        public ClampedFloatParameter moonPhase = new ClampedFloatParameter(0.6f, 0f, 1f);
        public ClampedFloatParameter moonPhaseRotation = new ClampedFloatParameter(300f, 0f, 360f);
        [InfoBox("Normalized earthshine added to the moon phase. XRender authors this value magnified by 1000; BRP exposes the shader-space value directly, so 0.01 corresponds to XRender's authored value 10.")]
        public ClampedFloatParameter moonEarthshine = new ClampedFloatParameter(0.01f, 0f, 0.5f);
        public ClampedFloatParameter moonPhaseSharpness = new ClampedFloatParameter(5f, 0f, 10f);
        [InfoBox("XRender radial-gradient scale around the active night main light; this is not an angular size.")]
        public ClampedFloatParameter moonFlareSize = new ClampedFloatParameter(0f, 0f, 5f);
        public ClampedFloatParameter moonFlareFalloff = new ClampedFloatParameter(50f, 1f, 100f);
        public ColorParameter moonFlareTint = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter moonLightBloomIntensity = new ClampedFloatParameter(0f, 0f, 5f);
        public ClampedFloatParameter moonLightBloomSize = new ClampedFloatParameter(0f, 0f, 5f);
        public ClampedFloatParameter moonLightBloomFalloff = new ClampedFloatParameter(3f, 1f, 100f);
        public ClampedFloatParameter moonLightBloomEdgeAlpha = new ClampedFloatParameter(20f, 0f, 100f);
        [InfoBox("Legacy authored values retained for profile compatibility. PhysicalSky uploads this 10..75 interval but neither its PC nor Mobile moon shader consumes it; visibility is controlled by the night permutation instead.")]
        public ClampedFloatParameter moonRiseBlendMin = new ClampedFloatParameter(0.1f, -1f, 1f);
        public ClampedFloatParameter moonRiseBlendMax = new ClampedFloatParameter(0.75f, -1f, 1f);

        [Title("Stars and Galaxy (XRender)")]
        [InfoBox("XRender's PhysicalSky star field is evaluated only for the camera sky at night and excluded from reflection captures. PC uses authored three-layer, area, twinkle, tint and full-galaxy controls. Mobile uses two fixed layers, fixed intensity/falloff and a fixed 115-degree galaxy-cloud transform; only layer speed and the shared textures/custom-star controls remain relevant.")]
        [InfoBox("XRender has no separate star-field enable flag; this compatibility switch defaults on. Black fallback textures keep an unconfigured profile dark.")]
        public BoolParameter starsEnabled = new BoolParameter(true);
        public TextureParameter starsTexture = new TextureParameter(null);
        public TextureParameter starsTintColorTexture = new TextureParameter(null);
        public ClampedFloatParameter starsIntensity = new ClampedFloatParameter(0.15f, 0f, 3f);
        public ClampedFloatParameter starsRotation = new ClampedFloatParameter(0f, 0f, 360f);
        public ColorParameter starsTintColor = new ColorParameter(Color.white, true, false, true);
        public ClampedFloatParameter starsTintColorSaturation = new ClampedFloatParameter(0f, 0f, 1f);
        public Vector2Parameter starsTintColorTextureTiling = new Vector2Parameter(Vector2.one);
        public Vector2Parameter starsTintColorTextureOffset = new Vector2Parameter(Vector2.zero);
        public ClampedFloatParameter starsLayer1Height = new ClampedFloatParameter(6f, 1f, 20f);
        public ClampedFloatParameter starsLayer2Height = new ClampedFloatParameter(7f, 1f, 20f);
        public ClampedFloatParameter starsLayer3Height = new ClampedFloatParameter(8f, 1f, 20f);
        public ClampedFloatParameter starsLayerSpeed = new ClampedFloatParameter(0.01f, 0f, 1f);
        public ClampedFloatParameter starsLayerTwinkleStrength = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter starsLayerTwinkleSpeed = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter starsLayer1Falloff = new ClampedFloatParameter(2f, 1f, 5f);
        public ClampedFloatParameter starsLayer2Falloff = new ClampedFloatParameter(2f, 1f, 5f);
        public ClampedFloatParameter starsLayer3Falloff = new ClampedFloatParameter(2f, 1f, 5f);
        public ClampedFloatParameter starsHorizonFalloff = new ClampedFloatParameter(1f, 0f, 5f);
        public TextureParameter areaStarsTexture = new TextureParameter(null);
        public ClampedFloatParameter areaStarsIntensity = new ClampedFloatParameter(0.15f, 0f, 3f);
        public Vector2Parameter areaStarsDensityMinMax = new Vector2Parameter(new Vector2(20f, 50f));
        public Vector2Parameter areaStarsMaskTiling = new Vector2Parameter(new Vector2(2f, 0.5f));
        public Vector2Parameter areaStarsMaskOffset = new Vector2Parameter(Vector2.zero);
        public ClampedFloatParameter areaStarsSpeed = new ClampedFloatParameter(0.1f, 0f, 1f);
        public ClampedFloatParameter areaStarsFalloff = new ClampedFloatParameter(1.25f, 1f, 10f);
        public ClampedFloatParameter areaStarsMaskFalloff = new ClampedFloatParameter(2.5f, 1f, 10f);
        public TextureParameter galaxyCloudTexture = new TextureParameter(null);
        public Vector2Parameter galaxyCloudTiling = new Vector2Parameter(new Vector2(0.5f, 1.5f));
        public Vector2Parameter galaxyCloudOffset = new Vector2Parameter(new Vector2(-0.3f, -0.3f));
        public ClampedFloatParameter galaxyCloudRotation = new ClampedFloatParameter(117f, 0f, 360f);
        public ClampedFloatParameter galaxyCloudIntensity = new ClampedFloatParameter(0.0001f, 0f, 1f);
        public ClampedFloatParameter galaxyCloudFalloff = new ClampedFloatParameter(2f, 1f, 100f);
        public ClampedFloatParameter galaxyStarIntensity = new ClampedFloatParameter(0.15f, 0f, 3f);
        public ClampedFloatParameter galaxyStarFalloff = new ClampedFloatParameter(1.5f, 1f, 10f);
        public ClampedFloatParameter galaxyStarHeight = new ClampedFloatParameter(6f, 1f, 20f);
        public ClampedFloatParameter galaxyStarSpeed = new ClampedFloatParameter(0.01f, 0f, 1f);
        [InfoBox("Optional XRender PhysicalSky custom star projected around the moon. Texture Scale/Offset correspond to Unity's generated _AddCustomStarTex_ST vector.")]
        public TextureParameter customStarTexture = new TextureParameter(null);
        public Vector2Parameter customStarTextureScale = new Vector2Parameter(Vector2.one);
        public Vector2Parameter customStarTextureOffset = new Vector2Parameter(Vector2.zero);
        public ClampedFloatParameter customStarRotation = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter customStarScaleMin = new ClampedFloatParameter(0.8f, 0f, 1f);
        public Vector4Parameter customStarIntensityMax = new Vector4Parameter(new Vector4(10f, 5f, 5f, 100f));
        public Vector4Parameter customStarIntensityMin = new Vector4Parameter(new Vector4(1f, 1f, 1f, 0.1f));
        public ClampedFloatParameter customStarScatterSpeed = new ClampedFloatParameter(10f, 0f, 100f);
        public ClampedFloatParameter customStarScatterInterval = new ClampedFloatParameter(5f, 0f, 100f);

        [Title("Panoramic Clouds (XRender)")]
        [InfoBox("Volume replacement for PhysicalSky's material + NatureCommon skybox-cloud contract. Default Texture maps to XRender's effective _CloudTexDay path; _CloudTexNight exists in the source material but is not sampled by PhysicalSky.")]
        [InfoBox("Matches XRender's enabled-by-default New SkyBox Cloud path. Black fallback textures keep it visually inert until a cloud texture is assigned.")]
        public BoolParameter panoramicCloudEnabled = new BoolParameter(true);
        public BoolParameter panoramicCloudUseDefaultTexture = new BoolParameter(false);
        public TextureParameter panoramicCloudDefaultTexture = new TextureParameter(null);
        public TextureParameter panoramicCloudPreviousWeatherTexture = new TextureParameter(null);
        public TextureParameter panoramicCloudCurrentWeatherTexture = new TextureParameter(null);
        public BoolParameter panoramicCloudTextureInTransition = new BoolParameter(false);
        public ClampedFloatParameter panoramicCloudTextureTransition = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter panoramicCloudDayUvOffset = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter panoramicCloudNightUvOffset = new ClampedFloatParameter(0f, -1f, 1f);
        public ClampedFloatParameter panoramicCloudRotationSpeed = new ClampedFloatParameter(0.0002f, -0.0006f, 0.0006f);
        public ClampedFloatParameter panoramicCloudSunnyLuminance = new ClampedFloatParameter(7000f, 0f, 100000f);
        public ClampedFloatParameter panoramicCloudNightLuminance = new ClampedFloatParameter(0.1f, 0f, 100000f);
        public BoolParameter panoramicCloudIgnoreTimeOfDayColors = new BoolParameter(false);
        public ColorParameter panoramicCloudBaseColor = new ColorParameter(Color.white, true, true, true);
        public ColorParameter panoramicCloudDetailSpecular = new ColorParameter(Color.white, true, true, true);
        [InfoBox("Matches XRender SkyBox Cloud Alpha's authored 0..100 range; PhysicalSky divides this value by 100 unless Ignore Time Of Day Colors is enabled.")]
        public ClampedFloatParameter panoramicCloudAlpha = new ClampedFloatParameter(1f, 0f, 100f);

        [Title("Physical Sky Desaturation (XRender)")]
        [InfoBox("XRender shares one desaturation effect between the atmosphere/weather-sky term and panoramic clouds. Force Enabled matches the material toggle; otherwise Effect matches the global _DesaturationEffect animated by SkyColorDesaturate.")]
        public BoolParameter physicalSkyDesaturationEnabled = new BoolParameter(false);
        public ColorParameter physicalSkyDesaturationColor = new ColorParameter(Color.white, false, true, true);
        [FormerlySerializedAs("panoramicCloudDesaturationBlend")]
        public ClampedFloatParameter physicalSkyDesaturationEffect = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter physicalSkyDesaturationIntensity = new ClampedFloatParameter(0.1f, 0f, 0.3f);
        [FormerlySerializedAs("panoramicCloudDesaturationIntensity")]
        public ClampedFloatParameter physicalSkyCloudDesaturationIntensity = new ClampedFloatParameter(0.05f, 0f, 0.1f);

        [Title("Weather Sky Coverage (XRender)")]
        [InfoBox("Shared PhysicalSky weather coverage. XRender consumes the raw maximum of Rain Intensity/Rain Wet Coverage and Snow Intensity/Snow Coverage, then applies distinct formulas to sky clouds, sun, moon and stars.")]
        [InfoBox("XRender evaluates weather coverage without a separate feature toggle. This compatibility switch defaults on; zero rain/snow weights keep the effect inactive.")]
        [InfoBox("Weather Cloud Shadow only offsets and shades the PhysicalSky weather-cloud texture. It does not attenuate scene lighting or fog; use Main Light Occlusion for that independent XRender lighting control.")]
        public BoolParameter weatherSkyCoverageEnabled = new BoolParameter(true);
        public TextureParameter weatherSkyCoverageTexture = new TextureParameter(null);
        public FloatParameter weatherRainIntensity = new FloatParameter(0f);
        public FloatParameter weatherRainWetCoverage = new FloatParameter(0f);
        public FloatParameter weatherSnowIntensity = new FloatParameter(0f);
        public FloatParameter weatherSnowCoverage = new FloatParameter(0f);
        public FloatParameter weatherCloudShadowMarchDistance = new FloatParameter(0.03f);
        public ColorParameter weatherCloudShadowBright = new ColorParameter(new Color(0.76f, 0.77f, 0.8f, 1f), false, true, true);
        public ColorParameter weatherCloudShadowDark = new ColorParameter(new Color(0.45f, 0.5f, 0.6f, 1f), false, true, true);

        [Title("Aerial Perspective")]
        [InfoBox("XRender mobile disables Aerial Perspective completely and does not allocate the 32x32x16 atmosphere Fog LUT. These controls remain authored in the profile but are consumed only on PC.")]
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
        [InfoBox("Legacy BRP analytic-fallback height fade. The physical XRender Fog LUT path does not consume it.")]
        public ClampedFloatParameter aerialPerspectiveHeightFalloff = new ClampedFloatParameter(0.0f, 0f, 2f);
        [InfoBox("Legacy BRP analytic-fallback tint. Physical Fog LUT RGB is graded only by outer-space main-light illuminance, environment occlusion and Atmosphere Fog Luminance Scale.")]
        public ColorParameter aerialPerspectiveTint = new ColorParameter(Color.white, true, false, true);
        [InfoBox("Matches XRender Atmosphere Fog Start Distance. This is the physical Fog LUT ray origin and lookup-depth offset, in BRP world units. The 100-unit default equals XRender's 0.1 km default in a meter-based world.")]
        [FormerlySerializedAs("aerialPerspectiveNearFadeStart")]
        public ClampedFloatParameter aerialPerspectiveStartDepth = new ClampedFloatParameter(100f, 0f, 100000f);
        [InfoBox("Legacy BRP analytic-fallback smooth-start endpoint. The physical path uses XRender's Start Distance plus its intrinsic first-froxel fade.")]
        public ClampedFloatParameter aerialPerspectiveNearFadeEnd = new ClampedFloatParameter(100.001f, 0f, 100000f);
        [InfoBox("Legacy BRP analytic-fallback opacity cap. Physical Fog LUT transmittance is used directly after XRender's intrinsic first-froxel fade.")]
        public ClampedFloatParameter aerialPerspectiveMaxOpacity = new ClampedFloatParameter(1f, 0f, 1f);
        public AtmosphereAerialPerspectivePlacementParameter aerialPerspectivePlacement = new AtmosphereAerialPerspectivePlacementParameter(AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky);
        public AtmosphereFogInteractionParameter aerialFogInteraction = new AtmosphereFogInteractionParameter(AtmosphereFogInteraction.Additive);

        // Source compatibility for scripts written against the pre-XRender-parity
        // BRP name. Unity assets migrate through FormerlySerializedAs above.
        public ClampedFloatParameter aerialPerspectiveNearFadeStart
        {
            get => aerialPerspectiveStartDepth;
            set => aerialPerspectiveStartDepth = value;
        }

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
