using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public readonly struct BurtTemporalAASettings
    {
        public static readonly BurtTemporalAASettings Default = new BurtTemporalAASettings(false, 0.94f, 1.0f, 1.1f);

        public bool Enabled { get; }
        public float Feedback { get; }
        public float JitterScale { get; }
        public float ClampStrength { get; }

        public BurtTemporalAASettings(bool enabled, float feedback, float jitterScale, float clampStrength)
        {
            Enabled = enabled;
            Feedback = feedback;
            JitterScale = jitterScale;
            ClampStrength = clampStrength;
        }
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
        public Matrix4x4 InverseCurrentViewProjectionMatrix { get; private set; } = Matrix4x4.identity;
        public BurtTemporalAASettings Settings { get; private set; } = BurtTemporalAASettings.Default;

        public static BurtTemporalAARequestState CreateDisabled(Matrix4x4 viewMatrix, Matrix4x4 projectionMatrix)
        {
            var viewProjection = projectionMatrix * viewMatrix;
            return new BurtTemporalAARequestState
            {
                Enabled = false,
                ViewMatrix = viewMatrix,
                NonJitteredProjectionMatrix = projectionMatrix,
                JitteredProjectionMatrix = projectionMatrix,
                CurrentViewProjectionMatrix = viewProjection,
                CurrentNonJitteredViewProjectionMatrix = viewProjection,
                PreviousViewProjectionMatrix = viewProjection,
                InverseCurrentViewProjectionMatrix = viewProjection.inverse,
                Settings = BurtTemporalAASettings.Default
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
                InverseCurrentViewProjectionMatrix = currentViewProjection.inverse,
                Settings = settings
            };
        }
    }

    internal readonly struct BurtTemporalAAHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public int Width { get; }
        public int Height { get; }
        public RenderTextureFormat Format { get; }
        public int FrameIndex { get; }
        public int HistoryAge { get; }
        public string LastInvalidationReason { get; }

        public BurtTemporalAAHistoryStatus(bool hasHistory, bool descriptorMatches, int width, int height, RenderTextureFormat format, int frameIndex, int historyAge, string lastInvalidationReason)
        {
            HasHistory = hasHistory;
            DescriptorMatches = descriptorMatches;
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

        private sealed class CameraState
        {
            public int FrameIndex;
            public int FirstValidFrameIndex;
            public Matrix4x4 PreviousViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredProjectionMatrix = Matrix4x4.identity;
            public RenderTexture History;
            public RenderTextureDescriptor Descriptor;
            public bool HasValidHistory;
            public bool HasPreviousCameraState;
            public string LastInvalidationReason = "NeverAllocated";
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();

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
            var descriptor = CreateHistoryDescriptor(camera);
            var descriptorMatches = state.History != null && Matches(state.Descriptor, descriptor);
            if (!descriptorMatches)
            {
                ReleaseHistory(state);
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                state.LastInvalidationReason = "DescriptorChanged";
            }

            if (state.HasPreviousCameraState && ProjectionChanged(projectionMatrix, state.PreviousNonJitteredProjectionMatrix))
            {
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                state.LastInvalidationReason = "ProjectionChanged";
            }

            var frameIndex = state.FrameIndex + 1;
            state.FrameIndex = frameIndex;
            var jitterPixels = CalculateHaltonJitter(frameIndex) * settings.JitterScale;
            var pixelWidth = Mathf.Max(1, descriptor.width);
            var pixelHeight = Mathf.Max(1, descriptor.height);
            var jitter = new Vector2(jitterPixels.x * 2f / pixelWidth, jitterPixels.y * 2f / pixelHeight);
            var jitteredProjection = projectionMatrix;
            jitteredProjection.m02 += jitter.x;
            jitteredProjection.m12 += jitter.y;

            return BurtTemporalAARequestState.Create(
                settings,
                frameIndex,
                jitterPixels,
                jitter,
                viewMatrix,
                projectionMatrix,
                jitteredProjection,
                state.PreviousViewProjectionMatrix,
                state.HasValidHistory && descriptorMatches);
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
            state.PreviousViewProjectionMatrix = temporalAA.CurrentViewProjectionMatrix;
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
            return mode >= BurtShadingDebugMode.TemporalAAHistory && mode <= BurtShadingDebugMode.TemporalAADifference;
        }

        public static RenderTexture EnsureHistoryTexture(Camera camera, out bool historyValid)
        {
            historyValid = false;
            if (camera == null)
            {
                return null;
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            var descriptor = CreateHistoryDescriptor(camera);
            if (state.History == null || !Matches(state.Descriptor, descriptor))
            {
                ReleaseHistory(state);
                state.Descriptor = descriptor;
                state.History = new RenderTexture(descriptor)
                {
                    name = "Burt TAA History " + camera.GetInstanceID(),
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                state.History.Create();
                state.HasValidHistory = false;
                state.FirstValidFrameIndex = 0;
                state.LastInvalidationReason = "HistoryAllocated";
            }

            historyValid = state.HasValidHistory;
            return state.History;
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
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state) || state.History == null)
            {
                return new BurtTemporalAAHistoryStatus(false, false, 0, 0, RenderTextureFormat.Default, 0, 0, "NoCameraOrHistory");
            }

            var descriptor = CreateHistoryDescriptor(camera);
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtTemporalAAHistoryStatus(state.HasValidHistory, Matches(state.Descriptor, descriptor), state.History.width, state.History.height, state.History.format, state.FrameIndex, historyAge, state.LastInvalidationReason);
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

        private static RenderTextureDescriptor CreateHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static bool Matches(RenderTextureDescriptor left, RenderTextureDescriptor right)
        {
            return left.width == right.width && left.height == right.height && left.colorFormat == right.colorFormat && left.sRGB == right.sRGB;
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

        private static void ReleaseHistory(CameraState state)
        {
            if (state == null || state.History == null)
            {
                return;
            }

            state.History.Release();
            Object.DestroyImmediate(state.History);
            state.History = null;
        }

        private static Vector2 CalculateHaltonJitter(int frameIndex)
        {
            return new Vector2(Halton(frameIndex & 1023, 2) - 0.5f, Halton(frameIndex & 1023, 3) - 0.5f);
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
