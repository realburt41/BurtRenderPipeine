using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Material、Shader、Matrix4x4 和 MeshTopology。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CommandBufferPool 和 RenderTarget 相关 API。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让后处理 Pass 可以直接接入现有 RenderGraph。
{
    internal sealed class BurtAllocatePostProcessColorPass : BurtRenderPass // 定义后处理中间颜色分配 Pass，负责申请 PostProcessColor 临时 RT。
    {
        public override string Name => "Burt Allocate Post Process Color"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果当前 request 没有启用后处理框架，就不声明资源写入。
            {
                return; // 直接结束配置，保持未启用时的 RenderGraph 干净。
            }

            builder.WritePostProcessColor(); // 声明这个 Pass 会创建并写入 PostProcessColor 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行后处理中间颜色 RT 的申请。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次判断，防止配置和执行之间状态变化。
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

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取 CommandBuffer，并用当前 Pass 名称命名。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.PostProcessColorTextureId, descriptor, FilterMode.Bilinear); // 申请 PostProcessColor 临时 RT，后续 No-op Copy 会先写入它。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.PostProcessColorTextureId, postProcessColorTarget.Identifier); // 把 PostProcessColor 暴露为全局纹理，方便调试或后续效果链采样。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请 RT 的命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtPostProcessPass : BurtRenderPass // 定义第一版正式后处理 Pass，支持 No-op Copy、Tonemapping 和 Color Adjustments。
    {
        private const string PostProcessShaderName = "Hidden/BurtRP/PostProcessCopy"; // 定义后处理 shader 的查找名称，必须和 shader 文件里的 Shader 名称一致。
        private const string TemporalAAComputeShaderResourcePath = "BurtTemporalAA";

        private const int MaxBloomMipCount = 8; // 第一版 Bloom 最多申请 8 级临时 RT，避免动态 RenderGraph 资源注册过重。

        private const int MaxBloomGaussianSamples = BurtPostProcessUtility.BloomGaussianMaxSamples; // Match XRender PC GaussianBlur sample cap.
        private const int BloomGaussianKernelCacheSize = MaxBloomMipCount * 2; // One horizontal and one vertical kernel per Bloom mip.
        private const float BloomGaussianKernelRadiusCacheScale = 1024f; // Quantize radius enough to keep cache stable without visible drift.
        private const float BloomGaussianKernelTintCacheScale = 1024f; // Quantize RGB tint for compact Bloom kernel cache keys.
        private const float BloomFireflyClamp = BurtPostProcessUtility.BloomPrefilterFireflyClamp; // Soft HDR luma cap for Bloom prefilter fireflies; regular highlights stay below this.
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
            TemporalAAUpscale = 17
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
        private static readonly int TemporalAAPrevUseCountTextureId = Shader.PropertyToID("_BurtTAAPrevUseCountTexture");
        private static readonly int TemporalAAMetadataTextureId = Shader.PropertyToID("_BurtTAAMetadataTexture");
        private static readonly int TemporalAAStencilTextureId = Shader.PropertyToID("_BurtTAAStencilTexture");
        private static readonly int TemporalAAResolveTextureId = Shader.PropertyToID("_BurtTAAResolveTexture");
        private static readonly int TemporalAAParallaxRejectionTextureId = Shader.PropertyToID("_BurtTAAParallaxRejectionTexture");
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
        private static readonly int TemporalAAJitterId = Shader.PropertyToID("_BurtTAAJitter");
        private static readonly int TemporalAATexelSizeId = Shader.PropertyToID("_BurtTAATexelSize");
        private static readonly int TemporalAAParamsId = Shader.PropertyToID("_BurtTAAParams");
        private static readonly int TemporalAAParams2Id = Shader.PropertyToID("_BurtTAAParams2");
        private static readonly int TemporalAAResponsiveParamsId = Shader.PropertyToID("_BurtTAAResponsiveParams");
        private static readonly int TemporalAAEdgeParamsId = Shader.PropertyToID("_BurtTAAEdgeParams");
        private static readonly int TemporalAAStencilTexelSizeId = Shader.PropertyToID("_BurtTAAStencilTexelSize");
        private static readonly int TemporalAAHistoryExposureCorrectionId = Shader.PropertyToID("_BurtTAAHistoryExposureCorrection");
        private static readonly int TemporalAACurrentSampleWeights0Id = Shader.PropertyToID("_BurtTAACurrentSampleWeights0");
        private static readonly int TemporalAACurrentSampleWeights1Id = Shader.PropertyToID("_BurtTAACurrentSampleWeights1");
        private static readonly int TemporalAACurrentSampleWeights2Id = Shader.PropertyToID("_BurtTAACurrentSampleWeights2");
        private static readonly int TemporalAAHasGBufferId = Shader.PropertyToID("_BurtTAAHasGBuffer");
        private static readonly int ShadingDebugEnabledId = Shader.PropertyToID(BurtShadingDebugSettings.EnabledShaderName);
        private static readonly Color TemporalAADebugUnavailableColor = new Color(0.65f, 0.05f, 0.9f, 1f);

        private static readonly int UseBloomId = Shader.PropertyToID("_BurtUseBloom"); // 缓存 Bloom 合成开关属性 ID。

        private static readonly int BloomIntensityId = Shader.PropertyToID("_BurtBloomIntensity"); // 缓存 Bloom 合成强度属性 ID。

        private static readonly int BloomThresholdId = Shader.PropertyToID("_BurtBloomThreshold"); // 缓存 Bloom 预过滤阈值属性 ID。

        private static readonly int BloomSoftKneeId = Shader.PropertyToID("_BurtBloomSoftKnee"); // 缓存 Bloom 软阈值属性 ID，降低小亮点跨像素移动时的闪烁。

        private static readonly int BloomTexelSizeId = Shader.PropertyToID("_BurtBloomTexelSize"); // 缓存 Bloom 当前源纹理 texel size 属性 ID。

        private static readonly int BloomBlurDirectionId = Shader.PropertyToID("_BurtBloomBlurDirection"); // 缓存 PC Bloom 高斯模糊方向和半径。

        private static readonly int BloomAdditiveTextureId = Shader.PropertyToID("_BurtBloomAdditiveTexture"); // 缓存 PC Bloom 高斯阶段的加法合成纹理。

        private static readonly int UseBloomAdditiveId = Shader.PropertyToID("_BurtUseBloomAdditive"); // 缓存 PC Bloom 是否启用加法合成。

        private static readonly int BloomSampleCountId = Shader.PropertyToID("_BurtBloomSampleCount"); // Cached PC Bloom Gaussian sample count.

        private static readonly int BloomSampleWeightsId = Shader.PropertyToID("_BurtBloomSampleWeights"); // Cached PC Bloom Gaussian weights.

        private static readonly int BloomSampleOffsetsId = Shader.PropertyToID("_BurtBloomSampleOffsets"); // Cached PC Bloom Gaussian offsets.

        private static readonly int BloomBypassThresholdId = Shader.PropertyToID("_BurtBloomBypassThreshold"); // Cached Bloom threshold bypass flag.

        private static readonly int BloomFireflyClampId = Shader.PropertyToID("_BurtBloomFireflyClamp"); // Cached Bloom prefilter firefly clamp.

        private static readonly int UseBloomAlphaId = Shader.PropertyToID("_BurtUseBloomAlpha"); // Cached Bloom alpha-channel output flag.

        private static readonly int BloomDebugModeId = Shader.PropertyToID("_BurtBloomDebugMode"); // Cached Bloom debug shader mode.

        private static readonly int BloomDebugYFlipId = Shader.PropertyToID("_BurtBloomDebugYFlip"); // Cached Bloom debug source orientation flag.

        private static readonly int[] BloomMipTextureIds = CreateBloomMipTextureIds(); // 缓存 Bloom mip 临时 RT 的属性 ID。

        private static readonly int BloomBlurTextureId = Shader.PropertyToID("_BurtBloomBlurTemp"); // 缓存 PC Bloom 高斯横向模糊临时 RT 的属性 ID。

        private static readonly int BloomDebugTextureId = Shader.PropertyToID("_BurtBloomDebugTexture"); // Stores the prefilter snapshot for Bloom debug view.
        private static readonly int AutoExposureTexelSizeId = Shader.PropertyToID("_BurtAutoExposureTexelSize");
        private static readonly int AutoExposureDebugModeId = Shader.PropertyToID("_BurtAutoExposureDebugMode");
        private static readonly int AutoExposureDebugParamsId = Shader.PropertyToID("_BurtAutoExposureDebugParams");
        private static readonly int[] AutoExposureTextureIds = CreateAutoExposureTextureIds();

        private static readonly Vector4[] BloomGaussianWeights = new Vector4[MaxBloomGaussianSamples]; // Reused upload buffer for Gaussian weights.

        private static readonly Vector4[] BloomGaussianOffsets = new Vector4[MaxBloomGaussianSamples]; // Reused upload buffer for Gaussian offsets.

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

        private Material postProcessMaterial;
        private static ComputeShader temporalAAComputeShader;

        private bool hasLoggedMissingShader; // 记录缺失 shader 警告是否已经输出，避免 Console 每帧刷屏。
        private static bool hasLoggedMissingTemporalAAComputeShader;
        private static bool hasLoggedMissingTemporalAAComputeKernel;

        public override string Name => "Burt Post Process"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        private sealed class BloomGaussianKernelCacheEntry
        {
            public bool Valid;
            public int Hash;
            public int RadiusKey;
            public int Width;
            public int Height;
            public bool Horizontal;
            public int TintRKey;
            public int TintGKey;
            public int TintBKey;
            public int SampleCount;
            public readonly Vector4[] Weights = new Vector4[MaxBloomGaussianSamples];
            public readonly Vector4[] Offsets = new Vector4[MaxBloomGaussianSamples];
        }

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果当前 request 没启用后处理框架，就不声明任何资源。
            {
                return; // 直接结束配置，保持关闭状态下没有额外依赖。
            }

            builder.ReadCameraColor(); // 声明先读取场景渲染完成后的 CameraColor。
            builder.ReadCameraDepth(); // TAA resolve reads depth when enabled; non-TAA paths ignore it.
            var temporalAA = builder.Request != null ? builder.Request.TemporalAA : null;
            if (temporalAA != null &&
                temporalAA.Enabled &&
                builder.ResourceRegistry != null &&
                builder.ResourceRegistry.ContainsRenderTarget(BurtRenderGraphResourceRegistry.GBuffer1Name))
            {
                builder.ReadGBuffer1();
            }

            builder.WritePostProcessColor(); // 声明第一段拷贝会写入 PostProcessColor。

            builder.ReadPostProcessColor(); // 声明第二段拷贝会读取 PostProcessColor。

            builder.WriteCameraColor(); // 声明最终会把结果写回 CameraColor，供 FinalBlit 继续输出。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行无效果后处理拷贝。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次检查开关，保证关闭后不会提交绘制命令。
            {
                return; // 未启用时直接跳过。
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

            var tonemappingMode = BurtPostProcessUtility.ResolveTonemappingMode(context.Asset); // 从当前 VolumeStack 安全解析本次后处理应该使用的 Tonemapping 模式。

            var exposureSettings = BurtPostProcessUtility.ResolvePhysicalExposureSettings(context.Request, context.Asset);
            var preExposureState = BurtPreExposureUtility.ResolveForFrame(exposureSettings);
            var residualPostExposureMultiplier = preExposureState.ResidualPostExposure;
            var postExposureMultiplier = exposureSettings.Multiplier; // 把 Global Volume 中的 EV 曝光转换成本次 shader 使用的线性倍率。

            var filmSettings = BurtPostProcessUtility.ResolveTonemappingFilmSettings(context.Asset); // 从 Global Volume 读取 UE/XRender Filmic 曲线参数，缺失时回退到默认值。

            var useColorAdjustments = BurtPostProcessUtility.ShouldUseColorAdjustments(context.Request, context.Asset); // 判断当前 VolumeStack 是否需要执行基础颜色调整。

            var colorAdjustmentsSettings = BurtPostProcessUtility.ResolveColorAdjustmentsSettings(context.Asset); // 从 Global Volume 读取基础颜色调整参数，缺失时回退到中性值。

            var temporalAA = context.Request.TemporalAA ?? BurtTemporalAARequestState.Disabled;
            var temporalAADebugRequested = BurtPostProcessUtility.IsTemporalAADebugRequested();
            var useTemporalAA = temporalAA.Enabled;
            var useTemporalAADebug = useTemporalAA && temporalAADebugRequested;
            var autoExposureDebugMode = BurtPostProcessUtility.ResolveAutoExposureDebugMode(BurtShadingDebugSettings.Mode);

            var bloomSettings = BurtPostProcessUtility.ResolveBloomSettings(context.Asset); // 从 Global Volume 读取 Bloom 参数，未启用时回退到关闭状态。

            var bloomMipCount = BurtPostProcessUtility.CalculateBloomMipCount(context.Request.Camera, bloomSettings); // 按当前相机尺寸和 Volume 上限计算实际 mip 数。
            var bloomDebugView = BurtPostProcessUtility.ResolveBloomDebugView(bloomSettings); // Shading Debug 的 Bloom Prefilter 会覆盖 Volume 内的 Bloom debug 下拉。

            var cmd = CommandBufferPool.Get(Name); // 从命令缓冲池获取 CommandBuffer，并用 Pass 名称命名。
            BurtPreExposureUtility.UploadGlobals(cmd, preExposureState);
            if (useTemporalAA &&
                BurtPreExposureUtility.ShouldInvalidateTemporalAAHistory(context.Request.Camera, preExposureState, out var preExposureInvalidationReason))
            {
                BurtTemporalAAUtility.InvalidateHistory(context.Request.Camera, preExposureInvalidationReason);
            }

            if (temporalAADebugRequested && !useTemporalAA)
            {
                ExecuteTemporalAADebugUnavailable(cmd, context.Request.Camera, cameraColorTarget);
                renderContext.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                BurtPostProcessUtility.LogPostProcessExecuted(context, tonemappingMode, postExposureMultiplier, preExposureState, useColorAdjustments, bloomSettings, 0);
                return;
            }


            if (useTemporalAA)
            {
                useTemporalAA = ExecuteTemporalAA(context, cmd, context.Request.Camera, cameraColorTarget, context.CameraDepthTarget, postProcessColorTarget, material, temporalAA, useTemporalAADebug);
                useTemporalAADebug = useTemporalAA && temporalAADebugRequested;
                if (!useTemporalAA && temporalAADebugRequested)
                {
                    ExecuteTemporalAADebugUnavailable(cmd, context.Request.Camera, cameraColorTarget);
                    renderContext.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                    BurtPostProcessUtility.LogPostProcessExecuted(context, tonemappingMode, postExposureMultiplier, preExposureState, useColorAdjustments, bloomSettings, 0);
                    return;
                }
            }

            if (useTemporalAADebug)
            {
                renderContext.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                BurtPostProcessUtility.LogPostProcessExecuted(context, tonemappingMode, postExposureMultiplier, preExposureState, useColorAdjustments, bloomSettings, 0);
                return;
            }

            if (bloomMipCount > 0) // 如果 Bloom 启用，就在同一个 Pass 内部管理临时 mip 链。
            {
                ExecuteBloom(cmd, context.Request.Camera, cameraColorTarget, material, bloomSettings, bloomDebugView, bloomMipCount, residualPostExposureMultiplier); // 先对 HDR CameraColor 做 prefilter/downsample/upsample。
            }

            if (BurtAutoExposureUtility.ShouldCapture(exposureSettings, context.Request.Camera))
            {
                BurtAutoExposureUtility.CaptureAverageLogLuminance(
                    cmd,
                    context.Request.Camera,
                    cameraColorTarget,
                    exposureSettings,
                    material,
                    AutoExposureTextureIds,
                    ShaderPass(PostProcessShaderPass.AutoExposureLogLuminanceReduce),
                    ShaderPass(PostProcessShaderPass.AutoExposureFinalReduce),
                    SourceTextureId,
                    AutoExposureTexelSizeId);
                exposureSettings = BurtPostProcessUtility.ResolvePhysicalExposureSettingsForFrame(context.Request, context.Asset, Time.deltaTime);
                postExposureMultiplier = exposureSettings.Multiplier;
                residualPostExposureMultiplier = BurtPreExposureUtility.ResolveResidualPostExposure(exposureSettings, preExposureState);
            }

            cmd.SetRenderTarget(postProcessColorTarget.Identifier); // 先绑定 PostProcessColor，让第一段全屏拷贝写入后处理中间目标。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request.Camera);

            var useBloomDebug = ShouldUseBloomDebugView(bloomDebugView, bloomMipCount); // Bloom debug 只在 Bloom 实际执行且没有其他 shading debug 抢占时显示。
            if (useBloomDebug)
            {
                SetBloomDebugSource(cmd, context.Request.Camera, cameraColorTarget.Identifier, bloomSettings, bloomDebugView, bloomMipCount, residualPostExposureMultiplier); // 把选中的 Bloom 中间纹理绑定到 debug pass。
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomDebug), MeshTopology.Triangles, 3, 1); // 直接把 Bloom debug 画到 PostProcessColor。
            }
            else if (autoExposureDebugMode > 0)
            {
                SetAutoExposureDebugSource(cmd, context.Request.Camera, cameraColorTarget.Identifier, exposureSettings, autoExposureDebugMode);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.AutoExposureDebug), MeshTopology.Triangles, 3, 1);
            }
            else
            {
                cmd.SetGlobalTexture(SourceTextureId, cameraColorTarget.Identifier); // 把 CameraColor 设置为当前拷贝 shader 的源纹理。

                cmd.SetGlobalTexture(BloomTextureId, bloomMipCount > 0 ? new RenderTargetIdentifier(BloomMipTextureIds[0]) : cameraColorTarget.Identifier); // Bloom 启用时把 mip0 交给最终合成，否则绑定一个有效兜底纹理。

                cmd.SetGlobalFloat(UseBloomId, bloomMipCount > 0 ? 1f : 0f); // 上传 Bloom 合成开关，确保默认不改变画面。

                cmd.SetGlobalFloat(BloomIntensityId, bloomMipCount > 0 ? bloomSettings.Intensity : 0f); // 上传 Bloom 强度，最终合成发生在 Tonemapping 前。

                cmd.SetGlobalFloat(UseBloomAlphaId, bloomMipCount > 0 && BurtPostProcessUtility.ShouldPreserveBloomAlpha(bloomSettings, bloomDebugView) ? 1f : 0f); // 上传 Bloom alpha 开关，默认不改变目标 alpha。

                cmd.SetGlobalFloat(TonemappingModeId, (float)tonemappingMode); // 上传 Tonemapping 模式，None 会让 shader 原样输出，其他模式会执行对应曲线。

                cmd.SetGlobalFloat(PostExposureId, residualPostExposureMultiplier); // 上传线性曝光倍率，让 Tonemapping 前可以整体调整 HDR 亮度。

                cmd.SetGlobalFloat(FilmSlopeId, filmSettings.Slope); // 上传 Film Slope，让 shader 的 UE/XRender 曲线和 Volume 参数一致。

                cmd.SetGlobalFloat(FilmToeId, filmSettings.Toe); // 上传 Film Toe，让 shader 控制暗部过渡。

                cmd.SetGlobalFloat(FilmShoulderId, filmSettings.Shoulder); // 上传 Film Shoulder，让 shader 控制高光压缩。

                cmd.SetGlobalFloat(FilmBlackClipId, filmSettings.BlackClip); // 上传 Film Black Clip，让 shader 控制黑位裁切。

                cmd.SetGlobalFloat(FilmWhiteClipId, filmSettings.WhiteClip); // 上传 Film White Clip，让 shader 控制白位裁切。

                cmd.SetGlobalFloat(FilmBlueCorrectionId, filmSettings.BlueCorrection); // 上传 Blue Correction，让 shader 对齐 XRender CombineLUT 中的蓝色修正。

                cmd.SetGlobalFloat(FilmExpandGamutId, filmSettings.ExpandGamut); // 上传 Expand Gamut，让 shader 对齐 XRender CombineLUT 中的高饱和颜色扩展。

                cmd.SetGlobalFloat(FilmToneCurveAmountId, filmSettings.ToneCurveAmount); // 上传 Tone Curve Amount，让 shader 支持按 XRender 的方式混合曲线强度。

                cmd.SetGlobalFloat(UseColorAdjustmentsId, useColorAdjustments ? 1f : 0f); // 上传 Color Adjustments 开关，让第一段后处理按需执行颜色调整。

                cmd.SetGlobalFloat(ColorAdjustmentsSaturationId, colorAdjustmentsSettings.Saturation); // 上传饱和度，1 表示不改变颜色鲜艳程度。

                cmd.SetGlobalFloat(ColorAdjustmentsContrastId, colorAdjustmentsSettings.Contrast); // 上传对比度，1 表示不改变明暗差异。

                cmd.SetGlobalFloat(ColorAdjustmentsGammaId, colorAdjustmentsSettings.Gamma); // 上传 Gamma，1 表示不改变整体明暗曲线。

                cmd.SetGlobalColor(ColorAdjustmentsColorFilterId, colorAdjustmentsSettings.ColorFilter); // 上传颜色滤镜，白色表示不额外染色。

                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.CopyAndComposite), MeshTopology.Triangles, 3, 1); // 绘制全屏三角形，把 CameraColor 处理到 PostProcessColor。
            }

            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 再绑定回 CameraColor，让第二段拷贝把后处理结果写回主颜色目标。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request.Camera);

            cmd.SetGlobalTexture(SourceTextureId, postProcessColorTarget.Identifier); // 把 PostProcessColor 设置为当前拷贝 shader 的源纹理。

            cmd.SetGlobalFloat(TonemappingModeId, (float)BurtTonemappingMode.None); // 第二段只负责回写 CameraColor，必须关闭 Tonemapping，避免同一帧重复套曲线。

            cmd.SetGlobalFloat(PostExposureId, 1f); // 第二段回写使用 1 倍曝光，保证它是纯拷贝。

            cmd.SetGlobalFloat(UseBloomId, 0f); // 第二段只负责纯拷贝，必须关闭 Bloom 合成。

            cmd.SetGlobalFloat(UseBloomAlphaId, 0f); // 第二段纯拷贝不再改 alpha。

            cmd.SetGlobalFloat(UseColorAdjustmentsId, 0f); // 第二段只负责把 PostProcessColor 写回 CameraColor，必须关闭颜色调整，避免同一帧重复执行。

            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.CopyAndComposite), MeshTopology.Triangles, 3, 1); // 再绘制一次全屏三角形，把 PostProcessColor 原样写回 CameraColor。

            ReleaseBloom(cmd, bloomMipCount, useBloomDebug && bloomDebugView == BurtBloomDebugView.Prefilter); // 释放 Bloom 临时 mip，命令会随同一个 CommandBuffer 一起提交。

            renderContext.ExecuteCommandBuffer(cmd); // 把两段拷贝命令一次性提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 释放 CommandBuffer，避免每帧产生 GC。

            BurtPostProcessUtility.LogPostProcessExecuted(context, tonemappingMode, postExposureMultiplier, preExposureState, useColorAdjustments, bloomSettings, bloomMipCount); // 如果用户开启了后处理调试日志，就输出本次后处理执行信息。
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
            bool useTemporalAADebug)
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
            if (histories.Color == null || histories.Depth == null)
            {
                BurtTemporalAAUtility.InvalidateHistory(camera, "HistoryTextureUnavailable");
                return false;
            }

            var width = Mathf.Max(1, histories.Color.width);
            var height = Mathf.Max(1, histories.Color.height);
            var colorDescriptor = BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera);
            var cameraTargetWidth = Mathf.Max(1, colorDescriptor.width);
            var cameraTargetHeight = Mathf.Max(1, colorDescriptor.height);
            var useTemporalAAUpscale = width != cameraTargetWidth || height != cameraTargetHeight;
            colorDescriptor.width = width;
            colorDescriptor.height = height;
            colorDescriptor.depthBufferBits = 0;
            colorDescriptor.msaaSamples = 1;
            colorDescriptor.useMipMap = false;
            colorDescriptor.autoGenerateMips = false;

            var scalarDescriptor = colorDescriptor;
            scalarDescriptor.colorFormat = RenderTextureFormat.RFloat;
            scalarDescriptor.sRGB = false;

            var parallaxDescriptor = colorDescriptor;
            parallaxDescriptor.colorFormat = RenderTextureFormat.RGHalf;
            parallaxDescriptor.sRGB = false;
            parallaxDescriptor.enableRandomWrite = true;

            var metadataDescriptor = colorDescriptor;
            metadataDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            metadataDescriptor.sRGB = false;

            var velocityDescriptor = colorDescriptor;
            velocityDescriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            velocityDescriptor.sRGB = false;

            var prevUseCountDescriptor = colorDescriptor;
            prevUseCountDescriptor.colorFormat = RenderTextureFormat.RInt;
            prevUseCountDescriptor.sRGB = false;
            prevUseCountDescriptor.enableRandomWrite = true;
            prevUseCountDescriptor.useMipMap = false;
            prevUseCountDescriptor.autoGenerateMips = false;

            cmd.GetTemporaryRT(TemporalAACurrentDepthTextureId, scalarDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAVelocityTextureId, velocityDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAADilatedVelocityTextureId, velocityDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAPrevUseCountTextureId, prevUseCountDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAMetadataTextureId, metadataDescriptor, FilterMode.Point);
            cmd.GetTemporaryRT(TemporalAAResolveTextureId, colorDescriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(TemporalAAParallaxRejectionTextureId, parallaxDescriptor, FilterMode.Bilinear);
            if (useTemporalAADebug)
            {
                cmd.GetTemporaryRT(TemporalAADebugTextureId, colorDescriptor, FilterMode.Bilinear);
            }

            var currentDepth = new RenderTargetIdentifier(TemporalAACurrentDepthTextureId);
            var velocity = new RenderTargetIdentifier(TemporalAAVelocityTextureId);
            var dilatedVelocity = new RenderTargetIdentifier(TemporalAADilatedVelocityTextureId);
            var prevUseCount = new RenderTargetIdentifier(TemporalAAPrevUseCountTextureId);
            var metadata = new RenderTargetIdentifier(TemporalAAMetadataTextureId);
            var resolveTarget = new RenderTargetIdentifier(TemporalAAResolveTextureId);
            var parallaxRejection = new RenderTargetIdentifier(TemporalAAParallaxRejectionTextureId);
            var debugTarget = new RenderTargetIdentifier(TemporalAADebugTextureId);

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalVector(TemporalAAStencilTexelSizeId, new Vector4(1f / cameraTargetWidth, 1f / cameraTargetHeight, cameraTargetWidth, cameraTargetHeight));
            cmd.SetGlobalVector(TemporalAAUpscaleTexelSizeId, new Vector4(1f / width, 1f / height, width, height));
            cmd.SetGlobalVector(TemporalAAUpscaleParamsId, new Vector4(cameraTargetWidth, cameraTargetHeight, cameraTargetWidth / (float)width, cameraTargetHeight / (float)height));
            SetTemporalAAGlobals(cmd, temporalAA, width, height, historyValid);

            cmd.SetRenderTarget(currentDepth);
            SetTemporalAAViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACurrentDepth), MeshTopology.Triangles, 3, 1);

            cmd.SetRenderTarget(velocity);
            SetTemporalAAViewport(cmd, width, height);
            cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACameraVelocity), MeshTopology.Triangles, 3, 1);

            var drewObjectMotionVectors = DrawTemporalAAObjectMotionVectors(context, cmd, camera, velocity, cameraDepthTarget, width, height, !useTemporalAAUpscale);
            temporalAA.ObjectMotionVectorPassDrawn = drewObjectMotionVectors;
            temporalAA.VelocityMode = drewObjectMotionVectors ? BurtTemporalAAVelocityMode.CameraAndObject : BurtTemporalAAVelocityMode.CameraOnly;

            cmd.SetRenderTarget(dilatedVelocity);
            SetTemporalAAViewport(cmd, width, height);
            cmd.SetGlobalTexture(TemporalAAVelocityTextureId, velocity);
            cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
            cmd.SetGlobalTexture(TemporalAAStencilTextureId, cameraDepthTarget.Identifier, RenderTextureSubElement.Stencil);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAVelocityDilation), MeshTopology.Triangles, 3, 1);

            cmd.SetRenderTarget(prevUseCount);
            SetTemporalAAViewport(cmd, width, height);
            cmd.ClearRenderTarget(false, true, Color.clear);

            if (!TryExecuteTemporalAAPrevUseCountCompute(cmd, dilatedVelocity, prevUseCount, width, height))
            {
                cmd.SetRenderTarget(cameraColorTarget.Identifier);
                SetTemporalAAViewport(cmd, width, height);
                cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
                cmd.SetRandomWriteTarget(1, prevUseCount);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAABuildPrevUseCount), MeshTopology.Triangles, 3, 1);
                cmd.ClearRandomWriteTargets();
            }

            var blackTexture = new RenderTargetIdentifier(Texture2D.blackTexture);
            if (!TryExecuteTemporalAADecimateHistoryCompute(cmd, dilatedVelocity, currentDepth, historyValid ? new RenderTargetIdentifier(histories.Depth) : currentDepth, prevUseCount, parallaxRejection, width, height))
            {
                cmd.SetRenderTarget(parallaxRejection);
                SetTemporalAAViewport(cmd, width, height);
                cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
                cmd.SetGlobalTexture(TemporalAAPrevUseCountTextureId, prevUseCount);
                cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
                cmd.SetGlobalTexture(TemporalAADepthHistoryTextureId, historyValid ? new RenderTargetIdentifier(histories.Depth) : currentDepth);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAADecimateHistory), MeshTopology.Triangles, 3, 1);
            }

            cmd.SetRenderTarget(metadata);
            SetTemporalAAViewport(cmd, width, height);
            cmd.SetGlobalTexture(TemporalAARawVelocityTextureId, velocity);
            cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
            cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
            cmd.SetGlobalTexture(TemporalAAParallaxRejectionTextureId, parallaxRejection);
            cmd.SetGlobalTexture(TemporalAAStencilTextureId, cameraDepthTarget.Identifier, RenderTextureSubElement.Stencil);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAMetadata), MeshTopology.Triangles, 3, 1);

            var hasGBuffer1 = context.GBuffer1Target.IsValid;
            cmd.SetGlobalTexture(SourceTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(TemporalAAHistoryTextureId, historyValid ? new RenderTargetIdentifier(histories.Color) : cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(TemporalAADepthHistoryTextureId, historyValid ? new RenderTargetIdentifier(histories.Depth) : currentDepth);
            cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
            cmd.SetGlobalTexture(TemporalAARawVelocityTextureId, velocity);
            cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
            cmd.SetGlobalTexture(TemporalAAPrevUseCountTextureId, prevUseCount);
            cmd.SetGlobalTexture(TemporalAAMetadataTextureId, metadata);
            cmd.SetGlobalTexture(TemporalAAParallaxRejectionTextureId, parallaxRejection);
            cmd.SetGlobalFloat(TemporalAAHasGBufferId, hasGBuffer1 ? 1f : 0f);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.GBuffer1Id, hasGBuffer1 ? context.GBuffer1Target.Identifier : blackTexture);
            cmd.SetGlobalFloat(ShadingDebugEnabledId, 0f);

            cmd.SetRenderTarget(resolveTarget);
            SetTemporalAAViewport(cmd, width, height);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAResolve), MeshTopology.Triangles, 3, 1);

            if (useTemporalAADebug)
            {
                cmd.SetRenderTarget(debugTarget);
                SetTemporalAAViewport(cmd, width, height);
                cmd.SetGlobalFloat(ShadingDebugEnabledId, 1f);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAResolve), MeshTopology.Triangles, 3, 1);
                cmd.SetGlobalFloat(ShadingDebugEnabledId, 0f);
            }

            cmd.SetRenderTarget(histories.Depth);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.SetGlobalTexture(TemporalAAVelocityTextureId, dilatedVelocity);
            cmd.SetGlobalTexture(TemporalAACurrentDepthTextureId, currentDepth);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAAClosestDepthCopy), MeshTopology.Triangles, 3, 1);

            cmd.SetRenderTarget(histories.Color);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
            cmd.SetGlobalTexture(SourceTextureId, resolveTarget);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACopy), MeshTopology.Triangles, 3, 1);
            BurtTemporalAAUtility.MarkHistoryValid(camera);

            cmd.SetRenderTarget(postProcessColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            DisablePostProcessEffects(cmd);
            cmd.SetGlobalTexture(SourceTextureId, resolveTarget);
            cmd.SetGlobalTexture(TemporalAAUpscaleCurrentTextureId, cameraColorTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(useTemporalAAUpscale ? PostProcessShaderPass.TemporalAAUpscale : PostProcessShaderPass.TemporalAACopy), MeshTopology.Triangles, 3, 1);

            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            DisablePostProcessEffects(cmd);
            cmd.SetGlobalTexture(SourceTextureId, useTemporalAADebug ? debugTarget : postProcessColorTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.TemporalAACopy), MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalFloat(ShadingDebugEnabledId, BurtShadingDebugSettings.IsDebugging ? 1f : 0f);

            if (useTemporalAADebug)
            {
                cmd.ReleaseTemporaryRT(TemporalAADebugTextureId);
            }

            cmd.ReleaseTemporaryRT(TemporalAAParallaxRejectionTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAResolveTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAMetadataTextureId);
            cmd.ReleaseTemporaryRT(TemporalAAPrevUseCountTextureId);
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
            cmd.SetGlobalVector(TemporalAAJitterId, new Vector4(temporalAA.Jitter.x, temporalAA.Jitter.y, temporalAA.JitterPixels.x, temporalAA.JitterPixels.y));
            cmd.SetGlobalVector(TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.SetGlobalVector(TemporalAAParamsId, new Vector4(0f, 0f, historyValid ? 1f : 0f, temporalAA.FrameIndex));
            cmd.SetGlobalVector(TemporalAAParams2Id, new Vector4(temporalAA.Settings.Sharpness, temporalAA.Settings.JitterScale, 0f, 0f));
            cmd.SetGlobalVector(TemporalAAResponsiveParamsId, new Vector4(0f, temporalAA.Settings.UntrustedMotionFeedbackScale, 0f, 0f));
            cmd.SetGlobalVector(TemporalAAEdgeParamsId, new Vector4(temporalAA.Settings.MotionEdgeResponsiveStrength, temporalAA.Settings.DepthEdgeResponsiveStrength, temporalAA.Settings.HistoryClampTightness, temporalAA.Settings.DepthWeightedFilterFloor));
            cmd.SetGlobalFloat(TemporalAAHistoryExposureCorrectionId, temporalAA.HistoryExposureCorrection);

            ComputeTemporalAACurrentSampleWeights(temporalAA.Jitter, out var weights0, out var weights1, out var weights2);
            cmd.SetGlobalVector(TemporalAACurrentSampleWeights0Id, weights0);
            cmd.SetGlobalVector(TemporalAACurrentSampleWeights1Id, weights1);
            cmd.SetGlobalVector(TemporalAACurrentSampleWeights2Id, weights2);
        }

        private static bool TryExecuteTemporalAAPrevUseCountCompute(
            CommandBuffer cmd,
            RenderTargetIdentifier velocity,
            RenderTargetIdentifier prevUseCount,
            int width,
            int height)
        {
            if (cmd == null || !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            var shader = GetTemporalAAComputeShader();
            if (shader == null || !TryFindTemporalAAComputeKernel(shader, "BuildPrevUseCountCS", out var kernel))
            {
                return false;
            }

            cmd.SetComputeTextureParam(shader, kernel, TemporalAAVelocityTextureId, velocity);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAPrevUseCountTextureId, prevUseCount);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.DispatchCompute(shader, kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);
            return true;
        }

        private static bool TryExecuteTemporalAADecimateHistoryCompute(
            CommandBuffer cmd,
            RenderTargetIdentifier velocity,
            RenderTargetIdentifier currentDepth,
            RenderTargetIdentifier depthHistory,
            RenderTargetIdentifier prevUseCount,
            RenderTargetIdentifier parallaxRejection,
            int width,
            int height)
        {
            if (cmd == null || !SystemInfo.supportsComputeShaders)
            {
                return false;
            }

            var shader = GetTemporalAAComputeShader();
            if (shader == null || !TryFindTemporalAAComputeKernel(shader, "DecimateHistoryCS", out var kernel))
            {
                return false;
            }

            cmd.SetComputeTextureParam(shader, kernel, TemporalAAVelocityTextureId, velocity);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAACurrentDepthTextureId, currentDepth);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAADepthHistoryTextureId, depthHistory);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAPrevUseCountTextureId, prevUseCount);
            cmd.SetComputeTextureParam(shader, kernel, TemporalAAParallaxRejectionTextureId, parallaxRejection);
            cmd.SetComputeVectorParam(shader, TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
            cmd.DispatchCompute(shader, kernel, Mathf.CeilToInt(width / 8f), Mathf.CeilToInt(height / 8f), 1);
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
            bool bindCameraDepthStencil)
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
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(new ShaderTagId("BurtMotionVectors"), sortingSettings)
            {
                perObjectData = PerObjectData.MotionVectors,
                enableDynamicBatching = false,
                enableInstancing = false
            };

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, camera.cullingMask);
            context.ScriptableContext.DrawRenderers(context.Request.CullingResults, ref drawingSettings, ref filteringSettings);
            return true;
        }

        private static bool ShouldUseBloomDebugView(BurtBloomDebugView debugView, int mipCount)
        {
            return mipCount > 0 &&
                debugView != BurtBloomDebugView.Disabled &&
                !BurtPostProcessUtility.IsPostProcessSuppressedByShadingDebug();
        }

        private static void DisablePostProcessEffects(CommandBuffer cmd)
        {
            BurtPreExposureUtility.UploadGlobals(cmd, BurtPreExposureState.Default);
            cmd.SetGlobalFloat(UseBloomId, 0f);
            cmd.SetGlobalFloat(UseBloomAlphaId, 0f);
            cmd.SetGlobalFloat(TonemappingModeId, 0f);
            cmd.SetGlobalFloat(PostExposureId, 1f);
            cmd.SetGlobalFloat(UseColorAdjustmentsId, 0f);
        }

        private static int[] CreateBloomMipTextureIds() // 创建 Bloom mip 临时 RT 的属性 ID 数组。
        {
            var ids = new int[MaxBloomMipCount]; // 固定上限，避免每帧分配数组。

            for (var i = 0; i < ids.Length; i++) // 遍历所有可能的 Bloom mip。
            {
                ids[i] = Shader.PropertyToID("_BurtBloomMip" + i); // 为每级 mip 生成稳定的全局纹理 ID。
            }

            return ids; // 返回缓存数组。
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

        private static void ExecuteBloom( // 在单个后处理 Pass 内部执行 Bloom mip 链。
            CommandBuffer cmd, // 接收当前后处理 CommandBuffer。
            Camera camera, // 接收当前相机，用来创建匹配尺寸的临时 RT。
            BurtRenderTargetHandle cameraColorTarget, // 接收 HDR CameraColor，作为 Bloom prefilter 源。
            Material material, // 接收后处理材质，复用其中的 Bloom 子 Pass。
            BurtBloomSettings settings, // 接收当前 Bloom 参数。
            BurtBloomDebugView debugView, // 接收当前有效 Bloom debug 视图，可能来自 Shading Debug 覆盖。
            int mipCount, // 接收本帧实际使用的 mip 数。
            float postExposureMultiplier) // 接收当前 Tonemapping 前曝光倍率，用来让 Bloom 阈值和最终曝光保持一致。
        {
            var preserveAlpha = BurtPostProcessUtility.ShouldPreserveBloomAlpha(settings, debugView); // Alpha debug also needs an alpha-capable Bloom chain even when the Volume output toggle is off.
            var descriptor = BurtPostProcessUtility.CreateBloomRenderTextureDescriptor(camera, 1, 1, settings, debugView); // Bloom mip 优先使用轻量 HDR 格式，尺寸在每级申请前填入。

            for (var i = 0; i < mipCount; i++) // 逐级申请 Bloom 临时 RT。
            {
                descriptor.width = GetBloomMipWidth(camera, i); // 使用和后续 viewport/source 完全一致的 mip 宽度。
                descriptor.height = GetBloomMipHeight(camera, i); // 使用和后续 viewport/source 完全一致的 mip 高度。
                cmd.GetTemporaryRT(BloomMipTextureIds[i], descriptor, FilterMode.Bilinear); // 申请当前 mip，使用双线性过滤配合上采样。
            }

            cmd.SetGlobalFloat(BloomThresholdId, settings.Threshold); // 上传 Bloom 阈值。
            cmd.SetGlobalFloat(BloomSoftKneeId, settings.SoftKnee); // 上传 Bloom 软阈值，让 prefilter 过渡更连续。
            cmd.SetGlobalFloat(BloomBypassThresholdId, BurtPostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings) ? 1f : 0f); // threshold 为 -1 时对齐 XRender，跳过亮度裁剪让所有像素参与 Bloom。
            cmd.SetGlobalFloat(BloomFireflyClampId, BloomFireflyClamp); // 在 Bloom prefilter 前软压极端 HDR 亮点，避免异常像素扩散到整条 mip 链。
            cmd.SetGlobalFloat(UseBloomAlphaId, preserveAlpha ? 1f : 0f); // 上传 Bloom alpha 开关，让预过滤和高斯阶段保持一致。
            cmd.SetGlobalFloat(PostExposureId, BurtPostProcessUtility.ResolveBloomPrefilterPostExposure(postExposureMultiplier)); // Bloom 阈值使用 Tonemapping 前曝光后的亮度判断，贴近最终画面亮度。
            cmd.SetRenderTarget(new RenderTargetIdentifier(BloomMipTextureIds[0])); // prefilter 写入 mip0。
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, GetBloomMipWidth(camera, 0), GetBloomMipHeight(camera, 0));
            SetBloomSource(cmd, cameraColorTarget.Identifier, camera); // CameraColor 是 prefilter 源。
            cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomPrefilter), MeshTopology.Triangles, 3, 1); // 执行高光预过滤。
            if (ShouldUseBloomDebugView(debugView, mipCount) && debugView == BurtBloomDebugView.Prefilter)
            {
                var debugDescriptor = BurtPostProcessUtility.CreateBloomRenderTextureDescriptor(camera, GetBloomMipWidth(camera, 0), GetBloomMipHeight(camera, 0), settings, debugView);
                cmd.GetTemporaryRT(BloomDebugTextureId, debugDescriptor, FilterMode.Bilinear);
                cmd.SetRenderTarget(new RenderTargetIdentifier(BloomDebugTextureId));
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, debugDescriptor.width, debugDescriptor.height);
                SetBloomSource(cmd, new RenderTargetIdentifier(BloomMipTextureIds[0]), debugDescriptor.width, debugDescriptor.height);
                cmd.SetGlobalFloat(UseBloomId, 0f);
                cmd.SetGlobalFloat(TonemappingModeId, (float)BurtTonemappingMode.None);
                cmd.SetGlobalFloat(PostExposureId, 1f);
                cmd.SetGlobalFloat(UseColorAdjustmentsId, 0f);
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.CopyAndComposite), MeshTopology.Triangles, 3, 1);
            }

            for (var i = 1; i < mipCount; i++) // 从 mip0 开始逐级下采样。
            {
                var sourceId = BloomMipTextureIds[i - 1]; // 上一层作为当前下采样源。
                var targetId = BloomMipTextureIds[i]; // 当前层作为下采样目标。

                cmd.SetRenderTarget(new RenderTargetIdentifier(targetId)); // 绑定当前 mip 作为写入目标。
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, GetBloomMipWidth(camera, i), GetBloomMipHeight(camera, i));
                SetBloomSource(cmd, new RenderTargetIdentifier(sourceId), GetBloomMipWidth(camera, i - 1), GetBloomMipHeight(camera, i - 1)); // 设置上一层源纹理和 texel size。
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomDownsample), MeshTopology.Triangles, 3, 1); // 执行 4 tap 下采样。
            }

            for (var i = mipCount - 1; i >= 0; i--) // 按 XRender PC Bloom 的思路，从小 mip 到大 mip 做高斯并叠加。
            {
                var width = GetBloomMipWidth(camera, i); // 计算当前 mip 宽度。
                var height = GetBloomMipHeight(camera, i); // 计算当前 mip 高度。
                var stageIndex = mipCount - 1 - i; // XRender PC Bloom 从最小 mip 的 Filter6 逐步走向 Filter1。
                var blurRadius = BurtPostProcessUtility.CalculateBloomBlurRadius(settings, width, stageIndex); // 用 scatter、sizeScale 和 Filter6..Filter1 百分比计算高斯半径。
                var stageTint = BurtPostProcessUtility.ResolveBloomStageTint(settings, stageIndex); // 获取当前 Bloom 阶段的 tint，纵向阶段乘入以贴近 XRender。
                var blurDescriptor = BurtPostProcessUtility.CreateBloomRenderTextureDescriptor(camera, width, height, settings, debugView); // 横向模糊临时 RT 只需要当前 mip 尺寸。

                cmd.GetTemporaryRT(BloomBlurTextureId, blurDescriptor, FilterMode.Bilinear); // 每级只临时申请一张横向高斯 RT，用完立即释放。
                cmd.SetRenderTarget(new RenderTargetIdentifier(BloomBlurTextureId)); // 横向高斯写入同尺寸临时 RT。
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                SetBloomSource(cmd, new RenderTargetIdentifier(BloomMipTextureIds[i]), width, height); // 当前 downsample mip 作为横向模糊源。
                SetBloomGaussianKernel(cmd, blurRadius, width, height, true, Color.white); // 横向阶段只做滤波，保持白色权重。
                cmd.SetGlobalVector(BloomBlurDirectionId, new Vector4(1f, 0f, blurRadius, 0f)); // 横向模糊轴和半径，shader 内按 XRender PC 高斯公式计算权重。
                cmd.SetGlobalFloat(UseBloomAdditiveId, 0f); // 横向阶段不做加法合成。
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomGaussian), MeshTopology.Triangles, 3, 1); // 执行 PC Bloom 横向高斯。

                cmd.SetRenderTarget(new RenderTargetIdentifier(BloomMipTextureIds[i])); // 纵向高斯写回当前 mip，后续更大 mip 会把它作为 additive 输入。
                BurtRenderTargetDescriptorUtility.SetViewport(cmd, width, height);
                SetBloomSource(cmd, new RenderTargetIdentifier(BloomBlurTextureId), width, height); // 横向模糊结果作为纵向源。
                SetBloomGaussianKernel(cmd, blurRadius, width, height, false, stageTint); // 纵向阶段乘入每级 tint，允许按 XRender 风格调 Bloom 颜色和权重。
                cmd.SetGlobalVector(BloomBlurDirectionId, new Vector4(0f, 1f, blurRadius, 0f)); // 纵向模糊轴和半径，shader 内按 XRender PC 高斯公式计算权重。
                cmd.SetGlobalTexture(BloomAdditiveTextureId, i + 1 < mipCount ? new RenderTargetIdentifier(BloomMipTextureIds[i + 1]) : new RenderTargetIdentifier(BloomBlurTextureId)); // 更小一级的已累积结果作为 additive，最小 mip 绑定兜底纹理。
                cmd.SetGlobalFloat(UseBloomAdditiveId, i + 1 < mipCount ? 1f : 0f); // 最小 mip 没有 additive，其他 mip 叠加上一轮结果。
                cmd.DrawProcedural(Matrix4x4.identity, material, ShaderPass(PostProcessShaderPass.BloomGaussian), MeshTopology.Triangles, 3, 1); // 执行 PC Bloom 纵向高斯并合成。
                cmd.ReleaseTemporaryRT(BloomBlurTextureId); // 当前 mip 的横向高斯 RT 已不再需要，立即释放以降低峰值显存。
            }

            cmd.SetGlobalFloat(UseBloomAdditiveId, 0f); // Bloom 链结束后关闭加法合成，避免影响后续全屏 pass。
            cmd.SetGlobalFloat(PostExposureId, 1f); // Bloom 链结束后恢复默认曝光，后续最终合成会重新上传正确值。
        }

        private static void SetBloomDebugSource(CommandBuffer cmd, Camera camera, RenderTargetIdentifier cameraColorTarget, BurtBloomSettings settings, BurtBloomDebugView debugView, int mipCount, float postExposureMultiplier) // 选择 Bloom debug 要显示的中间纹理。
        {
            var sourceId = BloomMipTextureIds[0]; // 默认显示最终合成后的 Bloom mip0。
            var sourceWidth = GetBloomMipWidth(camera, 0);
            var sourceHeight = GetBloomMipHeight(camera, 0);
            var debugMode = ResolveBloomDebugShaderMode(debugView);
            var useCameraColorSource = debugView == BurtBloomDebugView.ThresholdMask;

            if (useCameraColorSource)
            {
                sourceWidth = BurtPostProcessUtility.ResolveBloomSourceWidth(camera);
                sourceHeight = BurtPostProcessUtility.ResolveBloomSourceHeight(camera);
            }
            else if (debugView == BurtBloomDebugView.Prefilter)
            {
                sourceId = BloomDebugTextureId;
            }
            else if (debugView >= BurtBloomDebugView.Mip1 && debugView <= BurtBloomDebugView.Mip5)
            {
                var mipIndex = Mathf.Clamp((int)debugView - (int)BurtBloomDebugView.Mip1 + 1, 0, Mathf.Max(0, mipCount - 1));
                sourceId = BloomMipTextureIds[mipIndex];
                sourceWidth = GetBloomMipWidth(camera, mipIndex);
                sourceHeight = GetBloomMipHeight(camera, mipIndex);
            }

            cmd.SetGlobalTexture(SourceTextureId, useCameraColorSource ? cameraColorTarget : new RenderTargetIdentifier(sourceId));
            cmd.SetGlobalVector(BloomTexelSizeId, new Vector4(1f / Mathf.Max(1, sourceWidth), 1f / Mathf.Max(1, sourceHeight), sourceWidth, sourceHeight));
            if (debugView == BurtBloomDebugView.ThresholdMask)
            {
                cmd.SetGlobalFloat(BloomThresholdId, settings.Threshold);
                cmd.SetGlobalFloat(BloomSoftKneeId, settings.SoftKnee);
                cmd.SetGlobalFloat(BloomBypassThresholdId, BurtPostProcessUtility.ShouldBypassBloomPrefilterThreshold(settings) ? 1f : 0f);
                cmd.SetGlobalFloat(BloomFireflyClampId, BloomFireflyClamp);
                cmd.SetGlobalFloat(PostExposureId, BurtPostProcessUtility.ResolveBloomPrefilterPostExposure(postExposureMultiplier));
            }

            cmd.SetGlobalFloat(BloomDebugModeId, debugMode);
            cmd.SetGlobalFloat(BloomDebugYFlipId, debugView == BurtBloomDebugView.Prefilter ? 1f : 0f);
        }

        private static float ResolveBloomDebugShaderMode(BurtBloomDebugView debugView)
        {
            if (debugView == BurtBloomDebugView.Alpha)
            {
                return 1f;
            }

            return debugView == BurtBloomDebugView.ThresholdMask ? 2f : 0f;
        }

        private static void ReleaseBloom(CommandBuffer cmd, int mipCount, bool releaseDebugTexture) // 释放本帧申请的 Bloom 临时 RT。
        {
            for (var i = 0; i < mipCount; i++) // 只释放实际申请过的 mip。
            {
                cmd.ReleaseTemporaryRT(BloomMipTextureIds[i]); // 释放当前 Bloom mip。
            }

            if (releaseDebugTexture)
            {
                cmd.ReleaseTemporaryRT(BloomDebugTextureId); // 释放 prefilter debug 快照。
            }
        }

        private static void SetBloomGaussianKernel(CommandBuffer cmd, float radius, int width, int height, bool horizontal, Color tint) // Upload XRender PC-style bilinear-merged Gaussian kernel.
        {
            var radiusKey = Mathf.RoundToInt(Mathf.Clamp(radius, 0.00001f, MaxBloomGaussianSamples - 1) * BloomGaussianKernelRadiusCacheScale); // Quantize radius so equivalent Bloom frames reuse kernels.
            var tintRKey = Mathf.RoundToInt(tint.r * BloomGaussianKernelTintCacheScale); // Quantize stage tint for compact cache lookup.
            var tintGKey = Mathf.RoundToInt(tint.g * BloomGaussianKernelTintCacheScale);
            var tintBKey = Mathf.RoundToInt(tint.b * BloomGaussianKernelTintCacheScale);
            var sourceWidth = Mathf.Max(1, width);
            var sourceHeight = Mathf.Max(1, height);
            var cacheHash = CalculateBloomGaussianKernelHash(radiusKey, sourceWidth, sourceHeight, horizontal, tintRKey, tintGKey, tintBKey);
            var cacheEntry = GetBloomGaussianKernelCacheEntry(cacheHash, radiusKey, sourceWidth, sourceHeight, horizontal, tintRKey, tintGKey, tintBKey);
            var sampleCount = cacheEntry.SampleCount;

            CopyBloomGaussianKernel(cacheEntry, BloomGaussianWeights, BloomGaussianOffsets);

            cmd.SetGlobalFloat(BloomSampleCountId, sampleCount); // Shader reads the active count from fixed-size arrays.
            cmd.SetGlobalVectorArray(BloomSampleWeightsId, BloomGaussianWeights); // Upload normalized weights.
            cmd.SetGlobalVectorArray(BloomSampleOffsetsId, BloomGaussianOffsets); // Upload UV-space offsets.
        }

        private static BloomGaussianKernelCacheEntry GetBloomGaussianKernelCacheEntry(int hash, int radiusKey, int width, int height, bool horizontal, int tintRKey, int tintGKey, int tintBKey)
        {
            for (var i = 0; i < BloomGaussianKernelCache.Length; i++)
            {
                var entry = BloomGaussianKernelCache[i];
                if (entry.Valid &&
                    entry.Hash == hash &&
                    entry.RadiusKey == radiusKey &&
                    entry.Width == width &&
                    entry.Height == height &&
                    entry.Horizontal == horizontal &&
                    entry.TintRKey == tintRKey &&
                    entry.TintGKey == tintGKey &&
                    entry.TintBKey == tintBKey)
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
            target.TintRKey = tintRKey;
            target.TintGKey = tintGKey;
            target.TintBKey = tintBKey;
            target.SampleCount = ComputeBloomGaussianKernel(radiusKey / BloomGaussianKernelRadiusCacheScale, width, height, horizontal, tintRKey / BloomGaussianKernelTintCacheScale, tintGKey / BloomGaussianKernelTintCacheScale, tintBKey / BloomGaussianKernelTintCacheScale, target.Weights, target.Offsets); // Mirrors XRender Compute1DGaussianFilterKernel.

            return target;
        }

        private static int CalculateBloomGaussianKernelHash(int radiusKey, int width, int height, bool horizontal, int tintRKey, int tintGKey, int tintBKey)
        {
            unchecked
            {
                var hash = 17;
                hash = hash * 31 + radiusKey;
                hash = hash * 31 + width;
                hash = hash * 31 + height;
                hash = hash * 31 + (horizontal ? 1 : 0);
                hash = hash * 31 + tintRKey;
                hash = hash * 31 + tintGKey;
                hash = hash * 31 + tintBKey;

                return hash;
            }
        }

        private static void CopyBloomGaussianKernel(BloomGaussianKernelCacheEntry entry, Vector4[] weights, Vector4[] offsets)
        {
            for (var i = 0; i < MaxBloomGaussianSamples; i++)
            {
                weights[i] = entry.Weights[i];
                offsets[i] = entry.Offsets[i];
            }
        }

        private static int ComputeBloomGaussianKernel(float radius, int width, int height, bool horizontal, float tintR, float tintG, float tintB, Vector4[] weights, Vector4[] offsets) // Mirrors XRender Compute1DGaussianFilterKernel.
        {
            var clampedRadius = Mathf.Clamp(radius, 0.00001f, MaxBloomGaussianSamples - 1); // Avoid divide-by-zero and cap sample count.
            var integerRadius = Mathf.Min(Mathf.CeilToInt(clampedRadius), MaxBloomGaussianSamples - 1); // XRender uses ceil(radius) as integer radius.
            var sampleCount = 0; // Count bilinear-merged samples.
            var weightSum = 0f; // Used to normalize weights.
            var tintWeight = new Vector4(tintR, tintG, tintB, 1f); // RGB tint is stage-specific; alpha keeps the normalized Gaussian kernel.

            for (var sampleIndex = -integerRadius; sampleIndex <= integerRadius && sampleCount < MaxBloomGaussianSamples; sampleIndex += 2)
            {
                var weight0 = NormalDistributionUnscaled(sampleIndex, clampedRadius); // Current tap weight.
                var weight1 = sampleIndex != integerRadius ? NormalDistributionUnscaled(sampleIndex + 1, clampedRadius) : 0f; // Next tap weight.
                var totalWeight = weight0 + weight1; // Merged bilinear sample weight.
                var sampleOffset = sampleIndex + weight1 / Mathf.Max(totalWeight, 0.00001f); // XRender bilinear offset merge formula.
                var uvOffset = horizontal ? new Vector4(sampleOffset / width, 0f, 0f, 0f) : new Vector4(0f, sampleOffset / height, 0f, 0f); // Convert to UV-space offset.

                weights[sampleCount] = tintWeight * totalWeight;
                offsets[sampleCount] = uvOffset;
                weightSum += totalWeight;
                sampleCount++;
            }

            var weightSumInverse = 1f / Mathf.Max(weightSum, 0.00001f); // Normalize to preserve brightness.
            for (var i = 0; i < sampleCount; i++)
            {
                weights[i] *= weightSumInverse;
            }

            for (var i = sampleCount; i < MaxBloomGaussianSamples; i++)
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

        private static void SetBloomSource(CommandBuffer cmd, RenderTargetIdentifier source, Camera camera) // 用相机尺寸设置 Bloom 源纹理。
        {
            var width = Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.width : camera.pixelWidth); // 读取源宽度。
            var height = Mathf.Max(1, camera.targetTexture != null ? camera.targetTexture.height : camera.pixelHeight); // 读取源高度。

            SetBloomSource(cmd, source, width, height); // 转到统一上传函数。
        }

        private static void SetBloomSource(CommandBuffer cmd, RenderTargetIdentifier source, int width, int height) // 上传 Bloom 源纹理和 texel size。
        {
            cmd.SetGlobalTexture(SourceTextureId, source); // 复用后处理源纹理属性，供 Bloom 子 Pass 采样。
            cmd.SetGlobalVector(BloomTexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height)); // 上传 texel size，便于 shader 做邻域采样。
        }

        private static void SetAutoExposureDebugSource(CommandBuffer cmd, Camera camera, RenderTargetIdentifier source, BurtPhysicalExposureSettings exposure, int debugMode)
        {
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
        }

        private static int GetBloomMipWidth(Camera camera, int mipIndex) // 计算指定 Bloom mip 的宽度。
        {
            return BurtPostProcessUtility.GetBloomMipWidth(camera, mipIndex); // 和 Bloom 诊断共用同一套尺寸计算。
        }

        private static int GetBloomMipHeight(Camera camera, int mipIndex) // 计算指定 Bloom mip 的高度。
        {
            return BurtPostProcessUtility.GetBloomMipHeight(camera, mipIndex); // 和 Bloom 诊断共用同一套尺寸计算。
        }

        private Material GetPostProcessMaterial() // 定义获取后处理材质的内部辅助函数。
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

    internal sealed class BurtReleasePostProcessColorPass : BurtRenderPass // 定义后处理中间颜色释放 Pass，负责释放 PostProcessColor 临时 RT。
    {
        public override string Name => "Burt Release Post Process Color"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(builder.Request, builder.Asset)) // 如果后处理框架没有启用，就不声明资源依赖。
            {
                return; // 直接结束配置，避免关闭状态下出现无效资源读取。
            }

            builder.ReadPostProcessColor(); // 声明这个 Pass 依赖 PostProcessColor，表示它要结束这个临时资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 PostProcessColor 临时 RT 的释放。
        {
            if (!BurtPostProcessUtility.ShouldUsePostProcessFramework(context.Request, context.Asset)) // 执行阶段再次确认后处理框架仍然启用。
            {
                return; // 未启用时直接跳过，不释放未申请的资源。
            }

            var renderContext = context.ScriptableContext; // 从上下文中取出 Unity SRP 渲染上下文。

            var postProcessColorTarget = context.PostProcessColorTarget; // 从资源表中读取 PostProcessColor 句柄。

            if (!postProcessColorTarget.IsValid) // 如果句柄无效，说明当前图没有注册后处理中间 RT。
            {
                return; // 直接跳过，避免释放不存在的临时 RT。
            }

            var cmd = CommandBufferPool.Get(Name); // 从命令缓冲池获取 CommandBuffer，并用当前 Pass 名称命名。

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.PostProcessColorTextureId); // 释放前面申请的 PostProcessColor 临时 RT，避免资源泄漏到下一帧或下一个 request。

            renderContext.ExecuteCommandBuffer(cmd); // 把释放命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }
}
