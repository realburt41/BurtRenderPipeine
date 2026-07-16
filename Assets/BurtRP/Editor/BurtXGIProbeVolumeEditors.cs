using System.Collections.Generic;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    [InitializeOnLoad]
    [CanEditMultipleObjects]
    [CustomEditor(typeof(BurtGIProbeVolume))]
    internal sealed class BurtGIProbeVolumeEditor : UnityEditor.Editor
    {
        private struct ProbeDebugDrawSettings
        {
            public bool drawCells;
            public bool drawBricks;
            public bool drawProbePositions;
            public bool drawVirtualOffsets;
            public bool drawLoadedCellsOnly;
            public bool realtimeSubdivision;
            public bool depthTest;
            public int maxDebugProbeCount;
            public int minSubdivision;
            public int maxSubdivision;
            public float probeSize;
            public float virtualOffsetSize;
            public float probeCullingDistance;
            public float cellCullingDistance;
            public BurtXGIToolsProbeDebugLayer probeDebugLayer;
        }

        private static readonly Color[] subdivisionColors =
        {
            new Color(0.15f, 0.65f, 1f, 0.85f),
            new Color(0.15f, 1f, 0.55f, 0.85f),
            new Color(1f, 0.85f, 0.15f, 0.85f),
            new Color(1f, 0.45f, 0.15f, 0.85f),
            new Color(0.95f, 0.25f, 0.85f, 0.85f),
            new Color(0.55f, 0.35f, 1f, 0.85f),
            new Color(0.95f, 0.95f, 0.95f, 0.85f),
            new Color(0.45f, 0.45f, 0.45f, 0.85f)
        };

        private static readonly List<BurtXGIProbeBakingProcessor.RealtimeSubdivisionCell> realtimeSubdivisionCells =
            new List<BurtXGIProbeBakingProcessor.RealtimeSubdivisionCell>();
        private static double realtimeSubdivisionLastUpdateTime;
        private static string realtimeSubdivisionStatus = "Idle";
        private static int realtimeSubdivisionVisibleCellCount;
        private static int realtimeSubdivisionUpdatedCellCount;
        private static int realtimeSubdivisionBrickCount;

        private readonly BoxBoundsHandle boxHandle = new BoxBoundsHandle();
        private bool runtimeInfoFoldout = true;
        private bool probeDebugFoldout;
        private bool drawProbePositions;
        private bool drawVirtualOffsets;
        private bool drawLoadedCellsOnly = true;
        private int maxDebugProbeCount = 2048;
        private float debugProbeSize = 0.08f;

        static BurtGIProbeVolumeEditor()
        {
            SceneView.duringSceneGui -= DrawGlobalProbeDebugSceneView;
            SceneView.duringSceneGui += DrawGlobalProbeDebugSceneView;
        }

        public static void ValidateXGIProbeGizmoSettingsFromCommandLine()
        {
            var report = ValidateXGIProbeGizmoSettings(out var hasIssue);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var volume = target as BurtGIProbeVolume;
            if (volume == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Runtime", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Active Time Slice", BurtGIProbeVolume.ActiveTimeSlice.ToString());
            EditorGUILayout.LabelField("Active For Slice", volume.IsActiveForCurrentTimeSlice.ToString());
            EditorGUILayout.LabelField("Virtual Ready", volume.IsVirtualReady.ToString());
            DrawRuntimeInfo(volume);
            DrawRuntimeStreamingStatus(volume);
            DrawProbeDebugControls(volume);
        }

        private void DrawRuntimeInfo(BurtGIProbeVolume volume)
        {
            EditorGUILayout.Space();
            runtimeInfoFoldout = EditorGUILayout.Foldout(runtimeInfoFoldout, "XGI Runtime Info", true);
            if (!runtimeInfoFoldout)
            {
                return;
            }

            EditorGUILayout.LabelField("Ready", volume.IsReady.ToString());
            EditorGUILayout.LabelField("Virtual Data", volume.useVirtualProbeData.ToString());
            if (!volume.useVirtualProbeData)
            {
                EditorGUILayout.ObjectField("Direct Irradiance", volume.irradiance, typeof(Texture), false);
                return;
            }

            var expectedIndirectionEntries = Mathf.Max(1, volume.virtualIndirectionDimensions.x) *
                Mathf.Max(1, volume.virtualIndirectionDimensions.y) *
                Mathf.Max(1, volume.virtualIndirectionDimensions.z);
            var loadedEntryCount = ResolveLoadedVirtualEntryCount(volume);
            var bufferSource = volume.HasRuntimeVirtualBuffers
                ? "Runtime"
                : volume.virtualPageTable != null && volume.virtualIndirection != null
                    ? "Serialized"
                    : "Missing";

            EditorGUILayout.LabelField("Buffer Source", bufferSource);
            EditorGUILayout.LabelField("Runtime Buffers", volume.HasRuntimeVirtualBuffers.ToString());
            EditorGUILayout.LabelField("Page Table Entries", volume.VirtualPageTableEntryCount.ToString());
            EditorGUILayout.LabelField("Indirection Entries", volume.VirtualIndirectionEntryCount + " / " + expectedIndirectionEntries);
            EditorGUILayout.LabelField("Indirection Usage", FormatPercent(loadedEntryCount, expectedIndirectionEntries));
            EditorGUILayout.LabelField("Loaded Entry Min", volume.virtualMinLoadedEntry.ToString());
            EditorGUILayout.LabelField("Loaded Entry Max", volume.virtualMaxLoadedEntry.ToString());
            EditorGUILayout.LabelField("Has Loaded Entries", volume.HasLoadedVirtualEntries.ToString());
            EditorGUILayout.LabelField("Entry Grid", volume.virtualIndirectionDimensions.ToString());
            EditorGUILayout.LabelField("Entry Origin", volume.virtualMinEntryIndex.ToString());
            EditorGUILayout.LabelField("Entry Size", volume.virtualIndirectionEntrySize.ToString("0.###"));
            EditorGUILayout.LabelField("Physical Pool Dimensions", volume.virtualPhysicalPoolDimensions.ToString());
            EditorGUILayout.LabelField("Estimated Physical Memory", FormatMB(EstimateVirtualPhysicalPoolBytes(volume)));
            EditorGUILayout.LabelField("Estimated Virtual Memory", FormatMB((long)volume.VirtualPageTableEntryCount * sizeof(uint) + (long)volume.VirtualIndirectionEntryCount * sizeof(uint) * 3L));
            EditorGUILayout.LabelField("Apply Enabled", (volume.virtualEnableShading && volume.HasLoadedVirtualEntries).ToString());
            EditorGUILayout.LabelField("SH Bands", volume.virtualSHBands.ToString());
            EditorGUILayout.LabelField("L1/L2", volume.HasVirtualL1 + " / " + volume.HasVirtualL2);
            EditorGUILayout.LabelField("Validity", volume.HasVirtualValidity.ToString());
            EditorGUILayout.LabelField("Sky Visibility", volume.HasVirtualSkyVisibility.ToString());
            EditorGUILayout.LabelField("Sky Direction", volume.HasVirtualSkyShadingDirection.ToString());
            EditorGUILayout.LabelField("Sky Direction Status", volume.VirtualSkyShadingDirectionStatus);
        }

        private static void DrawRuntimeStreamingStatus(BurtGIProbeVolume volume)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Streaming", EditorStyles.boldLabel);
            if (!BurtGIVirtualProbeCellStreamer.TryGetForProbeVolume(volume, out var streamer))
            {
                EditorGUILayout.HelpBox("No active BurtGIVirtualProbeCellStreamer is bound to this volume.", MessageType.Info);
                return;
            }

            var activeAsset = streamer.ActiveBakedDataAsset;
            EditorGUILayout.ObjectField("Streamer", streamer, typeof(BurtGIVirtualProbeCellStreamer), true);
            EditorGUILayout.ObjectField("Physical Pool", streamer.physicalPool, typeof(BurtGIVirtualProbePhysicalPool), true);
            EditorGUILayout.ObjectField("Active Baked Asset", activeAsset, typeof(BurtXGIProbeBakedDataAsset), false);
            EditorGUILayout.LabelField("Scene Guid", string.IsNullOrEmpty(streamer.streamingSceneGuid) ? "<none>" : streamer.streamingSceneGuid);
            EditorGUILayout.LabelField("Initialized", streamer.IsInitialized.ToString());
            EditorGUILayout.LabelField("Status", streamer.LastStreamingStatus);
            EditorGUILayout.LabelField("Cells", streamer.LoadedCellCount + " / " + streamer.ConfiguredCellCount);
            EditorGUILayout.LabelField("Physical Chunks", streamer.OccupiedRuntimeChunkCount.ToString());
            EditorGUILayout.LabelField("SH Chunks", streamer.LoadedPhysicalChunkCount.ToString());
            EditorGUILayout.LabelField("Shared Chunks", streamer.LoadedSharedChunkCount.ToString());
            EditorGUILayout.LabelField("Resolved Binary Slices", streamer.ResolvedSliceCount.ToString());
            EditorGUILayout.LabelField("Time Slice Assets", CountTimeSliceAssets(streamer).ToString());
            if (activeAsset != null)
            {
                EditorGUILayout.LabelField("Asset Cells", activeAsset.cellCount.ToString());
                EditorGUILayout.LabelField("Asset Chunks", activeAsset.chunkCount.ToString());
                EditorGUILayout.LabelField("Asset Time Slice", activeAsset.hasTimeSliceSH ? activeAsset.timeSliceType.ToString() : "None");
            }

            var pool = streamer.physicalPool;
            if (pool != null)
            {
                EditorGUILayout.LabelField("Pool Initialized", pool.IsInitialized.ToString());
                EditorGUILayout.LabelField("Pool Chunks", pool.chunkDimensions + " Capacity=" + pool.ChunkCapacity);
                EditorGUILayout.LabelField("Pool Dimensions", pool.PhysicalPoolDimensions.ToString());
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Initialize Streaming"))
                {
                    streamer.InitializeStreaming();
                    SceneView.RepaintAll();
                }

                if (GUILayout.Button("Invalidate Streaming"))
                {
                    streamer.InvalidateCachedCellData();
                    SceneView.RepaintAll();
                }

                using (new EditorGUI.DisabledScope(activeAsset == null))
                {
                    if (GUILayout.Button("Select Asset"))
                    {
                        Selection.activeObject = activeAsset;
                        EditorGUIUtility.PingObject(activeAsset);
                    }
                }
            }
        }

        private static int CountTimeSliceAssets(BurtGIVirtualProbeCellStreamer streamer)
        {
            if (streamer == null || streamer.timeSliceBakedDataAssets == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < streamer.timeSliceBakedDataAssets.Count; index++)
            {
                if (streamer.timeSliceBakedDataAssets[index]?.asset != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int ResolveLoadedVirtualEntryCount(BurtGIProbeVolume volume)
        {
            if (volume == null || !volume.HasLoadedVirtualEntries)
            {
                return 0;
            }

            var min = volume.virtualMinLoadedEntry;
            var max = volume.virtualMaxLoadedEntry;
            return Mathf.Max(0, max.x - min.x + 1) *
                Mathf.Max(0, max.y - min.y + 1) *
                Mathf.Max(0, max.z - min.z + 1);
        }

        private static long EstimateVirtualPhysicalPoolBytes(BurtGIProbeVolume volume)
        {
            if (volume == null)
            {
                return 0L;
            }

            var dimensions = volume.virtualPhysicalPoolDimensions;
            var texelCount = (long)Mathf.Max(0, dimensions.x) * Mathf.Max(0, dimensions.y) * Mathf.Max(0, dimensions.z);
            var bytesPerTexel = 8L;
            if (volume.HasVirtualL1)
            {
                bytesPerTexel += 8L;
            }

            if (volume.HasVirtualL2)
            {
                bytesPerTexel += 16L;
            }

            if (volume.HasVirtualValidity)
            {
                bytesPerTexel += 1L;
            }

            if (volume.HasVirtualSkyVisibility)
            {
                bytesPerTexel += 8L;
            }

            if (volume.HasVirtualSkyShadingDirection)
            {
                bytesPerTexel += 1L;
            }

            return texelCount * bytesPerTexel;
        }

        private static string FormatPercent(int value, int total)
        {
            return total <= 0 ? "0%" : (value * 100f / total).ToString("0.##") + "%";
        }

        private static string FormatMB(long bytes)
        {
            return (bytes / (1024f * 1024f)).ToString("0.###") + " MB";
        }

        private void DrawProbeDebugControls(BurtGIProbeVolume volume)
        {
            EditorGUILayout.Space();
            probeDebugFoldout = EditorGUILayout.Foldout(probeDebugFoldout, "XGI Probe Debug", true);
            if (!probeDebugFoldout)
            {
                return;
            }

            var canDraw = TryGetProbeDebugData(volume, out _, out _, out var probePositions, out _, out _);
            using (new EditorGUI.DisabledScope(!canDraw))
            {
                drawProbePositions = EditorGUILayout.Toggle("Draw Probe Positions", drawProbePositions);
                drawVirtualOffsets = EditorGUILayout.Toggle("Draw Virtual Offsets", drawVirtualOffsets);
                drawLoadedCellsOnly = EditorGUILayout.Toggle("Loaded Cells Only", drawLoadedCellsOnly);
                maxDebugProbeCount = EditorGUILayout.IntSlider("Max Probes", maxDebugProbeCount, 64, 32768);
                debugProbeSize = EditorGUILayout.Slider("Probe Size", debugProbeSize, 0.01f, 1f);
            }

            if (!canDraw)
            {
                EditorGUILayout.HelpBox("Probe debug needs an active baked asset with a source config that still contains baked probe positions.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Config Probe Positions", probePositions.Length.ToString());
            if (drawProbePositions || drawVirtualOffsets)
            {
                SceneView.RepaintAll();
            }
        }

        private void OnSceneGUI()
        {
            var volume = target as BurtGIProbeVolume;
            if (volume == null)
            {
                return;
            }

            var size = ResolveVolumeSize(volume);
            using (new Handles.DrawingScope(Color.cyan, Matrix4x4.TRS(volume.transform.position, volume.transform.rotation, Vector3.one)))
            {
                boxHandle.center = Vector3.zero;
                boxHandle.size = size;
                EditorGUI.BeginChangeCheck();
                boxHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new Object[] { volume, volume.transform }, "Change Burt XGI Probe Volume Bounds");
                    volume.size = Vector3.Max(Vector3.one * 0.01f, boxHandle.size);
                    volume.extent = Mathf.Max(0.01f, Mathf.Max(volume.size.x, Mathf.Max(volume.size.y, volume.size.z)) * 0.5f);
                    volume.transform.position += volume.transform.rotation * boxHandle.center;
                    EditorUtility.SetDirty(volume);
                }
            }

            DrawProbeDebugGizmos(volume, CreateLocalDebugSettings());
        }

        private ProbeDebugDrawSettings CreateLocalDebugSettings()
        {
            return new ProbeDebugDrawSettings
            {
                drawProbePositions = drawProbePositions,
                drawVirtualOffsets = drawVirtualOffsets,
                drawLoadedCellsOnly = drawLoadedCellsOnly,
                realtimeSubdivision = false,
                depthTest = true,
                maxDebugProbeCount = maxDebugProbeCount,
                minSubdivision = 0,
                maxSubdivision = BurtXGIToolsDebugComponent.MaxProbeSubdivisionLevel,
                probeSize = debugProbeSize,
                virtualOffsetSize = debugProbeSize * 0.75f,
                probeCullingDistance = float.PositiveInfinity,
                cellCullingDistance = float.PositiveInfinity,
                probeDebugLayer = BurtXGIToolsProbeDebugLayer.Visibility
            };
        }

        private static void DrawGlobalProbeDebugSceneView(SceneView sceneView)
        {
            var debugComponent = BurtXGIToolsDebugComponent.Current;
            if (debugComponent == null ||
                (!debugComponent.drawCells &&
                 !debugComponent.drawBricks &&
                 !debugComponent.drawProbes &&
                 !debugComponent.drawVirtualOffset))
            {
                return;
            }

            debugComponent.OnAfterDeserialize();
            var settings = CreateGlobalDebugSettings(debugComponent);
            if (settings.realtimeSubdivision && (settings.drawCells || settings.drawBricks))
            {
                UpdateRealtimeSubdivisionSnapshot(sceneView, debugComponent);
                DrawRealtimeSubdivisionGizmos(settings);
            }

            var volumes = Object.FindObjectsOfType<BurtGIProbeVolume>(true);
            for (var volumeIndex = 0; volumeIndex < volumes.Length; volumeIndex++)
            {
                DrawProbeDebugGizmos(volumes[volumeIndex], settings);
            }
        }

        private static ProbeDebugDrawSettings CreateGlobalDebugSettings(BurtXGIToolsDebugComponent debugComponent)
        {
            return new ProbeDebugDrawSettings
            {
                drawCells = debugComponent.drawCells,
                drawBricks = debugComponent.drawBricks,
                drawProbePositions = debugComponent.drawProbes,
                drawVirtualOffsets = debugComponent.drawVirtualOffset,
                drawLoadedCellsOnly = !debugComponent.realtimeSubdivision,
                realtimeSubdivision = debugComponent.realtimeSubdivision,
                depthTest = debugComponent.drawProbesDepthTest,
                maxDebugProbeCount = 32768,
                minSubdivision = debugComponent.minSubdivToVisualize,
                maxSubdivision = debugComponent.maxSubdivToVisualize,
                probeSize = debugComponent.drawProbeSize,
                virtualOffsetSize = debugComponent.drawVirtualOffsetSize,
                probeCullingDistance = debugComponent.drawProbeCullingDistance,
                cellCullingDistance = debugComponent.subdivisionViewCullingDistance,
                probeDebugLayer = debugComponent.drawProbesDebugLayer
            };
        }

        internal static string ValidateXGIProbeGizmoSettings(out bool hasIssue)
        {
            hasIssue = false;
            var failures = new List<string>();
            var debugObject = new GameObject("Burt XGI Probe Gizmo Settings Validation");
            try
            {
                var debugComponent = debugObject.AddComponent<BurtXGIToolsDebugComponent>();
                debugComponent.drawCells = true;
                debugComponent.drawBricks = true;
                debugComponent.drawProbes = true;
                debugComponent.drawVirtualOffset = true;
                debugComponent.realtimeSubdivision = true;
                debugComponent.drawProbesDepthTest = false;
                debugComponent.minSubdivToVisualize = 2;
                debugComponent.maxSubdivToVisualize = 5;
                debugComponent.drawProbeSize = 1.25f;
                debugComponent.drawVirtualOffsetSize = 0.35f;
                debugComponent.drawProbeCullingDistance = 321f;
                debugComponent.subdivisionViewCullingDistance = 654f;
                debugComponent.drawProbesDebugLayer = BurtXGIToolsProbeDebugLayer.SHL0L1;
                var realtimeSettings = CreateGlobalDebugSettings(debugComponent);
                AddFailureIfFalse(failures, realtimeSettings.drawCells, "RealtimeDrawCells");
                AddFailureIfFalse(failures, realtimeSettings.drawBricks, "RealtimeDrawBricks");
                AddFailureIfFalse(failures, realtimeSettings.drawProbePositions, "RealtimeDrawProbes");
                AddFailureIfFalse(failures, realtimeSettings.drawVirtualOffsets, "RealtimeDrawVirtualOffset");
                AddFailureIfFalse(failures, !realtimeSettings.drawLoadedCellsOnly, "RealtimeDrawLoadedCellsOnly");
                AddFailureIfFalse(failures, realtimeSettings.realtimeSubdivision, "RealtimeSubdivision");
                AddFailureIfFalse(failures, !realtimeSettings.depthTest, "RealtimeDepthTest");
                AddFailureIfNotEqual(failures, 32768, realtimeSettings.maxDebugProbeCount, "RealtimeMaxDebugProbeCount");
                AddFailureIfNotEqual(failures, 2, realtimeSettings.minSubdivision, "RealtimeMinSubdivision");
                AddFailureIfNotEqual(failures, 5, realtimeSettings.maxSubdivision, "RealtimeMaxSubdivision");
                AddFailureIfNotEqual(failures, 1.25f, realtimeSettings.probeSize, "RealtimeProbeSize");
                AddFailureIfNotEqual(failures, 0.35f, realtimeSettings.virtualOffsetSize, "RealtimeVirtualOffsetSize");
                AddFailureIfNotEqual(failures, 321f, realtimeSettings.probeCullingDistance, "RealtimeProbeCullingDistance");
                AddFailureIfNotEqual(failures, 654f, realtimeSettings.cellCullingDistance, "RealtimeCellCullingDistance");
                AddFailureIfFalse(failures, realtimeSettings.probeDebugLayer == BurtXGIToolsProbeDebugLayer.SHL0L1, "RealtimeProbeDebugLayer");

                debugComponent.realtimeSubdivision = false;
                var bakedSettings = CreateGlobalDebugSettings(debugComponent);
                AddFailureIfFalse(failures, bakedSettings.drawLoadedCellsOnly, "BakedDrawLoadedCellsOnly");
                AddFailureIfFalse(failures, !bakedSettings.realtimeSubdivision, "BakedRealtimeSubdivision");
                AddFailureIfFalse(failures, bakedSettings.drawCells && bakedSettings.drawBricks && bakedSettings.drawProbePositions && bakedSettings.drawVirtualOffsets, "BakedDrawFlags");

                hasIssue = failures.Count > 0;
                return "Burt XGI probe gizmo settings validation completed.\n" +
                    "Failures=" + (failures.Count > 0 ? string.Join("|", failures) : "<none>") + "\n" +
                    "RealtimeSettings=" + DescribeProbeDebugSettings(realtimeSettings) + "\n" +
                    "BakedSettings=" + DescribeProbeDebugSettings(bakedSettings);
            }
            finally
            {
                Object.DestroyImmediate(debugObject);
            }
        }

        private static string DescribeProbeDebugSettings(ProbeDebugDrawSettings settings)
        {
            return "Cells=" + settings.drawCells +
                ",Bricks=" + settings.drawBricks +
                ",Probes=" + settings.drawProbePositions +
                ",VirtualOffset=" + settings.drawVirtualOffsets +
                ",LoadedOnly=" + settings.drawLoadedCellsOnly +
                ",Realtime=" + settings.realtimeSubdivision +
                ",DepthTest=" + settings.depthTest +
                ",Subdiv=" + settings.minSubdivision + "-" + settings.maxSubdivision +
                ",ProbeSize=" + settings.probeSize.ToString("0.###") +
                ",VirtualOffsetSize=" + settings.virtualOffsetSize.ToString("0.###") +
                ",ProbeDistance=" + settings.probeCullingDistance.ToString("0.###") +
                ",CellDistance=" + settings.cellCullingDistance.ToString("0.###") +
                ",Layer=" + settings.probeDebugLayer;
        }

        private static void AddFailureIfFalse(List<string> failures, bool condition, string label)
        {
            if (!condition)
            {
                failures.Add(label);
            }
        }

        private static void AddFailureIfNotEqual(List<string> failures, int expected, int actual, string label)
        {
            if (expected != actual)
            {
                failures.Add(label + "(" + actual + "!=" + expected + ")");
            }
        }

        private static void AddFailureIfNotEqual(List<string> failures, float expected, float actual, string label)
        {
            if (Mathf.Abs(expected - actual) > 0.0001f)
            {
                failures.Add(label + "(" + actual.ToString("0.###") + "!=" + expected.ToString("0.###") + ")");
            }
        }

        private static void DrawProbeDebugGizmos(BurtGIProbeVolume volume, ProbeDebugDrawSettings settings)
        {
            if (volume == null)
            {
                return;
            }

            if (settings.drawCells && !settings.realtimeSubdivision)
            {
                DrawBakedCellGizmos(volume, settings.cellCullingDistance);
            }

            if (settings.drawBricks && !settings.realtimeSubdivision)
            {
                DrawBakedBrickGizmos(volume, settings.cellCullingDistance, settings.minSubdivision, settings.maxSubdivision);
            }

            if ((!settings.drawProbePositions && !settings.drawVirtualOffsets) ||
                !TryGetProbeDebugData(volume, out var streamer, out var asset, out var probePositions, out var adjustedProbePositions, out var virtualOffsets))
            {
                return;
            }

            var cells = asset.cells;
            if (cells == null || cells.Length == 0)
            {
                return;
            }

            var drawn = 0;
            var totalProbeCount = Mathf.Max(1, CountDrawableProbes(streamer, cells, probePositions.Length, settings.drawLoadedCellsOnly));
            var stride = Mathf.Max(1, Mathf.CeilToInt(totalProbeCount / (float)Mathf.Max(1, settings.maxDebugProbeCount)));
            var seen = 0;
            var probeSize = Mathf.Max(0.001f, settings.probeSize);
            var offsetSize = Mathf.Max(0.001f, settings.virtualOffsetSize);
            var offsetColor = new Color(1f, 0.72f, 0.18f, 0.9f);
            var camera = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : Camera.current;
            var cullingDistanceSqr = ResolveDistanceSqr(settings.probeCullingDistance);
            var cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            var oldZTest = Handles.zTest;
            Handles.zTest = settings.depthTest ? CompareFunction.LessEqual : CompareFunction.Always;

            try
            {
                for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    var cell = cells[cellIndex];
                    var start = 0;
                    var count = 0;
                    if (cell == null ||
                        (settings.drawLoadedCellsOnly && !streamer.IsCellLoaded(cell.cellIndex)) ||
                        !IsSubdivisionVisible(cell.minSubdivisionLevel, settings.minSubdivision, settings.maxSubdivision) ||
                        !TryResolveProbeRange(cell, probePositions.Length, out start, out count))
                    {
                        continue;
                    }

                    var end = start + count;
                    for (var probeIndex = start; probeIndex < end; probeIndex++)
                    {
                        if (seen++ % stride != 0)
                        {
                            continue;
                        }

                        var basePosition = probePositions[probeIndex];
                        if (camera != null && (basePosition - cameraPosition).sqrMagnitude > cullingDistanceSqr)
                        {
                            continue;
                        }

                        if (settings.drawProbePositions)
                        {
                            Handles.color = ResolveProbeColor(settings.probeDebugLayer, cell.minSubdivisionLevel);
                            Handles.SphereHandleCap(0, basePosition, Quaternion.identity, probeSize, EventType.Repaint);
                        }

                        if (settings.drawVirtualOffsets && TryResolveAdjustedProbePosition(probeIndex, basePosition, adjustedProbePositions, virtualOffsets, out var adjustedPosition))
                        {
                            Handles.color = offsetColor;
                            Handles.DrawLine(basePosition, adjustedPosition);
                            Handles.SphereHandleCap(0, adjustedPosition, Quaternion.identity, offsetSize, EventType.Repaint);
                        }

                        drawn++;
                        if (drawn >= settings.maxDebugProbeCount)
                        {
                            return;
                        }
                    }
                }
            }
            finally
            {
                Handles.zTest = oldZTest;
            }
        }

        private static bool TryGetProbeDebugData(
            BurtGIProbeVolume volume,
            out BurtGIVirtualProbeCellStreamer streamer,
            out BurtXGIProbeBakedDataAsset asset,
            out Vector3[] probePositions,
            out Vector3[] adjustedProbePositions,
            out Vector3[] virtualOffsets)
        {
            streamer = null;
            asset = null;
            probePositions = null;
            adjustedProbePositions = null;
            virtualOffsets = null;
            if (volume == null ||
                !BurtGIVirtualProbeCellStreamer.TryGetForProbeVolume(volume, out streamer))
            {
                return false;
            }

            asset = streamer.ActiveBakedDataAsset;
            var config = asset != null ? asset.sourceConfig : null;
            probePositions = config != null ? config.bakedProbePositions : null;
            adjustedProbePositions = config != null ? config.bakedVirtualOffsetProbePositions : null;
            virtualOffsets = config != null ? config.bakedVirtualOffsets : null;
            return asset != null && asset.cells != null && asset.cells.Length > 0 &&
                probePositions != null && probePositions.Length > 0;
        }

        private static int CountDrawableProbes(
            BurtGIVirtualProbeCellStreamer streamer,
            BurtXGIProbeBakedCellData[] cells,
            int probePositionCount,
            bool loadedOnly)
        {
            var count = 0;
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var cell = cells[cellIndex];
                var rangeCount = 0;
                if (cell == null ||
                    (loadedOnly && !streamer.IsCellLoaded(cell.cellIndex)) ||
                    !TryResolveProbeRange(cell, probePositionCount, out _, out rangeCount))
                {
                    continue;
                }

                count += rangeCount;
            }

            return count;
        }

        private static bool TryResolveProbeRange(BurtXGIProbeBakedCellData cell, int probePositionCount, out int start, out int count)
        {
            start = 0;
            count = 0;
            if (cell == null || probePositionCount <= 0 || cell.probeStartIndex < 0 || cell.probeCount <= 0 ||
                cell.probeStartIndex >= probePositionCount)
            {
                return false;
            }

            start = cell.probeStartIndex;
            count = Mathf.Min(cell.probeCount, probePositionCount - start);
            return count > 0;
        }

        private static bool TryResolveAdjustedProbePosition(
            int probeIndex,
            Vector3 basePosition,
            Vector3[] adjustedProbePositions,
            Vector3[] virtualOffsets,
            out Vector3 adjustedPosition)
        {
            if (adjustedProbePositions != null && (uint)probeIndex < (uint)adjustedProbePositions.Length)
            {
                adjustedPosition = adjustedProbePositions[probeIndex];
                return (adjustedPosition - basePosition).sqrMagnitude > 0.00000001f;
            }

            if (virtualOffsets != null && (uint)probeIndex < (uint)virtualOffsets.Length)
            {
                var offset = virtualOffsets[probeIndex];
                adjustedPosition = basePosition + offset;
                return offset.sqrMagnitude > 0.00000001f;
            }

            adjustedPosition = basePosition;
            return false;
        }

        private static float ResolveDistanceSqr(float distance)
        {
            if (float.IsPositiveInfinity(distance) || distance >= 100000f)
            {
                return float.PositiveInfinity;
            }

            var clamped = Mathf.Max(0.01f, distance);
            return clamped * clamped;
        }

        private static bool IsSubdivisionVisible(int subdivisionLevel, int minSubdivision, int maxSubdivision)
        {
            return subdivisionLevel >= minSubdivision && subdivisionLevel <= maxSubdivision;
        }

        private static Color ResolveProbeColor(BurtXGIToolsProbeDebugLayer layer, int subdivisionLevel)
        {
            if (layer == BurtXGIToolsProbeDebugLayer.BrickSize)
            {
                return ResolveSubdivisionColor(subdivisionLevel, 0.9f);
            }

            return layer switch
            {
                BurtXGIToolsProbeDebugLayer.Validity => new Color(0.3f, 1f, 0.35f, 0.9f),
                BurtXGIToolsProbeDebugLayer.SH_Sky_Visibility => new Color(0.35f, 0.75f, 1f, 0.9f),
                BurtXGIToolsProbeDebugLayer.SH => new Color(1f, 0.7f, 0.25f, 0.9f),
                BurtXGIToolsProbeDebugLayer.SHL0 => new Color(0.9f, 0.55f, 1f, 0.9f),
                BurtXGIToolsProbeDebugLayer.SHL0L1 => new Color(0.55f, 1f, 0.8f, 0.9f),
                _ => new Color(0.18f, 0.72f, 1f, 0.9f)
            };
        }

        private static Color ResolveSubdivisionColor(int subdivisionLevel, float alpha)
        {
            var color = subdivisionColors[Mathf.Clamp(subdivisionLevel, 0, subdivisionColors.Length - 1)];
            color.a = alpha;
            return color;
        }

        private static void UpdateRealtimeSubdivisionSnapshot(SceneView sceneView, BurtXGIToolsDebugComponent debugComponent)
        {
            if (debugComponent == null || !debugComponent.realtimeSubdivision)
            {
                realtimeSubdivisionCells.Clear();
                realtimeSubdivisionStatus = "Disabled";
                realtimeSubdivisionLastUpdateTime = 0.0;
                realtimeSubdivisionVisibleCellCount = 0;
                realtimeSubdivisionUpdatedCellCount = 0;
                realtimeSubdivisionBrickCount = 0;
                return;
            }

            var delay = Mathf.Max(0f, debugComponent.subdivisionDelayInSeconds);
            var now = EditorApplication.timeSinceStartup;
            if (realtimeSubdivisionLastUpdateTime > 0.0 && now - realtimeSubdivisionLastUpdateTime < delay)
            {
                return;
            }

            realtimeSubdivisionLastUpdateTime = now;
            var config = ResolveRealtimeSubdivisionConfig();
            if (config == null)
            {
                realtimeSubdivisionCells.Clear();
                realtimeSubdivisionStatus = "MissingConfig";
                realtimeSubdivisionVisibleCellCount = 0;
                realtimeSubdivisionUpdatedCellCount = 0;
                realtimeSubdivisionBrickCount = 0;
                return;
            }

            var camera = sceneView != null ? sceneView.camera : Camera.current;
            BurtXGIProbeBakingProcessor.Instance.BuildRealtimeSubdivisionSnapshot(
                config,
                camera,
                debugComponent.subdivisionViewCullingDistance,
                debugComponent.subdivisionCellUpdatePerFrame,
                out var result);

            realtimeSubdivisionCells.Clear();
            if (result.cells != null)
            {
                realtimeSubdivisionCells.AddRange(result.cells);
            }

            realtimeSubdivisionVisibleCellCount = result.visibleCellCount;
            realtimeSubdivisionUpdatedCellCount = result.updatedCellCount;
            realtimeSubdivisionBrickCount = result.brickCount;
            realtimeSubdivisionStatus = result.success
                ? "Live Config=" + config.name +
                  " Visible=" + result.visibleCellCount +
                  " Updated=" + result.updatedCellCount +
                  " Bricks=" + result.brickCount +
                  " Budget=" + result.maxCellBudget
                : "Error " + result.error;

            SceneView.RepaintAll();
        }

        private static BurtXGIProbeBakingConfig ResolveRealtimeSubdivisionConfig()
        {
            var scene = SceneManager.GetActiveScene();
            var config = BurtXGIProbeBakingConfig.GetBakingConfigForScene(scene);
            if (config != null)
            {
                return config;
            }

            var streamers = Object.FindObjectsOfType<BurtGIVirtualProbeCellStreamer>(true);
            for (var i = 0; i < streamers.Length; i++)
            {
                var asset = streamers[i] != null ? streamers[i].ActiveBakedDataAsset : null;
                if (asset != null && asset.sourceConfig != null)
                {
                    return asset.sourceConfig;
                }
            }

            return null;
        }

        private static void DrawRealtimeSubdivisionGizmos(ProbeDebugDrawSettings settings)
        {
            if (Event.current != null && Event.current.type != EventType.Repaint)
            {
                return;
            }

            var oldZTest = Handles.zTest;
            Handles.zTest = settings.depthTest ? CompareFunction.LessEqual : CompareFunction.Always;
            try
            {
                for (var cellIndex = 0; cellIndex < realtimeSubdivisionCells.Count; cellIndex++)
                {
                    var cell = realtimeSubdivisionCells[cellIndex];
                    if (settings.drawCells)
                    {
                        Handles.color = new Color(0.12f, 0.82f, 1f, 0.55f);
                        Handles.DrawWireCube(cell.bounds.center, cell.bounds.size);
                    }

                    if (!settings.drawBricks || cell.bricks == null)
                    {
                        continue;
                    }

                    for (var brickIndex = 0; brickIndex < cell.bricks.Count; brickIndex++)
                    {
                        var brick = cell.bricks[brickIndex];
                        if (!IsSubdivisionVisible(brick.subdivisionLevel, settings.minSubdivision, settings.maxSubdivision))
                        {
                            continue;
                        }

                        Handles.color = ResolveSubdivisionColor(brick.subdivisionLevel, 0.65f);
                        Handles.DrawWireCube(brick.bounds.center, brick.bounds.size);
                    }
                }
            }
            finally
            {
                Handles.zTest = oldZTest;
            }

            DrawRealtimeSubdivisionStatus();
        }

        private static void DrawRealtimeSubdivisionStatus()
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(12f, 64f, 540f, 42f), EditorStyles.helpBox);
            GUILayout.Label("Burt XGI Realtime Subdivision: " + realtimeSubdivisionStatus, EditorStyles.miniLabel);
            GUILayout.Label(
                "Visible Cells " + realtimeSubdivisionVisibleCellCount +
                " | Updated Cells " + realtimeSubdivisionUpdatedCellCount +
                " | Bricks " + realtimeSubdivisionBrickCount,
                EditorStyles.miniLabel);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy)]
        private static void DrawSelectedGizmo(BurtGIProbeVolume volume, GizmoType gizmoType)
        {
            if (volume == null)
            {
                return;
            }

            using (new Handles.DrawingScope(new Color(0.3f, 0.8f, 1f, 0.9f), Matrix4x4.TRS(volume.transform.position, volume.transform.rotation, Vector3.one)))
            {
                Handles.DrawWireCube(Vector3.zero, ResolveVolumeSize(volume));
            }

            DrawBakedCellGizmos(volume);
        }

        private static void DrawBakedCellGizmos(BurtGIProbeVolume volume)
        {
            DrawBakedCellGizmos(volume, -1f);
        }

        private static void DrawBakedCellGizmos(BurtGIProbeVolume volume, float maxDrawDistance)
        {
            if (!BurtGIVirtualProbeCellStreamer.TryGetForProbeVolume(volume, out var streamer))
            {
                return;
            }

            var asset = streamer.ActiveBakedDataAsset;
            if (asset == null || asset.cells == null || asset.cells.Length == 0)
            {
                return;
            }

            var camera = Camera.current;
            if (camera == null && SceneView.lastActiveSceneView != null)
            {
                camera = SceneView.lastActiveSceneView.camera;
            }

            var planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            var maxDistance = maxDrawDistance > 0f ? maxDrawDistance : streamer.bakedDataLoadDistance;
            maxDistance = Mathf.Max(0.01f, maxDistance);
            var maxDistanceSqr = maxDistance * maxDistance;
            var cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            var unloadedColor = new Color(0.2f, 0.85f, 1f, 0.45f);
            var loadedColor = new Color(0.2f, 1f, 0.45f, 0.75f);
            for (var i = 0; i < asset.cells.Length; i++)
            {
                var cell = asset.cells[i];
                if (cell == null)
                {
                    continue;
                }

                var bounds = cell.bounds;
                if (bounds.size.sqrMagnitude <= 0.000001f)
                {
                    var cellSize = Mathf.Max(0.0001f, asset.ResolvedCellSizeInMeters);
                    var center = asset.probeOffset + new Vector3(cell.position.x, cell.position.y, cell.position.z) * cellSize +
                        Vector3.one * (cellSize * 0.5f);
                    bounds = new Bounds(center, Vector3.one * cellSize);
                }

                if (camera != null)
                {
                    if ((bounds.center - cameraPosition).sqrMagnitude > maxDistanceSqr ||
                        (planes != null && !GeometryUtility.TestPlanesAABB(planes, bounds)))
                    {
                        continue;
                    }
                }

                Handles.color = streamer.IsCellLoaded(cell.cellIndex) ? loadedColor : unloadedColor;
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
        }

        private static void DrawBakedBrickGizmos(BurtGIProbeVolume volume, float maxDrawDistance, int minSubdivision, int maxSubdivision)
        {
            if (!BurtGIVirtualProbeCellStreamer.TryGetForProbeVolume(volume, out var streamer))
            {
                return;
            }

            var asset = streamer.ActiveBakedDataAsset;
            var config = asset != null ? asset.sourceConfig : null;
            var bricks = config != null ? config.bakedPlacedBricks : null;
            if (asset == null || bricks == null || bricks.Length == 0)
            {
                return;
            }

            var camera = Camera.current;
            if (camera == null && SceneView.lastActiveSceneView != null)
            {
                camera = SceneView.lastActiveSceneView.camera;
            }

            var planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            var maxDistance = maxDrawDistance > 0f ? maxDrawDistance : streamer.bakedDataLoadDistance;
            maxDistance = Mathf.Max(0.01f, maxDistance);
            var maxDistanceSqr = maxDistance * maxDistance;
            var cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            var minBrickSize = Mathf.Max(0.0001f, asset.minBrickSize);
            var probeOffset = asset.bakedProbeOffset;

            for (var brickIndex = 0; brickIndex < bricks.Length; brickIndex++)
            {
                var brick = bricks[brickIndex];
                if (!IsSubdivisionVisible(brick.subdivisionLevel, minSubdivision, maxSubdivision))
                {
                    continue;
                }

                var brickSize = BurtXGIProbeBakingConfig.GetCellSizeInBricks(brick.subdivisionLevel) * minBrickSize;
                var bounds = new Bounds(
                    probeOffset + (Vector3)brick.position * minBrickSize + Vector3.one * (brickSize * 0.5f),
                    Vector3.one * brickSize);
                if (camera != null)
                {
                    if ((bounds.center - cameraPosition).sqrMagnitude > maxDistanceSqr ||
                        (planes != null && !GeometryUtility.TestPlanesAABB(planes, bounds)))
                    {
                        continue;
                    }
                }

                Handles.color = ResolveSubdivisionColor(brick.subdivisionLevel, 0.6f);
                Handles.DrawWireCube(bounds.center, bounds.size);
            }
        }

        private static Vector3 ResolveVolumeSize(BurtGIProbeVolume volume)
        {
            if (volume == null)
            {
                return Vector3.one;
            }

            if (volume.size.x > 0f && volume.size.y > 0f && volume.size.z > 0f)
            {
                return Vector3.Max(Vector3.one * 0.01f, volume.size);
            }

            var diameter = Mathf.Max(0.01f, volume.extent * 2f);
            return new Vector3(diameter, diameter, diameter);
        }
    }

    [CanEditMultipleObjects]
    [CustomEditor(typeof(BurtXGIProbeAdjustVolume))]
    internal sealed class BurtXGIProbeAdjustVolumeEditor : UnityEditor.Editor
    {
        private readonly BoxBoundsHandle boxHandle = new BoxBoundsHandle();
        private readonly SphereBoundsHandle sphereHandle = new SphereBoundsHandle();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var volume = target as BurtXGIProbeAdjustVolume;
            if (volume == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("XGI Adjustment", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Volume", volume.ComputeVolume().ToString("0.###"));
            if (volume.mode == BurtXGIProbeAdjustVolume.AdjustmentMode.ApplyVirtualOffset)
            {
                EditorGUILayout.Vector3Field("Virtual Offset", volume.GetVirtualOffset());
            }
        }

        private void OnSceneGUI()
        {
            var volume = target as BurtXGIProbeAdjustVolume;
            if (volume == null)
            {
                return;
            }

            using (new Handles.DrawingScope(new Color(0.3f, 0.7f, 1f, 0.95f), Matrix4x4.TRS(volume.transform.position, volume.transform.rotation, Vector3.one)))
            {
                DrawVolumeHandle(volume);
                DrawVirtualOffsetHandle(volume);
            }
        }

        private void DrawVolumeHandle(BurtXGIProbeAdjustVolume volume)
        {
            if (volume.shape == BurtXGIProbeAdjustVolume.VolumeShape.Sphere)
            {
                sphereHandle.center = Vector3.zero;
                sphereHandle.radius = Mathf.Max(0.01f, volume.radius);
                EditorGUI.BeginChangeCheck();
                sphereHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObjects(new Object[] { volume, volume.transform }, "Change Burt XGI Probe Adjust Volume Radius");
                    volume.radius = Mathf.Max(0.01f, sphereHandle.radius);
                    volume.transform.position += volume.transform.rotation * sphereHandle.center;
                    EditorUtility.SetDirty(volume);
                }

                return;
            }

            boxHandle.center = Vector3.zero;
            boxHandle.size = Vector3.Max(Vector3.one * 0.01f, volume.size);
            EditorGUI.BeginChangeCheck();
            boxHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new Object[] { volume, volume.transform }, "Change Burt XGI Probe Adjust Volume Bounds");
                volume.size = Vector3.Max(Vector3.one * 0.01f, boxHandle.size);
                volume.transform.position += volume.transform.rotation * boxHandle.center;
                EditorUtility.SetDirty(volume);
            }
        }

        private static void DrawVirtualOffsetHandle(BurtXGIProbeAdjustVolume volume)
        {
            if (volume.mode != BurtXGIProbeAdjustVolume.AdjustmentMode.ApplyVirtualOffset)
            {
                return;
            }

            var rotation = Quaternion.Euler(volume.virtualOffsetRotation);
            var direction = rotation * Vector3.forward;
            var distance = Mathf.Max(0f, volume.virtualOffsetDistance);
            Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
            Handles.ArrowHandleCap(0, Vector3.zero, rotation, Mathf.Max(0.25f, distance), EventType.Repaint);

            EditorGUI.BeginChangeCheck();
            var end = Handles.Slider(direction * distance, direction);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(volume, "Change Burt XGI Virtual Offset Distance");
                volume.virtualOffsetDistance = Mathf.Max(0f, Vector3.Dot(end, direction.normalized));
                EditorUtility.SetDirty(volume);
            }

            EditorGUI.BeginChangeCheck();
            var newRotation = Handles.RotationHandle(rotation, Vector3.zero);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(volume, "Change Burt XGI Virtual Offset Direction");
                volume.virtualOffsetRotation = newRotation.eulerAngles;
                EditorUtility.SetDirty(volume);
            }
        }

        [DrawGizmo(GizmoType.InSelectionHierarchy)]
        private static void DrawSelectedGizmo(BurtXGIProbeAdjustVolume volume, GizmoType gizmoType)
        {
            if (volume == null)
            {
                return;
            }

            using (new Handles.DrawingScope(new Color(0.3f, 0.7f, 1f, 0.9f), Matrix4x4.TRS(volume.transform.position, volume.transform.rotation, Vector3.one)))
            {
                if (volume.shape == BurtXGIProbeAdjustVolume.VolumeShape.Sphere)
                {
                    Handles.DrawWireDisc(Vector3.zero, Vector3.up, Mathf.Max(0f, volume.radius));
                    Handles.DrawWireDisc(Vector3.zero, Vector3.right, Mathf.Max(0f, volume.radius));
                    Handles.DrawWireDisc(Vector3.zero, Vector3.forward, Mathf.Max(0f, volume.radius));
                }
                else
                {
                    Handles.DrawWireCube(Vector3.zero, Vector3.Max(Vector3.zero, volume.size));
                }

                if (volume.mode == BurtXGIProbeAdjustVolume.AdjustmentMode.ApplyVirtualOffset)
                {
                    Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
                    Handles.ArrowHandleCap(0, Vector3.zero, Quaternion.Euler(volume.virtualOffsetRotation), Mathf.Max(0.25f, volume.virtualOffsetDistance), EventType.Repaint);
                }
            }
        }
    }
}
