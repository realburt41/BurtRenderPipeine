using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public readonly struct BurtAtmosphereDebugSnapshot
    {
        public readonly bool Enabled;
        public readonly float RayleighIntensity;
        public readonly float MieIntensity;
        public readonly float MieAnisotropy;
        public readonly Color RayleighScatteringCoefficient;
        public readonly Color MieScatteringCoefficient;
        public readonly Color MieAbsorptionCoefficient;
        public readonly float OzoneAbsorptionIntensity;
        public readonly Color OzoneAbsorptionCoefficient;
        public readonly float OzoneLayerCenter;
        public readonly float OzoneLayerThickness;
        public readonly float MultipleScatteringIntensity;
        public readonly float TraceSampleCountScale;
        public readonly float PlanetRadius;
        public readonly float AtmosphereHeight;
        public readonly float RayleighScaleHeight;
        public readonly float MieScaleHeight;
        public readonly AtmospherePlanetTransformMode PlanetTransformMode;
        public readonly Vector3 PlanetAnchorWorld;
        public readonly Vector3 PlanetCenterWorld;
        public readonly float WorldToKilometers;
        public readonly Color GroundAlbedo;
        public readonly Color GroundColor;
        public readonly Color SkyTint;
        public readonly Color SkyLuminanceFactor;
        public readonly float SunIntensity;
        public readonly float SunDiskSize;
        public readonly float SunDiskIntensity;
        public readonly float SunHaloSize;
        public readonly float SunHaloIntensity;
        public readonly AtmosphereSunSource SunSource;
        public readonly Vector3 CustomSunDirection;
        public readonly float MainLightTransmittanceStrength;
        public readonly float MainLightOcclusion;
        public readonly Color HorizonColor;
        public readonly Color HorizonSunsetColor;
        public readonly float HorizonIntensity;
        public readonly float HorizonFalloff;
        public readonly float HorizonSunsetInfluence;
        public readonly float GroundContribution;
        public readonly float GroundBlendStart;
        public readonly float GroundBlendEnd;
        public readonly float ExposureCompensation;
        public readonly float TonemapSafeSunIntensity;
        public readonly float StylizedSkyBlend;
        public readonly Color StylizedBaseSkyColorDay;
        public readonly Color StylizedBaseSkyColorDawnDusk;
        public readonly Color StylizedBaseSkyColorNight;
        public readonly Color StylizedHorizonSkyColorDay;
        public readonly Color StylizedHorizonSkyColorDawnDusk;
        public readonly Color StylizedHorizonSkyColorNight;
        public readonly float StylizedHorizonBrightness;
        public readonly float StylizedHorizonFalloff;
        public readonly Color StylizedSunDiskColorScale;
        public readonly Color StylizedSunGlowColor;
        public readonly float StylizedSunRiseBlendMin;
        public readonly float StylizedSunRiseBlendMax;
        public readonly float StylizedSunGlowScale;
        public readonly bool MoonEnabled;
        public readonly Texture2D MoonSurfaceTexture;
        public readonly Vector3 MoonRotationEuler;
        public readonly float MoonIntensity;
        public readonly float MoonAngularDiameter;
        public readonly Color MoonSurfaceTint;
        public readonly float MoonPhase;
        public readonly float MoonPhaseRotation;
        public readonly float MoonEarthshine;
        public readonly float MoonFlareSize;
        public readonly float MoonFlareFalloff;
        public readonly Color MoonFlareTint;
        public readonly float MoonRiseBlendMin;
        public readonly float MoonRiseBlendMax;
        public readonly bool AerialPerspectiveEnabled;
        public readonly float AerialPerspectiveDensityScale;
        public readonly float AerialPerspectiveLuminanceScale;
        public readonly float AerialPerspectiveSamplingDistanceScale;
        public readonly float AerialPerspectiveIntensity;
        public readonly float AerialPerspectiveDistance;
        public readonly float AerialPerspectiveHeightFalloff;
        public readonly Color AerialPerspectiveTint;
        public readonly float AerialPerspectiveNearFadeStart;
        public readonly float AerialPerspectiveNearFadeEnd;
        public readonly float AerialPerspectiveMaxOpacity;
        public readonly AtmosphereAerialPerspectivePlacement AerialPerspectivePlacement;
        public readonly AtmosphereFogInteraction FogInteraction;
        public readonly string SkyFormula;
        public readonly string AerialFormula;

        internal BurtAtmosphereDebugSnapshot(BurtAtmosphereSettings settings, string skyFormula, string aerialFormula)
        {
            Enabled = settings.Enabled;
            RayleighIntensity = settings.RayleighIntensity;
            MieIntensity = settings.MieIntensity;
            MieAnisotropy = settings.MieAnisotropy;
            RayleighScatteringCoefficient = settings.RayleighScatteringCoefficient;
            MieScatteringCoefficient = settings.MieScatteringCoefficient;
            MieAbsorptionCoefficient = settings.MieAbsorptionCoefficient;
            OzoneAbsorptionIntensity = settings.OzoneAbsorptionIntensity;
            OzoneAbsorptionCoefficient = settings.OzoneAbsorptionCoefficient;
            OzoneLayerCenter = settings.OzoneLayerCenter;
            OzoneLayerThickness = settings.OzoneLayerThickness;
            MultipleScatteringIntensity = settings.MultipleScatteringIntensity;
            TraceSampleCountScale = settings.TraceSampleCountScale;
            PlanetRadius = settings.PlanetRadius;
            AtmosphereHeight = settings.AtmosphereHeight;
            RayleighScaleHeight = settings.RayleighScaleHeight;
            MieScaleHeight = settings.MieScaleHeight;
            PlanetTransformMode = settings.PlanetTransformMode;
            PlanetAnchorWorld = settings.PlanetAnchorWorld;
            PlanetCenterWorld = settings.PlanetCenterWorld;
            WorldToKilometers = settings.WorldToKilometers;
            GroundAlbedo = settings.GroundAlbedo;
            GroundColor = settings.GroundColor;
            SkyTint = settings.SkyTint;
            SkyLuminanceFactor = settings.SkyLuminanceFactor;
            SunIntensity = settings.SunIntensity;
            SunDiskSize = settings.SunDiskSize;
            SunDiskIntensity = settings.SunDiskIntensity;
            SunHaloSize = settings.SunHaloSize;
            SunHaloIntensity = settings.SunHaloIntensity;
            SunSource = settings.SunSource;
            CustomSunDirection = settings.CustomSunDirection;
            MainLightTransmittanceStrength = settings.MainLightTransmittanceStrength;
            MainLightOcclusion = settings.MainLightOcclusion;
            HorizonColor = settings.HorizonColor;
            HorizonSunsetColor = settings.HorizonSunsetColor;
            HorizonIntensity = settings.HorizonIntensity;
            HorizonFalloff = settings.HorizonFalloff;
            HorizonSunsetInfluence = settings.HorizonSunsetInfluence;
            GroundContribution = settings.GroundContribution;
            GroundBlendStart = settings.GroundBlendStart;
            GroundBlendEnd = settings.GroundBlendEnd;
            ExposureCompensation = settings.ExposureCompensation;
            TonemapSafeSunIntensity = settings.TonemapSafeSunIntensity;
            StylizedSkyBlend = settings.StylizedSky.Blend;
            StylizedBaseSkyColorDay = settings.StylizedSky.BaseSkyColorDay;
            StylizedBaseSkyColorDawnDusk = settings.StylizedSky.BaseSkyColorDawnDusk;
            StylizedBaseSkyColorNight = settings.StylizedSky.BaseSkyColorNight;
            StylizedHorizonSkyColorDay = settings.StylizedSky.HorizonSkyColorDay;
            StylizedHorizonSkyColorDawnDusk = settings.StylizedSky.HorizonSkyColorDawnDusk;
            StylizedHorizonSkyColorNight = settings.StylizedSky.HorizonSkyColorNight;
            StylizedHorizonBrightness = settings.StylizedSky.HorizonBrightness;
            StylizedHorizonFalloff = settings.StylizedSky.HorizonFalloff;
            StylizedSunDiskColorScale = settings.StylizedSky.SunDiskColorScale;
            StylizedSunGlowColor = settings.StylizedSky.SunGlowColor;
            StylizedSunRiseBlendMin = settings.StylizedSky.SunRiseBlendMin;
            StylizedSunRiseBlendMax = settings.StylizedSky.SunRiseBlendMax;
            StylizedSunGlowScale = settings.StylizedSky.SunGlowScale;
            MoonEnabled = settings.Moon.Enabled;
            MoonSurfaceTexture = settings.Moon.SurfaceTexture;
            MoonRotationEuler = settings.Moon.RotationEuler;
            MoonIntensity = settings.Moon.Intensity;
            MoonAngularDiameter = settings.Moon.AngularDiameter;
            MoonSurfaceTint = settings.Moon.SurfaceTint;
            MoonPhase = settings.Moon.Phase;
            MoonPhaseRotation = settings.Moon.PhaseRotation;
            MoonEarthshine = settings.Moon.Earthshine;
            MoonFlareSize = settings.Moon.FlareSize;
            MoonFlareFalloff = settings.Moon.FlareFalloff;
            MoonFlareTint = settings.Moon.FlareTint;
            MoonRiseBlendMin = settings.Moon.RiseBlendMin;
            MoonRiseBlendMax = settings.Moon.RiseBlendMax;
            AerialPerspectiveEnabled = settings.AerialPerspectiveEnabled;
            AerialPerspectiveDensityScale = settings.AerialPerspectiveDensityScale;
            AerialPerspectiveLuminanceScale = settings.AerialPerspectiveLuminanceScale;
            AerialPerspectiveSamplingDistanceScale = settings.AerialPerspectiveSamplingDistanceScale;
            AerialPerspectiveIntensity = settings.AerialPerspectiveIntensity;
            AerialPerspectiveDistance = settings.AerialPerspectiveDistance;
            AerialPerspectiveHeightFalloff = settings.AerialPerspectiveHeightFalloff;
            AerialPerspectiveTint = settings.AerialPerspectiveTint;
            AerialPerspectiveNearFadeStart = settings.AerialPerspectiveNearFadeStart;
            AerialPerspectiveNearFadeEnd = settings.AerialPerspectiveNearFadeEnd;
            AerialPerspectiveMaxOpacity = settings.AerialPerspectiveMaxOpacity;
            AerialPerspectivePlacement = settings.AerialPerspectivePlacement;
            FogInteraction = settings.FogInteraction;
            SkyFormula = string.IsNullOrEmpty(skyFormula) ? BurtAtmosphereUtility.SkyFormulaName : skyFormula;
            AerialFormula = string.IsNullOrEmpty(aerialFormula) ? BurtAtmosphereUtility.AerialFormulaName : aerialFormula;
        }
    }

    public static class BurtAtmosphereDebugUtility
    {
        public static BurtAtmosphereDebugSnapshot GetSnapshot()
        {
            return new BurtAtmosphereDebugSnapshot(
                BurtAtmosphereUtility.ResolveSettings(),
                BurtAtmosphereUtility.SkyFormulaName,
                BurtAtmosphereUtility.AerialFormulaName);
        }

        public static string FormatDebugState()
        {
            return BurtAtmosphereUtility.FormatDebugState();
        }
    }

    internal readonly struct BurtAtmosphereStylizedSkySettings
    {
        public readonly float Blend;
        public readonly Color BaseSkyColorDay;
        public readonly Color BaseSkyColorDawnDusk;
        public readonly Color BaseSkyColorNight;
        public readonly Color HorizonSkyColorDay;
        public readonly Color HorizonSkyColorDawnDusk;
        public readonly Color HorizonSkyColorNight;
        public readonly float HorizonBrightness;
        public readonly float HorizonFalloff;
        public readonly Color SunDiskColorScale;
        public readonly Color SunGlowColor;
        public readonly float SunRiseBlendMin;
        public readonly float SunRiseBlendMax;
        public readonly float SunGlowScale;

        public BurtAtmosphereStylizedSkySettings(
            float blend,
            Color baseSkyColorDay,
            Color baseSkyColorDawnDusk,
            Color baseSkyColorNight,
            Color horizonSkyColorDay,
            Color horizonSkyColorDawnDusk,
            Color horizonSkyColorNight,
            float horizonBrightness,
            float horizonFalloff,
            Color sunDiskColorScale,
            Color sunGlowColor,
            float sunRiseBlendMin,
            float sunRiseBlendMax,
            float sunGlowScale)
        {
            Blend = Mathf.Clamp01(blend);
            BaseSkyColorDay = ClampHdrColor(baseSkyColorDay);
            BaseSkyColorDawnDusk = ClampHdrColor(baseSkyColorDawnDusk);
            BaseSkyColorNight = ClampHdrColor(baseSkyColorNight);
            HorizonSkyColorDay = ClampHdrColor(horizonSkyColorDay);
            HorizonSkyColorDawnDusk = ClampHdrColor(horizonSkyColorDawnDusk);
            HorizonSkyColorNight = ClampHdrColor(horizonSkyColorNight);
            HorizonBrightness = Mathf.Clamp(horizonBrightness, 0f, 100f);
            HorizonFalloff = Mathf.Clamp(horizonFalloff, 0.1f, 100f);
            SunDiskColorScale = ClampHdrColor(sunDiskColorScale);
            SunGlowColor = ClampHdrColor(sunGlowColor);
            SunRiseBlendMin = Mathf.Clamp(sunRiseBlendMin, -1f, 0.999f);
            SunRiseBlendMax = Mathf.Clamp(sunRiseBlendMax, SunRiseBlendMin + 0.001f, 1f);
            SunGlowScale = Mathf.Clamp(sunGlowScale, 0f, 5f);
        }

        public static BurtAtmosphereStylizedSkySettings Disabled => new BurtAtmosphereStylizedSkySettings(
            0f,
            new Color(0.0838f, 0.1645f, 0.8716f, 1f),
            new Color(0.1651f, 0.1946f, 0.3662f, 1f),
            new Color(0.0166f, 0.0265f, 0.1245f, 1f),
            new Color(0.55f, 0.66f, 1.92f, 1f),
            new Color(0.4735f, 0.1844f, 0.1274f, 1f),
            new Color(0.3132f, 0.2110f, 0.1672f, 1f),
            1.5f,
            10f,
            Color.white,
            Color.white,
            -0.6f,
            0.07f,
            3.5f);

        private static Color ClampHdrColor(Color value)
        {
            return new Color(Mathf.Max(0f, value.r), Mathf.Max(0f, value.g), Mathf.Max(0f, value.b), 1f);
        }
    }

    internal readonly struct BurtAtmosphereMoonSettings
    {
        public readonly bool Enabled;
        public readonly Texture2D SurfaceTexture;
        public readonly Vector3 RotationEuler;
        public readonly float Intensity;
        public readonly float AngularDiameter;
        public readonly Color SurfaceTint;
        public readonly float Phase;
        public readonly float PhaseRotation;
        public readonly float Earthshine;
        public readonly float FlareSize;
        public readonly float FlareFalloff;
        public readonly Color FlareTint;
        public readonly float RiseBlendMin;
        public readonly float RiseBlendMax;

        public BurtAtmosphereMoonSettings(
            bool enabled,
            Texture surfaceTexture,
            Vector3 rotationEuler,
            float intensity,
            float angularDiameter,
            Color surfaceTint,
            float phase,
            float phaseRotation,
            float earthshine,
            float flareSize,
            float flareFalloff,
            Color flareTint,
            float riseBlendMin,
            float riseBlendMax)
        {
            SurfaceTexture = surfaceTexture as Texture2D;
            RotationEuler = rotationEuler;
            Intensity = Mathf.Clamp(intensity, 0f, 130000f);
            AngularDiameter = Mathf.Clamp(angularDiameter, 0.05f, 90f);
            SurfaceTint = ClampHdrColor(surfaceTint);
            Phase = Mathf.Clamp01(phase);
            PhaseRotation = Mathf.Repeat(phaseRotation, 360f);
            Earthshine = Mathf.Clamp(earthshine, 0f, 0.5f);
            FlareSize = Mathf.Clamp(flareSize, 0f, 5f);
            FlareFalloff = Mathf.Clamp(flareFalloff, 1f, 100f);
            FlareTint = ClampHdrColor(flareTint);
            RiseBlendMin = Mathf.Clamp(riseBlendMin, -1f, 0.999f);
            RiseBlendMax = Mathf.Clamp(riseBlendMax, RiseBlendMin + 0.001f, 1f);
            Enabled = enabled && Intensity > 0.0001f;
        }

        public static BurtAtmosphereMoonSettings Disabled => new BurtAtmosphereMoonSettings(
            false,
            null,
            Vector3.zero,
            0f,
            6f,
            new Color(0.06f, 0.06f, 0.14f, 1f),
            0.6f,
            300f,
            0.01f,
            0f,
            50f,
            Color.white,
            0.1f,
            0.75f);

        private static Color ClampHdrColor(Color value)
        {
            return new Color(Mathf.Max(0f, value.r), Mathf.Max(0f, value.g), Mathf.Max(0f, value.b), 1f);
        }
    }

    internal readonly struct BurtAtmosphereSettings
    {
        public readonly bool Enabled;
        public readonly float RayleighIntensity;
        public readonly float MieIntensity;
        public readonly float MieAnisotropy;
        public readonly Color RayleighScatteringCoefficient;
        public readonly Color MieScatteringCoefficient;
        public readonly Color MieAbsorptionCoefficient;
        public readonly float OzoneAbsorptionIntensity;
        public readonly Color OzoneAbsorptionCoefficient;
        public readonly float OzoneLayerCenter;
        public readonly float OzoneLayerThickness;
        public readonly float MultipleScatteringIntensity;
        public readonly float TraceSampleCountScale;
        public readonly float PlanetRadius;
        public readonly float AtmosphereHeight;
        public readonly float RayleighScaleHeight;
        public readonly float MieScaleHeight;
        public readonly AtmospherePlanetTransformMode PlanetTransformMode;
        public readonly Vector3 PlanetAnchorWorld;
        public readonly Vector3 PlanetCenterWorld;
        public readonly float WorldToKilometers;
        public readonly Color GroundAlbedo;
        public readonly Color GroundColor;
        public readonly Color SkyTint;
        public readonly Color SkyLuminanceFactor;
        public readonly float SunIntensity;
        public readonly float SunDiskSize;
        public readonly float SunDiskIntensity;
        public readonly float SunHaloSize;
        public readonly float SunHaloIntensity;
        public readonly AtmosphereSunSource SunSource;
        public readonly Vector3 CustomSunDirection;
        public readonly float MainLightTransmittanceStrength;
        public readonly float MainLightOcclusion;
        public readonly Color HorizonColor;
        public readonly Color HorizonSunsetColor;
        public readonly float HorizonIntensity;
        public readonly float HorizonFalloff;
        public readonly float HorizonSunsetInfluence;
        public readonly float GroundContribution;
        public readonly float GroundBlendStart;
        public readonly float GroundBlendEnd;
        public readonly float ExposureCompensation;
        public readonly float TonemapSafeSunIntensity;
        public readonly BurtAtmosphereStylizedSkySettings StylizedSky;
        public readonly BurtAtmosphereMoonSettings Moon;
        public readonly bool AerialPerspectiveEnabled;
        public readonly float AerialPerspectiveDensityScale;
        public readonly float AerialPerspectiveLuminanceScale;
        public readonly float AerialPerspectiveSamplingDistanceScale;
        public readonly float AerialPerspectiveIntensity;
        public readonly float AerialPerspectiveDistance;
        public readonly float AerialPerspectiveHeightFalloff;
        public readonly Color AerialPerspectiveTint;
        public readonly float AerialPerspectiveNearFadeStart;
        public readonly float AerialPerspectiveNearFadeEnd;
        public readonly float AerialPerspectiveMaxOpacity;
        public readonly AtmosphereAerialPerspectivePlacement AerialPerspectivePlacement;
        public readonly AtmosphereFogInteraction FogInteraction;

        public BurtAtmosphereSettings(
            bool enabled,
            float rayleighIntensity,
            float mieIntensity,
            float mieAnisotropy,
            Color rayleighScatteringCoefficient,
            Color mieScatteringCoefficient,
            Color mieAbsorptionCoefficient,
            float ozoneAbsorptionIntensity,
            Color ozoneAbsorptionCoefficient,
            float ozoneLayerCenter,
            float ozoneLayerThickness,
            float multipleScatteringIntensity,
            float traceSampleCountScale,
            float planetRadius,
            float atmosphereHeight,
            float rayleighScaleHeight,
            float mieScaleHeight,
            AtmospherePlanetTransformMode planetTransformMode,
            Vector3 planetAnchorWorld,
            Vector3 planetCenterWorld,
            float worldToKilometers,
            Color groundAlbedo,
            Color groundColor,
            Color skyTint,
            Color skyLuminanceFactor,
            float sunIntensity,
            float sunDiskSize,
            float sunDiskIntensity,
            float sunHaloSize,
            float sunHaloIntensity,
            AtmosphereSunSource sunSource,
            Vector3 customSunDirection,
            float mainLightTransmittanceStrength,
            float mainLightOcclusion,
            Color horizonColor,
            Color horizonSunsetColor,
            float horizonIntensity,
            float horizonFalloff,
            float horizonSunsetInfluence,
            float groundContribution,
            float groundBlendStart,
            float groundBlendEnd,
            float exposureCompensation,
            float tonemapSafeSunIntensity,
            BurtAtmosphereStylizedSkySettings stylizedSky,
            BurtAtmosphereMoonSettings moon,
            bool aerialPerspectiveEnabled,
            float aerialPerspectiveDensityScale,
            float aerialPerspectiveLuminanceScale,
            float aerialPerspectiveSamplingDistanceScale,
            float aerialPerspectiveIntensity,
            float aerialPerspectiveDistance,
            float aerialPerspectiveHeightFalloff,
            Color aerialPerspectiveTint,
            float aerialPerspectiveNearFadeStart,
            float aerialPerspectiveNearFadeEnd,
            float aerialPerspectiveMaxOpacity,
            AtmosphereAerialPerspectivePlacement aerialPerspectivePlacement,
            AtmosphereFogInteraction fogInteraction)
        {
            Enabled = enabled;
            RayleighIntensity = Mathf.Max(0f, rayleighIntensity);
            MieIntensity = Mathf.Max(0f, mieIntensity);
            MieAnisotropy = Mathf.Clamp(mieAnisotropy, -0.95f, 0.95f);
            RayleighScatteringCoefficient = ClampCoefficient(rayleighScatteringCoefficient);
            MieScatteringCoefficient = ClampCoefficient(mieScatteringCoefficient);
            MieAbsorptionCoefficient = ClampCoefficient(mieAbsorptionCoefficient);
            OzoneAbsorptionIntensity = Mathf.Max(0f, ozoneAbsorptionIntensity);
            OzoneAbsorptionCoefficient = ClampCoefficient(ozoneAbsorptionCoefficient);
            OzoneLayerCenter = Mathf.Max(0f, ozoneLayerCenter);
            OzoneLayerThickness = Mathf.Max(0.1f, ozoneLayerThickness);
            MultipleScatteringIntensity = Mathf.Max(0f, multipleScatteringIntensity);
            TraceSampleCountScale = Mathf.Clamp(traceSampleCountScale, 0.25f, 8f);
            PlanetRadius = Mathf.Max(100f, planetRadius);
            AtmosphereHeight = Mathf.Max(1f, atmosphereHeight);
            RayleighScaleHeight = Mathf.Max(0.1f, rayleighScaleHeight);
            MieScaleHeight = Mathf.Max(0.1f, mieScaleHeight);
            PlanetTransformMode = planetTransformMode;
            PlanetAnchorWorld = planetAnchorWorld;
            PlanetCenterWorld = planetCenterWorld;
            WorldToKilometers = Mathf.Max(0.000001f, worldToKilometers);
            GroundAlbedo = ClampCoefficient(groundAlbedo);
            GroundColor = groundColor;
            SkyTint = skyTint;
            SkyLuminanceFactor = ClampCoefficient(skyLuminanceFactor);
            SunIntensity = Mathf.Max(0f, sunIntensity);
            SunDiskSize = Mathf.Clamp(sunDiskSize, BurtAtmosphereUtility.MinimumSunAngularDiameterDegrees, BurtAtmosphereUtility.MaximumSunAngularDiameterDegrees);
            SunDiskIntensity = Mathf.Max(0f, sunDiskIntensity);
            SunHaloSize = Mathf.Max(0.05f, sunHaloSize);
            SunHaloIntensity = Mathf.Max(0f, sunHaloIntensity);
            SunSource = sunSource;
            CustomSunDirection = customSunDirection.sqrMagnitude > 0.0001f ? customSunDirection.normalized : new Vector3(0.3f, 0.8f, 0.4f).normalized;
            MainLightTransmittanceStrength = Mathf.Clamp01(mainLightTransmittanceStrength);
            MainLightOcclusion = Mathf.Clamp01(mainLightOcclusion);
            HorizonColor = horizonColor;
            HorizonSunsetColor = horizonSunsetColor;
            HorizonIntensity = Mathf.Max(0f, horizonIntensity);
            HorizonFalloff = Mathf.Max(0.1f, horizonFalloff);
            HorizonSunsetInfluence = Mathf.Clamp01(horizonSunsetInfluence);
            GroundContribution = Mathf.Max(0f, groundContribution);
            GroundBlendStart = groundBlendStart;
            GroundBlendEnd = groundBlendEnd;
            ExposureCompensation = Mathf.Clamp(exposureCompensation, -8f, 8f);
            TonemapSafeSunIntensity = Mathf.Max(0.1f, tonemapSafeSunIntensity);
            StylizedSky = stylizedSky;
            Moon = moon;
            // The XRender LUT path is controlled only by density, luminance and
            // sampling-distance scale. Intensity and Distance belong to BRP's
            // analytic fallback and must not disable the physical path.
            AerialPerspectiveEnabled = enabled && aerialPerspectiveEnabled && aerialPerspectiveDensityScale > 0.0001f && aerialPerspectiveLuminanceScale > 0.0001f && aerialPerspectiveSamplingDistanceScale > 0.0001f;
            AerialPerspectiveDensityScale = Mathf.Clamp(aerialPerspectiveDensityScale, 0f, 21f);
            AerialPerspectiveLuminanceScale = Mathf.Clamp(aerialPerspectiveLuminanceScale, 0f, 20f);
            AerialPerspectiveSamplingDistanceScale = Mathf.Clamp(aerialPerspectiveSamplingDistanceScale, 0f, 20f);
            AerialPerspectiveIntensity = Mathf.Max(0f, aerialPerspectiveIntensity);
            AerialPerspectiveDistance = Mathf.Max(1f, aerialPerspectiveDistance);
            AerialPerspectiveHeightFalloff = Mathf.Max(0f, aerialPerspectiveHeightFalloff);
            AerialPerspectiveTint = aerialPerspectiveTint;
            AerialPerspectiveNearFadeStart = Mathf.Max(0f, aerialPerspectiveNearFadeStart);
            AerialPerspectiveNearFadeEnd = Mathf.Max(AerialPerspectiveNearFadeStart + 0.001f, aerialPerspectiveNearFadeEnd);
            AerialPerspectiveMaxOpacity = Mathf.Clamp01(aerialPerspectiveMaxOpacity);
            AerialPerspectivePlacement = aerialPerspectivePlacement;
            FogInteraction = fogInteraction;
        }

        public static BurtAtmosphereSettings Disabled => new BurtAtmosphereSettings(false, 0f, 0f, 0f, Color.black, Color.black, Color.black, 0f, Color.black, 25f, 15f, 0f, 1f, 6371f, 80f, 8f, 1.2f, AtmospherePlanetTransformMode.PlanetTopAtAbsoluteWorldOrigin, Vector3.zero, new Vector3(0f, -6371000f, 0f), 0.001f, Color.black, Color.black, Color.white, Color.white, 0f, 0.5f, 1.2f, 1f, 1f, AtmosphereSunSource.MainLight, Vector3.up, 0f, 1f, new Color(0.48f, 0.66f, 0.92f, 1f), new Color(0.95f, 0.82f, 0.58f, 1f), 1f, 0.65f, 0.35f, 0.22f, -0.02f, -0.20f, 0f, 4f, BurtAtmosphereStylizedSkySettings.Disabled, BurtAtmosphereMoonSettings.Disabled, false, 0f, 0f, 0f, 0f, 250f, 0f, new Color(0.70f, 0.82f, 1.0f, 1f), 0f, 50f, 0.65f, AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky, AtmosphereFogInteraction.Additive);

        private static Color ClampCoefficient(Color value)
        {
            return new Color(Mathf.Max(0f, value.r), Mathf.Max(0f, value.g), Mathf.Max(0f, value.b), 1f);
        }
    }

    internal static class BurtAtmosphereUtility
    {
        private const int MainLightTransmittanceSampleCount = 15;
        private const float MainLightTransmittanceReferenceAltitudeKm = 0.5f;

        public const float MinimumSunAngularDiameterDegrees = 0.05f;
        public const float MaximumSunAngularDiameterDegrees = 20f;
        public const string ShaderName = "Hidden/BurtRP/AtmosphereScattering";
        public const string SkyFormulaName = "LutPhysicalXRenderIntegratorDirectSkyUvMoonV17";
        public const string AerialFormulaName = "LutPhysicalCameraFrustum96KmXRenderTransparentTotalFogV23";
        public const string MainLightTransmittanceFormulaName = "XRenderGroundOpticalDepth15V1";
        public const string SunDiskFormulaName = "XRenderSolidAngleLuminanceLimbDarkeningV1";
        public const string MoonDiskFormulaName = "XRenderSolidAngleTexturePhaseEarthshineFlareV1";

        internal static Vector4 EvaluateMoonDiskLuminanceAndGeometry(BurtAtmosphereMoonSettings moon)
        {
            var halfApexRadians = Mathf.Clamp(moon.AngularDiameter, 0.05f, 90f) * 0.5f * Mathf.Deg2Rad;
            var solidAngle = Mathf.Max(2f * Mathf.PI * (1f - Mathf.Cos(halfApexRadians)), 0.0000001f);
            var diskLuminance = moon.Enabled ? moon.Intensity / solidAngle : 0f;
            return new Vector4(
                Mathf.Max(0f, diskLuminance),
                halfApexRadians,
                moon.FlareSize * Mathf.Deg2Rad,
                moon.FlareFalloff);
        }

        internal static void ResolveMoonBasis(
            BurtAtmosphereMoonSettings moon,
            out Vector3 direction,
            out Vector3 up,
            out Vector3 right)
        {
            var rotation = Quaternion.Euler(moon.RotationEuler);
            direction = -(rotation * Vector3.forward);
            up = rotation * Vector3.up;
            right = rotation * Vector3.right;
            direction.Normalize();
            up.Normalize();
            right.Normalize();
        }

        internal static Vector4 EvaluateSunDiskLuminanceAndCosHalfApex(BurtRenderRequest request, BurtAtmosphereSettings settings)
        {
            var angularDiameterDegrees = Mathf.Clamp(
                settings.SunDiskSize,
                MinimumSunAngularDiameterDegrees,
                MaximumSunAngularDiameterDegrees);
            var halfApexRadians = angularDiameterDegrees * 0.5f * Mathf.Deg2Rad;
            var cosHalfApex = Mathf.Cos(halfApexRadians);
            var solidAngle = Mathf.Max(2f * Mathf.PI * (1f - cosHalfApex), 0.0000001f);
            var outerSpaceIlluminance = request != null && request.LightingData != null
                ? request.LightingData.MainLightColorOuterSpace
                : Color.white;
            var luminanceScale = settings.SunIntensity * settings.SunDiskIntensity / solidAngle;
            return new Vector4(
                Mathf.Max(0f, outerSpaceIlluminance.r) * luminanceScale,
                Mathf.Max(0f, outerSpaceIlluminance.g) * luminanceScale,
                Mathf.Max(0f, outerSpaceIlluminance.b) * luminanceScale,
                cosHalfApex);
        }

        public static bool ShouldUseAtmosphere(BurtRenderRequest request)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.UICamera)
            {
                return false;
            }

            if (BurtCameraClearUtility.ResolveClearMode(request) != BurtCameraClearMode.Skybox)
            {
                return false;
            }

            return ResolveSettings().Enabled;
        }

        public static bool ShouldUseAerialPerspective(BurtRenderRequest request)
        {
            // Aerial perspective is a depth-based opaque composite, not a sky clear pass.
            // Keep preview/reflection/UI exclusions, but allow SolidColor cameras just as
            // XRender's atmosphere-fog combine does.
            if (request == null || !request.IsValid || request.Camera == null ||
                request.Type == BurtRenderRequestType.Preview ||
                request.Type == BurtRenderRequestType.Reflection ||
                request.Type == BurtRenderRequestType.UICamera)
            {
                return false;
            }

            var settings = ResolveSettings();
            return settings.Enabled && settings.AerialPerspectiveEnabled && settings.FogInteraction != AtmosphereFogInteraction.FogOnly;
        }

        public static bool ShouldApplyAerialPerspectiveAfterOpaqueBeforeSky(BurtRenderRequest request)
        {
            return ShouldUseAerialPerspective(request) && ResolveSettings().AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky;
        }

        public static bool ShouldApplyAerialPerspectiveAfterSkyBeforeSSR(BurtRenderRequest request)
        {
            return ShouldUseAerialPerspective(request) && ResolveSettings().AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.AfterSkyBeforeSSR;
        }

        public static bool ShouldApplyAerialPerspectiveBeforeTransparent(BurtRenderRequest request)
        {
            return ShouldUseAerialPerspective(request) && ResolveSettings().AerialPerspectivePlacement == AtmosphereAerialPerspectivePlacement.BeforeTransparent;
        }

        public static BurtAtmosphereSettings ResolveSettings()
        {
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            var atmosphere = stack != null ? stack.GetComponent<AtmosphereVolumeComponent>() : null;
            if (atmosphere == null || !atmosphere.IsEnabled())
            {
                return BurtAtmosphereSettings.Disabled;
            }

            var planetRadius = atmosphere.planetRadius.value;
            var worldToKilometers = atmosphere.worldToKilometers.value;
            var planetTransformMode = atmosphere.planetTransformMode.value;
            var planetAnchorWorld = atmosphere.planetAnchorWorld.value;
            var planetCenterWorld = ResolvePlanetCenterWorld(planetTransformMode, planetRadius, worldToKilometers, planetAnchorWorld, atmosphere.planetCenterWorld.value);
            return new BurtAtmosphereSettings(
                true,
                atmosphere.rayleighIntensity.value,
                atmosphere.mieIntensity.value,
                atmosphere.mieAnisotropy.value,
                atmosphere.rayleighScatteringCoefficient.value,
                atmosphere.mieScatteringCoefficient.value,
                atmosphere.mieAbsorptionCoefficient.value,
                atmosphere.ozoneAbsorptionIntensity.value,
                atmosphere.ozoneAbsorptionCoefficient.value,
                atmosphere.ozoneLayerCenter.value,
                atmosphere.ozoneLayerThickness.value,
                atmosphere.multipleScatteringIntensity.value,
                atmosphere.traceSampleCountScale.value,
                planetRadius,
                atmosphere.atmosphereHeight.value,
                atmosphere.rayleighScaleHeight.value,
                atmosphere.mieScaleHeight.value,
                planetTransformMode,
                planetAnchorWorld,
                planetCenterWorld,
                worldToKilometers,
                atmosphere.groundAlbedo.value,
                atmosphere.groundColor.value,
                atmosphere.skyTint.value,
                atmosphere.skyLuminanceFactor.value,
                atmosphere.sunIntensity.value,
                atmosphere.sunDiskSize.value,
                atmosphere.sunDiskIntensity.value,
                atmosphere.sunHaloSize.value,
                atmosphere.sunHaloIntensity.value,
                atmosphere.sunSource.value,
                atmosphere.customSunDirection.value,
                atmosphere.mainLightTransmittanceStrength.value,
                atmosphere.mainLightOcclusion.value,
                atmosphere.horizonColor.value,
                atmosphere.horizonSunsetColor.value,
                atmosphere.horizonIntensity.value,
                atmosphere.horizonFalloff.value,
                atmosphere.horizonSunsetInfluence.value,
                atmosphere.groundContribution.value,
                atmosphere.groundBlendStart.value,
                atmosphere.groundBlendEnd.value,
                atmosphere.exposureCompensation.value,
                atmosphere.tonemapSafeSunIntensity.value,
                new BurtAtmosphereStylizedSkySettings(
                    atmosphere.stylizedSkyBlend.value,
                    atmosphere.stylizedBaseSkyColorDay.value,
                    atmosphere.stylizedBaseSkyColorDawnDusk.value,
                    atmosphere.stylizedBaseSkyColorNight.value,
                    atmosphere.stylizedHorizonSkyColorDay.value,
                    atmosphere.stylizedHorizonSkyColorDawnDusk.value,
                    atmosphere.stylizedHorizonSkyColorNight.value,
                    atmosphere.stylizedHorizonBrightness.value,
                    atmosphere.stylizedHorizonFalloff.value,
                    atmosphere.stylizedSunDiskColorScale.value,
                    atmosphere.stylizedSunGlowColor.value,
                    atmosphere.stylizedSunRiseBlendMin.value,
                    atmosphere.stylizedSunRiseBlendMax.value,
                    atmosphere.stylizedSunGlowScale.value),
                new BurtAtmosphereMoonSettings(
                    atmosphere.moonEnabled.value,
                    atmosphere.moonSurfaceTexture.value,
                    atmosphere.moonRotationEuler.value,
                    atmosphere.moonIntensity.value,
                    atmosphere.moonAngularDiameter.value,
                    atmosphere.moonSurfaceTint.value,
                    atmosphere.moonPhase.value,
                    atmosphere.moonPhaseRotation.value,
                    atmosphere.moonEarthshine.value,
                    atmosphere.moonFlareSize.value,
                    atmosphere.moonFlareFalloff.value,
                    atmosphere.moonFlareTint.value,
                    atmosphere.moonRiseBlendMin.value,
                    atmosphere.moonRiseBlendMax.value),
                atmosphere.aerialPerspective.value,
                atmosphere.aerialPerspectiveDensityScale.value,
                atmosphere.aerialPerspectiveLuminanceScale.value,
                atmosphere.aerialPerspectiveSamplingDistanceScale.value,
                atmosphere.aerialPerspectiveIntensity.value,
                atmosphere.aerialPerspectiveDistance.value,
                atmosphere.aerialPerspectiveHeightFalloff.value,
                atmosphere.aerialPerspectiveTint.value,
                atmosphere.aerialPerspectiveNearFadeStart.value,
                atmosphere.aerialPerspectiveNearFadeEnd.value,
                atmosphere.aerialPerspectiveMaxOpacity.value,
                atmosphere.aerialPerspectivePlacement.value,
                atmosphere.aerialFogInteraction.value);
        }

        internal static Color EvaluateMainLightGroundTransmittance(BurtAtmosphereSettings settings, Vector3 lightDirection)
        {
            var strength = settings.MainLightTransmittanceStrength;
            if (!settings.Enabled || strength <= 0.0001f || lightDirection.sqrMagnitude <= 0.0001f)
            {
                return Color.white;
            }

            var direction = lightDirection.normalized;
            var bottomRadiusKm = Mathf.Max(settings.PlanetRadius, 1f);
            var topRadiusKm = bottomRadiusKm + Mathf.Max(settings.AtmosphereHeight, 0.01f);
            var origin = new Vector3(0f, bottomRadiusKm + MainLightTransmittanceReferenceAltitudeKm, 0f);
            var distanceToTopKm = RaySphereNearest(origin, direction, topRadiusKm);
            if (distanceToTopKm <= 0f)
            {
                return Color.white;
            }

            // XRender integrates all the way through the planet for a light below
            // the local horizon, which numerically converges to zero transmittance.
            // Resolve that equivalent result explicitly to avoid exponential overflow.
            var distanceToGroundKm = RaySphereNearest(origin, direction, bottomRadiusKm);
            if (distanceToGroundKm > 0f && distanceToGroundKm < distanceToTopKm)
            {
                return Color.Lerp(Color.white, Color.black, strength);
            }

            var rayleighCoefficient = new Vector3(
                settings.RayleighScatteringCoefficient.r,
                settings.RayleighScatteringCoefficient.g,
                settings.RayleighScatteringCoefficient.b) * settings.RayleighIntensity;
            var mieScale = settings.MieIntensity / 0.12f;
            var mieExtinctionCoefficient = new Vector3(
                settings.MieScatteringCoefficient.r + settings.MieAbsorptionCoefficient.r,
                settings.MieScatteringCoefficient.g + settings.MieAbsorptionCoefficient.g,
                settings.MieScatteringCoefficient.b + settings.MieAbsorptionCoefficient.b) * mieScale;
            var ozoneCoefficient = new Vector3(
                settings.OzoneAbsorptionCoefficient.r,
                settings.OzoneAbsorptionCoefficient.g,
                settings.OzoneAbsorptionCoefficient.b) * settings.OzoneAbsorptionIntensity;
            var sampleLengthKm = distanceToTopKm / MainLightTransmittanceSampleCount;
            var opticalDepth = Vector3.zero;
            for (var sampleIndex = 0; sampleIndex < MainLightTransmittanceSampleCount; sampleIndex++)
            {
                // Match XRender's CPU reference integration: 15 uniform samples
                // starting at the ground reference point rather than midpoint samples.
                var samplePosition = origin + direction * (distanceToTopKm * sampleIndex / MainLightTransmittanceSampleCount);
                var altitudeKm = Mathf.Max(samplePosition.magnitude - bottomRadiusKm, 0f);
                var rayleighDensity = Mathf.Exp(-altitudeKm / Mathf.Max(settings.RayleighScaleHeight, 0.01f));
                var mieDensity = Mathf.Exp(-altitudeKm / Mathf.Max(settings.MieScaleHeight, 0.01f));
                var ozoneHalfWidthKm = Mathf.Max(settings.OzoneLayerThickness, 0.1f);
                var ozoneDensity = Mathf.Clamp01(1f - Mathf.Abs(altitudeKm - settings.OzoneLayerCenter) / ozoneHalfWidthKm);
                opticalDepth += (rayleighCoefficient * rayleighDensity
                    + mieExtinctionCoefficient * mieDensity
                    + ozoneCoefficient * ozoneDensity) * sampleLengthKm;
            }

            var physicalTransmittance = new Color(
                Mathf.Exp(-Mathf.Min(opticalDepth.x, 80f)),
                Mathf.Exp(-Mathf.Min(opticalDepth.y, 80f)),
                Mathf.Exp(-Mathf.Min(opticalDepth.z, 80f)),
                1f);
            return Color.Lerp(Color.white, physicalTransmittance, strength);
        }

        private static float RaySphereNearest(Vector3 origin, Vector3 direction, float radius)
        {
            var b = Vector3.Dot(origin, direction);
            var c = Vector3.Dot(origin, origin) - radius * radius;
            var discriminant = b * b - c;
            if (discriminant < 0f)
            {
                return -1f;
            }

            var root = Mathf.Sqrt(discriminant);
            var nearDistance = -b - root;
            var farDistance = -b + root;
            return nearDistance > 0f ? nearDistance : (farDistance > 0f ? farDistance : -1f);
        }

        private static Vector3 ResolvePlanetCenterWorld(AtmospherePlanetTransformMode transformMode, float planetRadiusKm, float worldToKilometers, Vector3 anchorWorld, Vector3 explicitCenterWorld)
        {
            var radiusInWorldUnits = Mathf.Max(100f, planetRadiusKm) / Mathf.Max(0.000001f, worldToKilometers);
            switch (transformMode)
            {
                case AtmospherePlanetTransformMode.PlanetTopAtAbsoluteWorldOrigin:
                    return new Vector3(0f, -radiusInWorldUnits, 0f);
                case AtmospherePlanetTransformMode.PlanetTopAtAnchorWorld:
                    return anchorWorld + Vector3.down * radiusInWorldUnits;
                case AtmospherePlanetTransformMode.PlanetCenterAtAnchorWorld:
                    return anchorWorld;
                default:
                    return explicitCenterWorld;
            }
        }

        public static string FormatDebugState()
        {
            var settings = ResolveSettings();
            return string.Concat(
                "Enabled=", settings.Enabled,
                " Rayleigh=", Format(settings.RayleighIntensity),
                " Mie=", Format(settings.MieIntensity),
                " g=", Format(settings.MieAnisotropy),
                " RayleighCoeff=", FormatColor(settings.RayleighScatteringCoefficient),
                " MieScatterCoeff=", FormatColor(settings.MieScatteringCoefficient),
                " MieAbsorbCoeff=", FormatColor(settings.MieAbsorptionCoefficient),
                " RadiusKm=", Format(settings.PlanetRadius),
                " HeightKm=", Format(settings.AtmosphereHeight),
                " RayleighScaleKm=", Format(settings.RayleighScaleHeight),
                " MieScaleKm=", Format(settings.MieScaleHeight),
                " PlanetTransform=", settings.PlanetTransformMode,
                " PlanetAnchorWS=", FormatVector(settings.PlanetAnchorWorld),
                " PlanetCenterWS=", FormatVector(settings.PlanetCenterWorld),
                " WorldToKm=", Format(settings.WorldToKilometers),
                " GroundAlbedo=", FormatColor(settings.GroundAlbedo),
                " Ozone=", Format(settings.OzoneAbsorptionIntensity),
                " OzoneCoeff=", FormatColor(settings.OzoneAbsorptionCoefficient),
                " OzoneLayerKm=", Format(settings.OzoneLayerCenter), "/", Format(settings.OzoneLayerThickness),
                " MultiScatter=", Format(settings.MultipleScatteringIntensity),
                " TraceSamples=", Format(settings.TraceSampleCountScale),
                " Sun=", Format(settings.SunIntensity),
                " SunAngularDiameterDeg=", Format(settings.SunDiskSize),
                " SunDiskIntensity=", Format(settings.SunDiskIntensity),
                " SunDiskFormula=", SunDiskFormulaName,
                " SunHaloSize=", Format(settings.SunHaloSize),
                " SunHaloIntensity=", Format(settings.SunHaloIntensity),
                " SunSource=", settings.SunSource,
                " MainLightTransmittance=", Format(settings.MainLightTransmittanceStrength), "@", MainLightTransmittanceFormulaName,
                " MainLightOcclusion=", Format(settings.MainLightOcclusion),
                " Horizon=", Format(settings.HorizonIntensity),
                " HorizonFalloff=", Format(settings.HorizonFalloff),
                " HorizonSunsetInfluence=", Format(settings.HorizonSunsetInfluence),
                " Ground=", Format(settings.GroundContribution),
                " GroundBlend=", Format(settings.GroundBlendStart), "/", Format(settings.GroundBlendEnd),
                " ExposureEV=", Format(settings.ExposureCompensation),
                " SunClamp=", Format(settings.TonemapSafeSunIntensity),
                " SkyTint=", FormatColor(settings.SkyTint),
                " SkyLuminanceFactor=", FormatColor(settings.SkyLuminanceFactor),
                " HorizonColor=", FormatColor(settings.HorizonColor),
                " HorizonSunsetColor=", FormatColor(settings.HorizonSunsetColor),
                " GroundColor=", FormatColor(settings.GroundColor),
                " StylizedBlend=", Format(settings.StylizedSky.Blend),
                " StylizedBaseDay=", FormatColor(settings.StylizedSky.BaseSkyColorDay),
                " StylizedBaseDawnDusk=", FormatColor(settings.StylizedSky.BaseSkyColorDawnDusk),
                " StylizedBaseNight=", FormatColor(settings.StylizedSky.BaseSkyColorNight),
                " StylizedHorizonDay=", FormatColor(settings.StylizedSky.HorizonSkyColorDay),
                " StylizedHorizonDawnDusk=", FormatColor(settings.StylizedSky.HorizonSkyColorDawnDusk),
                " StylizedHorizonNight=", FormatColor(settings.StylizedSky.HorizonSkyColorNight),
                " StylizedHorizon=", Format(settings.StylizedSky.HorizonBrightness), "/", Format(settings.StylizedSky.HorizonFalloff),
                " StylizedSunRise=", Format(settings.StylizedSky.SunRiseBlendMin), "/", Format(settings.StylizedSky.SunRiseBlendMax),
                " StylizedSunGlow=", FormatColor(settings.StylizedSky.SunGlowColor), "x", Format(settings.StylizedSky.SunGlowScale),
                " Moon=", settings.Moon.Enabled,
                " MoonTexture=", settings.Moon.SurfaceTexture != null ? settings.Moon.SurfaceTexture.name : "WhiteFallback",
                " MoonRotation=", FormatVector(settings.Moon.RotationEuler),
                " MoonIlluminance=", Format(settings.Moon.Intensity),
                " MoonAngularDiameterDeg=", Format(settings.Moon.AngularDiameter),
                " MoonPhase=", Format(settings.Moon.Phase), "@", Format(settings.Moon.PhaseRotation),
                " MoonEarthshine=", Format(settings.Moon.Earthshine),
                " MoonFlare=", Format(settings.Moon.FlareSize), "/", Format(settings.Moon.FlareFalloff),
                " MoonRise=", Format(settings.Moon.RiseBlendMin), "/", Format(settings.Moon.RiseBlendMax),
                " MoonFormula=", MoonDiskFormulaName,
                " Aerial=", settings.AerialPerspectiveEnabled,
                " AerialDensityScale=", Format(settings.AerialPerspectiveDensityScale),
                " AerialLuminanceScale=", Format(settings.AerialPerspectiveLuminanceScale),
                " AerialSamplingDistanceScale=", Format(settings.AerialPerspectiveSamplingDistanceScale),
                " AerialFallbackIntensity=", Format(settings.AerialPerspectiveIntensity),
                " AerialFallbackDistance=", Format(settings.AerialPerspectiveDistance),
                " AerialLutCoverageKm=", Format(96f),
                " AerialHeightFalloff=", Format(settings.AerialPerspectiveHeightFalloff),
                " AerialNearFade=", Format(settings.AerialPerspectiveNearFadeStart), "/", Format(settings.AerialPerspectiveNearFadeEnd),
                " AerialMaxOpacity=", Format(settings.AerialPerspectiveMaxOpacity),
                " AerialPlacement=", settings.AerialPerspectivePlacement,
                " FogInteraction=", settings.FogInteraction,
                " AerialTint=", FormatColor(settings.AerialPerspectiveTint),
                " Formula=", SkyFormulaName);
        }

        public static string FormatRequestGate(BurtRenderRequest request)
        {
            if (request == null)
            {
                return "Request=null";
            }

            var clearMode = request.IsValid ? BurtCameraClearUtility.ResolveClearMode(request).ToString() : "Invalid";
            return string.Concat(
                "RequestType=", request.Type,
                " ClearMode=", clearMode,
                " SkyAllowed=", ShouldUseAtmosphere(request),
                " AerialAllowed=", ShouldUseAerialPerspective(request));
        }

        public static string FormatAerialPassState(BurtRenderRequest request)
        {
            var settings = ResolveSettings();
            var requested = ShouldUseAerialPerspective(request);
            return string.Concat(
                "Requested=", requested,
                " UsesSourceCopy=", requested,
                " SourceCopy=TemporaryCameraColor",
                " AffectsSkyPixelsFromSpace=", settings.AerialPerspectivePlacement != AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky,
                " SkipsSkyPixelsInsideAtmosphere=True",
                " TransparentContract=Alpha+Premultiply+Additive",
                " Placement=", settings.AerialPerspectivePlacement,
                " FogInteraction=", settings.FogInteraction,
                " Formula=", AerialFormulaName,
                " DensityScale=", Format(settings.AerialPerspectiveDensityScale),
                " LuminanceScale=", Format(settings.AerialPerspectiveLuminanceScale),
                " SamplingDistanceScale=", Format(settings.AerialPerspectiveSamplingDistanceScale),
                " FallbackIntensity=", Format(settings.AerialPerspectiveIntensity),
                " FallbackDistance=", Format(settings.AerialPerspectiveDistance),
                " LutCoverageKm=", Format(96f),
                " HeightFalloff=", Format(settings.AerialPerspectiveHeightFalloff),
                " NearFade=", Format(settings.AerialPerspectiveNearFadeStart), "/", Format(settings.AerialPerspectiveNearFadeEnd),
                " MaxOpacity=", Format(settings.AerialPerspectiveMaxOpacity),
                " Tint=", FormatColor(settings.AerialPerspectiveTint));
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Concat("(", Format(value.x), ",", Format(value.y), ",", Format(value.z), ")");
        }

        private static string FormatColor(Color color)
        {
            return string.Concat(
                "(",
                Format(color.r),
                ",",
                Format(color.g),
                ",",
                Format(color.b),
                ",",
                Format(color.a),
                ")");
        }
    }

    internal static class BurtAtmosphereReflectionUtility
    {
        private const int CubemapSize = 256;
        private const int CubemapFaceCount = 6;
        private const int AtmosphereCubemapPass = 2;

        private static readonly int FaceId = Shader.PropertyToID("_BurtAtmosphereCubemapFace");
        private static readonly Vector4 DefaultHDRDecodeValues = new Vector4(1f, 1f, 0f, 0f);

        private static RenderTexture atmosphereCubemap;
        private static Material material;
        private static bool hasLoggedMissingShader;
        private static int lastRenderedFrame = -1;
        private static int lastRenderedCameraId;
        private static int contentVersion;

        public static bool TryGetReflection(CommandBuffer cmd, BurtRenderRequest request, out Texture texture, out Vector4 hdrDecodeValues, out string source)
        {
            texture = null;
            hdrDecodeValues = DefaultHDRDecodeValues;
            source = "BurtAtmosphereReflectionUnavailable";

            if (cmd == null || request == null || !BurtAtmosphereUtility.ShouldUseAtmosphere(request))
            {
                return false;
            }

            var camera = request.Camera;
            if (camera == null)
            {
                return false;
            }

            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (!settings.Enabled)
            {
                return false;
            }

            var drawMaterial = BurtDrawAtmospherePass.CreateMaterial(ref material, ref hasLoggedMissingShader);
            if (drawMaterial == null)
            {
                source = "BurtAtmosphereReflectionMissingShader";
                return false;
            }

            EnsureTexture();
            if (atmosphereCubemap == null)
            {
                source = "BurtAtmosphereReflectionAllocationFailed";
                return false;
            }

            var currentFrame = Time.frameCount;
            var cameraId = camera.GetInstanceID();
            if (lastRenderedFrame != currentFrame || lastRenderedCameraId != cameraId)
            {
                var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
                BurtAtmosphereLutUtility.EnsureLuts(cmd, camera, settings, new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
                BurtDrawAtmospherePass.UploadMaterialProperties(drawMaterial, camera, request, settings);
                for (var face = 0; face < CubemapFaceCount; face++)
                {
                    cmd.SetRenderTarget(new RenderTargetIdentifier(atmosphereCubemap, 0, (CubemapFace)face));
                    cmd.SetViewport(new Rect(0f, 0f, CubemapSize, CubemapSize));
                    cmd.SetGlobalFloat(FaceId, face);
                    cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, AtmosphereCubemapPass, MeshTopology.Triangles, 3, 1);
                }

                lastRenderedFrame = currentFrame;
                lastRenderedCameraId = cameraId;
                unchecked
                {
                    contentVersion++;
                    if (contentVersion == 0)
                    {
                        contentVersion = 1;
                    }
                }
            }

            texture = atmosphereCubemap;
            source = "BurtAtmosphereCubemap";
            return true;
        }

        // Lets IBL distinguish a newly captured camera-relative atmosphere cubemap from
        // another request for the same texture during the same frame.
        public static bool TryGetContentVersion(Texture texture, out int version)
        {
            version = 0;
            if (texture == null || texture != atmosphereCubemap || contentVersion == 0)
            {
                return false;
            }

            version = contentVersion;
            return true;
        }

        public static bool HasReadyReflection(BurtRenderRequest request, out Texture texture, out Vector4 hdrDecodeValues, out string source)
        {
            hdrDecodeValues = DefaultHDRDecodeValues;
            source = "BurtAtmosphereReflectionUnavailable";
            texture = BurtAtmosphereUtility.ShouldUseAtmosphere(request) ? atmosphereCubemap : null;
            if (texture == null)
            {
                return false;
            }

            source = "BurtAtmosphereCubemap";
            return true;
        }

        public static string FormatDebugState()
        {
            return string.Concat(
                "Cubemap=", atmosphereCubemap != null && atmosphereCubemap.IsCreated() ? "Ready" : "Unavailable",
                " Size=", CubemapSize,
                " LastRenderedFrame=", lastRenderedFrame,
                " LastCamera=", lastRenderedCameraId,
                " ContentVersion=", contentVersion,
                " CaptureFormula=SkyViewMoonNoDirectSun",
                " Material=", material != null ? "Ready" : "Unavailable");
        }

        public static void Release()
        {
            ReleaseTexture(atmosphereCubemap);
            atmosphereCubemap = null;
            lastRenderedFrame = -1;
            lastRenderedCameraId = 0;
            contentVersion = 0;

            if (material != null)
            {
                DestroyUnityObject(material);
                material = null;
            }

            hasLoggedMissingShader = false;
        }

        private static void EnsureTexture()
        {
            if (atmosphereCubemap != null && atmosphereCubemap.IsCreated())
            {
                return;
            }

            ReleaseTexture(atmosphereCubemap);
            atmosphereCubemap = new RenderTexture(CubemapSize, CubemapSize, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = "Burt Atmosphere Reflection Cubemap",
                dimension = TextureDimension.Cube,
                volumeDepth = CubemapFaceCount,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            atmosphereCubemap.Create();
            lastRenderedFrame = -1;
            lastRenderedCameraId = 0;
            contentVersion = 0;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            DestroyUnityObject(texture);
        }

        private static void DestroyUnityObject(Object unityObject)
        {
            if (unityObject == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(unityObject);
            }
            else
            {
                Object.DestroyImmediate(unityObject);
            }
        }
    }
}
