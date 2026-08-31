using UnityEngine; // 引入 UnityEngine 命名空间，用来访问 Camera、LayerMask、Mathf 和 Debug。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来访问 VolumeManager 和 VolumeStack。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理工具可以被 RenderGraph 和 Pass 共享。
{
    internal static class PostProcessUtility // 定义后处理工具类，用来集中判断后处理框架是否应该运行。
    {
        public const float BloomPrefilterFireflyClamp = 0f;
        public const int BloomGaussianMaxSamples = 32;
        public const int BloomGaussianSampleCapacity = 64;
        public const int BloomXRenderStageCountMax = 6;

        public static bool IsPostProcessSuppressedByShadingDebug()
        {
            return BurtShadingDebugSettings.IsDebugging &&
                !IsBloomDebugRequested() &&
                !IsTemporalAADebugRequested() &&
                !IsAutoExposureDebugRequested();
        }

        public static bool IsBloomDebugRequested()
        {
            return ResolveBloomShadingDebugView() != BloomDebugView.Disabled;
        }

        public static bool IsBloomPrefilterDebugRequested()
        {
            return BurtShadingDebugSettings.Mode == BurtShadingDebugMode.BloomPrefilter;
        }

        public static bool IsTemporalAADebugRequested()
        {
            return BurtTemporalAAUtility.IsTemporalAADebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool IsAutoExposureDebugRequested()
        {
            return ResolveAutoExposureDebugMode(BurtShadingDebugSettings.Mode) > 0;
        }

        public static int ResolveAutoExposureDebugMode(BurtShadingDebugMode mode)
        {
            switch (mode)
            {
                case BurtShadingDebugMode.AutoExposureLuminance:
                    return 1;
                case BurtShadingDebugMode.AutoExposureMeteringWeight:
                    return 2;
                case BurtShadingDebugMode.AutoExposureHistogramRange:
                    return 3;
                case BurtShadingDebugMode.ExposureHDRSceneIlluminance:
                    return 1;
                case BurtShadingDebugMode.ExposureHDRSceneLuminance:
                    return 2;
                case BurtShadingDebugMode.ExposureHDRExposedLuminance:
                    return 3;
                case BurtShadingDebugMode.ExposureHDRToneMappedLuminance:
                    return 4;
                case BurtShadingDebugMode.ExposureHDRLightMeter:
                    return 5;
                case BurtShadingDebugMode.ExposureHDRLocalExposure:
                    return 6;
                case BurtShadingDebugMode.ExposureHDRLuminanceContrast:
                    return 7;
                default:
                    return 0;
            }
        }

        public static BloomDebugView ResolveBloomDebugView(BloomSettings settings)
        {
            var shadingDebugView = ResolveBloomShadingDebugView();
            return shadingDebugView != BloomDebugView.Disabled ? shadingDebugView : settings.DebugView;
        }

        public static BloomDebugView ResolveBloomShadingDebugView()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.BloomPrefilter:
                    return BloomDebugView.Prefilter;
                case BurtShadingDebugMode.BloomFinalBloom:
                    return BloomDebugView.FinalBloom;
                case BurtShadingDebugMode.BloomMip1:
                    return BloomDebugView.Mip1;
                case BurtShadingDebugMode.BloomMip2:
                    return BloomDebugView.Mip2;
                case BurtShadingDebugMode.BloomMip3:
                    return BloomDebugView.Mip3;
                case BurtShadingDebugMode.BloomMip4:
                    return BloomDebugView.Mip4;
                case BurtShadingDebugMode.BloomMip5:
                    return BloomDebugView.Mip5;
                case BurtShadingDebugMode.BloomAlpha:
                    return BloomDebugView.Alpha;
                case BurtShadingDebugMode.BloomThresholdMask:
                    return BloomDebugView.ThresholdMask;
                default:
                    return BloomDebugView.Disabled;
            }
        }

        public static bool ShouldPreserveBloomAlpha(BloomSettings settings, BloomDebugView debugView)
        {
            return settings.BloomAlphaChannel || debugView == BloomDebugView.Alpha;
        }

        public static string ResolveBloomAlphaReason(BloomSettings settings, BloomDebugView debugView)
        {
            if (settings.BloomAlphaChannel && debugView == BloomDebugView.Alpha)
            {
                return "VolumeAndAlphaDebug";
            }

            if (debugView == BloomDebugView.Alpha)
            {
                return "AlphaDebug";
            }

            return settings.BloomAlphaChannel ? "Volume" : "Disabled";
        }

        public static RenderTextureDescriptor CreateBloomRenderTextureDescriptor(Camera camera, int width, int height, BloomSettings settings, BloomDebugView debugView)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.width = Mathf.Max(1, width);
            descriptor.height = Mathf.Max(1, height);
            descriptor.colorFormat = ResolveBloomRenderTextureFormat(camera, settings, debugView);

            return descriptor;
        }

        public static RenderTextureDescriptor CreateBloomInputRenderTextureDescriptor(Camera camera, int width, int height)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.width = Mathf.Max(1, width);
            descriptor.height = Mathf.Max(1, height);
            if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf))
            {
                descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            }

            descriptor.sRGB = false;
            return descriptor;
        }

        public static int ResolveBloomSourceWidth(Camera camera)
        {
            if (camera == null)
            {
                return 1;
            }

            return Mathf.Max(1, BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera).width);
        }

        public static int ResolveBloomSourceHeight(Camera camera)
        {
            if (camera == null)
            {
                return 1;
            }

            return Mathf.Max(1, BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera).height);
        }

        public static int GetBloomMipWidth(Camera camera, int mipIndex)
        {
            var width = ResolveBloomSourceWidth(camera);
            for (var i = 0; i <= Mathf.Max(0, mipIndex); i++)
            {
                width = Mathf.Max(1, (width + 1) / 2);
            }

            return Mathf.Max(1, width);
        }

        public static int GetBloomMipHeight(Camera camera, int mipIndex)
        {
            var height = ResolveBloomSourceHeight(camera);
            for (var i = 0; i <= Mathf.Max(0, mipIndex); i++)
            {
                height = Mathf.Max(1, (height + 1) / 2);
            }

            return Mathf.Max(1, height);
        }

        public static long CalculateBloomMipPixelCount(Camera camera, int mipCount)
        {
            if (mipCount <= 0)
            {
                return 0L;
            }

            var pixelCount = 0L;
            for (var i = 0; i < BloomXRenderStageCountMax; i++)
            {
                pixelCount += (long)GetBloomMipWidth(camera, i) * GetBloomMipHeight(camera, i);
            }

            return pixelCount;
        }

        public static string FormatBloomMipSizes(Camera camera, int mipCount)
        {
            if (mipCount <= 0)
            {
                return "<none>";
            }

            var builder = new System.Text.StringBuilder();
            for (var i = 0; i < BloomXRenderStageCountMax; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(i).Append(':').Append(GetBloomMipWidth(camera, i)).Append('x').Append(GetBloomMipHeight(camera, i));
            }

            return builder.ToString();
        }

        public static int ResolveBloomDebugMipIndex(BloomDebugView debugView, int mipCount)
        {
            if (mipCount <= 0 || debugView == BloomDebugView.Disabled)
            {
                return -1;
            }

            if (debugView >= BloomDebugView.Mip1 && debugView <= BloomDebugView.Mip5)
            {
                return Mathf.Clamp((int)debugView - (int)BloomDebugView.Mip1 + 1, 1, BloomXRenderStageCountMax - 1);
            }

            return debugView == BloomDebugView.Prefilter ? 0 : ResolveBloomFirstStageMipIndex(mipCount);
        }

        public static string FormatBloomDebugTarget(Camera camera, BloomDebugView debugView, int mipCount)
        {
            if (debugView == BloomDebugView.ThresholdMask)
            {
                return "ThresholdMask:CameraColor:" + ResolveBloomSourceWidth(camera) + "x" + ResolveBloomSourceHeight(camera);
            }

            var mipIndex = ResolveBloomDebugMipIndex(debugView, mipCount);
            if (mipIndex < 0)
            {
                return debugView == BloomDebugView.Disabled ? "<none>" : "<unavailable>";
            }

            var prefix = debugView == BloomDebugView.Prefilter
                ? "PrefilterSnapshot"
                : debugView == BloomDebugView.Alpha
                    ? "Alpha"
                    : debugView == BloomDebugView.ThresholdMask
                        ? "ThresholdMask"
                        : debugView == BloomDebugView.FinalBloom
                            ? "FinalBloom"
                            : "MipDebug";

            return prefix + ":Mip" + mipIndex + ":" + GetBloomMipWidth(camera, mipIndex) + "x" + GetBloomMipHeight(camera, mipIndex);
        }

        public static string ResolveBloomStageFilterName(int stageIndexFromSmallest)
        {
            switch (Mathf.Clamp(stageIndexFromSmallest, 0, 5))
            {
                case 0:
                    return "Filter6";
                case 1:
                    return "Filter5";
                case 2:
                    return "Filter4";
                case 3:
                    return "Filter3";
                case 4:
                    return "Filter2";
                default:
                    return "Filter1";
            }
        }

        public static float ResolveBloomStageSize(BloomSettings settings, int stageIndexFromSmallest)
        {
            switch (Mathf.Clamp(stageIndexFromSmallest, 0, 5))
            {
                case 0:
                    return settings.Filter6Size;
                case 1:
                    return settings.Filter5Size;
                case 2:
                    return settings.Filter4Size;
                case 3:
                    return settings.Filter3Size;
                case 4:
                    return settings.Filter2Size;
                default:
                    return settings.Filter1Size;
            }
        }

        public static Color ResolveBloomStageTint(BloomSettings settings, int stageIndexFromSmallest)
        {
            switch (Mathf.Clamp(stageIndexFromSmallest, 0, 5))
            {
                case 0:
                    return settings.Filter6Tint;
                case 1:
                    return settings.Filter5Tint;
                case 2:
                    return settings.Filter4Tint;
                case 3:
                    return settings.Filter3Tint;
                case 4:
                    return settings.Filter2Tint;
                default:
                    return settings.Filter1Tint;
            }
        }

        public static Color CalculateBloomXRenderStageTint(BloomSettings settings, int stageIndexFromSmallest)
        {
            return ResolveBloomStageTint(settings, stageIndexFromSmallest) * (Mathf.Max(0f, settings.Intensity) / BloomXRenderStageCountMax);
        }

        public static float CalculateBloomBlurKernelSizePercent(BloomSettings settings, int stageIndexFromSmallest)
        {
            return ResolveBloomStageSize(settings, stageIndexFromSmallest) * Mathf.Max(0f, settings.SizeScale);
        }

        public static int ResolveBloomFirstStageMipIndex(int stageCount)
        {
            return Mathf.Clamp(BloomXRenderStageCountMax - Mathf.Clamp(stageCount, 1, BloomXRenderStageCountMax), 0, BloomXRenderStageCountMax - 1);
        }

        public static float CalculateBloomBlurRadius(BloomSettings settings, int sourceWidth, int stageIndexFromSmallest)
        {
            var kernelSizePercent = CalculateBloomBlurKernelSizePercent(settings, stageIndexFromSmallest);
            var sourceDimension = Mathf.Max(1, sourceWidth);

            return Mathf.Clamp(sourceDimension * kernelSizePercent * 0.01f * 0.5f, 0.00001f, BloomGaussianMaxSamples - 1);
        }

        public static int CalculateBloomGaussianSampleCount(float radius)
        {
            var clampedRadius = Mathf.Clamp(radius, 0.00001f, BloomGaussianMaxSamples - 1);
            var integerRadius = Mathf.Min(Mathf.CeilToInt(clampedRadius), BloomGaussianMaxSamples - 1);
            var sampleCount = 0;

            for (var sampleIndex = -integerRadius; sampleIndex <= integerRadius && sampleCount < BloomGaussianMaxSamples; sampleIndex += 2)
            {
                sampleCount++;
            }

            return sampleCount;
        }

        public static string FormatBloomStageDiagnostics(Camera camera, BloomSettings settings, int mipCount)
        {
            if (mipCount <= 0)
            {
                return "<none>";
            }

            var builder = new System.Text.StringBuilder();
            for (var stageIndex = 0; stageIndex < mipCount; stageIndex++)
            {
                if (stageIndex > 0)
                {
                    builder.Append(';');
                }

                var mipIndex = BloomXRenderStageCountMax - 1 - stageIndex;
                var width = GetBloomMipWidth(camera, mipIndex);
                var height = GetBloomMipHeight(camera, mipIndex);
                var radius = CalculateBloomBlurRadius(settings, width, stageIndex);
                var tint = ResolveBloomStageTint(settings, stageIndex);
                var xrenderTint = CalculateBloomXRenderStageTint(settings, stageIndex);

                builder.Append(stageIndex)
                    .Append(":Mip").Append(mipIndex)
                    .Append('/').Append(ResolveBloomStageFilterName(stageIndex))
                    .Append(':').Append(width).Append('x').Append(height)
                    .Append(":Size=").Append(ResolveBloomStageSize(settings, stageIndex).ToString("0.###"))
                    .Append(":KernelPct=").Append(CalculateBloomBlurKernelSizePercent(settings, stageIndex).ToString("0.###"))
                    .Append(":Radius=").Append(radius.ToString("0.###"))
                    .Append(":Samples=").Append(CalculateBloomGaussianSampleCount(radius))
                    .Append(":Tint=(").Append(tint.r.ToString("0.###"))
                    .Append(',').Append(tint.g.ToString("0.###"))
                    .Append(',').Append(tint.b.ToString("0.###"))
                    .Append("):XRTint=(").Append(xrenderTint.r.ToString("0.###"))
                    .Append(',').Append(xrenderTint.g.ToString("0.###"))
                    .Append(',').Append(xrenderTint.b.ToString("0.###"))
                    .Append(')');
            }

            return builder.ToString();
        }

        public static bool ShouldBypassBloomPrefilterThreshold(BloomSettings settings)
        {
            return settings.Threshold <= -1f;
        }

        public static float ResolveBloomPrefilterPostExposure(float postExposureMultiplier)
        {
            return Mathf.Max(0f, postExposureMultiplier);
        }

        public static float ResolveBloomPrefilterKnee(BloomSettings settings)
        {
            return 0f;
        }

        public static float ResolveBloomPrefilterSourceThreshold(BloomSettings settings, float postExposureMultiplier)
        {
            if (ShouldBypassBloomPrefilterThreshold(settings))
            {
                return 0f;
            }

            var prefilterExposure = ResolveBloomPrefilterPostExposure(postExposureMultiplier);
            return prefilterExposure > 0.0001f ? settings.Threshold / prefilterExposure : float.PositiveInfinity;
        }

        public static string FormatBloomPrefilterSourceThreshold(BloomSettings settings, float postExposureMultiplier)
        {
            var sourceThreshold = ResolveBloomPrefilterSourceThreshold(settings, postExposureMultiplier);
            return float.IsInfinity(sourceThreshold) ? "Infinity" : sourceThreshold.ToString("0.###");
        }

        public static RenderTextureFormat ResolveBloomRenderTextureFormat(Camera camera, BloomSettings settings, BloomDebugView debugView)
        {
            var fallbackFormat = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera).colorFormat;
            if (ShouldPreserveBloomAlpha(settings, debugView))
            {
                return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : fallbackFormat;
            }

            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ? RenderTextureFormat.RGB111110Float : fallbackFormat;
        }

        public static string ResolveBloomRenderTextureFormatReason(Camera camera, BloomSettings settings, BloomDebugView debugView)
        {
            var fallbackFormat = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera).colorFormat;
            var resolvedFormat = ResolveBloomRenderTextureFormat(camera, settings, debugView);
            if (ShouldPreserveBloomAlpha(settings, debugView))
            {
                return resolvedFormat == RenderTextureFormat.ARGBHalf ? "AlphaARGBHalf" : "AlphaFallbackTo" + fallbackFormat;
            }

            return resolvedFormat == RenderTextureFormat.RGB111110Float ? "PackedRGB111110Float" : "PackedFallbackTo" + fallbackFormat;
        }

        public static bool ShouldUsePostProcessFramework( // 定义判断当前 request 是否需要后处理框架的统一入口。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认相机任务是否有效。
            BurtRenderPipelineAsset asset) // 接收管线资产，用来读取后处理框架开关和 Volume 查询配置。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务。
            {
                return false; // 返回 false，避免为异常任务注册后处理资源或插入 Pass。
            }

            if (!request.IsValid) // 如果 request 已经被标记为无效，说明它不应该进入渲染图。
            {
                return false; // 返回 false，保持后处理和主渲染任务的有效性判断一致。
            }

            if (request.Camera == null) // 如果 request 没有关联 Camera，全屏后处理无法确定目标尺寸。
            {
                return false; // 返回 false，避免创建尺寸不明确的 PostProcessColor RT。
            }

            if (IsPreviewOrReflectionRequest(request)) // Unity Inspector/Asset Preview 和 ReflectionProbe 捕获不应该被项目里的 Volume Tonemapping 或调色影响。
            {
                return false; // 返回 false，避免 Cubemap/ReflectionProbe 等辅助渲染被后处理链改变颜色或曝光。
            }

            if (IsPostProcessSuppressedByShadingDebug())
            {
                return false;
            }

            var temporalAADebugRequested = IsTemporalAADebugRequested();
            var bloomDebugRequested = IsBloomDebugRequested();
            var autoExposureDebugRequested = IsAutoExposureDebugRequested();

            if (asset == null) // 如果管线资产为空，说明当前没有 Inspector 配置来源。
            {
                return temporalAADebugRequested || bloomDebugRequested || autoExposureDebugRequested; // TAA/Bloom/Auto Exposure debug still need the post stack so they can show a disabled/invalid state.
            }

            var settings = asset.PostProcessSettings; // 从管线资产读取后处理框架设置，资产内部会处理旧数据为空的兜底情况。

            if (settings == null) // 如果设置对象仍然为空，说明资产处于异常状态。
            {
                return temporalAADebugRequested || bloomDebugRequested || autoExposureDebugRequested; // Keep post-backed debug visible even when the asset settings object is missing.
            }

            if (!settings.EnablePostProcessing) // 如果资产关闭了后处理框架，就算 Volume 里有 Tonemapping 也不执行。
            {
                return temporalAADebugRequested || bloomDebugRequested || autoExposureDebugRequested; // Post-backed debug should fail visibly instead of being silently skipped.
            }

            return temporalAADebugRequested || bloomDebugRequested || autoExposureDebugRequested || HasActiveExposureVolume() || HasActiveTonemappingVolume() || HasActiveColorAdjustmentsVolume() || HasActiveColorGradingVolume() || HasActiveVignetteVolume() || HasActiveLensFlareVolume() || HasActiveDiaphragmDepthOfFieldVolume() || HasActiveRCASVolume() || HasActiveFastApproximateAAVolume() || HasActiveSubpixelMorphologicalAAVolume() || HasActiveBloomVolume() || HasActiveTemporalAASource(request); // Only real post effects allocate and run the framework; pure No-op is skipped.
        }

        public static bool ShouldUseBloom( // 定义判断当前 request 是否需要 Bloom 的统一入口。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认相机任务是否有效。
            BurtRenderPipelineAsset asset) // 接收管线资产，用来确认后处理总开关是否打开。
        {
            var bloomDebugRequested = IsBloomDebugRequested();
            if (request == null) // 如果 request 为空，说明没有合法渲染任务。
            {
                return false; // 返回 false，避免异常任务执行 Bloom。
            }

            if (!request.IsValid) // 如果 request 无效，就不应该执行任何后处理子链路。
            {
                return false; // 返回 false，保持 Bloom 和主渲染任务一致。
            }

            if (request.Camera == null) // 如果相机为空，就没有可靠的渲染尺寸。
            {
                return false; // 返回 false，避免申请尺寸不明确的 Bloom mip。
            }

            if (IsPreviewOrReflectionRequest(request)) // Preview / Reflection 不使用项目 Volume Bloom，避免资产预览被场景后处理污染。
            {
                return false; // 返回 false，让后处理 Pass 不申请 Bloom 临时 RT。
            }

            if (bloomDebugRequested)
            {
                return true; // Shading Debug 的 Bloom 入口需要强制生成 Bloom 中间纹理，即使资产/Volume 关闭。
            }

            if (!IsPostProcessEnabled(asset)) // 如果管线资产没有开启后处理框架，Volume Bloom 不允许改变画面。
            {
                return false; // 返回 false，让后处理 Pass 跳过 Bloom。
            }

            return bloomDebugRequested || HasActiveBloomVolume(); // Shading debug 里的 Bloom debug 也需要执行 Bloom 链来生成源纹理。
        }

        public static bool ShouldUseColorAdjustments( // 定义判断当前 request 是否需要 Color Adjustments 的统一入口。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认相机任务是否有效。
            BurtRenderPipelineAsset asset) // 接收管线资产，用来确认后处理总开关是否打开。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务。
            {
                return false; // 返回 false，避免异常任务执行颜色调整。
            }

            if (!request.IsValid) // 如果 request 无效，就不应该执行任何后处理子链路。
            {
                return false; // 返回 false，保持后处理子效果和主渲染任务一致。
            }

            if (request.Camera == null) // 如果相机为空，就没有可靠的渲染上下文可以驱动 Volume。
            {
                return false; // 返回 false，避免在异常相机任务里执行颜色调整。
            }

            if (IsPreviewOrReflectionRequest(request)) // Preview / Reflection 不使用项目 Volume 调色，避免资产预览颜色被场景后处理污染。
            {
                return false; // 返回 false，让后处理 Pass 不上传 Color Adjustments。
            }

            if (!IsPostProcessEnabled(asset)) // 如果管线资产没有开启后处理框架，Volume 调色不允许改变画面。
            {
                return false; // 返回 false，让后处理 Pass 不上传颜色调整参数。
            }

            return HasActiveColorAdjustmentsVolume(); // 只有当前 VolumeStack 中存在有效 Color Adjustments 时，才需要执行颜色调整。
        }

        public static bool ShouldUseVignette(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return IsPostProcessEffectAllowed(request, asset) && HasActiveVignetteVolume();
        }

        public static bool ShouldUseLensFlare(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return IsPostProcessEffectAllowed(request, asset) && HasActiveLensFlareVolume();
        }

        public static bool ShouldUseDiaphragmDepthOfField(
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset)
        {
            return IsPostProcessEffectAllowed(request, asset) && HasActiveDiaphragmDepthOfFieldVolume();
        }

        public static bool ShouldUseColorGrading(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return IsPostProcessEffectAllowed(request, asset) && HasActiveColorGradingVolume();
        }

        public static bool ShouldUseRCAS(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return IsPostProcessEffectAllowed(request, asset) && HasActiveRCASVolume();
        }

        public static bool ShouldUseFastApproximateAA(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return IsPostProcessEffectAllowed(request, asset) && HasActiveFastApproximateAAVolume();
        }

        public static bool ShouldUseSubpixelMorphologicalAA(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return IsPostProcessEffectAllowed(request, asset) && HasActiveSubpixelMorphologicalAAVolume();
        }

        private static bool IsPostProcessEffectAllowed(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null)
            {
                return false;
            }

            if (!request.IsValid)
            {
                return false;
            }

            if (request.Camera == null)
            {
                return false;
            }

            if (IsPreviewOrReflectionRequest(request))
            {
                return false;
            }

            return IsPostProcessEnabled(asset);
        }

        public static void UpdateVolumeStack( // 定义每个相机渲染前刷新 VolumeStack 的函数，保证后处理参数来自当前相机位置。
            BurtRenderRequest request, // 接收当前 request，用来读取相机 Transform。
            BurtRenderPipelineAsset asset) // 接收管线资产，用来读取 Volume 查询 LayerMask。
        {
            if (request == null) // 如果 request 为空，说明没有合法相机可以驱动 Volume 查询。
            {
                return; // 直接返回，避免访问空 request。
            }

            if (!request.IsValid) // 如果 request 无效，说明它不会进入正常渲染流程。
            {
                return; // 直接返回，避免无效任务刷新全局 VolumeStack。
            }

            var camera = request.Camera; // 从 request 里读取当前相机。

            if (camera == null) // 如果相机为空，就没有 Transform 可以参与本地 Volume 混合。
            {
                return; // 直接返回，后续解析会使用 VolumeStack 当前值或默认值。
            }

            if (IsPreviewOrReflectionRequest(request)) // Preview / Reflection 相机来自 Unity 辅助渲染，不应该刷新或继承场景 Volume。
            {
                return; // 直接返回，避免资产预览或 ReflectionProbe 捕获被场景 Tonemapping/Color Adjustments 影响。
            }

            if (asset == null) // 如果资产为空，就没有后处理 Volume LayerMask 配置来源。
            {
                return; // 直接返回，保持异常路径不改变全局 Volume 状态。
            }

            UpdateVolumeStack(camera, asset); // 按当前相机位置和资产 LayerMask 刷新 Unity VolumeStack，SSR/Shadow 等非后处理 Volume 也复用这套查询层。
        }

        public static void UpdateVolumeStack(Camera camera, BurtRenderPipelineAsset asset) // 在 request 创建前也可刷新 VolumeStack，让阴影 culling 距离能读取 Global Volume。
        {
            if (camera == null || asset == null) // 没有相机或资产时不能安全查询场景 Volume。
            {
                return; // 直接返回，调用方继续使用资产默认值。
            }

            if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) // Preview/Reflection 不继承场景 Volume，避免资产预览和探针捕获被场景设置污染。
            {
                return; // 跳过刷新，保持当前 VolumeStack 不被辅助相机改写。
            }

            VolumeManager.instance.Update(camera.transform, asset.PostProcessVolumeLayerMask); // 使用同一套 Volume LayerMask 查询后处理、SSR、TAA 和阴影覆盖。
        }

        public static bool ShouldLogPostProcessDebug(BurtRenderPipelineAsset asset) // 定义判断是否输出后处理调试日志的统一入口。
        {
            if (asset == null) // 如果没有资产配置，就没有日志开关来源。
            {
                return false; // 返回 false，避免异常路径刷日志。
            }

            var settings = asset.PostProcessSettings; // 读取后处理设置，便于检查调试日志开关。

            return settings != null && settings.EnableFrameworkDebugLog; // 只有设置存在并且显式打开日志时才允许 Pass 打印信息。
        }

        public static TonemappingMode ResolveTonemappingMode(BurtRenderPipelineAsset asset) // 定义安全解析 Tonemapping 模式的函数，避免 Pass 直接处理 VolumeStack 细节。
        {
            if (!IsPostProcessEnabled(asset)) // 如果管线资产没有打开后处理框架，就不允许 Volume Tonemapping 改变画面。
            {
                return TonemappingMode.None; // 返回 None，shader 会走原样输出。
            }

            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Tonemapping 组件。

            if (tonemapping == null) // 如果当前 VolumeStack 没有 Tonemapping 组件，就没有正式 Tonemapping 效果。
            {
                return TonemappingMode.None; // 返回 None，保持 No-op 或无后处理状态。
            }

            if (!tonemapping.IsEnabled()) // 如果组件未激活或模式为 None，就不执行 Tonemapping。
            {
                return TonemappingMode.None; // 返回 None，避免 Volume 默认值改变画面。
            }

            return tonemapping.mode.value; // 返回当前 Volume 混合后的 Tonemapping 模式。
        }

        public static float ResolvePostExposureMultiplier(BurtRenderPipelineAsset asset) // 定义把 Volume EV 曝光转换为线性倍率的函数。
        {
            return ResolvePhysicalExposureSettings(asset).Multiplier;
        }

        public static PhysicalExposureSettings ResolvePhysicalExposureSettings(BurtRenderPipelineAsset asset)
        {
            return ResolvePhysicalExposureSettings(null, asset);
        }

        public static PhysicalExposureSettings ResolvePhysicalExposureSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolvePhysicalExposureSettings(request, asset, false, 0f);
        }

        public static PhysicalExposureSettings ResolvePhysicalExposureSettingsForFrame(BurtRenderRequest request, BurtRenderPipelineAsset asset, float deltaTime)
        {
            return ResolvePhysicalExposureSettings(request, asset, true, deltaTime);
        }

        private static PhysicalExposureSettings ResolvePhysicalExposureSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool updateAutoExposure, float deltaTime)
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，曝光参数不应该影响画面。
            {
                return PhysicalExposureSettings.Default;
            }

            var exposure = GetExposureVolumeComponent();

            if (exposure == null)
            {
                return PhysicalExposureSettings.Default;
            }

            if (!exposure.IsEnabled())
            {
                return PhysicalExposureSettings.Default;
            }

            if (exposure.mode.value == ExposureMode.Automatic || exposure.mode.value == ExposureMode.AutomaticHistogram)
            {
                var camera = request != null ? request.Camera : null;
                return GpuExposureUtility.ResolveSettings(camera, exposure);
            }

            return new PhysicalExposureSettings(
                exposure.mode.value,
                exposure.manualEV100.value,
                exposure.iso.value,
                exposure.shutterTime.value,
                exposure.aperture.value,
                exposure.calibration.value,
                exposure.compensation.value);
        }

        public static TonemappingFilmSettings ResolveTonemappingFilmSettings(BurtRenderPipelineAsset asset) // 定义解析 UE/XRender Filmic 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，Film 参数不应该影响任何全屏拷贝。
            {
                return TonemappingFilmSettings.Default; // 返回默认参数，保证 shader 即使被调用也处于稳定状态。
            }

            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Tonemapping 组件。

            if (tonemapping == null) // 如果当前 VolumeStack 没有 Tonemapping 组件，就没有可覆盖的 Film 参数。
            {
                return TonemappingFilmSettings.Default; // 返回默认参数，对齐 XRender/UE 的基础外观。
            }

            if (!tonemapping.IsEnabled()) // 如果 Tonemapping 组件未启用，Film 参数不应该参与 No-op Copy。
            {
                return TonemappingFilmSettings.Default; // 返回默认参数，避免关闭模式下上传无意义的自定义值。
            }

            return new TonemappingFilmSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供 Pass 一次性上传给 shader。
                tonemapping.filmSlope.value, // 读取 Film Slope。
                tonemapping.filmToe.value, // 读取 Film Toe。
                tonemapping.filmShoulder.value, // 读取 Film Shoulder。
                tonemapping.filmBlackClip.value, // 读取 Film Black Clip。
                tonemapping.filmWhiteClip.value, // 读取 Film White Clip。
                tonemapping.blueCorrection.value, // 读取 Blue Correction。
                tonemapping.expandGamut.value, // 读取 Expand Gamut。
                tonemapping.toneCurveAmount.value); // 读取 Tone Curve Amount。
        }

        public static ColorAdjustmentsSettings ResolveColorAdjustmentsSettings(BurtRenderPipelineAsset asset) // 定义解析 Color Adjustments 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，调色参数不应该影响任何全屏拷贝。
            {
                return ColorAdjustmentsSettings.Default; // 返回默认调色参数，保证 shader 处于稳定的中性状态。
            }

            var colorAdjustments = GetColorAdjustmentsVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Color Adjustments 组件。

            if (colorAdjustments == null) // 如果当前 VolumeStack 没有 Color Adjustments 组件，就没有可覆盖的调色参数。
            {
                return ColorAdjustmentsSettings.Default; // 返回默认调色参数，让后处理保持中性。
            }

            if (!colorAdjustments.IsEnabled()) // 如果 Color Adjustments 组件没有真正启用，就不应该上传非中性调色参数。
            {
                return ColorAdjustmentsSettings.Default; // 返回默认调色参数，避免 Volume 默认值改变画面。
            }

            return new ColorAdjustmentsSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供后处理 Pass 一次性上传。
                colorAdjustments.saturation.value, // 读取饱和度。
                colorAdjustments.contrast.value, // 读取对比度。
                colorAdjustments.gamma.value, // 读取 Gamma。
                colorAdjustments.colorFilter.value); // 读取颜色滤镜。
        }

        public static VignetteSettings ResolveVignetteSettings(BurtRenderPipelineAsset asset)
        {
            if (!IsPostProcessEnabled(asset))
            {
                return VignetteSettings.Default;
            }

            var vignette = GetVignetteVolumeComponent();
            if (vignette == null || !vignette.IsEnabled())
            {
                return VignetteSettings.Default;
            }

            return new VignetteSettings(
                true,
                vignette.color.value,
                vignette.intensity.value,
                vignette.edgeWidth.value,
                vignette.edgeSoftness.value,
                vignette.fisheyeFovDeg.value,
                vignette.followAspect.value);
        }

        public static LensFlareSettings ResolveLensFlareSettings(BurtRenderPipelineAsset asset)
        {
            if (!IsPostProcessEnabled(asset))
            {
                return LensFlareSettings.Default;
            }

            var lensFlare = GetLensFlareVolumeComponent();
            if (lensFlare == null || !lensFlare.IsEnabled())
            {
                return LensFlareSettings.Default;
            }

            return new LensFlareSettings(
                true,
                lensFlare.data.value,
                lensFlare.intensity.value,
                lensFlare.scale.value,
                lensFlare.tint.value);
        }

        public static DiaphragmDepthOfFieldSettings ResolveDiaphragmDepthOfFieldSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!IsPostProcessEnabled(asset))
            {
                return DiaphragmDepthOfFieldSettings.Default;
            }

            var depthOfField = GetDiaphragmDepthOfFieldVolumeComponent();
            return DiaphragmDepthOfFieldSettings.Create(depthOfField, request != null ? request.Camera : null);
        }

        public static ColorGradingSettings ResolveColorGradingSettings(BurtRenderPipelineAsset asset)
        {
            if (!IsPostProcessEnabled(asset))
            {
                return ColorGradingSettings.Default;
            }

            var colorGrading = GetColorGradingVolumeComponent();
            if (colorGrading == null || !colorGrading.IsEnabled())
            {
                return ColorGradingSettings.Default;
            }

            return new ColorGradingSettings(
                true,
                colorGrading.enableWhiteBalance.value,
                colorGrading.temperatureMode.value,
                colorGrading.whiteTemp.value,
                colorGrading.whiteTint.value,
                colorGrading.enableColorGrading.value,
                colorGrading.globalSaturation.value,
                colorGrading.globalContrast.value,
                colorGrading.globalGamma.value,
                colorGrading.globalGain.value,
                colorGrading.globalOffset.value,
                colorGrading.shadowsSaturation.value,
                colorGrading.shadowsContrast.value,
                colorGrading.shadowsGamma.value,
                colorGrading.shadowsGain.value,
                colorGrading.shadowsOffset.value,
                colorGrading.shadowsMax.value,
                colorGrading.midtonesSaturation.value,
                colorGrading.midtonesContrast.value,
                colorGrading.midtonesGamma.value,
                colorGrading.midtonesGain.value,
                colorGrading.midtonesOffset.value,
                colorGrading.highlightsSaturation.value,
                colorGrading.highlightsContrast.value,
                colorGrading.highlightsGamma.value,
                colorGrading.highlightsGain.value,
                colorGrading.highlightsOffset.value,
                colorGrading.highlightsMin.value,
                colorGrading.highlightsMax.value,
                colorGrading.colorGradingIntensity.value,
                colorGrading.colorGradingLUT.value,
                colorGrading.colorLUTContribution.value,
                colorGrading.colorLUTSize.value);
        }

        public static RCASSettings ResolveRCASSettings(BurtRenderPipelineAsset asset)
        {
            if (!IsPostProcessEnabled(asset))
            {
                return RCASSettings.Default;
            }

            var rcas = GetRCASVolumeComponent();
            return rcas != null && rcas.IsEnabled()
                ? new RCASSettings(true, rcas.sharpness.value)
                : RCASSettings.Default;
        }

        public static FastApproximateAASettings ResolveFastApproximateAASettings(BurtRenderPipelineAsset asset)
        {
            if (!IsPostProcessEnabled(asset))
            {
                return FastApproximateAASettings.Default;
            }

            var fxaa = GetFastApproximateAAVolumeComponent();
            return fxaa != null && fxaa.IsEnabled()
                ? new FastApproximateAASettings(true, fxaa.subpixel.value, fxaa.edgeThreshold.value, fxaa.edgeThresholdMin.value)
                : FastApproximateAASettings.Default;
        }

        public static SubpixelMorphologicalAASettings ResolveSubpixelMorphologicalAASettings(BurtRenderPipelineAsset asset)
        {
            if (!IsPostProcessEnabled(asset))
            {
                return SubpixelMorphologicalAASettings.Default;
            }

            var smaa = GetSubpixelMorphologicalAAVolumeComponent();
            return smaa != null && smaa.IsEnabled()
                ? new SubpixelMorphologicalAASettings(true, smaa.threshold.value, smaa.blendStrength.value, smaa.maxSearchSteps.value)
                : SubpixelMorphologicalAASettings.Default;
        }

        public static BloomSettings ResolveBloomSettings(BurtRenderPipelineAsset asset) // 定义解析 Bloom 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            var bloomDebugRequested = IsBloomDebugRequested();
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，Bloom 参数不应该影响画面。
            {
                return bloomDebugRequested ? CreateBloomDebugFallbackSettings() : BloomSettings.Default; // Shading Debug 需要一条可见的 Bloom 链。
            }

            var bloom = GetBloomVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Bloom 组件。

            if (bloom == null) // 如果当前 VolumeStack 没有 Bloom 组件，就没有 Bloom 效果。
            {
                return bloomDebugRequested ? CreateBloomDebugFallbackSettings() : BloomSettings.Default; // 没有 Volume 时仍允许 Bloom debug 生成中间纹理。
            }

            if (!bloom.IsEnabled()) // 如果 Bloom 组件未激活或强度为 0，就不执行 Bloom。
            {
                return bloomDebugRequested ? CreateBloomDebugSettings(bloom) : BloomSettings.Default; // Debug 入口保留 Volume 参数，但强制执行 Bloom 链。
            }

            return new BloomSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供 Pass 一次性使用。
                true, // 标记 Bloom 已启用。
                bloom.threshold.value, // 读取阈值。
                bloom.softKnee.value, // 读取软阈值。
                bloom.intensity.value, // 读取合成强度。
                bloom.scatter.value, // 读取散布权重。
                bloom.sizeScale.value, // 读取 XRender 风格 Bloom 尺寸倍率。
                bloom.quality.value, // 读取 XRender 风格 Bloom 质量档。
                bloom.maxIterations.value, // 读取最大 mip 数。
                bloom.bloomAlphaChannel.value, // 读取 Bloom alpha 输出开关。
                bloom.debugView.value, // 读取 Bloom 调试输出模式。
                bloom.filter1Size.value, // 读取 Filter1 尺寸。
                bloom.filter2Size.value, // 读取 Filter2 尺寸。
                bloom.filter3Size.value, // 读取 Filter3 尺寸。
                bloom.filter4Size.value, // 读取 Filter4 尺寸。
                bloom.filter5Size.value, // 读取 Filter5 尺寸。
                bloom.filter6Size.value, // 读取 Filter6 尺寸。
                bloom.filter1Tint.value, // 读取 Filter1 tint。
                bloom.filter2Tint.value, // 读取 Filter2 tint。
                bloom.filter3Tint.value, // 读取 Filter3 tint。
                bloom.filter4Tint.value, // 读取 Filter4 tint。
                bloom.filter5Tint.value, // 读取 Filter5 tint。
                bloom.filter6Tint.value); // 读取 Filter6 tint。
        }

        private static BloomSettings CreateBloomDebugSettings(BloomVolumeComponent bloom)
        {
            if (bloom == null)
            {
                return CreateBloomDebugFallbackSettings();
            }

            return new BloomSettings(
                true,
                bloom.threshold.value,
                bloom.softKnee.value,
                Mathf.Max(bloom.intensity.value, BloomSettings.DefaultIntensity),
                bloom.scatter.value,
                bloom.sizeScale.value,
                BloomSettings.IsQualityEnabled(bloom.quality.value) ? bloom.quality.value : BloomSettings.DefaultQuality,
                bloom.maxIterations.value,
                bloom.bloomAlphaChannel.value || ResolveBloomShadingDebugView() == BloomDebugView.Alpha,
                ResolveBloomShadingDebugView(),
                bloom.filter1Size.value,
                bloom.filter2Size.value,
                bloom.filter3Size.value,
                bloom.filter4Size.value,
                bloom.filter5Size.value,
                bloom.filter6Size.value,
                bloom.filter1Tint.value,
                bloom.filter2Tint.value,
                bloom.filter3Tint.value,
                bloom.filter4Tint.value,
                bloom.filter5Tint.value,
                bloom.filter6Tint.value);
        }

        private static BloomSettings CreateBloomDebugFallbackSettings()
        {
            return new BloomSettings(
                true,
                BloomSettings.DefaultThreshold,
                BloomSettings.DefaultSoftKnee,
                BloomSettings.DefaultIntensity,
                BloomSettings.DefaultScatter,
                BloomSettings.DefaultSizeScale,
                BloomSettings.DefaultQuality,
                BloomSettings.DefaultMaxMipCount,
                ResolveBloomShadingDebugView() == BloomDebugView.Alpha,
                ResolveBloomShadingDebugView(),
                BloomSettings.DefaultFilter1Size,
                BloomSettings.DefaultFilter2Size,
                BloomSettings.DefaultFilter3Size,
                BloomSettings.DefaultFilter4Size,
                BloomSettings.DefaultFilter5Size,
                BloomSettings.DefaultFilter6Size,
                BloomSettings.DefaultFilter1Tint,
                BloomSettings.DefaultFilter2Tint,
                BloomSettings.DefaultFilter3Tint,
                BloomSettings.DefaultFilter4Tint,
                BloomSettings.DefaultFilter5Tint,
                BloomSettings.DefaultFilter6Tint);
        }

        public static bool ShouldUseTemporalAA(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return BurtTemporalAAUtility.ShouldUseTemporalAA(request, asset);
        }

        internal static string ResolveTemporalAAConfigurationDisabledReason(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return "InvalidRequest";
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return "AuxiliaryCamera";
            }

            if (IsPostProcessSuppressedByShadingDebug())
            {
                return "PostProcessSuppressedByShadingDebug";
            }

            if (asset == null)
            {
                return "PostProcessAssetMissing";
            }

            var postProcessSettings = asset.PostProcessSettings;
            if (postProcessSettings == null)
            {
                return "PostProcessSettingsMissing";
            }

            if (!postProcessSettings.EnablePostProcessing)
            {
                return "PostProcessFrameworkDisabledInAsset";
            }

            var temporalAA = GetTemporalAAVolumeComponent();
            if (IsTemporalAAExplicitlyDisabledByVolume(temporalAA))
            {
                return "TemporalAAVolumeEnabledFalse";
            }

            if (HasTemporalAAEnabledByCameraData(request))
            {
                return null;
            }

            if (temporalAA == null)
            {
                return "TemporalAAVolumeMissing";
            }

            if (!temporalAA.active)
            {
                return "TemporalAAVolumeInactive";
            }

            if (!temporalAA.enabled.overrideState)
            {
                return "TemporalAAVolumeEnabledNotOverridden";
            }

            if (!temporalAA.enabled.value)
            {
                return "TemporalAAVolumeEnabledFalse";
            }

            return null;
        }

        public static BurtTemporalAASettings ResolveTemporalAASettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return BurtTemporalAASettings.Default;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return BurtTemporalAASettings.Default;
            }

            if (!IsPostProcessEnabled(asset))
            {
                return BurtTemporalAASettings.Default;
            }

            var temporalAA = GetTemporalAAVolumeComponent();
            if (IsTemporalAAExplicitlyDisabledByVolume(temporalAA))
            {
                return BurtTemporalAASettings.Default;
            }

            if (temporalAA == null || !temporalAA.IsEnabled())
            {
                return HasTemporalAAEnabledByCameraData(request)
                    ? CreateDefaultTemporalAASettings(enabled: true)
                    : BurtTemporalAASettings.Default;
            }

            return new BurtTemporalAASettings(
                true,
                temporalAA.jitterScale.value,
                temporalAA.sharpness.value,
                temporalAA.untrustedMotionFeedbackScale.value,
                temporalAA.motionEdgeResponsiveStrength.value,
                temporalAA.depthEdgeResponsiveStrength.value,
                temporalAA.upscaleFactor.value);
        }

        private static BurtTemporalAASettings CreateDefaultTemporalAASettings(bool enabled)
        {
            var defaults = BurtTemporalAASettings.Default;
            return new BurtTemporalAASettings(
                enabled,
                defaults.JitterScale,
                defaults.Sharpness,
                defaults.UntrustedMotionFeedbackScale,
                defaults.MotionEdgeResponsiveStrength,
                defaults.DepthEdgeResponsiveStrength,
                defaults.UpscaleFactor);
        }

        public static int ResolveBloomMipCount(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 定义解析当前 request 实际 Bloom mip 数的函数。
        {
            if (!ShouldUseBloom(request, asset)) // 如果当前 request 不执行 Bloom，就不需要任何 mip。
            {
                return 0; // 返回 0 表示跳过 Bloom 链。
            }

            return CalculateBloomMipCount(request.Camera, ResolveBloomSettings(asset)); // 按相机尺寸和 Volume 参数计算实际 mip 数。
        }

        public static int CalculateBloomMipCount(Camera camera, BloomSettings settings) // 根据相机尺寸和设置计算实际 Bloom mip 数。
        {
            if (!settings.Enabled) // Bloom 未启用时不申请任何临时 RT。
            {
                return 0; // 返回 0 表示跳过 Bloom 链。
            }

            if (camera == null) // 没有相机时无法确定尺寸。
            {
                return 0; // 返回 0，保持安全跳过。
            }

            var qualityMipCount = ResolveBloomQualityMipCount(settings.Quality); // 按 XRender Q1-Q5 映射到最多参与的 Bloom 阶段数。
            if (qualityMipCount <= 0) // 质量档关闭或无效时跳过 Bloom 链。
            {
                return 0; // 返回 0 表示不申请 Bloom mip。
            }

            return Mathf.Min(Mathf.Clamp(settings.MaxMipCount, 1, BloomXRenderStageCountMax), qualityMipCount); // XRender 从公共半分辨率 Scene Downsample 开始，质量档只决定六级链里从哪个 mip 开始做高斯。
        }

        private static int ResolveBloomQualityMipCount(BloomQuality quality) // 对齐 XRender Bloom Q1-Q5 到 stage/mip 数的映射。
        {
            switch (quality)
            {
                case BloomQuality.Q1:
                case BloomQuality.Q2:
                    return 3; // XRender Q1/Q2 都使用 3 个 Bloom stage。
                case BloomQuality.Q3:
                    return 4; // XRender Q3 使用 4 个 Bloom stage。
                case BloomQuality.Q4:
                    return 5; // XRender Q4 使用 5 个 Bloom stage。
                case BloomQuality.Q5:
                    return 6; // XRender Q5 使用 6 个 Bloom stage。
                default:
                    return 0; // 0 表示关闭 Bloom stage。
            }
        }

        public static void LogPostProcessExecuted( // 定义后处理执行日志，集中格式避免 Pass 内部堆字符串逻辑。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文，用来读取相机和资产设置。
            TonemappingMode tonemappingMode, // 接收本次执行使用的 Tonemapping 模式。
            float postExposureMultiplier, // 接收本次执行使用的线性曝光倍率。
            PreExposureState preExposureState,
            bool useColorAdjustments, // 接收本次执行是否启用了 Color Adjustments。
            bool useVignette,
            VignetteSettings vignetteSettings,
            BloomSettings bloomSettings, // 接收本次执行使用的 Bloom 参数。
            int bloomMipCount, // 接收本次实际使用的 Bloom mip 数。
            bool useLensFlare = false,
            LensFlareSettings lensFlareSettings = default,
            bool useDiaphragmDepthOfField = false,
            DiaphragmDepthOfFieldSettings diaphragmDepthOfFieldSettings = default)
        {
            if (context == null) // 如果上下文为空，说明调用方状态异常。
            {
                return; // 直接返回，避免日志函数自己触发空引用。
            }

            if (!ShouldLogPostProcessDebug(context.Asset)) // 如果用户没有打开后处理调试日志，就不输出任何内容。
            {
                return; // 直接返回，保持默认运行时不刷 Console。
            }

            var request = context.Request; // 读取当前渲染请求，用来输出相机名称。

            var camera = request != null ? request.Camera : null; // 读取 request 里的相机，request 为空时保持 camera 为空。

            var cameraName = camera != null ? camera.name : "<null>"; // 把相机名转换成安全字符串，避免日志里出现空引用。

            var temporalAA = request != null ? request.TemporalAA : null;
            var temporalAADebugRequested = IsTemporalAADebugRequested();
            var bloomDebugView = ResolveBloomDebugView(bloomSettings);
            var bloomDebugRequested = IsBloomDebugRequested();
            var preserveBloomAlpha = ShouldPreserveBloomAlpha(bloomSettings, bloomDebugView);
            var bloomAlphaReason = ResolveBloomAlphaReason(bloomSettings, bloomDebugView);
            var bloomRenderTextureFormat = ResolveBloomRenderTextureFormat(camera, bloomSettings, bloomDebugView);
            var bloomRenderTextureFormatReason = ResolveBloomRenderTextureFormatReason(camera, bloomSettings, bloomDebugView);
            var bloomMipSizes = FormatBloomMipSizes(camera, bloomMipCount);
            var bloomMipPixels = CalculateBloomMipPixelCount(camera, bloomMipCount);
            var bloomStages = FormatBloomStageDiagnostics(camera, bloomSettings, bloomMipCount);
            var bloomDebugTarget = FormatBloomDebugTarget(camera, bloomDebugView, bloomMipCount);
            var residualPostExposureMultiplier = PreExposureUtility.SanitizeExposure(postExposureMultiplier, 1f) * preExposureState.InvPreExposure;
            var bloomPrefilterPostExposure = ResolveBloomPrefilterPostExposure(residualPostExposureMultiplier);
            var bloomPrefilterKnee = ResolveBloomPrefilterKnee(bloomSettings);
            var bloomPrefilterSourceThreshold = FormatBloomPrefilterSourceThreshold(bloomSettings, residualPostExposureMultiplier);
            var bloomPrefilterBypassThreshold = ShouldBypassBloomPrefilterThreshold(bloomSettings);
            var temporalHistory = BurtTemporalAAUtility.GetHistoryStatus(camera);
            var temporalAADisabledReason = BurtTemporalAAUtility.ResolveTemporalAADiagnosticDisabledReason(context.Request, context.Asset, context.RenderOptions);
            var temporalAASource = ResolveTemporalAASourceLabel(context.Request);
            var temporalAAVolumeState = ResolveTemporalAAVolumeStateLabel();
            var temporalAAFinalBlitYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request);
            const float temporalAADebugYFlip = 0f;
            var exposure = ResolvePhysicalExposureSettings(context.Request, context.Asset);
            var colorGradingSettings = ResolveColorGradingSettings(context.Asset);
            var rcasSettings = ResolveRCASSettings(context.Asset);
            var fxaaSettings = ResolveFastApproximateAASettings(context.Asset);
            var smaaSettings = ResolveSubpixelMorphologicalAASettings(context.Asset);
            var temporalAAEnabled = temporalAA != null && temporalAA.Enabled;
            var temporalAASettings = temporalAA != null ? temporalAA.Settings : BurtTemporalAASettings.Default;
            var logBuilder = new System.Text.StringBuilder(4096);
            logBuilder.Append("[BurtRP][PostProcess] Executed. Camera=").Append(cameraName)
                .Append(" Tonemapping=").Append(tonemappingMode)
                .Append(" ExposureMode=").Append(exposure.Mode)
                .Append(" EV100=").Append(exposure.EV100.ToString("0.###"))
                .Append(" ISO=").Append(exposure.ISO.ToString("0.###"))
                .Append(" Shutter=").Append(exposure.ShutterTime.ToString("0.######"))
                .Append(" Aperture=").Append(exposure.Aperture.ToString("0.###"))
                .Append(" ExposureCalibration=").Append(exposure.Calibration.ToString("0.###"))
                .Append(" ExposureCompensationEV=").Append(exposure.Compensation.ToString("0.###"))
                .Append(" ExposureMul=").Append(postExposureMultiplier)
                .Append(" PreExposure=").Append(preExposureState.PreExposure.ToString("0.###"))
                .Append(" InvPreExposure=").Append(preExposureState.InvPreExposure.ToString("0.###"))
                .Append(" ResidualExposure=").Append(residualPostExposureMultiplier.ToString("0.###"))
                .Append(" PreExposureEnabled=").Append(preExposureState.Enabled)
                .Append(" AutoAvgLuma=").Append(exposure.AutoAverageLuminance.ToString("0.###"))
                .Append(" AutoAvgLogLum=").Append(exposure.AutoAverageLogLuminance.ToString("0.###"))
                .Append(" AutoTargetEV100=").Append(exposure.AutoTargetEV100.ToString("0.###"))
                .Append(" AutoMinMaxEV100=").Append(exposure.AutoMinEV100.ToString("0.###")).Append('/').Append(exposure.AutoMaxEV100.ToString("0.###"))
                .Append(" AutoSpeedUpDown=").Append(exposure.AutoSpeedUp.ToString("0.###")).Append('/').Append(exposure.AutoSpeedDown.ToString("0.###"))
                .Append(" AutoLowHighPercent=").Append(exposure.AutoLowPercent.ToString("0.###")).Append('/').Append(exposure.AutoHighPercent.ToString("0.###"))
                .Append(" AutoHistogramMinMaxEV100=").Append(exposure.AutoHistogramMinEV100.ToString("0.###")).Append('/').Append(exposure.AutoHistogramMaxEV100.ToString("0.###"))
                .Append(" AutoSample=").Append(exposure.AutoHasSample)
                .Append(" AutoSampleCount=").Append(exposure.AutoSampleCount)
                .Append(" AutoSampleAgeFrames=").Append(exposure.AutoSampleAgeFrames)
                .Append(" AutoSampleRejectedReason=").Append(exposure.AutoSampleRejectedReason)
                .Append(" AutoReadbackPending=").Append(exposure.AutoReadbackPending)
                .Append(" AutoReadbackAgeFrames=").Append(exposure.AutoReadbackAgeFrames)
                .Append(" ColorAdjustments=").Append(useColorAdjustments)
                .Append(" ColorGrading=").Append(colorGradingSettings.Enabled)
                .Append(" ColorGradingWhiteBalance=").Append(colorGradingSettings.WhiteBalanceEnabled)
                .Append(" ColorGradingLUT=").Append(colorGradingSettings.HasLut)
                .Append(" ColorGradingLUTContribution=").Append(colorGradingSettings.LutContribution.ToString("0.###"))
                .Append(" ColorGradingIntensity=").Append(colorGradingSettings.Intensity.ToString("0.###"))
                .Append(" Vignette=").Append(useVignette)
                .Append(" VignetteIntensity=").Append(vignetteSettings.Intensity.ToString("0.###"))
                .Append(" VignetteEdgeWidth=").Append(vignetteSettings.EdgeWidth.ToString("0.###"))
                .Append(" VignetteEdgeSoftness=").Append(vignetteSettings.EdgeSoftness.ToString("0.###"))
                .Append(" VignetteFisheyeFovDeg=").Append(vignetteSettings.FisheyeFovDeg.ToString("0.###"))
                .Append(" VignetteFollowAspect=").Append(vignetteSettings.FollowAspect)
                .Append(" VignetteColor=(").Append(vignetteSettings.Color.r.ToString("0.###")).Append(',')
                .Append(vignetteSettings.Color.g.ToString("0.###")).Append(',')
                .Append(vignetteSettings.Color.b.ToString("0.###")).Append(',')
                .Append(vignetteSettings.Color.a.ToString("0.###")).Append(')')
                .Append(" LensFlare=").Append(useLensFlare)
                .Append(" LensFlareIntensity=").Append(lensFlareSettings.TotalParams.x.ToString("0.###"))
                .Append(" LensFlareScale=").Append(lensFlareSettings.TotalParams.y.ToString("0.###"))
                .Append(" LensFlareData=").Append(lensFlareSettings.TextureFlags1.z > 0.5f)
                .Append(" DiaphragmDOF=").Append(useDiaphragmDepthOfField)
                .Append(" DiaphragmDOFFocusM=").Append(diaphragmDepthOfFieldSettings.FocusDistanceMeters.ToString("0.###"))
                .Append(" DiaphragmDOFMaxRadiusPx=").Append(diaphragmDepthOfFieldSettings.MaxRadiusPixels.ToString("0.###"))
                .Append(" DiaphragmDOFSqueeze=").Append(diaphragmDepthOfFieldSettings.SqueezeFactor.ToString("0.###"))
                .Append(" DiaphragmDOFVisualize=").Append(diaphragmDepthOfFieldSettings.VisualizeDOF)
                .Append(" SMAA=").Append(smaaSettings.Enabled)
                .Append(" SMAAThreshold=").Append(smaaSettings.Threshold.ToString("0.###"))
                .Append(" SMAABlend=").Append(smaaSettings.BlendStrength.ToString("0.###"))
                .Append(" SMAAMaxSearchSteps=").Append(smaaSettings.MaxSearchSteps)
                .Append(" FXAA=").Append(fxaaSettings.Enabled)
                .Append(" FXAASubpixel=").Append(fxaaSettings.Subpixel.ToString("0.###"))
                .Append(" FXAAEdgeThreshold=").Append(fxaaSettings.EdgeThreshold.ToString("0.###"))
                .Append(" FXAAEdgeThresholdMin=").Append(fxaaSettings.EdgeThresholdMin.ToString("0.###"))
                .Append(" RCAS=").Append(rcasSettings.Enabled)
                .Append(" RCASSharpness=").Append(rcasSettings.Sharpness.ToString("0.###"))
                .Append(" Bloom=").Append(bloomSettings.Enabled)
                .Append(" BloomMips=").Append(bloomMipCount)
                .Append(" BloomMipSizes=").Append(bloomMipSizes)
                .Append(" BloomMipPixels=").Append(bloomMipPixels)
                .Append(" BloomStages=").Append(bloomStages)
                .Append(" BloomQuality=").Append(bloomSettings.Quality)
                .Append(" BloomMaxMips=").Append(bloomSettings.MaxMipCount)
                .Append(" BloomThreshold=").Append(bloomSettings.Threshold)
                .Append(" BloomSoftKnee=").Append(bloomSettings.SoftKnee)
                .Append(" BloomPrefilterPostExposure=").Append(bloomPrefilterPostExposure.ToString("0.###"))
                .Append(" BloomPrefilterKnee=").Append(bloomPrefilterKnee.ToString("0.###"))
                .Append(" BloomPrefilterSourceThreshold=").Append(bloomPrefilterSourceThreshold)
                .Append(" BloomPrefilterBypassThreshold=").Append(bloomPrefilterBypassThreshold)
                .Append(" BloomPrefilterFireflyClamp=").Append(BloomPrefilterFireflyClamp.ToString("0.###"))
                .Append(" BloomIntensity=").Append(bloomSettings.Intensity)
                .Append(" BloomScatter=").Append(bloomSettings.Scatter)
                .Append(" BloomSizeScale=").Append(bloomSettings.SizeScale)
                .Append(" BloomAlpha=").Append(bloomSettings.BloomAlphaChannel)
                .Append(" BloomAlphaRT=").Append(preserveBloomAlpha)
                .Append(" BloomAlphaReason=").Append(bloomAlphaReason)
                .Append(" BloomRTFormat=").Append(bloomRenderTextureFormat)
                .Append(" BloomRTFormatReason=").Append(bloomRenderTextureFormatReason)
                .Append(" BloomDebug=").Append(bloomDebugView)
                .Append(" BloomDebugSource=").Append(bloomDebugRequested ? "ShadingDebug" : "Volume")
                .Append(" BloomDebugTarget=").Append(bloomDebugTarget)
                .Append(" TAA=").Append(temporalAAEnabled)
                .Append(" TAAReason=").Append(temporalAADisabledReason)
                .Append(" TAASource=").Append(temporalAASource)
                .Append(" TAAVolume=").Append(temporalAAVolumeState)
                .Append(" TAAHistoryValid=").Append(temporalAA != null && temporalAA.HistoryValid)
                .Append(" TAAHistoryAge=").Append(temporalHistory.HistoryAge)
                .Append(" TAAHistoryReason=").Append(temporalHistory.LastInvalidationReason)
                .Append(" TAAVelocity=").Append(temporalAA != null ? temporalAA.VelocityMode.ToString() : BurtTemporalAAVelocityMode.Disabled.ToString())
                .Append(" TAAObjectMVPass=").Append(temporalAA != null && temporalAA.ObjectMotionVectorPassDrawn)
                .Append(" TAAJitterScale=").Append(temporalAASettings.JitterScale.ToString("0.###"))
                .Append(" TAAUpscaleSharpness=").Append(temporalAASettings.Sharpness.ToString("0.###"))
                .Append(" TAAUntrustedMVScale=").Append(temporalAASettings.UntrustedMotionFeedbackScale.ToString("0.###"))
                .Append(" TAAMotionEdge=").Append(temporalAASettings.MotionEdgeResponsiveStrength.ToString("0.###"))
                .Append(" TAADepthEdge=").Append(temporalAASettings.DepthEdgeResponsiveStrength.ToString("0.###"))
                .Append(" TAAUpscaleFactor=").Append(temporalAASettings.UpscaleFactor.ToString("0.###"))
                .Append(" TAADebugMode=").Append(temporalAADebugRequested ? BurtShadingDebugSettings.Mode.ToString() : "Disabled")
                .Append(" TAADebugActive=").Append(temporalAADebugRequested && temporalAAEnabled)
                .Append(" TAAFinalBlitYFlip=").Append(temporalAAFinalBlitYFlip.ToString("0.###"))
                .Append(" TAADebugYFlip=").Append(temporalAADebugYFlip.ToString("0.###"))
                .Append(" TAADebugCopy=").Append(temporalAADebugRequested && temporalAAEnabled ? "TwoStagePostProcessColor" : "Disabled")
                .Append(" TAAUVSpace=XRenderFullscreenPlatformSampleUv;HistoryDepthVelocityFeedbackSameOrientation;FinalCameraColorTemporalAACopy;FinalBlitHandlesDisplayFlip;XRenderVelocityCurrentMinusPreviousHistoryUvMinusVelocity")
                .Append(" TAAFilter=").Append(temporalAAEnabled ? "Current3x3ProjectionJitterFilter" : "Disabled")
                .Append(" TAANote=").Append(temporalAAEnabled ? "XRenderTSRAccumulationParity;ResolveXRenderCompute;ComputeDilateDecimate;XRenderDepthPixelRadiusDeviceZError;StencilMaskComputeFallback;FragmentDebugFallback;ColorDepthHistoryOnly;VelocityCurrentMinusPrevious;OpaqueSurfaceMotionOwnershipMatchesXRender;ProjectionJitterTranslateMatrix;RestoreJitteredMatricesBeforeDraw;StaticVelocitySubtractCurrentJitter;ClipToPrevClipCameraRelative;XRenderPerPixelCameraMotionClipReprojection;FurExplicitCameraMotionMode;XRenderPerAxisVelocityThreshold;ResolveKeeps3x3ProjectionJitterFilter;XRenderSingleRejectionBlendFactor;XRenderCatmullRom9TapCompute;XRenderCatmullRomClamp65472;XRenderHalfMinPerceptualInverse;SubmitBeforeProjectionRestore;FinalHistoryAvailabilityNoSurfaceGate;HistoryLayout39;ParallaxCoverageDepthGate;XRenderSigmaClamp15;XRenderAntiFlickerSDBoostDisabled;NoResolveSharpen;UIntPrevUseCountUAV;ScalarParallaxRejection;MaterialMotionVectorsPass;TAAExplicitGBuffer0And2Bind;GrassFoliageVertexAnimationMV;ObjectMotionInstancing;ObjectVelocityPerAxisThreshold;ObjectMotionSourceOverridesCameraAtZeroVelocity;RealStencilSubElement;TAAStencilMaskBit8FromRealStencil;TAAStencilMaskRawVelocityLowResFallback;TAAStencilMaskRealStencilOR;XRenderResponsiveStencilBit16Only;XRenderTransparentResponsiveWriteMask24;XRenderResponsiveBlend025;XRenderMotionVectorStencilProperties;XRenderMotionVectorZTestEqual;XRenderObjectMotionOpaqueQueue;ObjectVelocityRequiresStencilBit8;TAAUObjectMotionLowRes;TAAUResolveUpscalePass;TAAUDebugClosure489_491;TAAStencilMaskDebug495;HistoryValidReason;XRenderPointCurrentLoad;XRenderFinalAlpha;XRenderFloat2DilatedVelocity;XRenderSeparateClosestDepth;ClosestDepthScalarRT;TAADebugTwoStageCopy" : "Disabled");
            Debug.Log(logBuilder.ToString()); // 输出后处理执行摘要，说明当前模式、曝光倍率、颜色调整、Bloom 和 TAA 状态。
        }

        private static bool IsPostProcessEnabled(BurtRenderPipelineAsset asset) // 定义判断资产是否允许后处理运行的统一辅助函数。
        {
            if (IsPostProcessSuppressedByShadingDebug())
            {
                return false;
            }

            if (asset == null) // 如果资产为空，说明没有后处理总开关来源。
            {
                return false; // 返回 false，避免异常路径改变画面。
            }

            var settings = asset.PostProcessSettings; // 读取资产上的后处理框架设置。

            return settings != null && settings.EnablePostProcessing; // 只有设置存在且总开关打开时，Volume 效果才允许生效。
        }

        private static bool IsPreviewOrReflectionRequest(BurtRenderRequest request) // 判断当前 request 是否来自 Unity 编辑器预览或 ReflectionProbe 捕获。
        {
            return request != null && (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection); // 这些 request 都不应继承场景后处理。
        }

        private static bool HasActiveTonemappingVolume() // 定义判断当前 VolumeStack 是否存在有效 Tonemapping 的辅助函数。
        {
            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 中读取 BurtRP Tonemapping 组件。

            return tonemapping != null && tonemapping.IsEnabled(); // 只有组件存在、激活且模式不是 None 时，才认为 Tonemapping 需要运行。
        }

        private static bool HasActiveExposureVolume()
        {
            var exposure = GetExposureVolumeComponent();

            return exposure != null && exposure.IsEnabled();
        }

        private static bool HasActiveColorAdjustmentsVolume() // 定义判断当前 VolumeStack 是否存在有效 Color Adjustments 的辅助函数。
        {
            var colorAdjustments = GetColorAdjustmentsVolumeComponent(); // 从当前 VolumeStack 中读取 BurtRP Color Adjustments 组件。

            return colorAdjustments != null && colorAdjustments.IsEnabled(); // 只有组件存在、激活且参数被覆盖或偏离中性值时，才认为颜色调整需要运行。
        }

        private static bool HasActiveVignetteVolume()
        {
            var vignette = GetVignetteVolumeComponent();

            return vignette != null && vignette.IsEnabled();
        }

        private static bool HasActiveLensFlareVolume()
        {
            var lensFlare = GetLensFlareVolumeComponent();

            return lensFlare != null && lensFlare.IsEnabled();
        }

        private static bool HasActiveDiaphragmDepthOfFieldVolume()
        {
            var depthOfField = GetDiaphragmDepthOfFieldVolumeComponent();

            return depthOfField != null && depthOfField.IsEnabled();
        }

        private static bool HasActiveColorGradingVolume()
        {
            var colorGrading = GetColorGradingVolumeComponent();

            return colorGrading != null && colorGrading.IsEnabled();
        }

        private static bool HasActiveRCASVolume()
        {
            var rcas = GetRCASVolumeComponent();

            return rcas != null && rcas.IsEnabled();
        }

        private static bool HasActiveFastApproximateAAVolume()
        {
            var fxaa = GetFastApproximateAAVolumeComponent();

            return fxaa != null && fxaa.IsEnabled();
        }

        private static bool HasActiveSubpixelMorphologicalAAVolume()
        {
            var smaa = GetSubpixelMorphologicalAAVolumeComponent();

            return smaa != null && smaa.IsEnabled();
        }

        private static bool HasActiveBloomVolume() // 定义判断当前 VolumeStack 是否存在有效 Bloom 的辅助函数。
        {
            var bloom = GetBloomVolumeComponent(); // 从当前 VolumeStack 中读取 BurtRP Bloom 组件。

            return bloom != null && bloom.IsEnabled(); // 只有组件存在、激活且强度大于 0 时，才认为 Bloom 需要运行。
        }

        internal static bool HasActiveTemporalAAVolume()
        {
            var temporalAA = GetTemporalAAVolumeComponent();
            return temporalAA != null && temporalAA.IsEnabled();
        }

        internal static bool HasActiveTemporalAASource(BurtRenderRequest request)
        {
            if (IsTemporalAAExplicitlyDisabledByVolume())
            {
                return false;
            }

            return HasActiveTemporalAAVolume() || HasTemporalAAEnabledByCameraData(request);
        }

        internal static bool HasTemporalAAEnabledByCameraData(BurtRenderRequest request)
        {
            var cameraData = request != null ? request.CameraData : null;
            return cameraData != null && cameraData.AntialiasingMode == BurtCameraAntialiasingMode.TemporalAntialiasing;
        }

        internal static string ResolveTemporalAASourceLabel(BurtRenderRequest request)
        {
            if (IsTemporalAAExplicitlyDisabledByVolume())
            {
                return HasTemporalAAEnabledByCameraData(request) ? "DisabledByVolumeOverride(CameraDataSuppressed)" : "DisabledByVolumeOverride";
            }

            var volumeEnabled = HasActiveTemporalAAVolume();
            var cameraDataEnabled = HasTemporalAAEnabledByCameraData(request);
            if (volumeEnabled && cameraDataEnabled)
            {
                return "CameraData+Volume";
            }

            if (cameraDataEnabled)
            {
                return "CameraData";
            }

            return volumeEnabled ? "Volume" : "Disabled";
        }

        internal static string ResolveTemporalAAVolumeStateLabel()
        {
            var temporalAA = GetTemporalAAVolumeComponent();
            if (temporalAA == null)
            {
                return "Missing";
            }

            return "Active=" + temporalAA.active +
                ",EnabledOverride=" + temporalAA.enabled.overrideState +
                ",EnabledValue=" + temporalAA.enabled.value +
                ",JitterScale=" + temporalAA.jitterScale.value.ToString("0.###") +
                ",IsEnabled=" + temporalAA.IsEnabled() +
                ",ExplicitDisable=" + IsTemporalAAExplicitlyDisabledByVolume(temporalAA);
        }

        internal static bool IsTemporalAAExplicitlyDisabledByVolume()
        {
            return IsTemporalAAExplicitlyDisabledByVolume(GetTemporalAAVolumeComponent());
        }

        private static bool IsTemporalAAExplicitlyDisabledByVolume(TemporalAAVolumeComponent temporalAA)
        {
            return temporalAA != null && temporalAA.active && temporalAA.enabled.overrideState && !temporalAA.enabled.value;
        }

        private static TonemappingVolumeComponent GetTonemappingVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Tonemapping 组件的辅助函数。
        {
            var volumeManager = VolumeManager.instance; // 取得 Unity 当前全局 VolumeManager 实例。

            if (volumeManager == null) // 理论上 VolumeManager 是单例，但这里仍然做保护，避免异常域重载阶段出错。
            {
                return null; // 返回空组件，调用方会按无 Tonemapping 处理。
            }

            var stack = volumeManager.stack; // 读取当前已经由相机刷新过的 VolumeStack。

            if (stack == null) // 如果 VolumeStack 为空，说明 Volume 系统还没有准备好。
            {
                return null; // 返回空组件，保证后处理回退到 No-op 或关闭状态。
            }

            return stack.GetComponent<TonemappingVolumeComponent>(); // 返回 BurtRP Tonemapping 组件，未添加时 Unity 会返回默认组件或空值。
        }

        private static ExposureVolumeComponent GetExposureVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;
            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<ExposureVolumeComponent>();
        }

        private static ColorAdjustmentsVolumeComponent GetColorAdjustmentsVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Color Adjustments 组件的辅助函数。
        {
            var volumeManager = VolumeManager.instance; // 取得 Unity 当前全局 VolumeManager 实例。

            if (volumeManager == null) // 理论上 VolumeManager 是单例，但域重载阶段仍可能为空。
            {
                return null; // 返回空组件，调用方会按无 Color Adjustments 处理。
            }

            var stack = volumeManager.stack; // 读取当前已经由相机刷新过的 VolumeStack。

            if (stack == null) // 如果 VolumeStack 为空，说明 Volume 系统还没有准备好。
            {
                return null; // 返回空组件，保证后处理回退到无颜色调整状态。
            }

            return stack.GetComponent<ColorAdjustmentsVolumeComponent>(); // 返回 BurtRP Color Adjustments 组件，未添加时 Unity 会返回默认组件或空值。
        }

        private static VignetteVolumeComponent GetVignetteVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;

            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;

            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<VignetteVolumeComponent>();
        }

        private static LensFlareVolumeComponent GetLensFlareVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;

            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;

            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<LensFlareVolumeComponent>();
        }

        private static DiaphragmDepthOfFieldVolumeComponent GetDiaphragmDepthOfFieldVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;

            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;

            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<DiaphragmDepthOfFieldVolumeComponent>();
        }

        private static ColorGradingVolumeComponent GetColorGradingVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;
            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<ColorGradingVolumeComponent>();
        }

        private static RCASVolumeComponent GetRCASVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;
            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<RCASVolumeComponent>();
        }

        private static FastApproximateAAVolumeComponent GetFastApproximateAAVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;
            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<FastApproximateAAVolumeComponent>();
        }

        private static SubpixelMorphologicalAAVolumeComponent GetSubpixelMorphologicalAAVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;
            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<SubpixelMorphologicalAAVolumeComponent>();
        }

        private static BloomVolumeComponent GetBloomVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Bloom 组件的辅助函数。
        {
            var volumeManager = VolumeManager.instance; // 取得 Unity 当前全局 VolumeManager 实例。

            if (volumeManager == null) // 理论上 VolumeManager 是单例，但域重载阶段仍可能为空。
            {
                return null; // 返回空组件，调用方会按无 Bloom 处理。
            }

            var stack = volumeManager.stack; // 读取当前已经由相机刷新过的 VolumeStack。

            if (stack == null) // 如果 VolumeStack 为空，说明 Volume 系统还没有准备好。
            {
                return null; // 返回空组件，保证后处理回退到无 Bloom 状态。
            }

            return stack.GetComponent<BloomVolumeComponent>(); // 返回 BurtRP Bloom 组件，未添加时 Unity 会返回默认组件或空值。
        }

        private static TemporalAAVolumeComponent GetTemporalAAVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            if (volumeManager == null)
            {
                return null;
            }

            var stack = volumeManager.stack;
            if (stack == null)
            {
                return null;
            }

            return stack.GetComponent<TemporalAAVolumeComponent>();
        }
    }

    internal readonly struct PreExposureState
    {
        public static readonly PreExposureState Default = new PreExposureState(1f, 1f);

        public float PreExposure { get; }
        public float InvPreExposure { get; }
        public float ResidualPostExposure { get; }
        public float PostExposure { get; }
        public float ExposureRatio { get; }
        public bool Enabled { get; }

        public PreExposureState(float preExposure, float postExposure)
        {
            PreExposure = PreExposureUtility.SanitizeExposure(preExposure, 1f);
            InvPreExposure = 1f / PreExposure;
            PostExposure = PreExposureUtility.SanitizeExposure(postExposure, 1f);
            ResidualPostExposure = PostExposure / PreExposure;
            ExposureRatio = 1f;
            Enabled = Mathf.Abs(PreExposure - 1f) > 0.0001f;
        }
    }

    internal static class PreExposureUtility
    {
        public const float MinExposure = 0.0001f;
        public const float MaxExposure = 65504f;
        public const float TemporalAAInvalidationEVThreshold = 2.0f;

        public static readonly int PreExposureId = Shader.PropertyToID("_BurtPreExposure");
        public static readonly int InvPreExposureId = Shader.PropertyToID("_BurtInvPreExposure");
        public static readonly int PreExposureParamsId = Shader.PropertyToID("_BurtPreExposureParams");

        private static readonly System.Collections.Generic.Dictionary<int, float> CameraPreExposureHistory = new System.Collections.Generic.Dictionary<int, float>();

        public static PreExposureState ResolveForFrame(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(request, asset))
            {
                return PreExposureState.Default;
            }

            var exposureSettings = PostProcessUtility.ResolvePhysicalExposureSettings(request, asset);
            var postExposure = SanitizeExposure(exposureSettings.Multiplier, 1f);
            var preExposure = GpuExposureUtility.ResolvePreExposure(request.Camera, postExposure);
            return new PreExposureState(preExposure, postExposure);
        }

        public static PreExposureState ResolveForFrame(PhysicalExposureSettings exposureSettings)
        {
            var postExposure = SanitizeExposure(exposureSettings.Multiplier, 1f);
            return new PreExposureState(postExposure, postExposure);
        }

        public static float ResolveResidualPostExposure(PhysicalExposureSettings exposureSettings)
        {
            return ResolveForFrame(exposureSettings).ResidualPostExposure;
        }

        public static float ResolveResidualPostExposure(PhysicalExposureSettings exposureSettings, PreExposureState preExposureState)
        {
            return SanitizeExposure(exposureSettings.Multiplier, 1f) * preExposureState.InvPreExposure;
        }

        public static bool ShouldInvalidateTemporalAAHistory(Camera camera, PreExposureState state, out string reason)
        {
            reason = null;
            if (camera == null)
            {
                return false;
            }

            var cameraId = camera.GetInstanceID();
            var current = SanitizeExposure(state.PreExposure, 1f);
            if (!CameraPreExposureHistory.TryGetValue(cameraId, out var previous))
            {
                CameraPreExposureHistory[cameraId] = current;
                return false;
            }

            CameraPreExposureHistory[cameraId] = current;
            previous = SanitizeExposure(previous, 1f);
            var ratio = current / previous;
            if (Mathf.Abs(Mathf.Log(ratio, 2f)) <= TemporalAAInvalidationEVThreshold)
            {
                return false;
            }

            reason = "PreExposureChanged";
            return true;
        }

        public static void UploadGlobals(UnityEngine.Rendering.CommandBuffer cmd, PreExposureState state)
        {
            if (cmd == null)
            {
                return;
            }

            cmd.SetGlobalFloat(PreExposureId, state.PreExposure);
            cmd.SetGlobalFloat(InvPreExposureId, state.InvPreExposure);
            cmd.SetGlobalVector(PreExposureParamsId, new Vector4(state.PreExposure, state.InvPreExposure, state.PostExposure, state.ResidualPostExposure));
        }

        public static float SanitizeExposure(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = fallback;
            }

            return Mathf.Clamp(value, MinExposure, MaxExposure);
        }
    }
}
