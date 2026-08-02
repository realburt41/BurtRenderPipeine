using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtGIProbeDebugDrawPass : BurtRenderPass
    {
        private const int MaxInstancesPerDraw = 1023;
        private const int ProbesPerBrick = BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
            BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension *
            BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
        private const string ProbeShaderName = "Hidden/XRender/XGI/XGIProbe/Debug/DrawProbes";
        private const string VirtualOffsetShaderName = "Hidden/XRender/XGI/XGIProbe/Debug/DrawProbesVirtualOffset";
        private static readonly int TintId = Shader.PropertyToID("_BurtXGICompatTint");
        private static readonly int ZTestId = Shader.PropertyToID("_BurtXGICompatZTest");
        private static readonly int DebugLayerId = Shader.PropertyToID("_BurtXGICompatDebugLayer");
        private static readonly int InstanceColorId = Shader.PropertyToID("_BurtXGICompatInstanceColor");
        private static readonly int ProbeAtlasIndexId = Shader.PropertyToID("_BurtXGICompatProbeAtlasIndex");
        private static readonly List<BurtGIVirtualProbeCellStreamer> ActiveStreamers = new List<BurtGIVirtualProbeCellStreamer>();
        private static readonly Matrix4x4[] InstanceMatrices = new Matrix4x4[MaxInstancesPerDraw];
        private static readonly Vector4[] InstanceColors = new Vector4[MaxInstancesPerDraw];
        private static readonly Vector4[] InstanceProbeAtlasIndices = new Vector4[MaxInstancesPerDraw];
        private static readonly MaterialPropertyBlock InstanceProperties = new MaterialPropertyBlock();
        private static Mesh probeMesh;
        private static Mesh virtualOffsetMesh;
        private static Material probeMaterial;
        private static Material virtualOffsetMaterial;
        private static bool hasLoggedMissingProbeShader;
        private static bool hasLoggedMissingVirtualOffsetShader;
        private static DebugDrawStats lastStats;

        public override string Name => "Burt XGI Probe Debug Draw";

        private struct DebugDrawStats
        {
            internal int FrameIndex;
            internal bool Requested;
            internal bool DrawProbes;
            internal bool DrawVirtualOffsets;
            internal bool DepthTest;
            internal BurtXGIToolsProbeDebugLayer DebugLayer;
            internal int ActiveStreamerCount;
            internal int ConsideredStreamerCount;
            internal int LoadedCellCount;
            internal int ProbeInstanceCount;
            internal int VirtualOffsetInstanceCount;
            internal int AtlasMappedProbeCount;
            internal int AtlasFallbackProbeCount;
            internal bool HasVirtualReady;
            internal bool HasL0L1;
            internal bool HasL1;
            internal bool HasL2;
            internal bool HasValidity;
            internal bool HasSkyVisibility;
            internal bool HasSkyDirection;
            internal string Status;
        }

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!ShouldUse(builder.Request))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.ReadCameraColor();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !ShouldUse(context.Request))
            {
                StoreStats(BurtXGIToolsDebugComponent.Current, "Skipped");
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var colorTarget = context.CameraColorTarget;
            var depthTarget = context.CameraDepthTarget;
            var debugComponent = BurtXGIToolsDebugComponent.Current;
            if (camera == null || debugComponent == null || !colorTarget.IsValid || !depthTarget.IsValid)
            {
                StoreStats(debugComponent, "InvalidTarget");
                return;
            }

            debugComponent.OnAfterDeserialize();
            var stats = CreateStats(debugComponent, "Begin");
            if (BurtGIVirtualProbeCellStreamer.CopyActiveStreamers(ActiveStreamers) <= 0)
            {
                stats.Status = "NoActiveStreamer";
                lastStats = stats;
                return;
            }

            stats.ActiveStreamerCount = ActiveStreamers.Count;
            Material probeDrawMaterial = null;
            Material virtualOffsetDrawMaterial = null;
            var drawProbes = debugComponent.drawProbes && TryGetProbeMaterial(out probeDrawMaterial);
            var drawVirtualOffsets = debugComponent.drawVirtualOffset && TryGetVirtualOffsetMaterial(out virtualOffsetDrawMaterial);
            stats.DrawProbes = drawProbes;
            stats.DrawVirtualOffsets = drawVirtualOffsets;
            if (!drawProbes && !drawVirtualOffsets)
            {
                stats.Status = "MissingMaterial";
                lastStats = stats;
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(colorTarget.Identifier, depthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            var zTest = debugComponent.drawProbesDepthTest ? (int)CompareFunction.LessEqual : (int)CompareFunction.Always;
            if (drawProbes)
            {
                probeDrawMaterial.SetColor(TintId, Color.white);
                probeDrawMaterial.SetInt(ZTestId, zTest);
            }

            if (drawVirtualOffsets)
            {
                virtualOffsetDrawMaterial.SetColor(TintId, new Color(1f, 0.6f, 0.16f, 0.72f));
                virtualOffsetDrawMaterial.SetInt(ZTestId, zTest);
            }

            var cameraPosition = camera.transform.position;
            var cullingDistanceSqr = ResolveDistanceSqr(debugComponent.drawProbeCullingDistance);
            for (var streamerIndex = 0; streamerIndex < ActiveStreamers.Count; streamerIndex++)
            {
                var streamer = ActiveStreamers[streamerIndex];
                var asset = streamer != null ? streamer.ActiveBakedDataAsset : null;
                var config = asset != null ? asset.sourceConfig : null;
                var cells = asset != null ? asset.cells : null;
                var probePositions = config != null ? config.bakedProbePositions : null;
                if (streamer == null || asset == null || cells == null || probePositions == null || probePositions.Length == 0)
                {
                    continue;
                }

                stats.ConsideredStreamerCount++;
                stats.LoadedCellCount += streamer.LoadedCellCount;
                AccumulateVolumeStats(streamer.probeVolume, ref stats);
                var probeVolumeBound = BurtGIProbeVolumeUtility.UploadForDebug(cmd, streamer.probeVolume, context.Request, context.Asset);
                cmd.SetGlobalInt(DebugLayerId, probeVolumeBound ? (int)debugComponent.drawProbesDebugLayer : -1);
                if (drawProbes)
                {
                    stats.ProbeInstanceCount += DrawProbeInstances(cmd, streamer, asset, config, cells, probePositions, debugComponent, cameraPosition, cullingDistanceSqr, probeDrawMaterial, ref stats);
                }

                if (drawVirtualOffsets)
                {
                    var adjustedProbePositions = config.bakedVirtualOffsetProbePositions;
                    var virtualOffsets = config.bakedVirtualOffsets;
                    stats.VirtualOffsetInstanceCount += DrawVirtualOffsetInstances(
                        cmd,
                        streamer,
                        asset,
                        cells,
                        probePositions,
                        adjustedProbePositions,
                        virtualOffsets,
                        debugComponent,
                        cameraPosition,
                        cullingDistanceSqr,
                        virtualOffsetDrawMaterial);
                }
            }

            context.ExecuteLegacyCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            stats.Status = stats.ProbeInstanceCount > 0 || stats.VirtualOffsetInstanceCount > 0 ? "OK" : "NoVisibleInstances";
            lastStats = stats;
        }

        internal static bool ShouldUse(BurtRenderRequest request)
        {
            if (request == null || !request.IsValid)
            {
                return false;
            }

            var debugComponent = BurtXGIToolsDebugComponent.Current;
            return debugComponent != null && (debugComponent.drawProbes || debugComponent.drawVirtualOffset);
        }

        internal static string GetDebugStatus()
        {
            var debugComponent = BurtXGIToolsDebugComponent.Current;
            if (debugComponent == null)
            {
                return "Disabled(Component=<none>)";
            }

            var stats = lastStats;
            var requested = debugComponent.drawProbes || debugComponent.drawVirtualOffset;
            var builder = new StringBuilder(256);
            builder.Append(requested ? "Requested" : "Disabled");
            builder.Append("(DrawProbes=").Append(debugComponent.drawProbes);
            builder.Append(",DrawVirtualOffset=").Append(debugComponent.drawVirtualOffset);
            builder.Append(",Layer=").Append(debugComponent.drawProbesDebugLayer);
            builder.Append(",DepthTest=").Append(debugComponent.drawProbesDepthTest);
            builder.Append(",LastFrame=").Append(stats.FrameIndex);
            builder.Append(",LastStatus=").Append(string.IsNullOrEmpty(stats.Status) ? "<none>" : stats.Status);
            builder.Append(",LastRequested=").Append(stats.Requested);
            builder.Append(",LastDrawProbes=").Append(stats.DrawProbes);
            builder.Append(",LastDrawVirtualOffset=").Append(stats.DrawVirtualOffsets);
            builder.Append(",LastLayer=").Append(stats.DebugLayer);
            builder.Append(",LastDepthTest=").Append(stats.DepthTest);
            builder.Append(",ActiveStreamers=").Append(stats.ActiveStreamerCount);
            builder.Append(",ConsideredStreamers=").Append(stats.ConsideredStreamerCount);
            builder.Append(",LoadedCells=").Append(stats.LoadedCellCount);
            builder.Append(",ProbeInstances=").Append(stats.ProbeInstanceCount);
            builder.Append(",VirtualOffsetInstances=").Append(stats.VirtualOffsetInstanceCount);
            builder.Append(",AtlasMappedProbes=").Append(stats.AtlasMappedProbeCount);
            builder.Append(",AtlasFallbackProbes=").Append(stats.AtlasFallbackProbeCount);
            builder.Append(",VirtualReady=").Append(stats.HasVirtualReady);
            builder.Append(",L0L1=").Append(stats.HasL0L1);
            builder.Append(",L1=").Append(stats.HasL1);
            builder.Append(",L2=").Append(stats.HasL2);
            builder.Append(",Validity=").Append(stats.HasValidity);
            builder.Append(",SkyVisibility=").Append(stats.HasSkyVisibility);
            builder.Append(",SkyDirection=").Append(stats.HasSkyDirection);
            builder.Append(')');
            return builder.ToString();
        }

        private static DebugDrawStats CreateStats(BurtXGIToolsDebugComponent debugComponent, string status)
        {
            return new DebugDrawStats
            {
                FrameIndex = Time.frameCount,
                Requested = debugComponent != null && (debugComponent.drawProbes || debugComponent.drawVirtualOffset),
                DrawProbes = debugComponent != null && debugComponent.drawProbes,
                DrawVirtualOffsets = debugComponent != null && debugComponent.drawVirtualOffset,
                DepthTest = debugComponent != null && debugComponent.drawProbesDepthTest,
                DebugLayer = debugComponent != null ? debugComponent.drawProbesDebugLayer : BurtXGIToolsProbeDebugLayer.Visibility,
                Status = status
            };
        }

        private static void StoreStats(BurtXGIToolsDebugComponent debugComponent, string status)
        {
            lastStats = CreateStats(debugComponent, status);
        }

        private static void AccumulateVolumeStats(BurtGIProbeVolume volume, ref DebugDrawStats stats)
        {
            if (volume == null)
            {
                return;
            }

            stats.HasVirtualReady |= volume.IsVirtualReady;
            stats.HasL0L1 |= volume.IsVirtualReady;
            stats.HasL1 |= volume.HasVirtualL1;
            stats.HasL2 |= volume.HasVirtualL2;
            stats.HasValidity |= volume.HasVirtualValidity;
            stats.HasSkyVisibility |= volume.HasVirtualSkyVisibility;
            stats.HasSkyDirection |= volume.HasVirtualSkyShadingDirection;
        }

        private static int DrawProbeInstances(
            CommandBuffer cmd,
            BurtGIVirtualProbeCellStreamer streamer,
            BurtXGIProbeBakedDataAsset asset,
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedCellData[] cells,
            Vector3[] probePositions,
            BurtXGIToolsDebugComponent debugComponent,
            Vector3 cameraPosition,
            float cullingDistanceSqr,
            Material material,
            ref DebugDrawStats stats)
        {
            var mesh = GetProbeMesh();
            if (mesh == null)
            {
                return 0;
            }

            var count = 0;
            var totalCount = 0;
            var scale = Mathf.Max(0.001f, debugComponent.drawProbeSize);
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var cell = cells[cellIndex];
                if (!ShouldDrawCell(streamer, cell) ||
                    !TryResolveProbeRange(cell, probePositions.Length, out var start, out var probeCount))
                {
                    continue;
                }

                TryResolvePlacedCell(config, cell.cellIndex, out var placedCell);
                var end = start + probeCount;
                for (var probeIndex = start; probeIndex < end; probeIndex++)
                {
                    var position = probePositions[probeIndex];
                    if ((position - cameraPosition).sqrMagnitude > cullingDistanceSqr)
                    {
                        continue;
                    }

                    var subdivisionLevel = ResolveProbeSubdivisionLevel(config, cell, placedCell, probeIndex);
                    if (!IsSubdivisionVisible(subdivisionLevel, debugComponent))
                    {
                        continue;
                    }

                    InstanceMatrices[count++] = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * scale);
                    InstanceColors[count - 1] = ResolveProbeInstanceColor(debugComponent.drawProbesDebugLayer, subdivisionLevel, asset);
                    InstanceProbeAtlasIndices[count - 1] = ResolveProbeAtlasIndex(streamer, cell, probeIndex, subdivisionLevel);
                    if (InstanceProbeAtlasIndices[count - 1].w >= -0.5f)
                    {
                        stats.AtlasMappedProbeCount++;
                    }
                    else
                    {
                        stats.AtlasFallbackProbeCount++;
                    }
                    totalCount++;
                    if (count >= MaxInstancesPerDraw)
                    {
                        DrawBatch(cmd, mesh, material, count, true);
                        count = 0;
                    }
                }
            }

            DrawBatch(cmd, mesh, material, count, true);
            return totalCount;
        }

        private static int DrawVirtualOffsetInstances(
            CommandBuffer cmd,
            BurtGIVirtualProbeCellStreamer streamer,
            BurtXGIProbeBakedDataAsset asset,
            BurtXGIProbeBakedCellData[] cells,
            Vector3[] probePositions,
            Vector3[] adjustedProbePositions,
            Vector3[] virtualOffsets,
            BurtXGIToolsDebugComponent debugComponent,
            Vector3 cameraPosition,
            float cullingDistanceSqr,
            Material material)
        {
            var mesh = GetVirtualOffsetMesh();
            if (mesh == null)
            {
                return 0;
            }

            var count = 0;
            var totalCount = 0;
            var width = Mathf.Max(0.001f, debugComponent.drawVirtualOffsetSize);
            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var cell = cells[cellIndex];
                if (!ShouldDrawCell(streamer, cell) ||
                    !TryResolveProbeRange(cell, probePositions.Length, out var start, out var probeCount))
                {
                    continue;
                }

                var end = start + probeCount;
                for (var probeIndex = start; probeIndex < end; probeIndex++)
                {
                    var basePosition = probePositions[probeIndex];
                    if ((basePosition - cameraPosition).sqrMagnitude > cullingDistanceSqr ||
                        !TryResolveAdjustedProbePosition(probeIndex, basePosition, adjustedProbePositions, virtualOffsets, out var adjustedPosition))
                    {
                        continue;
                    }

                    var offset = adjustedPosition - basePosition;
                    var length = offset.magnitude;
                    if (length <= 0.0001f)
                    {
                        continue;
                    }

                    var rotation = Quaternion.LookRotation(offset.normalized);
                    InstanceMatrices[count++] = Matrix4x4.TRS(basePosition, rotation, new Vector3(width, width, length));
                    totalCount++;
                    if (count >= MaxInstancesPerDraw)
                    {
                        DrawBatch(cmd, mesh, material, count, false);
                        count = 0;
                    }
                }
            }

            DrawBatch(cmd, mesh, material, count, false);
            return totalCount;
        }

        private static bool ShouldDrawCell(BurtGIVirtualProbeCellStreamer streamer, BurtXGIProbeBakedCellData cell)
        {
            return streamer != null && cell != null &&
                streamer.IsCellLoaded(cell.cellIndex);
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

        private static void DrawBatch(CommandBuffer cmd, Mesh mesh, Material material, int count, bool useInstanceColors)
        {
            if (count <= 0 || cmd == null || mesh == null || material == null)
            {
                return;
            }

            if (useInstanceColors)
            {
                InstanceProperties.Clear();
                InstanceProperties.SetVectorArray(InstanceColorId, InstanceColors);
                InstanceProperties.SetVectorArray(ProbeAtlasIndexId, InstanceProbeAtlasIndices);
                cmd.DrawMeshInstanced(mesh, 0, material, 0, InstanceMatrices, count, InstanceProperties);
                return;
            }

            cmd.DrawMeshInstanced(mesh, 0, material, 0, InstanceMatrices, count);
        }

        private static bool TryResolvePlacedCell(BurtXGIProbeBakingConfig config, int cellIndex, out BurtXGIProbePlacedCell placedCell)
        {
            placedCell = default;
            var placedCells = config != null ? config.bakedPlacedCells : null;
            if (placedCells == null)
            {
                return false;
            }

            for (var index = 0; index < placedCells.Length; index++)
            {
                if (placedCells[index].index == cellIndex)
                {
                    placedCell = placedCells[index];
                    return true;
                }
            }

            return false;
        }

        private static int ResolveProbeSubdivisionLevel(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeBakedCellData cell,
            BurtXGIProbePlacedCell placedCell,
            int probeIndex)
        {
            var placedBricks = config != null ? config.bakedPlacedBricks : null;
            if (cell == null || placedBricks == null || placedBricks.Length == 0 || placedCell.brickCount <= 0)
            {
                return cell != null ? cell.minSubdivisionLevel : 0;
            }

            var localProbeIndex = Mathf.Max(0, probeIndex - cell.probeStartIndex);
            var localBrickIndex = localProbeIndex / ProbesPerBrick;
            var brickIndex = placedCell.brickStartIndex + localBrickIndex;
            if ((uint)brickIndex >= (uint)placedBricks.Length)
            {
                return cell.minSubdivisionLevel;
            }

            return Mathf.Max(0, placedBricks[brickIndex].subdivisionLevel);
        }

        private static Vector4 ResolveProbeAtlasIndex(
            BurtGIVirtualProbeCellStreamer streamer,
            BurtXGIProbeBakedCellData cell,
            int probeIndex,
            int subdivisionLevel)
        {
            if (streamer == null || streamer.physicalPool == null || cell == null || cell.chunks == null || cell.chunks.Length == 0)
            {
                return new Vector4(-1f, -1f, -1f, -1f);
            }

            var localProbeIndex = probeIndex - cell.probeStartIndex;
            if (localProbeIndex < 0)
            {
                return new Vector4(-1f, -1f, -1f, -1f);
            }

            var localBrickIndex = localProbeIndex / ProbesPerBrick;
            var localProbeInBrick = localProbeIndex - localBrickIndex * ProbesPerBrick;
            var localChunkIndex = localBrickIndex / BurtGIVirtualProbePhysicalPool.BricksPerChunk;
            if ((uint)localChunkIndex >= (uint)cell.chunks.Length || cell.chunks[localChunkIndex] == null)
            {
                return new Vector4(-1f, -1f, -1f, -1f);
            }

            var bakedChunkIndex = cell.chunks[localChunkIndex].physicalChunkIndex;
            if (!streamer.TryResolveRuntimePhysicalChunkIndex(cell.cellIndex, bakedChunkIndex, out var runtimeChunkIndex))
            {
                return new Vector4(-1f, -1f, -1f, -1f);
            }

            var brickIndexInChunk = localBrickIndex - localChunkIndex * BurtGIVirtualProbePhysicalPool.BricksPerChunk;
            if (brickIndexInChunk < 0 || brickIndexInChunk >= BurtGIVirtualProbePhysicalPool.BricksPerChunk)
            {
                return new Vector4(-1f, -1f, -1f, -1f);
            }

            var chunkOrigin = BurtGIVirtualProbePhysicalPool.GetChunkOrigin(
                runtimeChunkIndex,
                Vector3Int.Max(Vector3Int.one, streamer.physicalPool.chunkDimensions));
            var probeX = localProbeInBrick % BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
            var probeY = (localProbeInBrick / BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension) %
                BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
            var probeZ = localProbeInBrick /
                (BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension * BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension);
            var atlasX = chunkOrigin.x + brickIndexInChunk * BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension + probeX;
            var atlasY = chunkOrigin.y + probeY;
            var atlasZ = chunkOrigin.z + probeZ;
            return new Vector4(atlasX, atlasY, atlasZ, subdivisionLevel);
        }

        private static bool IsSubdivisionVisible(int subdivisionLevel, BurtXGIToolsDebugComponent debugComponent)
        {
            return debugComponent == null ||
                (subdivisionLevel >= debugComponent.minSubdivToVisualize &&
                 subdivisionLevel <= debugComponent.maxSubdivToVisualize);
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

        private static Color ResolveProbeTint(BurtXGIToolsProbeDebugLayer layer)
        {
            return layer switch
            {
                BurtXGIToolsProbeDebugLayer.BrickSize => new Color(0.25f, 1f, 0.45f, 0.68f),
                BurtXGIToolsProbeDebugLayer.Validity => new Color(0.3f, 1f, 0.35f, 0.68f),
                BurtXGIToolsProbeDebugLayer.SH_Sky_Visibility => new Color(0.35f, 0.75f, 1f, 0.68f),
                BurtXGIToolsProbeDebugLayer.SH => new Color(1f, 0.7f, 0.25f, 0.68f),
                BurtXGIToolsProbeDebugLayer.SHL0 => new Color(0.9f, 0.55f, 1f, 0.68f),
                BurtXGIToolsProbeDebugLayer.SHL0L1 => new Color(0.55f, 1f, 0.8f, 0.68f),
                _ => new Color(0.18f, 0.72f, 1f, 0.68f)
            };
        }

        private static Vector4 ResolveProbeInstanceColor(BurtXGIToolsProbeDebugLayer layer, int subdivisionLevel, BurtXGIProbeBakedDataAsset asset)
        {
            if (layer == BurtXGIToolsProbeDebugLayer.BrickSize)
            {
                return ResolveSubdivisionColor(subdivisionLevel);
            }

            var color = ResolveProbeTint(layer);
            if (layer == BurtXGIToolsProbeDebugLayer.Validity && (asset == null || !asset.hasValidity))
            {
                color = new Color(1f, 0.2f, 0.16f, 0.68f);
            }
            else if (layer == BurtXGIToolsProbeDebugLayer.SH_Sky_Visibility && (asset == null || !asset.hasSkyVisibility))
            {
                color = new Color(1f, 0.2f, 0.16f, 0.68f);
            }
            else if ((layer == BurtXGIToolsProbeDebugLayer.SH || layer == BurtXGIToolsProbeDebugLayer.SHL0 || layer == BurtXGIToolsProbeDebugLayer.SHL0L1) &&
                asset == null)
            {
                color = new Color(1f, 0.2f, 0.16f, 0.68f);
            }

            return color;
        }

        private static Vector4 ResolveSubdivisionColor(int subdivisionLevel)
        {
            var t = Mathf.InverseLerp(0f, BurtXGIToolsDebugComponent.MaxProbeSubdivisionLevel, Mathf.Clamp(subdivisionLevel, 0, BurtXGIToolsDebugComponent.MaxProbeSubdivisionLevel));
            var color = Color.HSVToRGB(Mathf.Lerp(0.56f, 0.02f, t), 0.82f, 1f);
            color.a = 0.7f;
            return color;
        }

        private static bool TryGetProbeMaterial(out Material material)
        {
            material = probeMaterial;
            if (material != null)
            {
                return true;
            }

            var shader = Shader.Find(ProbeShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingProbeShader)
                {
                    Debug.LogWarning("BurtRP could not find XGI probe debug shader: " + ProbeShaderName);
                    hasLoggedMissingProbeShader = true;
                }

                return false;
            }

            probeMaterial = CoreUtils.CreateEngineMaterial(shader);
            probeMaterial.enableInstancing = true;
            material = probeMaterial;
            return true;
        }

        private static bool TryGetVirtualOffsetMaterial(out Material material)
        {
            material = virtualOffsetMaterial;
            if (material != null)
            {
                return true;
            }

            var shader = Shader.Find(VirtualOffsetShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingVirtualOffsetShader)
                {
                    Debug.LogWarning("BurtRP could not find XGI probe virtual offset debug shader: " + VirtualOffsetShaderName);
                    hasLoggedMissingVirtualOffsetShader = true;
                }

                return false;
            }

            virtualOffsetMaterial = CoreUtils.CreateEngineMaterial(shader);
            virtualOffsetMaterial.enableInstancing = true;
            material = virtualOffsetMaterial;
            return true;
        }

        private static Mesh GetProbeMesh()
        {
            if (probeMesh != null)
            {
                return probeMesh;
            }

            probeMesh = BuildSphereMesh();
            probeMesh.hideFlags = HideFlags.HideAndDontSave;
            return probeMesh;
        }

        private static Mesh GetVirtualOffsetMesh()
        {
            if (virtualOffsetMesh != null)
            {
                return virtualOffsetMesh;
            }

            virtualOffsetMesh = BuildVirtualOffsetMesh();
            virtualOffsetMesh.hideFlags = HideFlags.HideAndDontSave;
            return virtualOffsetMesh;
        }

        private static Mesh BuildSphereMesh()
        {
            const int longitude = 12;
            const int latitude = 8;
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var indices = new List<int>();

            for (var y = 0; y <= latitude; y++)
            {
                var v = y / (float)latitude;
                var polar = Mathf.PI * v;
                var sin = Mathf.Sin(polar);
                var cos = Mathf.Cos(polar);
                for (var x = 0; x <= longitude; x++)
                {
                    var u = x / (float)longitude;
                    var azimuth = u * Mathf.PI * 2f;
                    var normal = new Vector3(Mathf.Cos(azimuth) * sin, cos, Mathf.Sin(azimuth) * sin);
                    vertices.Add(normal * 0.5f);
                    normals.Add(normal);
                    uvs.Add(new Vector2(u, v));
                }
            }

            var row = longitude + 1;
            for (var y = 0; y < latitude; y++)
            {
                for (var x = 0; x < longitude; x++)
                {
                    var a = y * row + x;
                    var b = a + 1;
                    var c = a + row;
                    var d = c + 1;
                    indices.Add(a);
                    indices.Add(c);
                    indices.Add(b);
                    indices.Add(b);
                    indices.Add(c);
                    indices.Add(d);
                }
            }

            var mesh = new Mesh { name = "Burt XGI Probe Debug Sphere" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildVirtualOffsetMesh()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(-0.5f, -0.5f, 0.72f),
                new Vector3(0.5f, -0.5f, 0.72f),
                new Vector3(0.5f, 0.5f, 0.72f),
                new Vector3(-0.5f, 0.5f, 0.72f)
            };
            var indices = new[]
            {
                0, 2, 3,
                0, 3, 4,
                0, 4, 5,
                0, 5, 2,
                1, 3, 2,
                1, 4, 3,
                1, 5, 4,
                1, 2, 5
            };
            var mesh = new Mesh { name = "Burt XGI Probe Debug Virtual Offset" };
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
