using System.Text;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    internal sealed class BurtXGIProbeBakingProcessor
    {
        internal struct PrepareResult
        {
            internal bool success;
            internal string error;
            internal int probeVolumeCount;
            internal Bounds globalBounds;
            internal Vector3Int minCellPosition;
            internal Vector3Int maxCellPosition;
            internal string report;
        }

        internal struct PlacementResult
        {
            internal bool success;
            internal string error;
            internal int cellCount;
            internal int brickCount;
            internal int probeCount;
            internal string backend;
            internal string gpuPlacementStatus;
            internal string gpuCullStatus;
            internal string report;
        }

        internal struct RealtimeSubdivisionBrick
        {
            internal Bounds bounds;
            internal int subdivisionLevel;
            internal Vector3Int position;
        }

        internal struct RealtimeSubdivisionCell
        {
            internal Bounds bounds;
            internal Vector3Int position;
            internal List<RealtimeSubdivisionBrick> bricks;
        }

        internal struct RealtimeSubdivisionSnapshotResult
        {
            internal bool success;
            internal string error;
            internal int candidateCellCount;
            internal int visibleCellCount;
            internal int updatedCellCount;
            internal int brickCount;
            internal int maxCellBudget;
            internal List<RealtimeSubdivisionCell> cells;
        }

        internal struct VirtualOffsetResult
        {
            internal bool success;
            internal string error;
            internal bool enabled;
            internal bool applied;
            internal int probeCount;
            internal int offsetCount;
            internal int invalidCount;
            internal string backend;
            internal string report;
        }

        internal struct SkyVisibilityResult
        {
            internal bool success;
            internal string error;
            internal bool enabled;
            internal bool shadingDirection;
            internal int probeCount;
            internal int occlusionCount;
            internal int directionCount;
            internal string backend;
            internal string report;
        }

        internal struct TimeSliceResult
        {
            internal bool success;
            internal string error;
            internal bool enabled;
            internal BurtGIProbeTimeSlice timeSlice;
            internal int probeCount;
            internal int shCount;
            internal int lightCount;
            internal int shadowedSampleCount;
            internal int batchCount;
            internal string backend;
            internal string report;
        }

        private struct XGIBakingProbeBatch
        {
            internal int probeStartIndex;
            internal int probeCount;
            internal Bounds bounds;
            internal bool hasBounds;
        }

        private readonly struct RealtimeSubdivisionCellCandidate
        {
            internal readonly Vector3Int Position;
            internal readonly Bounds Bounds;
            internal readonly float DistanceSqr;

            internal RealtimeSubdivisionCellCandidate(Vector3Int position, Bounds bounds, float distanceSqr)
            {
                Position = position;
                Bounds = bounds;
                DistanceSqr = distanceSqr;
            }
        }

        internal struct FinalizeCellsResult
        {
            internal bool success;
            internal string error;
            internal int cellCount;
            internal int finalizedCellCount;
            internal int chunkCount;
            internal string report;
        }

        internal struct SerializationResult
        {
            internal bool success;
            internal string error;
            internal int cellCount;
            internal int chunkCount;
            internal int pageTableEntryCount;
            internal int indirectionEntryCount;
            internal BurtXGIProbeBakedDataAsset asset;
            internal string report;
        }

        private static BurtXGIProbeBakingProcessor instance;
        private const int MaxPlacementLiteCells = 65536;
        private const int XRenderPageTableEntriesPerChunk = 243;
        private const int XRenderEntryMaxSubdivisionLevel = 3;
        private const int XGIVirtualOffsetMaxProbeCountPerBatch = 65535;
        private const int XGITimeSliceMaxProbeCountPerBatch = 65535 * 16;
        private const float SHBasis0 = 0.28209479177387814f;
        private const float SHBasis1 = 0.4886025119029199f;
        private const float SHBasis2 = 1.092548430592079f;
        private const float SHBasis3 = 0.31539156525252f;
        private const float SHBasis4 = 0.5462742152960395f;
        private const float XRenderL1CompressionScale = 4f;
        private const float XRenderL2CompressionScale = 7.1554176f;
        private const float XRenderMaxHalfValue = 65504f;
        private const int XGISkyPrecomputedDirectionCount = 255;
        private const int XGIProbeBakingThreadGroupSize = 8;
        private const int XGIPlacementGpuCullDiagnosticMaxAxis = 96;
        private const int XGIPlacementGpuDiagnosticReportCellCount = 4;
        private const int XGIPlacementDiagnosticSceneSdfTextureSize = 32;
        private const int XGIPlacementDiagnosticMeshSampleMaxCount = 512;
        private const int XGIPlacementDiagnosticGpuTriangleMaxCount = 4096;
        private const int XGIPlacementDiagnosticGpuBoundsMaxCount = 1024;
        private const string XGIProbeBakingComputeShaderResourcePath = "BurtGIXGIProbeBaking";
        private const string XGISceneVoxelBuildComputeShaderResourcePath = "BurtGISceneVoxelBuild";
        private const string XGIProbeBakingRayTracingResourcePath = "BurtGIXGIProbeBakingHardwareRayTracing";
        private const string XGIProbeBakingRayTracingPassName = "BurtGI";
        private const string XGISkyVisibilityComputeKernelName = "GenSkyVisibilityCS";
        private const string XGITimeSliceComputeKernelName = "GenTimeSliceCS";
        private const string XGIPlacementVoxelizeTrianglesKernelName = "VoxelizePlacementTriangles";
        private const string XGIPlacementProbeVolumeCullKernelName = "ProbeVolumeCull";
        private const string XGIPlacementGenBrickKernelName = "GenBrick";
        private const string XGISkyVisibilityRayGenName = "GenSkyVisibilityRGS";
        private const string XGITimeSliceRayGenName = "GenTimeSliceRGS";
        private const string XGIVirtualOffsetRayGenName = "GenVirtualOffsetRGS";
        private static readonly int XGIProbeBakingAccelerationStructureId = Shader.PropertyToID("_BurtGIXGIProbeBakingAccelerationStructure");
        private static readonly int XGIVirtualOffsetOutputId = Shader.PropertyToID("_RW_XGIVirtualOffsetGen_Output");
        private static readonly int XGIVirtualOffsetProbeDataId = Shader.PropertyToID("_XGIVirtualOffsetGen_ProbeData");
        private static readonly int XGIVirtualOffsetParamsId = Shader.PropertyToID("_XGIVirtualOffsetGen_Params");
        private static readonly int XGIVirtualOffsetParams1Id = Shader.PropertyToID("_XGIVirtualOffsetGen_Params1");
        private static readonly int XGISkyVisibilityOutputId = Shader.PropertyToID("_RW_XGISkyVisibilityGen_Output");
        private static readonly int XGISkyVisibilityDirectionOutputId = Shader.PropertyToID("_RW_XGISkyVisibilityGen_DirectionOutput");
        private static readonly int XGISkyVisibilityDirectionEncodedOutputId = Shader.PropertyToID("_RW_XGISkyVisibilityGen_DirectionEncodedOutput");
        private static readonly int XGISkyVisibilityProbePositionsId = Shader.PropertyToID("_XGISkyVisibilityGen_ProbePositions");
        private static readonly int XGISkyVisibilityParametersBufferId = Shader.PropertyToID("_XGISkyVisibilityGen_ParamtersBuffer");
        private static readonly int XGISkyVisibilityPrecomputedDirectionsId = Shader.PropertyToID("_XGISkyVisibilityGen_PrecomputedDirections");
        private static readonly int XGISkyVisibilityOffsetRayId = Shader.PropertyToID("_XGISkyVisibilityGen_OffsetRay");
        private static readonly int XGISkyVisibilityMaxBouncesId = Shader.PropertyToID("_XGISkyVisibilityGen_MaxBounces");
        private static readonly int XGISkyVisibilitySampleCountId = Shader.PropertyToID("_XGISkyVisibilityGen_SampleCount");
        private static readonly int XGISkyVisibilitySampleIndexId = Shader.PropertyToID("_XGISkyVisibilityGen_SampleIndex");
        private static readonly int XGISkyVisibilityParamsId = Shader.PropertyToID("_XGISkyVisibilityGen_Params");
        private static readonly int XGISkyVisibilityParams1Id = Shader.PropertyToID("_XGISkyVisibilityGen_Params1");
        private static readonly int XGITimeSliceOutputId = Shader.PropertyToID("_RW_XGITimeSliceGen_Output");
        private static readonly int XGITimeSliceProbePositionsId = Shader.PropertyToID("_XGITimeSliceGen_ProbePositions");
        private static readonly int XGITimeSliceSkyVisibilityId = Shader.PropertyToID("_XGITimeSliceGen_SkyVisibility");
        private static readonly int XGITimeSliceLightsId = Shader.PropertyToID("_XGITimeSliceGen_Lights");
        private static readonly int XGITimeSliceParamsId = Shader.PropertyToID("_XGITimeSliceGen_Params");
        private static readonly int XGITimeSliceParams1Id = Shader.PropertyToID("_XGITimeSliceGen_Params1");
        private static readonly int XGITimeSliceParams2Id = Shader.PropertyToID("_XGITimeSliceGen_Params2");
        private static readonly int XGITimeSliceParams3Id = Shader.PropertyToID("_XGITimeSliceGen_Params3");
        private static readonly int XGIPlacementVoxelizeTrianglesOccupancyId = Shader.PropertyToID("_RW_XGIPlacementVoxelizeTriangles_Occupancy");
        private static readonly int XGIPlacementVoxelizeTrianglesTrianglesId = Shader.PropertyToID("_XGIPlacementVoxelizeTriangles_Triangles");
        private static readonly int XGIPlacementVoxelizeTrianglesBoundsId = Shader.PropertyToID("_XGIPlacementVoxelizeTriangles_Bounds");
        private static readonly int XGIPlacementVoxelizeTrianglesCellMinWSId = Shader.PropertyToID("_XGIPlacementVoxelizeTriangles_CellMinWS");
        private static readonly int XGIPlacementVoxelizeTrianglesCellSizeWSId = Shader.PropertyToID("_XGIPlacementVoxelizeTriangles_CellSizeWS");
        private static readonly int XGIPlacementVoxelizeTrianglesParamsId = Shader.PropertyToID("_XGIPlacementVoxelizeTriangles_Params");
        private static readonly int XGIPlacementProbeVolumeCullResultId = Shader.PropertyToID("_RW_XGIPlacementProbeVolumeCullPass_Result");
        private static readonly int XGIPlacementProbeVolumeCullProbeVolumesId = Shader.PropertyToID("_XGIPlacementProbeVolumeCullPass_ProbeVolumes");
        private static readonly int XGIPlacementProbeVolumeCullVolumeOffsetWSId = Shader.PropertyToID("_XGIPlacementProbeVolumeCullPass_VolumeOffsetWS");
        private static readonly int XGIPlacementProbeVolumeCullMaxBrickCountId = Shader.PropertyToID("_XGIPlacementProbeVolumeCullPass_MaxBrickCount");
        private static readonly int XGIPlacementProbeVolumeCullProbeVolumeCountId = Shader.PropertyToID("_XGIPlacementProbeVolumeCullPass_ProbeVolumeCount");
        private static readonly int XGIPlacementProbeVolumeCullSubdivLevelId = Shader.PropertyToID("_XGIPlacementProbeVolumeCullPass_SubdivLevel");
        private static readonly int XGIPlacementProbeVolumeCullBrickSizeId = Shader.PropertyToID("_XGIPlacementProbeVolumeCullPass_BrickSize");
        private static readonly int XGISdfApplySdfTextureId = Shader.PropertyToID("_XGISdfApply_SdfTexture");
        private static readonly int XGISdfApplyTextureSizeId = Shader.PropertyToID("_XGISdfApply_TextureSize");
        private static readonly int XGIGenBrickProbeVolumeCullDataId = Shader.PropertyToID("_XGIGenBrickPass_ProbeVolumeCullData");
        private static readonly int XGIGenBrickBrickCountId = Shader.PropertyToID("_RW_XGIGenBrickPass_BrickCount");
        private static readonly int XGIGenBrickBricksId = Shader.PropertyToID("_RW_XGIGenBrickPass_Bricks");
        private static readonly int XGIGenBrickVolumeSizeInBricksId = Shader.PropertyToID("_XGIGenBrickPass_VolumeSizeInBricks");
        private static readonly int XGIGenBrickVolumeOffsetInBricksId = Shader.PropertyToID("_XGIGenBrickPass_VolumeOffsetInBricks");
        private static readonly int XGIGenBrickMaxBrickCountId = Shader.PropertyToID("_XGIGenBrickPass_MaxBrickCount");
        private static readonly int XGIGenBrickSubdivLevelId = Shader.PropertyToID("_XGIGenBrickPass_SubdivLevel");
        private static Vector3[] defaultSkyShadingDirections;
        private static readonly List<BurtXGIProbeAdjustVolume> EmptyVirtualOffsetAdjustVolumeList = new List<BurtXGIProbeAdjustVolume>(0);

        private struct XGIVirtualOffsetProbeData
        {
            public Vector3 position;
            public int probeIndex;
            public float tMax;
            public float originBias;
            public float geometryBias;
            public float validityThreshold;
        }

        private struct XGITimeSliceLightData
        {
            public Vector4 positionType;
            public Vector4 directionRange;
            public Vector4 colorOuterCos;
            public Vector4 spotInnerCos;
        }

        private struct XGIPlacementProbeVolumeGpuOBB
        {
            public Vector3 corner;
            public Vector3 X;
            public Vector3 Y;
            public Vector3 Z;
            public int minControllerSubdivLevel;
            public int maxControllerSubdivLevel;
            public int fillEmptySpaces;
            public int maxSubdivLevelInsideVolume;
        }

        private struct XGIPlacementGpuDiagnosticCellCandidate
        {
            public BurtXGIProbePlacedCell cell;
            public Bounds bounds;
        }

        private struct XGIPlacementGpuDiagnosticCellResult
        {
            public bool success;
            public string status;
            public List<BurtXGIProbePlacedBrick> readbackBricks;
            public ulong gpuBrickTotal;
            public int truncatedReadback;
            public bool usedFallbackSdf;
        }

        private sealed class XGIPlacementGpuCapturePreview
        {
            public readonly List<BurtXGIProbePlacedCell> cells = new List<BurtXGIProbePlacedCell>();
            public readonly List<BurtXGIProbePlacedBrick> bricks = new List<BurtXGIProbePlacedBrick>();
            public readonly List<Vector3> probePositions = new List<Vector3>();
            public int skippedEmptyCells;
        }

        private sealed class PlacementSdfGeometrySource
        {
            public Bounds bounds;
            public Collider collider;
            public Vector3[] surfaceSamples;
            public PlacementSdfTriangle[] surfaceTriangles;
            public string kind;
        }

        private struct PlacementSdfTriangle
        {
            public Vector3 a;
            public Vector3 b;
            public Vector3 c;
        }

        private struct XGIPlacementVoxelizeTriangleGpu
        {
            public Vector4 a;
            public Vector4 b;
            public Vector4 c;
        }

        private struct XGIPlacementVoxelizeBoundsGpu
        {
            public Vector4 center;
            public Vector4 extents;
        }

        internal static BurtXGIProbeBakingProcessor Instance => instance ??= new BurtXGIProbeBakingProcessor();

        internal static string ResolvePlacementGpuStatusLabel()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                return "Unsupported(SystemInfo.supportsComputeShaders=false)";
            }

            var shader = Resources.Load<ComputeShader>(XGIProbeBakingComputeShaderResourcePath);
            if (shader == null)
            {
                return "Missing(" + XGIProbeBakingComputeShaderResourcePath + ")";
            }

            var missing = string.Empty;
            if (!shader.HasKernel(XGIPlacementProbeVolumeCullKernelName))
            {
                missing = AppendStatusItem(missing, XGIPlacementProbeVolumeCullKernelName);
            }

            if (!shader.HasKernel(XGIPlacementGenBrickKernelName))
            {
                missing = AppendStatusItem(missing, XGIPlacementGenBrickKernelName);
            }

            if (!shader.HasKernel(XGIPlacementVoxelizeTrianglesKernelName))
            {
                missing = AppendStatusItem(missing, XGIPlacementVoxelizeTrianglesKernelName);
            }

            return string.IsNullOrEmpty(missing)
                ? "Ready(" + XGIProbeBakingComputeShaderResourcePath + "." + XGIPlacementProbeVolumeCullKernelName + "+" + XGIPlacementGenBrickKernelName + "+" + XGIPlacementVoxelizeTrianglesKernelName + ")"
                : "Missing(" + XGIProbeBakingComputeShaderResourcePath + "." + missing + ")";
        }

        private static string AppendStatusItem(string current, string item)
        {
            return string.IsNullOrEmpty(current) ? item : current + "," + item;
        }

        private static bool TryResolveBakingRayTracingAccelerationStructure(
            out Camera camera,
            out RayTracingAccelerationStructure accelerationStructure)
        {
            accelerationStructure = null;
            camera = Camera.current;
            if (camera == null && SceneView.lastActiveSceneView != null)
            {
                camera = SceneView.lastActiveSceneView.camera;
            }

            if (camera == null)
            {
                camera = Camera.main;
            }

            return camera != null &&
                BurtRayTracingAccelerationStructureUtility.TryGetForCamera(camera, out accelerationStructure);
        }

        internal bool Prepare(BurtXGIProbeBakingConfig config, out PrepareResult result)
        {
            result = default;
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildPrepareReport(config, result);
                return false;
            }

            if (!config.SupportsCurrentTimeSliceBake(out var timeSliceError))
            {
                result.error = timeSliceError;
                result.report = BuildPrepareReport(config, result);
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                result.error = "Active scene is not valid.";
                result.report = BuildPrepareReport(config, result);
                return false;
            }

            var volumeBounds = CollectBakingVolumeOBBs(config, scene, out var bounds, out var volumeCount);

            result.probeVolumeCount = volumeCount;
            if (volumeBounds.Count == 0 || volumeCount == 0)
            {
                result.error = "Baking set has no enabled BurtGIProbeVolume with a valid extent.";
                result.report = BuildPrepareReport(config, result);
                return false;
            }

            result.globalBounds = bounds;
            result.minCellPosition = config.PositionToCell(bounds.min);
            result.maxCellPosition = config.PositionToCell(bounds.max);
            config.CaptureSceneMetadata(scene);
            config.CaptureBakedMetadata(bounds, result.minCellPosition, result.maxCellPosition);
            SyncLoadedSceneBakeData(config, scene, bounds);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.report = BuildPrepareReport(config, result);
            return true;
        }

        internal bool RunPlacementLite(BurtXGIProbeBakingConfig config, out PlacementResult result)
        {
            result = default;
            result.backend = "LiteCPU";
            result.gpuPlacementStatus = ResolvePlacementGpuStatusLabel();
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildPlacementReport(config, result);
                return false;
            }

            if (config.globalBounds.size.sqrMagnitude <= 0.000001f)
            {
                if (!Prepare(config, out var prepareResult))
                {
                    result.error = prepareResult.error;
                    result.report = BuildPlacementReport(config, result);
                    return false;
                }
            }

            var cellMin = config.minCellPosition;
            var cellMax = config.maxCellPosition;
            var cellCountVector = cellMax - cellMin + Vector3Int.one;
            if (cellCountVector.x <= 0 || cellCountVector.y <= 0 || cellCountVector.z <= 0)
            {
                result.error = "Invalid XGI placement cell range.";
                result.report = BuildPlacementReport(config, result);
                return false;
            }

            var candidateCellCount = (long)cellCountVector.x * cellCountVector.y * cellCountVector.z;
            if (candidateCellCount > MaxPlacementLiteCells)
            {
                result.error = "Placement Lite cell range is too large: " + candidateCellCount + " cells.";
                result.report = BuildPlacementReport(config, result);
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            var volumeBounds = CollectBakingVolumeOBBs(config, scene, out _, out _);
            if (volumeBounds.Count == 0)
            {
                result.error = "Baking set has no enabled BurtGIProbeVolume with a valid extent.";
                result.report = BuildPlacementReport(config, result);
                return false;
            }

            var placedCells = new List<BurtXGIProbePlacedCell>();
            var placedBricks = new List<BurtXGIProbePlacedBrick>();
            var probePositions = new List<Vector3>();
            for (var z = cellMin.z; z <= cellMax.z; z++)
            {
                for (var y = cellMin.y; y <= cellMax.y; y++)
                {
                    for (var x = cellMin.x; x <= cellMax.x; x++)
                    {
                        var cellPosition = new Vector3Int(x, y, z);
                        var cellBounds = CreateCellBounds(config, cellPosition);
                        if (!IntersectsAny(cellBounds, volumeBounds))
                        {
                            continue;
                        }

                        var cellIndex = CellPositionToIndex(cellPosition, cellMin, cellCountVector);
                        var brickStart = placedBricks.Count;
                        var probeStart = probePositions.Count;
                        AppendAdaptivePlacementBricks(config, cellPosition, cellBounds, volumeBounds, cellIndex, placedBricks, probePositions);
                        if (placedBricks.Count == brickStart)
                        {
                            continue;
                        }

                        placedCells.Add(new BurtXGIProbePlacedCell
                        {
                            index = cellIndex,
                            position = cellPosition,
                            bounds = cellBounds,
                            brickStartIndex = brickStart,
                            brickCount = placedBricks.Count - brickStart,
                            probeStartIndex = probeStart,
                            probeCount = probePositions.Count - probeStart,
                            sceneGuids = CollectIntersectingSceneGuids(config, cellBounds, scene)
                        });
                    }
                }
            }

            if (placedCells.Count == 0)
            {
                result.error = "Placement Lite found no cells intersecting BurtGIProbeVolume bounds.";
                result.report = BuildPlacementReport(config, result);
                return false;
            }

            config.CapturePlacement(placedCells.ToArray(), placedBricks.ToArray(), probePositions.ToArray());
            EditorUtility.SetDirty(config);
            result.success = true;
            result.cellCount = placedCells.Count;
            result.brickCount = placedBricks.Count;
            result.probeCount = probePositions.Count;
            result.report = BuildPlacementReport(config, result);
            return true;
        }

        internal bool RunPlacementXRenderPath(BurtXGIProbeBakingConfig config, out PlacementResult result)
        {
            var gpuPlacementStatus = ResolvePlacementGpuStatusLabel();
            var success = RunPlacementLite(config, out result);
            result.gpuPlacementStatus = gpuPlacementStatus;
            var gpuCapturedPlacement = false;
            if (success && gpuPlacementStatus.StartsWith("Ready", System.StringComparison.Ordinal))
            {
                TryRunPlacementGpuProbeVolumeCullDiagnostic(config, out result.gpuCullStatus, out gpuCapturedPlacement);
                if (gpuCapturedPlacement && config != null)
                {
                    result.cellCount = config.bakedCellCount;
                    result.brickCount = config.bakedBrickCount;
                    result.probeCount = config.bakedProbeCount;
                }
            }

            result.backend = gpuCapturedPlacement
                ? "GPUPlacementCapture"
                : !string.IsNullOrEmpty(result.gpuCullStatus) && result.gpuCullStatus.StartsWith("Dispatched", System.StringComparison.Ordinal)
                ? "LiteCPU(GPUCullDispatched)"
                : gpuPlacementStatus.StartsWith("Ready", System.StringComparison.Ordinal)
                    ? "LiteCPU(GPUPlacementKernelsReady)"
                    : "LiteCPU(GPUPlacementUnavailable)";
            result.report = BuildPlacementReport(config, result);
            return success;
        }

        private static bool TryRunPlacementGpuProbeVolumeCullDiagnostic(
            BurtXGIProbeBakingConfig config,
            out string status,
            out bool capturedPlacement)
        {
            capturedPlacement = false;
            status = "Skipped(ConfigMissing)";
            if (config == null)
            {
                return false;
            }

            var cells = config.bakedPlacedCells ?? System.Array.Empty<BurtXGIProbePlacedCell>();
            if (cells.Length == 0)
            {
                status = "Skipped(NoPlacedCells)";
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            var volumeBounds = CollectActiveSceneVolumeOBBs(scene);
            if (volumeBounds.Count == 0)
            {
                status = "Skipped(NoProbeVolumes)";
                return false;
            }

            var diagnosticCells = SelectPlacementGpuDiagnosticCells(config, cells);
            if (diagnosticCells.Count == 0)
            {
                status = "Skipped(NoDiagnosticCells)";
                return false;
            }

            var shader = Resources.Load<ComputeShader>(XGIProbeBakingComputeShaderResourcePath);
            if (shader == null ||
                !shader.HasKernel(XGIPlacementProbeVolumeCullKernelName))
            {
                status = ResolvePlacementGpuStatusLabel();
                return false;
            }

            var cellReports = new StringBuilder();
            var dispatchedCellCount = 0;
            var batchReadbackBrickCount = 0;
            var batchTruncatedReadback = 0;
            var fallbackSdfCellCount = 0;
            ulong batchGpuBrickTotal = 0UL;
            var capturePreview = new XGIPlacementGpuCapturePreview();
            var omittedCellReportCount = 0;
            for (var diagnosticCellIndex = 0; diagnosticCellIndex < diagnosticCells.Count; diagnosticCellIndex++)
            {
                var diagnosticCell = diagnosticCells[diagnosticCellIndex];
                if (TryRunPlacementGpuProbeVolumeCullDiagnosticCell(
                        config,
                        scene,
                        volumeBounds,
                        shader,
                        diagnosticCell,
                        diagnosticCellIndex,
                        out var cellResult))
                {
                    dispatchedCellCount++;
                    AppendPlacementGpuDiagnosticCapturePreview(
                        config,
                        diagnosticCell.cell,
                        diagnosticCell.bounds,
                        cellResult,
                        capturePreview);
                }

                batchGpuBrickTotal += cellResult.gpuBrickTotal;
                batchReadbackBrickCount += cellResult.readbackBricks != null ? cellResult.readbackBricks.Count : 0;
                batchTruncatedReadback += cellResult.truncatedReadback;
                if (cellResult.usedFallbackSdf)
                {
                    fallbackSdfCellCount++;
                }

                if (diagnosticCellIndex < XGIPlacementGpuDiagnosticReportCellCount)
                {
                    if (cellReports.Length > 0)
                    {
                        cellReports.Append("||");
                    }

                    cellReports.Append(cellResult.status);
                }
                else
                {
                    omittedCellReportCount++;
                }
            }

            if (omittedCellReportCount > 0)
            {
                if (cellReports.Length > 0)
                {
                    cellReports.Append("||");
                }

                cellReports.Append("...Omitted=").Append(omittedCellReportCount);
            }

            var expectedPreviewProbeCount = CountPlacementProbePositionsForBrickCount(capturePreview.bricks.Count);
            var captureBlocker = ResolvePlacementGpuCaptureBlocker(
                diagnosticCells.Count,
                dispatchedCellCount,
                batchGpuBrickTotal,
                batchReadbackBrickCount,
                batchTruncatedReadback,
                fallbackSdfCellCount,
                capturePreview,
                expectedPreviewProbeCount);
            if (string.IsNullOrEmpty(captureBlocker))
            {
                config.CapturePlacement(
                    capturePreview.cells.ToArray(),
                    capturePreview.bricks.ToArray(),
                    capturePreview.probePositions.ToArray());
                EditorUtility.SetDirty(config);
                capturedPlacement = true;
            }

            status = "DispatchedBatch(Cells=" + dispatchedCellCount + "/" + diagnosticCells.Count +
                ",ReportCellLimit=" + XGIPlacementGpuDiagnosticReportCellCount +
                ",BatchCells=" + BuildPlacementGpuDiagnosticCellCandidateSummary(diagnosticCells) +
                ",GpuBrickTotal=" + batchGpuBrickTotal +
                ",GpuReadbackBricks=" + batchReadbackBrickCount +
                ",GpuReadbackProbes=" + CountPlacementProbePositionsForBrickCount(batchReadbackBrickCount) +
                ",TruncatedReadback=" + batchTruncatedReadback +
                ",FallbackSdfCells=" + fallbackSdfCellCount +
                ",PreviewCells=" + capturePreview.cells.Count +
                ",PreviewBricks=" + capturePreview.bricks.Count +
                ",PreviewProbes=" + capturePreview.probePositions.Count +
                ",CapturedPlacement=" + capturedPlacement +
                ",CaptureBlocker=" + (!string.IsNullOrEmpty(captureBlocker) ? captureBlocker : "None") +
                ",PreviewSkippedEmptyCells=" + capturePreview.skippedEmptyCells +
                ",Results=" + cellReports + ")";
            return dispatchedCellCount > 0;
        }

        private static bool TryRunPlacementGpuProbeVolumeCullDiagnosticCell(
            BurtXGIProbeBakingConfig config,
            Scene scene,
            List<BurtGIProbeVolumeBounds> volumeBounds,
            ComputeShader shader,
            XGIPlacementGpuDiagnosticCellCandidate selectedCandidate,
            int diagnosticCellIndex,
            out XGIPlacementGpuDiagnosticCellResult result)
        {
            result = default;
            var selectedCell = selectedCandidate.cell;
            var cellBounds = selectedCell.bounds;
            if (!IsValidBounds(cellBounds))
            {
                cellBounds = selectedCandidate.bounds;
            }

            var maxSubdivisionLevel = Mathf.Max(0, config.simplificationLevels);
            var baseBrickCountPerAxis = Mathf.Max(1, config.CellSizeInBricks);
            if (baseBrickCountPerAxis > XGIPlacementGpuCullDiagnosticMaxAxis)
            {
                result.status = "Cell" + diagnosticCellIndex + "(Skipped(BaseAxisTooLarge=" + baseBrickCountPerAxis + ",Max=" + XGIPlacementGpuCullDiagnosticMaxAxis + "))";
                return false;
            }

            var gpuVolumes = new List<XGIPlacementProbeVolumeGpuOBB>(volumeBounds.Count);
            var diagnosticMinSubdiv = maxSubdivisionLevel;
            var diagnosticMaxSubdiv = 0;
            for (var i = 0; i < volumeBounds.Count; i++)
            {
                var volume = volumeBounds[i];
                if (!OBBAABBIntersect(volume, cellBounds))
                {
                    continue;
                }

                ResolveVolumeSubdivisionRange(config, volume, cellBounds, out var minSubdiv, out var maxSubdiv);
                diagnosticMinSubdiv = Mathf.Min(diagnosticMinSubdiv, minSubdiv);
                diagnosticMaxSubdiv = Mathf.Max(diagnosticMaxSubdiv, maxSubdiv);
                gpuVolumes.Add(new XGIPlacementProbeVolumeGpuOBB
                {
                    corner = volume.corner,
                    X = volume.x,
                    Y = volume.y,
                    Z = volume.z,
                    minControllerSubdivLevel = minSubdiv,
                    maxControllerSubdivLevel = maxSubdiv,
                    fillEmptySpaces = volume.fillEmptySpaces ? 1 : 0,
                    maxSubdivLevelInsideVolume = 0
                });
            }

            if (gpuVolumes.Count == 0)
            {
                result.status = "Cell" + diagnosticCellIndex + "(Skipped(NoOverlappingProbeVolumes,Cell=" + selectedCell.position + "))";
                return false;
            }

            diagnosticMinSubdiv = Mathf.Clamp(diagnosticMinSubdiv, 0, maxSubdivisionLevel);
            diagnosticMaxSubdiv = Mathf.Clamp(diagnosticMaxSubdiv, diagnosticMinSubdiv, maxSubdivisionLevel);

            RenderTexture cullTexture = null;
            Texture sdfTexture = null;
            GraphicsBuffer volumeBuffer = null;
            GraphicsBuffer brickCountBuffer = null;
            GraphicsBuffer brickBuffer = null;
            try
            {
                var descriptor = new RenderTextureDescriptor
                {
                    width = baseBrickCountPerAxis,
                    height = baseBrickCountPerAxis,
                    volumeDepth = baseBrickCountPerAxis,
                    dimension = TextureDimension.Tex3D,
                    enableRandomWrite = true,
                    graphicsFormat = GraphicsFormat.R32G32_SFloat,
                    msaaSamples = 1,
                    useMipMap = true,
                    autoGenerateMips = false,
                    mipCount = maxSubdivisionLevel + 1
                };
                cullTexture = new RenderTexture(descriptor)
                {
                    name = "BurtXGIProbeBaking.Placement.ProbeVolumeCull.MipDiagnostic"
                };
                cullTexture.Create();

                var stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(XGIPlacementProbeVolumeGpuOBB));
                volumeBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, gpuVolumes.Count, stride);
                volumeBuffer.SetData(gpuVolumes.ToArray());

                var kernel = shader.FindKernel(XGIPlacementProbeVolumeCullKernelName);
                shader.SetBuffer(kernel, XGIPlacementProbeVolumeCullProbeVolumesId, volumeBuffer);
                shader.SetVector(XGIPlacementProbeVolumeCullVolumeOffsetWSId, new Vector4(cellBounds.min.x, cellBounds.min.y, cellBounds.min.z, 0f));
                shader.SetFloat(XGIPlacementProbeVolumeCullProbeVolumeCountId, gpuVolumes.Count);

                var levelCount = maxSubdivisionLevel + 1;
                var generatedByLevel = new int[levelCount];
                var fillEmptyByLevel = new int[levelCount];
                var axisByLevel = new int[levelCount];
                var mipAxisByLevel = new int[levelCount];
                var cpuCandidateByLevel = new int[levelCount];
                var cpuMultiLevelBrickCount = CountPlacementCandidateBricksBySubdivision(
                    config,
                    selectedCell.position,
                    cellBounds,
                    volumeBounds,
                    diagnosticMinSubdiv,
                    diagnosticMaxSubdiv,
                    cpuCandidateByLevel);
                for (var level = diagnosticMinSubdiv; level <= diagnosticMaxSubdiv; level++)
                {
                    var brickSizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(level);
                    var brickCountPerAxis = Mathf.Max(1, config.CellSizeInBricks / Mathf.Max(1, brickSizeInBricks));
                    var mipAxis = Mathf.Max(1, baseBrickCountPerAxis >> level);
                    var dispatchAxis = Mathf.Min(brickCountPerAxis, mipAxis);
                    axisByLevel[level] = dispatchAxis;
                    mipAxisByLevel[level] = mipAxis;

                    shader.SetTexture(kernel, XGIPlacementProbeVolumeCullResultId, cullTexture, level);
                    shader.SetVector(XGIPlacementProbeVolumeCullMaxBrickCountId, new Vector4(dispatchAxis, dispatchAxis, dispatchAxis, 0f));
                    shader.SetFloat(XGIPlacementProbeVolumeCullSubdivLevelId, level);
                    shader.SetFloat(XGIPlacementProbeVolumeCullBrickSizeId, cellBounds.size.x / Mathf.Max(1, dispatchAxis));
                    shader.Dispatch(
                        kernel,
                        Mathf.Max(1, Mathf.CeilToInt(dispatchAxis / (float)XGIProbeBakingThreadGroupSize)),
                        Mathf.Max(1, Mathf.CeilToInt(dispatchAxis / (float)XGIProbeBakingThreadGroupSize)),
                        dispatchAxis);
                }

                for (var level = diagnosticMinSubdiv; level <= diagnosticMaxSubdiv; level++)
                {
                    var request = AsyncGPUReadback.Request(cullTexture, level);
                    request.WaitForCompletion();
                    if (request.hasError)
                    {
                        result.status = "Cell" + diagnosticCellIndex + "(Failed(AsyncGPUReadback,Subdiv=" + level + "))";
                        return false;
                    }

                    var data = request.GetData<Vector2>();
                    var generated = 0;
                    var fillEmpty = 0;
                    var brickCountPerAxis = axisByLevel[level];
                    var mipAxis = mipAxisByLevel[level];
                    for (var z = 0; z < brickCountPerAxis; z++)
                    {
                        for (var y = 0; y < brickCountPerAxis; y++)
                        {
                            for (var x = 0; x < brickCountPerAxis; x++)
                            {
                                var dataIndex = x + mipAxis * (y + mipAxis * z);
                                if (dataIndex < 0 || dataIndex >= data.Length)
                                {
                                    continue;
                                }

                                var value = data[dataIndex];
                                if (value.x <= 0.5f)
                                {
                                    continue;
                                }

                                generated++;
                                if (value.y < 2147483000f)
                                {
                                    fillEmpty++;
                                }
                            }
                        }
                    }

                    generatedByLevel[level] = generated;
                    fillEmptyByLevel[level] = fillEmpty;
                }

                uint[] brickCounts = null;
                string[] brickSamples = null;
                ulong gpuBrickTotal = 0UL;
                List<BurtXGIProbePlacedBrick> gpuReadbackBricks = null;
                var truncatedBrickReadback = 0;
                var sdfStatus = "Skipped(NoGenBrick)";
                var usedFallbackSdf = false;
                if (shader.HasKernel(XGIPlacementGenBrickKernelName))
                {
                    var sdfTextureSize = Vector4.one;
                    if (!TryCreatePlacementDiagnosticVoxelJfaSdfTexture(scene, cellBounds, out var jfaSdfTexture, out sdfTextureSize, out sdfStatus))
                    {
                        if (TryCreatePlacementDiagnosticSceneSdfTexture(scene, cellBounds, out var cpuSdfTexture, out sdfTextureSize, out sdfStatus))
                        {
                            sdfTexture = cpuSdfTexture;
                        }
                    }
                    else
                    {
                        sdfTexture = jfaSdfTexture;
                    }

                    if (sdfTexture == null)
                    {
                        usedFallbackSdf = true;
                        var dummySdfTexture = new Texture3D(1, 1, 1, TextureFormat.RFloat, false)
                        {
                            name = "BurtXGIProbeBaking.Placement.GenBrick.DummyBlackSdf"
                        };
                        dummySdfTexture.SetPixel(0, 0, 0, Color.black);
                        dummySdfTexture.Apply(false, true);
                        sdfTexture = dummySdfTexture;
                        sdfTextureSize = new Vector4(1f, 1f, 1f, 0f);
                    }

                    brickCountBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, levelCount, sizeof(uint));
                    brickCountBuffer.SetData(new uint[levelCount]);
                    var brickBufferCount = Mathf.Max(1, baseBrickCountPerAxis * baseBrickCountPerAxis * baseBrickCountPerAxis);
                    brickBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, brickBufferCount, sizeof(float) * 4);
                    brickCounts = new uint[levelCount];
                    brickSamples = new string[levelCount];
                    gpuReadbackBricks = new List<BurtXGIProbePlacedBrick>();

                    var genBrickKernel = shader.FindKernel(XGIPlacementGenBrickKernelName);
                    shader.SetTexture(genBrickKernel, XGIGenBrickProbeVolumeCullDataId, cullTexture);
                    shader.SetTexture(genBrickKernel, XGISdfApplySdfTextureId, sdfTexture);
                    shader.SetBuffer(genBrickKernel, XGIGenBrickBrickCountId, brickCountBuffer);
                    shader.SetBuffer(genBrickKernel, XGIGenBrickBricksId, brickBuffer);
                    shader.SetVector(XGISdfApplyTextureSizeId, sdfTextureSize);
                    shader.SetVector(XGIGenBrickVolumeSizeInBricksId, new Vector4(config.CellSizeInBricks, config.CellSizeInBricks, config.CellSizeInBricks, 0f));
                    var volumeOffsetInBricks = ((cellBounds.min - config.probeOffset) / Mathf.Max(0.0001f, config.MinBrickSize));
                    shader.SetVector(XGIGenBrickVolumeOffsetInBricksId, new Vector4(volumeOffsetInBricks.x, volumeOffsetInBricks.y, volumeOffsetInBricks.z, 0f));

                    for (var level = diagnosticMinSubdiv; level <= diagnosticMaxSubdiv; level++)
                    {
                        var brickCountPerAxis = axisByLevel[level];
                        shader.SetVector(XGIGenBrickMaxBrickCountId, new Vector4(brickCountPerAxis, brickCountPerAxis, brickCountPerAxis, 0f));
                        shader.SetFloat(XGIGenBrickSubdivLevelId, level);
                        shader.Dispatch(
                            genBrickKernel,
                            Mathf.Max(1, Mathf.CeilToInt(brickCountPerAxis / (float)XGIProbeBakingThreadGroupSize)),
                            Mathf.Max(1, Mathf.CeilToInt(brickCountPerAxis / (float)XGIProbeBakingThreadGroupSize)),
                            brickCountPerAxis);

                        brickCountBuffer.GetData(brickCounts);
                        var brickCount = brickCounts[level];
                        gpuBrickTotal += brickCount;
                        if (brickCount == 0u)
                        {
                            continue;
                        }

                        var readbackCount = Mathf.Min((int)brickCount, brickBufferCount);
                        if (readbackCount < brickCount)
                        {
                            var missing = brickCount - (uint)readbackCount;
                            truncatedBrickReadback += missing > int.MaxValue ? int.MaxValue : (int)missing;
                        }

                        var brickPositions = new Vector4[readbackCount];
                        brickBuffer.GetData(brickPositions, 0, 0, readbackCount);
                        for (var i = 0; i < readbackCount; i++)
                        {
                            var position = brickPositions[i];
                            gpuReadbackBricks.Add(new BurtXGIProbePlacedBrick
                            {
                                position = new Vector3Int(
                                    Mathf.RoundToInt(position.x),
                                    Mathf.RoundToInt(position.y),
                                    Mathf.RoundToInt(position.z)),
                                subdivisionLevel = level,
                                cellIndex = selectedCell.index
                            });
                        }

                        var first = brickPositions[0];
                        var last = brickPositions[readbackCount - 1];
                        var firstBrick = new Vector3Int(Mathf.RoundToInt(first.x), Mathf.RoundToInt(first.y), Mathf.RoundToInt(first.z));
                        var lastBrick = new Vector3Int(Mathf.RoundToInt(last.x), Mathf.RoundToInt(last.y), Mathf.RoundToInt(last.z));
                        brickSamples[level] = firstBrick + "->" + lastBrick;
                    }
                }

                var levelSummary = new StringBuilder();
                for (var level = diagnosticMinSubdiv; level <= diagnosticMaxSubdiv; level++)
                {
                    if (levelSummary.Length > 0)
                    {
                        levelSummary.Append("|");
                    }

                    levelSummary
                        .Append(level).Append(":Axis=").Append(axisByLevel[level])
                        .Append(",MipAxis=").Append(mipAxisByLevel[level])
                        .Append(",CpuCandidate=").Append(cpuCandidateByLevel[level])
                        .Append(",Cull=").Append(generatedByLevel[level])
                        .Append(",FillEmpty=").Append(fillEmptyByLevel[level])
                        .Append(",Brick=").Append(brickCounts != null ? brickCounts[level].ToString() : "Skipped");
                    if (brickSamples != null && !string.IsNullOrEmpty(brickSamples[level]))
                    {
                        levelSummary.Append(",Sample=").Append(brickSamples[level]);
                    }
                }

                result.success = true;
                result.status = "Cell" + diagnosticCellIndex + "(Dispatched(Cell=" + selectedCell.position +
                    ",SubdivRange=" + diagnosticMinSubdiv + "-" + diagnosticMaxSubdiv +
                    ",BaseAxis=" + baseBrickCountPerAxis +
                    ",CpuCellBricks=" + selectedCell.brickCount +
                    ",CpuMultiLevelBricks=" + cpuMultiLevelBrickCount +
                    ",GpuBrickTotal=" + gpuBrickTotal +
                    ",GpuReadbackBricks=" + (gpuReadbackBricks != null ? gpuReadbackBricks.Count.ToString() : "Skipped") +
                    ",TruncatedReadback=" + truncatedBrickReadback +
                    ",Sdf=" + sdfStatus +
                    ",ProbeVolumes=" + gpuVolumes.Count +
                    ",Levels=" + levelSummary + "))";
                result.readbackBricks = gpuReadbackBricks;
                result.gpuBrickTotal = gpuBrickTotal;
                result.truncatedReadback = truncatedBrickReadback;
                result.usedFallbackSdf = usedFallbackSdf;
                return true;
            }
            finally
            {
                if (brickBuffer != null)
                {
                    brickBuffer.Release();
                }

                if (brickCountBuffer != null)
                {
                    brickCountBuffer.Release();
                }

                if (volumeBuffer != null)
                {
                    volumeBuffer.Release();
                }

                if (sdfTexture != null)
                {
                    if (sdfTexture is RenderTexture renderSdfTexture)
                    {
                        renderSdfTexture.Release();
                    }

                    Object.DestroyImmediate(sdfTexture);
                }

                if (cullTexture != null)
                {
                    cullTexture.Release();
                    Object.DestroyImmediate(cullTexture);
                }
            }
        }

        private static bool TryCreatePlacementDiagnosticVoxelJfaSdfTexture(
            Scene scene,
            Bounds cellBounds,
            out RenderTexture sdfTexture,
            out Vector4 sdfTextureSize,
            out string status)
        {
            sdfTexture = null;
            sdfTextureSize = Vector4.zero;
            if (!scene.IsValid() || !IsValidBounds(cellBounds))
            {
                status = "VoxelJfaSdfSkipped(InvalidSceneOrCell)";
                return false;
            }

            var shader = Resources.Load<ComputeShader>(XGISceneVoxelBuildComputeShaderResourcePath);
            if (shader == null)
            {
                status = "VoxelJfaSdfSkipped(MissingShader=" + XGISceneVoxelBuildComputeShaderResourcePath + ")";
                return false;
            }

            if (!BurtXGISdfGenDispatchUtility.HasRequiredKernels(shader))
            {
                status = "VoxelJfaSdfSkipped(MissingKernels)";
                return false;
            }

            var geometrySources = CollectPlacementSdfGeometrySources(scene, cellBounds);
            if (geometrySources.Count == 0)
            {
                status = "VoxelJfaSdfSkipped(NoSceneGeometry)";
                return false;
            }

            var textureSize = XGIPlacementDiagnosticSceneSdfTextureSize;
            var maxCellExtent = Mathf.Max(cellBounds.size.x, Mathf.Max(cellBounds.size.y, cellBounds.size.z));
            if (maxCellExtent <= 0.0001f)
            {
                status = "VoxelJfaSdfSkipped(InvalidCellExtent)";
                return false;
            }

            Texture occupancyTexture = null;
            var occupancyStatus = "Unknown";
            try
            {
                if (!TryCreatePlacementDiagnosticGpuTriangleOccupancyTexture(
                        cellBounds,
                        geometrySources,
                        textureSize,
                        maxCellExtent,
                        out occupancyTexture,
                        out var occupiedVoxelCount,
                        out occupancyStatus))
                {
                    var gpuOccupancyStatus = occupancyStatus;
                    occupancyTexture = CreatePlacementDiagnosticOccupancyTexture(cellBounds, geometrySources, textureSize, maxCellExtent, out occupiedVoxelCount);
                    occupancyStatus = occupancyTexture != null
                        ? "CpuTriangleDistance(Fallback=" + gpuOccupancyStatus + ")"
                        : "CpuTriangleDistanceFailed(Fallback=" + gpuOccupancyStatus + ")";
                }

                if (occupancyTexture == null || occupiedVoxelCount <= 0)
                {
                    status = "VoxelJfaSdfSkipped(NoOccupiedVoxels,Sources=" + geometrySources.Count + ",Occupancy=" + occupancyStatus + ")";
                    return false;
                }

                var context = new BurtXGISdfGenContext();
                if (!context.Configure("BurtXGIProbeBaking.Placement.GenBrick.VoxelJfaSdf", textureSize, 1, true, true))
                {
                    context.Dispose();
                    status = "VoxelJfaSdfSkipped(Context=" + context.ResolveStatusLabel() + ")";
                    return false;
                }

                var cmd = CommandBufferPool.Get("Burt XGI Probe Baking Placement SDF JFA");
                var dispatched = false;
                try
                {
                    dispatched = BurtXGISdfGenDispatchUtility.TryDispatchGenerateFromOccupancy(
                        cmd,
                        shader,
                        new RenderTargetIdentifier(occupancyTexture),
                        context,
                        0);
                    if (dispatched)
                    {
                        Graphics.ExecuteCommandBuffer(cmd);
                    }
                }
                finally
                {
                    CommandBufferPool.Release(cmd);
                }

                if (!dispatched)
                {
                    context.Dispose();
                    status = "VoxelJfaSdfSkipped(DispatchFailed)";
                    return false;
                }

                sdfTexture = context.sdfTexture;
                sdfTextureSize = new Vector4(textureSize, textureSize, textureSize, BurtXGISdfGenContext.ClipmapBorderSize);
                status = "VoxelJfaSdf(Size=" + textureSize + ",Sources=" + geometrySources.Count + ",MeshSamples=" + CountPlacementSdfMeshSamples(geometrySources) + ",MeshTriangles=" + CountPlacementSdfMeshTriangles(geometrySources) + ",Seeds=" + occupiedVoxelCount + ",Occupancy=" + occupancyStatus + ")";
                if (sdfTexture != null && sdfTexture.IsCreated())
                {
                    return true;
                }

                context.Dispose();
                sdfTexture = null;
                status = "VoxelJfaSdfSkipped(ResultTextureInvalid)";
                return false;
            }
            finally
            {
                if (occupancyTexture != null)
                {
                    if (occupancyTexture is RenderTexture renderOccupancyTexture)
                    {
                        renderOccupancyTexture.Release();
                    }

                    Object.DestroyImmediate(occupancyTexture);
                }
            }
        }

        private static bool TryCreatePlacementDiagnosticSceneSdfTexture(
            Scene scene,
            Bounds cellBounds,
            out Texture3D sdfTexture,
            out Vector4 sdfTextureSize,
            out string status)
        {
            sdfTexture = null;
            sdfTextureSize = Vector4.zero;
            if (!scene.IsValid() || !IsValidBounds(cellBounds))
            {
                status = "DummyBlackDiagnostic(InvalidSceneOrCell)";
                return false;
            }

            var geometrySources = CollectPlacementSdfGeometrySources(scene, cellBounds);
            if (geometrySources.Count == 0)
            {
                status = "DummyBlackDiagnostic(NoSceneGeometry)";
                return false;
            }

            var textureSize = XGIPlacementDiagnosticSceneSdfTextureSize;
            var maxCellExtent = Mathf.Max(cellBounds.size.x, Mathf.Max(cellBounds.size.y, cellBounds.size.z));
            if (maxCellExtent <= 0.0001f)
            {
                status = "DummyBlackDiagnostic(InvalidCellExtent)";
                return false;
            }

            sdfTexture = new Texture3D(textureSize, textureSize, textureSize, TextureFormat.RFloat, false)
            {
                name = "BurtXGIProbeBaking.Placement.GenBrick.SceneBoundsSdf"
            };

            var invTextureSize = 1f / textureSize;
            for (var z = 0; z < textureSize; z++)
            {
                for (var y = 0; y < textureSize; y++)
                {
                    for (var x = 0; x < textureSize; x++)
                    {
                        var position01 = new Vector3(
                            (x + 0.5f) * invTextureSize,
                            (y + 0.5f) * invTextureSize,
                            (z + 0.5f) * invTextureSize);
                        var positionWS = cellBounds.min + Vector3.Scale(position01, cellBounds.size);
                        var distance = float.PositiveInfinity;
                        for (var sourceIndex = 0; sourceIndex < geometrySources.Count; sourceIndex++)
                        {
                            distance = Mathf.Min(distance, DistanceToPlacementSdfGeometry(positionWS, geometrySources[sourceIndex]));
                        }

                        var distance01 = Mathf.Clamp01(distance / maxCellExtent);
                        sdfTexture.SetPixel(x, y, z, new Color(distance01, 0f, 0f, 0f));
                    }
                }
            }

            sdfTexture.Apply(false, true);
            sdfTextureSize = new Vector4(textureSize, textureSize, textureSize, 0f);
            status = "SceneGeometrySurfaceSdf(Size=" + textureSize + ",Sources=" + geometrySources.Count + ",MeshSamples=" + CountPlacementSdfMeshSamples(geometrySources) + ",MeshTriangles=" + CountPlacementSdfMeshTriangles(geometrySources) + ")";
            return true;
        }

        private static Texture3D CreatePlacementDiagnosticOccupancyTexture(
            Bounds cellBounds,
            List<PlacementSdfGeometrySource> geometrySources,
            int textureSize,
            float maxCellExtent,
            out int occupiedVoxelCount)
        {
            occupiedVoxelCount = 0;
            if (geometrySources == null || geometrySources.Count == 0 || textureSize <= 0 || maxCellExtent <= 0.0001f)
            {
                return null;
            }

            var occupancyTexture = new Texture3D(textureSize, textureSize, textureSize, TextureFormat.RGBAFloat, false)
            {
                name = "BurtXGIProbeBaking.Placement.GenBrick.OccupancySeeds"
            };

            var invTextureSize = 1f / textureSize;
            var voxelDetectionDistance = Mathf.Sqrt(3f) * maxCellExtent * invTextureSize * 0.5f;
            for (var z = 0; z < textureSize; z++)
            {
                for (var y = 0; y < textureSize; y++)
                {
                    for (var x = 0; x < textureSize; x++)
                    {
                        var position01 = new Vector3(
                            (x + 0.5f) * invTextureSize,
                            (y + 0.5f) * invTextureSize,
                            (z + 0.5f) * invTextureSize);
                        var positionWS = cellBounds.min + Vector3.Scale(position01, cellBounds.size);
                        var distance = float.PositiveInfinity;
                        for (var sourceIndex = 0; sourceIndex < geometrySources.Count; sourceIndex++)
                        {
                            distance = Mathf.Min(distance, DistanceToPlacementSdfGeometry(positionWS, geometrySources[sourceIndex]));
                        }

                        var occupied = distance <= voxelDetectionDistance;
                        if (occupied)
                        {
                            occupiedVoxelCount++;
                        }

                        occupancyTexture.SetPixel(x, y, z, occupied ? Color.white : Color.clear);
                    }
                }
            }

            occupancyTexture.Apply(false, true);
            return occupancyTexture;
        }

        private static bool TryCreatePlacementDiagnosticGpuTriangleOccupancyTexture(
            Bounds cellBounds,
            List<PlacementSdfGeometrySource> geometrySources,
            int textureSize,
            float maxCellExtent,
            out Texture occupancyTexture,
            out int occupiedVoxelCount,
            out string status)
        {
            occupancyTexture = null;
            occupiedVoxelCount = 0;
            if (geometrySources == null || geometrySources.Count == 0 || textureSize <= 0 || maxCellExtent <= 0.0001f)
            {
                status = "GpuTriangleVoxelizeSkipped(InvalidInput)";
                return false;
            }

            var shader = Resources.Load<ComputeShader>(XGIProbeBakingComputeShaderResourcePath);
            if (shader == null || !shader.HasKernel(XGIPlacementVoxelizeTrianglesKernelName))
            {
                status = "GpuTriangleVoxelizeSkipped(MissingKernel)";
                return false;
            }

            var triangles = BuildPlacementGpuVoxelizeTriangles(geometrySources);
            var bounds = BuildPlacementGpuVoxelizeBounds(geometrySources);
            if (triangles.Length == 0 && bounds.Length == 0)
            {
                status = "GpuTriangleVoxelizeSkipped(NoGeometry)";
                return false;
            }

            var triangleUpload = triangles.Length > 0 ? triangles : new XGIPlacementVoxelizeTriangleGpu[1];
            var boundsUpload = bounds.Length > 0 ? bounds : new XGIPlacementVoxelizeBoundsGpu[1];
            GraphicsBuffer triangleBuffer = null;
            GraphicsBuffer boundsBuffer = null;
            RenderTexture renderTexture = null;
            try
            {
                var descriptor = new RenderTextureDescriptor
                {
                    width = textureSize,
                    height = textureSize,
                    volumeDepth = textureSize,
                    dimension = TextureDimension.Tex3D,
                    enableRandomWrite = true,
                    graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
                    msaaSamples = 1,
                    depthBufferBits = 0
                };
                renderTexture = new RenderTexture(descriptor)
                {
                    name = "BurtXGIProbeBaking.Placement.GenBrick.GpuTriangleOccupancy"
                };
                renderTexture.Create();

                triangleBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    triangleUpload.Length,
                    System.Runtime.InteropServices.Marshal.SizeOf(typeof(XGIPlacementVoxelizeTriangleGpu)));
                triangleBuffer.SetData(triangleUpload);
                boundsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.Structured,
                    boundsUpload.Length,
                    System.Runtime.InteropServices.Marshal.SizeOf(typeof(XGIPlacementVoxelizeBoundsGpu)));
                boundsBuffer.SetData(boundsUpload);

                var kernel = shader.FindKernel(XGIPlacementVoxelizeTrianglesKernelName);
                shader.SetTexture(kernel, XGIPlacementVoxelizeTrianglesOccupancyId, renderTexture);
                shader.SetBuffer(kernel, XGIPlacementVoxelizeTrianglesTrianglesId, triangleBuffer);
                shader.SetBuffer(kernel, XGIPlacementVoxelizeTrianglesBoundsId, boundsBuffer);
                shader.SetVector(XGIPlacementVoxelizeTrianglesCellMinWSId, new Vector4(cellBounds.min.x, cellBounds.min.y, cellBounds.min.z, 0f));
                shader.SetVector(XGIPlacementVoxelizeTrianglesCellSizeWSId, new Vector4(cellBounds.size.x, cellBounds.size.y, cellBounds.size.z, 0f));
                var voxelDetectionDistance = Mathf.Sqrt(3f) * maxCellExtent / Mathf.Max(1f, textureSize) * 0.5f;
                shader.SetVector(XGIPlacementVoxelizeTrianglesParamsId, new Vector4(textureSize, triangles.Length, voxelDetectionDistance, bounds.Length));
                shader.Dispatch(
                    kernel,
                    Mathf.Max(1, Mathf.CeilToInt(textureSize / (float)XGIProbeBakingThreadGroupSize)),
                    Mathf.Max(1, Mathf.CeilToInt(textureSize / (float)XGIProbeBakingThreadGroupSize)),
                    textureSize);

                var request = AsyncGPUReadback.Request(renderTexture, 0);
                request.WaitForCompletion();
                if (request.hasError)
                {
                    status = "GpuTriangleVoxelizeSkipped(ReadbackFailed,Triangles=" + triangles.Length + ",Bounds=" + bounds.Length + ")";
                    renderTexture.Release();
                    Object.DestroyImmediate(renderTexture);
                    return false;
                }

                var data = request.GetData<Vector4>();
                for (var i = 0; i < data.Length; i++)
                {
                    if (data[i].x > 0.5f)
                    {
                        occupiedVoxelCount++;
                    }
                }

                occupancyTexture = renderTexture;
                status = "GpuTriangleVoxelize(Triangles=" + triangles.Length + ",Bounds=" + bounds.Length + ",Seeds=" + occupiedVoxelCount + ")";
                if (occupiedVoxelCount > 0)
                {
                    return true;
                }

                occupancyTexture = null;
                renderTexture.Release();
                Object.DestroyImmediate(renderTexture);
                status = "GpuTriangleVoxelizeSkipped(NoSeeds,Triangles=" + triangles.Length + ",Bounds=" + bounds.Length + ")";
                return false;
            }
            finally
            {
                if (triangleBuffer != null)
                {
                    triangleBuffer.Release();
                }

                if (boundsBuffer != null)
                {
                    boundsBuffer.Release();
                }
            }
        }

        private static XGIPlacementVoxelizeTriangleGpu[] BuildPlacementGpuVoxelizeTriangles(List<PlacementSdfGeometrySource> geometrySources)
        {
            if (geometrySources == null || geometrySources.Count == 0)
            {
                return System.Array.Empty<XGIPlacementVoxelizeTriangleGpu>();
            }

            var triangles = new List<XGIPlacementVoxelizeTriangleGpu>();
            for (var sourceIndex = 0; sourceIndex < geometrySources.Count; sourceIndex++)
            {
                var sourceTriangles = geometrySources[sourceIndex].surfaceTriangles;
                if (sourceTriangles == null)
                {
                    continue;
                }

                for (var triangleIndex = 0; triangleIndex < sourceTriangles.Length; triangleIndex++)
                {
                    var triangle = sourceTriangles[triangleIndex];
                    triangles.Add(new XGIPlacementVoxelizeTriangleGpu
                    {
                        a = new Vector4(triangle.a.x, triangle.a.y, triangle.a.z, 0f),
                        b = new Vector4(triangle.b.x, triangle.b.y, triangle.b.z, 0f),
                        c = new Vector4(triangle.c.x, triangle.c.y, triangle.c.z, 0f)
                    });
                }
            }

            if (triangles.Count <= XGIPlacementDiagnosticGpuTriangleMaxCount)
            {
                return triangles.ToArray();
            }

            var sampledTriangles = new XGIPlacementVoxelizeTriangleGpu[XGIPlacementDiagnosticGpuTriangleMaxCount];
            var step = triangles.Count / (float)XGIPlacementDiagnosticGpuTriangleMaxCount;
            for (var i = 0; i < sampledTriangles.Length; i++)
            {
                sampledTriangles[i] = triangles[Mathf.Min(triangles.Count - 1, Mathf.FloorToInt(i * step))];
            }

            return sampledTriangles;
        }

        private static XGIPlacementVoxelizeBoundsGpu[] BuildPlacementGpuVoxelizeBounds(List<PlacementSdfGeometrySource> geometrySources)
        {
            if (geometrySources == null || geometrySources.Count == 0)
            {
                return System.Array.Empty<XGIPlacementVoxelizeBoundsGpu>();
            }

            var boundsList = new List<XGIPlacementVoxelizeBoundsGpu>();
            for (var sourceIndex = 0; sourceIndex < geometrySources.Count; sourceIndex++)
            {
                var source = geometrySources[sourceIndex];
                if (!IsValidBounds(source.bounds))
                {
                    continue;
                }

                var hasMeshTriangles = source.surfaceTriangles != null && source.surfaceTriangles.Length > 0;
                if (hasMeshTriangles && source.collider == null)
                {
                    continue;
                }

                var extents = source.bounds.extents;
                if (extents.x <= 0f || extents.y <= 0f || extents.z <= 0f)
                {
                    continue;
                }

                var center = source.bounds.center;
                boundsList.Add(new XGIPlacementVoxelizeBoundsGpu
                {
                    center = new Vector4(center.x, center.y, center.z, 0f),
                    extents = new Vector4(extents.x, extents.y, extents.z, 0f)
                });
            }

            if (boundsList.Count <= XGIPlacementDiagnosticGpuBoundsMaxCount)
            {
                return boundsList.ToArray();
            }

            var sampledBounds = new XGIPlacementVoxelizeBoundsGpu[XGIPlacementDiagnosticGpuBoundsMaxCount];
            var step = boundsList.Count / (float)XGIPlacementDiagnosticGpuBoundsMaxCount;
            for (var i = 0; i < sampledBounds.Length; i++)
            {
                sampledBounds[i] = boundsList[Mathf.Min(boundsList.Count - 1, Mathf.FloorToInt(i * step))];
            }

            return sampledBounds;
        }

        private static List<PlacementSdfGeometrySource> CollectPlacementSdfGeometrySources(Scene scene, Bounds cellBounds)
        {
            var geometrySources = new List<PlacementSdfGeometrySource>();
            if (!scene.IsValid() || !IsValidBounds(cellBounds))
            {
                return geometrySources;
            }

            var renderers = Object.FindObjectsOfType<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null ||
                    !renderer.enabled ||
                    !renderer.gameObject.activeInHierarchy ||
                    renderer.gameObject.scene != scene ||
                    !IsValidBounds(renderer.bounds) ||
                    !renderer.bounds.Intersects(cellBounds))
                {
                    continue;
                }

                geometrySources.Add(CreatePlacementSdfRendererSource(renderer, cellBounds));
            }

            var colliders = Object.FindObjectsOfType<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null ||
                    !collider.enabled ||
                    !collider.gameObject.activeInHierarchy ||
                    collider.gameObject.scene != scene ||
                    !IsValidBounds(collider.bounds) ||
                    !collider.bounds.Intersects(cellBounds))
                {
                    continue;
                }

                geometrySources.Add(new PlacementSdfGeometrySource
                {
                    bounds = collider.bounds,
                    collider = collider,
                    kind = "Collider"
                });
            }

            return geometrySources;
        }

        private static PlacementSdfGeometrySource CreatePlacementSdfRendererSource(Renderer renderer, Bounds cellBounds)
        {
            var source = new PlacementSdfGeometrySource
            {
                bounds = renderer.bounds,
                kind = "Renderer"
            };

            if (TryBuildPlacementSdfRendererSurfaceSamples(renderer, cellBounds, out var samples, out var triangles))
            {
                source.surfaceSamples = samples;
                source.surfaceTriangles = triangles;
                source.kind = "RendererMesh";
            }

            return source;
        }

        private static bool TryBuildPlacementSdfRendererSurfaceSamples(
            Renderer renderer,
            Bounds cellBounds,
            out Vector3[] samples,
            out PlacementSdfTriangle[] triangles)
        {
            samples = null;
            triangles = null;
            if (renderer == null)
            {
                return false;
            }

            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
            {
                mesh = skinnedMeshRenderer.sharedMesh;
            }
            else
            {
                var meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    mesh = meshFilter.sharedMesh;
                }
            }

            if (mesh == null || mesh.vertexCount <= 0)
            {
                return false;
            }

            var vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0)
            {
                return false;
            }

            var matrix = renderer.localToWorldMatrix;
            var sampleList = new List<Vector3>();
            var vertexStep = Mathf.Max(1, vertices.Length / XGIPlacementDiagnosticMeshSampleMaxCount);
            for (var i = 0; i < vertices.Length && sampleList.Count < XGIPlacementDiagnosticMeshSampleMaxCount; i += vertexStep)
            {
                var position = matrix.MultiplyPoint3x4(vertices[i]);
                if (cellBounds.Contains(position) || DistanceToBounds(position, cellBounds) <= Mathf.Max(0.01f, cellBounds.size.magnitude * 0.05f))
                {
                    sampleList.Add(position);
                }
            }

            var indices = mesh.triangles;
            if (indices != null && indices.Length >= 3)
            {
                var sourceTriangleCount = indices.Length / 3;
                var triangleList = new List<PlacementSdfTriangle>();
                var triangleStep = Mathf.Max(1, sourceTriangleCount / XGIPlacementDiagnosticMeshSampleMaxCount);
                for (var i = 0; i < sourceTriangleCount && triangleList.Count < XGIPlacementDiagnosticMeshSampleMaxCount; i += triangleStep)
                {
                    var triangleIndex = i * 3;
                    var aIndex = Mathf.Clamp(indices[triangleIndex], 0, vertices.Length - 1);
                    var bIndex = Mathf.Clamp(indices[triangleIndex + 1], 0, vertices.Length - 1);
                    var cIndex = Mathf.Clamp(indices[triangleIndex + 2], 0, vertices.Length - 1);
                    var triangle = new PlacementSdfTriangle
                    {
                        a = matrix.MultiplyPoint3x4(vertices[aIndex]),
                        b = matrix.MultiplyPoint3x4(vertices[bIndex]),
                        c = matrix.MultiplyPoint3x4(vertices[cIndex])
                    };
                    if (!TriangleIntersectsBounds(triangle, cellBounds))
                    {
                        continue;
                    }

                    triangleList.Add(triangle);
                    AddPlacementSdfSampleIfNeeded(sampleList, triangle.a);
                    AddPlacementSdfSampleIfNeeded(sampleList, triangle.b);
                    AddPlacementSdfSampleIfNeeded(sampleList, triangle.c);
                }

                triangles = triangleList.Count > 0 ? triangleList.ToArray() : null;
            }

            if (sampleList.Count == 0 && (triangles == null || triangles.Length == 0))
            {
                var fallbackCount = Mathf.Min(vertices.Length, XGIPlacementDiagnosticMeshSampleMaxCount);
                var fallbackStep = Mathf.Max(1, vertices.Length / fallbackCount);
                for (var i = 0; i < fallbackCount; i++)
                {
                    sampleList.Add(matrix.MultiplyPoint3x4(vertices[Mathf.Min(vertices.Length - 1, i * fallbackStep)]));
                }
            }

            samples = sampleList.Count > 0 ? sampleList.ToArray() : null;
            return (samples != null && samples.Length > 0) || (triangles != null && triangles.Length > 0);
        }

        private static void AddPlacementSdfSampleIfNeeded(List<Vector3> samples, Vector3 position)
        {
            if (samples == null || samples.Count >= XGIPlacementDiagnosticMeshSampleMaxCount)
            {
                return;
            }

            samples.Add(position);
        }

        private static bool TriangleIntersectsBounds(PlacementSdfTriangle triangle, Bounds bounds)
        {
            if (!IsValidBounds(bounds))
            {
                return false;
            }

            var triangleBounds = new Bounds(triangle.a, Vector3.zero);
            triangleBounds.Encapsulate(triangle.b);
            triangleBounds.Encapsulate(triangle.c);
            return triangleBounds.Intersects(bounds);
        }

        private static float DistanceToPlacementSdfGeometry(Vector3 position, PlacementSdfGeometrySource source)
        {
            if (source == null)
            {
                return float.PositiveInfinity;
            }

            var distance = float.PositiveInfinity;
            if (source.collider != null)
            {
                var closestPoint = source.collider.ClosestPoint(position);
                if (IsFinite(closestPoint))
                {
                    distance = Mathf.Min(distance, (position - closestPoint).magnitude);
                }
            }

            if (source.surfaceTriangles != null && source.surfaceTriangles.Length > 0)
            {
                var nearestTriangleSqr = float.PositiveInfinity;
                for (var i = 0; i < source.surfaceTriangles.Length; i++)
                {
                    var closestPoint = ClosestPointOnTriangle(position, source.surfaceTriangles[i]);
                    nearestTriangleSqr = Mathf.Min(nearestTriangleSqr, (position - closestPoint).sqrMagnitude);
                }

                if (!float.IsPositiveInfinity(nearestTriangleSqr))
                {
                    distance = Mathf.Min(distance, Mathf.Sqrt(nearestTriangleSqr));
                }
            }

            if (source.surfaceSamples != null && source.surfaceSamples.Length > 0)
            {
                var nearestSqr = float.PositiveInfinity;
                for (var i = 0; i < source.surfaceSamples.Length; i++)
                {
                    nearestSqr = Mathf.Min(nearestSqr, (position - source.surfaceSamples[i]).sqrMagnitude);
                }

                if (!float.IsPositiveInfinity(nearestSqr))
                {
                    distance = Mathf.Min(distance, Mathf.Sqrt(nearestSqr));
                }
            }

            if (!IsFinite(distance))
            {
                distance = DistanceToBounds(position, source.bounds);
            }

            return distance;
        }

        private static Vector3 ClosestPointOnTriangle(Vector3 point, PlacementSdfTriangle triangle)
        {
            var ab = triangle.b - triangle.a;
            var ac = triangle.c - triangle.a;
            var ap = point - triangle.a;
            var d1 = Vector3.Dot(ab, ap);
            var d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f)
            {
                return triangle.a;
            }

            var bp = point - triangle.b;
            var d3 = Vector3.Dot(ab, bp);
            var d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3)
            {
                return triangle.b;
            }

            var vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                var v = d1 / (d1 - d3);
                return triangle.a + ab * v;
            }

            var cp = point - triangle.c;
            var d5 = Vector3.Dot(ab, cp);
            var d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6)
            {
                return triangle.c;
            }

            var vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                var w = d2 / (d2 - d6);
                return triangle.a + ac * w;
            }

            var va = d3 * d6 - d5 * d4;
            if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            {
                var w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return triangle.b + (triangle.c - triangle.b) * w;
            }

            var denom = 1f / (va + vb + vc);
            var baryB = vb * denom;
            var baryC = vc * denom;
            return triangle.a + ab * baryB + ac * baryC;
        }

        private static int CountPlacementSdfMeshSamples(List<PlacementSdfGeometrySource> sources)
        {
            if (sources == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < sources.Count; i++)
            {
                var samples = sources[i].surfaceSamples;
                count += samples != null ? samples.Length : 0;
            }

            return count;
        }

        private static int CountPlacementSdfMeshTriangles(List<PlacementSdfGeometrySource> sources)
        {
            if (sources == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < sources.Count; i++)
            {
                var triangles = sources[i].surfaceTriangles;
                count += triangles != null ? triangles.Length : 0;
            }

            return count;
        }

        private static List<XGIPlacementGpuDiagnosticCellCandidate> SelectPlacementGpuDiagnosticCells(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbePlacedCell[] cells)
        {
            var candidates = new List<XGIPlacementGpuDiagnosticCellCandidate>(Mathf.Min(cells != null ? cells.Length : 0, MaxPlacementLiteCells));
            if (config == null || cells == null || cells.Length == 0)
            {
                return candidates;
            }

            for (var i = 0; i < cells.Length && candidates.Count < MaxPlacementLiteCells; i++)
            {
                var cell = cells[i];
                if (cell.brickCount <= 0)
                {
                    continue;
                }

                AddPlacementGpuDiagnosticCellCandidate(config, cell, candidates);
            }

            for (var i = 0; i < cells.Length && candidates.Count < MaxPlacementLiteCells; i++)
            {
                var cell = cells[i];
                if (cell.brickCount > 0)
                {
                    continue;
                }

                AddPlacementGpuDiagnosticCellCandidate(config, cell, candidates);
            }

            return candidates;
        }

        private static void AddPlacementGpuDiagnosticCellCandidate(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbePlacedCell cell,
            List<XGIPlacementGpuDiagnosticCellCandidate> candidates)
        {
            var bounds = IsValidBounds(cell.bounds) ? cell.bounds : CreateCellBounds(config, cell.position);
            if (!IsValidBounds(bounds))
            {
                return;
            }

            candidates.Add(new XGIPlacementGpuDiagnosticCellCandidate
            {
                cell = cell,
                bounds = bounds
            });
        }

        private static string BuildPlacementGpuDiagnosticCellCandidateSummary(List<XGIPlacementGpuDiagnosticCellCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return "None";
            }

            var builder = new StringBuilder();
            var reportCount = Mathf.Min(candidates.Count, XGIPlacementGpuDiagnosticReportCellCount);
            for (var i = 0; i < reportCount; i++)
            {
                if (i > 0)
                {
                    builder.Append("|");
                }

                var cell = candidates[i].cell;
                builder
                    .Append(cell.position)
                    .Append(":Bricks=")
                    .Append(cell.brickCount);
            }

            if (candidates.Count > reportCount)
            {
                builder.Append("|...Total=").Append(candidates.Count);
            }

            return builder.ToString();
        }

        internal bool BuildRealtimeSubdivisionSnapshot(
            BurtXGIProbeBakingConfig config,
            Camera camera,
            float cullingDistance,
            int maxCells,
            out RealtimeSubdivisionSnapshotResult result)
        {
            result = default;
            result.cells = new List<RealtimeSubdivisionCell>();
            result.maxCellBudget = Mathf.Max(1, maxCells);
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            var volumeBounds = CollectActiveSceneVolumeOBBs(scene);
            if (volumeBounds.Count == 0)
            {
                result.error = "Active scene has no enabled BurtGIProbeVolume with a valid extent.";
                return false;
            }

            var bounds = BuildOBBBounds(volumeBounds[0]);
            for (var i = 1; i < volumeBounds.Count; i++)
            {
                bounds.Encapsulate(BuildOBBBounds(volumeBounds[i]));
            }

            var cellMin = config.PositionToCell(bounds.min);
            var cellMax = config.PositionToCell(bounds.max);
            var cellCountVector = cellMax - cellMin + Vector3Int.one;
            if (cellCountVector.x <= 0 || cellCountVector.y <= 0 || cellCountVector.z <= 0)
            {
                result.error = "Invalid realtime subdivision cell range.";
                return false;
            }

            var candidateCellCount = (long)cellCountVector.x * cellCountVector.y * cellCountVector.z;
            result.candidateCellCount = candidateCellCount > int.MaxValue ? int.MaxValue : (int)candidateCellCount;
            if (candidateCellCount > MaxPlacementLiteCells)
            {
                result.error = "Realtime subdivision cell range is too large: " + candidateCellCount + " cells.";
                return false;
            }

            var candidates = new List<RealtimeSubdivisionCellCandidate>(Mathf.Min(result.candidateCellCount, 4096));
            var planes = camera != null ? GeometryUtility.CalculateFrustumPlanes(camera) : null;
            var cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            var maxDistance = Mathf.Max(0.01f, cullingDistance);
            var maxDistanceSqr = maxDistance * maxDistance;
            for (var z = cellMin.z; z <= cellMax.z; z++)
            {
                for (var y = cellMin.y; y <= cellMax.y; y++)
                {
                    for (var x = cellMin.x; x <= cellMax.x; x++)
                    {
                        var cellPosition = new Vector3Int(x, y, z);
                        var cellBounds = CreateCellBounds(config, cellPosition);
                        if (!IntersectsAny(cellBounds, volumeBounds))
                        {
                            continue;
                        }

                        if (camera != null &&
                            ((cellBounds.center - cameraPosition).sqrMagnitude > maxDistanceSqr ||
                             (planes != null && !GeometryUtility.TestPlanesAABB(planes, cellBounds))))
                        {
                            continue;
                        }

                        candidates.Add(new RealtimeSubdivisionCellCandidate(
                            cellPosition,
                            cellBounds,
                            camera != null ? (cellBounds.center - cameraPosition).sqrMagnitude : 0f));
                    }
                }
            }

            result.visibleCellCount = candidates.Count;
            if (candidates.Count == 0)
            {
                result.success = true;
                return true;
            }

            candidates.Sort((left, right) => left.DistanceSqr.CompareTo(right.DistanceSqr));
            var scratchBricks = new List<BurtXGIProbePlacedBrick>();
            var scratchProbePositions = new List<Vector3>();
            var updateCount = Mathf.Min(candidates.Count, result.maxCellBudget);
            for (var candidateIndex = 0; candidateIndex < updateCount; candidateIndex++)
            {
                var candidate = candidates[candidateIndex];
                scratchBricks.Clear();
                scratchProbePositions.Clear();
                var cellIndex = CellPositionToIndex(candidate.Position, cellMin, cellCountVector);
                AppendAdaptivePlacementBricks(config, candidate.Position, candidate.Bounds, volumeBounds, cellIndex, scratchBricks, scratchProbePositions);
                if (scratchBricks.Count == 0)
                {
                    continue;
                }

                var cell = new RealtimeSubdivisionCell
                {
                    bounds = candidate.Bounds,
                    position = candidate.Position,
                    bricks = new List<RealtimeSubdivisionBrick>(scratchBricks.Count)
                };
                for (var brickIndex = 0; brickIndex < scratchBricks.Count; brickIndex++)
                {
                    var brick = scratchBricks[brickIndex];
                    var brickSizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(brick.subdivisionLevel);
                    cell.bricks.Add(new RealtimeSubdivisionBrick
                    {
                        bounds = CreateBrickBounds(config, brick.position, brickSizeInBricks),
                        subdivisionLevel = brick.subdivisionLevel,
                        position = brick.position
                    });
                }

                result.brickCount += cell.bricks.Count;
                result.cells.Add(cell);
            }

            result.updatedCellCount = result.cells.Count;
            result.success = true;
            return true;
        }

        internal bool RunVirtualOffsetLite(BurtXGIProbeBakingConfig config, out VirtualOffsetResult result)
        {
            result = default;
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            var probePositions = config.bakedProbePositions ?? System.Array.Empty<Vector3>();
            if (probePositions.Length == 0)
            {
                if (!RunPlacementLite(config, out var placementResult))
                {
                    result.error = placementResult.error;
                    result.report = BuildVirtualOffsetReport(config, result);
                    return false;
                }

                probePositions = config.bakedProbePositions ?? System.Array.Empty<Vector3>();
            }

            if (probePositions.Length == 0)
            {
                result.error = "VirtualOffset Lite requires baked probe positions.";
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            var offsets = new Vector3[probePositions.Length];
            var adjustedProbePositions = new Vector3[probePositions.Length];
            result.enabled = config.virtualOffset;
            result.probeCount = probePositions.Length;

            if (!config.virtualOffset)
            {
                for (var i = 0; i < adjustedProbePositions.Length; i++)
                {
                    adjustedProbePositions[i] = probePositions[i];
                }

                config.CaptureVirtualOffsets(offsets, adjustedProbePositions, 0, false);
                EditorUtility.SetDirty(config);
                result.success = true;
                result.offsetCount = offsets.Length;
                result.report = BuildVirtualOffsetReport(config, result);
                return true;
            }

            var scene = SceneManager.GetActiveScene();
            var geometry = CollectBakingVirtualOffsetGeometry(config, scene);
            var adjustVolumes = CollectBakingVirtualOffsetAdjustVolumes(config, scene);
            if (!geometry.HasGeometry && !adjustVolumes.HasAppliers)
            {
                result.error = "VirtualOffset Lite requires at least one active scene Collider, Renderer, or BurtXGIProbeAdjustVolume applier.";
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            Physics.SyncTransforms();
            var subdivisionLevels = ResolveProbeSubdivisionLevels(config, probePositions.Length);
            var adjustedCount = 0;
            for (var i = 0; i < probePositions.Length; i++)
            {
                var position = probePositions[i];
                if (TryResolveVirtualOffsetApplier(position, config, adjustVolumes, out var appliedOffset))
                {
                    offsets[i] = appliedOffset;
                    adjustedProbePositions[i] = position + appliedOffset;
                    adjustedCount++;
                    continue;
                }

                var overrideSettings = ResolveVirtualOffsetOverrideSettings(position, config, adjustVolumes);
                if (TryResolveVirtualOffset(position, config, subdivisionLevels[i], geometry, overrideSettings.geometryBias, out var offset))
                {
                    offsets[i] = offset;
                    adjustedProbePositions[i] = position + offset;
                    adjustedCount++;
                    continue;
                }

                adjustedProbePositions[i] = position;
            }

            config.CaptureVirtualOffsets(offsets, adjustedProbePositions, adjustedCount, adjustedCount > 0);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.applied = adjustedCount > 0;
            result.offsetCount = offsets.Length;
            result.invalidCount = adjustedCount;
            result.report = BuildVirtualOffsetReport(config, result);
            return true;
        }

        internal bool RunVirtualOffsetXRenderPath(BurtXGIProbeBakingConfig config, out VirtualOffsetResult result)
        {
            result = default;
            if (config != null && config.useHardWareRayTracing && SystemInfo.supportsRayTracing &&
                TryRunVirtualOffsetRayTracing(config, out result))
            {
                return true;
            }

            if (!RunVirtualOffsetLite(config, out result))
            {
                return false;
            }

            if (string.IsNullOrEmpty(result.backend))
            {
                result.backend = "LiteCPU";
                result.report = BuildVirtualOffsetReport(config, result);
            }

            return true;
        }

        private static bool TryRunVirtualOffsetRayTracing(BurtXGIProbeBakingConfig config, out VirtualOffsetResult result)
        {
            result = default;
            result.backend = "RayTracingFallback";
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            var probePositions = config.bakedProbePositions ?? System.Array.Empty<Vector3>();
            if (probePositions.Length == 0)
            {
                if (!Instance.RunPlacementLite(config, out var placementResult))
                {
                    result.error = placementResult.error;
                    result.report = BuildVirtualOffsetReport(config, result);
                    return false;
                }

                probePositions = config.bakedProbePositions ?? System.Array.Empty<Vector3>();
            }

            if (probePositions.Length == 0)
            {
                result.error = "VirtualOffset ray tracing requires baked probe positions.";
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            result.enabled = config.virtualOffset;
            result.probeCount = probePositions.Length;
            var offsets = new Vector3[probePositions.Length];
            var adjustedProbePositions = new Vector3[probePositions.Length];
            if (!config.virtualOffset)
            {
                for (var i = 0; i < adjustedProbePositions.Length; i++)
                {
                    adjustedProbePositions[i] = probePositions[i];
                }

                config.CaptureVirtualOffsets(offsets, adjustedProbePositions, 0, false);
                EditorUtility.SetDirty(config);
                result.success = true;
                result.backend = "Disabled";
                result.offsetCount = offsets.Length;
                result.report = BuildVirtualOffsetReport(config, result);
                return true;
            }

            var shader = Resources.Load<RayTracingShader>(XGIProbeBakingRayTracingResourcePath);
            if (shader == null)
            {
                result.error = "Missing ray tracing shader resource: " + XGIProbeBakingRayTracingResourcePath;
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            if (!TryResolveBakingRayTracingAccelerationStructure(out var rayTracingCamera, out var accelerationStructure))
            {
                result.error = "VirtualOffset ray tracing requires a valid BurtGI ray tracing acceleration structure.";
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            var probeData = new XGIVirtualOffsetProbeData[probePositions.Length];
            var subdivisionLevels = ResolveProbeSubdivisionLevels(config, probePositions.Length);
            var adjustVolumes = CollectBakingVirtualOffsetAdjustVolumes(config, SceneManager.GetActiveScene());
            for (var i = 0; i < probeData.Length; i++)
            {
                var overrideSettings = ResolveVirtualOffsetOverrideSettings(probePositions[i], config, adjustVolumes);
                if (TryResolveVirtualOffsetApplier(probePositions[i], config, adjustVolumes, out var appliedOffset))
                {
                    offsets[i] = appliedOffset;
                    overrideSettings.validityThreshold = 1f;
                }

                probeData[i] = new XGIVirtualOffsetProbeData
                {
                    position = probePositions[i],
                    probeIndex = i,
                    tMax = offsets[i].sqrMagnitude > 0.00000001f ? 0f : ResolveVirtualOffsetSearchDistance(config, subdivisionLevels[i]),
                    originBias = overrideSettings.originBias,
                    geometryBias = overrideSettings.geometryBias,
                    validityThreshold = overrideSettings.validityThreshold
                };
            }

            try
            {
                var batches = BuildBakingProbeBatches(config, probePositions, XGIVirtualOffsetMaxProbeCountPerBatch);
                for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    var batch = batches[batchIndex];
                    var batchOffset = batch.probeStartIndex;
                    var batchSize = batch.probeCount;
                    var batchProbeData = new XGIVirtualOffsetProbeData[batchSize];
                    var batchOffsets = new Vector3[batchSize];
                    System.Array.Copy(probeData, batchOffset, batchProbeData, 0, batchSize);
                    System.Array.Copy(offsets, batchOffset, batchOffsets, 0, batchSize);
                    ComputeBuffer outputBuffer = null;
                    ComputeBuffer probeDataBuffer = null;
                    try
                    {
                        outputBuffer = new ComputeBuffer(batchSize, sizeof(float) * 3);
                        probeDataBuffer = new ComputeBuffer(batchSize, sizeof(float) * 7 + sizeof(int));
                        outputBuffer.SetData(batchOffsets);
                        probeDataBuffer.SetData(batchProbeData);
                        var dispatchWidth = Mathf.CeilToInt(Mathf.Sqrt(batchSize));
                        var cmd = CommandBufferPool.Get("Burt XGI Probe Baking VirtualOffset RayTracing");
                        try
                        {
                            cmd.SetRayTracingShaderPass(shader, XGIProbeBakingRayTracingPassName);
                            cmd.SetRayTracingAccelerationStructure(shader, XGIProbeBakingAccelerationStructureId, accelerationStructure);
                            cmd.SetGlobalBuffer(XGIVirtualOffsetOutputId, outputBuffer);
                            cmd.SetGlobalBuffer(XGIVirtualOffsetProbeDataId, probeDataBuffer);
                            cmd.SetRayTracingVectorParam(shader, XGIVirtualOffsetParamsId, new Vector4(batchSize, 0f, dispatchWidth, batchIndex));
                            cmd.SetRayTracingVectorParam(shader, XGIVirtualOffsetParams1Id, new Vector4(batchOffset, batches.Count, 0f, 0f));
                            cmd.DispatchRays(shader, XGIVirtualOffsetRayGenName, (uint)dispatchWidth, (uint)dispatchWidth, 1u, rayTracingCamera);
                            Graphics.ExecuteCommandBuffer(cmd);
                        }
                        finally
                        {
                            CommandBufferPool.Release(cmd);
                        }

                        outputBuffer.GetData(batchOffsets);
                    }
                    finally
                    {
                        outputBuffer?.Release();
                        probeDataBuffer?.Release();
                    }

                    System.Array.Copy(batchOffsets, 0, offsets, batchOffset, batchSize);
                }
            }
            catch (System.Exception exception)
            {
                result.error = "VirtualOffset ray tracing failed: " + exception.Message;
                result.report = BuildVirtualOffsetReport(config, result);
                return false;
            }

            var adjustedCount = 0;
            for (var i = 0; i < adjustedProbePositions.Length; i++)
            {
                if (offsets[i].sqrMagnitude > 0.00000001f)
                {
                    adjustedCount++;
                }

                adjustedProbePositions[i] = probePositions[i] + offsets[i];
            }

            config.CaptureVirtualOffsets(offsets, adjustedProbePositions, adjustedCount, adjustedCount > 0);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.applied = adjustedCount > 0;
            result.offsetCount = offsets.Length;
            result.invalidCount = adjustedCount;
            result.report = BuildVirtualOffsetReport(config, result);
            return true;
        }

        internal bool RunSkyVisibilityLite(BurtXGIProbeBakingConfig config, out SkyVisibilityResult result)
        {
            result = default;
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            var probePositions = ResolveEffectiveProbePositions(config);
            if (probePositions.Length == 0)
            {
                if (!RunVirtualOffsetXRenderPath(config, out var virtualOffsetResult))
                {
                    result.error = virtualOffsetResult.error;
                    result.report = BuildSkyVisibilityReport(config, result);
                    return false;
                }

                probePositions = ResolveEffectiveProbePositions(config);
            }

            if (probePositions.Length == 0)
            {
                result.error = "SkyVisibility Lite requires baked probe positions.";
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            result.enabled = config.skyVisibility;
            result.shadingDirection = config.skyVisibility && config.skyVisibilityShadingDirection;
            result.probeCount = probePositions.Length;

            if (!config.skyVisibility)
            {
                config.CaptureSkyVisibility(System.Array.Empty<Vector4>(), System.Array.Empty<byte>());
                EditorUtility.SetDirty(config);
                result.success = true;
                result.report = BuildSkyVisibilityReport(config, result);
                return true;
            }

            var skyVisibility = new Vector4[probePositions.Length];
            var skyDirection = config.skyVisibilityShadingDirection ? new byte[probePositions.Length] : System.Array.Empty<byte>();
            var geometry = CollectBakingVirtualOffsetGeometry(config, SceneManager.GetActiveScene());
            var sampleCount = ResolveSkyVisibilitySampleCount(config);
            var maxRayDistance = ResolveSkyVisibilityRayDistance(config, geometry);
            Physics.SyncTransforms();
            for (var i = 0; i < probePositions.Length; i++)
            {
                skyVisibility[i] = BakeSkyVisibilityProbe(probePositions[i], config, geometry, sampleCount, maxRayDistance, out var skyDirectionIndex);
                if (skyDirection.Length > 0)
                {
                    skyDirection[i] = skyDirectionIndex;
                }
            }

            config.CaptureSkyVisibility(skyVisibility, skyDirection);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.occlusionCount = skyVisibility.Length;
            result.directionCount = skyDirection.Length;
            result.report = BuildSkyVisibilityReport(config, result);
            return true;
        }

        internal bool RunSkyVisibilityXRenderPath(BurtXGIProbeBakingConfig config, out SkyVisibilityResult result)
        {
            result = default;
            if (config != null && config.useHardWareRayTracing && SystemInfo.supportsRayTracing &&
                TryRunSkyVisibilityRayTracing(config, out result))
            {
                return true;
            }

            if (config != null && SystemInfo.supportsComputeShaders &&
                TryRunSkyVisibilityCompute(config, out result))
            {
                return true;
            }

            if (!RunSkyVisibilityLite(config, out result))
            {
                return false;
            }

            if (string.IsNullOrEmpty(result.backend))
            {
                result.backend = "LiteCPU";
                result.report = BuildSkyVisibilityReport(config, result);
            }

            return true;
        }

        private static bool TryRunSkyVisibilityRayTracing(BurtXGIProbeBakingConfig config, out SkyVisibilityResult result)
        {
            result = default;
            result.backend = "RayTracingFallback";
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            var probePositions = ResolveEffectiveProbePositions(config);
            if (probePositions.Length == 0)
            {
                if (!Instance.RunVirtualOffsetXRenderPath(config, out var virtualOffsetResult))
                {
                    result.error = virtualOffsetResult.error;
                    result.report = BuildSkyVisibilityReport(config, result);
                    return false;
                }

                probePositions = ResolveEffectiveProbePositions(config);
            }

            if (probePositions.Length == 0)
            {
                result.error = "SkyVisibility ray tracing requires baked probe positions.";
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            result.enabled = config.skyVisibility;
            result.shadingDirection = config.skyVisibility && config.skyVisibilityShadingDirection;
            result.probeCount = probePositions.Length;
            if (!config.skyVisibility)
            {
                config.CaptureSkyVisibility(System.Array.Empty<Vector4>(), System.Array.Empty<byte>());
                EditorUtility.SetDirty(config);
                result.success = true;
                result.backend = "Disabled";
                result.report = BuildSkyVisibilityReport(config, result);
                return true;
            }

            var shader = Resources.Load<RayTracingShader>(XGIProbeBakingRayTracingResourcePath);
            if (shader == null)
            {
                result.error = "Missing ray tracing shader resource: " + XGIProbeBakingRayTracingResourcePath;
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            if (!TryResolveBakingRayTracingAccelerationStructure(out var rayTracingCamera, out var accelerationStructure))
            {
                result.error = "SkyVisibility ray tracing requires a valid BurtGI ray tracing acceleration structure.";
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            var probeCount = probePositions.Length;
            var skyVisibility = new Vector4[probeCount];
            var skyDirectionEncoded = new uint[probeCount];
            var skyDirections = GetDefaultXGISkyShadingDirections();
            var sampleCount = Mathf.Max(1, ResolveSkyVisibilitySampleCount(config));
            var geometry = CollectBakingVirtualOffsetGeometry(config, SceneManager.GetActiveScene());
            var parameters = new Vector4[]
            {
                new Vector4(0f, sampleCount, 0f, 0f)
            };

            ComputeBuffer parametersBuffer = null;
            ComputeBuffer precomputedDirectionsBuffer = null;
            try
            {
                parametersBuffer = new ComputeBuffer(1, sizeof(float) * 4);
                precomputedDirectionsBuffer = new ComputeBuffer(skyDirections.Length, sizeof(float) * 3);
                parametersBuffer.SetData(parameters);
                precomputedDirectionsBuffer.SetData(skyDirections);

                var batches = BuildBakingProbeBatches(config, probePositions, XGITimeSliceMaxProbeCountPerBatch);
                for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    var batch = batches[batchIndex];
                    var batchOffset = batch.probeStartIndex;
                    var batchSize = batch.probeCount;
                    var batchMaxRayDistance = ResolveSkyVisibilityRayDistance(config, geometry, batch.bounds, batch.hasBounds);
                    var batchSkyVisibility = new Vector4[batchSize];
                    var batchSkyDirectionVectors = new Vector3[batchSize];
                    var batchSkyDirectionEncoded = new uint[batchSize];
                    var batchProbePositions = new Vector3[batchSize];
                    System.Array.Copy(probePositions, batchOffset, batchProbePositions, 0, batchSize);
                    ComputeBuffer skyVisibilityBuffer = null;
                    ComputeBuffer skyDirectionBuffer = null;
                    ComputeBuffer skyDirectionEncodedBuffer = null;
                    ComputeBuffer probePositionBuffer = null;
                    try
                    {
                        skyVisibilityBuffer = new ComputeBuffer(batchSize, sizeof(float) * 4);
                        skyDirectionBuffer = new ComputeBuffer(batchSize, sizeof(float) * 3);
                        skyDirectionEncodedBuffer = new ComputeBuffer(batchSize, sizeof(uint));
                        probePositionBuffer = new ComputeBuffer(batchSize, sizeof(float) * 3);
                        skyVisibilityBuffer.SetData(batchSkyVisibility);
                        skyDirectionBuffer.SetData(batchSkyDirectionVectors);
                        skyDirectionEncodedBuffer.SetData(batchSkyDirectionEncoded);
                        probePositionBuffer.SetData(batchProbePositions);

                        var dispatchWidth = Mathf.CeilToInt(Mathf.Sqrt(batchSize));
                        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                        {
                            parameters[0].x = sampleIndex;
                            parametersBuffer.SetData(parameters);
                            var cmd = CommandBufferPool.Get("Burt XGI Probe Baking SkyVisibility RayTracing");
                            try
                            {
                                cmd.SetRayTracingShaderPass(shader, XGIProbeBakingRayTracingPassName);
                                cmd.SetRayTracingAccelerationStructure(shader, XGIProbeBakingAccelerationStructureId, accelerationStructure);
                                cmd.SetGlobalBuffer(XGISkyVisibilityOutputId, skyVisibilityBuffer);
                                cmd.SetGlobalBuffer(XGISkyVisibilityDirectionOutputId, skyDirectionBuffer);
                                cmd.SetGlobalBuffer(XGISkyVisibilityDirectionEncodedOutputId, skyDirectionEncodedBuffer);
                                cmd.SetGlobalBuffer(XGISkyVisibilityProbePositionsId, probePositionBuffer);
                                cmd.SetGlobalBuffer(XGISkyVisibilityParametersBufferId, parametersBuffer);
                                cmd.SetGlobalBuffer(XGISkyVisibilityPrecomputedDirectionsId, precomputedDirectionsBuffer);
                                cmd.SetRayTracingFloatParam(shader, XGISkyVisibilityOffsetRayId, config.skyVisibilityOffsetRay);
                                cmd.SetRayTracingFloatParam(shader, XGISkyVisibilityMaxBouncesId, config.skyVisibilityBakingBounces);
                                cmd.SetRayTracingFloatParam(shader, XGISkyVisibilitySampleCountId, sampleCount);
                                cmd.SetRayTracingFloatParam(shader, XGISkyVisibilitySampleIndexId, sampleIndex);
                                cmd.SetRayTracingVectorParam(shader, XGISkyVisibilityParamsId, new Vector4(
                                    batchSize,
                                    config.skyVisibilityAverageAlbedo,
                                    dispatchWidth,
                                    config.skyVisibilityShadingDirection ? 1f : 0f));
                                cmd.SetRayTracingVectorParam(shader, XGISkyVisibilityParams1Id, new Vector4(
                                    batchIndex,
                                    batchOffset,
                                    config.skyVisibilityRayCullBackFace ? 1f : 0f,
                                    batchMaxRayDistance));
                                cmd.DispatchRays(shader, XGISkyVisibilityRayGenName, (uint)dispatchWidth, (uint)dispatchWidth, 1u, rayTracingCamera);
                                Graphics.ExecuteCommandBuffer(cmd);
                            }
                            finally
                            {
                                CommandBufferPool.Release(cmd);
                            }
                        }

                        skyVisibilityBuffer.GetData(batchSkyVisibility);
                        skyDirectionEncodedBuffer.GetData(batchSkyDirectionEncoded);
                    }
                    finally
                    {
                        skyVisibilityBuffer?.Release();
                        skyDirectionBuffer?.Release();
                        skyDirectionEncodedBuffer?.Release();
                        probePositionBuffer?.Release();
                    }

                    System.Array.Copy(batchSkyVisibility, 0, skyVisibility, batchOffset, batchSize);
                    System.Array.Copy(batchSkyDirectionEncoded, 0, skyDirectionEncoded, batchOffset, batchSize);
                }
            }
            catch (System.Exception exception)
            {
                result.error = "SkyVisibility ray tracing failed: " + exception.Message;
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }
            finally
            {
                parametersBuffer?.Release();
                precomputedDirectionsBuffer?.Release();
            }

            var skyDirection = config.skyVisibilityShadingDirection ? new byte[probeCount] : System.Array.Empty<byte>();
            for (var i = 0; i < skyDirection.Length; i++)
            {
                skyDirection[i] = (byte)Mathf.Clamp((int)skyDirectionEncoded[i], 0, 255);
            }

            config.CaptureSkyVisibility(skyVisibility, skyDirection);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.occlusionCount = skyVisibility.Length;
            result.directionCount = skyDirection.Length;
            result.report = BuildSkyVisibilityReport(config, result);
            return true;
        }

        private static bool TryRunSkyVisibilityCompute(BurtXGIProbeBakingConfig config, out SkyVisibilityResult result)
        {
            result = default;
            result.backend = "GpuComputeFallback";
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            var probePositions = ResolveEffectiveProbePositions(config);
            if (probePositions.Length == 0)
            {
                if (!Instance.RunVirtualOffsetXRenderPath(config, out var virtualOffsetResult))
                {
                    result.error = virtualOffsetResult.error;
                    result.report = BuildSkyVisibilityReport(config, result);
                    return false;
                }

                probePositions = ResolveEffectiveProbePositions(config);
            }

            if (probePositions.Length == 0)
            {
                result.error = "SkyVisibility GPU compute requires baked probe positions.";
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            result.enabled = config.skyVisibility;
            result.shadingDirection = config.skyVisibility && config.skyVisibilityShadingDirection;
            result.probeCount = probePositions.Length;

            if (!config.skyVisibility)
            {
                config.CaptureSkyVisibility(System.Array.Empty<Vector4>(), System.Array.Empty<byte>());
                EditorUtility.SetDirty(config);
                result.success = true;
                result.backend = "Disabled";
                result.report = BuildSkyVisibilityReport(config, result);
                return true;
            }

            var shader = Resources.Load<ComputeShader>(XGIProbeBakingComputeShaderResourcePath);
            if (shader == null)
            {
                result.error = "Missing compute shader resource: " + XGIProbeBakingComputeShaderResourcePath;
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            if (!shader.HasKernel(XGISkyVisibilityComputeKernelName))
            {
                result.error = "Missing compute kernel: " + XGIProbeBakingComputeShaderResourcePath + "." + XGISkyVisibilityComputeKernelName;
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }

            var kernel = shader.FindKernel(XGISkyVisibilityComputeKernelName);
            var probeCount = probePositions.Length;
            var skyVisibility = new Vector4[probeCount];
            var skyDirectionEncoded = new uint[probeCount];
            var skyDirections = GetDefaultXGISkyShadingDirections();
            var parameters = new[] { Vector4.zero };

            ComputeBuffer parametersBuffer = null;
            ComputeBuffer precomputedDirectionsBuffer = null;
            try
            {
                parametersBuffer = new ComputeBuffer(1, sizeof(float) * 4);
                precomputedDirectionsBuffer = new ComputeBuffer(skyDirections.Length, sizeof(float) * 3);

                parametersBuffer.SetData(parameters);
                precomputedDirectionsBuffer.SetData(skyDirections);

                shader.SetBuffer(kernel, "_XGISkyVisibilityGen_ParamtersBuffer", parametersBuffer);
                shader.SetBuffer(kernel, "_XGISkyVisibilityGen_PrecomputedDirections", precomputedDirectionsBuffer);
                shader.SetFloat("_XGISkyVisibilityGen_OffsetRay", config.skyVisibilityOffsetRay);
                shader.SetFloat("_XGISkyVisibilityGen_MaxBounces", config.skyVisibilityBakingBounces);

                var sampleCount = ResolveSkyVisibilitySampleCount(config);
                shader.SetFloat("_XGISkyVisibilityGen_SampleCount", sampleCount);
                var batches = BuildBakingProbeBatches(config, probePositions, XGITimeSliceMaxProbeCountPerBatch);
                for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    var batch = batches[batchIndex];
                    var batchOffset = batch.probeStartIndex;
                    var batchSize = batch.probeCount;
                    var batchProbePositions = new Vector3[batchSize];
                    var batchSkyVisibility = new Vector4[batchSize];
                    var batchSkyDirectionVectors = new Vector3[batchSize];
                    var batchSkyDirectionEncoded = new uint[batchSize];
                    System.Array.Copy(probePositions, batchOffset, batchProbePositions, 0, batchSize);
                    ComputeBuffer probePositionBuffer = null;
                    ComputeBuffer skyVisibilityBuffer = null;
                    ComputeBuffer skyDirectionBuffer = null;
                    ComputeBuffer skyDirectionEncodedBuffer = null;
                    try
                    {
                        probePositionBuffer = new ComputeBuffer(batchSize, sizeof(float) * 3);
                        skyVisibilityBuffer = new ComputeBuffer(batchSize, sizeof(float) * 4);
                        skyDirectionBuffer = new ComputeBuffer(batchSize, sizeof(float) * 3);
                        skyDirectionEncodedBuffer = new ComputeBuffer(batchSize, sizeof(uint));

                        probePositionBuffer.SetData(batchProbePositions);
                        skyVisibilityBuffer.SetData(batchSkyVisibility);
                        skyDirectionBuffer.SetData(batchSkyDirectionVectors);
                        skyDirectionEncodedBuffer.SetData(batchSkyDirectionEncoded);

                        shader.SetBuffer(kernel, "_RW_XGISkyVisibilityGen_Output", skyVisibilityBuffer);
                        shader.SetBuffer(kernel, "_RW_XGISkyVisibilityGen_DirectionOutput", skyDirectionBuffer);
                        shader.SetBuffer(kernel, "_RW_XGISkyVisibilityGen_DirectionEncodedOutput", skyDirectionEncodedBuffer);
                        shader.SetBuffer(kernel, "_XGISkyVisibilityGen_ProbePositions", probePositionBuffer);

                        var dispatchWidth = Mathf.CeilToInt(Mathf.Sqrt(batchSize));
                        var groupsX = Mathf.Max(1, Mathf.CeilToInt(dispatchWidth / (float)XGIProbeBakingThreadGroupSize));
                        var groupsY = groupsX;
                        shader.SetVector("_XGISkyVisibilityGen_Params", new Vector4(
                            batchSize,
                            config.skyVisibilityAverageAlbedo,
                            groupsX,
                            config.skyVisibilityShadingDirection ? 1f : 0f));
                        shader.SetVector("_XGISkyVisibilityGen_Params1", new Vector4(
                            batchIndex,
                            batchOffset,
                            config.skyVisibilityRayCullBackFace ? 1f : 0f,
                            0f));
                        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                        {
                            shader.SetFloat("_XGISkyVisibilityGen_SampleIndex", sampleIndex);
                            shader.Dispatch(kernel, groupsX, groupsY, 1);
                        }

                        skyVisibilityBuffer.GetData(batchSkyVisibility);
                        skyDirectionEncodedBuffer.GetData(batchSkyDirectionEncoded);
                    }
                    finally
                    {
                        probePositionBuffer?.Release();
                        skyVisibilityBuffer?.Release();
                        skyDirectionBuffer?.Release();
                        skyDirectionEncodedBuffer?.Release();
                    }

                    System.Array.Copy(batchSkyVisibility, 0, skyVisibility, batchOffset, batchSize);
                    System.Array.Copy(batchSkyDirectionEncoded, 0, skyDirectionEncoded, batchOffset, batchSize);
                }
            }
            catch (System.Exception exception)
            {
                result.error = "SkyVisibility GPU compute failed: " + exception.Message;
                result.report = BuildSkyVisibilityReport(config, result);
                return false;
            }
            finally
            {
                parametersBuffer?.Release();
                precomputedDirectionsBuffer?.Release();
            }

            var skyDirection = config.skyVisibilityShadingDirection ? new byte[probeCount] : System.Array.Empty<byte>();
            for (var i = 0; i < skyDirection.Length; i++)
            {
                skyDirection[i] = (byte)Mathf.Clamp((int)skyDirectionEncoded[i], 0, 255);
            }

            config.CaptureSkyVisibility(skyVisibility, skyDirection);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.occlusionCount = skyVisibility.Length;
            result.directionCount = skyDirection.Length;
            result.report = BuildSkyVisibilityReport(config, result);
            return true;
        }

        internal bool RunTimeSliceLite(BurtXGIProbeBakingConfig config, out TimeSliceResult result)
        {
            result = default;
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            if (!config.SupportsCurrentTimeSliceBake(out var timeSliceError))
            {
                result.error = timeSliceError;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            var probePositions = ResolveEffectiveProbePositions(config);
            if (probePositions.Length == 0)
            {
                if (!RunSkyVisibilityXRenderPath(config, out var skyVisibilityResult))
                {
                    result.error = skyVisibilityResult.error;
                    result.report = BuildTimeSliceReport(config, result);
                    return false;
                }

                probePositions = ResolveEffectiveProbePositions(config);
            }

            if (probePositions.Length == 0)
            {
                result.error = "TimeSlice Lite requires baked probe positions.";
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            result.enabled = config.useTimeSliceData;
            result.timeSlice = config.timeSliceType;
            result.probeCount = probePositions.Length;
            if (!config.useTimeSliceData)
            {
                config.CaptureTimeSliceSH(System.Array.Empty<BurtXGIProbeBakedSphericalHarmonicsL2>());
                EditorUtility.SetDirty(config);
                result.success = true;
                result.backend = "Disabled";
                result.report = BuildTimeSliceReport(config, result);
                return true;
            }

            var scene = SceneManager.GetActiveScene();
            var geometry = CollectBakingVirtualOffsetGeometry(config, scene);
            var lights = CollectBakingTimeSliceLights(config, scene);
            var bakedMainLightIntensity = ResolveBakedTimeSliceMainLightIntensity(lights);
            var maxRayDistance = ResolveTimeSliceRayDistance(config, geometry, lights);
            var ambientColor = ResolveTimeSliceAmbientColor();
            var sh = new BurtXGIProbeBakedSphericalHarmonicsL2[probePositions.Length];
            var shadowedSamples = 0;
            Physics.SyncTransforms();
            for (var i = 0; i < sh.Length; i++)
            {
                sh[i] = BakeTimeSliceProbeSH(
                    probePositions[i],
                    i,
                    config,
                    geometry,
                    lights,
                    ambientColor,
                    maxRayDistance,
                    ref shadowedSamples);
            }

            config.CaptureTimeSliceSH(sh, bakedMainLightIntensity);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.shCount = sh.Length;
            result.lightCount = lights.Count;
            result.shadowedSampleCount = shadowedSamples;
            result.batchCount = sh.Length > 0 ? 1 : 0;
            result.backend = "SceneLightCPU";
            result.report = BuildTimeSliceReport(config, result);
            return true;
        }

        internal bool RunTimeSliceXRenderPath(BurtXGIProbeBakingConfig config, out TimeSliceResult result)
        {
            result = default;
            if (config != null && config.useHardWareRayTracing && SystemInfo.supportsRayTracing &&
                TryRunTimeSliceRayTracing(config, out result))
            {
                return true;
            }

            if (RunTimeSliceLite(config, out result))
            {
                return true;
            }

            var liteError = result.error;
            if (config != null && SystemInfo.supportsComputeShaders &&
                TryRunTimeSliceCompute(config, out result))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(liteError) && string.IsNullOrEmpty(result.error))
            {
                result.error = liteError;
                result.report = BuildTimeSliceReport(config, result);
            }

            return false;
        }

        private static bool TryRunTimeSliceRayTracing(BurtXGIProbeBakingConfig config, out TimeSliceResult result)
        {
            result = default;
            result.backend = "RayTracingFallback";
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            if (!config.SupportsCurrentTimeSliceBake(out var timeSliceError))
            {
                result.error = timeSliceError;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            var probePositions = ResolveEffectiveProbePositions(config);
            if (probePositions.Length == 0)
            {
                if (!Instance.RunSkyVisibilityXRenderPath(config, out var skyVisibilityResult))
                {
                    result.error = skyVisibilityResult.error;
                    result.report = BuildTimeSliceReport(config, result);
                    return false;
                }

                probePositions = ResolveEffectiveProbePositions(config);
            }

            if (probePositions.Length == 0)
            {
                result.error = "TimeSlice ray tracing requires baked probe positions.";
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            result.enabled = config.useTimeSliceData;
            result.timeSlice = config.timeSliceType;
            result.probeCount = probePositions.Length;
            if (!config.useTimeSliceData)
            {
                config.CaptureTimeSliceSH(System.Array.Empty<BurtXGIProbeBakedSphericalHarmonicsL2>());
                EditorUtility.SetDirty(config);
                result.success = true;
                result.backend = "Disabled";
                result.report = BuildTimeSliceReport(config, result);
                return true;
            }

            var shader = Resources.Load<RayTracingShader>(XGIProbeBakingRayTracingResourcePath);
            if (shader == null)
            {
                result.error = "Missing ray tracing shader resource: " + XGIProbeBakingRayTracingResourcePath;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            if (!TryResolveBakingRayTracingAccelerationStructure(out var rayTracingCamera, out var accelerationStructure))
            {
                result.error = "TimeSlice ray tracing requires a valid BurtGI ray tracing acceleration structure.";
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            var scene = SceneManager.GetActiveScene();
            var geometry = CollectBakingVirtualOffsetGeometry(config, scene);
            var lights = CollectBakingTimeSliceLights(config, scene);
            var bakedMainLightIntensity = ResolveBakedTimeSliceMainLightIntensity(lights);
            var ambientColor = ResolveTimeSliceAmbientColor();
            var lightData = BuildTimeSliceRayTracingLightData(lights);
            var skyVisibility = new float[probePositions.Length];
            for (var i = 0; i < skyVisibility.Length; i++)
            {
                skyVisibility[i] = ResolveBakedSkyVisibility(config, i);
            }

            ComputeBuffer lightBuffer = null;
            var sh = new BurtXGIProbeBakedSphericalHarmonicsL2[probePositions.Length];
            var batches = BuildBakingProbeBatches(config, probePositions, XGITimeSliceMaxProbeCountPerBatch);
            var batchCount = batches.Count;
            try
            {
                lightBuffer = new ComputeBuffer(Mathf.Max(1, lightData.Length), sizeof(float) * 16);
                lightBuffer.SetData(lightData.Length > 0 ? lightData : new[] { default(XGITimeSliceLightData) });

                var sampleCount = Mathf.Max(1, config.timeSliceBakingSamples);
                var maxBounces = Mathf.Max(1, config.timeSliceBakingBounces);
                var sampleCountPerStep = ResolveTimeSliceSampleCountPerStep(config);
                for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
                {
                    var batch = batches[batchIndex];
                    var batchOffset = batch.probeStartIndex;
                    var batchSize = batch.probeCount;
                    var batchMaxRayDistance = ResolveTimeSliceRayDistance(config, geometry, lights, batch.bounds, batch.hasBounds);
                    var batchProbePositions = new Vector3[batchSize];
                    var batchSkyVisibility = new float[batchSize];
                    var batchRawSH = new float[batchSize * 27];
                    System.Array.Copy(probePositions, batchOffset, batchProbePositions, 0, batchSize);
                    System.Array.Copy(skyVisibility, batchOffset, batchSkyVisibility, 0, batchSize);
                    ComputeBuffer outputBuffer = null;
                    ComputeBuffer probePositionBuffer = null;
                    ComputeBuffer skyVisibilityBuffer = null;
                    try
                    {
                        outputBuffer = new ComputeBuffer(batchRawSH.Length, sizeof(float));
                        probePositionBuffer = new ComputeBuffer(batchSize, sizeof(float) * 3);
                        skyVisibilityBuffer = new ComputeBuffer(batchSize, sizeof(float));
                        outputBuffer.SetData(batchRawSH);
                        probePositionBuffer.SetData(batchProbePositions);
                        skyVisibilityBuffer.SetData(batchSkyVisibility);

                        var dispatchWidth = Mathf.CeilToInt(Mathf.Sqrt(batchSize));
                        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex += sampleCountPerStep)
                        {
                            var sampleCountThisDispatch = Mathf.Min(sampleCountPerStep, sampleCount - sampleIndex);
                            var cmd = CommandBufferPool.Get("Burt XGI Probe Baking TimeSlice RayTracing");
                            try
                            {
                                cmd.SetRayTracingShaderPass(shader, XGIProbeBakingRayTracingPassName);
                                cmd.SetRayTracingAccelerationStructure(shader, XGIProbeBakingAccelerationStructureId, accelerationStructure);
                                cmd.SetGlobalBuffer(XGITimeSliceOutputId, outputBuffer);
                                cmd.SetGlobalBuffer(XGITimeSliceProbePositionsId, probePositionBuffer);
                                cmd.SetGlobalBuffer(XGITimeSliceSkyVisibilityId, skyVisibilityBuffer);
                                cmd.SetGlobalBuffer(XGITimeSliceLightsId, lightBuffer);
                                cmd.SetRayTracingVectorParam(shader, XGITimeSliceParamsId, new Vector4(
                                    batchSize,
                                    lightData.Length,
                                    dispatchWidth,
                                    batchMaxRayDistance));
                                cmd.SetRayTracingVectorParam(shader, XGITimeSliceParams1Id, new Vector4(
                                    ambientColor.x,
                                    ambientColor.y,
                                    ambientColor.z,
                                    config.timeSliceOffsetRay));
                                cmd.SetRayTracingVectorParam(shader, XGITimeSliceParams2Id, new Vector4(
                                    sampleIndex,
                                    sampleCount,
                                    maxBounces,
                                    config.timeSliceRayCullBackFace ? 1f : 0f));
                                cmd.SetRayTracingVectorParam(shader, XGITimeSliceParams3Id, new Vector4(
                                    sampleCountThisDispatch,
                                    batchOffset,
                                    batchIndex,
                                    batchCount));
                                cmd.DispatchRays(shader, XGITimeSliceRayGenName, (uint)dispatchWidth, (uint)dispatchWidth, 1u, rayTracingCamera);
                                Graphics.ExecuteCommandBuffer(cmd);
                            }
                            finally
                            {
                                CommandBufferPool.Release(cmd);
                            }
                        }

                        outputBuffer.GetData(batchRawSH);
                    }
                    finally
                    {
                        outputBuffer?.Release();
                        probePositionBuffer?.Release();
                        skyVisibilityBuffer?.Release();
                    }

                    for (var i = 0; i < batchSize; i++)
                    {
                        sh[batchOffset + i] = PackRawTimeSliceSH(batchRawSH, i);
                    }
                }
            }
            catch (System.Exception exception)
            {
                result.error = "TimeSlice ray tracing failed: " + exception.Message;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }
            finally
            {
                lightBuffer?.Release();
            }

            config.CaptureTimeSliceSH(sh, bakedMainLightIntensity);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.shCount = sh.Length;
            result.lightCount = lights.Count;
            result.batchCount = batchCount;
            result.report = BuildTimeSliceReport(config, result);
            return true;
        }

        private static bool TryRunTimeSliceCompute(BurtXGIProbeBakingConfig config, out TimeSliceResult result)
        {
            result = default;
            result.backend = "GpuComputeFallback";
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            if (!config.SupportsCurrentTimeSliceBake(out var timeSliceError))
            {
                result.error = timeSliceError;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            var probePositions = ResolveEffectiveProbePositions(config);
            if (probePositions.Length == 0)
            {
                if (!Instance.RunSkyVisibilityXRenderPath(config, out var skyVisibilityResult))
                {
                    result.error = skyVisibilityResult.error;
                    result.report = BuildTimeSliceReport(config, result);
                    return false;
                }

                probePositions = ResolveEffectiveProbePositions(config);
            }

            if (probePositions.Length == 0)
            {
                result.error = "TimeSlice GPU compute requires baked probe positions.";
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            result.enabled = config.useTimeSliceData;
            result.timeSlice = config.timeSliceType;
            result.probeCount = probePositions.Length;
            if (!config.useTimeSliceData)
            {
                config.CaptureTimeSliceSH(System.Array.Empty<BurtXGIProbeBakedSphericalHarmonicsL2>());
                EditorUtility.SetDirty(config);
                result.success = true;
                result.backend = "Disabled";
                result.report = BuildTimeSliceReport(config, result);
                return true;
            }

            var shader = Resources.Load<ComputeShader>(XGIProbeBakingComputeShaderResourcePath);
            if (shader == null)
            {
                result.error = "Missing compute shader resource: " + XGIProbeBakingComputeShaderResourcePath;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            if (!shader.HasKernel(XGITimeSliceComputeKernelName))
            {
                result.error = "Missing compute kernel: " + XGIProbeBakingComputeShaderResourcePath + "." + XGITimeSliceComputeKernelName;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }

            var kernel = shader.FindKernel(XGITimeSliceComputeKernelName);
            var probeCount = probePositions.Length;
            var sh = new BurtXGIProbeBakedSphericalHarmonicsL2[probeCount];
            var parameters = new[] { Vector4.zero };
            var scene = SceneManager.GetActiveScene();
            var lights = CollectBakingTimeSliceLights(config, scene);
            var bakedMainLightIntensity = ResolveBakedTimeSliceMainLightIntensity(lights);
            var lightData = BuildTimeSliceRayTracingLightData(lights);
            var ambientColor = ResolveTimeSliceAmbientColor();
            var skyVisibility = new float[probeCount];
            for (var i = 0; i < skyVisibility.Length; i++)
            {
                skyVisibility[i] = ResolveBakedSkyVisibility(config, i);
            }

            ComputeBuffer parametersBuffer = null;
            ComputeBuffer lightBuffer = null;
            var batchCount = 0;
            try
            {
                parametersBuffer = new ComputeBuffer(1, sizeof(float) * 4);
                lightBuffer = new ComputeBuffer(Mathf.Max(1, lightData.Length), sizeof(float) * 16);
                parametersBuffer.SetData(parameters);
                lightBuffer.SetData(lightData.Length > 0 ? lightData : new[] { default(XGITimeSliceLightData) });

                shader.SetBuffer(kernel, "_XGITimeSliceGen_ParamtersBuffer", parametersBuffer);
                shader.SetBuffer(kernel, "_XGITimeSliceGen_Lights", lightBuffer);
                shader.SetFloat("_XGITimeSliceGen_OffsetRay", config.timeSliceOffsetRay);
                shader.SetFloat("_XGITimeSliceGen_MaxBounces", config.timeSliceBakingBounces);

                var sampleCount = Mathf.Max(1, config.timeSliceBakingSamples);
                shader.SetFloat("_XGITimeSliceGen_SampleCount", sampleCount);
                var batches = BuildBakingProbeBatches(config, probePositions, XGITimeSliceMaxProbeCountPerBatch);
                batchCount = batches.Count;
                for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
                {
                    var batch = batches[batchIndex];
                    var batchOffset = batch.probeStartIndex;
                    var batchSize = batch.probeCount;
                    var batchRawSH = new float[batchSize * 27];
                    var batchProbePositions = new Vector3[batchSize];
                    var batchSkyVisibility = new float[batchSize];
                    System.Array.Copy(probePositions, batchOffset, batchProbePositions, 0, batchSize);
                    System.Array.Copy(skyVisibility, batchOffset, batchSkyVisibility, 0, batchSize);
                    ComputeBuffer outputBuffer = null;
                    ComputeBuffer probePositionBuffer = null;
                    ComputeBuffer skyVisibilityBuffer = null;
                    try
                    {
                        outputBuffer = new ComputeBuffer(batchRawSH.Length, sizeof(float));
                        probePositionBuffer = new ComputeBuffer(batchSize, sizeof(float) * 3);
                        skyVisibilityBuffer = new ComputeBuffer(batchSize, sizeof(float));
                        outputBuffer.SetData(batchRawSH);
                        probePositionBuffer.SetData(batchProbePositions);
                        skyVisibilityBuffer.SetData(batchSkyVisibility);

                        shader.SetBuffer(kernel, "_RW_XGITimeSliceGen_Output", outputBuffer);
                        shader.SetBuffer(kernel, "_XGITimeSliceGen_ProbePositions", probePositionBuffer);
                        shader.SetBuffer(kernel, "_XGITimeSliceGen_SkyVisibility", skyVisibilityBuffer);

                        var dispatchWidth = Mathf.CeilToInt(Mathf.Sqrt(batchSize));
                        var groupsX = Mathf.Max(1, Mathf.CeilToInt(dispatchWidth / (float)XGIProbeBakingThreadGroupSize));
                        var groupsY = groupsX;
                        shader.SetVector("_XGITimeSliceGen_Params", new Vector4(batchSize, lightData.Length, groupsX, 0f));
                        shader.SetVector("_XGITimeSliceGen_Params1", new Vector4(
                            batchIndex,
                            batchOffset,
                            config.timeSliceRayCullBackFace ? 1f : 0f,
                            0f));
                        shader.SetVector("_XGITimeSliceGen_Params2", new Vector4(ambientColor.x, ambientColor.y, ambientColor.z, 0f));
                        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
                        {
                            shader.SetFloat("_XGITimeSliceGen_SampleIndex", sampleIndex);
                            shader.Dispatch(kernel, groupsX, groupsY, 1);
                        }

                        outputBuffer.GetData(batchRawSH);
                    }
                    finally
                    {
                        outputBuffer?.Release();
                        probePositionBuffer?.Release();
                        skyVisibilityBuffer?.Release();
                    }

                    for (var i = 0; i < batchSize; i++)
                    {
                        sh[batchOffset + i] = PackRawTimeSliceSH(batchRawSH, i);
                    }
                }
            }
            catch (System.Exception exception)
            {
                result.error = "TimeSlice GPU compute failed: " + exception.Message;
                result.report = BuildTimeSliceReport(config, result);
                return false;
            }
            finally
            {
                parametersBuffer?.Release();
                lightBuffer?.Release();
            }

            config.CaptureTimeSliceSH(sh, bakedMainLightIntensity);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.shCount = sh.Length;
            result.lightCount = lights.Count;
            result.batchCount = batchCount;
            result.report = BuildTimeSliceReport(config, result);
            return true;
        }

        internal bool RunFinalizeCellsLite(BurtXGIProbeBakingConfig config, out FinalizeCellsResult result)
        {
            result = default;
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildFinalizeCellsReport(config, result);
                return false;
            }

            var cells = config.bakedPlacedCells ?? System.Array.Empty<BurtXGIProbePlacedCell>();
            if (cells.Length == 0)
            {
                if (!RunTimeSliceXRenderPath(config, out var timeSliceResult))
                {
                    result.error = timeSliceResult.error;
                    result.report = BuildFinalizeCellsReport(config, result);
                    return false;
                }

                cells = config.bakedPlacedCells ?? System.Array.Empty<BurtXGIProbePlacedCell>();
            }

            if (cells.Length == 0)
            {
                result.error = "FinalizeCells Lite requires baked placement cells.";
                result.report = BuildFinalizeCellsReport(config, result);
                return false;
            }

            if (config.virtualOffset && !HasProbeRange(config.bakedVirtualOffsets, config.bakedProbeCount))
            {
                result.error = "FinalizeCells Lite requires virtual offset data for every baked probe.";
                result.report = BuildFinalizeCellsReport(config, result);
                return false;
            }

            if (config.skyVisibility && !HasProbeRange(config.bakedSkyVisibilityL0L1, config.bakedProbeCount))
            {
                result.error = "FinalizeCells Lite requires sky visibility data for every baked probe.";
                result.report = BuildFinalizeCellsReport(config, result);
                return false;
            }

            if (config.skyVisibility && config.skyVisibilityShadingDirection &&
                !HasProbeRange(config.bakedSkyShadingDirectionIndices, config.bakedProbeCount))
            {
                result.error = "FinalizeCells Lite requires sky shading direction data for every baked probe.";
                result.report = BuildFinalizeCellsReport(config, result);
                return false;
            }

            if (config.useTimeSliceData && !HasProbeRange(config.bakedTimeSliceSH, config.bakedProbeCount))
            {
                result.error = "FinalizeCells Lite requires time-slice SH data for every baked probe.";
                result.report = BuildFinalizeCellsReport(config, result);
                return false;
            }

            var finalizedCells = new BurtXGIProbeFinalizedCell[cells.Length];
            var chunkCount = 0;
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                var minSubdivision = ResolveMinSubdivision(config, cell);
                var shChunkCount = Mathf.Max(1, Mathf.CeilToInt(cell.brickCount / (float)Mathf.Max(1, config.chunkSizeInBricks)));
                chunkCount += shChunkCount;
                finalizedCells[i] = new BurtXGIProbeFinalizedCell
                {
                    cellIndex = cell.index,
                    position = cell.position,
                    bounds = cell.bounds,
                    minSubdivisionLevel = minSubdivision,
                    shChunkCount = shChunkCount,
                    brickStartIndex = cell.brickStartIndex,
                    brickCount = cell.brickCount,
                    probeStartIndex = cell.probeStartIndex,
                    probeCount = cell.probeCount,
                    sceneGuids = CopySceneGuids(cell.sceneGuids),
                    hasVirtualOffset = config.virtualOffset && HasProbeRange(config.bakedVirtualOffsets, cell.probeStartIndex, cell.probeCount),
                    hasSkyVisibility = config.skyVisibility && HasProbeRange(config.bakedSkyVisibilityL0L1, cell.probeStartIndex, cell.probeCount),
                    hasSkyShadingDirection = config.skyVisibility && config.skyVisibilityShadingDirection &&
                        HasProbeRange(config.bakedSkyShadingDirectionIndices, cell.probeStartIndex, cell.probeCount),
                    hasTimeSliceSH = config.useTimeSliceData && HasProbeRange(config.bakedTimeSliceSH, cell.probeStartIndex, cell.probeCount)
                };
            }

            config.CaptureFinalizedCells(finalizedCells);
            EditorUtility.SetDirty(config);
            result.success = true;
            result.cellCount = cells.Length;
            result.finalizedCellCount = finalizedCells.Length;
            result.chunkCount = chunkCount;
            result.report = BuildFinalizeCellsReport(config, result);
            return true;
        }

        internal bool RunSerializationLite(BurtXGIProbeBakingConfig config, out SerializationResult result)
        {
            result = default;
            if (config == null)
            {
                result.error = "Missing BurtXGIProbeBakingConfig.";
                result.report = BuildSerializationReport(config, result);
                return false;
            }

            var finalizedCells = config.bakedFinalizedCells ?? System.Array.Empty<BurtXGIProbeFinalizedCell>();
            if (finalizedCells.Length == 0)
            {
                if (!RunFinalizeCellsLite(config, out var finalizeResult))
                {
                    result.error = finalizeResult.error;
                    result.report = BuildSerializationReport(config, result);
                    return false;
                }

                finalizedCells = config.bakedFinalizedCells ?? System.Array.Empty<BurtXGIProbeFinalizedCell>();
            }

            if (finalizedCells.Length == 0)
            {
                result.error = "Serialization Lite requires finalized cells.";
                result.report = BuildSerializationReport(config, result);
                return false;
            }

            if (config.useTimeSliceData && !HasProbeRange(config.bakedTimeSliceSH, config.bakedProbeCount))
            {
                result.error = "Serialization Lite requires time-slice SH data for every baked probe when Time Slice Data is enabled.";
                result.report = BuildSerializationReport(config, result);
                return false;
            }

            var asset = CreateOrLoadBakedDataAsset(config);
            if (asset == null)
            {
                result.error = "Failed to create or load BurtXGIProbeBakedDataAsset.";
                result.report = BuildSerializationReport(config, result);
                return false;
            }

            PopulateBakedDataAsset(
                config,
                asset,
                finalizedCells,
                BuildSerializedChunk,
                out var cells,
                out var nextPhysicalChunkIndex,
                out var pageTableEntryCount,
                out var indirectionEntryCount);
            var sceneCellIndices = new List<int>(cells.Length);
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                if (cells[cellIndex] != null)
                {
                    sceneCellIndices.Add(cells[cellIndex].cellIndex);
                }
            }

            SyncPerSceneCellLists(config, finalizedCells, cells, sceneCellIndices);
            asset.perSceneCellLists = CopyPerSceneCellLists(config.perSceneCellLists);
            asset.chunkCount = nextPhysicalChunkIndex;
            asset.physicalPoolChunkDimensions = ResolvePhysicalPoolChunkDimensions(asset.chunkCount);
            asset.pageTableEntryCount = pageTableEntryCount;
            asset.indirectionEntryCount = indirectionEntryCount;
            config.CaptureSerializedData(asset);
            RefreshSerializedAssetSceneBindings(config, asset);
            EditorUtility.SetDirty(asset);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            result.success = true;
            result.cellCount = asset.cellCount;
            result.chunkCount = asset.chunkCount;
            result.pageTableEntryCount = asset.pageTableEntryCount;
            result.indirectionEntryCount = asset.indirectionEntryCount;
            result.asset = asset;
            result.report = BuildSerializationReport(config, result);
            return true;
        }

        internal static void PopulateBakedDataAsset(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedDataAsset asset,
            BurtXGIProbeFinalizedCell[] finalizedCells,
            System.Func<BurtXGIProbeBakingConfig, BurtXGIProbeFinalizedCell, int, int, BurtXGIProbeBakedChunk> buildChunk,
            out BurtXGIProbeBakedCellData[] cells,
            out int chunkCount,
            out int pageTableEntryCount,
            out int indirectionEntryCount)
        {
            finalizedCells ??= System.Array.Empty<BurtXGIProbeFinalizedCell>();
            asset.sourceConfig = config;
            asset.globalBounds = config.globalBounds;
            asset.minCellPosition = config.minCellPosition;
            asset.maxCellPosition = config.maxCellPosition;
            asset.probeOffset = config.BakedProbeOffset;
            asset.bakedProbeOffset = config.BakedProbeOffset;
            asset.bakedMinDistanceBetweenProbes = config.BakedMinDistanceBetweenProbes;
            asset.bakedSimplificationLevels = config.BakedSimplificationLevels;
            asset.bakedStreamerType = config.BakedStreamerType;
            asset.cellSizeInBricks = config.BakedCellSizeInBricks;
            asset.cellSizeInMeters = config.BakedCellSizeInMeters;
            asset.minBrickSize = config.BakedMinBrickSize;
            asset.chunkSizeInBricks = config.chunkSizeInBricks;
            asset.chunkProbeCount = config.ChunkProbeCount;
            asset.cellCount = finalizedCells.Length;
            asset.brickCount = config.bakedBrickCount;
            asset.probeCount = config.bakedProbeCount;
            var totalPhysicalChunkCount = ResolveTotalPhysicalChunkCount(finalizedCells);
            asset.entriesPerCellDimension = GetEntriesPerCellDimension(config);
            asset.virtualIndirectionDimensions = GetVirtualIndirectionDimensions(config, asset.entriesPerCellDimension);
            asset.virtualMinEntryPosition = config.minCellPosition * asset.entriesPerCellDimension;
            asset.physicalPoolChunkDimensions = ResolvePhysicalPoolChunkDimensions(totalPhysicalChunkCount);
            asset.hasVirtualOffset = config.virtualOffset && config.bakedVirtualOffsetCount >= config.bakedProbeCount;
            asset.hasValidity = finalizedCells.Length > 0;
            asset.hasSkyVisibility = config.bakedSkyVisibility;
            asset.hasSkyShadingDirection = config.bakedSkyShadingDirection;
            asset.hasTimeSliceSH = config.bakedUseTimeSlice;
            asset.timeSliceType = config.bakedTimeSliceType;
            asset.timeSliceMainLightIntensity = config.bakedTimeSliceMainLightIntensity;
            asset.CaptureRuntimeSettings(config);

            cells = new BurtXGIProbeBakedCellData[finalizedCells.Length];
            chunkCount = 0;
            pageTableEntryCount = 0;
            indirectionEntryCount = Mathf.Max(1, asset.virtualIndirectionDimensions.x * asset.virtualIndirectionDimensions.y * asset.virtualIndirectionDimensions.z);
            for (var i = 0; i < finalizedCells.Length; i++)
            {
                var sourceCell = finalizedCells[i];
                var chunks = new BurtXGIProbeBakedChunk[Mathf.Max(1, sourceCell.shChunkCount)];
                for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
                {
                    var physicalChunkIndex = chunkCount++;
                    chunks[chunkIndex] = buildChunk != null
                        ? buildChunk(config, sourceCell, chunkIndex, physicalChunkIndex)
                        : new BurtXGIProbeBakedChunk { physicalChunkIndex = physicalChunkIndex };
                }

                BuildXRenderVirtualEntries(
                    config,
                    asset,
                    sourceCell,
                    chunks,
                    pageTableEntryCount / XRenderPageTableEntriesPerChunk,
                    out var entryBlockMin,
                    out var entryBlockDimensions,
                    out var pageTableEntries,
                    out var indirectionEntries);

                cells[i] = new BurtXGIProbeBakedCellData
                {
                    cellIndex = sourceCell.cellIndex,
                    position = sourceCell.position,
                    bounds = sourceCell.bounds,
                    minSubdivisionLevel = sourceCell.minSubdivisionLevel,
                    shChunkCount = sourceCell.shChunkCount,
                    probeStartIndex = sourceCell.probeStartIndex,
                    probeCount = sourceCell.probeCount,
                    sceneGuids = CopySceneGuids(sourceCell.sceneGuids),
                    pageTableDestinationIndex = pageTableEntryCount,
                    indirectionDestinationIndex = GetFlatEntryIndex(entryBlockMin, asset.virtualIndirectionDimensions),
                    entryBlockMin = entryBlockMin,
                    entryBlockDimensions = entryBlockDimensions,
                    pageTableEntries = pageTableEntries,
                    indirectionEntries = indirectionEntries,
                    chunks = chunks
                };

                pageTableEntryCount += pageTableEntries.Length;
            }

            asset.cells = cells;
        }

        private static List<BurtXGIProbePerSceneCellList> CopyPerSceneCellLists(List<BurtXGIProbePerSceneCellList> source)
        {
            var result = new List<BurtXGIProbePerSceneCellList>();
            if (source == null)
            {
                return result;
            }

            for (var index = 0; index < source.Count; index++)
            {
                var entry = source[index];
                if (entry == null)
                {
                    continue;
                }

                result.Add(new BurtXGIProbePerSceneCellList
                {
                    sceneGuid = entry.sceneGuid,
                    cellIndices = entry.cellIndices != null ? new List<int>(entry.cellIndices) : new List<int>()
                });
            }

            return result;
        }

        private static void SyncPerSceneCellLists(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeFinalizedCell[] finalizedCells,
            BurtXGIProbeBakedCellData[] cells,
            List<int> fallbackCellIndices)
        {
            if (config == null)
            {
                return;
            }

            var syncedLists = BuildPerSceneCellLists(config.sceneBakeData, finalizedCells);
            if (syncedLists.Count == 0)
            {
                syncedLists = BuildPerSceneCellLists(config.sceneBakeData, cells);
            }

            if (syncedLists.Count > 0)
            {
                config.perSceneCellLists = syncedLists;
                return;
            }

            config.SetSceneCellIndices(SceneManager.GetActiveScene(), fallbackCellIndices);
        }

        private static List<BurtXGIProbePerSceneCellList> BuildPerSceneCellLists(
            List<BurtXGIProbeSceneBakeData> sceneBakeData,
            BurtXGIProbeFinalizedCell[] cells)
        {
            var result = new List<BurtXGIProbePerSceneCellList>();
            if (sceneBakeData == null || sceneBakeData.Count == 0 || cells == null || cells.Length == 0)
            {
                return result;
            }

            var validSceneGuids = BuildValidSceneGuidSet(sceneBakeData);
            if (validSceneGuids.Count == 0)
            {
                return result;
            }

            var cellLists = new Dictionary<string, List<int>>(System.StringComparer.OrdinalIgnoreCase);
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var cell = cells[cellIndex];
                if (cell.sceneGuids == null || cell.sceneGuids.Length == 0)
                {
                    continue;
                }

                for (var sceneIndex = 0; sceneIndex < cell.sceneGuids.Length; sceneIndex++)
                {
                    var sceneGuid = cell.sceneGuids[sceneIndex];
                    if (string.IsNullOrEmpty(sceneGuid) || !validSceneGuids.Contains(sceneGuid))
                    {
                        continue;
                    }

                    if (!cellLists.TryGetValue(sceneGuid, out var indices))
                    {
                        indices = new List<int>();
                        cellLists.Add(sceneGuid, indices);
                    }

                    indices.Add(cell.cellIndex);
                }
            }

            foreach (var pair in cellLists)
            {
                result.Add(new BurtXGIProbePerSceneCellList
                {
                    sceneGuid = pair.Key,
                    cellIndices = pair.Value
                });
            }

            return result;
        }

        private static List<BurtXGIProbePerSceneCellList> BuildPerSceneCellLists(
            List<BurtXGIProbeSceneBakeData> sceneBakeData,
            BurtXGIProbeBakedCellData[] cells)
        {
            var result = new List<BurtXGIProbePerSceneCellList>();
            if (sceneBakeData == null || sceneBakeData.Count == 0 || cells == null || cells.Length == 0)
            {
                return result;
            }

            for (var sceneIndex = 0; sceneIndex < sceneBakeData.Count; sceneIndex++)
            {
                var sceneData = sceneBakeData[sceneIndex];
                if (sceneData == null || !sceneData.bakeScene || string.IsNullOrEmpty(sceneData.sceneGuid) ||
                    !sceneData.hasProbeVolume || !HasValidBounds(sceneData.bounds))
                {
                    continue;
                }

                var cellIndices = new List<int>();
                for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
                {
                    var cell = cells[cellIndex];
                    if (cell != null && sceneData.bounds.Intersects(cell.bounds))
                    {
                        cellIndices.Add(cell.cellIndex);
                    }
                }

                if (cellIndices.Count == 0)
                {
                    continue;
                }

                result.Add(new BurtXGIProbePerSceneCellList
                {
                    sceneGuid = sceneData.sceneGuid,
                    cellIndices = cellIndices
                });
            }

            return result;
        }

        private static HashSet<string> BuildValidSceneGuidSet(List<BurtXGIProbeSceneBakeData> sceneBakeData)
        {
            var result = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (sceneBakeData == null)
            {
                return result;
            }

            for (var sceneIndex = 0; sceneIndex < sceneBakeData.Count; sceneIndex++)
            {
                var sceneData = sceneBakeData[sceneIndex];
                if (sceneData == null || !sceneData.bakeScene || string.IsNullOrEmpty(sceneData.sceneGuid) ||
                    !sceneData.hasProbeVolume)
                {
                    continue;
                }

                result.Add(sceneData.sceneGuid);
            }

            return result;
        }

        private static bool HasValidBounds(Bounds bounds)
        {
            var size = bounds.size;
            return size.x > 0.0001f && size.y > 0.0001f && size.z > 0.0001f;
        }

        private static void BuildXRenderVirtualEntries(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedDataAsset asset,
            BurtXGIProbeFinalizedCell cell,
            BurtXGIProbeBakedChunk[] chunks,
            int firstPageTableChunkIndex,
            out Vector3Int entryBlockMin,
            out Vector3Int entryBlockDimensions,
            out uint[] pageTableEntries,
            out Vector3Int[] indirectionEntries)
        {
            var entriesPerCell = Mathf.Max(1, asset.entriesPerCellDimension);
            entryBlockDimensions = new Vector3Int(entriesPerCell, entriesPerCell, entriesPerCell);
            entryBlockMin = (cell.position - asset.minCellPosition) * entriesPerCell;
            var entryCount = entriesPerCell * entriesPerCell * entriesPerCell;
            pageTableEntries = new uint[entryCount * XRenderPageTableEntriesPerChunk];
            indirectionEntries = new Vector3Int[entryCount];
            Fill(pageTableEntries, uint.MaxValue);
            Fill(indirectionEntries, new Vector3Int(-1, 0, 0));

            if (chunks == null || chunks.Length == 0)
            {
                return;
            }

            var bricks = config.bakedPlacedBricks ?? System.Array.Empty<BurtXGIProbePlacedBrick>();
            var entrySubdivLevel = Mathf.Min(XRenderEntryMaxSubdivisionLevel, Mathf.Max(0, config.BakedSimplificationLevels));
            var entrySizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(entrySubdivLevel);
            var firstBrickIndex = Mathf.Max(0, cell.brickStartIndex);
            var lastBrickIndex = Mathf.Min(bricks.Length, firstBrickIndex + Mathf.Max(0, cell.brickCount));
            for (var localEntryIndex = 0; localEntryIndex < indirectionEntries.Length; localEntryIndex++)
            {
                var localEntryCoord = DecodeFlatEntryIndex(localEntryIndex, entryBlockDimensions);
                var entryMinBrick = cell.position * config.BakedCellSizeInBricks + localEntryCoord * entrySizeInBricks;
                var entryMaxBrick = entryMinBrick + Vector3Int.one * entrySizeInBricks;
                var minSubdiv = 8;
                for (var brickIndex = firstBrickIndex; brickIndex < lastBrickIndex; brickIndex++)
                {
                    var brick = bricks[brickIndex];
                    var brickSizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(brick.subdivisionLevel);
                    var brickMax = brick.position + Vector3Int.one * brickSizeInBricks;
                    if (!BrickBoundsOverlap(brick.position, brickMax, entryMinBrick, entryMaxBrick))
                    {
                        continue;
                    }

                    minSubdiv = Mathf.Min(minSubdiv, brick.subdivisionLevel);
                }

                if (minSubdiv > 7)
                {
                    continue;
                }

                var minBrickSizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(minSubdiv);
                var validDimensions = Vector3Int.one * Mathf.Max(1, entrySizeInBricks / Mathf.Max(1, minBrickSizeInBricks));
                var pageTableChunkIndex = firstPageTableChunkIndex + localEntryIndex;
                indirectionEntries[localEntryIndex] = new Vector3Int(
                    unchecked((int)PackIndirectionMetadataX(pageTableChunkIndex, minSubdiv)),
                    unchecked((int)Pack10BitVector(Vector3Int.zero)),
                    unchecked((int)Pack10BitVector(validDimensions)));

                for (var brickIndex = firstBrickIndex; brickIndex < lastBrickIndex; brickIndex++)
                {
                    var brick = bricks[brickIndex];
                    if (brick.subdivisionLevel < minSubdiv)
                    {
                        continue;
                    }

                    var brickSizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(brick.subdivisionLevel);
                    var brickMax = brick.position + Vector3Int.one * brickSizeInBricks;
                    if (!BrickBoundsOverlap(brick.position, brickMax, entryMinBrick, entryMaxBrick))
                    {
                        continue;
                    }

                    var localBrickMin = Vector3Int.Max(brick.position, entryMinBrick) - entryMinBrick;
                    var localBrickMax = Vector3Int.Min(brickMax, entryMaxBrick) - entryMinBrick;
                    MarkBrickInPageTable(
                        pageTableEntries,
                        localEntryIndex * XRenderPageTableEntriesPerChunk,
                        localBrickMin,
                        localBrickMax,
                        minBrickSizeInBricks,
                        validDimensions,
                        PackPhysicalLocationForBrick(asset.physicalPoolChunkDimensions, chunks, brickIndex - firstBrickIndex, brick.subdivisionLevel));
                }
            }
        }

        private static int GetEntriesPerCellDimension(BurtXGIProbeBakingConfig config)
        {
            var entrySubdivLevel = Mathf.Min(XRenderEntryMaxSubdivisionLevel, Mathf.Max(0, config.BakedSimplificationLevels));
            var entrySizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(entrySubdivLevel);
            return Mathf.Max(1, config.BakedCellSizeInBricks / Mathf.Max(1, entrySizeInBricks));
        }

        private static Vector3Int GetVirtualIndirectionDimensions(BurtXGIProbeBakingConfig config, int entriesPerCellDimension)
        {
            var cellCount = config.maxCellPosition - config.minCellPosition + Vector3Int.one;
            return new Vector3Int(
                Mathf.Max(1, cellCount.x * entriesPerCellDimension),
                Mathf.Max(1, cellCount.y * entriesPerCellDimension),
                Mathf.Max(1, cellCount.z * entriesPerCellDimension));
        }

        private static int ResolveTotalPhysicalChunkCount(BurtXGIProbeFinalizedCell[] cells)
        {
            var chunkCount = 0;
            if (cells == null)
            {
                return chunkCount;
            }

            for (var i = 0; i < cells.Length; i++)
            {
                chunkCount += Mathf.Max(1, cells[i].shChunkCount);
            }

            return chunkCount;
        }

        private static Vector3Int ResolvePhysicalPoolChunkDimensions(int chunkCount)
        {
            var safeChunkCount = Mathf.Max(1, chunkCount);
            var maxTextureSize = Mathf.Max(1, SystemInfo.maxTexture3DSize);
            var maxChunksX = Mathf.Max(1, maxTextureSize / BurtGIVirtualProbePhysicalPool.ChunkWidth);
            var maxChunksY = Mathf.Max(1, maxTextureSize / BurtGIVirtualProbePhysicalPool.ChunkHeight);
            var maxChunksZ = Mathf.Max(1, maxTextureSize / BurtGIVirtualProbePhysicalPool.ChunkDepth);

            var x = Mathf.Min(safeChunkCount, maxChunksX);
            var remaining = Mathf.CeilToInt(safeChunkCount / (float)x);
            var y = Mathf.Min(Mathf.Max(1, remaining), maxChunksY);
            var z = Mathf.CeilToInt(safeChunkCount / (float)(x * y));
            if (z > maxChunksZ)
            {
                z = maxChunksZ;
                y = Mathf.Min(maxChunksY, Mathf.CeilToInt(safeChunkCount / (float)(x * z)));
            }

            return new Vector3Int(Mathf.Max(1, x), Mathf.Max(1, y), Mathf.Max(1, z));
        }

        private static int GetFlatEntryIndex(Vector3Int entryIndex, Vector3Int dimensions)
        {
            return entryIndex.x + entryIndex.y * dimensions.x + entryIndex.z * dimensions.x * dimensions.y;
        }

        private static Vector3Int DecodeFlatEntryIndex(int flatIndex, Vector3Int dimensions)
        {
            var z = flatIndex / (dimensions.x * dimensions.y);
            flatIndex -= z * dimensions.x * dimensions.y;
            var y = flatIndex / dimensions.x;
            var x = flatIndex - y * dimensions.x;
            return new Vector3Int(x, y, z);
        }

        private static bool BrickBoundsOverlap(Vector3Int firstMin, Vector3Int firstMax, Vector3Int secondMin, Vector3Int secondMax)
        {
            return firstMax.x > secondMin.x && secondMax.x > firstMin.x &&
                firstMax.y > secondMin.y && secondMax.y > firstMin.y &&
                firstMax.z > secondMin.z && secondMax.z > firstMin.z;
        }

        private static void MarkBrickInPageTable(
            uint[] pageTableEntries,
            int pageTableStart,
            Vector3Int localBrickMin,
            Vector3Int localBrickMax,
            int minBrickSizeInBricks,
            Vector3Int validDimensions,
            uint physicalLocation)
        {
            if (pageTableEntries == null || minBrickSizeInBricks <= 0 ||
                validDimensions.x <= 0 || validDimensions.y <= 0 || validDimensions.z <= 0)
            {
                return;
            }

            var brickMin = new Vector3Int(
                Mathf.Clamp(localBrickMin.x / minBrickSizeInBricks, 0, validDimensions.x),
                Mathf.Clamp(localBrickMin.y / minBrickSizeInBricks, 0, validDimensions.y),
                Mathf.Clamp(localBrickMin.z / minBrickSizeInBricks, 0, validDimensions.z));
            var brickMax = new Vector3Int(
                Mathf.Clamp(Mathf.CeilToInt(localBrickMax.x / (float)minBrickSizeInBricks), 0, validDimensions.x),
                Mathf.Clamp(Mathf.CeilToInt(localBrickMax.y / (float)minBrickSizeInBricks), 0, validDimensions.y),
                Mathf.Clamp(Mathf.CeilToInt(localBrickMax.z / (float)minBrickSizeInBricks), 0, validDimensions.z));
            for (var x = brickMin.x; x < brickMax.x; x++)
            {
                for (var z = brickMin.z; z < brickMax.z; z++)
                {
                    for (var y = brickMin.y; y < brickMax.y; y++)
                    {
                        var pageTableIndex = pageTableStart + z * (validDimensions.x * validDimensions.y) + x * validDimensions.y + y;
                        if (pageTableIndex >= 0 && pageTableIndex < pageTableEntries.Length)
                        {
                            pageTableEntries[pageTableIndex] = physicalLocation;
                        }
                    }
                }
            }
        }

        private static uint PackIndirectionMetadataX(int firstPageTableChunkIndex, int minSubdivision)
        {
            return ((uint)firstPageTableChunkIndex & 0x1fffffffu) | (((uint)minSubdivision & 0x7u) << 29);
        }

        private static uint Pack10BitVector(Vector3Int value)
        {
            return ((uint)value.x & 0x3ffu) |
                (((uint)value.y & 0x3ffu) << 10) |
                (((uint)value.z & 0x3ffu) << 20);
        }

        private static uint PackPhysicalLocationForBrick(Vector3Int physicalPoolChunkDimensions, BurtXGIProbeBakedChunk[] chunks, int localBrickIndex, int subdivisionLevel)
        {
            var chunkIndex = localBrickIndex / BurtGIVirtualProbePhysicalPool.BricksPerChunk;
            if (chunks == null || chunkIndex < 0 || chunkIndex >= chunks.Length || chunks[chunkIndex] == null)
            {
                return uint.MaxValue;
            }

            return PackPhysicalPageTableLocation(
                physicalPoolChunkDimensions,
                chunks[chunkIndex].physicalChunkIndex,
                localBrickIndex % BurtGIVirtualProbePhysicalPool.BricksPerChunk,
                subdivisionLevel);
        }

        private static uint PackPhysicalPageTableLocation(Vector3Int physicalPoolChunkDimensions, int physicalChunkIndex, int brickIndexInChunk, int subdivisionLevel)
        {
            var physicalLocation = BurtGIVirtualProbePhysicalPool.GetChunkBrickPhysicalLocation(
                physicalChunkIndex,
                brickIndexInChunk,
                physicalPoolChunkDimensions);
            return ((uint)physicalLocation & 0x0fffffffu) | (((uint)subdivisionLevel & 0xfu) << 28);
        }

        private static void Fill<T>(T[] array, T value)
        {
            if (array == null)
            {
                return;
            }

            for (var i = 0; i < array.Length; i++)
            {
                array[i] = value;
            }
        }

        private static bool TryGetVolumeOBB(BurtGIProbeVolume volume, out BurtGIProbeVolumeBounds obb)
        {
            return BurtGIProbeVolumePositioning.TryGetVolumeBounds(volume, out obb);
        }

        private static void SyncLoadedSceneBakeData(
            BurtXGIProbeBakingConfig config,
            Scene primaryScene,
            Bounds primarySceneBounds)
        {
            if (config == null)
            {
                return;
            }

            var touchedPrimaryScene = false;
            for (var sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                var scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    continue;
                }

                if (!ShouldIncludeSceneInBakingSet(config, scene, primaryScene))
                {
                    continue;
                }

                var hasProbeVolume = TryCollectSceneVolumeBounds(scene, out var sceneBounds, out var probeVolumeCount) &&
                    probeVolumeCount > 0;
                config.UpdateSceneBakeData(scene, hasProbeVolume, sceneBounds);
                if (scene == primaryScene)
                {
                    touchedPrimaryScene = true;
                }
            }

            if (!touchedPrimaryScene && primaryScene.IsValid())
            {
                config.UpdateSceneBakeData(primaryScene, true, primarySceneBounds);
            }

            if (primaryScene.IsValid())
            {
                config.CaptureSceneMetadata(primaryScene);
            }
        }

        private static bool TryCollectSceneVolumeBounds(Scene scene, out Bounds bounds, out int probeVolumeCount)
        {
            bounds = default;
            probeVolumeCount = 0;
            if (!scene.IsValid())
            {
                return false;
            }

            var volumes = Object.FindObjectsOfType<BurtGIProbeVolume>(true);
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled || volume.gameObject.scene != scene ||
                    !TryGetVolumeOBB(volume, out var volumeBounds))
                {
                    continue;
                }

                if (probeVolumeCount == 0)
                {
                    bounds = volumeBounds.bounds;
                }
                else
                {
                    bounds.Encapsulate(volumeBounds.bounds);
                }

                probeVolumeCount++;
            }

            return probeVolumeCount > 0;
        }

        private static List<BurtGIProbeVolumeBounds> CollectBakingVolumeOBBs(
            BurtXGIProbeBakingConfig config,
            Scene primaryScene,
            out Bounds bounds,
            out int probeVolumeCount)
        {
            var result = new List<BurtGIProbeVolumeBounds>();
            bounds = default;
            probeVolumeCount = 0;

            var volumes = Object.FindObjectsOfType<BurtGIProbeVolume>(true);
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled ||
                    !ShouldIncludeSceneInBakingSet(config, volume.gameObject.scene, primaryScene) ||
                    !TryGetVolumeOBB(volume, out var volumeBounds))
                {
                    continue;
                }

                result.Add(volumeBounds);
                if (probeVolumeCount == 0)
                {
                    bounds = volumeBounds.bounds;
                }
                else
                {
                    bounds.Encapsulate(volumeBounds.bounds);
                }

                probeVolumeCount++;
            }

            return result;
        }

        private static bool ShouldIncludeSceneInBakingSet(
            BurtXGIProbeBakingConfig config,
            Scene scene,
            Scene primaryScene)
        {
            if (config == null || !scene.IsValid() || !scene.isLoaded)
            {
                return false;
            }

            if (scene == primaryScene)
            {
                return true;
            }

            var sceneGuid = ResolveSceneGuid(scene);
            if (!string.IsNullOrEmpty(sceneGuid) &&
                config.TryGetSceneBakeData(sceneGuid, out var sceneData) &&
                sceneData != null)
            {
                return sceneData.bakeScene;
            }

            return !BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, config.platform, out var sceneConfig) ||
                sceneConfig == config ||
                config.IsEquivalent(sceneConfig);
        }

        private static string[] CollectIntersectingSceneGuids(BurtXGIProbeBakingConfig config, Bounds cellBounds, Scene fallbackScene)
        {
            var sceneGuids = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var volumes = Object.FindObjectsOfType<BurtGIProbeVolume>(true);
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled ||
                    !ShouldIncludeSceneInBakingSet(config, volume.gameObject.scene, fallbackScene) ||
                    !TryGetVolumeOBB(volume, out var volumeBounds) ||
                    !OBBAABBIntersect(volumeBounds, cellBounds))
                {
                    continue;
                }

                var sceneGuid = ResolveSceneGuid(volume.gameObject.scene);
                if (!string.IsNullOrEmpty(sceneGuid))
                {
                    sceneGuids.Add(sceneGuid);
                }
            }

            if (sceneGuids.Count == 0)
            {
                var fallbackGuid = ResolveSceneGuid(fallbackScene);
                if (!string.IsNullOrEmpty(fallbackGuid))
                {
                    sceneGuids.Add(fallbackGuid);
                }
            }

            var result = new string[sceneGuids.Count];
            sceneGuids.CopyTo(result);
            return result;
        }

        private static string ResolveSceneGuid(Scene scene)
        {
            if (!scene.IsValid())
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(scene.path))
            {
                return AssetDatabase.AssetPathToGUID(scene.path);
            }

            return scene.name ?? string.Empty;
        }

        private static string[] CopySceneGuids(string[] sceneGuids)
        {
            if (sceneGuids == null || sceneGuids.Length == 0)
            {
                return System.Array.Empty<string>();
            }

            var copy = new string[sceneGuids.Length];
            System.Array.Copy(sceneGuids, copy, sceneGuids.Length);
            return copy;
        }

        private static List<BurtGIProbeVolumeBounds> CollectActiveSceneVolumeOBBs(Scene scene)
        {
            var bounds = new List<BurtGIProbeVolumeBounds>();
            if (!scene.IsValid())
            {
                return bounds;
            }

            var volumes = Object.FindObjectsOfType<BurtGIProbeVolume>(true);
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || !volume.isActiveAndEnabled || volume.gameObject.scene != scene ||
                    !TryGetVolumeOBB(volume, out var volumeBounds))
                {
                    continue;
                }

                bounds.Add(volumeBounds);
            }

            return bounds;
        }

        private static Bounds CreateCellBounds(BurtXGIProbeBakingConfig config, Vector3Int cellPosition)
        {
            var cellSize = config.CellSizeInMeters;
            var min = config.probeOffset + new Vector3(cellPosition.x, cellPosition.y, cellPosition.z) * cellSize;
            return new Bounds(min + Vector3.one * (cellSize * 0.5f), Vector3.one * cellSize);
        }

        private static bool IntersectsAny(Bounds cellBounds, List<BurtGIProbeVolumeBounds> volumeBounds)
        {
            for (var i = 0; i < volumeBounds.Count; i++)
            {
                if (OBBAABBIntersect(volumeBounds[i], cellBounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolvePlacementSubdivisionLevel(
            BurtXGIProbeBakingConfig config,
            Bounds cellBounds,
            List<BurtGIProbeVolumeBounds> volumeBounds)
        {
            var fallback = Mathf.Clamp(config.simplificationLevels - 1, 0, config.simplificationLevels);
            var resolved = fallback;
            var foundOverride = false;
            for (var i = 0; i < volumeBounds.Count; i++)
            {
                var volume = volumeBounds[i];
                if (!volume.overridesSubdivLevels || !OBBAABBIntersect(volume, cellBounds))
                {
                    continue;
                }

                ResolveVolumeSubdivisionRange(config, volume, cellBounds, out var minSubdiv, out var maxSubdiv);
                resolved = Mathf.Clamp(resolved, minSubdiv, maxSubdiv);
                foundOverride = true;
            }

            return foundOverride ? resolved : fallback;
        }

        private static bool IsBrickAcceptedByAnyVolume(
            BurtXGIProbeBakingConfig config,
            Bounds cellBounds,
            Bounds brickBounds,
            int subdivisionLevel,
            List<BurtGIProbeVolumeBounds> volumeBounds)
        {
            for (var i = 0; i < volumeBounds.Count; i++)
            {
                var volume = volumeBounds[i];
                if (!OBBAABBIntersect(volume, brickBounds))
                {
                    continue;
                }

                ResolveVolumeSubdivisionRange(config, volume, cellBounds, out var minSubdiv, out var maxSubdiv);
                if (subdivisionLevel >= minSubdiv && subdivisionLevel <= maxSubdiv)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResolveVolumeSubdivisionRange(
            BurtXGIProbeBakingConfig config,
            BurtGIProbeVolumeBounds volume,
            Bounds cellBounds,
            out int minSubdiv,
            out int maxSubdiv)
        {
            var maxSubdivisionLevel = Mathf.Max(0, config.simplificationLevels);
            volume.GetSubdivisionOverride(maxSubdivisionLevel, out minSubdiv, out maxSubdiv);

            var clippedBounds = volume.bounds;
            clippedBounds.min = Vector3.Max(clippedBounds.min, cellBounds.min);
            clippedBounds.max = Vector3.Min(clippedBounds.max, cellBounds.max);
            var volumeMax = MaxSubdivLevelInProbeVolume(config, clippedBounds.size, maxSubdivisionLevel);
            volumeMax = Mathf.Max(volumeMax, minSubdiv);
            maxSubdiv = Mathf.Min(maxSubdiv, volumeMax);
        }

        private static int MaxSubdivLevelInProbeVolume(BurtXGIProbeBakingConfig config, Vector3 volumeSize, int maxSubdivisionLevel)
        {
            var maxSizedDim = Mathf.Max(volumeSize.x, Mathf.Max(volumeSize.y, volumeSize.z));
            var maxSideInBricks = maxSizedDim / Mathf.Max(0.0001f, config.minDistanceBetweenProbes);
            var subdiv = maxSideInBricks > 0.0001f ? Mathf.FloorToInt(Mathf.Log(maxSideInBricks, BurtGIVirtualProbePhysicalPool.BrickCellCount)) : 0;
            return Mathf.Max(subdiv, maxSubdivisionLevel) - 1;
        }

        private static Bounds BuildOBBBounds(BurtGIProbeVolumeBounds obb)
        {
            return obb.CalculateAABB();
        }

        private static bool OBBAABBIntersect(BurtGIProbeVolumeBounds obb, Bounds bounds)
        {
            return BurtGIProbeVolumePositioning.OBBAABBIntersect(in obb, bounds, obb.bounds);
        }

        private sealed class VirtualOffsetGeometry
        {
            internal readonly List<Collider> colliders = new List<Collider>();
            internal readonly List<Renderer> renderers = new List<Renderer>();
            internal Bounds bounds;
            internal bool hasBounds;
            internal bool HasGeometry => colliders.Count > 0 || renderers.Count > 0;

            internal void Encapsulate(Bounds value)
            {
                if (!hasBounds)
                {
                    bounds = value;
                    hasBounds = true;
                    return;
                }

                bounds.Encapsulate(value);
            }
        }

        private sealed class VirtualOffsetAdjustVolumes
        {
            internal readonly List<BurtXGIProbeAdjustVolume> appliers = new List<BurtXGIProbeAdjustVolume>();
            internal readonly List<BurtXGIProbeAdjustVolume> overriders = new List<BurtXGIProbeAdjustVolume>();
            internal readonly Dictionary<int, VirtualOffsetAdjustVolumesForCell> cells = new Dictionary<int, VirtualOffsetAdjustVolumesForCell>();
            internal Vector3Int minCellPosition;
            internal Vector3Int cellCount;
            internal bool HasAppliers => appliers.Count > 0;
            internal bool HasVolumes => appliers.Count > 0 || overriders.Count > 0;
            internal bool HasCellIndex => cells.Count > 0 && cellCount.x > 0 && cellCount.y > 0 && cellCount.z > 0;
        }

        private sealed class VirtualOffsetAdjustVolumesForCell
        {
            internal readonly List<BurtXGIProbeAdjustVolume> appliers = new List<BurtXGIProbeAdjustVolume>();
            internal readonly List<BurtXGIProbeAdjustVolume> overriders = new List<BurtXGIProbeAdjustVolume>();
        }

        private struct VirtualOffsetOverrideSettings
        {
            internal float originBias;
            internal float geometryBias;
            internal float validityThreshold;
        }

        private sealed class TimeSliceLight
        {
            internal Light light;
            internal LightType type;
            internal Vector3 position;
            internal Vector3 direction;
            internal Vector3 color;
            internal float range;
            internal float spotAngle;
            internal float innerSpotAngle;
        }

        private static int ResolveTimeSliceSampleCountPerStep(BurtXGIProbeBakingConfig config)
        {
            if (config == null)
            {
                return 16;
            }

            return Mathf.Max(16, config.timeSliceSampleCountPerStep);
        }

        private static XGITimeSliceLightData[] BuildTimeSliceRayTracingLightData(List<TimeSliceLight> lights)
        {
            if (lights == null || lights.Count == 0)
            {
                return System.Array.Empty<XGITimeSliceLightData>();
            }

            var result = new XGITimeSliceLightData[lights.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var light = lights[i];
                var type = light.type == LightType.Directional ? 0f : light.type == LightType.Spot ? 2f : 1f;
                var direction = light.direction.sqrMagnitude > 0.000001f ? light.direction.normalized : Vector3.forward;
                var outerCos = light.type == LightType.Spot ? Mathf.Cos(light.spotAngle * 0.5f * Mathf.Deg2Rad) : -1f;
                var innerCos = light.type == LightType.Spot ? Mathf.Cos(light.innerSpotAngle * 0.5f * Mathf.Deg2Rad) : 1f;
                result[i] = new XGITimeSliceLightData
                {
                    positionType = new Vector4(light.position.x, light.position.y, light.position.z, type),
                    directionRange = new Vector4(direction.x, direction.y, direction.z, light.range),
                    colorOuterCos = new Vector4(light.color.x, light.color.y, light.color.z, outerCos),
                    spotInnerCos = new Vector4(innerCos, 0f, 0f, 0f)
                };
            }

            return result;
        }

        private static VirtualOffsetGeometry CollectBakingVirtualOffsetGeometry(BurtXGIProbeBakingConfig config, Scene primaryScene)
        {
            var geometry = new VirtualOffsetGeometry();
            if (config == null || !primaryScene.IsValid())
            {
                return geometry;
            }

            var colliders = Object.FindObjectsOfType<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider == null || !collider.enabled || collider.isTrigger ||
                    !ShouldIncludeSceneInBakingSet(config, collider.gameObject.scene, primaryScene) ||
                    !collider.gameObject.activeInHierarchy ||
                    collider.GetComponentInParent<BurtGIProbeVolume>() != null)
                {
                    continue;
                }

                geometry.colliders.Add(collider);
                geometry.Encapsulate(collider.bounds);
            }

            var renderers = Object.FindObjectsOfType<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled ||
                    !ShouldIncludeSceneInBakingSet(config, renderer.gameObject.scene, primaryScene) ||
                    !renderer.gameObject.activeInHierarchy ||
                    renderer.GetComponentInParent<BurtGIProbeVolume>() != null)
                {
                    continue;
                }

                geometry.renderers.Add(renderer);
                geometry.Encapsulate(renderer.bounds);
            }

            return geometry;
        }

        private static VirtualOffsetAdjustVolumes CollectBakingVirtualOffsetAdjustVolumes(BurtXGIProbeBakingConfig config, Scene primaryScene)
        {
            var result = new VirtualOffsetAdjustVolumes();
            if (config == null || !primaryScene.IsValid())
            {
                return result;
            }

            var canBuildCellIndex = TryGetVirtualOffsetCellIndexLayout(config, out var minCellPosition, out var cellCount);
            if (canBuildCellIndex)
            {
                result.minCellPosition = minCellPosition;
                result.cellCount = cellCount;
            }

            var volumes = Object.FindObjectsOfType<BurtXGIProbeAdjustVolume>(true);
            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null || !volume.enabled || !volume.gameObject.activeInHierarchy ||
                    !ShouldIncludeSceneInBakingSet(config, volume.gameObject.scene, primaryScene))
                {
                    continue;
                }

                switch (volume.mode)
                {
                    case BurtXGIProbeAdjustVolume.AdjustmentMode.ApplyVirtualOffset:
                        result.appliers.Add(volume);
                        AddVirtualOffsetAdjustVolumeToCellIndex(result, config, volume, true, canBuildCellIndex);
                        break;
                    case BurtXGIProbeAdjustVolume.AdjustmentMode.OverrideVirtualOffsetSettings:
                        result.overriders.Add(volume);
                        AddVirtualOffsetAdjustVolumeToCellIndex(result, config, volume, false, canBuildCellIndex);
                        break;
                }
            }

            result.appliers.Sort(CompareVirtualOffsetAdjustVolumePriority);
            result.overriders.Sort(CompareVirtualOffsetAdjustVolumePriority);
            foreach (var pair in result.cells)
            {
                pair.Value.appliers.Sort(CompareVirtualOffsetAdjustVolumePriority);
                pair.Value.overriders.Sort(CompareVirtualOffsetAdjustVolumePriority);
            }
            return result;
        }

        private static bool TryGetVirtualOffsetCellIndexLayout(
            BurtXGIProbeBakingConfig config,
            out Vector3Int minCellPosition,
            out Vector3Int cellCount)
        {
            minCellPosition = default;
            cellCount = default;
            if (config == null)
            {
                return false;
            }

            minCellPosition = config.minCellPosition;
            cellCount = config.maxCellPosition - config.minCellPosition + Vector3Int.one;
            return cellCount.x > 0 && cellCount.y > 0 && cellCount.z > 0;
        }

        private static void AddVirtualOffsetAdjustVolumeToCellIndex(
            VirtualOffsetAdjustVolumes result,
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeAdjustVolume volume,
            bool applier,
            bool canBuildCellIndex)
        {
            if (!canBuildCellIndex || result == null || config == null || volume == null)
            {
                return;
            }

            volume.GetOBBAndAABB(out _, out var bounds);
            var min = Vector3Int.Max(config.PositionToCell(bounds.min), result.minCellPosition);
            var max = Vector3Int.Min(config.PositionToCell(bounds.max), result.minCellPosition + result.cellCount - Vector3Int.one);
            if (max.x < min.x || max.y < min.y || max.z < min.z)
            {
                return;
            }

            for (var z = min.z; z <= max.z; z++)
            {
                for (var y = min.y; y <= max.y; y++)
                {
                    for (var x = min.x; x <= max.x; x++)
                    {
                        var cellIndex = CellPositionToIndex(new Vector3Int(x, y, z), result.minCellPosition, result.cellCount);
                        if (!result.cells.TryGetValue(cellIndex, out var cellVolumes))
                        {
                            result.cells[cellIndex] = cellVolumes = new VirtualOffsetAdjustVolumesForCell();
                        }

                        if (applier)
                        {
                            cellVolumes.appliers.Add(volume);
                        }
                        else
                        {
                            cellVolumes.overriders.Add(volume);
                        }
                    }
                }
            }
        }

        private static int CompareVirtualOffsetAdjustVolumePriority(
            BurtXGIProbeAdjustVolume lhs,
            BurtXGIProbeAdjustVolume rhs)
        {
            var lhsVolume = lhs != null ? lhs.ComputeVolume() : 0f;
            var rhsVolume = rhs != null ? rhs.ComputeVolume() : 0f;
            return rhsVolume.CompareTo(lhsVolume);
        }

        private static bool TryResolveVirtualOffsetApplier(
            Vector3 position,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetAdjustVolumes adjustVolumes,
            out Vector3 offset)
        {
            offset = Vector3.zero;
            if (adjustVolumes == null)
            {
                return false;
            }

            var appliers = GetVirtualOffsetAppliersForPosition(position, config, adjustVolumes);
            for (var i = 0; i < appliers.Count; i++)
            {
                var volume = appliers[i];
                if (volume != null && volume.ContainsPoint(position))
                {
                    offset = volume.GetVirtualOffset();
                    return offset.sqrMagnitude > 0.00000001f;
                }
            }

            return false;
        }

        private static VirtualOffsetOverrideSettings ResolveVirtualOffsetOverrideSettings(
            Vector3 position,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetAdjustVolumes adjustVolumes)
        {
            var result = new VirtualOffsetOverrideSettings
            {
                originBias = config != null ? config.virtualOffsetRayOriginBias : -0.001f,
                geometryBias = ResolveVirtualOffsetGeometryBias(config),
                validityThreshold = config != null ? config.virtualOffsetValidityThreshold : 0.25f
            };

            if (adjustVolumes == null)
            {
                return result;
            }

            var overriders = GetVirtualOffsetOverridersForPosition(position, config, adjustVolumes);
            for (var i = 0; i < overriders.Count; i++)
            {
                var volume = overriders[i];
                if (volume == null || !volume.ContainsPoint(position))
                {
                    continue;
                }

                result.originBias = volume.rayOriginBias;
                result.geometryBias = Mathf.Max(0f, volume.geometryBias);
                result.validityThreshold = Mathf.Clamp01(1f - volume.virtualOffsetThreshold);
                break;
            }

            return result;
        }

        private static List<BurtXGIProbeAdjustVolume> GetVirtualOffsetAppliersForPosition(
            Vector3 position,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetAdjustVolumes adjustVolumes)
        {
            if (TryGetVirtualOffsetAdjustVolumesForPosition(position, config, adjustVolumes, out var cellVolumes))
            {
                return cellVolumes != null ? cellVolumes.appliers : EmptyVirtualOffsetAdjustVolumeList;
            }

            return adjustVolumes != null ? adjustVolumes.appliers : EmptyVirtualOffsetAdjustVolumeList;
        }

        private static List<BurtXGIProbeAdjustVolume> GetVirtualOffsetOverridersForPosition(
            Vector3 position,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetAdjustVolumes adjustVolumes)
        {
            if (TryGetVirtualOffsetAdjustVolumesForPosition(position, config, adjustVolumes, out var cellVolumes))
            {
                return cellVolumes != null ? cellVolumes.overriders : EmptyVirtualOffsetAdjustVolumeList;
            }

            return adjustVolumes != null ? adjustVolumes.overriders : EmptyVirtualOffsetAdjustVolumeList;
        }

        private static bool TryGetVirtualOffsetAdjustVolumesForPosition(
            Vector3 position,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetAdjustVolumes adjustVolumes,
            out VirtualOffsetAdjustVolumesForCell cellVolumes)
        {
            cellVolumes = null;
            if (config == null || adjustVolumes == null || !adjustVolumes.HasCellIndex)
            {
                return false;
            }

            var cellPosition = config.PositionToCell(position);
            var maxCellPosition = adjustVolumes.minCellPosition + adjustVolumes.cellCount - Vector3Int.one;
            if (cellPosition.x < adjustVolumes.minCellPosition.x || cellPosition.y < adjustVolumes.minCellPosition.y || cellPosition.z < adjustVolumes.minCellPosition.z ||
                cellPosition.x > maxCellPosition.x || cellPosition.y > maxCellPosition.y || cellPosition.z > maxCellPosition.z)
            {
                return true;
            }

            var cellIndex = CellPositionToIndex(cellPosition, adjustVolumes.minCellPosition, adjustVolumes.cellCount);
            adjustVolumes.cells.TryGetValue(cellIndex, out cellVolumes);
            return true;
        }

        private static List<TimeSliceLight> CollectBakingTimeSliceLights(BurtXGIProbeBakingConfig config, Scene primaryScene)
        {
            var result = new List<TimeSliceLight>();
            if (config == null || !primaryScene.IsValid())
            {
                return result;
            }

            var lights = Object.FindObjectsOfType<Light>(true);
            for (var i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy ||
                    !ShouldIncludeSceneInBakingSet(config, light.gameObject.scene, primaryScene) ||
                    light.intensity <= 0f)
                {
                    continue;
                }

                var color = ColorToVector3(light.color) * Mathf.Max(0f, light.intensity) * Mathf.Max(0f, light.bounceIntensity);
                if (color.sqrMagnitude <= 0.0000001f)
                {
                    continue;
                }

                result.Add(new TimeSliceLight
                {
                    light = light,
                    type = light.type,
                    position = light.transform.position,
                    direction = light.transform.forward.normalized,
                    color = color,
                    range = Mathf.Max(0.001f, light.range),
                    spotAngle = Mathf.Clamp(light.spotAngle, 0.001f, 179.9f),
                    innerSpotAngle = Mathf.Clamp(light.innerSpotAngle, 0.001f, Mathf.Max(0.001f, light.spotAngle))
                });
            }

            return result;
        }

        private static float ResolveBakedTimeSliceMainLightIntensity(List<TimeSliceLight> lights)
        {
            if (lights == null || lights.Count == 0)
            {
                return 1f;
            }

            var strongestAny = 0f;
            var strongestDirectional = 0f;
            for (var i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                var intensity = Mathf.Max(0f, light.color.x, light.color.y, light.color.z);
                strongestAny = Mathf.Max(strongestAny, intensity);
                if (light.type == LightType.Directional)
                {
                    strongestDirectional = Mathf.Max(strongestDirectional, intensity);
                }
            }

            return Mathf.Max(0.0001f, strongestDirectional > 0f ? strongestDirectional : strongestAny);
        }

        private static BurtXGIProbeBakedSphericalHarmonicsL2 BakeTimeSliceProbeSH(
            Vector3 position,
            int probeIndex,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetGeometry geometry,
            List<TimeSliceLight> lights,
            Vector3 ambientColor,
            float maxRayDistance,
            ref int shadowedSamples)
        {
            var skyVisibility = ResolveBakedSkyVisibility(config, probeIndex);
            var l0 = Vector3.Max(Vector3.zero, ambientColor * skyVisibility) / SHBasis0;
            var l1R = Vector3.zero;
            var l1G = Vector3.zero;
            var l1B = Vector3.zero;
            var l2R = Vector4.zero;
            var l2G = Vector4.zero;
            var l2B = Vector4.zero;
            var l2C = Vector3.zero;
            var offset = Mathf.Max(0f, config.timeSliceOffsetRay);

            for (var lightIndex = 0; lightIndex < lights.Count; lightIndex++)
            {
                if (!TryEvaluateTimeSliceLight(lights[lightIndex], position, out var lightColor, out var lightDirection, out var rayDistance))
                {
                    continue;
                }

                var occlusionDistance = Mathf.Min(Mathf.Max(rayDistance - offset, 0.001f), maxRayDistance);
                if (IsTimeSliceRayOccluded(position, lightDirection, offset, occlusionDistance, geometry))
                {
                    shadowedSamples++;
                    continue;
                }

                AddTimeSliceDirectionalSH(ref l0, ref l1R, ref l1G, ref l1B, ref l2R, ref l2G, ref l2B, ref l2C, lightColor, lightDirection);
            }

            ConvolveTimeSliceRadianceToIrradiance(ref l1R, ref l1G, ref l1B, ref l2R, ref l2G, ref l2B, ref l2C);
            return PackBakedTimeSliceSH(l0, l1R, l1G, l1B, l2R, l2G, l2B, l2C);
        }

        private static bool TryEvaluateTimeSliceLight(
            TimeSliceLight source,
            Vector3 position,
            out Vector3 color,
            out Vector3 lightDirection,
            out float rayDistance)
        {
            color = Vector3.zero;
            lightDirection = Vector3.up;
            rayDistance = float.PositiveInfinity;
            if (source == null || source.light == null || !source.light.enabled)
            {
                return false;
            }

            switch (source.type)
            {
                case LightType.Directional:
                    lightDirection = (-source.direction).normalized;
                    color = source.color;
                    return color.sqrMagnitude > 0.0000001f && lightDirection.sqrMagnitude > 0.5f;

                case LightType.Point:
                case LightType.Spot:
                default:
                    var toLight = source.position - position;
                    var distance = toLight.magnitude;
                    if (distance <= 0.0001f || distance >= source.range)
                    {
                        return false;
                    }

                    lightDirection = toLight / distance;
                    rayDistance = distance;
                    var normalizedDistance = Mathf.Clamp01(distance / source.range);
                    var attenuation = Mathf.Pow(1f - normalizedDistance * normalizedDistance, 2f);
                    if (source.type == LightType.Spot)
                    {
                        var lightToProbe = -lightDirection;
                        var spotCos = Vector3.Dot(lightToProbe, source.direction);
                        var outerCos = Mathf.Cos(source.spotAngle * 0.5f * Mathf.Deg2Rad);
                        if (spotCos <= outerCos)
                        {
                            return false;
                        }

                        var innerCos = Mathf.Cos(source.innerSpotAngle * 0.5f * Mathf.Deg2Rad);
                        var spotAttenuation = innerCos <= outerCos ? 1f : Mathf.Clamp01((spotCos - outerCos) / (innerCos - outerCos));
                        attenuation *= spotAttenuation * spotAttenuation;
                    }

                    color = source.color * attenuation;
                    return color.sqrMagnitude > 0.0000001f;
            }
        }

        private static Vector3 ResolveTimeSliceAmbientColor()
        {
            switch (RenderSettings.ambientMode)
            {
                case UnityEngine.Rendering.AmbientMode.Trilight:
                    return (ColorToVector3(RenderSettings.ambientSkyColor) +
                            ColorToVector3(RenderSettings.ambientEquatorColor) +
                            ColorToVector3(RenderSettings.ambientGroundColor)) / 3f;
                case UnityEngine.Rendering.AmbientMode.Flat:
                    return ColorToVector3(RenderSettings.ambientLight);
                case UnityEngine.Rendering.AmbientMode.Skybox:
                default:
                    var probe = RenderSettings.ambientProbe;
                    var l0 = new Vector3(probe[0, 0], probe[1, 0], probe[2, 0]) * SHBasis0;
                    if (l0.sqrMagnitude > 0.0000001f)
                    {
                        return Vector3.Max(Vector3.zero, l0);
                    }

                    return ColorToVector3(RenderSettings.ambientLight);
            }
        }

        private static float ResolveBakedSkyVisibility(BurtXGIProbeBakingConfig config, int probeIndex)
        {
            if (config == null || !config.skyVisibility || config.bakedSkyVisibilityL0L1 == null ||
                probeIndex < 0 || probeIndex >= config.bakedSkyVisibilityL0L1.Length)
            {
                return 1f;
            }

            return Mathf.Clamp01(config.bakedSkyVisibilityL0L1[probeIndex].x * SHBasis0);
        }

        private static float ResolveTimeSliceRayDistance(
            BurtXGIProbeBakingConfig config,
            VirtualOffsetGeometry geometry,
            List<TimeSliceLight> lights)
        {
            return ResolveTimeSliceRayDistance(config, geometry, lights, default, false);
        }

        private static float ResolveTimeSliceRayDistance(
            BurtXGIProbeBakingConfig config,
            VirtualOffsetGeometry geometry,
            List<TimeSliceLight> lights,
            Bounds requestBounds,
            bool hasRequestBounds)
        {
            var size = config.globalBounds.size.magnitude;
            if (hasRequestBounds)
            {
                size = Mathf.Max(size, requestBounds.size.magnitude);
            }

            if (geometry != null && geometry.hasBounds)
            {
                size = Mathf.Max(size, geometry.bounds.size.magnitude);
            }

            for (var i = 0; i < lights.Count; i++)
            {
                if (lights[i] != null && lights[i].type != LightType.Directional)
                {
                    size = Mathf.Max(size, lights[i].range);
                }
            }

            return Mathf.Max(64f, size * 2f + Mathf.Max(0f, config.timeSliceOffsetRay) + config.MinBrickSize);
        }

        private static bool IsTimeSliceRayOccluded(
            Vector3 position,
            Vector3 direction,
            float offset,
            float maxRayDistance,
            VirtualOffsetGeometry geometry)
        {
            if (geometry == null || !geometry.HasGeometry || maxRayDistance <= 0.0001f)
            {
                return false;
            }

            var origin = position + direction * Mathf.Max(0f, offset);
            for (var i = 0; i < geometry.colliders.Count; i++)
            {
                var collider = geometry.colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (collider.bounds.Contains(origin) ||
                    collider.Raycast(new Ray(origin, direction), out _, maxRayDistance))
                {
                    return true;
                }
            }

            for (var i = 0; i < geometry.renderers.Count; i++)
            {
                var renderer = geometry.renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (renderer.bounds.Contains(origin) || RayIntersectsBounds(origin, direction, renderer.bounds, maxRayDistance))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddTimeSliceDirectionalSH(
            ref Vector3 l0,
            ref Vector3 l1R,
            ref Vector3 l1G,
            ref Vector3 l1B,
            ref Vector4 l2R,
            ref Vector4 l2G,
            ref Vector4 l2B,
            ref Vector3 l2C,
            Vector3 lightColor,
            Vector3 direction)
        {
            var safeDirection = direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.up;
            var halfEnergy = Vector3.Max(Vector3.zero, lightColor) * 0.5f;
            l0 += halfEnergy / SHBasis0;
            var l1 = halfEnergy / SHBasis1;
            var xrenderDirectionL1 = new Vector3(-safeDirection.x, safeDirection.y, -safeDirection.z);
            l1R += xrenderDirectionL1 * l1.x;
            l1G += xrenderDirectionL1 * l1.y;
            l1B += xrenderDirectionL1 * l1.z;

            var xgiAxis = new Vector3(safeDirection.z, safeDirection.x, safeDirection.y);
            var xgiAxisSquared = Vector3.Scale(xgiAxis, xgiAxis);
            var basisL2 = new Vector4(
                SHBasis2 * xgiAxis.x * xgiAxis.y,
                -SHBasis2 * xgiAxis.y * xgiAxis.z,
                SHBasis3 * (3f * xgiAxisSquared.z - 1f),
                -SHBasis2 * xgiAxis.x * xgiAxis.z);
            var basisL2C = SHBasis4 * (xgiAxisSquared.x - xgiAxisSquared.y);
            const float l2AnisotropyScale = 0.5f;
            l2R += basisL2 * (halfEnergy.x * l2AnisotropyScale);
            l2G += basisL2 * (halfEnergy.y * l2AnisotropyScale);
            l2B += basisL2 * (halfEnergy.z * l2AnisotropyScale);
            l2C += new Vector3(halfEnergy.x, halfEnergy.y, halfEnergy.z) * (basisL2C * l2AnisotropyScale);
        }

        private static void ConvolveTimeSliceRadianceToIrradiance(
            ref Vector3 l1R,
            ref Vector3 l1G,
            ref Vector3 l1B,
            ref Vector4 l2R,
            ref Vector4 l2G,
            ref Vector4 l2B,
            ref Vector3 l2C)
        {
            const float l1Scale = 0.6666666667f;
            const float l2Scale = 0.25f;
            l1R *= l1Scale;
            l1G *= l1Scale;
            l1B *= l1Scale;
            l2R *= l2Scale;
            l2G *= l2Scale;
            l2B *= l2Scale;
            l2C *= l2Scale;
        }

        private static BurtXGIProbeBakedSphericalHarmonicsL2 PackBakedTimeSliceSH(
            Vector3 l0,
            Vector3 l1R,
            Vector3 l1G,
            Vector3 l1B,
            Vector4 l2R,
            Vector4 l2G,
            Vector4 l2B,
            Vector3 l2C)
        {
            l0 = Vector3.Max(Vector3.zero, l0);
            return new BurtXGIProbeBakedSphericalHarmonicsL2
            {
                c0 = l0,
                c1 = new Vector3(
                    EncodeCompressedSH(l1R.x, l0.x, XRenderL1CompressionScale),
                    EncodeCompressedSH(l1G.x, l0.y, XRenderL1CompressionScale),
                    EncodeCompressedSH(l1R.y, l0.x, XRenderL1CompressionScale)),
                c2 = new Vector3(
                    EncodeCompressedSH(l1G.y, l0.y, XRenderL1CompressionScale),
                    EncodeCompressedSH(l1G.z, l0.y, XRenderL1CompressionScale),
                    EncodeCompressedSH(l1R.z, l0.x, XRenderL1CompressionScale)),
                c3 = new Vector3(
                    EncodeCompressedSH(l1B.x, l0.z, XRenderL1CompressionScale),
                    EncodeCompressedSH(l1B.y, l0.z, XRenderL1CompressionScale),
                    EncodeCompressedSH(l1B.z, l0.z, XRenderL1CompressionScale)),
                c4 = new Vector3(
                    EncodeCompressedSH(l2R.x, l0.x, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2G.x, l0.y, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2B.x, l0.z, XRenderL2CompressionScale)),
                c5 = new Vector3(
                    EncodeCompressedSH(l2R.y, l0.x, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2G.y, l0.y, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2B.y, l0.z, XRenderL2CompressionScale)),
                c6 = new Vector3(
                    EncodeCompressedSH(l2R.z, l0.x, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2G.z, l0.y, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2B.z, l0.z, XRenderL2CompressionScale)),
                c7 = new Vector3(
                    EncodeCompressedSH(l2R.w, l0.x, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2G.w, l0.y, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2B.w, l0.z, XRenderL2CompressionScale)),
                c8 = new Vector3(
                    EncodeCompressedSH(l2C.x, l0.x, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2C.y, l0.y, XRenderL2CompressionScale),
                    EncodeCompressedSH(l2C.z, l0.z, XRenderL2CompressionScale))
            };
        }

        private static BurtXGIProbeBakedSphericalHarmonicsL2 PackRawTimeSliceSH(float[] rawSH, int probeIndex)
        {
            var baseIndex = probeIndex * 27;
            if (rawSH == null || baseIndex < 0 || baseIndex + 26 >= rawSH.Length)
            {
                return BurtXGIProbeBakedSphericalHarmonicsL2.Ambient(Vector3.zero);
            }

            var l0 = new Vector3(rawSH[baseIndex], rawSH[baseIndex + 9], rawSH[baseIndex + 18]);
            var l1R = new Vector3(rawSH[baseIndex + 1], rawSH[baseIndex + 2], rawSH[baseIndex + 3]);
            var l1G = new Vector3(rawSH[baseIndex + 10], rawSH[baseIndex + 11], rawSH[baseIndex + 12]);
            var l1B = new Vector3(rawSH[baseIndex + 19], rawSH[baseIndex + 20], rawSH[baseIndex + 21]);
            var l2R = new Vector4(rawSH[baseIndex + 4], rawSH[baseIndex + 5], rawSH[baseIndex + 6], rawSH[baseIndex + 7]);
            var l2G = new Vector4(rawSH[baseIndex + 13], rawSH[baseIndex + 14], rawSH[baseIndex + 15], rawSH[baseIndex + 16]);
            var l2B = new Vector4(rawSH[baseIndex + 22], rawSH[baseIndex + 23], rawSH[baseIndex + 24], rawSH[baseIndex + 25]);
            var l2C = new Vector3(rawSH[baseIndex + 8], rawSH[baseIndex + 17], rawSH[baseIndex + 26]);
            return PackBakedTimeSliceSH(l0, l1R, l1G, l1B, l2R, l2G, l2B, l2C);
        }

        private static float EncodeCompressedSH(float coefficient, float l0, float scale)
        {
            if (Mathf.Abs(l0) <= 0.000001f || scale <= 0.000001f)
            {
                return 0.5f;
            }

            return Mathf.Clamp01(coefficient / (l0 * scale) + 0.5f);
        }

        private static Vector3 ColorToVector3(Color color)
        {
            return new Vector3(color.r, color.g, color.b);
        }

        private static int[] ResolveProbeSubdivisionLevels(BurtXGIProbeBakingConfig config, int probeCount)
        {
            var subdivisionLevels = new int[Mathf.Max(0, probeCount)];
            Fill(subdivisionLevels, Mathf.Max(0, config.simplificationLevels));
            var cells = config.bakedPlacedCells ?? System.Array.Empty<BurtXGIProbePlacedCell>();
            var bricks = config.bakedPlacedBricks ?? System.Array.Empty<BurtXGIProbePlacedBrick>();
            var probesPerBrick = BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var cell = cells[cellIndex];
                var brickEnd = Mathf.Min(bricks.Length, cell.brickStartIndex + cell.brickCount);
                for (var brickIndex = Mathf.Max(0, cell.brickStartIndex); brickIndex < brickEnd; brickIndex++)
                {
                    var probeStart = cell.probeStartIndex + (brickIndex - cell.brickStartIndex) * probesPerBrick;
                    var probeEnd = Mathf.Min(subdivisionLevels.Length, probeStart + probesPerBrick);
                    for (var probeIndex = Mathf.Max(0, probeStart); probeIndex < probeEnd; probeIndex++)
                    {
                        subdivisionLevels[probeIndex] = Mathf.Max(0, bricks[brickIndex].subdivisionLevel);
                    }
                }
            }

            return subdivisionLevels;
        }

        private static bool TryResolveVirtualOffset(
            Vector3 position,
            BurtXGIProbeBakingConfig config,
            int subdivisionLevel,
            VirtualOffsetGeometry geometry,
            float geometryBias,
            out Vector3 offset)
        {
            offset = Vector3.zero;
            var searchDistance = ResolveVirtualOffsetSearchDistance(config, subdivisionLevel);
            if (geometry == null || !geometry.HasGeometry || searchDistance <= 0.000001f)
            {
                return false;
            }

            var bestOffset = Vector3.zero;
            var bestMagnitude = float.PositiveInfinity;
            for (var i = 0; i < geometry.colliders.Count; i++)
            {
                var collider = geometry.colliders[i];
                if (collider == null || !collider.enabled || !collider.bounds.Contains(position))
                {
                    continue;
                }

                var candidate = ResolveBoundsExitOffset(position, collider.bounds, geometryBias);
                var magnitude = candidate.sqrMagnitude;
                if (magnitude < bestMagnitude && magnitude <= searchDistance * searchDistance)
                {
                    bestMagnitude = magnitude;
                    bestOffset = candidate;
                }
            }

            for (var i = 0; i < geometry.renderers.Count; i++)
            {
                var renderer = geometry.renderers[i];
                if (renderer == null || !renderer.enabled || !renderer.bounds.Contains(position))
                {
                    continue;
                }

                var candidate = ResolveBoundsExitOffset(position, renderer.bounds, geometryBias);
                var magnitude = candidate.sqrMagnitude;
                if (magnitude < bestMagnitude && magnitude <= searchDistance * searchDistance)
                {
                    bestMagnitude = magnitude;
                    bestOffset = candidate;
                }
            }

            if (float.IsInfinity(bestMagnitude) || float.IsNaN(bestMagnitude))
            {
                return false;
            }

            offset = bestOffset;
            return offset.sqrMagnitude > 0.0000001f;
        }

        private static float ResolveVirtualOffsetSearchDistance(BurtXGIProbeBakingConfig config, int subdivisionLevel)
        {
            var brickSize = BurtXGIProbeBakingConfig.GetCellSizeInBricks(Mathf.Max(0, subdivisionLevel));
            var probeSpacing = brickSize * config.MinBrickSize / BurtGIVirtualProbePhysicalPool.BrickCellCount;
            return Mathf.Max(0f, config.virtualOffsetSearchMultiplier) * probeSpacing;
        }

        private static float ResolveVirtualOffsetGeometryBias(BurtXGIProbeBakingConfig config)
        {
            if (config == null)
            {
                return 0.001f;
            }

            return Mathf.Max(config.virtualOffsetOutOfGeoOffset, config.MinBrickSize * 0.001f);
        }

        private static Vector3 ResolveBoundsExitOffset(Vector3 position, Bounds bounds, float geometryBias)
        {
            var min = bounds.min;
            var max = bounds.max;
            var distances = new[]
            {
                new Vector4(-1f, 0f, 0f, Mathf.Max(0f, position.x - min.x)),
                new Vector4(1f, 0f, 0f, Mathf.Max(0f, max.x - position.x)),
                new Vector4(0f, -1f, 0f, Mathf.Max(0f, position.y - min.y)),
                new Vector4(0f, 1f, 0f, Mathf.Max(0f, max.y - position.y)),
                new Vector4(0f, 0f, -1f, Mathf.Max(0f, position.z - min.z)),
                new Vector4(0f, 0f, 1f, Mathf.Max(0f, max.z - position.z))
            };
            var best = distances[0];
            for (var i = 1; i < distances.Length; i++)
            {
                if (distances[i].w < best.w)
                {
                    best = distances[i];
                }
            }

            return new Vector3(best.x, best.y, best.z) * (best.w + Mathf.Max(0f, geometryBias));
        }

        private static int ResolveSkyVisibilitySampleCount(BurtXGIProbeBakingConfig config)
        {
            var requested = Mathf.Max(config.skyVisibilitySampleCountPerStep, Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(1, config.skyVisibilityBakingSamples))));
            return Mathf.Clamp(requested, 8, 128);
        }

        private static float ResolveSkyVisibilityRayDistance(BurtXGIProbeBakingConfig config, VirtualOffsetGeometry geometry)
        {
            return ResolveSkyVisibilityRayDistance(config, geometry, default, false);
        }

        private static float ResolveSkyVisibilityRayDistance(
            BurtXGIProbeBakingConfig config,
            VirtualOffsetGeometry geometry,
            Bounds requestBounds,
            bool hasRequestBounds)
        {
            var size = config.globalBounds.size.magnitude;
            if (hasRequestBounds)
            {
                size = requestBounds.size.magnitude;
            }
            else if (geometry != null && geometry.hasBounds)
            {
                size = Mathf.Max(size, geometry.bounds.size.magnitude);
            }

            return Mathf.Max(64f, size * 2f + config.skyVisibilityOffsetRay + config.MinBrickSize);
        }

        private static Vector4 BakeSkyVisibilityProbe(
            Vector3 position,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetGeometry geometry,
            int sampleCount,
            float maxRayDistance,
            out byte skyDirectionIndex)
        {
            skyDirectionIndex = 255;
            if (sampleCount <= 0 || geometry == null || !geometry.HasGeometry)
            {
                return new Vector4(1f / SHBasis0, 0f, 0f, 0f);
            }

            var visibleCount = 0;
            var visibleDirectionSum = Vector3.zero;
            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                var direction = GenerateSkyVisibilitySampleDirection(sampleIndex, sampleCount);
                if (!IsSkyDirectionOccluded(position, direction, config, geometry, maxRayDistance))
                {
                    visibleCount++;
                    visibleDirectionSum += direction;
                }
            }

            var visibility = Mathf.Clamp01(visibleCount / (float)sampleCount);
            if (visibleCount > 0 && visibleDirectionSum.sqrMagnitude > 0.000001f)
            {
                skyDirectionIndex = EncodeDefaultSkyDirectionIndex(visibleDirectionSum.normalized);
            }

            var anisotropy = Vector3.zero;
            if (visibleCount > 0 && visibility < 0.999f && visibility > 0.001f)
            {
                anisotropy = visibleDirectionSum.normalized * (visibility * (1f - visibility) * 0.5f);
            }

            return new Vector4(
                visibility / SHBasis0,
                anisotropy.x / SHBasis1,
                anisotropy.y / SHBasis1,
                anisotropy.z / SHBasis1);
        }

        private static Vector3 GenerateSkyVisibilitySampleDirection(int sampleIndex, int sampleCount)
        {
            var t = (sampleIndex + 0.5f) / Mathf.Max(1f, sampleCount);
            var y = t;
            var radius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            var phi = sampleIndex * 2.39996323f;
            return new Vector3(Mathf.Cos(phi) * radius, y, Mathf.Sin(phi) * radius).normalized;
        }

        private static bool IsSkyDirectionOccluded(
            Vector3 position,
            Vector3 direction,
            BurtXGIProbeBakingConfig config,
            VirtualOffsetGeometry geometry,
            float maxRayDistance)
        {
            var offset = Mathf.Max(0f, config.skyVisibilityOffsetRay);
            var origin = position + direction * offset;
            for (var i = 0; i < geometry.colliders.Count; i++)
            {
                var collider = geometry.colliders[i];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (collider.bounds.Contains(origin) ||
                    collider.Raycast(new Ray(origin, direction), out _, maxRayDistance))
                {
                    return true;
                }
            }

            for (var i = 0; i < geometry.renderers.Count; i++)
            {
                var renderer = geometry.renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                if (renderer.bounds.Contains(origin) || RayIntersectsBounds(origin, direction, renderer.bounds, maxRayDistance))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool RayIntersectsBounds(Vector3 origin, Vector3 direction, Bounds bounds, float maxDistance)
        {
            var tMin = 0f;
            var tMax = maxDistance;
            if (!IntersectSlab(origin.x, direction.x, bounds.min.x, bounds.max.x, ref tMin, ref tMax)) return false;
            if (!IntersectSlab(origin.y, direction.y, bounds.min.y, bounds.max.y, ref tMin, ref tMax)) return false;
            if (!IntersectSlab(origin.z, direction.z, bounds.min.z, bounds.max.z, ref tMin, ref tMax)) return false;
            return tMax >= 0f && tMin <= maxDistance;
        }

        private static bool IntersectSlab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(direction) <= 0.000001f)
            {
                return origin >= min && origin <= max;
            }

            var invDirection = 1f / direction;
            var near = (min - origin) * invDirection;
            var far = (max - origin) * invDirection;
            if (near > far)
            {
                var tmp = near;
                near = far;
                far = tmp;
            }

            tMin = Mathf.Max(tMin, near);
            tMax = Mathf.Min(tMax, far);
            return tMin <= tMax;
        }

        private static byte EncodeDefaultSkyDirectionIndex(Vector3 direction)
        {
            if (direction.sqrMagnitude <= 0.00000001f)
            {
                return 255;
            }

            direction.Normalize();
            var directions = GetDefaultXGISkyShadingDirections();
            var bestIndex = 255;
            var bestDot = -10f;
            for (var index = 0; index < directions.Length; index++)
            {
                var dot = Vector3.Dot(direction, directions[index]);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    bestIndex = index;
                }
            }

            return (byte)Mathf.Clamp(bestIndex, 0, 254);
        }

        internal static Vector3[] CreateDefaultXGISkyShadingDirections()
        {
            var directions = new Vector3[XGISkyPrecomputedDirectionCount];
            var sqrtDirectionCount = Mathf.Sqrt(XGISkyPrecomputedDirectionCount);
            var phi = 0f;
            for (var index = 0; index < XGISkyPrecomputedDirectionCount; index++)
            {
                var h = -1f + 2f * index / (XGISkyPrecomputedDirectionCount - 1f);
                var theta = Mathf.Acos(h);
                if (index == 0 || index == XGISkyPrecomputedDirectionCount - 1)
                {
                    phi = 0f;
                }
                else
                {
                    phi += 3.6f / sqrtDirectionCount / Mathf.Sqrt(1f - h * h);
                }

                var candidate = new Vector3(
                    Mathf.Sin(theta) * Mathf.Cos(phi),
                    Mathf.Sin(theta) * Mathf.Sin(phi),
                    Mathf.Cos(theta));
                candidate.Normalize();
                directions[index] = candidate;
            }

            return directions;
        }

        private static Vector3[] GetDefaultXGISkyShadingDirections()
        {
            return defaultSkyShadingDirections ??= CreateDefaultXGISkyShadingDirections();
        }

        private static int CellPositionToIndex(Vector3Int cellPosition, Vector3Int minCellPosition, Vector3Int cellCount)
        {
            var local = cellPosition - minCellPosition;
            return local.x + local.y * cellCount.x + local.z * cellCount.x * cellCount.y;
        }

        private static void AppendAdaptivePlacementBricks(
            BurtXGIProbeBakingConfig config,
            Vector3Int cellPosition,
            Bounds cellBounds,
            List<BurtGIProbeVolumeBounds> volumeBounds,
            int cellIndex,
            List<BurtXGIProbePlacedBrick> placedBricks,
            List<Vector3> probePositions)
        {
            var brickSubdivisionLevel = ResolvePlacementSubdivisionLevel(config, cellBounds, volumeBounds);
            var brickSizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(brickSubdivisionLevel);
            var bricksPerAxis = Mathf.Max(1, config.CellSizeInBricks / Mathf.Max(1, brickSizeInBricks));
            var cellMinBrick = cellPosition * config.CellSizeInBricks;
            for (var z = 0; z < bricksPerAxis; z++)
            {
                for (var y = 0; y < bricksPerAxis; y++)
                {
                    for (var x = 0; x < bricksPerAxis; x++)
                    {
                        var brickPosition = cellMinBrick + new Vector3Int(x, y, z) * brickSizeInBricks;
                        var brickBounds = CreateBrickBounds(config, brickPosition, brickSizeInBricks);
                        if (!cellBounds.Intersects(brickBounds) ||
                            !IsBrickAcceptedByAnyVolume(config, cellBounds, brickBounds, brickSubdivisionLevel, volumeBounds))
                        {
                            continue;
                        }

                        var brick = new BurtXGIProbePlacedBrick
                        {
                            position = brickPosition,
                            subdivisionLevel = brickSubdivisionLevel,
                            cellIndex = cellIndex
                        };
                        placedBricks.Add(brick);
                        AppendBrickProbePositions(config, brick, probePositions);
                    }
                }
            }
        }

        private static int CountPlacementCandidateBricksBySubdivision(
            BurtXGIProbeBakingConfig config,
            Vector3Int cellPosition,
            Bounds cellBounds,
            List<BurtGIProbeVolumeBounds> volumeBounds,
            int minSubdivision,
            int maxSubdivision,
            int[] countsBySubdivision)
        {
            var total = 0;
            minSubdivision = Mathf.Clamp(minSubdivision, 0, Mathf.Max(0, config.simplificationLevels));
            maxSubdivision = Mathf.Clamp(maxSubdivision, minSubdivision, Mathf.Max(0, config.simplificationLevels));
            var cellMinBrick = cellPosition * config.CellSizeInBricks;
            for (var subdivisionLevel = minSubdivision; subdivisionLevel <= maxSubdivision; subdivisionLevel++)
            {
                var brickSizeInBricks = BurtXGIProbeBakingConfig.GetCellSizeInBricks(subdivisionLevel);
                var bricksPerAxis = Mathf.Max(1, config.CellSizeInBricks / Mathf.Max(1, brickSizeInBricks));
                var count = 0;
                for (var z = 0; z < bricksPerAxis; z++)
                {
                    for (var y = 0; y < bricksPerAxis; y++)
                    {
                        for (var x = 0; x < bricksPerAxis; x++)
                        {
                            var brickPosition = cellMinBrick + new Vector3Int(x, y, z) * brickSizeInBricks;
                            var brickBounds = CreateBrickBounds(config, brickPosition, brickSizeInBricks);
                            if (!cellBounds.Intersects(brickBounds) ||
                                !IsBrickAcceptedByAnyVolume(config, cellBounds, brickBounds, subdivisionLevel, volumeBounds))
                            {
                                continue;
                            }

                            count++;
                        }
                    }
                }

                if (countsBySubdivision != null && subdivisionLevel < countsBySubdivision.Length)
                {
                    countsBySubdivision[subdivisionLevel] = count;
                }

                total += count;
            }

            return total;
        }

        private static void AppendPlacementGpuDiagnosticCapturePreview(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbePlacedCell sourceCell,
            Bounds fallbackBounds,
            XGIPlacementGpuDiagnosticCellResult cellResult,
            XGIPlacementGpuCapturePreview preview)
        {
            if (config == null || preview == null)
            {
                return;
            }

            var readbackBricks = cellResult.readbackBricks;
            if (readbackBricks == null || readbackBricks.Count == 0)
            {
                preview.skippedEmptyCells++;
                return;
            }

            var cellBounds = sourceCell.bounds;
            if (!IsValidBounds(cellBounds))
            {
                cellBounds = fallbackBounds;
            }

            var brickStart = preview.bricks.Count;
            var probeStart = preview.probePositions.Count;
            for (var i = 0; i < readbackBricks.Count; i++)
            {
                var brick = readbackBricks[i];
                brick.cellIndex = sourceCell.index;
                preview.bricks.Add(brick);
                AppendBrickProbePositions(config, brick, preview.probePositions);
            }

            var brickCount = preview.bricks.Count - brickStart;
            if (brickCount == 0)
            {
                preview.skippedEmptyCells++;
                return;
            }

            preview.cells.Add(new BurtXGIProbePlacedCell
            {
                index = sourceCell.index,
                position = sourceCell.position,
                bounds = cellBounds,
                brickStartIndex = brickStart,
                brickCount = brickCount,
                probeStartIndex = probeStart,
                probeCount = preview.probePositions.Count - probeStart,
                sceneGuids = CopySceneGuids(sourceCell.sceneGuids)
            });
        }

        private static string ResolvePlacementGpuCaptureBlocker(
            int requestedCellCount,
            int dispatchedCellCount,
            ulong gpuBrickTotal,
            int readbackBrickCount,
            int truncatedReadback,
            int fallbackSdfCellCount,
            XGIPlacementGpuCapturePreview preview,
            int expectedPreviewProbeCount)
        {
            if (requestedCellCount <= 0)
            {
                return "NoRequestedCells";
            }

            if (dispatchedCellCount != requestedCellCount)
            {
                return "IncompleteDispatch(" + dispatchedCellCount + "/" + requestedCellCount + ")";
            }

            if (preview == null)
            {
                return "MissingPreview";
            }

            if (preview.cells.Count != requestedCellCount)
            {
                return "IncompletePreviewCells(" + preview.cells.Count + "/" + requestedCellCount + ")";
            }

            if (preview.skippedEmptyCells > 0)
            {
                return "SkippedEmptyCells(" + preview.skippedEmptyCells + ")";
            }

            if (truncatedReadback > 0)
            {
                return "TruncatedReadback(" + truncatedReadback + ")";
            }

            if (fallbackSdfCellCount > 0)
            {
                return "FallbackSdf(" + fallbackSdfCellCount + ")";
            }

            if (gpuBrickTotal > int.MaxValue)
            {
                return "GpuBrickTotalOverflow(" + gpuBrickTotal + ")";
            }

            if ((int)gpuBrickTotal != readbackBrickCount ||
                readbackBrickCount != preview.bricks.Count)
            {
                return "BrickCountMismatch(Gpu=" + gpuBrickTotal + ",Readback=" + readbackBrickCount + ",Preview=" + preview.bricks.Count + ")";
            }

            if (preview.bricks.Count <= 0 || preview.probePositions.Count <= 0)
            {
                return "EmptyPreview";
            }

            if (preview.probePositions.Count != expectedPreviewProbeCount)
            {
                return "ProbeCountMismatch(Expected=" + expectedPreviewProbeCount + ",Preview=" + preview.probePositions.Count + ")";
            }

            return string.Empty;
        }

        private static void AppendBrickProbePositions(BurtXGIProbeBakingConfig config, BurtXGIProbePlacedBrick brick, List<Vector3> probePositions)
        {
            var scale = config.MinBrickSize / BurtGIVirtualProbePhysicalPool.BrickCellCount;
            var brickSize = BurtXGIProbeBakingConfig.GetCellSizeInBricks(brick.subdivisionLevel);
            var brickOffset = brick.position * BurtGIVirtualProbePhysicalPool.BrickCellCount;
            for (var z = 0; z < BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension; z++)
            {
                for (var y = 0; y < BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension; y++)
                {
                    for (var x = 0; x < BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension; x++)
                    {
                        var probeOffset = brickOffset + new Vector3Int(x, y, z) * brickSize;
                        probePositions.Add(config.probeOffset + (Vector3)probeOffset * scale);
                    }
                }
            }
        }

        private static int CountPlacementProbePositionsForBrickCount(int brickCount)
        {
            var probesPerBrick =
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
            return Mathf.Max(0, brickCount) * probesPerBrick;
        }

        private static Bounds CreateBrickBounds(BurtXGIProbeBakingConfig config, Vector3Int brickPosition, int brickSizeInBricks)
        {
            var min = config.probeOffset + (Vector3)brickPosition * config.MinBrickSize;
            var size = Vector3.one * (brickSizeInBricks * config.MinBrickSize);
            return new Bounds(min + size * 0.5f, size);
        }

        private static Vector3 FindNearestBoundsCenter(Vector3 position, List<Bounds> volumeBounds)
        {
            var nearestCenter = volumeBounds[0].center;
            var nearestDistance = float.PositiveInfinity;
            for (var i = 0; i < volumeBounds.Count; i++)
            {
                var bounds = volumeBounds[i];
                var closest = bounds.ClosestPoint(position);
                var distance = (closest - position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCenter = bounds.center;
                }
            }

            return nearestCenter;
        }

        private static float ResolveVirtualOffsetMagnitude(BurtXGIProbeBakingConfig config)
        {
            var searchLimit = Mathf.Max(0f, config.virtualOffsetSearchMultiplier) * config.MinBrickSize;
            if (searchLimit <= 0.000001f)
            {
                return 0f;
            }

            var requested = Mathf.Max(0f, config.virtualOffsetOutOfGeoOffset);
            if (requested <= 0.000001f)
            {
                requested = Mathf.Max(0f, -config.virtualOffsetRayOriginBias);
            }

            if (requested <= 0.000001f)
            {
                requested = config.MinBrickSize * 0.01f;
            }

            return Mathf.Min(requested, searchLimit);
        }

        private static Vector3[] ResolveEffectiveProbePositions(BurtXGIProbeBakingConfig config)
        {
            var baseProbePositions = config.bakedProbePositions ?? System.Array.Empty<Vector3>();
            var virtualOffsetProbePositions = config.bakedVirtualOffsetProbePositions ?? System.Array.Empty<Vector3>();
            if (virtualOffsetProbePositions.Length == baseProbePositions.Length && virtualOffsetProbePositions.Length > 0)
            {
                return virtualOffsetProbePositions;
            }

            return baseProbePositions;
        }

        private static List<XGIBakingProbeBatch> BuildBakingProbeBatches(
            BurtXGIProbeBakingConfig config,
            Vector3[] probePositions,
            int maxProbeCountPerBatch)
        {
            var batches = new List<XGIBakingProbeBatch>();
            if (probePositions == null || probePositions.Length == 0)
            {
                return batches;
            }

            var cells = config.bakedPlacedCells ?? System.Array.Empty<BurtXGIProbePlacedCell>();
            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (!TryResolveBakingProbeRange(cell.probeStartIndex, cell.probeCount, probePositions.Length, out var probeStartIndex, out var probeCount))
                {
                    continue;
                }

                var bounds = cell.bounds;
                var hasBounds = IsValidBounds(bounds);
                if (!hasBounds)
                {
                    bounds = BuildProbeRangeBounds(config, probePositions, probeStartIndex, probeCount);
                    hasBounds = IsValidBounds(bounds);
                }

                AddBakingProbeBatches(batches, probeStartIndex, probeCount, bounds, hasBounds, maxProbeCountPerBatch);
            }

            if (batches.Count == 0)
            {
                var bounds = BuildProbeRangeBounds(config, probePositions, 0, probePositions.Length);
                AddBakingProbeBatches(batches, 0, probePositions.Length, bounds, IsValidBounds(bounds), maxProbeCountPerBatch);
            }

            return batches;
        }

        private static bool TryResolveBakingProbeRange(
            int sourceStart,
            int sourceCount,
            int totalProbeCount,
            out int probeStartIndex,
            out int probeCount)
        {
            probeStartIndex = 0;
            probeCount = 0;
            if (totalProbeCount <= 0 || sourceStart < 0 || sourceStart >= totalProbeCount || sourceCount <= 0)
            {
                return false;
            }

            probeStartIndex = sourceStart;
            probeCount = Mathf.Min(sourceCount, totalProbeCount - sourceStart);
            return probeCount > 0;
        }

        private static void AddBakingProbeBatches(
            List<XGIBakingProbeBatch> batches,
            int probeStartIndex,
            int probeCount,
            Bounds bounds,
            bool hasBounds,
            int maxProbeCountPerBatch)
        {
            var remaining = probeCount;
            var start = probeStartIndex;
            var maxBatchSize = Mathf.Max(1, maxProbeCountPerBatch);
            while (remaining > 0)
            {
                var count = Mathf.Min(remaining, maxBatchSize);
                batches.Add(new XGIBakingProbeBatch
                {
                    probeStartIndex = start,
                    probeCount = count,
                    bounds = bounds,
                    hasBounds = hasBounds
                });

                start += count;
                remaining -= count;
            }
        }

        private static Bounds BuildProbeRangeBounds(
            BurtXGIProbeBakingConfig config,
            Vector3[] probePositions,
            int probeStartIndex,
            int probeCount)
        {
            var firstProbeIndex = Mathf.Clamp(probeStartIndex, 0, probePositions.Length - 1);
            var bounds = new Bounds(probePositions[firstProbeIndex], Vector3.zero);
            var probeEndIndex = Mathf.Min(probePositions.Length, probeStartIndex + Mathf.Max(0, probeCount));
            for (var i = probeStartIndex + 1; i < probeEndIndex; i++)
            {
                bounds.Encapsulate(probePositions[i]);
            }

            var padding = config != null ? config.MinBrickSize : 0.01f;
            bounds.Expand(Vector3.one * Mathf.Max(0.01f, padding));
            return bounds;
        }

        private static bool IsValidBounds(Bounds bounds)
        {
            return IsFinite(bounds.center) && IsFinite(bounds.size) && bounds.size.sqrMagnitude > 0.000001f;
        }

        private static float DistanceToBounds(Vector3 position, Bounds bounds)
        {
            var delta = position - bounds.center;
            var outside = new Vector3(
                Mathf.Max(Mathf.Abs(delta.x) - bounds.extents.x, 0f),
                Mathf.Max(Mathf.Abs(delta.y) - bounds.extents.y, 0f),
                Mathf.Max(Mathf.Abs(delta.z) - bounds.extents.z, 0f));
            return outside.magnitude;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool HasProbeRange<T>(T[] values, int expectedCount)
        {
            return values != null && values.Length >= expectedCount;
        }

        private static bool HasProbeRange<T>(T[] values, int start, int count)
        {
            return values != null && start >= 0 && count >= 0 && start + count <= values.Length;
        }

        private static int ResolveMinSubdivision(BurtXGIProbeBakingConfig config, BurtXGIProbePlacedCell cell)
        {
            var bricks = config.bakedPlacedBricks ?? System.Array.Empty<BurtXGIProbePlacedBrick>();
            var minSubdivision = config.BakedSimplificationLevels;
            var end = Mathf.Min(bricks.Length, cell.brickStartIndex + cell.brickCount);
            for (var i = Mathf.Max(0, cell.brickStartIndex); i < end; i++)
            {
                minSubdivision = Mathf.Min(minSubdivision, bricks[i].subdivisionLevel);
            }

            return minSubdivision;
        }

        private static BurtXGIProbeBakedDataAsset CreateOrLoadBakedDataAsset(BurtXGIProbeBakingConfig config)
        {
            var configPath = AssetDatabase.GetAssetPath(config);
            var directory = string.IsNullOrEmpty(configPath) ? "Assets/BurtRP/Generated/XGI" : System.IO.Path.GetDirectoryName(configPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
            {
                directory = "Assets/BurtRP/Generated/XGI";
            }

            EnsureAssetFolder(directory);
            var sliceSuffix = config.useTimeSliceData
                ? "_" + BurtGIProbeTimeSliceUtility.ToXRenderName(config.timeSliceType)
                : string.Empty;
            var assetPath = directory + "/" + config.name + sliceSuffix + "_BakedData.asset";
            var asset = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakedDataAsset>(assetPath);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<BurtXGIProbeBakedDataAsset>();
            AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath(assetPath));
            return asset;
        }

        private static void RefreshSerializedAssetSceneBindings(BurtXGIProbeBakingConfig config, BurtXGIProbeBakedDataAsset asset)
        {
            if (config == null || asset == null)
            {
                return;
            }

            var refreshedAnyScene = false;
            if (asset.perSceneCellLists != null)
            {
                for (var index = 0; index < asset.perSceneCellLists.Count; index++)
                {
                    var entry = asset.perSceneCellLists[index];
                    if (entry == null || string.IsNullOrEmpty(entry.sceneGuid) ||
                        !TryFindLoadedSceneByGuid(entry.sceneGuid, out var scene))
                    {
                        continue;
                    }

                    BurtXGIProbeSceneBindingUtility.CreateOrRefresh(scene, config, asset, false, false);
                    AssignSerializedAssetToSceneStreamers(config, asset, scene, entry.sceneGuid);
                    refreshedAnyScene = true;
                }
            }

            if (refreshedAnyScene)
            {
                return;
            }

            var activeScene = SceneManager.GetActiveScene();
            BurtXGIProbeSceneBindingUtility.CreateOrRefresh(activeScene, config, asset, false, false);
            AssignSerializedAssetToSceneStreamers(config, asset, activeScene, config.sceneGuid);
        }

        private static bool TryFindLoadedSceneByGuid(string sceneGuid, out Scene scene)
        {
            scene = default;
            if (string.IsNullOrEmpty(sceneGuid))
            {
                return false;
            }

            for (var index = 0; index < SceneManager.sceneCount; index++)
            {
                var candidate = SceneManager.GetSceneAt(index);
                if (!candidate.IsValid() || !candidate.isLoaded || string.IsNullOrEmpty(candidate.path))
                {
                    continue;
                }

                var candidateGuid = AssetDatabase.AssetPathToGUID(candidate.path);
                if (string.Equals(candidateGuid, sceneGuid, System.StringComparison.OrdinalIgnoreCase))
                {
                    scene = candidate;
                    return true;
                }
            }

            return false;
        }

        private static void AssignSerializedAssetToSceneStreamers(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedDataAsset asset,
            Scene scene,
            string sceneGuid)
        {
            if (config == null || asset == null || !scene.IsValid())
            {
                return;
            }

            var streamers = Object.FindObjectsOfType<BurtGIVirtualProbeCellStreamer>(true);
            for (var i = 0; i < streamers.Length; i++)
            {
                var streamer = streamers[i];
                if (streamer == null || streamer.gameObject.scene != scene)
                {
                    continue;
                }

                if (asset.hasTimeSliceSH)
                {
                    RegisterTimeSliceBakedDataAsset(streamer, asset);
                    if (streamer.bakedDataAsset == null || asset.timeSliceType == BurtGIProbeTimeSlice.Day)
                    {
                        streamer.bakedDataAsset = asset;
                    }
                }
                else
                {
                    streamer.bakedDataAsset = asset;
                }

                streamer.streamingSceneGuid = !string.IsNullOrEmpty(sceneGuid) ? sceneGuid : config.sceneGuid;
                streamer.runtimePageTableEntryCount = Mathf.Max(1, asset.pageTableEntryCount);
                streamer.runtimeIndirectionEntryCount = Mathf.Max(1, asset.indirectionEntryCount);
                if (asset.chunkCount > 0 && streamer.physicalPool != null)
                {
                    streamer.physicalPool.chunkDimensions = asset.ResolvedPhysicalPoolChunkDimensions;
                }

                if (streamer.physicalPool != null)
                {
                    if (asset.HasBakedValidityData)
                    {
                        streamer.physicalPool.allocateValidity = true;
                    }

                    if (asset.HasBakedSkyVisibilityData)
                    {
                        streamer.physicalPool.allocateSkyVisibility = true;
                    }

                    if (asset.HasBakedSkyShadingDirectionData)
                    {
                        streamer.physicalPool.allocateSkyShadingDirection = true;
                    }

                    if (asset.HasBakedL2Data)
                    {
                        streamer.physicalPool.allocateL2 = true;
                    }
                }

                if (streamer.probeVolume != null)
                {
                    streamer.probeVolume.useVirtualProbeData = true;
                    streamer.probeVolume.virtualIndirectionDimensions = Vector3Int.Max(Vector3Int.one, asset.virtualIndirectionDimensions);
                    streamer.probeVolume.virtualMinEntryIndex = asset.virtualMinEntryPosition;
                    streamer.probeVolume.virtualIndirectionEntrySize = asset.ResolvedCellSizeInMeters / Mathf.Max(1, asset.entriesPerCellDimension);
                    streamer.probeVolume.virtualMinBrickSize = asset.minBrickSize;
                    streamer.probeVolume.virtualPositionOffset = asset.probeOffset;
                    asset.ApplyRuntimeSettings(streamer.probeVolume);
                    streamer.probeVolume.useTimeSlice = asset.hasTimeSliceSH && !streamer.HasTimeSliceBakedDataAssets;
                    if (asset.hasTimeSliceSH && !streamer.HasTimeSliceBakedDataAssets)
                    {
                        streamer.probeVolume.timeSlice = asset.timeSliceType;
                    }
                    streamer.probeVolume.virtualTimeSliceMainLightIntensity = asset.timeSliceMainLightIntensity;
                }

                EditorUtility.SetDirty(streamer);
                if (streamer.physicalPool != null)
                {
                    EditorUtility.SetDirty(streamer.physicalPool);
                }

                if (streamer.probeVolume != null)
                {
                    EditorUtility.SetDirty(streamer.probeVolume);
                }
            }
        }

        private static void RegisterTimeSliceBakedDataAsset(BurtGIVirtualProbeCellStreamer streamer, BurtXGIProbeBakedDataAsset asset)
        {
            if (streamer == null || asset == null)
            {
                return;
            }

            streamer.timeSliceBakedDataAssets ??= new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();
            for (var i = 0; i < streamer.timeSliceBakedDataAssets.Count; i++)
            {
                var entry = streamer.timeSliceBakedDataAssets[i];
                if (entry == null || entry.timeSlice != asset.timeSliceType)
                {
                    continue;
                }

                entry.asset = asset;
                return;
            }

            streamer.timeSliceBakedDataAssets.Add(new BurtGIVirtualProbeCellStreamer.TimeSliceBakedData
            {
                timeSlice = asset.timeSliceType,
                asset = asset
            });
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            if (parts.Length == 0 || parts[0] != "Assets")
            {
                return;
            }

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static BurtXGIProbeBakedChunk BuildSerializedChunk(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeFinalizedCell cell,
            int cellChunkIndex,
            int physicalChunkIndex)
        {
            var chunkProbeCount = config.ChunkProbeCount;
            var chunk = new BurtXGIProbeBakedChunk
            {
                physicalChunkIndex = physicalChunkIndex,
                sharedPhysicalChunkIndex = physicalChunkIndex,
                validity = new byte[chunkProbeCount]
            };

            var hasTimeSliceSH = config.bakedUseTimeSlice && HasProbeRange(config.bakedTimeSliceSH, config.bakedProbeCount);
            if (hasTimeSliceSH)
            {
                chunk.l0L1Rx = new byte[chunkProbeCount * 8];
            }

            var hasL1 = config.systemParameters.shBands.HasL1();
            var hasL2 = config.systemParameters.shBands.HasL2();
            if (hasTimeSliceSH && hasL1)
            {
                chunk.l1GL1Ry = new byte[chunkProbeCount * 4];
                chunk.l1BL1Rz = new byte[chunkProbeCount * 4];
            }

            if (hasTimeSliceSH && hasL2)
            {
                chunk.l20 = new byte[chunkProbeCount * 4];
                chunk.l21 = new byte[chunkProbeCount * 4];
                chunk.l22 = new byte[chunkProbeCount * 4];
                chunk.l23 = new byte[chunkProbeCount * 4];
            }

            if (config.bakedSkyVisibility)
            {
                chunk.skyVisibilityL0L1 = new byte[chunkProbeCount * 8];
                if (config.bakedSkyShadingDirection)
                {
                    chunk.skyShadingDirectionIndices = new byte[chunkProbeCount];
                    System.Array.Fill(chunk.skyShadingDirectionIndices, (byte)255);
                }
            }

            var firstProbe = cell.probeStartIndex + cellChunkIndex * chunkProbeCount;
            var probesPerBrick = BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
            for (var brickIndexInChunk = 0; brickIndexInChunk < BurtGIVirtualProbePhysicalPool.BricksPerChunk; brickIndexInChunk++)
            {
                for (var probeIndexInBrick = 0; probeIndexInBrick < probesPerBrick; probeIndexInBrick++)
                {
                    var globalProbeIndex = firstProbe + brickIndexInChunk * probesPerBrick + probeIndexInBrick;
                    if (globalProbeIndex < cell.probeStartIndex || globalProbeIndex >= cell.probeStartIndex + cell.probeCount)
                    {
                        continue;
                    }

                    var localProbeIndex = GetChunkTextureLinearProbeIndex(brickIndexInChunk, probeIndexInBrick);
                    chunk.validity[localProbeIndex] = 255;
                    if (hasTimeSliceSH)
                    {
                        var sh = config.bakedTimeSliceSH != null && globalProbeIndex < config.bakedTimeSliceSH.Length
                            ? config.bakedTimeSliceSH[globalProbeIndex]
                            : default;
                        WriteHalf4(chunk.l0L1Rx, localProbeIndex, new Vector4(sh.c0.x, sh.c0.y, sh.c0.z, sh.c1.x));
                        if (hasL1)
                        {
                            WriteByte4(chunk.l1GL1Ry, localProbeIndex, new Vector4(sh.c1.y, sh.c2.x, sh.c2.y, sh.c1.z));
                            WriteByte4(chunk.l1BL1Rz, localProbeIndex, new Vector4(sh.c3.x, sh.c3.y, sh.c3.z, sh.c2.z));
                        }

                        if (hasL2)
                        {
                            WriteByte4(chunk.l20, localProbeIndex, new Vector4(sh.c4.x, sh.c5.x, sh.c6.x, sh.c7.x));
                            WriteByte4(chunk.l21, localProbeIndex, new Vector4(sh.c4.y, sh.c5.y, sh.c6.y, sh.c7.y));
                            WriteByte4(chunk.l22, localProbeIndex, new Vector4(sh.c4.z, sh.c5.z, sh.c6.z, sh.c7.z));
                            WriteByte4(chunk.l23, localProbeIndex, new Vector4(sh.c8.x, sh.c8.y, sh.c8.z, 0f));
                        }
                    }

                    if (config.bakedSkyVisibility && config.bakedSkyVisibilityL0L1 != null &&
                        globalProbeIndex < config.bakedSkyVisibilityL0L1.Length)
                    {
                        WriteHalf4(chunk.skyVisibilityL0L1, localProbeIndex, config.bakedSkyVisibilityL0L1[globalProbeIndex]);
                        if (config.bakedSkyShadingDirection && config.bakedSkyShadingDirectionIndices != null &&
                            globalProbeIndex < config.bakedSkyShadingDirectionIndices.Length)
                        {
                            chunk.skyShadingDirectionIndices[localProbeIndex] = config.bakedSkyShadingDirectionIndices[globalProbeIndex];
                        }
                    }
                }
            }

            return chunk;
        }

        private static int GetChunkTextureLinearProbeIndex(int brickIndexInChunk, int probeIndexInBrick)
        {
            var probeDim = BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
            var probeZ = probeIndexInBrick / (probeDim * probeDim);
            var probeY = (probeIndexInBrick - probeZ * probeDim * probeDim) / probeDim;
            var probeX = probeIndexInBrick - probeZ * probeDim * probeDim - probeY * probeDim;
            return brickIndexInChunk * probeDim + probeX +
                probeY * BurtGIVirtualProbePhysicalPool.ChunkWidth +
                probeZ * BurtGIVirtualProbePhysicalPool.ChunkWidth * BurtGIVirtualProbePhysicalPool.ChunkHeight;
        }

        private static void WriteHalf4(byte[] target, int probeIndex, Vector4 value)
        {
            if (target == null || target.Length < (probeIndex + 1) * 8)
            {
                return;
            }

            var offset = probeIndex * 8;
            WriteHalf(target, offset + 0, value.x);
            WriteHalf(target, offset + 2, value.y);
            WriteHalf(target, offset + 4, value.z);
            WriteHalf(target, offset + 6, value.w);
        }

        private static void WriteHalf(byte[] target, int offset, float value)
        {
            if (float.IsNaN(value))
            {
                value = 0f;
            }

            value = Mathf.Min(value, XRenderMaxHalfValue);
            var half = Mathf.FloatToHalf(value);
            target[offset] = (byte)(half & 0xff);
            target[offset + 1] = (byte)((half >> 8) & 0xff);
        }

        private static void WriteByte4(byte[] target, int probeIndex, Vector4 value)
        {
            if (target == null || target.Length < (probeIndex + 1) * 4)
            {
                return;
            }

            var offset = probeIndex * 4;
            target[offset + 0] = FloatToByte(value.x);
            target[offset + 1] = FloatToByte(value.y);
            target[offset + 2] = FloatToByte(value.z);
            target[offset + 3] = FloatToByte(value.w);
        }

        private static byte FloatToByte(float value)
        {
            if (float.IsNaN(value))
            {
                value = 0.5f;
            }

            return (byte)Mathf.RoundToInt(Mathf.Clamp01(value) * 255f);
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static string BuildPrepareReport(BurtXGIProbeBakingConfig config, PrepareResult result)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[BurtRP][XGIProbeBakingPrepare]");
            builder.AppendLine("Config=" + (config != null ? config.name : "None"));
            builder.AppendLine("Success=" + result.success);
            builder.AppendLine("ProbeVolumes=" + result.probeVolumeCount);
            if (!string.IsNullOrEmpty(result.error))
            {
                builder.AppendLine("Error=" + result.error);
            }

            if (result.probeVolumeCount > 0)
            {
                builder.AppendLine("BoundsCenter=" + FormatVector(result.globalBounds.center) + " BoundsSize=" + FormatVector(result.globalBounds.size));
                builder.AppendLine("CellRange=" + result.minCellPosition + ".." + result.maxCellPosition);
            }

            if (config != null)
            {
                builder.Append("Layout=CellSizeMeters=").Append(config.CellSizeInMeters.ToString("0.###"))
                    .Append(",BakedCellSizeMeters=").Append(config.BakedCellSizeInMeters.ToString("0.###"))
                    .Append(",ChunkProbes=").Append(config.ChunkProbeCount)
                    .Append(",GPUChunkBytes=").Append(config.ChunkGPUMemoryBytes)
                    .Append(",SkyVisibility=").Append(config.skyVisibility)
                    .Append(",TimeSlice=").Append(config.useTimeSliceData);
            }

            return builder.ToString();
        }

        private static string BuildPlacementReport(BurtXGIProbeBakingConfig config, PlacementResult result)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[BurtRP][XGIProbeBakingPlacementLiteAdaptive]");
            builder.AppendLine("Config=" + (config != null ? config.name : "None"));
            builder.AppendLine("Success=" + result.success);
            if (!string.IsNullOrEmpty(result.error))
            {
                builder.AppendLine("Error=" + result.error);
            }

            builder.AppendLine("Cells=" + result.cellCount + " Bricks=" + result.brickCount + " Probes=" + result.probeCount);
            builder.AppendLine("Backend=" + (!string.IsNullOrEmpty(result.backend) ? result.backend : "LiteCPU") +
                " GpuPlacement=" + (!string.IsNullOrEmpty(result.gpuPlacementStatus) ? result.gpuPlacementStatus : ResolvePlacementGpuStatusLabel()));
            if (!string.IsNullOrEmpty(result.gpuCullStatus))
            {
                builder.AppendLine("GpuCull=" + result.gpuCullStatus);
            }

            if (config != null)
            {
                builder.Append("CellRange=").Append(config.minCellPosition).Append("..").Append(config.maxCellPosition)
                    .Append(",CellSizeMeters=").Append(config.CellSizeInMeters.ToString("0.###"))
                    .Append(",MinBrickSizeMeters=").Append(config.MinBrickSize.ToString("0.###"))
                    .Append(",BakedCellSizeMeters=").Append(config.BakedCellSizeInMeters.ToString("0.###"))
                    .Append(",BakedMinBrickSizeMeters=").Append(config.BakedMinBrickSize.ToString("0.###"))
                    .Append(",BricksPerCellAxis=3")
                    .Append(",Subdivision=").Append(config.simplificationLevels);
            }

            return builder.ToString();
        }

        private static string BuildFinalizeCellsReport(BurtXGIProbeBakingConfig config, FinalizeCellsResult result)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[BurtRP][XGIProbeBakingFinalizeCellsLite]");
            builder.AppendLine("Config=" + (config != null ? config.name : "None"));
            builder.AppendLine("Success=" + result.success);
            if (!string.IsNullOrEmpty(result.error))
            {
                builder.AppendLine("Error=" + result.error);
            }

            builder.AppendLine("Cells=" + result.cellCount + " Finalized=" + result.finalizedCellCount + " Chunks=" + result.chunkCount);
            if (config != null)
            {
                builder.Append("BakedProbes=").Append(config.bakedProbeCount)
                    .Append(",VirtualOffsets=").Append(config.bakedVirtualOffsetCount)
                    .Append(",SkyVisibility=").Append(config.bakedSkyVisibilityCount)
                    .Append(",TimeSliceSH=").Append(config.bakedTimeSliceSHCount);
            }

            return builder.ToString();
        }

        private static string BuildSerializationReport(BurtXGIProbeBakingConfig config, SerializationResult result)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[BurtRP][XGIProbeBakingSerializationLite]");
            builder.AppendLine("Config=" + (config != null ? config.name : "None"));
            builder.AppendLine("Success=" + result.success);
            if (!string.IsNullOrEmpty(result.error))
            {
                builder.AppendLine("Error=" + result.error);
            }

            builder.AppendLine("Cells=" + result.cellCount + " Chunks=" + result.chunkCount);
            builder.AppendLine("PageTableEntries=" + result.pageTableEntryCount + " IndirectionEntries=" + result.indirectionEntryCount);
            if (result.asset != null)
            {
                builder.AppendLine("Asset=" + AssetDatabase.GetAssetPath(result.asset));
                builder.AppendLine("AssetTimeSlice=" + result.asset.timeSliceType +
                    " MainLight=" + result.asset.timeSliceMainLightIntensity.ToString("0.###"));
                var physicalPoolChunkDimensions = result.asset.ResolvedPhysicalPoolChunkDimensions;
                builder.AppendLine("PhysicalPoolChunkDimensions=" + physicalPoolChunkDimensions + " PhysicalPoolDimensions=" +
                    new Vector3Int(
                        physicalPoolChunkDimensions.x * BurtGIVirtualProbePhysicalPool.ChunkWidth,
                        physicalPoolChunkDimensions.y * BurtGIVirtualProbePhysicalPool.ChunkHeight,
                        physicalPoolChunkDimensions.z * BurtGIVirtualProbePhysicalPool.ChunkDepth));
            }

            if (config != null)
            {
                builder.Append("ChunkProbeCount=").Append(config.ChunkProbeCount)
                    .Append(",L0L1RxChunkBytes=").Append(config.L0L1RxChunkSize)
                    .Append(",L1ChunkBytes=").Append(config.L1ChunkSize)
                    .Append(",L2ChunkBytes=").Append(config.L2TextureChunkSize)
                    .Append(",SharedChunkBytes=").Append(config.SharedDataChunkSize);
            }

            return builder.ToString();
        }

        private static string BuildTimeSliceReport(BurtXGIProbeBakingConfig config, TimeSliceResult result)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[BurtRP][XGIProbeBakingTimeSlice]");
            builder.AppendLine("Config=" + (config != null ? config.name : "None"));
            builder.AppendLine("Success=" + result.success);
            if (!string.IsNullOrEmpty(result.error))
            {
                builder.AppendLine("Error=" + result.error);
            }

            builder.AppendLine("Backend=" + (!string.IsNullOrEmpty(result.backend) ? result.backend : "SceneLightCPU"));
            builder.AppendLine("Enabled=" + result.enabled + " TimeSlice=" + result.timeSlice);
            builder.AppendLine("Probes=" + result.probeCount + " SH=" + result.shCount + " Lights=" + result.lightCount + " ShadowedSamples=" + result.shadowedSampleCount + " Batches=" + result.batchCount);
            if (config != null)
            {
                builder.Append("Samples=").Append(config.timeSliceBakingSamples)
                    .Append(",Bounces=").Append(config.timeSliceBakingBounces)
                    .Append(",SamplesPerStep=").Append(ResolveTimeSliceSampleCountPerStep(config))
                    .Append(",OffsetRay=").Append(config.timeSliceOffsetRay.ToString("0.###"))
                    .Append(",MainLightSHIntensity=").Append(config.mainLightSHIntensity.ToString("0.###"));
            }

            return builder.ToString();
        }

        private static string BuildSkyVisibilityReport(BurtXGIProbeBakingConfig config, SkyVisibilityResult result)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[BurtRP][XGIProbeBakingSkyVisibility]");
            builder.AppendLine("Config=" + (config != null ? config.name : "None"));
            builder.AppendLine("Success=" + result.success);
            if (!string.IsNullOrEmpty(result.error))
            {
                builder.AppendLine("Error=" + result.error);
            }

            builder.AppendLine("Backend=" + (!string.IsNullOrEmpty(result.backend) ? result.backend : "LiteCPU"));
            builder.AppendLine("Enabled=" + result.enabled + " ShadingDirection=" + result.shadingDirection);
            builder.AppendLine("Probes=" + result.probeCount + " SkyVisibilityL0L1=" + result.occlusionCount + " DirectionIndices=" + result.directionCount);
            if (config != null)
            {
                builder.Append("Samples=").Append(config.skyVisibilityBakingSamples)
                    .Append(",Bounces=").Append(config.skyVisibilityBakingBounces)
                    .Append(",AverageAlbedo=").Append(config.skyVisibilityAverageAlbedo.ToString("0.###"))
                    .Append(",OffsetRay=").Append(config.skyVisibilityOffsetRay.ToString("0.###"));
            }

            return builder.ToString();
        }

        private static string BuildVirtualOffsetReport(BurtXGIProbeBakingConfig config, VirtualOffsetResult result)
        {
            var builder = new StringBuilder(1024);
            builder.AppendLine("[BurtRP][XGIProbeBakingVirtualOffset]");
            builder.AppendLine("Config=" + (config != null ? config.name : "None"));
            builder.AppendLine("Success=" + result.success);
            if (!string.IsNullOrEmpty(result.error))
            {
                builder.AppendLine("Error=" + result.error);
            }

            builder.AppendLine("Backend=" + (!string.IsNullOrEmpty(result.backend) ? result.backend : "LiteCPU"));
            builder.AppendLine("Enabled=" + result.enabled + " Applied=" + result.applied);
            builder.AppendLine("Probes=" + result.probeCount + " Offsets=" + result.offsetCount + " AdjustedProbes=" + result.invalidCount);
            if (config != null)
            {
                builder.Append("SearchMultiplier=").Append(config.virtualOffsetSearchMultiplier.ToString("0.###"))
                    .Append(",ValidityThreshold=").Append(config.virtualOffsetValidityThreshold.ToString("0.###"))
                    .Append(",OutOfGeoOffset=").Append(config.virtualOffsetOutOfGeoOffset.ToString("0.###"))
                    .Append(",RayOriginBias=").Append(config.virtualOffsetRayOriginBias.ToString("0.###"));
            }

            return builder.ToString();
        }

        private static string FormatVector(Vector3 value)
        {
            return "(" + value.x.ToString("0.###") + "," + value.y.ToString("0.###") + "," + value.z.ToString("0.###") + ")";
        }
    }
}
