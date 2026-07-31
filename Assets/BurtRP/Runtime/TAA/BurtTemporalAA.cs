using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public readonly struct BurtTemporalAASettings
    {
        public static readonly BurtTemporalAASettings Default = new BurtTemporalAASettings(false, 1.0f, 0.04f, 0.35f, 1.2f, 1.1f, 1.0f);

        public bool Enabled { get; }
        public float JitterScale { get; }
        public float Sharpness { get; }
        public float UntrustedMotionFeedbackScale { get; }
        public float MotionEdgeResponsiveStrength { get; }
        public float DepthEdgeResponsiveStrength { get; }
        public float UpscaleFactor { get; }

        public BurtTemporalAASettings(bool enabled, float jitterScale, float sharpness)
            : this(enabled, jitterScale, sharpness, 0.35f, 1.2f, 1.1f, 1.0f)
        {
        }

        public BurtTemporalAASettings(
            bool enabled,
            float jitterScale,
            float sharpness,
            float untrustedMotionFeedbackScale,
            float motionEdgeResponsiveStrength,
            float depthEdgeResponsiveStrength,
            float upscaleFactor)
        {
            Enabled = enabled;
            JitterScale = jitterScale;
            Sharpness = sharpness;
            UntrustedMotionFeedbackScale = untrustedMotionFeedbackScale;
            MotionEdgeResponsiveStrength = motionEdgeResponsiveStrength;
            DepthEdgeResponsiveStrength = depthEdgeResponsiveStrength;
            UpscaleFactor = Mathf.Clamp(upscaleFactor, 1f, 2f);
        }

        public BurtTemporalAASettings WithJitterScale(float jitterScale)
        {
            return new BurtTemporalAASettings(
                Enabled,
                jitterScale,
                Sharpness,
                UntrustedMotionFeedbackScale,
                MotionEdgeResponsiveStrength,
                DepthEdgeResponsiveStrength,
                UpscaleFactor);
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
        public int JitterPhaseCount { get; private set; } = 8;
        public Matrix4x4 NonJitteredProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 JitteredProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 ViewMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 CurrentViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 CurrentNonJitteredViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 PreviousViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 PreviousNonJitteredViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 InverseCurrentViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 InverseCurrentNonJitteredViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public Matrix4x4 ClipToPreviousClipMatrix { get; private set; } = Matrix4x4.identity;
        public float CurrentPreExposure { get; private set; } = 1f;
        public float HistoryExposureCorrection { get; private set; } = 1f;
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
                InverseCurrentNonJitteredViewProjectionMatrix = viewProjection.inverse,
                ClipToPreviousClipMatrix = Matrix4x4.identity,
                CurrentPreExposure = 1f,
                HistoryExposureCorrection = 1f,
                Settings = BurtTemporalAASettings.Default,
                VelocityMode = BurtTemporalAAVelocityMode.Disabled
            };
        }

        internal static BurtTemporalAARequestState Create(
            BurtTemporalAASettings settings,
            int frameIndex,
            Vector2 jitterPixels,
            Vector2 jitter,
            int jitterPhaseCount,
            Matrix4x4 viewMatrix,
            Matrix4x4 nonJitteredProjectionMatrix,
            Matrix4x4 jitteredProjectionMatrix,
            Matrix4x4 previousViewProjectionMatrix,
            Matrix4x4 previousNonJitteredViewProjectionMatrix,
            Matrix4x4 clipToPreviousClipMatrix,
            float currentPreExposure,
            float historyExposureCorrection,
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
                JitterPhaseCount = Mathf.Max(1, jitterPhaseCount),
                ViewMatrix = viewMatrix,
                NonJitteredProjectionMatrix = nonJitteredProjectionMatrix,
                JitteredProjectionMatrix = jitteredProjectionMatrix,
                CurrentViewProjectionMatrix = currentViewProjection,
                CurrentNonJitteredViewProjectionMatrix = currentNonJitteredViewProjection,
                PreviousViewProjectionMatrix = previousViewProjectionMatrix,
                PreviousNonJitteredViewProjectionMatrix = previousNonJitteredViewProjectionMatrix,
                InverseCurrentViewProjectionMatrix = currentViewProjection.inverse,
                InverseCurrentNonJitteredViewProjectionMatrix = currentNonJitteredViewProjection.inverse,
                ClipToPreviousClipMatrix = clipToPreviousClipMatrix,
                CurrentPreExposure = Mathf.Max(currentPreExposure, 0.0001f),
                HistoryExposureCorrection = Mathf.Max(historyExposureCorrection, 0f),
                Settings = settings,
                VelocityMode = BurtTemporalAAVelocityMode.CameraOnly
            };
        }
    }

    internal readonly struct BurtTemporalAAHistoryTextures
    {
        public RenderTexture PreviousColor { get; }
        public RenderTexture CurrentColor { get; }
        public RenderTexture Depth { get; }
        public RenderTexture PreviousGuide { get; }
        public RenderTexture CurrentGuide { get; }

        public BurtTemporalAAHistoryTextures(
            RenderTexture previousColor,
            RenderTexture currentColor,
            RenderTexture depth,
            RenderTexture previousGuide,
            RenderTexture currentGuide)
        {
            PreviousColor = previousColor;
            CurrentColor = currentColor;
            Depth = depth;
            PreviousGuide = previousGuide;
            CurrentGuide = currentGuide;
        }
    }

    internal readonly struct BurtTemporalAAHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public bool HasDepthHistory { get; }
        public bool DepthDescriptorMatches { get; }
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
        private const float TemporalAAPostProcessSignatureEpsilon = 0.001f;
        private const float SceneViewJitterScaleMultiplier = 0.5f;
        private const int NativeTemporalAAJitterPhaseCount = 8;
        private const int MaxTemporalAAJitterPhaseCount = 1024;
        private const float TAAUHistoryResolutionScale = 2f;
        private const int TAAUMaxHistoryDimension = 6144;
        private const int CameraStatePruneInterval = 128;
        private const string PostProcessShaderName = "Hidden/BurtRP/PostProcessCopy";
        private const int HistoryLayoutVersion = 36;

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
            public Vector4 PreviousTemporalAAPostProcessSignature0;
            public Vector4 PreviousTemporalAAPostProcessSignature1;
            public Vector4 PreviousTemporalAAPostProcessSignature2;
            public Vector4 PreviousTemporalAAPostProcessSignature3;
            public float PreviousPreExposure = 1f;
            public Matrix4x4 PreviousViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousViewMatrix = Matrix4x4.identity;
            public RenderTexture ColorHistory;
            public RenderTexture CurrentColorHistory;
            public RenderTexture DepthHistory;
            public RenderTexture GuideHistory;
            public RenderTexture CurrentGuideHistory;
            public RenderTextureDescriptor ColorDescriptor;
            public RenderTextureDescriptor DepthDescriptor;
            public RenderTextureDescriptor GuideDescriptor;
            public int HistoryLayoutVersion;
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

        private static BurtTemporalAASettings ApplyTemporalAADebugOverrides(BurtTemporalAASettings settings, out int? jitterFrameOverride)
        {
            jitterFrameOverride = null;
            var temporalAA = GetTemporalAAVolumeComponent();
            if (temporalAA == null || !temporalAA.IsEnabled())
            {
                return settings;
            }

            if (temporalAA.debugFreezeJitter.value)
            {
                jitterFrameOverride = Mathf.Clamp(temporalAA.debugJitterFrame.value, 0, MaxTemporalAAJitterPhaseCount - 1) + 1;
            }

            return temporalAA.debugOverrideJitterScale.value
                ? settings.WithJitterScale(temporalAA.debugJitterScale.value)
                : settings;
        }

        private static BurtTemporalAASettings ApplyTemporalAAViewJitterScale(BurtTemporalAASettings settings, Camera camera)
        {
            if (camera != null && camera.cameraType == CameraType.SceneView)
            {
                return settings.WithJitterScale(settings.JitterScale * SceneViewJitterScaleMultiplier);
            }

            return settings;
        }

        private static TemporalAAVolumeComponent GetTemporalAAVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null || volumeManager.stack == null)
            {
                return null;
            }

            return volumeManager.stack.GetComponent<TemporalAAVolumeComponent>();
        }

        public static BurtTemporalAARequestState PrepareRequest(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return PrepareRequest(request, asset, null);
        }

        public static BurtTemporalAARequestState PrepareRequest(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRequestRenderOptions renderOptions)
        {
            var camera = request != null ? request.Camera : null;
            var viewMatrix = camera != null ? camera.worldToCameraMatrix : Matrix4x4.identity;
            var projectionMatrix = camera != null ? camera.projectionMatrix : Matrix4x4.identity;

            var disabledReason = ResolveTemporalAADisabledReason(request, asset, renderOptions);
            if (!string.IsNullOrEmpty(disabledReason))
            {
                InvalidateHistory(camera, disabledReason);
                return BurtTemporalAARequestState.CreateDisabled(viewMatrix, projectionMatrix);
            }

            var settings = PostProcessUtility.ResolveTemporalAASettings(request, asset);
            settings = ApplyTemporalAAViewJitterScale(settings, camera);
            settings = ApplyTemporalAADebugOverrides(settings, out var jitterFrameOverride);
            var cameraId = camera.GetInstanceID();
            var state = GetOrCreateState(cameraId);
            state.Camera = camera;
            PruneDisposedCameraStates();
            var rendererMode = asset != null ? asset.RendererMode : BurtRendererMode.Forward;
            ResolveTemporalAAPostProcessSignature(out var postProcessSignature0, out var postProcessSignature1, out var postProcessSignature2, out var postProcessSignature3);
            var currentPreExposure = PreExposureUtility.ResolveForFrame(request, asset).PreExposure;
            var previousPreExposure = state.HasPreviousCameraState ? state.PreviousPreExposure : currentPreExposure;
            var historyExposureCorrection = currentPreExposure / Mathf.Max(previousPreExposure, 0.0001f);
            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateScalarHistoryDescriptor(camera);
            var colorMatches = state.ColorHistory != null &&
                state.CurrentColorHistory != null &&
                Matches(state.ColorDescriptor, colorDescriptor);
            var depthMatches = state.DepthHistory != null && Matches(state.DepthDescriptor, depthDescriptor);
            var layoutMatches = state.HistoryLayoutVersion == HistoryLayoutVersion;
            var descriptorsMatch = colorMatches && depthMatches && layoutMatches;
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
                postProcessSignature0,
                postProcessSignature1,
                postProcessSignature2,
                postProcessSignature3,
                projectionMatrix,
                descriptorsMatch);
            if (!layoutMatches && string.IsNullOrEmpty(invalidationReason))
            {
                invalidationReason = "HistoryLayoutChanged";
            }
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
            var jitterFrameIndex = jitterFrameOverride.HasValue ? jitterFrameOverride.Value : frameIndex;
            var jitterPhaseCount = CalculateJitterPhaseCount(camera);
            var jitterPixels = CalculateHaltonJitter(jitterFrameIndex, jitterPhaseCount) * settings.JitterScale;
            var pixelWidth = Mathf.Max(1, colorDescriptor.width);
            var pixelHeight = Mathf.Max(1, colorDescriptor.height);
            var jitter = new Vector2(jitterPixels.x * 2f / pixelWidth, jitterPixels.y * 2f / pixelHeight);
            // Match XRender/Unity temporal jitter: translating clip space is not the
            // same as adding m02/m12 directly on perspective projection matrices.
            var jitteredProjection = Matrix4x4.Translate(new Vector3(jitter.x, jitter.y, 0f)) * projectionMatrix;

            var previousViewProjection = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
            var previousNonJitteredViewProjection = state.HasPreviousCameraState ? state.PreviousNonJitteredViewProjectionMatrix : previousViewProjection;
            var previousViewMatrix = state.HasPreviousCameraState ? state.PreviousViewMatrix : viewMatrix;
            var previousNonJitteredProjectionMatrix = state.HasPreviousCameraState ? state.PreviousNonJitteredProjectionMatrix : projectionMatrix;
            var clipToPreviousClip = CalculateClipToPreviousClip(
                viewMatrix,
                projectionMatrix,
                previousViewMatrix,
                previousNonJitteredProjectionMatrix);

            return BurtTemporalAARequestState.Create(
                settings,
                frameIndex,
                jitterPixels,
                jitter,
                jitterPhaseCount,
                viewMatrix,
                projectionMatrix,
                jitteredProjection,
                previousViewProjection,
                previousNonJitteredViewProjection,
                clipToPreviousClip,
                currentPreExposure,
                historyExposureCorrection,
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
            ResolveTemporalAAPostProcessSignature(out state.PreviousTemporalAAPostProcessSignature0, out state.PreviousTemporalAAPostProcessSignature1, out state.PreviousTemporalAAPostProcessSignature2, out state.PreviousTemporalAAPostProcessSignature3);
            state.PreviousPreExposure = temporalAA.CurrentPreExposure;
            state.PreviousViewProjectionMatrix = temporalAA.CurrentViewProjectionMatrix;
            state.PreviousNonJitteredViewProjectionMatrix = temporalAA.CurrentNonJitteredViewProjectionMatrix;
            state.PreviousNonJitteredProjectionMatrix = temporalAA.NonJitteredProjectionMatrix;
            state.PreviousViewMatrix = temporalAA.ViewMatrix;
            state.HistoryLayoutVersion = HistoryLayoutVersion;
            state.HasPreviousCameraState = true;
        }

        private static Matrix4x4 CalculateClipToPreviousClip(
            Matrix4x4 currentViewMatrix,
            Matrix4x4 currentNonJitteredProjectionMatrix,
            Matrix4x4 previousViewMatrix,
            Matrix4x4 previousNonJitteredProjectionMatrix)
        {
            var currentGpuNonJitteredProjection = GL.GetGPUProjectionMatrix(currentNonJitteredProjectionMatrix, true);
            var previousGpuNonJitteredProjection = GL.GetGPUProjectionMatrix(previousNonJitteredProjectionMatrix, true);
            var currentInverseProjection = currentGpuNonJitteredProjection.inverse;
            var currentInverseView = currentViewMatrix.inverse;
            var previousView = previousViewMatrix;
            var previousInverseView = previousViewMatrix.inverse;

            currentInverseView.m03 -= previousInverseView.m03;
            currentInverseView.m13 -= previousInverseView.m13;
            currentInverseView.m23 -= previousInverseView.m23;
            previousView.m03 = 0f;
            previousView.m13 = 0f;
            previousView.m23 = 0f;

            return previousGpuNonJitteredProjection * (previousView * (currentInverseView * currentInverseProjection));
        }

        public static bool ShouldUseTemporalAA(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseTemporalAA(request, asset, null);
        }

        public static bool ShouldUseTemporalAA(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRequestRenderOptions renderOptions)
        {
            return string.IsNullOrEmpty(ResolveTemporalAADisabledReason(request, asset, renderOptions));
        }

        public static string ResolveTemporalAADiagnosticDisabledReason(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRequestRenderOptions renderOptions)
        {
            return ResolveTemporalAADisabledReason(request, asset, renderOptions) ?? "Enabled";
        }

        private static string ResolveTemporalAADisabledReason(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRequestRenderOptions renderOptions)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return "InvalidRequest";
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return "AuxiliaryCamera";
            }

            if (PostProcessUtility.IsPostProcessSuppressedByShadingDebug())
            {
                return "PostProcessSuppressedByShadingDebug";
            }

            var settings = PostProcessUtility.ResolveTemporalAASettings(request, asset);
            if (!settings.Enabled)
            {
                return PostProcessUtility.ResolveTemporalAAConfigurationDisabledReason(request, asset) ?? "DisabledByVolumeOrAsset";
            }

            if (renderOptions != null && renderOptions.UseSharedRenderTargets && renderOptions.RequestCountInStack > 1)
            {
                return "SharedCameraStackUnsupported";
            }

            if (renderOptions != null && !renderOptions.ShouldFinalBlit)
            {
                return "PostProcessNotFinalBlitRequest";
            }

            if (!PostProcessUtility.ShouldUsePostProcessFramework(request, asset))
            {
                return "PostProcessFrameworkUnavailable";
            }

            return Shader.Find(PostProcessShaderName) != null ? null : "PostProcessShaderMissing";
        }

        public static bool IsTemporalAADebugMode(BurtShadingDebugMode mode)
        {
            return (mode >= BurtShadingDebugMode.TemporalAAHistory && mode <= BurtShadingDebugMode.TemporalAAResponsiveMask)
                || mode == BurtShadingDebugMode.TemporalAARejectionReasons
                || mode == BurtShadingDebugMode.TemporalAAFeedbackWeight
                || mode == BurtShadingDebugMode.TemporalAAPrevUseCount
                || mode == BurtShadingDebugMode.TemporalAAMetadata
                || mode == BurtShadingDebugMode.TemporalAAObjectMotionMask
                || mode == BurtShadingDebugMode.TemporalAAUpscaleState
                || mode == BurtShadingDebugMode.TemporalAAStencilMask;
        }

        public static BurtTemporalAAHistoryTextures EnsureHistoryTextures(Camera camera, out bool historyValid)
        {
            historyValid = false;
            if (camera == null)
            {
                return new BurtTemporalAAHistoryTextures(null, null, null, null, null);
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            state.Camera = camera;
            var colorDescriptor = CreateColorHistoryDescriptor(camera);
            var depthDescriptor = CreateScalarHistoryDescriptor(camera);
            var guideDescriptor = CreateGuideHistoryDescriptor(camera);
            var needsGuideHistory = ShouldUseOutputResolutionHistory(camera);

            if (state.ColorHistory == null ||
                state.CurrentColorHistory == null ||
                !Matches(state.ColorDescriptor, colorDescriptor))
            {
                ReleaseTexture(state.ColorHistory);
                ReleaseTexture(state.CurrentColorHistory);
                state.ColorDescriptor = colorDescriptor;
                state.ColorHistory = CreateHistoryTexture(colorDescriptor, "Burt TAA Previous Color History " + camera.GetInstanceID(), FilterMode.Bilinear);
                state.CurrentColorHistory = CreateHistoryTexture(colorDescriptor, "Burt TAA Current Color History " + camera.GetInstanceID(), FilterMode.Bilinear);
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

            if (needsGuideHistory &&
                (state.GuideHistory == null ||
                 state.CurrentGuideHistory == null ||
                 !Matches(state.GuideDescriptor, guideDescriptor)))
            {
                ReleaseTexture(state.GuideHistory);
                ReleaseTexture(state.CurrentGuideHistory);
                state.GuideDescriptor = guideDescriptor;
                state.GuideHistory = CreateHistoryTexture(guideDescriptor, "Burt TAAU Previous Guide History " + camera.GetInstanceID(), FilterMode.Bilinear);
                state.CurrentGuideHistory = CreateHistoryTexture(guideDescriptor, "Burt TAAU Current Guide History " + camera.GetInstanceID(), FilterMode.Bilinear);
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                SetAllocationInvalidationReason(state, "GuideHistoryAllocated");
            }
            else if (!needsGuideHistory && state.GuideHistory != null)
            {
                ReleaseTexture(state.GuideHistory);
                ReleaseTexture(state.CurrentGuideHistory);
                state.GuideHistory = null;
                state.CurrentGuideHistory = null;
                state.GuideDescriptor = default;
            }

            historyValid = state.HasValidHistory;
            return new BurtTemporalAAHistoryTextures(
                state.ColorHistory,
                state.CurrentColorHistory,
                state.DepthHistory,
                state.GuideHistory,
                state.CurrentGuideHistory);
        }

        public static RenderTexture EnsureHistoryTexture(Camera camera, out bool historyValid)
        {
            return EnsureHistoryTextures(camera, out historyValid).PreviousColor;
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

            Swap(ref state.ColorHistory, ref state.CurrentColorHistory);
            if (state.GuideHistory != null && state.CurrentGuideHistory != null)
            {
                Swap(ref state.GuideHistory, ref state.CurrentGuideHistory);
            }

            state.HasValidHistory = true;
        }

        public static void InvalidateHistory(Camera camera)
        {
            InvalidateHistory(camera, "DisabledOrManual");
        }

        public static void InvalidateHistory(Camera camera, string reason)
        {
            if (camera == null)
            {
                return;
            }

            if (CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                state.LastInvalidationReason = NormalizeInvalidationReason(reason, "DisabledOrManual");
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
            var hasColor = state.ColorHistory != null && state.CurrentColorHistory != null;
            var hasDepth = state.DepthHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            var hasUsableHistory = state.HasValidHistory && hasColor;
            var statusReason = hasUsableHistory && historyAge > 0 ? "HistoryValid" : state.LastInvalidationReason;

            return new BurtTemporalAAHistoryStatus(
                hasUsableHistory,
                hasColor && Matches(state.ColorDescriptor, colorDescriptor),
                hasDepth,
                hasDepth && Matches(state.DepthDescriptor, depthDescriptor),
                hasColor ? state.ColorHistory.width : 0,
                hasColor ? state.ColorHistory.height : 0,
                hasColor ? state.ColorHistory.format : RenderTextureFormat.Default,
                state.FrameIndex,
                historyAge,
                statusReason);
        }

        private static BurtTemporalAAHistoryStatus CreateEmptyHistoryStatus(string reason)
        {
            return new BurtTemporalAAHistoryStatus(false, false, false, false, 0, 0, RenderTextureFormat.Default, 0, 0, reason);
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
            var descriptor = ShouldUseOutputResolutionHistory(camera)
                ? BurtRenderTargetDescriptorUtility.CreateOutputPostProcessColorDescriptor(camera)
                : BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            if (ShouldUseOutputResolutionHistory(camera))
            {
                ApplyTAAUHistoryResolutionScale(ref descriptor);
            }
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = ShouldUseOutputResolutionHistory(camera);
            return descriptor;
        }

        internal static RenderTextureDescriptor CreateScalarHistoryDescriptor(Camera camera)
        {
            // Closest-depth history is consumed by input-resolution velocity dilation and
            // disocclusion rejection, so it deliberately stays at the input resolution.
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.RFloat;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            return descriptor;
        }

        private static void Swap<T>(ref T previous, ref T current)
        {
            var temporary = previous;
            previous = current;
            current = temporary;
        }

        private static RenderTextureDescriptor CreateGuideHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.sRGB = false;
            descriptor.enableRandomWrite = true;
            return descriptor;
        }

        private static void ApplyTAAUHistoryResolutionScale(ref RenderTextureDescriptor descriptor)
        {
            var maxScale = Mathf.Min(
                TAAUHistoryResolutionScale,
                TAAUMaxHistoryDimension / (float)Mathf.Max(1, Mathf.Max(descriptor.width, descriptor.height)));
            maxScale = Mathf.Max(1f, maxScale);
            descriptor.width = Mathf.Max(1, Mathf.CeilToInt(descriptor.width * maxScale));
            descriptor.height = Mathf.Max(1, Mathf.CeilToInt(descriptor.height * maxScale));
        }

        private static bool ShouldUseOutputResolutionHistory(Camera camera)
        {
            return BurtRenderTargetDescriptorUtility.ResolveInputRenderScale(camera) < 0.9999f;
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
            return left.width == right.width
                && left.height == right.height
                && left.colorFormat == right.colorFormat
                && left.graphicsFormat == right.graphicsFormat
                && left.depthBufferBits == right.depthBufferBits
                && left.msaaSamples == right.msaaSamples
                && left.useMipMap == right.useMipMap
                && left.autoGenerateMips == right.autoGenerateMips
                && left.sRGB == right.sRGB;
        }

        private static string ResolveHistoryInvalidationReason(
            Camera camera,
            CameraState state,
            BurtRendererMode rendererMode,
            int targetTextureId,
            int targetWidth,
            int targetHeight,
            Vector2 renderScale,
            Vector4 postProcessSignature0,
            Vector4 postProcessSignature1,
            Vector4 postProcessSignature2,
            Vector4 postProcessSignature3,
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

            if (VectorChanged(postProcessSignature0, state.PreviousTemporalAAPostProcessSignature0, TemporalAAPostProcessSignatureEpsilon)
                || VectorChanged(postProcessSignature1, state.PreviousTemporalAAPostProcessSignature1, TemporalAAPostProcessSignatureEpsilon)
                || VectorChanged(postProcessSignature2, state.PreviousTemporalAAPostProcessSignature2, TemporalAAPostProcessSignatureEpsilon)
                || VectorChanged(postProcessSignature3, state.PreviousTemporalAAPostProcessSignature3, TemporalAAPostProcessSignatureEpsilon))
            {
                return "PostProcessColorChanged";
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
            state.LastInvalidationReason = NormalizeInvalidationReason(reason, "Unknown");
        }

        private static string NormalizeInvalidationReason(string reason, string fallback)
        {
            return string.IsNullOrEmpty(reason) ? fallback : reason;
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
            return string.IsNullOrEmpty(reason) || reason == "HistoryAllocated" || reason == "DepthHistoryAllocated";
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

        private static bool VectorChanged(Vector4 current, Vector4 previous, float epsilon)
        {
            return FloatChanged(current.x, previous.x, epsilon)
                || FloatChanged(current.y, previous.y, epsilon)
                || FloatChanged(current.z, previous.z, epsilon)
                || FloatChanged(current.w, previous.w, epsilon);
        }

        private static void ResolveTemporalAAPostProcessSignature(out Vector4 signature0, out Vector4 signature1, out Vector4 signature2, out Vector4 signature3)
        {
            var volumeManager = VolumeManager.instance;
            var stack = volumeManager != null ? volumeManager.stack : null;
            if (stack == null)
            {
                signature0 = Vector4.zero;
                signature1 = Vector4.zero;
                signature2 = Vector4.zero;
                signature3 = Vector4.zero;
                return;
            }

            var tonemapping = stack.GetComponent<TonemappingVolumeComponent>();
            var exposure = stack.GetComponent<ExposureVolumeComponent>();
            var colorAdjustments = stack.GetComponent<ColorAdjustmentsVolumeComponent>();
            var vignette = stack.GetComponent<VignetteVolumeComponent>();
            var temporalAA = stack.GetComponent<TemporalAAVolumeComponent>();
            var tonemappingEnabled = tonemapping != null && tonemapping.IsEnabled();
            var exposureEnabled = exposure != null && exposure.IsEnabled();
            var colorAdjustmentsEnabled = colorAdjustments != null && colorAdjustments.IsEnabled();
            var vignetteEnabled = vignette != null && vignette.IsEnabled();
            var temporalAAUpscaleFactor = temporalAA != null && temporalAA.IsEnabled() ? temporalAA.upscaleFactor.value : BurtTemporalAASettings.Default.UpscaleFactor;
            var exposureMultiplier = exposureEnabled &&
                exposure.mode.value != ExposureMode.Automatic &&
                exposure.mode.value != ExposureMode.AutomaticHistogram
                ? new PhysicalExposureSettings(
                    exposure.mode.value,
                    exposure.manualEV100.value,
                    exposure.iso.value,
                    exposure.shutterTime.value,
                    exposure.aperture.value,
                    exposure.calibration.value,
                    exposure.compensation.value).Multiplier
                : 1f;
            var saturation = colorAdjustmentsEnabled ? colorAdjustments.saturation.value : ColorAdjustmentsSettings.DefaultSaturation;
            var contrast = colorAdjustmentsEnabled ? colorAdjustments.contrast.value : ColorAdjustmentsSettings.DefaultContrast;
            var gamma = colorAdjustmentsEnabled ? colorAdjustments.gamma.value : ColorAdjustmentsSettings.DefaultGamma;
            var colorFilter = colorAdjustmentsEnabled ? colorAdjustments.colorFilter.value : ColorAdjustmentsSettings.DefaultColorFilter;
            var vignetteColor = vignetteEnabled ? vignette.color.value : VignetteSettings.DefaultColor;
            var vignetteIntensity = vignetteEnabled ? vignette.intensity.value : VignetteSettings.DefaultIntensity;
            var vignetteEdgeWidth = vignetteEnabled ? vignette.edgeWidth.value : VignetteSettings.DefaultEdgeWidth;
            var vignetteEdgeSoftness = vignetteEnabled ? vignette.edgeSoftness.value : VignetteSettings.DefaultEdgeSoftness;
            var vignetteFisheyeFovDeg = vignetteEnabled ? vignette.fisheyeFovDeg.value : VignetteSettings.DefaultFisheyeFovDeg;
            var vignetteFollowAspect = vignetteEnabled && vignette.followAspect.value ? 1f : 0f;
            signature0 = new Vector4(
                exposureMultiplier,
                saturation,
                contrast,
                gamma);
            signature1 = new Vector4(
                colorFilter.r,
                colorFilter.g,
                colorFilter.b,
                (tonemappingEnabled ? (int)tonemapping.mode.value + 1 : 0f) + Mathf.Clamp(temporalAAUpscaleFactor, 1f, 2f) * 0.01f);
            signature2 = new Vector4(
                vignetteColor.r,
                vignetteColor.g,
                vignetteColor.b,
                vignetteColor.a * vignetteIntensity);
            signature3 = new Vector4(
                vignetteEdgeWidth,
                vignetteEdgeSoftness,
                vignetteFisheyeFovDeg,
                vignetteFollowAspect);
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
            ReleaseTexture(state.CurrentColorHistory);
            ReleaseTexture(state.DepthHistory);
            ReleaseTexture(state.GuideHistory);
            ReleaseTexture(state.CurrentGuideHistory);
            state.ColorHistory = null;
            state.CurrentColorHistory = null;
            state.DepthHistory = null;
            state.GuideHistory = null;
            state.CurrentGuideHistory = null;
            state.HistoryLayoutVersion = 0;
            state.HasValidHistory = false;
            state.FirstValidFrameIndex = 0;
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

        private static int CalculateJitterPhaseCount(Camera camera)
        {
            if (!ShouldUseOutputResolutionHistory(camera))
            {
                return NativeTemporalAAJitterPhaseCount;
            }

            var input = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            var output = BurtRenderTargetDescriptorUtility.CreateOutputPostProcessColorDescriptor(camera);
            var upscaleX = output.width / (float)Mathf.Max(1, input.width);
            var upscaleY = output.height / (float)Mathf.Max(1, input.height);
            var upscaleRatio = Mathf.Max(1f, Mathf.Max(upscaleX, upscaleY));
            return Mathf.Clamp(
                Mathf.CeilToInt(NativeTemporalAAJitterPhaseCount * upscaleRatio * upscaleRatio),
                NativeTemporalAAJitterPhaseCount,
                MaxTemporalAAJitterPhaseCount);
        }

        private static Vector2 CalculateHaltonJitter(int frameIndex, int phaseCount)
        {
            var sequenceLength = Mathf.Clamp(phaseCount, 1, MaxTemporalAAJitterPhaseCount);
            var sequenceIndex = ((Mathf.Max(1, frameIndex) - 1) % sequenceLength) + 1;
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
