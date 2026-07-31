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
        public readonly Color EffectiveRayleighScatteringCoefficient;
        public readonly Color EffectiveMieScatteringCoefficient;
        public readonly Color EffectiveMieAbsorptionCoefficient;
        public readonly Color EffectiveMieExtinctionCoefficient;
        public readonly Color EffectiveOzoneAbsorptionCoefficient;
        public readonly float EffectiveRayleighDensityExpScale;
        public readonly float EffectiveMieDensityExpScale;
        public readonly Vector4 EffectiveOzoneDensityProfile;
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
        public readonly float PhysicalSkyTimeOfDayCurve;
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
        public readonly Texture2D MoonPhaseNormalTexture;
        public readonly Vector3 MoonRotationEuler;
        public readonly float MoonIntensity;
        public readonly float MoonAngularDiameter;
        public readonly Color MoonSurfaceTint;
        public readonly Color MoonAdditionalTint;
        public readonly float MoonPhase;
        public readonly float MoonPhaseRotation;
        public readonly float MoonEarthshine;
        public readonly float MoonPhaseSharpness;
        public readonly float MoonFlareSize;
        public readonly float MoonFlareFalloff;
        public readonly Color MoonFlareTint;
        public readonly float MoonLightBloomIntensity;
        public readonly float MoonLightBloomSize;
        public readonly float MoonLightBloomFalloff;
        public readonly float MoonLightBloomEdgeAlpha;
        public readonly float MoonRiseBlendMin;
        public readonly float MoonRiseBlendMax;
        public readonly bool StarsEnabled;
        public readonly Texture2D StarsTexture;
        public readonly Texture2D StarsTintColorTexture;
        public readonly Texture2D AreaStarsTexture;
        public readonly Texture2D GalaxyCloudTexture;
        public readonly float StarsIntensity;
        public readonly float StarsRotation;
        public readonly float StarsTwinkleStrength;
        public readonly float AreaStarsIntensity;
        public readonly float GalaxyCloudIntensity;
        public readonly float GalaxyStarIntensity;
        public readonly Texture2D CustomStarTexture;
        public readonly float CustomStarRotation;
        public readonly float CustomStarScatterSpeed;
        public readonly float CustomStarScatterInterval;
        public readonly bool PanoramicCloudEnabled;
        public readonly Texture2D PanoramicCloudDefaultTexture;
        public readonly Texture2D PanoramicCloudPreviousTexture;
        public readonly Texture2D PanoramicCloudCurrentTexture;
        public readonly bool PanoramicCloudTextureInTransition;
        public readonly float PanoramicCloudTextureTransition;
        public readonly float PanoramicCloudSunnyLuminance;
        public readonly float PanoramicCloudNightLuminance;
        public readonly float PanoramicCloudAlpha;
        public readonly bool PhysicalSkyDesaturationForceEnabled;
        public readonly Color PhysicalSkyDesaturationColor;
        public readonly float PhysicalSkyDesaturationEffect;
        public readonly float PhysicalSkyDesaturationBlend;
        public readonly float PhysicalSkyDesaturationIntensity;
        public readonly float PhysicalSkyCloudDesaturationIntensity;
        public readonly bool WeatherSkyCoverageEnabled;
        public readonly Texture2D WeatherSkyCoverageTexture;
        public readonly float WeatherRainIntensity;
        public readonly float WeatherRainWetCoverage;
        public readonly float WeatherSnowIntensity;
        public readonly float WeatherSnowCoverage;
        public readonly bool AerialPerspectiveEnabled;
        public readonly float AerialPerspectiveDensityScale;
        public readonly float AerialPerspectiveLuminanceScale;
        public readonly float AerialPerspectiveSamplingDistanceScale;
        public readonly float AerialPerspectiveIntensity;
        public readonly float AerialPerspectiveDistance;
        public readonly float AerialPerspectiveHeightFalloff;
        public readonly Color AerialPerspectiveTint;
        public readonly float AerialPerspectiveStartDepth;
        public readonly float AerialPerspectiveNearFadeEnd;
        public readonly float AerialPerspectiveMaxOpacity;
        public readonly AtmosphereAerialPerspectivePlacement AerialPerspectivePlacement;
        public readonly AtmosphereFogInteraction FogInteraction;
        public readonly string SkyFormula;
        public readonly string SkyCaptureFormula;
        public readonly string AerialFormula;

        public float AerialPerspectiveNearFadeStart => AerialPerspectiveStartDepth;

        internal BurtAtmosphereDebugSnapshot(
            BurtAtmosphereSettings settings,
            string skyFormula,
            string skyCaptureFormula,
            string aerialFormula)
        {
            var effectiveCoefficients = BurtAtmosphereUtility.ResolveEffectiveCoefficients(settings);
            var effectiveDensityProfile = BurtAtmosphereUtility.ResolveDensityProfile(settings);
            Enabled = settings.Enabled;
            RayleighIntensity = settings.RayleighIntensity;
            MieIntensity = settings.MieIntensity;
            MieAnisotropy = settings.MieAnisotropy;
            RayleighScatteringCoefficient = settings.RayleighScatteringCoefficient;
            MieScatteringCoefficient = settings.MieScatteringCoefficient;
            MieAbsorptionCoefficient = settings.MieAbsorptionCoefficient;
            OzoneAbsorptionIntensity = settings.OzoneAbsorptionIntensity;
            OzoneAbsorptionCoefficient = settings.OzoneAbsorptionCoefficient;
            EffectiveRayleighScatteringCoefficient = effectiveCoefficients.RayleighScattering;
            EffectiveMieScatteringCoefficient = effectiveCoefficients.MieScattering;
            EffectiveMieAbsorptionCoefficient = effectiveCoefficients.MieAbsorption;
            EffectiveMieExtinctionCoefficient = effectiveCoefficients.MieExtinction;
            EffectiveOzoneAbsorptionCoefficient = effectiveCoefficients.OzoneAbsorption;
            EffectiveRayleighDensityExpScale = effectiveDensityProfile.RayleighDensityExpScale;
            EffectiveMieDensityExpScale = effectiveDensityProfile.MieDensityExpScale;
            EffectiveOzoneDensityProfile = new Vector4(
                effectiveDensityProfile.OzoneDensity0LinearTerm,
                effectiveDensityProfile.OzoneDensity0ConstantTerm,
                effectiveDensityProfile.OzoneDensity1LinearTerm,
                effectiveDensityProfile.OzoneDensity1ConstantTerm);
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
            PhysicalSkyTimeOfDayCurve = settings.PhysicalSkyTimeOfDayCurve;
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
            MoonPhaseNormalTexture = settings.Moon.PhaseNormalTexture;
            MoonRotationEuler = settings.Moon.RotationEuler;
            MoonIntensity = settings.Moon.Intensity;
            MoonAngularDiameter = settings.Moon.AngularDiameter;
            MoonSurfaceTint = settings.Moon.SurfaceTint;
            MoonAdditionalTint = settings.Moon.AdditionalTint;
            MoonPhase = settings.Moon.Phase;
            MoonPhaseRotation = settings.Moon.PhaseRotation;
            MoonEarthshine = settings.Moon.Earthshine;
            MoonPhaseSharpness = settings.Moon.PhaseSharpness;
            MoonFlareSize = settings.Moon.FlareSize;
            MoonFlareFalloff = settings.Moon.FlareFalloff;
            MoonFlareTint = settings.Moon.FlareTint;
            MoonLightBloomIntensity = settings.Moon.LightBloomIntensity;
            MoonLightBloomSize = settings.Moon.LightBloomSize;
            MoonLightBloomFalloff = settings.Moon.LightBloomFalloff;
            MoonLightBloomEdgeAlpha = settings.Moon.LightBloomEdgeAlpha;
            MoonRiseBlendMin = settings.Moon.RiseBlendMin;
            MoonRiseBlendMax = settings.Moon.RiseBlendMax;
            StarsEnabled = settings.Stars.Enabled;
            StarsTexture = settings.Stars.StarsTexture;
            StarsTintColorTexture = settings.Stars.TintColorTexture;
            AreaStarsTexture = settings.Stars.AreaTexture;
            GalaxyCloudTexture = settings.Stars.GalaxyCloudTexture;
            StarsIntensity = settings.Stars.Intensity;
            StarsRotation = settings.Stars.Rotation;
            StarsTwinkleStrength = settings.Stars.TwinkleStrength;
            AreaStarsIntensity = settings.Stars.AreaIntensity;
            GalaxyCloudIntensity = settings.Stars.GalaxyCloudIntensity;
            GalaxyStarIntensity = settings.Stars.GalaxyStarIntensity;
            CustomStarTexture = settings.Stars.CustomStarTexture;
            CustomStarRotation = settings.Stars.CustomStarRotation;
            CustomStarScatterSpeed = settings.Stars.CustomStarScatterSpeed;
            CustomStarScatterInterval = settings.Stars.CustomStarScatterInterval;
            PanoramicCloudEnabled = settings.PanoramicClouds.Enabled;
            PanoramicCloudDefaultTexture = settings.PanoramicClouds.DefaultTexture;
            PanoramicCloudPreviousTexture = settings.PanoramicClouds.PreviousWeatherTexture;
            PanoramicCloudCurrentTexture = settings.PanoramicClouds.CurrentWeatherTexture;
            PanoramicCloudTextureInTransition = settings.PanoramicClouds.TextureInTransition;
            PanoramicCloudTextureTransition = settings.PanoramicClouds.TextureTransition;
            PanoramicCloudSunnyLuminance = settings.PanoramicClouds.SunnyLuminance;
            PanoramicCloudNightLuminance = settings.PanoramicClouds.NightLuminance;
            PanoramicCloudAlpha = settings.PanoramicClouds.Alpha;
            PhysicalSkyDesaturationForceEnabled = settings.PhysicalSkyDesaturation.ForceEnabled;
            PhysicalSkyDesaturationColor = settings.PhysicalSkyDesaturation.Color;
            PhysicalSkyDesaturationEffect = settings.PhysicalSkyDesaturation.Effect;
            PhysicalSkyDesaturationBlend = settings.PhysicalSkyDesaturation.Blend;
            PhysicalSkyDesaturationIntensity = settings.PhysicalSkyDesaturation.SkyIntensity;
            PhysicalSkyCloudDesaturationIntensity = settings.PhysicalSkyDesaturation.CloudIntensity;
            WeatherSkyCoverageEnabled = settings.Weather.Enabled;
            WeatherSkyCoverageTexture = settings.Weather.CoverageTexture;
            WeatherRainIntensity = settings.Weather.RainIntensity;
            WeatherRainWetCoverage = settings.Weather.RainWetCoverage;
            WeatherSnowIntensity = settings.Weather.SnowIntensity;
            WeatherSnowCoverage = settings.Weather.SnowCoverage;
            AerialPerspectiveEnabled = settings.AerialPerspectiveEnabled;
            AerialPerspectiveDensityScale = settings.AerialPerspectiveDensityScale;
            AerialPerspectiveLuminanceScale = settings.AerialPerspectiveLuminanceScale;
            AerialPerspectiveSamplingDistanceScale = settings.AerialPerspectiveSamplingDistanceScale;
            AerialPerspectiveIntensity = settings.AerialPerspectiveIntensity;
            AerialPerspectiveDistance = settings.AerialPerspectiveDistance;
            AerialPerspectiveHeightFalloff = settings.AerialPerspectiveHeightFalloff;
            AerialPerspectiveTint = settings.AerialPerspectiveTint;
            AerialPerspectiveStartDepth = settings.AerialPerspectiveStartDepth;
            AerialPerspectiveNearFadeEnd = settings.AerialPerspectiveNearFadeEnd;
            AerialPerspectiveMaxOpacity = settings.AerialPerspectiveMaxOpacity;
            AerialPerspectivePlacement = settings.AerialPerspectivePlacement;
            FogInteraction = settings.FogInteraction;
            SkyFormula = string.IsNullOrEmpty(skyFormula) ? BurtAtmosphereUtility.SkyFormulaName : skyFormula;
            SkyCaptureFormula = string.IsNullOrEmpty(skyCaptureFormula)
                ? BurtAtmosphereUtility.SkyCaptureFormulaName
                : skyCaptureFormula;
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
                BurtAtmosphereUtility.SkyCaptureFormulaName,
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
            HorizonFalloff = Mathf.Clamp(horizonFalloff, 0f, 100f);
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
        public readonly Texture2D PhaseNormalTexture;
        public readonly Vector3 RotationEuler;
        public readonly float Intensity;
        public readonly float AngularDiameter;
        public readonly Color SurfaceTint;
        public readonly Color AdditionalTint;
        public readonly float Phase;
        public readonly float PhaseRotation;
        public readonly float Earthshine;
        public readonly float PhaseSharpness;
        public readonly float FlareSize;
        public readonly float FlareFalloff;
        public readonly Color FlareTint;
        public readonly float LightBloomIntensity;
        public readonly float LightBloomSize;
        public readonly float LightBloomFalloff;
        public readonly float LightBloomEdgeAlpha;
        public readonly float RiseBlendMin;
        public readonly float RiseBlendMax;

        public BurtAtmosphereMoonSettings(
            bool enabled,
            Texture surfaceTexture,
            Texture phaseNormalTexture,
            Vector3 rotationEuler,
            float intensity,
            float angularDiameter,
            Color surfaceTint,
            Color additionalTint,
            float phase,
            float phaseRotation,
            float earthshine,
            float phaseSharpness,
            float flareSize,
            float flareFalloff,
            Color flareTint,
            float lightBloomIntensity,
            float lightBloomSize,
            float lightBloomFalloff,
            float lightBloomEdgeAlpha,
            float riseBlendMin,
            float riseBlendMax)
        {
            SurfaceTexture = surfaceTexture as Texture2D;
            PhaseNormalTexture = phaseNormalTexture as Texture2D;
            RotationEuler = rotationEuler;
            Intensity = Mathf.Clamp(intensity, 0f, 130000f);
            AngularDiameter = Mathf.Clamp(angularDiameter, 0.1f, 20f);
            SurfaceTint = ClampHdrColor(surfaceTint);
            AdditionalTint = ClampHdrColor(additionalTint);
            Phase = Mathf.Clamp01(phase);
            PhaseRotation = Mathf.Repeat(phaseRotation, 360f);
            Earthshine = Mathf.Clamp(earthshine, 0f, 0.5f);
            PhaseSharpness = Mathf.Clamp(phaseSharpness, 0f, 10f);
            FlareSize = Mathf.Clamp(flareSize, 0f, 5f);
            FlareFalloff = Mathf.Clamp(flareFalloff, 1f, 100f);
            FlareTint = ClampHdrColor(flareTint);
            LightBloomIntensity = Mathf.Clamp(lightBloomIntensity, 0f, 5f);
            LightBloomSize = Mathf.Clamp(lightBloomSize, 0f, 5f);
            LightBloomFalloff = Mathf.Clamp(lightBloomFalloff, 1f, 100f);
            LightBloomEdgeAlpha = Mathf.Clamp(lightBloomEdgeAlpha, 0f, 100f);
            RiseBlendMin = Mathf.Clamp(riseBlendMin, -1f, 0.999f);
            RiseBlendMax = Mathf.Clamp(riseBlendMax, RiseBlendMin + 0.001f, 1f);
            Enabled = enabled && Intensity > 0.0001f;
        }

        public static BurtAtmosphereMoonSettings Disabled => new BurtAtmosphereMoonSettings(
            false,
            null,
            null,
            Vector3.zero,
            0f,
            2f,
            new Color(0.06f, 0.06f, 0.14f, 1f),
            Color.white,
            0.6f,
            300f,
            0.01f,
            5f,
            0f,
            50f,
            Color.white,
            0f,
            0f,
            3f,
            20f,
            0.1f,
            0.75f);

        private static Color ClampHdrColor(Color value)
        {
            return new Color(Mathf.Max(0f, value.r), Mathf.Max(0f, value.g), Mathf.Max(0f, value.b), 1f);
        }
    }

    internal readonly struct BurtAtmosphereStarSettings
    {
        public readonly bool Enabled;
        public readonly Texture2D StarsTexture;
        public readonly Texture2D TintColorTexture;
        public readonly float Intensity;
        public readonly float Rotation;
        public readonly Color TintColor;
        public readonly float TintColorSaturation;
        public readonly Vector2 TintColorTextureTiling;
        public readonly Vector2 TintColorTextureOffset;
        public readonly Vector3 LayerHeights;
        public readonly float LayerSpeed;
        public readonly float TwinkleStrength;
        public readonly float TwinkleSpeed;
        public readonly Vector3 LayerFalloffs;
        public readonly float HorizonFalloff;
        public readonly Texture2D AreaTexture;
        public readonly float AreaIntensity;
        public readonly Vector2 AreaDensityMinMax;
        public readonly Vector2 AreaMaskTiling;
        public readonly Vector2 AreaMaskOffset;
        public readonly float AreaSpeed;
        public readonly float AreaFalloff;
        public readonly float AreaMaskFalloff;
        public readonly Texture2D GalaxyCloudTexture;
        public readonly Vector2 GalaxyCloudTiling;
        public readonly Vector2 GalaxyCloudOffset;
        public readonly float GalaxyCloudRotation;
        public readonly float GalaxyCloudIntensity;
        public readonly float GalaxyCloudFalloff;
        public readonly float GalaxyStarIntensity;
        public readonly float GalaxyStarFalloff;
        public readonly float GalaxyStarHeight;
        public readonly float GalaxyStarSpeed;
        public readonly Texture2D CustomStarTexture;
        public readonly Vector2 CustomStarTextureScale;
        public readonly Vector2 CustomStarTextureOffset;
        public readonly float CustomStarRotation;
        public readonly float CustomStarScaleMin;
        public readonly Vector4 CustomStarIntensityMax;
        public readonly Vector4 CustomStarIntensityMin;
        public readonly float CustomStarScatterSpeed;
        public readonly float CustomStarScatterInterval;

        public BurtAtmosphereStarSettings(
            bool enabled,
            Texture starsTexture,
            Texture tintColorTexture,
            float intensity,
            float rotation,
            Color tintColor,
            float tintColorSaturation,
            Vector2 tintColorTextureTiling,
            Vector2 tintColorTextureOffset,
            float layer1Height,
            float layer2Height,
            float layer3Height,
            float layerSpeed,
            float twinkleStrength,
            float twinkleSpeed,
            float layer1Falloff,
            float layer2Falloff,
            float layer3Falloff,
            float horizonFalloff,
            Texture areaTexture,
            float areaIntensity,
            Vector2 areaDensityMinMax,
            Vector2 areaMaskTiling,
            Vector2 areaMaskOffset,
            float areaSpeed,
            float areaFalloff,
            float areaMaskFalloff,
            Texture galaxyCloudTexture,
            Vector2 galaxyCloudTiling,
            Vector2 galaxyCloudOffset,
            float galaxyCloudRotation,
            float galaxyCloudIntensity,
            float galaxyCloudFalloff,
            float galaxyStarIntensity,
            float galaxyStarFalloff,
            float galaxyStarHeight,
            float galaxyStarSpeed,
            Texture customStarTexture,
            Vector2 customStarTextureScale,
            Vector2 customStarTextureOffset,
            float customStarRotation,
            float customStarScaleMin,
            Vector4 customStarIntensityMax,
            Vector4 customStarIntensityMin,
            float customStarScatterSpeed,
            float customStarScatterInterval)
        {
            StarsTexture = starsTexture as Texture2D;
            TintColorTexture = tintColorTexture as Texture2D;
            Intensity = Mathf.Clamp(intensity, 0f, 3f);
            // XRender uploads this authored 0..360 value directly into cos/sin.
            // Preserve the effective radians contract instead of silently fixing it.
            Rotation = Mathf.Clamp(rotation, 0f, 360f);
            TintColor = ClampHdrColor(tintColor);
            TintColorSaturation = Mathf.Clamp01(tintColorSaturation);
            TintColorTextureTiling = tintColorTextureTiling;
            TintColorTextureOffset = tintColorTextureOffset;
            LayerHeights = new Vector3(
                Mathf.Clamp(layer1Height, 1f, 20f),
                Mathf.Clamp(layer2Height, 1f, 20f),
                Mathf.Clamp(layer3Height, 1f, 20f));
            LayerSpeed = Mathf.Clamp01(layerSpeed);
            TwinkleStrength = Mathf.Clamp01(twinkleStrength);
            TwinkleSpeed = Mathf.Clamp01(twinkleSpeed);
            LayerFalloffs = new Vector3(
                Mathf.Clamp(layer1Falloff, 1f, 5f),
                Mathf.Clamp(layer2Falloff, 1f, 5f),
                Mathf.Clamp(layer3Falloff, 1f, 5f));
            HorizonFalloff = Mathf.Clamp(horizonFalloff, 0f, 5f);
            AreaTexture = areaTexture as Texture2D;
            AreaIntensity = Mathf.Clamp(areaIntensity, 0f, 3f);
            var areaDensityMin = Mathf.Clamp(areaDensityMinMax.x, 1f, 100f);
            var areaDensityMax = Mathf.Clamp(areaDensityMinMax.y, areaDensityMin, 100f);
            AreaDensityMinMax = new Vector2(areaDensityMin, areaDensityMax);
            AreaMaskTiling = areaMaskTiling;
            AreaMaskOffset = areaMaskOffset;
            AreaSpeed = Mathf.Clamp01(areaSpeed);
            AreaFalloff = Mathf.Clamp(areaFalloff, 1f, 10f);
            AreaMaskFalloff = Mathf.Clamp(areaMaskFalloff, 1f, 10f);
            GalaxyCloudTexture = galaxyCloudTexture as Texture2D;
            GalaxyCloudTiling = galaxyCloudTiling;
            GalaxyCloudOffset = galaxyCloudOffset;
            GalaxyCloudRotation = Mathf.Repeat(galaxyCloudRotation, 360f);
            GalaxyCloudIntensity = Mathf.Clamp01(galaxyCloudIntensity);
            GalaxyCloudFalloff = Mathf.Clamp(galaxyCloudFalloff, 1f, 100f);
            GalaxyStarIntensity = Mathf.Clamp(galaxyStarIntensity, 0f, 3f);
            GalaxyStarFalloff = Mathf.Clamp(galaxyStarFalloff, 1f, 10f);
            GalaxyStarHeight = Mathf.Clamp(galaxyStarHeight, 1f, 20f);
            GalaxyStarSpeed = Mathf.Clamp01(galaxyStarSpeed);
            CustomStarTexture = customStarTexture as Texture2D;
            CustomStarTextureScale = customStarTextureScale;
            CustomStarTextureOffset = customStarTextureOffset;
            CustomStarRotation = Mathf.Clamp01(customStarRotation);
            CustomStarScaleMin = Mathf.Clamp01(customStarScaleMin);
            CustomStarIntensityMax = customStarIntensityMax;
            CustomStarIntensityMin = customStarIntensityMin;
            CustomStarScatterSpeed = Mathf.Clamp(customStarScatterSpeed, 0f, 100f);
            CustomStarScatterInterval = Mathf.Clamp(customStarScatterInterval, 0f, 100f);
            var hasSourceTexture = StarsTexture != null || AreaTexture != null
                || GalaxyCloudTexture != null || CustomStarTexture != null;
            // PhysicalSky Mobile bakes its star and galaxy-cloud intensities,
            // so PC-authored zero intensity cannot be used as a shared
            // early-out. Missing source textures remain safely black-bound.
            Enabled = enabled && hasSourceTexture;
        }

        public static BurtAtmosphereStarSettings Disabled => new BurtAtmosphereStarSettings(
            false,
            null,
            null,
            0.15f,
            0f,
            Color.white,
            0f,
            Vector2.one,
            Vector2.zero,
            6f,
            7f,
            8f,
            0.01f,
            0.5f,
            0.5f,
            2f,
            2f,
            2f,
            1f,
            null,
            0.15f,
            new Vector2(20f, 50f),
            new Vector2(2f, 0.5f),
            Vector2.zero,
            0.1f,
            1.25f,
            2.5f,
            null,
            new Vector2(0.5f, 1.5f),
            new Vector2(-0.3f, -0.3f),
            117f,
            0.0001f,
            2f,
            0.15f,
            1.5f,
            6f,
            0.01f,
            null,
            Vector2.one,
            Vector2.zero,
            0f,
            0.8f,
            new Vector4(10f, 5f, 5f, 100f),
            new Vector4(1f, 1f, 1f, 0.1f),
            10f,
            5f);

        private static Color ClampHdrColor(Color value)
        {
            return new Color(Mathf.Max(0f, value.r), Mathf.Max(0f, value.g), Mathf.Max(0f, value.b), 1f);
        }

    }

    internal readonly struct BurtAtmospherePanoramicCloudSettings
    {
        public readonly bool Enabled;
        public readonly bool UseDefaultTexture;
        public readonly Texture2D DefaultTexture;
        public readonly Texture2D PreviousWeatherTexture;
        public readonly Texture2D CurrentWeatherTexture;
        public readonly bool TextureInTransition;
        public readonly float TextureTransition;
        public readonly float DayUvOffset;
        public readonly float NightUvOffset;
        public readonly float RotationSpeed;
        public readonly float SunnyLuminance;
        public readonly float NightLuminance;
        public readonly bool IgnoreTimeOfDayColors;
        public readonly Color BaseColor;
        public readonly Color DetailSpecular;
        public readonly float Alpha;

        public BurtAtmospherePanoramicCloudSettings(
            bool enabled,
            bool useDefaultTexture,
            Texture defaultTexture,
            Texture previousWeatherTexture,
            Texture currentWeatherTexture,
            bool textureInTransition,
            float textureTransition,
            float dayUvOffset,
            float nightUvOffset,
            float rotationSpeed,
            float sunnyLuminance,
            float nightLuminance,
            bool ignoreTimeOfDayColors,
            Color baseColor,
            Color detailSpecular,
            float alpha)
        {
            UseDefaultTexture = useDefaultTexture;
            DefaultTexture = defaultTexture as Texture2D;
            PreviousWeatherTexture = previousWeatherTexture as Texture2D;
            CurrentWeatherTexture = currentWeatherTexture as Texture2D;
            TextureInTransition = !useDefaultTexture
                && textureInTransition
                && PreviousWeatherTexture != null
                && CurrentWeatherTexture != null;
            TextureTransition = Mathf.Clamp01(textureTransition);
            DayUvOffset = Mathf.Clamp(dayUvOffset, -1f, 1f);
            NightUvOffset = Mathf.Clamp(nightUvOffset, -1f, 1f);
            RotationSpeed = Mathf.Clamp(rotationSpeed, -0.0006f, 0.0006f);
            SunnyLuminance = Mathf.Clamp(sunnyLuminance, 0f, 100000f);
            NightLuminance = Mathf.Clamp(nightLuminance, 0f, 100000f);
            IgnoreTimeOfDayColors = ignoreTimeOfDayColors;
            BaseColor = ClampHdrColor(baseColor);
            DetailSpecular = ClampHdrColor(detailSpecular);
            Alpha = Mathf.Clamp(alpha, 0f, 100f);
            var selectedTexture = useDefaultTexture ? DefaultTexture : CurrentWeatherTexture;
            Enabled = enabled && selectedTexture != null
                && (SunnyLuminance > 0.000001f || NightLuminance > 0.000001f);
        }

        public static BurtAtmospherePanoramicCloudSettings Disabled => new BurtAtmospherePanoramicCloudSettings(
            false,
            false,
            null,
            null,
            null,
            false,
            1f,
            0f,
            0f,
            0.0002f,
            7000f,
            0.1f,
            false,
            Color.white,
            Color.white,
            1f);

        private static Color ClampHdrColor(Color value)
        {
            return new Color(Mathf.Max(0f, value.r), Mathf.Max(0f, value.g), Mathf.Max(0f, value.b), 1f);
        }
    }

    internal readonly struct BurtAtmospherePhysicalSkyDesaturationSettings
    {
        public readonly bool ForceEnabled;
        public readonly Color Color;
        public readonly float Effect;
        public readonly float Blend;
        public readonly float SkyIntensity;
        public readonly float CloudIntensity;

        public BurtAtmospherePhysicalSkyDesaturationSettings(
            bool forceEnabled,
            Color color,
            float effect,
            float skyIntensity,
            float cloudIntensity)
        {
            ForceEnabled = forceEnabled;
            Color = ClampColor(color);
            Effect = Mathf.Clamp01(effect);
            Blend = forceEnabled ? 1f : Effect;
            SkyIntensity = Mathf.Clamp(skyIntensity, 0f, 0.3f);
            CloudIntensity = Mathf.Clamp(cloudIntensity, 0f, 0.1f);
        }

        public static BurtAtmospherePhysicalSkyDesaturationSettings Disabled
            => new BurtAtmospherePhysicalSkyDesaturationSettings(
                false,
                Color.white,
                0f,
                0.1f,
                0.05f);

        private static Color ClampColor(Color value)
        {
            return new Color(
                Mathf.Max(0f, value.r),
                Mathf.Max(0f, value.g),
                Mathf.Max(0f, value.b),
                1f);
        }
    }

    internal readonly struct BurtAtmosphereWeatherSettings
    {
        public readonly bool Enabled;
        public readonly Texture2D CoverageTexture;
        public readonly float RainIntensity;
        public readonly float RainWetCoverage;
        public readonly float SnowIntensity;
        public readonly float SnowCoverage;
        public readonly float CloudShadowMarchDistance;
        public readonly Color CloudShadowBright;
        public readonly Color CloudShadowDark;

        public BurtAtmosphereWeatherSettings(
            bool enabled,
            Texture coverageTexture,
            float rainIntensity,
            float rainWetCoverage,
            float snowIntensity,
            float snowCoverage,
            float cloudShadowMarchDistance,
            Color cloudShadowBright,
            Color cloudShadowDark)
        {
            CoverageTexture = coverageTexture as Texture2D;
            RainIntensity = rainIntensity;
            RainWetCoverage = rainWetCoverage;
            SnowIntensity = snowIntensity;
            SnowCoverage = snowCoverage;
            CloudShadowMarchDistance = cloudShadowMarchDistance;
            CloudShadowBright = cloudShadowBright;
            CloudShadowDark = cloudShadowDark;
            // XRender always uploads the NatureCommon weather values and lets
            // each PhysicalSky formula decide where saturation is required.
            // BRP retains an explicit compatibility switch, but zero or
            // negative authored values must not silently disable raw semantics.
            Enabled = enabled;
        }

        public static BurtAtmosphereWeatherSettings Disabled => new BurtAtmosphereWeatherSettings(
            false,
            null,
            0f,
            0f,
            0f,
            0f,
            0.03f,
            new Color(0.76f, 0.77f, 0.8f, 1f),
            new Color(0.45f, 0.5f, 0.6f, 1f));
    }

    internal readonly struct BurtAtmosphereEffectiveCoefficients
    {
        public readonly Color RayleighScattering;
        public readonly Color MieScattering;
        public readonly Color MieAbsorption;
        public readonly Color MieExtinction;
        public readonly Color OzoneAbsorption;

        public BurtAtmosphereEffectiveCoefficients(
            Color rayleighScattering,
            Color mieScattering,
            Color mieAbsorption,
            Color ozoneAbsorption)
        {
            RayleighScattering = rayleighScattering;
            MieScattering = mieScattering;
            MieAbsorption = mieAbsorption;
            MieExtinction = new Color(
                mieScattering.r + mieAbsorption.r,
                mieScattering.g + mieAbsorption.g,
                mieScattering.b + mieAbsorption.b,
                1f);
            OzoneAbsorption = ozoneAbsorption;
        }
    }

    internal readonly struct BurtAtmosphereDensityProfile
    {
        public readonly float RayleighDensityExpScale;
        public readonly float MieDensityExpScale;
        public readonly float OzoneLayerSplitAltitude;
        public readonly float OzoneDensity0LinearTerm;
        public readonly float OzoneDensity0ConstantTerm;
        public readonly float OzoneDensity1LinearTerm;
        public readonly float OzoneDensity1ConstantTerm;

        public BurtAtmosphereDensityProfile(
            float rayleighDensityExpScale,
            float mieDensityExpScale,
            float ozoneLayerSplitAltitude,
            float ozoneDensity0LinearTerm,
            float ozoneDensity0ConstantTerm,
            float ozoneDensity1LinearTerm,
            float ozoneDensity1ConstantTerm)
        {
            RayleighDensityExpScale = rayleighDensityExpScale;
            MieDensityExpScale = mieDensityExpScale;
            OzoneLayerSplitAltitude = ozoneLayerSplitAltitude;
            OzoneDensity0LinearTerm = ozoneDensity0LinearTerm;
            OzoneDensity0ConstantTerm = ozoneDensity0ConstantTerm;
            OzoneDensity1LinearTerm = ozoneDensity1LinearTerm;
            OzoneDensity1ConstantTerm = ozoneDensity1ConstantTerm;
        }

        public float EvaluateRayleigh(float altitudeKm)
        {
            return Mathf.Exp(RayleighDensityExpScale * Mathf.Max(altitudeKm, 0f));
        }

        public float EvaluateMie(float altitudeKm)
        {
            return Mathf.Exp(MieDensityExpScale * Mathf.Max(altitudeKm, 0f));
        }

        public float EvaluateOzone(float altitudeKm)
        {
            var clampedAltitudeKm = Mathf.Max(altitudeKm, 0f);
            var density = clampedAltitudeKm < OzoneLayerSplitAltitude
                ? OzoneDensity0LinearTerm * clampedAltitudeKm + OzoneDensity0ConstantTerm
                : OzoneDensity1LinearTerm * clampedAltitudeKm + OzoneDensity1ConstantTerm;
            return Mathf.Clamp01(density);
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
        public readonly float PhysicalSkyTimeOfDayCurve;
        public readonly Material PhysicalSkyMaterial;
        public readonly Mesh PhysicalSkyMesh;
        public readonly Vector3 PhysicalSkyMeshWorldPosition;
        public readonly BurtAtmosphereStylizedSkySettings StylizedSky;
        public readonly BurtAtmosphereMoonSettings Moon;
        public readonly BurtAtmosphereStarSettings Stars;
        public readonly BurtAtmospherePanoramicCloudSettings PanoramicClouds;
        public readonly BurtAtmospherePhysicalSkyDesaturationSettings PhysicalSkyDesaturation;
        public readonly BurtAtmosphereWeatherSettings Weather;
        public readonly bool AerialPerspectiveEnabled;
        public readonly float AerialPerspectiveDensityScale;
        public readonly float AerialPerspectiveLuminanceScale;
        public readonly float AerialPerspectiveSamplingDistanceScale;
        public readonly float AerialPerspectiveIntensity;
        public readonly float AerialPerspectiveDistance;
        public readonly float AerialPerspectiveHeightFalloff;
        public readonly Color AerialPerspectiveTint;
        public readonly float AerialPerspectiveStartDepth;
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
            float physicalSkyTimeOfDayCurve,
            BurtAtmosphereStylizedSkySettings stylizedSky,
            BurtAtmosphereMoonSettings moon,
            BurtAtmosphereStarSettings stars,
            BurtAtmospherePanoramicCloudSettings panoramicClouds,
            BurtAtmospherePhysicalSkyDesaturationSettings physicalSkyDesaturation,
            BurtAtmosphereWeatherSettings weather,
            bool aerialPerspectiveEnabled,
            float aerialPerspectiveDensityScale,
            float aerialPerspectiveLuminanceScale,
            float aerialPerspectiveSamplingDistanceScale,
            float aerialPerspectiveIntensity,
            float aerialPerspectiveDistance,
            float aerialPerspectiveHeightFalloff,
            Color aerialPerspectiveTint,
            float aerialPerspectiveStartDepth,
            float aerialPerspectiveNearFadeEnd,
            float aerialPerspectiveMaxOpacity,
            AtmosphereAerialPerspectivePlacement aerialPerspectivePlacement,
            AtmosphereFogInteraction fogInteraction,
            Mesh physicalSkyMesh = null,
            Vector3 physicalSkyMeshWorldPosition = default,
            Material physicalSkyMaterial = null)
        {
            Enabled = enabled;
            RayleighIntensity = Mathf.Max(0f, rayleighIntensity);
            MieIntensity = Mathf.Max(0f, mieIntensity);
            MieAnisotropy = Mathf.Clamp(mieAnisotropy, 0f, 0.999f);
            RayleighScatteringCoefficient = ClampCoefficient(rayleighScatteringCoefficient);
            MieScatteringCoefficient = ClampCoefficient(mieScatteringCoefficient);
            MieAbsorptionCoefficient = ClampCoefficient(mieAbsorptionCoefficient);
            OzoneAbsorptionIntensity = Mathf.Max(0f, ozoneAbsorptionIntensity);
            OzoneAbsorptionCoefficient = ClampCoefficient(ozoneAbsorptionCoefficient);
            OzoneLayerCenter = Mathf.Clamp(ozoneLayerCenter, 0f, 60f);
            OzoneLayerThickness = Mathf.Clamp(ozoneLayerThickness, 0.01f, 20f);
            MultipleScatteringIntensity = Mathf.Clamp(multipleScatteringIntensity, 0f, 2f);
            TraceSampleCountScale = Mathf.Clamp(traceSampleCountScale, 0.25f, 8f);
            PlanetRadius = Mathf.Clamp(planetRadius, 0.1f, 10000f);
            AtmosphereHeight = Mathf.Clamp(atmosphereHeight, 0.1f, 200f);
            RayleighScaleHeight = Mathf.Clamp(rayleighScaleHeight, 0.001f, 20f);
            MieScaleHeight = Mathf.Clamp(mieScaleHeight, 0.001f, 10f);
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
            PhysicalSkyTimeOfDayCurve = Mathf.Clamp01(physicalSkyTimeOfDayCurve);
            PhysicalSkyMaterial = physicalSkyMaterial;
            PhysicalSkyMesh = physicalSkyMesh;
            PhysicalSkyMeshWorldPosition = physicalSkyMeshWorldPosition;
            StylizedSky = stylizedSky;
            Moon = moon;
            Stars = stars;
            PanoramicClouds = panoramicClouds;
            PhysicalSkyDesaturation = physicalSkyDesaturation;
            Weather = weather;
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
            AerialPerspectiveStartDepth = Mathf.Max(0f, aerialPerspectiveStartDepth);
            AerialPerspectiveNearFadeEnd = Mathf.Max(AerialPerspectiveStartDepth + 0.001f, aerialPerspectiveNearFadeEnd);
            AerialPerspectiveMaxOpacity = Mathf.Clamp01(aerialPerspectiveMaxOpacity);
            AerialPerspectivePlacement = aerialPerspectivePlacement;
            FogInteraction = fogInteraction;
        }

        public float AerialPerspectiveNearFadeStart => AerialPerspectiveStartDepth;

        public static BurtAtmosphereSettings Disabled => new BurtAtmosphereSettings(false, 0f, 0f, 0f, Color.black, Color.black, Color.black, 0f, Color.black, 25f, 15f, 0f, 1f, 6360f, 60f, 8f, 1.2f, AtmospherePlanetTransformMode.PlanetTopAtAbsoluteWorldOrigin, Vector3.zero, new Vector3(0f, -6360000f, 0f), 0.001f, Color.black, Color.black, Color.white, Color.white, 0f, 0.5f, 1.2f, 1f, 1f, AtmosphereSunSource.MainLight, Vector3.up, 0f, 1f, new Color(0.48f, 0.66f, 0.92f, 1f), new Color(0.95f, 0.82f, 0.58f, 1f), 1f, 0.65f, 0.35f, 0.22f, -0.02f, -0.20f, 0f, 4f, 0f, BurtAtmosphereStylizedSkySettings.Disabled, BurtAtmosphereMoonSettings.Disabled, BurtAtmosphereStarSettings.Disabled, BurtAtmospherePanoramicCloudSettings.Disabled, BurtAtmospherePhysicalSkyDesaturationSettings.Disabled, BurtAtmosphereWeatherSettings.Disabled, false, 0f, 0f, 0f, 0f, 250f, 0f, new Color(0.70f, 0.82f, 1.0f, 1f), 0f, 50f, 0.65f, AtmosphereAerialPerspectivePlacement.AfterOpaqueBeforeSky, AtmosphereFogInteraction.Additive);

        private static Color ClampCoefficient(Color value)
        {
            return new Color(Mathf.Max(0f, value.r), Mathf.Max(0f, value.g), Mathf.Max(0f, value.b), 1f);
        }
    }

    internal static class BurtAtmosphereUtility
    {
        private const int MainLightTransmittanceSampleCount = 15;
        private const float MainLightTransmittanceReferenceAltitudeKm = 0.5f;

        public const float MinimumSunAngularDiameterDegrees = 0f;
        public const float MaximumSunAngularDiameterDegrees = 90f;
        public const string ShaderName = "Hidden/BurtRP/AtmosphereScattering";
        public const string SkyFormulaName = "LutPhysicalXRenderExactMultipleScatteringExactOpticalDepthNativeLinearClampExactHgIntegratorDirectSkyUvVariableScheduleFastAngleHorizontalContractInsideAtmosphereConsumerDuffBasisExactIlluminancePhysicalSkyTodPermutationUploadOnlyStylizedPcMobileCelestialWeatherBinarySunMoonStarsCustomStarPanoramicCloudRawHdrDesaturationCaptureParityRawFinalSumNoLegacyExposureStrictFarDepthZTestLEqualBlendOneSrcAlphaPreserveTargetAlphaAuthoredMeshUv01V42";
        public const string SkyCaptureFormulaName = "XRenderPhysicalSkyCaptureInsideAtmosphereConsumerDuffBasisExactIlluminanceUploadOnlyStylizedPcMobileWeatherGateDesaturationPanoramicCloudRawHdrRawFinalSumSceneLinearNoLegacyExposureZTestLEqualBlendOneSrcAlphaPreserveTargetAlphaAuthoredMeshUv01V10";
        public const string AerialFormulaName = "LutPhysicalCameraFrustumXRenderExactMultipleScatteringExactOpticalDepthNativeLinearClampExactHgSegmentIntegralVariableScheduleFastAngleHorizontalContractSharedDepthMappingStartDepthIntrinsicFroxelWeightUnitLightOuterIlluminanceOcclusionLuminanceScalePreExposureNoLegacyShapeNoTintNonJitteredProjectionTransparentTotalFogStrictFarDepthPreserveTargetAlphaV33";
        public const string AtmosphereCombineFormulaName = "XRenderStrictFarDepthReturnBlackAlphaOneRawFinalSumColorDepthAttachedZTestLEqualBlendOneSrcAlphaZeroOneTargetAlphaPreservedSkyboxReplacementV3";
        public const string AtmosphereCombineTopologyFormulaName = "XRenderPcSinglePassMobilePrepareThenTileDrawV1";
        public const string AtmosphereCombinePermutationFormulaName = "XRenderCaptureKeywordNightTodGt05CaptureForcesDayPreviewSkippedV1";
        public const string AtmosphereReflectionCaptureFormulaName = "XRenderSkyLightComponentWorldPositionCameraFallbackOwnerOriginCacheEveryFrameOnDemandRequestVersionSceneLinearV2";
        public const string AerialProjectionFormulaName = "XRenderNonJitteredGpuProjectionPlatformUvOrthographic10000V1";
        public const string AtmospherePhaseFormulaName = "XRenderRayleighPbrtHenyeyGreensteinExactV1";
        public const string AtmosphereLutSamplingFormulaName = "XRenderRawParameterUvNativeLinearClampMip0V1";
        public const string AtmosphereTransmittanceTransformFormulaName = "XRenderRawUnitUvDistanceParameterizationExactInverseLayerRootSelectionSpaceEntry005V1";
        public const string AtmosphereEffectiveCoefficientFormulaName = "XRenderNormalizedColorTimesScaleLegacyRayleigh1Mie012Ozone1SharedLutCpuCacheMaterialDebugV1";
        public const string AtmosphereDensityProfileFormulaName = "XRenderCpuResolvedRayleighMieExpOzonePiecewiseSaturateExact001001RangesSharedLutCpuDebugV1";
        public const string AtmospherePlanetGeometryFormulaName = "XRenderBottomRadius01TopHeight01TransformModesWorldToKmRaySphereGenericSurfaceZeroV1";
        public const string AtmosphereLayerIntersectionFormulaName = "XRenderExplicitAtmosphereHitLookAtGroundRangeNearBottomFarTopInsidePlanetSquaredV1";
        public const string AtmosphereFogReprojectionFormulaName = "XRenderCurrSliceMixedHorizonThresholdViewLift002UndergroundSurfaceBackHorizonMirrorV1";
        public const string AtmosphereFogConsumerFormulaName = "XRenderPcScreenUvFlipDistanceKmSqrtDepthFirstHalfFroxelWeightUnitLightRgbAlphaZeroLuminanceReturnsNoFogPhysicalDebugOpacityFromFinalTransmittanceRawInscatterV2";
        public const string AtmosphereLutOpticalDepthFormulaName = "XRenderFixed03CpuDensityExpTentCoefficientsV1";
        public const string AtmosphereMultipleScatteringFormulaName = "XRenderTwoDirection4PiIsotropicGeometricSeriesGroundV1";
        public const string AtmosphereVariableSamplingFormulaName = "XRenderSky4To32Inv150Horizontal4To32Upload150QuadraticV1";
        public const string AtmosphereSkyViewTransformFormulaName = "XRenderAcosFast4Atan2FastSubUvV1";
        public const string AtmosphereHorizontalScatteringFormulaName = "XRenderCameraGeocentricMin16Uv5048BakedSkyAndMultipleScaleStructuredToConstantBufferV2";
        public const string AtmosphereSkyViewConsumerFormulaName = "XRenderInsideTopStrictDuffBasisOuterLightOcclusionSkyScaleV1";
        public const string WeatherLightingPropagationFormulaName = "XRenderWeatherCloudShadowPhysicalSkyOnlyMainLightOcclusionIndependentSceneLightFogPanoramicOnceV1";
        public const string MainLightTransmittanceFormulaName = "XRenderGroundReference05KmElevationCollapsedOpticalDepth15RawUndergroundDensityDirectExpOptionalStrengthV2";
        public const string PhysicalSkyTimeOfDayFormulaName = "XRenderExplicitTodCurveDayZeroNightSineNightPermutationGt05V1";
        public const string PhysicalSkyAnimationTimeFormulaName = "XRenderElapsedSecondsDiv20Split128CpuUploadStablePhaseReconstructionRawAnimationMultiply20V1";
        public const string PhysicalSkyStylizedFormulaName = "XRenderProjectPhysicalSkyUploadOnlyBlendBaseHorizonSunGlowSunRiseSkyTintAnalyticFallbackV1";
        public const string SunDiskFormulaName = "XRenderProjectPhysicalSkySolidAngleHardDiskWorldDotTodGt0WeatherPreExposedClamp64000NoCaptureNoAtmosphereTrNoPlanetNoLimbNoOcclusionDiameter0To90Default6V4";
        public const string MoonDiskFormulaName = "XRenderPhysicalSkyNightPermutationPcWorldBasisUnconditionalPhaseNormalWhiteDummyFastAsinPhaseWeightedBloomRawFlareWeatherMeshUv0NoClampMobilePackedGbaFixedFlareNoWeatherNoRiseNoPlanetNoCaptureV7";
        public const string StarFieldFormulaName = "XRenderPhysicalSkyNightPermutationPcWorldBasisThreeLayerAreaTwinkleTintMip0GalaxyUnconditionalMoonMaskWorldPositionHorizonWeatherMeshUv0RawNoClampMobileFixedTwoLayerGalaxyCloudWorldPositionHorizonCustomStarNoWeatherNoPlanetStableSplitTimeV7";
        public const string PhysicalSkyCelestialUploadOnlyFormulaName = "XRenderProjectPhysicalSkyUploadOnlyMoonFogMoonRiseStarsDayBrightnessStarsRiseV1";
        public const string PhysicalSkyPlatformFormulaName = "XRenderProjectPhysicalSkyPcFullMobileFixedCelestialNoWeatherSkyV1";
        public const string PhysicalSkyTextureSamplingFormulaName = "XRenderFixedLinearClampMoonPhaseCustomStarMip0GalaxyImplicitFixedLinearRepeatStarsAreaTintMip0WeatherPanoramicImplicitV1";
        public const string PhysicalSkyTextureBindingFormulaName = "XRenderPerDrawMoonWhitePhaseWhiteStarsBlackTintWhiteAreaBlackGalaxyBlackCustomBlackPanoramicBlackWeatherWhiteNoStalePreparedGlobalsV1";
        public const string PhysicalSkyAuthoredDefaultsFormulaName = "XRenderEarth6360Top6420MieG08Ozone25x15Multiple1EnvSun05SunDiameter6Range0To90Moon2AlwaysEvaluatedCelestialWeatherPanoramicCustomStarUnrestrictedVectorsZeroEdgeRangesV2";
        public const string AtmosphereColorSpaceFormulaName = "XRenderGroundAlbedoGammaToLinearOnlyOtherAtmospherePhysicalSkyHdrColorsRawAlphaPerSourceDiscardRulesV1";
        public const string PanoramicCloudFormulaName = "XRenderPhysicalSkyTodGt05NatureCommonRadialWeatherTransitionMeshUv1SplitElapsedTimeRawHdrV5";
        public const string WeatherSkyFormulaName = "XRenderPhysicalSkyPcRawNatureCommonWeightsShadowOffsetColorsSkyMoonStarsCoverageMeshUv0SplitElapsedTimeMobileSunDiskOnlyV6";
        public const string PhysicalSkyDesaturationFormulaName = "XRenderPhysicalSkySharedSkyCloudDesaturationV1";
        public const string PhysicalSkyMeshUvFormulaName = "XRenderCustomMaterialPass0CustomMeshDefaultSkyMeshSha256A9227D38C440AE5B81C2068D2E38DC9184381781BF6042A15440288BDD02BA0BImporterExactExceptGuidProceduralTriangleFallbackMeshUv0WeatherCelestialMeshUv1PanoramicTranslationOnlyV3";
        public const string AtmospherePlatformFormulaName = "XRenderPcFogLutAerialMobileNoFogLutNoAerialFixedForwardSkyCache10Deg100WorldUnitsV1";
        public const string AtmosphereLutResourceFormulaName = "XRenderPackedRgbLutsWhenTypedUavSupportedExplicitRgba16fStockUnityCompatibilityLinearClampExplicitLod0NoMipsHorizontalStructuredToConstantBufferFogRgba16fDispatchScopedCompleteKernelBindingsNoAmbientComputeStateV5";
        public const string AtmosphereAsyncComputeFormulaName = "XRenderGlobalAndFeatureGateBackgroundQueueGraphicsToComputeToGraphicsFenceCrossCameraPersistentResourceRecoveryDeferredTransactionalBatchV2";

        internal static bool IsMobileAtmospherePlatform => Application.isMobilePlatform;

        internal static bool SupportsAtmosphereFogLut => !IsMobileAtmospherePlatform;

        // XRender resolves each authored color/scale pair once in RenderProxy.
        // BRP's legacy intensity controls map onto those scales, so every
        // physical consumer must use this result rather than rescaling locally.
        internal static BurtAtmosphereEffectiveCoefficients ResolveEffectiveCoefficients(
            BurtAtmosphereSettings settings)
        {
            const float mieIntensityReference = 0.12f;
            var rayleighScale = Mathf.Max(settings.RayleighIntensity, 0f);
            var mieScale = Mathf.Max(settings.MieIntensity, 0f) / mieIntensityReference;
            var ozoneScale = Mathf.Max(settings.OzoneAbsorptionIntensity, 0f);
            return new BurtAtmosphereEffectiveCoefficients(
                ScaleCoefficient(settings.RayleighScatteringCoefficient, rayleighScale),
                ScaleCoefficient(settings.MieScatteringCoefficient, mieScale),
                ScaleCoefficient(settings.MieAbsorptionCoefficient, mieScale),
                ScaleCoefficient(settings.OzoneAbsorptionCoefficient, ozoneScale));
        }

        private static Color ScaleCoefficient(Color coefficient, float scale)
        {
            return new Color(
                coefficient.r * scale,
                coefficient.g * scale,
                coefficient.b * scale,
                1f);
        }

        // XRender converts scale heights and its ozone tent to shader-ready
        // coefficients in RenderProxy. Settings are already clamped to the
        // source-authored ranges, so no wider consumer-specific floor belongs
        // here.
        internal static BurtAtmosphereDensityProfile ResolveDensityProfile(
            BurtAtmosphereSettings settings)
        {
            var rayleighDensityExpScale = -1f / settings.RayleighScaleHeight;
            var mieDensityExpScale = -1f / settings.MieScaleHeight;
            var ozoneDensity0LinearTerm = 1f / settings.OzoneLayerThickness;
            var ozoneDensity1LinearTerm = -ozoneDensity0LinearTerm;
            var ozoneDensity0ConstantTerm =
                1f - settings.OzoneLayerCenter * ozoneDensity0LinearTerm;
            var ozoneDensity1ConstantTerm =
                1f - settings.OzoneLayerCenter * ozoneDensity1LinearTerm;
            return new BurtAtmosphereDensityProfile(
                rayleighDensityExpScale,
                mieDensityExpScale,
                settings.OzoneLayerCenter,
                ozoneDensity0LinearTerm,
                ozoneDensity0ConstantTerm,
                ozoneDensity1LinearTerm,
                ozoneDensity1ConstantTerm);
        }

        internal static Vector4 EvaluateMoonDiskLuminanceAndGeometry(BurtAtmosphereMoonSettings moon)
        {
            var halfApexRadians = Mathf.Clamp(moon.AngularDiameter, 0.1f, 20f) * 0.5f * Mathf.Deg2Rad;
            var solidAngle = Mathf.Max(2f * Mathf.PI * (1f - Mathf.Cos(halfApexRadians)), 0.0000001f);
            var diskLuminance = moon.Enabled ? moon.Intensity / solidAngle : 0f;
            return new Vector4(
                Mathf.Max(0f, diskLuminance),
                halfApexRadians,
                moon.FlareSize,
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
            var solidAngle = 2f * Mathf.PI * (1f - cosHalfApex);
            var hasMainLight = request != null
                && request.LightingData != null
                && request.LightingData.HasMainLight;
            var outerSpaceIlluminance = hasMainLight
                ? request.LightingData.MainLightColorOuterSpace
                : Color.clear;
            // The project's PhysicalSky material maps XRender's
            // m_SunDiskColorScale to this existing BRP color control. Its
            // scalar SunIntensity/SunDiskIntensity controls remain available
            // only to the analytic fallback and do not alter the physical disk.
            var colorScale = settings.StylizedSky.SunDiskColorScale;
            var inverseSolidAngle = 1f / solidAngle;
            return new Vector4(
                outerSpaceIlluminance.r * colorScale.r * inverseSolidAngle,
                outerSpaceIlluminance.g * colorScale.g * inverseSolidAngle,
                outerSpaceIlluminance.b * colorScale.b * inverseSolidAngle,
                cosHalfApex);
        }

        // Matches XRender's ShouldRenderAtmosphereForRequest contract. LUT
        // generation and all atmosphere consumers depend on feature resources,
        // a valid camera and a real main light; they do not depend on whether
        // this camera clears to the physical sky.
        public static bool ShouldUseAtmosphereResources(BurtRenderRequest request)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview ||
                request.Type == BurtRenderRequestType.UICamera)
            {
                return false;
            }

            var lightingData = request.LightingData;
            return lightingData != null &&
                lightingData.HasMainLight &&
                ResolveSettings().Enabled;
        }

        public static bool ShouldUseAtmosphereAsyncCompute(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return TryResolveAtmosphereAsyncComputeGate(request, asset, out _);
        }

        public static string FormatAtmosphereAsyncComputeGate(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            var enabled = TryResolveAtmosphereAsyncComputeGate(request, asset, out var reason);
            var cameraAllowsAsync =
                request == null ||
                request.CameraData == null ||
                request.CameraData.EnableAsyncCompute;
            return string.Concat(
                "Enabled=", enabled,
                " Reason=", reason,
                " Global=", asset != null && asset.EnableAsyncCompute,
                " Feature=", asset != null && asset.EnableAtmosphereLutAsyncCompute,
                " Camera=", cameraAllowsAsync,
                " ComputeSupported=", SystemInfo.supportsComputeShaders,
                " AsyncSupported=", SystemInfo.supportsAsyncCompute,
                " FenceSupported=", SystemInfo.supportsGraphicsFence,
                " Queue=Background",
                " Contract=", AtmosphereAsyncComputeFormulaName);
        }

        private static bool TryResolveAtmosphereAsyncComputeGate(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            out string reason)
        {
            if (asset == null)
            {
                reason = "AssetMissing";
                return false;
            }

            if (!asset.EnableAsyncCompute)
            {
                reason = "GlobalDisabled";
                return false;
            }

            if (!asset.EnableAtmosphereLutAsyncCompute)
            {
                reason = "AtmosphereFeatureDisabled";
                return false;
            }

            if (!SystemInfo.supportsComputeShaders)
            {
                reason = "ComputeShadersUnsupported";
                return false;
            }

            if (!SystemInfo.supportsAsyncCompute)
            {
                reason = "AsyncComputeUnsupported";
                return false;
            }

            if (!SystemInfo.supportsGraphicsFence)
            {
                reason = "GraphicsFenceUnsupported";
                return false;
            }

            if (!ShouldUseAtmosphereResources(request))
            {
                reason = "AtmosphereResourcesDisabled";
                return false;
            }

            if (request.CameraData != null && !request.CameraData.EnableAsyncCompute)
            {
                reason = "CameraDisabled";
                return false;
            }

            // Reflection captures have no useful shadow overlap window in BRP and
            // immediately consume the LUT while building their sky reflection.
            if (request.Type == BurtRenderRequestType.Reflection)
            {
                reason = "ReflectionSynchronous";
                return false;
            }

            reason = "Enabled";
            return true;
        }

        // The visible physical-sky combine is a stricter consumer of the
        // atmosphere resources. Reflection requests keep their normal probe
        // rendering path, and non-Skybox cameras retain their authored clear.
        public static bool ShouldUseAtmosphere(BurtRenderRequest request)
        {
            if (!ShouldUseAtmosphereResources(request) ||
                request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            return BurtCameraClearUtility.ResolveClearMode(request) == BurtCameraClearMode.Skybox;
        }

        public static bool ShouldUseAerialPerspective(BurtRenderRequest request)
        {
            // Aerial perspective is a depth-based opaque composite, not a sky clear pass.
            // Keep reflection excluded, but allow SolidColor cameras just as
            // XRender's atmosphere-fog combine does. Preview/UI and invalid
            // main-light cases are already rejected by the resource contract.
            // XRender removes the 3D atmosphere Fog LUT and every aerial
            // perspective consumer on mobile.
            if (!SupportsAtmosphereFogLut ||
                !ShouldUseAtmosphereResources(request) ||
                request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            var settings = ResolveSettings();
            return settings.AerialPerspectiveEnabled && settings.FogInteraction != AtmosphereFogInteraction.FogOnly;
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
                atmosphere.groundAlbedo.value.linear,
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
                atmosphere.physicalSkyTimeOfDayCurve.value,
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
                    atmosphere.moonPhaseNormalTexture.value,
                    atmosphere.moonRotationEuler.value,
                    atmosphere.moonIntensity.value,
                    atmosphere.moonAngularDiameter.value,
                    atmosphere.moonSurfaceTint.value,
                    atmosphere.moonAdditionalTint.value,
                    atmosphere.moonPhase.value,
                    atmosphere.moonPhaseRotation.value,
                    atmosphere.moonEarthshine.value,
                    atmosphere.moonPhaseSharpness.value,
                    atmosphere.moonFlareSize.value,
                    atmosphere.moonFlareFalloff.value,
                    atmosphere.moonFlareTint.value,
                    atmosphere.moonLightBloomIntensity.value,
                    atmosphere.moonLightBloomSize.value,
                    atmosphere.moonLightBloomFalloff.value,
                    atmosphere.moonLightBloomEdgeAlpha.value,
                    atmosphere.moonRiseBlendMin.value,
                    atmosphere.moonRiseBlendMax.value),
                new BurtAtmosphereStarSettings(
                    atmosphere.starsEnabled.value,
                    atmosphere.starsTexture.value,
                    atmosphere.starsTintColorTexture.value,
                    atmosphere.starsIntensity.value,
                    atmosphere.starsRotation.value,
                    atmosphere.starsTintColor.value,
                    atmosphere.starsTintColorSaturation.value,
                    atmosphere.starsTintColorTextureTiling.value,
                    atmosphere.starsTintColorTextureOffset.value,
                    atmosphere.starsLayer1Height.value,
                    atmosphere.starsLayer2Height.value,
                    atmosphere.starsLayer3Height.value,
                    atmosphere.starsLayerSpeed.value,
                    atmosphere.starsLayerTwinkleStrength.value,
                    atmosphere.starsLayerTwinkleSpeed.value,
                    atmosphere.starsLayer1Falloff.value,
                    atmosphere.starsLayer2Falloff.value,
                    atmosphere.starsLayer3Falloff.value,
                    atmosphere.starsHorizonFalloff.value,
                    atmosphere.areaStarsTexture.value,
                    atmosphere.areaStarsIntensity.value,
                    atmosphere.areaStarsDensityMinMax.value,
                    atmosphere.areaStarsMaskTiling.value,
                    atmosphere.areaStarsMaskOffset.value,
                    atmosphere.areaStarsSpeed.value,
                    atmosphere.areaStarsFalloff.value,
                    atmosphere.areaStarsMaskFalloff.value,
                    atmosphere.galaxyCloudTexture.value,
                    atmosphere.galaxyCloudTiling.value,
                    atmosphere.galaxyCloudOffset.value,
                    atmosphere.galaxyCloudRotation.value,
                    atmosphere.galaxyCloudIntensity.value,
                    atmosphere.galaxyCloudFalloff.value,
                    atmosphere.galaxyStarIntensity.value,
                    atmosphere.galaxyStarFalloff.value,
                    atmosphere.galaxyStarHeight.value,
                    atmosphere.galaxyStarSpeed.value,
                    atmosphere.customStarTexture.value,
                    atmosphere.customStarTextureScale.value,
                    atmosphere.customStarTextureOffset.value,
                    atmosphere.customStarRotation.value,
                    atmosphere.customStarScaleMin.value,
                    atmosphere.customStarIntensityMax.value,
                    atmosphere.customStarIntensityMin.value,
                    atmosphere.customStarScatterSpeed.value,
                    atmosphere.customStarScatterInterval.value),
                new BurtAtmospherePanoramicCloudSettings(
                    atmosphere.panoramicCloudEnabled.value,
                    atmosphere.panoramicCloudUseDefaultTexture.value,
                    atmosphere.panoramicCloudDefaultTexture.value,
                    atmosphere.panoramicCloudPreviousWeatherTexture.value,
                    atmosphere.panoramicCloudCurrentWeatherTexture.value,
                    atmosphere.panoramicCloudTextureInTransition.value,
                    atmosphere.panoramicCloudTextureTransition.value,
                    atmosphere.panoramicCloudDayUvOffset.value,
                    atmosphere.panoramicCloudNightUvOffset.value,
                    atmosphere.panoramicCloudRotationSpeed.value,
                    atmosphere.panoramicCloudSunnyLuminance.value,
                    atmosphere.panoramicCloudNightLuminance.value,
                    atmosphere.panoramicCloudIgnoreTimeOfDayColors.value,
                    atmosphere.panoramicCloudBaseColor.value,
                    atmosphere.panoramicCloudDetailSpecular.value,
                    atmosphere.panoramicCloudAlpha.value),
                new BurtAtmospherePhysicalSkyDesaturationSettings(
                    atmosphere.physicalSkyDesaturationEnabled.value,
                    atmosphere.physicalSkyDesaturationColor.value,
                    atmosphere.physicalSkyDesaturationEffect.value,
                    atmosphere.physicalSkyDesaturationIntensity.value,
                    atmosphere.physicalSkyCloudDesaturationIntensity.value),
                new BurtAtmosphereWeatherSettings(
                    atmosphere.weatherSkyCoverageEnabled.value,
                    atmosphere.weatherSkyCoverageTexture.value,
                    atmosphere.weatherRainIntensity.value,
                    atmosphere.weatherRainWetCoverage.value,
                    atmosphere.weatherSnowIntensity.value,
                    atmosphere.weatherSnowCoverage.value,
                    atmosphere.weatherCloudShadowMarchDistance.value,
                    atmosphere.weatherCloudShadowBright.value,
                    atmosphere.weatherCloudShadowDark.value),
                atmosphere.aerialPerspective.value,
                atmosphere.aerialPerspectiveDensityScale.value,
                atmosphere.aerialPerspectiveLuminanceScale.value,
                atmosphere.aerialPerspectiveSamplingDistanceScale.value,
                atmosphere.aerialPerspectiveIntensity.value,
                atmosphere.aerialPerspectiveDistance.value,
                atmosphere.aerialPerspectiveHeightFalloff.value,
                atmosphere.aerialPerspectiveTint.value,
                atmosphere.aerialPerspectiveStartDepth.value,
                atmosphere.aerialPerspectiveNearFadeEnd.value,
                atmosphere.aerialPerspectiveMaxOpacity.value,
                atmosphere.aerialPerspectivePlacement.value,
                atmosphere.aerialFogInteraction.value,
                atmosphere.physicalSkyMesh.value,
                atmosphere.physicalSkyMeshWorldPosition.value,
                atmosphere.physicalSkyMaterial.value);
        }

        internal static Color EvaluateMainLightGroundTransmittance(BurtAtmosphereSettings settings, Vector3 lightDirection)
        {
            var strength = settings.MainLightTransmittanceStrength;
            if (!settings.Enabled || strength <= 0f || lightDirection.sqrMagnitude <= 0f)
            {
                return Color.white;
            }

            // XRender discards azimuth for this spherically symmetric reference
            // integration and rebuilds a direction from the light elevation.
            var normalizedLightDirection = lightDirection.normalized;
            var elevation = Mathf.Max(
                -90f * Mathf.Deg2Rad,
                Mathf.Asin(normalizedLightDirection.y));
            var direction = new Vector3(0f, Mathf.Sin(elevation), Mathf.Cos(elevation));
            var bottomRadiusKm = Mathf.Max(settings.PlanetRadius, 0.1f);
            var topRadiusKm = bottomRadiusKm + Mathf.Max(settings.AtmosphereHeight, 0.1f);
            var origin = new Vector3(0f, bottomRadiusKm + MainLightTransmittanceReferenceAltitudeKm, 0f);
            var distanceToTopKm = RaySphereNearest(origin, direction, topRadiusKm);
            if (distanceToTopKm <= 0f)
            {
                return Color.white;
            }

            var effectiveCoefficients = ResolveEffectiveCoefficients(settings);
            var densityProfile = ResolveDensityProfile(settings);
            var rayleighCoefficient = new Vector3(
                effectiveCoefficients.RayleighScattering.r,
                effectiveCoefficients.RayleighScattering.g,
                effectiveCoefficients.RayleighScattering.b);
            var mieExtinctionCoefficient = new Vector3(
                effectiveCoefficients.MieExtinction.r,
                effectiveCoefficients.MieExtinction.g,
                effectiveCoefficients.MieExtinction.b);
            var ozoneCoefficient = new Vector3(
                effectiveCoefficients.OzoneAbsorption.r,
                effectiveCoefficients.OzoneAbsorption.g,
                effectiveCoefficients.OzoneAbsorption.b);
            var sampleLengthKm = distanceToTopKm / MainLightTransmittanceSampleCount;
            var opticalDepth = Vector3.zero;
            for (var sampleIndex = 0; sampleIndex < MainLightTransmittanceSampleCount; sampleIndex++)
            {
                // Match XRender's CPU reference integration: 15 uniform samples
                // starting at the ground reference point rather than midpoint samples.
                var samplePosition = origin + direction * (distanceToTopKm * sampleIndex / MainLightTransmittanceSampleCount);
                var altitudeKm = samplePosition.magnitude - bottomRadiusKm;
                // This CPU reference deliberately evaluates the authored profiles
                // at the raw altitude. Below-horizon rays therefore reproduce
                // XRender's underground exponential density rather than being
                // clamped to a surface-density approximation.
                var rayleighDensity = Mathf.Max(
                    0f,
                    Mathf.Exp(densityProfile.RayleighDensityExpScale * altitudeKm));
                var mieDensity = Mathf.Max(
                    0f,
                    Mathf.Exp(densityProfile.MieDensityExpScale * altitudeKm));
                var ozoneDensity = altitudeKm < densityProfile.OzoneLayerSplitAltitude
                    ? densityProfile.OzoneDensity0LinearTerm * altitudeKm + densityProfile.OzoneDensity0ConstantTerm
                    : densityProfile.OzoneDensity1LinearTerm * altitudeKm + densityProfile.OzoneDensity1ConstantTerm;
                ozoneDensity = Mathf.Clamp01(ozoneDensity);
                opticalDepth += (rayleighCoefficient * rayleighDensity
                    + mieExtinctionCoefficient * mieDensity
                    + ozoneCoefficient * ozoneDensity) * sampleLengthKm;
            }

            var physicalTransmittance = new Color(
                Mathf.Exp(-opticalDepth.x),
                Mathf.Exp(-opticalDepth.y),
                Mathf.Exp(-opticalDepth.z),
                1f);
            return Color.Lerp(Color.white, physicalTransmittance, strength);
        }

        private static float RaySphereNearest(Vector3 origin, Vector3 direction, float radius)
        {
            var a = Vector3.Dot(direction, direction);
            var b = 2f * Vector3.Dot(origin, direction);
            var c = Vector3.Dot(origin, origin) - radius * radius;
            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return -1f;
            }

            var root = Mathf.Sqrt(discriminant);
            var nearDistance = (-b - root) / (2f * a);
            var farDistance = (-b + root) / (2f * a);
            if (nearDistance < 0f && farDistance < 0f)
            {
                return -1f;
            }

            if (nearDistance < 0f)
            {
                return Mathf.Max(0f, farDistance);
            }

            if (farDistance < 0f)
            {
                return Mathf.Max(0f, nearDistance);
            }

            return Mathf.Max(0f, Mathf.Min(nearDistance, farDistance));
        }

        private static Vector3 ResolvePlanetCenterWorld(AtmospherePlanetTransformMode transformMode, float planetRadiusKm, float worldToKilometers, Vector3 anchorWorld, Vector3 explicitCenterWorld)
        {
            var radiusInWorldUnits =
                Mathf.Max(0.1f, planetRadiusKm) /
                Mathf.Max(0.000001f, worldToKilometers);
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
            var effectiveCoefficients = ResolveEffectiveCoefficients(settings);
            var densityProfile = ResolveDensityProfile(settings);
            return string.Concat(
                "Enabled=", settings.Enabled,
                " Rayleigh=", Format(settings.RayleighIntensity),
                " Mie=", Format(settings.MieIntensity),
                " g=", Format(settings.MieAnisotropy),
                " RayleighCoeff=", FormatColor(settings.RayleighScatteringCoefficient),
                " MieScatterCoeff=", FormatColor(settings.MieScatteringCoefficient),
                " MieAbsorbCoeff=", FormatColor(settings.MieAbsorptionCoefficient),
                " EffectiveRayleighKmInv=", FormatColor(effectiveCoefficients.RayleighScattering),
                " EffectiveMieScatterKmInv=", FormatColor(effectiveCoefficients.MieScattering),
                " EffectiveMieAbsorbKmInv=", FormatColor(effectiveCoefficients.MieAbsorption),
                " EffectiveMieExtinctionKmInv=", FormatColor(effectiveCoefficients.MieExtinction),
                " RadiusKm=", Format(settings.PlanetRadius),
                " HeightKm=", Format(settings.AtmosphereHeight),
                " RayleighScaleKm=", Format(settings.RayleighScaleHeight),
                " MieScaleKm=", Format(settings.MieScaleHeight),
                " RayleighDensityExpScale=", Format(densityProfile.RayleighDensityExpScale),
                " MieDensityExpScale=", Format(densityProfile.MieDensityExpScale),
                " PlanetTransform=", settings.PlanetTransformMode,
                " PlanetAnchorWS=", FormatVector(settings.PlanetAnchorWorld),
                " PlanetCenterWS=", FormatVector(settings.PlanetCenterWorld),
                " WorldToKm=", Format(settings.WorldToKilometers),
                " GroundAlbedo=", FormatColor(settings.GroundAlbedo),
                " Ozone=", Format(settings.OzoneAbsorptionIntensity),
                " OzoneCoeff=", FormatColor(settings.OzoneAbsorptionCoefficient),
                " EffectiveOzoneKmInv=", FormatColor(effectiveCoefficients.OzoneAbsorption),
                " OzoneLayerKm=", Format(settings.OzoneLayerCenter), "/", Format(settings.OzoneLayerThickness),
                " OzoneDensityProfile=", Format(densityProfile.OzoneDensity0LinearTerm), "/",
                    Format(densityProfile.OzoneDensity0ConstantTerm), "/",
                    Format(densityProfile.OzoneDensity1LinearTerm), "/",
                    Format(densityProfile.OzoneDensity1ConstantTerm),
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
                " PhysicalSkyTod=", Format(settings.PhysicalSkyTimeOfDayCurve), "@", PhysicalSkyTimeOfDayFormulaName,
                " PhysicalSkyMaterial=", settings.PhysicalSkyMaterial != null ? settings.PhysicalSkyMaterial.name : "BurtAtmosphereScattering",
                " PhysicalSkyMesh=", settings.PhysicalSkyMesh != null ? settings.PhysicalSkyMesh.name : "XRenderDefaultSkyMesh",
                " PhysicalSkyMeshPosition=", FormatVector(settings.PhysicalSkyMeshWorldPosition),
                " PhysicalSkyMeshUv=", PhysicalSkyMeshUvFormulaName,
                " SkyTint=", FormatColor(settings.SkyTint),
                " SkyLuminanceFactor=", FormatColor(settings.SkyLuminanceFactor),
                " HorizonColor=", FormatColor(settings.HorizonColor),
                " HorizonSunsetColor=", FormatColor(settings.HorizonSunsetColor),
                " GroundColor=", FormatColor(settings.GroundColor),
                " StylizedAnalyticFallbackBlend=", Format(settings.StylizedSky.Blend),
                " StylizedBaseDay=", FormatColor(settings.StylizedSky.BaseSkyColorDay),
                " StylizedBaseDawnDusk=", FormatColor(settings.StylizedSky.BaseSkyColorDawnDusk),
                " StylizedBaseNight=", FormatColor(settings.StylizedSky.BaseSkyColorNight),
                " StylizedHorizonDay=", FormatColor(settings.StylizedSky.HorizonSkyColorDay),
                " StylizedHorizonDawnDusk=", FormatColor(settings.StylizedSky.HorizonSkyColorDawnDusk),
                " StylizedHorizonNight=", FormatColor(settings.StylizedSky.HorizonSkyColorNight),
                " StylizedHorizon=", Format(settings.StylizedSky.HorizonBrightness), "/", Format(settings.StylizedSky.HorizonFalloff),
                " StylizedSunRise=", Format(settings.StylizedSky.SunRiseBlendMin), "/", Format(settings.StylizedSky.SunRiseBlendMax),
                " StylizedSunGlow=", FormatColor(settings.StylizedSky.SunGlowColor), "x", Format(settings.StylizedSky.SunGlowScale),
                " StylizedPhysicalSkyContract=", PhysicalSkyStylizedFormulaName,
                " Moon=", settings.Moon.Enabled,
                " MoonTexture=", settings.Moon.SurfaceTexture != null ? settings.Moon.SurfaceTexture.name : "WhiteFallback",
                " MoonPhaseNormal=", settings.Moon.PhaseNormalTexture != null ? settings.Moon.PhaseNormalTexture.name : "WhiteDummy",
                " MoonRotation=", FormatVector(settings.Moon.RotationEuler),
                " MoonIlluminance=", Format(settings.Moon.Intensity),
                " MoonAngularDiameterDeg=", Format(settings.Moon.AngularDiameter),
                " MoonAdditionalTint=", FormatColor(settings.Moon.AdditionalTint),
                " MoonPhase=", Format(settings.Moon.Phase), "@", Format(settings.Moon.PhaseRotation),
                " MoonEarthshineSharpness=", Format(settings.Moon.Earthshine), "/", Format(settings.Moon.PhaseSharpness),
                " MoonFlare=", Format(settings.Moon.FlareSize), "/", Format(settings.Moon.FlareFalloff),
                " MoonBloom=", Format(settings.Moon.LightBloomIntensity), "/", Format(settings.Moon.LightBloomSize), "/", Format(settings.Moon.LightBloomFalloff), "/", Format(settings.Moon.LightBloomEdgeAlpha),
                " MoonRiseUploadOnly=", Format(settings.Moon.RiseBlendMin), "/", Format(settings.Moon.RiseBlendMax),
                " MoonFormula=", MoonDiskFormulaName,
                " Stars=", settings.Stars.Enabled,
                " StarsTexture=", settings.Stars.StarsTexture != null ? settings.Stars.StarsTexture.name : "BlackFallback",
                " StarsTintTexture=", settings.Stars.TintColorTexture != null ? settings.Stars.TintColorTexture.name : "WhiteFallback",
                " StarsIntensity=", Format(settings.Stars.Intensity),
                " StarsRotationRad=", Format(settings.Stars.Rotation),
                " StarsTwinkle=", Format(settings.Stars.TwinkleStrength), "@", Format(settings.Stars.TwinkleSpeed),
                " AreaStars=", Format(settings.Stars.AreaIntensity),
                " GalaxyCloud=", Format(settings.Stars.GalaxyCloudIntensity),
                " GalaxyStars=", Format(settings.Stars.GalaxyStarIntensity),
                " CustomStar=", settings.Stars.CustomStarTexture != null ? settings.Stars.CustomStarTexture.name : "BlackFallback",
                " CustomStarRotation=", Format(settings.Stars.CustomStarRotation),
                " CustomStarScatter=", Format(settings.Stars.CustomStarScatterSpeed), "/", Format(settings.Stars.CustomStarScatterInterval),
                " StarsFormula=", StarFieldFormulaName,
                " PhysicalSkyAnimationTime=", PhysicalSkyAnimationTimeFormulaName,
                " CelestialUploadOnlyContract=", PhysicalSkyCelestialUploadOnlyFormulaName,
                " PhysicalSkyPlatformContract=", PhysicalSkyPlatformFormulaName,
                " PhysicalSkyTextureSamplingContract=", PhysicalSkyTextureSamplingFormulaName,
                " PhysicalSkyTextureBindingContract=", PhysicalSkyTextureBindingFormulaName,
                " PhysicalSkyAuthoredDefaultsContract=", PhysicalSkyAuthoredDefaultsFormulaName,
                " AtmosphereColorSpaceContract=", AtmosphereColorSpaceFormulaName,
                " PanoramicCloud=", settings.PanoramicClouds.Enabled,
                " PanoramicCloudMode=", settings.PanoramicClouds.UseDefaultTexture ? "DefaultTexture" : "WeatherTexture",
                " PanoramicCloudDefault=", settings.PanoramicClouds.DefaultTexture != null ? settings.PanoramicClouds.DefaultTexture.name : "BlackFallback",
                " PanoramicCloudPrevious=", settings.PanoramicClouds.PreviousWeatherTexture != null ? settings.PanoramicClouds.PreviousWeatherTexture.name : "BlackFallback",
                " PanoramicCloudCurrent=", settings.PanoramicClouds.CurrentWeatherTexture != null ? settings.PanoramicClouds.CurrentWeatherTexture.name : "BlackFallback",
                " PanoramicCloudTransition=", settings.PanoramicClouds.TextureInTransition, "@", Format(settings.PanoramicClouds.TextureTransition),
                " PanoramicCloudLuminance=", Format(settings.PanoramicClouds.SunnyLuminance), "/", Format(settings.PanoramicClouds.NightLuminance),
                " PanoramicCloudAlpha=", Format(settings.PanoramicClouds.Alpha),
                " PanoramicCloudFormula=", PanoramicCloudFormulaName,
                " PhysicalSkyDesaturationForce=", settings.PhysicalSkyDesaturation.ForceEnabled,
                " PhysicalSkyDesaturationEffect=", Format(settings.PhysicalSkyDesaturation.Effect),
                " PhysicalSkyDesaturationBlend=", Format(settings.PhysicalSkyDesaturation.Blend),
                " PhysicalSkyDesaturationColor=", FormatColor(settings.PhysicalSkyDesaturation.Color),
                " PhysicalSkyDesaturationIntensity=", Format(settings.PhysicalSkyDesaturation.SkyIntensity),
                " PhysicalSkyCloudDesaturationIntensity=", Format(settings.PhysicalSkyDesaturation.CloudIntensity),
                " PhysicalSkyDesaturationFormula=", PhysicalSkyDesaturationFormulaName,
                " WeatherSky=", settings.Weather.Enabled,
                " WeatherSkyTexture=", settings.Weather.CoverageTexture != null ? settings.Weather.CoverageTexture.name : "WhiteFallback",
                " WeatherRain=", Format(settings.Weather.RainIntensity), "/", Format(settings.Weather.RainWetCoverage),
                " WeatherSnow=", Format(settings.Weather.SnowIntensity), "/", Format(settings.Weather.SnowCoverage),
                " WeatherShadowMarch=", Format(settings.Weather.CloudShadowMarchDistance),
                " WeatherSkyFormula=", WeatherSkyFormulaName,
                " WeatherLightingPropagationFormula=", WeatherLightingPropagationFormulaName,
                " AtmospherePhaseFormula=", AtmospherePhaseFormulaName,
                " AtmosphereLutSamplingFormula=", AtmosphereLutSamplingFormulaName,
                " AtmosphereTransmittanceTransformFormula=", AtmosphereTransmittanceTransformFormulaName,
                " AtmosphereEffectiveCoefficientFormula=", AtmosphereEffectiveCoefficientFormulaName,
                " AtmosphereDensityProfileFormula=", AtmosphereDensityProfileFormulaName,
                " AtmospherePlanetGeometryFormula=", AtmospherePlanetGeometryFormulaName,
                " AtmosphereLayerIntersectionFormula=", AtmosphereLayerIntersectionFormulaName,
                " AtmosphereFogReprojectionFormula=", AtmosphereFogReprojectionFormulaName,
                " AtmosphereFogConsumerFormula=", AtmosphereFogConsumerFormulaName,
                " AtmosphereLutOpticalDepthFormula=", AtmosphereLutOpticalDepthFormulaName,
                " AtmosphereMultipleScatteringFormula=", AtmosphereMultipleScatteringFormulaName,
                " AtmosphereVariableSamplingFormula=", AtmosphereVariableSamplingFormulaName,
                " AtmosphereSkyViewTransformFormula=", AtmosphereSkyViewTransformFormulaName,
                " AtmosphereHorizontalScatteringFormula=", AtmosphereHorizontalScatteringFormulaName,
                " AtmosphereSkyViewConsumerFormula=", AtmosphereSkyViewConsumerFormulaName,
                " Aerial=", settings.AerialPerspectiveEnabled,
                " AerialDensityScale=", Format(settings.AerialPerspectiveDensityScale),
                " AerialLuminanceScale=", Format(settings.AerialPerspectiveLuminanceScale),
                " AerialSamplingDistanceScale=", Format(settings.AerialPerspectiveSamplingDistanceScale),
                " AerialFallbackIntensity=", Format(settings.AerialPerspectiveIntensity),
                " AerialFallbackDistance=", Format(settings.AerialPerspectiveDistance),
                " AerialLutCoverageKm=", Format(BurtAtmosphereLutUtility.FogLutCoverageKm),
                " AerialHeightFalloff=", Format(settings.AerialPerspectiveHeightFalloff),
                " AerialStartDepth=", Format(settings.AerialPerspectiveStartDepth),
                " AerialSmoothStartEnd=", Format(settings.AerialPerspectiveNearFadeEnd),
                " AerialMaxOpacity=", Format(settings.AerialPerspectiveMaxOpacity),
                " AerialPlacement=", settings.AerialPerspectivePlacement,
                " FogInteraction=", settings.FogInteraction,
                " AerialTint=", FormatColor(settings.AerialPerspectiveTint),
                " CombineContract=", AtmosphereCombineFormulaName,
                " CombineTopologyContract=", AtmosphereCombineTopologyFormulaName,
                " CombinePermutationContract=", AtmosphereCombinePermutationFormulaName,
                " SkyCaptureFormula=", SkyCaptureFormulaName,
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
                " Mobile=", IsMobileAtmospherePlatform,
                " FogLutSupported=", SupportsAtmosphereFogLut,
                " LutResourceContract=", AtmosphereLutResourceFormulaName,
                " AsyncComputeContract=", AtmosphereAsyncComputeFormulaName,
                " ResourcesAllowed=", ShouldUseAtmosphereResources(request),
                " HasMainLight=", request.LightingData != null && request.LightingData.HasMainLight,
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
                " CombineContract=", AtmosphereCombineFormulaName,
                " Formula=", AerialFormulaName,
                " Projection=", AerialProjectionFormulaName,
                " Phase=", AtmospherePhaseFormulaName,
                " LutSampling=", AtmosphereLutSamplingFormulaName,
                " OpticalDepth=", AtmosphereLutOpticalDepthFormulaName,
                " MultipleScattering=", AtmosphereMultipleScatteringFormulaName,
                " VariableSampling=", AtmosphereVariableSamplingFormulaName,
                " SkyViewTransform=", AtmosphereSkyViewTransformFormulaName,
                " HorizontalScattering=", AtmosphereHorizontalScatteringFormulaName,
                " DensityScale=", Format(settings.AerialPerspectiveDensityScale),
                " LuminanceScale=", Format(settings.AerialPerspectiveLuminanceScale),
                " SamplingDistanceScale=", Format(settings.AerialPerspectiveSamplingDistanceScale),
                " FallbackIntensity=", Format(settings.AerialPerspectiveIntensity),
                " FallbackDistance=", Format(settings.AerialPerspectiveDistance),
                " LutCoverageKm=", Format(BurtAtmosphereLutUtility.FogLutCoverageKm),
                " FallbackHeightFalloff=", Format(settings.AerialPerspectiveHeightFalloff),
                " StartDepth=", Format(settings.AerialPerspectiveStartDepth),
                " FallbackSmoothStartEnd=", Format(settings.AerialPerspectiveNearFadeEnd),
                " FallbackMaxOpacity=", Format(settings.AerialPerspectiveMaxOpacity),
                " FallbackTint=", FormatColor(settings.AerialPerspectiveTint));
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
        private const int BuiltInAtmosphereCubemapPass = 2;
        private const int CustomAtmosphereCubemapPass = 0;

        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_BurtAtmosphereInverseViewProjection");
        private static readonly int SkyMeshViewProjectionId = Shader.PropertyToID("_BurtAtmosphereSkyMeshViewProjection");
        private static readonly int ProceduralSkyId = Shader.PropertyToID("_BurtAtmosphereProceduralSky");
        private static readonly int CameraPositionWSId = Shader.PropertyToID("_BurtAtmosphereCameraPositionWS");
        private static readonly Vector4 DefaultHDRDecodeValues = new Vector4(1f, 1f, 0f, 0f);

        private static RenderTexture atmosphereCubemap;
        private static Material material;
        private static MaterialPropertyBlock skyMeshProperties;
        private static bool hasLoggedMissingShader;
        private static int lastRenderedFrame = -1;
        private static int lastCaptureOwnerId;
        private static Vector3 lastCaptureOrigin;
        private static string lastCaptureSource = "Unavailable";
        private static bool lastCaptureEveryFrame;
        private static int lastCaptureRequestVersion;
        private static int contentVersion;

        public static bool TryGetReflection(CommandBuffer cmd, BurtRenderRequest request, out Texture texture, out Vector4 hdrDecodeValues, out string source)
        {
            var camera = request != null ? request.Camera : null;
            var captureOrigin = camera != null ? camera.transform.position : Vector3.zero;
            var captureOwnerId = camera != null ? camera.GetInstanceID() : 0;
            return TryGetReflection(
                cmd,
                request,
                captureOrigin,
                captureOwnerId,
                "Camera",
                true,
                0,
                out texture,
                out hdrDecodeValues,
                out source);
        }

        public static bool TryGetReflection(
            CommandBuffer cmd,
            BurtRenderRequest request,
            Vector3 captureOrigin,
            int captureOwnerId,
            string captureSource,
            bool captureEveryFrame,
            int captureRequestVersion,
            out Texture texture,
            out Vector4 hdrDecodeValues,
            out string source)
        {
            texture = null;
            hdrDecodeValues = DefaultHDRDecodeValues;
            source = "BurtAtmosphereReflectionUnavailable";

            if (cmd == null || request == null || !BurtAtmosphereUtility.ShouldUseAtmosphereResources(request))
            {
                return false;
            }

            var camera = request.Camera;
            if (camera == null)
            {
                return false;
            }

            captureOrigin = SanitizeCaptureOrigin(captureOrigin, camera.transform.position);
            if (captureOwnerId == 0)
            {
                captureOwnerId = camera.GetInstanceID();
            }

            var safeCaptureSource = string.IsNullOrEmpty(captureSource)
                ? "Camera"
                : captureSource;
            var resolvedSource = safeCaptureSource == "Camera"
                ? "BurtAtmosphereCubemap"
                : "BurtAtmosphereCubemap(" + safeCaptureSource + ")";
            var settings = BurtAtmosphereUtility.ResolveSettings();
            if (!settings.Enabled)
            {
                return false;
            }

            var skyMesh = BurtDrawAtmospherePass.ResolveSkyMesh(settings);
            var drawMaterial = settings.PhysicalSkyMaterial != null
                ? settings.PhysicalSkyMaterial
                : BurtDrawAtmospherePass.CreateMaterial(ref material, ref hasLoggedMissingShader);
            if (drawMaterial == null)
            {
                source = "BurtAtmosphereReflectionMissingShader";
                return false;
            }

            var requiredPassCount = settings.PhysicalSkyMaterial != null
                ? CustomAtmosphereCubemapPass + 1
                : BuiltInAtmosphereCubemapPass + 1;
            if (!BurtDrawAtmospherePass.IsMaterialSupported(
                    drawMaterial,
                    requiredPassCount))
            {
                source = "BurtAtmosphereReflectionUnsupportedShader";
                return false;
            }

            EnsureTexture();
            if (atmosphereCubemap == null)
            {
                source = "BurtAtmosphereReflectionAllocationFailed";
                return false;
            }

            var currentFrame = Time.frameCount;
            var captureRequired =
                contentVersion == 0 ||
                lastCaptureOwnerId != captureOwnerId ||
                lastCaptureOrigin != captureOrigin ||
                lastCaptureRequestVersion != captureRequestVersion ||
                (captureEveryFrame && lastRenderedFrame != currentFrame);
            if (captureRequired)
            {
                var sunDirection4 = BurtDrawAtmospherePass.ResolveSunDirection(request, settings);
                // XRender prepares the shared atmosphere LUTs from the render
                // request camera, then substitutes the SkyLight per-face camera
                // table only for Atmosphere Combine. Preserve that split here:
                // LUT state stays request-relative while the property block below
                // overrides capture position and face matrices for the cubemap.
                BurtAtmosphereLutUtility.EnsureLuts(cmd, camera, settings, new Vector3(sunDirection4.x, sunDirection4.y, sunDirection4.z));
                BurtDrawAtmospherePass.UploadMaterialProperties(drawMaterial, camera, request, settings);
                BurtDrawAtmospherePass.PreparePermutation(cmd, settings, true);
                if (skyMeshProperties == null)
                {
                    skyMeshProperties = new MaterialPropertyBlock();
                }

                var skyMeshLocalToWorld = BurtDrawAtmospherePass.ResolveSkyMeshLocalToWorld(settings);
                var useProceduralFallback = skyMesh == null;
                var materialPass = settings.PhysicalSkyMaterial != null
                    ? CustomAtmosphereCubemapPass
                    : BuiltInAtmosphereCubemapPass;
                for (var face = 0; face < CubemapFaceCount; face++)
                {
                    cmd.SetRenderTarget(new RenderTargetIdentifier(atmosphereCubemap, 0, (CubemapFace)face));
                    cmd.SetViewport(new Rect(0f, 0f, CubemapSize, CubemapSize));
                    cmd.ClearRenderTarget(false, true, Color.clear);
                    skyMeshProperties.Clear();
                    var faceViewProjection = BuildCubemapFaceViewProjection(
                        captureOrigin,
                        (CubemapFace)face);
                    skyMeshProperties.SetMatrix(SkyMeshViewProjectionId, faceViewProjection);
                    skyMeshProperties.SetMatrix(InverseViewProjectionId, faceViewProjection.inverse);
                    skyMeshProperties.SetFloat(ProceduralSkyId, useProceduralFallback ? 1f : 0f);
                    skyMeshProperties.SetVector(CameraPositionWSId, captureOrigin);
                    if (useProceduralFallback)
                    {
                        cmd.DrawProcedural(
                            Matrix4x4.identity,
                            drawMaterial,
                            materialPass,
                            MeshTopology.Triangles,
                            3,
                            1,
                            skyMeshProperties);
                    }
                    else
                    {
                        cmd.DrawMesh(
                            skyMesh,
                            skyMeshLocalToWorld,
                            drawMaterial,
                            0,
                            materialPass,
                            skyMeshProperties);
                    }
                }

                lastRenderedFrame = currentFrame;
                lastCaptureOwnerId = captureOwnerId;
                lastCaptureOrigin = captureOrigin;
                lastCaptureSource = resolvedSource;
                lastCaptureEveryFrame = captureEveryFrame;
                lastCaptureRequestVersion = captureRequestVersion;
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
            source = resolvedSource;
            return true;
        }

        private static Vector3 SanitizeCaptureOrigin(Vector3 captureOrigin, Vector3 fallback)
        {
            return IsFinite(captureOrigin.x) &&
                   IsFinite(captureOrigin.y) &&
                   IsFinite(captureOrigin.z)
                ? captureOrigin
                : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static Matrix4x4 BuildCubemapFaceViewProjection(
            Vector3 origin,
            CubemapFace face)
        {
            Vector3 right;
            Vector3 up;
            Vector3 forward;
            switch (face)
            {
                case CubemapFace.PositiveX:
                    right = Vector3.back;
                    up = Vector3.down;
                    forward = Vector3.right;
                    break;
                case CubemapFace.NegativeX:
                    right = Vector3.forward;
                    up = Vector3.down;
                    forward = Vector3.left;
                    break;
                case CubemapFace.PositiveY:
                    right = Vector3.right;
                    up = Vector3.forward;
                    forward = Vector3.up;
                    break;
                case CubemapFace.NegativeY:
                    right = Vector3.right;
                    up = Vector3.back;
                    forward = Vector3.down;
                    break;
                case CubemapFace.PositiveZ:
                    right = Vector3.right;
                    up = Vector3.down;
                    forward = Vector3.forward;
                    break;
                default:
                    right = Vector3.left;
                    up = Vector3.down;
                    forward = Vector3.back;
                    break;
            }

            var viewProjection = Matrix4x4.zero;
            viewProjection.SetRow(
                0,
                new Vector4(right.x, right.y, right.z, -Vector3.Dot(right, origin)));
            viewProjection.SetRow(
                1,
                new Vector4(up.x, up.y, up.z, -Vector3.Dot(up, origin)));
            viewProjection.SetRow(
                3,
                new Vector4(forward.x, forward.y, forward.z, -Vector3.Dot(forward, origin)));
            return viewProjection;
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
            texture = BurtAtmosphereUtility.ShouldUseAtmosphereResources(request) ? atmosphereCubemap : null;
            if (texture == null)
            {
                return false;
            }

            source = string.IsNullOrEmpty(lastCaptureSource)
                ? "BurtAtmosphereCubemap"
                : lastCaptureSource;
            return true;
        }

        public static string FormatDebugState()
        {
            return string.Concat(
                "Cubemap=", atmosphereCubemap != null && atmosphereCubemap.IsCreated() ? "Ready" : "Unavailable",
                " Size=", CubemapSize,
                " LastRenderedFrame=", lastRenderedFrame,
                " LastCaptureOwner=", lastCaptureOwnerId,
                " LastCaptureOrigin=", lastCaptureOrigin,
                " LastCaptureSource=", lastCaptureSource,
                " LastCaptureMode=", lastCaptureEveryFrame ? "EveryFrame" : "OnDemand",
                " LastCaptureRequestVersion=", lastCaptureRequestVersion,
                " ContentVersion=", contentVersion,
                " CaptureFormula=", BurtAtmosphereUtility.SkyCaptureFormulaName,
                " CaptureLifecycle=", BurtAtmosphereUtility.AtmosphereReflectionCaptureFormulaName,
                " CaptureColorSpace=SceneLinearUnPreExposed",
                " MeshUvContract=", BurtAtmosphereUtility.PhysicalSkyMeshUvFormulaName,
                " CombineContract=", BurtAtmosphereUtility.AtmosphereCombineFormulaName,
                " BuiltInMaterial=", material != null ? "Ready" : "Unavailable",
                " CustomMaterial=", BurtAtmosphereUtility.ResolveSettings().PhysicalSkyMaterial != null
                    ? BurtAtmosphereUtility.ResolveSettings().PhysicalSkyMaterial.name
                    : "None");
        }

        public static void Release()
        {
            ReleaseTexture(atmosphereCubemap);
            atmosphereCubemap = null;
            lastRenderedFrame = -1;
            lastCaptureOwnerId = 0;
            lastCaptureOrigin = Vector3.zero;
            lastCaptureSource = "Unavailable";
            lastCaptureEveryFrame = false;
            lastCaptureRequestVersion = 0;
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
            lastCaptureOwnerId = 0;
            lastCaptureOrigin = Vector3.zero;
            lastCaptureSource = "Unavailable";
            lastCaptureEveryFrame = false;
            lastCaptureRequestVersion = 0;
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
