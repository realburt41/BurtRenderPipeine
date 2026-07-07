using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [UnityEngine.Scripting.APIUpdating.MovedFromAttribute(true, "Burt.RenderPipeline", null, "BurtAutoExposureDebugSnapshot")]
    public readonly struct AutoExposureDebugSnapshot
    {
        public readonly bool HasState;
        public readonly ExposureMode Mode;
        public readonly float CurrentEV100;
        public readonly float TargetEV100;
        public readonly float AverageLuminance;
        public readonly float AverageLogLuminance;
        public readonly float MinEV100;
        public readonly float MaxEV100;
        public readonly float MiddleGrey;
        public readonly float LowPercent;
        public readonly float HighPercent;
        public readonly float HistogramMinEV100;
        public readonly float HistogramMaxEV100;
        public readonly bool HasSample;
        public readonly bool ReadbackPending;
        public readonly int ReadbackAgeFrames;
        public readonly int SampleAgeFrames;
        public readonly int ReadbackCompletedAgeFrames;
        public readonly int SampleCount;
        public readonly string SampleRejectedReason;
        public readonly string SampleStatus;
        public readonly bool UsingFallbackSample;
        public readonly int ReadbackWidth;
        public readonly int ReadbackHeight;

        public AutoExposureDebugSnapshot(
            bool hasState,
            ExposureMode mode,
            float currentEV100,
            float targetEV100,
            float averageLuminance,
            float averageLogLuminance,
            float minEV100,
            float maxEV100,
            float middleGrey,
            float lowPercent,
            float highPercent,
            float histogramMinEV100,
            float histogramMaxEV100,
            bool hasSample,
            bool readbackPending,
            int readbackAgeFrames,
            int sampleAgeFrames,
            int readbackCompletedAgeFrames,
            int sampleCount,
            string sampleRejectedReason,
            string sampleStatus,
            bool usingFallbackSample,
            int readbackWidth,
            int readbackHeight)
        {
            HasState = hasState;
            Mode = mode;
            CurrentEV100 = currentEV100;
            TargetEV100 = targetEV100;
            AverageLuminance = averageLuminance;
            AverageLogLuminance = averageLogLuminance;
            MinEV100 = minEV100;
            MaxEV100 = maxEV100;
            MiddleGrey = middleGrey;
            LowPercent = lowPercent;
            HighPercent = highPercent;
            HistogramMinEV100 = histogramMinEV100;
            HistogramMaxEV100 = histogramMaxEV100;
            HasSample = hasSample;
            ReadbackPending = readbackPending;
            ReadbackAgeFrames = readbackAgeFrames;
            SampleAgeFrames = sampleAgeFrames;
            ReadbackCompletedAgeFrames = readbackCompletedAgeFrames;
            SampleCount = sampleCount;
            SampleRejectedReason = string.IsNullOrEmpty(sampleRejectedReason) ? PhysicalExposureSettings.DefaultAutoSampleRejectedReason : sampleRejectedReason;
            SampleStatus = string.IsNullOrEmpty(sampleStatus) ? "Waiting" : sampleStatus;
            UsingFallbackSample = usingFallbackSample;
            ReadbackWidth = Mathf.Max(0, readbackWidth);
            ReadbackHeight = Mathf.Max(0, readbackHeight);
        }
    }

    public static class AutoExposureDebugUtility
    {
        public static bool TryGetSnapshot(Camera camera, out AutoExposureDebugSnapshot snapshot)
        {
            return AutoExposureUtility.TryGetDebugSnapshot(camera, out snapshot);
        }
    }

    internal static class AutoExposureUtility
    {
        private const int HistogramBinCount = 64;
        private const float LuminanceFloor = 0.000001f;
        private const float LogLuminanceMin = -20f;
        private const float LogLuminanceMax = 16f;
        private const float MinimumReliableLogLuminance = -16f;
        private const float MinimumReliableBasicAverageLogLuminance = -6.643856f; // log2(0.01): reject near-black basic averages that would explode exposure.
        private const float HistogramCenterWeightMin = 0.2f;
        private const float HistogramCenterWeightInnerRadius = 0.35f;
        private const float HistogramCenterWeightOuterRadius = 0.9f;
        private const int HistogramReadbackMaxDimension = 128;
        private const int HistogramReadbackMaxPixels = 4096;
        private const int ReadbackTimeoutFrames = 8;
        private const string SampleRejectedReasonNone = PhysicalExposureSettings.DefaultAutoSampleRejectedReason;
        private const string SampleRejectedReasonReadbackError = "ReadbackError";
        private const string SampleRejectedReasonModeChanged = "ModeChanged";
        private const string SampleRejectedReasonEmptyReadback = "EmptyReadback";
        private const string SampleRejectedReasonLowLuminance = "LowLuminance";
        private const string SampleRejectedReasonReadbackTimeout = "ReadbackTimeout";
        private const string SampleStatusAccepted = "Accepted latest readback";
        private const string SampleStatusPending = "Waiting for async readback";
        private const string SampleStatusFallback = "Using previous valid sample";
        private const string SampleStatusWaiting = "Waiting for first valid sample";

        private sealed class CameraExposureState
        {
            public Camera Camera;
            public float CurrentEV100 = PhysicalExposureSettings.DefaultManualEv100;
            public float TargetEV100 = PhysicalExposureSettings.DefaultAutoTargetEv100;
            public float AverageLuminance = PhysicalExposureSettings.DefaultAutoAverageLuminance;
            public float AverageLogLuminance = PhysicalExposureSettings.DefaultAutoAverageLogLuminance;
            public float MinEV100 = PhysicalExposureSettings.DefaultAutoMinEv100;
            public float MaxEV100 = PhysicalExposureSettings.DefaultAutoMaxEv100;
            public float MiddleGrey = PhysicalExposureSettings.DefaultAutoMiddleGrey;
            public float LowPercent = PhysicalExposureSettings.DefaultAutoLowPercent;
            public float HighPercent = PhysicalExposureSettings.DefaultAutoHighPercent;
            public float HistogramMinEV100 = PhysicalExposureSettings.DefaultAutoHistogramMinEv100;
            public float HistogramMaxEV100 = PhysicalExposureSettings.DefaultAutoHistogramMaxEv100;
            public ExposureMode Mode = PhysicalExposureSettings.DefaultMode;
            public bool HasSample;
            public bool ReadbackPending;
            public string SampleRejectedReason = SampleRejectedReasonNone;
            public int LastFrameTouched = -1;
            public int ReadbackRequestedFrame = -1;
            public int ReadbackCompletedFrame = -1;
            public int LastSampleFrame = -1;
            public int SampleCount;
            public bool UsingFallbackSample;
            public RenderTexture ReadbackTexture;
            public readonly float[] HistogramBins = new float[HistogramBinCount];
        }

        private static readonly Dictionary<int, CameraExposureState> CameraStates = new Dictionary<int, CameraExposureState>();
        private static readonly List<int> PruneKeys = new List<int>(8);

        public static PhysicalExposureSettings ResolveSettings(
            Camera camera,
            ExposureVolumeComponent exposure)
        {
            if (exposure == null || !exposure.IsEnabled())
            {
                return PhysicalExposureSettings.Default;
            }

            var baseSettings = CreateSettings(exposure, PhysicalExposureSettings.DefaultManualEv100, false, false);
            if (!IsAutomaticMode(baseSettings.Mode))
            {
                ResetState(camera);
                return baseSettings;
            }

            var state = GetOrCreateState(camera);
            if (state == null)
            {
                return CreateSettings(exposure, PhysicalExposureSettings.DefaultManualEv100, false, false);
            }

            state.Camera = camera;
            state.LastFrameTouched = Time.frameCount;
            CacheAutoParameters(state, exposure);
            RecoverTimedOutReadback(state);

            return CreateSettings(
                exposure,
                state.CurrentEV100,
                state.HasSample,
                state.ReadbackPending,
                state.AverageLuminance,
                state.AverageLogLuminance,
                state.TargetEV100,
                CalculateFrameAge(state.ReadbackRequestedFrame),
                CalculateFrameAge(state.LastSampleFrame),
                state.SampleCount,
                state.SampleRejectedReason);
        }

        public static bool ShouldCapture(PhysicalExposureSettings settings, Camera camera)
        {
            return camera != null &&
                IsAutomaticMode(settings.Mode) &&
                SystemInfo.supportsAsyncGPUReadback;
        }

        public static void CaptureAverageLogLuminance(
            CommandBuffer cmd,
            Camera camera,
            BurtRenderTargetHandle cameraColorTarget,
            PhysicalExposureSettings settings,
            Material material,
            int[] textureIds,
            int reduceShaderPassIndex,
            int finalShaderPassIndex,
            int sourceTextureId,
            int texelSizeId)
        {
            if (cmd == null ||
                camera == null ||
                !cameraColorTarget.IsValid ||
                material == null ||
                textureIds == null ||
                textureIds.Length == 0 ||
                !SystemInfo.supportsAsyncGPUReadback ||
                !IsAutomaticMode(settings.Mode))
            {
                return;
            }

            var state = GetOrCreateState(camera);
            if (state == null || state.ReadbackPending)
            {
                RecoverTimedOutReadback(state);
                return;
            }

            state.Mode = settings.Mode;
            CacheAutoParameters(state, settings);

            if (settings.Mode == ExposureMode.AutomaticHistogram)
            {
                CaptureHistogramLogLuminance(
                    cmd,
                    camera,
                    cameraColorTarget,
                    material,
                    reduceShaderPassIndex,
                    sourceTextureId,
                    texelSizeId,
                    state);
                return;
            }

            if (!EnsureReadbackTexture(state, 1, 1))
            {
                return;
            }

            var descriptor = CreateLogLuminanceDescriptor();
            var mipCount = CalculateExposureMipCount(camera);
            for (var i = 0; i < mipCount; i++)
            {
                descriptor.width = GetMipWidth(camera, i);
                descriptor.height = GetMipHeight(camera, i);
                cmd.GetTemporaryRT(textureIds[i], descriptor, FilterMode.Bilinear);
            }

            var firstWidth = GetMipWidth(camera, 0);
            var firstHeight = GetMipHeight(camera, 0);
            cmd.SetRenderTarget(new RenderTargetIdentifier(textureIds[0]));
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, firstWidth, firstHeight);
            cmd.SetGlobalTexture(sourceTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalVector(texelSizeId, CreateTexelSize(camera));
            cmd.DrawProcedural(Matrix4x4.identity, material, reduceShaderPassIndex, MeshTopology.Triangles, 3, 1);

            for (var i = 1; i < mipCount; i++)
            {
                var sourceWidth = GetMipWidth(camera, i - 1);
                var sourceHeight = GetMipHeight(camera, i - 1);
                var targetWidth = GetMipWidth(camera, i);
                var targetHeight = GetMipHeight(camera, i);
                cmd.SetRenderTarget(new RenderTargetIdentifier(textureIds[i]));
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, targetWidth, targetHeight);
                cmd.SetGlobalTexture(sourceTextureId, new RenderTargetIdentifier(textureIds[i - 1]));
                cmd.SetGlobalVector(texelSizeId, new Vector4(1f / sourceWidth, 1f / sourceHeight, sourceWidth, sourceHeight));
                cmd.DrawProcedural(Matrix4x4.identity, material, finalShaderPassIndex, MeshTopology.Triangles, 3, 1);
            }

            var finalSourceWidth = GetMipWidth(camera, mipCount - 1);
            var finalSourceHeight = GetMipHeight(camera, mipCount - 1);
            cmd.SetRenderTarget(state.ReadbackTexture);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, 1, 1);
            cmd.SetGlobalTexture(sourceTextureId, new RenderTargetIdentifier(textureIds[mipCount - 1]));
            cmd.SetGlobalVector(texelSizeId, new Vector4(1f / finalSourceWidth, 1f / finalSourceHeight, finalSourceWidth, finalSourceHeight));
            cmd.DrawProcedural(Matrix4x4.identity, material, finalShaderPassIndex, MeshTopology.Triangles, 3, 1);

            state.ReadbackPending = true;
            state.ReadbackRequestedFrame = Time.frameCount;
            cmd.RequestAsyncReadback(state.ReadbackTexture, request => CompleteReadback(camera.GetInstanceID(), settings.Mode, request));

            for (var i = 0; i < mipCount; i++)
            {
                cmd.ReleaseTemporaryRT(textureIds[i]);
            }
        }

        public static PhysicalExposureSettings UpdateAfterCapture(Camera camera, ExposureVolumeComponent exposure, float deltaTime)
        {
            if (exposure == null || !exposure.IsEnabled() || !IsAutomaticMode(exposure.mode.value))
            {
                ResetState(camera);
                return ResolveSettings(camera, exposure);
            }

            var state = GetOrCreateState(camera);
            if (state == null)
            {
                return ResolveSettings(camera, exposure);
            }

            CacheAutoParameters(state, exposure);

            if (state.HasSample)
            {
                var targetEV100 = Mathf.Clamp(CalculateTargetEV100(state.AverageLogLuminance, state.MiddleGrey), state.MinEV100, state.MaxEV100);
                state.TargetEV100 = targetEV100;

                var speed = targetEV100 > state.CurrentEV100
                    ? Mathf.Clamp(SanitizeFinite(exposure.autoSpeedUp.value, PhysicalExposureSettings.DefaultAutoSpeedUp), 0.02f, 20f)
                    : Mathf.Clamp(SanitizeFinite(exposure.autoSpeedDown.value, PhysicalExposureSettings.DefaultAutoSpeedDown), 0.02f, 20f);
                state.CurrentEV100 = AdaptEV(state.CurrentEV100, targetEV100, Mathf.Max(0f, deltaTime), speed);
            }
            else
            {
                state.CurrentEV100 = Mathf.Clamp(state.CurrentEV100, state.MinEV100, state.MaxEV100);
                state.TargetEV100 = state.CurrentEV100;
            }

            PruneDisposedCameraStates();
            return ResolveSettings(camera, exposure);
        }

        public static PhysicalExposureSettings GetDebugSettings(Camera camera, ExposureVolumeComponent exposure)
        {
            return ResolveSettings(camera, exposure);
        }

        internal static bool TryGetDebugSnapshot(Camera camera, out AutoExposureDebugSnapshot snapshot)
        {
            snapshot = default;
            if (camera == null)
            {
                return false;
            }

            if (!CameraStates.TryGetValue(camera.GetInstanceID(), out var state) || state == null)
            {
                return false;
            }

            RecoverTimedOutReadback(state);
            snapshot = new AutoExposureDebugSnapshot(
                true,
                state.Mode,
                state.CurrentEV100,
                state.TargetEV100,
                state.AverageLuminance,
                state.AverageLogLuminance,
                state.MinEV100,
                state.MaxEV100,
                state.MiddleGrey,
                state.LowPercent,
                state.HighPercent,
                state.HistogramMinEV100,
                state.HistogramMaxEV100,
                state.HasSample,
                state.ReadbackPending,
                CalculateFrameAge(state.ReadbackRequestedFrame),
                CalculateFrameAge(state.LastSampleFrame),
                CalculateFrameAge(state.ReadbackCompletedFrame),
                state.SampleCount,
                state.SampleRejectedReason,
                GetSampleStatus(state),
                state.UsingFallbackSample,
                state.ReadbackTexture != null ? state.ReadbackTexture.width : 0,
                state.ReadbackTexture != null ? state.ReadbackTexture.height : 0);
            return true;
        }

        public static void ResetState(Camera camera)
        {
            if (camera == null)
            {
                return;
            }

            var cameraId = camera.GetInstanceID();
            if (CameraStates.TryGetValue(cameraId, out var state))
            {
                ReleaseReadbackTexture(state);
            }

            CameraStates.Remove(cameraId);
        }

        private static PhysicalExposureSettings CreateSettings(
            ExposureVolumeComponent exposure,
            float autoEV100,
            bool autoHasSample,
            bool autoReadbackPending,
            float autoAverageLuminance = PhysicalExposureSettings.DefaultAutoAverageLuminance,
            float autoAverageLogLuminance = PhysicalExposureSettings.DefaultAutoAverageLogLuminance,
            float autoTargetEV100 = PhysicalExposureSettings.DefaultAutoTargetEv100,
            int autoReadbackAgeFrames = PhysicalExposureSettings.DefaultAutoFrameAge,
            int autoSampleAgeFrames = PhysicalExposureSettings.DefaultAutoFrameAge,
            int autoSampleCount = PhysicalExposureSettings.DefaultAutoSampleCount,
            string autoSampleRejectedReason = PhysicalExposureSettings.DefaultAutoSampleRejectedReason)
        {
            return new PhysicalExposureSettings(
                exposure.mode.value,
                exposure.manualEV100.value,
                exposure.iso.value,
                exposure.shutterTime.value,
                exposure.aperture.value,
                exposure.calibration.value,
                exposure.compensation.value,
                autoEV100,
                exposure.autoMinEV100.value,
                exposure.autoMaxEV100.value,
                exposure.autoMiddleGrey.value,
                exposure.autoSpeedUp.value,
                exposure.autoSpeedDown.value,
                exposure.autoLowPercent.value,
                exposure.autoHighPercent.value,
                exposure.autoHistogramMinEV100.value,
                exposure.autoHistogramMaxEV100.value,
                autoAverageLuminance,
                autoAverageLogLuminance,
                autoTargetEV100,
                autoHasSample,
                autoReadbackPending,
                autoReadbackAgeFrames,
                autoSampleAgeFrames,
                autoSampleCount,
                autoSampleRejectedReason);
        }

        private static void CompleteReadback(int cameraId, ExposureMode mode, AsyncGPUReadbackRequest request)
        {
            if (!CameraStates.TryGetValue(cameraId, out var state))
            {
                return;
            }

            state.ReadbackPending = false;
            state.ReadbackCompletedFrame = Time.frameCount;
            state.ReadbackRequestedFrame = -1;
            state.UsingFallbackSample = false;
            if (request.hasError)
            {
                state.UsingFallbackSample = state.HasSample;
                state.SampleRejectedReason = SampleRejectedReasonReadbackError;
                return;
            }

            if (state.Mode != mode)
            {
                state.UsingFallbackSample = state.HasSample;
                state.SampleRejectedReason = SampleRejectedReasonModeChanged;
                return;
            }

            var data = request.GetData<float>();
            if (data.Length <= 0)
            {
                state.UsingFallbackSample = state.HasSample;
                state.SampleRejectedReason = SampleRejectedReasonEmptyReadback;
                return;
            }

            var averageLogLuminance = mode == ExposureMode.AutomaticHistogram
                ? CalculateHistogramAverageLogLuminance(state, data)
                : Mathf.Clamp(SanitizeFinite(data[0], PhysicalExposureSettings.DefaultAutoAverageLogLuminance), LogLuminanceMin, LogLuminanceMax);
            ApplyAverageLogLuminance(state, averageLogLuminance);
        }

        private static void ApplyAverageLogLuminance(CameraExposureState state, float averageLogLuminance)
        {
            var previousAverageLogLuminance = state.AverageLogLuminance;
            var previousAverageLuminance = state.AverageLuminance;
            var previousTargetEV100 = state.TargetEV100;
            var hadReliableSample = state.HasSample && IsReliableAverageLogLuminance(state.Mode, previousAverageLogLuminance);
            var averageLuminance = Mathf.Clamp(Mathf.Pow(2f, averageLogLuminance), LuminanceFloor, 65504f);

            if (!IsReliableAverageLogLuminance(state.Mode, averageLogLuminance))
            {
                if (hadReliableSample)
                {
                    state.AverageLogLuminance = previousAverageLogLuminance;
                    state.AverageLuminance = previousAverageLuminance;
                    state.TargetEV100 = Mathf.Clamp(previousTargetEV100, state.MinEV100, state.MaxEV100);
                    state.CurrentEV100 = Mathf.Clamp(state.CurrentEV100, state.MinEV100, state.MaxEV100);
                    state.HasSample = true;
                    state.UsingFallbackSample = true;
                }
                else
                {
                    state.AverageLogLuminance = averageLogLuminance;
                    state.AverageLuminance = averageLuminance;
                    state.HasSample = false;
                    state.CurrentEV100 = Mathf.Clamp(PhysicalExposureSettings.DefaultAutoTargetEv100, state.MinEV100, state.MaxEV100);
                    state.TargetEV100 = state.CurrentEV100;
                    state.UsingFallbackSample = false;
                }

                state.SampleRejectedReason = SampleRejectedReasonLowLuminance;
                return;
            }

            state.AverageLogLuminance = averageLogLuminance;
            state.AverageLuminance = averageLuminance;
            state.SampleRejectedReason = SampleRejectedReasonNone;
            state.UsingFallbackSample = false;
            state.LastSampleFrame = Time.frameCount;
            state.SampleCount++;

            var targetEV100 = Mathf.Clamp(CalculateTargetEV100(averageLogLuminance, state.MiddleGrey), state.MinEV100, state.MaxEV100);
            if (!state.HasSample)
            {
                state.CurrentEV100 = targetEV100;
            }

            state.TargetEV100 = targetEV100;
            state.HasSample = true;
        }

        private static void CaptureHistogramLogLuminance(
            CommandBuffer cmd,
            Camera camera,
            BurtRenderTargetHandle cameraColorTarget,
            Material material,
            int reduceShaderPassIndex,
            int sourceTextureId,
            int texelSizeId,
            CameraExposureState state)
        {
            GetHistogramReadbackSize(camera, out var width, out var height);
            if (!EnsureReadbackTexture(state, width, height))
            {
                return;
            }

            cmd.SetRenderTarget(state.ReadbackTexture);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.SetGlobalTexture(sourceTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalVector(texelSizeId, CreateTexelSize(camera));
            cmd.DrawProcedural(Matrix4x4.identity, material, reduceShaderPassIndex, MeshTopology.Triangles, 3, 1);

            state.ReadbackPending = true;
            state.ReadbackRequestedFrame = Time.frameCount;
            cmd.RequestAsyncReadback(state.ReadbackTexture, request => CompleteReadback(camera.GetInstanceID(), ExposureMode.AutomaticHistogram, request));
        }

        private static void CacheAutoParameters(CameraExposureState state, ExposureVolumeComponent exposure)
        {
            if (state == null || exposure == null)
            {
                return;
            }

            var minEV100 = Mathf.Clamp(SanitizeFinite(exposure.autoMinEV100.value, PhysicalExposureSettings.DefaultAutoMinEv100), -16f, 24f);
            var maxEV100 = Mathf.Clamp(SanitizeFinite(exposure.autoMaxEV100.value, PhysicalExposureSettings.DefaultAutoMaxEv100), -16f, 24f);
            if (maxEV100 < minEV100)
            {
                var swapped = minEV100;
                minEV100 = maxEV100;
                maxEV100 = swapped;
            }

            state.MinEV100 = minEV100;
            state.MaxEV100 = maxEV100;
            state.MiddleGrey = Mathf.Clamp(SanitizeFinite(exposure.autoMiddleGrey.value, PhysicalExposureSettings.DefaultAutoMiddleGrey), 0.001f, 1f);
            var lowPercent = Mathf.Clamp(SanitizeFinite(exposure.autoLowPercent.value, PhysicalExposureSettings.DefaultAutoLowPercent), 0f, 100f);
            var highPercent = Mathf.Clamp(SanitizeFinite(exposure.autoHighPercent.value, PhysicalExposureSettings.DefaultAutoHighPercent), 0f, 100f);
            if (highPercent < lowPercent)
            {
                var swappedPercent = lowPercent;
                lowPercent = highPercent;
                highPercent = swappedPercent;
            }

            var histogramMinEV100 = Mathf.Clamp(SanitizeFinite(exposure.autoHistogramMinEV100.value, PhysicalExposureSettings.DefaultAutoHistogramMinEv100), -16f, 24f);
            var histogramMaxEV100 = Mathf.Clamp(SanitizeFinite(exposure.autoHistogramMaxEV100.value, PhysicalExposureSettings.DefaultAutoHistogramMaxEv100), -16f, 24f);
            if (histogramMaxEV100 < histogramMinEV100)
            {
                var swappedHistogram = histogramMinEV100;
                histogramMinEV100 = histogramMaxEV100;
                histogramMaxEV100 = swappedHistogram;
            }

            state.LowPercent = lowPercent;
            state.HighPercent = highPercent;
            state.HistogramMinEV100 = histogramMinEV100;
            state.HistogramMaxEV100 = histogramMaxEV100;
            state.Mode = exposure.mode.value;
            state.CurrentEV100 = Mathf.Clamp(state.CurrentEV100, state.MinEV100, state.MaxEV100);
            state.TargetEV100 = Mathf.Clamp(state.TargetEV100, state.MinEV100, state.MaxEV100);
        }

        private static void CacheAutoParameters(CameraExposureState state, PhysicalExposureSettings settings)
        {
            if (state == null)
            {
                return;
            }

            state.MinEV100 = settings.AutoMinEV100;
            state.MaxEV100 = settings.AutoMaxEV100;
            state.MiddleGrey = settings.AutoMiddleGrey;
            state.LowPercent = settings.AutoLowPercent;
            state.HighPercent = settings.AutoHighPercent;
            state.HistogramMinEV100 = settings.AutoHistogramMinEV100;
            state.HistogramMaxEV100 = settings.AutoHistogramMaxEV100;
            state.Mode = settings.Mode;
            state.CurrentEV100 = Mathf.Clamp(state.CurrentEV100, state.MinEV100, state.MaxEV100);
            state.TargetEV100 = Mathf.Clamp(state.TargetEV100, state.MinEV100, state.MaxEV100);
        }

        private static CameraExposureState GetOrCreateState(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            var cameraId = camera.GetInstanceID();
            if (!CameraStates.TryGetValue(cameraId, out var state))
            {
                state = new CameraExposureState
                {
                    Camera = camera,
                    LastFrameTouched = Time.frameCount
                };
                CameraStates.Add(cameraId, state);
            }

            return state;
        }

        private static void PruneDisposedCameraStates()
        {
            PruneKeys.Clear();
            foreach (var pair in CameraStates)
            {
                var state = pair.Value;
                if (state == null || state.Camera == null || Time.frameCount - state.LastFrameTouched > 240)
                {
                    PruneKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < PruneKeys.Count; i++)
            {
                if (CameraStates.TryGetValue(PruneKeys[i], out var state))
                {
                    ReleaseReadbackTexture(state);
                }

                CameraStates.Remove(PruneKeys[i]);
            }
        }

        private static bool EnsureReadbackTexture(CameraExposureState state, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);

            if (state.ReadbackTexture != null &&
                state.ReadbackTexture.width == width &&
                state.ReadbackTexture.height == height)
            {
                return true;
            }

            ReleaseReadbackTexture(state);

            var descriptor = CreateLogLuminanceDescriptor(width, height);
            state.ReadbackTexture = new RenderTexture(descriptor)
            {
                name = "Burt Auto Exposure Readback",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            return state.ReadbackTexture.Create();
        }

        private static void GetHistogramReadbackSize(Camera camera, out int width, out int height)
        {
            width = GetMipWidth(camera, 0);
            height = GetMipHeight(camera, 0);

            var maxDimension = Mathf.Max(width, height);
            if (maxDimension > HistogramReadbackMaxDimension)
            {
                var dimensionScale = HistogramReadbackMaxDimension / (float)maxDimension;
                width = Mathf.Max(1, Mathf.RoundToInt(width * dimensionScale));
                height = Mathf.Max(1, Mathf.RoundToInt(height * dimensionScale));
            }

            var pixelCount = width * height;
            if (pixelCount > HistogramReadbackMaxPixels)
            {
                var pixelScale = Mathf.Sqrt(HistogramReadbackMaxPixels / (float)pixelCount);
                width = Mathf.Max(1, Mathf.RoundToInt(width * pixelScale));
                height = Mathf.Max(1, Mathf.RoundToInt(height * pixelScale));
            }
        }

        private static void ReleaseReadbackTexture(CameraExposureState state)
        {
            if (state == null || state.ReadbackTexture == null)
            {
                return;
            }

            state.ReadbackTexture.Release();
            if (Application.isPlaying)
            {
                Object.Destroy(state.ReadbackTexture);
            }
            else
            {
                Object.DestroyImmediate(state.ReadbackTexture);
            }

            state.ReadbackTexture = null;
        }

        private static RenderTextureDescriptor CreateLogLuminanceDescriptor(int width = 1, int height = 1)
        {
            var descriptor = new RenderTextureDescriptor(Mathf.Max(1, width), Mathf.Max(1, height), RenderTextureFormat.RFloat, 0)
            {
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            return descriptor;
        }

        private static float CalculateHistogramAverageLogLuminance(CameraExposureState state, Unity.Collections.NativeArray<float> data)
        {
            for (var i = 0; i < state.HistogramBins.Length; i++)
            {
                state.HistogramBins[i] = 0f;
            }

            var histogramLogMin = state.HistogramMinEV100;
            var histogramLogMax = state.HistogramMaxEV100;
            if (Mathf.Abs(histogramLogMax - histogramLogMin) < 0.0001f)
            {
                histogramLogMax = histogramLogMin + 0.0001f;
            }

            var readbackWidth = state.ReadbackTexture != null ? Mathf.Max(1, state.ReadbackTexture.width) : data.Length;
            var readbackHeight = state.ReadbackTexture != null ? Mathf.Max(1, state.ReadbackTexture.height) : 1;
            var reliableSampleWeight = 0f;
            var histogramRange = histogramLogMax - histogramLogMin;
            for (var i = 0; i < data.Length; i++)
            {
                var logLuminance = Mathf.Clamp(SanitizeFinite(data[i], LogLuminanceMin), LogLuminanceMin, LogLuminanceMax);
                var sampleWeight = CalculateHistogramSampleWeight(i, readbackWidth, readbackHeight);
                if (logLuminance > MinimumReliableLogLuminance)
                {
                    reliableSampleWeight += sampleWeight;
                }

                var bucketPosition = Mathf.Clamp01((logLuminance - histogramLogMin) / histogramRange) * (HistogramBinCount - 1);
                var bucket0 = Mathf.Clamp(Mathf.FloorToInt(bucketPosition), 0, HistogramBinCount - 1);
                var bucket1 = Mathf.Min(bucket0 + 1, HistogramBinCount - 1);
                var weight1 = bucketPosition - bucket0;
                state.HistogramBins[bucket0] += (1f - weight1) * sampleWeight;
                state.HistogramBins[bucket1] += weight1 * sampleWeight;
            }

            if (reliableSampleWeight <= 0.0001f)
            {
                return LogLuminanceMin;
            }

            var histogramSum = 0f;
            for (var i = 0; i < state.HistogramBins.Length; i++)
            {
                histogramSum += state.HistogramBins[i];
            }

            if (histogramSum <= 0.0001f)
            {
                return LogLuminanceMin;
            }

            var lowCut = histogramSum * Mathf.Clamp01(state.LowPercent * 0.01f);
            var highCut = histogramSum * Mathf.Clamp01(state.HighPercent * 0.01f);
            var averageLogLuminance = CalculateHistogramAverageLogLuminance(state.HistogramBins, histogramLogMin, histogramLogMax, lowCut, highCut);
            if (IsReliableAverageLogLuminance(state.Mode, averageLogLuminance))
            {
                return averageLogLuminance;
            }

            return CalculateHistogramAverageLogLuminance(state.HistogramBins, histogramLogMin, histogramLogMax, 0f, histogramSum);
        }

        private static float CalculateHistogramSampleWeight(int sampleIndex, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            var x = sampleIndex % width;
            var y = sampleIndex / width;
            var centeredX = ((x + 0.5f) / width - 0.5f) * 2f;
            var centeredY = ((y + 0.5f) / height - 0.5f) * 2f;
            centeredX *= width / (float)height;

            var radius = Mathf.Sqrt(centeredX * centeredX + centeredY * centeredY);
            var t = Mathf.InverseLerp(HistogramCenterWeightInnerRadius, HistogramCenterWeightOuterRadius, radius);
            t = t * t * (3f - 2f * t);
            return Mathf.Lerp(1f, HistogramCenterWeightMin, t);
        }

        private static float CalculateHistogramAverageLogLuminance(float[] histogramBins, float histogramLogMin, float histogramLogMax, float lowCut, float highCut)
        {
            var includedWeight = 0f;
            var weightedLogLuminance = 0f;
            var histogramRange = histogramLogMax - histogramLogMin;
            for (var i = 0; i < histogramBins.Length; i++)
            {
                var value = histogramBins[i];
                var lowRemoved = Mathf.Min(value, lowCut);
                value -= lowRemoved;
                lowCut -= lowRemoved;
                highCut -= lowRemoved;

                value = Mathf.Min(value, highCut);
                highCut -= value;
                if (value <= 0f)
                {
                    continue;
                }

                var bucketLogLuminance = histogramLogMin + histogramRange * i / (histogramBins.Length - 1);
                weightedLogLuminance += bucketLogLuminance * value;
                includedWeight += value;
            }

            return includedWeight > 0.0001f
                ? Mathf.Clamp(weightedLogLuminance / includedWeight, LogLuminanceMin, LogLuminanceMax)
                : LogLuminanceMin;
        }

        private static int CalculateExposureMipCount(Camera camera)
        {
            var width = GetMipWidth(camera, 0);
            var height = GetMipHeight(camera, 0);
            var count = 1;
            while ((width > 1 || height > 1) && count < 16)
            {
                width = Mathf.Max(1, width / 2);
                height = Mathf.Max(1, height / 2);
                count++;
            }

            return count;
        }

        private static int GetMipWidth(Camera camera, int mipIndex)
        {
            GetTargetSize(camera, out var width, out _);
            width = Mathf.Max(1, width / 16);
            for (var i = 0; i < mipIndex; i++)
            {
                width = Mathf.Max(1, width / 2);
            }

            return width;
        }

        private static int GetMipHeight(Camera camera, int mipIndex)
        {
            GetTargetSize(camera, out _, out var height);
            height = Mathf.Max(1, height / 16);
            for (var i = 0; i < mipIndex; i++)
            {
                height = Mathf.Max(1, height / 2);
            }

            return height;
        }

        private static Vector4 CreateTexelSize(Camera camera)
        {
            GetTargetSize(camera, out var width, out var height);
            return new Vector4(1f / width, 1f / height, width, height);
        }

        private static void GetTargetSize(Camera camera, out int width, out int height)
        {
            width = 1;
            height = 1;
            if (camera == null)
            {
                return;
            }

            if (camera.targetTexture != null)
            {
                width = Mathf.Max(1, camera.targetTexture.width);
                height = Mathf.Max(1, camera.targetTexture.height);
                return;
            }

            width = Mathf.Max(1, camera.pixelWidth);
            height = Mathf.Max(1, camera.pixelHeight);
        }

        private static float MoveTowardsEV(float current, float target, float maxDelta)
        {
            if (Mathf.Abs(target - current) <= maxDelta)
            {
                return target;
            }

            return current + Mathf.Sign(target - current) * maxDelta;
        }

        private static float AdaptEV(float current, float target, float deltaTime, float speed)
        {
            if (deltaTime <= 0f || speed <= 0f)
            {
                return current;
            }

            var t = 1f - Mathf.Exp(-speed * deltaTime);
            var adapted = Mathf.Lerp(current, target, Mathf.Clamp01(t));
            return Mathf.Abs(adapted - target) < 0.001f ? target : adapted;
        }

        private static void RecoverTimedOutReadback(CameraExposureState state)
        {
            if (state == null || !state.ReadbackPending)
            {
                return;
            }

            if (CalculateFrameAge(state.ReadbackRequestedFrame) <= ReadbackTimeoutFrames)
            {
                return;
            }

            state.ReadbackPending = false;
            state.ReadbackRequestedFrame = -1;
            state.ReadbackCompletedFrame = Time.frameCount;
            state.UsingFallbackSample = state.HasSample;
            state.SampleRejectedReason = SampleRejectedReasonReadbackTimeout;
        }

        private static string GetSampleStatus(CameraExposureState state)
        {
            if (state == null)
            {
                return SampleStatusWaiting;
            }

            if (state.ReadbackPending)
            {
                return SampleStatusPending;
            }

            if (state.UsingFallbackSample)
            {
                return SampleStatusFallback;
            }

            return state.HasSample ? SampleStatusAccepted : SampleStatusWaiting;
        }

        private static int CalculateFrameAge(int frame)
        {
            return frame >= 0 ? Mathf.Max(0, Time.frameCount - frame) : PhysicalExposureSettings.DefaultAutoFrameAge;
        }

        private static float CalculateTargetEV100(float averageLogLuminance, float middleGrey)
        {
            return averageLogLuminance - Mathf.Log(Mathf.Max(middleGrey, 0.001f), 2f);
        }

        private static bool IsReliableAverageLogLuminance(ExposureMode mode, float averageLogLuminance)
        {
            if (averageLogLuminance <= MinimumReliableLogLuminance)
            {
                return false;
            }

            return mode != ExposureMode.Automatic || averageLogLuminance >= MinimumReliableBasicAverageLogLuminance;
        }

        private static bool IsAutomaticMode(ExposureMode mode)
        {
            return mode == ExposureMode.Automatic || mode == ExposureMode.AutomaticHistogram;
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }
    }
}
