using UnityEngine; // 引入 UnityEngine 命名空间，用来访问 Camera、LayerMask、Mathf 和 Debug。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来访问 VolumeManager 和 VolumeStack。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理工具可以被 RenderGraph 和 Pass 共享。
{
    internal static class BurtPostProcessUtility // 定义后处理工具类，用来集中判断后处理框架是否应该运行。
    {
        public const float BloomPrefilterFireflyClamp = 64f;
        public const int BloomGaussianMaxSamples = 64;
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
            return ResolveBloomShadingDebugView() != BurtBloomDebugView.Disabled;
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
                default:
                    return 0;
            }
        }

        public static BurtBloomDebugView ResolveBloomDebugView(BurtBloomSettings settings)
        {
            var shadingDebugView = ResolveBloomShadingDebugView();
            return shadingDebugView != BurtBloomDebugView.Disabled ? shadingDebugView : settings.DebugView;
        }

        public static BurtBloomDebugView ResolveBloomShadingDebugView()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.BloomPrefilter:
                    return BurtBloomDebugView.Prefilter;
                case BurtShadingDebugMode.BloomFinalBloom:
                    return BurtBloomDebugView.FinalBloom;
                case BurtShadingDebugMode.BloomMip1:
                    return BurtBloomDebugView.Mip1;
                case BurtShadingDebugMode.BloomMip2:
                    return BurtBloomDebugView.Mip2;
                case BurtShadingDebugMode.BloomMip3:
                    return BurtBloomDebugView.Mip3;
                case BurtShadingDebugMode.BloomMip4:
                    return BurtBloomDebugView.Mip4;
                case BurtShadingDebugMode.BloomMip5:
                    return BurtBloomDebugView.Mip5;
                case BurtShadingDebugMode.BloomAlpha:
                    return BurtBloomDebugView.Alpha;
                case BurtShadingDebugMode.BloomThresholdMask:
                    return BurtBloomDebugView.ThresholdMask;
                default:
                    return BurtBloomDebugView.Disabled;
            }
        }

        public static bool ShouldPreserveBloomAlpha(BurtBloomSettings settings, BurtBloomDebugView debugView)
        {
            return settings.BloomAlphaChannel || debugView == BurtBloomDebugView.Alpha;
        }

        public static string ResolveBloomAlphaReason(BurtBloomSettings settings, BurtBloomDebugView debugView)
        {
            if (settings.BloomAlphaChannel && debugView == BurtBloomDebugView.Alpha)
            {
                return "VolumeAndAlphaDebug";
            }

            if (debugView == BurtBloomDebugView.Alpha)
            {
                return "AlphaDebug";
            }

            return settings.BloomAlphaChannel ? "Volume" : "Disabled";
        }

        public static RenderTextureDescriptor CreateBloomRenderTextureDescriptor(Camera camera, int width, int height, BurtBloomSettings settings, BurtBloomDebugView debugView)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            descriptor.width = Mathf.Max(1, width);
            descriptor.height = Mathf.Max(1, height);
            descriptor.colorFormat = ResolveBloomRenderTextureFormat(camera, settings, debugView);

            return descriptor;
        }

        public static int ResolveBloomSourceWidth(Camera camera)
        {
            if (camera == null)
            {
                return 1;
            }

            return Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.width : camera.pixelWidth);
        }

        public static int ResolveBloomSourceHeight(Camera camera)
        {
            if (camera == null)
            {
                return 1;
            }

            return Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.height : camera.pixelHeight);
        }

        public static int GetBloomMipWidth(Camera camera, int mipIndex)
        {
            var width = Mathf.Max(1, ResolveBloomSourceWidth(camera) / 2);
            for (var i = 0; i < mipIndex; i++)
            {
                width = Mathf.Max(1, width / 2);
            }

            return Mathf.Max(1, width);
        }

        public static int GetBloomMipHeight(Camera camera, int mipIndex)
        {
            var height = Mathf.Max(1, ResolveBloomSourceHeight(camera) / 2);
            for (var i = 0; i < mipIndex; i++)
            {
                height = Mathf.Max(1, height / 2);
            }

            return Mathf.Max(1, height);
        }

        public static long CalculateBloomMipPixelCount(Camera camera, int mipCount)
        {
            var pixelCount = 0L;
            for (var i = 0; i < mipCount; i++)
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
            for (var i = 0; i < mipCount; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(i).Append(':').Append(GetBloomMipWidth(camera, i)).Append('x').Append(GetBloomMipHeight(camera, i));
            }

            return builder.ToString();
        }

        public static int ResolveBloomDebugMipIndex(BurtBloomDebugView debugView, int mipCount)
        {
            if (mipCount <= 0 || debugView == BurtBloomDebugView.Disabled)
            {
                return -1;
            }

            if (debugView >= BurtBloomDebugView.Mip1 && debugView <= BurtBloomDebugView.Mip5)
            {
                return Mathf.Clamp((int)debugView - (int)BurtBloomDebugView.Mip1 + 1, 0, Mathf.Max(0, mipCount - 1));
            }

            return 0;
        }

        public static string FormatBloomDebugTarget(Camera camera, BurtBloomDebugView debugView, int mipCount)
        {
            if (debugView == BurtBloomDebugView.ThresholdMask)
            {
                return "ThresholdMask:CameraColor:" + ResolveBloomSourceWidth(camera) + "x" + ResolveBloomSourceHeight(camera);
            }

            var mipIndex = ResolveBloomDebugMipIndex(debugView, mipCount);
            if (mipIndex < 0)
            {
                return debugView == BurtBloomDebugView.Disabled ? "<none>" : "<unavailable>";
            }

            var prefix = debugView == BurtBloomDebugView.Prefilter
                ? "PrefilterSnapshot"
                : debugView == BurtBloomDebugView.Alpha
                    ? "Alpha"
                    : debugView == BurtBloomDebugView.ThresholdMask
                        ? "ThresholdMask"
                        : debugView == BurtBloomDebugView.FinalBloom
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

        public static float ResolveBloomStageSize(BurtBloomSettings settings, int stageIndexFromSmallest)
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

        public static Color ResolveBloomStageTint(BurtBloomSettings settings, int stageIndexFromSmallest)
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

        public static Color CalculateBloomXRenderStageTint(BurtBloomSettings settings, int stageIndexFromSmallest)
        {
            return ResolveBloomStageTint(settings, stageIndexFromSmallest) * (Mathf.Max(0f, settings.Intensity) / BloomXRenderStageCountMax);
        }

        public static float CalculateBloomBlurKernelSizePercent(BurtBloomSettings settings, int stageIndexFromSmallest)
        {
            var scatter = Mathf.Clamp01(settings.Scatter);
            return ResolveBloomStageSize(settings, stageIndexFromSmallest) * Mathf.Max(0f, settings.SizeScale) * Mathf.Lerp(0.5f, 4f, scatter);
        }

        public static float CalculateBloomBlurRadius(BurtBloomSettings settings, int sourceWidth, int stageIndexFromSmallest)
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

        public static string FormatBloomStageDiagnostics(Camera camera, BurtBloomSettings settings, int mipCount)
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

                var mipIndex = mipCount - 1 - stageIndex;
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

        public static bool ShouldBypassBloomPrefilterThreshold(BurtBloomSettings settings)
        {
            return settings.Threshold <= -1f;
        }

        public static float ResolveBloomPrefilterPostExposure(float postExposureMultiplier)
        {
            return Mathf.Max(0f, postExposureMultiplier);
        }

        public static float ResolveBloomPrefilterKnee(BurtBloomSettings settings)
        {
            return Mathf.Max(settings.Threshold * settings.SoftKnee, 0.0001f);
        }

        public static float ResolveBloomPrefilterSourceThreshold(BurtBloomSettings settings, float postExposureMultiplier)
        {
            if (ShouldBypassBloomPrefilterThreshold(settings))
            {
                return 0f;
            }

            var prefilterExposure = ResolveBloomPrefilterPostExposure(postExposureMultiplier);
            return prefilterExposure > 0.0001f ? settings.Threshold / prefilterExposure : float.PositiveInfinity;
        }

        public static string FormatBloomPrefilterSourceThreshold(BurtBloomSettings settings, float postExposureMultiplier)
        {
            var sourceThreshold = ResolveBloomPrefilterSourceThreshold(settings, postExposureMultiplier);
            return float.IsInfinity(sourceThreshold) ? "Infinity" : sourceThreshold.ToString("0.###");
        }

        public static RenderTextureFormat ResolveBloomRenderTextureFormat(Camera camera, BurtBloomSettings settings, BurtBloomDebugView debugView)
        {
            var fallbackFormat = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera).colorFormat;
            if (ShouldPreserveBloomAlpha(settings, debugView))
            {
                return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf) ? RenderTextureFormat.ARGBHalf : fallbackFormat;
            }

            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float) ? RenderTextureFormat.RGB111110Float : fallbackFormat;
        }

        public static string ResolveBloomRenderTextureFormatReason(Camera camera, BurtBloomSettings settings, BurtBloomDebugView debugView)
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

            return temporalAADebugRequested || bloomDebugRequested || autoExposureDebugRequested || HasActiveExposureVolume() || HasActiveTonemappingVolume() || HasActiveColorAdjustmentsVolume() || HasActiveBloomVolume() || HasActiveTemporalAAVolume(); // Only real post effects allocate and run the framework; pure No-op is skipped.
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

        public static BurtTonemappingMode ResolveTonemappingMode(BurtRenderPipelineAsset asset) // 定义安全解析 Tonemapping 模式的函数，避免 Pass 直接处理 VolumeStack 细节。
        {
            if (!IsPostProcessEnabled(asset)) // 如果管线资产没有打开后处理框架，就不允许 Volume Tonemapping 改变画面。
            {
                return BurtTonemappingMode.None; // 返回 None，shader 会走原样输出。
            }

            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Tonemapping 组件。

            if (tonemapping == null) // 如果当前 VolumeStack 没有 Tonemapping 组件，就没有正式 Tonemapping 效果。
            {
                return BurtTonemappingMode.None; // 返回 None，保持 No-op 或无后处理状态。
            }

            if (!tonemapping.IsEnabled()) // 如果组件未激活或模式为 None，就不执行 Tonemapping。
            {
                return BurtTonemappingMode.None; // 返回 None，避免 Volume 默认值改变画面。
            }

            return tonemapping.mode.value; // 返回当前 Volume 混合后的 Tonemapping 模式。
        }

        public static float ResolvePostExposureMultiplier(BurtRenderPipelineAsset asset) // 定义把 Volume EV 曝光转换为线性倍率的函数。
        {
            return ResolvePhysicalExposureSettings(asset).Multiplier;
        }

        public static BurtPhysicalExposureSettings ResolvePhysicalExposureSettings(BurtRenderPipelineAsset asset)
        {
            return ResolvePhysicalExposureSettings(null, asset);
        }

        public static BurtPhysicalExposureSettings ResolvePhysicalExposureSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolvePhysicalExposureSettings(request, asset, false, 0f);
        }

        public static BurtPhysicalExposureSettings ResolvePhysicalExposureSettingsForFrame(BurtRenderRequest request, BurtRenderPipelineAsset asset, float deltaTime)
        {
            return ResolvePhysicalExposureSettings(request, asset, true, deltaTime);
        }

        private static BurtPhysicalExposureSettings ResolvePhysicalExposureSettings(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool updateAutoExposure, float deltaTime)
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，曝光参数不应该影响画面。
            {
                return BurtPhysicalExposureSettings.Default;
            }

            var exposure = GetExposureVolumeComponent();

            if (exposure == null)
            {
                return BurtPhysicalExposureSettings.Default;
            }

            if (!exposure.IsEnabled())
            {
                return BurtPhysicalExposureSettings.Default;
            }

            if (exposure.mode.value == BurtExposureMode.Automatic || exposure.mode.value == BurtExposureMode.AutomaticHistogram)
            {
                var camera = request != null ? request.Camera : null;
                return updateAutoExposure
                    ? BurtAutoExposureUtility.UpdateAfterCapture(camera, exposure, deltaTime)
                    : BurtAutoExposureUtility.ResolveSettings(camera, exposure);
            }

            return new BurtPhysicalExposureSettings(
                exposure.mode.value,
                exposure.manualEV100.value,
                exposure.iso.value,
                exposure.shutterTime.value,
                exposure.aperture.value,
                exposure.calibration.value,
                exposure.compensation.value);
        }

        public static BurtTonemappingFilmSettings ResolveTonemappingFilmSettings(BurtRenderPipelineAsset asset) // 定义解析 UE/XRender Filmic 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，Film 参数不应该影响任何全屏拷贝。
            {
                return BurtTonemappingFilmSettings.Default; // 返回默认参数，保证 shader 即使被调用也处于稳定状态。
            }

            var tonemapping = GetTonemappingVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Tonemapping 组件。

            if (tonemapping == null) // 如果当前 VolumeStack 没有 Tonemapping 组件，就没有可覆盖的 Film 参数。
            {
                return BurtTonemappingFilmSettings.Default; // 返回默认参数，对齐 XRender/UE 的基础外观。
            }

            if (!tonemapping.IsEnabled()) // 如果 Tonemapping 组件未启用，Film 参数不应该参与 No-op Copy。
            {
                return BurtTonemappingFilmSettings.Default; // 返回默认参数，避免关闭模式下上传无意义的自定义值。
            }

            return new BurtTonemappingFilmSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供 Pass 一次性上传给 shader。
                tonemapping.filmSlope.value, // 读取 Film Slope。
                tonemapping.filmToe.value, // 读取 Film Toe。
                tonemapping.filmShoulder.value, // 读取 Film Shoulder。
                tonemapping.filmBlackClip.value, // 读取 Film Black Clip。
                tonemapping.filmWhiteClip.value, // 读取 Film White Clip。
                tonemapping.blueCorrection.value, // 读取 Blue Correction。
                tonemapping.expandGamut.value, // 读取 Expand Gamut。
                tonemapping.toneCurveAmount.value); // 读取 Tone Curve Amount。
        }

        public static BurtColorAdjustmentsSettings ResolveColorAdjustmentsSettings(BurtRenderPipelineAsset asset) // 定义解析 Color Adjustments 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，调色参数不应该影响任何全屏拷贝。
            {
                return BurtColorAdjustmentsSettings.Default; // 返回默认调色参数，保证 shader 处于稳定的中性状态。
            }

            var colorAdjustments = GetColorAdjustmentsVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Color Adjustments 组件。

            if (colorAdjustments == null) // 如果当前 VolumeStack 没有 Color Adjustments 组件，就没有可覆盖的调色参数。
            {
                return BurtColorAdjustmentsSettings.Default; // 返回默认调色参数，让后处理保持中性。
            }

            if (!colorAdjustments.IsEnabled()) // 如果 Color Adjustments 组件没有真正启用，就不应该上传非中性调色参数。
            {
                return BurtColorAdjustmentsSettings.Default; // 返回默认调色参数，避免 Volume 默认值改变画面。
            }

            return new BurtColorAdjustmentsSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供后处理 Pass 一次性上传。
                colorAdjustments.saturation.value, // 读取饱和度。
                colorAdjustments.contrast.value, // 读取对比度。
                colorAdjustments.gamma.value, // 读取 Gamma。
                colorAdjustments.colorFilter.value); // 读取颜色滤镜。
        }

        public static BurtBloomSettings ResolveBloomSettings(BurtRenderPipelineAsset asset) // 定义解析 Bloom 参数的函数，让 Pass 不直接访问 Volume 组件字段。
        {
            var bloomDebugRequested = IsBloomDebugRequested();
            if (!IsPostProcessEnabled(asset)) // 如果后处理框架关闭，Bloom 参数不应该影响画面。
            {
                return bloomDebugRequested ? CreateBloomDebugFallbackSettings() : BurtBloomSettings.Default; // Shading Debug 需要一条可见的 Bloom 链。
            }

            var bloom = GetBloomVolumeComponent(); // 从当前 VolumeStack 读取 BurtRP Bloom 组件。

            if (bloom == null) // 如果当前 VolumeStack 没有 Bloom 组件，就没有 Bloom 效果。
            {
                return bloomDebugRequested ? CreateBloomDebugFallbackSettings() : BurtBloomSettings.Default; // 没有 Volume 时仍允许 Bloom debug 生成中间纹理。
            }

            if (!bloom.IsEnabled()) // 如果 Bloom 组件未激活或强度为 0，就不执行 Bloom。
            {
                return bloomDebugRequested ? CreateBloomDebugSettings(bloom) : BurtBloomSettings.Default; // Debug 入口保留 Volume 参数，但强制执行 Bloom 链。
            }

            return new BurtBloomSettings( // 把当前 Volume 混合后的参数收拢成不可变设置，供 Pass 一次性使用。
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

        private static BurtBloomSettings CreateBloomDebugSettings(BurtBloomVolumeComponent bloom)
        {
            if (bloom == null)
            {
                return CreateBloomDebugFallbackSettings();
            }

            return new BurtBloomSettings(
                true,
                bloom.threshold.value,
                bloom.softKnee.value,
                Mathf.Max(bloom.intensity.value, BurtBloomSettings.DefaultIntensity),
                bloom.scatter.value,
                bloom.sizeScale.value,
                BurtBloomSettings.IsQualityEnabled(bloom.quality.value) ? bloom.quality.value : BurtBloomSettings.DefaultQuality,
                bloom.maxIterations.value,
                bloom.bloomAlphaChannel.value || ResolveBloomShadingDebugView() == BurtBloomDebugView.Alpha,
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

        private static BurtBloomSettings CreateBloomDebugFallbackSettings()
        {
            return new BurtBloomSettings(
                true,
                BurtBloomSettings.DefaultThreshold,
                BurtBloomSettings.DefaultSoftKnee,
                BurtBloomSettings.DefaultIntensity,
                BurtBloomSettings.DefaultScatter,
                BurtBloomSettings.DefaultSizeScale,
                BurtBloomSettings.DefaultQuality,
                BurtBloomSettings.DefaultMaxMipCount,
                ResolveBloomShadingDebugView() == BurtBloomDebugView.Alpha,
                ResolveBloomShadingDebugView(),
                BurtBloomSettings.DefaultFilter1Size,
                BurtBloomSettings.DefaultFilter2Size,
                BurtBloomSettings.DefaultFilter3Size,
                BurtBloomSettings.DefaultFilter4Size,
                BurtBloomSettings.DefaultFilter5Size,
                BurtBloomSettings.DefaultFilter6Size,
                BurtBloomSettings.DefaultFilter1Tint,
                BurtBloomSettings.DefaultFilter2Tint,
                BurtBloomSettings.DefaultFilter3Tint,
                BurtBloomSettings.DefaultFilter4Tint,
                BurtBloomSettings.DefaultFilter5Tint,
                BurtBloomSettings.DefaultFilter6Tint);
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
            if (temporalAA == null || !temporalAA.IsEnabled())
            {
                return BurtTemporalAASettings.Default;
            }

            return new BurtTemporalAASettings(
                true,
                temporalAA.feedback.value,
                temporalAA.jitterScale.value,
                temporalAA.clampStrength.value,
                temporalAA.sharpness.value,
                temporalAA.staticEdgeRelaxation.value,
                temporalAA.lumaRejectionStrength.value,
                temporalAA.clipRejectionStrength.value,
                temporalAA.depthRejectionStrength.value,
                temporalAA.motionRejectionStart.value,
                temporalAA.motionRejectionRange.value,
                temporalAA.historyConfidenceWeight.value,
                temporalAA.historyConfidenceBoost.value,
                temporalAA.confidenceGrowth.value,
                temporalAA.antiFlickering.value,
                temporalAA.motionVectorRejection.value,
                temporalAA.baseBlendFactor.value,
                temporalAA.responsiveRejectionStrength.value,
                temporalAA.untrustedMotionFeedbackScale.value,
                temporalAA.disocclusionFeedbackScale.value,
                temporalAA.motionEdgeResponsiveStrength.value,
                temporalAA.depthEdgeResponsiveStrength.value,
                temporalAA.historyClampTightness.value,
                temporalAA.depthWeightedFilterFloor.value);
        }

        public static int ResolveBloomMipCount(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 定义解析当前 request 实际 Bloom mip 数的函数。
        {
            if (!ShouldUseBloom(request, asset)) // 如果当前 request 不执行 Bloom，就不需要任何 mip。
            {
                return 0; // 返回 0 表示跳过 Bloom 链。
            }

            return CalculateBloomMipCount(request.Camera, ResolveBloomSettings(asset)); // 按相机尺寸和 Volume 参数计算实际 mip 数。
        }

        public static int CalculateBloomMipCount(Camera camera, BurtBloomSettings settings) // 根据相机尺寸和设置计算实际 Bloom mip 数。
        {
            if (!settings.Enabled) // Bloom 未启用时不申请任何临时 RT。
            {
                return 0; // 返回 0 表示跳过 Bloom 链。
            }

            if (camera == null) // 没有相机时无法确定尺寸。
            {
                return 0; // 返回 0，保持安全跳过。
            }

            var width = GetBloomMipWidth(camera, 0); // Bloom mip0 使用半分辨率，并保持最小 1。
            var height = GetBloomMipHeight(camera, 0); // Bloom mip0 使用半分辨率，并保持最小 1。
            var qualityMipCount = ResolveBloomQualityMipCount(settings.Quality); // 按 XRender Q1-Q5 映射到最多参与的 Bloom 阶段数。
            if (qualityMipCount <= 0) // 质量档关闭或无效时跳过 Bloom 链。
            {
                return 0; // 返回 0 表示不申请 Bloom mip。
            }

            var maxMipCount = Mathf.Min(Mathf.Clamp(settings.MaxMipCount, 1, 8), qualityMipCount); // 质量档决定目标阶段数，maxIterations 作为兼容上限继续生效。
            var count = 0; // 记录实际可用 mip 数。

            while (count < maxMipCount && width >= 1 && height >= 1) // 按分辨率逐级停止，避免申请 0 尺寸 RT。
            {
                count++; // 当前尺寸可用，纳入 mip 链。

                if (width == 1 && height == 1) // 已经到 1x1 时不能继续下降。
                {
                    break; // 停止 mip 计算。
                }

                width = Mathf.Max(1, width / 2); // 下一层宽度减半，并保持最小 1。
                height = Mathf.Max(1, height / 2); // 下一层高度减半，并保持最小 1。
            }

            return count; // 返回实际 mip 数。
        }

        private static int ResolveBloomQualityMipCount(BurtBloomQuality quality) // 对齐 XRender Bloom Q1-Q5 到 stage/mip 数的映射。
        {
            switch (quality)
            {
                case BurtBloomQuality.Q1:
                case BurtBloomQuality.Q2:
                    return 3; // XRender Q1/Q2 都使用 3 个 Bloom stage。
                case BurtBloomQuality.Q3:
                    return 4; // XRender Q3 使用 4 个 Bloom stage。
                case BurtBloomQuality.Q4:
                    return 5; // XRender Q4 使用 5 个 Bloom stage。
                case BurtBloomQuality.Q5:
                    return 6; // XRender Q5 使用 6 个 Bloom stage。
                default:
                    return 0; // 0 表示关闭 Bloom stage。
            }
        }

        public static void LogPostProcessExecuted( // 定义后处理执行日志，集中格式避免 Pass 内部堆字符串逻辑。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文，用来读取相机和资产设置。
            BurtTonemappingMode tonemappingMode, // 接收本次执行使用的 Tonemapping 模式。
            float postExposureMultiplier, // 接收本次执行使用的线性曝光倍率。
            BurtPreExposureState preExposureState,
            bool useColorAdjustments, // 接收本次执行是否启用了 Color Adjustments。
            BurtBloomSettings bloomSettings, // 接收本次执行使用的 Bloom 参数。
            int bloomMipCount) // 接收本次实际使用的 Bloom mip 数。
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
            var residualPostExposureMultiplier = BurtPreExposureUtility.SanitizeExposure(postExposureMultiplier, 1f) * preExposureState.InvPreExposure;
            var bloomPrefilterPostExposure = ResolveBloomPrefilterPostExposure(residualPostExposureMultiplier);
            var bloomPrefilterKnee = ResolveBloomPrefilterKnee(bloomSettings);
            var bloomPrefilterSourceThreshold = FormatBloomPrefilterSourceThreshold(bloomSettings, residualPostExposureMultiplier);
            var bloomPrefilterBypassThreshold = ShouldBypassBloomPrefilterThreshold(bloomSettings);
            var temporalHistory = BurtTemporalAAUtility.GetHistoryStatus(camera);
            var temporalAADisabledReason = BurtTemporalAAUtility.ResolveTemporalAADiagnosticDisabledReason(context.Request, context.Asset, context.RenderOptions);
            var temporalAAVolumeState = ResolveTemporalAAVolumeStateLabel();
            var temporalAAFinalBlitYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request);
            const float temporalAADebugYFlip = 0f;
            var exposure = ResolvePhysicalExposureSettings(context.Request, context.Asset);
            Debug.Log("[BurtRP][PostProcess] Executed. Camera=" + cameraName + " Tonemapping=" + tonemappingMode + " ExposureMode=" + exposure.Mode + " EV100=" + exposure.EV100.ToString("0.###") + " ISO=" + exposure.ISO.ToString("0.###") + " Shutter=" + exposure.ShutterTime.ToString("0.######") + " Aperture=" + exposure.Aperture.ToString("0.###") + " ExposureCalibration=" + exposure.Calibration.ToString("0.###") + " ExposureCompensationEV=" + exposure.Compensation.ToString("0.###") + " ExposureMul=" + postExposureMultiplier + " PreExposure=" + preExposureState.PreExposure.ToString("0.###") + " InvPreExposure=" + preExposureState.InvPreExposure.ToString("0.###") + " ResidualExposure=" + residualPostExposureMultiplier.ToString("0.###") + " PreExposureEnabled=" + preExposureState.Enabled + " AutoAvgLuma=" + exposure.AutoAverageLuminance.ToString("0.###") + " AutoAvgLogLum=" + exposure.AutoAverageLogLuminance.ToString("0.###") + " AutoTargetEV100=" + exposure.AutoTargetEV100.ToString("0.###") + " AutoMinMaxEV100=" + exposure.AutoMinEV100.ToString("0.###") + "/" + exposure.AutoMaxEV100.ToString("0.###") + " AutoSpeedUpDown=" + exposure.AutoSpeedUp.ToString("0.###") + "/" + exposure.AutoSpeedDown.ToString("0.###") + " AutoLowHighPercent=" + exposure.AutoLowPercent.ToString("0.###") + "/" + exposure.AutoHighPercent.ToString("0.###") + " AutoHistogramMinMaxEV100=" + exposure.AutoHistogramMinEV100.ToString("0.###") + "/" + exposure.AutoHistogramMaxEV100.ToString("0.###") + " AutoSample=" + exposure.AutoHasSample + " AutoSampleCount=" + exposure.AutoSampleCount + " AutoSampleAgeFrames=" + exposure.AutoSampleAgeFrames + " AutoSampleRejectedReason=" + exposure.AutoSampleRejectedReason + " AutoReadbackPending=" + exposure.AutoReadbackPending + " AutoReadbackAgeFrames=" + exposure.AutoReadbackAgeFrames + " ColorAdjustments=" + useColorAdjustments + " Bloom=" + bloomSettings.Enabled + " BloomMips=" + bloomMipCount + " BloomMipSizes=" + bloomMipSizes + " BloomMipPixels=" + bloomMipPixels + " BloomStages=" + bloomStages + " BloomQuality=" + bloomSettings.Quality + " BloomMaxMips=" + bloomSettings.MaxMipCount + " BloomThreshold=" + bloomSettings.Threshold + " BloomSoftKnee=" + bloomSettings.SoftKnee + " BloomPrefilterPostExposure=" + bloomPrefilterPostExposure.ToString("0.###") + " BloomPrefilterKnee=" + bloomPrefilterKnee.ToString("0.###") + " BloomPrefilterSourceThreshold=" + bloomPrefilterSourceThreshold + " BloomPrefilterBypassThreshold=" + bloomPrefilterBypassThreshold + " BloomPrefilterFireflyClamp=" + BloomPrefilterFireflyClamp.ToString("0.###") + " BloomIntensity=" + bloomSettings.Intensity + " BloomScatter=" + bloomSettings.Scatter + " BloomSizeScale=" + bloomSettings.SizeScale + " BloomAlpha=" + bloomSettings.BloomAlphaChannel + " BloomAlphaRT=" + preserveBloomAlpha + " BloomAlphaReason=" + bloomAlphaReason + " BloomRTFormat=" + bloomRenderTextureFormat + " BloomRTFormatReason=" + bloomRenderTextureFormatReason + " BloomDebug=" + bloomDebugView + " BloomDebugSource=" + (bloomDebugRequested ? "ShadingDebug" : "Volume") + " BloomDebugTarget=" + bloomDebugTarget + " TAA=" + (temporalAA != null && temporalAA.Enabled) + " TAAReason=" + temporalAADisabledReason + " TAAVolume=" + temporalAAVolumeState + " TAAHistoryValid=" + (temporalAA != null && temporalAA.HistoryValid) + " TAAHistoryAge=" + temporalHistory.HistoryAge + " TAAHistoryReason=" + temporalHistory.LastInvalidationReason + " TAAVelocity=" + (temporalAA != null ? temporalAA.VelocityMode.ToString() : BurtTemporalAAVelocityMode.Disabled.ToString()) + " TAAObjectMVPass=" + (temporalAA != null && temporalAA.ObjectMotionVectorPassDrawn) + " TAASharpness=" + (temporalAA != null ? temporalAA.Settings.Sharpness.ToString("0.###") : BurtTemporalAASettings.Default.Sharpness.ToString("0.###")) + " TAAStaticRelax=" + (temporalAA != null ? temporalAA.Settings.StaticEdgeRelaxation.ToString("0.###") : BurtTemporalAASettings.Default.StaticEdgeRelaxation.ToString("0.###")) + " TAALumaReject=" + (temporalAA != null ? temporalAA.Settings.LumaRejectionStrength.ToString("0.###") : BurtTemporalAASettings.Default.LumaRejectionStrength.ToString("0.###")) + " TAADepthReject=" + (temporalAA != null ? temporalAA.Settings.DepthRejectionStrength.ToString("0.###") : BurtTemporalAASettings.Default.DepthRejectionStrength.ToString("0.###")) + " TAAMotionReject=" + (temporalAA != null ? (temporalAA.Settings.MotionRejectionStart.ToString("0.###") + "/" + temporalAA.Settings.MotionRejectionRange.ToString("0.###")) : (BurtTemporalAASettings.Default.MotionRejectionStart.ToString("0.###") + "/" + BurtTemporalAASettings.Default.MotionRejectionRange.ToString("0.###"))) + " TAAAntiFlicker=" + (temporalAA != null ? temporalAA.Settings.AntiFlickering.ToString("0.###") : BurtTemporalAASettings.Default.AntiFlickering.ToString("0.###")) + " TAAMVReject=" + (temporalAA != null ? temporalAA.Settings.MotionVectorRejection.ToString("0.###") : BurtTemporalAASettings.Default.MotionVectorRejection.ToString("0.###")) + " TAAResponsive=" + (temporalAA != null ? temporalAA.Settings.ResponsiveRejectionStrength.ToString("0.###") : BurtTemporalAASettings.Default.ResponsiveRejectionStrength.ToString("0.###")) + " TAAUntrustedMVScale=" + (temporalAA != null ? temporalAA.Settings.UntrustedMotionFeedbackScale.ToString("0.###") : BurtTemporalAASettings.Default.UntrustedMotionFeedbackScale.ToString("0.###")) + " TAADisocclusionScale=" + (temporalAA != null ? temporalAA.Settings.DisocclusionFeedbackScale.ToString("0.###") : BurtTemporalAASettings.Default.DisocclusionFeedbackScale.ToString("0.###")) + " TAAMotionEdge=" + (temporalAA != null ? temporalAA.Settings.MotionEdgeResponsiveStrength.ToString("0.###") : BurtTemporalAASettings.Default.MotionEdgeResponsiveStrength.ToString("0.###")) + " TAADepthEdge=" + (temporalAA != null ? temporalAA.Settings.DepthEdgeResponsiveStrength.ToString("0.###") : BurtTemporalAASettings.Default.DepthEdgeResponsiveStrength.ToString("0.###")) + " TAAClampTight=" + (temporalAA != null ? temporalAA.Settings.HistoryClampTightness.ToString("0.###") : BurtTemporalAASettings.Default.HistoryClampTightness.ToString("0.###")) + " TAADepthFilterFloor=" + (temporalAA != null ? temporalAA.Settings.DepthWeightedFilterFloor.ToString("0.###") : BurtTemporalAASettings.Default.DepthWeightedFilterFloor.ToString("0.###")) + " TAADebugMode=" + (temporalAADebugRequested ? BurtShadingDebugSettings.Mode.ToString() : "Disabled") + " TAADebugActive=" + (temporalAADebugRequested && temporalAA != null && temporalAA.Enabled) + " TAAFinalBlitYFlip=" + temporalAAFinalBlitYFlip.ToString("0.###") + " TAADebugYFlip=" + temporalAADebugYFlip.ToString("0.###") + " TAAUVSpace=ResolveNoPreFlip;FinalBlitHandlesDisplayFlip;BurtVelocityPrevMinusCurrentHistoryUvPlusVelocity TAAFilter=Current3x3OffsetMinusPixelJitter TAANote=XRenderPixelFilterParity;VelocitySignInternalToBurt"); // 输出后处理执行摘要，说明当前模式、曝光倍率、颜色调整、Bloom 和 TAA 状态。
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
                ",IsEnabled=" + temporalAA.IsEnabled();
        }

        private static BurtTonemappingVolumeComponent GetTonemappingVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Tonemapping 组件的辅助函数。
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

            return stack.GetComponent<BurtTonemappingVolumeComponent>(); // 返回 BurtRP Tonemapping 组件，未添加时 Unity 会返回默认组件或空值。
        }

        private static BurtExposureVolumeComponent GetExposureVolumeComponent()
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

            return stack.GetComponent<BurtExposureVolumeComponent>();
        }

        private static BurtColorAdjustmentsVolumeComponent GetColorAdjustmentsVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Color Adjustments 组件的辅助函数。
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

            return stack.GetComponent<BurtColorAdjustmentsVolumeComponent>(); // 返回 BurtRP Color Adjustments 组件，未添加时 Unity 会返回默认组件或空值。
        }

        private static BurtBloomVolumeComponent GetBloomVolumeComponent() // 定义从 Unity VolumeStack 读取 BurtRP Bloom 组件的辅助函数。
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

            return stack.GetComponent<BurtBloomVolumeComponent>(); // 返回 BurtRP Bloom 组件，未添加时 Unity 会返回默认组件或空值。
        }

        private static BurtTemporalAAVolumeComponent GetTemporalAAVolumeComponent()
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

            return stack.GetComponent<BurtTemporalAAVolumeComponent>();
        }
    }

    internal readonly struct BurtPreExposureState
    {
        public static readonly BurtPreExposureState Default = new BurtPreExposureState(1f, 1f);

        public float PreExposure { get; }
        public float InvPreExposure { get; }
        public float ResidualPostExposure { get; }
        public float PostExposure { get; }
        public float ExposureRatio { get; }
        public bool Enabled { get; }

        public BurtPreExposureState(float preExposure, float postExposure)
        {
            PreExposure = BurtPreExposureUtility.SanitizeExposure(preExposure, 1f);
            InvPreExposure = 1f / PreExposure;
            PostExposure = BurtPreExposureUtility.SanitizeExposure(postExposure, 1f);
            ResidualPostExposure = PostExposure / PreExposure;
            ExposureRatio = 1f;
            Enabled = Mathf.Abs(PreExposure - 1f) > 0.0001f;
        }
    }

    internal static class BurtPreExposureUtility
    {
        public const float MinExposure = 0.0001f;
        public const float MaxExposure = 65504f;
        public const float TemporalAAInvalidationEVThreshold = 0.25f;

        public static readonly int PreExposureId = Shader.PropertyToID("_BurtPreExposure");
        public static readonly int InvPreExposureId = Shader.PropertyToID("_BurtInvPreExposure");
        public static readonly int PreExposureParamsId = Shader.PropertyToID("_BurtPreExposureParams");

        private static readonly System.Collections.Generic.Dictionary<int, float> CameraPreExposureHistory = new System.Collections.Generic.Dictionary<int, float>();

        public static BurtPreExposureState ResolveForFrame(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(request, asset))
            {
                return BurtPreExposureState.Default;
            }

            var exposureSettings = BurtPostProcessUtility.ResolvePhysicalExposureSettings(request, asset);
            return ResolveForFrame(exposureSettings);
        }

        public static BurtPreExposureState ResolveForFrame(BurtPhysicalExposureSettings exposureSettings)
        {
            var postExposure = SanitizeExposure(exposureSettings.Multiplier, 1f);
            return new BurtPreExposureState(postExposure, postExposure);
        }

        public static float ResolveResidualPostExposure(BurtPhysicalExposureSettings exposureSettings)
        {
            return ResolveForFrame(exposureSettings).ResidualPostExposure;
        }

        public static float ResolveResidualPostExposure(BurtPhysicalExposureSettings exposureSettings, BurtPreExposureState preExposureState)
        {
            return SanitizeExposure(exposureSettings.Multiplier, 1f) * preExposureState.InvPreExposure;
        }

        public static bool ShouldInvalidateTemporalAAHistory(Camera camera, BurtPreExposureState state, out string reason)
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

        public static void UploadGlobals(UnityEngine.Rendering.CommandBuffer cmd, BurtPreExposureState state)
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
