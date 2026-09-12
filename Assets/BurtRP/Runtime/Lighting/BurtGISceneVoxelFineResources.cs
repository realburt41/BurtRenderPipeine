using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    // Resource ownership only. This class does not enable fine voxels, select a
    // clipmap, change a Volume/Profile, or claim that recorded GPU work completed.
    internal sealed class BurtGISceneVoxelFineResources : IDisposable
    {
        public const int MaximumCapacity = 1048576;
        public const int MaterialStride = 24;
        public const int LightingStride = 16;
        public const int SlotStride = MaterialStride + LightingStride + sizeof(uint) * 2;
        public const int ScanGroupSize = 256;

        public const string LowName = "_BurtGISceneVoxelFineLow";
        public const string HighName = "_BurtGISceneVoxelFineHigh";
        public const string OffsetsName = "_BurtGISceneVoxelFineOffsets";
        public const string OwnersName = "_BurtGISceneVoxelFineOwners";
        public const string MaterialsName = "_BurtGISceneVoxelFineMaterials";
        public const string LightingName = "_BurtGISceneVoxelFineLighting";
        public const string CellCoordsName = "_BurtGISceneVoxelFineCellCoords";
        public const string CountsName = "_BurtGISceneVoxelFineCounts";
        public const string GroupSumsName = "_BurtGISceneVoxelFineGroupSums";
        public const string GroupOffsetsName = "_BurtGISceneVoxelFineGroupOffsets";
        public const string SuperGroupSumsName = "_BurtGISceneVoxelFineSuperGroupSums";
        public const string SuperGroupOffsetsName = "_BurtGISceneVoxelFineSuperGroupOffsets";
        public const string ResolutionName = "_BurtGISceneVoxelFineResolution";
        public const string CapacityName = "_BurtGISceneVoxelFineCapacity";
        public const string SummaryName = "_BurtGISceneVoxelFineSummary";
        public const string GeometryReadyName = "_BurtGISceneVoxelFineGeometryReady";
        public const string HierarchyReadyName = "_BurtGISceneVoxelFineHierarchyReady";
        public const string TraceStepBudgetName = "_BurtGISceneVoxelFineTraceStepBudget";
        public const string CenterExtentName = "_BurtGISceneVoxelCenterExtent";

        private static readonly string[] BuildKernelNames =
        {
            "ClearGeometry", "ClearPool", "ScanNodes", "ScanGroups", "ScanSuperGroups", "FinalizeNodes", "BuildCoarseSummary"
        };

        private RenderTexture low, high, offsets, summary;
        private readonly BurtGISceneVoxelOctreeUtility.ResourceSet hierarchy = new BurtGISceneVoxelOctreeUtility.ResourceSet();
        private ComputeBuffer owners, materials, lighting, cellCoords, counts;
        private ComputeBuffer groupSums, groupOffsets, superGroupSums, superGroupOffsets;
        private bool disposed, geometryValid, lightingValid;
        private Vector4 centerExtent;
        private ulong sourceEpoch;
        private uint generation;
        // Geometry can be reused for several distinct lighting updates. A second
        // completion token prevents an older update from publishing the newer one.
        private uint lightingTicket;
        private bool lightingRecordProtocolUsed, lightingTicketActive;
        // Ordered-build provenance, distinct from CPU/GPU completion flags. Only a
        // caller-owned, exclusively recorded CB may claim this chain once.
        private CommandBuffer recordedBuildBuffer;
        private int recordedBuildPhase, recordedBuildBytes;
        private uint recordedBuildGeneration;
        private bool recordedFrameClaimed;
        private BurtGISceneVoxelFineFrameTransaction recordedFrame;

        public RenderTexture Low => low;
        public RenderTexture High => high;
        public RenderTexture Offsets => offsets;
        // Alpha is OR(fine occupancy); RGB is deliberately zero, never material/lighting.
        public RenderTexture Summary => summary;
        public BurtGISceneVoxelOctreeUtility.ResourceSet Hierarchy => hierarchy;
        public ComputeBuffer Owners => owners;
        public ComputeBuffer Materials => materials;
        public ComputeBuffer Lighting => lighting;
        public ComputeBuffer CellCoords => cellCoords;
        // Counts are [total demanded fine cells, unallocated fine cells, allocated fine cells].
        public ComputeBuffer Counts => counts;
        public ComputeBuffer GroupSums => groupSums;
        public ComputeBuffer GroupOffsets => groupOffsets;
        public ComputeBuffer SuperGroupSums => superGroupSums;
        public ComputeBuffer SuperGroupOffsets => superGroupOffsets;

        public int Resolution { get; private set; }
        public int FineResolution => Resolution * 4;
        public int NodeCount { get; private set; }
        public int GroupCount { get; private set; }
        public int SuperGroupCount { get; private set; }
        public int LogicalCapacity { get; private set; }
        public int PhysicalCapacity { get; private set; }
        public uint Generation => generation;
        public ulong SourceEpoch => sourceEpoch;
        public Vector4 CenterExtent => centerExtent;
        public bool GeometryValid => geometryValid && IsAllocated;
        public bool LightingValid => lightingValid && GeometryValid && (!lightingRecordProtocolUsed || HierarchyValid);
        public bool HierarchyValid => hierarchy.Valid && GeometryValid;
        public uint LightingTicket => lightingTicketActive ? lightingTicket : 0u;
        public BurtGISceneVoxelFineFrameTransaction ScheduledFrame =>
            recordedFrame != null && recordedFrame.IsScheduled ? recordedFrame : null;

        // Excludes driver overhead/alignment. A safety ceiling is not a performance approval.
        public long EstimatedAllocatedBytes => IsAllocated
            ? NodeCount * 20L + HierarchyTexelCount * 8L + PhysicalCapacity * (long)SlotStride +
              (GroupCount * 2L + SuperGroupCount * 2L + 3L) * sizeof(uint)
            : 0L;

        public bool IsAllocated => !disposed && Resolution > 0 &&
            IsTextureReady(low, Resolution) && IsTextureReady(high, Resolution) && IsTextureReady(offsets, Resolution) &&
            IsTextureReady(summary, Resolution, GraphicsFormat.R16G16B16A16_SFloat) && IsHierarchyAllocated &&
            IsBufferReady(owners, PhysicalCapacity, sizeof(uint)) &&
            IsBufferReady(materials, PhysicalCapacity, MaterialStride) &&
            IsBufferReady(lighting, PhysicalCapacity, LightingStride) &&
            IsBufferReady(cellCoords, PhysicalCapacity, sizeof(uint)) &&
            IsBufferReady(counts, 3, sizeof(uint)) &&
            IsBufferReady(groupSums, GroupCount, sizeof(uint)) && IsBufferReady(groupOffsets, GroupCount, sizeof(uint)) &&
            IsBufferReady(superGroupSums, SuperGroupCount, sizeof(uint)) && IsBufferReady(superGroupOffsets, SuperGroupCount, sizeof(uint));

        private int LeafResolution => Math.Max(1, Resolution / 4);
        private int ParentResolution => Math.Max(1, Resolution / 16);
        private int RootResolution => Math.Max(1, Resolution / 64);
        private long HierarchyTexelCount => Cube(LeafResolution) + Cube(ParentResolution) + Cube(RootResolution);
        private bool IsHierarchyAllocated => hierarchy.RadianceResolution == Resolution &&
            IsTextureReady(hierarchy.LeafLow, LeafResolution) && IsTextureReady(hierarchy.LeafHigh, LeafResolution) &&
            IsTextureReady(hierarchy.ParentLow, ParentResolution) && IsTextureReady(hierarchy.ParentHigh, ParentResolution) &&
            IsTextureReady(hierarchy.RootLow, RootResolution) && IsTextureReady(hierarchy.RootHigh, RootResolution);

        public BurtGISceneVoxelFineResources() { }

        public BurtGISceneVoxelFineResources(int resolution, int capacity, string name)
        {
            Ensure(resolution, capacity, name);
        }

        // True means new resources were allocated; false means a matching allocation was reused.
        // Invalid arguments and allocation failures throw. Reallocation never retains old GPU resources.
        public bool Ensure(int resolution, int capacity, string name)
        {
            ThrowIfDisposed();
            if (capacity < 0 || capacity > MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Fine voxel logical capacity must be in [0, 1048576].");

            int normalized = BurtScreenSpaceGlobalIlluminationPassUtility.NormalizeSceneVoxelRadianceResolution(resolution);
            if (IsAllocated && Resolution == normalized && LogicalCapacity == capacity)
                return false;

            Exception releaseFailure = ReleaseResources();
            if (releaseFailure != null)
                throw new InvalidOperationException("Could not completely release previous fine voxel resources.", releaseFailure);

            bool complete = false;
            Exception allocationFailure = null;
            try
            {
                if (!SystemInfo.supportsComputeShaders || !SystemInfo.supports3DTextures ||
                    !SystemInfo.IsFormatSupported(GraphicsFormat.R32_UInt, FormatUsage.LoadStore) ||
                    !SystemInfo.IsFormatSupported(GraphicsFormat.R16G16B16A16_SFloat, FormatUsage.LoadStore))
                    throw new NotSupportedException("Fine voxel resources require compute, R32_UInt and RGBA16F 3D UAV support.");

                Resolution = normalized;
                NodeCount = checked(normalized * normalized * normalized);
                GroupCount = DivideRoundUp(NodeCount, ScanGroupSize);
                SuperGroupCount = DivideRoundUp(GroupCount, ScanGroupSize);
                LogicalCapacity = capacity;
                PhysicalCapacity = Math.Max(capacity, 1);
                string prefix = string.IsNullOrEmpty(name) ? "BurtGI Fine Voxel" : name;

                low = CreateTexture(Resolution, prefix + " Low");
                high = CreateTexture(Resolution, prefix + " High");
                offsets = CreateTexture(Resolution, prefix + " Offsets");
                summary = CreateTexture(Resolution, prefix + " Summary", GraphicsFormat.R16G16B16A16_SFloat);
                hierarchy.RadianceResolution = Resolution;
                hierarchy.LeafLow = CreateTexture(LeafResolution, prefix + " Fine Octree Leaf Low");
                hierarchy.LeafHigh = CreateTexture(LeafResolution, prefix + " Fine Octree Leaf High");
                hierarchy.ParentLow = CreateTexture(ParentResolution, prefix + " Fine Octree Parent Low");
                hierarchy.ParentHigh = CreateTexture(ParentResolution, prefix + " Fine Octree Parent High");
                hierarchy.RootLow = CreateTexture(RootResolution, prefix + " Fine Octree Root Low");
                hierarchy.RootHigh = CreateTexture(RootResolution, prefix + " Fine Octree Root High");
                owners = CreateBuffer(PhysicalCapacity, sizeof(uint), prefix + " Owners");
                materials = CreateBuffer(PhysicalCapacity, MaterialStride, prefix + " Materials");
                lighting = CreateBuffer(PhysicalCapacity, LightingStride, prefix + " Lighting");
                cellCoords = CreateBuffer(PhysicalCapacity, sizeof(uint), prefix + " CellCoords");
                counts = CreateBuffer(3, sizeof(uint), prefix + " Counts");
                groupSums = CreateBuffer(GroupCount, sizeof(uint), prefix + " GroupSums");
                groupOffsets = CreateBuffer(GroupCount, sizeof(uint), prefix + " GroupOffsets");
                superGroupSums = CreateBuffer(SuperGroupCount, sizeof(uint), prefix + " SuperGroupSums");
                superGroupOffsets = CreateBuffer(SuperGroupCount, sizeof(uint), prefix + " SuperGroupOffsets");
                if (!IsAllocated)
                    throw new InvalidOperationException("Fine voxel allocation did not produce all required GPU resources.");
                complete = true;
                return true;
            }
            catch (Exception exception)
            {
                allocationFailure = exception;
                throw;
            }
            finally
            {
                if (!complete)
                {
                    Exception cleanupFailure = ReleaseResources();
                    if (cleanupFailure != null)
                        throw new AggregateException("Fine voxel allocation and cleanup failed.", allocationFailure, cleanupFailure);
                }
            }
        }

        // The generation is a CPU completion token. Capture it AFTER recording the
        // complete build and only mark validity when that build is accepted by its caller.
        public uint Invalidate()
        {
            ResetRecordedBuild();
            geometryValid = false;
            InvalidateLighting();
            lightingRecordProtocolUsed = false;
            hierarchy.Valid = false;
            centerExtent = Vector4.zero;
            sourceEpoch = 0;
            unchecked { generation++; }
            if (generation == 0) generation = 1;
            return generation;
        }

        public bool MarkGeometryValid(uint expectedGeneration, Vector4 bounds, ulong expectedSourceEpoch)
        {
            // A scheduling receipt is intentionally not a completion protocol. Do
            // not let the legacy completion API replace this generation's source
            // tuple while its same-frame consumers still hold a scheduled token.
            if (recordedFrameClaimed || !IsAllocated || expectedGeneration != generation || !ValidBounds(bounds))
                return false;
            if (geometryValid)
                return sourceEpoch == expectedSourceEpoch && centerExtent.Equals(bounds);
            centerExtent = bounds;
            sourceEpoch = expectedSourceEpoch;
            geometryValid = true;
            lightingValid = false;
            return true;
        }

        public bool MarkLightingValid(uint expectedGeneration, ulong expectedSourceEpoch)
        {
            // Legacy callers accept independently managed lighting work. Once this
            // geometry generation uses RecordFineLighting, only its ticketed
            // completion API may publish lighting (including after cancellation).
            if (lightingRecordProtocolUsed || !GeometryValid || expectedGeneration != generation || expectedSourceEpoch != sourceEpoch)
                return false;
            lightingValid = true;
            return true;
        }

        // Invalidates only lighting and outstanding lighting completions. Geometry,
        // material, hierarchy, bounds and source epoch remain available for relight.
        public void InvalidateLighting()
        {
            CancelRecordedFrame();
            InvalidateLightingState();
        }

        private void InvalidateLightingState()
        {
            lightingValid = false;
            lightingTicketActive = false;
            unchecked { lightingTicket++; }
            if (lightingTicket == 0) lightingTicket = 1;
        }

        public bool MarkRecordedLightingValid(uint expectedGeneration, ulong expectedSourceEpoch, uint expectedLightingTicket)
        {
            if (!lightingRecordProtocolUsed || !lightingTicketActive || expectedLightingTicket == 0u ||
                expectedLightingTicket != lightingTicket || !HierarchyValid ||
                expectedGeneration != generation || expectedSourceEpoch != sourceEpoch)
                return false;
            lightingValid = true;
            return true;
        }

        // Recording commands is not a GPU completion claim. The caller accepts this
        // generation only after executing the complete matching geometry/summary build.
        public bool MarkHierarchyValid(uint expectedGeneration, ulong expectedSourceEpoch)
        {
            if (!GeometryValid || expectedGeneration != generation || expectedSourceEpoch != sourceEpoch)
                return false;
            hierarchy.Valid = true;
            return true;
        }

        public bool MatchesSource(uint expectedGeneration, Vector4 bounds, ulong expectedSourceEpoch)
        {
            return GeometryValid && generation == expectedGeneration && sourceEpoch == expectedSourceEpoch && centerExtent.Equals(bounds);
        }

        public void RecordClear(CommandBuffer commandBuffer, ComputeShader shader)
        {
            RequireReady(commandBuffer, shader);
            int geometryKernel = RequireKernel(shader, "ClearGeometry", 4, 4, 4);
            int poolKernel = RequireKernel(shader, "ClearPool", 64, 1, 1);
            Invalidate();
            BindBuild(commandBuffer, shader, geometryKernel, "ClearGeometry");
            int groups = DivideRoundUp(Resolution, 4);
            commandBuffer.DispatchCompute(shader, geometryKernel, groups, groups, groups);
            BindBuild(commandBuffer, shader, poolKernel, "ClearPool");
            // The shader clears max(logicalCapacity, 1); its queries still use logical capacity.
            commandBuffer.DispatchCompute(shader, poolKernel, DivideRoundUp(PhysicalCapacity, 64), 1, 1);
            TrackRecordedBuild(commandBuffer, 1);
        }

        // Geometry raster must be recorded between RecordClear and RecordAllocate.
        // Material resolve and lighting are caller-owned later steps; no valid bit is set here.
        public void RecordAllocate(CommandBuffer commandBuffer, ComputeShader shader)
        {
            RequireReady(commandBuffer, shader);
            int nodes = RequireKernel(shader, "ScanNodes", 256, 1, 1);
            int groups = RequireKernel(shader, "ScanGroups", 256, 1, 1);
            int superGroups = RequireKernel(shader, "ScanSuperGroups", 256, 1, 1);
            int finalize = RequireKernel(shader, "FinalizeNodes", 256, 1, 1);
            bool orderedClear = MatchesRecordedBuild(commandBuffer, 1);
            Invalidate();
            BindBuild(commandBuffer, shader, nodes, "ScanNodes");
            commandBuffer.DispatchCompute(shader, nodes, GroupCount, 1, 1);
            BindBuild(commandBuffer, shader, groups, "ScanGroups");
            commandBuffer.DispatchCompute(shader, groups, SuperGroupCount, 1, 1);
            BindBuild(commandBuffer, shader, superGroups, "ScanSuperGroups");
            commandBuffer.DispatchCompute(shader, superGroups, 1, 1, 1);
            BindBuild(commandBuffer, shader, finalize, "FinalizeNodes");
            commandBuffer.DispatchCompute(shader, finalize, GroupCount, 1, 1);
            if (orderedClear) TrackRecordedBuild(commandBuffer, 2);
        }

        public void RecordBuildSummary(CommandBuffer commandBuffer, ComputeShader shader)
        {
            RequireReady(commandBuffer, shader);
            int kernel = RequireKernel(shader, "BuildCoarseSummary", 4, 4, 4);
            bool orderedAllocate = MatchesRecordedBuild(commandBuffer, 2);
            if (!orderedAllocate) ResetRecordedBuild();
            hierarchy.Valid = false;
            InvalidateLighting();
            BindBuild(commandBuffer, shader, kernel, "BuildCoarseSummary");
            int groups = DivideRoundUp(Resolution, 4);
            commandBuffer.DispatchCompute(shader, kernel, groups, groups, groups);
        }

        // This hierarchy is owned by this fine generation. The old independently
        // rasterized coarse geometry must never cull a fine-occupied node.
        public void RecordBuildHierarchy(CommandBuffer commandBuffer, ComputeShader buildShader, ComputeShader octreeShader)
        {
            RequireReady(commandBuffer, buildShader);
            if (octreeShader == null) throw new ArgumentNullException(nameof(octreeShader));
            int leaf = RequireKernel(octreeShader, "SceneVoxelBuildOctreeLeafCS", 1, 1, 1);
            int parent = RequireKernel(octreeShader, "SceneVoxelBuildOctreeParentCS", 1, 1, 1);
            int root = RequireKernel(octreeShader, "SceneVoxelBuildOctreeRootCS", 1, 1, 1);
            bool orderedAllocate = MatchesRecordedBuild(commandBuffer, 2);
            RecordBuildSummary(commandBuffer, buildShader);
            BindTexture(commandBuffer, octreeShader, leaf, "_BurtGISceneVoxelGeometryReadTexture", summary);
            BindHierarchyPair(commandBuffer, octreeShader, leaf, "Leaf", hierarchy.LeafLow, hierarchy.LeafHigh);
            commandBuffer.DispatchCompute(octreeShader, leaf, LeafResolution, LeafResolution, LeafResolution);
            BindHierarchyPair(commandBuffer, octreeShader, parent, "Leaf", hierarchy.LeafLow, hierarchy.LeafHigh);
            BindHierarchyPair(commandBuffer, octreeShader, parent, "Parent", hierarchy.ParentLow, hierarchy.ParentHigh);
            commandBuffer.DispatchCompute(octreeShader, parent, ParentResolution, ParentResolution, ParentResolution);
            BindHierarchyPair(commandBuffer, octreeShader, root, "Parent", hierarchy.ParentLow, hierarchy.ParentHigh);
            BindHierarchyPair(commandBuffer, octreeShader, root, "Root", hierarchy.RootLow, hierarchy.RootHigh);
            commandBuffer.DispatchCompute(octreeShader, root, RootResolution, RootResolution, RootResolution);
            if (orderedAllocate) TrackRecordedBuild(commandBuffer, 3);
        }

        // Called only after the native producer has recorded BOTH material phases
        // and the complete fine hierarchy into this same exclusive CB. The resource
        // chain proves clear/allocation/hierarchy ordering; native raster coverage
        // and material preflight remain the producer's contract, not inferred here.
        // No completed validity, bounds, or source epoch are published by this API.
        public bool TryBeginRecordedFrame(CommandBuffer commandBuffer, BurtRenderGraphContext context,
            uint expectedGeneration, Vector4 bounds, ulong expectedSourceEpoch,
            out BurtGISceneVoxelFineFrameTransaction transaction)
        {
            return TryBeginRecordedFrame(commandBuffer, context, expectedGeneration, bounds, expectedSourceEpoch, 0, out transaction);
        }

        public bool TryBeginRecordedFrame(CommandBuffer commandBuffer, BurtRenderGraphContext context,
            uint expectedGeneration, Vector4 bounds, ulong expectedSourceEpoch, int level,
            out BurtGISceneVoxelFineFrameTransaction transaction)
        {
            transaction = null;
            if (!IsAllocated || context == null || context.Request == null || context.Request.Camera == null ||
                expectedGeneration != generation || !ValidBounds(bounds) || recordedFrameClaimed || level < 0 || level > 5 ||
                GeometryValid || LightingValid || HierarchyValid || ReferenceEquals(commandBuffer, context.CommandBuffer) ||
                !MatchesRecordedBuild(commandBuffer, 3))
                return false;
            if (!BurtGISceneVoxelFineFrameTransaction.TryCreate(this, commandBuffer, context,
                generation, bounds, expectedSourceEpoch, level, recordedBuildBytes, out transaction)) return false;
            recordedFrameClaimed = true;
            recordedFrame = transaction;
            return true;
        }

        internal bool OwnsRecordedFrame(BurtGISceneVoxelFineFrameTransaction transaction)
        {
            return IsAllocated && ReferenceEquals(recordedFrame, transaction) && recordedFrameClaimed &&
                transaction.Generation == generation && recordedBuildGeneration == generation;
        }

        internal bool RecordFrameLighting(BurtGISceneVoxelFineFrameTransaction transaction,
            CommandBuffer commandBuffer, ComputeShader shader, int stepBudget)
        {
            RequireReady(commandBuffer, shader);
            int kernel = RequireKernel(shader, "SceneVoxelBuildFineLightingCS", 64, 1, 1);
            if (!OwnsRecordedFrame(transaction) || !transaction.CanRecordLighting(commandBuffer)) return false;
            InvalidateLightingState();
            lightingRecordProtocolUsed = true;
            // Zero-capacity occupancy remains queryable as opaque/unknown material.
            if (LogicalCapacity == 0) return true;
            BindBuffer(commandBuffer, shader, kernel, MaterialsName, materials);
            BindBuffer(commandBuffer, shader, kernel, OwnersName, owners);
            BindBuffer(commandBuffer, shader, kernel, CellCoordsName, cellCoords);
            BindBuffer(commandBuffer, shader, kernel, LightingName, lighting);
            commandBuffer.SetComputeIntParam(shader, CapacityName, LogicalCapacity);
            RecordGeometryBindings(commandBuffer, shader, kernel, transaction.Bounds, stepBudget);
            commandBuffer.DispatchCompute(shader, kernel, DivideRoundUp(LogicalCapacity, 64), 1, 1);
            return true;
        }

        internal void CancelRecordedFrame(BurtGISceneVoxelFineFrameTransaction transaction)
        {
            if (!ReferenceEquals(recordedFrame, transaction)) return;
            CancelRecordedFrame();
            InvalidateLightingState();
        }

        private void CancelRecordedFrame()
        {
            var previous = recordedFrame;
            recordedFrame = null;
            previous?.CancelFromOwner();
        }

        private void ResetRecordedBuild()
        {
            CancelRecordedFrame();
            recordedBuildBuffer = null;
            recordedBuildPhase = recordedBuildBytes = 0;
            recordedBuildGeneration = 0;
            recordedFrameClaimed = false;
        }

        private bool MatchesRecordedBuild(CommandBuffer commandBuffer, int phase)
        {
            return commandBuffer != null && ReferenceEquals(commandBuffer, recordedBuildBuffer) &&
                recordedBuildPhase == phase && recordedBuildGeneration == generation &&
                commandBuffer.sizeInBytes >= recordedBuildBytes && recordedBuildBytes > 0;
        }

        private void TrackRecordedBuild(CommandBuffer commandBuffer, int phase)
        {
            recordedBuildBuffer = commandBuffer;
            recordedBuildPhase = phase;
            recordedBuildBytes = commandBuffer.sizeInBytes;
            recordedBuildGeneration = generation;
        }

        private static void BindHierarchyPair(CommandBuffer commandBuffer, ComputeShader shader, int kernel,
            string level, RenderTexture lowTexture, RenderTexture highTexture)
        {
            BindTexture(commandBuffer, shader, kernel, "_BurtGISceneVoxelOctree" + level + "LowTexture", lowTexture);
            BindTexture(commandBuffer, shader, kernel, "_BurtGISceneVoxelOctree" + level + "HighTexture", highTexture);
        }

        // Environment uploads must precede this final geometry binding. Bounds,
        // resolution and hierarchy are all from the same accepted source. No
        // offsets/material payload/legacy occupancy mip are geometry-query inputs.
        // False records nothing; the caller must not dispatch on a rejected bind.
        public bool RecordBindGeometryQuery(CommandBuffer commandBuffer, ComputeShader shader, int kernel,
            uint expectedGeneration, Vector4 expectedBounds, ulong expectedSourceEpoch, int stepBudget = 512)
        {
            RequireReady(commandBuffer, shader);
            if (kernel < 0) throw new ArgumentOutOfRangeException(nameof(kernel));
            if (!MatchesSource(expectedGeneration, expectedBounds, expectedSourceEpoch) || !HierarchyValid)
                return false;

            RecordGeometryBindings(commandBuffer, shader, kernel, centerExtent, stepBudget);
            return true;
        }

        private void RecordGeometryBindings(CommandBuffer commandBuffer, ComputeShader shader, int kernel,
            Vector4 bounds, int stepBudget)
        {
            BindTexture(commandBuffer, shader, kernel, LowName, low);
            BindTexture(commandBuffer, shader, kernel, HighName, high);
            BindHierarchyPair(commandBuffer, shader, kernel, "Leaf", hierarchy.LeafLow, hierarchy.LeafHigh);
            BindHierarchyPair(commandBuffer, shader, kernel, "Parent", hierarchy.ParentLow, hierarchy.ParentHigh);
            BindHierarchyPair(commandBuffer, shader, kernel, "Root", hierarchy.RootLow, hierarchy.RootHigh);
            commandBuffer.SetComputeIntParam(shader, ResolutionName, Resolution);
            commandBuffer.SetComputeIntParam(shader, GeometryReadyName, 1);
            commandBuffer.SetComputeIntParam(shader, HierarchyReadyName, 1);
            commandBuffer.SetComputeIntParam(shader, TraceStepBudgetName, Mathf.Clamp(stepBudget, 96, 512));
            commandBuffer.SetComputeVectorParam(shader, CenterExtentName, bounds);
        }

        // The caller records light/environment/shadow/cache inputs BEFORE this
        // method and executes the buffer in source order. A CPU ticket prevents
        // stale completion publication; it cannot cancel already queued GPU work.
        // If recording throws after it starts, discard the whole caller-owned
        // buffer. This method does not clear unrelated environment commands.
        // False (stale source) records nothing and preserves prior lighting. A
        // successful call clears LightingValid, even for an empty logical pool.
        public bool RecordFineLighting(CommandBuffer commandBuffer, ComputeShader shader,
            uint expectedGeneration, Vector4 expectedBounds, ulong expectedSourceEpoch,
            out uint recordedLightingTicket, int stepBudget = 512)
        {
            recordedLightingTicket = 0u;
            RequireReady(commandBuffer, shader);
            int kernel = RequireKernel(shader, "SceneVoxelBuildFineLightingCS", 64, 1, 1);
            if (!MatchesSource(expectedGeneration, expectedBounds, expectedSourceEpoch) || !HierarchyValid)
                return false;

            InvalidateLighting();
            lightingRecordProtocolUsed = true;
            // Occupancy is still valid with zero material capacity. Do not bind or
            // dispatch the one-element physical dummy as if it were real lighting.
            if (LogicalCapacity == 0) return true;
            try
            {
                BindBuffer(commandBuffer, shader, kernel, MaterialsName, materials);
                BindBuffer(commandBuffer, shader, kernel, OwnersName, owners);
                BindBuffer(commandBuffer, shader, kernel, CellCoordsName, cellCoords);
                BindBuffer(commandBuffer, shader, kernel, LightingName, lighting);
                commandBuffer.SetComputeIntParam(shader, CapacityName, LogicalCapacity);
                if (!RecordBindGeometryQuery(commandBuffer, shader, kernel,
                    expectedGeneration, expectedBounds, expectedSourceEpoch, stepBudget))
                    throw new InvalidOperationException("Fine geometry changed while recording lighting.");
                commandBuffer.DispatchCompute(shader, kernel, DivideRoundUp(LogicalCapacity, 64), 1, 1);
                lightingTicketActive = true;
                recordedLightingTicket = lightingTicket;
                return true;
            }
            catch
            {
                InvalidateLighting();
                throw;
            }
        }

        // Bind only the resources actually used by this build kernel. This avoids
        // relying on unrelated global bindings or introducing unused D3D11 UAV slots.
        public void BindBuild(CommandBuffer commandBuffer, ComputeShader shader, int kernel)
        {
            RequireReady(commandBuffer, shader);
            foreach (string name in BuildKernelNames)
            {
                if (shader.HasKernel(name) && shader.FindKernel(name) == kernel)
                {
                    BindBuild(commandBuffer, shader, kernel, name);
                    return;
                }
            }
            throw new ArgumentException("Kernel is not part of the fine voxel build protocol.", nameof(kernel));
        }

        private void BindBuild(CommandBuffer commandBuffer, ComputeShader shader, int kernel, string name)
        {
            commandBuffer.SetComputeIntParam(shader, ResolutionName, Resolution);
            commandBuffer.SetComputeIntParam(shader, CapacityName, LogicalCapacity);
            switch (name)
            {
                case "ClearGeometry":
                    BindTexture(commandBuffer, shader, kernel, LowName, low);
                    BindTexture(commandBuffer, shader, kernel, HighName, high);
                    BindTexture(commandBuffer, shader, kernel, OffsetsName, offsets);
                    break;
                case "ClearPool":
                    BindBuffer(commandBuffer, shader, kernel, OwnersName, owners);
                    BindBuffer(commandBuffer, shader, kernel, MaterialsName, materials);
                    BindBuffer(commandBuffer, shader, kernel, LightingName, lighting);
                    BindBuffer(commandBuffer, shader, kernel, CellCoordsName, cellCoords);
                    break;
                case "ScanNodes":
                    BindTexture(commandBuffer, shader, kernel, LowName, low);
                    BindTexture(commandBuffer, shader, kernel, HighName, high);
                    BindTexture(commandBuffer, shader, kernel, OffsetsName, offsets);
                    BindBuffer(commandBuffer, shader, kernel, GroupSumsName, groupSums);
                    break;
                case "ScanGroups":
                    BindBuffer(commandBuffer, shader, kernel, GroupSumsName, groupSums);
                    BindBuffer(commandBuffer, shader, kernel, GroupOffsetsName, groupOffsets);
                    BindBuffer(commandBuffer, shader, kernel, SuperGroupSumsName, superGroupSums);
                    break;
                case "ScanSuperGroups":
                    BindBuffer(commandBuffer, shader, kernel, SuperGroupSumsName, superGroupSums);
                    BindBuffer(commandBuffer, shader, kernel, SuperGroupOffsetsName, superGroupOffsets);
                    BindBuffer(commandBuffer, shader, kernel, CountsName, counts);
                    break;
                case "FinalizeNodes":
                    BindTexture(commandBuffer, shader, kernel, LowName, low);
                    BindTexture(commandBuffer, shader, kernel, HighName, high);
                    BindTexture(commandBuffer, shader, kernel, OffsetsName, offsets);
                    BindBuffer(commandBuffer, shader, kernel, GroupOffsetsName, groupOffsets);
                    BindBuffer(commandBuffer, shader, kernel, SuperGroupOffsetsName, superGroupOffsets);
                    BindBuffer(commandBuffer, shader, kernel, CellCoordsName, cellCoords);
                    BindBuffer(commandBuffer, shader, kernel, CountsName, counts);
                    break;
                case "BuildCoarseSummary":
                    BindTexture(commandBuffer, shader, kernel, LowName, low);
                    BindTexture(commandBuffer, shader, kernel, HighName, high);
                    BindTexture(commandBuffer, shader, kernel, SummaryName, summary);
                    break;
                default:
                    throw new ArgumentException("Unknown fine voxel build kernel.", nameof(name));
            }
        }

        // These are SRV/constant bindings only. The caller explicitly binds Low/High
        // as occupancy u1/u2 or Owners/Materials as material u1/u2, then clears UAVs.
        public void BindRaster(Material material)
        {
            RequireAllocated();
            if (material == null) throw new ArgumentNullException(nameof(material));
            material.SetInt(ResolutionName, Resolution);
            material.SetInt(CapacityName, LogicalCapacity);
            material.SetTexture(LowName, low);
            material.SetTexture(HighName, high);
            material.SetTexture(OffsetsName, offsets);
        }

        // Caller must scope/restore these globals; this method neither activates a
        // shader variant nor publishes validity. Prefer BindRaster for owned materials.
        public void RecordBindGlobals(CommandBuffer commandBuffer)
        {
            RequireAllocated();
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            commandBuffer.SetGlobalInt(ResolutionName, Resolution);
            commandBuffer.SetGlobalInt(CapacityName, LogicalCapacity);
            commandBuffer.SetGlobalTexture(LowName, low);
            commandBuffer.SetGlobalTexture(HighName, high);
            commandBuffer.SetGlobalTexture(OffsetsName, offsets);
            commandBuffer.SetGlobalBuffer(OwnersName, owners);
            commandBuffer.SetGlobalBuffer(MaterialsName, materials);
            commandBuffer.SetGlobalBuffer(LightingName, lighting);
            commandBuffer.SetGlobalBuffer(CellCoordsName, cellCoords);
            commandBuffer.SetGlobalBuffer(CountsName, counts);
        }

        public void Dispose()
        {
            if (disposed) return;
            Exception failure = ReleaseResources();
            if (failure != null)
                throw new InvalidOperationException("Fine voxel resource cleanup failed.", failure);
            disposed = true;
        }

        private Exception ReleaseResources()
        {
            Invalidate();
            var failures = new List<Exception>();
            ReleaseTexture(ref low, failures);
            ReleaseTexture(ref high, failures);
            ReleaseTexture(ref offsets, failures);
            ReleaseTexture(ref summary, failures);
            ReleaseTexture(ref hierarchy.LeafLow, failures);
            ReleaseTexture(ref hierarchy.LeafHigh, failures);
            ReleaseTexture(ref hierarchy.ParentLow, failures);
            ReleaseTexture(ref hierarchy.ParentHigh, failures);
            ReleaseTexture(ref hierarchy.RootLow, failures);
            ReleaseTexture(ref hierarchy.RootHigh, failures);
            ReleaseBuffer(ref owners, failures);
            ReleaseBuffer(ref materials, failures);
            ReleaseBuffer(ref lighting, failures);
            ReleaseBuffer(ref cellCoords, failures);
            ReleaseBuffer(ref counts, failures);
            ReleaseBuffer(ref groupSums, failures);
            ReleaseBuffer(ref groupOffsets, failures);
            ReleaseBuffer(ref superGroupSums, failures);
            ReleaseBuffer(ref superGroupOffsets, failures);
            Resolution = NodeCount = GroupCount = SuperGroupCount = LogicalCapacity = PhysicalCapacity = 0;
            return failures.Count == 0 ? null : new AggregateException(failures);
        }

        private static RenderTexture CreateTexture(int resolution, string name, GraphicsFormat format = GraphicsFormat.R32_UInt)
        {
            var descriptor = new RenderTextureDescriptor(resolution, resolution, format, 0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution,
                enableRandomWrite = true,
                msaaSamples = 1,
                mipCount = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            bool complete = false;
            try
            {
                if (!texture.Create()) throw new InvalidOperationException("Could not allocate " + name + ".");
                complete = true;
                return texture;
            }
            finally
            {
                if (!complete)
                {
                    try { texture.Release(); }
                    finally { UnityEngine.Object.DestroyImmediate(texture); }
                }
            }
        }

        private static ComputeBuffer CreateBuffer(int count, int stride, string name)
        {
            var buffer = new ComputeBuffer(count, stride, ComputeBufferType.Structured);
            try
            {
                buffer.name = name;
                if (!buffer.IsValid()) throw new InvalidOperationException("Could not allocate " + name + ".");
                return buffer;
            }
            catch
            {
                buffer.Release();
                throw;
            }
        }

        private static void ReleaseTexture(ref RenderTexture resource, List<Exception> failures)
        {
            RenderTexture texture = resource;
            if (texture == null) { resource = null; return; }
            try { texture.Release(); } catch (Exception exception) { failures.Add(exception); }
            try { UnityEngine.Object.DestroyImmediate(texture); } catch (Exception exception) { failures.Add(exception); }
            // Retain a failed cleanup target for a later Dispose/Ensure retry.
            if (texture == null) resource = null;
        }

        private static void ReleaseBuffer(ref ComputeBuffer resource, List<Exception> failures)
        {
            ComputeBuffer buffer = resource;
            if (buffer == null) return;
            try
            {
                buffer.Release();
                if (!buffer.IsValid()) resource = null;
                else failures.Add(new InvalidOperationException("A released fine voxel buffer is still valid."));
            }
            catch (Exception exception) { failures.Add(exception); }
        }

        private static bool IsTextureReady(RenderTexture texture, int resolution, GraphicsFormat format = GraphicsFormat.R32_UInt)
        {
            return texture != null && texture.IsCreated() && texture.dimension == TextureDimension.Tex3D &&
                texture.graphicsFormat == format && texture.width == resolution &&
                texture.height == resolution && texture.volumeDepth == resolution && texture.enableRandomWrite;
        }

        private static bool IsBufferReady(ComputeBuffer buffer, int count, int stride)
        {
            return buffer != null && buffer.IsValid() && buffer.count == count && buffer.stride == stride;
        }

        private static int RequireKernel(ComputeShader shader, string name, uint x, uint y, uint z)
        {
            if (!shader.HasKernel(name)) throw new ArgumentException("Fine voxel kernel is missing: " + name, nameof(shader));
            int kernel = shader.FindKernel(name);
            shader.GetKernelThreadGroupSizes(kernel, out uint actualX, out uint actualY, out uint actualZ);
            if (actualX != x || actualY != y || actualZ != z)
                throw new InvalidOperationException("Fine voxel dispatch contract changed for " + name + ".");
            return kernel;
        }

        private void RequireReady(CommandBuffer commandBuffer, ComputeShader shader)
        {
            RequireAllocated();
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            if (shader == null) throw new ArgumentNullException(nameof(shader));
        }

        private void RequireAllocated()
        {
            ThrowIfDisposed();
            if (!IsAllocated) throw new InvalidOperationException("Fine voxel resources are not allocated.");
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BurtGISceneVoxelFineResources));
        }

        private static bool ValidBounds(Vector4 bounds)
        {
            return IsFinite(bounds.x) && IsFinite(bounds.y) && IsFinite(bounds.z) && IsFinite(bounds.w) && bounds.w > 0f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static int DivideRoundUp(int value, int groupSize) => (value + groupSize - 1) / groupSize;
        private static long Cube(int value) => (long)value * value * value;
        private static void BindTexture(CommandBuffer commandBuffer, ComputeShader shader, int kernel, string name, RenderTexture texture)
            => commandBuffer.SetComputeTextureParam(shader, kernel, name, texture);
        private static void BindBuffer(CommandBuffer commandBuffer, ComputeShader shader, int kernel, string name, ComputeBuffer buffer)
            => commandBuffer.SetComputeBufferParam(shader, kernel, name, buffer);
    }
}
