using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public readonly struct BurtTemporalAASettings
    {
        public static readonly BurtTemporalAASettings Default = new BurtTemporalAASettings(false, 0.93f, 1.0f, 0.95f, 0.04f, 0.28f, 1.15f, 1.25f, 1.35f, 16f, 56f, 0.22f, 0.96f, 0.06f, 0.5f, 0f, 0.875f, 0.65f, 0.35f, 0.18f, 1.2f, 1.1f, 1.15f, 0.12f);

        public bool Enabled { get; }
        public float Feedback { get; }
        public float JitterScale { get; }
        public float ClampStrength { get; }
        public float Sharpness { get; }
        public float StaticEdgeRelaxation { get; }
        public float LumaRejectionStrength { get; }
        public float ClipRejectionStrength { get; }
        public float DepthRejectionStrength { get; }
        public float MotionRejectionStart { get; }
        public float MotionRejectionRange { get; }
        public float HistoryConfidenceWeight { get; }
        public float HistoryConfidenceBoost { get; }
        public float ConfidenceGrowth { get; }
        public float AntiFlickering { get; }
        public float MotionVectorRejection { get; }
        public float BaseBlendFactor { get; }
        public float ResponsiveRejectionStrength { get; }
        public float UntrustedMotionFeedbackScale { get; }
        public float DisocclusionFeedbackScale { get; }
        public float MotionEdgeResponsiveStrength { get; }
        public float DepthEdgeResponsiveStrength { get; }
        public float HistoryClampTightness { get; }
        public float DepthWeightedFilterFloor { get; }

        public BurtTemporalAASettings(bool enabled, float feedback, float jitterScale, float clampStrength, float sharpness, float staticEdgeRelaxation)
            : this(enabled, feedback, jitterScale, clampStrength, sharpness, staticEdgeRelaxation, 1.15f, 1.25f, 1.35f, 16f, 56f, 0.22f, 0.96f, 0.06f, 0.5f, 0f, 0.875f, 0.65f, 0.35f, 0.18f, 1.2f, 1.1f, 1.15f, 0.12f)
        {
        }

        public BurtTemporalAASettings(
            bool enabled,
            float feedback,
            float jitterScale,
            float clampStrength,
            float sharpness,
            float staticEdgeRelaxation,
            float lumaRejectionStrength,
            float clipRejectionStrength,
            float depthRejectionStrength,
            float motionRejectionStart,
            float motionRejectionRange,
            float historyConfidenceWeight,
            float historyConfidenceBoost,
            float confidenceGrowth,
            float antiFlickering,
            float motionVectorRejection,
            float baseBlendFactor,
            float responsiveRejectionStrength,
            float untrustedMotionFeedbackScale,
            float disocclusionFeedbackScale,
            float motionEdgeResponsiveStrength,
            float depthEdgeResponsiveStrength,
            float historyClampTightness,
            float depthWeightedFilterFloor)
        {
            Enabled = enabled;
            Feedback = feedback;
            JitterScale = jitterScale;
            ClampStrength = clampStrength;
            Sharpness = sharpness;
            StaticEdgeRelaxation = staticEdgeRelaxation;
            LumaRejectionStrength = lumaRejectionStrength;
            ClipRejectionStrength = clipRejectionStrength;
            DepthRejectionStrength = depthRejectionStrength;
            MotionRejectionStart = motionRejectionStart;
            MotionRejectionRange = motionRejectionRange;
            HistoryConfidenceWeight = historyConfidenceWeight;
            HistoryConfidenceBoost = historyConfidenceBoost;
            ConfidenceGrowth = confidenceGrowth;
            AntiFlickering = antiFlickering;
            MotionVectorRejection = motionVectorRejection;
            BaseBlendFactor = baseBlendFactor;
            ResponsiveRejectionStrength = responsiveRejectionStrength;
            UntrustedMotionFeedbackScale = untrustedMotionFeedbackScale;
            DisocclusionFeedbackScale = disocclusionFeedbackScale;
            MotionEdgeResponsiveStrength = motionEdgeResponsiveStrength;
            DepthEdgeResponsiveStrength = depthEdgeResponsiveStrength;
            HistoryClampTightness = historyClampTightness;
            DepthWeightedFilterFloor = depthWeightedFilterFloor;
        }
    }

    public enum BurtTemporalAAVelocityMode
    {
        Disabled = 0,
        CameraOnly = 1,
        CameraAndObject = 2
    }

    public sealed class BurtTemporalAARequestState
    {
        public static readonly BurtTemporalAARequestState Disabled = new BurtTemporalAARequestState();

        public bool Enabled { get; private set; }
        public bool HistoryValid { get; internal set; }
        public int FrameIndex { get; private set; }
        public Vector2 Jitter { get; private set; }
        public Vector2 JitterPixels { get; private set; }
        public Matrix4x4 NonJitteredProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 JitteredProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 ViewMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 CurrentViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 CurrentNonJitteredViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 PreviousViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 PreviousNonJitteredViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 InverseCurrentViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public BurtTemporalAASettings Settings { get; private set; } = BurtTemporalAASettings.Default;
        public BurtTemporalAAVelocityMode VelocityMode { get; internal set; } = BurtTemporalAAVelocityMode.Disabled;
        public bool ObjectMotionVectorPassDrawn { get; internal set; }

        public static BurtTemporalAARequestState CreateDisabled(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            var gpuProjectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, true);
            var viewProjection = gpuProjectionMatrix * viewMatrix;
            return new BurtTemporalAARequestState
            {
                Enabled = false,
                ViewMatrix = viewMatrix,
                NonJitteredProjectionMatrix = projectionMatrix,
                JitteredProjectionMatrix = projectionMatrix,
                CurrentViewProjectionMatrix = viewProjection,
                CurrentNonJitteredViewProjectionMatrix = viewProjection,
                PreviousViewProjectionMatrix = viewProjection,
                PreviousNonJitteredViewProjectionMatrix = viewProjection,
                InverseCurrentViewProjectionMatrix = viewProjection.inverse,
                Settings = BurtTemporalAASettings.Default,
                VelocityMode = BurtTemporalAAVelocityMode.Disabled
            };
        }

        internal static BurtTemporalAARequestState Create(
            BurtTemporalAASettings settings,
            int frameIndex,
            Vector2 jitterPixels,
            Vector2 jitter,
            Matrix4x4 viewMatrix,
            Matrix4x4 nonJitteredProjectionMatrix,
            Matrix4x4 jitteredProjectionMatrix,
            Matrix4x4 previousViewProjectionMatrix,
            Matrix4x4 previousNonJitteredViewProjectionMatrix,
            bool historyValid)
        {
            var gpuJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(jitteredProjectionMatrix, true);
            var gpuNonJitteredProjectionMatrix = GL.GetGPUProjectionMatrix(nonJitteredProjectionMatrix, true);
            var currentViewProjection = gpuJitteredProjectionMatrix * viewMatrix;
            var currentNonJitteredViewProjection = gpuNonJitteredProjectionMatrix * viewMatrix;
            return new BurtTemporalAARequestState
            {
                Enabled = settings.Enabled,
                HistoryValid = historyValid,
                FrameIndex = frameIndex,
                JitterPixels = jitterPixels,
                Jitter = jitter,
                ViewMatrix = viewMatrix,
                NonJitteredProjectionMatrix = nonJitteredProjectionMatrix,
                JitteredProjectionMatrix = jitteredProjectionMatrix,
                CurrentViewProjectionMatrix = currentViewProjection,
                CurrentNonJitteredViewProjectionMatrix = currentNonJitteredViewProjection,
                PreviousViewProjectionMatrix = previousViewProjectionMatrix,
                PreviousNonJitteredViewProjectionMatrix = previousNonJitteredViewProjectionMatrix,
                InverseCurrentViewProjectionMatrix = currentViewProjection.inverse,
                Settings = settings,
                VelocityMode = BurtTemporalAAVelocityMode.CameraOnly
            };
        }
    }

    internal readonly struct BurtTemporalAAHistoryTextures
    {
        public RenderTexture Color { get; }
        public RenderTexture Depth { get; }
        public RenderTexture Confidence { get; }
        public RenderTexture AntiFlicker { get; }

        public BurtTemporalAAHistoryTextures(RenderTexture color, RenderTexture depth, RenderTexture confidence, RenderTexture antiFlicker)
        {
            Color = color;
            Depth = depth;
            Confidence = confidence;
            AntiFlicker = antiFlicker;
        }
    }

    internal readonly struct BurtTemporalAAHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public bool HasDepthHistory { get; }
        public bool DepthDescriptorMatches { get; }
        public bool HasConfidenceHistory { get; }
        public bool ConfidenceDescriptorMatches { get; }
        public bool HasAntiFlickerHistory { get; }
        public bool AntiFlickerDescriptorMatches { get; }
        public int Width { get; }
        public int Height { get; }
        public RenderTextureFormat Format { get; }
        public int FrameIndex { get; }
        public int HistoryAge { get; }
        public string LastInvalidationReason { get; }

        public BurtTemporalAAHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            bool hasDepthHistory,
            bool depthDescriptorMatches,
            bool hasConfidenceHistory,
            bool confidenceDescriptorMatches,
            bool hasAntiFlickerHistory,
            bool antiFlickerDescriptorMatches,
            int width,
            int height,
            RenderTextureFormat format,
            int frameIndex,
            int historyAge,
            string lastInvalidationReason)
        {
            HasHistory = hasHistory;
            DescriptorMatches = descriptorMatches;
            HasDepthHistory = hasDepthHistory;
            DepthDescriptorMatches = depthDescriptorMatches;
            HasConfidenceHistory = hasConfidenceHistory;
            ConfidenceDescriptorMatches = confidenceDescriptorMatches;
            HasAntiFlickerHistory = hasAntiFlickerHistory;
            AntiFlickerDescriptorMatches = antiFlickerDescriptorMatches;
            Width = width;
            Height = height;
            Format = format;
            FrameIndex = frameIndex;
            HistoryAge = historyAge;
            LastInvalidationReason = lastInvalidationReason;
        }
    }

    internal static class BurtTemporalAAUtility
    {
        private const float ProjectionChangeEpsilon = 0.0001f;
        private const int HaltonSequenceLength = 1024;
        private const int CameraStatePruneInterval = 128;

        private sealed class CameraState
        {
            public Camera Camera;
            public int FrameIndex;
            public int FirstValidFrameIndex;
            public BurtRendererMode CurrentRendererMode = BurtRendererMode.Forward;
            public BurtRendererMode PreviousRendererMode = BurtRendererMode.Forward;
            public Vector3 PreviousCameraPosition;
            public Quaternion PreviousCameraRotation = Quaternion.identity;
            public bool PreviousOrthographic;
            public float PreviousFieldOfView;
            public float PreviousOrthographicSize;
            public float PreviousNearClipPlane;
            public float PreviousFarClipPlane;
            public int PreviousTargetTextureId;
            public int PreviousTargetWidth;
            public int PreviousTargetHeight;
            public Vector2 PreviousRenderScale = Vector2.one;
            public Matrix4x4 PreviousViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredProjectionMatrix = Matrix4x4.identity;
            public RenderTexture ColorHistory;
            public RenderTexture DepthHistory;
            public RenderTexture ConfidenceHistory;
            public RenderTexture AntiFlickerHistory;
            public RenderTextureDescriptor ColorDescriptor;
            public RenderTextureDescriptor DepthDescriptor;
            public RenderTextureDescriptor ConfidenceDescriptor;
            public RenderTextureDescriptor AntiFlickerDescriptor;
            public bool HasValidHistory;
            public bool HasPreviousCameraState;
            public string LastInvalidationReason = "NeverAllocated";
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();
        private static readonly List<int> CameraStateRemovalKeys = new List<int>();
        private static int cameraStatePruneCounter;

        public static void RecoverCameraProjectionForCulling(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (IsAuxiliaryRenderCamera(camera))
            {
                return;
            }

            ResetCameraProjectionState(camera);
        }

        public static void RestoreCameraProjectionAfterJitter(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            // Leave Camera back in Unity-owned projection mode; otherwise FOV/aspect edits stay ignored.
            ResetCameraProjectionState(camera);
        }

        private static void ResetCameraProjectionState(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            camera.ResetCullingMatrix();
            camera.ResetProjectionMatrix();
            camera.nonJitteredProjectionMatrix = camera.projectionMatrix;
        }

        private static bool IsAuxiliaryRenderCamera(Camera camera)
        {
            return camera != null && (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection);
        }

        public static BurtTemporalAARequestState PrepareRequest(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var camera = request != null ? request.Camera : null;
            var viewMatrix = camera != null ? camera.worldToCameraMatrix : Matrix4x4.identity;
            var projectionMatrix = camera != null ? camera.projectionMatrix : Matrix4x4.identity;

            if (!ShouldUseTemporalAA(request, asset))
            {
                InvalidateHistory(camera);
                return BurtTemporalAARequestState.CreateDisabled(viewMatrix, projectionMatrix);
            }

            var settings = BurtPostProcessUtility.ResolveTemporalAASettings(request, asset);
            var cameraId = camera.GetInstanceID();
            var state = GetOrCreateState(cameraId);
            state.Camera = camera;
            PruneDisposedCameraStates();
            var rendererMode = asset != null ? asset.RendererMode : BurtRendererMode.Forward;
            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateScalarHistoryDescriptor(camera);
            var confidenceDescriptor = CreateScalarHistoryDescriptor(camera);
            var antiFlickerDescriptor = CreateAntiFlickerHistoryDescriptor(camera);
            var colorMatches = state.ColorHistory != null && Matches(state.ColorDescriptor, colorDescriptor);
            var depthMatches = state.DepthHistory != null && Matches(state.DepthDescriptor, depthDescriptor);
            var confidenceMatches = state.ConfidenceHistory != null && Matches(state.ConfidenceDescriptor, confidenceDescriptor);
            var antiFlickerMatches = state.AntiFlickerHistory != null && Matches(state.AntiFlickerDescriptor, antiFlickerDescriptor);
            var descriptorsMatch = colorMatches && depthMatches && confidenceMatches && antiFlickerMatches;
            var targetTextureId = GetTargetTextureId(camera);
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            var renderScale = CalculateRenderScale(camera, colorDescriptor);
            var invalidationReason = ResolveHistoryInvalidationReason(
                camera,
                state,
                rendererMode,
                targetTextureId,
                targetWidth,
                targetHeight,
                renderScale,
                projectionMatrix,
                descriptorsMatch);
            state.CurrentRendererMode = rendererMode;

            if (!descriptorsMatch)
            {
                ReleaseHistory(state);
            }

            if (!string.IsNullOrEmpty(invalidationReason))
            {
                InvalidateState(state, invalidationReason);
            }

            var frameIndex = state.FrameIndex + 1;
            state.FrameIndex = frameIndex;
            var jitterPixels = CalculateHaltonJitter(frameIndex) * settings.JitterScale;
            var pixelWidth = Mathf.Max(1, colorDescriptor.width);
            var pixelHeight = Mathf.Max(1, colorDescriptor.height);
            var jitter = new Vector2(jitterPixels.x * 2f / pixelWidth, jitterPixels.y * 2f / pixelHeight);
            var jitteredProjection = projectionMatrix;
            jitteredProjection.m02 += jitter.x;
            jitteredProjection.m12 += jitter.y;

            var previousViewProjection = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
            var previousNonJitteredViewProjection = state.HasPreviousCameraState ? state.PreviousNonJitteredViewProjectionMatrix : previousViewProjection;

            return BurtTemporalAARequestState.Create(
                settings,
                frameIndex,
                jitterPixels,
                jitter,
                viewMatrix,
                projectionMatrix,
                jitteredProjection,
                previousViewProjection,
                previousNonJitteredViewProjection,
                state.HasValidHistory && descriptorsMatch);
        }

        public static void CommitRequest(BurtRenderRequest request)
        {
            var temporalAA = request != null ? request.TemporalAA : null;
            var camera = request != null ? request.Camera : null;
            if (camera == null || temporalAA == null || !temporalAA.Enabled)
            {
                return;
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            state.Camera = camera;
            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            state.PreviousRendererMode = state.CurrentRendererMode;
            state.PreviousCameraPosition = camera.transform.position;
            state.PreviousCameraRotation = camera.transform.rotation;
            state.PreviousOrthographic = camera.orthographic;
            state.PreviousFieldOfView = camera.fieldOfView;
            state.PreviousOrthographicSize = camera.orthographicSize;
            state.PreviousNearClipPlane = camera.nearClipPlane;
            state.PreviousFarClipPlane = camera.farClipPlane;
            state.PreviousTargetTextureId = GetTargetTextureId(camera);
            state.PreviousTargetWidth = targetWidth;
            state.PreviousTargetHeight = targetHeight;
            state.PreviousRenderScale = CalculateRenderScale(camera, colorDescriptor);
            state.PreviousViewProjectionMatrix = temporalAA.CurrentViewProjectionMatrix;
            state.PreviousNonJitteredViewProjectionMatrix = temporalAA.CurrentNonJitteredViewProjectionMatrix;
            state.PreviousNonJitteredProjectionMatrix = temporalAA.NonJitteredProjectionMatrix;
            state.HasPreviousCameraState = true;
        }

        public static bool ShouldUseTemporalAA(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            return BurtPostProcessUtility.ResolveTemporalAASettings(request, asset).Enabled;
        }

        public static bool IsTemporalAADebugMode(BurtShadingDebugMode mode)
        {
            return mode >= BurtShadingDebugMode.TemporalAAHistory && mode <= BurtShadingDebugMode.TemporalAAResponsiveMask;
        }

        public static BurtTemporalAAHistoryTextures EnsureHistoryTextures(Camera camera, out bool historyValid)
        {
            historyValid = false;
            if (camera == null)
            {
                return new BurtTemporalAAHistoryTextures(null, null, null, null);
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            state.Camera = camera;
            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateScalarHistoryDescriptor(camera);
            var confidenceDescriptor = CreateScalarHistoryDescriptor(camera);
            var antiFlickerDescriptor = CreateAntiFlickerHistoryDescriptor(camera);

            if (state.ColorHistory == null || !Matches(state.ColorDescriptor, colorDescriptor))
            {
                ReleaseTexture(state.ColorHistory);
                state.ColorDescriptor = colorDescriptor;
                state.ColorHistory = CreateHistoryTexture(colorDescriptor, "Burt TAA Color History " + camera.GetInstanceID(), FilterMode.Bilinear);
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                SetAllocationInvalidationReason(state, "HistoryAllocated");
            }

            if (state.DepthHistory == null || !Matches(state.DepthDescriptor, depthDescriptor))
            {
                ReleaseTexture(state.DepthHistory);
                state.DepthDescriptor = depthDescriptor;
                state.DepthHistory = CreateHistoryTexture(depthDescriptor, "Burt TAA Depth History " + camera.GetInstanceID(), FilterMode.Point);
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                SetAllocationInvalidationReason(state, "DepthHistoryAllocated");
            }

            if (state.ConfidenceHistory == null || !Matches(state.ConfidenceDescriptor, confidenceDescriptor))
            {
                ReleaseTexture(state.ConfidenceHistory);
                state.ConfidenceDescriptor = confidenceDescriptor;
                state.ConfidenceHistory = CreateHistoryTexture(confidenceDescriptor, "Burt TAA Confidence History " + camera.GetInstanceID(), FilterMode.Bilinear);
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                SetAllocationInvalidationReason(state, "ConfidenceHistoryAllocated");
            }

            if (state.AntiFlickerHistory == null || !Matches(state.AntiFlickerDescriptor, antiFlickerDescriptor))
            {
                ReleaseTexture(state.AntiFlickerHistory);
                state.AntiFlickerDescriptor = antiFlickerDescriptor;
                state.AntiFlickerHistory = CreateHistoryTexture(antiFlickerDescriptor, "Burt TAA Anti Flicker History " + camera.GetInstanceID(), FilterMode.Point);
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                SetAllocationInvalidationReason(state, "AntiFlickerHistoryAllocated");
            }

            historyValid = state.HasValidHistory;
            return new BurtTemporalAAHistoryTextures(state.ColorHistory, state.DepthHistory, state.ConfidenceHistory, state.AntiFlickerHistory);
        }

        public static RenderTexture EnsureHistoryTexture(Camera camera, out bool historyValid)
        {
            return EnsureHistoryTextures(camera, out historyValid).Color;
        }

        public static void MarkHistoryValid(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            if (!state.HasValidHistory)
            {
                state.FirstValidFrameIndex = state.FrameIndex;
            }

            state.HasValidHistory = true;
        }

        public static void InvalidateHistory(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            if (CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                state.LastInvalidationReason = "DisabledOrManual";
            }
        }

        public static BurtTemporalAAHistoryStatus GetHistoryStatus(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return CreateEmptyHistoryStatus("NoCameraOrHistory");
            }

            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateScalarHistoryDescriptor(camera);
            var confidenceDescriptor = CreateScalarHistoryDescriptor(camera);
            var hasColor = state.ColorHistory != null;
            var hasDepth = state.DepthHistory != null;
            var hasConfidence = state.ConfidenceHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;

            return new BurtTemporalAAHistoryStatus(
                state.HasValidHistory && hasColor,
                hasColor && Matches(state.ColorDescriptor, colorDescriptor),
                hasDepth,
                hasDepth && Matches(state.DepthDescriptor, depthDescriptor),
                hasConfidence,
                hasConfidence && Matches(state.ConfidenceDescriptor, confidenceDescriptor),
                state.AntiFlickerHistory != null,
                state.AntiFlickerHistory != null && Matches(state.AntiFlickerDescriptor, CreateAntiFlickerHistoryDescriptor(camera)),
                hasColor ? state.ColorHistory.width : 0,
                hasColor ? state.ColorHistory.height : 0,
                hasColor ? state.ColorHistory.format : RenderTextureFormat.Default,
                state.FrameIndex,
                historyAge,
                state.LastInvalidationReason);
        }

        private static BurtTemporalAAHistoryStatus CreateEmptyHistoryStatus(string reason)
        {
            return new BurtTemporalAAHistoryStatus(false, false, false, false, false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, reason);
        }

        private static CameraState GetOrCreateState(int cameraId)
        {
            if (!CameraStates.TryGetValue(cameraId, out var state))
            {
                state = new CameraState();
                CameraStates.Add(cameraId, state);
            }

            return state;
        }

        private static void PruneDisposedCameraStates()
        {
            cameraStatePruneCounter++;
            if (cameraStatePruneCounter < CameraStatePruneInterval)
            {
                return;
            }

            cameraStatePruneCounter = 0;
            CameraStateRemovalKeys.Clear();
            foreach (var pair in CameraStates)
            {
                if (pair.Value.Camera != null)
                {
                    continue;
                }

                ReleaseHistory(pair.Value);
                CameraStateRemovalKeys.Add(pair.Key);
            }

            for (var i = 0; i < CameraStateRemovalKeys.Count; i++)
            {
                CameraStates.Remove(CameraStateRemovalKeys[i]);
            }

            CameraStateRemovalKeys.Clear();
        }

        private static RenderTextureDescriptor CreateColorHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        internal static RenderTextureDescriptor CreateScalarHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.RFloat;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        private static RenderTextureDescriptor CreateAntiFlickerHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.RGHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        private static RenderTexture CreateHistoryTexture(RenderTextureDescriptor descriptor, string name, FilterMode filterMode)
        {
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        private static bool Matches(RenderTextureDescriptor left, RenderTextureDescriptor right)
        {
            return left.width == right.width && left.height == right.height && left.colorFormat == right.colorFormat && left.sRGB == right.sRGB;
        }

        private static string ResolveHistoryInvalidationReason(
            Camera camera,
            CameraState state,
            BurtRendererMode rendererMode,
            int targetTextureId,
            int targetWidth,
            int targetHeight,
            Vector2 renderScale,
            Matrix4x4 projectionMatrix,
            bool descriptorsMatch)
        {
            if (state == null)
            {
                return "NoCameraState";
            }

            if (!state.HasPreviousCameraState)
            {
                return descriptorsMatch ? null : "DescriptorChanged";
            }

            if (rendererMode != state.PreviousRendererMode)
            {
                return "RendererModeChanged";
            }

            if (targetTextureId != state.PreviousTargetTextureId)
            {
                return "TargetTextureChanged";
            }

            if (targetWidth != state.PreviousTargetWidth || targetHeight != state.PreviousTargetHeight)
            {
                return targetTextureId != 0 ? "TargetTextureSizeChanged" : "CameraResolutionChanged";
            }

            if (VectorChanged(renderScale, state.PreviousRenderScale, 0.0001f))
            {
                return "RenderScaleChanged";
            }

            if (camera != null)
            {
                if (camera.orthographic != state.PreviousOrthographic)
                {
                    return "ProjectionModeChanged";
                }

                if (camera.orthographic)
                {
                    if (FloatChanged(camera.orthographicSize, state.PreviousOrthographicSize, 0.0001f))
                    {
                        return "OrthographicSizeChanged";
                    }
                }
                else if (FloatChanged(camera.fieldOfView, state.PreviousFieldOfView, 0.0001f))
                {
                    return "FOVChanged";
                }

                if (FloatChanged(camera.nearClipPlane, state.PreviousNearClipPlane, 0.0001f))
                {
                    return "NearClipChanged";
                }

                if (FloatChanged(camera.farClipPlane, state.PreviousFarClipPlane, 0.001f))
                {
                    return "FarClipChanged";
                }
            }

            if (!descriptorsMatch)
            {
                return "DescriptorChanged";
            }

            if (ProjectionChanged(projectionMatrix, state.PreviousNonJitteredProjectionMatrix))
            {
                return "ProjectionChanged";
            }

            return CameraCutDetected(camera, state) ? "CameraCut" : null;
        }

        private static void InvalidateState(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            state.HasValidHistory = false;
            state.FirstValidFrameIndex = 0;
            state.LastInvalidationReason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        }

        private static void SetAllocationInvalidationReason(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            if (!state.HasPreviousCameraState || state.LastInvalidationReason == "NeverAllocated" || IsAllocationInvalidationReason(state.LastInvalidationReason))
            {
                state.LastInvalidationReason = reason;
            }
        }

        private static bool IsAllocationInvalidationReason(string reason)
        {
            return string.IsNullOrEmpty(reason) || reason == "HistoryAllocated" || reason == "DepthHistoryAllocated" || reason == "ConfidenceHistoryAllocated" || reason == "AntiFlickerHistoryAllocated";
        }

        private static int GetTargetTextureId(Camera camera)
        {
            return camera != null && camera.targetTexture != null ? camera.targetTexture.GetInstanceID() : 0;
        }

        private static void GetTargetSize(Camera camera, out int width, out int height)
        {
            if (camera != null && camera.targetTexture != null)
            {
                width = Mathf.Max(1, camera.targetTexture.width);
                height = Mathf.Max(1, camera.targetTexture.height);
                return;
            }

            width = Mathf.Max(1, camera != null ? camera.pixelWidth : 1);
            height = Mathf.Max(1, camera != null ? camera.pixelHeight : 1);
        }

        private static Vector2 CalculateRenderScale(Camera camera, RenderTextureDescriptor descriptor)
        {
            GetTargetSize(camera, out var width, out var height);
            return new Vector2(
                Mathf.Max(1, descriptor.width) / (float)Mathf.Max(1, width),
                Mathf.Max(1, descriptor.height) / (float)Mathf.Max(1, height));
        }

        private static bool FloatChanged(float current, float previous, float epsilon)
        {
            return Mathf.Abs(current - previous) > epsilon;
        }

        private static bool VectorChanged(Vector2 current, Vector2 previous, float epsilon)
        {
            return FloatChanged(current.x, previous.x, epsilon) || FloatChanged(current.y, previous.y, epsilon);
        }

        private static bool ProjectionChanged(Matrix4x4 current, Matrix4x4 previous)
        {
            for (var i = 0; i < 16; i++)
            {
                if (Mathf.Abs(current[i] - previous[i]) > ProjectionChangeEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CameraCutDetected(Camera camera, CameraState state)
        {
            if (camera == null || state == null)
            {
                return false;
            }

            var transform = camera.transform;
            var positionDelta = transform.position - state.PreviousCameraPosition;
            var rotationDelta = Quaternion.Angle(transform.rotation, state.PreviousCameraRotation);
            var farClip = Mathf.Max(camera.farClipPlane, 1f);
            var cutDistance = Mathf.Clamp(farClip * 0.25f, 25f, 250f);
            return positionDelta.sqrMagnitude > cutDistance * cutDistance || rotationDelta > 60f;
        }

        private static void ReleaseHistory(CameraState state)
        {
            if (state == null)
            {
                return;
            }

            ReleaseTexture(state.ColorHistory);
            ReleaseTexture(state.DepthHistory);
            ReleaseTexture(state.ConfidenceHistory);
            ReleaseTexture(state.AntiFlickerHistory);
            state.ColorHistory = null;
            state.DepthHistory = null;
            state.ConfidenceHistory = null;
            state.AntiFlickerHistory = null;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            Object.DestroyImmediate(texture);
        }

        private static Vector2 CalculateHaltonJitter(int frameIndex)
        {
            var sequenceIndex = frameIndex % HaltonSequenceLength;
            return new Vector2(Halton(sequenceIndex, 2) - 0.5f, Halton(sequenceIndex, 3) - 0.5f);
        }

        private static float Halton(int index, int radix)
        {
            var result = 0f;
            var fraction = 1f / radix;
            while (index > 0)
            {
                result += (index % radix) * fraction;
                index /= radix;
                fraction /= radix;
            }

            return result;
        }
    }
}
