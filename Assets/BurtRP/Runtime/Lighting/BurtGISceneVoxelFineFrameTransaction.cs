using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    // A scheduling receipt, NEVER a GPU-completion token. Geometry, material,
    // hierarchy and lighting are one ordered graphics-queue producer transaction.
    // The caller retains exclusive control of the producer CB until Submit:
    // do not Clear, Release, mutate earlier commands or execute it independently.
    internal sealed class BurtGISceneVoxelFineFrameTransaction : IDisposable
    {
        private enum Phase { GeometryRecorded, LightingRecorded, Scheduled, Cancelled }
        private readonly BurtGISceneVoxelFineResources owner;
        private readonly CommandBuffer producerBuffer;
        private readonly BurtRenderGraphContext frameContext;
        private readonly BurtRenderRequest request;
        private readonly Camera camera;
        private readonly CommandBuffer graphicsBuffer;
        private readonly int cameraId, requestFrame;
        private readonly int geometryEndBytes;
        private int lightingEndBytes;
        private Phase phase;
        private FrameStream stream;
        private static readonly ConditionalWeakTable<CommandBuffer, FrameStream> Streams =
            new ConditionalWeakTable<CommandBuffer, FrameStream>();
        private static readonly int QueryBoundsId = Shader.PropertyToID("_BurtGIScreenProbeFineBounds");
        private static readonly int QueryParamsId = Shader.PropertyToID("_BurtGIScreenProbeFineParams");
        private static readonly int QueryMaskId = Shader.PropertyToID("_BurtGIScreenProbeFineLevelMask");
        private static readonly int QueryReadyId = Shader.PropertyToID("_BurtGIScreenProbeFineSourceReady");
        private static readonly string[] QueryResourceSuffixes =
        { "Low", "High", "Offsets", "LeafLow", "LeafHigh", "ParentLow", "ParentHigh", "RootLow", "RootHigh", "Owners", "Materials", "Lighting" };
        private static readonly int[,] QueryResourceIds = CreateQueryResourceIds();

        private BurtGISceneVoxelFineFrameTransaction(BurtGISceneVoxelFineResources owner,
            CommandBuffer producerBuffer, BurtRenderGraphContext context, uint generation,
            Vector4 bounds, ulong sourceEpoch, int level, int geometryEndBytes)
        {
            this.owner = owner;
            this.producerBuffer = producerBuffer;
            frameContext = context;
            request = context.Request;
            camera = request.Camera;
            cameraId = camera.GetInstanceID();
            requestFrame = request.RenderFrameIndex;
            graphicsBuffer = context.CommandBuffer;
            Generation = generation;
            Bounds = bounds;
            SourceEpoch = sourceEpoch;
            Level = level;
            this.geometryEndBytes = geometryEndBytes;
        }

        internal static bool TryCreate(BurtGISceneVoxelFineResources owner, CommandBuffer commandBuffer,
            BurtRenderGraphContext context, uint generation, Vector4 bounds, ulong sourceEpoch,
            int level, int geometryEndBytes, out BurtGISceneVoxelFineFrameTransaction transaction)
        {
            transaction = null;
            FrameStream candidate;
            if (Streams.TryGetValue(commandBuffer, out candidate) && !candidate.MatchesContext(context))
            {
                Streams.Remove(commandBuffer);
                candidate = null;
            }
            if (candidate == null)
            {
                candidate = new FrameStream(context);
                Streams.Add(commandBuffer, candidate);
            }
            if (candidate.Submitted || candidate.Cancelled) return false;
            foreach (var member in candidate.Members)
                if (member.Level == level || ReferenceEquals(member.Owner, owner)) return false;
            transaction = new BurtGISceneVoxelFineFrameTransaction(owner, commandBuffer, context,
                generation, bounds, sourceEpoch, level, geometryEndBytes) { stream = candidate };
            candidate.Members.Add(transaction);
            return true;
        }

        public BurtGISceneVoxelFineResources Owner => owner;
        public uint Generation { get; }
        public Vector4 Bounds { get; }
        public ulong SourceEpoch { get; }
        public int CameraId => cameraId;
        public int RequestFrame => requestFrame;
        public int Level { get; }
        public int Resolution => owner.Resolution;
        public bool IsCancelled => phase == Phase.Cancelled || !owner.OwnsRecordedFrame(this);
        public bool IsScheduled => phase == Phase.Scheduled && owner.OwnsRecordedFrame(this) && MatchesContext(frameContext);

        internal bool CanRecordLighting(CommandBuffer commandBuffer)
        {
            return phase == Phase.GeometryRecorded && owner.OwnsRecordedFrame(this) &&
                ReferenceEquals(commandBuffer, producerBuffer) && MatchesContext(frameContext) &&
                producerBuffer.sizeInBytes >= geometryEndBytes;
        }

        // Environment/SH/light-grid/cache uploads must already be in this same CB.
        // Only final fine bounds, geometry and material/lighting bindings are added.
        public bool RecordLighting(CommandBuffer commandBuffer, ComputeShader shader, int stepBudget = 512)
        {
            if (!CanRecordLighting(commandBuffer)) return false;
            try
            {
                if (!owner.RecordFrameLighting(this, commandBuffer, shader, stepBudget)) return false;
                lightingEndBytes = producerBuffer.sizeInBytes;
                phase = Phase.LightingRecorded;
                return true;
            }
            catch { Cancel(); throw; }
        }

        // Performs the actual queue handoff instead of trusting a caller's boolean
        // assertion. ExecuteLegacyCommandBuffer flushes prior shared graphics work;
        // it does not Submit or wait for the GPU. Invoke at a producer pass boundary
        // where no further unmarked commands are appended to a flushed shared CB.
        public bool Submit(BurtRenderGraphContext context)
        {
            return SubmitBatch(context, new[] { this }, 1u << Level);
        }

        // All selected levels are submitted atomically in ONE graphics queue call.
        // Every token registered for this private CB must be included, and the mask
        // must equal the producer's expected active-level mask, not merely a subset
        // of whichever levels happened to record successfully.
        public static bool SubmitBatch(BurtRenderGraphContext context,
            IReadOnlyList<BurtGISceneVoxelFineFrameTransaction> transactions, uint expectedLevelMask)
        {
            if (transactions == null || transactions.Count == 0 || expectedLevelMask == 0 || (expectedLevelMask & ~63u) != 0) return false;
            var first = transactions[0];
            if (first == null || first.stream.Submitted || first.stream.Cancelled ||
                first.stream.Members.Count != transactions.Count) return false;
            uint actualMask = 0;
            foreach (var member in transactions)
            {
                if (member == null || !ReferenceEquals(member.stream, first.stream) ||
                    member.phase != Phase.LightingRecorded || !member.owner.OwnsRecordedFrame(member) ||
                    !member.MatchesContext(context) || ReferenceEquals(member.producerBuffer, context.CommandBuffer) ||
                    member.producerBuffer.sizeInBytes < member.lightingEndBytes || (actualMask & (1u << member.Level)) != 0)
                    return false;
                actualMask |= 1u << member.Level;
            }
            if (actualMask != expectedLevelMask) return false;
            try
            {
                context.ExecuteLegacyCommandBuffer(first.producerBuffer);
                first.stream.Submitted = true;
                foreach (var member in transactions) member.phase = Phase.Scheduled;
                return true;
            }
            catch { first.Cancel(); throw; }
        }

        public bool MatchesScheduledSource(BurtRenderGraphContext context, uint generation,
            Vector4 bounds, ulong sourceEpoch)
        {
            return IsScheduled && MatchesContext(context) && generation == Generation &&
                sourceEpoch == SourceEpoch && bounds.Equals(Bounds);
        }

        // Binds the complete ScreenProbe ABI only after ALL selected source tuples
        // match the same submitted transaction. expectedBounds/epochs are the
        // consumer's independently resolved current values, indexed by level 0..5.
        // boundLevelCount is 1 for the single-level static kernel, 6 for multi-level.
        // First rollout is graphics-only: an async queue switch or private consumer
        // CB is rejected, since this receipt is not a cross-queue GPU fence.
        public static bool RecordBindFullQuery(CommandBuffer commandBuffer, ComputeShader shader, int kernel,
            BurtRenderGraphContext context, IReadOnlyList<BurtGISceneVoxelFineFrameTransaction> transactions,
            uint selectedLevelMask, IReadOnlyList<Vector4> expectedBounds, IReadOnlyList<ulong> expectedSourceEpochs,
            int boundLevelCount, int stepBudget = 512)
        {
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (kernel < 0) throw new ArgumentOutOfRangeException(nameof(kernel));
            // Explicitly revoke any previous dispatch's ready flags even on failure.
            // The caller must skip the fine dispatch when this method returns false.
            commandBuffer.SetComputeIntParam(shader, QueryReadyId, 0);
            commandBuffer.SetComputeIntParam(shader, QueryMaskId, 0);
            if (context == null || !ReferenceEquals(commandBuffer, context.CommandBuffer) ||
                (boundLevelCount != 1 && boundLevelCount != 6) || transactions == null || transactions.Count == 0 ||
                expectedBounds == null || expectedBounds.Count < 6 || expectedSourceEpochs == null || expectedSourceEpochs.Count < 6 ||
                selectedLevelMask == 0 || (selectedLevelMask & ~((1u << boundLevelCount) - 1u)) != 0)
                return false;
            var byLevel = new BurtGISceneVoxelFineFrameTransaction[6];
            var first = transactions[0];
            if (first == null || !first.IsScheduled || first.stream.Members.Count != transactions.Count) return false;
            uint actualMask = 0;
            foreach (var member in transactions)
            {
                if (member == null || !ReferenceEquals(member.stream, first.stream) ||
                    member.Level >= boundLevelCount || byLevel[member.Level] != null ||
                    !member.MatchesScheduledSource(context, member.owner.Generation,
                        expectedBounds[member.Level], expectedSourceEpochs[member.Level]))
                    return false;
                byLevel[member.Level] = member;
                actualMask |= 1u << member.Level;
            }
            if (actualMask != selectedLevelMask) return false;
            var bounds = new Vector4[6];
            var parameters = new Vector4[6];
            for (int level = 0; level < boundLevelCount; level++)
            {
                var member = byLevel[level];
                // All statically declared SRVs are bound. An unselected level uses
                // a real selected owner's resources as an unread typed placeholder;
                // its ready bits/bounds/mask remain zero and cannot select it.
                BindQueryLevel(commandBuffer, shader, kernel, level, (member ?? first).owner);
                if (member == null) continue;
                bounds[level] = member.Bounds;
                parameters[level] = new Vector4(member.owner.Resolution, member.owner.LogicalCapacity,
                    1 | 2 | 4 | 8, Mathf.Clamp(stepBudget, 96, 512));
            }
            commandBuffer.SetComputeVectorArrayParam(shader, QueryBoundsId, bounds);
            commandBuffer.SetComputeVectorArrayParam(shader, QueryParamsId, parameters);
            commandBuffer.SetComputeIntParam(shader, QueryMaskId, (int)selectedLevelMask);
            commandBuffer.SetComputeIntParam(shader, QueryReadyId, 1);
            return true;
        }

        private static int[,] CreateQueryResourceIds()
        {
            var ids = new int[6, QueryResourceSuffixes.Length];
            for (int level = 0; level < 6; level++)
                for (int resource = 0; resource < QueryResourceSuffixes.Length; resource++)
                    ids[level, resource] = Shader.PropertyToID("_BurtGIScreenProbeFine" + level + QueryResourceSuffixes[resource]);
            return ids;
        }

        private static void BindQueryLevel(CommandBuffer commandBuffer, ComputeShader shader, int kernel,
            int level, BurtGISceneVoxelFineResources source)
        {
            var tree = source.Hierarchy;
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 0], source.Low);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 1], source.High);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 2], source.Offsets);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 3], tree.LeafLow);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 4], tree.LeafHigh);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 5], tree.ParentLow);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 6], tree.ParentHigh);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 7], tree.RootLow);
            commandBuffer.SetComputeTextureParam(shader, kernel, QueryResourceIds[level, 8], tree.RootHigh);
            commandBuffer.SetComputeBufferParam(shader, kernel, QueryResourceIds[level, 9], source.Owners);
            commandBuffer.SetComputeBufferParam(shader, kernel, QueryResourceIds[level, 10], source.Materials);
            commandBuffer.SetComputeBufferParam(shader, kernel, QueryResourceIds[level, 11], source.Lighting);
        }

        private bool MatchesContext(BurtRenderGraphContext context)
        {
            return context != null && ReferenceEquals(context, frameContext) &&
                ReferenceEquals(context.Request, request) && ReferenceEquals(request.Camera, camera) && camera != null &&
                camera.GetInstanceID() == cameraId && request.RenderFrameIndex == requestFrame &&
                ReferenceEquals(context.CommandBuffer, graphicsBuffer);
        }

        public void Cancel() { owner.CancelRecordedFrame(this); phase = Phase.Cancelled; }
        internal void CancelFromOwner() { phase = Phase.Cancelled; stream.Cancel(); }

        // Discarding a never-submitted record revokes it. Disposing a successfully
        // submitted receipt does not cancel GPU commands or revoke later same-frame
        // consumers; use Cancel explicitly when the owning producer fails instead.
        public void Dispose() { if (phase != Phase.Scheduled) Cancel(); }

        private sealed class FrameStream
        {
            private readonly BurtRenderGraphContext context;
            private readonly BurtRenderRequest request;
            private readonly Camera camera;
            private readonly int frame;
            public readonly List<BurtGISceneVoxelFineFrameTransaction> Members = new List<BurtGISceneVoxelFineFrameTransaction>();
            public bool Submitted, Cancelled;
            public FrameStream(BurtRenderGraphContext context)
            { this.context = context; request = context.Request; camera = request.Camera; frame = request.RenderFrameIndex; }
            public bool MatchesContext(BurtRenderGraphContext candidate)
            { return ReferenceEquals(context, candidate) && candidate != null && ReferenceEquals(candidate.Request, request) && camera != null && ReferenceEquals(request.Camera, camera) && request.RenderFrameIndex == frame; }
            public void Cancel() { Cancelled = true; foreach (var member in Members) member.phase = Phase.Cancelled; }
        }
    }
}
