using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    [CustomEditor(typeof(BurtGIVirtualProbeCellStreamer))]
    internal sealed class BurtGIVirtualProbeCellStreamerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var streamer = target as BurtGIVirtualProbeCellStreamer;
            if (streamer == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Streaming Runtime", EditorStyles.boldLabel);
            DrawRuntimeStatus(streamer);
            DrawActiveAssetStatus(streamer.ActiveBakedDataAsset, streamer.streamingSceneGuid);
            DrawActions(streamer);
        }

        private static void DrawRuntimeStatus(BurtGIVirtualProbeCellStreamer streamer)
        {
            var pool = streamer.physicalPool;
            var poolCapacity = pool != null ? pool.ChunkCapacity : 0;
            var physicalUsage = poolCapacity > 0
                ? (float)streamer.OccupiedRuntimeChunkCount / poolCapacity
                : 0f;

            EditorGUILayout.LabelField("Initialized", streamer.IsInitialized.ToString());
            EditorGUILayout.LabelField("Status", streamer.LastStreamingStatus);
            EditorGUILayout.LabelField("Probe Volume", FormatObjectName(streamer.probeVolume));
            EditorGUILayout.LabelField("Physical Pool", FormatObjectName(pool));
            EditorGUILayout.LabelField("Scene Guid", string.IsNullOrEmpty(streamer.streamingSceneGuid) ? "<none>" : streamer.streamingSceneGuid);
            EditorGUILayout.LabelField("Automatic Streaming", streamer.automaticStreaming.ToString());
            EditorGUILayout.LabelField("Load Distance", streamer.bakedDataLoadDistance.ToString("0.###"));
            EditorGUILayout.LabelField("Max Cells Per Frame", streamer.maxCellsToLoadPerFrame.ToString());
            EditorGUILayout.LabelField("Cells Loaded / Configured", streamer.LoadedCellCount + " / " + streamer.ConfiguredCellCount);
            EditorGUILayout.LabelField("Physical Chunks", streamer.OccupiedRuntimeChunkCount + " / " + poolCapacity + " (" + FormatPercent(physicalUsage) + ")");
            EditorGUILayout.LabelField("SH Chunks", streamer.LoadedPhysicalChunkCount.ToString());
            EditorGUILayout.LabelField("Shared Chunks", streamer.LoadedSharedChunkCount.ToString());
            EditorGUILayout.LabelField("Resolved Slices", streamer.ResolvedSliceCount.ToString());
            EditorGUILayout.LabelField("Time Slice Assets", CountTimeSliceAssets(streamer).ToString());

            if (pool == null)
            {
                return;
            }

            EditorGUILayout.LabelField("Pool Initialized", pool.IsInitialized.ToString());
            EditorGUILayout.LabelField("Pool Chunk Dimensions", pool.chunkDimensions.ToString());
            EditorGUILayout.LabelField("Pool Texture Dimensions", pool.PhysicalPoolDimensions.ToString());
            EditorGUILayout.LabelField("Pool Feature Flags", FormatPoolFeatures(pool));
        }

        private static void DrawActiveAssetStatus(BurtXGIProbeBakedDataAsset activeAsset, string sceneGuid)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Active Baked Asset", EditorStyles.boldLabel);
            if (activeAsset == null)
            {
                EditorGUILayout.HelpBox("No active XGI baked data asset is resolved for this streamer.", MessageType.Info);
                return;
            }

            EditorGUILayout.ObjectField("Asset", activeAsset, typeof(BurtXGIProbeBakedDataAsset), false);
            EditorGUILayout.LabelField("Time Slice", activeAsset.timeSliceType.ToString());
            EditorGUILayout.LabelField("Cells", activeAsset.cellCount.ToString());
            EditorGUILayout.LabelField("Runtime Scene Cells", CountRuntimeSceneCells(activeAsset, sceneGuid).ToString());
            EditorGUILayout.LabelField("Chunks", activeAsset.chunkCount.ToString());
            EditorGUILayout.LabelField("Bricks", activeAsset.brickCount.ToString());
            EditorGUILayout.LabelField("Probes", activeAsset.probeCount.ToString());
            EditorGUILayout.LabelField("Page Table Entries", activeAsset.pageTableEntryCount.ToString());
            EditorGUILayout.LabelField("Indirection Entries", activeAsset.indirectionEntryCount.ToString());
            EditorGUILayout.LabelField("Indirection Dimensions", activeAsset.virtualIndirectionDimensions.ToString());
            EditorGUILayout.LabelField("Physical Pool Chunks", activeAsset.ResolvedPhysicalPoolChunkDimensions.ToString());
            EditorGUILayout.LabelField("Cell Size", activeAsset.ResolvedCellSizeInMeters.ToString("0.###"));
            EditorGUILayout.LabelField("Memory Budget", activeAsset.RuntimeMemoryBudget.ToString());
            EditorGUILayout.LabelField("SH Bands", activeAsset.RuntimeSHBands.ToString());
            EditorGUILayout.LabelField("Feature Flags", FormatAssetFeatures(activeAsset));
        }

        private static void DrawActions(BurtGIVirtualProbeCellStreamer streamer)
        {
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Initialize Streaming"))
                {
                    streamer.InitializeStreaming();
                    EditorUtility.SetDirty(streamer);
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Invalidate Streaming"))
                {
                    streamer.InvalidateCachedCellData();
                    EditorUtility.SetDirty(streamer);
                    SceneView.RepaintAll();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(streamer.physicalPool == null))
                {
                    if (GUILayout.Button("Initialize Pool"))
                    {
                        streamer.physicalPool.InitializePool();
                        EditorUtility.SetDirty(streamer.physicalPool);
                        SceneView.RepaintAll();
                    }
                }

                var activeAsset = streamer.ActiveBakedDataAsset;
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

        private static int CountRuntimeSceneCells(BurtXGIProbeBakedDataAsset asset, string sceneGuid)
        {
            if (asset == null)
            {
                return 0;
            }

            var cells = asset.GetRuntimeSceneCellIndices(sceneGuid);
            return cells != null ? cells.Count : asset.cells != null ? asset.cells.Length : 0;
        }

        private static int CountTimeSliceAssets(BurtGIVirtualProbeCellStreamer streamer)
        {
            var assets = streamer.timeSliceBakedDataAssets;
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

        private static string FormatAssetFeatures(BurtXGIProbeBakedDataAsset asset)
        {
            return "L2=" + asset.HasBakedL2Data +
                ",Validity=" + asset.HasBakedValidityData +
                ",SkyVisibility=" + asset.HasBakedSkyVisibilityData +
                ",SkyDirection=" + asset.HasBakedSkyShadingDirectionData +
                ",VirtualOffset=" + asset.hasVirtualOffset;
        }

        private static string FormatPoolFeatures(BurtGIVirtualProbePhysicalPool pool)
        {
            return "L2=" + pool.allocateL2 +
                ",Validity=" + pool.allocateValidity +
                ",SkyVisibility=" + pool.allocateSkyVisibility +
                ",SkyDirection=" + pool.allocateSkyShadingDirection;
        }

        private static string FormatObjectName(Object value)
        {
            return value != null ? value.name : "<none>";
        }

        private static string FormatPercent(float value)
        {
            return Mathf.Clamp01(value).ToString("P1");
        }
    }
}
