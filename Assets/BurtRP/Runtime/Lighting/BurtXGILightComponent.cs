using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace Burt.RenderPipeline
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XGI Light Component")]
    [MovedFrom(true, "XRender.Pipeline.Modules.XGI", "FunPlus.WorldX.XRender.Runtime", "XGILightComponent")]
    public sealed class BurtXGILightComponent : MonoBehaviour, ISerializationCallbackReceiver
    {
        public int priority;

        [Header("Base")]
        [FormerlySerializedAs("m_OverrideConfig")]
        public bool overrideConfig;
        [FormerlySerializedAs("m_Enable")]
        public bool enable = true;
        public bool useProbeFirst;
        [FormerlySerializedAs("m_EnableXGIBackface")]
        public bool enableBackfaceDiffuse;
        [FormerlySerializedAs("m_EnableXGIGlossy")]
        public bool enableRoughSpecular = true;
        [FormerlySerializedAs("m_UseTranslucencyVolume")]
        public bool useTranslucencyVolume = true;
        [FormerlySerializedAs("m_Intensity")]
        [Range(0f, 2f)] public float intensity = 1f;
        [FormerlySerializedAs("m_XGICharacterIntensity")]
        [Range(0f, 2f)] public float characterIntensity = 1.5f;
        [FormerlySerializedAs("m_IntensityScale")]
        [Range(0.001f, 100f)] public float intensityScale = 1f;
        [FormerlySerializedAs("m_DiffuseColorBoost")]
        [Range(1f, 4f)] public float diffuseColorBoost = 1f;
        [FormerlySerializedAs("m_AvoidBleeding")]
        [Range(0f, 1f)] public float avoidBleeding = 0.5f;
        [FormerlySerializedAs("m_EnableXGIScreenRatio")]
        [Range(0f, 1f)] public float screenRatio = 1f;
        [FormerlySerializedAs("m_EnableXGIScreenRatioSpeed")]
        [Range(0f, 1f)] public float screenRatioSpeed = 0.1f;

        [Header("Short Range AO")]
        [FormerlySerializedAs("m_UseShortRangeAO")]
        public bool shortRangeAO = true;
        [Range(0f, 1f)] public float shortRangeAOWeight = 1f;
        [FormerlySerializedAs("m_XGIApplyAOWeight")]
        [Range(0f, 1f)] public float shortRangeAOApplyWeight = 1f;
        [FormerlySerializedAs("m_ShortRangeAOSlopeCompareToleranceScale")]
        [Range(0f, 10f)] public float shortRangeAOSlopeCompareToleranceScale = 1f;

        [Header("Screen Probe")]
        [FormerlySerializedAs("m_ScreenProbeDownSampleFactor")]
        [Range(8, 64)] public int screenProbeSpacingPixels = 16;
        [FormerlySerializedAs("m_ScreenProbeAdaptiveAllocationFraction")]
        [Range(0.01f, 1f)] public float screenProbeAdaptiveAllocationFraction = 0.5f;
        [Range(8, 64)] public int screenProbeAdaptiveMinDownSampleFactor = 8;
        [FormerlySerializedAs("m_ScreenProbeTraceOctahedronResolution")]
        [Range(1, 8)] public int screenProbeTraceOctahedronResolution = 8;
        [FormerlySerializedAs("m_ScreenProbeMaxTraceDistance")]
        [Range(0.01f, 65504f)] public float screenProbeTraceDistance = 200f;
        [FormerlySerializedAs("m_DiffuseMaxTraceScreenDistance")]
        [Range(0.001f, 5f)] public float screenProbeTraceScreenDistance = 5f;
        [FormerlySerializedAs("m_ScreenProbeTraceVoxelMaxTraceStep")]
        [Range(1, 64)] public int screenProbeTraceVoxelMaxTraceSteps = 64;
        [FormerlySerializedAs("m_ScreenProbeTraceVoxelStepFactor")]
        [Range(0.1f, 10f)] public float screenProbeTraceVoxelStepFactor = 1f;
        [FormerlySerializedAs("m_EnableScreenProbeTraceHierarchically")]
        public bool screenProbeTraceHierarchically = true;
        [Range(1, 50)] public int screenProbeTraceHierarchicalMaxIterations = 50;
        [Range(0.0001f, 1f)] public float screenProbeTraceRelativeDepthThickness = 0.02f;
        [Range(0.0001f, 1f)] public float screenProbeTraceHistoryDepthTestRelativeThickness = 0.01f;
        [FormerlySerializedAs("m_ScreenProbeScreenTracesThicknessScaleWhenNoFallback")]
        [Range(0.001f, 2f)] public float screenProbeScreenTraceThicknessScaleWhenNoFallback = 2f;
        [FormerlySerializedAs("m_ScreenProbeGatherMaxRayIntensity")]
        [Range(0.01f, 40f)] public float screenProbeGatherMaxRayIntensity = 1f;
        [Range(1, 32)] public int screenProbeSampleCount = 8;
        [Range(0f, 0.98f)] public float screenProbeTemporalFeedback = 0.9f;
        [Range(0f, 0.98f)] public float screenProbeTemporalFilterHistoryWeight = 0.5f;
        [FormerlySerializedAs("m_EnableScreenProbeFilterTemporal")]
        public bool screenProbeTemporalFilter = true;
        [FormerlySerializedAs("m_EnableScreenProbeReprojection")]
        public bool screenProbeTemporalReprojection = true;
        [FormerlySerializedAs("m_ScreenProbeReprojectionMaxFramesAccumulated")]
        [Range(1, 50)] public int screenProbeReprojectionMaxFramesAccumulated = 20;
        [FormerlySerializedAs("m_ScreenProbeHistoryDistanceThreshold")]
        [Range(0.001f, 1f)] public float screenProbeHistoryDistanceThreshold = 0.02f;
        [Range(0f, 180f)] public float screenProbeTemporalHistoryNormalThreshold = 45f;
        [FormerlySerializedAs("m_ScreenProbeReprojection_DepthRejectParamsA")]
        [Range(1f, 50f)] public float screenProbeReprojectionDepthRejectParamsA = 4f;
        [FormerlySerializedAs("m_ScreenProbeReprojection_DepthRejectParamsB")]
        [Range(1f, 50f)] public float screenProbeReprojectionDepthRejectParamsB = 2f;
        [FormerlySerializedAs("m_ScreenProbeTemporalExposureCheckThreshold")]
        [Range(0.01f, 2f)] public float screenProbeTemporalExposureCheckThreshold = 0.1f;
        [FormerlySerializedAs("m_ScreenProbeTemporalPlayerVelocityThreshold")]
        [Range(0f, 10f)] public float screenProbeTemporalPlayerVelocityThreshold = 0.1f;
        [Range(0f, 1f)] public float screenProbeApplyStrength = 1f;
        public bool screenProbeTraceCompact = true;
        [SerializeField, HideInInspector]
        private bool m_ScreenProbeTraceDisableCompact;
        public bool screenProbeTraceHardwareRay;
        [FormerlySerializedAs("m_ScreenProbeTraceUseWorldRadianceClipMap")]
        public bool screenProbeTraceUseWorldRadianceClipMap = true;
        public ScreenProbeTraceSource screenProbeTraceSources = ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.VoxelOctree | ScreenProbeTraceSource.SkyCubemap;
        [FormerlySerializedAs("m_EnableScreenProbeTraceTypes")]
        [SerializeField, HideInInspector]
        private int legacyScreenProbeTraceTypes = -1;
        [FormerlySerializedAs("m_ScreenProbeImportanceSampling")]
        public bool screenProbeImportanceSampling = true;
        [FormerlySerializedAs("m_ScreenProbeImportanceSampleLighting")]
        public bool screenProbeImportanceSampleLighting = true;
        public bool screenProbeImportanceSampleProbeRadianceHistory = true;
        [Range(0.001f, 10f)] public float screenProbeImportanceSamplingHistoryDistanceThreshold = 0.3f;
        [FormerlySerializedAs("m_ScreenProbeFixedJitterIndex")]
        [Range(-1, 16)] public int screenProbeFixedJitterIndex = -1;
        [FormerlySerializedAs("m_EnableScreenProbeFilterSpatial")]
        public bool screenProbeSpatialFilter = true;
        [FormerlySerializedAs("m_ScreenProbeFilterSpatialTimes")]
        [Range(1, 8)] public int screenProbeSpatialFilterPasses = 3;
        [Range(0, 2)] public int screenProbeSpatialFilterHalfKernelSize = 1;
        [Range(0.001f, 180f)] public float screenProbeSpatialFilterMaxRadianceHitAngle = 10f;
        [Range(0.001f, 10000f)] public float screenProbeSpatialFilterPositionWeightScale = 1000f;
        public bool screenProbeFixupBorders = true;
        [FormerlySerializedAs("m_IrradianceFormat")]
        public ScreenProbeIrradianceFormat screenProbeIrradianceFormat = ScreenProbeIrradianceFormat.SH3;
        [FormerlySerializedAs("m_IntegrateType")]
        public ScreenProbeIntegrateType screenProbeIntegrateType = ScreenProbeIntegrateType.SimpleIntegrate;
        [FormerlySerializedAs("m_IntegrateMethod")]
        public ScreenProbeIntegrateMethod screenProbeIntegrateMethod = ScreenProbeIntegrateMethod.SphericalHarmonic;
        [FormerlySerializedAs("m_SkylightLeaking")]
        [Range(0f, 1f)] public float screenProbeSkylightLeaking;
        [Range(0f, 1f)] public float screenProbeSkylightLeakingRoughness = 0.3f;
        [FormerlySerializedAs("m_FullSkylightLeakingDistance")]
        [Range(0.001f, 20f)] public float screenProbeFullSkylightLeakingDistance = 10f;
        public bool screenProbeTraceSkyCubemap = true;
        [SerializeField, HideInInspector]
        private bool m_DisableTraceSkyCubemap;

        [Header("Radiance Cache")]
        [FormerlySerializedAs("m_RadianceCacheType")]
        public ScreenProbeRadianceCacheType radianceCacheType = ScreenProbeRadianceCacheType.None;
        [FormerlySerializedAs("m_RadianceCacheForceFullUpdate")]
        public bool radianceCacheForceFullUpdate;
        [FormerlySerializedAs("m_RadianceCacheTraceHardwareRay")]
        public bool radianceCacheTraceHardwareRay;
        [FormerlySerializedAs("m_RadianceCacheCalculateIrradiance")]
        public bool radianceCacheCalculateIrradiance;
        [FormerlySerializedAs("m_EnableMultiBounceFromRadianceCache")]
        public bool radianceCacheEnableMultiBounceFromRadianceCache;
        [FormerlySerializedAs("m_RadianceProbeResolution")]
        [Range(8, 64)] public int radianceCacheRadianceProbeResolution = 32;
        [FormerlySerializedAs("m_RadianceCacheIrradianceProbeResolution")]
        [Range(6, 32)] public int radianceCacheIrradianceProbeResolution = 6;
        [FormerlySerializedAs("m_RadianceCacheOcclusionProbeResolution")]
        [Range(6, 32)] public int radianceCacheOcclusionProbeResolution = 16;
        public bool radianceCacheFilterProbes;
        [Range(0.001f, 3.14159f)] public float radianceCacheFilterMaxRadianceHitAngle = 0.2f;
        [Range(0.1f, 8f)] public float radianceCacheReprojectionRadiusScale = 1.5f;
        [FormerlySerializedAs("m_RadianceCacheClipMapNum")]
        [Range(1, 6)] public int radianceCacheClipMapCount = 5;
        [FormerlySerializedAs("m_RadianceCacheClipMapResolution")]
        [Range(1, 256)] public int radianceCacheClipMapResolution = 48;
        [FormerlySerializedAs("m_RadianceCacheClipMapWorldExtent")]
        [Range(1f, 200f)] public float radianceCacheClipMapWorldExtent = 40f;
        [FormerlySerializedAs("m_RadianceCacheNumProbesToTraceBudget")]
        [Range(1, 100000)] public int radianceCacheNumProbesToTraceBudget = 100;
        [FormerlySerializedAs("m_RadianceProbeResolution_Irradiance")]
        [Range(8, 64)] public int radianceCacheIrradianceRadianceProbeResolution = 16;
        [FormerlySerializedAs("m_RadianceCacheClipMapNum_Irradiance")]
        [Range(1, 6)] public int radianceCacheIrradianceClipMapCount = 4;
        [FormerlySerializedAs("m_RadianceCacheClipMapResolution_Irradiance")]
        [Range(1, 256)] public int radianceCacheIrradianceClipMapResolution = 64;
        [FormerlySerializedAs("m_RadianceCacheClipMapWorldExtent_Irradiance")]
        [Range(1f, 200f)] public float radianceCacheIrradianceClipMapWorldExtent = 50f;
        [FormerlySerializedAs("m_RadianceCacheNumProbesToTraceBudget_Irradiance")]
        [Range(1, 100000)] public int radianceCacheIrradianceNumProbesToTraceBudget = 200;
        [FormerlySerializedAs("m_RadianceCacheVisualizeRadiusScale")]
        [Range(0.001f, 0.5f)] public float radianceCacheVisualizeRadiusScale = 0.05f;
        [FormerlySerializedAs("m_RadianceCacheVisualizeClipmapIndex")]
        [Range(-1, 5)] public int radianceCacheVisualizeClipmapIndex = -1;
        [Range(0, 5000)] public int radianceCacheHashGridDebugMaxCellDecay = 500;

        [Header("Translucency Volume")]
        [FormerlySerializedAs("m_TranslucencyVolumeGridPixelSize")]
        [Range(8, 128)] public int translucencyVolumeGridPixelSize = 64;
        [FormerlySerializedAs("m_TranslucencyGridEndDistanceFromCamera")]
        [Range(10f, 1000f)] public float translucencyVolumeEndDistanceFromCamera = 80f;
        [FormerlySerializedAs("m_TranslucencyGridDistributionZScale")]
        [Range(1f, 6f)] public float translucencyVolumeGridDistributionZScale = 4f;
        [FormerlySerializedAs("m_TranslucencyVolumeTracingOctahedronResolution")]
        [Range(1, 8)] public int translucencyVolumeTracingOctahedronResolution = 3;
        public bool translucencyVolumeJitter = true;
        [FormerlySerializedAs("m_TranslucencyVolumeUseTemporalReprojection")]
        public bool translucencyVolumeUseTemporalReprojection = true;
        [FormerlySerializedAs("m_TranslucencyVolumeHistoryWeight")]
        [Range(0.9f, 0.99f)] public float translucencyVolumeHistoryWeight = 0.95f;
        [FormerlySerializedAs("m_TranslucencyVolumeTemporalMaxRayDirections")]
        [Range(0, 8)] public int translucencyVolumeTemporalMaxRayDirections = 8;
        [FormerlySerializedAs("m_TranslucencyVolumeSpatialFilter")]
        public bool translucencyVolumeSpatialFilter = true;
        [FormerlySerializedAs("m_TranslucencyVolumeSpatialFilterSampleCount")]
        [Range(1, 5)] public int translucencyVolumeSpatialFilterSampleCount = 3;
        [Range(0.1f, 20f)] public float translucencyVolumeSpatialFilterStandardDeviation = 5f;
        [Range(-1f, 4f)] public float translucencyVolumeGridCenterOffsetFromDepthBuffer = 0.5f;
        [Range(0f, 8f)] public float translucencyVolumeOffsetThresholdToAcceptDepthBufferOffset = 1f;
        [Range(0.1f, 10f)] public float translucencyVolumeTraceStepFactor = 1f;
        [Range(0.0001f, 2000f)] public float translucencyVolumeMaxTraceDistance = 200f;
        [Range(0f, 10f)] public float translucencyVolumeVoxelTraceStartDistanceScale = 1f;
        [Range(0.1f, 100f)] public float translucencyVolumeMaxRayIntensity = 20f;

        [Header("Scene Voxel")]
        [FormerlySerializedAs("m_SceneRepresentVoxelClipMapFirstWorldExtent")]
        [Range(1f, 1000f)] public float sceneVoxelClipMapFirstWorldExtent = 25f;
        [FormerlySerializedAs("m_SceneRepresentVoxelFollowCamera")]
        public bool sceneVoxelFollowCamera = true;
        [FormerlySerializedAs("m_SceneRepresentVoxelCameraForward")]
        [Range(0f, 100f)] public float sceneVoxelCameraForward = 10f;
        [FormerlySerializedAs("m_SceneRepresentVoxelOrigin")]
        public Vector3 sceneVoxelOrigin;
        [FormerlySerializedAs("m_SceneRepresentVoxelizeCameraType")]
        [SerializeField, HideInInspector]
        private int legacySceneVoxelizeCameraType = -1;
        [FormerlySerializedAs("voxelOctree_VoxelSize")]
        [SerializeField, HideInInspector]
        private int legacySceneVoxelOctreeVoxelSize = -1;
        [FormerlySerializedAs("voxelOctree_VoxelSizeWS")]
        [SerializeField, HideInInspector]
        private float legacySceneVoxelOctreeVoxelSizeWS = -1f;
        [FormerlySerializedAs("m_AlwaysUpdateVoxel")]
        [FormerlySerializedAs("voxelAlwaysUpdate")]
        public bool sceneVoxelAlwaysUpdate;
        [FormerlySerializedAs("m_SceneRepresentVoxelOriginUpdateDistance")]
        [Range(0.0001f, 1000f)] public float sceneVoxelOriginUpdateDistance = 50f;
        [FormerlySerializedAs("m_CloseAutoReVoxelInEditor")]
        [SerializeField, HideInInspector]
        private bool legacyCloseAutoReVoxelInEditor;
        [FormerlySerializedAs("m_SceneRepresentVoxelClipMapNum")]
        [FormerlySerializedAs("voxelOctree_ClipmapCount")]
        [Range(1, 4)] public int sceneVoxelClipMapCount = 4;
        [FormerlySerializedAs("voxelOctree_VoxelSizeScaleForClipmap")]
        [Range(1f, 4f)] public float sceneVoxelClipMapDistributionBase = 3f;
        [FormerlySerializedAs("clipmapOffset03")]
        public Vector4 sceneVoxelClipMapOffset03 = new Vector4(10f, 20f, 30f, 50f);
        [FormerlySerializedAs("clipmapUpdateDistance03")]
        public Vector4 sceneVoxelClipMapUpdateDistance03 = new Vector4(30f, 60f, 70f, 100f);
        [FormerlySerializedAs("clipmapOffset47")]
        public Vector4 sceneVoxelClipMapOffset47 = new Vector4(60f, 120f, 250f, 500f);
        [FormerlySerializedAs("clipmapUpdateDistance47")]
        public Vector4 sceneVoxelClipMapUpdateDistance47 = new Vector4(200f, 400f, 800f, 1600f);
        [FormerlySerializedAs("m_SceneRepresentVoxelClipMapResolution")]
        [Range(16, 64)] public int sceneVoxelClipMapResolution = 64;
        [FormerlySerializedAs("voxelOctree_MaterialBudget")]
        public SceneVoxelMaterialMemoryBudget sceneVoxelMaterialBudget = SceneVoxelMaterialMemoryBudget.Medium;
        [FormerlySerializedAs("voxelOctree_MaterialGenMethod")]
        public SceneVoxelMaterialGenerateMethod sceneVoxelMaterialGenerateMethod = SceneVoxelMaterialGenerateMethod.Atomic;
        [FormerlySerializedAs("drawVegetation")]
        public bool sceneVoxelDrawVegetation = true;
        [FormerlySerializedAs("drawGrass")]
        public bool sceneVoxelDrawGrass;
        [FormerlySerializedAs("voxelOctree_LightingType")]
        public SceneVoxelLightingType sceneVoxelLightingType = SceneVoxelLightingType.Indirect;
        [FormerlySerializedAs("voxelOctree_LightingDirectionalShadow")]
        public bool sceneVoxelLightingDirectionalShadow = true;
        [FormerlySerializedAs("voxelOctree_LightingPunctualLightShadow")]
        public bool sceneVoxelLightingPunctualShadow = true;
        [FormerlySerializedAs("voxelOctree_LightingSkyLight")]
        public bool sceneVoxelLightingSkyLight = true;
        [FormerlySerializedAs("m_SceneRepresentVoxelMaxSampleCount")]
        [Range(1, 200)] public int sceneVoxelMaxSampleCount = 12;
        [FormerlySerializedAs("m_SceneRepresentUpdateVoxelMultiBounce")]
        public bool sceneVoxelMultiBounce = true;
        [FormerlySerializedAs("m_VoxelDirectionsNum")]
        [Range(1, 6)] public int sceneVoxelDirectionCount = 6;
        [Range(1, 64)] public int sceneVoxelTraceMaxSteps = 64;
        [Range(0.1f, 10f)] public float sceneVoxelTraceStepFactor = 1f;
        [FormerlySerializedAs("m_VoxelDirectLightIntensity")]
        [Range(0f, 5f)] public float sceneVoxelDirectLightIntensity = 1f;
        [FormerlySerializedAs("m_VoxelDirectLightTint")]
        public Color sceneVoxelDirectLightTint = Color.white;
        [FormerlySerializedAs("m_VoxelIndirectLightIntensity")]
        [FormerlySerializedAs("m_SceneRepresentUpdateVoxelIndirectLightingScaler")]
        [Range(0f, 1f)] public float sceneVoxelIndirectLightIntensity = 0.5f;
        [FormerlySerializedAs("m_VoxelIndirectLightTint")]
        public Color sceneVoxelIndirectLightTint = Color.white;
        [FormerlySerializedAs("m_VoxelEnableSkyVis")]
        public bool sceneVoxelEnableSkyVisibility;
        [FormerlySerializedAs("m_SceneRepresentVoxelDebugExpandView")]
        public bool sceneVoxelDebugExpandView;
        [FormerlySerializedAs("m_SceneRepresentVoxelDebugExpandViewDistance")]
        [Range(0f, 5000f)] public float sceneVoxelDebugExpandViewDistance = 1000f;
        [FormerlySerializedAs("m_SceneRepresentVoxelDebugShowMipmapID")]
        [Range(0, 8)] public int sceneVoxelDebugShowMipmapID;
        public BurtXGIToolsVoxelDebugLayer sceneVoxelDebugLayer = BurtXGIToolsVoxelDebugLayer.Visibility;
        public bool sceneVoxelDebugByTrace;
        public bool sceneVoxelDebugDrawProbe;
        [Range(0.01f, 2f)] public float sceneVoxelDebugProbeSizeWS = 0.5f;
        [Range(0f, 100f)] public float localSkyProbeCameraDistance = 2f;
        [FormerlySerializedAs("m_LocalSkyProbeShowDebugSphere")]
        public bool localSkyProbeShowDebugSphere = true;

        [Header("Irradiance Field")]
        [FormerlySerializedAs("m_UseIrradianceFieldGather")]
        public bool useIrradianceFieldGather;
        [Range(0f, 2f)] public float irradianceFieldStrength = 1f;
        [FormerlySerializedAs("m_UseIrradianceFieldBaked")]
        public bool useIrradianceFieldBaked;

        private static readonly List<BurtXGILightComponent> ActiveComponents = new List<BurtXGILightComponent>();

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_ScreenProbeTraceDisableCompact)
            {
                screenProbeTraceCompact = false;
            }

            if (m_DisableTraceSkyCubemap)
            {
                screenProbeTraceSkyCubemap = false;
            }

            if (legacySceneVoxelizeCameraType >= 0)
            {
                sceneVoxelFollowCamera = legacySceneVoxelizeCameraType == 0;
            }

            if (legacySceneVoxelOctreeVoxelSize > 0 && legacySceneVoxelOctreeVoxelSizeWS > 0f)
            {
                sceneVoxelClipMapFirstWorldExtent = ResolveLegacySceneVoxelFirstWorldExtent(
                    legacySceneVoxelOctreeVoxelSize,
                    legacySceneVoxelOctreeVoxelSizeWS);
            }

            sceneVoxelClipMapCount = Mathf.Clamp(sceneVoxelClipMapCount, 1, 4);

            if (legacyScreenProbeTraceTypes >= 0)
            {
                ApplyLegacyScreenProbeTraceTypes(legacyScreenProbeTraceTypes);
            }
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_ScreenProbeTraceDisableCompact = false;
            m_DisableTraceSkyCubemap = false;
            legacySceneVoxelizeCameraType = -1;
            legacySceneVoxelOctreeVoxelSize = -1;
            legacySceneVoxelOctreeVoxelSizeWS = -1f;
            legacyScreenProbeTraceTypes = -1;
        }

        private static float ResolveLegacySceneVoxelFirstWorldExtent(int legacyVoxelSize, float legacyVoxelSizeWS)
        {
            var voxelSize = Mathf.Clamp(legacyVoxelSize, 4, 512);
            var voxelSizeWS = Mathf.Max(legacyVoxelSizeWS, 0.3f);
            return Mathf.Clamp(voxelSize * voxelSizeWS * 0.5f, 1f, 1000f);
        }

        private void ApplyLegacyScreenProbeTraceTypes(int legacyTraceTypes)
        {
            var migratedTraceSources = ScreenProbeTraceSource.None;
            if ((legacyTraceTypes & 0x0001) != 0)
            {
                migratedTraceSources |= ScreenProbeTraceSource.Screen;
            }

            if ((legacyTraceTypes & 0x0004) != 0 || (legacyTraceTypes & 0x0008) != 0)
            {
                migratedTraceSources |= ScreenProbeTraceSource.VoxelOctree;
            }

            if ((legacyTraceTypes & 0x0010) != 0)
            {
                migratedTraceSources |= ScreenProbeTraceSource.LocalSkyProbe;
            }

            if ((legacyTraceTypes & 0x0020) != 0)
            {
                screenProbeTraceHardwareRay = true;
            }

            if ((legacyTraceTypes & 0x1000) != 0)
            {
                migratedTraceSources |= ScreenProbeTraceSource.SkyCubemap;
                screenProbeTraceSkyCubemap = true;
            }

            if (migratedTraceSources != ScreenProbeTraceSource.None)
            {
                screenProbeTraceSources = migratedTraceSources;
            }
        }

        private void OnEnable()
        {
            if (!ActiveComponents.Contains(this))
            {
                ActiveComponents.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveComponents.Remove(this);
        }

        private void OnDestroy()
        {
            ActiveComponents.Remove(this);
        }

        [ContextMenu("Recreate XGI Scene Voxel")]
        public void ReCreateVoxel()
        {
            TryReCreateVoxel();
        }

        [ContextMenu("Recreate XGI Scene Voxel Octree")]
        public void ReCreateVoxelOctree()
        {
            TryReCreateVoxelOctree();
        }

        public void TryReCreateVoxel()
        {
            InvalidateSceneVoxel("ManualReCreateVoxel", invalidateOctree: false);
        }

        public void TryReCreateVoxelOctree()
        {
            InvalidateSceneVoxel("ManualReCreateVoxelOctree", invalidateOctree: true);
        }

        internal static bool TryGetBest(Camera camera, out BurtXGILightComponent component)
        {
            component = null;
            var bestPriority = int.MinValue;
            for (var index = ActiveComponents.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveComponents[index];
                if (candidate == null)
                {
                    ActiveComponents.RemoveAt(index);
                    continue;
                }

                if (!candidate.isActiveAndEnabled || candidate.priority < bestPriority)
                {
                    continue;
                }

                bestPriority = candidate.priority;
                component = candidate;
            }

            return component != null;
        }

        private static void InvalidateSceneVoxel(string reason, bool invalidateOctree)
        {
            var mainCamera = Camera.main;
            InvalidateSceneVoxel(mainCamera, reason);

            var cameras = Camera.allCameras;
            for (var index = 0; index < cameras.Length; ++index)
            {
                var camera = cameras[index];
                if (camera != null && camera != mainCamera)
                {
                    InvalidateSceneVoxel(camera, reason);
                }
            }

            if (invalidateOctree)
            {
                BurtGISceneVoxelOctreeUtility.Invalidate();
            }
        }

        private static void InvalidateSceneVoxel(Camera camera, string reason)
        {
            if (camera == null)
            {
                return;
            }

            BurtGISceneVoxelHistoryUtility.InvalidateHistory(camera, reason);
            BurtGISceneVoxelClipmapStateUtility.Invalidate(camera);
        }

        internal static string GetDebugStatus(Camera camera)
        {
            var activeCount = 0;
            for (var index = ActiveComponents.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveComponents[index];
                if (candidate == null)
                {
                    ActiveComponents.RemoveAt(index);
                    continue;
                }

                if (candidate.isActiveAndEnabled)
                {
                    activeCount++;
                }
            }

            if (!TryGetBest(camera, out var component))
            {
                return "None(Active=0)";
            }

            return "Component(Name=" + component.name +
                ",Priority=" + component.priority +
                ",Active=" + activeCount +
                ",Enable=" + component.enable +
                ",OverrideConfig=" + component.overrideConfig +
                ",ProbeFirst=" + component.useProbeFirst +
                ",Intensity=" + component.intensity.ToString("0.###") +
                ",ScreenProbeSpacing=" + component.screenProbeSpacingPixels +
                ",RadianceCache=" + component.radianceCacheType +
                ",HashGridDebugDecay=" + component.radianceCacheHashGridDebugMaxCellDecay +
                ",IrradianceField=" + component.useIrradianceFieldGather +
                ",LocalSkyProbeDistance=" + component.localSkyProbeCameraDistance.ToString("0.###") +
                ",LocalSkyProbeShowDebug=" + component.localSkyProbeShowDebugSphere +
                ",SceneVoxelDirections=" + component.sceneVoxelDirectionCount +
                ",SceneVoxelTrace=" + component.sceneVoxelTraceMaxSteps + "x" + component.sceneVoxelTraceStepFactor.ToString("0.###") +
                ",SceneVoxel=" + component.sceneVoxelLightingType + ")";
        }

        internal BurtScreenSpaceGlobalIlluminationSettings ApplyToSettings(BurtScreenSpaceGlobalIlluminationSettings settings)
        {
            if (!settings.Enabled)
            {
                return settings;
            }

            var directTint = sceneVoxelDirectLightTint.linear;
            var indirectTint = sceneVoxelIndirectLightTint.linear;
            var constrainedBackfaceDiffuse = overrideConfig ? enableBackfaceDiffuse : enableBackfaceDiffuse && settings.EnableBackfaceDiffuse;
            var constrainedRoughSpecular = overrideConfig ? enableRoughSpecular : enableRoughSpecular && settings.EnableRoughSpecular;
            var constrainedTranslucencyVolume = overrideConfig ? useTranslucencyVolume : useTranslucencyVolume && settings.UseTranslucencyVolume;
            return new BurtScreenSpaceGlobalIlluminationSettings(
                true,
                settings.Quality,
                settings.Resolution,
                intensity,
                settings.Radius,
                settings.SampleCount,
                settings.MaxSteps,
                settings.Thickness,
                settings.SkyFallback,
                settings.RadianceClamp,
                settings.NormalWeight,
                settings.DistanceFade,
                settings.Blur,
                settings.BlurSharpness,
                settings.SpatialDenoiseRadius,
                settings.SpatialDenoiseStrength,
                settings.LeakGuardStrength,
                settings.EdgeFadeStrength,
                settings.NormalConeTightness,
                settings.SkyEdgeSuppression,
                shortRangeAO,
                shortRangeAOWeight,
                shortRangeAOApplyWeight,
                shortRangeAOSlopeCompareToleranceScale,
                settings.TemporalAccumulation,
                settings.TemporalFeedback,
                settings.TemporalDepthRejection,
                settings.TemporalNormalRejection,
                settings.TemporalClamp,
                settings.TemporalVarianceClamp,
                settings.TemporalHitRejection,
                useIrradianceFieldGather ? ScreenSpaceGlobalIlluminationFinalGather.IrradianceField : ScreenSpaceGlobalIlluminationFinalGather.ScreenProbe,
                irradianceFieldStrength,
                useIrradianceFieldBaked,
                constrainedBackfaceDiffuse,
                constrainedRoughSpecular,
                settings.ScreenProbeMaxRoughnessToEvaluateRoughSpecular,
                characterIntensity,
                screenRatio,
                screenRatioSpeed,
                constrainedTranslucencyVolume,
                sceneVoxelAlwaysUpdate,
                sceneVoxelOriginUpdateDistance,
                sceneVoxelClipMapCount,
                sceneVoxelClipMapDistributionBase,
                sceneVoxelClipMapOffset03,
                sceneVoxelClipMapUpdateDistance03,
                sceneVoxelClipMapOffset47,
                sceneVoxelClipMapUpdateDistance47,
                sceneVoxelClipMapResolution,
                sceneVoxelMaterialBudget,
                sceneVoxelMaterialGenerateMethod,
                sceneVoxelDrawVegetation,
                sceneVoxelDrawGrass,
                sceneVoxelLightingType,
                sceneVoxelLightingDirectionalShadow,
                sceneVoxelLightingPunctualShadow,
                sceneVoxelLightingSkyLight,
                diffuseColorBoost,
                avoidBleeding,
                sceneVoxelMaxSampleCount,
                sceneVoxelMultiBounce,
                sceneVoxelDirectionCount,
                sceneVoxelTraceMaxSteps,
                sceneVoxelTraceStepFactor,
                screenProbeSkylightLeaking,
                screenProbeSkylightLeakingRoughness,
                screenProbeFullSkylightLeakingDistance,
                screenProbeTraceSkyCubemap,
                sceneVoxelDirectLightIntensity,
                new Vector3(directTint.r, directTint.g, directTint.b),
                sceneVoxelIndirectLightIntensity,
                new Vector3(indirectTint.r, indirectTint.g, indirectTint.b),
                sceneVoxelEnableSkyVisibility,
                sceneVoxelDebugExpandView,
                sceneVoxelDebugExpandViewDistance,
                sceneVoxelDebugShowMipmapID,
                (int)sceneVoxelDebugLayer,
                sceneVoxelDebugByTrace,
                sceneVoxelDebugDrawProbe,
                sceneVoxelDebugProbeSizeWS,
                localSkyProbeShowDebugSphere);
        }

        internal BurtScreenSpaceGlobalIlluminationScreenProbeSettings ApplyToScreenProbeSettings(BurtScreenSpaceGlobalIlluminationScreenProbeSettings settings)
        {
            if (!settings.Enabled)
            {
                return settings;
            }

            var constrainedTraceSources = overrideConfig ? screenProbeTraceSources : screenProbeTraceSources & settings.TraceSources;
            if (!overrideConfig && settings.TraceSkyCubemap && screenProbeTraceSkyCubemap)
            {
                constrainedTraceSources |= ScreenProbeTraceSource.SkyCubemap;
            }
            if (!screenProbeTraceSkyCubemap)
            {
                constrainedTraceSources &= ~ScreenProbeTraceSource.SkyCubemap;
            }

            var constrainedRadianceCacheType = radianceCacheType;
            if (!overrideConfig && settings.RadianceCacheType == ScreenProbeRadianceCacheType.ClipMap && radianceCacheType == ScreenProbeRadianceCacheType.None)
            {
                constrainedRadianceCacheType = ScreenProbeRadianceCacheType.ClipMap;
            }

            var constrainedTraceWorldRadianceClipMap = screenProbeTraceUseWorldRadianceClipMap ||
                (!overrideConfig && settings.TraceUseWorldRadianceClipMap);

            return new BurtScreenSpaceGlobalIlluminationScreenProbeSettings(
                true,
                screenProbeSpacingPixels,
                screenProbeAdaptiveAllocationFraction,
                screenProbeAdaptiveMinDownSampleFactor,
                screenProbeTraceOctahedronResolution,
                screenProbeTraceDistance,
                screenProbeTraceScreenDistance,
                screenProbeTraceVoxelMaxTraceSteps,
                screenProbeTraceVoxelStepFactor,
                screenProbeTraceHierarchically,
                screenProbeTraceHierarchicalMaxIterations,
                screenProbeTraceRelativeDepthThickness,
                screenProbeTraceHistoryDepthTestRelativeThickness,
                screenProbeScreenTraceThicknessScaleWhenNoFallback,
                screenProbeGatherMaxRayIntensity,
                intensityScale,
                screenProbeSampleCount,
                screenProbeTemporalFeedback,
                screenProbeTemporalFilterHistoryWeight,
                screenProbeTemporalFilter,
                screenProbeTemporalReprojection,
                screenProbeReprojectionMaxFramesAccumulated,
                screenProbeHistoryDistanceThreshold,
                screenProbeTemporalHistoryNormalThreshold,
                screenProbeReprojectionDepthRejectParamsA,
                screenProbeReprojectionDepthRejectParamsB,
                screenProbeTemporalExposureCheckThreshold,
                screenProbeTemporalPlayerVelocityThreshold,
                screenProbeApplyStrength,
                (screenProbeTraceCompact || screenProbeTraceHardwareRay) &&
                    BurtScreenSpaceGlobalIlluminationPassUtility.SupportsScreenProbeTraceCompactCompute(),
                screenProbeTraceHardwareRay,
                constrainedTraceWorldRadianceClipMap &&
                    BurtScreenSpaceGlobalIlluminationPassUtility.SupportsRadianceCacheClipMapCompute(),
                constrainedTraceSources,
                screenProbeImportanceSampling,
                screenProbeImportanceSampleLighting,
                screenProbeImportanceSampleProbeRadianceHistory,
                screenProbeImportanceSamplingHistoryDistanceThreshold,
                screenProbeFixedJitterIndex,
                screenProbeSpatialFilter,
                screenProbeSpatialFilterPasses,
                screenProbeSpatialFilterHalfKernelSize,
                screenProbeSpatialFilterMaxRadianceHitAngle,
                screenProbeSpatialFilterPositionWeightScale,
                screenProbeFixupBorders,
                screenProbeIrradianceFormat,
                screenProbeIntegrateType,
                screenProbeIntegrateMethod,
                constrainedRadianceCacheType,
                radianceCacheForceFullUpdate,
                radianceCacheTraceHardwareRay,
                radianceCacheCalculateIrradiance,
                radianceCacheEnableMultiBounceFromRadianceCache,
                radianceCacheRadianceProbeResolution,
                radianceCacheIrradianceProbeResolution,
                radianceCacheOcclusionProbeResolution,
                radianceCacheFilterProbes,
                radianceCacheFilterMaxRadianceHitAngle,
                radianceCacheReprojectionRadiusScale,
                radianceCacheClipMapCount,
                radianceCacheClipMapResolution,
                radianceCacheClipMapWorldExtent,
                radianceCacheNumProbesToTraceBudget,
                radianceCacheIrradianceRadianceProbeResolution,
                radianceCacheIrradianceClipMapCount,
                radianceCacheIrradianceClipMapResolution,
                radianceCacheIrradianceClipMapWorldExtent,
                radianceCacheIrradianceNumProbesToTraceBudget,
                radianceCacheVisualizeRadiusScale,
                radianceCacheVisualizeClipmapIndex,
                radianceCacheHashGridDebugMaxCellDecay,
                translucencyVolumeGridPixelSize,
                translucencyVolumeEndDistanceFromCamera,
                translucencyVolumeGridDistributionZScale,
                translucencyVolumeTracingOctahedronResolution,
                translucencyVolumeJitter,
                translucencyVolumeUseTemporalReprojection,
                translucencyVolumeHistoryWeight,
                translucencyVolumeTemporalMaxRayDirections,
                translucencyVolumeSpatialFilter,
                translucencyVolumeSpatialFilterSampleCount,
                translucencyVolumeSpatialFilterStandardDeviation,
                translucencyVolumeGridCenterOffsetFromDepthBuffer,
                translucencyVolumeOffsetThresholdToAcceptDepthBufferOffset,
                translucencyVolumeTraceStepFactor,
                translucencyVolumeMaxTraceDistance,
                translucencyVolumeVoxelTraceStartDistanceScale,
                translucencyVolumeMaxRayIntensity,
                sceneVoxelClipMapFirstWorldExtent,
                sceneVoxelClipMapDistributionBase,
                sceneVoxelFollowCamera,
                sceneVoxelCameraForward,
                sceneVoxelOrigin);
        }
    }
}
