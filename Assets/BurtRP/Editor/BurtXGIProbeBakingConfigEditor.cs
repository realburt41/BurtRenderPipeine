using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    [CustomEditor(typeof(BurtXGIProbeBakingConfig))]
    internal sealed class BurtXGIProbeBakingConfigEditor : UnityEditor.Editor
    {
        private static readonly BurtGIProbeTimeSlice[] AllTimeSlices =
        {
            BurtGIProbeTimeSlice.Morning,
            BurtGIProbeTimeSlice.Day,
            BurtGIProbeTimeSlice.Sunset,
            BurtGIProbeTimeSlice.Night
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            var config = target as BurtXGIProbeBakingConfig;
            if (config == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XRender-Compatible Layout", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Cell Size", config.CellSizeInMeters.ToString("0.###") + " m");
            EditorGUILayout.LabelField("Chunk Probes", config.ChunkProbeCount.ToString());
            EditorGUILayout.LabelField("L0/L1Rx Chunk", FormatBytes(config.L0L1RxChunkSize));
            EditorGUILayout.LabelField("L1 Chunk", FormatBytes(config.L1ChunkSize));
            EditorGUILayout.LabelField("L2 Texture Chunk", FormatBytes(config.L2TextureChunkSize));
            EditorGUILayout.LabelField("Shared Chunk", FormatBytes(config.SharedDataChunkSize));
            EditorGUILayout.LabelField("GPU Chunk", FormatBytes(config.ChunkGPUMemoryBytes));
            EditorGUILayout.LabelField("Baked Cells / Bricks / Probes", config.bakedCellCount + " / " + config.bakedBrickCount + " / " + config.bakedProbeCount);
            EditorGUILayout.LabelField("Virtual Offset Vectors", config.bakedVirtualOffsetCount.ToString());
            EditorGUILayout.LabelField("Virtual Offset Invalid", config.bakedVirtualOffsetInvalidCount.ToString());
            EditorGUILayout.LabelField("Virtual Offset Applied", config.bakedVirtualOffsetApplied.ToString());
            EditorGUILayout.LabelField("Sky Visibility L0L1", config.bakedSkyVisibilityCount.ToString());
            EditorGUILayout.LabelField("Sky Direction Indices", config.bakedSkyShadingDirectionCount.ToString());
            EditorGUILayout.LabelField("Time Slice SH", config.bakedTimeSliceSHCount + " (" + config.bakedTimeSliceType + ")");
            EditorGUILayout.LabelField("Time Slice Main Light", config.bakedTimeSliceMainLightIntensity.ToString("0.###"));
            var timeSliceAssetCount = CountTimeSliceAssets(config);
            EditorGUILayout.LabelField("Time Slice Baked Assets", timeSliceAssetCount.ToString());
            EditorGUILayout.LabelField("Finalized Cells", config.bakedFinalizedCellCount.ToString());
            EditorGUILayout.LabelField("Serialized Cells / Chunks", config.bakedSerializedCellCount + " / " + config.bakedSerializedChunkCount);
            EditorGUILayout.ObjectField("Baked Data Asset", config.bakedDataAsset, typeof(BurtXGIProbeBakedDataAsset), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Scene Binding", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scene Name", string.IsNullOrEmpty(config.sceneName) ? "<none>" : config.sceneName);
            EditorGUILayout.LabelField("Scene Path", string.IsNullOrEmpty(config.scenePath) ? "<none>" : config.scenePath);
            EditorGUILayout.LabelField("Scene Guid", string.IsNullOrEmpty(config.sceneGuid) ? "<none>" : config.sceneGuid);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Capture Active Scene"))
                {
                    Undo.RecordObject(config, "Capture Burt XGI Probe Config Scene");
                    config.CaptureSceneMetadata(SceneManager.GetActiveScene());
                    EditorUtility.SetDirty(config);
                }

                if (GUILayout.Button("Refresh Scene Binding"))
                {
                    RefreshSceneBinding(config);
                }
            }

            if (config.useTimeSliceData && !config.bakeAllTimeSlices && config.EnsureSupportedTimeSliceForCurrentScene())
            {
                serializedObject.Update();
            }

            if (!SupportsSelectedTimeSliceBake(config, out var errorMessage))
            {
                EditorGUILayout.HelpBox(errorMessage, MessageType.Warning);
            }
            if (!config.useTimeSliceData)
            {
                EditorGUILayout.HelpBox("Time Slice Data is disabled. Bake will serialize probe structure, validity, and shared sky-visibility data without baked SH chunks.", MessageType.Info);
            }
            else if (SupportsSelectedTimeSliceBake(config, out _) && !BurtXGIProbeBakingConfig.HasValidTimeOfDaySource())
            {
                EditorGUILayout.HelpBox("Current scene has no valid Burt XGI time-of-day source. Only Day time slice can be baked.", MessageType.Info);
            }
            else if (config.bakeAllTimeSlices && !HasAllTimeSliceAssets(config, out var missingSlices))
            {
                EditorGUILayout.HelpBox(
                    "Bake All Time Slices is enabled, but baked assets are missing for: " + missingSlices +
                    ". Runtime will fall back to another valid slice, matching XRender, until those slices are baked.",
                    MessageType.Warning);
            }

            if (GUILayout.Button("Validate XGI Probe Baking Scene"))
            {
                var validation = BurtXGIProbeBakeAPI.ValidateScene(config);
                if (!validation.HasRuntimeProbeData || !validation.hasBakingConfig)
                {
                    Debug.LogWarning(validation.report);
                }
                else
                {
                    Debug.Log(validation.report);
                }
            }

            using (new EditorGUI.DisabledScope(Application.isPlaying || BurtXGIProbeBakeAPI.IsRunning))
            {
                if (GUILayout.Button("Bake XGI Probe Data"))
                {
                    BurtXGIProbeBakeAPI.BakeAsync(config, null, LogBakeResult);
                }

                if (config.useTimeSliceData && config.bakeAllTimeSlices && GUILayout.Button("Bake All XGI Time Slices"))
                {
                    BurtXGIProbeBakeAPI.BakeAllTimeSlicesAsync(config, null, LogBakeResult);
                }
            }
        }

        private static void RefreshSceneBinding(BurtXGIProbeBakingConfig config)
        {
            if (config == null)
            {
                return;
            }

            var scene = ResolveScene(config);
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("Burt XGI Probe", "Unable to resolve a valid scene for this baking config.", "OK");
                return;
            }

            var binding = BurtXGIProbeSceneBindingUtility.CreateOrRefresh(
                scene,
                config,
                ResolveBestBakedAsset(config),
                false,
                true);
            if (binding == null)
            {
                EditorUtility.DisplayDialog("Burt XGI Probe", "Unable to create or refresh the scene binding.", "OK");
                return;
            }

            Selection.activeGameObject = binding.gameObject;
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

        private static Scene ResolveScene(BurtXGIProbeBakingConfig config)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (config == null)
            {
                return activeScene;
            }

            if (config.MatchesScene(activeScene))
            {
                return activeScene;
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var scene = SceneManager.GetSceneAt(index);
                if (config.MatchesScene(scene))
                {
                    return scene;
                }
            }

            return activeScene;
        }

        private static void LogBakeResult(BurtXGIProbeBakeAPI.BakeResult result)
        {
            var message = "Burt XGI probe bake " + result.status + " in " +
                result.elapsedSeconds.ToString("0.###") + "s. " + result.error;
            if (result.status == BurtXGIProbeBakeAPI.BakeStatus.Success)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

        private static int CountTimeSliceAssets(BurtXGIProbeBakingConfig config)
        {
            if (config == null || config.timeSliceBakedDataAssets == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < config.timeSliceBakedDataAssets.Count; index++)
            {
                if (config.timeSliceBakedDataAssets[index]?.asset != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool SupportsSelectedTimeSliceBake(BurtXGIProbeBakingConfig config, out string errorMessage)
        {
            if (config == null)
            {
                errorMessage = "Missing Burt XGI probe baking config.";
                return false;
            }

            if (!config.bakeAllTimeSlices)
            {
                return config.SupportsCurrentTimeSliceBake(out errorMessage);
            }

            for (var index = 0; index < AllTimeSlices.Length; index++)
            {
                if (!config.SupportsTimeSliceBake(AllTimeSlices[index], out errorMessage))
                {
                    return false;
                }
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool HasAllTimeSliceAssets(BurtXGIProbeBakingConfig config, out string missingSlices)
        {
            missingSlices = string.Empty;
            if (config == null)
            {
                return false;
            }

            for (var index = 0; index < AllTimeSlices.Length; index++)
            {
                var slice = AllTimeSlices[index];
                if (config.TryGetBakedDataAssetForTimeSlice(slice, out _))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(missingSlices))
                {
                    missingSlices += ", ";
                }

                missingSlices += slice;
            }

            return string.IsNullOrEmpty(missingSlices);
        }

        private static string FormatBytes(int bytes)
        {
            if (bytes >= 1024 * 1024)
            {
                return (bytes / (1024f * 1024f)).ToString("0.##") + " MB";
            }

            if (bytes >= 1024)
            {
                return (bytes / 1024f).ToString("0.##") + " KB";
            }

            return bytes + " B";
        }
    }
}
