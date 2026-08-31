using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Material、Shader、Matrix4x4 和 MeshTopology。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CommandBufferPool 和 RenderTarget 相关 API。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理 Pass 可以直接接入现有 RenderGraph。
{
    internal sealed class AllocatePostProcessColorPass : BurtRenderPass // 定义后处理中间颜色分配 Pass，负责申请 PostProcessColor 临时 RT。
    {
        public override string Name => "Allocate Post Process Color"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果当前 request 没有启用后处理框架，就不声明资源写入。
            {
                return; // 直接结束配置，保持未启用时的 RenderGraph 干净。
            }

            builder.WritePostProcessColor(); // 声明这个 Pass 会创建并写入 PostProcessColor 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行后处理中间颜色 RT 的申请。
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次判断，防止配置和执行之间状态变化。
            {
                return; // 未启用时直接跳过，不申请任何临时 RT。
            }

            var renderContext = context.ScriptableContext; // 从执行上下文中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从执行上下文中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出相机，用来创建匹配尺寸的后处理 RT。

            var postProcessColorTarget = context.PostProcessColorTarget; // 从资源表中取出 PostProcessColor 句柄。

            if (!postProcessColorTarget.IsValid) // 如果资源句柄无效，说明 RenderGraph 没有注册 PostProcessColor。
            {
                return; // 直接跳过，避免申请一个后续 Pass 无法找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera); // 创建和 CameraColor 匹配的后处理颜色 RT 描述。

            context.ResourceRegistry.SetRenderTargetDescriptor(BurtRenderGraphResourceRegistry.PostProcessColorName, descriptor, FilterMode.Bilinear, "Burt Post Process Color");
            postProcessColorTarget = context.ResourceRegistry.AllocateRenderTarget(BurtRenderGraphResourceRegistry.PostProcessColorName);

            var cmd = context.AcquireCommandBuffer(Name); // 复用 RenderGraph 当前的统一命令流，让 RenderDoc 保持连续的 Pass 录制结构。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.PostProcessColorTextureId, postProcessColorTarget.Identifier); // 把 PostProcessColor 暴露为全局纹理，方便调试或后续效果链采样。

            context.ExecuteAndReleaseCommandBuffer(cmd); // 共享命令流由 RenderGraph 在 Pass 边界统一提交；独立执行时仍兼容本地缓冲。
        }
    }

    internal sealed class PostProcessPass : BurtRenderPass // 定义第一版正式后处理 Pass，支持 No-op Copy、Tonemapping 和 Color Adjustments。
    {
        private const string PostProcessShaderName = "Hidden/BurtRP/PostProcessCopy"; // 定义后处理 shader 的查找名称，必须和 shader 文件里的 Shader 名称一致。
        private const string TemporalAAComputeShaderResourcePath = "BurtTemporalAA";
        private const string SMAAAreaTextureResourcePath = "SMAA/AreaTex";
        private const string SMAASearchTextureResourcePath = "SMAA/SearchTex";
        private const int MaxBloomMipCount = 8; // 第一版 Bloom 最多申请 8 级临时 RT，避免动态 RenderGraph 资源注册过重。

        private const int MaxBloomGaussianSamples = PostProcessUtility.BloomGaussianMaxSamples; // Match XRender PC GaussianBlur sample cap.
        private const int BloomGaussianSampleCapacity = PostProcessUtility.BloomGaussianSampleCapacity; // XRender keeps 64 shader slots while the PC runtime caps active samples at 32.
        private const int BloomGaussianKernelCacheSize = MaxBloomMipCount * 2; // One horizontal and one vertical kernel per Bloom mip.
        private const float BloomGaussianKernelRadiusCacheScale = 1024f; // Quantize radius enough to keep cache stable without visible drift.
        private static readonly RenderQueueRange TemporalAAObjectMotionVectorQueueRange = RenderQueueRange.opaque;
        private static readonly RenderQueueRange TemporalAATransparentMotionVectorQueueRange = RenderQueueRange.transparent;
        private static readonly ShaderTagId TemporalAAObjectMotionVectorsTag = new ShaderTagId("BurtMotionVectors");
        private static readonly ShaderTagId TemporalAAForwardOnlyMotionVectorsTag = new ShaderTagId("BurtForwardOnlyMotionVectors");
        private enum PostProcessShaderPass
        {
            CopyAndComposite = 0,
            BloomPrefilter = 1,
            BloomDownsample = 2,
            BloomGaussian = 3,
            TemporalAAResolve = 4,
            TemporalAACurrentDepth = 5,
            TemporalAACameraVelocity = 6,
            TemporalAAVelocityDilation = 7,
            TemporalAADecimateHistory = 8,
            TemporalAACopy = 9,
            BloomDebug = 10,
            TemporalAAClosestDepthCopy = 11,
            TemporalAABuildPrevUseCount = 12,
            AutoExposureLogLuminanceReduce = 13,
            AutoExposureFinalReduce = 14,
            AutoExposureDebug = 15,
            TemporalAAMetadata = 16,
            TemporalAAUpscale = 17,
            TemporalAABuildStencilMask = 18,
            Vignette = 19,
            RCAS = 20,
            FXAA = 21,
            SMAAEdgeDetection = 22,
            SMAABlendWeights = 23,
            SMAANeighborhoodBlending = 24,
            LensFlare = 25,
            DiaphragmDepthOfField = 26,
            PlainCopy = 27
        }

        private static int ShaderPass(PostProcessShaderPass pass)
        {
            return (int)pass;
        }

        private static readonly int SourceTextureId = Shader.PropertyToID("_BurtPostProcessSourceTexture"); // 缓存源纹理属性 ID，避免每帧通过字符串查找。

        private static readonly int BloomTextureId = Shader.PropertyToID("_BurtBloomTexture"); // 缓存 Bloom 合成纹理属性 ID，最终合成时采样 mip0。

        private static readonly int TemporalAAHistoryTextureId = Shader.PropertyToID("_BurtTAAHistoryTexture");
        private static readonly int TemporalAADepthHistoryTextureId = Shader.PropertyToID("_BurtTAADepthHistoryTexture");
        private static readonly int TemporalAACurrentDepthTextureId = Shader.PropertyToID("_BurtTAACurrentDepthTexture");
        private static readonly int TemporalAARawVelocityTextureId = Shader.PropertyToID("_BurtTAARawVelocityTexture");
        private static readonly int TemporalAAVelocityTextureId = Shader.PropertyToID("_BurtTAAVelocityTexture");
        private static readonly int TemporalAADilatedVelocityTextureId = Shader.PropertyToID("_BurtTAADilatedVelocityTexture");
        private static readonly int TemporalAADilateMaskTextureId = Shader.PropertyToID("_BurtTAADilateMaskTexture");
        private static readonly int TemporalAADilateMaskOutputTextureId = Shader.PropertyToID("_BurtTAADilateMaskOutputTexture");
        private static readonly int TemporalAAClosestDepthTextureId = Shader.PropertyToID("_BurtTAAClosestDepthTexture");
        private static readonly int TemporalAAClosestDepthOutputTextureId = Shader.PropertyToID("_BurtTAAClosestDepthOutputTexture");
        private static readonly int TemporalAAPrevUseCountTextureId = Shader.PropertyToID("_BurtTAAPrevUseCountTexture");
        private static readonly int TemporalAAPrevUseCountUintTextureId = Shader.PropertyToID("_BurtTAAPrevUseCountUintTexture");
        private static readonly int TemporalAAPrevUseCountOutputTextureId = Shader.PropertyToID("_BurtTAAPrevUseCountOutputTexture");
        private static readonly int TemporalAAMetadataTextureId = Shader.PropertyToID("_BurtTAAMetadataTexture");
        private static readonly int TemporalAAStencilMaskTextureId = Shader.PropertyToID("_BurtTAAStencilMaskTexture");
        private static readonly int TemporalAAResponsiveMaskTextureId = Shader.PropertyToID("_BurtTAAResponsiveMaskTexture");
        private static readonly int TemporalAAResolveTextureId = Shader.PropertyToID("_BurtTAAResolveTexture");
        private static readonly int TemporalAAParallaxRejectionTextureId = Shader.PropertyToID("_BurtTAAParallaxRejectionTexture");
        private static readonly int TemporalAAParallaxRejectionOutputTextureId = Shader.PropertyToID("_BurtTAAParallaxRejectionOutputTexture");
        private static readonly int TemporalAAHistoryRejectionTextureId = Shader.PropertyToID("_BurtTAAHistoryRejectionTexture");
        private static readonly int TemporalAAHistoryRejectionOutputTextureId = Shader.PropertyToID("_BurtTAAHistoryRejectionOutputTexture");
        private static readonly int TemporalAADilatedHistoryRejectionTextureId = Shader.PropertyToID("_BurtTAADilatedHistoryRejectionTexture");
        private static readonly int TemporalAADilatedHistoryRejectionOutputTextureId = Shader.PropertyToID("_BurtTAADilatedHistoryRejectionOutputTexture");
        private static readonly int TemporalAAHasDilatedHistoryRejectionId = Shader.PropertyToID("_BurtTAAHasDilatedHistoryRejection");
        private static readonly int TemporalAAUReprojectedGuideTextureId = Shader.PropertyToID("_BurtTAAUReprojectedGuideTexture");
        private static readonly int TemporalAAUReprojectedGuideOutputTextureId = Shader.PropertyToID("_BurtTAAUReprojectedGuideOutputTexture");
        private static readonly int TemporalAAUHistoryGuideTextureId = Shader.PropertyToID("_BurtTAAUHistoryGuideTexture");
        private static readonly int TemporalAAUHistoryGuideOutputTextureId = Shader.PropertyToID("_BurtTAAUHistoryGuideOutputTexture");
        private static readonly int TemporalAAUShadingRejectionTextureId = Shader.PropertyToID("_BurtTAAUShadingRejectionTexture");
        private static readonly int TemporalAAUShadingRejectionOutputTextureId = Shader.PropertyToID("_BurtTAAUShadingRejectionOutputTexture");
        private static readonly int TemporalAAUDilatedShadingRejectionTextureId = Shader.PropertyToID("_BurtTAAUDilatedShadingRejectionTexture");
        private static readonly int TemporalAAUDilatedShadingRejectionOutputTextureId = Shader.PropertyToID("_BurtTAAUDilatedShadingRejectionOutputTexture");
        private static readonly int TemporalAAUUpdatedHistoryTextureId = Shader.PropertyToID("_BurtTAAUUpdatedHistoryTexture");
        private static readonly int TemporalAAUUpdatedHistoryOutputTextureId = Shader.PropertyToID("_BurtTAAUUpdatedHistoryOutputTexture");
        private static readonly int TemporalAAUFinalBlendDebugOutputTextureId = Shader.PropertyToID("_BurtTAAUFinalBlendDebugOutputTexture");
        private static readonly int TemporalAAUOutputTextureId = Shader.PropertyToID("_BurtTAAUOutputTexture");
        private static readonly int TemporalAAUHistoryParamsId = Shader.PropertyToID("_BurtTAAUHistoryParams");
        private static readonly int TemporalAAUOutputTexelSizeId = Shader.PropertyToID("_BurtTAAUOutputTexelSize");
        private static readonly int TemporalAAUInputTexelSizeId = Shader.PropertyToID("_BurtTAAUInputTexelSize");
        private static readonly int TemporalAAUGuideTexelSizeId = Shader.PropertyToID("_BurtTAAUGuideTexelSize");
        private static readonly int TemporalAAUpscaleCurrentTextureId = Shader.PropertyToID("_BurtTAAUpscaleCurrentTexture");
        private static readonly int TemporalAAUpscaleTexelSizeId = Shader.PropertyToID("_BurtTAAUpscaleTexelSize");
        private static readonly int TemporalAAUpscaleParamsId = Shader.PropertyToID("_BurtTAAUpscaleParams");
        private static readonly int TemporalAADebugTextureId = Shader.PropertyToID("_BurtTAADebugTexture");
        private static readonly int TemporalAAPreviousViewProjectionId = Shader.PropertyToID("_BurtTAAPreviousViewProjection");
        private static readonly int TemporalAAPreviousNonJitteredViewProjectionId = Shader.PropertyToID("_BurtTAAPreviousNonJitteredViewProjection");
        private static readonly int TemporalAACurrentViewProjectionId = Shader.PropertyToID("_BurtTAACurrentViewProjection");
        private static readonly int TemporalAACurrentNonJitteredViewProjectionId = Shader.PropertyToID("_BurtTAACurrentNonJitteredViewProjection");
        private static readonly int TemporalAAInverseCurrentViewProjectionId = Shader.PropertyToID("_BurtTAAInverseCurrentViewProjection");
        private static readonly int TemporalAAInverseCurrentNonJitteredViewProjectionId = Shader.PropertyToID("_BurtTAAInverseCurrentNonJitteredViewProjection");
        private static readonly int TemporalAAClipToPreviousClipId = Shader.PropertyToID("_BurtTAAClipToPreviousClip");
        private static readonly int TemporalAAJitterId = Shader.PropertyToID("_BurtTAAJitter");
        private static readonly int TemporalAATexelSizeId = Shader.PropertyToID("_BurtTAATexelSize");
        private static readonly int TemporalAAHistoryTexelSizeId = Shader.PropertyToID("_BurtTAAHistoryTexelSize");
        private static readonly int TemporalAADepthHistoryTexelSizeId = Shader.PropertyToID("_BurtTAADepthHistoryTexelSize");
        private static readonly int TemporalAAParamsId = Shader.PropertyToID("_BurtTAAParams");
        private static readonly int TemporalAAParams2Id = Shader.PropertyToID("_BurtTAAParams2");
        private static readonly int TemporalAADepthParamsId = Shader.PropertyToID("_BurtTAADepthParams");
        private static readonly int TemporalAADecimateModeId = Shader.PropertyToID("_BurtTAADecimateMode");
        private static readonly int TemporalAADilateModeId = Shader.PropertyToID("_BurtTAADilateMode");
        private static readonly int TemporalAAResponsiveParamsId = Shader.PropertyToID("_BurtTAAResponsiveParams");
        private static readonly int TemporalAAEdgeParamsId = Shader.PropertyToID("_BurtTAAEdgeParams");
        private static readonly int TemporalAAStencilTexelSizeId = Shader.PropertyToID("_BurtTAAStencilTexelSize");
        private static readonly int TemporalAAHistoryExposureCorrectionId = Shader.PropertyToID("_BurtTAAHistoryExposureCorrection");
        private static readonly int TemporalAACurrentSampleWeights0Id = Shader.PropertyToID("_BurtTAACurrentSampleWeights0");
        private static readonly int TemporalAACurrentSampleWeights1Id = Shader.PropertyToID("_BurtTAACurrentSampleWeights1");
        private static readonly int TemporalAACurrentSampleWeights2Id = Shader.PropertyToID("_BurtTAACurrentSampleWeights2");
        private static readonly int TemporalAAHasGBufferId = Shader.PropertyToID("_BurtTAAHasGBuffer");
        private static readonly int TemporalAAPreviousRenderDeltaTimeId = Shader.PropertyToID("_BurtTAAPreviousRenderDeltaTime");
        private static readonly int ShadingDebugEnabledId = Shader.PropertyToID(BurtShadingDebugSettings.EnabledShaderName);
        private static readonly Color TemporalAADebugUnavailableColor = new Color(0.65f, 0.05f, 0.9f, 1f);

        private static readonly int UseBloomId = Shader.PropertyToID("_BurtUseBloom"); // 缓存 Bloom 合成开关属性 ID。

        private static readonly int BloomIntensityId = Shader.PropertyToID("_BurtBloomIntensity"); // 缓存 Bloom 合成强度属性 ID。

        private static readonly int BloomThresholdId = Shader.PropertyToID("_BurtBloomThreshold"); // 缓存 Bloom 预过滤阈值属性 ID。

        private static readonly int BloomTexelSizeId = Shader.PropertyToID("_BurtBloomTexelSize"); // 缓存 Bloom 当前源纹理 texel size 属性 ID。

        private static readonly int BloomAdditiveTextureId = Shader.PropertyToID("_BurtBloomAdditiveTexture"); // 缓存 PC Bloom 高斯阶段的加法合成纹理。

        private static readonly int UseBloomAdditiveId = Shader.PropertyToID("_BurtUseBloomAdditive"); // 缓存 PC Bloom 是否启用加法合成。

        private static readonly int BloomSampleCountId = Shader.PropertyToID("_BurtBloomSampleCount"); // Cached PC Bloom Gaussian sample count.

        private static readonly int BloomSampleWeightsId = Shader.PropertyToID("_BurtBloomSampleWeights"); // Cached PC Bloom Gaussian weights.

        private static readonly int BloomSampleOffsetsId = Shader.PropertyToID("_BurtBloomSampleOffsets"); // Cached PC Bloom Gaussian offsets.

        private static readonly int BloomBypassThresholdId = Shader.PropertyToID("_BurtBloomBypassThreshold"); // Cached Bloom threshold bypass flag.

        private static readonly int BloomExposureScaleId = Shader.PropertyToID("_BurtBloomExposureScale");

        private static readonly int UseBloomAlphaId = Shader.PropertyToID("_BurtUseBloomAlpha"); // Cached Bloom alpha-channel output flag.

        private static readonly int BloomDebugModeId = Shader.PropertyToID("_BurtBloomDebugMode"); // Cached Bloom debug shader mode.

        private static readonly int BloomDebugYFlipId = Shader.PropertyToID("_BurtBloomDebugYFlip"); // Cached Bloom debug source orientation flag.

        private static readonly int AutoExposureTexelSizeId = Shader.PropertyToID("_BurtAutoExposureTexelSize");
        private static readonly int AutoExposureDebugModeId = Shader.PropertyToID("_BurtAutoExposureDebugMode");
        private static readonly int AutoExposureDebugParamsId = Shader.PropertyToID("_BurtAutoExposureDebugParams");
        private static readonly int AutoExposureDebugParams2Id = Shader.PropertyToID("_BurtAutoExposureDebugParams2");
        private static readonly int AutoExposureDebugMeteringMaskId = Shader.PropertyToID("_BurtAutoExposureDebugMeteringMask");
        private static readonly int AutoExposureDebugUseMeteringMaskId = Shader.PropertyToID("_BurtAutoExposureDebugUseMeteringMask");
        private static readonly int AutoExposureDebugHistogramTextureId = Shader.PropertyToID("_BurtAutoExposureDebugHistogramTexture");
        private static readonly int AutoExposureDebugHasHistogramId = Shader.PropertyToID("_BurtAutoExposureDebugHasHistogram");
        private static readonly int AutoExposureDebugToneMappedTextureId = Shader.PropertyToID("_BurtAutoExposureDebugToneMappedTexture");
        private static readonly int AutoExposureDebugHasToneMappedTextureId = Shader.PropertyToID("_BurtAutoExposureDebugHasToneMappedTexture");
        private static readonly int AutoExposureDebugFlipYId = Shader.PropertyToID("_BurtAutoExposureDebugFlipY");
        private static readonly int PlainCopyFlipYId = Shader.PropertyToID("_BurtPlainCopyFlipY");
        private static readonly int[] AutoExposureTextureIds = CreateAutoExposureTextureIds();

        private static readonly Vector4[] BloomGaussianWeights = new Vector4[BloomGaussianSampleCapacity]; // Match XRender's 64-slot constant-buffer layout.

        private static readonly Vector4[] BloomGaussianOffsets = new Vector4[BloomGaussianSampleCapacity]; // Match XRender's 64-slot constant-buffer layout.

        private static readonly BloomGaussianKernelCacheEntry[] BloomGaussianKernelCache = CreateBloomGaussianKernelCache();

        private static int BloomGaussianKernelCacheNextIndex;

        private static readonly Vector2Int[] TemporalAACurrentSampleOffsets =
        {
            new Vector2Int(0, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, -1),
            new Vector2Int(-1, 1),
            new Vector2Int(1, -1),
            new Vector2Int(1, 1),
            new Vector2Int(-1, -1)
        };

        private static readonly float[] TemporalAACurrentSampleWeights = new float[9];

        private static readonly int TonemappingModeId = Shader.PropertyToID("_BurtTonemappingMode"); // 缓存 Tonemapping 模式属性 ID，避免每帧通过字符串查找。

        private static readonly int PostExposureId = Shader.PropertyToID("_BurtPostExposure"); // 缓存后处理曝光倍率属性 ID，避免每帧通过字符串查找。
        private static readonly int ExposureTextureId = Shader.PropertyToID("_BurtExposureTexture");
        private static readonly int UseExposureTextureId = Shader.PropertyToID("_BurtUseExposureTexture");
        private static readonly int UseLocalExposureId = Shader.PropertyToID("_BurtUseLocalExposure");
        private static readonly int LocalExposureHistogramTextureId = Shader.PropertyToID("_BurtLocalExposureHistogramTexture");
        private static readonly int LocalExposureBlurredLogLuminanceTextureId = Shader.PropertyToID("_BurtLocalExposureBlurredLogLuminanceTexture");
        private static readonly int LocalExposureContrastParamsId = Shader.PropertyToID("_BurtLocalExposureContrastParams");
        private static readonly int LocalExposureThresholdParamsId = Shader.PropertyToID("_BurtLocalExposureThresholdParams");
        private static readonly int LocalExposureGridParamsId = Shader.PropertyToID("_BurtLocalExposureGridParams");

        private static readonly int FilmSlopeId = Shader.PropertyToID("_BurtFilmSlope"); // 缓存 UE/XRender Film Slope 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmToeId = Shader.PropertyToID("_BurtFilmToe"); // 缓存 UE/XRender Film Toe 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmShoulderId = Shader.PropertyToID("_BurtFilmShoulder"); // 缓存 UE/XRender Film Shoulder 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmBlackClipId = Shader.PropertyToID("_BurtFilmBlackClip"); // 缓存 UE/XRender Film Black Clip 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmWhiteClipId = Shader.PropertyToID("_BurtFilmWhiteClip"); // 缓存 UE/XRender Film White Clip 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmBlueCorrectionId = Shader.PropertyToID("_BurtFilmBlueCorrection"); // 缓存 XRender Blue Correction 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmExpandGamutId = Shader.PropertyToID("_BurtFilmExpandGamut"); // 缓存 XRender Expand Gamut 属性 ID，避免每帧通过字符串查找。

        private static readonly int FilmToneCurveAmountId = Shader.PropertyToID("_BurtFilmToneCurveAmount"); // 缓存 XRender Tone Curve Amount 属性 ID，避免每帧通过字符串查找。

        private static readonly int UseColorAdjustmentsId = Shader.PropertyToID("_BurtUseColorAdjustments"); // 缓存是否启用 Color Adjustments 的属性 ID，避免每帧通过字符串查找。

        private static readonly int ColorAdjustmentsSaturationId = Shader.PropertyToID("_BurtColorAdjustmentsSaturation"); // 缓存饱和度属性 ID，避免每帧通过字符串查找。

        private static readonly int ColorAdjustmentsContrastId = Shader.PropertyToID("_BurtColorAdjustmentsContrast"); // 缓存对比度属性 ID，避免每帧通过字符串查找。

        private static readonly int ColorAdjustmentsGammaId = Shader.PropertyToID("_BurtColorAdjustmentsGamma"); // 缓存 Gamma 属性 ID，避免每帧通过字符串查找。

        private static readonly int ColorAdjustmentsColorFilterId = Shader.PropertyToID("_BurtColorAdjustmentsColorFilter"); // 缓存颜色滤镜属性 ID，避免每帧通过字符串查找。

        private static readonly int UseVignetteId = Shader.PropertyToID("_BurtUseVignette");
        private static readonly int VignetteColorId = Shader.PropertyToID("_BurtVignetteColor");
        private static readonly int VignetteParamsId = Shader.PropertyToID("_BurtVignetteParams");
        private static readonly int VignetteOptionsId = Shader.PropertyToID("_BurtVignetteOptions");
        private static readonly int PostProcessTexelSizeId = Shader.PropertyToID("_BurtPostProcessTexelSize");
        private static readonly int UseColorGradingId = Shader.PropertyToID("_BurtUseColorGrading");
        private static readonly int UseWhiteBalanceId = Shader.PropertyToID("_BurtUseWhiteBalance");
        private static readonly int WhiteBalanceParamsId = Shader.PropertyToID("_BurtWhiteBalanceParams");
        private static readonly int ColorGradingParamsId = Shader.PropertyToID("_BurtColorGradingParams");
        private static readonly int ColorGradingRangesId = Shader.PropertyToID("_BurtColorGradingRanges");
        private static readonly int ColorGradingGlobalSaturationId = Shader.PropertyToID("_BurtColorGradingGlobalSaturation");
        private static readonly int ColorGradingGlobalContrastId = Shader.PropertyToID("_BurtColorGradingGlobalContrast");
        private static readonly int ColorGradingGlobalGammaId = Shader.PropertyToID("_BurtColorGradingGlobalGamma");
        private static readonly int ColorGradingGlobalGainId = Shader.PropertyToID("_BurtColorGradingGlobalGain");
        private static readonly int ColorGradingGlobalOffsetId = Shader.PropertyToID("_BurtColorGradingGlobalOffset");
        private static readonly int ColorGradingShadowsSaturationId = Shader.PropertyToID("_BurtColorGradingShadowsSaturation");
        private static readonly int ColorGradingShadowsContrastId = Shader.PropertyToID("_BurtColorGradingShadowsContrast");
        private static readonly int ColorGradingShadowsGammaId = Shader.PropertyToID("_BurtColorGradingShadowsGamma");
        private static readonly int ColorGradingShadowsGainId = Shader.PropertyToID("_BurtColorGradingShadowsGain");
        private static readonly int ColorGradingShadowsOffsetId = Shader.PropertyToID("_BurtColorGradingShadowsOffset");
        private static readonly int ColorGradingMidtonesSaturationId = Shader.PropertyToID("_BurtColorGradingMidtonesSaturation");
        private static readonly int ColorGradingMidtonesContrastId = Shader.PropertyToID("_BurtColorGradingMidtonesContrast");
        private static readonly int ColorGradingMidtonesGammaId = Shader.PropertyToID("_BurtColorGradingMidtonesGamma");
        private static readonly int ColorGradingMidtonesGainId = Shader.PropertyToID("_BurtColorGradingMidtonesGain");
        private static readonly int ColorGradingMidtonesOffsetId = Shader.PropertyToID("_BurtColorGradingMidtonesOffset");
        private static readonly int ColorGradingHighlightsSaturationId = Shader.PropertyToID("_BurtColorGradingHighlightsSaturation");
        private static readonly int ColorGradingHighlightsContrastId = Shader.PropertyToID("_BurtColorGradingHighlightsContrast");
        private static readonly int ColorGradingHighlightsGammaId = Shader.PropertyToID("_BurtColorGradingHighlightsGamma");
        private static readonly int ColorGradingHighlightsGainId = Shader.PropertyToID("_BurtColorGradingHighlightsGain");
        private static readonly int ColorGradingHighlightsOffsetId = Shader.PropertyToID("_BurtColorGradingHighlightsOffset");
        private static readonly int ColorGradingLutId = Shader.PropertyToID("_BurtColorGradingLUT");
        private static readonly int ColorGradingLutParamsId = Shader.PropertyToID("_BurtColorGradingLutParams");
        private static readonly int RCASParamsId = Shader.PropertyToID("_BurtRCASParams");
        private static readonly int FXAAParamsId = Shader.PropertyToID("_BurtFXAAParams");
        private static readonly int SMAAParamsId = Shader.PropertyToID("_BurtSMAAParams");
        private static readonly int SMAAEdgeTextureId = Shader.PropertyToID("_BurtSMAAEdgeTexture");
        private static readonly int SMAABlendTextureId = Shader.PropertyToID("_BurtSMAABlendTexture");
        private static readonly int SMAAAreaTextureId = Shader.PropertyToID("_BurtSMAAAreaTexture");
        private static readonly int SMAASearchTextureId = Shader.PropertyToID("_BurtSMAASearchTexture");
        private static readonly int LensFlareBokeh0TextureId = Shader.PropertyToID("_BurtLensFlareBokeh0Tex");
        private static readonly int LensFlareBokeh1TextureId = Shader.PropertyToID("_BurtLensFlareBokeh1Tex");
        private static readonly int LensFlareBokeh2TextureId = Shader.PropertyToID("_BurtLensFlareBokeh2Tex");
        private static readonly int LensFlareBokeh3TextureId = Shader.PropertyToID("_BurtLensFlareBokeh3Tex");
        private static readonly int LensFlareBokeh4TextureId = Shader.PropertyToID("_BurtLensFlareBokeh4Tex");
        private static readonly int LensFlareLineTextureId = Shader.PropertyToID("_BurtLensFlareLineTex");
        private static readonly int LensFlareHiZDepthTextureId = BurtRenderGraphResourceRegistry.HiZDepthTextureId;
        private static readonly int LensFlareViewProjectionId = Shader.PropertyToID("_BurtLensFlareViewProjection");
        private static readonly int LensFlareBokeh0ScaleAndPositionId = Shader.PropertyToID("_BurtLensFlareBokeh0ScaleAndPosition");
        private static readonly int LensFlareBokeh1ScaleAndPositionId = Shader.PropertyToID("_BurtLensFlareBokeh1ScaleAndPosition");
        private static readonly int LensFlareBokeh2ScaleAndPositionId = Shader.PropertyToID("_BurtLensFlareBokeh2ScaleAndPosition");
        private static readonly int LensFlareBokeh3ScaleAndPositionId = Shader.PropertyToID("_BurtLensFlareBokeh3ScaleAndPosition");
        private static readonly int LensFlareBokeh4ScaleAndPositionId = Shader.PropertyToID("_BurtLensFlareBokeh4ScaleAndPosition");
        private static readonly int LensFlareBokeh0ColorId = Shader.PropertyToID("_BurtLensFlareBokeh0Color");
        private static readonly int LensFlareBokeh1ColorId = Shader.PropertyToID("_BurtLensFlareBokeh1Color");
        private static readonly int LensFlareBokeh2ColorId = Shader.PropertyToID("_BurtLensFlareBokeh2Color");
        private static readonly int LensFlareBokeh3ColorId = Shader.PropertyToID("_BurtLensFlareBokeh3Color");
        private static readonly int LensFlareBokeh4ColorId = Shader.PropertyToID("_BurtLensFlareBokeh4Color");
        private static readonly int LensFlareLineParamsId = Shader.PropertyToID("_BurtLensFlareLineParams");
        private static readonly int LensFlareTotalParamsId = Shader.PropertyToID("_BurtLensFlareTotalParams");
        private static readonly int LensFlareTintColorId = Shader.PropertyToID("_BurtLensFlareTintColor");
        private static readonly int LensFlareTextureFlags0Id = Shader.PropertyToID("_BurtLensFlareTextureFlags0");
        private static readonly int LensFlareTextureFlags1Id = Shader.PropertyToID("_BurtLensFlareTextureFlags1");
        private static readonly int LensFlareDepthParamsId = Shader.PropertyToID("_BurtLensFlareDepthParams");
        private static readonly int DiaphragmDepthOfFieldParams0Id = Shader.PropertyToID("_BurtDiaphragmDOFParams0");
        private static readonly int DiaphragmDepthOfFieldParams1Id = Shader.PropertyToID("_BurtDiaphragmDOFParams1");
        private static readonly int DiaphragmDepthOfFieldParams2Id = Shader.PropertyToID("_BurtDiaphragmDOFParams2");
        private static Material postProcessMaterial;
        private static ComputeShader temporalAAComputeShader;
        private static Texture2D smaaAreaTexture;
        private static Texture2D smaaSearchTexture;
        private static bool hasLoggedMissingShader; // 记录缺失 shader 警告是否已经输出，避免 Console 每帧刷屏。
        private static bool hasLoggedMissingTemporalAAComputeShader;
        private static bool hasLoggedMissingTemporalAAComputeKernel;
        private static bool hasLoggedMissingSMAATextures;
        private static bool hasLoggedMissingSMAAPasses;
        private static bool hasLoggedMissingLensFlarePass;
        private static bool hasLoggedMissingDiaphragmDepthOfFieldPass;

        public override string Name => "Post Process"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        private sealed class BloomGaussianKernelCacheEntry
        {
            public bool Valid;
            public int Hash;
            public int RadiusKey;
            public int Width;
            public int Height;
            public bool Horizontal;
            public int SampleCount;
            public readonly Vector4[] Weights = new Vector4[BloomGaussianSampleCapacity];
            public readonly Vector4[] Offsets = new Vector4[BloomGaussianSampleCapacity];
        }

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果当前 request 没启用后处理框架，就不声明任何资源。
            {
                return; // 直接结束配置，保持关闭状态下没有额外依赖。
            }

            builder.ReadCameraColor(); // 声明先读取场景渲染完成后的 CameraColor。

            builder.WritePostProcessColor(); // 声明第一段拷贝会写入 PostProcessColor。

            builder.ReadPostProcessColor(); // 声明第二段拷贝会读取 PostProcessColor。

            builder.WriteCameraColor(); // 声明最终会把结果写回 CameraColor，供 FinalBlit 继续输出。

            var bloomStageCount = PostProcessUtility.ResolveBloomMipCount(builder.Request, builder.Asset);
            if (bloomStageCount > 0)
            {
                builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.BloomInputName);
                var bloomSettings = PostProcessUtility.ResolveBloomSettings(builder.Asset);
                if (!PostProcessUtility.ShouldBypassBloomPrefilterThreshold(bloomSettings))
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.BloomSetupName);
                }

                for (var mipIndex = 0; mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; mipIndex++)
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(mipIndex));
                }

                var firstMipIndex = PostProcessUtility.ResolveBloomFirstStageMipIndex(bloomStageCount);
                for (var mipIndex = firstMipIndex; mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; mipIndex++)
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex));
                }
            }
        }

        public override void Execute(BurtRenderGraphContext context) // 执行无效果后处理拷贝。
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次检查开关，保证关闭后不会提交绘制命令。
            {
                return; // 未启用时直接跳过。
            }

            if (PostProcessUtility.IsTemporalAADebugRequested())
            {
                return;
            }

            var renderContext = context.ScriptableContext; // 从上下文中取出 Unity SRP 渲染上下文。

            var cameraColorTarget = context.CameraColorTarget; // 读取 CameraColor 句柄，作为后处理源和最终回写目标。

            var postProcessColorTarget = context.PostProcessColorTarget; // 读取 PostProcessColor 句柄，作为中间 ping-pong 目标。

            if (!cameraColorTarget.IsValid) // 如果 CameraColor 无效，说明场景颜色还没有可采样的源。
            {
                InvalidateTemporalAAIfEnabled(context, "ResolveMissingCameraColor");
                return; // 直接跳过，避免采样无效纹理。
            }

            if (!postProcessColorTarget.IsValid) // 如果 PostProcessColor 无效，说明分配 Pass 或资源注册没有生效。
            {
                InvalidateTemporalAAIfEnabled(context, "ResolveMissingPostProcessColor");
                return; // 直接跳过，避免写入无效目标。
            }

            var material = GetPostProcessMaterial(); // 获取或创建后处理材质。

            if (material == null) // 如果材质为空，说明 shader 没找到或创建失败。
            {
                InvalidateTemporalAAIfEnabled(context, "PostProcessShaderMissing");
                return; // 直接跳过，避免提交无效绘制。
            }

            var tonemappingMode = PostProcessUtility.ResolveTonemappingMode(context.Asset); // 从当前 VolumeStack 安全解析本次后处理应该使用的 Tonemapping 模式。

            var exposureSettings = PostProcessUtility.ResolvePhysicalExposureSettings(context.Request, context.Asset);
            var preExposureState = PreExposureUtility.ResolveForFrame(context.Request, context.Asset);
            var residualPostExposureMultiplier = preExposureState.ResidualPostExposure;
            var postExposureMultiplier = exposureSettings.Multiplier; // 把 Global Volume 中的 EV 曝光转换成本次 shader 使用的线性倍率。

            var filmSettings = PostProcessUtility.ResolveTonemappingFilmSettings(context.Asset); // 从 Global Volume 读取 UE/XRender Filmic 曲线参数，缺失时回退到默认值。

            var useColorAdjustments = PostProcessUtility.ShouldUseColorAdjustments(context.Request, context.Asset); // 判断当前 VolumeStack 是否需要执行基础颜色调整。

            var colorAdjustmentsSettings = PostProcessUtility.ResolveColorAdjustmentsSettings(context.Asset); // 从 Global Volume 读取基础颜色调整参数，缺失时回退到中性值。

            var colorGradingSettings = PostProcessUtility.ResolveColorGradingSettings(context.Asset);
            var useColorGrading = PostProcessUtility.ShouldUseColorGrading(context.Request, context.Asset);
            var useVignette = PostProcessUtility.ShouldUseVignette(context.Request, context.Asset);
            var vignetteSettings = PostProcessUtility.ResolveVignetteSettings(context.Asset);
            var autoExposureDebugMode = PostProcessUtility.ResolveAutoExposureDebugMode(BurtShadingDebugSettings.Mode);

            var bloomSettings = PostProcessUtility.ResolveBloomSettings(context.Asset); // 从 Global Volume 读取 Bloom 参数，未启用时回退到关闭状态。

            var bloomMipCount = PostProcessUtility.ResolveBloomMipCount(context.Request, context.Asset); // 按当前相机尺寸和 Volume 上限计算实际 mip 数。
            var bloomDebugView = PostProcessUtility.ResolveBloomDebugView(bloomSettings); // Shading Debug 的 Bloom Prefilter 会覆盖 Volume 内的 Bloom debug 下拉。
            var bloomFirstMipIndex = bloomMipCount > 0 ? PostProcessUtility.ResolveBloomFirstStageMipIndex(bloomMipCount) : 0;
            var bloomOutputTarget = bloomMipCount > 0 && context.ResourceRegistry != null
                ? context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(bloomFirstMipIndex))
                : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(0));
            var hasBloomOutput = bloomMipCount > 0 && bloomOutputTarget.IsValid;

            ResolveActivePostProcessSize(context, out var postProcessWidth, out var postProcessHeight);

            var cmd = context.AcquireCommandBuffer(Name); // 复用 RenderGraph 当前的统一命令流，避免后处理被拆成孤立提交。
            PreExposureUtility.UploadGlobals(cmd, preExposureState);
            cmd.SetRenderTarget(postProcessColorTarget.Identifier); // 先绑定 PostProcessColor，让第一段全屏拷贝写入后处理中间目标。
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, postProcessWidth, postProcessHeight);
            SetPostProcessTexelSize(cmd, postProcessWidth, postProcessHeight);

            var autoExposureDebugDrawnToCameraColor = false;
            var useBloomDebug = ShouldUseBloomDebugView(bloomDebugView, bloomMipCount); // Bloom debug 只在 Bloom 实际执行且没有其他 shading debug 抢占时显示。
            if (useBloomDebug)
            {
                SetBloomDebugSource(cmd, context, cameraColorTarget.Identifier, bloomSettings, bloomDebugView, bloomMipCount, preExposureState); // 把选中的 Bloom 图资源绑定到 debug pass。
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomDebug), MeshTopology.Triangles, 3, 1); // 直接把 Bloom debug 画到 PostProcessColor。
            }
            else if (autoExposureDebugMode > 0)
            {
                var needsRealToneMappedSource = autoExposureDebugMode == 4 || autoExposureDebugMode == 5;
                if (needsRealToneMappedSource)
                {
                    SetCompositeGlobals(
                        cmd,
                        context,
                        cameraColorTarget.Identifier,
                        hasBloomOutput ? bloomOutputTarget.Identifier : cameraColorTarget.Identifier,
                        hasBloomOutput,
                        bloomSettings,
                        bloomDebugView,
                        bloomMipCount,
                        tonemappingMode,
                        residualPostExposureMultiplier,
                        exposureSettings,
                        filmSettings,
                        useColorAdjustments,
                        colorAdjustmentsSettings,
                        colorGradingSettings,
                        useColorGrading);
                    cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.CopyAndComposite), MeshTopology.Triangles, 3, 1);
                    cmd.SetRenderTarget(cameraColorTarget.Identifier);
                    BurtRenderTargetDescriptorUtility.SetViewport(cmd, postProcessWidth, postProcessHeight);
                    autoExposureDebugDrawnToCameraColor = true;
                }

                SetAutoExposureDebugSource(
                    cmd,
                    context,
                    cameraColorTarget.Identifier,
                    exposureSettings,
                    autoExposureDebugMode,
                    needsRealToneMappedSource ? postProcessColorTarget.Identifier : cameraColorTarget.Identifier,
                    needsRealToneMappedSource);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.AutoExposureDebug), MeshTopology.Triangles, 3, 1);
            }
            else
            {
                SetCompositeGlobals(
                    cmd,
                    context,
                    cameraColorTarget.Identifier,
                    hasBloomOutput ? bloomOutputTarget.Identifier : cameraColorTarget.Identifier,
                    hasBloomOutput,
                    bloomSettings,
                    bloomDebugView,
                    bloomMipCount,
                    tonemappingMode,
                    residualPostExposureMultiplier,
                    exposureSettings,
                    filmSettings,
                    useColorAdjustments,
                    colorAdjustmentsSettings,
                    colorGradingSettings,
                    useColorGrading);

                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.CopyAndComposite), MeshTopology.Triangles, 3, 1); // 绘制全屏三角形，把 CameraColor 处理到 PostProcessColor。
            }

            DisablePostProcessEffects(cmd);
            var allowFinalPostEffects = !useBloomDebug && autoExposureDebugMode <= 0;
            var currentSource = autoExposureDebugDrawnToCameraColor ? cameraColorTarget.Identifier : postProcessColorTarget.Identifier;
            var currentIsCameraColor = autoExposureDebugDrawnToCameraColor;
            var nextTargetIsCameraColor = true;

            if (allowFinalPostEffects && useVignette)
            {
                SetVignetteGlobals(cmd, vignetteSettings);
                DrawFinalPostProcessPass(cmd, context, material, currentSource, nextTargetIsCameraColor ? cameraColorTarget.Identifier : postProcessColorTarget.Identifier, PostProcessShaderPass.Vignette);
                currentIsCameraColor = nextTargetIsCameraColor;
                currentSource = currentIsCameraColor ? cameraColorTarget.Identifier : postProcessColorTarget.Identifier;
                nextTargetIsCameraColor = !nextTargetIsCameraColor;
                cmd.SetGlobalFloat(UseVignetteId, 0f);
            }

            if (!currentIsCameraColor)
            {
                if (autoExposureDebugMode > 0)
                {
                    // Auto-exposure debug is already a final display image. The
                    // intermediate RT -> CameraColor transition needs one explicit
                    // UV correction on D3D so the scene and XRender-style overlay
                    // keep their top-left orientation.
                    DrawFinalPostProcessPass(
                        cmd,
                        context,
                        material,
                        currentSource,
                        cameraColorTarget.Identifier,
                        PostProcessShaderPass.PlainCopy,
                        true);
                }
                else
                {
                    // Match XRender's post chain: tone-map/composite exactly once,
                    // then use a neutral transfer when ping-ponging back to the
                    // camera color target. Re-running CopyAndComposite here applies
                    // exposure, bloom and tone mapping a second time.
                    DrawFinalPostProcessPass(cmd, context, material, currentSource, cameraColorTarget.Identifier, PostProcessShaderPass.PlainCopy);
                }
            }


            context.ExecuteAndReleaseCommandBuffer(cmd); // 共享命令流在 Pass 结束时统一提交。

            PostProcessUtility.LogPostProcessExecuted(context, tonemappingMode, postExposureMultiplier, preExposureState, useColorAdjustments, useVignette, vignetteSettings, bloomSettings, bloomMipCount); // 如果用户开启了后处理调试日志，就输出本次后处理执行信息。
        }

        internal static bool ShouldUseTemporalAAPass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return PostProcessUtility.IsTemporalAADebugRequested() || PostProcessUtility.ShouldUseTemporalAA(request, asset);
        }

        internal static bool ShouldUseTemporalAAUpscale(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return request != null &&
                request.Camera != null &&
                PostProcessUtility.ShouldUseTemporalAA(request, asset) &&
                BurtRenderTargetDescriptorUtility.ResolveInputRenderScale(request.Camera) < 0.9999f;
        }

        internal static bool ShouldUseDiaphragmDepthOfFieldPass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return !PostProcessUtility.IsTemporalAADebugRequested() && PostProcessUtility.ShouldUseDiaphragmDepthOfField(request, asset);
        }

        internal static bool ShouldUseLensFlarePass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return !PostProcessUtility.IsTemporalAADebugRequested() && PostProcessUtility.ShouldUseLensFlare(request, asset);
        }

        internal static bool ShouldUseBloomPass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return !PostProcessUtility.IsTemporalAADebugRequested() && PostProcessUtility.ResolveBloomMipCount(request, asset) > 0;
        }

        internal static bool ShouldUseSubpixelMorphologicalAAPass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFinalAAPass(request, asset) && PostProcessUtility.ShouldUseSubpixelMorphologicalAA(request, asset);
        }

        internal static bool ShouldUseFastApproximateAAPass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFinalAAPass(request, asset) && PostProcessUtility.ShouldUseFastApproximateAA(request, asset);
        }

        internal static bool ShouldUseRCASPass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFinalAAPass(request, asset) && PostProcessUtility.ShouldUseRCAS(request, asset);
        }

        private static bool ShouldUseFinalAAPass(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return !PostProcessUtility.IsTemporalAADebugRequested() &&
                !PostProcessUtility.IsBloomDebugRequested() &&
                PostProcessUtility.ResolveAutoExposureDebugMode(BurtShadingDebugSettings.Mode) <= 0;
        }

        internal sealed class TemporalAAPass : BurtRenderPass
        {
            public override string Name => "Temporal AA";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseTemporalAAPass(builder.Request, builder.Asset))
                {
                    return;
                }

                builder.ReadCameraColor();
                builder.ReadCameraDepth();
                if (ShouldUseTemporalAAUpscale(builder.Request, builder.Asset))
                {
                    builder.WriteTemporalAAOutput();
                }
                else
                {
                    builder.WritePostProcessColor();
                }

                var temporalAA = builder.Request != null ? builder.Request.TemporalAA : null;
                if (temporalAA != null &&
                    temporalAA.Enabled &&
                    builder.ResourceRegistry != null &&
                    builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.GBuffer0Name) &&
                    builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.GBuffer2Name))
                {
                    builder.ReadGBuffer0();
                    builder.ReadGBuffer2();
                }
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseTemporalAAPass(context.Request, context.Asset))
                {
                    return;
                }

                var cameraColorTarget = context.CameraColorTarget;
                var postProcessColorTarget = context.PostProcessColorTarget;
                var temporalAAOutputTarget = context.TemporalAAOutputTarget;
                var cameraDepthTarget = context.CameraDepthTarget;
                if (!cameraColorTarget.IsValid)
                {
                    InvalidateTemporalAAIfEnabled(context, "ResolveMissingCameraColor");
                    return;
                }

                var useTemporalAAUpscale = ShouldUseTemporalAAUpscale(context.Request, context.Asset);
                if (useTemporalAAUpscale && context.ResourceRegistry != null)
                {
                    var temporalAAOutputDescriptor = BurtRenderTargetDescriptorUtility.CreateOutputPostProcessColorDescriptor(context.Request.Camera);
                    var temporalAAUOutputFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32;
                    if (SystemInfo.IsFormatSupported(temporalAAUOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.Render) &&
                        SystemInfo.IsFormatSupported(temporalAAUOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.Sample) &&
                        SystemInfo.IsFormatSupported(temporalAAUOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore))
                    {
                        temporalAAOutputDescriptor.graphicsFormat = temporalAAUOutputFormat;
                    }
                    else
                    {
                        // Keep XRender's HDR output contract when packed
                        // R11G11B10 UAV writes are unavailable (notably D3D11).
                        temporalAAOutputDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
                    }
                    temporalAAOutputDescriptor.sRGB = false;
                    temporalAAOutputDescriptor.enableRandomWrite = SystemInfo.supportsComputeShaders;
                    context.ResourceRegistry.SetRenderTargetDescriptor(BurtRenderGraphResourceRegistry.TemporalAAOutputName, temporalAAOutputDescriptor, FilterMode.Bilinear, "Burt Temporal AA Output");
                    temporalAAOutputTarget = context.ResourceRegistry.AllocateRenderTarget(BurtRenderGraphResourceRegistry.TemporalAAOutputName);
                }

                if ((!useTemporalAAUpscale && !postProcessColorTarget.IsValid) ||
                    (useTemporalAAUpscale && !temporalAAOutputTarget.IsValid))
                {
                    InvalidateTemporalAAIfEnabled(context, useTemporalAAUpscale ? "ResolveMissingTemporalAAOutput" : "ResolveMissingPostProcessColor");
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    InvalidateTemporalAAIfEnabled(context, "PostProcessShaderMissing");
                    return;
                }

                var temporalAA = context.Request.TemporalAA ?? BurtTemporalAARequestState.Disabled;
                var temporalAADebugRequested = PostProcessUtility.IsTemporalAADebugRequested();
                var useTemporalAA = temporalAA.Enabled;
                if (!useTemporalAA && !temporalAADebugRequested)
                {
                    return;
                }

                var exposureSettings = PostProcessUtility.ResolvePhysicalExposureSettings(context.Request, context.Asset);
                var preExposureState = PreExposureUtility.ResolveForFrame(context.Request, context.Asset);
                var cmd = context.AcquireCommandBuffer(Name);
                PreExposureUtility.UploadGlobals(cmd, preExposureState);
                if (useTemporalAA &&
                    PreExposureUtility.ShouldInvalidateTemporalAAHistory(context.Request.Camera, preExposureState, out var preExposureInvalidationReason))
                {
                    BurtTemporalAAUtility.InvalidateHistory(context.Request.Camera, preExposureInvalidationReason);
                }

                if (temporalAADebugRequested && !useTemporalAA)
                {
                    ExecuteTemporalAADebugUnavailable(cmd, context.Request.Camera, postProcessColorTarget);
                    context.ExecuteAndReleaseCommandBuffer(cmd);
                    return;
                }

                var useTemporalAADebug = useTemporalAA && temporalAADebugRequested;
                var temporalAAOutputTargetForResolve = useTemporalAAUpscale ? temporalAAOutputTarget : postProcessColorTarget;
                useTemporalAA = ExecuteTemporalAA(context, cmd, context.Request.Camera, cameraColorTarget, cameraDepthTarget, temporalAAOutputTargetForResolve, material, temporalAA, useTemporalAADebug, useTemporalAAUpscale);
                if (!useTemporalAA && temporalAADebugRequested)
                {
                    ExecuteTemporalAADebugUnavailable(cmd, context.Request.Camera, temporalAAOutputTargetForResolve);
                }
                else if (!useTemporalAA)
                {
                    DrawFinalPostProcessPass(
                        cmd,
                        context.Request.Camera,
                        material,
                        cameraColorTarget.Identifier,
                        temporalAAOutputTargetForResolve.Identifier,
                        PostProcessShaderPass.PlainCopy);
                }

                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class TemporalAAFinalCopyPass : BurtRenderPass
        {
            public override string Name => "Temporal AA Final Copy";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseTemporalAAPass(builder.Request, builder.Asset))
                {
                    return;
                }

                if (ShouldUseTemporalAAUpscale(builder.Request, builder.Asset))
                {
                    builder.ReadTemporalAAOutput();
                    // TAAU transfers ownership from the scaled scene target to a
                    // full-resolution raster target consumed by Bloom/tonemapping.
                    builder.WritePostProcessColor();
                }
                else
                {
                    builder.ReadPostProcessColor();
                }
                builder.WriteCameraColor();
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseTemporalAAPass(context.Request, context.Asset) ||
                    context.Request == null ||
                    context.Request.Camera == null ||
                    !context.CameraColorTarget.IsValid)
                {
                    return;
                }

                var useTemporalAAUpscale = ShouldUseTemporalAAUpscale(context.Request, context.Asset);
                var sourceTarget = useTemporalAAUpscale ? context.TemporalAAOutputTarget : context.PostProcessColorTarget;
                if (!sourceTarget.IsValid ||
                    (useTemporalAAUpscale && (context.ResourceRegistry == null || !context.PostProcessColorTarget.IsValid)))
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                DisablePostProcessEffects(cmd);

                if (useTemporalAAUpscale)
                {
                    BurtRenderResolutionStageUtility.BeginOutputResolutionStage(context.Request.Camera);
                    // XRender changes sceneColor ownership at its BeforeBloom DRS
                    // stage. Recreate both raster ping-pong targets at the TAAU
                    // output extent, then normalize the compute UAV orientation
                    // through the same platform-aware TemporalAACopy shader used
                    // by native TSR.
                    var fullResolutionDescriptor = BurtRenderTargetDescriptorUtility.CreateOutputPostProcessColorDescriptor(context.Request.Camera);
                    if (context.ResourceRegistry.TryGetAllocatedRenderTexture(BurtRenderGraphResourceRegistry.TemporalAAOutputName, out var temporalAAOutputTexture) &&
                        temporalAAOutputTexture != null)
                    {
                        fullResolutionDescriptor = temporalAAOutputTexture.descriptor;
                    }

                    fullResolutionDescriptor.depthBufferBits = 0;
                    fullResolutionDescriptor.msaaSamples = 1;
                    fullResolutionDescriptor.useMipMap = false;
                    fullResolutionDescriptor.autoGenerateMips = false;
                    fullResolutionDescriptor.enableRandomWrite = false;

                    context.ResourceRegistry.SetRenderTargetDescriptor(
                        BurtRenderGraphResourceRegistry.CameraColorName,
                        fullResolutionDescriptor,
                        FilterMode.Bilinear,
                        "Burt Camera Color Post TAAU");
                    var cameraColorTarget = context.ResourceRegistry.AllocateRenderTarget(BurtRenderGraphResourceRegistry.CameraColorName);
                    context.ResourceRegistry.SetRenderTargetDescriptor(
                        BurtRenderGraphResourceRegistry.PostProcessColorName,
                        fullResolutionDescriptor,
                        FilterMode.Bilinear,
                        "Burt Post Process Color Post TAAU");
                    var postProcessColorTarget = context.ResourceRegistry.AllocateRenderTarget(BurtRenderGraphResourceRegistry.PostProcessColorName);
                    if (!cameraColorTarget.IsValid || !postProcessColorTarget.IsValid)
                    {
                        context.ExecuteAndReleaseCommandBuffer(cmd);
                        return;
                    }

                    cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, cameraColorTarget.Identifier);
                    cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.PostProcessColorTextureId, postProcessColorTarget.Identifier);
                    // XRender hands the compute output to the post stack without a
                    // fullscreen UV transform. Preserve that storage orientation
                    // exactly: a raster copy applies UNITY_UV_STARTS_AT_TOP and
                    // turns the D3D UAV result upside down before Bloom/tonemapping.
                    cmd.CopyTexture(sourceTarget.Identifier, cameraColorTarget.Identifier);
                }
                else
                {
                    DrawFinalPostProcessPass(
                        cmd,
                        context.Request.Camera,
                        material,
                        sourceTarget.Identifier,
                        context.CameraColorTarget.Identifier,
                        PostProcessShaderPass.TemporalAACopy);
                }
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class DiaphragmDepthOfFieldPass : BurtRenderPass
        {
            public override string Name => "Diaphragm Depth Of Field";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseDiaphragmDepthOfFieldPass(builder.Request, builder.Asset))
                {
                    return;
                }

                builder.ReadCameraColor();
                builder.ReadCameraDepth();
                builder.WritePostProcessColor();
                builder.ReadPostProcessColor();
                builder.WriteCameraColor();
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseDiaphragmDepthOfFieldPass(context.Request, context.Asset))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveDiaphragmDepthOfFieldSettings(context.Request, context.Asset);
                if (!settings.Enabled)
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                ExecuteDiaphragmDepthOfField(cmd, context.Request.Camera, context.CameraColorTarget, context.CameraDepthTarget, context.PostProcessColorTarget, material, settings);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class LensFlarePass : BurtRenderPass
        {
            public override string Name => "Lens Flare";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseLensFlarePass(builder.Request, builder.Asset))
                {
                    return;
                }

                builder.ReadCameraColor();
                builder.ReadCameraDepth();
                builder.WriteCameraColor();
                if (builder.ResourceRegistry != null &&
                    builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.HiZDepthName))
                {
                    builder.ReadHiZDepth();
                }
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseLensFlarePass(context.Request, context.Asset))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveLensFlareSettings(context.Asset);
                if (!settings.Enabled)
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                ExecuteLensFlare(cmd, context.Request.Camera, context.CameraColorTarget, context.CameraDepthTarget, context.HiZDepthTarget, material, settings);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class BloomBuildPassSequence
        {
            private static readonly string[] BloomTokens = { "Bloom" };
            private readonly BurtRenderPass sceneDownsamplePass = new BloomSceneDownsamplePass();
            private readonly BurtRenderPass prefilterPass = new BloomPrefilterPass();
            private readonly BurtRenderPass[] downsamplePasses = new BurtRenderPass[BurtRenderGraphResourceRegistry.BloomPyramidCount - 1];
            private readonly BurtRenderPass[] horizontalPasses = new BurtRenderPass[BurtRenderGraphResourceRegistry.BloomPyramidCount];
            private readonly BurtRenderPass[] verticalPasses = new BurtRenderPass[BurtRenderGraphResourceRegistry.BloomPyramidCount];

            public BloomBuildPassSequence()
            {
                for (var stageIndex = 1; stageIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; stageIndex++)
                {
                    downsamplePasses[stageIndex - 1] = new BloomDownsamplePass(stageIndex);
                }

                for (var mipIndex = 0; mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; mipIndex++)
                {
                    horizontalPasses[mipIndex] = new BloomGaussianHorizontalPass(mipIndex);
                    verticalPasses[mipIndex] = new BloomGaussianVerticalPass(mipIndex);
                }
            }

            public void AddToGraph(BurtRenderGraph graph, BurtRenderRequest request, BurtRenderPipelineAsset asset)
            {
                if (graph == null)
                {
                    return;
                }

                var stageCount = PostProcessUtility.ResolveBloomMipCount(request, asset);
                using var featureBlock = new BurtRenderGraphFeatureBlock(
                    graph,
                    "Bloom",
                    PostProcessPass.ShouldUseBloomPass(request, asset) && stageCount > 0,
                    BloomTokens,
                    BloomTokens);
                if (!featureBlock.IsEnabled)
                {
                    return;
                }

                graph.AddPass(sceneDownsamplePass);
                var settings = PostProcessUtility.ResolveBloomSettings(asset);
                if (!PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings))
                {
                    graph.AddPass(prefilterPass);
                }

                // Every active Gaussian level ultimately reads the common downsample chain.
                // The final downsample slot has no consumer: mip N reads downsample N-1.
                for (var passIndex = 0; passIndex < downsamplePasses.Length; passIndex++)
                {
                    graph.AddPass(downsamplePasses[passIndex]);
                }

                var firstMipIndex = PostProcessUtility.ResolveBloomFirstStageMipIndex(stageCount);
                for (var mipIndex = BurtRenderGraphResourceRegistry.BloomPyramidCount - 1; mipIndex >= firstMipIndex; mipIndex--)
                {
                    graph.AddPass(horizontalPasses[mipIndex]);
                    graph.AddPass(verticalPasses[mipIndex]);
                }
            }
        }

        internal static BloomBuildPassSequence CreateBloomBuildPasses()
        {
            return new BloomBuildPassSequence();
        }

        internal sealed class BloomSceneDownsamplePass : BurtRenderPass
        {
            public override string Name => "Bloom Scene Downsample";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!ShouldUseBloomPass(builder.Request, builder.Asset))
                {
                    return;
                }

                builder.ReadCameraColor();
                builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.BloomInputName);
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!ShouldUseBloomPass(context.Request, context.Asset) || !context.CameraColorTarget.IsValid)
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                var target = AllocateBloomInputGraphTarget(context);
                if (material == null || !target.IsValid)
                {
                    return;
                }

                ResolveActivePostProcessSize(context, out var sourceWidth, out var sourceHeight);
                var cmd = context.AcquireCommandBuffer(Name);
                cmd.SetRenderTarget(target.Identifier);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, GetBloomMipWidth(context, 0), GetBloomMipHeight(context, 0));
                SetBloomSource(cmd, context.CameraColorTarget.Identifier, sourceWidth, sourceHeight);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomDownsample), MeshTopology.Triangles, 3, 1);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class BloomPrefilterPass : BurtRenderPass
        {
            public override string Name => "Bloom Prefilter";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseBloomPass(builder.Request, builder.Asset) ||
                    PostProcessUtility.ShouldBypassBloomPrefilterThreshold(PostProcessUtility.ResolveBloomSettings(builder.Asset)))
                {
                    return;
                }

                builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.BloomInputName);
                builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.BloomSetupName);
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseBloomPass(context.Request, context.Asset))
                {
                    return;
                }

                var inputTarget = context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.BloomInputName);
                if (!inputTarget.IsValid)
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveBloomSettings(context.Asset);
                if (PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings))
                {
                    return;
                }

                var exposureSettings = PostProcessUtility.ResolvePhysicalExposureSettings(context.Request, context.Asset);
                var preExposureState = PreExposureUtility.ResolveForFrame(context.Request, context.Asset);
                var target = AllocateBloomGraphTarget(context, BurtRenderGraphResourceRegistry.BloomSetupName, 0, settings);
                if (!target.IsValid)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                PreExposureUtility.UploadGlobals(cmd, preExposureState);
                cmd.SetGlobalFloat(BloomThresholdId, settings.Threshold);
                cmd.SetGlobalFloat(BloomExposureScaleId, preExposureState.PostExposure);
                var hasExposureTexture = GpuExposureUtility.TryGetCurrentTexture(context.Request.Camera, out var exposureTexture);
                cmd.SetGlobalTexture(ExposureTextureId, hasExposureTexture ? exposureTexture : Texture2D.whiteTexture);
                cmd.SetGlobalFloat(UseExposureTextureId, hasExposureTexture ? 1f : 0f);
                cmd.SetGlobalFloat(UseBloomAlphaId, PostProcessUtility.ShouldPreserveBloomAlpha(settings, PostProcessUtility.ResolveBloomDebugView(settings)) ? 1f : 0f);
                cmd.SetRenderTarget(target.Identifier);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, GetBloomMipWidth(context, 0), GetBloomMipHeight(context, 0));
                SetBloomSource(cmd, inputTarget.Identifier, GetBloomMipWidth(context, 0), GetBloomMipHeight(context, 0));
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomPrefilter), MeshTopology.Triangles, 3, 1);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class BloomDownsamplePass : BurtRenderPass
        {
            private readonly int stageIndex;

            public BloomDownsamplePass(int stageIndex)
            {
                this.stageIndex = Mathf.Clamp(stageIndex, 1, BurtRenderGraphResourceRegistry.BloomPyramidCount);
            }

            public override string Name => "Bloom Downsample Stage " + stageIndex;

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!ShouldUseBloomPass(builder.Request, builder.Asset))
                {
                    return;
                }

                if (stageIndex == 1)
                {
                    var settings = PostProcessUtility.ResolveBloomSettings(builder.Asset);
                    builder.ReadRenderTarget(PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings)
                        ? BurtRenderGraphResourceRegistry.BloomInputName
                        : BurtRenderGraphResourceRegistry.BloomSetupName);
                }
                else
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(stageIndex - 2));
                }

                builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(stageIndex - 1));
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!ShouldUseBloomPass(context.Request, context.Asset))
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                var settings = PostProcessUtility.ResolveBloomSettings(context.Asset);
                var source = stageIndex == 1
                    ? context.ResourceRegistry.GetRenderTarget(PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings)
                        ? BurtRenderGraphResourceRegistry.BloomInputName
                        : BurtRenderGraphResourceRegistry.BloomSetupName)
                    : context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(stageIndex - 2));
                var targetName = BurtRenderGraphResourceRegistry.GetBloomDownsampleName(stageIndex - 1);
                var target = AllocateBloomGraphTarget(context, targetName, stageIndex, settings);
                if (material == null || !source.IsValid || !target.IsValid)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                cmd.SetRenderTarget(target.Identifier);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, GetBloomMipWidth(context, stageIndex), GetBloomMipHeight(context, stageIndex));
                SetBloomSource(cmd, source.Identifier, GetBloomMipWidth(context, stageIndex - 1), GetBloomMipHeight(context, stageIndex - 1));
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomDownsample), MeshTopology.Triangles, 3, 1);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class BloomGaussianHorizontalPass : BurtRenderPass
        {
            private readonly int mipIndex;

            public BloomGaussianHorizontalPass(int mipIndex)
            {
                this.mipIndex = Mathf.Clamp(mipIndex, 0, BurtRenderGraphResourceRegistry.BloomPyramidCount - 1);
            }

            public override string Name => "Bloom Gaussian H " + mipIndex;

            public override void Configure(BurtRenderPassBuilder builder)
            {
                var stageCount = PostProcessUtility.ResolveBloomMipCount(builder.Request, builder.Asset);
                if (!IsBloomGaussianMipActive(mipIndex, stageCount))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveBloomSettings(builder.Asset);
                if (mipIndex == 0)
                {
                    builder.ReadRenderTarget(PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings)
                        ? BurtRenderGraphResourceRegistry.BloomInputName
                        : BurtRenderGraphResourceRegistry.BloomSetupName);
                }
                else
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(mipIndex - 1));
                }

                builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalName(mipIndex));
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                var stageCount = PostProcessUtility.ResolveBloomMipCount(context.Request, context.Asset);
                if (!IsBloomGaussianMipActive(mipIndex, stageCount))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveBloomSettings(context.Asset);
                var material = GetPostProcessMaterial();
                var source = mipIndex == 0
                    ? context.ResourceRegistry.GetRenderTarget(PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings)
                        ? BurtRenderGraphResourceRegistry.BloomInputName
                        : BurtRenderGraphResourceRegistry.BloomSetupName)
                    : context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(mipIndex - 1));
                var target = AllocateBloomGraphTarget(context, BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalName(mipIndex), mipIndex, settings);
                if (material == null || !source.IsValid || !target.IsValid)
                {
                    return;
                }

                var width = GetBloomMipWidth(context, mipIndex);
                var height = GetBloomMipHeight(context, mipIndex);
                var stageIndex = BurtRenderGraphResourceRegistry.BloomPyramidCount - 1 - mipIndex;
                var blurRadius = PostProcessUtility.CalculateBloomBlurRadius(settings, width, stageIndex);
                var cmd = context.AcquireCommandBuffer(Name);
                cmd.SetRenderTarget(target.Identifier);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                SetBloomSource(cmd, source.Identifier, width, height);
                SetBloomGaussianKernel(cmd, blurRadius, width, height, true, Color.white);
                cmd.SetGlobalFloat(UseBloomAdditiveId, 0f);
                cmd.SetGlobalFloat(UseBloomAlphaId, PostProcessUtility.ShouldPreserveBloomAlpha(settings, PostProcessUtility.ResolveBloomDebugView(settings)) ? 1f : 0f);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomGaussian), MeshTopology.Triangles, 3, 1);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class BloomGaussianVerticalPass : BurtRenderPass
        {
            private readonly int mipIndex;

            public BloomGaussianVerticalPass(int mipIndex)
            {
                this.mipIndex = Mathf.Clamp(mipIndex, 0, BurtRenderGraphResourceRegistry.BloomPyramidCount - 1);
            }

            public override string Name => "Bloom Gaussian V " + mipIndex;

            public override void Configure(BurtRenderPassBuilder builder)
            {
                var stageCount = PostProcessUtility.ResolveBloomMipCount(builder.Request, builder.Asset);
                if (!IsBloomGaussianMipActive(mipIndex, stageCount))
                {
                    return;
                }

                builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalName(mipIndex));
                if (mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount - 1)
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex + 1));
                }

                builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex));
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                var stageCount = PostProcessUtility.ResolveBloomMipCount(context.Request, context.Asset);
                if (!IsBloomGaussianMipActive(mipIndex, stageCount))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveBloomSettings(context.Asset);
                var material = GetPostProcessMaterial();
                var source = context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalName(mipIndex));
                var target = AllocateBloomGraphTarget(context, BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex), mipIndex, settings);
                if (material == null || !source.IsValid || !target.IsValid)
                {
                    return;
                }

                var width = GetBloomMipWidth(context, mipIndex);
                var height = GetBloomMipHeight(context, mipIndex);
                var stageIndex = BurtRenderGraphResourceRegistry.BloomPyramidCount - 1 - mipIndex;
                var blurRadius = PostProcessUtility.CalculateBloomBlurRadius(settings, width, stageIndex);
                var stageTint = PostProcessUtility.CalculateBloomXRenderStageTint(settings, stageIndex);
                var useAdditive = mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount - 1;
                var additive = useAdditive
                    ? context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex + 1))
                    : source;
                var cmd = context.AcquireCommandBuffer(Name);
                cmd.SetRenderTarget(target.Identifier);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                SetBloomSource(cmd, source.Identifier, width, height);
                SetBloomGaussianKernel(cmd, blurRadius, width, height, false, stageTint);
                cmd.SetGlobalTexture(BloomAdditiveTextureId, additive.Identifier);
                cmd.SetGlobalFloat(UseBloomAdditiveId, useAdditive ? 1f : 0f);
                cmd.SetGlobalFloat(UseBloomAlphaId, PostProcessUtility.ShouldPreserveBloomAlpha(settings, PostProcessUtility.ResolveBloomDebugView(settings)) ? 1f : 0f);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomGaussian), MeshTopology.Triangles, 3, 1);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class ReleaseBloomPass : BurtRenderPass
        {
            public override string Name => "Release Bloom";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                var stageCount = PostProcessUtility.ResolveBloomMipCount(builder.Request, builder.Asset);
                if (stageCount <= 0)
                {
                    return;
                }

                builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.BloomInputName);
                var settings = PostProcessUtility.ResolveBloomSettings(builder.Asset);
                if (!PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings))
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.BloomSetupName);
                }

                for (var stageIndex = 0; stageIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; stageIndex++)
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(stageIndex));
                }

                var firstMipIndex = PostProcessUtility.ResolveBloomFirstStageMipIndex(stageCount);
                for (var mipIndex = firstMipIndex; mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; mipIndex++)
                {
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalName(mipIndex));
                    builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex));
                }
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (PostProcessUtility.ResolveBloomMipCount(context.Request, context.Asset) <= 0 || context.ResourceRegistry == null)
                {
                    return;
                }

                context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.BloomInputName);
                context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.BloomSetupName);
                for (var mipIndex = 0; mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount; mipIndex++)
                {
                    context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(mipIndex));
                    context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianHorizontalName(mipIndex));
                    context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex));
                }
            }
        }

        internal sealed class SubpixelMorphologicalAAPass : BurtRenderPass
        {
            public override string Name => "SMAA";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseSubpixelMorphologicalAAPass(builder.Request, builder.Asset))
                {
                    return;
                }

                builder.ReadCameraColor();
                builder.WritePostProcessColor();
                builder.ReadPostProcessColor();
                builder.WriteCameraColor();
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseSubpixelMorphologicalAAPass(context.Request, context.Asset))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveSubpixelMorphologicalAASettings(context.Asset);
                if (!settings.Enabled || !context.CameraColorTarget.IsValid || !context.PostProcessColorTarget.IsValid)
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                if (ExecuteSMAA(cmd, context, material, context.CameraColorTarget.Identifier, context.PostProcessColorTarget.Identifier, settings))
                {
                    DrawFinalPostProcessPass(cmd, context, material, context.PostProcessColorTarget.Identifier, context.CameraColorTarget.Identifier, PostProcessShaderPass.PlainCopy);
                }

                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class FastApproximateAAPass : BurtRenderPass
        {
            public override string Name => "FXAA";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseFastApproximateAAPass(builder.Request, builder.Asset))
                {
                    return;
                }

                builder.ReadCameraColor();
                builder.WritePostProcessColor();
                builder.ReadPostProcessColor();
                builder.WriteCameraColor();
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseFastApproximateAAPass(context.Request, context.Asset))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveFastApproximateAASettings(context.Asset);
                if (!settings.Enabled || !context.CameraColorTarget.IsValid || !context.PostProcessColorTarget.IsValid)
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                SetFXAAGlobals(cmd, settings);
                DrawFinalPostProcessPass(cmd, context, material, context.CameraColorTarget.Identifier, context.PostProcessColorTarget.Identifier, PostProcessShaderPass.FXAA);
                DrawFinalPostProcessPass(cmd, context, material, context.PostProcessColorTarget.Identifier, context.CameraColorTarget.Identifier, PostProcessShaderPass.PlainCopy);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        internal sealed class RCASPass : BurtRenderPass
        {
            public override string Name => "RCAS";

            public override void Configure(BurtRenderPassBuilder builder)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset) ||
                    !ShouldUseRCASPass(builder.Request, builder.Asset))
                {
                    return;
                }

                builder.ReadCameraColor();
                builder.WritePostProcessColor();
                builder.ReadPostProcessColor();
                builder.WriteCameraColor();
            }

            public override void Execute(BurtRenderGraphContext context)
            {
                if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset) ||
                    !ShouldUseRCASPass(context.Request, context.Asset))
                {
                    return;
                }

                var settings = PostProcessUtility.ResolveRCASSettings(context.Asset);
                if (!settings.Enabled || !context.CameraColorTarget.IsValid || !context.PostProcessColorTarget.IsValid)
                {
                    return;
                }

                var material = GetPostProcessMaterial();
                if (material == null)
                {
                    return;
                }

                var cmd = context.AcquireCommandBuffer(Name);
                SetRCASGlobals(cmd, settings);
                DrawFinalPostProcessPass(cmd, context, material, context.CameraColorTarget.Identifier, context.PostProcessColorTarget.Identifier, PostProcessShaderPass.RCAS);
                DrawFinalPostProcessPass(cmd, context, material, context.PostProcessColorTarget.Identifier, context.CameraColorTarget.Identifier, PostProcessShaderPass.PlainCopy);
                context.ExecuteAndReleaseCommandBuffer(cmd);
            }
        }

        private static void SetVignetteGlobals(CommandBuffer cmd, VignetteSettings settings)
        {
            var edgeSoftness = Mathf.Max(settings.EdgeSoftness, 0.01f);
            var edgeWidth = 1f - (1f - settings.EdgeWidth) / edgeSoftness;
            cmd.SetGlobalFloat(UseVignetteId, settings.Enabled ? 1f : 0f);
            cmd.SetGlobalColor(VignetteColorId, settings.Color);
            cmd.SetGlobalVector(VignetteParamsId, new Vector4(settings.Intensity, edgeWidth, 1f / edgeSoftness, settings.FisheyeFovDeg));
            cmd.SetGlobalVector(VignetteOptionsId, new Vector4(settings.FollowAspect ? 1f : 0f, 0f, 0f, 0f));
        }

        private static bool ExecuteDiaphragmDepthOfField(
            CommandBuffer cmd,
            Camera camera,
            BurtRenderTargetHandle cameraColorTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle postProcessColorTarget,
            Material material,
            DiaphragmDepthOfFieldSettings settings)
        {
            if (cmd == null || camera == null || material == null || !settings.Enabled)
            {
                return false;
            }

            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid || !postProcessColorTarget.IsValid)
            {
                return false;
            }

            if (!HasPostProcessShaderPass(material, PostProcessShaderPass.DiaphragmDepthOfField, "Diaphragm Depth Of Field", ref hasLoggedMissingDiaphragmDepthOfFieldPass))
            {
                return false;
            }

            cmd.SetRenderTarget(postProcessColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(SourceTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
            SetPostProcessTexelSize(cmd, camera);
            SetDiaphragmDepthOfFieldGlobals(cmd, settings);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.DiaphragmDepthOfField), MeshTopology.Triangles, 3, 1);

            DrawFinalPostProcessPass(cmd, camera, material, postProcessColorTarget.Identifier, cameraColorTarget.Identifier, PostProcessShaderPass.PlainCopy);
            return true;
        }

        private static void SetDiaphragmDepthOfFieldGlobals(CommandBuffer cmd, DiaphragmDepthOfFieldSettings settings)
        {
            cmd.SetGlobalVector(
                DiaphragmDepthOfFieldParams0Id,
                new Vector4(
                    settings.FocusDistanceMeters,
                    settings.InfinityBackgroundCocRadius,
                    settings.MinForegroundCocRadius,
                    settings.MaxBackgroundCocRadius));
            cmd.SetGlobalVector(
                DiaphragmDepthOfFieldParams1Id,
                new Vector4(
                    settings.DepthBlurExponent,
                    settings.MaxDepthBlurRadius,
                    settings.MaxRadiusPixels,
                    0f));
            cmd.SetGlobalVector(
                DiaphragmDepthOfFieldParams2Id,
                new Vector4(
                    settings.SqueezeFactor,
                    settings.SmoothGather ? 1f : 0f,
                    settings.VisualizeDOF ? 1f : 0f,
                    0f));
        }

        private static bool ExecuteLensFlare(
            CommandBuffer cmd,
            Camera camera,
            BurtRenderTargetHandle cameraColorTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle hiZDepthTarget,
            Material material,
            LensFlareSettings settings)
        {
            if (cmd == null || camera == null || material == null || !settings.Enabled)
            {
                return false;
            }

            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid)
            {
                return false;
            }

            if (!HasPostProcessShaderPass(material, PostProcessShaderPass.LensFlare, "Lens Flare", ref hasLoggedMissingLensFlarePass))
            {
                return false;
            }

            SetLensFlareGlobals(cmd, camera, cameraDepthTarget, hiZDepthTarget, settings);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.LensFlare), MeshTopology.Triangles, 3, 1);
            return true;
        }

        private static void SetLensFlareGlobals(CommandBuffer cmd, Camera camera, BurtRenderTargetHandle cameraDepthTarget, BurtRenderTargetHandle hiZDepthTarget, LensFlareSettings settings)
        {
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(LensFlareHiZDepthTextureId, hiZDepthTarget.IsValid ? hiZDepthTarget.Identifier : cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(LensFlareBokeh0TextureId, ResolveTextureOrBlack(settings.Bokeh0Texture));
            cmd.SetGlobalTexture(LensFlareBokeh1TextureId, ResolveTextureOrBlack(settings.Bokeh1Texture));
            cmd.SetGlobalTexture(LensFlareBokeh2TextureId, ResolveTextureOrBlack(settings.Bokeh2Texture));
            cmd.SetGlobalTexture(LensFlareBokeh3TextureId, ResolveTextureOrBlack(settings.Bokeh3Texture));
            cmd.SetGlobalTexture(LensFlareBokeh4TextureId, ResolveTextureOrBlack(settings.Bokeh4Texture));
            cmd.SetGlobalTexture(LensFlareLineTextureId, ResolveTextureOrBlack(settings.LineTexture));
            cmd.SetGlobalMatrix(LensFlareViewProjectionId, ResolveLensFlareViewProjection(camera));
            cmd.SetGlobalVector(LensFlareBokeh0ScaleAndPositionId, settings.Bokeh0ScaleAndPosition);
            cmd.SetGlobalVector(LensFlareBokeh1ScaleAndPositionId, settings.Bokeh1ScaleAndPosition);
            cmd.SetGlobalVector(LensFlareBokeh2ScaleAndPositionId, settings.Bokeh2ScaleAndPosition);
            cmd.SetGlobalVector(LensFlareBokeh3ScaleAndPositionId, settings.Bokeh3ScaleAndPosition);
            cmd.SetGlobalVector(LensFlareBokeh4ScaleAndPositionId, settings.Bokeh4ScaleAndPosition);
            cmd.SetGlobalVector(LensFlareBokeh0ColorId, settings.Bokeh0Color);
            cmd.SetGlobalVector(LensFlareBokeh1ColorId, settings.Bokeh1Color);
            cmd.SetGlobalVector(LensFlareBokeh2ColorId, settings.Bokeh2Color);
            cmd.SetGlobalVector(LensFlareBokeh3ColorId, settings.Bokeh3Color);
            cmd.SetGlobalVector(LensFlareBokeh4ColorId, settings.Bokeh4Color);
            cmd.SetGlobalVector(LensFlareLineParamsId, settings.LineParams);
            cmd.SetGlobalVector(LensFlareTotalParamsId, settings.TotalParams);
            cmd.SetGlobalVector(LensFlareTintColorId, settings.TotalTintColor);
            cmd.SetGlobalVector(LensFlareTextureFlags0Id, settings.TextureFlags0);
            cmd.SetGlobalVector(LensFlareTextureFlags1Id, settings.TextureFlags1);
            cmd.SetGlobalVector(LensFlareDepthParamsId, new Vector4(hiZDepthTarget.IsValid ? 1f : 0f, 1f, 0f, 0f));
        }

        private static Matrix4x4 ResolveLensFlareViewProjection(Camera camera)
        {
            if (camera == null)
            {
                return Matrix4x4.identity;
            }

            var projectionMatrix = camera.nonJitteredProjectionMatrix;
            if (projectionMatrix == Matrix4x4.zero)
            {
                projectionMatrix = camera.projectionMatrix;
            }

            return GL.GetGPUProjectionMatrix(projectionMatrix, true) * camera.worldToCameraMatrix;
        }

        private static Texture ResolveTextureOrBlack(Texture2D texture)
        {
            return texture != null ? texture : Texture2D.blackTexture;
        }

        private static bool HasPostProcessShaderPass(Material material, PostProcessShaderPass pass, string featureName, ref bool hasLoggedMissingPass)
        {
            if (material == null)
            {
                return false;
            }

            var requiredPassCount = ShaderPass(pass) + 1;
            if (material.passCount >= requiredPassCount)
            {
                hasLoggedMissingPass = false;
                return true;
            }

            if (!hasLoggedMissingPass)
            {
                Debug.LogWarning(
                    "BurtRP " + featureName + " is enabled, but shader " + PostProcessShaderName +
                    " has only " + material.passCount +
                    " passes. Expected at least " + requiredPassCount +
                    ". The effect will be skipped until the shader is reimported without errors.");
                hasLoggedMissingPass = true;
            }

            return false;
        }

        private static void SetCompositeGlobals(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            RenderTargetIdentifier source,
            RenderTargetIdentifier bloomSource,
            bool hasBloomOutput,
            BloomSettings bloomSettings,
            BloomDebugView bloomDebugView,
            int bloomMipCount,
            TonemappingMode tonemappingMode,
            float residualPostExposureMultiplier,
            PhysicalExposureSettings exposureSettings,
            TonemappingFilmSettings filmSettings,
            bool useColorAdjustments,
            ColorAdjustmentsSettings colorAdjustmentsSettings,
            ColorGradingSettings colorGradingSettings,
            bool useColorGrading)
        {
            cmd.SetGlobalTexture(SourceTextureId, source);
            cmd.SetGlobalTexture(BloomTextureId, bloomSource);
            cmd.SetGlobalFloat(UseBloomId, hasBloomOutput ? 1f : 0f);
            cmd.SetGlobalFloat(BloomIntensityId, hasBloomOutput ? 1f : 0f);
            cmd.SetGlobalFloat(UseBloomAlphaId, bloomMipCount > 0 && PostProcessUtility.ShouldPreserveBloomAlpha(bloomSettings, bloomDebugView) ? 1f : 0f);
            cmd.SetGlobalFloat(TonemappingModeId, (float)tonemappingMode);
            cmd.SetGlobalFloat(PostExposureId, residualPostExposureMultiplier);

            var hasExposureTexture = GpuExposureUtility.TryGetCurrentTexture(context.Request.Camera, out var exposureTexture);
            cmd.SetGlobalTexture(ExposureTextureId, hasExposureTexture ? exposureTexture : Texture2D.whiteTexture);
            cmd.SetGlobalFloat(UseExposureTextureId, hasExposureTexture ? 1f : 0f);
            SetLocalExposureGlobals(cmd, context, exposureSettings);

            cmd.SetGlobalFloat(FilmSlopeId, filmSettings.Slope);
            cmd.SetGlobalFloat(FilmToeId, filmSettings.Toe);
            cmd.SetGlobalFloat(FilmShoulderId, filmSettings.Shoulder);
            cmd.SetGlobalFloat(FilmBlackClipId, filmSettings.BlackClip);
            cmd.SetGlobalFloat(FilmWhiteClipId, filmSettings.WhiteClip);
            cmd.SetGlobalFloat(FilmBlueCorrectionId, filmSettings.BlueCorrection);
            cmd.SetGlobalFloat(FilmExpandGamutId, filmSettings.ExpandGamut);
            cmd.SetGlobalFloat(FilmToneCurveAmountId, filmSettings.ToneCurveAmount);

            cmd.SetGlobalFloat(UseColorAdjustmentsId, useColorAdjustments ? 1f : 0f);
            cmd.SetGlobalFloat(ColorAdjustmentsSaturationId, colorAdjustmentsSettings.Saturation);
            cmd.SetGlobalFloat(ColorAdjustmentsContrastId, colorAdjustmentsSettings.Contrast);
            cmd.SetGlobalFloat(ColorAdjustmentsGammaId, colorAdjustmentsSettings.Gamma);
            cmd.SetGlobalColor(ColorAdjustmentsColorFilterId, colorAdjustmentsSettings.ColorFilter);
            SetColorGradingGlobals(cmd, colorGradingSettings, useColorGrading);
        }

        private static void SetColorGradingGlobals(CommandBuffer cmd, ColorGradingSettings settings, bool useColorGrading)
        {
            var enabled = useColorGrading && settings.Enabled;
            cmd.SetGlobalFloat(UseColorGradingId, enabled ? 1f : 0f);
            cmd.SetGlobalFloat(UseWhiteBalanceId, enabled && settings.WhiteBalanceEnabled ? 1f : 0f);
            cmd.SetGlobalVector(WhiteBalanceParamsId, new Vector4(settings.WhiteTemp, settings.WhiteTint, (float)settings.TemperatureMode, 0f));
            cmd.SetGlobalVector(ColorGradingParamsId, new Vector4(settings.ColorGradingEnabled ? 1f : 0f, settings.Intensity, settings.HasLut ? 1f : 0f, 0f));
            cmd.SetGlobalVector(ColorGradingRangesId, new Vector4(settings.ShadowsMax, settings.HighlightsMin, settings.HighlightsMax, 0f));
            cmd.SetGlobalVector(ColorGradingGlobalSaturationId, settings.GlobalSaturation);
            cmd.SetGlobalVector(ColorGradingGlobalContrastId, settings.GlobalContrast);
            cmd.SetGlobalVector(ColorGradingGlobalGammaId, settings.GlobalGamma);
            cmd.SetGlobalVector(ColorGradingGlobalGainId, settings.GlobalGain);
            cmd.SetGlobalVector(ColorGradingGlobalOffsetId, settings.GlobalOffset);
            cmd.SetGlobalVector(ColorGradingShadowsSaturationId, settings.ShadowsSaturation);
            cmd.SetGlobalVector(ColorGradingShadowsContrastId, settings.ShadowsContrast);
            cmd.SetGlobalVector(ColorGradingShadowsGammaId, settings.ShadowsGamma);
            cmd.SetGlobalVector(ColorGradingShadowsGainId, settings.ShadowsGain);
            cmd.SetGlobalVector(ColorGradingShadowsOffsetId, settings.ShadowsOffset);
            cmd.SetGlobalVector(ColorGradingMidtonesSaturationId, settings.MidtonesSaturation);
            cmd.SetGlobalVector(ColorGradingMidtonesContrastId, settings.MidtonesContrast);
            cmd.SetGlobalVector(ColorGradingMidtonesGammaId, settings.MidtonesGamma);
            cmd.SetGlobalVector(ColorGradingMidtonesGainId, settings.MidtonesGain);
            cmd.SetGlobalVector(ColorGradingMidtonesOffsetId, settings.MidtonesOffset);
            cmd.SetGlobalVector(ColorGradingHighlightsSaturationId, settings.HighlightsSaturation);
            cmd.SetGlobalVector(ColorGradingHighlightsContrastId, settings.HighlightsContrast);
            cmd.SetGlobalVector(ColorGradingHighlightsGammaId, settings.HighlightsGamma);
            cmd.SetGlobalVector(ColorGradingHighlightsGainId, settings.HighlightsGain);
            cmd.SetGlobalVector(ColorGradingHighlightsOffsetId, settings.HighlightsOffset);
            cmd.SetGlobalTexture(ColorGradingLutId, settings.HasLut ? settings.Lut : Texture2D.blackTexture);
            cmd.SetGlobalVector(ColorGradingLutParamsId, new Vector4(settings.LutSize, 1f / Mathf.Max(1, settings.LutSize), settings.HasLut ? settings.LutContribution : 0f, 0f));
        }

        private static void DrawFinalPostProcessPass(CommandBuffer cmd, Camera camera, Material material, RenderTargetIdentifier source, RenderTargetIdentifier target, PostProcessShaderPass pass, bool flipY = false)
        {
            cmd.SetRenderTarget(target);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.SetGlobalTexture(SourceTextureId, source);
            cmd.SetGlobalFloat(PlainCopyFlipYId, flipY ? 1f : 0f);
            SetPostProcessTexelSize(cmd, camera);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(pass), MeshTopology.Triangles, 3, 1);
        }

        private static void DrawFinalPostProcessPass(CommandBuffer cmd, BurtRenderGraphContext context, Material material, RenderTargetIdentifier source, RenderTargetIdentifier target, PostProcessShaderPass pass, bool flipY = false)
        {
            ResolveActivePostProcessSize(context, out var width, out var height);
            cmd.SetRenderTarget(target);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.SetGlobalTexture(SourceTextureId, source);
            cmd.SetGlobalFloat(PlainCopyFlipYId, flipY ? 1f : 0f);
            SetPostProcessTexelSize(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(pass), MeshTopology.Triangles, 3, 1);
        }

        private static void ResolveActivePostProcessSize(BurtRenderGraphContext context, out int width, out int height)
        {
            width = 1;
            height = 1;
            if (context != null &&
                context.ResourceRegistry != null &&
                context.ResourceRegistry.TryGetAllocatedRenderTexture(BurtRenderGraphResourceRegistry.CameraColorName, out var cameraColorTexture) &&
                cameraColorTexture != null)
            {
                width = Mathf.Max(1, cameraColorTexture.width);
                height = Mathf.Max(1, cameraColorTexture.height);
                return;
            }

            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            width = Mathf.Max(1, descriptor.width);
            height = Mathf.Max(1, descriptor.height);
        }

        private static bool ExecuteSMAA(CommandBuffer cmd, BurtRenderGraphContext context, Material material, RenderTargetIdentifier source, RenderTargetIdentifier target, SubpixelMorphologicalAASettings settings)
        {
            if (!HasSMAAPasses(material))
            {
                return false;
            }

            if (!EnsureSMAATextures())
            {
                return false;
            }

            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            ResolveActivePostProcessSize(context, out var width, out var height);
            descriptor.width = width;
            descriptor.height = height;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.colorFormat = RenderTextureFormat.ARGB32;
            descriptor.sRGB = false;
            descriptor.enableRandomWrite = false;

            cmd.GetTemporaryRT(SMAAEdgeTextureId, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(SMAABlendTextureId, descriptor, FilterMode.Bilinear);

            var edgeTarget = new RenderTargetIdentifier(SMAAEdgeTextureId);
            var blendTarget = new RenderTargetIdentifier(SMAABlendTextureId);

            SetSMAAGlobals(cmd, settings);
            cmd.SetGlobalTexture(SMAAAreaTextureId, smaaAreaTexture);
            cmd.SetGlobalTexture(SMAASearchTextureId, smaaSearchTexture);
            SetPostProcessTexelSize(cmd, width, height);

            cmd.SetRenderTarget(edgeTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetGlobalTexture(SourceTextureId, source);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.SMAAEdgeDetection), MeshTopology.Triangles, 3, 1);

            cmd.SetRenderTarget(blendTarget);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetGlobalTexture(SMAAEdgeTextureId, edgeTarget);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.SMAABlendWeights), MeshTopology.Triangles, 3, 1);

            cmd.SetRenderTarget(target);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.SetGlobalTexture(SourceTextureId, source);
            cmd.SetGlobalTexture(SMAABlendTextureId, blendTarget);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.SMAANeighborhoodBlending), MeshTopology.Triangles, 3, 1);

            cmd.ReleaseTemporaryRT(SMAABlendTextureId);
            cmd.ReleaseTemporaryRT(SMAAEdgeTextureId);
            return true;
        }

        private static bool HasSMAAPasses(Material material)
        {
            if (material == null)
            {
                return false;
            }

            var requiredPassCount = ShaderPass(PostProcessShaderPass.SMAANeighborhoodBlending) + 1;
            if (material.passCount >= requiredPassCount)
            {
                hasLoggedMissingSMAAPasses = false;
                return true;
            }

            if (!hasLoggedMissingSMAAPasses)
            {
                Debug.LogWarning(
                    "BurtRP SMAA is enabled, but shader " + PostProcessShaderName +
                    " has only " + material.passCount +
                    " passes. Expected at least " + requiredPassCount +
                    ". SMAA will be skipped until the shader is reimported without errors.");
                hasLoggedMissingSMAAPasses = true;
            }

            return false;
        }

        private static bool EnsureSMAATextures()
        {
            if (smaaAreaTexture == null)
            {
                smaaAreaTexture = Resources.Load<Texture2D>(SMAAAreaTextureResourcePath);
            }

            if (smaaSearchTexture == null)
            {
                smaaSearchTexture = Resources.Load<Texture2D>(SMAASearchTextureResourcePath);
            }

            if (smaaAreaTexture != null && smaaSearchTexture != null)
            {
                hasLoggedMissingSMAATextures = false;
                return true;
            }

            if (!hasLoggedMissingSMAATextures)
            {
                Debug.LogWarning("BurtRP SMAA is enabled, but SMAA lookup textures could not be loaded from Resources/SMAA.");
                hasLoggedMissingSMAATextures = true;
            }

            return false;
        }

        private static void SetPostProcessTexelSize(CommandBuffer cmd, Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            SetPostProcessTexelSize(cmd, Mathf.Max(1, descriptor.width), Mathf.Max(1, descriptor.height));
        }

        private static void SetPostProcessTexelSize(CommandBuffer cmd, int width, int height)
        {
            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            cmd.SetGlobalVector(PostProcessTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
        }

        private static void SetRCASGlobals(CommandBuffer cmd, RCASSettings settings)
        {
            cmd.SetGlobalVector(RCASParamsId, new Vector4(settings.Sharpness, 0f, 0f, 0f));
        }

        private static void SetFXAAGlobals(CommandBuffer cmd, FastApproximateAASettings settings)
        {
            cmd.SetGlobalVector(FXAAParamsId, new Vector4(settings.Subpixel, settings.EdgeThreshold, settings.EdgeThresholdMin, 0f));
        }

        private static void SetSMAAGlobals(CommandBuffer cmd, SubpixelMorphologicalAASettings settings)
        {
            cmd.SetGlobalVector(SMAAParamsId, new Vector4(settings.Threshold, settings.BlendStrength, settings.MaxSearchSteps, 0f));
        }

        private static void InvalidateTemporalAAIfEnabled(BurtRenderGraphContext context, string reason)
        {
            var request = context != null ? context.Request : null;
            var temporalAA = request != null ? request.TemporalAA : null;
            if (temporalAA == null || !temporalAA.Enabled)
            {
                return;
            }

            BurtTemporalAAUtility.InvalidateHistory(request.Camera, reason);
        }

        private static void ExecuteTemporalAADebugUnavailable(CommandBuffer cmd, Camera camera, BurtRenderTargetHandle cameraColorTarget)
        {
            if (cmd == null || camera == null || !cameraColorTarget.IsValid)
            {
                return;
            }

            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.ClearRenderTarget(false, true, TemporalAADebugUnavailableColor);
        }

        private static void SetTemporalAAViewport(CommandBuffer cmd, int width, int height)
        {
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
        }

        private static bool ExecuteTemporalAA(
            BurtRenderGraphContext context,
            CommandBuffer cmd,
            Camera camera,
            BurtRenderTargetHandle cameraColorTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            BurtRenderTargetHandle postProcessColorTarget,
            Material material,
            BurtTemporalAARequestState temporalAA,
            bool useTemporalAADebug,
            bool useTemporalAAUpscale)
        {
            if (context == null)
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "ResolveInvalidContext");
                return false;
            }

            if (camera == null)
            {
                return false;
            }

            if (temporalAA == null || !temporalAA.Enabled)
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "ResolveDisabled");
                return false;
            }

            if (!cameraColorTarget.IsValid)
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "ResolveMissingCameraColor");
                return false;
            }

            if (!cameraDepthTarget.IsValid)
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "ResolveMissingCameraDepth");
                return false;
            }

            if (!postProcessColorTarget.IsValid)
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "ResolveMissingPostProcessColor");
                return false;
            }

            if (material == null)
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "PostProcessShaderMissing");
                return false;
            }

            var histories = BurtTemporalAAUtility.EnsureHistoryTextures(camera, out var historyValid);
            temporalAA.HistoryValid = historyValid;
            if (histories.PreviousColor == null ||
                histories.CurrentColor == null ||
                histories.Depth == null ||
                (useTemporalAAUpscale && (histories.PreviousGuide == null || histories.CurrentGuide == null)))
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "HistoryTextureUnavailable");
                return false;
            }

            var colorDescriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            var outputDescriptor = BurtRenderTargetDescriptorUtility.CreateOutputPostProcessColorDescriptor(camera);
            var width = Mathf.Max(1, colorDescriptor.width);
            var height = Mathf.Max(1, colorDescriptor.height);
            var historyWidth = Mathf.Max(1, histories.PreviousColor.width);
            var historyHeight = Mathf.Max(1, histories.PreviousColor.height);
            var depthHistoryWidth = Mathf.Max(1, histories.Depth.width);
            var depthHistoryHeight = Mathf.Max(1, histories.Depth.height);
            var cameraTargetWidth = Mathf.Max(1, useTemporalAAUpscale ? outputDescriptor.width : colorDescriptor.width);
            var cameraTargetHeight = Mathf.Max(1, useTemporalAAUpscale ? outputDescriptor.height : colorDescriptor.height);
            var useTemporalAAComputeDilateDecimate = CanUseTemporalAAComputeDilateDecimatePath(useTemporalAAUpscale);
            var useGBufferVelocity = BurtGBufferVelocityUtility.IsEnabled(context) &&
                context.GBuffer0Target.IsValid && context.GBuffer2Target.IsValid;
            var useTemporalAAUComputePath = useTemporalAAUpscale &&
                useTemporalAAComputeDilateDecimate &&
                CanUseTemporalAAUComputePath(histories);
            // XRender native TSR is DilateVelocity -> DecimateHistory ->
            // Accumulate.  The older BRP shading-rejection pair is only needed
            // by the legacy upscale fallback when the dedicated TAAU kernels
            // cannot run; executing it for native TSR is dead work and allocates
            // two full-resolution R16 targets every frame.
            var useLegacyTemporalAAUpscaleRejection = useTemporalAAUpscale &&
                useTemporalAAComputeDilateDecimate &&
                !useTemporalAAUComputePath;
            // Mode 331 is exactly the dedicated full-resolution packed output of
            // ResolveTAAUHistoryAACS. Present that resource directly instead of
            // routing it through the input-sized fragment debug path.
            var presentResolvedTAAUDebugDirectly = useTemporalAADebug &&
                useTemporalAAUComputePath &&
                BurtShadingDebugSettings.Mode == BurtShadingDebugMode.TemporalAAResolvedColor;
            // XRender writes TAAU BLEND_FACTOR from UpdateHistory itself at
            // history resolution, then scales that debug UAV to output size.
            var presentTAAUFinalBlendDebugDirectly = useTemporalAADebug &&
                useTemporalAAUComputePath &&
                BurtShadingDebugSettings.Mode == BurtShadingDebugMode.TemporalAAFeedback;
            var useTemporalAADebugTexture = useTemporalAADebug && !presentResolvedTAAUDebugDirectly;
            colorDescriptor.width = width;
            colorDescriptor.height = height;
            colorDescriptor.depthBufferBits = 0;
            colorDescriptor.msaaSamples = 1;
            colorDescriptor.useMipMap = false;
            colorDescriptor.autoGenerateMips = false;

            var resolveDescriptor = colorDescriptor;
            resolveDescriptor.enableRandomWrite = SystemInfo.supportsComputeShaders;

            // XRender converts TAAU diagnostics to output resolution before
            // presenting them. Keeping this temporary target at input size
            // makes a low-resolution debug image look like a partial TAAU
            // resolve when copied into the full-resolution output.
            var debugDescriptor = useTemporalAAUpscale ? outputDescriptor : resolveDescriptor;
            if (presentTAAUFinalBlendDebugDirectly)
            {
                debugDescriptor.width = historyWidth;
                debugDescriptor.height = historyHeight;
                debugDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            }
            debugDescriptor.depthBufferBits = 0;
            debugDescriptor.msaaSamples = 1;
            debugDescriptor.useMipMap = false;
            debugDescriptor.autoGenerateMips = false;
            debugDescriptor.enableRandomWrite = presentTAAUFinalBlendDebugDirectly;

            var scalarDescriptor = colorDescriptor;
            scalarDescriptor.colorFormat = RenderTextureFormat.RFloat;
            scalarDescriptor.sRGB = false;

            var parallaxDescriptor = colorDescriptor;
            parallaxDescriptor.sRGB = false;
            if (useTemporalAAComputeDilateDecimate)
            {
                parallaxDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
                parallaxDescriptor.enableRandomWrite = true;
            }
            else
            {
                parallaxDescriptor.colorFormat = RenderTextureFormat.RGHalf;
            }

            var historyRejectionDescriptor = colorDescriptor;
            historyRejectionDescriptor.sRGB = false;
            if (useTemporalAAComputeDilateDecimate)
            {
                historyRejectionDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
                historyRejectionDescriptor.enableRandomWrite = true;
            }

            var metadataDescriptor = colorDescriptor;
            metadataDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            metadataDescriptor.sRGB = false;

            var temporalAAUGuideDescriptor = histories.PreviousGuide != null
                ? histories.PreviousGuide.descriptor
                : colorDescriptor;
            temporalAAUGuideDescriptor.sRGB = false;
            temporalAAUGuideDescriptor.enableRandomWrite = true;

            var temporalAAURejectionDescriptor = colorDescriptor;
            temporalAAURejectionDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            temporalAAURejectionDescriptor.sRGB = false;
            temporalAAURejectionDescriptor.enableRandomWrite = true;

            var velocityDescriptor = colorDescriptor;
            velocityDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            velocityDescriptor.sRGB = false;

            var dilatedVelocityDescriptor = colorDescriptor;
            dilatedVelocityDescriptor.sRGB = false;
            if (useTemporalAAComputeDilateDecimate)
            {
                dilatedVelocityDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat;
                dilatedVelocityDescriptor.enableRandomWrite = true;
            }
            else
            {
                dilatedVelocityDescriptor.colorFormat = RenderTextureFormat.RGHalf;
            }

            var closestDepthDescriptor = scalarDescriptor;
            if (useTemporalAAComputeDilateDecimate)
            {
                closestDepthDescriptor.graphicsFormat = useTemporalAAUpscale
                    ? UnityEngine.Experimental.Rendering.GraphicsFormat.R16_UNorm
                    : UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
                closestDepthDescriptor.enableRandomWrite = true;
            }

            var dilateMaskDescriptor = colorDescriptor;
            dilateMaskDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            dilateMaskDescriptor.sRGB = false;
            dilateMaskDescriptor.enableRandomWrite = useTemporalAAComputeDilateDecimate;

            var prevUseCountDescriptor = colorDescriptor;
            prevUseCountDescriptor.colorFormat = RenderTextureFormat.RFloat;
            prevUseCountDescriptor.sRGB = false;
            prevUseCountDescriptor.useMipMap = false;
            prevUseCountDescriptor.autoGenerateMips = false;

            var prevUseCountUintDescriptor = colorDescriptor;
            prevUseCountUintDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt;
            prevUseCountUintDescriptor.sRGB = false;
            prevUseCountUintDescriptor.useMipMap = false;
            prevUseCountUintDescriptor.autoGenerateMips = false;
            prevUseCountUintDescriptor.enableRandomWrite = true;

            cmd.GetTemporaryRT(TemporalAACurrentDepthTextureId, scalarDescriptor, FilterMode.Point);
            if (!useGBufferVelocity)
            {
                cmd.GetTemporaryRT(TemporalAAVelocityTextureId, velocityDescriptor, FilterMode.Point);
            }
            cmd.GetTemporaryRT(TemporalAADilatedVelocityTextureId, dilatedVelocityDescriptor, FilterMode.Point);
            if (useTemporalAAComputeDilateDecimate)
            {
                cmd.GetTemporaryRT(TemporalAADilateMaskTextureId, dilateMaskDescriptor, FilterMode.Point);
            }
            cmd.GetTemporaryRT(TemporalAAClosestDepthTextureId, closestDepthDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAPrevUseCountTextureId, prevUseCountDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAStencilMaskTextureId, scalarDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAResponsiveMaskTextureId, scalarDescriptor, FilterMode.Point);
            if (useTemporalAAComputeDilateDecimate)
            {
                cmd.GetTemporaryRT(TemporalAAPrevUseCountUintTextureId, prevUseCountUintDescriptor, FilterMode.Point);
            }

            cmd.GetTemporaryRT(TemporalAAMetadataTextureId, metadataDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAResolveTextureId, resolveDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(TemporalAAParallaxRejectionTextureId, parallaxDescriptor, FilterMode.Bilinear);
            if (useLegacyTemporalAAUpscaleRejection)
            {
                cmd.GetTemporaryRT(TemporalAAHistoryRejectionTextureId, historyRejectionDescriptor, FilterMode.Point);
                cmd.GetTemporaryRT(TemporalAADilatedHistoryRejectionTextureId, historyRejectionDescriptor, FilterMode.Point);
            }
            if (useTemporalAAUComputePath)
            {
                cmd.GetTemporaryRT(TemporalAAUReprojectedGuideTextureId, temporalAAUGuideDescriptor, FilterMode.Bilinear);
                cmd.GetTemporaryRT(TemporalAAUShadingRejectionTextureId, temporalAAURejectionDescriptor, FilterMode.Point);
                cmd.GetTemporaryRT(TemporalAAUDilatedShadingRejectionTextureId, temporalAAURejectionDescriptor, FilterMode.Point);
            }
            if (useTemporalAADebugTexture)
            {
                cmd.GetTemporaryRT(TemporalAADebugTextureId, debugDescriptor, FilterMode.Bilinear);
            }

            var currentDepth = new RenderTargetIdentifier(TemporalAACurrentDepthTextureId);
            var velocity = new RenderTargetIdentifier(TemporalAAVelocityTextureId);
            var dilatedVelocity = new RenderTargetIdentifier(TemporalAADilatedVelocityTextureId);
            var dilateMask = new RenderTargetIdentifier(TemporalAADilateMaskTextureId);
            var closestDepth = new RenderTargetIdentifier(TemporalAAClosestDepthTextureId);
            var prevUseCount = new RenderTargetIdentifier(TemporalAAPrevUseCountTextureId);
            var prevUseCountUint = new RenderTargetIdentifier(TemporalAAPrevUseCountUintTextureId);
            var stencilMask = new RenderTargetIdentifier(TemporalAAStencilMaskTextureId);
            var responsiveMask = new RenderTargetIdentifier(TemporalAAResponsiveMaskTextureId);
            var metadata = new RenderTargetIdentifier(TemporalAAMetadataTextureId);
            var resolveTarget = new RenderTargetIdentifier(TemporalAAResolveTextureId);
            var parallaxRejection = new RenderTargetIdentifier(TemporalAAParallaxRejectionTextureId);
            var historyRejection = new RenderTargetIdentifier(TemporalAAHistoryRejectionTextureId);
            var dilatedHistoryRejection = new RenderTargetIdentifier(TemporalAADilatedHistoryRejectionTextureId);
            var temporalAAUReprojectedGuide = new RenderTargetIdentifier(TemporalAAUReprojectedGuideTextureId);
            var temporalAAUHistoryGuide = new RenderTargetIdentifier(histories.CurrentGuide);
            var temporalAAUShadingRejection = new RenderTargetIdentifier(TemporalAAUShadingRejectionTextureId);
            var temporalAAUDilatedShadingRejection = new RenderTargetIdentifier(TemporalAAUDilatedShadingRejectionTextureId);
            var temporalAAUUpdatedHistory = new RenderTargetIdentifier(histories.CurrentColor);
            // Match XRender's ownership hand-off: the compute resolve writes the
            // graph's full-resolution TAAU output directly. A second raster copy
            // would inherit the low-resolution camera render area and can clip the
            // output back to the input extent.
            var temporalAAUOutput = postProcessColorTarget.Identifier;
            var debugTarget = new RenderTargetIdentifier(TemporalAADebugTextureId);
            var blackTexture = new RenderTargetIdentifier(Texture2D.blackTexture);
            var hasTaaGBuffer = context.GBuffer0Target.IsValid && context.GBuffer2Target.IsValid;

            cmd.SetGlobalTexture(TemporalAAStencilMaskTextureId, stencilMask);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
            BurtDeferredStencilTextureUtility.BindGlobal(cmd, cameraDepthTarget, camera);
            cmd.SetGlobalVector(TemporalAAStencilTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalVector(TemporalAAHistoryTexelSizeId, new Vector4(1f / historyWidth, 1f / historyHeight, historyWidth, historyHeight));
            cmd.SetGlobalVector(TemporalAADepthHistoryTexelSizeId, new Vector4(1f / depthHistoryWidth, 1f / depthHistoryHeight, depthHistoryWidth, depthHistoryHeight));
            cmd.SetGlobalVector(TemporalAAUpscaleTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            var temporalAAUpscaleParams = new Vector4(cameraTargetWidth, cameraTargetHeight, cameraTargetWidth / (float)width, cameraTargetHeight / (float)height);
            cmd.SetGlobalVector(TemporalAAUpscaleParamsId, temporalAAUpscaleParams);
            SetTemporalAAGlobals(cmd, temporalAA, width, height, historyValid);

            cmd.SetRenderTarget(currentDepth);
            SetTemporalAAViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACurrentDepth), MeshTopology.Triangles, 3, 1);

            if (!useGBufferVelocity)
            {
                cmd.SetRenderTarget(velocity);
                SetTemporalAAViewport(cmd, width, height);
                cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACameraVelocity), MeshTopology.Triangles, 3, 1);
            }

            cmd.SetRenderTarget(responsiveMask);
            SetTemporalAAViewport(cmd, width, height);
            cmd.ClearRenderTarget(false, true, Color.clear);

            var drewObjectMotionVectors = DrawTemporalAAObjectMotionVectors(
                context,
                cmd,
                camera,
                velocity,
                cameraDepthTarget,
                width,
                height,
                bindCameraDepthStencil: true,
                // XRender keeps deferred velocity owned by GBuffer. BRP supplements only
                // explicitly forward-only opaque surfaces; the pure-forward renderer still
                // draws both motion-vector tags because it has no GBuffer velocity owner.
                includeOpaque: true,
                deferredGBufferOwnsOpaqueVelocity: useGBufferVelocity);
            if (!useGBufferVelocity)
            {
                drewObjectMotionVectors |= DrawTemporalAAMultipassFurMotionVectors(context, cmd, velocity, cameraDepthTarget, width, height);
            }
            temporalAA.ObjectMotionVectorPassDrawn = drewObjectMotionVectors;
            temporalAA.VelocityMode = useGBufferVelocity || drewObjectMotionVectors
                ? BurtTemporalAAVelocityMode.CameraAndObject
                : BurtTemporalAAVelocityMode.CameraOnly;

            DrawTemporalAAResponsiveAAMask(context, cmd, camera, responsiveMask, cameraDepthTarget, width, height);

            cmd.SetRenderTarget(stencilMask);
            SetTemporalAAViewport(cmd, width, height);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetGlobalTexture(TemporalAAVelocityTextureId, velocity);
            cmd.SetGlobalTexture(TemporalAAResponsiveMaskTextureId, responsiveMask);
            // The stencil-mask pass merges the native S8 bits with BRP's velocity
            // ownership payload. Some Unity backends expose a sampleable S8 view
            // even after later passes have lost bit 8, so availability alone cannot
            // decide whether the payload is needed. Responsive-AA ownership still
            // clears bit 8 in the shader, matching XRender's Ref=16/WriteMask=24.
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAABuildStencilMask), MeshTopology.Triangles, 3, 1);

            cmd.SetGlobalTexture(TemporalAAStencilMaskTextureId, stencilMask);

            var historyDepthForDecimate = historyValid ? new RenderTargetIdentifier(histories.Depth) : currentDepth;
            if (useTemporalAAComputeDilateDecimate)
            {
                ExecuteTemporalAADilateVelocityCompute(
                    cmd,
                    currentDepth,
                    velocity,
                    stencilMask,
                    dilatedVelocity,
                    dilateMask,
                    closestDepth,
                    prevUseCountUint,
                    temporalAA,
                    useTemporalAAUpscale,
                    width,
                    height,
                    width,
                    height);

                cmd.SetRenderTarget(prevUseCount);
                SetTemporalAAViewport(cmd, width, height);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAABuildPrevUseCount), MeshTopology.Triangles, 3, 1);

                ExecuteTemporalAADecimateHistoryCompute(
                    cmd,
                    dilatedVelocity,
                    dilateMask,
                    prevUseCountUint,
                    closestDepth,
                    historyDepthForDecimate,
                    parallaxRejection,
                    width,
                    height,
                    historyValid ? depthHistoryWidth : width,
                    historyValid ? depthHistoryHeight : height,
                    useTemporalAAUpscale);
            }
            else
            {
                cmd.SetRenderTarget(new[] { dilatedVelocity, closestDepth }, new RenderTargetIdentifier(BuiltinRenderTextureType.None));
                SetTemporalAAViewport(cmd, width, height);
                cmd.SetGlobalTexture(TemporalAAVelocityTextureId, velocity);
                cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
                cmd.SetGlobalTexture(TemporalAAStencilMaskTextureId, stencilMask);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAVelocityDilation), MeshTopology.Triangles, 3, 1);

                cmd.SetRenderTarget(prevUseCount);
                SetTemporalAAViewport(cmd, width, height);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAABuildPrevUseCount), MeshTopology.Triangles, 3, 1);

                cmd.SetRenderTarget(parallaxRejection);
                SetTemporalAAViewport(cmd, width, height);
                cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
                cmd.SetGlobalTexture(TemporalAAPrevUseCountTextureId, prevUseCount);
                cmd.SetGlobalTexture(TemporalAAClosestDepthTextureId, closestDepth);
                cmd.SetGlobalTexture(TemporalAADepthHistoryTextureId, historyDepthForDecimate);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAADecimateHistory), MeshTopology.Triangles, 3, 1);
            }

            cmd.SetRenderTarget(metadata);
            SetTemporalAAViewport(cmd, width, height);
            cmd.SetGlobalTexture(TemporalAARawVelocityTextureId, velocity);
            cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
            cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
            cmd.SetGlobalTexture(TemporalAAClosestDepthTextureId, closestDepth);
            cmd.SetGlobalTexture(TemporalAAParallaxRejectionTextureId, parallaxRejection);
            cmd.SetGlobalTexture(TemporalAAStencilMaskTextureId, stencilMask);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAMetadata), MeshTopology.Triangles, 3, 1);

            if (useLegacyTemporalAAUpscaleRejection)
            {
                ExecuteTemporalAAHistoryRejectionCompute(
                    cmd,
                    cameraColorTarget.Identifier,
                    historyValid ? new RenderTargetIdentifier(histories.PreviousColor) : cameraColorTarget.Identifier,
                    dilatedVelocity,
                    parallaxRejection,
                    metadata,
                    stencilMask,
                    historyRejection,
                    dilatedHistoryRejection,
                    temporalAA.HistoryExposureCorrection,
                    width,
                    height);
                cmd.SetGlobalTexture(TemporalAADilatedHistoryRejectionTextureId, dilatedHistoryRejection);
                cmd.SetGlobalFloat(TemporalAAHasDilatedHistoryRejectionId, 1f);
            }
            else
            {
                cmd.SetGlobalFloat(TemporalAAHasDilatedHistoryRejectionId, 0f);
            }

            // TAAU history is larger than the input, so even on a reset frame bind the
            // correctly-sized persistent texture and let the history-valid flag bypass it.
            var historyColorTarget = (historyValid || useTemporalAAUpscale)
                ? new RenderTargetIdentifier(histories.PreviousColor)
                : cameraColorTarget.Identifier;
            var historyDepthTarget = historyValid ? new RenderTargetIdentifier(histories.Depth) : currentDepth;
            cmd.SetGlobalTexture(SourceTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(TemporalAAHistoryTextureId, historyColorTarget);
            cmd.SetGlobalTexture(TemporalAADepthHistoryTextureId, historyDepthTarget);
            cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
            cmd.SetGlobalTexture(TemporalAAClosestDepthTextureId, closestDepth);
            cmd.SetGlobalTexture(TemporalAARawVelocityTextureId, velocity);
            cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
            cmd.SetGlobalTexture(TemporalAAPrevUseCountTextureId, prevUseCount);
            cmd.SetGlobalTexture(TemporalAAMetadataTextureId, metadata);
            cmd.SetGlobalTexture(TemporalAAParallaxRejectionTextureId, parallaxRejection);
            cmd.SetGlobalFloat(TemporalAAHasGBufferId, hasTaaGBuffer ? 1f : 0f);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.GBuffer0Id, hasTaaGBuffer ? context.GBuffer0Target.Identifier : blackTexture);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.GBuffer2Id, hasTaaGBuffer ? context.GBuffer2Target.Identifier : blackTexture);
            cmd.SetGlobalFloat(ShadingDebugEnabledId, 0f);

            var resolvedHighResolutionHistory = useTemporalAAUComputePath &&
                TryExecuteTemporalAAUPipeline(
                    cmd,
                    cameraColorTarget.Identifier,
                    historyColorTarget,
                    new RenderTargetIdentifier(histories.PreviousGuide),
                    dilatedVelocity,
                    dilateMask,
                    stencilMask,
                    temporalAAUReprojectedGuide,
                    temporalAAUHistoryGuide,
                    temporalAAUShadingRejection,
                    temporalAAUDilatedShadingRejection,
                    temporalAAUUpdatedHistory,
                    temporalAAUOutput,
                    debugTarget,
                    presentTAAUFinalBlendDebugDirectly,
                    temporalAA,
                    historyValid,
                    width,
                    height,
                    cameraTargetWidth,
                    cameraTargetHeight,
                    historyWidth,
                    historyHeight);

            if (!resolvedHighResolutionHistory &&
                (!useTemporalAAComputeDilateDecimate || !TryExecuteTemporalAAResolveCompute(
                    cmd,
                    cameraColorTarget.Identifier,
                    historyColorTarget,
                    historyDepthTarget,
                    dilatedVelocity,
                    parallaxRejection,
                    useLegacyTemporalAAUpscaleRejection ? dilatedHistoryRejection : parallaxRejection,
                    metadata,
                    stencilMask,
                    resolveTarget,
                    temporalAA,
                    historyValid,
                    width,
                    height,
                    width,
                    height)))
            {
                cmd.SetRenderTarget(resolveTarget);
                SetTemporalAAViewport(cmd, width, height);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAResolve), MeshTopology.Triangles, 3, 1);
            }

            if (useTemporalAADebugTexture && !presentTAAUFinalBlendDebugDirectly)
            {
                cmd.SetRenderTarget(debugTarget);
                SetTemporalAAViewport(
                    cmd,
                    useTemporalAAUpscale ? cameraTargetWidth : width,
                    useTemporalAAUpscale ? cameraTargetHeight : height);
                if (resolvedHighResolutionHistory &&
                    BurtShadingDebugSettings.Mode == BurtShadingDebugMode.TemporalAAResolvedColor)
                {
                    // The TAAU result is produced by ResolveTAAUHistoryAACS,
                    // not by the native-resolution fragment resolve below.
                    // Copy the actual compute output so mode 331 remains an
                    // honest resolved-color diagnostic at every render scale.
                    DisablePostProcessEffects(cmd);
                    cmd.SetGlobalTexture(SourceTextureId, temporalAAUOutput);
                    cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACopy), MeshTopology.Triangles, 3, 1);
                }
                else
                {
                    cmd.SetGlobalFloat(ShadingDebugEnabledId, 1f);
                    cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAResolve), MeshTopology.Triangles, 3, 1);
                    cmd.SetGlobalFloat(ShadingDebugEnabledId, 0f);
                }
            }

            cmd.SetRenderTarget(histories.Depth);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, Mathf.Max(1, histories.Depth.width), Mathf.Max(1, histories.Depth.height));
            // XRender seeds native TSR's first depth history from scene depth
            // directly, then stores the dilated closest depth on later frames.
            // TAAU always owns its dedicated closest-depth history path.
            var depthHistorySource = !historyValid && !useTemporalAAUpscale
                ? currentDepth
                : closestDepth;
            cmd.SetGlobalTexture(TemporalAAClosestDepthTextureId, depthHistorySource);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAClosestDepthCopy), MeshTopology.Triangles, 3, 1);

            if (!resolvedHighResolutionHistory)
            {
                cmd.SetRenderTarget(histories.CurrentColor);
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, historyWidth, historyHeight);
                cmd.SetGlobalTexture(SourceTextureId, resolveTarget);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACopy), MeshTopology.Triangles, 3, 1);
            }
            // The TAAU compute path writes directly into the current side of the
            // persistent ping-pong pair. MarkHistoryValid swaps the pair here.
            BurtTemporalAAUtility.MarkHistoryValid(camera);

            if (!resolvedHighResolutionHistory || useTemporalAADebug)
            {
                cmd.SetRenderTarget(postProcessColorTarget.Identifier);
                if (useTemporalAAUpscale)
                {
                    BurtRenderTargetDescriptorUtility.SetOutputTargetViewport(cmd, camera);
                }
                else
                {
                    BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
                }
                DisablePostProcessEffects(cmd);
                cmd.SetGlobalTexture(SourceTextureId, useTemporalAADebug ? debugTarget : resolveTarget);
                cmd.SetGlobalTexture(TemporalAAUpscaleCurrentTextureId, cameraColorTarget.Identifier);
                var postProcessCopyPass = useTemporalAADebug
                    ? PostProcessShaderPass.TemporalAACopy
                    : (useTemporalAAUpscale ? PostProcessShaderPass.TemporalAAUpscale : PostProcessShaderPass.TemporalAACopy);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(postProcessCopyPass), MeshTopology.Triangles, 3, 1);
            }

            cmd.SetGlobalFloat(ShadingDebugEnabledId, BurtShadingDebugSettings.IsDebugging ? 1f : 0f);
            cmd.SetGlobalFloat(TemporalAAHasDilatedHistoryRejectionId, 0f);

            if (useTemporalAADebugTexture)
            {
                cmd.ReleaseTemporaryRT(TemporalAADebugTextureId);
            }

            cmd.ReleaseTemporaryRT(TemporalAAParallaxRejectionTextureId);
            if (useLegacyTemporalAAUpscaleRejection)
            {
                cmd.ReleaseTemporaryRT(TemporalAADilatedHistoryRejectionTextureId);
                cmd.ReleaseTemporaryRT(TemporalAAHistoryRejectionTextureId);
            }
            if (useTemporalAAUComputePath)
            {
                cmd.ReleaseTemporaryRT(TemporalAAUDilatedShadingRejectionTextureId);
                cmd.ReleaseTemporaryRT(TemporalAAUShadingRejectionTextureId);
                cmd.ReleaseTemporaryRT(TemporalAAUReprojectedGuideTextureId);
            }
            cmd.ReleaseTemporaryRT(TemporalAAResolveTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAMetadataTextureId);
            if (useTemporalAAComputeDilateDecimate)
            {
                cmd.ReleaseTemporaryRT(TemporalAAPrevUseCountUintTextureId);
            }

            cmd.ReleaseTemporaryRT(TemporalAAPrevUseCountTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAStencilMaskTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAResponsiveMaskTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAClosestDepthTextureId);
            if (useTemporalAAComputeDilateDecimate)
            {
                cmd.ReleaseTemporaryRT(TemporalAADilateMaskTextureId);
            }
            cmd.ReleaseTemporaryRT(TemporalAADilatedVelocityTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAVelocityTextureId);
            cmd.ReleaseTemporaryRT(TemporalAACurrentDepthTextureId);
            return true;
        }

        private static void SetTemporalAAGlobals(CommandBuffer cmd, BurtTemporalAARequestState temporalAA, int width, int height, bool historyValid)
        {
            cmd.SetGlobalMatrix(TemporalAAPreviousViewProjectionId, temporalAA.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAAPreviousNonJitteredViewProjectionId, temporalAA.PreviousNonJitteredViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAACurrentViewProjectionId, temporalAA.CurrentViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAACurrentNonJitteredViewProjectionId, temporalAA.CurrentNonJitteredViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAAInverseCurrentViewProjectionId, temporalAA.InverseCurrentViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAAInverseCurrentNonJitteredViewProjectionId, temporalAA.InverseCurrentNonJitteredViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAAClipToPreviousClipId, temporalAA.ClipToPreviousClipMatrix);
            cmd.SetGlobalFloat(TemporalAAPreviousRenderDeltaTimeId, temporalAA.PreviousRenderDeltaTime);
            cmd.SetGlobalVector(TemporalAAJitterId, new Vector4(temporalAA.Jitter.x, temporalAA.Jitter.y, temporalAA.JitterPixels.x, temporalAA.JitterPixels.y));
            cmd.SetGlobalVector(TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetGlobalVector(TemporalAAParamsId, new Vector4(0f, 0f, historyValid ? 1f : 0f, temporalAA.FrameIndex));
            cmd.SetGlobalVector(TemporalAAParams2Id, new Vector4(temporalAA.Settings.Sharpness, temporalAA.Settings.JitterScale, 0f, 0f));
            cmd.SetGlobalVector(TemporalAADepthParamsId, CalculateTemporalAADepthParams(temporalAA, width));
            cmd.SetGlobalVector(TemporalAAResponsiveParamsId, new Vector4(0f, temporalAA.Settings.UntrustedMotionFeedbackScale, 0f, 0f));
            cmd.SetGlobalVector(TemporalAAEdgeParamsId, new Vector4(temporalAA.Settings.MotionEdgeResponsiveStrength, temporalAA.Settings.DepthEdgeResponsiveStrength, 0f, 0f));
            cmd.SetGlobalFloat(TemporalAAHistoryExposureCorrectionId, temporalAA.HistoryExposureCorrection);

            ComputeTemporalAACurrentSampleWeights(temporalAA.Jitter, out var weights0, out var weights1, out var weights2);
            cmd.SetGlobalVector(TemporalAACurrentSampleWeights0Id, weights0);
            cmd.SetGlobalVector(TemporalAACurrentSampleWeights1Id, weights1);
            cmd.SetGlobalVector(TemporalAACurrentSampleWeights2Id, weights2);
        }

        private static Vector4 CalculateTemporalAADepthParams(BurtTemporalAARequestState temporalAA, int width)
        {
            var projection = temporalAA != null ? temporalAA.NonJitteredProjectionMatrix : Matrix4x4.identity;
            var inverseProjection = GL.GetGPUProjectionMatrix(projection, true).inverse;
            var depthPixelRadiusScale = Mathf.Abs(inverseProjection.m00) / Mathf.Max(1, width);
            return new Vector4(depthPixelRadiusScale, 0f, 0f, 0f);
        }

        private static bool CanUseTemporalAAComputeDilateDecimatePath(bool useTemporalAAUpscale)
        {
            var closestDepthFormat = useTemporalAAUpscale
                ? UnityEngine.Experimental.Rendering.GraphicsFormat.R16_UNorm
                : UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
            if (!SystemInfo.supportsComputeShaders ||
                !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R32_UInt, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore) ||
                !SystemInfo.IsFormatSupported(closestDepthFormat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore) ||
                !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16_SFloat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore) ||
                !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore))
            {
                return false;
            }

            var shader = GetTemporalAAComputeShader();
            int clearKernel;
            int dilateKernel;
            int decimateKernel;
            int rejectionKernel;
            int dilateRejectionKernel;
            return shader != null &&
                TryFindTemporalAAComputeKernel(shader, "ClearPrevUseCountAACS", out clearKernel) &&
                TryFindTemporalAAComputeKernel(shader, "DilateVelocityAACS", out dilateKernel) &&
                TryFindTemporalAAComputeKernel(shader, "DecimateHistoryAACS", out decimateKernel) &&
                TryFindTemporalAAComputeKernel(shader, "BuildHistoryRejectionAACS", out rejectionKernel) &&
                TryFindTemporalAAComputeKernel(shader, "DilateHistoryRejectionAACS", out dilateRejectionKernel);
        }

        private static void ExecuteTemporalAADilateVelocityCompute(
            CommandBuffer cmd,
            RenderTargetIdentifier currentDepth,
            RenderTargetIdentifier rawVelocity,
            RenderTargetIdentifier stencilMask,
            RenderTargetIdentifier dilatedVelocity,
            RenderTargetIdentifier dilateMask,
            RenderTargetIdentifier closestDepth,
            RenderTargetIdentifier prevUseCountUint,
            BurtTemporalAARequestState temporalAA,
            bool useTemporalAAUpscale,
            int width,
            int height,
            int stencilWidth,
            int stencilHeight)
        {
            var shader = GetTemporalAAComputeShader();
            var clearKernel = shader.FindKernel("ClearPrevUseCountAACS");
            cmd.SetComputeTextureParam(shader, clearKernel, TemporalAAPrevUseCountOutputTextureId, prevUseCountUint);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.DispatchCompute(shader, clearKernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);

            var kernel = shader.FindKernel("DilateVelocityAACS");
            cmd.SetComputeTextureParam(shader, kernel, TemporalAACurrentDepthTextureId, currentDepth);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAARawVelocityTextureId, rawVelocity);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAStencilMaskTextureId, stencilMask);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAADilatedVelocityTextureId, dilatedVelocity);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAADilateMaskOutputTextureId, dilateMask);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAClosestDepthOutputTextureId, closestDepth);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAPrevUseCountOutputTextureId, prevUseCountUint);
            cmd.SetComputeMatrixParam(shader, TemporalAAInverseCurrentNonJitteredViewProjectionId, temporalAA != null ? temporalAA.InverseCurrentNonJitteredViewProjectionMatrix : Matrix4x4.identity);
            cmd.SetComputeMatrixParam(shader, TemporalAAPreviousNonJitteredViewProjectionId, temporalAA != null ? temporalAA.PreviousNonJitteredViewProjectionMatrix : Matrix4x4.identity);
            cmd.SetComputeMatrixParam(shader, TemporalAAClipToPreviousClipId, temporalAA != null ? temporalAA.ClipToPreviousClipMatrix : Matrix4x4.identity);
            var jitter = temporalAA != null ? temporalAA.Jitter : Vector2.zero;
            cmd.SetComputeVectorParam(shader, TemporalAAJitterId, new Vector4(jitter.x, jitter.y, temporalAA != null ? temporalAA.JitterPixels.x : 0f, temporalAA != null ? temporalAA.JitterPixels.y : 0f));
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetComputeVectorParam(shader, TemporalAAStencilTexelSizeId, new Vector4(1f / Mathf.Max(1, stencilWidth), 1f / Mathf.Max(1, stencilHeight), stencilWidth, stencilHeight));
            cmd.SetComputeVectorParam(shader, TemporalAADepthParamsId, CalculateTemporalAADepthParams(temporalAA, width));
            cmd.SetComputeFloatParam(shader, TemporalAADilateModeId, useTemporalAAUpscale ? 1f : 0f);
            cmd.DispatchCompute(shader, kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);
        }

        private static void ExecuteTemporalAADecimateHistoryCompute(
            CommandBuffer cmd,
            RenderTargetIdentifier dilatedVelocity,
            RenderTargetIdentifier dilateMask,
            RenderTargetIdentifier prevUseCountUint,
            RenderTargetIdentifier closestDepth,
            RenderTargetIdentifier historyDepth,
            RenderTargetIdentifier parallaxRejection,
            int width,
            int height,
            int depthHistoryWidth,
            int depthHistoryHeight,
            bool useTemporalAAUpscale)
        {
            var shader = GetTemporalAAComputeShader();
            var kernel = shader.FindKernel("DecimateHistoryAACS");
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAVelocityTextureId, dilatedVelocity);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAADilateMaskOutputTextureId, dilateMask);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAPrevUseCountUintTextureId, prevUseCountUint);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAClosestDepthTextureId, closestDepth);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAADepthHistoryTextureId, historyDepth);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAParallaxRejectionOutputTextureId, parallaxRejection);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetComputeVectorParam(shader, TemporalAADepthHistoryTexelSizeId, new Vector4(
                1f / Mathf.Max(1, depthHistoryWidth),
                1f / Mathf.Max(1, depthHistoryHeight),
                depthHistoryWidth,
                depthHistoryHeight));
            cmd.SetComputeFloatParam(shader, TemporalAADecimateModeId, useTemporalAAUpscale ? 1f : 0f);
            cmd.DispatchCompute(shader, kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);
        }

        private static void ExecuteTemporalAAHistoryRejectionCompute(
            CommandBuffer cmd,
            RenderTargetIdentifier source,
            RenderTargetIdentifier history,
            RenderTargetIdentifier dilatedVelocity,
            RenderTargetIdentifier parallaxRejection,
            RenderTargetIdentifier metadata,
            RenderTargetIdentifier stencilMask,
            RenderTargetIdentifier historyRejection,
            RenderTargetIdentifier dilatedHistoryRejection,
            float historyExposureCorrection,
            int width,
            int height)
        {
            var shader = GetTemporalAAComputeShader();
            var buildKernel = shader.FindKernel("BuildHistoryRejectionAACS");
            cmd.SetComputeTextureParam(shader, buildKernel, SourceTextureId, source);
            cmd.SetComputeTextureParam(shader, buildKernel, TemporalAAHistoryTextureId, history);
            cmd.SetComputeTextureParam(shader, buildKernel, TemporalAAVelocityTextureId, dilatedVelocity);
            cmd.SetComputeTextureParam(shader, buildKernel, TemporalAAParallaxRejectionTextureId, parallaxRejection);
            cmd.SetComputeTextureParam(shader, buildKernel, TemporalAAMetadataTextureId, metadata);
            cmd.SetComputeTextureParam(shader, buildKernel, TemporalAAStencilMaskTextureId, stencilMask);
            cmd.SetComputeTextureParam(shader, buildKernel, TemporalAAHistoryRejectionOutputTextureId, historyRejection);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetComputeVectorParam(shader, TemporalAAStencilTexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetComputeFloatParam(shader, TemporalAAHistoryExposureCorrectionId, historyExposureCorrection);
            cmd.DispatchCompute(shader, buildKernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);

            var dilateKernel = shader.FindKernel("DilateHistoryRejectionAACS");
            cmd.SetComputeTextureParam(shader, dilateKernel, TemporalAAHistoryRejectionTextureId, historyRejection);
            cmd.SetComputeTextureParam(shader, dilateKernel, TemporalAAMetadataTextureId, metadata);
            cmd.SetComputeTextureParam(shader, dilateKernel, TemporalAAStencilMaskTextureId, stencilMask);
            cmd.SetComputeTextureParam(shader, dilateKernel, TemporalAADilatedHistoryRejectionOutputTextureId, dilatedHistoryRejection);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetComputeVectorParam(shader, TemporalAAStencilTexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.DispatchCompute(shader, dilateKernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);
        }

        private static bool TryExecuteTemporalAAResolveCompute(
            CommandBuffer cmd,
            RenderTargetIdentifier source,
            RenderTargetIdentifier history,
            RenderTargetIdentifier historyDepth,
            RenderTargetIdentifier velocity,
            RenderTargetIdentifier parallaxRejection,
            RenderTargetIdentifier dilatedHistoryRejection,
            RenderTargetIdentifier metadata,
            RenderTargetIdentifier stencilMask,
            RenderTargetIdentifier resolveTarget,
            BurtTemporalAARequestState temporalAA,
            bool historyValid,
            int width,
            int height,
            int stencilWidth,
            int stencilHeight)
        {
            if (cmd == null || !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            var shader = GetTemporalAAComputeShader();
            if (shader == null || !TryFindTemporalAAComputeKernel(shader, "ResolveTemporalAACS", out var kernel))
            {
                return false;
            }

            cmd.SetComputeTextureParam(shader, kernel, SourceTextureId, source);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAHistoryTextureId, history);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAADepthHistoryTextureId, historyDepth);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAVelocityTextureId, velocity);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAParallaxRejectionTextureId, parallaxRejection);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAADilatedHistoryRejectionTextureId, dilatedHistoryRejection);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAMetadataTextureId, metadata);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAStencilMaskTextureId, stencilMask);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAResolveTextureId, resolveTarget);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetComputeVectorParam(shader, TemporalAAStencilTexelSizeId, new Vector4(1f / Mathf.Max(1, stencilWidth), 1f / Mathf.Max(1, stencilHeight), stencilWidth, stencilHeight));
            cmd.SetComputeVectorParam(shader, TemporalAAParamsId, new Vector4(0f, 0f, historyValid ? 1f : 0f, temporalAA != null ? temporalAA.FrameIndex : 0f));
            cmd.SetComputeFloatParam(shader, TemporalAAHistoryExposureCorrectionId, temporalAA != null ? temporalAA.HistoryExposureCorrection : 1f);

            var jitter = temporalAA != null ? temporalAA.Jitter : Vector2.zero;
            ComputeTemporalAACurrentSampleWeights(jitter, out var weights0, out var weights1, out var weights2);
            cmd.SetComputeVectorParam(shader, TemporalAACurrentSampleWeights0Id, weights0);
            cmd.SetComputeVectorParam(shader, TemporalAACurrentSampleWeights1Id, weights1);
            cmd.SetComputeVectorParam(shader, TemporalAACurrentSampleWeights2Id, weights2);

            cmd.DispatchCompute(shader, kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);
            return true;
        }

        private static bool CanUseTemporalAAUComputePath(BurtTemporalAAHistoryTextures histories)
        {
            var packedOutputFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32;
            var fallbackOutputFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;
            var canUsePackedOutput =
                SystemInfo.IsFormatSupported(packedOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.Render) &&
                SystemInfo.IsFormatSupported(packedOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.Sample) &&
                SystemInfo.IsFormatSupported(packedOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore);
            var canUseFallbackOutput =
                SystemInfo.IsFormatSupported(fallbackOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.Render) &&
                SystemInfo.IsFormatSupported(fallbackOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.Sample) &&
                SystemInfo.IsFormatSupported(fallbackOutputFormat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore);
            if (!SystemInfo.supportsComputeShaders ||
                histories.PreviousColor == null ||
                histories.CurrentColor == null ||
                histories.PreviousGuide == null ||
                histories.CurrentGuide == null ||
                !histories.CurrentColor.enableRandomWrite ||
                !histories.CurrentGuide.enableRandomWrite ||
                !SystemInfo.IsFormatSupported(histories.PreviousColor.graphicsFormat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore) ||
                !SystemInfo.IsFormatSupported(histories.PreviousGuide.graphicsFormat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore) ||
                (!canUsePackedOutput && !canUseFallbackOutput) ||
                !SystemInfo.IsFormatSupported(UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore))
            {
                return false;
            }

            var shader = GetTemporalAAComputeShader();
            int reprojectKernel;
            int rejectKernel;
            int dilateRejectionKernel;
            int updateKernel;
            int resolveKernel;
            return shader != null &&
                TryFindTemporalAAComputeKernel(shader, "ReprojectTAAUHistoryGuideAACS", out reprojectKernel) &&
                TryFindTemporalAAComputeKernel(shader, "RejectTAAUShadingAACS", out rejectKernel) &&
                TryFindTemporalAAComputeKernel(shader, "DilateTAAUShadingRejectionAACS", out dilateRejectionKernel) &&
                TryFindTemporalAAComputeKernel(shader, "UpdateTAAUHistoryAACS", out updateKernel) &&
                TryFindTemporalAAComputeKernel(shader, "ResolveTAAUHistoryAACS", out resolveKernel);
        }

        private static bool TryExecuteTemporalAAUPipeline(
            CommandBuffer cmd,
            RenderTargetIdentifier source,
            RenderTargetIdentifier history,
            RenderTargetIdentifier previousGuide,
            RenderTargetIdentifier velocity,
            RenderTargetIdentifier dilateMask,
            RenderTargetIdentifier stencilMask,
            RenderTargetIdentifier reprojectedGuide,
            RenderTargetIdentifier currentGuide,
            RenderTargetIdentifier shadingRejection,
            RenderTargetIdentifier dilatedShadingRejection,
            RenderTargetIdentifier updatedHistory,
            RenderTargetIdentifier output,
            RenderTargetIdentifier finalBlendDebugOutput,
            bool writeFinalBlendDebug,
            BurtTemporalAARequestState temporalAA,
            bool historyValid,
            int inputWidth,
            int inputHeight,
            int outputWidth,
            int outputHeight,
            int historyWidth,
            int historyHeight)
        {
            if (cmd == null || !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            var shader = GetTemporalAAComputeShader();
            if (shader == null ||
                !TryFindTemporalAAComputeKernel(shader, "ReprojectTAAUHistoryGuideAACS", out var reprojectKernel) ||
                !TryFindTemporalAAComputeKernel(shader, "RejectTAAUShadingAACS", out var rejectKernel) ||
                !TryFindTemporalAAComputeKernel(shader, "DilateTAAUShadingRejectionAACS", out var dilateRejectionKernel) ||
                !TryFindTemporalAAComputeKernel(
                    shader,
                    writeFinalBlendDebug ? "UpdateTAAUHistoryDebugAACS" : "UpdateTAAUHistoryAACS",
                    out var updateKernel) ||
                !TryFindTemporalAAComputeKernel(shader, "ResolveTAAUHistoryAACS", out var resolveKernel))
            {
                return false;
            }

            var inputTexelSize = new Vector4(1f / Mathf.Max(1, inputWidth), 1f / Mathf.Max(1, inputHeight), inputWidth, inputHeight);
            var outputTexelSize = new Vector4(1f / Mathf.Max(1, outputWidth), 1f / Mathf.Max(1, outputHeight), outputWidth, outputHeight);
            var historyTexelSize = new Vector4(1f / Mathf.Max(1, historyWidth), 1f / Mathf.Max(1, historyHeight), historyWidth, historyHeight);
            var historyScale = historyWidth / (float)Mathf.Max(1, outputWidth);
            var historySampleCount = Mathf.Max(4f, 16f / Mathf.Max(1f, historyScale * historyScale));
            var historyHysteresis = 1f / historySampleCount;
            var outputToInput = inputWidth / (float)Mathf.Max(1, outputWidth);
            var theoreticalBlend = 1f / (1f + 16f / Mathf.Max(0.0001f, outputToInput * outputToInput));
            var packedHistoryFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.B10G11R11_UFloatPack32;
            var usePackedHistory = SystemInfo.IsFormatSupported(packedHistoryFormat, UnityEngine.Experimental.Rendering.FormatUsage.Render) &&
                SystemInfo.IsFormatSupported(packedHistoryFormat, UnityEngine.Experimental.Rendering.FormatUsage.Sample) &&
                SystemInfo.IsFormatSupported(packedHistoryFormat, UnityEngine.Experimental.Rendering.FormatUsage.LoadStore);
            var historyParams = new Vector4(historySampleCount, historyHysteresis, theoreticalBlend, usePackedHistory ? 1f : 0f);
            var frameParams = new Vector4(0f, 0f, historyValid ? 1f : 0f, temporalAA != null ? temporalAA.FrameIndex : 0f);
            var jitter = temporalAA != null ? temporalAA.Jitter : Vector2.zero;
            var jitterPixels = temporalAA != null ? temporalAA.JitterPixels : Vector2.zero;

            cmd.SetComputeTextureParam(shader, reprojectKernel, TemporalAAUHistoryGuideTextureId, previousGuide);
            cmd.SetComputeTextureParam(shader, reprojectKernel, TemporalAAVelocityTextureId, velocity);
            cmd.SetComputeTextureParam(shader, reprojectKernel, TemporalAAUReprojectedGuideOutputTextureId, reprojectedGuide);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUInputTexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAHistoryTexelSizeId, historyTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUGuideTexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAParamsId, frameParams);
            cmd.SetComputeFloatParam(shader, TemporalAAHistoryExposureCorrectionId, temporalAA != null ? temporalAA.HistoryExposureCorrection : 1f);
            cmd.DispatchCompute(shader, reprojectKernel, Mathf.CeilToInt(inputWidth / 8f), Mathf.CeilToInt(inputHeight / 8f), 1);

            cmd.SetComputeTextureParam(shader, rejectKernel, SourceTextureId, source);
            cmd.SetComputeTextureParam(shader, rejectKernel, TemporalAAUReprojectedGuideTextureId, reprojectedGuide);
            cmd.SetComputeTextureParam(shader, rejectKernel, TemporalAADilateMaskTextureId, dilateMask);
            cmd.SetComputeTextureParam(shader, rejectKernel, TemporalAAStencilMaskTextureId, stencilMask);
            cmd.SetComputeTextureParam(shader, rejectKernel, TemporalAAUHistoryGuideOutputTextureId, currentGuide);
            cmd.SetComputeTextureParam(shader, rejectKernel, TemporalAAUShadingRejectionOutputTextureId, shadingRejection);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUInputTexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUGuideTexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAStencilTexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAParamsId, frameParams);
            cmd.DispatchCompute(shader, rejectKernel, Mathf.CeilToInt(inputWidth / 8f), Mathf.CeilToInt(inputHeight / 8f), 1);

            cmd.SetComputeTextureParam(shader, dilateRejectionKernel, TemporalAAUShadingRejectionTextureId, shadingRejection);
            cmd.SetComputeTextureParam(shader, dilateRejectionKernel, TemporalAAUDilatedShadingRejectionOutputTextureId, dilatedShadingRejection);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUInputTexelSizeId, inputTexelSize);
            cmd.DispatchCompute(shader, dilateRejectionKernel, Mathf.CeilToInt(inputWidth / 8f), Mathf.CeilToInt(inputHeight / 8f), 1);

            cmd.SetComputeTextureParam(shader, updateKernel, SourceTextureId, source);
            cmd.SetComputeTextureParam(shader, updateKernel, TemporalAAHistoryTextureId, history);
            cmd.SetComputeTextureParam(shader, updateKernel, TemporalAAVelocityTextureId, velocity);
            cmd.SetComputeTextureParam(shader, updateKernel, TemporalAAUDilatedShadingRejectionTextureId, dilatedShadingRejection);
            cmd.SetComputeTextureParam(shader, updateKernel, TemporalAAStencilMaskTextureId, stencilMask);
            cmd.SetComputeTextureParam(shader, updateKernel, TemporalAAUUpdatedHistoryOutputTextureId, updatedHistory);
            if (writeFinalBlendDebug)
            {
                cmd.SetComputeTextureParam(shader, updateKernel, TemporalAAUFinalBlendDebugOutputTextureId, finalBlendDebugOutput);
            }
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUInputTexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAHistoryTexelSizeId, historyTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUOutputTexelSizeId, outputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAStencilTexelSizeId, inputTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUHistoryParamsId, historyParams);
            cmd.SetComputeVectorParam(shader, TemporalAAParamsId, frameParams);
            cmd.SetComputeVectorParam(shader, TemporalAAJitterId, new Vector4(jitter.x, jitter.y, jitterPixels.x, jitterPixels.y));
            cmd.SetComputeFloatParam(shader, TemporalAAHistoryExposureCorrectionId, temporalAA != null ? temporalAA.HistoryExposureCorrection : 1f);
            cmd.DispatchCompute(shader, updateKernel, Mathf.CeilToInt(historyWidth / 8f), Mathf.CeilToInt(historyHeight / 8f), 1);

            cmd.SetComputeTextureParam(shader, resolveKernel, TemporalAAUUpdatedHistoryTextureId, updatedHistory);
            cmd.SetComputeTextureParam(shader, resolveKernel, TemporalAAUOutputTextureId, output);
            cmd.SetComputeVectorParam(shader, TemporalAAHistoryTexelSizeId, historyTexelSize);
            cmd.SetComputeVectorParam(shader, TemporalAAUOutputTexelSizeId, outputTexelSize);
            cmd.DispatchCompute(shader, resolveKernel, Mathf.CeilToInt(outputWidth / 8f), Mathf.CeilToInt(outputHeight / 8f), 1);
            return true;
        }

        private static ComputeShader GetTemporalAAComputeShader()
        {
            if (temporalAAComputeShader != null)
            {
                return temporalAAComputeShader;
            }

            temporalAAComputeShader = Resources.Load<ComputeShader>(TemporalAAComputeShaderResourcePath);
            if (temporalAAComputeShader == null && !hasLoggedMissingTemporalAAComputeShader)
            {
                Debug.LogWarning("BurtRP could not find compute shader resource: " + TemporalAAComputeShaderResourcePath);
                hasLoggedMissingTemporalAAComputeShader = true;
            }

            return temporalAAComputeShader;
        }

        private static bool TryFindTemporalAAComputeKernel(ComputeShader shader, string kernelName, out int kernel)
        {
            kernel = -1;
            if (shader == null || !shader.HasKernel(kernelName))
            {
                if (!hasLoggedMissingTemporalAAComputeKernel)
                {
                    Debug.LogWarning("BurtRP compute shader missing kernel: " + kernelName);
                    hasLoggedMissingTemporalAAComputeKernel = true;
                }

                return false;
            }

            kernel = shader.FindKernel(kernelName);
            return true;
        }

        private static void ComputeTemporalAACurrentSampleWeights(Vector2 jitter, out Vector4 weights0, out Vector4 weights1, out Vector4 weights2)
        {
            var totalWeight = 0f;
            for (var i = 0; i < TemporalAACurrentSampleWeights.Length; i++)
            {
                var x = TemporalAACurrentSampleOffsets[i].x + jitter.x;
                var y = TemporalAACurrentSampleOffsets[i].y + jitter.y;
                var weight = Mathf.Exp((-0.5f / 0.22f) * (x * x + y * y));
                TemporalAACurrentSampleWeights[i] = weight;
                totalWeight += weight;
            }

            var inverseTotalWeight = 1f / Mathf.Max(totalWeight, 0.00001f);
            for (var i = 0; i < TemporalAACurrentSampleWeights.Length; i++)
            {
                TemporalAACurrentSampleWeights[i] *= inverseTotalWeight;
            }

            weights0 = new Vector4(TemporalAACurrentSampleWeights[0], TemporalAACurrentSampleWeights[1], TemporalAACurrentSampleWeights[2], TemporalAACurrentSampleWeights[3]);
            weights1 = new Vector4(TemporalAACurrentSampleWeights[4], TemporalAACurrentSampleWeights[5], TemporalAACurrentSampleWeights[6], TemporalAACurrentSampleWeights[7]);
            weights2 = new Vector4(TemporalAACurrentSampleWeights[8], 0f, 0f, 0f);
        }

        private static bool DrawTemporalAAObjectMotionVectors(
            BurtRenderGraphContext context,
            CommandBuffer cmd,
            Camera camera,
            RenderTargetIdentifier velocityTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            int width,
            int height,
            bool bindCameraDepthStencil,
            bool includeOpaque,
            bool deferredGBufferOwnsOpaqueVelocity)
        {
            if (context == null || context.Request == null || camera == null)
            {
                return false;
            }

            camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

            if (bindCameraDepthStencil && cameraDepthTarget.IsValid)
            {
                cmd.SetRenderTarget(velocityTarget, cameraDepthTarget.Identifier);
            }
            else
            {
                cmd.SetRenderTarget(velocityTarget);
            }

            SetTemporalAAViewport(cmd, width, height);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            // DrawRendererList is recorded into the graph-owned command buffer. Keep the
            // velocity target, viewport and camera matrices in that same ordered stream;
            // flushing here clears the buffer before the renderer list is recorded and lets
            // the list inherit an unrelated render target.

            if (includeOpaque)
            {
                var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
                // Match XRender's desktop deferred ownership: GBuffer velocity is final for
                // deferred surfaces. Only forward-only opaque surfaces need a supplemental
                // motion pass, otherwise a second draw can overwrite the correct GBuffer
                // vector with a materially different deformation/previous-transform path.
                var primaryTag = deferredGBufferOwnsOpaqueVelocity
                    ? TemporalAAForwardOnlyMotionVectorsTag
                    : TemporalAAObjectMotionVectorsTag;
                var drawingSettings = new DrawingSettings(primaryTag, sortingSettings)
                {
                    perObjectData = PerObjectData.MotionVectors,
                    enableDynamicBatching = false,
                    enableInstancing = true
                };
                if (!deferredGBufferOwnsOpaqueVelocity)
                {
                    drawingSettings.SetShaderPassName(1, TemporalAAForwardOnlyMotionVectorsTag);
                }

                var filteringSettings = new FilteringSettings(TemporalAAObjectMotionVectorQueueRange, camera.cullingMask);
                context.DrawRendererList(context.Request.CullingResults, ref drawingSettings, ref filteringSettings);
            }

            // Match XRender desktop TAA: transparent DefaultLit writes the
            // responsive-AA stencil bit, but it does not own a motion vector.
            // Reprojection therefore keeps the velocity/depth of the visible
            // opaque surface behind it and raises current-frame contribution
            // to the fixed responsive 25% instead of injecting an alpha-blended
            // surface velocity into the shared motion buffer.
            return includeOpaque;
        }

        private static void DrawTemporalAAResponsiveAAMask(
            BurtRenderGraphContext context,
            CommandBuffer cmd,
            Camera camera,
            RenderTargetIdentifier maskTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            int width,
            int height)
        {
            if (context == null || context.Request == null || cmd == null || camera == null || !cameraDepthTarget.IsValid)
            {
                return;
            }

            // XRender writes responsive AA only from transparent Forward
            // surfaces (and water), never from opaque GBuffer or object-motion
            // passes. The material mask pass reproduces that coverage when the
            // native stencil subresource cannot be sampled. Using LEqual keeps
            // visible alpha-blended surfaces eligible while rejecting geometry
            // hidden behind opaque camera depth.
            cmd.SetRenderTarget(maskTarget, cameraDepthTarget.Identifier);
            SetTemporalAAViewport(cmd, width, height);
            // This pass shares the motion-vector vertex path and must use the same jittered
            // camera matrices as the preceding object-vector draw. Fullscreen passes may have
            // changed command-buffer matrices before we get here.
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            // Do not flush between SetRenderTarget and DrawRendererList. Renderer lists are
            // graph-command-buffer commands, so the responsive mask binding must remain in
            // the same submission as the draws that consume it.

            var transparentSortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonTransparent };
            var transparentDrawingSettings = new DrawingSettings(new ShaderTagId("BurtResponsiveAAMask"), transparentSortingSettings)
            {
                perObjectData = PerObjectData.MotionVectors,
                enableDynamicBatching = false,
                enableInstancing = true
            };
            var transparentFilteringSettings = new FilteringSettings(TemporalAATransparentMotionVectorQueueRange, camera.cullingMask);
            context.DrawRendererList(context.Request.CullingResults, ref transparentDrawingSettings, ref transparentFilteringSettings);
        }

        private static bool DrawTemporalAAMultipassFurMotionVectors(
            BurtRenderGraphContext context,
            CommandBuffer cmd,
            RenderTargetIdentifier velocityTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            int width,
            int height)
        {
            if (context == null || cmd == null || !cameraDepthTarget.IsValid || BurtMultipassRenderer.RegisteredRendererCount <= 0)
            {
                return false;
            }

            cmd.SetRenderTarget(velocityTarget, cameraDepthTarget.Identifier);
            SetTemporalAAViewport(cmd, width, height);
            BurtFurBlurPassUtility.UploadMotionVectorGlobals(cmd, context.Request, width, height);
            BurtMultipassRenderer.DrawAll(cmd, context, BurtMultipassShaderPass.MotionVectors, RenderQueueRange.opaque);
            return true;
        }

        private static bool ShouldUseBloomDebugView(BloomDebugView debugView, int mipCount)
        {
            return mipCount > 0 &&
                debugView != BloomDebugView.Disabled &&
                !PostProcessUtility.IsPostProcessSuppressedByShadingDebug();
        }

        private static void DisablePostProcessEffects(CommandBuffer cmd)
        {
            PreExposureUtility.UploadGlobals(cmd, PreExposureState.Default);
            cmd.SetGlobalFloat(UseBloomId, 0f);
            cmd.SetGlobalFloat(UseBloomAlphaId, 0f);
            cmd.SetGlobalFloat(TonemappingModeId, 0f);
            cmd.SetGlobalFloat(PostExposureId, 1f);
            cmd.SetGlobalFloat(UseExposureTextureId, 0f);
            cmd.SetGlobalFloat(UseLocalExposureId, 0f);
            cmd.SetGlobalFloat(UseColorAdjustmentsId, 0f);
            cmd.SetGlobalFloat(UseColorGradingId, 0f);
            cmd.SetGlobalFloat(UseWhiteBalanceId, 0f);
            cmd.SetGlobalFloat(UseVignetteId, 0f);
        }

        private static void SetLocalExposureGlobals(CommandBuffer cmd, BurtRenderGraphContext context, PhysicalExposureSettings exposureSettings)
        {
            var localExposure = VolumeManager.instance.stack.GetComponent<LocalExposureVolumeComponent>();
            if (localExposure == null || !localExposure.IsEnabled() ||
                !GpuExposureUtility.TryGetLocalExposureTextures(context.Request.Camera, out var histogram, out var blurredLogLuminance))
            {
                cmd.SetGlobalFloat(UseLocalExposureId, 0f);
                return;
            }

            var averageLuminance = Mathf.Max(exposureSettings.AutoAverageLuminance, 0.000001f);
            var luminanceEv100 = Mathf.Log(averageLuminance, 2f) + Mathf.Log(1f / 0.18f, 2f);
            var highlightCurve = localExposure.highlightContrastCurve.value;
            var shadowCurve = localExposure.shadowContrastCurve.value;
            var highlightContrast = localExposure.highlightContrast.value * (highlightCurve != null ? highlightCurve.Evaluate(luminanceEv100) : 1f);
            var shadowContrast = localExposure.shadowContrast.value * (shadowCurve != null ? shadowCurve.Evaluate(luminanceEv100) : 1f);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(context.Request.Camera);
            // XRender fills the bilateral histogram from the shared half-size
            // exposure source. Scale UVs by the populated part of the 64x64
            // histogram tiles, not by full-resolution camera groups.
            var histogramSourceWidth = Mathf.Max(1, (descriptor.width + 1) / 2);
            var histogramSourceHeight = Mathf.Max(1, (descriptor.height + 1) / 2);
            var groupCountX = Mathf.Max(1, histogram.width);
            var groupCountY = Mathf.Max(1, histogram.height);
            var bilateralUvScaleX = (float)histogramSourceWidth / (64f * groupCountX);
            var bilateralUvScaleY = (float)histogramSourceHeight / (64f * groupCountY);

            cmd.SetGlobalTexture(LocalExposureHistogramTextureId, histogram);
            cmd.SetGlobalTexture(LocalExposureBlurredLogLuminanceTextureId, blurredLogLuminance);
            cmd.SetGlobalVector(LocalExposureContrastParamsId, new Vector4(
                highlightContrast,
                shadowContrast,
                localExposure.detailStrength.value,
                localExposure.blurredLuminanceBlend.value));
            var middleGreyExposureCompensation = Mathf.Pow(2f, localExposure.middleGreyBias.value);
            if (exposureSettings.Mode == ExposureMode.ManualEV100 || exposureSettings.Mode == ExposureMode.PhysicalCamera)
            {
                // XRender cancels the manual exposure compensation here. The
                // exposure texture's W channel multiplies it back when the
                // local middle-grey value is formed in the shader.
                var manualCompensationScale = Mathf.Pow(2f, exposureSettings.Compensation) *
                    Mathf.Max(exposureSettings.Calibration, 0f);
                middleGreyExposureCompensation /= Mathf.Max(manualCompensationScale, 0.0001f);
            }
            cmd.SetGlobalVector(LocalExposureThresholdParamsId, new Vector4(
                localExposure.highlightThreshold.value,
                localExposure.shadowThreshold.value,
                middleGreyExposureCompensation,
                0f));
            cmd.SetGlobalVector(LocalExposureGridParamsId, new Vector4(
                bilateralUvScaleX,
                bilateralUvScaleY,
                exposureSettings.AutoHistogramMinEV100,
                exposureSettings.AutoHistogramMaxEV100));
            cmd.SetGlobalFloat(UseLocalExposureId, 1f);
        }

        private static int[] CreateAutoExposureTextureIds()
        {
            var ids = new int[16];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = Shader.PropertyToID("_BurtAutoExposureLogLum" + i);
            }

            return ids;
        }

        private static BloomGaussianKernelCacheEntry[] CreateBloomGaussianKernelCache()
        {
            var entries = new BloomGaussianKernelCacheEntry[BloomGaussianKernelCacheSize];
            for (var i = 0; i < entries.Length; i++)
            {
                entries[i] = new BloomGaussianKernelCacheEntry();
            }

            return entries;
        }

        private static bool IsBloomGaussianMipActive(int mipIndex, int stageCount)
        {
            return stageCount > 0 &&
                mipIndex >= PostProcessUtility.ResolveBloomFirstStageMipIndex(stageCount) &&
                mipIndex < BurtRenderGraphResourceRegistry.BloomPyramidCount;
        }

        private static BurtRenderTargetHandle AllocateBloomGraphTarget(
            BurtRenderGraphContext context,
            string resourceName,
            int mipIndex,
            BloomSettings settings)
        {
            if (context == null || context.ResourceRegistry == null || context.Request == null || context.Request.Camera == null)
            {
                return BurtRenderTargetHandle.Invalid(resourceName);
            }

            RenderTextureDescriptor descriptor;
            if (PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings))
            {
                descriptor = PostProcessUtility.CreateBloomInputRenderTextureDescriptor(
                    context.Request.Camera,
                    GetBloomMipWidth(context, mipIndex),
                    GetBloomMipHeight(context, mipIndex));
            }
            else
            {
                descriptor = PostProcessUtility.CreateBloomRenderTextureDescriptor(
                    context.Request.Camera,
                    GetBloomMipWidth(context, mipIndex),
                    GetBloomMipHeight(context, mipIndex),
                    settings,
                    PostProcessUtility.ResolveBloomDebugView(settings));
            }

            context.ResourceRegistry.SetRenderTargetDescriptor(resourceName, descriptor, FilterMode.Bilinear, "Burt " + resourceName);
            return context.ResourceRegistry.AllocateRenderTarget(resourceName);
        }

        private static BurtRenderTargetHandle AllocateBloomInputGraphTarget(BurtRenderGraphContext context)
        {
            const string resourceName = BurtRenderGraphResourceRegistry.BloomInputName;
            if (context == null || context.ResourceRegistry == null || context.Request == null || context.Request.Camera == null)
            {
                return BurtRenderTargetHandle.Invalid(resourceName);
            }

            var descriptor = PostProcessUtility.CreateBloomInputRenderTextureDescriptor(
                context.Request.Camera,
                GetBloomMipWidth(context, 0),
                GetBloomMipHeight(context, 0));
            context.ResourceRegistry.SetRenderTargetDescriptor(resourceName, descriptor, FilterMode.Bilinear, "Burt " + resourceName);
            return context.ResourceRegistry.AllocateRenderTarget(resourceName);
        }

        private static void SetBloomDebugSource(CommandBuffer cmd, BurtRenderGraphContext context, RenderTargetIdentifier cameraColorTarget, BloomSettings settings, BloomDebugView debugView, int stageCount, PreExposureState preExposureState)
        {
            var camera = context.Request.Camera;
            var firstMipIndex = PostProcessUtility.ResolveBloomFirstStageMipIndex(stageCount);
            var sourceWidth = GetBloomMipWidth(context, firstMipIndex);
            var sourceHeight = GetBloomMipHeight(context, firstMipIndex);
            var source = context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(firstMipIndex));
            if (debugView == BloomDebugView.ThresholdMask)
            {
                source = context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.BloomInputName);
                sourceWidth = GetBloomMipWidth(context, 0);
                sourceHeight = GetBloomMipHeight(context, 0);
            }
            else if (debugView == BloomDebugView.Prefilter)
            {
                source = context.ResourceRegistry.GetRenderTarget(PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings)
                    ? BurtRenderGraphResourceRegistry.BloomInputName
                    : BurtRenderGraphResourceRegistry.BloomSetupName);
                sourceWidth = GetBloomMipWidth(context, 0);
                sourceHeight = GetBloomMipHeight(context, 0);
            }
            else if (debugView >= BloomDebugView.Mip1 && debugView <= BloomDebugView.Mip5)
            {
                var mipIndex = Mathf.Clamp((int)debugView - (int)BloomDebugView.Mip1 + 1, 1, BurtRenderGraphResourceRegistry.BloomPyramidCount - 1);
                source = IsBloomGaussianMipActive(mipIndex, stageCount)
                    ? context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomGaussianVerticalName(mipIndex))
                    : context.ResourceRegistry.GetRenderTarget(BurtRenderGraphResourceRegistry.GetBloomDownsampleName(mipIndex - 1));
                sourceWidth = GetBloomMipWidth(context, mipIndex);
                sourceHeight = GetBloomMipHeight(context, mipIndex);
            }

            cmd.SetGlobalTexture(SourceTextureId, !source.IsValid ? cameraColorTarget : source.Identifier);
            cmd.SetGlobalVector(BloomTexelSizeId, new Vector4(1f / Mathf.Max(1, sourceWidth), 1f / Mathf.Max(1, sourceHeight), sourceWidth, sourceHeight));
            if (debugView == BloomDebugView.ThresholdMask)
            {
                cmd.SetGlobalFloat(BloomThresholdId, settings.Threshold);
                cmd.SetGlobalFloat(BloomExposureScaleId, preExposureState.PostExposure);
                cmd.SetGlobalFloat(BloomBypassThresholdId, PostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings) ? 1f : 0f);
            }

            cmd.SetGlobalFloat(BloomDebugModeId, ResolveBloomDebugShaderMode(debugView));
            cmd.SetGlobalFloat(BloomDebugYFlipId, 0f);
        }

        private static float ResolveBloomDebugShaderMode(BloomDebugView debugView)
        {
            if (debugView == BloomDebugView.Alpha)
            {
                return 1f;
            }

            return debugView == BloomDebugView.ThresholdMask ? 2f : 0f;
        }

        private static void SetBloomGaussianKernel(CommandBuffer cmd, float radius, int width, int height, bool horizontal, Color tint) // Upload XRender PC-style bilinear-merged Gaussian kernel.
        {
            var radiusKey = Mathf.RoundToInt(Mathf.Clamp(radius, 0.00001f, MaxBloomGaussianSamples - 1) * BloomGaussianKernelRadiusCacheScale); // Quantize radius so equivalent Bloom frames reuse kernels.
            var sourceWidth = Mathf.Max(1, width);
            var sourceHeight = Mathf.Max(1, height);
            var cacheHash = CalculateBloomGaussianKernelHash(radiusKey, sourceWidth, sourceHeight, horizontal);
            var cacheEntry = GetBloomGaussianKernelCacheEntry(cacheHash, radiusKey, sourceWidth, sourceHeight, horizontal);
            var sampleCount = cacheEntry.SampleCount;

            CopyBloomGaussianKernel(cacheEntry, BloomGaussianWeights, BloomGaussianOffsets, tint);

            cmd.SetGlobalFloat(BloomSampleCountId, sampleCount); // Shader reads the active count from fixed-size arrays.
            cmd.SetGlobalVectorArray(BloomSampleWeightsId, BloomGaussianWeights); // Upload normalized weights.
            cmd.SetGlobalVectorArray(BloomSampleOffsetsId, BloomGaussianOffsets); // Upload UV-space offsets.
        }

        private static BloomGaussianKernelCacheEntry GetBloomGaussianKernelCacheEntry(int hash, int radiusKey, int width, int height, bool horizontal)
        {
            for (var i = 0; i < BloomGaussianKernelCache.Length; i++)
            {
                var entry = BloomGaussianKernelCache[i];
                if (entry.Valid &&
                    entry.Hash == hash &&
                    entry.RadiusKey == radiusKey &&
                    entry.Width == width &&
                    entry.Height == height &&
                    entry.Horizontal == horizontal)
                {
                    return entry;
                }
            }

            var target = BloomGaussianKernelCache[BloomGaussianKernelCacheNextIndex];
            BloomGaussianKernelCacheNextIndex = (BloomGaussianKernelCacheNextIndex + 1) % BloomGaussianKernelCache.Length;
            target.Valid = true;
            target.Hash = hash;
            target.RadiusKey = radiusKey;
            target.Width = width;
            target.Height = height;
            target.Horizontal = horizontal;
            target.SampleCount = ComputeBloomGaussianKernel(radiusKey / BloomGaussianKernelRadiusCacheScale, width, height, horizontal, target.Weights, target.Offsets); // Cache the normalized kernel shape; apply the unquantized stage tint only when uploading it.

            return target;
        }

        private static int CalculateBloomGaussianKernelHash(int radiusKey, int width, int height, bool horizontal)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + radiusKey;
                hash = hash * 31 + width;
                hash = hash * 31 + height;
                hash = hash * 31 + (horizontal ? 1 : 0);

                return hash;
            }
        }

        private static void CopyBloomGaussianKernel(BloomGaussianKernelCacheEntry entry, Vector4[] weights, Vector4[] offsets, Color tint)
        {
            for (var i = 0; i < BloomGaussianSampleCapacity; i++)
            {
                var weight = entry.Weights[i];
                weights[i] = new Vector4(weight.x * tint.r, weight.y * tint.g, weight.z * tint.b, weight.w);
                offsets[i] = entry.Offsets[i];
            }
        }

        private static int ComputeBloomGaussianKernel(float radius, int width, int height, bool horizontal, Vector4[] weights, Vector4[] offsets) // Mirrors XRender Compute1DGaussianFilterKernel.
        {
            var clampedRadius = Mathf.Clamp(radius, 0.00001f, MaxBloomGaussianSamples - 1); // Avoid divide-by-zero and cap sample count.
            var integerRadius = Mathf.Min(Mathf.CeilToInt(clampedRadius), MaxBloomGaussianSamples - 1); // XRender uses ceil(radius) as integer radius.
            var sampleCount = 0; // Count bilinear-merged samples.
            var weightSum = 0f; // Used to normalize weights.

            for (var sampleIndex = -integerRadius; sampleIndex <= integerRadius && sampleCount < MaxBloomGaussianSamples; sampleIndex += 2)
            {
                var weight0 = NormalDistributionUnscaled(sampleIndex, clampedRadius); // Current tap weight.
                var weight1 = sampleIndex != integerRadius ? NormalDistributionUnscaled(sampleIndex + 1, clampedRadius) : 0f; // Next tap weight.
                var totalWeight = weight0 + weight1; // Merged bilinear sample weight.
                var sampleOffset = sampleIndex + weight1 / Mathf.Max(totalWeight, 0.00001f); // XRender bilinear offset merge formula.
                var uvOffset = horizontal ? new Vector4(sampleOffset / width, 0f, 0f, 0f) : new Vector4(0f, sampleOffset / height, 0f, 0f); // Convert to UV-space offset.

                weights[sampleCount] = Vector4.one * totalWeight;
                offsets[sampleCount] = uvOffset;
                weightSum += totalWeight;
                sampleCount++;
            }

            var weightSumInverse = 1f / Mathf.Max(weightSum, 0.00001f); // Normalize to preserve brightness.
            for (var i = 0; i < sampleCount; i++)
            {
                weights[i] *= weightSumInverse;
            }

            for (var i = sampleCount; i < BloomGaussianSampleCapacity; i++)
            {
                weights[i] = Vector4.zero;
                offsets[i] = Vector4.zero;
            }

            return sampleCount;
        }

        private static float NormalDistributionUnscaled(float x, float sigma) // XRender PC Bloom legacy Gaussian.
        {
            var normalized = Mathf.Abs(x) / sigma; // Normalize distance by radius.

            return Mathf.Exp(-16.7f * normalized * normalized); // XRender legacyCompatibilityConstant = -16.7.
        }

        private static void SetBloomSource(CommandBuffer cmd, RenderTargetIdentifier source, int width, int height) // 上传 Bloom 源纹理和 texel size。
        {
            cmd.SetGlobalTexture(SourceTextureId, source); // 复用后处理源纹理属性，供 Bloom 子 Pass 采样。
            cmd.SetGlobalVector(BloomTexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height)); // 上传 texel size，便于 shader 做邻域采样。
        }

        private static void SetAutoExposureDebugSource(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            RenderTargetIdentifier source,
            PhysicalExposureSettings exposure,
            int debugMode,
            RenderTargetIdentifier toneMappedSource,
            bool hasToneMappedSource)
        {
            var camera = context.Request.Camera;
            var width = 1;
            var height = 1;
            if (camera != null)
            {
                width = Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.width : camera.pixelWidth);
                height = Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.height : camera.pixelHeight);
            }

            cmd.SetGlobalTexture(SourceTextureId, source);
            cmd.SetGlobalVector(AutoExposureTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalFloat(AutoExposureDebugModeId, debugMode);
            cmd.SetGlobalVector(AutoExposureDebugParamsId, new Vector4(
                exposure.AutoHistogramMinEV100,
                exposure.AutoHistogramMaxEV100,
                exposure.AutoMiddleGrey,
                exposure.AutoAverageLogLuminance));

            var hasExposureTexture = GpuExposureUtility.TryGetCurrentTexture(camera, out var exposureTexture);
            cmd.SetGlobalTexture(ExposureTextureId, hasExposureTexture ? exposureTexture : Texture2D.whiteTexture);
            cmd.SetGlobalFloat(UseExposureTextureId, hasExposureTexture ? 1f : 0f);

            var currentScale = exposure.Multiplier;
            var targetScale = exposure.Multiplier;
            var averageLuminance = Mathf.Max(exposure.AutoAverageLuminance, 0.000001f);
            var compensationScale = Mathf.Pow(2f, exposure.Compensation) * Mathf.Max(exposure.Calibration, 0f);
            if (GpuExposureUtility.TryGetSnapshot(camera, out var gpuSnapshot))
            {
                currentScale = gpuSnapshot.CurrentScale;
                targetScale = gpuSnapshot.TargetScale;
                averageLuminance = Mathf.Max(gpuSnapshot.AverageLuminance, 0.000001f);
                compensationScale = gpuSnapshot.CompensationScale;
            }
            cmd.SetGlobalVector(AutoExposureDebugParams2Id, new Vector4(currentScale, targetScale, averageLuminance, compensationScale));

            var exposureComponent = VolumeManager.instance.stack.GetComponent<ExposureVolumeComponent>();
            var meteringMask = exposureComponent != null ? exposureComponent.meteringMask.value : null;
            cmd.SetGlobalTexture(AutoExposureDebugMeteringMaskId, meteringMask != null ? meteringMask : Texture2D.whiteTexture);
            cmd.SetGlobalFloat(AutoExposureDebugUseMeteringMaskId, meteringMask != null ? 1f : 0f);
            var hasHistogram = GpuExposureUtility.TryGetHistogramTexture(camera, out var histogramTexture);
            cmd.SetGlobalTexture(AutoExposureDebugHistogramTextureId, hasHistogram ? histogramTexture : Texture2D.blackTexture);
            cmd.SetGlobalFloat(AutoExposureDebugHasHistogramId, hasHistogram ? 1f : 0f);
            cmd.SetGlobalTexture(AutoExposureDebugToneMappedTextureId, toneMappedSource);
            cmd.SetGlobalFloat(AutoExposureDebugHasToneMappedTextureId, hasToneMappedSource ? 1f : 0f);
            // Tone-mapped HDR debug writes directly to CameraColor to avoid reading
            // and writing the same intermediate texture. Match the Y orientation of
            // the other HDR views, which pass through the final composite copy.
            cmd.SetGlobalFloat(AutoExposureDebugFlipYId, hasToneMappedSource ? 1f : 0f);
            cmd.SetGlobalFloat(TonemappingModeId, (float)PostProcessUtility.ResolveTonemappingMode(context.Asset));
            var filmSettings = PostProcessUtility.ResolveTonemappingFilmSettings(context.Asset);
            cmd.SetGlobalFloat(FilmSlopeId, filmSettings.Slope);
            cmd.SetGlobalFloat(FilmToeId, filmSettings.Toe);
            cmd.SetGlobalFloat(FilmShoulderId, filmSettings.Shoulder);
            cmd.SetGlobalFloat(FilmBlackClipId, filmSettings.BlackClip);
            cmd.SetGlobalFloat(FilmWhiteClipId, filmSettings.WhiteClip);
            SetLocalExposureGlobals(cmd, context, exposure);
        }

        private static int GetBloomMipWidth(BurtRenderGraphContext context, int mipIndex) // 计算指定 Bloom mip 的宽度。
        {
            ResolveActivePostProcessSize(context, out var width, out _);
            for (var i = 0; i <= Mathf.Max(0, mipIndex); i++)
            {
                width = Mathf.Max(1, (width + 1) / 2);
            }

            return width;
        }

        private static int GetBloomMipHeight(BurtRenderGraphContext context, int mipIndex) // 计算指定 Bloom mip 的高度。
        {
            ResolveActivePostProcessSize(context, out _, out var height);
            for (var i = 0; i <= Mathf.Max(0, mipIndex); i++)
            {
                height = Mathf.Max(1, (height + 1) / 2);
            }

            return height;
        }

        private static Material GetPostProcessMaterial() // 定义获取后处理材质的内部辅助函数。
        {
            if (postProcessMaterial != null) // 如果材质之前已经创建过，就直接复用。
            {
                return postProcessMaterial; // 返回缓存材质，避免重复创建。
            }

            var shader = Shader.Find(PostProcessShaderName); // 按名称查找后处理 shader。

            if (shader == null) // 如果 shader 查找失败，说明资源未导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过缺失 shader 警告，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + PostProcessShaderName); // 输出缺失 shader 警告，方便定位资源问题。

                    hasLoggedMissingShader = true; // 标记警告已输出，避免每帧重复刷屏。
                }

                return null; // 返回空材质，让调用方安全跳过后处理 Pass。
            }

            postProcessMaterial = new Material(shader); // 使用找到的 shader 创建运行时材质。

            postProcessMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质，并避免它被保存进场景或资源。

            return postProcessMaterial; // 返回创建好的材质。
        }
    }

    internal sealed class ReleasePostProcessColorPass : BurtRenderPass // 定义后处理中间颜色释放 Pass，负责释放 PostProcessColor 临时 RT。
    {
        public override string Name => "Release Post Process Color"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果后处理框架没有启用，就不声明资源依赖。
            {
                return; // 直接结束配置，避免关闭状态下出现无效资源读取。
            }

            builder.ReadPostProcessColor(); // 声明这个 Pass 依赖 PostProcessColor，表示它要结束这个临时资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 PostProcessColor 临时 RT 的释放。
        {
            if (!PostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次确认后处理框架仍然启用。
            {
                return; // 未启用时直接跳过，不释放未申请的资源。
            }

            var postProcessColorTarget = context.PostProcessColorTarget; // 从资源表中读取 PostProcessColor 句柄。

            if (!postProcessColorTarget.IsValid) // 如果句柄无效，说明当前图没有注册后处理中间 RT。
            {
                return; // 直接跳过，避免释放不存在的临时 RT。
            }

            context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.PostProcessColorName);
        }
    }

    internal sealed class ReleaseTemporalAAOutputPass : BurtRenderPass
    {
        public override string Name => "Release Temporal AA Output";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (PostProcessPass.ShouldUseTemporalAAUpscale(builder.Request, builder.Asset))
            {
                builder.ReadTemporalAAOutput();
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!PostProcessPass.ShouldUseTemporalAAUpscale(context.Request, context.Asset) ||
                !context.TemporalAAOutputTarget.IsValid)
            {
                return;
            }

            context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.TemporalAAOutputName);
        }
    }
}
