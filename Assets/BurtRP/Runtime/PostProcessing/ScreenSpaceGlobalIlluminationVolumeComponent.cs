using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationQuality")]
    public enum ScreenSpaceGlobalIlluminationQuality
    {
        Custom = 0,
        Low = 1,
        Medium = 2,
        High = 3,
        Ultra = 4
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationResolution")]

    public enum ScreenSpaceGlobalIlluminationResolution
    {
        Full = 0,
        Half = 1
    }

    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationFinalGather")]
    public enum ScreenSpaceGlobalIlluminationFinalGather
    {
        ScreenProbe = 0,
        IrradianceField = 1
    }

    public enum ScreenProbeIrradianceFormat
    {
        SH3 = 0,
        Octahedral = 1
    }

    public enum ScreenProbeIntegrateType
    {
        SimpleIntegrate = 0,
        TileClassification = 1
    }

    public enum ScreenProbeIntegrateMethod
    {
        SphericalHarmonic = 0
    }

    public enum ScreenProbeRadianceCacheType
    {
        None = 0,
        HashGrid = 1,
        ClipMap = 2
    }

    public enum SceneVoxelMaterialMemoryBudget
    {
        None = 0,
        Low = 512,
        Medium = 1024,
        High = 2048,
        Ultra = 4096
    }

    public enum SceneVoxelMaterialGenerateMethod
    {
        Atomic = 0,
        PendingList = 1
    }

    public enum SceneVoxelLightingType
    {
        None = 0,
        Directional = 1,
        Direct = 2,
        Indirect = 3
    }

    [Flags]
    public enum ScreenProbeTraceSource
    {
        None = 0,
        Screen = 1 << 0,
        HashGridCache = 1 << 1,
        VoxelOctree = 1 << 2,
        SceneVoxel = VoxelOctree,
        ScreenGrid = 1 << 3,
        RadianceCacheClipMap = 1 << 4,
        LocalSkyProbe = 1 << 5,
        SkyCubemap = 1 << 6,
        All = Screen | HashGridCache | VoxelOctree | ScreenGrid | RadianceCacheClipMap | LocalSkyProbe | SkyCubemap
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationQualityParameter")]
    public sealed class ScreenSpaceGlobalIlluminationQualityParameter : VolumeParameter<ScreenSpaceGlobalIlluminationQuality>, IEquatable<ScreenSpaceGlobalIlluminationQualityParameter>
    {
        public ScreenSpaceGlobalIlluminationQualityParameter(ScreenSpaceGlobalIlluminationQuality value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenSpaceGlobalIlluminationQualityParameter lhs, ScreenSpaceGlobalIlluminationQuality rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenSpaceGlobalIlluminationQualityParameter lhs, ScreenSpaceGlobalIlluminationQuality rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenSpaceGlobalIlluminationQualityParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenSpaceGlobalIlluminationQualityParameter);
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationResolutionParameter")]
    public sealed class ScreenSpaceGlobalIlluminationResolutionParameter : VolumeParameter<ScreenSpaceGlobalIlluminationResolution>, IEquatable<ScreenSpaceGlobalIlluminationResolutionParameter>
    {
        public ScreenSpaceGlobalIlluminationResolutionParameter(ScreenSpaceGlobalIlluminationResolution value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenSpaceGlobalIlluminationResolutionParameter lhs, ScreenSpaceGlobalIlluminationResolution rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenSpaceGlobalIlluminationResolutionParameter lhs, ScreenSpaceGlobalIlluminationResolution rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenSpaceGlobalIlluminationResolutionParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenSpaceGlobalIlluminationResolutionParameter);
        }
    }

    [Serializable]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationFinalGatherParameter")]
    public sealed class ScreenSpaceGlobalIlluminationFinalGatherParameter : VolumeParameter<ScreenSpaceGlobalIlluminationFinalGather>, IEquatable<ScreenSpaceGlobalIlluminationFinalGatherParameter>
    {
        public ScreenSpaceGlobalIlluminationFinalGatherParameter(ScreenSpaceGlobalIlluminationFinalGather value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenSpaceGlobalIlluminationFinalGatherParameter lhs, ScreenSpaceGlobalIlluminationFinalGather rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenSpaceGlobalIlluminationFinalGatherParameter lhs, ScreenSpaceGlobalIlluminationFinalGather rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenSpaceGlobalIlluminationFinalGatherParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenSpaceGlobalIlluminationFinalGatherParameter);
        }
    }

    [Serializable]
    public sealed class ScreenProbeIrradianceFormatParameter : VolumeParameter<ScreenProbeIrradianceFormat>, IEquatable<ScreenProbeIrradianceFormatParameter>
    {
        public ScreenProbeIrradianceFormatParameter(ScreenProbeIrradianceFormat value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenProbeIrradianceFormatParameter lhs, ScreenProbeIrradianceFormat rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenProbeIrradianceFormatParameter lhs, ScreenProbeIrradianceFormat rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenProbeIrradianceFormatParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenProbeIrradianceFormatParameter);
        }
    }

    [Serializable]
    public sealed class ScreenProbeIntegrateTypeParameter : VolumeParameter<ScreenProbeIntegrateType>, IEquatable<ScreenProbeIntegrateTypeParameter>
    {
        public ScreenProbeIntegrateTypeParameter(ScreenProbeIntegrateType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenProbeIntegrateTypeParameter lhs, ScreenProbeIntegrateType rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenProbeIntegrateTypeParameter lhs, ScreenProbeIntegrateType rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenProbeIntegrateTypeParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenProbeIntegrateTypeParameter);
        }
    }

    [Serializable]
    public sealed class ScreenProbeIntegrateMethodParameter : VolumeParameter<ScreenProbeIntegrateMethod>, IEquatable<ScreenProbeIntegrateMethodParameter>
    {
        public ScreenProbeIntegrateMethodParameter(ScreenProbeIntegrateMethod value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenProbeIntegrateMethodParameter lhs, ScreenProbeIntegrateMethod rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenProbeIntegrateMethodParameter lhs, ScreenProbeIntegrateMethod rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenProbeIntegrateMethodParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenProbeIntegrateMethodParameter);
        }
    }

    [Serializable]
    public sealed class ScreenProbeRadianceCacheTypeParameter : VolumeParameter<ScreenProbeRadianceCacheType>, IEquatable<ScreenProbeRadianceCacheTypeParameter>
    {
        public ScreenProbeRadianceCacheTypeParameter(ScreenProbeRadianceCacheType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenProbeRadianceCacheTypeParameter lhs, ScreenProbeRadianceCacheType rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenProbeRadianceCacheTypeParameter lhs, ScreenProbeRadianceCacheType rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenProbeRadianceCacheTypeParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenProbeRadianceCacheTypeParameter);
        }
    }

    [Serializable]
    public sealed class ScreenProbeTraceSourceParameter : VolumeParameter<ScreenProbeTraceSource>, IEquatable<ScreenProbeTraceSourceParameter>
    {
        public ScreenProbeTraceSourceParameter(ScreenProbeTraceSource value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public static bool operator ==(ScreenProbeTraceSourceParameter lhs, ScreenProbeTraceSource rhs)
        {
            return lhs != null && lhs.value == rhs;
        }

        public static bool operator !=(ScreenProbeTraceSourceParameter lhs, ScreenProbeTraceSource rhs)
        {
            return !(lhs == rhs);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }

        public bool Equals(ScreenProbeTraceSourceParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScreenProbeTraceSourceParameter);
        }
    }

    [Serializable]
    public sealed class SceneVoxelMaterialMemoryBudgetParameter : VolumeParameter<SceneVoxelMaterialMemoryBudget>, IEquatable<SceneVoxelMaterialMemoryBudgetParameter>
    {
        public SceneVoxelMaterialMemoryBudgetParameter(SceneVoxelMaterialMemoryBudget value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public bool Equals(SceneVoxelMaterialMemoryBudgetParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SceneVoxelMaterialMemoryBudgetParameter);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }
    }

    [Serializable]
    public sealed class SceneVoxelMaterialGenerateMethodParameter : VolumeParameter<SceneVoxelMaterialGenerateMethod>, IEquatable<SceneVoxelMaterialGenerateMethodParameter>
    {
        public SceneVoxelMaterialGenerateMethodParameter(SceneVoxelMaterialGenerateMethod value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public bool Equals(SceneVoxelMaterialGenerateMethodParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SceneVoxelMaterialGenerateMethodParameter);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }
    }

    [Serializable]
    public sealed class SceneVoxelLightingTypeParameter : VolumeParameter<SceneVoxelLightingType>, IEquatable<SceneVoxelLightingTypeParameter>
    {
        public SceneVoxelLightingTypeParameter(SceneVoxelLightingType value, bool overrideState = false)
            : base(value, overrideState)
        {
        }

        public bool Equals(SceneVoxelLightingTypeParameter other)
        {
            return other != null && value == other.value && overrideState == other.overrideState;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as SceneVoxelLightingTypeParameter);
        }

        public override int GetHashCode()
        {
            return ((int)value * 31) + (overrideState ? 1 : 0);
        }
    }

    [Serializable]
    [VolumeComponentMenu("Rendering/Screen Space Global Illumination")]
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtScreenSpaceGlobalIlluminationVolumeComponent")]
    public sealed class ScreenSpaceGlobalIlluminationVolumeComponent : VolumeComponent
    {
        private const float Epsilon = 0.0001f;

        [Title("BurtRP BurtGI")]
        [InfoBox("Deferred v2.2 diffuse GI. It traces against the current screen color, temporally denoises the result, and guards thin edges against sky/surface leaks.")]
        public BoolParameter enabled = new BoolParameter(false);
        [InfoBox("Custom keeps the manual values below. Low/Medium/High/Ultra apply XGI-style presets for resolution, trace, blur and indirect-channel cost.")]
        public ScreenSpaceGlobalIlluminationQualityParameter quality = new ScreenSpaceGlobalIlluminationQualityParameter(ScreenSpaceGlobalIlluminationQuality.Medium);
        [InfoBox("Used by Custom. Low and Medium force Half for performance; High and Ultra force Full for quality.")]
        public ScreenSpaceGlobalIlluminationResolutionParameter resolution = new ScreenSpaceGlobalIlluminationResolutionParameter(ScreenSpaceGlobalIlluminationResolution.Half);
        public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter radius = new ClampedFloatParameter(2f, 0.05f, 20f);
        public ClampedIntParameter sampleCount = new ClampedIntParameter(12, 1, 32);
        public ClampedIntParameter maxSteps = new ClampedIntParameter(8, 1, 64);
        public ClampedFloatParameter thickness = new ClampedFloatParameter(0.35f, 0.01f, 3f);
        public ClampedFloatParameter skyFallback = new ClampedFloatParameter(1f, 0f, 2f);
        public ClampedFloatParameter radianceClamp = new ClampedFloatParameter(8f, 0.1f, 64f);
        public ClampedFloatParameter normalWeight = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter distanceFade = new ClampedFloatParameter(80f, 1f, 800f);
        [Title("Spatial Denoise")]
        public BoolParameter blur = new BoolParameter(true);
        public ClampedFloatParameter blurSharpness = new ClampedFloatParameter(0.18f, 0f, 1f);
        public ClampedFloatParameter spatialDenoiseRadius = new ClampedFloatParameter(1.25f, 0.5f, 3f);
        public ClampedFloatParameter spatialDenoiseStrength = new ClampedFloatParameter(0.75f, 0f, 1f);
        [Title("Leak Guard")]
        public ClampedFloatParameter leakGuardStrength = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter edgeFadeStrength = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedFloatParameter normalConeTightness = new ClampedFloatParameter(0.55f, 0f, 1f);
        public ClampedFloatParameter skyEdgeSuppression = new ClampedFloatParameter(0.6f, 0f, 1f);

        [Title("Temporal Denoise")]
        public BoolParameter temporalAccumulation = new BoolParameter(true);
        public ClampedFloatParameter temporalFeedback = new ClampedFloatParameter(0.86f, 0f, 0.98f);
        public ClampedFloatParameter temporalDepthRejection = new ClampedFloatParameter(0.02f, 0.001f, 0.2f);
        public ClampedFloatParameter temporalNormalRejection = new ClampedFloatParameter(0.65f, 0f, 1f);
        public ClampedFloatParameter temporalClamp = new ClampedFloatParameter(1f, 0.25f, 4f);
        public ClampedFloatParameter temporalVarianceClamp = new ClampedFloatParameter(1.25f, 0f, 4f);
        public ClampedFloatParameter temporalHitRejection = new ClampedFloatParameter(0.55f, 0f, 1f);

        [Title("ScreenProbe Lite")]
        [InfoBox("XGI-style ScreenProbe path. It gathers a low-resolution probe grid, traces a compact octahedral atlas, and blends the resulting irradiance into BurtGI when Apply Strength is above zero.")]
        public BoolParameter screenProbeLite = new BoolParameter(true);
        public ClampedIntParameter screenProbeSpacingPixels = new ClampedIntParameter(16, 8, 64);
        public ClampedFloatParameter screenProbeAdaptiveAllocationFraction = new ClampedFloatParameter(0.5f, 0.01f, 1f);
        public ClampedIntParameter screenProbeAdaptiveMinDownSampleFactor = new ClampedIntParameter(8, 8, 64);
        public ClampedIntParameter screenProbeTraceOctahedronResolution = new ClampedIntParameter(8, 1, 8);
        public ClampedFloatParameter screenProbeTraceDistance = new ClampedFloatParameter(200f, 0.01f, 65504f);
        public ClampedFloatParameter screenProbeTraceScreenDistance = new ClampedFloatParameter(5f, 0.001f, 5f);
        public ClampedIntParameter screenProbeTraceVoxelMaxTraceSteps = new ClampedIntParameter(64, 1, 64);
        public ClampedFloatParameter screenProbeTraceVoxelStepFactor = new ClampedFloatParameter(1f, 0.1f, 10f);
        public BoolParameter screenProbeTraceHierarchically = new BoolParameter(true);
        public ClampedIntParameter screenProbeTraceHierarchicalMaxIterations = new ClampedIntParameter(50, 1, 50);
        public ClampedFloatParameter screenProbeTraceRelativeDepthThickness = new ClampedFloatParameter(0.02f, 0.0001f, 1f);
        public ClampedFloatParameter screenProbeTraceHistoryDepthTestRelativeThickness = new ClampedFloatParameter(0.01f, 0.0001f, 1f);
        public ClampedFloatParameter screenProbeScreenTraceThicknessScaleWhenNoFallback = new ClampedFloatParameter(2f, 0.001f, 2f);
        public ClampedFloatParameter screenProbeGatherMaxRayIntensity = new ClampedFloatParameter(1f, 0.01f, 40f);
        public ClampedIntParameter screenProbeSampleCount = new ClampedIntParameter(8, 1, 32);
        public ClampedFloatParameter screenProbeTemporalFeedback = new ClampedFloatParameter(0.9f, 0f, 0.98f);
        public ClampedFloatParameter screenProbeTemporalFilterHistoryWeight = new ClampedFloatParameter(0.5f, 0f, 0.98f);
        public BoolParameter screenProbeTemporalFilter = new BoolParameter(true);
        public BoolParameter screenProbeTemporalReprojection = new BoolParameter(true);
        public ClampedIntParameter screenProbeReprojectionMaxFramesAccumulated = new ClampedIntParameter(20, 1, 50);
        public ClampedFloatParameter screenProbeHistoryDistanceThreshold = new ClampedFloatParameter(0.02f, 0.001f, 1f);
        public ClampedFloatParameter screenProbeTemporalHistoryNormalThreshold = new ClampedFloatParameter(45f, 0f, 180f);
        public ClampedFloatParameter screenProbeReprojectionDepthRejectParamsA = new ClampedFloatParameter(4f, 1f, 50f);
        public ClampedFloatParameter screenProbeReprojectionDepthRejectParamsB = new ClampedFloatParameter(2f, 1f, 50f);
        public ClampedFloatParameter screenProbeTemporalExposureCheckThreshold = new ClampedFloatParameter(0.1f, 0.01f, 2f);
        public ClampedFloatParameter screenProbeTemporalPlayerVelocityThreshold = new ClampedFloatParameter(0.1f, 0f, 10f);
        public ClampedFloatParameter screenProbeApplyStrength = new ClampedFloatParameter(1f, 0f, 1f);
        public BoolParameter screenProbeTraceCompact = new BoolParameter(true);
        public BoolParameter screenProbeTraceHardwareRay = new BoolParameter(false);
        public BoolParameter screenProbeTraceUseWorldRadianceClipMap = new BoolParameter(true);
        public ScreenProbeTraceSourceParameter screenProbeTraceSources = new ScreenProbeTraceSourceParameter(ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.VoxelOctree | ScreenProbeTraceSource.SkyCubemap);
        public BoolParameter screenProbeImportanceSampling = new BoolParameter(true);
        public BoolParameter screenProbeImportanceSampleLighting = new BoolParameter(true);
        public BoolParameter screenProbeImportanceSampleProbeRadianceHistory = new BoolParameter(true);
        public ClampedFloatParameter screenProbeImportanceSamplingHistoryDistanceThreshold = new ClampedFloatParameter(0.3f, 0.001f, 10f);
        public ClampedIntParameter screenProbeFixedJitterIndex = new ClampedIntParameter(-1, -1, 16);
        public BoolParameter screenProbeSpatialFilter = new BoolParameter(true);
        public ClampedIntParameter screenProbeSpatialFilterPasses = new ClampedIntParameter(3, 1, 8);
        public ClampedIntParameter screenProbeSpatialFilterHalfKernelSize = new ClampedIntParameter(1, 0, 2);
        public ClampedFloatParameter screenProbeSpatialFilterMaxRadianceHitAngle = new ClampedFloatParameter(10f, 0.001f, 180f);
        public ClampedFloatParameter screenProbeSpatialFilterPositionWeightScale = new ClampedFloatParameter(1000f, 0.001f, 10000f);
        public BoolParameter screenProbeFixupBorders = new BoolParameter(true);
        public ScreenProbeIrradianceFormatParameter screenProbeIrradianceFormat = new ScreenProbeIrradianceFormatParameter(ScreenProbeIrradianceFormat.SH3);
        public ScreenProbeIntegrateTypeParameter screenProbeIntegrateType = new ScreenProbeIntegrateTypeParameter(ScreenProbeIntegrateType.SimpleIntegrate);
        public ScreenProbeIntegrateMethodParameter screenProbeIntegrateMethod = new ScreenProbeIntegrateMethodParameter(ScreenProbeIntegrateMethod.SphericalHarmonic);

        [Title("Radiance Cache")]
        public ScreenProbeRadianceCacheTypeParameter screenProbeRadianceCacheType = new ScreenProbeRadianceCacheTypeParameter(ScreenProbeRadianceCacheType.None);
        public BoolParameter screenProbeRadianceCacheForceFullUpdate = new BoolParameter(false);
        public BoolParameter screenProbeRadianceCacheTraceHardwareRay = new BoolParameter(false);
        public BoolParameter screenProbeRadianceCacheCalculateIrradiance = new BoolParameter(false);
        public BoolParameter screenProbeRadianceCacheEnableMultiBounceFromRadianceCache = new BoolParameter(false);
        public ClampedIntParameter screenProbeRadianceCacheRadianceProbeResolution = new ClampedIntParameter(32, 8, 64);
        public ClampedIntParameter screenProbeRadianceCacheIrradianceProbeResolution = new ClampedIntParameter(6, 6, 32);
        public ClampedIntParameter screenProbeRadianceCacheOcclusionProbeResolution = new ClampedIntParameter(16, 6, 32);
        public BoolParameter screenProbeRadianceCacheFilterProbes = new BoolParameter(false);
        public ClampedFloatParameter screenProbeRadianceCacheFilterMaxRadianceHitAngle = new ClampedFloatParameter(0.2f, 0.001f, 3.14159f);
        public ClampedFloatParameter screenProbeRadianceCacheReprojectionRadiusScale = new ClampedFloatParameter(1.5f, 0.1f, 8f);
        public ClampedIntParameter screenProbeRadianceCacheClipMapCount = new ClampedIntParameter(5, 1, 6);
        public ClampedIntParameter screenProbeRadianceCacheClipMapResolution = new ClampedIntParameter(48, 1, 256);
        public ClampedFloatParameter screenProbeRadianceCacheClipMapWorldExtent = new ClampedFloatParameter(40f, 1f, 200f);
        public ClampedIntParameter screenProbeRadianceCacheNumProbesToTraceBudget = new ClampedIntParameter(100, 1, 100000);
        public ClampedIntParameter screenProbeRadianceCacheIrradianceRadianceProbeResolution = new ClampedIntParameter(16, 8, 64);
        public ClampedIntParameter screenProbeRadianceCacheIrradianceClipMapCount = new ClampedIntParameter(4, 1, 6);
        public ClampedIntParameter screenProbeRadianceCacheIrradianceClipMapResolution = new ClampedIntParameter(64, 1, 256);
        public ClampedFloatParameter screenProbeRadianceCacheIrradianceClipMapWorldExtent = new ClampedFloatParameter(50f, 1f, 200f);
        public ClampedIntParameter screenProbeRadianceCacheIrradianceNumProbesToTraceBudget = new ClampedIntParameter(200, 1, 100000);
        public ClampedFloatParameter screenProbeRadianceCacheVisualizeRadiusScale = new ClampedFloatParameter(0.05f, 0.001f, 0.5f);
        public ClampedIntParameter screenProbeRadianceCacheVisualizeClipmapIndex = new ClampedIntParameter(-1, -1, 5);
        [InfoBox("XRender HashGrid debug decay window in frames. Set to 0 to show only cells that contributed this frame.")]
        public ClampedIntParameter screenProbeRadianceCacheHashGridDebugMaxCellDecay = new ClampedIntParameter(500, 0, 5000);

        [Title("XGI Final Gather")]
        [InfoBox("ScreenProbe gathers the filtered probe atlas. IrradianceField directly interpolates the Radiance Cache ClipMap onto the full-resolution GBuffer, matching XRender's alternate final-gather path.")]
        public ScreenSpaceGlobalIlluminationFinalGatherParameter finalGather = new ScreenSpaceGlobalIlluminationFinalGatherParameter(ScreenSpaceGlobalIlluminationFinalGather.ScreenProbe);
        public ClampedFloatParameter irradianceFieldStrength = new ClampedFloatParameter(1f, 0f, 2f);
        public BoolParameter irradianceFieldBaked = new BoolParameter(false);

        [Title("XGI Indirect Channels")]
        [InfoBox("Matches XRender's XGI light controls. These gates affect deferred application and invalidate the channel history when changed.")]
        public BoolParameter enableBackfaceDiffuse = new BoolParameter(false);
        public BoolParameter enableRoughSpecular = new BoolParameter(true);
        public ClampedFloatParameter screenProbeMaxRoughnessToEvaluateRoughSpecular = new ClampedFloatParameter(0.4f, 0f, 1f);
        public ClampedFloatParameter xgiIntensityScale = new ClampedFloatParameter(1f, 0.001f, 100f);
        public ClampedFloatParameter xgiCharacterIntensity = new ClampedFloatParameter(1.5f, 0f, 2f);
        public ClampedFloatParameter xgiScreenRatio = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter xgiScreenRatioSpeed = new ClampedFloatParameter(0.1f, 0f, 1f);
        public BoolParameter xgiUseProbeFirst = new BoolParameter(false);
        public BoolParameter useTranslucencyVolume = new BoolParameter(true);
        public ClampedIntParameter translucencyVolumeGridPixelSize = new ClampedIntParameter(64, 8, 128);
        public ClampedFloatParameter translucencyVolumeEndDistanceFromCamera = new ClampedFloatParameter(80f, 10f, 1000f);
        public ClampedFloatParameter translucencyVolumeGridDistributionZScale = new ClampedFloatParameter(4f, 1f, 6f);
        public ClampedIntParameter translucencyVolumeTracingOctahedronResolution = new ClampedIntParameter(3, 1, 8);
        public BoolParameter translucencyVolumeJitter = new BoolParameter(true);
        public BoolParameter translucencyVolumeUseTemporalReprojection = new BoolParameter(true);
        public ClampedFloatParameter translucencyVolumeHistoryWeight = new ClampedFloatParameter(0.95f, 0.9f, 0.99f);
        public ClampedIntParameter translucencyVolumeTemporalMaxRayDirections = new ClampedIntParameter(8, 0, 8);
        public BoolParameter translucencyVolumeSpatialFilter = new BoolParameter(true);
        public ClampedIntParameter translucencyVolumeSpatialFilterSampleCount = new ClampedIntParameter(3, 1, 5);
        public ClampedFloatParameter translucencyVolumeSpatialFilterStandardDeviation = new ClampedFloatParameter(5f, 0.1f, 20f);
        public ClampedFloatParameter translucencyVolumeGridCenterOffsetFromDepthBuffer = new ClampedFloatParameter(0.5f, -1f, 4f);
        public ClampedFloatParameter translucencyVolumeOffsetThresholdToAcceptDepthBufferOffset = new ClampedFloatParameter(1f, 0f, 8f);
        public ClampedFloatParameter translucencyVolumeTraceStepFactor = new ClampedFloatParameter(1f, 0.1f, 10f);
        public ClampedFloatParameter translucencyVolumeMaxTraceDistance = new ClampedFloatParameter(200f, 0.0001f, 2000f);
        public ClampedFloatParameter translucencyVolumeVoxelTraceStartDistanceScale = new ClampedFloatParameter(1f, 0f, 10f);
        public ClampedFloatParameter translucencyVolumeMaxRayIntensity = new ClampedFloatParameter(20f, 0.1f, 100f);
        public ClampedFloatParameter sceneVoxelClipMapFirstWorldExtent = new ClampedFloatParameter(25f, 1f, 1000f);
        public BoolParameter sceneVoxelFollowCamera = new BoolParameter(true);
        public ClampedFloatParameter sceneVoxelCameraForward = new ClampedFloatParameter(10f, 0f, 100f);
        public Vector3Parameter sceneVoxelOrigin = new Vector3Parameter(Vector3.zero);
        public BoolParameter sceneVoxelAlwaysUpdate = new BoolParameter(false);
        public ClampedFloatParameter sceneVoxelOriginUpdateDistance = new ClampedFloatParameter(50f, 0.0001f, 1000f);
        public ClampedIntParameter sceneVoxelClipMapCount = new ClampedIntParameter(4, 1, 4);
        public ClampedFloatParameter sceneVoxelClipMapDistributionBase = new ClampedFloatParameter(3f, 1f, 4f);
        public Vector4Parameter sceneVoxelClipMapOffset03 = new Vector4Parameter(new Vector4(10f, 20f, 30f, 50f));
        public Vector4Parameter sceneVoxelClipMapUpdateDistance03 = new Vector4Parameter(new Vector4(30f, 60f, 70f, 100f));
        public Vector4Parameter sceneVoxelClipMapOffset47 = new Vector4Parameter(new Vector4(60f, 120f, 250f, 500f));
        public Vector4Parameter sceneVoxelClipMapUpdateDistance47 = new Vector4Parameter(new Vector4(200f, 400f, 800f, 1600f));
        public ClampedIntParameter sceneVoxelClipMapResolution = new ClampedIntParameter(64, 16, 64);
        public SceneVoxelMaterialMemoryBudgetParameter sceneVoxelMaterialBudget = new SceneVoxelMaterialMemoryBudgetParameter(SceneVoxelMaterialMemoryBudget.Medium);
        public SceneVoxelMaterialGenerateMethodParameter sceneVoxelMaterialGenerateMethod = new SceneVoxelMaterialGenerateMethodParameter(SceneVoxelMaterialGenerateMethod.Atomic);
        public BoolParameter sceneVoxelDrawVegetation = new BoolParameter(true);
        public BoolParameter sceneVoxelDrawGrass = new BoolParameter(false);
        public SceneVoxelLightingTypeParameter sceneVoxelLightingType = new SceneVoxelLightingTypeParameter(SceneVoxelLightingType.Indirect);
        public BoolParameter sceneVoxelLightingDirectionalShadow = new BoolParameter(true);
        public BoolParameter sceneVoxelLightingPunctualShadow = new BoolParameter(true);
        public BoolParameter sceneVoxelLightingSkyLight = new BoolParameter(true);
        public ClampedFloatParameter sceneVoxelDiffuseColorBoost = new ClampedFloatParameter(1f, 1f, 4f);
        public ClampedFloatParameter sceneVoxelAvoidBleeding = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ClampedIntParameter sceneVoxelMaxSampleCount = new ClampedIntParameter(12, 1, 200);
        public BoolParameter sceneVoxelMultiBounce = new BoolParameter(true);
        public ClampedIntParameter sceneVoxelDirectionCount = new ClampedIntParameter(6, 1, 6);
        public ClampedFloatParameter sceneVoxelDirectLightIntensity = new ClampedFloatParameter(1f, 0f, 5f);
        public ColorParameter sceneVoxelDirectLightTint = new ColorParameter(Color.white, true, false, false);
        public ClampedFloatParameter sceneVoxelIndirectLightIntensity = new ClampedFloatParameter(0.5f, 0f, 1f);
        public ColorParameter sceneVoxelIndirectLightTint = new ColorParameter(Color.white, true, false, false);
        public BoolParameter sceneVoxelEnableSkyVisibility = new BoolParameter(false);
        public BoolParameter sceneVoxelDebugExpandView = new BoolParameter(false);
        public ClampedFloatParameter sceneVoxelDebugExpandViewDistance = new ClampedFloatParameter(1000f, 0f, 5000f);
        public ClampedIntParameter sceneVoxelDebugShowMipmapID = new ClampedIntParameter(0, 0, 8);
        public BoolParameter sceneVoxelDebugByTrace = new BoolParameter(false);
        public BoolParameter sceneVoxelDebugDrawProbe = new BoolParameter(false);
        public ClampedFloatParameter sceneVoxelDebugProbeSizeWS = new ClampedFloatParameter(0.5f, 0.01f, 2f);
        public ClampedFloatParameter localSkyProbeCameraDistance = new ClampedFloatParameter(2f, 0f, 100f);
        public BoolParameter localSkyProbeShowDebugSphere = new BoolParameter(true);
        public ClampedIntParameter sceneVoxelTraceMaxSteps = new ClampedIntParameter(64, 1, 64);
        public ClampedFloatParameter sceneVoxelTraceStepFactor = new ClampedFloatParameter(1f, 0.1f, 10f);
        public ClampedFloatParameter screenProbeSkylightLeaking = new ClampedFloatParameter(0f, 0f, 1f);
        public ClampedFloatParameter screenProbeSkylightLeakingRoughness = new ClampedFloatParameter(0.3f, 0f, 1f);
        public ClampedFloatParameter screenProbeFullSkylightLeakingDistance = new ClampedFloatParameter(10f, 0.001f, 20f);
        public BoolParameter screenProbeTraceSkyCubemap = new BoolParameter(true);

        [Title("Short Range AO")]
        [InfoBox("XGI-style near-field occlusion for indirect lighting, evaluated from the ScreenProbe trace-atlas bent-normal texture and its temporal history.")]
        public BoolParameter shortRangeAO = new BoolParameter(true);
        public ClampedFloatParameter shortRangeAOWeight = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter shortRangeAOApplyWeight = new ClampedFloatParameter(1f, 0f, 1f);
        public ClampedFloatParameter shortRangeAOSlopeCompareToleranceScale = new ClampedFloatParameter(1f, 0f, 10f);

        public bool IsEnabled()
        {
            return active && enabled.value && intensity.value > Epsilon && radius.value > Epsilon && sampleCount.value > 0;
        }
    }
}
