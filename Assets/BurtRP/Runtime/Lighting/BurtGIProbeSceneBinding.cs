using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rendering/BurtRP/XGI Probe Scene Binding")]
    [MovedFrom(true, "UnityEngine.Rendering", "FunPlus.WorldX.XRender.Runtime", "XGIProbeVolumeScene")]
    public sealed class BurtGIProbeSceneBinding : MonoBehaviour
    {
        public enum PlatformMode
        {
            Auto,
            PC,
            Mobile
        }

        [Serializable]
        public sealed class TimeSliceBakingConfig
        {
            public BurtGIProbeTimeSlice timeSlice = BurtGIProbeTimeSlice.Day;
            public BurtXGIProbeBakingConfig config;
        }

        public BurtGIProbeVolume probeVolume;
        public BurtGIVirtualProbePhysicalPool physicalPool;
        public BurtGIVirtualProbeCellStreamer streamer;

        [Header("Scene")]
        public string sceneGuid = string.Empty;

        [Header("Fallback")]
        public BurtXGIProbeBakingConfig bakingConfig;
        public BurtXGIProbeBakedDataAsset bakedDataAsset;
        public List<TimeSliceBakingConfig> timeSliceBakingConfigs =
            new List<TimeSliceBakingConfig>();
        public List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> timeSliceBakedDataAssets =
            new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();

        [Header("Platform Assets")]
        public PlatformMode platformMode = PlatformMode.Auto;
        public BurtXGIProbeBakingConfig pcBakingConfig;
        public BurtXGIProbeBakedDataAsset pcBakedDataAsset;
        public List<TimeSliceBakingConfig> pcTimeSliceBakingConfigs =
            new List<TimeSliceBakingConfig>();
        public List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> pcTimeSliceBakedDataAssets =
            new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();
        public BurtXGIProbeBakingConfig mobileBakingConfig;
        public BurtXGIProbeBakedDataAsset mobileBakedDataAsset;
        public List<TimeSliceBakingConfig> mobileTimeSliceBakingConfigs =
            new List<TimeSliceBakingConfig>();
        public List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> mobileTimeSliceBakedDataAssets =
            new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();

        public bool autoFindComponents = true;
        public bool applyOnEnable = true;
        public bool unloadStreamingOnDisable = true;
        public bool initializeStreamingOnPlay;
        public bool monitorRuntimeChanges = true;
        public bool monitorInEditMode = true;
        public int priority;

        [Min(0.01f)] public float bakedDataLoadDistance = 64f;
        [Min(1)] public int maxCellsToLoadPerFrame = 1;
        public bool automaticStreaming = true;

        [Header("Runtime Platform Clamp")]
        public bool runtimeSupportXGIProbe = true;
        public bool runtimeEnableShading = true;
        public bool runtimeEnableSkyVisibility = true;
        public BurtXGIProbeTextureMemoryBudget runtimeMemoryBudgetLimit = BurtXGIProbeTextureMemoryBudget.Film;
        public BurtXGIProbeSHBands runtimeSHBandsLimit = BurtXGIProbeSHBands.SphericalHarmonicsL2;
        public bool runtimeOverrideNormalBias;
        public float runtimeNormalBias;
        public bool runtimeOverrideViewBias;
        public float runtimeViewBias;
        public bool runtimeOverrideLightIntensity;
        [Min(0f)] public float runtimeLightIntensity = 1.5f;
        public bool runtimeOverrideSkyVisibilityIntensity;
        [Range(0f, 1f)] public float runtimeSkyVisibilityIntensity = 1f;
        public bool runtimeOverrideSkyVisibilityOffset;
        public float runtimeSkyVisibilityOffset;

        private static readonly List<BurtGIProbeSceneBinding> ActiveBindings = new List<BurtGIProbeSceneBinding>();

        private PlatformMode lastAppliedPlatformMode = (PlatformMode)(-1);
        private string lastAppliedSceneGuid = string.Empty;
        private BurtXGIProbeBakingConfig lastAppliedBakingConfig;
        private BurtXGIProbeBakedDataAsset lastAppliedBakedDataAsset;
        private int lastAppliedTimeSliceAssetSignature = -1;
        private int lastAppliedRuntimeSettingsSignature = -1;

        private void OnEnable()
        {
#if UNITY_EDITOR
            RefreshSceneGuidIfNeeded();
            EnsureBakingConfigReferencesIfNeeded();
#endif
            if (!ActiveBindings.Contains(this))
            {
                ActiveBindings.Add(this);
            }

            if (applyOnEnable)
            {
                ApplyConfiguration();
            }
        }

        private void OnDisable()
        {
            ActiveBindings.Remove(this);
            if (unloadStreamingOnDisable)
            {
                ResolveComponents();
                streamer?.InvalidateCachedCellData();
            }
        }

        private void OnDestroy()
        {
            ActiveBindings.Remove(this);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            RefreshSceneGuidIfNeeded();
            EnsureBakingConfigReferencesIfNeeded();
#endif
            if (!isActiveAndEnabled)
            {
                return;
            }

            ApplyConfiguration(false);
        }

        private void Update()
        {
            if (!monitorRuntimeChanges || (!Application.isPlaying && !monitorInEditMode))
            {
                return;
            }

#if UNITY_EDITOR
            RefreshSceneGuidIfNeeded();
            EnsureBakingConfigReferencesIfNeeded();
#endif
            if (HasResolvedBindingChanged())
            {
                ApplyConfiguration(false);
            }
        }

        [ContextMenu("Apply XGI Probe Scene Binding")]
        public void ApplyConfiguration()
        {
#if UNITY_EDITOR
            RefreshSceneGuidIfNeeded();
            EnsureBakingConfigReferencesIfNeeded();
#endif
            ApplyConfiguration(initializeStreamingOnPlay && Application.isPlaying);
        }

        [ContextMenu("Clear Active XGI Probe Scene Binding")]
        public void Clear()
        {
            SetBakingConfigForPlatform(null, GetActiveBakingConfigPlatform());
        }

        public PlatformMode GetActiveBakingConfigPlatform()
        {
            return ResolvePlatformMode();
        }

        public BurtXGIProbeBakingConfig GetActiveBakingConfig()
        {
            return ResolveBakingConfig();
        }

        public void SetBakingConfigForPlatform(BurtXGIProbeBakingConfig config)
        {
            SetBakingConfigForPlatform(config, config != null && config.platform == BurtXGIProbeBakingPlatform.Mobile
                ? PlatformMode.Mobile
                : PlatformMode.PC);
        }

        public void SetBakingConfigForPlatform(BurtXGIProbeBakingConfig config, PlatformMode platform)
        {
            switch (platform)
            {
                case PlatformMode.Mobile:
                    mobileBakingConfig = config;
                    mobileBakedDataAsset = null;
                    mobileTimeSliceBakingConfigs.Clear();
                    mobileTimeSliceBakedDataAssets.Clear();
                    if (config == null)
                    {
                        ClearFallbackConfiguration();
                    }

                    break;
                case PlatformMode.PC:
                    pcBakingConfig = config;
                    pcBakedDataAsset = null;
                    pcTimeSliceBakingConfigs.Clear();
                    pcTimeSliceBakedDataAssets.Clear();
                    if (config == null)
                    {
                        ClearFallbackConfiguration();
                    }

                    break;
                default:
                    SetFallbackConfiguration(config);
                    break;
            }

            if (bakingConfig == null && config != null && platform != PlatformMode.Mobile)
            {
                bakingConfig = config;
            }

            ApplyConfiguration();
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        private void SetFallbackConfiguration(BurtXGIProbeBakingConfig config)
        {
            bakingConfig = config;
            bakedDataAsset = null;
            timeSliceBakingConfigs.Clear();
            timeSliceBakedDataAssets.Clear();
        }

        private void ClearFallbackConfiguration()
        {
            SetFallbackConfiguration(null);
        }

#if UNITY_EDITOR
        private void RefreshSceneGuidIfNeeded()
        {
            if (!string.IsNullOrEmpty(sceneGuid) ||
                !gameObject.scene.IsValid() ||
                string.IsNullOrEmpty(gameObject.scene.path))
            {
                return;
            }

            var guid = AssetDatabase.AssetPathToGUID(gameObject.scene.path);
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            sceneGuid = guid;
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
        }

        private void EnsureBakingConfigReferencesIfNeeded()
        {
            if (!gameObject.scene.IsValid())
            {
                return;
            }

            var changed = false;
            if (pcBakingConfig == null &&
                BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(gameObject.scene, BurtXGIProbeBakingPlatform.PC, out var pcConfig))
            {
                pcBakingConfig = pcConfig;
                changed = true;
            }

            if (mobileBakingConfig == null &&
                BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(gameObject.scene, BurtXGIProbeBakingPlatform.Mobile, out var mobileConfig))
            {
                mobileBakingConfig = mobileConfig;
                changed = true;
            }

            if (bakingConfig == null)
            {
                bakingConfig = ResolvePlatformMode() == PlatformMode.Mobile
                    ? mobileBakingConfig != null ? mobileBakingConfig : pcBakingConfig
                    : pcBakingConfig != null ? pcBakingConfig : mobileBakingConfig;
                changed |= bakingConfig != null;
            }

            if (changed && !Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
        }
#endif

        private void ApplyConfiguration(bool initializeStreaming)
        {
            ResolveComponents();
            if (probeVolume == null || physicalPool == null || streamer == null)
            {
                return;
            }

            var resolvedBakedDataAsset = ResolveBakedDataAsset();
            var resolvedTimeSliceAssets = ResolveTimeSliceBakedDataAssets();
            var assetBindingChanged = streamer.bakedDataAsset != resolvedBakedDataAsset ||
                !string.Equals(streamer.streamingSceneGuid, sceneGuid, StringComparison.OrdinalIgnoreCase) ||
                !TimeSliceAssetsMatch(streamer.timeSliceBakedDataAssets, resolvedTimeSliceAssets);

            physicalPool.probeVolume = probeVolume;
            streamer.probeVolume = probeVolume;
            streamer.physicalPool = physicalPool;
            streamer.bakedDataAsset = resolvedBakedDataAsset;
            streamer.streamingSceneGuid = sceneGuid;
            streamer.timeSliceBakedDataAssets = resolvedTimeSliceAssets;
            streamer.bakedDataLoadDistance = Mathf.Max(0.01f, bakedDataLoadDistance);
            streamer.maxCellsToLoadPerFrame = Mathf.Max(1, maxCellsToLoadPerFrame);
            streamer.automaticStreaming = automaticStreaming;
            var resolvedRuntimeLightIntensity = Mathf.Max(0f, runtimeLightIntensity);
            var resolvedRuntimeSkyVisibilityIntensity = Mathf.Clamp01(runtimeSkyVisibilityIntensity);
            var runtimeClampChanged = streamer.runtimeSupportXGIProbe != runtimeSupportXGIProbe ||
                streamer.runtimeEnableShading != runtimeEnableShading ||
                streamer.runtimeEnableSkyVisibility != runtimeEnableSkyVisibility ||
                streamer.runtimeMemoryBudgetLimit != runtimeMemoryBudgetLimit ||
                streamer.runtimeSHBandsLimit != runtimeSHBandsLimit ||
                streamer.runtimeOverrideNormalBias != runtimeOverrideNormalBias ||
                !Mathf.Approximately(streamer.runtimeNormalBias, runtimeNormalBias) ||
                streamer.runtimeOverrideViewBias != runtimeOverrideViewBias ||
                !Mathf.Approximately(streamer.runtimeViewBias, runtimeViewBias) ||
                streamer.runtimeOverrideLightIntensity != runtimeOverrideLightIntensity ||
                !Mathf.Approximately(streamer.runtimeLightIntensity, resolvedRuntimeLightIntensity) ||
                streamer.runtimeOverrideSkyVisibilityIntensity != runtimeOverrideSkyVisibilityIntensity ||
                !Mathf.Approximately(streamer.runtimeSkyVisibilityIntensity, resolvedRuntimeSkyVisibilityIntensity) ||
                streamer.runtimeOverrideSkyVisibilityOffset != runtimeOverrideSkyVisibilityOffset ||
                !Mathf.Approximately(streamer.runtimeSkyVisibilityOffset, runtimeSkyVisibilityOffset);
            streamer.runtimeSupportXGIProbe = runtimeSupportXGIProbe;
            streamer.runtimeEnableShading = runtimeEnableShading;
            streamer.runtimeEnableSkyVisibility = runtimeEnableSkyVisibility;
            streamer.runtimeMemoryBudgetLimit = runtimeMemoryBudgetLimit;
            streamer.runtimeSHBandsLimit = runtimeSHBandsLimit;
            streamer.runtimeOverrideNormalBias = runtimeOverrideNormalBias;
            streamer.runtimeNormalBias = runtimeNormalBias;
            streamer.runtimeOverrideViewBias = runtimeOverrideViewBias;
            streamer.runtimeViewBias = runtimeViewBias;
            streamer.runtimeOverrideLightIntensity = runtimeOverrideLightIntensity;
            streamer.runtimeLightIntensity = resolvedRuntimeLightIntensity;
            streamer.runtimeOverrideSkyVisibilityIntensity = runtimeOverrideSkyVisibilityIntensity;
            streamer.runtimeSkyVisibilityIntensity = resolvedRuntimeSkyVisibilityIntensity;
            streamer.runtimeOverrideSkyVisibilityOffset = runtimeOverrideSkyVisibilityOffset;
            streamer.runtimeSkyVisibilityOffset = runtimeSkyVisibilityOffset;
            if (assetBindingChanged || runtimeClampChanged)
            {
                streamer.InvalidateCachedCellData();
            }

            if (initializeStreaming && streamer.isActiveAndEnabled)
            {
                streamer.InitializeStreaming();
            }

            UpdateAppliedSignature(resolvedBakedDataAsset, resolvedTimeSliceAssets);
        }

        private bool HasResolvedBindingChanged()
        {
            var resolvedPlatform = ResolvePlatformMode();
            var resolvedConfig = ResolveBakingConfig();
            var resolvedAsset = ResolveBakedDataAsset();
            var resolvedTimeSliceAssets = ResolveTimeSliceBakedDataAssets();
            return resolvedPlatform != lastAppliedPlatformMode ||
                !string.Equals(sceneGuid ?? string.Empty, lastAppliedSceneGuid ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                resolvedConfig != lastAppliedBakingConfig ||
                resolvedAsset != lastAppliedBakedDataAsset ||
                CalculateTimeSliceAssetSignature(resolvedTimeSliceAssets) != lastAppliedTimeSliceAssetSignature ||
                CalculateRuntimeSettingsSignature() != lastAppliedRuntimeSettingsSignature;
        }

        private void UpdateAppliedSignature(
            BurtXGIProbeBakedDataAsset resolvedBakedDataAsset,
            List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> resolvedTimeSliceAssets)
        {
            lastAppliedPlatformMode = ResolvePlatformMode();
            lastAppliedSceneGuid = sceneGuid ?? string.Empty;
            lastAppliedBakingConfig = ResolveBakingConfig();
            lastAppliedBakedDataAsset = resolvedBakedDataAsset;
            lastAppliedTimeSliceAssetSignature = CalculateTimeSliceAssetSignature(resolvedTimeSliceAssets);
            lastAppliedRuntimeSettingsSignature = CalculateRuntimeSettingsSignature();
        }

        private void ResolveComponents()
        {
            if (!autoFindComponents)
            {
                return;
            }

            probeVolume ??= GetComponent<BurtGIProbeVolume>();
            physicalPool ??= GetComponent<BurtGIVirtualProbePhysicalPool>();
            streamer ??= GetComponent<BurtGIVirtualProbeCellStreamer>();
            probeVolume ??= GetComponentInChildren<BurtGIProbeVolume>();
            physicalPool ??= GetComponentInChildren<BurtGIVirtualProbePhysicalPool>();
            streamer ??= GetComponentInChildren<BurtGIVirtualProbeCellStreamer>();
        }

        public string GetDebugStatus()
        {
            ResolveComponents();
            return "SceneBinding(Name=" + name +
                ",Priority=" + priority +
                ",Volume=" + (probeVolume != null ? probeVolume.name : "<none>") +
                ",Pool=" + (physicalPool != null ? physicalPool.name : "<none>") +
                ",Streamer=" + (streamer != null ? streamer.name : "<none>") +
                ",SceneGuid=" + (string.IsNullOrEmpty(sceneGuid) ? "<none>" : sceneGuid) +
                ",Platform=" + ResolvePlatformMode() +
                ",Config=" + (ResolveBakingConfig() != null ? ResolveBakingConfig().name : "<none>") +
                ",BakedAsset=" + (ResolveBakedDataAsset() != null ? ResolveBakedDataAsset().name : "<none>") +
                ",TimeSliceAssets=" + CountTimeSliceAssets(ResolveTimeSliceBakedDataAssets()) +
                ",StreamingSceneGuid=" + (streamer != null && !string.IsNullOrEmpty(streamer.streamingSceneGuid) ? streamer.streamingSceneGuid : "<none>") +
                ",RuntimeSupport=" + runtimeSupportXGIProbe +
                ",RuntimeShading=" + runtimeEnableShading +
                ",RuntimeSky=" + runtimeEnableSkyVisibility +
                ",RuntimeBudgetLimit=" + runtimeMemoryBudgetLimit +
                ",RuntimeSHLimit=" + runtimeSHBandsLimit +
                ",OverrideBias=" + (runtimeOverrideNormalBias || runtimeOverrideViewBias) +
                ",OverrideIntensity=" + runtimeOverrideLightIntensity +
                ",OverrideSkyVisibility=" + (runtimeOverrideSkyVisibilityIntensity || runtimeOverrideSkyVisibilityOffset) +
                ",AutoStreaming=" + automaticStreaming + ")";
        }

        public static string GetDebugStatus(Camera camera)
        {
            var activeCount = 0;
            var bestPriority = int.MinValue;
            BurtGIProbeSceneBinding bestBinding = null;
            for (var index = ActiveBindings.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveBindings[index];
                if (candidate == null)
                {
                    ActiveBindings.RemoveAt(index);
                    continue;
                }

                if (!candidate.isActiveAndEnabled)
                {
                    continue;
                }

                activeCount++;
                candidate.ResolveComponents();
                var containsCamera = camera == null || candidate.probeVolume == null ||
                    Contains(candidate.probeVolume, camera.transform.position);
                var score = candidate.priority + (containsCamera ? 1000000 : 0);
                if (score < bestPriority)
                {
                    continue;
                }

                bestPriority = score;
                bestBinding = candidate;
            }

            return bestBinding != null
                ? bestBinding.GetDebugStatus() + ",Active=" + activeCount
                : "SceneBinding(None,Active=" + activeCount + ")";
        }

        private static bool Contains(BurtGIProbeVolume volume, Vector3 worldPosition)
        {
            if (volume == null)
            {
                return false;
            }

            var local = volume.DirectWorldToLocalMatrix.MultiplyPoint3x4(worldPosition);
            var half = volume.LocalHalfExtents;
            return Mathf.Abs(local.x) <= half.x &&
                Mathf.Abs(local.y) <= half.y &&
                Mathf.Abs(local.z) <= half.z;
        }

        private BurtXGIProbeBakedDataAsset ResolveBakedDataAsset()
        {
            switch (ResolvePlatformMode())
            {
                case PlatformMode.Mobile:
                    return mobileBakedDataAsset != null
                        ? mobileBakedDataAsset
                        : ResolveBakedDataAssetFromConfig(mobileBakingConfig) ??
                            bakedDataAsset ??
                            ResolveBakedDataAssetFromConfig(bakingConfig);
                case PlatformMode.PC:
                    return pcBakedDataAsset != null
                        ? pcBakedDataAsset
                        : ResolveBakedDataAssetFromConfig(pcBakingConfig) ??
                            bakedDataAsset ??
                            ResolveBakedDataAssetFromConfig(bakingConfig);
                default:
                    return bakedDataAsset ?? ResolveBakedDataAssetFromConfig(bakingConfig);
            }
        }

        private List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> ResolveTimeSliceBakedDataAssets()
        {
            var source = timeSliceBakedDataAssets;
            var configSource = timeSliceBakingConfigs;
            var directConfig = bakingConfig;
            switch (ResolvePlatformMode())
            {
                case PlatformMode.Mobile:
                    directConfig = mobileBakingConfig != null ? mobileBakingConfig : bakingConfig;
                    if (HasTimeSliceAssets(mobileTimeSliceBakedDataAssets))
                    {
                        source = mobileTimeSliceBakedDataAssets;
                    }
                    else if (HasTimeSliceBakingConfigs(mobileTimeSliceBakingConfigs))
                    {
                        source = null;
                        configSource = mobileTimeSliceBakingConfigs;
                    }
                    break;
                case PlatformMode.PC:
                    directConfig = pcBakingConfig != null ? pcBakingConfig : bakingConfig;
                    if (HasTimeSliceAssets(pcTimeSliceBakedDataAssets))
                    {
                        source = pcTimeSliceBakedDataAssets;
                    }
                    else if (HasTimeSliceBakingConfigs(pcTimeSliceBakingConfigs))
                    {
                        source = null;
                        configSource = pcTimeSliceBakingConfigs;
                    }
                    break;
            }

            if (HasTimeSliceAssets(source))
            {
                return CopyTimeSliceBakedDataAssets(source);
            }

            var resolvedFromConfigs = ConvertTimeSliceBakingConfigs(configSource);
            return HasTimeSliceAssets(resolvedFromConfigs)
                ? resolvedFromConfigs
                : ConvertTimeSliceBakedDataAssetsFromConfig(directConfig);
        }

        private BurtXGIProbeBakingConfig ResolveBakingConfig()
        {
            switch (ResolvePlatformMode())
            {
                case PlatformMode.Mobile:
                    return mobileBakingConfig != null ? mobileBakingConfig : bakingConfig;
                case PlatformMode.PC:
                    return pcBakingConfig != null ? pcBakingConfig : bakingConfig;
                default:
                    return bakingConfig;
            }
        }

        private static BurtXGIProbeBakedDataAsset ResolveBakedDataAssetFromConfig(BurtXGIProbeBakingConfig config)
        {
            return config != null ? config.bakedDataAsset : null;
        }

        private static List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> ConvertTimeSliceBakingConfigs(List<TimeSliceBakingConfig> configs)
        {
            var result = new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();
            if (configs == null)
            {
                return result;
            }

            for (var index = 0; index < configs.Count; index++)
            {
                var entry = configs[index];
                var timeSlice = entry != null ? BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice) : BurtGIProbeTimeSlice.Day;
                var asset = entry != null ? ResolveBakedDataAssetFromConfig(entry.config, timeSlice) : null;
                if (asset == null)
                {
                    continue;
                }

                result.Add(new BurtGIVirtualProbeCellStreamer.TimeSliceBakedData
                {
                    timeSlice = timeSlice,
                    asset = asset
                });
            }

            return result;
        }

        private static List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> ConvertTimeSliceBakedDataAssetsFromConfig(BurtXGIProbeBakingConfig config)
        {
            return config != null
                ? config.GetTimeSliceBakedDataAssets()
                : new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();
        }

        private static bool HasTimeSliceBakingConfigs(List<TimeSliceBakingConfig> configs)
        {
            if (configs == null)
            {
                return false;
            }

            for (var index = 0; index < configs.Count; index++)
            {
                var entry = configs[index];
                var timeSlice = entry != null ? BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice) : BurtGIProbeTimeSlice.Day;
                if (entry?.config != null && ResolveBakedDataAssetFromConfig(entry.config, timeSlice) != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static BurtXGIProbeBakedDataAsset ResolveBakedDataAssetFromConfig(BurtXGIProbeBakingConfig config, BurtGIProbeTimeSlice timeSlice)
        {
            if (config == null)
            {
                return null;
            }

            if (config.TryGetBakedDataAssetForTimeSlice(timeSlice, out var timeSliceAsset))
            {
                return timeSliceAsset;
            }

            return config.bakedDataAsset;
        }

        private static List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> CopyTimeSliceBakedDataAssets(List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> source)
        {
            var result = new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();
            if (source == null)
            {
                return result;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var entry = source[index];
                if (entry == null)
                {
                    result.Add(null);
                    continue;
                }

                result.Add(new BurtGIVirtualProbeCellStreamer.TimeSliceBakedData
                {
                    timeSlice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice),
                    asset = entry.asset
                });
            }

            return result;
        }

        private PlatformMode ResolvePlatformMode()
        {
            if (platformMode != PlatformMode.Auto)
            {
                return platformMode;
            }

            return BurtXGIProbeBakingConfig.GetCurrentPlatform() == BurtXGIProbeBakingPlatform.Mobile
                ? PlatformMode.Mobile
                : PlatformMode.PC;
        }

        private static bool HasTimeSliceAssets(List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> assets)
        {
            if (assets == null)
            {
                return false;
            }

            for (var index = 0; index < assets.Count; index++)
            {
                if (assets[index]?.asset != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TimeSliceAssetsMatch(
            List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> left,
            List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> right)
        {
            var leftCount = left != null ? left.Count : 0;
            var rightCount = right != null ? right.Count : 0;
            if (leftCount != rightCount)
            {
                return false;
            }

            for (var index = 0; index < leftCount; index++)
            {
                var leftEntry = left[index];
                var rightEntry = right[index];
                if (leftEntry == null || rightEntry == null)
                {
                    if (leftEntry != rightEntry)
                    {
                        return false;
                    }

                    continue;
                }

                if (BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(leftEntry.timeSlice) !=
                    BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(rightEntry.timeSlice) ||
                    leftEntry.asset != rightEntry.asset)
                {
                    return false;
                }
            }

            return true;
        }

        private static int CountTimeSliceAssets(List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> assets)
        {
            if (assets == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < assets.Count; index++)
            {
                if (assets[index]?.asset != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CalculateTimeSliceAssetSignature(List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> assets)
        {
            unchecked
            {
                var hash = 17;
                var count = assets != null ? assets.Count : 0;
                hash = hash * 31 + count;
                for (var index = 0; index < count; index++)
                {
                    var entry = assets[index];
                    hash = hash * 31 + (entry != null ? (int)BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice) : -1);
                    hash = hash * 31 + (entry?.asset != null ? entry.asset.GetInstanceID() : 0);
                }

                return hash;
            }
        }

        private int CalculateRuntimeSettingsSignature()
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + runtimeSupportXGIProbe.GetHashCode();
                hash = hash * 31 + runtimeEnableShading.GetHashCode();
                hash = hash * 31 + runtimeEnableSkyVisibility.GetHashCode();
                hash = hash * 31 + (int)runtimeMemoryBudgetLimit;
                hash = hash * 31 + (int)runtimeSHBandsLimit;
                hash = hash * 31 + runtimeOverrideNormalBias.GetHashCode();
                hash = hash * 31 + QuantizeRuntimeFloat(runtimeNormalBias);
                hash = hash * 31 + runtimeOverrideViewBias.GetHashCode();
                hash = hash * 31 + QuantizeRuntimeFloat(runtimeViewBias);
                hash = hash * 31 + runtimeOverrideLightIntensity.GetHashCode();
                hash = hash * 31 + QuantizeRuntimeFloat(Mathf.Max(0f, runtimeLightIntensity));
                hash = hash * 31 + runtimeOverrideSkyVisibilityIntensity.GetHashCode();
                hash = hash * 31 + QuantizeRuntimeFloat(Mathf.Clamp01(runtimeSkyVisibilityIntensity));
                hash = hash * 31 + runtimeOverrideSkyVisibilityOffset.GetHashCode();
                hash = hash * 31 + QuantizeRuntimeFloat(runtimeSkyVisibilityOffset);
                hash = hash * 31 + Mathf.Max(0, maxCellsToLoadPerFrame);
                hash = hash * 31 + QuantizeRuntimeFloat(Mathf.Max(0.01f, bakedDataLoadDistance));
                hash = hash * 31 + automaticStreaming.GetHashCode();
                return hash;
            }
        }

        private static int QuantizeRuntimeFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value)
                ? 0
                : Mathf.RoundToInt(value * 10000f);
        }
    }
}
