using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    // Explicit camera-scoped rollout. This is not a profile default or a replacement
    // for unsupported GI routes. Enabling is Editor-only until end-to-end acceptance.
    internal static class BurtGISceneVoxelFineRuntime
    {
        [Serializable]
        private sealed class Status
        {
            public bool enabled, ready;
            public int capacity, preparedFrame = -1, consumedFrame = -1, ownedLevels;
            public uint levelMask;
            public ulong epoch;
            public string selectedKernel = "Coarse(default)", failure = "";
        }

        private sealed class State
        {
            public Camera Camera;
            public readonly Status Report = new Status();
            public readonly List<BurtGISceneVoxelFineFrameTransaction> Frames = new List<BurtGISceneVoxelFineFrameTransaction>(6);
            public readonly Vector4[] Bounds = new Vector4[6];
            public readonly ulong[] Epochs = new ulong[6];
            public int Signature, RequestFrame = -1;
            public bool HasSignature, Supported, HasSupportState, HasScreenSource, HasHashGridSource;
            public string SupportReason, ProducerFailure;
            public BurtRenderRequest Request;
        }

        private static readonly Dictionary<int, State> States = new Dictionary<int, State>();
        private static ComputeShader buildShader, octreeShader;

        public static bool IsRequested(Camera camera)
        {
            return camera != null && States.TryGetValue(camera.GetInstanceID(), out var state) &&
                state.Camera == camera && state.Report.enabled;
        }

        public static string GetStatus(Camera camera)
        {
            if (camera == null || !States.TryGetValue(camera.GetInstanceID(), out var state) || state.Camera != camera)
                return JsonUtility.ToJson(new Status());
            int count = 0;
            for (int level = 0; level < 6; ++level)
                if (BurtGISceneVoxelClipmapStateUtility.TryGetFineResources(camera, level, out _, out _)) ++count;
            state.Report.ownedLevels = count;
            return JsonUtility.ToJson(state.Report);
        }

#if UNITY_EDITOR
        // Invoke between render requests, never from a render callback. It does not
        // change a scene, profile, material, camera pose, or save any user asset.
        public static void Configure(Camera camera, bool enabled, int capacity)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (capacity < 0 || capacity > BurtGISceneVoxelFineResources.MaximumCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (!States.TryGetValue(camera.GetInstanceID(), out var state) || state.Camera != camera)
            {
                state = new State { Camera = camera };
                States[camera.GetInstanceID()] = state;
            }
            if (state.Report.enabled == enabled && (!enabled || state.Report.capacity == capacity)) return;
            CancelFrames(state);
            InvalidateHistories(camera);
            BurtGISceneVoxelClipmapStateUtility.ReleaseFine(camera);
            state.Report.enabled = enabled;
            state.Report.capacity = capacity;
            state.Report.ready = false;
            state.Report.levelMask = 0;
            state.Report.preparedFrame = state.Report.consumedFrame = -1;
            state.Report.selectedKernel = enabled ? "PendingFine" : "Coarse(default)";
            state.Report.failure = "";
            state.HasSignature = false;
            state.HasSupportState = false;
            state.ProducerFailure = null;
            state.RequestFrame = -1;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterCleanup()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseAll;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseAll;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ReleaseAll()
        {
            foreach (var state in States.Values)
            {
                CancelFrames(state);
                if (state.Camera != null) BurtGISceneVoxelClipmapStateUtility.ReleaseFine(state.Camera);
            }
            States.Clear();
        }

        // Bookkeeping only: normal pipeline teardown calls this AFTER the
        // clipmap owner has released every camera's native resources. Do not
        // call from a producer/consumer callback or treat it as a GPU fence.
        internal static void ReleaseCameraStates()
        {
            foreach (var state in States.Values)
            {
                CancelFrames(state);
                state.Request = null;
                state.Camera = null;
            }
            States.Clear();
        }

        // Called after the existing destroyed-camera owner prune. A live but
        // disabled/offscreen camera intentionally retains its opt-in and state.
        // This method never disposes a fine pool or modifies another camera.
        internal static void PruneDisposedCameraStates()
        {
            List<int> removalKeys = null;
            foreach (var pair in States)
            {
                if (pair.Value.Camera != null) continue;
                CancelFrames(pair.Value);
                pair.Value.Request = null;
                pair.Value.Camera = null;
                if (removalKeys == null) removalKeys = new List<int>();
                removalKeys.Add(pair.Key);
            }
            if (removalKeys == null) return;
            foreach (var key in removalKeys) States.Remove(key);
        }

        // Called before graph resource import, so configuration invalidation is
        // consumed by this request's history allocation rather than the next one.
        public static void PrepareRequest(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var camera = request != null ? request.Camera : null;
            if (!IsRequested(camera)) return;
            var state = States[camera.GetInstanceID()];
            if (ReferenceEquals(state.Request, request) && state.RequestFrame == request.RenderFrameIndex) return;
            CancelFrames(state);
            state.Request = request;
            state.RequestFrame = request.RenderFrameIndex;
            state.Report.ready = false;
            state.Report.levelMask = 0;
            state.Report.preparedFrame = state.Report.consumedFrame = -1;
            var gi = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(request, asset);
            var probe = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(request, asset);
            int signature;
            unchecked { signature = (gi.CreateHistorySignature() * 397) ^ probe.CreateHistorySignature() ^ state.Report.capacity ^ 448; }
            if (!state.HasSignature || state.Signature != signature)
            {
                InvalidateHistories(camera);
                state.Signature = signature;
                state.HasSignature = true;
            }
            string reason = null;
            if (!gi.Enabled || !probe.Enabled || gi.FinalGather != ScreenSpaceGlobalIlluminationFinalGather.ScreenProbe)
                reason = "FineRequiresEnabledScreenProbe";
            else if (!probe.TraceCompact || !SupportsTraceSources(probe.TraceSources) ||
                probe.TraceHardwareRay || (probe.RadianceCacheType != ScreenProbeRadianceCacheType.None &&
                    probe.RadianceCacheType != ScreenProbeRadianceCacheType.HashGrid) ||
                probe.TraceUseWorldRadianceClipMap)
                reason = "FineRequiresCompactSceneVoxelWithOptionalScreenOrHashGrid";
            else if (gi.SceneVoxelMultiBounce || gi.UseTranslucencyVolume || gi.SceneVoxelLightingType != SceneVoxelLightingType.Direct)
                reason = "FineLightingRouteNotYetSupported";
            else if (BurtGISceneVoxelVolume.TryGetBestForCamera(camera, out _))
                reason = "FineExternalVoxelVolumeNotYetSupported";
            // External-volume/device route availability can change independently
            // of serialized settings. Reject the old history once on that transition.
            if (state.HasSupportState && state.SupportReason != reason) InvalidateHistories(camera);
            state.SupportReason = reason;
            state.HasSupportState = true;
            state.Supported = reason == null;
            state.HasScreenSource = probe.TraceScreen;
            state.HasHashGridSource = probe.TraceHashGridCache &&
                BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationRadianceCacheHashGrid(request, asset);
            state.Report.failure = reason ?? state.ProducerFailure ?? "";
            state.Report.selectedKernel = gi.SceneVoxelClipMapCount <= 1 ? "ScreenProbeTraceVoxelFineCS" : "ScreenProbeTraceVoxelFineClipmapsCS";
        }

        // Called AFTER the common clipmap coordinate update but BEFORE dense history.
        // All levels are built every frame during correctness rollout; no stale
        // coarse RGB, Probe SH or CPU-completed validity is used to fill missing data.
        public static void Produce(BurtRenderGraphContext context, BurtScreenSpaceGlobalIlluminationSettings gi)
        {
            var camera = context.Request.Camera;
            var state = States[camera.GetInstanceID()];
            if (!state.Supported) return;
            if (!ReferenceEquals(state.Request, context.Request) || state.RequestFrame != context.Request.RenderFrameIndex ||
                !context.HasSharedCommandBuffer)
            {
                Fail(state, "FineRequiresCurrentSharedGraphicsRequest");
                return;
            }
            var cmd = new CommandBuffer { name = "BurtGI Fine Scene Producer" };
            try
            {
                if (!BurtGISceneVoxelClipmapStateUtility.ConfigureFineResources(camera, true, state.Signature))
                    throw new InvalidOperationException("FineCameraOwnerNotInitialized");
                int count = Mathf.Clamp(gi.SceneVoxelClipMapCount, 1, 6);
                uint mask = (1u << count) - 1u;
                var owners = new BurtGISceneVoxelFineResources[count];
                for (int level = 0; level < count; ++level)
                    if (!BurtGISceneVoxelClipmapStateUtility.TryEnsureFineResources(camera, level, state.Report.capacity,
                        out owners[level], out state.Bounds[level], out _))
                        throw new InvalidOperationException("FineOwnerAllocationFailed:" + level);
                if (buildShader == null) buildShader = Resources.Load<ComputeShader>("BurtGISceneVoxelFineBuild");
                if (octreeShader == null) octreeShader = Resources.Load<ComputeShader>("BurtGISceneVoxelOctreeBuild");
                ++state.Report.epoch;
                for (int level = 0; level < count; ++level)
                {
                    if (!BurtGISceneVoxelGpuRasterizerUtility.TryRecordFineGeometryOwned(cmd, camera, state.Bounds[level],
                        owners[level], gi, buildShader, octreeShader, out var generation, out var reason, true))
                        throw new InvalidOperationException(reason);
                    state.Epochs[level] = state.Report.epoch;
                    if (!owners[level].TryBeginRecordedFrame(cmd, context, generation, state.Bounds[level],
                        state.Epochs[level], level, out var frame))
                        throw new InvalidOperationException("FineGeometryTransactionRejected:" + level);
                    state.Frames.Add(frame);
                    if (!BurtScreenSpaceGlobalIlluminationRadianceCacheClipMapPass.TryRecordFineSceneVoxelFrameLighting(cmd, context, frame, out reason))
                        throw new InvalidOperationException(reason);
                }
                // Native raster temporarily bound its own dummy target and viewport.
                // Restore the normal camera target explicitly before leaving this pass.
                cmd.ClearRandomWriteTargets();
                cmd.SetRenderTarget(context.CameraColorTarget.Identifier, context.CameraDepthTarget.Identifier);
                BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
                if (!BurtGISceneVoxelFineFrameTransaction.SubmitBatch(context, state.Frames, mask))
                    throw new InvalidOperationException("FineAtomicGraphicsSubmissionRejected");
                // Recovering source availability changes the lighting history,
                // but must not invalidate the fine transaction just submitted.
                if (state.ProducerFailure != null) InvalidateHistories(camera, false);
                state.ProducerFailure = null;
                state.Report.levelMask = mask;
                state.Report.preparedFrame = context.Request.RenderFrameIndex;
                state.Report.ready = true;
                state.Report.failure = "";
            }
            catch (Exception exception)
            {
                // Discard the whole unsubmitted stream; a failed submission never
                // publishes a consumer receipt. Owners remain allocated for safe reuse.
                Fail(state, exception.Message);
            }
            finally { cmd.Dispose(); }
        }

        public static bool BindTrace(CommandBuffer cmd, ComputeShader shader, int kernel, BurtRenderGraphContext context)
        {
            var camera = context.Request.Camera;
            if (!IsRequested(camera)) return false;
            var state = States[camera.GetInstanceID()];
            if (!state.Report.ready || state.Report.preparedFrame != context.Request.RenderFrameIndex)
                return false;
            foreach (var frame in state.Frames)
            {
                if (!BurtGISceneVoxelClipmapStateUtility.TryGetFineResources(camera, frame.Level,
                    out var currentOwner, out var currentBounds) || !ReferenceEquals(currentOwner, frame.Owner) ||
                    !currentBounds.Equals(frame.Bounds))
                {
                    Fail(state, "FineTraceCameraOwnerOrBoundsChanged");
                    return false;
                }
                state.Bounds[frame.Level] = currentBounds;
            }
            bool bound = BurtGISceneVoxelFineFrameTransaction.RecordBindFullQuery(cmd, shader, kernel, context,
                state.Frames, state.Report.levelMask, state.Bounds, state.Epochs, state.Frames.Count == 1 ? 1 : 6, 512);
            if (!bound) Fail(state, "FineTraceSourceBindingRejected");
            return bound;
        }

        public static bool IsPreparedFrame(BurtRenderGraphContext context)
        {
            if (context?.Request?.Camera == null || !IsRequested(context.Request.Camera)) return false;
            var state = States[context.Request.Camera.GetInstanceID()];
            return state.Report.ready && ReferenceEquals(state.Request, context.Request) &&
                state.Report.preparedFrame == context.Request.RenderFrameIndex;
        }

        internal static bool SupportsTraceSources(ScreenProbeTraceSource sources)
        {
            const ScreenProbeTraceSource allowed = ScreenProbeTraceSource.SceneVoxel |
                ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.HashGridCache;
            return (sources & ScreenProbeTraceSource.SceneVoxel) != 0 && (sources & ~allowed) == 0;
        }

        // A failed fine producer must not suppress the independent Screen source.
        // Permit this only for a supported, current mixed request, never for an
        // unsupported cache/lighting configuration or a previous camera request.
        public static bool CanContinueScreenTrace(BurtRenderGraphContext context)
        {
            if (context?.Request?.Camera == null || !IsRequested(context.Request.Camera)) return false;
            var state = States[context.Request.Camera.GetInstanceID()];
            return state.Supported && state.HasScreenSource &&
                ReferenceEquals(state.Request, context.Request) &&
                state.RequestFrame == context.Request.RenderFrameIndex;
        }

        public static bool CanContinueIndependentTrace(BurtRenderGraphContext context)
        {
            if (context?.Request?.Camera == null || !IsRequested(context.Request.Camera)) return false;
            var state = States[context.Request.Camera.GetInstanceID()];
            return state.Supported && (state.HasScreenSource || state.HasHashGridSource) &&
                ReferenceEquals(state.Request, context.Request) &&
                state.RequestFrame == context.Request.RenderFrameIndex;
        }

        public static void MarkTraceRecorded(BurtRenderGraphContext context)
        {
            if (IsRequested(context.Request.Camera))
                States[context.Request.Camera.GetInstanceID()].Report.consumedFrame = context.Request.RenderFrameIndex;
        }

        private static void Fail(State state, string reason)
        {
            // PrepareRequest resets per-frame readiness, not fault identity.
            // Repeated failure must not erase previous SceneColor every frame:
            // an independent Screen source still needs that input to converge.
            if (state.ProducerFailure != reason || state.Report.ready) InvalidateHistories(state.Camera);
            state.ProducerFailure = reason;
            CancelFrames(state);
            state.Report.ready = false;
            state.Report.levelMask = 0;
            state.Report.failure = reason;
        }

        private static void CancelFrames(State state)
        {
            foreach (var frame in state.Frames) frame.Cancel();
            state.Frames.Clear();
        }

        private static void InvalidateHistories(Camera camera, bool invalidateFineSource = true)
        {
            const string reason = "FineVoxelModeChanged";
            if (invalidateFineSource) BurtGISceneVoxelClipmapStateUtility.Invalidate(camera);
            BurtGISceneVoxelHistoryUtility.InvalidateHistory(camera, reason);
            BurtScreenSpaceGlobalIlluminationScreenProbeHistoryUtility.InvalidateHistory(camera, reason);
            BurtScreenSpaceGlobalIlluminationHistoryUtility.InvalidateHistory(camera, reason);
            BurtScreenSpaceGlobalIlluminationIndirectChannelHistoryUtility.InvalidateHistory(camera);
            BurtRadianceCacheClipMapHistoryUtility.InvalidateHistory(camera, reason);
            BurtRadianceCacheHashGridHistoryUtility.InvalidateHistory(camera, reason);
            BurtGITranslucencyVolumeHistoryUtility.InvalidateHistory(camera, reason);
            BurtGIPreviousSceneColorHistoryUtility.InvalidateHistory(camera);
        }
    }
}
