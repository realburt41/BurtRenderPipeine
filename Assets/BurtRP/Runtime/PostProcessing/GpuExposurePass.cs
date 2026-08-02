using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal readonly struct GpuExposureSnapshot
    {
        public readonly bool HasSample;
        public readonly float CurrentScale;
        public readonly float TargetScale;
        public readonly float AverageLuminance;
        public readonly float CompensationScale;
        public readonly int SampleCount;
        public readonly int SampleAgeFrames;
        public readonly bool ReadbackPending;

        public GpuExposureSnapshot(
            bool hasSample,
            float currentScale,
            float targetScale,
            float averageLuminance,
            float compensationScale,
            int sampleCount,
            int sampleAgeFrames,
            bool readbackPending)
        {
            HasSample = hasSample;
            CurrentScale = currentScale;
            TargetScale = targetScale;
            AverageLuminance = averageLuminance;
            CompensationScale = compensationScale;
            SampleCount = sampleCount;
            SampleAgeFrames = sampleAgeFrames;
            ReadbackPending = readbackPending;
        }
    }

    internal static class GpuExposureUtility
    {
        private const string ComputeResourcePath = "BurtExposure";
        private const int HistogramScatterWidth = 128;
        private const int HistogramWidth = 16;
        private const int HistogramHeight = 2;
        private const int LocalExposureDownsampleStageCount = 5;
        private const float MiddleGrey = 0.18f;
        private const float AdaptationTransitionDistance = 1.5f;
        private const float FrameTimeEpsilon = 1f / 60f;

        private sealed class CameraState
        {
            public Camera Camera;
            public readonly RenderTexture[] ExposureTextures = new RenderTexture[2];
            public RenderTexture HistogramTexture;
            public RenderTexture LocalHistogramTexture;
            public readonly RenderTexture[] LocalDownsampleTextures = new RenderTexture[LocalExposureDownsampleStageCount];
            public RenderTexture LocalLogLuminanceTexture;
            public RenderTexture LocalBlurHorizontalTexture;
            public RenderTexture LocalBlurredLogLuminanceTexture;
            public int LocalWidth;
            public int LocalHeight;
            public int LocalBlurWidth;
            public int LocalBlurHeight;
            public bool LocalExposureValid;
            public int CurrentTextureIndex;
            public bool Initialized;
            public bool ForceTarget = true;
            public bool ReadbackPending;
            public bool HasSample;
            public int LastTouchedFrame = -1;
            public int LastSampleFrame = -1;
            public int SampleCount;
            public float CurrentScale = 1f;
            public float TargetScale = 1f;
            public float AverageLuminance = 1f;
            public float CompensationScale = 1f;
        }

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();
        private static readonly List<int> PruneKeys = new List<int>(8);
        private static ComputeShader computeShader;
        private static int histogramGenerateKernel = -1;
        private static int histogramConvertKernel = -1;
        private static int histogramAutoExposureKernel = -1;
        private static int manualExposureKernel = -1;
        private static int localExposureDownsampleKernel = -1;
        private static int localExposureHistogramKernel = -1;
        private static int localExposureSetupLogLuminanceKernel = -1;
        private static int localExposureGaussianHorizontalKernel = -1;
        private static int localExposureGaussianVerticalKernel = -1;
        private static bool shaderLoadAttempted;

        private static readonly int SceneColorId = Shader.PropertyToID("_BurtExposureSceneColor");
        private static readonly int MeteringMaskId = Shader.PropertyToID("_BurtExposureMeteringMask");
        private static readonly int HistogramScatterId = Shader.PropertyToID("_BurtExposureHistogramScatter");
        private static readonly int HistogramScatterUavId = Shader.PropertyToID("_BurtExposureHistogramScatterUAV");
        private static readonly int HistoryTextureId = Shader.PropertyToID("_BurtExposureHistoryTexture");
        private static readonly int HistogramTextureId = Shader.PropertyToID("_BurtExposureHistogramTexture");
        private static readonly int HistogramUavId = Shader.PropertyToID("_BurtExposureHistogramUAV");
        private static readonly int OutputUavId = Shader.PropertyToID("_BurtExposureOutputUAV");
        private static readonly int SceneExtentId = Shader.PropertyToID("_BurtExposureSceneExtent");
        private static readonly int HistogramParamsId = Shader.PropertyToID("_BurtExposureHistogramParams");
        private static readonly int RangeParamsId = Shader.PropertyToID("_BurtExposureRangeParams");
        private static readonly int AdaptationParamsId = Shader.PropertyToID("_BurtExposureAdaptationParams");
        private static readonly int AdaptationShapeId = Shader.PropertyToID("_BurtExposureAdaptationShape");
        private static readonly int CompensationParamsId = Shader.PropertyToID("_BurtExposureCompensationParams");
        private static readonly int HistogramScatterTemporaryId = Shader.PropertyToID("_BurtExposureHistogramScatterTemporary");
        private static readonly int LocalExposureSceneColorId = Shader.PropertyToID("_BurtLocalExposureSceneColor");
        private static readonly int LocalExposureDownsampleSourceId = Shader.PropertyToID("_BurtLocalExposureDownsampleSource");
        private static readonly int LocalExposureDownsampleUavId = Shader.PropertyToID("_BurtLocalExposureDownsampleUAV");
        private static readonly int LocalExposureDownsampleExtentId = Shader.PropertyToID("_BurtLocalExposureDownsampleExtent");
        private static readonly int LocalExposureBlurSourceId = Shader.PropertyToID("_BurtLocalExposureBlurSource");
        private static readonly int LocalExposureHistogramUavId = Shader.PropertyToID("_BurtLocalExposureHistogramUAV");
        private static readonly int LocalExposureLogLuminanceUavId = Shader.PropertyToID("_BurtLocalExposureLogLuminanceUAV");
        private static readonly int LocalExposureBlurUavId = Shader.PropertyToID("_BurtLocalExposureBlurUAV");
        private static readonly int LocalExposureExtentId = Shader.PropertyToID("_BurtLocalExposureExtent");
        private static readonly int LocalExposureBlurParamsId = Shader.PropertyToID("_BurtLocalExposureBlurParams");

        public static bool IsSupported => SystemInfo.supportsComputeShaders && EnsureShader();

        public static PhysicalExposureSettings ResolveSettings(Camera camera, ExposureVolumeComponent exposure)
        {
            if (exposure == null || !exposure.IsEnabled())
                return PhysicalExposureSettings.Default;

            if (exposure.mode.value != ExposureMode.Automatic && exposure.mode.value != ExposureMode.AutomaticHistogram)
            {
                return new PhysicalExposureSettings(
                    exposure.mode.value,
                    exposure.manualEV100.value,
                    exposure.iso.value,
                    exposure.shutterTime.value,
                    exposure.aperture.value,
                    exposure.calibration.value,
                    exposure.compensation.value);
            }

            var state = GetOrCreateState(camera);
            var scale = state != null && state.HasSample ? SanitizeScale(state.CurrentScale) : 1f;
            var calibration = Mathf.Max(exposure.calibration.value, 0.0001f);
            var currentEv100 = exposure.compensation.value + Mathf.Log(calibration, 2f) - Mathf.Log(scale, 2f);
            var averageLuminance = state != null ? Mathf.Max(state.AverageLuminance, 0.000001f) : 1f;
            var targetScale = state != null ? SanitizeScale(state.TargetScale) : scale;
            var targetEv100 = exposure.compensation.value + Mathf.Log(calibration, 2f) - Mathf.Log(targetScale, 2f);

            return new PhysicalExposureSettings(
                exposure.mode.value,
                exposure.manualEV100.value,
                exposure.iso.value,
                exposure.shutterTime.value,
                exposure.aperture.value,
                exposure.calibration.value,
                exposure.compensation.value,
                currentEv100,
                exposure.autoMinEV100.value,
                exposure.autoMaxEV100.value,
                exposure.autoMiddleGrey.value,
                exposure.autoSpeedUp.value,
                exposure.autoSpeedDown.value,
                exposure.autoLowPercent.value,
                exposure.autoHighPercent.value,
                exposure.autoHistogramMinEV100.value,
                exposure.autoHistogramMaxEV100.value,
                averageLuminance,
                Mathf.Log(averageLuminance, 2f),
                targetEv100,
                state != null && state.HasSample,
                state != null && state.ReadbackPending,
                -1,
                state != null && state.LastSampleFrame >= 0 ? Mathf.Max(0, Time.frameCount - state.LastSampleFrame) : -1,
                state != null ? state.SampleCount : 0,
                PhysicalExposureSettings.DefaultAutoSampleRejectedReason);
        }

        public static bool TryGetCurrentTexture(Camera camera, out RenderTexture texture)
        {
            texture = null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state) || !state.Initialized)
                return false;
            texture = state.ExposureTextures[state.CurrentTextureIndex];
            return texture != null && texture.IsCreated();
        }

        public static bool TryGetSnapshot(Camera camera, out GpuExposureSnapshot snapshot)
        {
            snapshot = default;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
                return false;
            snapshot = new GpuExposureSnapshot(
                state.HasSample,
                state.CurrentScale,
                state.TargetScale,
                state.AverageLuminance,
                state.CompensationScale,
                state.SampleCount,
                state.LastSampleFrame >= 0 ? Mathf.Max(0, Time.frameCount - state.LastSampleFrame) : -1,
                state.ReadbackPending);
            return true;
        }

        public static bool TryGetLocalExposureTextures(Camera camera, out RenderTexture histogram, out RenderTexture blurredLogLuminance)
        {
            histogram = null;
            blurredLogLuminance = null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state) || !state.LocalExposureValid)
                return false;
            histogram = state.LocalHistogramTexture;
            blurredLogLuminance = state.LocalBlurredLogLuminanceTexture;
            return histogram != null && histogram.IsCreated() && blurredLogLuminance != null && blurredLogLuminance.IsCreated();
        }

        public static bool TryGetHistogramTexture(Camera camera, out RenderTexture histogram)
        {
            histogram = null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
                return false;
            histogram = state.HistogramTexture;
            return histogram != null && histogram.IsCreated();
        }

        public static void Execute(CommandBuffer cmd, Camera camera, BurtRenderTargetHandle cameraColor, ExposureVolumeComponent exposure, float invPreExposure)
        {
            if (cmd == null || camera == null || !cameraColor.IsValid || !IsSupported)
                return;

            var state = GetOrCreateState(camera);
            if (state == null || !EnsureExposureTextures(state))
                return;

            state.Camera = camera;
            state.LastTouchedFrame = Time.frameCount;
            var previousTextureIndex = state.CurrentTextureIndex;
            state.CurrentTextureIndex = 1 - state.CurrentTextureIndex;
            var historyTexture = state.ExposureTextures[previousTextureIndex];
            var outputTexture = state.ExposureTextures[state.CurrentTextureIndex];

            if (!state.Initialized)
            {
                for (var i = 0; i < state.ExposureTextures.Length; i++)
                {
                    cmd.SetRenderTarget(state.ExposureTextures[i]);
                    cmd.ClearRenderTarget(false, true, Color.white);
                }
                state.Initialized = true;
                state.ForceTarget = true;
            }

            var component = exposure != null && exposure.active && exposure.enabled.value ? exposure : null;
            var mode = component != null ? component.mode.value : ExposureMode.ManualEV100;
            var sceneDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var sceneWidth = Mathf.Max(1, sceneDescriptor.width);
            var sceneHeight = Mathf.Max(1, sceneDescriptor.height);
            UploadCommonParameters(cmd, component, state.ForceTarget, invPreExposure, sceneWidth, sceneHeight);
            var meteringMask = component != null && component.meteringMask.value != null
                ? component.meteringMask.value
                : Texture2D.whiteTexture;

            var localExposure = VolumeManager.instance.stack.GetComponent<LocalExposureVolumeComponent>();
            state.LocalExposureValid = localExposure != null && localExposure.IsEnabled() &&
                ExecuteLocalExposure(cmd, cameraColor, state, localExposure, meteringMask, sceneWidth, sceneHeight);

            if (mode == ExposureMode.Automatic || mode == ExposureMode.AutomaticHistogram)
            {
                var scatterDescriptor = new RenderTextureDescriptor(HistogramScatterWidth, 1)
                {
                    graphicsFormat = GraphicsFormat.R32_UInt,
                    depthBufferBits = 0,
                    msaaSamples = 1,
                    enableRandomWrite = true,
                    sRGB = false
                };
                cmd.GetTemporaryRT(HistogramScatterTemporaryId, scatterDescriptor, FilterMode.Point);
                cmd.SetRenderTarget(new RenderTargetIdentifier(HistogramScatterTemporaryId));
                cmd.ClearRenderTarget(false, true, Color.clear);

                cmd.BeginSample("Atomic Histogram Generate Pass");
                cmd.SetComputeTextureParam(computeShader, histogramGenerateKernel, SceneColorId, cameraColor.Identifier);
                cmd.SetComputeTextureParam(computeShader, histogramGenerateKernel, MeteringMaskId, meteringMask);
                cmd.SetComputeTextureParam(computeShader, histogramGenerateKernel, HistogramScatterUavId, new RenderTargetIdentifier(HistogramScatterTemporaryId));
                cmd.DispatchCompute(computeShader, histogramGenerateKernel, sceneHeight, 1, 1);
                cmd.EndSample("Atomic Histogram Generate Pass");

                cmd.BeginSample("Histogram Convert Pass");
                cmd.SetComputeTextureParam(computeShader, histogramConvertKernel, HistogramScatterId, new RenderTargetIdentifier(HistogramScatterTemporaryId));
                cmd.SetComputeTextureParam(computeShader, histogramConvertKernel, HistoryTextureId, historyTexture);
                cmd.SetComputeTextureParam(computeShader, histogramConvertKernel, HistogramUavId, state.HistogramTexture);
                cmd.DispatchCompute(computeShader, histogramConvertKernel, 1, 1, 1);
                cmd.EndSample("Histogram Convert Pass");

                cmd.BeginSample("Histogram Auto Exposure Pass");
                cmd.SetComputeTextureParam(computeShader, histogramAutoExposureKernel, HistogramTextureId, state.HistogramTexture);
                cmd.SetComputeTextureParam(computeShader, histogramAutoExposureKernel, OutputUavId, outputTexture);
                cmd.DispatchCompute(computeShader, histogramAutoExposureKernel, 1, 1, 1);
                cmd.EndSample("Histogram Auto Exposure Pass");

                cmd.ReleaseTemporaryRT(HistogramScatterTemporaryId);
            }
            else
            {
                cmd.BeginSample("Manual Exposure Pass");
                cmd.SetComputeTextureParam(computeShader, manualExposureKernel, OutputUavId, outputTexture);
                cmd.DispatchCompute(computeShader, manualExposureKernel, 1, 1, 1);
                cmd.EndSample("Manual Exposure Pass");
            }

            state.ForceTarget = false;
            RequestReadback(cmd, camera.GetInstanceID(), outputTexture, state);
            PruneDisposedCameraStates();
        }

        private static void UploadCommonParameters(CommandBuffer cmd, ExposureVolumeComponent exposure, bool forceTarget, float invPreExposure, int sceneWidth, int sceneHeight)
        {
            var mode = exposure != null ? exposure.mode.value : ExposureMode.ManualEV100;
            var manualEv100 = exposure != null ? exposure.manualEV100.value : 0f;
            var minEv100 = exposure != null ? exposure.autoMinEV100.value : PhysicalExposureSettings.DefaultAutoMinEv100;
            var maxEv100 = exposure != null ? exposure.autoMaxEV100.value : PhysicalExposureSettings.DefaultAutoMaxEv100;
            if (maxEv100 < minEv100)
                Swap(ref minEv100, ref maxEv100);
            var histogramMin = exposure != null ? exposure.autoHistogramMinEV100.value : PhysicalExposureSettings.DefaultAutoHistogramMinEv100;
            var histogramMax = exposure != null ? exposure.autoHistogramMaxEV100.value : PhysicalExposureSettings.DefaultAutoHistogramMaxEv100;
            if (histogramMax <= histogramMin)
                histogramMax = histogramMin + 0.001f;
            var histogramScale = 1f / (histogramMax - histogramMin);
            var histogramBias = -histogramMin * histogramScale;
            var lowPercent = Mathf.Clamp(exposure != null ? exposure.autoLowPercent.value : 10f, 1f, 99f) * 0.01f;
            var highPercent = Mathf.Clamp(exposure != null ? exposure.autoHighPercent.value : 90f, 1f, 99f) * 0.01f;
            lowPercent = Mathf.Min(lowPercent, highPercent);
            var speedUp = Mathf.Max(exposure != null ? exposure.autoSpeedUp.value : 3f, 0.001f);
            var speedDown = Mathf.Max(exposure != null ? exposure.autoSpeedDown.value : 1f, 0.001f);
            var exponentialUpM = CalculateExponentialSlopeModifier(speedUp);
            var exponentialDownM = CalculateExponentialSlopeModifier(speedDown);
            var compensationScale = Mathf.Pow(2f, exposure != null ? exposure.compensation.value : 0f) *
                Mathf.Max(exposure != null ? exposure.calibration.value : 1f, 0f);
            var luminanceMin = mode == ExposureMode.Automatic ? 0.0001f : Mathf.Pow(2f, histogramMin);

            cmd.SetComputeVectorParam(computeShader, SceneExtentId, new Vector4(
                sceneWidth,
                sceneHeight,
                1f / sceneWidth,
                1f / sceneHeight));
            cmd.SetComputeVectorParam(computeShader, HistogramParamsId, new Vector4(histogramScale, histogramBias, luminanceMin, 1f));
            cmd.SetComputeVectorParam(computeShader, RangeParamsId, new Vector4(
                lowPercent,
                highPercent,
                Mathf.Pow(2f, minEv100) * MiddleGrey,
                Mathf.Pow(2f, maxEv100) * MiddleGrey));
            cmd.SetComputeVectorParam(computeShader, AdaptationParamsId, new Vector4(
                Mathf.Max(Time.deltaTime, 0f),
                speedUp,
                speedDown,
                forceTarget || mode == ExposureMode.ManualEV100 || mode == ExposureMode.PhysicalCamera ? 1f : 0f));
            cmd.SetComputeVectorParam(computeShader, AdaptationShapeId, new Vector4(exponentialUpM, exponentialDownM, AdaptationTransitionDistance, Mathf.Max(invPreExposure, 0.0001f)));
            cmd.SetComputeVectorParam(computeShader, CompensationParamsId, new Vector4(compensationScale, 1f, ResolveManualEv100(exposure), 0f));
        }

        private static float ResolveManualEv100(ExposureVolumeComponent exposure)
        {
            if (exposure == null)
                return 0f;
            if (exposure.mode.value == ExposureMode.PhysicalCamera)
            {
                var aperture = Mathf.Max(exposure.aperture.value, 0.1f);
                var shutter = Mathf.Max(exposure.shutterTime.value, 0.000001f);
                var iso = Mathf.Max(exposure.iso.value, 1f);
                return Mathf.Log((aperture * aperture) / shutter * (100f / iso), 2f);
            }
            return exposure.manualEV100.value;
        }

        private static float CalculateExponentialSlopeModifier(float speed)
        {
            var startTime = AdaptationTransitionDistance / Mathf.Max(speed, 0.001f);
            return FrameTimeEpsilon / Mathf.Max((1f - Mathf.Pow(2f, -FrameTimeEpsilon * speed)) * startTime, 0.000001f);
        }

        private static bool EnsureShader()
        {
            if (computeShader != null)
                return true;
            if (shaderLoadAttempted)
                return false;
            shaderLoadAttempted = true;
            computeShader = Resources.Load<ComputeShader>(ComputeResourcePath);
            if (computeShader == null)
            {
                Debug.LogError("[BurtRP][Exposure] Missing Resources/BurtExposure.compute.");
                return false;
            }
            histogramGenerateKernel = computeShader.FindKernel("AtomicHistogramGenerateCSMain");
            histogramConvertKernel = computeShader.FindKernel("HistogramConvertCSMain");
            histogramAutoExposureKernel = computeShader.FindKernel("HistogramAutoExposureCSMain");
            manualExposureKernel = computeShader.FindKernel("ManualExposureCSMain");
            localExposureDownsampleKernel = computeShader.FindKernel("LocalExposureDownsampleCSMain");
            localExposureHistogramKernel = computeShader.FindKernel("LocalExposureHistogramCSMain");
            localExposureSetupLogLuminanceKernel = computeShader.FindKernel("LocalExposureSetupLogLuminanceCSMain");
            localExposureGaussianHorizontalKernel = computeShader.FindKernel("LocalExposureGaussianHorizontalCSMain");
            localExposureGaussianVerticalKernel = computeShader.FindKernel("LocalExposureGaussianVerticalCSMain");
            return true;
        }

        private static bool ExecuteLocalExposure(
            CommandBuffer cmd,
            BurtRenderTargetHandle cameraColor,
            CameraState state,
            LocalExposureVolumeComponent settings,
            Texture meteringMask,
            int sceneWidth,
            int sceneHeight)
        {
            var histogramWidth = Mathf.Max(1, Mathf.CeilToInt(sceneWidth / 64f));
            var histogramHeight = Mathf.Max(1, Mathf.CeilToInt(sceneHeight / 64f));
            // XRender feeds local exposure's Gaussian setup from scene downsample
            // chain index 5: full, 1/2, 1/4, 1/8, 1/16, 1/32.
            var blurredWidth = Mathf.Max(1, Mathf.CeilToInt(sceneWidth / 32f));
            var blurredHeight = Mathf.Max(1, Mathf.CeilToInt(sceneHeight / 32f));
            if (!EnsureLocalExposureDownsampleTextures(state, sceneWidth, sceneHeight))
                return false;
            if (!EnsureLocalExposureTextures(state, histogramWidth, histogramHeight, blurredWidth, blurredHeight))
                return false;

            var downsampleSourceWidth = sceneWidth;
            var downsampleSourceHeight = sceneHeight;
            for (var stageIndex = 0; stageIndex < LocalExposureDownsampleStageCount; stageIndex++)
            {
                var downsampleTarget = state.LocalDownsampleTextures[stageIndex];
                var downsampleSource = stageIndex == 0
                    ? cameraColor.Identifier
                    : new RenderTargetIdentifier(state.LocalDownsampleTextures[stageIndex - 1]);
                cmd.BeginSample("LocalExposure.SceneDownsample" + (stageIndex + 1));
                cmd.SetComputeVectorParam(computeShader, LocalExposureDownsampleExtentId, new Vector4(
                    downsampleSourceWidth,
                    downsampleSourceHeight,
                    downsampleTarget.width,
                    downsampleTarget.height));
                cmd.SetComputeTextureParam(computeShader, localExposureDownsampleKernel, LocalExposureDownsampleSourceId, downsampleSource);
                cmd.SetComputeTextureParam(computeShader, localExposureDownsampleKernel, LocalExposureDownsampleUavId, downsampleTarget);
                cmd.DispatchCompute(
                    computeShader,
                    localExposureDownsampleKernel,
                    Mathf.CeilToInt(downsampleTarget.width / 8f),
                    Mathf.CeilToInt(downsampleTarget.height / 8f),
                    1);
                cmd.EndSample("LocalExposure.SceneDownsample" + (stageIndex + 1));
                downsampleSourceWidth = downsampleTarget.width;
                downsampleSourceHeight = downsampleTarget.height;
            }

            cmd.BeginSample("LocalExposureHistogramPass");
            cmd.SetComputeVectorParam(computeShader, LocalExposureExtentId, new Vector4(histogramWidth, histogramHeight, blurredWidth, blurredHeight));
            cmd.SetComputeTextureParam(computeShader, localExposureHistogramKernel, LocalExposureSceneColorId, cameraColor.Identifier);
            cmd.SetComputeTextureParam(computeShader, localExposureHistogramKernel, MeteringMaskId, meteringMask);
            cmd.SetComputeTextureParam(computeShader, localExposureHistogramKernel, LocalExposureHistogramUavId, state.LocalHistogramTexture);
            cmd.DispatchCompute(computeShader, localExposureHistogramKernel, histogramWidth, histogramHeight, 1);
            cmd.EndSample("LocalExposureHistogramPass");

            cmd.BeginSample("LocalExposureSetupLogLuminancePass");
            cmd.SetComputeTextureParam(
                computeShader,
                localExposureSetupLogLuminanceKernel,
                LocalExposureSceneColorId,
                state.LocalDownsampleTextures[LocalExposureDownsampleStageCount - 1]);
            cmd.SetComputeTextureParam(computeShader, localExposureSetupLogLuminanceKernel, LocalExposureLogLuminanceUavId, state.LocalLogLuminanceTexture);
            cmd.DispatchCompute(computeShader, localExposureSetupLogLuminanceKernel, Mathf.CeilToInt(blurredWidth / 8f), Mathf.CeilToInt(blurredHeight / 8f), 1);
            cmd.EndSample("LocalExposureSetupLogLuminancePass");

            var blurRadius = Mathf.Clamp(blurredWidth * settings.blurredLuminanceKernelSizePercent.value * 0.005f, 0.00001f, 31f);
            cmd.SetComputeVectorParam(computeShader, LocalExposureBlurParamsId, new Vector4(blurRadius, blurredWidth, blurredHeight, 0f));
            cmd.BeginSample("LocalExposure.GaussianBlurX");
            cmd.SetComputeTextureParam(computeShader, localExposureGaussianHorizontalKernel, LocalExposureBlurSourceId, state.LocalLogLuminanceTexture);
            cmd.SetComputeTextureParam(computeShader, localExposureGaussianHorizontalKernel, LocalExposureBlurUavId, state.LocalBlurHorizontalTexture);
            cmd.DispatchCompute(computeShader, localExposureGaussianHorizontalKernel, Mathf.CeilToInt(blurredWidth / 8f), Mathf.CeilToInt(blurredHeight / 8f), 1);
            cmd.EndSample("LocalExposure.GaussianBlurX");

            cmd.BeginSample("LocalExposure.GaussianBlurY");
            cmd.SetComputeTextureParam(computeShader, localExposureGaussianVerticalKernel, LocalExposureBlurSourceId, state.LocalBlurHorizontalTexture);
            cmd.SetComputeTextureParam(computeShader, localExposureGaussianVerticalKernel, LocalExposureBlurUavId, state.LocalBlurredLogLuminanceTexture);
            cmd.DispatchCompute(computeShader, localExposureGaussianVerticalKernel, Mathf.CeilToInt(blurredWidth / 8f), Mathf.CeilToInt(blurredHeight / 8f), 1);
            cmd.EndSample("LocalExposure.GaussianBlurY");
            return true;
        }

        private static bool EnsureLocalExposureDownsampleTextures(CameraState state, int sceneWidth, int sceneHeight)
        {
            var expectedWidth = Mathf.Max(1, sceneWidth);
            var expectedHeight = Mathf.Max(1, sceneHeight);
            var valid = true;
            for (var stageIndex = 0; stageIndex < LocalExposureDownsampleStageCount; stageIndex++)
            {
                expectedWidth = Mathf.Max(1, (expectedWidth + 1) / 2);
                expectedHeight = Mathf.Max(1, (expectedHeight + 1) / 2);
                var texture = state.LocalDownsampleTextures[stageIndex];
                valid &= texture != null && texture.IsCreated() &&
                    texture.width == expectedWidth && texture.height == expectedHeight;
            }

            if (valid)
                return true;

            ReleaseLocalExposureDownsampleTextures(state);
            expectedWidth = Mathf.Max(1, sceneWidth);
            expectedHeight = Mathf.Max(1, sceneHeight);
            for (var stageIndex = 0; stageIndex < LocalExposureDownsampleStageCount; stageIndex++)
            {
                expectedWidth = Mathf.Max(1, (expectedWidth + 1) / 2);
                expectedHeight = Mathf.Max(1, (expectedHeight + 1) / 2);
                var texture = new RenderTexture(expectedWidth, expectedHeight, 0)
                {
                    name = "Burt Local Exposure Scene Downsample " + (stageIndex + 1),
                    graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                    enableRandomWrite = true,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                if (!texture.Create())
                {
                    DestroyTextureObject(texture);
                    ReleaseLocalExposureDownsampleTextures(state);
                    return false;
                }
                state.LocalDownsampleTextures[stageIndex] = texture;
            }
            return true;
        }

        private static void ReleaseLocalExposureDownsampleTextures(CameraState state)
        {
            for (var stageIndex = 0; stageIndex < state.LocalDownsampleTextures.Length; stageIndex++)
            {
                var texture = state.LocalDownsampleTextures[stageIndex];
                if (texture == null)
                    continue;
                texture.Release();
                DestroyTextureObject(texture);
                state.LocalDownsampleTextures[stageIndex] = null;
            }
        }

        private static bool EnsureLocalExposureTextures(CameraState state, int histogramWidth, int histogramHeight, int blurredWidth, int blurredHeight)
        {
            if (state.LocalHistogramTexture != null && state.LocalHistogramTexture.IsCreated() &&
                state.LocalWidth == histogramWidth && state.LocalHeight == histogramHeight &&
                state.LocalBlurWidth == blurredWidth && state.LocalBlurHeight == blurredHeight &&
                state.LocalLogLuminanceTexture != null && state.LocalLogLuminanceTexture.IsCreated() &&
                state.LocalBlurHorizontalTexture != null && state.LocalBlurHorizontalTexture.IsCreated() &&
                state.LocalBlurredLogLuminanceTexture != null && state.LocalBlurredLogLuminanceTexture.IsCreated())
                return true;

            ReleaseTexture(ref state.LocalHistogramTexture);
            ReleaseTexture(ref state.LocalLogLuminanceTexture);
            ReleaseTexture(ref state.LocalBlurHorizontalTexture);
            ReleaseTexture(ref state.LocalBlurredLogLuminanceTexture);

            var histogram = new RenderTexture(histogramWidth, histogramHeight, 0)
            {
                name = "Burt Local Exposure Histogram Texture",
                graphicsFormat = GraphicsFormat.R32G32_SFloat,
                dimension = TextureDimension.Tex3D,
                volumeDepth = 32,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false
            };
            if (!histogram.Create())
            {
                DestroyTextureObject(histogram);
                return false;
            }
            state.LocalHistogramTexture = histogram;
            state.LocalLogLuminanceTexture = CreateLocalLuminanceTexture("Burt Local Exposure Log Luminance", blurredWidth, blurredHeight);
            state.LocalBlurHorizontalTexture = CreateLocalLuminanceTexture("Burt Local Exposure Gaussian Horizontal", blurredWidth, blurredHeight);
            state.LocalBlurredLogLuminanceTexture = CreateLocalLuminanceTexture("Burt Local Exposure Blurred Log Luminance", blurredWidth, blurredHeight);
            state.LocalWidth = histogramWidth;
            state.LocalHeight = histogramHeight;
            state.LocalBlurWidth = blurredWidth;
            state.LocalBlurHeight = blurredHeight;
            return state.LocalLogLuminanceTexture != null && state.LocalBlurHorizontalTexture != null && state.LocalBlurredLogLuminanceTexture != null;
        }

        private static RenderTexture CreateLocalLuminanceTexture(string name, int width, int height)
        {
            var texture = new RenderTexture(width, height, 0)
            {
                name = name,
                graphicsFormat = GraphicsFormat.R16_SFloat,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Mirror,
                useMipMap = false,
                autoGenerateMips = false
            };
            if (texture.Create())
                return texture;
            DestroyTextureObject(texture);
            return null;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;
            texture.Release();
            DestroyTextureObject(texture);
            texture = null;
        }

        private static void DestroyTextureObject(RenderTexture texture)
        {
            if (texture == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                return;
            }
#endif
            UnityEngine.Object.Destroy(texture);
        }

        private static CameraState GetOrCreateState(Camera camera)
        {
            if (camera == null)
                return null;
            var id = camera.GetInstanceID();
            if (!CameraStates.TryGetValue(id, out var state))
            {
                state = new CameraState { Camera = camera };
                CameraStates.Add(id, state);
            }
            return state;
        }

        private static bool EnsureExposureTextures(CameraState state)
        {
            for (var i = 0; i < state.ExposureTextures.Length; i++)
            {
                var texture = state.ExposureTextures[i];
                if (texture != null && texture.IsCreated())
                    continue;
                if (texture != null)
                    DestroyTextureObject(texture);
                texture = new RenderTexture(2, 1, 0)
                {
                    name = $"Burt Exposure Texture {i}",
                    graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
                    enableRandomWrite = true,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                if (!texture.Create())
                {
                    DestroyTextureObject(texture);
                    return false;
                }
                state.ExposureTextures[i] = texture;
                state.Initialized = false;
            }

            if (state.HistogramTexture == null || !state.HistogramTexture.IsCreated())
            {
                ReleaseTexture(ref state.HistogramTexture);
                state.HistogramTexture = new RenderTexture(HistogramWidth, HistogramHeight, 0)
                {
                    name = "Burt Exposure Histogram Texture",
                    graphicsFormat = GraphicsFormat.R32G32B32A32_SFloat,
                    enableRandomWrite = true,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    useMipMap = false,
                    autoGenerateMips = false
                };
                if (!state.HistogramTexture.Create())
                {
                    ReleaseTexture(ref state.HistogramTexture);
                    return false;
                }
            }
            return true;
        }

        private static void RequestReadback(CommandBuffer cmd, int cameraId, RenderTexture texture, CameraState state)
        {
            if (!SystemInfo.supportsAsyncGPUReadback || state.ReadbackPending)
                return;
            state.ReadbackPending = true;
            cmd.RequestAsyncReadback(texture, request => CompleteReadback(cameraId, request));
        }

        private static void CompleteReadback(int cameraId, AsyncGPUReadbackRequest request)
        {
            if (!CameraStates.TryGetValue(cameraId, out var state))
                return;
            state.ReadbackPending = false;
            if (request.hasError)
                return;
            var data = request.GetData<Vector4>();
            if (data.Length < 1)
                return;
            var exposure = data[0];
            state.CurrentScale = SanitizeScale(exposure.x);
            state.TargetScale = SanitizeScale(exposure.y);
            state.AverageLuminance = Mathf.Max(SanitizeFinite(exposure.z, 1f), 0.000001f);
            state.CompensationScale = SanitizeScale(exposure.w);
            state.HasSample = true;
            state.LastSampleFrame = Time.frameCount;
            state.SampleCount++;
        }

        private static void PruneDisposedCameraStates()
        {
            if ((Time.frameCount & 63) != 0)
                return;
            PruneKeys.Clear();
            foreach (var pair in CameraStates)
            {
                if (pair.Value.Camera == null || Time.frameCount - pair.Value.LastTouchedFrame > 600)
                    PruneKeys.Add(pair.Key);
            }
            foreach (var key in PruneKeys)
            {
                var state = CameraStates[key];
                foreach (var texture in state.ExposureTextures)
                {
                    if (texture != null)
                    {
                        texture.Release();
                        DestroyTextureObject(texture);
                    }
                }
                ReleaseTexture(ref state.HistogramTexture);
                ReleaseLocalExposureDownsampleTextures(state);
                ReleaseTexture(ref state.LocalHistogramTexture);
                ReleaseTexture(ref state.LocalLogLuminanceTexture);
                ReleaseTexture(ref state.LocalBlurHorizontalTexture);
                ReleaseTexture(ref state.LocalBlurredLogLuminanceTexture);
                CameraStates.Remove(key);
            }
        }

        private static float SanitizeScale(float value)
        {
            return Mathf.Clamp(SanitizeFinite(value, 1f), PreExposureUtility.MinExposure, PreExposureUtility.MaxExposure);
        }

        private static float SanitizeFinite(float value, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
        }

        private static void Swap(ref float a, ref float b)
        {
            var temp = a;
            a = b;
            b = temp;
        }
    }

    internal sealed class GpuExposurePass : BurtRenderPass
    {
        public override string Name => "Exposure";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset))
                return;
            builder.ReadCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) || !context.CameraColorTarget.IsValid)
                return;
            var exposure = VolumeManager.instance.stack.GetComponent<ExposureVolumeComponent>();
            var cmd = context.AcquireCommandBuffer(Name);
            var invPreExposure = PreExposureUtility.ResolveForFrame(context.Request, context.Asset).InvPreExposure;
            GpuExposureUtility.Execute(cmd, context.Request.Camera, context.CameraColorTarget, exposure, invPreExposure);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }
    }
}
