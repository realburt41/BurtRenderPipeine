using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    [CustomEditor(typeof(BurtGIProbeSceneBinding))]
    internal sealed class BurtGIProbeSceneBindingEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var binding = target as BurtGIProbeSceneBinding;
            if (binding == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Scene Binding", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scene", ResolveScene(binding).name);
            EditorGUILayout.LabelField("Scene Guid", ResolveSceneGuid(binding));
            EditorGUILayout.LabelField("Volume", binding.probeVolume != null ? binding.probeVolume.name : "<none>");
            EditorGUILayout.LabelField("Physical Pool", binding.physicalPool != null ? binding.physicalPool.name : "<none>");
            EditorGUILayout.LabelField("Streamer", binding.streamer != null ? binding.streamer.name : "<none>");
            EditorGUILayout.LabelField("Active Platform", binding.GetActiveBakingConfigPlatform().ToString());
            EditorGUILayout.LabelField("Active Config", FormatObjectName(binding.GetActiveBakingConfig()));
            EditorGUILayout.LabelField("Active Baked Asset", FormatObjectName(ResolveActiveBakedAsset(binding)));
            EditorGUILayout.LabelField("Time Slice", BurtGIProbeVolume.ActiveTimeSlice.ToString());
            EditorGUILayout.LabelField("Configured Time Slices", CountConfiguredTimeSlices(binding).ToString());

            DrawRuntimeSettings(binding);
            DrawStreamingStatus(binding);
            DrawDebugStatus(binding);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Refresh Scene Binding"))
                {
                    RefreshSceneBinding(binding, findConfigs: false);
                }

                if (GUILayout.Button("Find Configs For Scene"))
                {
                    RefreshSceneBinding(binding, findConfigs: true);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Apply Configuration"))
                {
                    Undo.RecordObject(binding, "Apply Burt XGI Probe Scene Binding");
                    binding.ApplyConfiguration();
                    EditorUtility.SetDirty(binding);
                    SceneView.RepaintAll();
                }

                using (new EditorGUI.DisabledScope(binding.streamer == null))
                {
                    if (GUILayout.Button("Initialize Streaming"))
                    {
                        binding.streamer.InitializeStreaming();
                        EditorUtility.SetDirty(binding.streamer);
                        SceneView.RepaintAll();
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(binding.streamer == null))
                {
                    if (GUILayout.Button("Invalidate Streaming"))
                    {
                        binding.streamer.InvalidateCachedCellData();
                        EditorUtility.SetDirty(binding.streamer);
                        SceneView.RepaintAll();
                    }
                }

                var activeAsset = ResolveActiveBakedAsset(binding);
                using (new EditorGUI.DisabledScope(activeAsset == null))
                {
                    if (GUILayout.Button("Select Baked Asset"))
                    {
                        Selection.activeObject = activeAsset;
                        EditorGUIUtility.PingObject(activeAsset);
                    }
                }
            }
        }

        private static void DrawRuntimeSettings(BurtGIProbeSceneBinding binding)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Runtime Settings", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Support XGI Probe", binding.runtimeSupportXGIProbe.ToString());
            EditorGUILayout.LabelField("Enable Shading", binding.runtimeEnableShading.ToString());
            EditorGUILayout.LabelField("Enable Sky Visibility", binding.runtimeEnableSkyVisibility.ToString());
            EditorGUILayout.LabelField("Memory Budget Limit", binding.runtimeMemoryBudgetLimit.ToString());
            EditorGUILayout.LabelField("SH Bands Limit", binding.runtimeSHBandsLimit.ToString());
            EditorGUILayout.LabelField("Automatic Streaming", binding.automaticStreaming.ToString());
            EditorGUILayout.LabelField("Max Cells Per Frame", binding.maxCellsToLoadPerFrame.ToString());
            EditorGUILayout.LabelField("Load Distance", binding.bakedDataLoadDistance.ToString("0.###"));
            EditorGUILayout.LabelField("Override Bias", (binding.runtimeOverrideNormalBias || binding.runtimeOverrideViewBias).ToString());
            EditorGUILayout.LabelField("Override Intensity", binding.runtimeOverrideLightIntensity.ToString());
            EditorGUILayout.LabelField("Override Sky Visibility", (binding.runtimeOverrideSkyVisibilityIntensity || binding.runtimeOverrideSkyVisibilityOffset).ToString());
        }

        private static void DrawStreamingStatus(BurtGIProbeSceneBinding binding)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Streaming Status", EditorStyles.boldLabel);
            var streamer = binding.streamer;
            if (streamer == null)
            {
                EditorGUILayout.HelpBox("No BurtGIVirtualProbeCellStreamer is assigned.", MessageType.Info);
                return;
            }

            var pool = streamer.physicalPool;
            var activeAsset = streamer.ActiveBakedDataAsset;
            var poolCapacity = pool != null ? pool.ChunkCapacity : 0;
            var physicalUsage = poolCapacity > 0
                ? (float)streamer.LoadedPhysicalChunkCount / poolCapacity
                : 0f;

            EditorGUILayout.LabelField("Initialized", streamer.IsInitialized.ToString());
            EditorGUILayout.LabelField("Status", streamer.LastStreamingStatus);
            EditorGUILayout.LabelField("Active Baked Asset", FormatObjectName(activeAsset));
            EditorGUILayout.LabelField("Scene Guid", string.IsNullOrEmpty(streamer.streamingSceneGuid) ? "<none>" : streamer.streamingSceneGuid);
            EditorGUILayout.LabelField("Cells Loaded / Configured", streamer.LoadedCellCount + " / " + streamer.ConfiguredCellCount);
            EditorGUILayout.LabelField("Physical Chunks", streamer.LoadedPhysicalChunkCount + " / " + poolCapacity + " (" + FormatPercent(physicalUsage) + ")");
            EditorGUILayout.LabelField("Shared Chunks", streamer.LoadedSharedChunkCount.ToString());
            EditorGUILayout.LabelField("Resolved Slices", streamer.ResolvedSliceCount.ToString());
            EditorGUILayout.LabelField("Time Slice Assets", CountTimeSliceAssets(streamer.timeSliceBakedDataAssets).ToString());
            EditorGUILayout.LabelField("Physical Pool", FormatObjectName(pool));
            if (pool != null)
            {
                EditorGUILayout.LabelField("Pool Initialized", pool.IsInitialized.ToString());
                EditorGUILayout.LabelField("Pool Chunks", pool.chunkDimensions.ToString());
                EditorGUILayout.LabelField("Pool Dimensions", pool.PhysicalPoolDimensions.ToString());
            }

            if (activeAsset != null)
            {
                EditorGUILayout.LabelField("Asset Cells", (activeAsset.cells != null ? activeAsset.cells.Length : 0).ToString());
                EditorGUILayout.LabelField("Asset Chunks", activeAsset.chunkCount.ToString());
                EditorGUILayout.LabelField("Asset Time Slice", activeAsset.timeSliceType.ToString());
                var assetSceneGuid = activeAsset.sourceConfig != null ? activeAsset.sourceConfig.sceneGuid : streamer.streamingSceneGuid;
                EditorGUILayout.LabelField("Asset Scene Guid", string.IsNullOrEmpty(assetSceneGuid) ? "<none>" : assetSceneGuid);
            }
        }

        private static void DrawDebugStatus(BurtGIProbeSceneBinding binding)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Debug Status", EditorStyles.boldLabel);
            var status = binding.GetDebugStatus();
            EditorGUILayout.TextArea(status, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 3f));
        }

        private static void RefreshSceneBinding(BurtGIProbeSceneBinding binding, bool findConfigs)
        {
            if (binding == null)
            {
                return;
            }

            Undo.RecordObject(binding, "Refresh Burt XGI Probe Scene Binding");
            binding.sceneGuid = ResolveSceneGuid(binding);

            if (findConfigs)
            {
                var scene = ResolveScene(binding);
                var pcConfig = FindBestConfigForScene(scene, BurtXGIProbeBakingPlatform.PC);
                var mobileConfig = FindBestConfigForScene(scene, BurtXGIProbeBakingPlatform.Mobile);
                if (pcConfig != null)
                {
                    binding.pcBakingConfig = pcConfig;
                    if (binding.bakingConfig == null)
                    {
                        binding.bakingConfig = pcConfig;
                    }
                }

                if (mobileConfig != null)
                {
                    binding.mobileBakingConfig = mobileConfig;
                }
            }

            var primaryConfig = binding.GetActiveBakingConfig();
            var refreshedBinding = BurtXGIProbeSceneBindingUtility.CreateOrRefresh(
                ResolveScene(binding),
                primaryConfig,
                ResolveBestBakedAsset(primaryConfig),
                false,
                true);
            binding = refreshedBinding != null ? refreshedBinding : binding;
            binding.ApplyConfiguration();
            EditorUtility.SetDirty(binding);
            if (binding.gameObject.scene.IsValid())
            {
                EditorSceneManager.MarkSceneDirty(binding.gameObject.scene);
            }

            SceneView.RepaintAll();
        }

        private static Scene ResolveScene(BurtGIProbeSceneBinding binding)
        {
            if (binding != null && binding.gameObject.scene.IsValid())
            {
                return binding.gameObject.scene;
            }

            return SceneManager.GetActiveScene();
        }

        private static string ResolveSceneGuid(BurtGIProbeSceneBinding binding)
        {
            var scene = ResolveScene(binding);
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
            {
                return string.Empty;
            }

            return AssetDatabase.AssetPathToGUID(scene.path);
        }

        private static BurtXGIProbeBakingConfig FindBestConfigForScene(Scene scene, BurtXGIProbeBakingPlatform platform)
        {
            if (!scene.IsValid())
            {
                return null;
            }

            if (BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, platform, out var resolvedConfig))
            {
                return resolvedConfig;
            }

            var sceneName = scene.name ?? string.Empty;
            var scenePath = scene.path ?? string.Empty;
            var sceneDirectory = string.IsNullOrEmpty(scenePath)
                ? string.Empty
                : scenePath.Substring(0, scenePath.LastIndexOf('/') + 1);
            var guids = AssetDatabase.FindAssets("t:BurtXGIProbeBakingConfig");
            BurtXGIProbeBakingConfig bestConfig = null;
            var bestScore = int.MinValue;
            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var config = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(path);
                if (config == null || config.platform != platform)
                {
                    continue;
                }

                var score = ScoreConfigForScene(config, path, sceneName, sceneDirectory);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestConfig = config;
            }

            return bestScore > 0 ? bestConfig : null;
        }

        private static int ScoreConfigForScene(BurtXGIProbeBakingConfig config, string path, string sceneName, string sceneDirectory)
        {
            var score = 0;
            var lowerName = config.name.ToLowerInvariant();
            var lowerPath = path.ToLowerInvariant();
            var lowerSceneName = sceneName.ToLowerInvariant();
            var lowerSceneDirectory = sceneDirectory.ToLowerInvariant();

            if (!string.IsNullOrEmpty(lowerSceneName) && lowerName.Contains(lowerSceneName))
            {
                score += 100;
            }

            if (!string.IsNullOrEmpty(lowerSceneName) && lowerPath.Contains(lowerSceneName))
            {
                score += 50;
            }

            if (!string.IsNullOrEmpty(lowerSceneDirectory) && lowerPath.StartsWith(lowerSceneDirectory, StringComparison.Ordinal))
            {
                score += 25;
            }

            if (config.bakedDataAsset != null)
            {
                score += 10;
            }

            return score;
        }

        private static BurtXGIProbeBakedDataAsset ResolveActiveBakedAsset(BurtGIProbeSceneBinding binding)
        {
            if (binding == null)
            {
                return null;
            }

            if (binding.streamer != null && binding.streamer.ActiveBakedDataAsset != null)
            {
                return binding.streamer.ActiveBakedDataAsset;
            }

            var config = binding.GetActiveBakingConfig();
            if (config == null)
            {
                return binding.GetActiveBakingConfigPlatform() == BurtGIProbeSceneBinding.PlatformMode.Mobile
                    ? binding.mobileBakedDataAsset
                    : binding.pcBakedDataAsset != null
                        ? binding.pcBakedDataAsset
                        : binding.bakedDataAsset;
            }

            if (config.TryGetBakedDataAssetForTimeSlice(BurtGIProbeVolume.ActiveTimeSlice, out var timeSliceAsset))
            {
                return timeSliceAsset;
            }

            return config.bakedDataAsset != null ? config.bakedDataAsset : binding.bakedDataAsset;
        }

        private static BurtXGIProbeBakedDataAsset ResolveBestBakedAsset(BurtXGIProbeBakingConfig config)
        {
            if (config == null)
            {
                return null;
            }

            return config.TryGetBakedDataAssetForTimeSlice(BurtGIProbeVolume.ActiveTimeSlice, out var timeSliceAsset)
                ? timeSliceAsset
                : config.bakedDataAsset;
        }

        private static int CountConfiguredTimeSlices(BurtGIProbeSceneBinding binding)
        {
            if (binding == null)
            {
                return 0;
            }

            return binding.GetActiveBakingConfigPlatform() == BurtGIProbeSceneBinding.PlatformMode.Mobile
                ? CountTimeSliceAssets(binding.mobileTimeSliceBakedDataAssets) + CountTimeSliceConfigs(binding.mobileTimeSliceBakingConfigs)
                : CountTimeSliceAssets(binding.pcTimeSliceBakedDataAssets) + CountTimeSliceConfigs(binding.pcTimeSliceBakingConfigs);
        }

        private static int CountTimeSliceAssets(System.Collections.Generic.List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> assets)
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

        private static int CountTimeSliceConfigs(System.Collections.Generic.List<BurtGIProbeSceneBinding.TimeSliceBakingConfig> configs)
        {
            if (configs == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < configs.Count; index++)
            {
                if (configs[index] != null && configs[index].config != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatObjectName(UnityEngine.Object value)
        {
            return value != null ? value.name : "<none>";
        }

        private static string FormatPercent(float value)
        {
            return Mathf.Clamp01(value).ToString("P1");
        }
    }
}
