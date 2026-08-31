using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public readonly struct BurtVolumetricFogDebugSnapshot
    {
        public readonly bool Enabled;
        public readonly float VisibleDistance;
        public readonly float StartDistance;
        public readonly int StepCount;
        public readonly bool Jitter;
        public readonly float Height;
        public readonly float Density;
        public readonly float HeightFalloff;
        public readonly float SecondLayerHeightOffset;
        public readonly float SecondLayerDensity;
        public readonly float SecondLayerHeightFalloff;
        public readonly float ExtinctionScale;
        public readonly float MaxOpacity;
        public readonly Color Albedo;
        public readonly float Anisotropy;
        public readonly float DirectIntensity;
        public readonly float AmbientIntensity;
        public readonly float FarStylizedFactor;
        public readonly BurtVolumetricFogShadowSourceMode ShadowSourceMode;
        public readonly bool FogMapEnabled;
        public readonly Texture2D FogMapTexture;
        public readonly Vector2 FogMapCenterXZ;
        public readonly Vector2 FogMapCoverageXZ;
        public readonly float FogMapMinAltitude;
        public readonly float FogMapMaxAltitude;
        public readonly bool SkyAmbientSHAvailable;
        public readonly bool TranslucencyGIActive;
        public readonly string Formula;

        internal BurtVolumetricFogDebugSnapshot(BurtVolumetricFogSettings settings, string formula)
        {
            Enabled = settings.Enabled;
            VisibleDistance = settings.VisibleDistance;
            StartDistance = settings.StartDistance;
            StepCount = settings.StepCount;
            Jitter = settings.Jitter;
            Height = settings.Height;
            Density = settings.Density;
            HeightFalloff = settings.HeightFalloff;
            SecondLayerHeightOffset = settings.SecondLayerHeightOffset;
            SecondLayerDensity = settings.SecondLayerDensity;
            SecondLayerHeightFalloff = settings.SecondLayerHeightFalloff;
            ExtinctionScale = settings.ExtinctionScale;
            MaxOpacity = settings.MaxOpacity;
            Albedo = settings.Albedo;
            Anisotropy = settings.Anisotropy;
            DirectIntensity = settings.DirectIntensity;
            AmbientIntensity = settings.AmbientIntensity;
            FarStylizedFactor = settings.FarStylizedFactor;
            ShadowSourceMode = settings.ShadowSourceMode;
            FogMapEnabled = settings.FogMap.Enabled;
            FogMapTexture = settings.FogMap.Texture;
            FogMapCenterXZ = settings.FogMap.CenterXZ;
            FogMapCoverageXZ = settings.FogMap.CoverageXZ;
            FogMapMinAltitude = settings.FogMap.MinAltitude;
            FogMapMaxAltitude = settings.FogMap.MaxAltitude;
            SkyAmbientSHAvailable = BurtVolumetricFogUtility.IsSkyAmbientSHAvailable();
            TranslucencyGIActive = BurtVolumetricFogIntegratedUtility.IsTranslucencyGIActive;
            Formula = string.IsNullOrEmpty(formula) ? BurtVolumetricFogUtility.FormulaName : formula;
        }
    }

    public static class BurtVolumetricFogDebugUtility
    {
        public static BurtVolumetricFogDebugSnapshot GetSnapshot()
        {
            return new BurtVolumetricFogDebugSnapshot(BurtVolumetricFogUtility.ResolveSettings(), BurtVolumetricFogUtility.FormulaName);
        }

        public static string FormatDebugState()
        {
            return BurtVolumetricFogUtility.FormatDebugState();
        }
    }

    internal readonly struct BurtVolumetricFogMapSettings
    {
        public readonly bool Enabled;
        public readonly Texture2D Texture;
        public readonly Vector2 CenterXZ;
        public readonly Vector2 CoverageXZ;
        public readonly float MinAltitude;
        public readonly float MaxAltitude;

        public BurtVolumetricFogMapSettings(
            bool enabled,
            Texture texture,
            Vector2 centerXZ,
            Vector2 coverageXZ,
            float minAltitude,
            float maxAltitude)
        {
            Texture = texture as Texture2D;
            CenterXZ = centerXZ;
            CoverageXZ = new Vector2(
                Mathf.Max(Mathf.Abs(coverageXZ.x), 1f),
                Mathf.Max(Mathf.Abs(coverageXZ.y), 1f));
            MinAltitude = Mathf.Min(minAltitude, maxAltitude);
            MaxAltitude = Mathf.Max(minAltitude, maxAltitude);
            Enabled = enabled && Texture != null;
        }

        public static BurtVolumetricFogMapSettings Disabled => new BurtVolumetricFogMapSettings(
            false,
            null,
            Vector2.zero,
            new Vector2(4096f, 4096f),
            -200f,
            500f);
    }

    internal readonly struct BurtVolumetricFogSettings
    {
        public readonly bool Enabled;
        public readonly float VisibleDistance;
        public readonly float StartDistance;
        public readonly int StepCount;
        public readonly bool Jitter;
        public readonly float Height;
        public readonly float Density;
        public readonly float HeightFalloff;
        public readonly float SecondLayerHeightOffset;
        public readonly float SecondLayerDensity;
        public readonly float SecondLayerHeightFalloff;
        public readonly float ExtinctionScale;
        public readonly float MaxOpacity;
        public readonly Color Albedo;
        public readonly float Anisotropy;
        public readonly float DirectIntensity;
        public readonly float AmbientIntensity;
        public readonly float FarStylizedFactor;
        public readonly BurtVolumetricFogShadowSourceMode ShadowSourceMode;
        public readonly BurtVolumetricFogMapSettings FogMap;
        public readonly BurtAtmosphereHorizontalFogSettings HorizontalScattering;

        public BurtVolumetricFogSettings(
            bool enabled,
            float visibleDistance,
            float startDistance,
            int stepCount,
            bool jitter,
            float height,
            float density,
            float heightFalloff,
            float secondLayerHeightOffset,
            float secondLayerDensity,
            float secondLayerHeightFalloff,
            float extinctionScale,
            float maxOpacity,
            Color albedo,
            float anisotropy,
            float directIntensity,
            float ambientIntensity,
            float farStylizedFactor,
            BurtVolumetricFogShadowSourceMode shadowSourceMode,
            BurtVolumetricFogMapSettings fogMap,
            BurtAtmosphereHorizontalFogSettings horizontalScattering)
        {
            VisibleDistance = Mathf.Max(1f, visibleDistance);
            StartDistance = Mathf.Max(0f, startDistance);
            StepCount = Mathf.Clamp(stepCount, 4, 96);
            Jitter = jitter;
            Height = height;
            Density = Mathf.Clamp(density, 0f, 1f);
            HeightFalloff = Mathf.Clamp(heightFalloff, 0.001f, 4f);
            SecondLayerHeightOffset = secondLayerHeightOffset;
            SecondLayerDensity = Mathf.Clamp(secondLayerDensity, 0f, 1f);
            SecondLayerHeightFalloff = Mathf.Clamp(secondLayerHeightFalloff, 0f, 4f);
            ExtinctionScale = Mathf.Clamp(extinctionScale, 0.01f, 10f);
            MaxOpacity = Mathf.Clamp01(maxOpacity);
            Albedo = albedo;
            Anisotropy = Mathf.Clamp(anisotropy, -0.9f, 0.9f);
            DirectIntensity = Mathf.Max(0f, directIntensity);
            AmbientIntensity = Mathf.Max(0f, ambientIntensity);
            FarStylizedFactor = Mathf.Clamp01(farStylizedFactor);
            ShadowSourceMode = shadowSourceMode == BurtVolumetricFogShadowSourceMode.ShadowReference
                ? BurtVolumetricFogShadowSourceMode.ShadowReference
                : BurtVolumetricFogShadowSourceMode.RuntimeFilteredShadow;
            FogMap = fogMap;
            HorizontalScattering = horizontalScattering;
            Enabled = enabled && VisibleDistance > 0.001f &&
                (Density > 0.000001f || SecondLayerDensity > 0.000001f || FogMap.Enabled) &&
                ExtinctionScale > 0.000001f && MaxOpacity > 0.000001f;
        }

        public static BurtVolumetricFogSettings Disabled => new BurtVolumetricFogSettings(
            false, 300f, 0f, 24, false, 0f, 0f, 0.15f,
            0f, 0f, 0f,
            1f, 0f,
            Color.white, 0f, 0f, 0f,
            0.2f, BurtVolumetricFogShadowSourceMode.RuntimeFilteredShadow,
            BurtVolumetricFogMapSettings.Disabled,
            BurtAtmosphereHorizontalFogSettings.Disabled);
    }

    internal static class BurtVolumetricFogUtility
    {
        public const string ShaderName = "Hidden/BurtRP/VolumetricFog";
        public const string FormulaName = "XRenderPunctualClusterFalloffModeCellBiasTranslucencyGISH2SkySHL1DualLayerFogMapLocalMaterialTemporalFarStylizedShadowSourceFurthestHiZR16Thread8x8x1Halton16SafeLightBuffersV16";
        private static readonly int AmbientSHEnabledId = Shader.PropertyToID("_BurtAmbientSHEnabled");

        internal static bool IsSkyAmbientSHAvailable()
        {
            return Shader.GetGlobalFloat(AmbientSHEnabledId) > 0.5f;
        }

        public static bool ShouldUseVolumetricFog(BurtRenderRequest request)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview ||
                request.Type == BurtRenderRequestType.Reflection ||
                request.Type == BurtRenderRequestType.UICamera)
            {
                return false;
            }

            return ResolveSettings().Enabled;
        }

        public static BurtVolumetricFogSettings ResolveSettings()
        {
            var stack = VolumeManager.instance != null ? VolumeManager.instance.stack : null;
            var fog = stack != null ? stack.GetComponent<VolumetricFogVolumeComponent>() : null;
            if (fog == null || !fog.IsEnabled())
            {
                return BurtVolumetricFogSettings.Disabled;
            }

            return new BurtVolumetricFogSettings(
                true,
                fog.visibleDistance.value,
                fog.startDistance.value,
                fog.stepCount.value,
                fog.jitter.value,
                fog.height.value,
                fog.density.value,
                fog.heightFalloff.value,
                fog.secondLayerHeightOffset.value,
                fog.secondLayerDensity.value,
                fog.secondLayerHeightFalloff.value,
                fog.extinctionScale.value,
                fog.maxOpacity.value,
                fog.albedo.value,
                fog.anisotropy.value,
                fog.directIntensity.value,
                fog.ambientIntensity.value,
                fog.farStylizedFactor.value,
                fog.shadowSourceMode.value,
                new BurtVolumetricFogMapSettings(
                    fog.useFogMap.value,
                    fog.fogMap.value,
                    fog.fogMapCenterXZ.value,
                    fog.fogMapCoverageXZ.value,
                    fog.fogMapMinAltitude.value,
                    fog.fogMapMaxAltitude.value),
                new BurtAtmosphereHorizontalFogSettings(
                    fog.useAtmosphereHorizontalScattering.value,
                    fog.atmosphereRayleighTint.value,
                    fog.atmosphereRayleighScale.value,
                    fog.atmosphereMieTint.value,
                    fog.atmosphereMieScale.value,
                    fog.atmosphereMultipleScatteringTint.value,
                    fog.atmosphereMultipleScatteringScale.value));
        }

        public static string FormatDebugState(BurtRenderRequest request)
        {
            var settings = ResolveSettings();
            return string.Concat(
                "Requested=", ShouldUseVolumetricFog(request),
                " Enabled=", settings.Enabled,
                " VisibleDistance=", Format(settings.VisibleDistance),
                " Start=", Format(settings.StartDistance),
                " Steps=", settings.StepCount,
                " Jitter=", settings.Jitter,
                " Height=", Format(settings.Height),
                " Density=", Format(settings.Density),
                " Falloff=", Format(settings.HeightFalloff),
                " SecondHeightOffset=", Format(settings.SecondLayerHeightOffset),
                " SecondHeight=", Format(settings.Height + settings.SecondLayerHeightOffset),
                " SecondDensity=", Format(settings.SecondLayerDensity),
                " SecondFalloff=", Format(settings.SecondLayerHeightFalloff),
                " ExtinctionScale=", Format(settings.ExtinctionScale),
                " MaxOpacity=", Format(settings.MaxOpacity),
                " Albedo=", FormatColor(settings.Albedo),
                " Anisotropy=", Format(settings.Anisotropy),
                " Direct=", Format(settings.DirectIntensity),
                " Ambient=", Format(settings.AmbientIntensity),
                " FarStylized=", Format(settings.FarStylizedFactor),
                " ShadowSource=", settings.ShadowSourceMode,
                " PunctualDistanceBias=FroxelCellRadiusMin1m",
                " PunctualFalloff=PerLightInverseSquaredOrLinear",
                " AmbientModel=TGI_SH2->SkySH_L0L1",
                " AmbientSHAvailable=", IsSkyAmbientSHAvailable(),
                " FogMap=", settings.FogMap.Enabled,
                " FogMapTexture=", settings.FogMap.Texture != null ? settings.FogMap.Texture.name : "None",
                " FogMapCenter=", FormatVector2(settings.FogMap.CenterXZ),
                " FogMapCoverage=", FormatVector2(settings.FogMap.CoverageXZ),
                " FogMapAltitude=", Format(settings.FogMap.MinAltitude), "..", Format(settings.FogMap.MaxAltitude),
                " AtmosphereHS=", settings.HorizontalScattering.Enabled,
                " HSRayleigh=", FormatColor(settings.HorizontalScattering.RayleighTint), "*", Format(settings.HorizontalScattering.RayleighScale),
                " HSMie=", FormatColor(settings.HorizontalScattering.MieTint), "*", Format(settings.HorizontalScattering.MieScale),
                " HSMultiple=", FormatColor(settings.HorizontalScattering.MultipleScatteringTint), "*", Format(settings.HorizontalScattering.MultipleScatteringScale),
                " Integrated=", BurtVolumetricFogIntegratedUtility.FormatDebugState(),
                " Transparent=True TotalOrder=VFThenHFThenAF",
                " Formula=", FormulaName);
        }

        public static string FormatDebugState()
        {
            var settings = ResolveSettings();
            return string.Concat(
                "Enabled=", settings.Enabled,
                " VisibleDistance=", Format(settings.VisibleDistance),
                " Start=", Format(settings.StartDistance),
                " Steps=", settings.StepCount,
                " Jitter=", settings.Jitter,
                " Height=", Format(settings.Height),
                " Density=", Format(settings.Density),
                " Falloff=", Format(settings.HeightFalloff),
                " SecondHeightOffset=", Format(settings.SecondLayerHeightOffset),
                " SecondHeight=", Format(settings.Height + settings.SecondLayerHeightOffset),
                " SecondDensity=", Format(settings.SecondLayerDensity),
                " SecondFalloff=", Format(settings.SecondLayerHeightFalloff),
                " ExtinctionScale=", Format(settings.ExtinctionScale),
                " MaxOpacity=", Format(settings.MaxOpacity),
                " Albedo=", FormatColor(settings.Albedo),
                " Anisotropy=", Format(settings.Anisotropy),
                " Direct=", Format(settings.DirectIntensity),
                " Ambient=", Format(settings.AmbientIntensity),
                " FarStylized=", Format(settings.FarStylizedFactor),
                " ShadowSource=", settings.ShadowSourceMode,
                " PunctualDistanceBias=FroxelCellRadiusMin1m",
                " PunctualFalloff=PerLightInverseSquaredOrLinear",
                " AmbientModel=TGI_SH2->SkySH_L0L1",
                " AmbientSHAvailable=", IsSkyAmbientSHAvailable(),
                " FogMap=", settings.FogMap.Enabled,
                " FogMapTexture=", settings.FogMap.Texture != null ? settings.FogMap.Texture.name : "None",
                " FogMapCenter=", FormatVector2(settings.FogMap.CenterXZ),
                " FogMapCoverage=", FormatVector2(settings.FogMap.CoverageXZ),
                " FogMapAltitude=", Format(settings.FogMap.MinAltitude), "..", Format(settings.FogMap.MaxAltitude),
                " AtmosphereHS=", settings.HorizontalScattering.Enabled,
                " HSRayleigh=", FormatColor(settings.HorizontalScattering.RayleighTint), "*", Format(settings.HorizontalScattering.RayleighScale),
                " HSMie=", FormatColor(settings.HorizontalScattering.MieTint), "*", Format(settings.HorizontalScattering.MieScale),
                " HSMultiple=", FormatColor(settings.HorizontalScattering.MultipleScatteringTint), "*", Format(settings.HorizontalScattering.MultipleScatteringScale),
                " Integrated=", BurtVolumetricFogIntegratedUtility.FormatDebugState(),
                " Transparent=True TotalOrder=VFThenHFThenAF",
                " Formula=", FormulaName);
        }

        private static string Format(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string FormatColor(Color color)
        {
            return string.Concat("(", Format(color.r), ",", Format(color.g), ",", Format(color.b), ")");
        }

        private static string FormatVector2(Vector2 value)
        {
            return string.Concat("(", Format(value.x), ",", Format(value.y), ")");
        }
    }

    internal static class BurtVolumetricFogIntegratedUtility
    {
        private const string ComputeResourcePath = "BurtVolumetricFogIntegration";
        private const string FurthestHiZMip0KernelName = "BuildFurthestHiZMip0";
        private const string FurthestHiZMipKernelName = "BuildFurthestHiZMip";
        private const string ConservativeDepthKernelName = "BuildConservativeDepth";
        private const string MaterialKernelName = "BuildMaterialVolume";
        private const string LocalInjectKernelName = "InjectLocalVolume";
        private const string LightingKernelName = "BuildLightingVolume";
        private const string IntegrationKernelName = "BuildIntegratedVolume";
        private const int GridDepth = 256;
        private const int MaterialGridDepth = 128;
        private const int MaxVisibleLocalVolumeCount = 16;
        private const int VolumeThreadGroupSizeX = 8;
        private const int VolumeThreadGroupSizeY = 8;
        private const int VolumeThreadGroupSizeZ = 1;
        private const int TemporalPhaseCount = 16;
        private const float GridDepthDistributionScale = 33f;
        private const float MinimumGridFarDistance = 2000f;
        private const float ConservativeDepthFootprintMargin = 0.5f;
        private const float ConservativeDepthSliceSafetyMargin = 2f;
        private const float TemporalHistoryAlpha = 0.9f;
        private const float ConservativeHistoryPositionThresholdSquared = 10f;
        private const float ConservativeHistoryDirectionThreshold = 0.2f;
        private const int HistoryRetentionFrames = 300;

        private static readonly int IntegratedLutId = Shader.PropertyToID("_BurtVolumetricFogIntegratedLut");
        private static readonly int IntegratedEnabledId = Shader.PropertyToID("_BurtVolumetricFogIntegratedEnabled");
        private static readonly int IntegratedGridZParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedGridZParams");
        private static readonly int IntegratedSamplingParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedSamplingParams");
        private static readonly int OutputId = Shader.PropertyToID("_BurtVolumetricFogIntegratedOutput");
        private static readonly int GridSizeId = Shader.PropertyToID("_BurtVolumetricFogIntegratedGridSize");
        private static readonly int CameraDepthTextureId = Shader.PropertyToID("_BurtVolumetricFogCameraDepthTexture");
        private static readonly int FurthestHiZSourceId = Shader.PropertyToID("_BurtVolumetricFogFurthestHiZSource");
        private static readonly int FurthestHiZOutputId = Shader.PropertyToID("_BurtVolumetricFogFurthestHiZOutput");
        private static readonly int FurthestHiZTextureId = Shader.PropertyToID("_BurtVolumetricFogFurthestHiZTexture");
        private static readonly int FurthestHiZBuildParamsId = Shader.PropertyToID("_BurtVolumetricFogFurthestHiZBuildParams");
        private static readonly int FurthestHiZParamsId = Shader.PropertyToID("_BurtVolumetricFogFurthestHiZParams");
        private static readonly int ConservativeDepthOutputId = Shader.PropertyToID("_BurtVolumetricFogConservativeDepthOutput");
        private static readonly int ConservativeDepthTextureId = Shader.PropertyToID("_BurtVolumetricFogConservativeDepthTexture");
        private static readonly int PreviousConservativeDepthTextureId = Shader.PropertyToID("_BurtVolumetricFogPreviousConservativeDepthTexture");
        private static readonly int ConservativeDepthParamsId = Shader.PropertyToID("_BurtVolumetricFogConservativeDepthParams");
        private static readonly int MaterialOutputId = Shader.PropertyToID("_BurtVolumetricFogMaterialOutput");
        private static readonly int MaterialTextureId = Shader.PropertyToID("_BurtVolumetricFogMaterialTexture");
        private static readonly int LocalDensityTextureId = Shader.PropertyToID("_BurtVolumetricFogLocalDensityTexture");
        private static readonly int LocalDispatchParamsId = Shader.PropertyToID("_BurtVolumetricFogLocalDispatchParams");
        private static readonly int LocalAlbedoExtinctionId = Shader.PropertyToID("_BurtVolumetricFogLocalAlbedoExtinction");
        private static readonly int LocalAlbedoTopGradientId = Shader.PropertyToID("_BurtVolumetricFogLocalAlbedoTopGradient");
        private static readonly int LocalShapeParamsId = Shader.PropertyToID("_BurtVolumetricFogLocalShapeParams");
        private static readonly int LocalPositiveFaceFadeId = Shader.PropertyToID("_BurtVolumetricFogLocalPositiveFaceFade");
        private static readonly int LocalNegativeFaceFadeId = Shader.PropertyToID("_BurtVolumetricFogLocalNegativeFaceFade");
        private static readonly int LocalDistanceFadeId = Shader.PropertyToID("_BurtVolumetricFogLocalDistanceFade");
        private static readonly int LocalTextureTilingId = Shader.PropertyToID("_BurtVolumetricFogLocalTextureTiling");
        private static readonly int LocalTextureScrollingId = Shader.PropertyToID("_BurtVolumetricFogLocalTextureScrolling");
        private static readonly int LocalWorldToVolumeId = Shader.PropertyToID("_BurtVolumetricFogLocalWorldToVolume");
        private static readonly int LightingOutputId = Shader.PropertyToID("_BurtVolumetricFogLightingOutput");
        private static readonly int LightingTextureId = Shader.PropertyToID("_BurtVolumetricFogLightingTexture");
        private static readonly int LightingHistoryId = Shader.PropertyToID("_BurtVolumetricFogLightingHistory");
        private static readonly int PreviousViewProjectionId = Shader.PropertyToID("_BurtVolumetricFogPreviousViewProjection");
        private static readonly int TemporalParamsId = Shader.PropertyToID("_BurtVolumetricFogTemporalParams");
        private static readonly int HaltonJitterId = Shader.PropertyToID("_BurtVolumetricFogHaltonJitter");
        private static readonly int BuildParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedBuildParams");
        private static readonly int DensityParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedDensityParams");
        private static readonly int SecondDensityParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedSecondDensityParams");
        private static readonly int FogMapTextureId = Shader.PropertyToID("_BurtVolumetricFogMapTexture");
        private static readonly int FogMapWorldParamsId = Shader.PropertyToID("_BurtVolumetricFogMapWorldParams");
        private static readonly int FogMapAltitudeParamsId = Shader.PropertyToID("_BurtVolumetricFogMapAltitudeParams");
        private static readonly int ScatteringParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedScatteringParams");
        private static readonly int AlbedoId = Shader.PropertyToID("_BurtVolumetricFogIntegratedAlbedo");
        private static readonly int InverseViewProjectionId = Shader.PropertyToID("_BurtVolumetricFogIntegratedInverseViewProjection");
        private static readonly int WorldToViewId = Shader.PropertyToID("_BurtVolumetricFogIntegratedWorldToView");
        private static readonly int CameraPositionId = Shader.PropertyToID("_BurtVolumetricFogIntegratedCameraPositionWS");
        private static readonly int MainLightDirectionId = Shader.PropertyToID("_BurtVolumetricFogIntegratedMainLightDirection");
        private static readonly int LegacyLightColorScaleId = Shader.PropertyToID("_BurtVolumetricFogIntegratedLegacyLightColorScale");
        private static readonly int MainLightOcclusionId = Shader.PropertyToID("_BurtVolumetricFogIntegratedMainLightOcclusion");
        private static readonly int ShadowParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedShadowParams");
        private static readonly int AtmosphereParamsId = Shader.PropertyToID("_BurtVolumetricFogIntegratedAtmosphereParams");
        private static readonly int HorizontalSunDirectionId = Shader.PropertyToID("_BurtVolumetricFogIntegratedHorizontalSunDirection");
        private static readonly int HorizontalLightColorId = Shader.PropertyToID("_BurtVolumetricFogIntegratedHorizontalLightColor");
        private static readonly int HorizontalRayleighTintScaleId = Shader.PropertyToID("_BurtVolumetricFogIntegratedHorizontalRayleighTintScale");
        private static readonly int HorizontalMieTintScaleId = Shader.PropertyToID("_BurtVolumetricFogIntegratedHorizontalMieTintScale");
        private static readonly int HorizontalMultipleTintScaleId = Shader.PropertyToID("_BurtVolumetricFogIntegratedHorizontalMultipleTintScale");
        private static readonly int AmbientSHEnabledId = Shader.PropertyToID("_BurtAmbientSHEnabled");
        private static readonly int AmbientSHArId = Shader.PropertyToID("_BurtAmbientSHAr");
        private static readonly int AmbientSHAgId = Shader.PropertyToID("_BurtAmbientSHAg");
        private static readonly int AmbientSHAbId = Shader.PropertyToID("_BurtAmbientSHAb");
        private static readonly int AmbientSHBrId = Shader.PropertyToID("_BurtAmbientSHBr");
        private static readonly int AmbientSHBgId = Shader.PropertyToID("_BurtAmbientSHBg");
        private static readonly int AmbientSHBbId = Shader.PropertyToID("_BurtAmbientSHBb");
        private static readonly int TranslucencyVolume0Id = Shader.PropertyToID("_BurtVolumetricFogTranslucencyVolume0");
        private static readonly int TranslucencyVolume1Id = Shader.PropertyToID("_BurtVolumetricFogTranslucencyVolume1");
        private static readonly int TranslucencyGIParamsId = Shader.PropertyToID("_BurtVolumetricFogTranslucencyGIParams");
        private static readonly int TranslucencyVolumeGridSizeId = Shader.PropertyToID("_BurtGITranslucencyVolumeGridSize");
        private static readonly int TranslucencyVolumeGridZParamsId = Shader.PropertyToID("_BurtGITranslucencyVolumeGridZParams");
        private static readonly int AdditionalLightBufferId = Shader.PropertyToID("_BurtAdditionalLightBuffer");
        private static readonly int AdditionalLightBufferEnabledId = Shader.PropertyToID("_BurtAdditionalLightBufferEnabled");
        private static readonly int MainLightShadowMapId = BurtRenderGraphResourceRegistry.MainLightShadowMapId;

        private static ComputeShader computeShader;
        private static int furthestHiZMip0Kernel = -1;
        private static int furthestHiZMipKernel = -1;
        private static int conservativeDepthKernel = -1;
        private static int materialKernel = -1;
        private static int localInjectKernel = -1;
        private static int lightingKernel = -1;
        private static int integrationKernel = -1;
        private static RenderTexture integratedLut;
        private static RenderTexture materialLut;
        private static RenderTexture furthestHiZ;
        private static Texture3D whiteDensityTexture;
        private static GraphicsBuffer additionalLightFallbackBuffer;
        private static GraphicsBuffer clusterCountFallbackBuffer;
        private static GraphicsBuffer clusterListFallbackBuffer;
        private static GraphicsBuffer clusterOffsetFallbackBuffer;
        private static readonly Dictionary<int, CameraHistoryState> cameraHistories = new Dictionary<int, CameraHistoryState>();
        private static readonly List<int> staleCameraIds = new List<int>();
        private static readonly List<BurtLocalVolumetricFog> visibleLocalVolumes = new List<BurtLocalVolumetricFog>(MaxVisibleLocalVolumeCount);
        private static readonly Vector3[] localVolumeCorners = new Vector3[8];
        private static int gridWidth;
        private static int gridHeight;
        private static int visibleSliceEnd;
        private static int lastBuiltFrame = -1;
        private static int lastBuiltCameraId;
        private static Vector4 gridZParams;
        private static Vector4 samplingParams;
        private static bool ready;
        private static bool initializationFailed;
        private static bool lastLightingHistoryValid;
        private static bool lastConservativeHistoryValid;
        private static string lastHistoryInvalidationReason = "NotBuilt";
        private static int lastVisibleLocalVolumeCount;
        private static int lastInjectedLocalVolumeCount;
        private static bool lastTranslucencyGIEnabled;
        private static bool lastClusterLightListEnabled;
        private static BurtVolumetricFogResolutionTier lastResolutionTier = BurtVolumetricFogResolutionTier.Low;
        private static bool lastFurthestHiZUsed;
        private static int furthestHiZWidth;
        private static int furthestHiZHeight;
        private static int furthestHiZMipCount;
        private static BurtRenderRequest lastBuiltRequest;

        internal static bool IsTranslucencyGIActive => lastTranslucencyGIEnabled;

        private sealed class CameraHistoryState
        {
            public readonly RenderTexture[] Lighting = new RenderTexture[2];
            public readonly RenderTexture[] ConservativeDepth = new RenderTexture[2];
            public int Width;
            public int Height;
            public int ReadIndex;
            public bool HasLightingHistory;
            public bool HasConservativeHistory;
            public int LastFrame = -1;
            public Matrix4x4 PreviousViewProjection = Matrix4x4.identity;
            public Matrix4x4 PreviousProjection = Matrix4x4.identity;
            public Vector3 PreviousPosition;
            public Vector3 PreviousForward = Vector3.forward;
            public int PreviousSettingsSignature;
        }

        public static bool Build(
            CommandBuffer cmd,
            Camera camera,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtVolumetricFogSettings settings,
            BurtVolumetricFogResolutionTier resolutionTier,
            RenderTargetIdentifier cameraDepthTexture,
            RenderTargetIdentifier translucencyVolume0,
            RenderTargetIdentifier translucencyVolume1,
            bool translucencyGIEnabled,
            BurtRenderTargetHandle mainLightShadowMap,
            BurtRenderBufferHandle additionalLightBuffer,
            BurtRenderBufferHandle clusterLightCountBuffer,
            BurtRenderBufferHandle clusterLightListBuffer,
            BurtRenderBufferHandle clusterLightOffsetBuffer)
        {
            lastClusterLightListEnabled = false;
            lastBuiltRequest = null;
            if (cmd == null || camera == null || camera.orthographic || request == null || !settings.Enabled ||
                !SystemInfo.supportsComputeShaders || !SystemInfo.supports3DTextures ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.LoadStore) ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.Sample) ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, FormatUsage.LoadStore) ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R16_SFloat, FormatUsage.Sample))
            {
                ready = false;
                lastTranslucencyGIEnabled = false;
                if (cmd != null)
                {
                    cmd.SetGlobalFloat(IntegratedEnabledId, 0f);
                }

                return false;
            }

            var cameraDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            ResolveGridResolution(resolutionTier, out var width, out var height);
            if (!EnsureSharedResources(width, height))
            {
                ready = false;
                lastTranslucencyGIEnabled = false;
                cmd.SetGlobalFloat(IntegratedEnabledId, 0f);
                return false;
            }

            CleanupStaleCameraHistories(Time.frameCount);
            var cameraId = camera.GetInstanceID();
            if (!TryGetCameraHistory(cameraId, width, height, out var historyState))
            {
                ready = false;
                lastTranslucencyGIEnabled = false;
                cmd.SetGlobalFloat(IntegratedEnabledId, 0f);
                return false;
            }

            ResolveGridZParams(camera, settings.VisibleDistance, out var b, out var o, out var s, out visibleSliceEnd);
            gridZParams = new Vector4(b, o, s, GridDepth);
            samplingParams = new Vector4(visibleSliceEnd, settings.VisibleDistance, settings.StartDistance, 1f / GridDepth);

            var view = camera.worldToCameraMatrix;
            var temporalAA = request.TemporalAA;
            var nonJitteredProjection = temporalAA != null
                ? temporalAA.NonJitteredProjectionMatrix
                : camera.projectionMatrix;
            var gpuProjection = GL.GetGPUProjectionMatrix(nonJitteredProjection, true);
            var currentViewProjection = gpuProjection * view;
            var inverseViewProjection = currentViewProjection.inverse;
            BurtLocalVolumetricFogRegistry.CollectVisible(
                camera,
                settings.VisibleDistance,
                MaxVisibleLocalVolumeCount,
                visibleLocalVolumes);
            var useFurthestHiZ = BuildFurthestHiZ(
                cmd,
                cameraDepthTexture,
                cameraDescriptor.width,
                cameraDescriptor.height);
            var settingsSignature = ComputeSettingsSignature(settings, translucencyGIEnabled, useFurthestHiZ);
            var historyInvalidationReason = ResolveHistoryInvalidationReason(
                historyState,
                nonJitteredProjection,
                settingsSignature,
                Time.frameCount);
            var lightingHistoryValid = historyInvalidationReason == null;
            var conservativeHistoryValid = lightingHistoryValid && historyState.HasConservativeHistory &&
                (camera.transform.position - historyState.PreviousPosition).sqrMagnitude <= ConservativeHistoryPositionThresholdSquared &&
                Vector3.Dot(camera.transform.forward, historyState.PreviousForward) >= ConservativeHistoryDirectionThreshold;
            var writeIndex = 1 - historyState.ReadIndex;
            var lightingHistory = historyState.Lighting[historyState.ReadIndex];
            var lightingOutput = historyState.Lighting[writeIndex];
            var previousConservativeDepth = historyState.ConservativeDepth[historyState.ReadIndex];
            var currentConservativeDepth = historyState.ConservativeDepth[writeIndex];
            var lightingData = request.LightingData;
            BindAdditionalLightBuffer(cmd, lightingData, additionalLightBuffer);
            var clusterLightListEnabled = BindClusterLightList(
                cmd,
                lightingData,
                clusterLightCountBuffer,
                clusterLightListBuffer,
                clusterLightOffsetBuffer);
            var mainLightDirection = lightingData != null ? lightingData.MainLightDirection : Vector3.up;
            if (mainLightDirection.sqrMagnitude <= 0.0001f)
            {
                mainLightDirection = Vector3.up;
            }

            mainLightDirection.Normalize();
            var outerSpaceLight = lightingData != null ? lightingData.MainLightColorOuterSpace : Color.white;
            var atmosphereTransmittance = lightingData != null ? lightingData.AtmosphereTransmittance : Color.white;
            var mainLightVolumetricScale = lightingData != null && lightingData.HasMainLight
                ? Mathf.Max(0f, lightingData.MainLightVolumetricScatteringIntensityScale)
                : 0f;
            var atmosphereSettings = BurtAtmosphereUtility.ResolveSettings();
            var horizontalSunDirection = BurtDrawAtmospherePass.ResolveSunDirection(request, atmosphereSettings);
            var horizontalVolumetricScale = lightingData != null && lightingData.HasMainLight
                ? Mathf.Max(0f, lightingData.MainLightVolumetricScatteringIntensityScale)
                : 1f;
            var horizontalLutAvailable = settings.HorizontalScattering.Enabled &&
                BurtAtmosphereUtility.ShouldUseAtmosphereResources(request) &&
                BurtAtmosphereLutUtility.BindHorizontalScatteringToCompute(
                    cmd,
                    computeShader,
                    lightingKernel);

            cmd.SetComputeVectorParam(computeShader, GridSizeId, new Vector4(gridWidth, gridHeight, GridDepth, visibleSliceEnd));
            cmd.SetComputeVectorParam(computeShader, ConservativeDepthParamsId, new Vector4(
                cameraDescriptor.width,
                cameraDescriptor.height,
                ConservativeDepthFootprintMargin,
                ConservativeDepthSliceSafetyMargin));
            cmd.SetComputeVectorParam(computeShader, FurthestHiZParamsId, new Vector4(
                furthestHiZWidth,
                furthestHiZHeight,
                furthestHiZMipCount,
                useFurthestHiZ ? 1f : 0f));
            cmd.SetComputeTextureParam(computeShader, conservativeDepthKernel, CameraDepthTextureId, cameraDepthTexture);
            cmd.SetComputeTextureParam(
                computeShader,
                conservativeDepthKernel,
                FurthestHiZTextureId,
                useFurthestHiZ ? furthestHiZ : Texture2D.blackTexture);
            cmd.SetComputeTextureParam(computeShader, conservativeDepthKernel, ConservativeDepthOutputId, currentConservativeDepth);
            cmd.DispatchCompute(
                computeShader,
                conservativeDepthKernel,
                Mathf.CeilToInt(gridWidth / 8f),
                Mathf.CeilToInt(gridHeight / 8f),
                1);

            var temporalPhase = Time.frameCount % TemporalPhaseCount;
            cmd.SetComputeVectorParam(computeShader, IntegratedGridZParamsId, gridZParams);
            cmd.SetComputeVectorParam(computeShader, BuildParamsId, new Vector4(
                settings.VisibleDistance,
                settings.StartDistance,
                settings.MaxOpacity,
                settings.Jitter ? temporalPhase : -1f));
            cmd.SetComputeVectorParam(computeShader, DensityParamsId, new Vector4(
                settings.Height,
                settings.Density,
                settings.HeightFalloff,
                settings.ExtinctionScale));
            cmd.SetComputeVectorParam(computeShader, SecondDensityParamsId, new Vector4(
                settings.Height + settings.SecondLayerHeightOffset,
                settings.SecondLayerDensity,
                settings.SecondLayerHeightFalloff,
                0f));
            var fogMapTexture = settings.FogMap.Enabled && settings.FogMap.Texture != null
                ? settings.FogMap.Texture
                : Texture2D.blackTexture;
            cmd.SetComputeVectorParam(computeShader, FogMapWorldParamsId, new Vector4(
                settings.FogMap.CenterXZ.x,
                settings.FogMap.CenterXZ.y,
                1f / settings.FogMap.CoverageXZ.x,
                1f / settings.FogMap.CoverageXZ.y));
            cmd.SetComputeVectorParam(computeShader, FogMapAltitudeParamsId, new Vector4(
                settings.FogMap.MinAltitude,
                settings.FogMap.MaxAltitude,
                settings.FogMap.Enabled ? 1f : 0f,
                0f));
            cmd.SetComputeTextureParam(computeShader, materialKernel, FogMapTextureId, fogMapTexture);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, FogMapTextureId, fogMapTexture);
            cmd.SetComputeVectorParam(computeShader, ScatteringParamsId, new Vector4(
                settings.Anisotropy,
                settings.DirectIntensity,
                settings.AmbientIntensity,
                horizontalLutAvailable ? 1f : 0f));
            ResolveTranslucencyGIGridParams(
                camera,
                request,
                asset,
                translucencyGIEnabled,
                out var translucencyGIGridSize,
                out var translucencyGIGridZParams);
            cmd.SetComputeVectorParam(computeShader, TranslucencyGIParamsId, new Vector4(
                translucencyGIEnabled ? 1f : 0f,
                0f,
                0f,
                0f));
            // Deferred opaque lighting deliberately clears these globals because it must not
            // consume the translucency volume. Volumetric fog is a later, valid consumer, so it
            // must restore the metadata explicitly instead of inheriting that cleared state.
            cmd.SetComputeVectorParam(computeShader, TranslucencyVolumeGridSizeId, translucencyGIGridSize);
            cmd.SetComputeVectorParam(computeShader, TranslucencyVolumeGridZParamsId, translucencyGIGridZParams);
            cmd.SetGlobalVector(TranslucencyVolumeGridSizeId, translucencyGIGridSize);
            cmd.SetGlobalVector(TranslucencyVolumeGridZParamsId, translucencyGIGridZParams);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, TranslucencyVolume0Id, translucencyVolume0);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, TranslucencyVolume1Id, translucencyVolume1);
            cmd.SetComputeVectorParam(computeShader, AlbedoId, settings.Albedo);
            cmd.SetComputeMatrixParam(computeShader, InverseViewProjectionId, inverseViewProjection);
            cmd.SetComputeMatrixParam(computeShader, WorldToViewId, view);
            cmd.SetComputeVectorParam(computeShader, CameraPositionId, camera.transform.position);
            cmd.SetComputeVectorParam(computeShader, MainLightDirectionId, new Vector4(
                mainLightDirection.x,
                mainLightDirection.y,
                mainLightDirection.z,
                0f));
            cmd.SetComputeVectorParam(computeShader, LegacyLightColorScaleId, new Vector4(
                Mathf.Max(0f, outerSpaceLight.r * atmosphereTransmittance.r),
                Mathf.Max(0f, outerSpaceLight.g * atmosphereTransmittance.g),
                Mathf.Max(0f, outerSpaceLight.b * atmosphereTransmittance.b),
                mainLightVolumetricScale));
            cmd.SetComputeFloatParam(computeShader, MainLightOcclusionId, Mathf.Clamp01(atmosphereSettings.MainLightOcclusion));
            cmd.SetComputeVectorParam(computeShader, ShadowParamsId, new Vector4(
                settings.FarStylizedFactor,
                (float)settings.ShadowSourceMode,
                0f,
                0f));
            cmd.SetComputeVectorParam(computeShader, AtmosphereParamsId, new Vector4(
                horizontalLutAvailable ? 1f : 0f,
                atmosphereSettings.SunSource == AtmosphereSunSource.MainLight ? 1f : 0f,
                0f,
                0f));
            cmd.SetComputeVectorParam(computeShader, HorizontalSunDirectionId, horizontalSunDirection);
            var horizontalLightScale = Mathf.Max(0f, atmosphereSettings.SunIntensity) * horizontalVolumetricScale;
            cmd.SetComputeVectorParam(computeShader, HorizontalLightColorId, new Vector4(
                Mathf.Max(0f, outerSpaceLight.r) * horizontalLightScale,
                Mathf.Max(0f, outerSpaceLight.g) * horizontalLightScale,
                Mathf.Max(0f, outerSpaceLight.b) * horizontalLightScale,
                0f));
            cmd.SetComputeVectorParam(computeShader, HorizontalRayleighTintScaleId, ToTintScaleVector(
                settings.HorizontalScattering.RayleighTint,
                settings.HorizontalScattering.RayleighScale));
            cmd.SetComputeVectorParam(computeShader, HorizontalMieTintScaleId, ToTintScaleVector(
                settings.HorizontalScattering.MieTint,
                settings.HorizontalScattering.MieScale));
            cmd.SetComputeVectorParam(computeShader, HorizontalMultipleTintScaleId, ToTintScaleVector(
                settings.HorizontalScattering.MultipleScatteringTint,
                settings.HorizontalScattering.MultipleScatteringScale));
            cmd.SetComputeTextureParam(computeShader, materialKernel, MaterialOutputId, materialLut);
            cmd.DispatchCompute(
                computeShader,
                materialKernel,
                Mathf.CeilToInt((float)gridWidth / VolumeThreadGroupSizeX),
                Mathf.CeilToInt((float)gridHeight / VolumeThreadGroupSizeY),
                Mathf.CeilToInt((float)MaterialGridDepth / VolumeThreadGroupSizeZ));

            var injectedLocalVolumeCount = 0;
            for (var localVolumeIndex = 0; localVolumeIndex < visibleLocalVolumes.Count; localVolumeIndex++)
            {
                var localVolume = visibleLocalVolumes[localVolumeIndex];
                if (!TryResolveLocalVolumeDispatchBounds(
                    camera,
                    localVolume,
                    currentViewProjection,
                    b,
                    o,
                    s,
                    out var dispatchOffset,
                    out var dispatchSize))
                {
                    continue;
                }

                DispatchLocalVolume(cmd, localVolume, dispatchOffset, dispatchSize);
                injectedLocalVolumeCount++;
            }

            // Match XRender's PC sequence: a 16-frame Halton(2,3,5) phase,
            // with index zero skipped to avoid the corner sample.
            var haltonIndex = temporalPhase + 1;
            var haltonJitter = settings.Jitter
                ? new Vector4(
                    RadicalInverse(haltonIndex, 2),
                    RadicalInverse(haltonIndex, 3),
                    RadicalInverse(haltonIndex, 5),
                    0f)
                : new Vector4(0.5f, 0.5f, 0.5f, 0f);
            cmd.SetComputeMatrixParam(computeShader, PreviousViewProjectionId, historyState.PreviousViewProjection);
            cmd.SetComputeVectorParam(computeShader, TemporalParamsId, new Vector4(
                lightingHistoryValid ? 1f : 0f,
                conservativeHistoryValid ? 1f : 0f,
                TemporalHistoryAlpha,
                settings.Jitter ? 1f : 0f));
            cmd.SetComputeVectorParam(computeShader, HaltonJitterId, haltonJitter);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, ConservativeDepthTextureId, currentConservativeDepth);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, PreviousConservativeDepthTextureId, previousConservativeDepth);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, LightingHistoryId, lightingHistory);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, LightingOutputId, lightingOutput);
            cmd.SetComputeTextureParam(computeShader, lightingKernel, MaterialTextureId, materialLut);
            if (mainLightShadowMap.IsValid)
            {
                cmd.SetComputeTextureParam(computeShader, lightingKernel, MainLightShadowMapId, mainLightShadowMap.Identifier);
            }
            else
            {
                // Compute resources are kernel-local. Bind a harmless texture even when
                // shadow strength is zero so Unity never dispatches with an unset slot.
                cmd.SetComputeTextureParam(computeShader, lightingKernel, MainLightShadowMapId, Texture2D.whiteTexture);
            }
            cmd.DispatchCompute(
                computeShader,
                lightingKernel,
                Mathf.CeilToInt((float)gridWidth / VolumeThreadGroupSizeX),
                Mathf.CeilToInt((float)gridHeight / VolumeThreadGroupSizeY),
                Mathf.CeilToInt((float)GridDepth / VolumeThreadGroupSizeZ));

            cmd.SetComputeTextureParam(computeShader, integrationKernel, OutputId, integratedLut);
            cmd.SetComputeTextureParam(computeShader, integrationKernel, ConservativeDepthTextureId, currentConservativeDepth);
            cmd.SetComputeTextureParam(computeShader, integrationKernel, LightingTextureId, lightingOutput);
            cmd.DispatchCompute(computeShader, integrationKernel, Mathf.CeilToInt(gridWidth / 8f), Mathf.CeilToInt(gridHeight / 8f), 1);

            historyState.ReadIndex = writeIndex;
            historyState.HasLightingHistory = true;
            historyState.HasConservativeHistory = true;
            historyState.LastFrame = Time.frameCount;
            historyState.PreviousViewProjection = currentViewProjection;
            historyState.PreviousProjection = nonJitteredProjection;
            historyState.PreviousPosition = camera.transform.position;
            historyState.PreviousForward = camera.transform.forward;
            historyState.PreviousSettingsSignature = settingsSignature;

            ready = true;
            lastBuiltFrame = Time.frameCount;
            lastBuiltCameraId = cameraId;
            lastBuiltRequest = request;
            lastLightingHistoryValid = lightingHistoryValid;
            lastConservativeHistoryValid = conservativeHistoryValid;
            lastHistoryInvalidationReason = lightingHistoryValid
                ? (conservativeHistoryValid ? "None" : "ConservativeCameraJump")
                : historyInvalidationReason;
            lastVisibleLocalVolumeCount = visibleLocalVolumes.Count;
            lastInjectedLocalVolumeCount = injectedLocalVolumeCount;
            lastTranslucencyGIEnabled = translucencyGIEnabled;
            lastClusterLightListEnabled = clusterLightListEnabled;
            lastResolutionTier = resolutionTier == BurtVolumetricFogResolutionTier.High
                ? BurtVolumetricFogResolutionTier.High
                : BurtVolumetricFogResolutionTier.Low;
            lastFurthestHiZUsed = useFurthestHiZ;
            BindGlobals(cmd, true);
            return true;
        }

        private static void ResolveTranslucencyGIGridParams(
            Camera camera,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            bool enabled,
            out Vector4 gridSize,
            out Vector4 gridZParams)
        {
            if (!enabled)
            {
                // Keep every divisor valid even if a stale shader branch samples the fallback.
                gridSize = Vector4.one;
                gridZParams = Vector4.zero;
                return;
            }

            var screenProbeSettings =
                BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(
                    request,
                    asset);
            var descriptor =
                BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeDescriptor(
                    camera,
                    screenProbeSettings);
            gridSize = new Vector4(
                descriptor.width,
                descriptor.height,
                descriptor.volumeDepth,
                BurtScreenSpaceGlobalIlluminationPassUtility.TranslucencyVolumeMaterialIntensityScale);
            gridZParams =
                BurtScreenSpaceGlobalIlluminationPassUtility.ResolveTranslucencyVolumeGridZParams(screenProbeSettings);
        }

        public static void BindForTransparentFog(CommandBuffer cmd, Camera camera, BurtRenderRequest request)
        {
            if (cmd == null)
            {
                return;
            }

            var valid = ready &&
                integratedLut != null &&
                integratedLut.IsCreated() &&
                camera != null &&
                request != null &&
                BurtVolumetricFogUtility.ShouldUseVolumetricFog(request) &&
                ReferenceEquals(lastBuiltRequest, request) &&
                lastBuiltFrame == Time.frameCount &&
                lastBuiltCameraId == camera.GetInstanceID();
            BindGlobals(cmd, valid);
        }

        public static void BeginCameraRequest(
            CommandBuffer cmd,
            BurtRenderRequest request)
        {
            lastBuiltRequest = null;
            if (cmd != null)
            {
                BindGlobals(cmd, false);
            }
        }

        public static string FormatDebugState()
        {
            return string.Concat(
                ready ? "Ready" : "Unavailable",
                " Grid=", gridWidth, "x", gridHeight, "x", GridDepth,
                " ResolutionTier=", lastResolutionTier,
                " ConservativeDepth=R16_SFloat/", lastFurthestHiZUsed ? "FurthestHiZ" : "CameraDepthFallback",
                " FurthestHiZ=", furthestHiZWidth, "x", furthestHiZHeight, " Mips=", furthestHiZMipCount,
                " VisibleSlices=", visibleSliceEnd,
                " CameraHistories=", cameraHistories.Count,
                " MaterialVolume=", materialLut != null && materialLut.IsCreated() ? "Ready" : "Unavailable",
                " LocalVolumes=", lastInjectedLocalVolumeCount, "/", lastVisibleLocalVolumeCount,
                " LocalRegistry=", BurtLocalVolumetricFogRegistry.ActiveCount,
                " AmbientSource=", lastTranslucencyGIEnabled ? "TranslucencyGI_SH2" : "SkySH_L0L1",
                " PunctualList=", lastClusterLightListEnabled ? "Clustered" : "ArrayFallback",
                " LightingHistory=", lastLightingHistoryValid ? "Valid" : "Invalid",
                " ConservativeHistory=", lastConservativeHistoryValid ? "Valid" : "Invalid",
                " HistoryReason=", lastHistoryInvalidationReason,
                " HistoryAlpha=", TemporalHistoryAlpha.ToString("0.###", CultureInfo.InvariantCulture),
                " Threads=", VolumeThreadGroupSizeX, "x", VolumeThreadGroupSizeY, "x", VolumeThreadGroupSizeZ,
                " HaltonPhases=", TemporalPhaseCount,
                " BootstrapSamples=4",
                " FootprintMargin=", ConservativeDepthFootprintMargin,
                " SliceSafety=", ConservativeDepthSliceSafetyMargin,
                " Frame=", lastBuiltFrame,
                " Camera=", lastBuiltCameraId,
                " Formula=log2(depth*B+O)*33");
        }

        public static void Release()
        {
            ReleaseTexture(ref integratedLut);
            ReleaseTexture(ref materialLut);
            ReleaseTexture(ref furthestHiZ);
            ReleaseWhiteDensityTexture();
            ReleaseClusterFallbackBuffers();
            foreach (var history in cameraHistories.Values)
            {
                ReleaseHistoryState(history);
            }

            cameraHistories.Clear();
            staleCameraIds.Clear();
            computeShader = null;
            furthestHiZMip0Kernel = -1;
            furthestHiZMipKernel = -1;
            conservativeDepthKernel = -1;
            materialKernel = -1;
            localInjectKernel = -1;
            lightingKernel = -1;
            integrationKernel = -1;
            gridWidth = 0;
            gridHeight = 0;
            visibleSliceEnd = 0;
            lastBuiltFrame = -1;
            lastBuiltCameraId = 0;
            lastBuiltRequest = null;
            gridZParams = Vector4.zero;
            samplingParams = Vector4.zero;
            ready = false;
            initializationFailed = false;
            lastLightingHistoryValid = false;
            lastConservativeHistoryValid = false;
            lastHistoryInvalidationReason = "Released";
            lastTranslucencyGIEnabled = false;
            lastClusterLightListEnabled = false;
            lastResolutionTier = BurtVolumetricFogResolutionTier.Low;
            lastFurthestHiZUsed = false;
            furthestHiZWidth = 0;
            furthestHiZHeight = 0;
            furthestHiZMipCount = 0;
            lastVisibleLocalVolumeCount = 0;
            lastInjectedLocalVolumeCount = 0;
            visibleLocalVolumes.Clear();
        }

        private static bool BuildFurthestHiZ(
            CommandBuffer cmd,
            RenderTargetIdentifier cameraDepthTexture,
            int cameraWidth,
            int cameraHeight)
        {
            if (cmd == null || !EnsureFurthestHiZResources(cameraWidth, cameraHeight))
            {
                return false;
            }

            var textureIdentifier = new RenderTargetIdentifier(furthestHiZ);
            var sourceWidth = Mathf.Max(1, cameraWidth);
            var sourceHeight = Mathf.Max(1, cameraHeight);
            cmd.SetComputeVectorParam(computeShader, FurthestHiZBuildParamsId, new Vector4(
                sourceWidth,
                sourceHeight,
                furthestHiZWidth,
                furthestHiZHeight));
            cmd.SetComputeTextureParam(computeShader, furthestHiZMip0Kernel, CameraDepthTextureId, cameraDepthTexture);
            cmd.SetComputeTextureParam(computeShader, furthestHiZMip0Kernel, FurthestHiZOutputId, textureIdentifier, 0);
            cmd.DispatchCompute(
                computeShader,
                furthestHiZMip0Kernel,
                Mathf.CeilToInt(furthestHiZWidth / 8f),
                Mathf.CeilToInt(furthestHiZHeight / 8f),
                1);

            sourceWidth = furthestHiZWidth;
            sourceHeight = furthestHiZHeight;
            for (var mip = 1; mip < furthestHiZMipCount; mip++)
            {
                var targetWidth = Mathf.Max(1, (sourceWidth + 1) / 2);
                var targetHeight = Mathf.Max(1, (sourceHeight + 1) / 2);
                cmd.SetComputeVectorParam(computeShader, FurthestHiZBuildParamsId, new Vector4(
                    sourceWidth,
                    sourceHeight,
                    targetWidth,
                    targetHeight));
                cmd.SetComputeTextureParam(computeShader, furthestHiZMipKernel, FurthestHiZSourceId, textureIdentifier, mip - 1);
                cmd.SetComputeTextureParam(computeShader, furthestHiZMipKernel, FurthestHiZOutputId, textureIdentifier, mip);
                cmd.DispatchCompute(
                    computeShader,
                    furthestHiZMipKernel,
                    Mathf.CeilToInt(targetWidth / 8f),
                    Mathf.CeilToInt(targetHeight / 8f),
                    1);
                sourceWidth = targetWidth;
                sourceHeight = targetHeight;
            }

            return true;
        }

        private static bool EnsureFurthestHiZResources(int cameraWidth, int cameraHeight)
        {
            if (!SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.LoadStore) ||
                !SystemInfo.IsFormatSupported(GraphicsFormat.R32_SFloat, FormatUsage.Sample))
            {
                ReleaseTexture(ref furthestHiZ);
                furthestHiZWidth = 0;
                furthestHiZHeight = 0;
                furthestHiZMipCount = 0;
                return false;
            }

            var width = Mathf.Max(1, Mathf.NextPowerOfTwo(Mathf.Max(1, cameraWidth >> 1)));
            var height = Mathf.Max(1, Mathf.NextPowerOfTwo(Mathf.Max(1, cameraHeight >> 1)));
            var mipCount = BurtRenderTargetDescriptorUtility.CalculateMipCount(width, height);
            if (furthestHiZ != null && furthestHiZ.IsCreated() &&
                furthestHiZ.width == width && furthestHiZ.height == height &&
                furthestHiZ.mipmapCount == mipCount)
            {
                furthestHiZWidth = width;
                furthestHiZHeight = height;
                furthestHiZMipCount = mipCount;
                return true;
            }

            ReleaseTexture(ref furthestHiZ);
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.R32_SFloat, 0)
            {
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = true,
                autoGenerateMips = false,
                mipCount = mipCount,
                sRGB = false
            };
            furthestHiZ = new RenderTexture(descriptor)
            {
                name = "Burt VF Furthest HZB",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!furthestHiZ.Create())
            {
                ReleaseTexture(ref furthestHiZ);
                furthestHiZWidth = 0;
                furthestHiZHeight = 0;
                furthestHiZMipCount = 0;
                return false;
            }

            furthestHiZWidth = width;
            furthestHiZHeight = height;
            furthestHiZMipCount = mipCount;
            return true;
        }

        private static void ResolveGridResolution(
            BurtVolumetricFogResolutionTier resolutionTier,
            out int width,
            out int height)
        {
            if (resolutionTier == BurtVolumetricFogResolutionTier.High)
            {
                width = 240;
                height = 135;
                return;
            }

            width = 160;
            height = 90;
        }

        private static bool EnsureSharedResources(int width, int height)
        {
            if (initializationFailed)
            {
                return false;
            }

            if (computeShader == null)
            {
                computeShader = Resources.Load<ComputeShader>(ComputeResourcePath);
                if (computeShader == null)
                {
                    initializationFailed = true;
                    return false;
                }

                try
                {
                    furthestHiZMip0Kernel = computeShader.FindKernel(FurthestHiZMip0KernelName);
                    furthestHiZMipKernel = computeShader.FindKernel(FurthestHiZMipKernelName);
                    conservativeDepthKernel = computeShader.FindKernel(ConservativeDepthKernelName);
                    materialKernel = computeShader.FindKernel(MaterialKernelName);
                    localInjectKernel = computeShader.FindKernel(LocalInjectKernelName);
                    lightingKernel = computeShader.FindKernel(LightingKernelName);
                    integrationKernel = computeShader.FindKernel(IntegrationKernelName);
                }
                catch
                {
                    initializationFailed = true;
                    furthestHiZMip0Kernel = -1;
                    furthestHiZMipKernel = -1;
                    conservativeDepthKernel = -1;
                    materialKernel = -1;
                    localInjectKernel = -1;
                    lightingKernel = -1;
                    integrationKernel = -1;
                    return false;
                }
            }

            if (!EnsureClusterFallbackBuffers())
            {
                initializationFailed = true;
                return false;
            }

            if (integratedLut != null && integratedLut.IsCreated() &&
                materialLut != null && materialLut.IsCreated() &&
                gridWidth == width && gridHeight == height)
            {
                EnsureWhiteDensityTexture();
                return true;
            }

            ReleaseTexture(ref integratedLut);
            ReleaseTexture(ref materialLut);
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, 0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = GridDepth,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            integratedLut = new RenderTexture(descriptor)
            {
                name = "Burt Volumetric Fog Integrated LUT",
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!integratedLut.Create())
            {
                ReleaseTexture(ref integratedLut);
                initializationFailed = true;
                return false;
            }

            descriptor.volumeDepth = MaterialGridDepth;
            materialLut = new RenderTexture(descriptor)
            {
                name = "Burt Volumetric Fog Material LUT",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!materialLut.Create())
            {
                ReleaseTexture(ref integratedLut);
                ReleaseTexture(ref materialLut);
                initializationFailed = true;
                return false;
            }

            gridWidth = width;
            gridHeight = height;
            EnsureWhiteDensityTexture();
            return true;
        }

        private static bool BindClusterLightList(
            CommandBuffer cmd,
            BurtLightingData lightingData,
            BurtRenderBufferHandle countBuffer,
            BurtRenderBufferHandle listBuffer,
            BurtRenderBufferHandle offsetBuffer)
        {
            var enabled = lightingData != null &&
                lightingData.ClusterLightUploaded &&
                lightingData.AdditionalLightCount > 0 &&
                lightingData.ClusterLightGridX > 0 &&
                lightingData.ClusterLightGridY > 0 &&
                lightingData.ClusterLightDepthSliceCount > 0 &&
                lightingData.ClusterLightMaxLightsPerCluster > 0 &&
                lightingData.ClusterLightListCapacity > 0 &&
                countBuffer.IsValid && countBuffer.HasBuffer &&
                listBuffer.IsValid && listBuffer.HasBuffer &&
                offsetBuffer.IsValid && offsetBuffer.HasBuffer;

            cmd.SetComputeFloatParam(computeShader, BurtTiledLightData.ClusterLightBufferEnabledId, enabled ? 1f : 0f);
            cmd.SetComputeVectorParam(
                computeShader,
                BurtTiledLightData.ClusterLightGridParamsId,
                enabled
                    ? new Vector4(
                        lightingData.ClusterLightGridX,
                        lightingData.ClusterLightGridY,
                        lightingData.ClusterLightDepthSliceCount,
                        lightingData.ClusterLightMaxLightsPerCluster)
                    : Vector4.zero);
            cmd.SetComputeVectorParam(
                computeShader,
                BurtTiledLightData.ClusterLightDepthParamsId,
                enabled
                    ? new Vector4(
                        lightingData.ClusterLightNearPlane,
                        lightingData.ClusterLightFarPlane,
                        lightingData.ClusterLightInvDepthRange,
                        lightingData.ClusterLightDepthSliceCount)
                    : Vector4.zero);
            cmd.SetComputeVectorParam(
                computeShader,
                BurtTiledLightData.ClusterLightWorldToViewZId,
                enabled ? lightingData.ClusterLightWorldToViewZ : Vector4.zero);
            cmd.SetComputeBufferParam(
                computeShader,
                lightingKernel,
                BurtTiledLightData.ClusterLightCountBufferId,
                enabled ? countBuffer.Buffer : clusterCountFallbackBuffer);
            cmd.SetComputeBufferParam(
                computeShader,
                lightingKernel,
                BurtTiledLightData.ClusterLightListBufferId,
                enabled ? listBuffer.Buffer : clusterListFallbackBuffer);
            cmd.SetComputeBufferParam(
                computeShader,
                lightingKernel,
                BurtTiledLightData.ClusterLightOffsetBufferId,
                enabled ? offsetBuffer.Buffer : clusterOffsetFallbackBuffer);
            return enabled;
        }

        private static bool BindAdditionalLightBuffer(
            CommandBuffer cmd,
            BurtLightingData lightingData,
            BurtRenderBufferHandle additionalLightBuffer)
        {
            var enabled = lightingData != null &&
                lightingData.AdditionalLightBufferUploaded &&
                lightingData.AdditionalLightCount > 0 &&
                additionalLightBuffer.IsValid &&
                additionalLightBuffer.HasBuffer;
            cmd.SetComputeBufferParam(
                computeShader,
                lightingKernel,
                AdditionalLightBufferId,
                enabled ? additionalLightBuffer.Buffer : additionalLightFallbackBuffer);
            cmd.SetComputeFloatParam(computeShader, AdditionalLightBufferEnabledId, enabled ? 1f : 0f);
            return enabled;
        }

        private static bool EnsureClusterFallbackBuffers()
        {
            if (additionalLightFallbackBuffer != null &&
                clusterCountFallbackBuffer != null &&
                clusterListFallbackBuffer != null &&
                clusterOffsetFallbackBuffer != null)
            {
                return true;
            }

            ReleaseClusterFallbackBuffers();
            try
            {
                additionalLightFallbackBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    1,
                    BurtLightingData.AdditionalLightBufferStride)
                {
                    name = "Burt VF Additional Light Fallback"
                };
                clusterCountFallbackBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint))
                {
                    name = "Burt VF Cluster Count Fallback"
                };
                clusterListFallbackBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint))
                {
                    name = "Burt VF Cluster List Fallback"
                };
                clusterOffsetFallbackBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint) * 2)
                {
                    name = "Burt VF Cluster Offset Fallback"
                };
                additionalLightFallbackBuffer.SetData(new Vector4[] { Vector4.zero });
                clusterCountFallbackBuffer.SetData(new uint[] { 0u });
                clusterListFallbackBuffer.SetData(new uint[] { 0u });
                clusterOffsetFallbackBuffer.SetData(new Vector2Int[] { Vector2Int.zero });
                return true;
            }
            catch
            {
                ReleaseClusterFallbackBuffers();
                return false;
            }
        }

        private static void ReleaseClusterFallbackBuffers()
        {
            ReleaseBuffer(ref additionalLightFallbackBuffer);
            ReleaseBuffer(ref clusterCountFallbackBuffer);
            ReleaseBuffer(ref clusterListFallbackBuffer);
            ReleaseBuffer(ref clusterOffsetFallbackBuffer);
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            buffer.Release();
            buffer = null;
        }

        private static bool TryGetCameraHistory(
            int cameraId,
            int width,
            int height,
            out CameraHistoryState state)
        {
            if (cameraHistories.TryGetValue(cameraId, out state) &&
                state.Width == width && state.Height == height &&
                HistoryTexturesAreCreated(state))
            {
                return true;
            }

            if (state != null)
            {
                ReleaseHistoryState(state);
                cameraHistories.Remove(cameraId);
            }

            state = new CameraHistoryState
            {
                Width = width,
                Height = height
            };
            for (var index = 0; index < 2; index++)
            {
                state.Lighting[index] = CreateLightingTexture(width, height, cameraId, index);
                state.ConservativeDepth[index] = CreateConservativeDepthTexture(width, height, cameraId, index);
                if (state.Lighting[index] == null || state.ConservativeDepth[index] == null)
                {
                    ReleaseHistoryState(state);
                    state = null;
                    return false;
                }
            }

            cameraHistories.Add(cameraId, state);
            return true;
        }

        private static bool TryResolveLocalVolumeDispatchBounds(
            Camera camera,
            BurtLocalVolumetricFog volume,
            Matrix4x4 viewProjection,
            float gridB,
            float gridO,
            float gridS,
            out Vector3Int dispatchOffset,
            out Vector3Int dispatchSize)
        {
            dispatchOffset = Vector3Int.zero;
            dispatchSize = Vector3Int.zero;
            if (camera == null || volume == null)
            {
                return false;
            }

            var localToWorld = volume.VolumeLocalToWorldMatrix;
            var cornerIndex = 0;
            for (var z = -1; z <= 1; z += 2)
            {
                for (var y = -1; y <= 1; y += 2)
                {
                    for (var x = -1; x <= 1; x += 2)
                    {
                        localVolumeCorners[cornerIndex++] = localToWorld.MultiplyPoint3x4(
                            new Vector3(x, y, z) * 0.5f);
                    }
                }
            }

            var view = camera.worldToCameraMatrix;
            var minimumViewDepth = float.PositiveInfinity;
            var maximumViewDepth = float.NegativeInfinity;
            var minimumUv = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var maximumUv = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            var forceFullScreenBounds = IsPointInsideLocalVolume(volume, camera.transform.position);
            var hasProjectedCorner = false;
            for (var index = 0; index < localVolumeCorners.Length; index++)
            {
                var corner = localVolumeCorners[index];
                var viewPosition = view.MultiplyPoint3x4(corner);
                var viewDepth = -viewPosition.z;
                minimumViewDepth = Mathf.Min(minimumViewDepth, viewDepth);
                maximumViewDepth = Mathf.Max(maximumViewDepth, viewDepth);

                var clip = viewProjection * new Vector4(corner.x, corner.y, corner.z, 1f);
                if (clip.w <= 0.0001f)
                {
                    forceFullScreenBounds = true;
                    continue;
                }

                var inverseW = 1f / clip.w;
                var uv = new Vector2(
                    clip.x * inverseW * 0.5f + 0.5f,
                    clip.y * inverseW * 0.5f + 0.5f);
                if (SystemInfo.graphicsUVStartsAtTop)
                {
                    uv.y = 1f - uv.y;
                }

                minimumUv = Vector2.Min(minimumUv, uv);
                maximumUv = Vector2.Max(maximumUv, uv);
                hasProjectedCorner = true;
            }

            if (maximumViewDepth <= Mathf.Max(camera.nearClipPlane, 0.001f))
            {
                return false;
            }

            var clampedMinimumDepth = Mathf.Max(minimumViewDepth, camera.nearClipPlane);
            var minimumSlice = MapViewDepthToSlice(clampedMinimumDepth, gridB, gridO, gridS);
            var maximumSlice = MapViewDepthToSlice(maximumViewDepth, gridB, gridO, gridS);
            var minimumZ = Mathf.Clamp(Mathf.FloorToInt(minimumSlice) - 1, 0, MaterialGridDepth);
            var maximumZ = Mathf.Clamp(Mathf.CeilToInt(maximumSlice) + 2, 0, MaterialGridDepth);
            if (maximumZ <= minimumZ)
            {
                return false;
            }

            var minimumX = 0;
            var minimumY = 0;
            var maximumX = gridWidth;
            var maximumY = gridHeight;
            if (!forceFullScreenBounds && hasProjectedCorner)
            {
                minimumX = Mathf.Clamp(Mathf.FloorToInt(minimumUv.x * gridWidth) - 1, 0, gridWidth);
                minimumY = Mathf.Clamp(Mathf.FloorToInt(minimumUv.y * gridHeight) - 1, 0, gridHeight);
                maximumX = Mathf.Clamp(Mathf.CeilToInt(maximumUv.x * gridWidth) + 1, 0, gridWidth);
                maximumY = Mathf.Clamp(Mathf.CeilToInt(maximumUv.y * gridHeight) + 1, 0, gridHeight);
            }

            if (maximumX <= minimumX || maximumY <= minimumY)
            {
                return false;
            }

            dispatchOffset = new Vector3Int(minimumX, minimumY, minimumZ);
            dispatchSize = new Vector3Int(
                maximumX - minimumX,
                maximumY - minimumY,
                maximumZ - minimumZ);
            return dispatchSize.x > 0 && dispatchSize.y > 0 && dispatchSize.z > 0;
        }

        private static void DispatchLocalVolume(
            CommandBuffer cmd,
            BurtLocalVolumetricFog volume,
            Vector3Int dispatchOffset,
            Vector3Int dispatchSize)
        {
            var bottomAlbedo = volume.albedo.linear;
            var topAlbedo = volume.albedoTop.linear;
            var densityTexture = volume.densityTexture != null
                ? volume.densityTexture
                : whiteDensityTexture;
            cmd.SetComputeTextureParam(computeShader, localInjectKernel, MaterialOutputId, materialLut);
            cmd.SetComputeTextureParam(computeShader, localInjectKernel, LocalDensityTextureId, densityTexture);
            cmd.SetComputeVectorParam(computeShader, LocalDispatchParamsId, new Vector4(
                dispatchOffset.x,
                dispatchOffset.y,
                dispatchOffset.z,
                volume.densityTexture != null ? 1f : 0f));
            cmd.SetComputeVectorParam(computeShader, LocalAlbedoExtinctionId, new Vector4(
                bottomAlbedo.r,
                bottomAlbedo.g,
                bottomAlbedo.b,
                Mathf.Clamp01(volume.extinction)));
            cmd.SetComputeVectorParam(computeShader, LocalAlbedoTopGradientId, new Vector4(
                topAlbedo.r,
                topAlbedo.g,
                topAlbedo.b,
                volume.useVerticalColorGradient ? 1f : 0f));
            cmd.SetComputeVectorParam(computeShader, LocalShapeParamsId, new Vector4(
                Mathf.Max(0f, volume.heightFalloff),
                volume.falloffMode == BurtLocalVolumetricFogFalloffMode.Exponential ? 1f : 0f,
                volume.invertFade ? 1f : 0f,
                volume.blendMode == BurtLocalVolumetricFogBlendMode.Additive ? 1f : 0f));
            cmd.SetComputeVectorParam(computeShader, LocalPositiveFaceFadeId, volume.positiveFaceFade);
            cmd.SetComputeVectorParam(computeShader, LocalNegativeFaceFadeId, volume.negativeFaceFade);
            cmd.SetComputeVectorParam(computeShader, LocalDistanceFadeId, new Vector4(
                Mathf.Max(0f, volume.distanceFadeStart),
                Mathf.Max(volume.distanceFadeStart, volume.distanceFadeEnd),
                0f,
                0f));
            cmd.SetComputeVectorParam(computeShader, LocalTextureTilingId, volume.textureTiling);
            cmd.SetComputeVectorParam(computeShader, LocalTextureScrollingId, volume.textureScrollingSpeed);
            cmd.SetComputeMatrixParam(computeShader, LocalWorldToVolumeId, volume.WorldToVolumeLocalMatrix);
            cmd.DispatchCompute(
                computeShader,
                localInjectKernel,
                Mathf.CeilToInt(dispatchSize.x / 4f),
                Mathf.CeilToInt(dispatchSize.y / 4f),
                Mathf.CeilToInt(dispatchSize.z / 4f));
        }

        private static bool IsPointInsideLocalVolume(BurtLocalVolumetricFog volume, Vector3 point)
        {
            var localPoint = volume.WorldToVolumeLocalMatrix.MultiplyPoint3x4(point);
            return Mathf.Abs(localPoint.x) <= 0.5f &&
                Mathf.Abs(localPoint.y) <= 0.5f &&
                Mathf.Abs(localPoint.z) <= 0.5f;
        }

        private static float MapViewDepthToSlice(float viewDepth, float gridB, float gridO, float gridS)
        {
            var logArgument = Mathf.Max(viewDepth * gridB + gridO, 0.000001f);
            return Mathf.Log(logArgument, 2f) * gridS;
        }

        private static RenderTexture CreateLightingTexture(int width, int height, int cameraId, int index)
        {
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, 0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = GridDepth,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            var texture = new RenderTexture(descriptor)
            {
                name = string.Concat("Burt VF Lighting History Camera ", cameraId, " ", index),
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (texture.Create())
            {
                return texture;
            }

            ReleaseTexture(ref texture);
            return null;
        }

        private static RenderTexture CreateConservativeDepthTexture(int width, int height, int cameraId, int index)
        {
            var descriptor = new RenderTextureDescriptor(width, height, GraphicsFormat.R16_SFloat, 0)
            {
                dimension = TextureDimension.Tex2D,
                enableRandomWrite = true,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            var texture = new RenderTexture(descriptor)
            {
                name = string.Concat("Burt VF Conservative Depth Camera ", cameraId, " ", index),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (texture.Create())
            {
                return texture;
            }

            ReleaseTexture(ref texture);
            return null;
        }

        private static bool HistoryTexturesAreCreated(CameraHistoryState state)
        {
            return state != null &&
                state.Lighting[0] != null && state.Lighting[0].IsCreated() &&
                state.Lighting[1] != null && state.Lighting[1].IsCreated() &&
                state.ConservativeDepth[0] != null && state.ConservativeDepth[0].IsCreated() &&
                state.ConservativeDepth[1] != null && state.ConservativeDepth[1].IsCreated();
        }

        private static void CleanupStaleCameraHistories(int frame)
        {
            staleCameraIds.Clear();
            foreach (var pair in cameraHistories)
            {
                if (pair.Value.LastFrame >= 0 && frame - pair.Value.LastFrame > HistoryRetentionFrames)
                {
                    staleCameraIds.Add(pair.Key);
                }
            }

            for (var index = 0; index < staleCameraIds.Count; index++)
            {
                var cameraId = staleCameraIds[index];
                if (cameraHistories.TryGetValue(cameraId, out var state))
                {
                    ReleaseHistoryState(state);
                    cameraHistories.Remove(cameraId);
                }
            }

            staleCameraIds.Clear();
        }

        private static string ResolveHistoryInvalidationReason(
            CameraHistoryState state,
            Matrix4x4 projection,
            int settingsSignature,
            int frame)
        {
            if (state == null || !state.HasLightingHistory)
            {
                return "NoLightingHistory";
            }

            if (state.LastFrame != frame - 1)
            {
                return "NonConsecutiveFrame";
            }

            if (!Approximately(state.PreviousProjection, projection))
            {
                return "ProjectionChanged";
            }

            if (state.PreviousSettingsSignature != settingsSignature)
            {
                return "FogSettingsChanged";
            }

            return null;
        }

        private static bool Approximately(Matrix4x4 a, Matrix4x4 b)
        {
            for (var index = 0; index < 16; index++)
            {
                if (Mathf.Abs(a[index] - b[index]) > 0.0001f)
                {
                    return false;
                }
            }

            return true;
        }

        private static int ComputeSettingsSignature(
            BurtVolumetricFogSettings settings,
            bool translucencyGIEnabled,
            bool useFurthestHiZ)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + settings.VisibleDistance.GetHashCode();
                hash = hash * 31 + settings.Height.GetHashCode();
                hash = hash * 31 + settings.Density.GetHashCode();
                hash = hash * 31 + settings.HeightFalloff.GetHashCode();
                hash = hash * 31 + settings.SecondLayerHeightOffset.GetHashCode();
                hash = hash * 31 + settings.SecondLayerDensity.GetHashCode();
                hash = hash * 31 + settings.SecondLayerHeightFalloff.GetHashCode();
                hash = hash * 31 + settings.ExtinctionScale.GetHashCode();
                hash = hash * 31 + settings.Albedo.GetHashCode();
                hash = hash * 31 + settings.Anisotropy.GetHashCode();
                hash = hash * 31 + settings.DirectIntensity.GetHashCode();
                hash = hash * 31 + settings.AmbientIntensity.GetHashCode();
                hash = hash * 31 + settings.FarStylizedFactor.GetHashCode();
                hash = hash * 31 + settings.ShadowSourceMode.GetHashCode();
                hash = hash * 31 + useFurthestHiZ.GetHashCode();
                hash = hash * 31 + settings.FogMap.Enabled.GetHashCode();
                hash = hash * 31 + (settings.FogMap.Texture != null ? settings.FogMap.Texture.GetInstanceID() : 0);
                hash = hash * 31 + settings.FogMap.CenterXZ.GetHashCode();
                hash = hash * 31 + settings.FogMap.CoverageXZ.GetHashCode();
                hash = hash * 31 + settings.FogMap.MinAltitude.GetHashCode();
                hash = hash * 31 + settings.FogMap.MaxAltitude.GetHashCode();
                hash = hash * 31 + settings.HorizontalScattering.Enabled.GetHashCode();
                hash = hash * 31 + settings.HorizontalScattering.RayleighTint.GetHashCode();
                hash = hash * 31 + settings.HorizontalScattering.RayleighScale.GetHashCode();
                hash = hash * 31 + settings.HorizontalScattering.MieTint.GetHashCode();
                hash = hash * 31 + settings.HorizontalScattering.MieScale.GetHashCode();
                hash = hash * 31 + settings.HorizontalScattering.MultipleScatteringTint.GetHashCode();
                hash = hash * 31 + settings.HorizontalScattering.MultipleScatteringScale.GetHashCode();
                hash = hash * 31 + translucencyGIEnabled.GetHashCode();
                if (!translucencyGIEnabled)
                {
                    hash = hash * 31 + ComputeAmbientLightingSignature();
                }
                return hash;
            }
        }

        private static int ComputeAmbientLightingSignature()
        {
            unchecked
            {
                var enabled = Shader.GetGlobalFloat(AmbientSHEnabledId) > 0.5f;
                var hash = enabled ? 1 : 0;
                if (!enabled)
                {
                    return hash;
                }

                hash = hash * 31 + Shader.GetGlobalVector(AmbientSHArId).GetHashCode();
                hash = hash * 31 + Shader.GetGlobalVector(AmbientSHAgId).GetHashCode();
                hash = hash * 31 + Shader.GetGlobalVector(AmbientSHAbId).GetHashCode();
                var shBr = Shader.GetGlobalVector(AmbientSHBrId);
                var shBg = Shader.GetGlobalVector(AmbientSHBgId);
                var shBb = Shader.GetGlobalVector(AmbientSHBbId);
                hash = hash * 31 + new Vector3(shBr.z, shBg.z, shBb.z).GetHashCode();
                return hash;
            }
        }

        private static float RadicalInverse(int index, int radix)
        {
            var inverseRadix = 1f / Mathf.Max(radix, 2);
            var fraction = inverseRadix;
            var result = 0f;
            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction *= inverseRadix;
            }

            return result;
        }

        private static void ReleaseHistoryState(CameraHistoryState state)
        {
            if (state == null)
            {
                return;
            }

            for (var index = 0; index < 2; index++)
            {
                ReleaseTexture(ref state.Lighting[index]);
                ReleaseTexture(ref state.ConservativeDepth[index]);
            }

            state.HasLightingHistory = false;
            state.HasConservativeHistory = false;
        }

        private static void ResolveGridZParams(
            Camera camera,
            float visibleDistance,
            out float b,
            out float o,
            out float s,
            out int visibleSlices)
        {
            var near = Mathf.Max(0.01f, camera.nearClipPlane);
            var far = Mathf.Max(MinimumGridFarDistance, visibleDistance, near + 0.01f);
            s = GridDepthDistributionScale;
            var depthPower = Mathf.Pow(2f, (GridDepth - 1f) / s);
            o = (far - near * depthPower) / Mathf.Max(far - near, 0.0001f);
            b = (1f - o) / near;
            var clampedVisibleDistance = Mathf.Clamp(visibleDistance, near, far);
            var logArgument = Mathf.Max(clampedVisibleDistance * b + o, 0.000001f);
            var visibleSliceCenter = Mathf.Log(logArgument, 2f) * s;
            // XRender treats this as a loop-end-exclusive count. floor(center)+1
            // keeps the final slice when the cutoff maps exactly to a slice center.
            visibleSlices = Mathf.Approximately(clampedVisibleDistance, far)
                ? GridDepth
                : Mathf.Clamp(Mathf.FloorToInt(visibleSliceCenter) + 1, 1, GridDepth);
        }

        internal static float ResolveFeatureBoundaryDepth(
            Camera camera,
            float visibleDistance)
        {
            if (camera == null)
            {
                return 0f;
            }

            ResolveGridZParams(
                camera,
                visibleDistance,
                out var b,
                out var o,
                out var s,
                out _);
            var featureBoundaryDepth =
                (Mathf.Pow(2f, MaterialGridDepth / Mathf.Max(s, 0.0001f)) - o) /
                Mathf.Max(b, 0.000001f);
            return Mathf.Max(0f, featureBoundaryDepth);
        }

        private static Vector4 ToTintScaleVector(Color tint, float scale)
        {
            var linearTint = tint.linear;
            var safeScale = Mathf.Max(0f, scale);
            return new Vector4(
                linearTint.r * safeScale,
                linearTint.g * safeScale,
                linearTint.b * safeScale,
                safeScale);
        }

        private static void BindGlobals(CommandBuffer cmd, bool enabled)
        {
            cmd.SetGlobalFloat(IntegratedEnabledId, enabled ? 1f : 0f);
            if (!enabled)
            {
                return;
            }

            cmd.SetGlobalTexture(IntegratedLutId, integratedLut);
            cmd.SetGlobalVector(IntegratedGridZParamsId, gridZParams);
            cmd.SetGlobalVector(IntegratedSamplingParamsId, samplingParams);
        }

        private static void EnsureWhiteDensityTexture()
        {
            if (whiteDensityTexture != null)
            {
                return;
            }

            whiteDensityTexture = new Texture3D(1, 1, 1, TextureFormat.RGBA32, false)
            {
                name = "Burt VF White Density",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.HideAndDontSave
            };
            whiteDensityTexture.SetPixel(0, 0, 0, Color.white);
            whiteDensityTexture.Apply(false, true);
        }

        private static void ReleaseWhiteDensityTexture()
        {
            if (whiteDensityTexture == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(whiteDensityTexture);
            }
            else
            {
                Object.DestroyImmediate(whiteDensityTexture);
            }

            whiteDensityTexture = null;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            if (Application.isPlaying)
            {
                Object.Destroy(texture);
            }
            else
            {
                Object.DestroyImmediate(texture);
            }

            texture = null;
        }
    }
}
