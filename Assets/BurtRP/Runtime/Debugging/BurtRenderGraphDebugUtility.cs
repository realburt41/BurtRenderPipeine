using System.Collections.Generic; // 引入泛型集合命名空间，用 IReadOnlyList、Dictionary 和 List 组织 Pass 与资源关系。
using System.Globalization; // 用 InvariantCulture 输出稳定的小数格式，避免不同系统区域设置影响 dump。
using System.Text; // 引入文本构建命名空间，用 StringBuilder 组合多行 RenderGraph dump。
using UnityEngine; // 引入 UnityEngine 命名空间，用 Camera、RenderTexture 和 RenderTextureDescriptor 输出 RT 诊断状态。

namespace Burt.RenderPipeline // 定义 BurtRP 的运行时命名空间，让工具能直接访问 BurtRenderRequest 和资源使用类型。
{
    internal static class BurtRenderGraphDebugUtility // 定义 RenderGraph dump 格式化工具，把日志排版细节从 BurtRenderGraph 执行类中拆出来。
    {
        private const int BaseDumpCapacity = 768; // 定义 dump 基础容量，覆盖标题、Request、Camera、校验和资源摘要等固定内容。

        private const int PerPassDumpCapacity = 220; // 定义每个 Pass 的估算容量，减少多 Pass 场景下 StringBuilder 扩容次数。

        private const int MaxCullCandidateDumpLines = 16; // Keep future-culling diagnostics compact in large graphs.

        private const int MaxCullReadinessDumpLines = 24; // Limit per-pass why-not-cull rows so full dumps stay readable.

        public static string BuildDump( // 保留旧签名，兼容只需要 Pass 资源声明的调用方。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出 Request 类型和 Camera 名称。
            int passCount, // 接收 RenderGraph 当前 Pass 数量，确保 dump 中的 Pass Count 和图本身一致。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages) // 接收每个 Pass 的资源读写声明，由 RenderGraph 配置阶段收集。
        {
            return BuildDump(request, passCount, resourceUsages, null, null, null); // 没有图级校验、资源表和 RT 执行选项时，输出基础 dump。
        }

        public static string BuildDump( // 保留完整旧签名，兼容还没有接入 RT 生命周期选项的调用方。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出 Request 类型和 Camera 名称。
            int passCount, // 接收 RenderGraph 当前 Pass 数量，确保 dump 中的 Pass Count 和图本身一致。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages, // 接收每个 Pass 的资源读写声明，由 RenderGraph 配置阶段收集。
            IReadOnlyList<string> validationMessages, // 接收图级别校验消息，通常只在 Debug 开关开启时输出。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收资源注册表，用来判断资源是否为外部导入。
        {
            return BuildDump(request, passCount, resourceUsages, validationMessages, resourceRegistry, null); // 旧调用没有 RT 执行选项时，用 <none> 表示未提供。
        }

        public static string BuildDump( // 构建完整 RenderGraph 调试文本，调用方仍然决定是否真正输出到 Console。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出 Request 类型和 Camera 名称。
            int passCount, // 接收 RenderGraph 当前 Pass 数量，确保 dump 中的 Pass Count 和图本身一致。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages, // 接收每个 Pass 的资源读写声明，由 RenderGraph 配置阶段收集。
            IReadOnlyList<string> validationMessages, // 接收图级别校验消息，通常只在 Debug 开关开启时输出。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收资源注册表，用来判断资源是否为外部导入。
            BurtRequestRenderOptions renderOptions, // 接收当前 request 的栈级 RT 生命周期选项，用来输出 Allocate/FinalBlit/Release 决策。
            BurtRenderPipelineAsset asset = null) // 接收当前管线资产，用来输出 Renderer Mode 和 Debug View 状态；旧调用可以保持为空。
        {
            var usageCount = resourceUsages != null ? resourceUsages.Count : 0; // 读取资源使用记录数量；列表为空时按 0 处理。

            var validationCount = validationMessages != null ? validationMessages.Count : 0; // 读取图级校验消息数量，帮助估算容量。

            var renderOptionsCapacity = renderOptions != null ? 256 : 48; // RT 生命周期行较长，预留额外空间减少 StringBuilder 扩容。

            var pipelineStateCapacity = 1900; // Pipeline/Camera/RT/PostProcess/Deferred/Material 状态会额外打印多行诊断信息，预留固定容量。

            var capacity = BaseDumpCapacity + renderOptionsCapacity + pipelineStateCapacity + usageCount * PerPassDumpCapacity + validationCount * 96; // 根据 Pass、校验、RT 选项和管线状态数量估算字符串容量。

            var builder = BurtDebugStringBuilderPool.Get(capacity); // 从调试 StringBuilder 池租借构建器，避免每帧开启日志时频繁分配。

            try // 使用 try/finally 保证构建器一定归还池中。
            {
                AppendDump(builder, request, passCount, resourceUsages, validationMessages, resourceRegistry, renderOptions, asset); // 把实际排版逻辑写到构建器里，BuildDump 只负责生命周期管理。

                return builder.ToString(); // 返回完整 dump 字符串，后续是否 Debug.Log 仍由外层 asset 开关控制。
            }
            finally // 无论 ToString 之前是否发生异常，都执行归还逻辑。
            {
                BurtDebugStringBuilderPool.Release(builder); // 归还构建器，避免 debug 工具引入额外长期分配。
            }
        }

        public static void AppendDump( // 保留旧签名，兼容已有调试组合代码。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出任务和相机信息。
            int passCount, // 接收当前 RenderGraph 的 Pass 数量。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages) // 接收当前 RenderGraph 的资源读写记录。
        {
            AppendDump(builder, request, passCount, resourceUsages, null, null, null); // 没有校验消息、资源表和 RT 执行选项时输出基础信息。
        }

        public static void AppendDump( // 保留完整旧签名，兼容还没有接入 RT 生命周期选项的调用方。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出任务和相机信息。
            int passCount, // 接收当前 RenderGraph 的 Pass 数量。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages, // 接收当前 RenderGraph 的资源读写记录。
            IReadOnlyList<string> validationMessages, // 接收图级别校验消息。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收资源注册表，用于判断外部资源。
        {
            AppendDump(builder, request, passCount, resourceUsages, validationMessages, resourceRegistry, null); // 旧调用没有 RT 执行选项时，用 <none> 表示未提供。
        }

        public static void AppendDump( // 把 RenderGraph dump 追加到调用方提供的构建器，方便未来组合更大的诊断文本。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出任务和相机信息。
            int passCount, // 接收当前 RenderGraph 的 Pass 数量。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages, // 接收当前 RenderGraph 的资源读写记录。
            IReadOnlyList<string> validationMessages, // 接收图级别校验消息。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收资源注册表，用于判断外部资源。
            BurtRequestRenderOptions renderOptions, // 接收当前 request 的栈级 RT 生命周期选项。
            BurtRenderPipelineAsset asset = null) // 接收当前管线资产，用来输出 Renderer Mode 和各类 Debug View 状态。
        {
            if (builder == null) // 如果没有构建器，就没有安全写入目标。
            {
                return; // 直接返回，避免调试格式化影响渲染主流程。
            }

            BurtDebugLogUtility.AppendScopedHeaderLine(builder, BurtDebugLogUtility.RenderGraphPrefix); // 写入统一标题 [BurtRP][BurtRenderGraph]，方便 Console 过滤。

            AppendRequestInfo(builder, request); // 写入 Request 和 Camera 基础信息，让 dump 一眼能看出来自哪次渲染请求。

            AppendRenderOptions(builder, renderOptions); // 写入 RT 生命周期决策，让你不用只靠 Pass 列表反推 Allocate、FinalBlit 和 Release。

            AppendPipelineState(builder, request, asset, renderOptions); // 写入管线和调试状态，方便对齐 Forward / Deferred 时确认当前到底由哪个开关驱动画面。

            AppendLightingState(builder, request, asset, resourceRegistry, renderOptions); // 写入追加光数量、buffer 上传和当前 shader 读取路径。

            AppendCameraState(builder, request); // 写入当前相机的 HDR、尺寸和 targetTexture 状态，用来排查 Game/Scene/Preview 差异。

            AppendRenderTargetState(builder, request, asset, resourceRegistry); // 写入 CameraColor、CameraDepth、PostProcessColor、GBuffer 和最终目标的格式状态。

            AppendPostProcessState(builder, request, asset, renderOptions); // 写入后处理框架、Volume Tonemapping 和 Color Adjustments 的运行状态。

            AppendDeferredState(builder, request, asset, resourceRegistry, renderOptions); // 写入 Deferred GBuffer 注册和调试模式状态，方便确认 Deferred 分支是否真的生效。

            if (IsPreviewOrReflectionRequest(request)) // Preview/Reflection 强制走 Forward，Deferred 材质分类对资产预览或 Probe 捕获没有意义。
            {
                builder.AppendLine("Deferred Material State:"); // 保留段落标题，方便对比普通 Deferred dump。

                builder.AppendLine("  <skipped: preview/reflection request uses Forward path>"); // 明确说明 Preview/Reflection 被隔离到 Forward 路径。
            }
            else
            {
                BurtDeferredMaterialDebugUtility.AppendDebugState(builder, request, asset); // 写入 Deferred 材质分类诊断，确认场景材质会进入 GBuffer、ForwardOnly 还是可能在 Deferred 下不可见。
            }

            BurtDebugLogUtility.AppendKeyValueLine(builder, "Pass Count", passCount); // 写入 RenderGraph 中的 Pass 数量，和实际执行列表保持一致。

            AppendValidationMessages(builder, validationMessages, resourceUsages); // 写入图级和 Pass 级校验消息，优先展示可能的问题。

            var resourceRiskCounters = BuildResourceRiskCounters(validationMessages, resourceUsages); // Reuse the same counters for risk and health summaries.

            var cullCandidateStats = BuildCullCandidateStats(resourceUsages, resourceRegistry); // Analyze future culling only; execution remains sequential.

            AppendResourceRiskSummary(builder, resourceRiskCounters); // Print compact counters so large dumps are easier to triage.

            AppendGraphHealth(builder, passCount, resourceUsages, resourceRegistry, resourceRiskCounters, cullCandidateStats); // One-line health summary for large dumps.

            AppendCullCandidateSummary(builder, cullCandidateStats); // Debug-only future culling hints; no pass is skipped.

            AppendCullReadinessSummary(builder, cullCandidateStats); // Explain why passes are not cullable yet without changing execution.

            AppendResourceRegistry(builder, resourceRegistry); // List registered graph resources, including future logical buffers.

            builder.AppendLine("Passes:"); // 写入 Pass 列表标题，把后面的资源读写详情归为同一组。

            AppendPassUsages(builder, resourceUsages); // 写入每个 Pass 的 Read/Write 资源列表，帮助定位资源声明缺失或顺序问题。

            builder.AppendLine("Resource Lifetimes:"); // Show first and last access for every declared graph resource.

            AppendResourceLifetimes(builder, resourceUsages, resourceRegistry); // Writes resource lifetime spans without changing scheduling.

            builder.AppendLine("Resources:"); // 写入资源视角标题，方便从资源名反查生产者和消费者。

            AppendResourceSummary(builder, resourceUsages, resourceRegistry); // 写入资源生产者、消费者和缺失资源提示。
        }

        private static void AppendRequestInfo( // 写入 request 层面的基础信息。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request) // 接收当前渲染请求，可能为空。
        {
            if (request == null) // 如果 request 为空，说明调用方只想 dump 图数据或传入了异常参数。
            {
                BurtDebugLogUtility.AppendKeyValueLine(builder, "Request", "null"); // 显式写出 request 为空，避免日志看起来像缺字段。

                BurtDebugLogUtility.AppendKeyValueLine(builder, "Camera", "null"); // request 为空时相机也无法读取，所以写出 null。

                return; // 结束 request 信息写入，避免访问空对象。
            }

            BurtDebugLogUtility.AppendKeyValueLine(builder, "Request", request.Type); // 写入 request 类型，例如 MainCamera、UICamera 或 Preview。

            var cameraName = request.Camera != null ? request.Camera.name : "null"; // 读取相机名称；相机为空时用 null 占位。

            BurtDebugLogUtility.AppendKeyValueLine(builder, "Camera", cameraName); // 单独写一行 Camera，比分隔在 Request 同一行更容易扫描。
        }

        private static void AppendRenderOptions( // 写入当前 request 的 RenderTarget 生命周期选项。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RT 生命周期选项，可能为空。
        {
            builder.AppendLine("Render Options:"); // 单独成段输出，避免和 Request/Pass 信息混在一起。

            if (renderOptions == null) // 如果调用方没有传入执行选项，说明当前 dump 来自旧路径或测试代码。
            {
                builder.AppendLine("  <none>"); // 明确写出没有 RT 生命周期信息，避免误以为字段丢失。

                return; // 没有更多字段可以输出。
            }

            builder.Append("  RTPlan=").Append(renderOptions.RenderTargetPlanName); // 写入栈级 RT 计划名称，例如 SingleBaseStackRT 或 SharedStackRT。

            builder.Append(" StackId=").Append(renderOptions.StackId); // 写入逻辑相机栈编号，方便和 Frame Debug 对齐。

            builder.Append(" StackIndex=").Append(renderOptions.RequestIndexInStack).Append('/').Append(renderOptions.RequestCountInStack); // 写入 request 在栈内的位置。

            builder.Append(" SharedRT=").Append(renderOptions.UseSharedRenderTargets); // 写入是否复用栈级 CameraColor/CameraDepth。

            builder.Append(" First=").Append(renderOptions.IsFirstRequestInStack); // 写入是否为栈内第一个 request。

            builder.Append(" Last=").Append(renderOptions.IsLastRequestInStack); // 写入是否为栈内最后一个 request。

            builder.Append(" AllocateColor=").Append(renderOptions.ShouldAllocateCameraColor); // 写入是否插入 CameraColor 分配 Pass。

            builder.Append(" AllocateDepth=").Append(renderOptions.ShouldAllocateCameraDepth); // 写入是否插入 CameraDepth 分配 Pass。

            builder.Append(" FinalBlit=").Append(renderOptions.ShouldFinalBlit); // 写入是否插入最终输出 Pass。

            builder.Append(" ReleaseColor=").Append(renderOptions.ShouldReleaseCameraColor); // 写入是否插入 CameraColor 释放 Pass。

            builder.Append(" ReleaseDepth=").Append(renderOptions.ShouldReleaseCameraDepth); // 写入是否插入 CameraDepth 释放 Pass。

            builder.AppendLine(); // 当前 RT 生命周期行结束。
        }

        private static void AppendPipelineState( // 写入当前管线资产和调试开关状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来让间接光 Debug 按相机选择场景 ReflectionProbe。
            BurtRenderPipelineAsset asset, // 接收当前管线资产，可能为空。
            BurtRequestRenderOptions renderOptions) // 接收 request 级 RT 生命周期，用来解释 Deferred debug 资源是否会真正构建。
        {
            builder.AppendLine("Pipeline State:"); // 单独成段输出，避免和 RT 生命周期或 Pass 列表混在一起。

            if (asset == null) // 如果没有资产，说明调用方走了旧 dump 入口或资产异常。
            {
                builder.Append("  Asset=<none>"); // 明确写出没有资产，让后续字段缺省值的来源更清楚。

                builder.Append(" ShadingDebugMode=").Append(BurtShadingDebugSettings.Mode); // 即使资产为空，也输出全局 Shading Debug 模式，方便排查 Overlay 残留状态。

                builder.Append(" ShadingDebugEnabled=").Append(BurtShadingDebugSettings.IsDebugging); // 输出全局 Shading Debug 是否启用。

                builder.AppendLine(); // 结束资产缺失状态行。

                return; // 没有资产时无法继续读取 Renderer Mode、GBuffer 或阴影调试开关。
            }

            var resolvedGBufferMode = BurtGBufferDebugViewUtility.ResolveGBufferDebugViewMode(asset); // 解析资产面板和 Overlay 合并后的最终 GBuffer Debug 模式。

            var gBufferSource = BurtGBufferDebugViewUtility.ResolveGBufferDebugViewSource(asset); // 解析 GBuffer Debug 的来源，区分资产面板和 Overlay。

            var gBufferDebugYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(request);

            builder.Append("  RendererMode=").Append(asset.RendererMode); // 写入当前渲染路径，用来确认正在看 Forward 还是 Deferred。

            builder.Append(" EffectiveRendererMode=").Append(IsDeferredRequest(request, asset) ? "Deferred" : "Forward"); // 写入当前 request 实际走的渲染路径，Preview 会强制走 Forward。

            builder.Append(" DeferredForwardOnlyOpaqueFallback=").Append(asset.EnableDeferredForwardOpaqueFallback); // 写入 Deferred 后 ForwardOnly 不透明兜底开关，方便确认是否会绘制不能写 GBuffer 的专用前向物体。

            builder.Append(" DepthPrepass=").Append(asset.EnableDepthPrepass); // 写入 Depth Prepass 开关，方便排查深度依赖和 GBuffer 绘制顺序。

            builder.AppendLine(); // 结束第一行核心管线状态。

            builder.Append("  ShadingDebugMode=").Append(BurtShadingDebugSettings.Mode); // 写入当前 Overlay / 运行时共享的 Shading Debug 模式。

            builder.Append(" ShadingDebugEnabled=").Append(BurtShadingDebugSettings.IsDebugging); // 写入 Shading Debug 是否启用，避免只看 enum 忘记 None 表示关闭。

            builder.Append(" GBufferDebugAssetMode=").Append(asset.GBufferDebugViewMode); // 写入资产面板上的 GBuffer Debug 模式。

            builder.Append(" GBufferDebugResolvedMode=").Append(resolvedGBufferMode); // 写入最终生效的 GBuffer Debug 模式。

            builder.Append(" GBufferDebugSource=").Append(gBufferSource); // 写入最终模式来源，方便确认是否由 Overlay 触发。

            builder.Append(" GBufferDebugYFlip=").Append(FormatFloat(gBufferDebugYFlip));

            builder.AppendLine(); // 结束第二行调试状态。

            builder.Append("  Atmosphere=").Append(BurtAtmosphereUtility.FormatDebugState());
            builder.Append(" AtmospherePassRequested=").Append(BurtAtmosphereUtility.ShouldUseAtmosphere(request));
            builder.Append(" AerialPerspectivePassRequested=").Append(BurtAtmosphereUtility.ShouldUseAerialPerspective(request));
            builder.Append(" AtmosphereGate=").Append(BurtAtmosphereUtility.FormatRequestGate(request));
            builder.AppendLine();
            builder.Append("  AtmosphereAerialPass=").Append(BurtAtmosphereUtility.FormatAerialPassState(request));
            builder.AppendLine();
            builder.Append("  Fog=").Append(BurtFogUtility.FormatDebugState(request));
            builder.AppendLine();
            builder.Append("  VolumetricFog=").Append(BurtVolumetricFogUtility.FormatDebugState(request));
            builder.AppendLine();
            AppendTileLightDebugPipelineState(builder, request, asset, renderOptions);

            builder.Append("  DepthDebugView=").Append(asset.EnableDepthDebugView); // 写入 CameraDepth 全屏调试开关。

            builder.Append(" UnsupportedShaderDebug=").Append(asset.EnableUnsupportedShaderDebug); // 写入错误材质绘制开关。

            builder.Append(" HiZDebugView=").Append(asset.EnableHiZDebugView);

            builder.Append(" HiZDebugMip=").Append(asset.HiZDebugMip);

            builder.Append(" HiZDebugScale=").Append(asset.HiZDebugScale.ToString("0.###"));

            builder.AppendLine(); // 结束第三行全屏调试状态。

            AppendRefractionDebugState(builder, request, asset);
            var ssrSettings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(request, asset);
            var ssrSuppressedByShadingDebug = BurtScreenSpaceReflectionPassUtility.IsScreenSpaceReflectionSuppressedByShadingDebug();
            var ssrHistory = BurtScreenSpaceReflectionHistoryUtility.GetHistoryStatus(request != null ? request.Camera : null);

            builder.Append("  SSREnabled=").Append(ssrSettings.Enabled);

            builder.Append(" SSRSuppressedByShadingDebug=").Append(ssrSuppressedByShadingDebug);

            builder.Append(" SSRDebugMode=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionDebugModeLabel());

            builder.Append(" SSRShaderStatus=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderStatusLabel());

            builder.Append(" SSRTraceMode=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionTraceModeLabel(request, asset));
            builder.Append(" SSRComposite=SpecularReplaceCameraIBL");
            builder.Append(" SSRFallback=CameraIBLPreserved");
            builder.Append(" SSRApply=XRenderPureReflectMip0Alpha");
            builder.Append(" SSRClearCoat=DualLayerTrace");
            builder.Append(" SSRHiZTraceExperimental=").Append(ssrSettings.ExperimentalHiZTrace);

            builder.Append(" SSRHiZDiagnostics=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionHiZDiagnosticsStatusLabel());

            builder.Append(" SSRMaxSteps=").Append(ssrSettings.MaxSteps);

            builder.Append(" SSRMaxDistance=").Append(ssrSettings.MaxDistance.ToString("0.###"));

            builder.Append(" SSRThickness=").Append(ssrSettings.Thickness.ToString("0.###"));

            builder.Append(" SSRIntensity=").Append(ssrSettings.Intensity.ToString("0.###"));

            builder.Append(" SSRRoughnessFade=").Append(ssrSettings.RoughnessFade.ToString("0.###"));

            builder.Append(" SSRTemporal=").Append(ssrSettings.TemporalAccumulation);

            builder.Append(" SSRTemporalFeedback=").Append(ssrSettings.TemporalFeedback.ToString("0.###"));

            builder.Append(" SSRHistoryValid=").Append(ssrHistory.HasHistory);

            builder.Append(" SSRHistoryAllocated=").Append(ssrHistory.HasHistory || ssrHistory.HasDepthHistory || ssrHistory.HasNormalRoughnessHistory);

            builder.Append(" SSRHistoryMatches=").Append(ssrHistory.DescriptorMatches);

            builder.Append(" SSRDepthHistoryAllocated=").Append(ssrHistory.HasDepthHistory);

            builder.Append(" SSRDepthHistoryMatches=").Append(ssrHistory.DepthDescriptorMatches);

            builder.Append(" SSRNormalRoughnessHistoryAllocated=").Append(ssrHistory.HasNormalRoughnessHistory);

            builder.Append(" SSRNormalRoughnessHistoryMatches=").Append(ssrHistory.NormalRoughnessDescriptorMatches);

            builder.Append(" SSRHistoryAge=").Append(ssrHistory.HistoryAge);

            builder.Append(" SSRFrame=").Append(ssrHistory.FrameIndex);

            builder.Append(" SSRHistoryReason=").Append(ssrHistory.LastInvalidationReason);

            builder.AppendLine();

            AppendPreIntegratedFGDebugState(builder, asset);
            BurtIndirectLightingUtility.AppendDebugState(builder, request); // 写入 BurtRP 全局间接光数据源状态，方便确认 Deferred 不再依赖 Forward DrawRenderers 副作用。
            BurtIndirectLightingUtility.AppendXRenderComparisonReport(builder, request, asset);
        }

        private static void AppendRefractionDebugState(StringBuilder builder, BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var enabled = BurtRefractionPassUtility.ShouldUseRefraction(request, asset);
            builder.Append("  RefractionEnabled=").Append(enabled);
            builder.Append(" RefractionMaterialGate=").Append(BurtRefractionPassUtility.ResolveRefractionCandidateGateLabel(request, asset));
            builder.Append(" RefractionPath=").Append(BurtRefractionPassUtility.ResolveRefractionPathLabel(request, asset));
            builder.Append(" RefractionTransparentOnly=True");
            builder.Append(" RefractionShader=BurtRP/Lit");
            builder.Append(" RefractionDistortionPass=BurtRefractionDistortion");
            builder.Append(" RefractionInlineFallback=False");
            builder.Append(" RefractionApply=FullScreenApplyMerge");
            builder.Append(" RefractionSceneColorMipBuild=CameraColorBlitGenerateMips");
            builder.Append(" RefractionResources=").Append(BurtRefractionPassUtility.ResolveRefractionResourceLabel(request));
            builder.AppendLine();
        }

        private static void AppendPreIntegratedFGDebugState(StringBuilder builder, BurtRenderPipelineAsset asset)
        {
            var lut = asset != null ? asset.PreintegratedFGLut : null;
            builder.Append("  PreIntegratedFG=").Append(lut != null);
            builder.Append(" PreIntegratedFGName=").Append(lut != null ? FormatTextureName(lut) : "<none>");
            builder.Append(" PreIntegratedFGSize=").Append(lut != null ? lut.width + "x" + lut.height : "<none>");
            builder.Append(" PreIntegratedFGFormat=").Append(lut != null ? lut.format.ToString() : "<none>");
            builder.Append(" PreIntegratedFGGraphicsFormat=").Append(lut != null ? lut.graphicsFormat.ToString() : "<none>");
            builder.Append(" PreIntegratedFGMipCount=").Append(lut != null ? lut.mipmapCount : 0);
            builder.Append(" PreIntegratedFGReadable=").Append(lut != null && lut.isReadable);
            builder.Append(" PreIntegratedFGParity=").Append(lut != null ? BurtImageBasedFilterUtility.PreIntegratedFGParity : "FallbackApprox");
            builder.Append(" PreIntegratedFGValidation=").Append(BurtImageBasedFilterUtility.ValidatePreIntegratedFGLut(lut));
            builder.AppendLine();
        }

        private static void AppendTileLightDebugPipelineState(StringBuilder builder, BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRequestRenderOptions renderOptions)
        {
            var requested = IsTileLightDebugRequested();
            var rendererSupported = IsDeferredRequest(request, asset);
            var hasLocalDeferredTargets = HasLocalDeferredTargets(renderOptions);

            builder.Append("  TileLightDebugRequested=").Append(requested);
            builder.Append(" TileLightDebugRendererSupported=").Append(rendererSupported);
            builder.Append(" TileLightDebugLocalDeferredTargets=").Append(hasLocalDeferredTargets);
            builder.Append(" TileLightDebugNote=").Append(ResolveTileLightDebugPipelineNote(requested, rendererSupported, hasLocalDeferredTargets));
            builder.AppendLine();
        }

        private static void AppendLightingState(
            StringBuilder builder,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry,
            BurtRequestRenderOptions renderOptions)
        {
            builder.AppendLine("Lighting State:");

            var lightingData = request != null ? request.LightingData : null;
            var additionalLightBufferRegistered = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);
            var additionalLightBufferAllocatedAtDumpTime = resourceRegistry != null && resourceRegistry.IsBufferAllocated(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);
            var additionalLightBufferDescriptorValid = resourceRegistry != null && resourceRegistry.HasValidBufferDescriptor(BurtRenderGraphResourceRegistry.AdditionalLightBufferName);
            BurtRenderBufferDescriptor additionalLightBufferDescriptor = default;
            var hasAdditionalLightBufferDescriptor = resourceRegistry != null && resourceRegistry.TryGetBufferDescriptor(BurtRenderGraphResourceRegistry.AdditionalLightBufferName, out additionalLightBufferDescriptor);
            var expectedAdditionalLightBufferDescriptor = BurtLightingData.CreateAdditionalLightBufferDescriptor();
            var additionalLightBufferDescriptorMatches = hasAdditionalLightBufferDescriptor &&
                additionalLightBufferDescriptor.Count == expectedAdditionalLightBufferDescriptor.Count &&
                additionalLightBufferDescriptor.Stride == expectedAdditionalLightBufferDescriptor.Stride &&
                additionalLightBufferDescriptor.Target == expectedAdditionalLightBufferDescriptor.Target;
            var additionalLightBufferUploadedThisFrame = lightingData != null && lightingData.AdditionalLightBufferUploaded;
            var additionalLightBufferReleasedBeforeDump = additionalLightBufferUploadedThisFrame && additionalLightBufferRegistered && !additionalLightBufferAllocatedAtDumpTime;

            builder.Append("  AdditionalLightCount=").Append(lightingData != null ? lightingData.AdditionalLightCount : 0);
            builder.Append(" MaxAdditionalLights=").Append(BurtLightingData.MaxAdditionalLights);
            builder.Append(" VisibleLightCount=").Append(lightingData != null ? lightingData.VisibleLightCount : 0);
            builder.Append(" AdditionalLightShadingPath=").Append(lightingData != null ? lightingData.AdditionalLightShadingPath : BurtLightingData.AdditionalLightShadingPathLabel);
            builder.AppendLine();

            builder.Append("  AdditionalLightBufferRegistered=").Append(additionalLightBufferRegistered);
            builder.Append(" AdditionalLightBufferAllocatedAtDumpTime=").Append(additionalLightBufferAllocatedAtDumpTime);
            builder.Append(" AdditionalLightBufferDescriptorValid=").Append(additionalLightBufferDescriptorValid);
            builder.Append(" AdditionalLightBufferUploadAttemptedThisFrame=").Append(lightingData != null && lightingData.AdditionalLightBufferUploadAttempted);
            builder.Append(" AdditionalLightBufferUploadedThisFrame=").Append(additionalLightBufferUploadedThisFrame);
            builder.Append(" AdditionalLightBufferReleasedBeforeDump=").Append(additionalLightBufferReleasedBeforeDump);
            builder.Append(" AdditionalLightBufferRows=").Append(BurtLightingData.AdditionalLightBufferVectorCount);
            builder.Append(" AdditionalLightBufferStride=").Append(BurtLightingData.AdditionalLightBufferStride);
            builder.Append(" AdditionalLightBufferDescriptorCount=").Append(hasAdditionalLightBufferDescriptor ? additionalLightBufferDescriptor.Count : 0);
            builder.Append(" AdditionalLightBufferDescriptorStride=").Append(hasAdditionalLightBufferDescriptor ? additionalLightBufferDescriptor.Stride : 0);
            builder.Append(" AdditionalLightBufferDescriptorMatches=").Append(additionalLightBufferDescriptorMatches);
            builder.AppendLine();

            AppendMainLightShadowState(builder, request, asset, lightingData, resourceRegistry);
            AppendAdditionalLightShadowState(builder, lightingData, resourceRegistry);
            AppendPerObjectShadowState(builder, request, asset, resourceRegistry);
            AppendTileLightDebugState(builder, request, asset, lightingData, resourceRegistry, renderOptions);
            AppendAdditionalLightDetails(builder, lightingData);
        }

        private static void AppendMainLightShadowState(
            StringBuilder builder,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtLightingData lightingData,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var mapRegistered = IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.MainLightShadowMapName);
            var shouldUse = BurtShadowUtility.ShouldUseMainLightShadow(request, asset);
            var shadowData = BurtShadowUtility.ResolveMainLightShadowData(request, asset);
            var camera = request != null ? request.Camera : null;
            BurtMainLightShadowCascadeCache cascadeCache = null;
            var cascadeCacheValid = shadowData != null && BurtMainLightShadowMatrixUtility.TryGetMainLightShadowCascadeCache(request, shadowData, out cascadeCache);
            var cascadeCount = cascadeCacheValid ? cascadeCache.CascadeCount : BurtShadowUtility.ResolveMainLightShadowCascadeCount(shadowData);
            var tileResolution = cascadeCacheValid ? cascadeCache.TileResolution : BurtShadowUtility.ResolveMainLightShadowTileResolution(shadowData);
            var atlasResolution = cascadeCacheValid ? cascadeCache.AtlasResolution : BurtShadowUtility.ResolveMainLightShadowAtlasResolution(shadowData);
            var cascadeSplits = shadowData != null ? shadowData.CreateMainLightShadowCascadeSplitVector() : new Vector3(1f, 0f, 0f);
            var mainLightIndex = lightingData != null ? lightingData.MainLightIndex : -1;
            var hasMainLightObject = request != null &&
                mainLightIndex >= 0 &&
                mainLightIndex < request.CullingResults.visibleLights.Length &&
                request.CullingResults.visibleLights[mainLightIndex].light != null;
            var mainLightRenderingLayerMask = hasMainLightObject
                ? request.CullingResults.visibleLights[mainLightIndex].light.renderingLayerMask
                : 0;
            Bounds shadowCasterBounds = default;
            var shadowCasterBoundsValid = request != null &&
                mainLightIndex >= 0 &&
                mainLightIndex < request.CullingResults.visibleLights.Length &&
                request.CullingResults.GetShadowCasterBounds(mainLightIndex, out shadowCasterBounds);
            var cascadeSphereRadii = "(0,0,0,0)";
            var firstMatrixHash = "00000000";
            if (cascadeCacheValid)
            {
                cascadeSphereRadii = "("
                    + FormatFloat(Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[0].w))) + ","
                    + FormatFloat(Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[1].w))) + ","
                    + FormatFloat(Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[2].w))) + ","
                    + FormatFloat(Mathf.Sqrt(Mathf.Max(0f, cascadeCache.CascadeSpheres[3].w))) + ")";
                firstMatrixHash = FormatMatrixHash(cascadeCache.WorldToShadowMatrices[0]);
            }

            builder.Append("  MainLightShadowMapRegistered=").Append(mapRegistered);
            builder.Append(" MainLightShadowRequested=").Append(shouldUse);
            builder.Append(" MainLightShadowMapValid=").Append(shouldUse && mapRegistered);
            builder.Append(" HasMainLight=").Append(lightingData != null && lightingData.HasMainLight);
            builder.Append(" MainLightIndex=").Append(mainLightIndex);
            builder.Append(" MainLightRenderingLayerMask=").Append(hasMainLightObject ? FormatHexMask(mainLightRenderingLayerMask) : "<none>");
            builder.Append(" MainLightShadowUseRenderingLayerMaskTest=False");
            builder.Append(" MainLightShadowDrawPolicy=UnityDrawShadowsNoRenderingLayerMaskTest");
            builder.Append(" MainLightShadowCasterBiasPolicy=AssetTexelCasterBiasPlusPolygonOffset");
            builder.Append(" MainLightShadowRTBinding=DepthOnlyTemporaryRT");
            builder.Append(" MainLightShadowMatrixPolicy=UnitySRPReversedZTextureXYZAtlas");
            builder.Append(" MainLightShadowZPolicy=RejectZ<=0AndZ>=1");
            builder.Append(" MainLightShadowReversedZ=").Append(SystemInfo.usesReversedZBuffer);
            builder.Append(" MainLightShadowClearDepth=").Append(FormatFloat(BurtShadowRenderTargetUtility.ResolveMainLightShadowClearDepth()));
            builder.Append(" MainLightShadowReceiverBiasPolicy=DisabledCasterPolygonOffsetOnly");
            builder.Append(" MainLightShadowReceiverNormalBiasFactor=0");
            builder.Append(" LightShadowType=").Append(shadowData != null ? shadowData.MainLightShadowType.ToString() : "None");
            builder.Append(" ShadowCasterBoundsValid=").Append(shadowCasterBoundsValid);
            builder.Append(" ShadowCasterBounds=").Append(shadowCasterBoundsValid ? FormatBounds(shadowCasterBounds) : "<none>");
            builder.Append(" ShadowStrength=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowStrength) : "0");
            builder.Append(" ShadowDistance=").Append(FormatFloat(BurtShadowUtility.ResolveMainLightShadowDistance(asset, camera)));
            builder.Append(" ShadowResolution=").Append(shadowData != null ? shadowData.MainLightShadowResolution : 0);
            builder.Append(" CascadeCount=").Append(cascadeCount);
            builder.Append(" TileResolution=").Append(tileResolution);
            builder.Append(" AtlasResolution=").Append(atlasResolution);
            builder.Append(" CascadeCacheValid=").Append(cascadeCacheValid);
            builder.Append(" CascadeSphereRadii=").Append(cascadeSphereRadii);
            builder.Append(" CascadeSplits=").Append(FormatVector3(cascadeSplits.x, cascadeSplits.y, cascadeSplits.z));
            builder.Append(" CascadeBlendDistance=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowCascadeBlendDistance) : "0");
            builder.Append(" ShadowFadeDistance=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowFadeDistance) : "0");
            builder.Append(" DepthBias=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowDepthBias) : "0");
            builder.Append(" NormalBias=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowNormalBias) : "0");
            builder.Append(" SampleBias=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowSampleBias) : "0");
            builder.Append(" SoftSampling=").Append(shadowData != null && (shadowData.IsMainLightShadowSoft || (shadowData.IsMainLightShadowHard && shadowData.EnableMainLightShadowPCSS)));
            builder.Append(" PCSS=").Append(shadowData != null && shadowData.IsMainLightShadowHard && shadowData.EnableMainLightShadowPCSS);
            builder.Append(" PCSSLightSize=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowPCSSLightSize) : "0");
            builder.Append(" PCSSBlockerSearchRadius=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowPCSSBlockerSearchRadius) : "0");
            builder.Append(" PCSSMaxFilterRadius=").Append(shadowData != null ? FormatFloat(shadowData.MainLightShadowPCSSMaxFilterRadius) : "0");
            builder.Append(" MatrixHash=").Append(firstMatrixHash);
            builder.AppendLine();
        }

        private static void AppendAdditionalLightShadowState(StringBuilder builder, BurtLightingData lightingData, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var atlasRegistered = IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.AdditionalLightShadowAtlasName);
            var markedSlots = lightingData != null ? Mathf.Clamp(lightingData.AdditionalLightShadowMarkedCount, 0, BurtLightingData.MaxAdditionalLights) : 0;
            var activeSlots = CountActiveAdditionalLightShadowSlots(lightingData);
            var softSlots = CountSoftAdditionalLightShadowSlots(lightingData);
            var maxActiveSlot = ResolveMaxActiveAdditionalLightShadowSlot(lightingData);

            builder.Append("  AdditionalLightShadowAtlasRegistered=").Append(atlasRegistered);
            builder.Append(" AdditionalLightShadowAtlasValid=").Append(lightingData != null && lightingData.AdditionalLightShadowAtlasValid);
            builder.Append(" AdditionalLightShadowCacheValid=").Append(lightingData != null && lightingData.AdditionalLightShadowCacheValid);
            builder.Append(" AdditionalLightShadowCandidates=").Append(lightingData != null ? lightingData.AdditionalLightShadowCandidateCount : 0);
            builder.Append(" AdditionalLightShadowMarkedSlots=").Append(markedSlots);
            builder.Append(" AdditionalLightShadowActiveSlots=").Append(activeSlots);
            builder.Append(" AdditionalLightShadowSoftSlots=").Append(softSlots);
            builder.Append(" AdditionalLightShadowMaxActiveSlot=").Append(maxActiveSlot);
            builder.Append(" AdditionalLightShadowSparseSlots=").Append(activeSlots > 0 && maxActiveSlot + 1 > activeSlots);
            builder.Append(" AdditionalLightShadowTileResolution=").Append(lightingData != null ? lightingData.AdditionalLightShadowTileResolution : 0);
            builder.Append(" AdditionalLightShadowAtlasResolution=").Append(lightingData != null ? lightingData.AdditionalLightShadowAtlasResolution : 0);
            builder.Append(" AdditionalLightShadowAtlasTileGrid=");
            builder.Append(lightingData != null ? lightingData.AdditionalLightShadowAtlasTileCountX : 0);
            builder.Append("x").Append(lightingData != null ? lightingData.AdditionalLightShadowAtlasTileCountY : 0);
            builder.Append(" AdditionalLightShadowActiveSlices=").Append(lightingData != null ? lightingData.AdditionalLightShadowActiveSliceCount : 0);
            builder.AppendLine();

            builder.Append("  AdditionalLightShadowDiagnostics:");
            builder.Append(" PrepareAttempted=").Append(lightingData != null && lightingData.AdditionalLightShadowPrepareAttempted);
            builder.Append(" PrepareSucceeded=").Append(lightingData != null && lightingData.AdditionalLightShadowPrepareSucceeded);
            builder.Append(" PrepareFailed=").Append(lightingData != null ? lightingData.AdditionalLightShadowPrepareFailedCount : 0);
            builder.Append(" PointUnsupported=").Append(lightingData != null ? lightingData.AdditionalLightShadowPointUnsupportedCount : 0);
            builder.Append(" UnsupportedType=").Append(lightingData != null ? lightingData.AdditionalLightShadowUnsupportedTypeCount : 0);
            builder.Append(" ShadowTypeNone=").Append(lightingData != null ? lightingData.AdditionalLightShadowNoneCount : 0);
            builder.Append(" ShadowStrengthZero=").Append(lightingData != null ? lightingData.AdditionalLightShadowStrengthZeroCount : 0);
            builder.Append(" SlotLimitExceeded=").Append(lightingData != null ? lightingData.AdditionalLightShadowSlotLimitExceededCount : 0);
            builder.AppendLine();

            AppendAdditionalLightShadowSlices(builder, lightingData);
        }

        private static void AppendPerObjectShadowState(
            StringBuilder builder,
            BurtRenderRequest request,
            BurtRenderPipelineAsset asset,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var atlasRegistered = IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.PerObjectShadowAtlasName);
            var shouldUse = BurtPerObjectShadowUtility.ShouldUsePerObjectShadow(request, asset);
            var preparedData = shouldUse ? BurtPerObjectShadowUtility.Prepare(request) : null;
            CountPerObjectShadowMainLayerMissing(preparedData, out var missingMainLayerRenderers, out var missingMainLayerMultipassRenderers);

            builder.Append("  PerObjectShadowAtlasRegistered=").Append(atlasRegistered);
            builder.Append(" PerObjectShadowRequested=").Append(shouldUse);
            builder.Append(" PerObjectShadowRegistryActive=").Append(BurtPerObjectShadowRegistry.ActiveCount);
            builder.Append(" PerObjectShadowCollected=").Append(preparedData != null ? preparedData.Components.Count : 0);
            builder.Append(" PerObjectShadowPreparedSlices=").Append(preparedData != null ? preparedData.SliceCount : 0);
            builder.Append(" PerObjectShadowLayerPolicy=RecoverMainLightPlusPerObjectBit");
            builder.Append(" PerObjectShadowMissingMainLayerRenderers=").Append(missingMainLayerRenderers);
            builder.Append(" PerObjectShadowMissingMainLayerMultipass=").Append(missingMainLayerMultipassRenderers);
            builder.Append(" PerObjectShadowAtlasSize=");
            builder.Append(preparedData != null ? preparedData.AtlasWidth : 0);
            builder.Append("x").Append(preparedData != null ? preparedData.AtlasHeight : 0);
            builder.Append(" PerObjectShadowMaxSlices=").Append(BurtPerObjectShadowUtility.MaxSlices);
            builder.AppendLine();

            AppendPerObjectShadowSlices(builder, preparedData);
        }

        private static void CountPerObjectShadowMainLayerMissing(
            PerObjectShadowPrepareData preparedData,
            out int missingRenderers,
            out int missingMultipassRenderers)
        {
            missingRenderers = 0;
            missingMultipassRenderers = 0;
            if (preparedData == null || preparedData.SliceCount <= 0)
            {
                return;
            }

            var sliceCount = Mathf.Clamp(preparedData.SliceCount, 0, BurtPerObjectShadowUtility.MaxSlices);
            for (var sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
            {
                var slice = preparedData.Slices[sliceIndex];
                if (slice == null)
                {
                    continue;
                }

                if (slice.Renderers != null)
                {
                    for (var rendererIndex = 0; rendererIndex < slice.Renderers.Count; rendererIndex++)
                    {
                        var renderer = slice.Renderers[rendererIndex];
                        if (renderer != null && (renderer.renderingLayerMask & BurtPerObjectShadow.MainLightRenderingLayerMask) == 0u)
                        {
                            missingRenderers++;
                        }
                    }
                }

                if (slice.MultipassRenderers != null)
                {
                    for (var rendererIndex = 0; rendererIndex < slice.MultipassRenderers.Count; rendererIndex++)
                    {
                        var multipassRenderer = slice.MultipassRenderers[rendererIndex];
                        if (multipassRenderer != null && ((uint)multipassRenderer.m_RenderingLayerMask & BurtPerObjectShadow.MainLightRenderingLayerMask) == 0u)
                        {
                            missingMultipassRenderers++;
                        }
                    }
                }
            }
        }

        private static void AppendPerObjectShadowSlices(StringBuilder builder, PerObjectShadowPrepareData preparedData)
        {
            if (preparedData == null || preparedData.SliceCount <= 0)
            {
                return;
            }

            var sliceCount = Mathf.Clamp(preparedData.SliceCount, 0, BurtPerObjectShadowUtility.MaxSlices);
            for (var sliceIndex = 0; sliceIndex < sliceCount; sliceIndex++)
            {
                var slice = preparedData.Slices[sliceIndex];
                if (slice == null)
                {
                    continue;
                }

                builder.Append("    PerObjectShadowSlice[").Append(sliceIndex).Append("]:");
                builder.Append(" Renderers=").Append(slice.Renderers != null ? slice.Renderers.Count : 0);
                builder.Append(" MultipassRenderers=").Append(slice.MultipassRenderers != null ? slice.MultipassRenderers.Count : 0);
                builder.Append(" Resolution=").Append(slice.SliceResolution);
                builder.Append(" Strength=").Append(FormatFloat(slice.Strength));
                builder.Append(" ReceiverDepthBias=").Append(FormatFloat(slice.ReceiverDepthBias));
                builder.Append(" ReceiverDistance=").Append(FormatFloat(slice.ReceiverDistance));
                builder.Append(" ReceiverNormalBias=").Append(FormatFloat(slice.ReceiverNormalBias));
                builder.Append(" WorldTexelSize=").Append(FormatFloat(slice.WorldTexelSize));
                builder.Append(" DepthRangeWorld=").Append(FormatFloat(slice.DepthRangeWorld));
                builder.Append(" Viewport=").Append(FormatRect(slice.Viewport));
                builder.Append(" AtlasRect=").Append(FormatVector4(slice.AtlasRect));
                builder.Append(" MatrixHash=").Append(FormatMatrixHash(slice.WorldToShadowMatrix));
                builder.AppendLine();
            }
        }

        private static void AppendAdditionalLightShadowSlices(StringBuilder builder, BurtLightingData lightingData)
        {
            if (lightingData == null || lightingData.AdditionalLightShadowActiveSliceCount <= 0)
            {
                builder.AppendLine("  Additional Shadow Slices: <none>");
                return;
            }

            var activeSliceCount = Mathf.Clamp(lightingData.AdditionalLightShadowActiveSliceCount, 0, BurtLightingData.MaxAdditionalLightShadowSlices);
            builder.AppendLine("  Additional Shadow Slices:");
            for (var sliceIndex = 0; sliceIndex < activeSliceCount; sliceIndex++)
            {
                var lightIndex = lightingData.AdditionalLightShadowSliceLightIndices[sliceIndex];
                var faceIndex = lightingData.AdditionalLightShadowSliceFaceIndices[sliceIndex];
                var atlasRect = lightingData.AdditionalLightShadowSliceAtlasRects[sliceIndex];
                var validLight = lightIndex >= 0 && lightIndex < BurtLightingData.MaxAdditionalLights;
                var lightParams = validLight && lightingData.AdditionalLightShadowLightParams != null && lightingData.AdditionalLightShadowLightParams.Length > lightIndex
                    ? lightingData.AdditionalLightShadowLightParams[lightIndex]
                    : Vector4.zero;
                var splitData = lightingData.AdditionalLightShadowSliceSplitDatas[sliceIndex];
                var projectionMatrix = lightingData.AdditionalLightShadowSliceProjectionMatrices[sliceIndex];
                var worldToShadowMatrix = lightingData.AdditionalLightShadowSliceWorldToShadowMatrices[sliceIndex];

                builder.Append("    #").Append(sliceIndex);
                builder.Append(" Light=").Append(lightIndex);
                builder.Append(" StableKey=").Append(validLight ? lightingData.AdditionalLightShadowStableKeys[lightIndex] : 0);
                builder.Append(" FirstSlice=").Append(validLight ? Mathf.RoundToInt(lightParams.x) : -1);
                builder.Append(" Face=").Append(FormatAdditionalLightShadowFace(faceIndex, lightParams.z));
                builder.Append(" Rect=").Append(FormatVector4(atlasRect));
                builder.Append(" ReceiverNormalBias=").Append(FormatFloat(lightParams.w));
                builder.Append(" ProjectionZ=(").Append(FormatFloat(projectionMatrix.m22)).Append(',').Append(FormatFloat(projectionMatrix.m23)).Append(')');
                builder.Append(" CullingSphere=").Append(FormatVector4(splitData.cullingSphere));
                builder.Append(" MatrixHash=").Append(FormatMatrixHash(worldToShadowMatrix));
                builder.AppendLine();
            }
        }

        private static void AppendTileLightDebugState(StringBuilder builder, BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtLightingData lightingData, BurtRenderGraphResourceRegistry resourceRegistry, BurtRequestRenderOptions renderOptions)
        {
            var countRegistered = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
            var listRegistered = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName);
            var offsetRegistered = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
            var clusterCountRegistered = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
            var clusterListRegistered = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
            var clusterOffsetRegistered = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
            var countAllocated = resourceRegistry != null && resourceRegistry.IsBufferAllocated(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
            var listAllocated = resourceRegistry != null && resourceRegistry.IsBufferAllocated(BurtRenderGraphResourceRegistry.TileLightListBufferName);
            var offsetAllocated = resourceRegistry != null && resourceRegistry.IsBufferAllocated(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
            var clusterCountAllocated = resourceRegistry != null && resourceRegistry.IsBufferAllocated(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
            var clusterListAllocated = resourceRegistry != null && resourceRegistry.IsBufferAllocated(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
            var clusterOffsetAllocated = resourceRegistry != null && resourceRegistry.IsBufferAllocated(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
            var countDescriptorValid = resourceRegistry != null && resourceRegistry.HasValidBufferDescriptor(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
            var listDescriptorValid = resourceRegistry != null && resourceRegistry.HasValidBufferDescriptor(BurtRenderGraphResourceRegistry.TileLightListBufferName);
            var offsetDescriptorValid = resourceRegistry != null && resourceRegistry.HasValidBufferDescriptor(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
            var clusterCountDescriptorValid = resourceRegistry != null && resourceRegistry.HasValidBufferDescriptor(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName);
            var clusterListDescriptorValid = resourceRegistry != null && resourceRegistry.HasValidBufferDescriptor(BurtRenderGraphResourceRegistry.ClusterLightListBufferName);
            var clusterOffsetDescriptorValid = resourceRegistry != null && resourceRegistry.HasValidBufferDescriptor(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
            BurtRenderBufferDescriptor countDescriptor = default;
            BurtRenderBufferDescriptor listDescriptor = default;
            BurtRenderBufferDescriptor offsetDescriptor = default;
            BurtRenderBufferDescriptor clusterCountDescriptor = default;
            BurtRenderBufferDescriptor clusterListDescriptor = default;
            BurtRenderBufferDescriptor clusterOffsetDescriptor = default;
            var hasCountDescriptor = resourceRegistry != null && resourceRegistry.TryGetBufferDescriptor(BurtRenderGraphResourceRegistry.TileLightCountBufferName, out countDescriptor);
            var hasListDescriptor = resourceRegistry != null && resourceRegistry.TryGetBufferDescriptor(BurtRenderGraphResourceRegistry.TileLightListBufferName, out listDescriptor);
            var hasOffsetDescriptor = resourceRegistry != null && resourceRegistry.TryGetBufferDescriptor(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName, out offsetDescriptor);
            var hasClusterCountDescriptor = resourceRegistry != null && resourceRegistry.TryGetBufferDescriptor(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName, out clusterCountDescriptor);
            var hasClusterListDescriptor = resourceRegistry != null && resourceRegistry.TryGetBufferDescriptor(BurtRenderGraphResourceRegistry.ClusterLightListBufferName, out clusterListDescriptor);
            var hasClusterOffsetDescriptor = resourceRegistry != null && resourceRegistry.TryGetBufferDescriptor(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName, out clusterOffsetDescriptor);
            var uploaded = lightingData != null && lightingData.TileLightDebugUploaded;
            var clusterUploaded = lightingData != null && lightingData.ClusterLightUploaded;
            var requested = IsTileLightDebugRequested();
            var requiresListBuffers = requested && BurtShadingDebugSettings.Mode == BurtShadingDebugMode.TileLightOccupancy;
            var requiredBuffersRegistered = countRegistered && (!requiresListBuffers || (listRegistered && offsetRegistered));
            var requiredDescriptorsValid = countDescriptorValid && (!requiresListBuffers || (listDescriptorValid && offsetDescriptorValid));
            var requiredBuffersReleased = !countAllocated && (!requiresListBuffers || (!listAllocated && !offsetAllocated));
            var releasedBeforeDump = uploaded && requiredBuffersRegistered && requiredBuffersReleased;
            var clusterBuffersRegistered = clusterCountRegistered && clusterListRegistered && clusterOffsetRegistered;
            var clusterDescriptorsValid = clusterCountDescriptorValid && clusterListDescriptorValid && clusterOffsetDescriptorValid;
            var clusterBuffersReleased = !clusterCountAllocated && !clusterListAllocated && !clusterOffsetAllocated;
            var clusterReleasedBeforeDump = clusterUploaded && clusterBuffersRegistered && clusterBuffersReleased;
            var effectiveDeferred = IsDeferredRequest(request, asset);
            var hasLocalDeferredTargets = HasLocalDeferredTargets(renderOptions);
            var active = requested && effectiveDeferred && hasLocalDeferredTargets && requiredBuffersRegistered && requiredDescriptorsValid && uploaded;
            var status = ResolveTileLightDebugStatus(requested, effectiveDeferred, hasLocalDeferredTargets, requiredBuffersRegistered, requiredDescriptorsValid, lightingData, uploaded);
            var buildGate = ResolveTileLightDebugBuildGate(
                requested,
                effectiveDeferred,
                hasLocalDeferredTargets,
                countRegistered,
                listRegistered,
                offsetRegistered,
                countDescriptorValid,
                listDescriptorValid,
                offsetDescriptorValid,
                requiresListBuffers,
                lightingData,
                uploaded);
            var uniformTileCount = lightingData != null && lightingData.TileLightTileCount > 0 && lightingData.TileLightMinCount == lightingData.TileLightMaxCount;
            var visualNote = ResolveTileLightDebugVisualNote(requested, active, lightingData, uniformTileCount);
            var hasCountSnapshot = lightingData != null && lightingData.TileLightDebugCountSnapshotLength >= lightingData.TileLightTileCount && lightingData.TileLightTileCount > 0;

            builder.Append("  TileLightDebugRequested=").Append(requested);
            builder.Append(" TileLightDebugActive=").Append(active);
            builder.Append(" TileLightDebugStatus=").Append(status);
            builder.Append(" TileLightDebugViewMode=").Append(requested ? BurtShadingDebugSettings.Mode.ToString() : "Disabled");
            builder.Append(" TileLightDebugGpuPath=").Append(BurtTileLightDebugViewUtility.ResolveGpuPathLabel());
            builder.Append(" TileLightDebugLocalDeferredTargets=").Append(hasLocalDeferredTargets);
            builder.Append(" TileLightDebugBuildGate=").Append(buildGate);
            builder.Append(" TileLightCountUniform=").Append(uniformTileCount);
            builder.Append(" TileLightCountSnapshot=").Append(hasCountSnapshot);
            builder.Append(" TileLightDebugVisualNote=").Append(visualNote);
            builder.AppendLine();

            builder.Append("  TiledLightDebugBuildAttempted=").Append(lightingData != null && lightingData.TileLightDebugBuildAttempted);
            builder.Append(" TiledLightDebugUploadedThisFrame=").Append(uploaded);
            builder.Append(" TiledLightDebugReleasedBeforeDump=").Append(releasedBeforeDump);
            builder.Append(" TiledLightDebugMode=").Append(lightingData != null ? lightingData.TileLightDebugBuildMode : "Disabled");
            builder.Append(" TileSize=").Append(lightingData != null ? lightingData.TileLightTileSize : BurtTiledLightData.TileSize);
            builder.Append(" TileGrid=").Append(lightingData != null ? lightingData.TileLightGridX : 0).Append("x").Append(lightingData != null ? lightingData.TileLightGridY : 0);
            builder.Append(" TileCount=").Append(lightingData != null ? lightingData.TileLightTileCount : 0);
            builder.Append(" MaxLightsPerTile=").Append(lightingData != null ? lightingData.TileLightMaxLightsPerTile : BurtTiledLightData.MaxLightsPerTile);
            builder.Append(" TileListCapacity=").Append(lightingData != null ? lightingData.TileLightListCapacity : 0);
            builder.Append(" TileMinLightCount=").Append(lightingData != null ? lightingData.TileLightMinCount : 0);
            builder.Append(" TileMaxLightCount=").Append(lightingData != null ? lightingData.TileLightMaxCount : 0);
            builder.Append(" TileAverageLightCount=").Append(lightingData != null ? FormatFloat(lightingData.TileLightAverageCount) : "0");
            builder.Append(" TileOverflowTiles=").Append(lightingData != null ? lightingData.TileLightOverflowTileCount : 0);
            builder.Append(" TileMaxOverflowExtraCount=").Append(lightingData != null ? lightingData.TileLightMaxOverflowExtraCount : 0);
            builder.AppendLine();

            builder.Append("  ClusterLightUploadedThisFrame=").Append(clusterUploaded);
            builder.Append(" ClusterLightReleasedBeforeDump=").Append(clusterReleasedBeforeDump);
            builder.Append(" ClusterTileSize=").Append(lightingData != null ? lightingData.ClusterLightTileSize : BurtTiledLightData.TileSize);
            builder.Append(" ClusterGrid=").Append(lightingData != null ? lightingData.ClusterLightGridX : 0).Append("x").Append(lightingData != null ? lightingData.ClusterLightGridY : 0);
            builder.Append(" ClusterDepthSlices=").Append(lightingData != null ? lightingData.ClusterLightDepthSliceCount : 0);
            builder.Append(" ClusterCount=").Append(lightingData != null ? lightingData.ClusterLightClusterCount : 0);
            builder.Append(" MaxLightsPerCluster=").Append(lightingData != null ? lightingData.ClusterLightMaxLightsPerCluster : BurtTiledLightData.MaxLightsPerCluster);
            builder.Append(" ClusterListCapacity=").Append(lightingData != null ? lightingData.ClusterLightListCapacity : 0);
            builder.Append(" ClusterMinLightCount=").Append(lightingData != null ? lightingData.ClusterLightMinCount : 0);
            builder.Append(" ClusterMaxLightCount=").Append(lightingData != null ? lightingData.ClusterLightMaxCount : 0);
            builder.Append(" ClusterAverageLightCount=").Append(lightingData != null ? FormatFloat(lightingData.ClusterLightAverageCount) : "0");
            builder.Append(" ClusterOverflowClusters=").Append(lightingData != null ? lightingData.ClusterLightOverflowClusterCount : 0);
            builder.Append(" ClusterMaxOverflowExtraCount=").Append(lightingData != null ? lightingData.ClusterLightMaxOverflowExtraCount : 0);
            builder.Append(" ClusterNearFar=").Append(lightingData != null ? FormatFloat(lightingData.ClusterLightNearPlane) : "0");
            builder.Append("/").Append(lightingData != null ? FormatFloat(lightingData.ClusterLightFarPlane) : "0");
            builder.Append(" ClusterInvDepthRange=").Append(lightingData != null ? FormatFloat(lightingData.ClusterLightInvDepthRange) : "0");
            builder.Append(" ClusterWorldToViewZ=").Append(lightingData != null ? FormatVector4(lightingData.ClusterLightWorldToViewZ) : "(0,0,0,0)");
            builder.AppendLine();

            builder.Append("  TileLightCountBufferRegistered=").Append(countRegistered);
            builder.Append(" TileLightCountBufferAllocatedAtDumpTime=").Append(countAllocated);
            builder.Append(" TileLightCountBufferDescriptorValid=").Append(countDescriptorValid);
            builder.Append(" TileLightListBufferRegistered=").Append(listRegistered);
            builder.Append(" TileLightListBufferAllocatedAtDumpTime=").Append(listAllocated);
            builder.Append(" TileLightListBufferDescriptorValid=").Append(listDescriptorValid);
            builder.Append(" TileLightOffsetBufferRegistered=").Append(offsetRegistered);
            builder.Append(" TileLightOffsetBufferAllocatedAtDumpTime=").Append(offsetAllocated);
            builder.Append(" TileLightOffsetBufferDescriptorValid=").Append(offsetDescriptorValid);
            builder.AppendLine();

            builder.Append("  TileLightBufferDescriptors: Count=(").Append(FormatBufferDescriptor(hasCountDescriptor, countDescriptor));
            builder.Append(") List=(").Append(FormatBufferDescriptor(hasListDescriptor, listDescriptor));
            builder.Append(") Offset=(").Append(FormatBufferDescriptor(hasOffsetDescriptor, offsetDescriptor));
            builder.AppendLine(")");

            builder.Append("  ClusterLightCountBufferRegistered=").Append(clusterCountRegistered);
            builder.Append(" ClusterLightCountBufferAllocatedAtDumpTime=").Append(clusterCountAllocated);
            builder.Append(" ClusterLightCountBufferDescriptorValid=").Append(clusterCountDescriptorValid);
            builder.Append(" ClusterLightListBufferRegistered=").Append(clusterListRegistered);
            builder.Append(" ClusterLightListBufferAllocatedAtDumpTime=").Append(clusterListAllocated);
            builder.Append(" ClusterLightListBufferDescriptorValid=").Append(clusterListDescriptorValid);
            builder.Append(" ClusterLightOffsetBufferRegistered=").Append(clusterOffsetRegistered);
            builder.Append(" ClusterLightOffsetBufferAllocatedAtDumpTime=").Append(clusterOffsetAllocated);
            builder.Append(" ClusterLightOffsetBufferDescriptorValid=").Append(clusterOffsetDescriptorValid);
            builder.Append(" ClusterLightBuffersReady=").Append(clusterBuffersRegistered && clusterDescriptorsValid);
            builder.AppendLine();

            builder.Append("  ClusterLightBufferDescriptors: Count=(").Append(FormatBufferDescriptor(hasClusterCountDescriptor, clusterCountDescriptor));
            builder.Append(") List=(").Append(FormatBufferDescriptor(hasClusterListDescriptor, clusterListDescriptor));
            builder.Append(") Offset=(").Append(FormatBufferDescriptor(hasClusterOffsetDescriptor, clusterOffsetDescriptor));
            builder.AppendLine(")");

            if (requested || (lightingData != null && lightingData.TileLightDebugBuildAttempted))
            {
                AppendTileLightCountSamples(builder, lightingData);
            }
        }

        private static void AppendTileLightCountSamples(StringBuilder builder, BurtLightingData lightingData)
        {
            builder.Append("  TileLightCountSamples: ");

            if (!TryGetTileLightCountSnapshot(lightingData, out var snapshot, out var safeCount, out var gridX, out var gridY))
            {
                builder.AppendLine("<none: snapshot unavailable>");
                return;
            }

            uint minCount = uint.MaxValue;
            uint maxCount = 0u;
            ulong sumCount = 0ul;
            var minIndex = 0;
            var maxIndex = 0;
            var nonZeroTiles = 0;

            for (var tileIndex = 0; tileIndex < safeCount; tileIndex++)
            {
                var count = snapshot[tileIndex];
                if (count < minCount)
                {
                    minCount = count;
                    minIndex = tileIndex;
                }

                if (count > maxCount)
                {
                    maxCount = count;
                    maxIndex = tileIndex;
                }

                if (count > 0u)
                {
                    nonZeroTiles++;
                }

                sumCount += count;
            }

            var averageCount = safeCount > 0 ? (float)sumCount / safeCount : 0f;
            var statsMatch = lightingData != null &&
                safeCount == lightingData.TileLightTileCount &&
                (int)minCount == lightingData.TileLightMinCount &&
                (int)maxCount == lightingData.TileLightMaxCount &&
                Mathf.Abs(averageCount - lightingData.TileLightAverageCount) <= 0.001f;

            builder.Append("SnapshotLength=").Append(lightingData != null ? lightingData.TileLightDebugCountSnapshotLength : 0);
            builder.Append(" SafeCount=").Append(safeCount);
            builder.Append(" Grid=").Append(gridX).Append("x").Append(gridY);
            builder.Append(" NonZeroTiles=").Append(nonZeroTiles);
            builder.Append(" SnapshotMin=").Append(minCount);
            builder.Append(" SnapshotMax=").Append(maxCount);
            builder.Append(" SnapshotAverage=").Append(FormatFloat(averageCount));
            builder.Append(" StatsMatch=").Append(statsMatch);
            builder.AppendLine();

            builder.Append("  TileLightCountSamplePoints: ");
            AppendTileLightSample(builder, "Corner00", 0, 0, gridX, gridY, safeCount, snapshot);
            builder.Append(" ");
            AppendTileLightSample(builder, "CornerX0", gridX - 1, 0, gridX, gridY, safeCount, snapshot);
            builder.Append(" ");
            AppendTileLightSample(builder, "Corner0Y", 0, gridY - 1, gridX, gridY, safeCount, snapshot);
            builder.Append(" ");
            AppendTileLightSample(builder, "CornerXY", gridX - 1, gridY - 1, gridX, gridY, safeCount, snapshot);
            builder.Append(" ");
            AppendTileLightSample(builder, "Center", gridX / 2, gridY / 2, gridX, gridY, safeCount, snapshot);
            builder.Append(" ");
            AppendTileLightSample(builder, "Min", minIndex % gridX, minIndex / gridX, gridX, gridY, safeCount, snapshot);
            builder.Append(" ");
            AppendTileLightSample(builder, "Max", maxIndex % gridX, maxIndex / gridX, gridX, gridY, safeCount, snapshot);
            builder.AppendLine();
        }

        private static bool TryGetTileLightCountSnapshot(
            BurtLightingData lightingData,
            out uint[] snapshot,
            out int safeCount,
            out int gridX,
            out int gridY)
        {
            snapshot = lightingData != null ? lightingData.TileLightDebugCountSnapshot : null;
            gridX = lightingData != null ? lightingData.TileLightGridX : 0;
            gridY = lightingData != null ? lightingData.TileLightGridY : 0;
            safeCount = 0;

            if (lightingData == null || snapshot == null || gridX <= 0 || gridY <= 0)
            {
                return false;
            }

            var declaredCount = Mathf.Min(lightingData.TileLightTileCount, lightingData.TileLightDebugCountSnapshotLength);
            var gridCount = gridX * gridY;
            safeCount = Mathf.Min(Mathf.Min(declaredCount, snapshot.Length), gridCount);
            return safeCount > 0;
        }

        private static void AppendTileLightSample(
            StringBuilder builder,
            string label,
            int tileX,
            int tileY,
            int gridX,
            int gridY,
            int safeCount,
            uint[] snapshot)
        {
            var clampedX = Mathf.Clamp(tileX, 0, gridX - 1);
            var clampedY = Mathf.Clamp(tileY, 0, gridY - 1);
            var tileIndex = clampedY * gridX + clampedX;

            builder.Append(label);
            builder.Append('(').Append(clampedX).Append(',').Append(clampedY).Append(")=");

            if (snapshot == null || tileIndex < 0 || tileIndex >= safeCount)
            {
                builder.Append("<out-of-range>");
                return;
            }

            builder.Append(snapshot[tileIndex]);
        }

        private static void AppendAdditionalLightDetails(StringBuilder builder, BurtLightingData lightingData)
        {
            if (lightingData == null || lightingData.AdditionalLightCount <= 0)
            {
                builder.AppendLine("  Additional Lights: <none>");
                return;
            }

            builder.AppendLine("  Additional Lights:");
            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);

            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                var positionAndRange = lightingData.AdditionalLightPositionAndRange[lightIndex];
                var colorAndType = lightingData.AdditionalLightColorAndType[lightIndex];
                var directionAndSpot = lightingData.AdditionalLightDirectionAndSpot[lightIndex];
                var spotParams = lightingData.AdditionalLightSpotParams[lightIndex];

                builder.Append("    #").Append(lightIndex);
                builder.Append(" Type=").Append(FormatAdditionalLightType(colorAndType.w));
                builder.Append(" Color=").Append(FormatVector3(colorAndType.x, colorAndType.y, colorAndType.z));
                builder.Append(" Position=").Append(FormatVector3(positionAndRange.x, positionAndRange.y, positionAndRange.z));
                builder.Append(" Range=").Append(FormatFloat(positionAndRange.w));
                builder.Append(" Direction=").Append(FormatVector3(directionAndSpot.x, directionAndSpot.y, directionAndSpot.z));
                builder.Append(" VolumetricScale=").Append(FormatFloat(directionAndSpot.w));
                builder.Append(" VolumetricNearCutoff=").Append(FormatFloat(spotParams.w));
                builder.Append(" SpotParams=").Append(FormatSpotParams(spotParams));
                builder.Append(" Shadow=").Append(FormatAdditionalLightShadowState(lightingData, lightIndex));
                builder.AppendLine();
            }
        }

        private static int CountActiveAdditionalLightShadowSlots(BurtLightingData lightingData)
        {
            if (lightingData == null)
            {
                return 0;
            }

            var count = 0;
            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);
            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                if (IsActiveAdditionalLightShadowSlot(lightingData, lightIndex))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSoftAdditionalLightShadowSlots(BurtLightingData lightingData)
        {
            if (lightingData == null)
            {
                return 0;
            }

            var count = 0;
            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);
            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                if (IsActiveAdditionalLightShadowSlot(lightingData, lightIndex) && lightingData.AdditionalLightShadowData[lightIndex].w > 0.5f)
                {
                    count++;
                }
            }

            return count;
        }

        private static int ResolveMaxActiveAdditionalLightShadowSlot(BurtLightingData lightingData)
        {
            if (lightingData == null)
            {
                return -1;
            }

            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);
            for (var lightIndex = additionalLightCount - 1; lightIndex >= 0; lightIndex--)
            {
                if (IsActiveAdditionalLightShadowSlot(lightingData, lightIndex))
                {
                    return lightIndex;
                }
            }

            return -1;
        }

        private static bool IsActiveAdditionalLightShadowSlot(BurtLightingData lightingData, int lightIndex)
        {
            return lightingData != null &&
                lightIndex >= 0 &&
                lightIndex < BurtLightingData.MaxAdditionalLights &&
                lightIndex < lightingData.AdditionalLightCount &&
                lightingData.AdditionalLightShadowData[lightIndex].x > 0.5f &&
                lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex] >= 0;
        }

        private static bool IsTileLightDebugRequested()
        {
            return BurtTileLightDebugViewUtility.IsTileLightDebugMode(BurtShadingDebugSettings.Mode);
        }

        private static bool HasLocalDeferredTargets(BurtRequestRenderOptions renderOptions)
        {
            return renderOptions == null || (renderOptions.ShouldAllocateCameraColor && renderOptions.ShouldAllocateCameraDepth);
        }

        private static string ResolveTileLightDebugPipelineNote(bool requested, bool rendererSupported, bool hasLocalDeferredTargets)
        {
            if (!requested)
            {
                return "Disabled";
            }

            if (!rendererSupported)
            {
                return "RequiresDeferredRenderer";
            }

            return hasLocalDeferredTargets ? "DeferredRenderer" : "WaitingForLocalDeferredTargets";
        }

        private static string ResolveTileLightDebugStatus(
            bool requested,
            bool effectiveDeferred,
            bool hasLocalDeferredTargets,
            bool requiredBuffersRegistered,
            bool requiredDescriptorsValid,
            BurtLightingData lightingData,
            bool uploaded)
        {
            if (!requested)
            {
                return "Disabled";
            }

            if (!effectiveDeferred)
            {
                return "RequiresDeferredRenderer";
            }

            if (!hasLocalDeferredTargets)
            {
                return "WaitingForLocalDeferredTargets";
            }

            if (!requiredBuffersRegistered)
            {
                return "TileBuffersNotRegistered";
            }

            if (!requiredDescriptorsValid)
            {
                return "TileBufferDescriptorInvalid";
            }

            if (lightingData == null || !lightingData.TileLightDebugBuildAttempted)
            {
                return "BuildNotAttempted";
            }

            return uploaded ? "Active" : "UploadFailed";
        }

        private static string ResolveTileLightDebugBuildGate(
            bool requested,
            bool effectiveDeferred,
            bool hasLocalDeferredTargets,
            bool countRegistered,
            bool listRegistered,
            bool offsetRegistered,
            bool countDescriptorValid,
            bool listDescriptorValid,
            bool offsetDescriptorValid,
            bool requiresListBuffers,
            BurtLightingData lightingData,
            bool uploaded)
        {
            if (!requested)
            {
                return "ModeDisabled";
            }

            if (!effectiveDeferred)
            {
                return "RequiresDeferredRenderer";
            }

            if (!hasLocalDeferredTargets)
            {
                return "WaitingForLocalDeferredTargets";
            }

            if (!countRegistered)
            {
                return "CountBufferNotRegistered";
            }

            if (!countDescriptorValid)
            {
                return "CountBufferDescriptorInvalid";
            }

            if (requiresListBuffers && (!listRegistered || !offsetRegistered))
            {
                return "ListOrOffsetBufferNotRegistered";
            }

            if (requiresListBuffers && (!listDescriptorValid || !offsetDescriptorValid))
            {
                return "ListOrOffsetBufferDescriptorInvalid";
            }

            if (lightingData == null)
            {
                return "LightingDataUnavailable";
            }

            if (!lightingData.TileLightDebugBuildAttempted)
            {
                return "BuildPassNotAttempted";
            }

            return uploaded ? "BuiltAndUploaded" : "BuildRanUploadFailed";
        }

        private static string ResolveTileLightDebugVisualNote(bool requested, bool active, BurtLightingData lightingData, bool uniformTileCount)
        {
            if (!requested)
            {
                return "Disabled";
            }

            if (!active)
            {
                return "Inactive";
            }

            if (lightingData == null || lightingData.TileLightTileCount <= 0)
            {
                return "NoTileData";
            }

            if (lightingData.AdditionalLightCount <= 0)
            {
                return "NoAdditionalLights";
            }

            if (lightingData.TileLightOverflowTileCount > 0)
            {
                return "OverflowTiles";
            }

            if (!uniformTileCount)
            {
                return "VariesByTile";
            }

            return lightingData.TileLightMaxCount <= 0 ? "AllTilesZeroLights" : "AllTilesSameCount_MoveLightsOrReduceRange";
        }

        private static string FormatBufferDescriptor(bool hasDescriptor, BurtRenderBufferDescriptor descriptor)
        {
            if (!hasDescriptor)
            {
                return "<none>";
            }

            return "Count=" + descriptor.Count + ",Stride=" + descriptor.Stride + ",Target=" + descriptor.Target;
        }

        private static string FormatAdditionalLightType(float lightType)
        {
            if (Mathf.Abs(lightType) < 0.5f)
            {
                return "Directional";
            }

            if (Mathf.Abs(lightType - 1f) < 0.5f)
            {
                return "Point";
            }

            if (Mathf.Abs(lightType - 2f) < 0.5f)
            {
                return "Spot";
            }

            return "Unknown(" + FormatFloat(lightType) + ")";
        }

        private static string FormatSpotParams(Vector4 spotParams)
        {
            return "(InnerCos=" + FormatFloat(spotParams.x)
                + " OuterCos=" + FormatFloat(spotParams.y)
                + " InvAngleRange=" + FormatFloat(spotParams.z)
                + " Spare=" + FormatFloat(spotParams.w)
                + ")";
        }

        private static string FormatAdditionalLightShadowState(BurtLightingData lightingData, int lightIndex)
        {
            if (lightingData == null || lightIndex < 0 || lightIndex >= BurtLightingData.MaxAdditionalLights)
            {
                return "<none>";
            }

            var visibleLightIndex = lightingData.AdditionalLightShadowVisibleLightIndices[lightIndex];
            var shadowData = lightingData.AdditionalLightShadowData[lightIndex];
            var status = lightingData.AdditionalLightShadowStatuses != null && lightingData.AdditionalLightShadowStatuses.Length > lightIndex
                ? lightingData.AdditionalLightShadowStatuses[lightIndex]
                : BurtAdditionalLightShadowStatus.None;
            var sourceMode = lightingData.AdditionalLightShadowSourceModes != null && lightingData.AdditionalLightShadowSourceModes.Length > lightIndex
                ? lightingData.AdditionalLightShadowSourceModes[lightIndex]
                : LightShadows.None;
            var sourceStrength = lightingData.AdditionalLightShadowSourceStrengths != null && lightingData.AdditionalLightShadowSourceStrengths.Length > lightIndex
                ? lightingData.AdditionalLightShadowSourceStrengths[lightIndex]
                : 0f;
            if (visibleLightIndex < 0 && shadowData.y <= 0.0001f)
            {
                return status == BurtAdditionalLightShadowStatus.None ? "None" : status.ToString();
            }

            var shadowMode = shadowData.w > 0.5f ? "Soft" : "Hard";
            var lightParams = lightingData.AdditionalLightShadowLightParams != null && lightingData.AdditionalLightShadowLightParams.Length > lightIndex
                ? lightingData.AdditionalLightShadowLightParams[lightIndex]
                : Vector4.zero;
            if (shadowData.x <= 0.5f)
            {
                return status + "(VisibleLight=" + visibleLightIndex
                    + ",Strength=" + FormatFloat(shadowData.y)
                    + ",UnityShadows=" + sourceMode
                    + ",UnityStrength=" + FormatFloat(sourceStrength)
                    + ",Mode=" + shadowMode
                    + ")";
            }

            return status + "(VisibleLight=" + visibleLightIndex
                + ",Strength=" + FormatFloat(shadowData.y)
                + ",Tile=" + FormatFloat(shadowData.z)
                + ",FirstSlice=" + FormatFloat(lightParams.x)
                + ",SliceCount=" + FormatFloat(lightParams.y)
                + ",ShadowType=" + FormatAdditionalLightShadowType(lightParams.z)
                + ",ReceiverNormalBias=" + FormatFloat(lightParams.w)
                + ",UnityShadows=" + sourceMode
                + ",UnityStrength=" + FormatFloat(sourceStrength)
                + ",Mode=" + shadowMode
                + ",StableKey=" + lightingData.AdditionalLightShadowStableKeys[lightIndex]
                + ",MatrixHash=" + FormatAdditionalLightShadowFirstSliceMatrixHash(lightingData, lightParams)
                + ",Rect=" + FormatVector4(lightingData.AdditionalLightShadowAtlasRects[lightIndex])
                + ")";
        }

        private static string FormatAdditionalLightShadowFace(int faceIndex, float shadowType)
        {
            if (!(shadowType > 0.5f && shadowType < 1.5f))
            {
                return FormatFloat(faceIndex);
            }

            switch ((CubemapFace)Mathf.Clamp(faceIndex, 0, BurtLightingData.PointLightShadowFaceCount - 1))
            {
                case CubemapFace.PositiveX:
                    return "PositiveX";
                case CubemapFace.NegativeX:
                    return "NegativeX";
                case CubemapFace.PositiveY:
                    return "PositiveY";
                case CubemapFace.NegativeY:
                    return "NegativeY";
                case CubemapFace.PositiveZ:
                    return "PositiveZ";
                case CubemapFace.NegativeZ:
                    return "NegativeZ";
                default:
                    return FormatFloat(faceIndex);
            }
        }

        private static string FormatAdditionalLightShadowType(float type)
        {
            if (type > 0.5f && type < 1.5f)
            {
                return "Point";
            }

            if (type > 1.5f && type < 2.5f)
            {
                return "Spot";
            }

            return FormatFloat(type);
        }

        private static string FormatVector4(Vector4 value)
        {
            return "(" + FormatFloat(value.x)
                + "," + FormatFloat(value.y)
                + "," + FormatFloat(value.z)
                + "," + FormatFloat(value.w)
                + ")";
        }

        private static string FormatRect(Rect value)
        {
            return "(" + FormatFloat(value.x)
                + "," + FormatFloat(value.y)
                + "," + FormatFloat(value.width)
                + "," + FormatFloat(value.height)
                + ")";
        }

        private static string FormatBounds(Bounds value)
        {
            return "(Center=" + FormatVector3(value.center.x, value.center.y, value.center.z)
                + ",Size=" + FormatVector3(value.size.x, value.size.y, value.size.z)
                + ")";
        }

        private static string FormatHexMask(int value)
        {
            return "0x" + unchecked((uint)value).ToString("X8", CultureInfo.InvariantCulture);
        }

        private static string FormatSubsurfaceProfiles(BurtSubsurfaceProfilePalette palette)
        {
            var count = Mathf.Clamp(palette.Count, 1, BurtSubsurfaceProfilePalette.MaxProfiles);
            var builder = new StringBuilder();
            for (var i = 0; i < BurtSubsurfaceProfilePalette.MaxProfiles; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                builder.Append(i).Append(":");
                if (i < count)
                {
                    builder.Append(palette.GetName(i));
                }
                else
                {
                    builder.Append("<fallback:").Append(palette.GetName(0)).Append(">");
                }
            }

            return builder.ToString();
        }

        private static string ResolveScreenSpaceSubsurfacePath(bool enabled, bool usesMaskTexture, bool usesBurley, bool usesSeparable)
        {
            if (!enabled)
            {
                return "Disabled";
            }

            var path = "CopySource";
            if (usesBurley)
            {
                path += "BuildVelocity";
            }

            if (usesMaskTexture)
            {
                path += "BuildMask";
            }

            path += "Setup";
            if (usesSeparable)
            {
                path += "SeparableHorizontalVertical";
            }

            if (usesBurley)
            {
                path += "BurleyGroupsBurleyStoreHistory";
            }

            return path + "CombineFinalCopy";
        }

        private static string ResolveScreenSpaceSubsurfaceResourceMode(bool enabled, bool usesBurley, bool usesSeparable)
        {
            if (!enabled)
            {
                return "Disabled";
            }

            if (usesBurley && usesSeparable)
            {
                return "Mixed4S5S";
            }

            if (usesBurley)
            {
                return "5SBurleyOnly";
            }

            if (usesSeparable)
            {
                return "4SSeparableOnly";
            }

            return "NoActiveMaterialMode";
        }

        private static string FormatAdditionalLightShadowFirstSliceMatrixHash(BurtLightingData lightingData, Vector4 lightParams)
        {
            if (lightingData == null)
            {
                return "00000000";
            }

            var sliceIndex = Mathf.RoundToInt(lightParams.x);
            if (sliceIndex < 0 || sliceIndex >= BurtLightingData.MaxAdditionalLightShadowSlices)
            {
                return "00000000";
            }

            return FormatMatrixHash(lightingData.AdditionalLightShadowSliceWorldToShadowMatrices[sliceIndex]);
        }

        private static string FormatMatrixHash(Matrix4x4 matrix)
        {
            unchecked
            {
                var hash = 17;
                for (var row = 0; row < 4; row++)
                {
                    for (var column = 0; column < 4; column++)
                    {
                        hash = hash * 31 + Mathf.RoundToInt(matrix[row, column] * 100000f);
                    }
                }

                return hash.ToString("X8", CultureInfo.InvariantCulture);
            }
        }

        private static string FormatVector3(float x, float y, float z)
        {
            return "(" + FormatFloat(x) + "," + FormatFloat(y) + "," + FormatFloat(z) + ")";
        }

        private static string FormatVector3Int(Vector3Int value)
        {
            return "(" + value.x + "," + value.y + "," + value.z + ")";
        }

        private static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void AppendCameraState( // 写入当前相机本身的诊断状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request) // 接收当前渲染请求，用来读取相机和最终输出目标。
        {
            builder.AppendLine("Camera State:"); // 单独成段输出，方便和 RT 格式、后处理状态分开扫描。

            var camera = request != null ? request.Camera : null; // 从 request 安全读取相机，request 为空时保持 camera 为空。

            if (camera == null) // 如果没有相机，说明当前 dump 来自异常路径。
            {
                builder.AppendLine("  Camera=<none>"); // 明确写出没有相机，避免后续字段看起来像被截断。

                return; // 没有相机时无法继续读取 HDR、像素尺寸或 targetTexture。
            }

            builder.Append("  Name=").Append(camera.name); // 写入相机名称，方便和 Console 中的 Request 行对应。

            builder.Append(" CameraType=").Append(camera.cameraType); // 写入 Unity 相机类型，区分 Game、SceneView 和 Preview。

            builder.Append(" PixelSize=").Append(Mathf.Max(1, camera.pixelWidth)).Append('x').Append(Mathf.Max(1, camera.pixelHeight)); // 写入相机当前像素尺寸。

            builder.Append(" AllowHDR=").Append(camera.allowHDR); // 写入相机 HDR 开关，排查 HDR 能量是否可能被提前截断。

            builder.Append(" TargetTexture=").Append(FormatRenderTextureName(camera.targetTexture)); // 写入 targetTexture 名称或 <none>。

            builder.Append(" FinalTarget=").Append(request != null ? request.TargetIdentifier.ToString() : "<none>"); // 写入 request 最终输出目标，方便确认是否写到 CameraTarget 或 RenderTexture。

            builder.AppendLine(); // 结束相机状态行。

            if (camera.targetTexture != null) // 如果相机输出到 RenderTexture，就补充目标 RT 的真实格式。
            {
                builder.Append("  TargetTextureState="); // 写入 targetTexture 详情标签。

                AppendRenderTextureState(builder, camera.targetTexture); // 输出 targetTexture 的尺寸、格式、HDR/LDR 和采样数。

                builder.AppendLine(); // targetTexture 详情行结束。
            }
        }

        private static void AppendRenderTargetState( // 写入 BurtRP 中间 RT 的格式和注册状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来推导 RT 描述。
            BurtRenderPipelineAsset asset, // 接收管线资产，用来判断哪些资源理论上会注册。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收 RenderGraph 资源表，用来输出实际注册状态。
        {
            builder.AppendLine("Render Target State:"); // 单独成段输出，后续排查 HDR/LDR 和格式问题时优先看这里。

            var camera = request != null ? request.Camera : null; // 从 request 安全读取相机，描述工具会对空相机回退 1x1。

            AppendDescriptorLine(builder, "CameraColor", BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.CameraColorName); // 输出 CameraColor 中间 RT 描述。

            AppendDescriptorLine(builder, "CameraDepth", BurtRenderTargetDescriptorUtility.CreateCameraDepthDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.CameraDepthName); // 输出 CameraDepth 中间 RT 描述。

            if (PostProcessUtility.ShouldUsePostProcessFramework(request, asset)) // 只有当前 request 实际会使用后处理框架时，才输出 PostProcessColor 描述。
            {
                AppendDescriptorLine(builder, "PostProcessColor", BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.PostProcessColorName); // 输出后处理 ping-pong RT 描述。
            }
            else // 当前 request 不执行后处理框架。
            {
                AppendSkippedRenderTargetLine(builder, "PostProcessColor", resourceRegistry, BurtRenderGraphResourceRegistry.PostProcessColorName); // 写出跳过状态，方便确认不是资源丢失。
            }

            if (BurtRefractionPassUtility.ShouldUseRefraction(request, asset))
            {
                AppendDescriptorLine(builder, "RefractionDistortion", BurtRenderTargetDescriptorUtility.CreateRefractionDistortionDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.RefractionDistortionName);
                AppendDescriptorLine(builder, "RefractionSceneColorMipChain", BurtRenderTargetDescriptorUtility.CreateRefractionSceneColorMipChainDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.RefractionSceneColorMipChainName);
            }
            else
            {
                AppendSkippedRenderTargetLine(builder, "RefractionDistortion", resourceRegistry, BurtRenderGraphResourceRegistry.RefractionDistortionName);
                AppendSkippedRenderTargetLine(builder, "RefractionSceneColorMipChain", resourceRegistry, BurtRenderGraphResourceRegistry.RefractionSceneColorMipChainName);
            }

            if (IsDeferredRequest(request, asset)) // 当前 request 真正走 Deferred 时才会申请 GBuffer。
            {
                AppendDescriptorLine(builder, "GBuffer0", BurtRenderTargetDescriptorUtility.CreateGBuffer0Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer0Name); // 输出 GBuffer0 格式，保存 DepthNormals prepass 写入的 normal/roughness。

                AppendDescriptorLine(builder, "GBuffer1", BurtRenderTargetDescriptorUtility.CreateGBuffer1Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer1Name); // 输出 GBuffer1 格式，当前保存 baseColor/occlusion。

                AppendDescriptorLine(builder, "GBuffer2", BurtRenderTargetDescriptorUtility.CreateGBuffer2Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer2Name); // 输出 GBuffer2 格式，保存 shading model/material channel、metallic、smoothness 和 reflectance。

                AppendDescriptorLine(builder, "GBuffer3", BurtRenderTargetDescriptorUtility.CreateGBuffer3Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer3Name);

                AppendDescriptorLine(builder, "GBuffer4", BurtRenderTargetDescriptorUtility.CreateGBuffer4Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer4Name);

                AppendDescriptorLine(builder, "GBuffer5", BurtRenderTargetDescriptorUtility.CreateGBuffer5Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer5Name);

                AppendDescriptorLine(builder, "GBufferObjectIndex", BurtRenderTargetDescriptorUtility.CreateGBufferObjectIndexDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBufferObjectIndexName);

                AppendDescriptorLine(builder, "DeferredLightingDepth", BurtRenderTargetDescriptorUtility.CreateDeferredLightingDepthDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.DeferredLightingDepthName);

                if (BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusion(request, asset))
                {
                    AppendDescriptorLine(builder, "ScreenSpaceAmbientOcclusionRaw", BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
                    AppendDescriptorLine(builder, "ScreenSpaceAmbientOcclusionFinal", BurtRenderTargetDescriptorUtility.CreateScreenSpaceAmbientOcclusionDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);
                }
                else
                {
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceAmbientOcclusionRaw", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceAmbientOcclusionFinal", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);
                }

                if (BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadow(request, asset))
                {
                    var ssShadowSettings = BurtScreenSpaceShadowPassUtility.ResolveScreenSpaceShadowSettings(request, asset);
                    AppendDescriptorLine(builder, "ScreenSpaceShadow", BurtRenderTargetDescriptorUtility.CreateScreenSpaceShadowDescriptor(camera, ssShadowSettings.DownsampleFactor), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);
                }
                else
                {
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceShadow", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);
                }

                if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIllumination(request, asset))
                {
                    var burtGISettings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(request, asset);
                    var burtGIScreenProbeSettings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(request, asset);
                    AppendDescriptorLine(builder, "ScreenSpaceGlobalIlluminationRaw", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, burtGISettings), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);
                    AppendDescriptorLine(builder, "ScreenSpaceGlobalIllumination", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, burtGISettings), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
                    AppendDescriptorLine(builder, "BurtGIBackfaceDiffuseIndirect", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, burtGISettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
                    AppendDescriptorLine(builder, "BurtGIRoughSpecularIndirect", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDescriptor(camera, burtGISettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);
                    AppendDescriptorLine(builder, "BurtGITranslucencyVolume0", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name);
                    AppendDescriptorLine(builder, "BurtGITranslucencyVolume1", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name);
                    AppendDescriptorLine(builder, "BurtGITranslucencyVolumeFilter0", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name);
                    AppendDescriptorLine(builder, "BurtGITranslucencyVolumeFilter1", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name);
                    AppendDescriptorLine(builder, "BurtGITranslucencyVolumeTraceRadiance", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeTraceAtlasDescriptor(camera, burtGIScreenProbeSettings, RenderTextureFormat.ARGBHalf), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceRadianceName);
                    AppendDescriptorLine(builder, "BurtGITranslucencyVolumeTraceFilteredRadiance", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeTraceAtlasDescriptor(camera, burtGIScreenProbeSettings, RenderTextureFormat.ARGBHalf), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceFilteredRadianceName);
                    AppendDescriptorLine(builder, "BurtGITranslucencyVolumeTraceHitDistance", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationTranslucencyVolumeTraceAtlasDescriptor(camera, burtGIScreenProbeSettings, RenderTextureFormat.RHalf), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceHitDistanceName);
                    if (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(request, asset))
                    {
                        AppendDescriptorLine(builder, "BurtGITemporalDiagnostics", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationDiagnosticsDescriptor(camera, burtGISettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);
                    }
                    else
                    {
                        AppendSkippedRenderTargetLine(builder, "BurtGITemporalDiagnostics", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);
                    }

                    if (burtGIScreenProbeSettings.Enabled)
                    {
                        var burtGIScreenProbeDescriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeDescriptor(camera, burtGIScreenProbeSettings);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeScreenDepth", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbePlacementDescriptor(camera, burtGIScreenProbeSettings, BurtScreenProbePlacementTextureKind.ScreenDepth), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeWorldNormal", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbePlacementDescriptor(camera, burtGIScreenProbeSettings, BurtScreenProbePlacementTextureKind.WorldNormal), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeWorldPosition", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbePlacementDescriptor(camera, burtGIScreenProbeSettings, BurtScreenProbePlacementTextureKind.WorldPosition), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeAdaptiveProbeHeader", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeAdaptiveDescriptor(camera, burtGIScreenProbeSettings, BurtScreenProbeAdaptiveTextureKind.ProbeHeader), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeHeaderName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeAdaptiveProbeIndices", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeAdaptiveDescriptor(camera, burtGIScreenProbeSettings, BurtScreenProbeAdaptiveTextureKind.ProbeIndices), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeIndicesName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeRadiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeIrradiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeConfidence", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeHitDistance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName);
                        var burtGIScreenProbeBentNormalDescriptor = burtGIScreenProbeDescriptor;
                        burtGIScreenProbeBentNormalDescriptor.enableRandomWrite = true;
                        AppendDescriptorLine(builder, "BurtGIScreenProbeBentNormal", burtGIScreenProbeBentNormalDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName);
                        var burtGIScreenProbeTraceDescriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeTraceDescriptor(camera, burtGIScreenProbeSettings);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeTraceRadiance", burtGIScreenProbeTraceDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeTraceHit", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeTraceHitDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeTemporalRadiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeTemporalIrradiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeTemporalConfidence", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeFilteredRadiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeFilteredIrradiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeFilteredConfidence", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeFixupRadiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeFixupIrradiance", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeFixupConfidence", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName);
                        var burtGIScreenProbeMipDescriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeMipDescriptor(camera, burtGIScreenProbeSettings, 1);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeMipRadiance", burtGIScreenProbeMipDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeMipIrradiance", burtGIScreenProbeMipDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeMipConfidence", burtGIScreenProbeMipDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName);
                        var burtGIScreenProbeMip2Descriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeMipDescriptor(camera, burtGIScreenProbeSettings, 2);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeMip2Radiance", burtGIScreenProbeMip2Descriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2RadianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeMip2Irradiance", burtGIScreenProbeMip2Descriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2IrradianceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeMip2Confidence", burtGIScreenProbeMip2Descriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2ConfidenceName);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeRadianceSHAmbient", burtGIScreenProbeDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName);
                        var burtGIScreenProbeSHDirectionalDescriptor = BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationScreenProbeSHDirectionalDescriptor(camera, burtGIScreenProbeSettings);
                        AppendDescriptorLine(builder, "BurtGIScreenProbeRadianceSHDirectional", burtGIScreenProbeSHDirectionalDescriptor, resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName);
                        AppendDescriptorLine(builder, "BurtGIRadianceCacheClipMapIndirection", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapIndirectionDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName);
                        AppendDescriptorLine(builder, "BurtGIRadianceCacheClipMapDepthProbeAtlas", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapDepthProbeAtlasDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName);
                        AppendDescriptorLine(builder, "BurtGIRadianceCacheClipMapRadianceProbeAtlas", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapRadianceProbeAtlasDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName);
                        AppendDescriptorLine(builder, "BurtGIRadianceCacheClipMapFinalRadianceAtlas", BurtScreenSpaceGlobalIlluminationPassUtility.CreateScreenSpaceGlobalIlluminationRadianceCacheClipMapFinalRadianceAtlasDescriptor(camera, burtGIScreenProbeSettings), resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName);
                    }
                    else
                    {
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeScreenDepth", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeWorldNormal", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeWorldPosition", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeHitDistance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeBentNormal", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTraceRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTraceHit", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadianceSHAmbient", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadianceSHDirectional", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapIndirection", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapDepthProbeAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapRadianceProbeAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName);
                        AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapFinalRadianceAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName);
                    }
                }
                else
                {
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceGlobalIlluminationRaw", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceGlobalIllumination", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIBackfaceDiffuseIndirect", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIRoughSpecularIndirect", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);
                    AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolume0", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name);
                    AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolume1", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name);
                    AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeFilter0", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name);
                    AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeFilter1", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name);
                    AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeTraceRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeTraceFilteredRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceFilteredRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeTraceHitDistance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceHitDistanceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGITemporalDiagnostics", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeScreenDepth", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeWorldNormal", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeWorldPosition", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeHitDistance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeBentNormal", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTraceRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTraceHit", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadianceSHAmbient", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadianceSHDirectional", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapIndirection", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapDepthProbeAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapRadianceProbeAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName);
                    AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapFinalRadianceAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName);
                }

                if (BurtHiZDepthPassUtility.ShouldUseHiZDepth(request, asset))
                {
                    AppendDescriptorLine(builder, "HiZDepth", BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.HiZDepthName);
                }
                else
                {
                    AppendSkippedRenderTargetLine(builder, "HiZDepth", resourceRegistry, BurtRenderGraphResourceRegistry.HiZDepthName);
                }

                if (BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflections(request, asset))
                {
                    AppendDescriptorLine(builder, "ScreenSpaceReflectionColor", BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
                    AppendDescriptorLine(builder, "ScreenSpaceReflectionDenoisedColor", BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionDenoisedColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);
                    AppendDescriptorLine(builder, "ScreenSpaceReflectionTemporalColor", BurtRenderTargetDescriptorUtility.CreateScreenSpaceReflectionTemporalColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);
                }
                else
                {
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionDenoisedColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionTemporalColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);
                }

                if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(request, asset))
                {
                    AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceSource", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
                    AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceSetup", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceSetupDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
                    AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceProfileIDAndType", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceProfileIDAndTypeDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(request, asset))
                    {
                        AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceMask", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceMaskDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
                    }
                    else
                    {
                        AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceMask", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
                    }

                    AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceTemp", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
                    AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceBlur", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
                    AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceCombine", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceCombineDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
                    if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(request, asset))
                    {
                        AppendDescriptorLine(builder, "ScreenSpaceSubsurfaceVelocity", BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceVelocityDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
                    }
                    else
                    {
                        AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceVelocity", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
                    }
                }
                else
                {
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceSource", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceSetup", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceProfileIDAndType", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceMask", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceTemp", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceBlur", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceCombine", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
                    AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceVelocity", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
                }
            }
            else // Forward 模式不注册 GBuffer。
            {
                AppendSkippedRenderTargetLine(builder, "GBuffer0", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer0Name); // 写出 GBuffer0 跳过状态。

                AppendSkippedRenderTargetLine(builder, "GBuffer1", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer1Name); // 写出 GBuffer1 跳过状态。

                AppendSkippedRenderTargetLine(builder, "GBuffer2", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer2Name); // 写出 GBuffer2 跳过状态。

                AppendSkippedRenderTargetLine(builder, "GBuffer3", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer3Name);

                AppendSkippedRenderTargetLine(builder, "GBuffer4", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer4Name);

                AppendSkippedRenderTargetLine(builder, "GBuffer5", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer5Name);

                AppendSkippedRenderTargetLine(builder, "GBufferObjectIndex", resourceRegistry, BurtRenderGraphResourceRegistry.GBufferObjectIndexName);

                AppendSkippedRenderTargetLine(builder, "DeferredLightingDepth", resourceRegistry, BurtRenderGraphResourceRegistry.DeferredLightingDepthName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceAmbientOcclusionRaw", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceAmbientOcclusionFinal", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceShadow", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceShadowName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceGlobalIlluminationRaw", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceGlobalIllumination", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName);

                AppendSkippedRenderTargetLine(builder, "BurtGIBackfaceDiffuseIndirect", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName);

                AppendSkippedRenderTargetLine(builder, "BurtGIRoughSpecularIndirect", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName);

                AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolume0", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name);

                AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolume1", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name);

                AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeFilter0", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name);

                AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeFilter1", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name);
                AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeTraceRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceRadianceName);
                AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeTraceFilteredRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceFilteredRadianceName);
                AppendSkippedRenderTargetLine(builder, "BurtGITranslucencyVolumeTraceHitDistance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceHitDistanceName);

                AppendSkippedRenderTargetLine(builder, "BurtGITemporalDiagnostics", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeScreenDepth", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeWorldNormal", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeWorldPosition", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeAdaptiveProbeHeader", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeHeaderName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeAdaptiveProbeIndices", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeIndicesName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeHitDistance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeBentNormal", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTraceRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTraceHit", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeTemporalConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFilteredConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeFixupConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipRadiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipIrradiance", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeMipConfidence", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadianceSHAmbient", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName);

                AppendSkippedRenderTargetLine(builder, "BurtGIScreenProbeRadianceSHDirectional", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName);

                AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapIndirection", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName);

                AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapDepthProbeAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName);

                AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapRadianceProbeAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName);

                AppendSkippedRenderTargetLine(builder, "BurtGIRadianceCacheClipMapFinalRadianceAtlas", resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName);

                AppendSkippedRenderTargetLine(builder, "HiZDepth", resourceRegistry, BurtRenderGraphResourceRegistry.HiZDepthName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionDenoisedColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionTemporalColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceSource", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceSetup", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceProfileIDAndType", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceMask", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceTemp", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceBlur", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceCombine", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
                AppendSkippedRenderTargetLine(builder, "ScreenSpaceSubsurfaceVelocity", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
            }

            builder.Append("  FinalCameraTarget Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FinalCameraTargetName)); // 写入最终输出目标是否已注册。

            builder.Append(" Identifier=").Append(request != null ? request.TargetIdentifier.ToString() : "<none>"); // 写入最终目标 RenderTargetIdentifier，定位 CameraTarget 或具体 RenderTexture。

            builder.AppendLine(); // 结束最终输出目标行。
        }

        private static void AppendPostProcessState( // 写入后处理框架和 Volume 效果状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断后处理是否应该执行。
            BurtRenderPipelineAsset asset, // 接收当前管线资产，用来读取后处理框架设置。
            BurtRequestRenderOptions renderOptions) // Receives request render options for BurtGI/TAA exposure parity.
        {
            builder.AppendLine("PostProcess State:"); // 单独成段输出，方便排查 No-op、Tonemapping 和 Color Adjustments。

            var settings = asset != null ? asset.PostProcessSettings : null; // 从资产安全读取后处理框架设置。

            var assetEnabled = settings != null && settings.EnablePostProcessing; // 判断资产上的后处理总开关。

            var noOpCopy = settings != null && settings.EnableNoOpCopy; // 判断资产上的 No-op Copy 验证开关。

            var shouldRunFramework = PostProcessUtility.ShouldUsePostProcessFramework(request, asset); // 解析当前 request 最终是否会插入后处理链。

            var suppressedByShadingDebug = PostProcessUtility.IsPostProcessSuppressedByShadingDebug();

            var tonemappingMode = PostProcessUtility.ResolveTonemappingMode(asset); // 解析当前 Volume 中真正生效的 Tonemapping 模式。

            var exposureSettings = PostProcessUtility.ResolvePhysicalExposureSettings(request, asset);
            var postExposureMultiplier = exposureSettings.Multiplier; // 解析当前 Volume 中 Tonemapping 前曝光倍率。
            var preExposureState = PreExposureUtility.ResolveForFrame(request, asset);
            var residualPostExposureMultiplier = PreExposureUtility.ResolveResidualPostExposure(exposureSettings, preExposureState);

            var useColorAdjustments = PostProcessUtility.ShouldUseColorAdjustments(request, asset); // 判断当前 Volume 是否启用基础颜色调整。
            var colorGradingSettings = PostProcessUtility.ResolveColorGradingSettings(asset);
            var useVignette = PostProcessUtility.ShouldUseVignette(request, asset);
            var vignetteSettings = PostProcessUtility.ResolveVignetteSettings(asset);
            var rcasSettings = PostProcessUtility.ResolveRCASSettings(asset);
            var fxaaSettings = PostProcessUtility.ResolveFastApproximateAASettings(asset);
            var smaaSettings = PostProcessUtility.ResolveSubpixelMorphologicalAASettings(asset);

            var bloomSettings = PostProcessUtility.ResolveBloomSettings(asset); // 解析当前 Volume 中真正生效的 Bloom 参数。

            var bloomEnabled = PostProcessUtility.ShouldUseBloom(request, asset); // 判断当前 request 是否启用 Bloom。

            var bloomMipCount = PostProcessUtility.ResolveBloomMipCount(request, asset); // 解析当前 request 实际会使用的 Bloom mip 数。
            var bloomDebugView = PostProcessUtility.ResolveBloomDebugView(bloomSettings); // 合并 Volume Bloom Debug 和 Shading Debug 中的 Bloom 入口。
            var bloomDebugRequested = PostProcessUtility.IsBloomDebugRequested();
            var preserveBloomAlpha = PostProcessUtility.ShouldPreserveBloomAlpha(bloomSettings, bloomDebugView);
            var bloomAlphaReason = PostProcessUtility.ResolveBloomAlphaReason(bloomSettings, bloomDebugView);
            var bloomRenderTextureFormat = PostProcessUtility.ResolveBloomRenderTextureFormat(request != null ? request.Camera : null, bloomSettings, bloomDebugView);
            var bloomRenderTextureFormatReason = PostProcessUtility.ResolveBloomRenderTextureFormatReason(request != null ? request.Camera : null, bloomSettings, bloomDebugView);
            var bloomMipSizes = PostProcessUtility.FormatBloomMipSizes(request != null ? request.Camera : null, bloomMipCount);
            var bloomMipPixels = PostProcessUtility.CalculateBloomMipPixelCount(request != null ? request.Camera : null, bloomMipCount);
            var bloomStages = PostProcessUtility.FormatBloomStageDiagnostics(request != null ? request.Camera : null, bloomSettings, bloomMipCount);
            var bloomDebugTarget = PostProcessUtility.FormatBloomDebugTarget(request != null ? request.Camera : null, bloomDebugView, bloomMipCount);
            var bloomPrefilterPostExposure = PostProcessUtility.ResolveBloomPrefilterPostExposure(residualPostExposureMultiplier);
            var bloomPrefilterKnee = PostProcessUtility.ResolveBloomPrefilterKnee(bloomSettings);
            var bloomPrefilterSourceThreshold = PostProcessUtility.FormatBloomPrefilterSourceThreshold(bloomSettings, residualPostExposureMultiplier);
            var bloomPrefilterBypassThreshold = PostProcessUtility.ShouldBypassBloomPrefilterThreshold(bloomSettings);

            var temporalAA = request != null ? request.TemporalAA : null;
            var temporalAAEnabled = temporalAA != null && temporalAA.Enabled;
            var temporalAASettings = PostProcessUtility.ResolveTemporalAASettings(request, asset);
            var temporalAADisabledReason = BurtTemporalAAUtility.ResolveTemporalAADiagnosticDisabledReason(request, asset, renderOptions);
            var temporalAASource = PostProcessUtility.ResolveTemporalAASourceLabel(request);
            var temporalAAVolumeState = PostProcessUtility.ResolveTemporalAAVolumeStateLabel();
            var temporalHistory = BurtTemporalAAUtility.GetHistoryStatus(request != null ? request.Camera : null);
            var burtGITAAConfidence = BurtScreenSpaceGlobalIlluminationPassUtility.ShouldExposeScreenSpaceGlobalIlluminationToTemporalAA(request, asset, renderOptions) ? "BoundCurrentGIAlpha" : "Disabled";
            var finalBlitYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(request);
            const float temporalAADebugYFlip = 0f;

            builder.Append("  AssetEnabled=").Append(assetEnabled); // 写入后处理总开关。

            builder.Append(" NoOpCopy=").Append(noOpCopy); // 写入 No-op Copy 开关。

            builder.Append(" ShouldRunFramework=").Append(shouldRunFramework); // 写入当前 request 是否真正执行后处理链。

            builder.Append(" SuppressedByShadingDebug=").Append(suppressedByShadingDebug);

            builder.Append(" Tonemapping=").Append(tonemappingMode); // 写入 Volume 解析后的 Tonemapping 模式。

            builder.Append(" ExposureMode=").Append(exposureSettings.Mode);

            builder.Append(" EV100=").Append(exposureSettings.EV100.ToString("0.###"));

            builder.Append(" ISO=").Append(exposureSettings.ISO.ToString("0.###"));

            builder.Append(" Shutter=").Append(exposureSettings.ShutterTime.ToString("0.######"));

            builder.Append(" Aperture=").Append(exposureSettings.Aperture.ToString("0.###"));

            builder.Append(" ExposureCalibration=").Append(exposureSettings.Calibration.ToString("0.###"));

            builder.Append(" ExposureCompensationEV=").Append(exposureSettings.Compensation.ToString("0.###"));

            builder.Append(" PostExposureMul=").Append(postExposureMultiplier.ToString("0.###")); // 写入 EV 转换后的线性曝光倍率。
            builder.Append(" PreExposure=").Append(preExposureState.PreExposure.ToString("0.###"));
            builder.Append(" InvPreExposure=").Append(preExposureState.InvPreExposure.ToString("0.###"));
            builder.Append(" ResidualPostExposure=").Append(residualPostExposureMultiplier.ToString("0.###"));
            builder.Append(" PreExposureEnabled=").Append(preExposureState.Enabled);
            builder.Append(" PreExposureTAAInvalidationEV=").Append(PreExposureUtility.TemporalAAInvalidationEVThreshold.ToString("0.###"));

            builder.Append(" AutoAvgLuma=").Append(exposureSettings.AutoAverageLuminance.ToString("0.###"));
            builder.Append(" AutoAvgLogLum=").Append(exposureSettings.AutoAverageLogLuminance.ToString("0.###"));
            builder.Append(" AutoTargetEV100=").Append(exposureSettings.AutoTargetEV100.ToString("0.###"));
            builder.Append(" AutoMinMaxEV100=").Append(exposureSettings.AutoMinEV100.ToString("0.###")).Append('/').Append(exposureSettings.AutoMaxEV100.ToString("0.###"));
            builder.Append(" AutoMiddleGrey=").Append(exposureSettings.AutoMiddleGrey.ToString("0.###"));
            builder.Append(" AutoSpeedUpDown=").Append(exposureSettings.AutoSpeedUp.ToString("0.###")).Append('/').Append(exposureSettings.AutoSpeedDown.ToString("0.###"));
            builder.Append(" AutoLowHighPercent=").Append(exposureSettings.AutoLowPercent.ToString("0.###")).Append('/').Append(exposureSettings.AutoHighPercent.ToString("0.###"));
            builder.Append(" AutoHistogramMinMaxEV100=").Append(exposureSettings.AutoHistogramMinEV100.ToString("0.###")).Append('/').Append(exposureSettings.AutoHistogramMaxEV100.ToString("0.###"));
            builder.Append(" AutoSample=").Append(exposureSettings.AutoHasSample);
            builder.Append(" AutoSampleCount=").Append(exposureSettings.AutoSampleCount);
            builder.Append(" AutoSampleAgeFrames=").Append(exposureSettings.AutoSampleAgeFrames);
            builder.Append(" AutoSampleRejectedReason=").Append(exposureSettings.AutoSampleRejectedReason);
            builder.Append(" AutoReadbackPending=").Append(exposureSettings.AutoReadbackPending);
            builder.Append(" AutoReadbackAgeFrames=").Append(exposureSettings.AutoReadbackAgeFrames);

            builder.Append(" ColorAdjustments=").Append(useColorAdjustments); // 写入是否启用颜色调整。
            builder.Append(" ColorGrading=").Append(colorGradingSettings.Enabled);
            builder.Append(" ColorGradingWhiteBalance=").Append(colorGradingSettings.WhiteBalanceEnabled);
            builder.Append(" ColorGradingLUT=").Append(colorGradingSettings.HasLut);
            builder.Append(" ColorGradingLUTContribution=").Append(FormatFloat(colorGradingSettings.LutContribution));
            builder.Append(" ColorGradingIntensity=").Append(FormatFloat(colorGradingSettings.Intensity));
            builder.Append(" Vignette=").Append(useVignette);
            builder.Append(" VignetteIntensity=").Append(FormatFloat(vignetteSettings.Intensity));
            builder.Append(" VignetteEdgeWidth=").Append(FormatFloat(vignetteSettings.EdgeWidth));
            builder.Append(" VignetteEdgeSoftness=").Append(FormatFloat(vignetteSettings.EdgeSoftness));
            builder.Append(" VignetteFisheyeFovDeg=").Append(FormatFloat(vignetteSettings.FisheyeFovDeg));
            builder.Append(" VignetteFollowAspect=").Append(vignetteSettings.FollowAspect);
            builder.Append(" VignetteColor=").Append(FormatVector4(new Vector4(vignetteSettings.Color.r, vignetteSettings.Color.g, vignetteSettings.Color.b, vignetteSettings.Color.a)));
            builder.Append(" SMAA=").Append(smaaSettings.Enabled);
            builder.Append(" SMAAThreshold=").Append(FormatFloat(smaaSettings.Threshold));
            builder.Append(" SMAABlend=").Append(FormatFloat(smaaSettings.BlendStrength));
            builder.Append(" SMAAMaxSearchSteps=").Append(smaaSettings.MaxSearchSteps);
            builder.Append(" FXAA=").Append(fxaaSettings.Enabled);
            builder.Append(" FXAASubpixel=").Append(FormatFloat(fxaaSettings.Subpixel));
            builder.Append(" FXAAEdgeThreshold=").Append(FormatFloat(fxaaSettings.EdgeThreshold));
            builder.Append(" FXAAEdgeThresholdMin=").Append(FormatFloat(fxaaSettings.EdgeThresholdMin));
            builder.Append(" RCAS=").Append(rcasSettings.Enabled);
            builder.Append(" RCASSharpness=").Append(FormatFloat(rcasSettings.Sharpness));

            builder.Append(" BloomEnabled=").Append(bloomEnabled); // 写入是否启用 Bloom。

            builder.Append(" BloomMips=").Append(bloomMipCount); // 写入当前 request 实际使用的 Bloom mip 数。

            builder.Append(" BloomMipSizes=").Append(bloomMipSizes); // 写入 Bloom 临时 mip 链尺寸，便于排查上下颠倒、尺寸错配和质量档映射。

            builder.Append(" BloomMipPixels=").Append(bloomMipPixels); // 写入 Bloom mip 链总像素数，便于粗略判断临时 RT 成本。

            builder.Append(" BloomStages=").Append(bloomStages); // 写入每级 Bloom blur 实际 Filter、尺寸、半径、采样数和 tint。

            builder.Append(" BloomQuality=").Append(bloomSettings.Quality); // 写入 Bloom 质量档，便于确认 Q1-Q5 映射。

            builder.Append(" BloomMaxMips=").Append(bloomSettings.MaxMipCount); // 写入 Bloom mip 上限，便于排查 quality 与 maxIterations 的夹取结果。

            builder.Append(" BloomThreshold=").Append(bloomSettings.Threshold.ToString("0.###")); // 写入 Bloom 阈值。

            builder.Append(" BloomSoftKnee=").Append(bloomSettings.SoftKnee.ToString("0.###")); // 写入 Bloom soft knee，便于解释 prefilter 过渡宽度。

            builder.Append(" BloomPrefilterPostExposure=").Append(bloomPrefilterPostExposure.ToString("0.###")); // 写入 shader prefilter 阈值判断使用的曝光倍率。

            builder.Append(" BloomPrefilterKnee=").Append(bloomPrefilterKnee.ToString("0.###")); // 写入 shader 中 max(threshold * softKnee, 0.0001) 的结果。

            builder.Append(" BloomPrefilterSourceThreshold=").Append(bloomPrefilterSourceThreshold); // 写入换算到曝光前源亮度的大致阈值。

            builder.Append(" BloomPrefilterBypassThreshold=").Append(bloomPrefilterBypassThreshold); // 写入 threshold <= -1 时是否跳过阈值裁剪。

            builder.Append(" BloomPrefilterFireflyClamp=").Append(PostProcessUtility.BloomPrefilterFireflyClamp.ToString("0.###")); // 写入 prefilter 前的极亮点软夹取上限。

            builder.Append(" BloomIntensity=").Append(bloomSettings.Intensity.ToString("0.###")); // 写入 Bloom 强度。

            builder.Append(" BloomScatter=").Append(bloomSettings.Scatter.ToString("0.###")); // 写入 Bloom 散布。

            builder.Append(" BloomSizeScale=").Append(bloomSettings.SizeScale.ToString("0.###")); // 写入 Bloom 尺寸倍率。

            builder.Append(" BloomAlpha=").Append(bloomSettings.BloomAlphaChannel); // 写入 Bloom alpha 输出开关。

            builder.Append(" BloomAlphaRT=").Append(preserveBloomAlpha); // 写入 Bloom 临时 RT 是否保留 alpha 通道。

            builder.Append(" BloomAlphaReason=").Append(bloomAlphaReason); // 写入 Bloom alpha 被保留的来源。

            builder.Append(" BloomRTFormat=").Append(bloomRenderTextureFormat); // 写入 Bloom RT 实际会申请的 RenderTextureFormat。

            builder.Append(" BloomRTFormatReason=").Append(bloomRenderTextureFormatReason); // 写入 Bloom RT 格式选择或 fallback 原因。

            builder.Append(" BloomDebug=").Append(bloomDebugView); // 写入最终生效的 Bloom 调试视图，便于确认是否正在覆盖最终画面。

            builder.Append(" BloomDebugSource=").Append(bloomDebugRequested ? "ShadingDebug" : "Volume"); // 标明 Bloom debug 是从 Shading Debug 菜单还是 Volume 参数进入。

            builder.Append(" BloomDebugTarget=").Append(bloomDebugTarget); // 写入当前 Bloom debug 实际采样的 mip 和尺寸。

            builder.Append(" BloomDebugSuppressedByShadingDebug=").Append(bloomDebugView != BloomDebugView.Disabled && suppressedByShadingDebug); // Shading debug 优先级更高时，Bloom debug 不覆盖画面。

            builder.Append(" TAAEnabled=").Append(temporalAAEnabled);
            builder.Append(" TAAReason=").Append(temporalAADisabledReason);
            builder.Append(" TAASource=").Append(temporalAASource);
            builder.Append(" TAAVolume=").Append(temporalAAVolumeState);
            builder.Append(" TAAHistoryValid=").Append(temporalAAEnabled && temporalAA.HistoryValid);
            builder.Append(" TAAHistoryAllocated=").Append(temporalHistory.HasHistory);
            builder.Append(" TAAHistoryMatches=").Append(temporalHistory.DescriptorMatches);
            builder.Append(" TAADepthHistoryAllocated=").Append(temporalHistory.HasDepthHistory);
            builder.Append(" TAADepthHistoryMatches=").Append(temporalHistory.DepthDescriptorMatches);
            builder.Append(" TAAHistoryAge=").Append(temporalHistory.HistoryAge);
            builder.Append(" TAAFrame=").Append(temporalAA != null ? temporalAA.FrameIndex.ToString() : temporalHistory.FrameIndex.ToString());
            builder.Append(" TAAHistoryReason=").Append(temporalHistory.LastInvalidationReason);
            builder.Append(" TAAJitter=").Append(temporalAA != null ? temporalAA.JitterPixels.ToString("F3") : "<none>");
            builder.Append(" TAAJitterScale=").Append((temporalAA != null ? temporalAA.Settings.JitterScale : temporalAASettings.JitterScale).ToString("0.###"));
            builder.Append(" TAAConfiguredJitterScale=").Append(temporalAASettings.JitterScale.ToString("0.###"));
            builder.Append(" TAAViewJitterScale=").Append(request != null && request.Camera != null && request.Camera.cameraType == CameraType.SceneView ? "SceneView0.5" : "Default");
            builder.Append(" TAAUpscaleSharpness=").Append(temporalAASettings.Sharpness.ToString("0.###"));
            builder.Append(" TAAUntrustedMVScale=").Append(temporalAASettings.UntrustedMotionFeedbackScale.ToString("0.###"));
            builder.Append(" TAAMotionEdge=").Append(temporalAASettings.MotionEdgeResponsiveStrength.ToString("0.###"));
            builder.Append(" TAADepthEdge=").Append(temporalAASettings.DepthEdgeResponsiveStrength.ToString("0.###"));
            builder.Append(" TAAUpscaleFactor=").Append(temporalAASettings.UpscaleFactor.ToString("0.###"));
            builder.Append(" TAABurtGIConfidence=").Append(burtGITAAConfidence);
            builder.Append(" TAAVelocity=").Append(temporalAA != null ? temporalAA.VelocityMode.ToString() : BurtTemporalAAVelocityMode.Disabled.ToString());
            builder.Append(" TAAObjectMVPass=").Append(temporalAA != null && temporalAA.ObjectMotionVectorPassDrawn);
            builder.Append(" TAADebugMode=").Append(BurtTemporalAAUtility.IsTemporalAADebugMode(BurtShadingDebugSettings.Mode) ? BurtShadingDebugSettings.Mode.ToString() : "Disabled");
            builder.Append(" TAAFinalBlitYFlip=").Append(FormatFloat(finalBlitYFlip));
            builder.Append(" TAADebugYFlip=").Append(FormatFloat(temporalAADebugYFlip));
            builder.Append(" TAADebugCopy=").Append(BurtTemporalAAUtility.IsTemporalAADebugMode(BurtShadingDebugSettings.Mode) && temporalAAEnabled ? "TwoStagePostProcessColor" : "Disabled");
            builder.Append(" TAAUVSpace=XRenderFullscreenPlatformSampleUv;HistoryDepthVelocityFeedbackSameOrientation;FinalCameraColorTemporalAACopy;FinalBlitHandlesDisplayFlip;XRenderVelocityCurrentMinusPreviousHistoryUvMinusVelocity");
            builder.Append(" TAAFilter=").Append(temporalAAEnabled ? "Current3x3ProjectionJitterFilter" : "Disabled");
            builder.Append(" TAANote=").Append(temporalAAEnabled ? "XRenderTSRAccumulationParity;ResolveXRenderCompute;ComputeDilateDecimate;XRenderDepthPixelRadiusDeviceZError;StencilMaskComputeFallback;FragmentDebugFallback;ColorDepthHistoryOnly;VelocityCurrentMinusPrevious;ValidObjectVelocityOnly;ProjectionJitterTranslateMatrix;RestoreJitteredMatricesBeforeDraw;StaticVelocitySubtractCurrentJitter;ClipToPrevClipCameraRelative;XRenderPerAxisVelocityThreshold;ResolveKeeps3x3ProjectionJitterFilter;XRenderSingleRejectionBlendFactor;XRenderCatmullRom9TapCompute;SubmitBeforeProjectionRestore;FinalHistoryAvailabilityNoSurfaceGate;HistoryLayout30;ParallaxCoverageDepthGate;XRenderSigmaClamp15;XRenderTemporalContrastSDBoost;NoResolveSharpen;UIntPrevUseCountUAV;ScalarParallaxRejection;MaterialMotionVectorsPass;TAAExplicitGBuffer0And2Bind;GrassFoliageVertexAnimationMV;ObjectMotionInstancing;ObjectVelocityPerAxisThreshold;RealStencilSubElement;TAAStencilMaskBit8FromRealStencil;TAAStencilMaskRawVelocityLowResFallback;TAAStencilMaskRealStencilOR;XRenderResponsiveStencilBit16Only;XRenderResponsiveBlend025;XRenderMotionVectorStencilProperties;XRenderMotionVectorZTestEqual;XRenderObjectMotionOpaqueQueue;ObjectVelocityRequiresStencilBit8;TAAUObjectMotionLowRes;TAAUResolveUpscalePass;TAAUDebugClosure489_491;TAAStencilMaskDebug495;HistoryValidReason;XRenderPointCurrentLoad;XRenderFinalAlpha;XRenderFloat2DilatedVelocity;XRenderSeparateClosestDepth;ClosestDepthScalarRT;TAADebugTwoStageCopy" : "Disabled");

            builder.Append(" VolumeLayerMask=").Append(asset != null ? asset.PostProcessVolumeLayerMask.value.ToString() : "<none>"); // 写入 Volume 查询层，排查 Volume 不生效时很有用。

            builder.AppendLine(); // 结束后处理状态行。
        }

        private static void AppendDeferredState( // 写入 Deferred 相关资源和调试状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断 Preview 是否强制走 Forward。
            BurtRenderPipelineAsset asset, // 接收管线资产，用来读取 Deferred 开关。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收资源表，用来确认 GBuffer 是否注册。
            BurtRequestRenderOptions renderOptions) // Receives request render options for BurtGI/TAA exposure parity.
        {
            builder.AppendLine("Deferred State:"); // 单独成段输出，让 Deferred 分支是否生效一眼可见。

            var isDeferred = IsDeferredRequest(request, asset); // 判断当前 request 是否真的处于 Deferred 路径。
            var ssaoSettings = BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionSettings(request, asset);
            var ssShadowSettings = BurtScreenSpaceShadowPassUtility.ResolveScreenSpaceShadowSettings(request, asset);
            var ssaoHistory = BurtScreenSpaceAmbientOcclusionHistoryUtility.GetHistoryStatus(request != null ? request.Camera : null);
            var burtGISettings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationSettings(request, asset);
            var burtGIScreenProbeSettings = BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeSettings(request, asset);
            var burtGIHistory = BurtScreenSpaceGlobalIlluminationHistoryUtility.GetHistoryStatus(request, burtGISettings);
            var burtGIScreenProbeHistory = BurtScreenSpaceGlobalIlluminationScreenProbeHistoryUtility.GetHistoryStatus(request, burtGIScreenProbeSettings);
            var furBlurHistory = BurtFurBlurHistoryUtility.GetHistoryStatus(request != null ? request.Camera : null);
            var furBlurSettings = BurtFurBlurPassUtility.ResolveSettings();
            var temporalAA = request != null ? request.TemporalAA : null;
            var ssaoEnabled = isDeferred && ssaoSettings.Enabled;
            var ssShadowEnabled = isDeferred && ssShadowSettings.Enabled;
            var burtGIEnabled = isDeferred && burtGISettings.Enabled;
            var furBlurEnabled = isDeferred && BurtFurBlurPassUtility.ShouldUseFurBlur(request, asset);
            var furBlurThetaTemporalEnabled = furBlurEnabled && furBlurSettings.ThetaTemporal;
            var furBlurColorTemporalEnabled = furBlurEnabled && furBlurSettings.ColorTemporal;
            var furBlurTiledSetupEnabled = furBlurEnabled && BurtFurBlurPassUtility.ShouldUseTiledFurBlur(request, asset);
            var furBlurTiledBlurDrawEnabled = furBlurEnabled && BurtFurBlurPassUtility.ShouldUseTiledBlurDraw(request, asset);
            var furBlurSetupStage = furBlurTiledSetupEnabled ? "PropertyVelocityTiledSetupFillArgsIndirectDilate2" : "PropertyVelocityFullscreenDilate2";
            var furBlurPropertyStage = furBlurThetaTemporalEnabled ? "ThetaTAAStableProperty" : "CurrentProperty";
            var furBlurBlurStage = furBlurTiledBlurDrawEnabled ? "IndirectBlur" : "Blur";
            var furBlurResultStage = furBlurColorTemporalEnabled ? "ResultTAAComposite" : "CompositeCurrent";
            var furBlurPath = furBlurEnabled ? (furBlurSetupStage + furBlurPropertyStage + furBlurBlurStage + furBlurResultStage) : "Disabled";
            var furBlurTemporalLabel = "FurVelocityFallbackCameraDepthReprojection;ThetaTAA=" +
                (furBlurThetaTemporalEnabled ? "On" : "Off") +
                ";ResultTAA=" +
                (furBlurColorTemporalEnabled ? "On" : "Off");
            var ssaoDebugRequested = BurtScreenSpaceAmbientOcclusionPassUtility.IsScreenSpaceAmbientOcclusionDebugMode(BurtShadingDebugSettings.Mode);
            var ssaoDebugPassRequested = isDeferred && BurtScreenSpaceAmbientOcclusionPassUtility.ShouldUseScreenSpaceAmbientOcclusionDebugView(request, asset);
            var ssShadowDebugRequested = BurtScreenSpaceShadowPassUtility.IsScreenSpaceShadowDebugMode(BurtShadingDebugSettings.Mode);
            var ssShadowDebugPassRequested = isDeferred && BurtScreenSpaceShadowPassUtility.ShouldUseScreenSpaceShadowDebugView(request, asset);
            var burtGIDebugPassRequested = isDeferred && BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationDebugView(request, asset);
            var burtGITemporalDiagnosticsRequested = isDeferred && BurtScreenSpaceGlobalIlluminationPassUtility.ShouldUseScreenSpaceGlobalIlluminationTemporalDiagnostics(request, asset);

            builder.Append("  Enabled=").Append(isDeferred); // 写入 Deferred 是否启用。

            builder.Append(" ForwardOnlyFallback=").Append(asset != null && asset.EnableDeferredForwardOpaqueFallback); // 写入 Deferred 后 ForwardOnly 兜底开关。

            builder.Append(" GBuffer0Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer0Name)); // 写入 GBuffer0 是否已注册。

            builder.Append(" GBuffer1Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer1Name)); // 写入 GBuffer1 是否已注册。

            builder.Append(" GBuffer2Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer2Name)); // 写入 GBuffer2 是否已注册。

            builder.Append(" GBuffer3Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer3Name));

            builder.Append(" GBuffer4Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer4Name));

            builder.Append(" GBuffer5Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer5Name));

            builder.Append(" GBufferObjectIndexRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBufferObjectIndexName));

            builder.Append(" DeferredLightingDepthRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.DeferredLightingDepthName));

            builder.Append(" GBufferLayoutContract=FixedSlotPC_NormalBasePropertyCustomEmissionHighCustom_ObjectIndex_StencilSMHighBits");

            builder.Append(" ClusterLightCountRegistered=").Append(resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName));
            builder.Append(" ClusterLightListRegistered=").Append(resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName));
            builder.Append(" ClusterLightOffsetRegistered=").Append(resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName));

            builder.Append(" SSAORawRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionRawName));

            builder.Append(" SSAOFinalRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceAmbientOcclusionName));

            builder.Append(" SSShadowRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceShadowName));

            builder.Append(" SSAODebugMode=").Append(BurtScreenSpaceAmbientOcclusionPassUtility.ResolveScreenSpaceAmbientOcclusionDebugModeLabel());

            builder.Append(" BurtGIRawRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationRawName));

            builder.Append(" BurtGIFinalRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceGlobalIlluminationName));

            builder.Append(" BurtGIBackfaceDiffuseRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIBackfaceDiffuseIndirectName));

            builder.Append(" BurtGIRoughSpecularRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRoughSpecularIndirectName));

            builder.Append(" BurtGITranslucencyVolume0Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume0Name));

            builder.Append(" BurtGITranslucencyVolume1Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolume1Name));

            builder.Append(" BurtGITranslucencyVolumeFilter0Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter0Name));

            builder.Append(" BurtGITranslucencyVolumeFilter1Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeFilter1Name));
            builder.Append(" BurtGITranslucencyVolumeTraceRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceRadianceName));
            builder.Append(" BurtGITranslucencyVolumeTraceFilteredRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceFilteredRadianceName));
            builder.Append(" BurtGITranslucencyVolumeTraceHitDistanceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITranslucencyVolumeTraceHitDistanceName));

            builder.Append(" BurtGITemporalDiagnosticsRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGITemporalDiagnosticsName));

            builder.Append(" BurtGIScreenProbeIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIndirectArgsBufferName));
            builder.Append(" BurtGIScreenProbeIntegrateTileIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileIndirectArgsBufferName));
            builder.Append(" BurtGIScreenProbeIntegrateTileDataDiffuseBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataDiffuseBufferName));
            builder.Append(" BurtGIScreenProbeIntegrateTileDataAllBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataAllBufferName));
            builder.Append(" BurtGIScreenProbeIntegrateTileClassificationRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileClassificationName));
            builder.Append(" BurtGIScreenProbeTraceCompactTexelCountBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelCountBufferName));
            builder.Append(" BurtGIScreenProbeTraceCompactTexelDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelDataBufferName));
            builder.Append(" BurtGIScreenProbeTraceCompactIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactIndirectArgsBufferName));
            builder.Append(" BurtGIScreenProbeTraceCompactThreadCountXBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactThreadCountXBufferName));

            builder.Append(" BurtGIScreenProbeScreenDepthRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeScreenDepthName));

            builder.Append(" BurtGIScreenProbeWorldNormalRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldNormalName));

            builder.Append(" BurtGIScreenProbeWorldPositionRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeWorldPositionName));

            builder.Append(" BurtGIScreenProbeAdaptiveProbeHeaderRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeHeaderName));

            builder.Append(" BurtGIScreenProbeAdaptiveProbeIndicesRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeIndicesName));

            builder.Append(" BurtGIScreenProbeAdaptiveProbeNumBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeNumBufferName));

            builder.Append(" BurtGIScreenProbeAdaptiveProbeDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeDataBufferName));

            builder.Append(" BurtGIScreenProbeRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceName));

            builder.Append(" BurtGIScreenProbeIrradianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIrradianceName));

            builder.Append(" BurtGIScreenProbeConfidenceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeConfidenceName));

            builder.Append(" BurtGIScreenProbeHitDistanceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeHitDistanceName));

            builder.Append(" BurtGIScreenProbeBentNormalRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeBentNormalName));

            builder.Append(" BurtGIScreenProbeTraceRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceRadianceName));

            builder.Append(" BurtGIScreenProbeTraceHitRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceHitName));

            builder.Append(" BurtGIScreenProbeTemporalRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalRadianceName));

            builder.Append(" BurtGIScreenProbeTemporalIrradianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalIrradianceName));

            builder.Append(" BurtGIScreenProbeTemporalConfidenceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTemporalConfidenceName));

            builder.Append(" BurtGIScreenProbeFilteredRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredRadianceName));

            builder.Append(" BurtGIScreenProbeFilteredIrradianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredIrradianceName));

            builder.Append(" BurtGIScreenProbeFilteredConfidenceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFilteredConfidenceName));

            builder.Append(" BurtGIScreenProbeFixupRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupRadianceName));

            builder.Append(" BurtGIScreenProbeFixupIrradianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupIrradianceName));

            builder.Append(" BurtGIScreenProbeFixupConfidenceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeFixupConfidenceName));

            builder.Append(" BurtGIScreenProbeMipRadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipRadianceName));

            builder.Append(" BurtGIScreenProbeMipIrradianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipIrradianceName));

            builder.Append(" BurtGIScreenProbeMipConfidenceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMipConfidenceName));

            builder.Append(" BurtGIScreenProbeMip2RadianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2RadianceName));

            builder.Append(" BurtGIScreenProbeMip2IrradianceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2IrradianceName));

            builder.Append(" BurtGIScreenProbeMip2ConfidenceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeMip2ConfidenceName));

            builder.Append(" BurtGIScreenProbeRadianceSHAmbientRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHAmbientName));

            builder.Append(" BurtGIScreenProbeRadianceSHDirectionalRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeRadianceSHDirectionalName));

            AppendBurtGIRadianceCacheClipMapResourceState(builder, resourceRegistry);

            builder.Append(" BurtGIDebugMode=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationDebugModeLabel());

            builder.Append(" HiZDepthRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.HiZDepthName));

            builder.Append(" SSRColorRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName));

            builder.Append(" SSRDenoisedColorRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName));

            builder.Append(" SSRTemporalColorRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName));

            builder.Append(" SSSSourceRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName));
            builder.Append(" SSSSetupRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName));
            builder.Append(" SSSProfileIDAndTypeRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName));
            builder.Append(" SSSMaskRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName));
            builder.Append(" SSSCoarseMaskRegistered=False");
            builder.Append(" SSSTempRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName));
            builder.Append(" SSSBlurRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName));
            builder.Append(" SSSCombineRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName));
            builder.Append(" FurBlurPropertyRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FurBlurPropertyName));
            builder.Append(" FurBlurPropertyTempRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FurBlurPropertyTempName));
            builder.Append(" FurBlurColorRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FurBlurColorName));
            builder.Append(" FurBlurTemporalRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FurBlurTemporalName));
            builder.Append(" FurBlurVelocityRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FurBlurVelocityName));
            builder.Append(" FurBlurArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FurBlurArgsBufferName));
            builder.Append(" FurBlurTileDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName));

            if (isDeferred)
            {
                var ssrSettings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(request, asset);
                var ssrSuppressedByShadingDebug = BurtScreenSpaceReflectionPassUtility.IsScreenSpaceReflectionSuppressedByShadingDebug();
                var sssSuppressedByShadingDebug = BurtScreenSpaceSubsurfacePassUtility.IsScreenSpaceSubsurfaceSuppressedByShadingDebug();
                var ssrHistory = BurtScreenSpaceReflectionHistoryUtility.GetHistoryStatus(request != null ? request.Camera : null);
                var screenSpaceSubsurfaceEnabled = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(request, asset);
                var screenSpaceSubsurfaceUsesBurley = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(request, asset);
                var screenSpaceSubsurfaceUsesSeparable = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceSeparable(request, asset);
                var screenSpaceSubsurfaceHasBurleyMaterial = BurtScreenSpaceSubsurfacePassUtility.HasScreenSpaceSubsurfaceBurleyMaterialCandidate(request, asset);
                var screenSpaceSubsurfaceHasSeparableMaterial = BurtScreenSpaceSubsurfacePassUtility.HasScreenSpaceSubsurfaceSeparableMaterialCandidate(request, asset);
                var screenSpaceSubsurfaceDebugSampling = BurtShadingDebugSettings.IsDebugging &&
                    BurtScreenSpaceSubsurfacePassUtility.IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugSettings.Mode);
                var screenSpaceSubsurfaceTemporalSamplingReady = temporalAA != null &&
                    temporalAA.Enabled &&
                    temporalAA.HistoryValid &&
                    temporalAA.VelocityMode != BurtTemporalAAVelocityMode.Disabled;
                var screenSpaceSubsurfaceHistory = BurtScreenSpaceSubsurfaceHistoryUtility.GetHistoryStatus(request);
                var shouldUseHiZDepth = BurtHiZDepthPassUtility.ShouldUseHiZDepth(request, asset);
                var screenSpaceSubsurfaceUsesStencilTexture = screenSpaceSubsurfaceEnabled && BurtScreenSpaceSubsurfacePassUtility.ShouldUseStencilTexture(request);
                var screenSpaceSubsurfaceUsesMaskTexture = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(request, asset);
                builder.Append(" HiZNeeded=").Append(shouldUseHiZDepth);

                if (shouldUseHiZDepth)
                {
                    var hiZDescriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(request != null ? request.Camera : null);
                    builder.Append(" HiZMips=").Append(BurtRenderTargetDescriptorUtility.CalculateMipCount(hiZDescriptor.width, hiZDescriptor.height));
                    builder.Append(" HiZMode=ClosestRawDepth");
                }

                builder.Append(" HiZDebugView=").Append(asset != null && asset.EnableHiZDebugView);
                builder.Append(" HiZDebugMip=").Append(asset != null ? asset.HiZDebugMip.ToString() : "<none>");
                builder.Append(" SSSEnabled=").Append(screenSpaceSubsurfaceEnabled);
                builder.Append(" SSSSuppressedByShadingDebug=").Append(sssSuppressedByShadingDebug);
                builder.Append(" SSSMaterialGate=").Append(BurtScreenSpaceSubsurfacePassUtility.ResolveScreenSpaceSubsurfaceMaterialGateDebugLabel(request, asset));
                builder.Append(" SSSAlgorithmMode=").Append(BurtScreenSpaceSubsurfacePassUtility.ResolveScreenSpaceSubsurfaceAlgorithmModeLabel(request, asset));
                builder.Append(" SSSResourceMode=").Append(ResolveScreenSpaceSubsurfaceResourceMode(screenSpaceSubsurfaceEnabled, screenSpaceSubsurfaceUsesBurley, screenSpaceSubsurfaceUsesSeparable));
                builder.Append(" SSSPassExpected=").Append(screenSpaceSubsurfaceEnabled);
                builder.Append(" SSSPath=").Append(ResolveScreenSpaceSubsurfacePath(screenSpaceSubsurfaceEnabled, screenSpaceSubsurfaceUsesMaskTexture, screenSpaceSubsurfaceUsesBurley, screenSpaceSubsurfaceUsesSeparable));
                builder.Append(" SSSKernel=").Append(screenSpaceSubsurfaceEnabled ? "MaterialSelectable4SSeparable5SBurley+ProfileLut66IndirectBurleyAdaptiveHistory" : "Disabled");
                builder.Append(" SSS4SResources=").Append(screenSpaceSubsurfaceUsesSeparable ? (screenSpaceSubsurfaceHasSeparableMaterial ? "SetupTempBlurCombine" : "SetupTempBlurCombine(PixelGate)") : "Disabled");
                builder.Append(" SSS4SMaterialCandidate=").Append(screenSpaceSubsurfaceHasSeparableMaterial);
                builder.Append(" SSS5SResources=").Append(screenSpaceSubsurfaceUsesBurley ? (screenSpaceSubsurfaceHasBurleyMaterial ? "VelocityHistoryBurleyArgsGroups" : "VelocityHistoryBurleyArgsGroups(PixelGate)") : "Disabled");
                builder.Append(" SSS5SMaterialCandidate=").Append(screenSpaceSubsurfaceHasBurleyMaterial);
                builder.Append(" SSSProfileLut=").Append(screenSpaceSubsurfaceEnabled ? "Enabled" : "Disabled");
                builder.Append(" SSSSetupPassExpected=").Append(screenSpaceSubsurfaceEnabled);
                builder.Append(" SSSCoarseMaskPassExpected=False");
                builder.Append(" SSSCoarseMaskSource=").Append(screenSpaceSubsurfaceEnabled ? "SetupCoarse8x8" : "Disabled");
                builder.Append(" SSSCombinePassExpected=").Append(screenSpaceSubsurfaceEnabled);
                builder.Append(" SSSIndirect=").Append(screenSpaceSubsurfaceEnabled ? "ProfileSHWrapped" : "Disabled");
                builder.Append(" SSSDebugMode=").Append(BurtScreenSpaceSubsurfacePassUtility.IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugSettings.Mode) ? BurtShadingDebugSettings.Mode.ToString() : "Disabled");
                builder.Append(" SSSDebugShaderMode=").Append(BurtScreenSpaceSubsurfacePassUtility.ResolveScreenSpaceSubsurfaceShaderDebugMode());
                builder.Append(" SSSSplitDebug=").Append(screenSpaceSubsurfaceEnabled ? "Setup+Mask+Blur+Combine+ProfileTintRaw+ProfileTintedFinal+BlurAlpha+BlurRadius+BlurDelta+SeparableValidity+SeparableIO+SeparableChain+Algorithm+History" : "Disabled");
                builder.Append(" SSSDiffuseSpecularSplit=").Append(screenSpaceSubsurfaceEnabled);
                builder.Append(" SSSDiffuseLuminanceSource=").Append(screenSpaceSubsurfaceEnabled ? "DeferredSubsurfaceAlphaLightingSpaceSpecularGuard" : "Disabled");
                builder.Append(" SSSProfileIDSource=").Append(screenSpaceSubsurfaceEnabled ? "ScreenSpaceSubsurfaceBaseColorAlpha" : "Disabled");
                builder.Append(" SSSStencilGated=").Append(screenSpaceSubsurfaceEnabled ? (screenSpaceSubsurfaceUsesStencilTexture ? "DepthStencilTexture+ProfileMask" : "MaskTexture+GBufferFallback") : "Disabled");
                builder.Append(" SSSStencilTexture=").Append(screenSpaceSubsurfaceUsesStencilTexture);
                builder.Append(" SSSMaskFallback=").Append(screenSpaceSubsurfaceUsesMaskTexture ? (screenSpaceSubsurfaceUsesStencilTexture ? "DebugMaskTexture" : "GBuffer2StrengthProfileMask") : "Disabled");
                builder.Append(" SSSHistoryVelocity=").Append(screenSpaceSubsurfaceUsesBurley ? "RawSceneVelocity" : "Disabled");
                builder.Append(" SSSVelocityDilation=Removed");
                builder.Append(" SSSBurleySampling=").Append(screenSpaceSubsurfaceUsesBurley ? (screenSpaceSubsurfaceDebugSampling ? "StableDebugNoDepthNoiseMin64" : (screenSpaceSubsurfaceTemporalSamplingReady && screenSpaceSubsurfaceHistory.HasHistory ? "TemporalAnimatedAdaptive48To64" : "StableNoTemporalNoDepthNoiseMin64")) : "Disabled");
                builder.Append(" SSSHistoryValid=").Append(screenSpaceSubsurfaceUsesBurley && screenSpaceSubsurfaceHistory.HasHistory);
                builder.Append(" SSSHistoryAge=").Append(screenSpaceSubsurfaceUsesBurley ? screenSpaceSubsurfaceHistory.HistoryAge : 0);
                builder.Append(" SSSHistoryReason=").Append(screenSpaceSubsurfaceUsesBurley ? screenSpaceSubsurfaceHistory.Reason : "Disabled");
                builder.Append(" SSSTemporalVelocityMode=").Append(screenSpaceSubsurfaceUsesBurley && temporalAA != null ? temporalAA.VelocityMode.ToString() : BurtTemporalAAVelocityMode.Disabled.ToString());
                builder.Append(" SSSTemporalHistoryValid=").Append(screenSpaceSubsurfaceUsesBurley && temporalAA != null && temporalAA.HistoryValid);
                builder.Append(" SSSObjectMVPass=").Append(screenSpaceSubsurfaceUsesBurley && temporalAA != null && temporalAA.ObjectMotionVectorPassDrawn);
                if (asset != null)
                {
                    var sssProfile = asset.ScreenSpaceSubsurfaceProfileSettings;
                    builder.Append(" SSSProfile=").Append(sssProfile.ProfileName);
                    builder.Append(" SSSUsesProfile=").Append(sssProfile.UsesProfile);
                    var sssProfilePalette = asset.ScreenSpaceSubsurfaceProfilePalette;
                    builder.Append(" SSSProfileCount=").Append(sssProfilePalette.Count);
                    builder.Append(" SSSProfiles=").Append(FormatSubsurfaceProfiles(sssProfilePalette));
                    builder.Append(" SSSSurfaceAlbedo=").Append(FormatVector4(sssProfile.SurfaceAlbedoVector));
                    builder.Append(" SSSMeanFreePath=").Append(FormatVector4(sssProfile.MeanFreePathVector));
                    var sssEffectiveDiffuseMeanFreePath = BurtSubsurfaceLutUtility.GetEffectiveDiffuseMeanFreePathForLut(sssProfile);
                    builder.Append(" SSSEffectiveDMFP=").Append(FormatVector3(
                        sssEffectiveDiffuseMeanFreePath.x,
                        sssEffectiveDiffuseMeanFreePath.y,
                        sssEffectiveDiffuseMeanFreePath.z));
                    builder.Append(" SSSDualSpecular=").Append(FormatVector4(sssProfile.DualSpecularVector));
                    builder.Append(" SSSTint=").Append(FormatVector4(sssProfile.TintVector));
                    builder.Append(" SSSBoundaryColorBleed=").Append(FormatVector4(sssProfile.BoundaryColorBleedVector));
                    builder.Append(" SSSRadiusPixels=").Append(FormatFloat(sssProfile.RadiusPixels));
                    builder.Append(" SSSDepthSigma=").Append(FormatFloat(sssProfile.DepthSigma));
                    builder.Append(" SSSNormalSigma=").Append(FormatFloat(sssProfile.NormalSigma));
                    builder.Append(" SSSBlend=").Append(FormatFloat(sssProfile.Blend));
                    builder.Append(" SSSDistanceScale=").Append(FormatFloat(sssProfile.DistanceScale));
                    builder.Append(" SSSBoundaryBleed=").Append(FormatFloat(sssProfile.BoundaryBleed));
                    builder.Append(" SSSTintStrength=").Append(FormatFloat(sssProfile.TintStrength));
                    builder.Append(" SSSMinStrength=").Append(FormatFloat(sssProfile.MinStrength));
                }
                else
                {
                    builder.Append(" SSSProfile=<none>");
                    builder.Append(" SSSUsesProfile=False");
                    builder.Append(" SSSProfileCount=0");
                    builder.Append(" SSSProfiles=<none>");
                    builder.Append(" SSSSurfaceAlbedo=<none>");
                    builder.Append(" SSSMeanFreePath=<none>");
                    builder.Append(" SSSEffectiveDMFP=<none>");
                    builder.Append(" SSSDualSpecular=<none>");
                    builder.Append(" SSSTint=<none>");
                    builder.Append(" SSSBoundaryColorBleed=<none>");
                    builder.Append(" SSSRadiusPixels=<none>");
                    builder.Append(" SSSDepthSigma=<none>");
                    builder.Append(" SSSNormalSigma=<none>");
                    builder.Append(" SSSBlend=<none>");
                    builder.Append(" SSSDistanceScale=<none>");
                    builder.Append(" SSSBoundaryBleed=<none>");
                    builder.Append(" SSSTintStrength=<none>");
                    builder.Append(" SSSMinStrength=<none>");
                }
                builder.Append(" SSAOEnabled=").Append(ssaoSettings.Enabled);
                builder.Append(" SSAODebugRequested=").Append(ssaoDebugRequested);
                builder.Append(" SSAOTracePassExpected=").Append(ssaoEnabled);
                builder.Append(" SSAOBlurPassExpected=").Append(ssaoEnabled);
                builder.Append(" SSAODebugPassRequested=").Append(ssaoDebugPassRequested);
                builder.Append(" SSAOOutputTarget=ScreenSpaceAmbientOcclusion");
                builder.Append(" SSAOOutputSemantic=FinalVisibilityWithPowerIntensityTemporalOptional");
                builder.Append(" SSAORawTarget=ScreenSpaceAmbientOcclusionRaw");
                builder.Append(" SSAORawSemantic=PreCurveVisibility");
                builder.Append(" SSAOQuality=").Append(ssaoSettings.Quality);
                builder.Append(" SSAOAlgorithm=").Append(ssaoSettings.Algorithm);
                builder.Append(" SSAOPresetProfile=").Append(ssaoSettings.Algorithm).Append(".").Append(ssaoSettings.Quality);
                builder.Append(" SSAOGTAOStrength=").Append(FormatFloat(ssaoSettings.GTAOStrength));
                builder.Append(" SSAOHBAOStrength=").Append(FormatFloat(ssaoSettings.HBAOStrength));
                builder.Append(" SSAORadius=").Append(FormatFloat(ssaoSettings.Radius));
                builder.Append(" SSAOIntensity=").Append(FormatFloat(ssaoSettings.Intensity));
                builder.Append(" SSAOSamples=").Append(ssaoSettings.SampleCount);
                builder.Append(" SSAODirections=").Append(ssaoSettings.DirectionCount);
                builder.Append(" SSAOBias=").Append(FormatFloat(ssaoSettings.Bias));
                builder.Append(" SSAOPower=").Append(FormatFloat(ssaoSettings.Power));
                builder.Append(" SSAOHalfResolution=").Append(ssaoSettings.HalfResolution);
                builder.Append(" SSAOBlur=").Append(ssaoSettings.Blur);
                builder.Append(" SSAOSpatialDenoise=").Append(ssaoSettings.SpatialDenoise);
                builder.Append(" SSAOBlurSharpness=").Append(FormatFloat(ssaoSettings.BlurSharpness));
                builder.Append(" SSAOHorizonSearch=").Append(ssaoSettings.HorizonSearch);
                builder.Append(" SSAOThickness=").Append(FormatFloat(ssaoSettings.Thickness));
                builder.Append(" SSAOFadeRadius=").Append(FormatFloat(ssaoSettings.FadeRadius));
                builder.Append(" SSAOFadeDistance=").Append(FormatFloat(ssaoSettings.FadeDistance));
                builder.Append(" SSAOTemporal=").Append(ssaoSettings.TemporalAccumulation);
                builder.Append(" SSAOTemporalFeedback=").Append(FormatFloat(ssaoSettings.TemporalFeedback));
                builder.Append(" SSAOTemporalDepthRejection=").Append(FormatFloat(ssaoSettings.TemporalDepthRejection));
                builder.Append(" SSAOTemporalClamp=").Append(FormatFloat(ssaoSettings.TemporalClamp));
                builder.Append(" SSAOHistoryValid=").Append(ssaoHistory.HasHistory);
                builder.Append(" SSAOHistoryAllocated=").Append(ssaoHistory.HasHistory || ssaoHistory.HasDepthHistory);
                builder.Append(" SSAOHistoryMatches=").Append(ssaoHistory.DescriptorMatches);
                builder.Append(" SSAODepthHistoryAllocated=").Append(ssaoHistory.HasDepthHistory);
                builder.Append(" SSAODepthHistoryMatches=").Append(ssaoHistory.DepthDescriptorMatches);
                builder.Append(" SSAOHistoryAge=").Append(ssaoHistory.HistoryAge);
                builder.Append(" SSAOFrame=").Append(ssaoHistory.FrameIndex);
                builder.Append(" SSAOFirstValidFrame=").Append(ssaoHistory.FirstValidFrameIndex);
                builder.Append(" SSAOLastInvalidationFrame=").Append(ssaoHistory.LastInvalidationFrameIndex);
                builder.Append(" SSAOHistoryReason=").Append(ssaoHistory.LastInvalidationReason);
                builder.Append(" SSShadowEnabled=").Append(ssShadowSettings.Enabled);
                builder.Append(" SSShadowDebugRequested=").Append(ssShadowDebugRequested);
                builder.Append(" SSShadowDebugMode=").Append(BurtScreenSpaceShadowPassUtility.ResolveScreenSpaceShadowDebugModeLabel());
                builder.Append(" SSShadowTracePassExpected=").Append(ssShadowEnabled);
                builder.Append(" SSShadowDebugPassRequested=").Append(ssShadowDebugPassRequested);
                builder.Append(" SSShadowOutputTarget=ScreenSpaceShadow");
                builder.Append(" SSShadowOutputSemantic=MainLightScreenSpaceVisibilityDeferredWithFoliageWeight");
                builder.Append(" SSShadowSamples=").Append(ssShadowSettings.SampleCount);
                builder.Append(" SSShadowDownsampleFactor=").Append(ssShadowSettings.DownsampleFactor);
                builder.Append(" SSShadowHalfResolution=").Append(ssShadowSettings.HalfResolution);
                builder.Append(" SSShadowQuarterResolution=").Append(ssShadowSettings.QuarterResolution);
                builder.Append(" SSShadowMaxDistance=").Append(FormatFloat(ssShadowSettings.MaxDistance));
                builder.Append(" SSShadowDepthOffset=").Append(FormatFloat(ssShadowSettings.DepthOffset));
                builder.Append(" SSShadowThickness=").Append(FormatFloat(ssShadowSettings.Thickness));
                builder.Append(" SSShadowBilinearThreshold=").Append(FormatFloat(ssShadowSettings.BilinearThreshold));
                builder.Append(" SSShadowBilinearSamplingOffset=").Append(ssShadowSettings.BilinearSamplingOffset);
                builder.Append(" SSShadowIntensity=").Append(FormatFloat(ssShadowSettings.Intensity));
                builder.Append(" SSShadowGrassContrast=").Append(FormatFloat(ssShadowSettings.GrassContrast));
                builder.Append(" SSShadowDetailContrast=").Append(FormatFloat(ssShadowSettings.DetailContrast));
                builder.Append(" SSShadowFoliageContrast=").Append(FormatFloat(ssShadowSettings.FoliageContrast));
                builder.Append(" SSShadowCharacterContrast=").Append(FormatFloat(ssShadowSettings.CharacterContrast));
                builder.Append(" SSShadowFadeDistance=").Append(FormatFloat(ssShadowSettings.FadeDistance));
                builder.Append(" SSShadowFadeRadius=").Append(FormatFloat(ssShadowSettings.FadeRadius));
                builder.Append(" BurtGIEnabled=").Append(burtGISettings.Enabled);
                builder.Append(" BurtGIRuntimeStatus=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationRuntimeStatusLabel(request));
                builder.Append(" BurtGISuppressedByShadingDebug=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.IsScreenSpaceGlobalIlluminationSuppressedByShadingDebug());
                builder.Append(" BurtGIDebugPassRequested=").Append(burtGIDebugPassRequested);
                builder.Append(" BurtGIShaderStatus=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationShaderStatusLabel());
                builder.Append(" BurtGIRadianceCacheComputeStatus=").Append(BurtScreenSpaceGlobalIlluminationRadianceCacheClipMapPass.ResolveComputeResourceStatusLabel());
                builder.Append(" BurtGICopyTexture3DStatus=").Append(BurtScreenSpaceGlobalIlluminationPass.ResolveCopyTexture3DResourceStatusLabel());
                builder.Append(" BurtGITranslucencyVolumeComputeStatus=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveTranslucencyVolumeComputeStatusLabel());
                builder.Append(" BurtGITranslucencyVolumeRuntimeStatus=").Append(BurtGITranslucencyVolumeRuntimeState.ResolveStatusLabel());
                builder.Append(" BurtGITraceMode=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationTraceModeLabel(burtGISettings));
                builder.Append(" BurtGIScreenProbeTraceVoxelMaxSteps=").Append(burtGIScreenProbeSettings.TraceVoxelMaxTraceSteps);
                builder.Append(" BurtGIScreenProbeTraceVoxelStepFactor=").Append(FormatFloat(burtGIScreenProbeSettings.TraceVoxelStepFactor));
                builder.Append(" BurtGIPipeline=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationPipelineStageLabel(request, asset, renderOptions, burtGISettings));
                builder.Append(" BurtGIXGIParity=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationXGIParityLabel(burtGISettings));
                builder.Append(" BurtGIDebugChannels=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationDebugChannelLabel());
                builder.Append(" BurtGIInspect=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationInspectLabel());
                builder.Append(" BurtGIPlacement=BeforeDeferredLighting");
                builder.Append(" BurtGIComposite=ApplyIndirectDiffuseBackfaceRoughSpecular");
                builder.Append(" BurtGIColorBleed=NearFieldDiffuseGather24EdgeGuarded+StrongDiffuseAlbedoTint1.0+ChromaBoost1.65+HitEnergyPow0.35");
                builder.Append(" BurtGIOutputTarget=ScreenSpaceGlobalIllumination");
                builder.Append(" BurtGIRawTarget=ScreenSpaceGlobalIlluminationRaw");
                builder.Append(" BurtGIBackfaceDiffuseTarget=BurtGIBackfaceDiffuseIndirect");
                builder.Append(" BurtGIRoughSpecularTarget=BurtGIRoughSpecularIndirect");
                builder.Append(" BurtGITranslucencyVolumeTargets=BurtGITranslucencyVolumeFilter0,BurtGITranslucencyVolumeFilter1");
                builder.Append(" BurtGITranslucencyVolumeSourceTargets=BurtGITranslucencyVolume0,BurtGITranslucencyVolume1");
                builder.Append(" BurtGITranslucencyVolumeTraceTargets=BurtGITranslucencyVolumeTraceRadiance,BurtGITranslucencyVolumeTraceFilteredRadiance,BurtGITranslucencyVolumeTraceHitDistance");
                builder.Append(" BurtGITranslucencyVolumeTraceMode=ScreenGIBackfaceConeFallbackVoxelOctMultiDirectionLite+TraceAtlasXYZFilter+FilteredTraceAtlasIntegrate");
                builder.Append(" BurtGITranslucencyVolumeIntegrateMode=FilteredTraceAtlasSH2InputOverwritesVolumeBeforeVolumeSpatialFilter");
                builder.Append(" BurtGITranslucencyVolumeTraceHitDistanceEncoding=HitDistanceOrMaxTraceDistanceOnMiss");
                builder.Append(" BurtGITranslucencyVolumeApplyUV=XRenderWorldClipNonJitteredVP");
                builder.Append(" BurtGITranslucencyVolumeTraceOctahedronResolution=").Append(burtGIScreenProbeSettings.TranslucencyVolumeTracingOctahedronResolution);
                builder.Append(" BurtGITranslucencyVolumeGridPixelSize=").Append(burtGIScreenProbeSettings.TranslucencyVolumeGridPixelSize);
                builder.Append(" BurtGITranslucencyVolumeEndDistance=").Append(FormatFloat(burtGIScreenProbeSettings.TranslucencyVolumeEndDistanceFromCamera));
                builder.Append(" BurtGITranslucencyVolumeGridDistributionZ=").Append(FormatFloat(burtGIScreenProbeSettings.TranslucencyVolumeGridDistributionZScale));
                builder.Append(" BurtGITranslucencyVolumeTemporal=").Append(burtGIScreenProbeSettings.TranslucencyVolumeUseTemporalReprojection);
                builder.Append(" BurtGITranslucencyVolumeHistoryWeight=").Append(FormatFloat(burtGIScreenProbeSettings.TranslucencyVolumeHistoryWeight));
                builder.Append(" BurtGITranslucencyVolumeTemporalMaxRayDirections=").Append(burtGIScreenProbeSettings.TranslucencyVolumeTemporalMaxRayDirections);
                builder.Append(" BurtGITranslucencyVolumeSpatialFilter=").Append(burtGIScreenProbeSettings.TranslucencyVolumeSpatialFilter);
                builder.Append(" BurtGITranslucencyVolumeSpatialSamples=").Append(burtGIScreenProbeSettings.TranslucencyVolumeSpatialFilterSampleCount);
                builder.Append(" BurtGITranslucencyVolumeTraceStepFactor=").Append(FormatFloat(burtGIScreenProbeSettings.TranslucencyVolumeTraceStepFactor));
                builder.Append(" BurtGITranslucencyVolumeMaxTraceDistance=").Append(FormatFloat(burtGIScreenProbeSettings.TranslucencyVolumeMaxTraceDistance));
                builder.Append(" BurtGITranslucencyVolumeVoxelTraceStartScale=").Append(FormatFloat(burtGIScreenProbeSettings.TranslucencyVolumeVoxelTraceStartDistanceScale));
                builder.Append(" BurtGITranslucencyVolumeMaxRayIntensity=").Append(FormatFloat(burtGIScreenProbeSettings.TranslucencyVolumeMaxRayIntensity));
                var burtGITranslucencyVolumeHistory = BurtGITranslucencyVolumeHistoryUtility.GetHistoryStatus(request, burtGISettings, burtGIScreenProbeSettings);
                builder.Append(" BurtGITranslucencyVolumeHistoryValid=").Append(burtGITranslucencyVolumeHistory.HasHistory);
                builder.Append(" BurtGITranslucencyVolumeHistoryMatches=").Append(burtGITranslucencyVolumeHistory.DescriptorMatches);
                builder.Append(" BurtGITranslucencyVolumeHistorySize=").Append(burtGITranslucencyVolumeHistory.Width).Append('x').Append(burtGITranslucencyVolumeHistory.Height).Append('x').Append(burtGITranslucencyVolumeHistory.Depth);
                builder.Append(" BurtGITranslucencyVolumeHistoryFrame=").Append(burtGITranslucencyVolumeHistory.FrameIndex);
                builder.Append(" BurtGITranslucencyVolumeHistoryReason=").Append(burtGITranslucencyVolumeHistory.LastInvalidationReason);
                builder.Append(" BurtGITranslucencyVolumeHistoryMode=WorldReprojectUVZTrilinearLite");
                builder.Append(" BurtGITemporalDiagnosticsTarget=BurtGITemporalDiagnostics");
                builder.Append(" BurtGIScreenProbe=").Append(burtGIScreenProbeSettings.Enabled ? "Enabled" : "Disabled");
                builder.Append(" BurtGIScreenProbeTraceRadianceIntensityScale=").Append(FormatFloat(burtGIScreenProbeSettings.TraceRadianceIntensityScale));
                builder.Append(" BurtGIScreenProbeIrradianceFormat=").Append(burtGIScreenProbeSettings.IrradianceFormat);
                builder.Append(" BurtGIScreenProbeIntegrateType=").Append(burtGIScreenProbeSettings.IntegrateType);
                builder.Append(" BurtGIScreenProbeIntegrateMethod=").Append(burtGIScreenProbeSettings.IntegrateMethod);
                builder.Append(" BurtGIRadianceCacheClipMap=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveRadianceCacheClipMapResourceContractLabel(request != null ? request.Camera : null, burtGIScreenProbeSettings, asset));
                builder.Append(" BurtGIRadianceCacheClipMapResolution=").Append(burtGIScreenProbeSettings.RadianceCacheClipMapResolution);
                builder.Append(" BurtGIRadianceCacheClipMapWorldExtent=").Append(burtGIScreenProbeSettings.RadianceCacheClipMapWorldExtent.ToString("0.###"));
                builder.Append(" BurtGIRadianceCacheClipMapTraceBudget=").Append(burtGIScreenProbeSettings.RadianceCacheNumProbesToTraceBudget);
                builder.Append(" BurtGIRadianceCacheVisualizeRadiusScale=").Append(burtGIScreenProbeSettings.RadianceCacheVisualizeRadiusScale.ToString("0.###"));
                builder.Append(" BurtGIScreenProbeTraceHardwareRay=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenProbeHardwareRayTracingStatusLabel(asset, burtGIScreenProbeSettings));
                builder.Append(" BurtGIRadianceCacheTraceHardwareRay=").Append(burtGIScreenProbeSettings.RadianceCacheTraceHardwareRay ? "Requested" : "VolumeOff");
                builder.Append(" BurtGIRadianceCacheTracePath=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveRadianceCacheHardwareRayTracingStatusLabel(asset, burtGIScreenProbeSettings));
                builder.Append(" BurtXGILightComponent=").Append(BurtXGILightComponent.GetDebugStatus(request != null ? request.Camera : null));
                builder.Append(" BurtGIRadianceCacheVisualizeGeometry=ProceduralProbeCubeOverlayPass19");
                var burtGIRadianceCacheClipMapHistory = BurtRadianceCacheClipMapHistoryUtility.GetHistoryStatus(request, burtGIScreenProbeSettings);
                builder.Append(" BurtGIRadianceCacheClipMapHistoryValid=").Append(burtGIRadianceCacheClipMapHistory.HasHistory);
                builder.Append(" BurtGIRadianceCacheClipMapHistoryMatches=").Append(burtGIRadianceCacheClipMapHistory.DescriptorMatches);
                builder.Append(" BurtGIRadianceCacheClipMapHistorySize=").Append(burtGIRadianceCacheClipMapHistory.Width).Append('x').Append(burtGIRadianceCacheClipMapHistory.Height);
                builder.Append(" BurtGIRadianceCacheClipMapHistoryFrame=").Append(burtGIRadianceCacheClipMapHistory.FrameIndex);
                builder.Append(" BurtGIRadianceCacheClipMapHistoryReason=").Append(burtGIRadianceCacheClipMapHistory.LastInvalidationReason);
                builder.Append(" BurtGIRadianceCacheClipMapHistoryMode=PersistentIndirectionRemapDepthFinalAtlasMipRestoreLite");
                var burtGISceneVoxelHistory = BurtGISceneVoxelHistoryUtility.GetHistoryStatus(request, burtGISettings, burtGIScreenProbeSettings);
                builder.Append(" BurtGISceneVoxelHistoryValid=").Append(burtGISceneVoxelHistory.HasHistory);
                builder.Append(" BurtGISceneVoxelHistoryMatches=").Append(burtGISceneVoxelHistory.DescriptorMatches);
                builder.Append(" BurtGISceneVoxelHistoryResolution=").Append(burtGISceneVoxelHistory.Resolution);
                builder.Append(" BurtGISceneVoxelHistoryFrame=").Append(burtGISceneVoxelHistory.FrameIndex);
                builder.Append(" BurtGISceneVoxelHistoryReason=").Append(burtGISceneVoxelHistory.LastInvalidationReason);
                builder.Append(" BurtGISceneVoxelHistoryMode=RestoreThenVisibleProbeOverlay");
                builder.Append(" BurtGISceneVoxelAlwaysUpdate=").Append(burtGISettings.SceneVoxelAlwaysUpdate);
                builder.Append(" BurtGISceneVoxelOriginUpdateDistance=").Append(burtGISettings.SceneVoxelOriginUpdateDistance.ToString("0.###"));
                builder.Append(" BurtGISceneVoxelClipMapResolution=").Append(burtGISettings.SceneVoxelClipMapResolution);
                builder.Append(" BurtGISceneVoxelClipMapCount=").Append(burtGISettings.SceneVoxelClipMapCount);
                builder.Append(" BurtGISceneVoxelClipMapFirstWorldExtent=").Append(burtGIScreenProbeSettings.SceneVoxelClipMapFirstWorldExtent.ToString("0.###"));
                builder.Append(" BurtGISceneVoxelClipMapDistributionBase=").Append(burtGISettings.SceneVoxelClipMapDistributionBase.ToString("0.###"));
                builder.Append(" BurtGISceneVoxelClipMapOffset03=").Append(FormatVector4(burtGISettings.SceneVoxelClipMapOffset03));
                builder.Append(" BurtGISceneVoxelClipMapUpdateDistance03=").Append(FormatVector4(burtGISettings.SceneVoxelClipMapUpdateDistance03));
                builder.Append(" BurtGISceneVoxelMaterialBudget=").Append(burtGISettings.SceneVoxelMaterialBudget);
                builder.Append(" BurtGISceneVoxelMaterialGenerateMethod=").Append(burtGISettings.SceneVoxelMaterialGenerateMethod);
                builder.Append(" BurtGISceneVoxelDrawVegetation=").Append(burtGISettings.SceneVoxelDrawVegetation);
                builder.Append(" BurtGISceneVoxelDrawGrass=").Append(burtGISettings.SceneVoxelDrawGrass);
                builder.Append(" BurtGISceneVoxelLightingType=").Append(burtGISettings.SceneVoxelLightingType);
                builder.Append(" BurtGISceneVoxelLightingDirectionalShadow=").Append(burtGISettings.SceneVoxelLightingDirectionalShadow);
                builder.Append(" BurtGISceneVoxelLightingPunctualShadow=").Append(burtGISettings.SceneVoxelLightingPunctualShadow);
                builder.Append(" BurtGISceneVoxelLightingSkyLight=").Append(burtGISettings.SceneVoxelLightingSkyLight);
                builder.Append(" BurtGISceneVoxelEnableSkyVisibility=").Append(burtGISettings.SceneVoxelEnableSkyVisibility);
                builder.Append(" BurtGISceneVoxelMaxSampleCount=").Append(burtGISettings.SceneVoxelMaxSampleCount);
                builder.Append(" BurtGISceneVoxelMultiBounce=").Append(burtGISettings.SceneVoxelMultiBounce);
                builder.Append(" BurtGISceneVoxelDebugExpandView=").Append(burtGISettings.SceneVoxelDebugExpandView);
                builder.Append(" BurtGISceneVoxelDebugExpandViewDistance=").Append(FormatFloat(burtGISettings.SceneVoxelDebugExpandViewDistance));
                builder.Append(" BurtGISceneVoxelDebugShowMipmapID=").Append(burtGISettings.SceneVoxelDebugShowMipmapID);
                builder.Append(" BurtGISceneVoxelDebugLayer=").Append(burtGISettings.SceneVoxelDebugLayer);
                builder.Append(" BurtGISceneVoxelDebugByTrace=").Append(burtGISettings.SceneVoxelDebugByTrace);
                builder.Append(" BurtGISceneVoxelDebugDrawProbe=").Append(burtGISettings.SceneVoxelDebugDrawProbe);
                builder.Append(" BurtGISceneVoxelDebugProbeSizeWS=").Append(FormatFloat(burtGISettings.SceneVoxelDebugProbeSizeWS));
                builder.Append(" BurtGILocalSkyProbeShowDebugSphere=").Append(burtGISettings.LocalSkyProbeShowDebugSphere);
                builder.Append(" BurtGISceneVoxelFollowCamera=").Append(burtGIScreenProbeSettings.SceneVoxelFollowCamera);
                builder.Append(" BurtGISceneVoxelCameraForward=").Append(burtGIScreenProbeSettings.SceneVoxelCameraForward.ToString("0.###"));
                builder.Append(" BurtGISceneVoxelOrigin=").Append(FormatVector3(burtGIScreenProbeSettings.SceneVoxelOrigin.x, burtGIScreenProbeSettings.SceneVoxelOrigin.y, burtGIScreenProbeSettings.SceneVoxelOrigin.z));
                builder.Append(" BurtGISceneVoxelOctree=").Append(BurtGISceneVoxelOctreeUtility.DebugStatus);
                builder.Append(" BurtGISceneVoxelClipmaps=").Append(BurtGISceneVoxelClipmapStateUtility.GetDebugStatus(request != null ? request.Camera : null));
                builder.Append(" BurtGIXGILightGrid=").Append(BurtGIXGILightGridUtility.GetDebugStatus(request != null ? request.Camera : null));
                var burtGISceneVoxelVolumeActive = BurtGISceneVoxelVolume.TryGetBestForCamera(request != null ? request.Camera : null, out var burtGISceneVoxelVolume);
                builder.Append(" BurtGISceneVoxelVolumeActive=").Append(burtGISceneVoxelVolumeActive);
                if (burtGISceneVoxelVolumeActive)
                {
                    builder.Append(" BurtGISceneVoxelVolumeCenterExtent=").Append(FormatVector4(burtGISceneVoxelVolume.CenterExtent));
                }
                builder.Append(" BurtGISceneVoxelMeshTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCollectedTriangleCount);
                builder.Append(" BurtGISceneVoxelSkinnedTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCollectedSkinnedTriangleCount);
                builder.Append(" BurtGISceneVoxelTerrainTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCollectedTerrainTriangleCount);
                builder.Append(" BurtGISceneVoxelTerrainTreeTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCollectedTerrainTreeTriangleCount);
                builder.Append(" BurtGISceneVoxelTerrainDetailTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCollectedTerrainDetailTriangleCount);
                builder.Append(" BurtGISceneVoxelTexturedTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCollectedTexturedTriangleCount);
                builder.Append(" BurtGISceneVoxelAlphaClippedTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastAlphaClippedTriangleCount);
                builder.Append(" BurtGIVoxelLightTriangleCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCollectedVoxelLightTriangleCount);
                builder.Append(" BurtGISceneVoxelSkippedTerrainTreeInstanceCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastSkippedTerrainTreeInstanceCount);
                builder.Append(" BurtGISceneVoxelSkippedTerrainDetailInstanceCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastSkippedTerrainDetailInstanceCount);
                builder.Append(" BurtGISceneVoxelCandidateRendererCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCandidateRendererCount);
                builder.Append(" BurtGISceneVoxelCandidateTerrainCount=").Append(BurtGISceneVoxelMeshRasterizerUtility.LastCandidateTerrainCount);
                builder.Append(" BurtGISceneVoxelMeshUpdateInterval=").Append(BurtGISceneVoxelMeshRasterizerUtility.UpdateInterval);
                var burtGIProbeVolumeActive = BurtGIProbeVolume.TryGetBestForCamera(request != null ? request.Camera : null, out var burtGIProbeVolume);
                builder.Append(" BurtGIProbeVolumeActive=").Append(burtGIProbeVolumeActive);
                builder.Append(" BurtGIProbeRuntimeInfo=").Append(BurtGIProbeVolumeUtility.GetDebugStatus(request != null ? request.Camera : null));
                builder.Append(" BurtGIProbeDebugDraw=").Append(BurtGIProbeDebugDrawPass.GetDebugStatus());
                builder.Append(" BurtGIProbePhysicalPoolUploadStatus=").Append(BurtGIVirtualProbePhysicalPool.ResolveUploadComputeStatusLabel());
                builder.Append(" BurtGIProbeTimeSlice=").Append(BurtGIProbeVolume.ActiveTimeSlice);
                if (BurtGIProbeVolume.TryGetTimeSliceClock(BurtGIProbeVolume.ActiveTimeSlice, out var burtGIProbeClockHour, out var burtGIProbeClockMinute))
                {
                    builder.Append(" BurtGIProbeTimeSliceClock=").Append(burtGIProbeClockHour.ToString("00")).Append(':').Append(burtGIProbeClockMinute.ToString("00"));
                }
                if (burtGIProbeVolumeActive)
                {
                    builder.Append(" BurtGIProbeVolumeCenterExtent=").Append(FormatVector4(burtGIProbeVolume.CenterExtent));
                    var burtGIProbeHalfExtents = burtGIProbeVolume.LocalHalfExtents;
                    builder.Append(" BurtGIProbeVolumeHalfExtents=").Append(FormatVector3(burtGIProbeHalfExtents.x, burtGIProbeHalfExtents.y, burtGIProbeHalfExtents.z));
                    builder.Append(" BurtGIProbeVolumeSize=").Append(FormatVector3(burtGIProbeVolume.size.x, burtGIProbeVolume.size.y, burtGIProbeVolume.size.z));
                    var burtGIProbeRotation = burtGIProbeVolume.transform.rotation.eulerAngles;
                    builder.Append(" BurtGIProbeVolumeRotation=").Append(FormatVector3(burtGIProbeRotation.x, burtGIProbeRotation.y, burtGIProbeRotation.z));
                    builder.Append(" BurtGIProbeVolumeMode=").Append(burtGIProbeVolume.IsVirtualReady ? "XGIVirtual" : "IrradianceTexture");
                    builder.Append(" BurtGIProbeVolumeUsesTimeSlice=").Append(burtGIProbeVolume.useTimeSlice);
                    builder.Append(" BurtGIProbeVolumeTexture=").Append(burtGIProbeVolume.irradiance != null ? burtGIProbeVolume.irradiance.name : "<none>");
                    if (burtGIProbeVolume.IsVirtualReady)
                    {
                        builder.Append(" BurtGIProbeVirtualPageTable=").Append(burtGIProbeVolume.virtualPageTable != null ? burtGIProbeVolume.virtualPageTable.name : "<none>");
                        builder.Append(" BurtGIProbeVirtualIndirection=").Append(burtGIProbeVolume.virtualIndirection != null ? burtGIProbeVolume.virtualIndirection.name : "<none>");
                        builder.Append(" BurtGIProbeVirtualL2=").Append(burtGIProbeVolume.HasVirtualL2);
                        builder.Append(" BurtGIProbeVirtualValidity=").Append(burtGIProbeVolume.HasVirtualValidity);
                        builder.Append(" BurtGIProbeVirtualSkyVisibility=").Append(burtGIProbeVolume.HasVirtualSkyVisibility);
                        builder.Append(" BurtGIProbeVirtualSkyShadingDirection=").Append(burtGIProbeVolume.HasVirtualSkyShadingDirection);
                        builder.Append(" BurtGIProbeVirtualLightIntensity=").Append(FormatFloat(burtGIProbeVolume.virtualLightIntensity));
                        builder.Append(" BurtGIProbeVirtualSkyVisibilityIntensity=").Append(FormatFloat(burtGIProbeVolume.virtualSkyVisibilityIntensity));
                        builder.Append(" BurtGIProbeVirtualSkyVisibilityTint=").Append(FormatVector3(burtGIProbeVolume.virtualSkyVisibilityTint.r, burtGIProbeVolume.virtualSkyVisibilityTint.g, burtGIProbeVolume.virtualSkyVisibilityTint.b));
                        builder.Append(" BurtGIProbeVirtualSkyVisibilityOffset=").Append(FormatFloat(burtGIProbeVolume.virtualSkyVisibilityOffset));
                        builder.Append(" BurtGIProbeVirtualMainLightSHIntensity=").Append(FormatFloat(burtGIProbeVolume.virtualMainLightSHIntensity));
                        builder.Append(" BurtGIProbeVirtualMainLightSHTint=").Append(FormatVector3(burtGIProbeVolume.virtualMainLightSHTint.r, burtGIProbeVolume.virtualMainLightSHTint.g, burtGIProbeVolume.virtualMainLightSHTint.b));
                        builder.Append(" BurtGIProbeVirtualTimeSliceMainLightIntensity=").Append(FormatFloat(burtGIProbeVolume.virtualTimeSliceMainLightIntensity));
                        builder.Append(" BurtGIProbeVirtualMainLightOcclusion=").Append(FormatFloat(burtGIProbeVolume.virtualMainLightOcclusion));
                        builder.Append(" BurtGIProbeVirtualMainLightSHUsesPreExposure=").Append(burtGIProbeVolume.virtualMainLightSHUsesPreExposure);
                        builder.Append(" BurtGIProbeVirtualLoadedMin=").Append(FormatVector3Int(burtGIProbeVolume.virtualMinLoadedEntry));
                        builder.Append(" BurtGIProbeVirtualLoadedMax=").Append(FormatVector3Int(burtGIProbeVolume.virtualMaxLoadedEntry));
                        builder.Append(" BurtGIProbeVirtualPhysicalPool=").Append(FormatVector3Int(burtGIProbeVolume.virtualPhysicalPoolDimensions));
                        builder.Append(" BurtGIProbeVirtualBuffers=").Append(burtGIProbeVolume.PageTableBuffer != null && burtGIProbeVolume.IndirectionBuffer != null);
                        builder.Append(" BurtGIProbeVirtualPageTableEntries=").Append(burtGIProbeVolume.VirtualPageTableEntryCount);
                        builder.Append(" BurtGIProbeVirtualIndirectionEntries=").Append(burtGIProbeVolume.VirtualIndirectionEntryCount);
                        var burtGIProbeStreamingActive = BurtGIVirtualProbeCellStreamer.TryGetForProbeVolume(burtGIProbeVolume, out var burtGIProbeStreamer);
                        builder.Append(" BurtGIProbeStreamingActive=").Append(burtGIProbeStreamingActive);
                        if (burtGIProbeStreamingActive)
                        {
                            builder.Append(" BurtGIProbeStreamingInitialized=").Append(burtGIProbeStreamer.IsInitialized);
                            builder.Append(" BurtGIProbeStreamingLoadedCells=").Append(burtGIProbeStreamer.LoadedCellCount);
                            builder.Append(" BurtGIProbeStreamingConfiguredCells=").Append(burtGIProbeStreamer.ConfiguredCellCount);
                            builder.Append(" BurtGIProbeStreamingPhysicalChunks=").Append(burtGIProbeStreamer.LoadedPhysicalChunkCount);
                            builder.Append(" BurtGIProbeStreamingSharedChunks=").Append(burtGIProbeStreamer.LoadedSharedChunkCount);
                            builder.Append(" BurtGIProbeStreamingResolvedSlices=").Append(burtGIProbeStreamer.ResolvedSliceCount);
                            builder.Append(" BurtGIProbePhysicalPoolInitialized=").Append(burtGIProbeStreamer.physicalPool != null && burtGIProbeStreamer.physicalPool.IsInitialized);
                            builder.Append(" BurtGIProbePhysicalPoolCapacity=").Append(burtGIProbeStreamer.physicalPool != null ? burtGIProbeStreamer.physicalPool.ChunkCapacity : 0);
                            builder.Append(" BurtGIProbePhysicalPoolDimensions=").Append(burtGIProbeStreamer.physicalPool != null ? FormatVector3Int(burtGIProbeStreamer.physicalPool.PhysicalPoolDimensions) : "<none>");
                        }
                    }
                }
                var burtGIRadianceCacheHashGridHistory = BurtRadianceCacheHashGridHistoryUtility.GetHistoryStatus(request, burtGIScreenProbeSettings);
                builder.Append(" BurtGIRadianceCacheHashGridHistoryValid=").Append(burtGIRadianceCacheHashGridHistory.HasHistory);
                builder.Append(" BurtGIRadianceCacheHashGridHistoryMatches=").Append(burtGIRadianceCacheHashGridHistory.DescriptorMatches);
                builder.Append(" BurtGIRadianceCacheHashGridHistoryCells=").Append(burtGIRadianceCacheHashGridHistory.CellCount);
                builder.Append(" BurtGIRadianceCacheHashGridHistoryTiles=").Append(burtGIRadianceCacheHashGridHistory.TileCount);
                builder.Append(" BurtGIRadianceCacheHashGridHistoryFrame=").Append(burtGIRadianceCacheHashGridHistory.FrameIndex);
                builder.Append(" BurtGIRadianceCacheHashGridHistoryReason=").Append(burtGIRadianceCacheHashGridHistory.LastInvalidationReason);
                AppendBurtGIRadianceCacheClipMapResourceState(builder, resourceRegistry);
                builder.Append(" BurtGIScreenProbeIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIndirectArgsBufferName));
                builder.Append(" BurtGIScreenProbeIntegrateTileIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileIndirectArgsBufferName));
                builder.Append(" BurtGIScreenProbeIntegrateTileDataDiffuseBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataDiffuseBufferName));
                builder.Append(" BurtGIScreenProbeIntegrateTileDataAllBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileDataAllBufferName));
                builder.Append(" BurtGIScreenProbeIntegrateTileClassificationRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeIntegrateTileClassificationName));
                builder.Append(" BurtGIScreenProbeTraceCompactTexelCountBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelCountBufferName));
                builder.Append(" BurtGIScreenProbeTraceCompactTexelDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactTexelDataBufferName));
                builder.Append(" BurtGIScreenProbeTraceCompactIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactIndirectArgsBufferName));
                builder.Append(" BurtGIScreenProbeTraceCompactThreadCountXBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeTraceCompactThreadCountXBufferName));
                builder.Append(" BurtGIScreenProbeTraceCompact=ComputeLiteProducerConsumer+TraceAtlasHashGridBucketTilePingPongPackedCacheLite");
                builder.Append(" BurtGIScreenProbeAdaptiveProbeHeaderRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeHeaderName));
                builder.Append(" BurtGIScreenProbeAdaptiveProbeIndicesRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeIndicesName));
                builder.Append(" BurtGIScreenProbeAdaptiveProbeNumBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeNumBufferName));
                builder.Append(" BurtGIScreenProbeAdaptiveProbeDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIScreenProbeAdaptiveProbeDataBufferName));
                builder.Append(" BurtGIScreenProbeAdaptivePlacement=ResourceContractTileCapacity").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenProbeAdaptiveProbeIndexTileCapacity(burtGIScreenProbeSettings));
                builder.Append(" BurtGIScreenProbeAdaptiveMinDownSampleFactor=").Append(burtGIScreenProbeSettings.AdaptiveMinDownSampleFactor);
                builder.Append(" BurtGIScreenProbeTraceOctahedronResolution=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenProbeTracingOctahedronResolution(burtGIScreenProbeSettings));
                builder.Append(" BurtGIScreenProbeIrradianceFormat=").Append(burtGIScreenProbeSettings.IrradianceFormat);
                builder.Append(" BurtGIScreenProbeIntegrateType=").Append(burtGIScreenProbeSettings.IntegrateType);
                builder.Append(" BurtGIScreenProbeIntegrateMethod=").Append(burtGIScreenProbeSettings.IntegrateMethod);
                builder.Append(" BurtGIScreenProbeStage=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeStageLabel(burtGIScreenProbeSettings));
                builder.Append(" BurtGIScreenProbeGrid=").Append(burtGIScreenProbeSettings.Enabled ? ("Spacing" + burtGIScreenProbeSettings.SpacingPixels + "px") : "Disabled");
                builder.Append(" BurtGIScreenProbeDebug=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationScreenProbeDebugLabel(burtGIScreenProbeSettings));
                builder.Append(" BurtGIScreenProbeTraceDistance=").Append(FormatFloat(burtGIScreenProbeSettings.TraceDistance));
                builder.Append(" BurtGIScreenProbeTraceVoxelMaxSteps=").Append(burtGIScreenProbeSettings.TraceVoxelMaxTraceSteps);
                builder.Append(" BurtGIScreenProbeTraceVoxelStepFactor=").Append(FormatFloat(burtGIScreenProbeSettings.TraceVoxelStepFactor));
                builder.Append(" BurtGIScreenProbeSamples=").Append(burtGIScreenProbeSettings.SampleCount);
                builder.Append(" BurtGIScreenProbeTemporalFeedback=").Append(FormatFloat(burtGIScreenProbeSettings.TemporalFeedback));
                builder.Append(" BurtGIScreenProbeApplyStrength=").Append(FormatFloat(burtGIScreenProbeSettings.ApplyStrength));
                builder.Append(" BurtGIScreenProbeSpatialFilterPasses=").Append(burtGIScreenProbeSettings.SpatialFilterPasses);
                builder.Append(" BurtGIScreenProbeSpatialFilterHalfKernel=").Append(burtGIScreenProbeSettings.SpatialFilterHalfKernelSize);
                builder.Append(" BurtGIScreenProbeFixupBorders=").Append(burtGIScreenProbeSettings.FixupBorders);
                builder.Append(" BurtGIScreenProbeHistoryValid=").Append(burtGIScreenProbeHistory.HasHistory);
                builder.Append(" BurtGIScreenProbeHistoryMatches=").Append(burtGIScreenProbeHistory.DescriptorMatches);
                builder.Append(" BurtGIScreenProbeHistorySize=").Append(burtGIScreenProbeHistory.Width).Append('x').Append(burtGIScreenProbeHistory.Height);
                builder.Append(" BurtGIScreenProbeHistoryFrame=").Append(burtGIScreenProbeHistory.FrameIndex);
                builder.Append(" BurtGIScreenProbeHistoryReason=").Append(burtGIScreenProbeHistory.LastInvalidationReason);
                builder.Append(" BurtGIScreenProbeExposureCheckThreshold=").Append(burtGIScreenProbeSettings.TemporalExposureCheckThreshold.ToString("0.###"));
                builder.Append(" BurtGIScreenProbePlayerVelocityThreshold=").Append(burtGIScreenProbeSettings.TemporalPlayerVelocityThreshold.ToString("0.###"));
                builder.Append(" BurtGIScreenProbeHistoryNormalThreshold=").Append(burtGIScreenProbeSettings.TemporalHistoryNormalThreshold.ToString("0.###"));
                builder.Append(" BurtGIScreenProbeFastUpdateMode=XRenderHistoryDisabled");
                builder.Append(" BurtGIQuality=").Append(burtGISettings.Quality);
                builder.Append(" BurtGIResolution=").Append(burtGISettings.Resolution);
                builder.Append(" BurtGIFinalGather=").Append(burtGISettings.FinalGather);
                builder.Append(" BurtGIIrradianceFieldStrength=").Append(FormatFloat(burtGISettings.IrradianceFieldStrength));
                builder.Append(" BurtGIIrradianceFieldBaked=").Append(burtGISettings.IrradianceFieldBaked);
                builder.Append(" BurtGIIntensity=").Append(FormatFloat(burtGISettings.Intensity));
                builder.Append(" BurtGIXGIScreenRatio=").Append(FormatFloat(burtGISettings.XGIScreenRatio));
                builder.Append(" BurtGIXGIScreenRatioSpeed=").Append(FormatFloat(burtGISettings.XGIScreenRatioSpeed));
                builder.Append(" BurtGIRadius=").Append(FormatFloat(burtGISettings.Radius));
                builder.Append(" BurtGISamples=").Append(burtGISettings.SampleCount);
                builder.Append(" BurtGIMaxSteps=").Append(burtGISettings.MaxSteps);
                builder.Append(" BurtGIThickness=").Append(FormatFloat(burtGISettings.Thickness));
                builder.Append(" BurtGISkyFallback=").Append(FormatFloat(burtGISettings.SkyFallback));
                builder.Append(" BurtGIRadianceClamp=").Append(FormatFloat(burtGISettings.RadianceClamp));
                builder.Append(" BurtGINormalWeight=").Append(FormatFloat(burtGISettings.NormalWeight));
                builder.Append(" BurtGIDistanceFade=").Append(FormatFloat(burtGISettings.DistanceFade));
                builder.Append(" BurtGIBlur=").Append(burtGISettings.Blur);
                builder.Append(" BurtGIBlurSharpness=").Append(FormatFloat(burtGISettings.BlurSharpness));
                builder.Append(" BurtGISpatialDenoiseRadius=").Append(FormatFloat(burtGISettings.SpatialDenoiseRadius));
                builder.Append(" BurtGISpatialDenoiseStrength=").Append(FormatFloat(burtGISettings.SpatialDenoiseStrength));
                builder.Append(" BurtGILeakGuardStrength=").Append(FormatFloat(burtGISettings.LeakGuardStrength));
                builder.Append(" BurtGIEdgeFadeStrength=").Append(FormatFloat(burtGISettings.EdgeFadeStrength));
                builder.Append(" BurtGINormalConeTightness=").Append(FormatFloat(burtGISettings.NormalConeTightness));
                builder.Append(" BurtGISkyEdgeSuppression=").Append(FormatFloat(burtGISettings.SkyEdgeSuppression));
                builder.Append(" BurtGIShortRangeAO=").Append(burtGISettings.ShortRangeAO ? "Enabled" : "Disabled");
                builder.Append(" BurtGIShortRangeAOWeight=").Append(FormatFloat(burtGISettings.ShortRangeAOWeight));
                builder.Append(" BurtGIShortRangeAOApplyWeight=").Append(FormatFloat(burtGISettings.ShortRangeAOApplyWeight));
                builder.Append(" BurtGIShortRangeAOSlopeTolerance=").Append(FormatFloat(burtGISettings.ShortRangeAOSlopeCompareToleranceScale));
                builder.Append(" BurtGITemporal=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenSpaceGlobalIlluminationTemporalStatusLabel(request, burtGISettings));
                builder.Append(" BurtGITemporalFeedback=").Append(FormatFloat(burtGISettings.TemporalFeedback));
                builder.Append(" BurtGITemporalDepthRejection=").Append(FormatFloat(burtGISettings.TemporalDepthRejection));
                builder.Append(" BurtGITemporalNormalRejection=").Append(FormatFloat(burtGISettings.TemporalNormalRejection));
                builder.Append(" BurtGITemporalClamp=").Append(FormatFloat(burtGISettings.TemporalClamp));
                builder.Append(" BurtGITemporalVarianceClamp=").Append(FormatFloat(burtGISettings.TemporalVarianceClamp));
                builder.Append(" BurtGITemporalHitRejection=").Append(FormatFloat(burtGISettings.TemporalHitRejection));
                builder.Append(" BurtGITemporalConfidence=").Append(burtGITemporalDiagnosticsRequested ? "DiagnosticsRT" : (BurtScreenSpaceGlobalIlluminationPassUtility.ShouldExposeScreenSpaceGlobalIlluminationToTemporalAA(request, asset, renderOptions) ? "ExternalTAA(CurrentGIAlpha)" : "Disabled"));
                builder.Append(" BurtGIHistoryValid=").Append(burtGIHistory.HasHistory);
                builder.Append(" BurtGIHistoryAllocated=").Append(burtGIHistory.HasHistory || burtGIHistory.HasDepthNormalHistory);
                builder.Append(" BurtGIHistoryMatches=").Append(burtGIHistory.DescriptorMatches);
                builder.Append(" BurtGIScreenProbeComputeStatus=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveScreenProbeComputeStatusLabel());
                builder.Append(" BurtGIHashGridDebugMaxCellDecay=").Append(burtGIScreenProbeSettings.RadianceCacheHashGridDebugMaxCellDecay);
                builder.Append(" BurtGIDepthNormalHistoryAllocated=").Append(burtGIHistory.HasDepthNormalHistory);
                builder.Append(" BurtGIDepthNormalHistoryMatches=").Append(burtGIHistory.DepthNormalDescriptorMatches);
                builder.Append(" BurtGIHistoryAge=").Append(burtGIHistory.HistoryAge);
                builder.Append(" BurtGIFrame=").Append(burtGIHistory.FrameIndex);
                builder.Append(" BurtGIFirstValidFrame=").Append(burtGIHistory.FirstValidFrameIndex);
                builder.Append(" BurtGILastInvalidationFrame=").Append(burtGIHistory.LastInvalidationFrameIndex);
                builder.Append(" BurtGIHistoryReason=").Append(burtGIHistory.LastInvalidationReason);
                builder.Append(" BurtGIXGICompatShaderStatus=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveXGICompatShaderStatusLabel());
                builder.Append(" BurtGIXGICompatRayTracingStatus=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveXGICompatRayTracingStatusLabel());
                builder.Append(" BurtGIRayTracingAS=").Append(BurtRayTracingAccelerationStructureUtility.ResolveStatusLabel(request != null ? request.Camera : null));
                builder.Append(" BurtGIXGIResourceStatus=").Append(BurtScreenSpaceGlobalIlluminationPassUtility.ResolveXGIResourceStatusReport().Replace("\r\n", ";").Replace('\n', ';'));
                builder.Append(" FurBlurEnabled=").Append(furBlurEnabled);
                builder.Append(" FurBlurCandidateGate=").Append(BurtFurBlurPassUtility.ResolveFurBlurCandidateGateLabel(request, asset));
                builder.Append(" FurBlurPath=").Append(furBlurPath);
                builder.Append(" MultipassRenderers=").Append(BurtMultipassRenderer.RegisteredRendererCount);
                builder.Append(" MultipassSkinnedRenderers=").Append(BurtMultipassRenderer.RegisteredSkinnedRendererCount);
                builder.Append(" MultipassBakedSkinnedMeshes=").Append(BurtMultipassRenderer.BakedSkinnedMeshCountThisFrame);
                builder.Append(" FurBlurTemporal=").Append(furBlurTemporalLabel);
                builder.Append(" FurBlurThetaTemporal=").Append(furBlurSettings.ThetaTemporal);
                builder.Append(" FurBlurColorTemporal=").Append(furBlurSettings.ColorTemporal);
                builder.Append(" FurBlurObjectVelocity=MultipassFurVelocityRT");
                builder.Append(" FurBlurTiled=").Append(BurtFurBlurPassUtility.ResolveFurBlurTiledStatusLabel());
                builder.Append(" FurBlurDebugMode=").Append(BurtFurBlurPassUtility.ResolveFurBlurDebugModeLabel());
                builder.Append(" FurBlurDebugShaderMode=").Append(BurtFurBlurPassUtility.ResolveFurBlurShaderDebugMode());
                builder.Append(" FurBlurShaderStatus=").Append(BurtFurBlurPassUtility.ResolveFurBlurShaderStatusLabel());
                builder.Append(" FurBlurHistoryValid=").Append(furBlurHistory.HasHistory);
                builder.Append(" FurBlurHistoryMatches=").Append(furBlurHistory.DescriptorMatches);
                builder.Append(" FurBlurPropertyHistoryValid=").Append(furBlurHistory.HasPropertyHistory);
                builder.Append(" FurBlurPropertyHistoryMatches=").Append(furBlurHistory.PropertyDescriptorMatches);
                builder.Append(" FurBlurHistoryAge=").Append(furBlurHistory.HistoryAge);
                builder.Append(" FurBlurFrame=").Append(furBlurHistory.FrameIndex);
                builder.Append(" FurBlurHistoryReason=").Append(furBlurHistory.Reason);
                builder.Append(" FurBlurRadiusCm=").Append(FormatFloat(furBlurSettings.RadiusCm));
                builder.Append(" FurBlurDepthThresholdEye=").Append(FormatFloat(furBlurSettings.DepthThresholdEye));
                builder.Append(" FurBlurDirectionDilationThreshold=").Append(FormatFloat(furBlurSettings.DirectionDilationThreshold));
                builder.Append(" FurBlurThetaFeedback=").Append(FormatFloat(furBlurSettings.ThetaFeedback));
                builder.Append(" FurBlurTemporalFeedback=").Append(FormatFloat(furBlurSettings.TemporalFeedback));
                builder.Append(" FurBlurDepthEncoding=SceneDeviceDepthInitializedFromCameraDepth;FurPixelsOverrideWithSVPositionZ");
                builder.Append(" FurBlurReversedZ=").Append(SystemInfo.usesReversedZBuffer);
                builder.Append(" SSREnabled=").Append(ssrSettings.Enabled);
                builder.Append(" SSRSuppressedByShadingDebug=").Append(ssrSuppressedByShadingDebug);
                builder.Append(" SSRDebugMode=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionDebugModeLabel());
                builder.Append(" SSRShaderStatus=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionShaderStatusLabel());
                builder.Append(" SSRTraceMode=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionTraceModeLabel(request, asset));
                builder.Append(" SSRComposite=SpecularReplaceCameraIBL");
                builder.Append(" SSRFallback=CameraIBLPreserved");
                builder.Append(" SSRApply=XRenderPureReflectMip0Alpha");
                builder.Append(" SSRClearCoat=DualLayerTrace");
                builder.Append(" SSRHiZTraceExperimental=").Append(ssrSettings.ExperimentalHiZTrace);
                builder.Append(" SSRHiZDiagnostics=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionHiZDiagnosticsStatusLabel());
                builder.Append(" SSRMaxSteps=").Append(ssrSettings.MaxSteps);
                builder.Append(" SSRTemporal=").Append(ssrSettings.TemporalAccumulation);
                builder.Append(" SSRHistoryValid=").Append(ssrHistory.HasHistory);
                builder.Append(" SSRHistoryMatches=").Append(ssrHistory.DescriptorMatches);
                builder.Append(" SSRDepthHistoryAllocated=").Append(ssrHistory.HasDepthHistory);
                builder.Append(" SSRDepthHistoryMatches=").Append(ssrHistory.DepthDescriptorMatches);
                builder.Append(" SSRNormalRoughnessHistoryAllocated=").Append(ssrHistory.HasNormalRoughnessHistory);
                builder.Append(" SSRNormalRoughnessHistoryMatches=").Append(ssrHistory.NormalRoughnessDescriptorMatches);
                builder.Append(" SSRHistoryAge=").Append(ssrHistory.HistoryAge);
                builder.Append(" SSRHistoryReason=").Append(ssrHistory.LastInvalidationReason);
            }
            builder.Append(" GBufferDebugMode=").Append(asset != null ? BurtGBufferDebugViewUtility.ResolveGBufferDebugViewMode(asset).ToString() : "<none>"); // 写入最终 GBuffer Debug 模式。

            builder.Append(" GBufferDebugSource=").Append(asset != null ? BurtGBufferDebugViewUtility.ResolveGBufferDebugViewSource(asset).ToString() : "<none>"); // 写入 GBuffer Debug 来源。

            builder.AppendLine(); // 结束 Deferred 状态行。
        }

        private static void AppendDescriptorLine( // 写入一行 RenderTextureDescriptor 诊断信息。
            StringBuilder builder, // 接收要写入的字符串构建器。
            string label, // 接收资源显示名称。
            RenderTextureDescriptor descriptor, // 接收要输出的 RT 描述。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收资源表，用来判断资源是否注册。
            string resourceName) // 接收资源表中的逻辑名称。
        {
            builder.Append("  ").Append(label); // 写入资源标签。

            builder.Append(" Registered=").Append(IsRegistered(resourceRegistry, resourceName)); // 写入资源是否已注册到当前图。

            builder.Append(" "); // 写入一个空格分隔注册状态和描述内容。

            AppendDescriptorState(builder, descriptor); // 写入尺寸、格式、sRGB、MSAA 等描述信息。

            builder.AppendLine(); // 当前资源行结束。
        }

        private static void AppendBurtGIRadianceCacheClipMapResourceState(StringBuilder builder, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            builder.Append(" BurtGIRadianceCacheClipMapIndirectionRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapIndirectionName));
            builder.Append(" BurtGIRadianceCacheClipMapDepthProbeAtlasRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapDepthProbeAtlasName));
            builder.Append(" BurtGIRadianceCacheClipMapRadianceProbeAtlasRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbeAtlasName));
            builder.Append(" BurtGIRadianceCacheClipMapFinalRadianceAtlasRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFinalRadianceAtlasName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeOcclusionAtlasRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeOcclusionAtlasName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeAllocatorBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeAllocatorBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListAllocatorBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeFreeListBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeFreeListBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastUsedFrameBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeLastTracedFrameBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeWorldOffsetBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeWorldOffsetBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeTraceDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceDataBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceAllocatorBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapPriorityHistogramBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPriorityHistogramBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapMaxUpdateBucketBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxUpdateBucketBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapMaxTracesFromMaxUpdateBucketBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbesToUpdateTraceCostBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapRadianceProbePDFBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceProbePDFBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapClearProbePDFsIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapGenerateProbeTraceTilesIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileAllocatorBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFilterProbesIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapPrepareProbeOcclusionIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapFixupProbeBordersIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapTraceProbesIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortProbeTraceTilesIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapRadianceCacheHardwareRayTracingIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapHardwareRayTracingRayAllocatorBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapProbeTraceTileDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapProbeTraceTileDataBufferName));
            builder.Append(" BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheClipMapSortedProbeTraceTileDataBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridValueBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridValueBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridValueBufferExternal=").Append(IsExternalBuffer(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridValueBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridTileBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridTileBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridTileBufferExternal=").Append(IsExternalBuffer(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridTileBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridCountBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridCountBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridCountBufferExternal=").Append(IsExternalBuffer(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridCountBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridVisibilityCellQueryBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridVisibilityCellQueryBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridUpdateCellValueBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateCellValueBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridUpdateCellValueBufferExternal=").Append(IsExternalBuffer(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateCellValueBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridUpdateTileBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateTileBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateTilesIndirectArgsBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridUpdateTilesGroupCountXBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridDebugCellBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridDebugCellBufferName));
            builder.Append(" BurtGIRadianceCacheHashGridDebugDrawArgsBufferRegistered=").Append(IsBufferRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.BurtGIRadianceCacheHashGridDebugDrawArgsBufferName));
        }

        private static void AppendSkippedRenderTargetLine( // 写入一个当前路径不使用的资源状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            string label, // 接收资源显示名称。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收资源表，用来判断资源是否意外注册。
            string resourceName) // 接收资源表中的逻辑名称。
        {
            builder.Append("  ").Append(label); // 写入资源标签。

            builder.Append(" Registered=").Append(IsRegistered(resourceRegistry, resourceName)); // 写入当前图是否注册了这个资源，方便发现条件分支不一致。

            builder.AppendLine(" <skipped>"); // 明确表示这个资源在当前渲染路径中不会分配。
        }

        private static void AppendDescriptorState( // 把 RenderTextureDescriptor 转成紧凑的可读文本。
            StringBuilder builder, // 接收要写入的字符串构建器。
            RenderTextureDescriptor descriptor) // 接收 RT 描述。
        {
            builder.Append("Size=").Append(descriptor.width).Append('x').Append(descriptor.height); // 写入 RT 尺寸。

            builder.Append(" ColorFormat=").Append(descriptor.colorFormat); // 写入旧版 RenderTextureFormat，方便和 Inspector targetTexture 对齐。

            builder.Append(" GraphicsFormat=").Append(descriptor.graphicsFormat); // 写入底层 GraphicsFormat，排查平台格式选择时更准确。

            builder.Append(" DepthBits=").Append(descriptor.depthBufferBits); // 写入深度位数，区分颜色 RT 和深度 RT。

            builder.Append(" sRGB=").Append(descriptor.sRGB); // 写入 sRGB 标记，排查 Gamma/Linear 或 copy 变色问题。

            builder.Append(" MSAA=").Append(descriptor.msaaSamples); // 写入 MSAA 采样数，排查 MRT 或 FinalBlit 采样数不一致。

            builder.Append(" MipMap=").Append(descriptor.useMipMap); // 写入是否生成 mip，排查不必要的相机 RT mip 开销。

            builder.Append(" Dimension=").Append(descriptor.dimension); // 写入纹理维度，确认当前不是意外的数组或 cube。
            builder.Append(" VolumeDepth=").Append(descriptor.volumeDepth);
        }

        private static void AppendRenderTextureState( // 把真实 RenderTexture 转成紧凑可读文本。
            StringBuilder builder, // 接收要写入的字符串构建器。
            RenderTexture renderTexture) // 接收真实 targetTexture，可能为空。
        {
            if (renderTexture == null) // 如果没有 targetTexture。
            {
                builder.Append("<none>"); // 写入空占位。

                return; // 没有更多信息。
            }

            builder.Append("Name=").Append(FormatRenderTextureName(renderTexture)); // 写入 RenderTexture 名称或实例 ID。

            builder.Append(" Size=").Append(renderTexture.width).Append('x').Append(renderTexture.height); // 写入真实 RT 尺寸。

            builder.Append(" Format=").Append(renderTexture.format); // 写入 RenderTextureFormat，方便判断 HDR/LDR。

            builder.Append(" GraphicsFormat=").Append(renderTexture.graphicsFormat); // 写入底层 GraphicsFormat。

            builder.Append(" DepthBits=").Append(renderTexture.depth); // 写入 targetTexture 自带深度位数。

            builder.Append(" sRGB=").Append(renderTexture.sRGB); // 写入 sRGB 标记，排查颜色空间问题。

            builder.Append(" MSAA=").Append(renderTexture.antiAliasing); // 写入真实 RT 抗锯齿采样数。

            builder.Append(" MipMap=").Append(renderTexture.useMipMap); // 写入是否使用 mipmap。

            builder.Append(" Dimension=").Append(renderTexture.dimension); // 写入纹理维度。
            builder.Append(" VolumeDepth=").Append(renderTexture.volumeDepth);
        }

        private static string FormatRenderTextureName(RenderTexture renderTexture) // 生成 RenderTexture 的简短名称。
        {
            if (renderTexture == null) // 如果没有 RenderTexture。
            {
                return "<none>"; // 返回空占位。
            }

            return string.IsNullOrEmpty(renderTexture.name) ? "InstanceID " + renderTexture.GetInstanceID() : renderTexture.name; // 有名称时输出名称，没有名称时输出 InstanceID。
        }

        private static string FormatTextureName(Texture texture)
        {
            if (texture == null)
            {
                return "<none>";
            }

            return string.IsNullOrEmpty(texture.name) ? "InstanceID " + texture.GetInstanceID() : texture.name;
        }

        private static bool IsRegistered( // 判断资源表里是否注册了某个逻辑资源。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收资源表，可能为空。
            string resourceName) // 接收资源逻辑名。
        {
            return resourceRegistry != null && resourceRegistry.ContainsRenderTarget(resourceName); // 资源表存在且包含该名称时返回 true。
        }


        private static bool IsBufferRegistered(
            BurtRenderGraphResourceRegistry resourceRegistry,
            string bufferName)
        {
            return resourceRegistry != null && resourceRegistry.ContainsBuffer(bufferName);
        }

        private static bool IsExternalBuffer(
            BurtRenderGraphResourceRegistry resourceRegistry,
            string bufferName)
        {
            return resourceRegistry != null && resourceRegistry.IsExternalBuffer(bufferName);
        }

        private static bool IsDeferredRequest(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 判断当前 request 实际是否使用 Deferred 路径。
        {
            if (IsPreviewOrReflectionRequest(request)) // Preview/Reflection 已在 Pipeline 层强制走 Forward。
            {
                return false; // 返回 false，让 Debug 输出和实际组装器、资源注册保持一致。
            }

            return asset != null && asset.RendererMode == BurtRendererMode.Deferred; // 非 Preview 时跟随资产上的 Renderer Mode。
        }

        private static bool IsPreviewOrReflectionRequest(BurtRenderRequest request) // 判断当前 request 是否被强制隔离到 Forward 辅助路径。
        {
            return request != null && (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection); // Preview 和 Reflection 都不应显示为 Deferred。
        }

        private static void AppendValidationMessages( // 写入 RenderGraph 校验消息。
            StringBuilder builder, // 接收要写入的字符串构建器。
            IReadOnlyList<string> validationMessages, // 接收图级别校验消息。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages) // 接收 Pass 级别资源使用记录，用来读取局部校验消息。
        {
            builder.AppendLine("Validation:"); // 校验信息放在 Pass 明细前，方便打开日志后先看到问题。

            var hasMessages = false; // 标记是否写过任何校验消息。

            if (validationMessages != null) // 图级消息可能为空，先做空值保护。
            {
                for (var messageIndex = 0; messageIndex < validationMessages.Count; messageIndex++) // 遍历所有图级消息。
                {
                    AppendValidationLine(builder, "Graph", validationMessages[messageIndex]); // 写入图级消息。

                    hasMessages = true; // 标记已经写入至少一条消息。
                }
            }

            if (resourceUsages != null) // Pass 级消息依附于 Usage 列表，列表为空时跳过。
            {
                for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++) // 遍历每个 Pass 的资源使用记录。
                {
                    var usage = resourceUsages[usageIndex]; // 取出当前 Usage。

                    if (usage == null || usage.ValidationMessages == null) // 空 Usage 没有可输出的局部消息。
                    {
                        continue; // 跳过空记录。
                    }

                    for (var messageIndex = 0; messageIndex < usage.ValidationMessages.Count; messageIndex++) // 遍历当前 Pass 的校验消息。
                    {
                        AppendValidationLine(builder, FormatPassLabel(usageIndex, usage), usage.ValidationMessages[messageIndex]); // 写入带 Pass 标签的消息。

                        hasMessages = true; // 标记已经写入至少一条消息。
                    }
                }
            }

            if (!hasMessages) // 如果没有任何校验消息，显式写出 OK。
            {
                builder.AppendLine("  OK"); // 避免读者误以为 Validation 段落被截断。
            }
        }

        private static void AppendValidationLine( // 写入单条校验消息。
            StringBuilder builder, // 接收要写入的字符串构建器。
            string scope, // 接收消息作用域，例如 Graph 或 Pass #0。
            string message) // 接收消息正文。
        {
            builder.Append("  - "); // 使用列表形式，让多条问题更容易扫描。

            builder.Append(string.IsNullOrEmpty(scope) ? "Unknown" : scope); // 写入作用域，方便快速定位。

            builder.Append(": "); // 写入作用域和正文之间的分隔符。

            builder.AppendLine(string.IsNullOrEmpty(message) ? "<empty>" : message); // 写入消息正文；空消息用占位符。
        }

        private static ResourceRiskCounters BuildResourceRiskCounters( // Collects validation counters once so multiple debug sections agree.
            IReadOnlyList<string> validationMessages,
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages)
        {
            var counters = new ResourceRiskCounters();

            AccumulateRiskMessages(validationMessages, counters);

            if (resourceUsages != null)
            {
                for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++)
                {
                    var usage = resourceUsages[usageIndex];
                    if (usage == null)
                    {
                        continue;
                    }

                    AccumulateRiskMessages(usage.ValidationMessages, counters);
                }
            }

            return counters;
        }

        private static void AppendResourceRiskSummary( // Writes compact validation counters for quick graph health checks.
            StringBuilder builder,
            ResourceRiskCounters counters)
        {
            if (counters == null)
            {
                counters = new ResourceRiskCounters();
            }

            builder.AppendLine("Resource Risks:");

            if (counters.Total == 0)
            {
                builder.AppendLine("  OK");
                return;
            }

            builder.Append("  ReadBeforeWrite=");
            builder.Append(counters.ReadBeforeWrite);
            builder.Append(" Unregistered=");
            builder.Append(counters.Unregistered);
            builder.Append(" Missing=");
            builder.Append(counters.Missing);
            builder.Append(" Duplicate=");
            builder.Append(counters.DuplicateDeclarations);
            builder.Append(" SamePassReadWrite=");
            builder.Append(counters.SamePassReadWrite);
            builder.Append(" ReleaseIssues=");
            builder.Append(counters.ReleaseIssues);
            builder.Append(" GlobalState=");
            builder.Append(counters.GlobalStateIssues);
            builder.Append(" Culling=");
            builder.Append(counters.CullingIssues);
            builder.Append(" TerminalWrite=");
            builder.Append(counters.TerminalWriteIssues);
            builder.Append(" NoConsumer=");
            builder.Append(counters.NoConsumer);
            builder.Append(" Other=");
            builder.AppendLine(counters.Other.ToString());
        }

        private static void AccumulateRiskMessages(IReadOnlyList<string> messages, ResourceRiskCounters counters)
        {
            if (messages == null || counters == null)
            {
                return;
            }

            for (var messageIndex = 0; messageIndex < messages.Count; messageIndex++)
            {
                AccumulateRiskMessage(messages[messageIndex], counters);
            }
        }

        private static void AccumulateRiskMessage(string message, ResourceRiskCounters counters)
        {
            if (string.IsNullOrEmpty(message) || counters == null)
            {
                return;
            }

            if (message.IndexOf("Read-before-Write", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.ReadBeforeWrite++;
            }
            else if (message.IndexOf("未注册", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("Unregistered", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.Unregistered++;
            }
            else if (message.IndexOf("缺失", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("Missing", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.Missing++;
            }
            else if (message.IndexOf("重复", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("Duplicate", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.DuplicateDeclarations++;
            }
            else if (message.IndexOf("同时 Read/Write", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.SamePassReadWrite++;
            }
            else if (message.IndexOf("Release", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("释放", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.ReleaseIssues++;
            }
            else if (message.IndexOf("全局状态", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("Global", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.GlobalStateIssues++;
            }
            else if (message.IndexOf("AllowCulling", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("HasSideEffects", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("裁剪", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.CullingIssues++;
            }
            else if (message.IndexOf("AllowUnconsumedWrite", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                     message.IndexOf("终端写入", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.TerminalWriteIssues++;
            }
            else if (message.IndexOf("没有消费者", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                counters.NoConsumer++;
            }
            else
            {
                counters.Other++;
            }
        }

        private static void AppendGraphHealth( // Writes a compact, read-only summary of graph metadata.
            StringBuilder builder,
            int passCount,
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages,
            BurtRenderGraphResourceRegistry resourceRegistry,
            ResourceRiskCounters riskCounters,
            CullCandidateStats cullStats)
        {
            if (riskCounters == null)
            {
                riskCounters = new ResourceRiskCounters();
            }

            if (cullStats == null)
            {
                cullStats = new CullCandidateStats();
            }

            var usageCount = resourceUsages != null ? resourceUsages.Count : 0;
            var nullUsageCount = CountNullUsages(resourceUsages);
            var validationIssueCount = riskCounters.Total;
            var lifetimes = BuildResourceLifetimes(resourceUsages, resourceRegistry);
            var longestLifetimeName = "<none>";
            var longestLifetimeSpan = -1;
            FindLongestLifetime(lifetimes, ref longestLifetimeName, ref longestLifetimeSpan);
            if (longestLifetimeSpan < 0)
            {
                longestLifetimeSpan = 0;
            }

            builder.AppendLine("Graph Health:");
            builder.Append("  Status=");
            builder.Append(riskCounters.Total == 0 && passCount == usageCount && nullUsageCount == 0 ? "OK" : "NeedsAttention");
            builder.Append(" Passes=");
            builder.Append(passCount);
            builder.Append(" Usages=");
            builder.Append(usageCount);
            builder.Append(" NullUsages=");
            builder.Append(nullUsageCount);
            builder.Append(" RegisteredRT=");
            builder.Append(resourceRegistry != null ? CountNames(resourceRegistry.RenderTargetNames) : 0);
            builder.Append(" RegisteredBuffers=");
            builder.Append(resourceRegistry != null ? CountNames(resourceRegistry.BufferNames) : 0);
            builder.Append(" Resources=");
            builder.Append(lifetimes.Count);
            builder.Append(" Longest=");
            builder.Append(longestLifetimeName);
            builder.Append("(Span=");
            builder.Append(longestLifetimeSpan);
            builder.Append(")");
            builder.Append(" ValidationIssues=");
            builder.Append(validationIssueCount);
            builder.Append(" RiskIssues=");
            builder.Append(riskCounters.Total);
            builder.Append(" AllowCulling=");
            builder.Append(cullStats.AllowCullingCount);
            builder.Append(" SideEffectPasses=");
            builder.Append(cullStats.SideEffectCount);
            builder.Append(" CullCandidates=");
            builder.Append(cullStats.Candidates.Count);
            builder.AppendLine(" NonInvasive=True");
        }

        private static int CountNullUsages(IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages)
        {
            if (resourceUsages == null)
            {
                return 0;
            }

            var count = 0;
            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++)
            {
                if (resourceUsages[usageIndex] == null)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountNames(IEnumerable<string> names)
        {
            if (names == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var name in names)
            {
                count++;
            }

            return count;
        }

        private static void FindLongestLifetime(Dictionary<string, ResourceLifetime> lifetimes, ref string longestName, ref int longestSpan)
        {
            if (lifetimes == null)
            {
                return;
            }

            foreach (var pair in lifetimes)
            {
                var lifetime = pair.Value;
                if (lifetime == null || lifetime.FirstPassIndex == int.MaxValue || lifetime.LastPassIndex == int.MinValue)
                {
                    continue;
                }

                var span = lifetime.LastPassIndex - lifetime.FirstPassIndex;
                if (span > longestSpan)
                {
                    longestSpan = span;
                    longestName = lifetime.Name;
                }
            }
        }

        private static CullCandidateStats BuildCullCandidateStats( // Analyzes future pass-culling metadata without changing execution.
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var stats = new CullCandidateStats();
            if (resourceUsages == null)
            {
                return stats;
            }

            var futureReadPasses = BuildFutureReadPasses(resourceUsages);
            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++)
            {
                stats.TotalPasses++;
                var usage = resourceUsages[usageIndex];
                if (usage == null)
                {
                    stats.NullUsageCount++;
                    stats.ReadinessRecords.Add(new CullReadinessRecord("Pass #" + usageIndex, false, new[] { "NullUsage" }, null));
                    continue;
                }

                if (usage.AllowCulling)
                {
                    stats.AllowCullingCount++;
                }
                else
                {
                    stats.BlockedByCullingDisabled++;
                }

                if (usage.HasSideEffects)
                {
                    stats.SideEffectCount++;
                }
                else
                {
                    stats.NoSideEffectCount++;
                }

                if (!usage.AllowCulling || usage.HasSideEffects)
                {
                    stats.NeedsMetadataCount++;
                }

                var producedResources = new List<string>();
                CollectCullProducedResources(producedResources, usage);
                var hasTerminalWrite = HasTerminalWriteMarker(usage);
                var hasExternalWrite = HasExternalProducedResource(producedResources, resourceRegistry);
                var hasFutureConsumer = HasFutureConsumer(producedResources, GetEffectivePassIndex(usageIndex, usage), futureReadPasses);
                var hasDeclaredOutputs = producedResources.Count > 0;
                var reasons = new List<string>();

                if (!usage.AllowCulling)
                {
                    AddUnique(reasons, "CullingDisabled");
                }

                if (usage.HasSideEffects)
                {
                    AddUnique(reasons, "HasSideEffects");
                }

                if (hasFutureConsumer)
                {
                    stats.FeedsFutureReadCount++;
                    AddUnique(reasons, "FeedsFutureRead");
                }
                else if (hasDeclaredOutputs)
                {
                    stats.NoFutureConsumerCount++;
                    AddUnique(reasons, "NoFutureConsumer");
                }

                if (hasExternalWrite)
                {
                    stats.ExternalWriteCount++;
                    AddUnique(reasons, "ExternalWrite");
                }

                if (hasTerminalWrite)
                {
                    stats.TerminalWriteCount++;
                    AddUnique(reasons, "TerminalWrite");
                }

                if (!hasDeclaredOutputs)
                {
                    stats.NoDeclaredOutputsCount++;
                    AddUnique(reasons, "NoDeclaredOutputs");
                }

                var metadataReady = usage.AllowCulling && !usage.HasSideEffects;
                var isCandidate = metadataReady && !hasTerminalWrite && !hasExternalWrite && !hasFutureConsumer;
                if (isCandidate)
                {
                    AddUnique(reasons, hasDeclaredOutputs ? "OutputsHaveNoFutureRead" : "NoDeclaredOutputs");
                }

                stats.ReadinessRecords.Add(new CullReadinessRecord(FormatPassLabel(usageIndex, usage), isCandidate, reasons, producedResources));

                if (usage.AllowCulling && usage.HasSideEffects)
                {
                    stats.BlockedBySideEffects++;
                }

                if (!usage.AllowCulling || usage.HasSideEffects)
                {
                    continue;
                }

                stats.MetadataReadyCount++;

                if (hasTerminalWrite)
                {
                    stats.BlockedByTerminalWrite++;
                    continue;
                }

                if (hasExternalWrite)
                {
                    stats.BlockedByExternalWrite++;
                    continue;
                }

                if (hasFutureConsumer)
                {
                    stats.BlockedByFutureConsumer++;
                    continue;
                }

                var reason = producedResources.Count == 0 ? "NoDeclaredOutputs" : "OutputsHaveNoFutureRead";
                stats.Candidates.Add(new CullCandidate(FormatPassLabel(usageIndex, usage), reason, producedResources));
            }

            return stats;
        }

        private static void AppendCullCandidateSummary(StringBuilder builder, CullCandidateStats stats) // Reports future culling hints only.
        {
            builder.AppendLine("Cull Candidates:");

            if (stats == null || stats.TotalPasses == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            builder.Append("  Metadata: AllowCulling=");
            builder.Append(stats.AllowCullingCount);
            builder.Append(" NoSideEffects=");
            builder.Append(stats.NoSideEffectCount);
            builder.Append(" MetadataReady=");
            builder.Append(stats.MetadataReadyCount);
            builder.Append(" Candidates=");
            builder.Append(stats.Candidates.Count);
            builder.Append(" BlockedByDisabled=");
            builder.Append(stats.BlockedByCullingDisabled);
            builder.Append(" BlockedBySideEffects=");
            builder.Append(stats.BlockedBySideEffects);
            builder.Append(" BlockedByConsumer=");
            builder.Append(stats.BlockedByFutureConsumer);
            builder.Append(" BlockedByExternalWrite=");
            builder.Append(stats.BlockedByExternalWrite);
            builder.Append(" BlockedByTerminalWrite=");
            builder.AppendLine(stats.BlockedByTerminalWrite.ToString());

            if (stats.Candidates.Count == 0)
            {
                if (stats.AllowCullingCount == 0)
                {
                    builder.AppendLine("  <none: all passes currently opt out of culling>");
                }
                else
                {
                    builder.AppendLine("  <none: metadata-ready passes still feed later reads or terminal/external writes>");
                }

                return;
            }

            var lineCount = stats.Candidates.Count < MaxCullCandidateDumpLines ? stats.Candidates.Count : MaxCullCandidateDumpLines;
            for (var candidateIndex = 0; candidateIndex < lineCount; candidateIndex++)
            {
                var candidate = stats.Candidates[candidateIndex];
                builder.Append("  ");
                builder.Append(candidate.PassLabel);
                builder.Append(" Reason=");
                builder.Append(candidate.Reason);
                builder.Append(" Outputs=");
                AppendInlineStringList(builder, candidate.Outputs);
                builder.AppendLine();
            }

            if (stats.Candidates.Count > lineCount)
            {
                builder.Append("  ... ");
                builder.Append(stats.Candidates.Count - lineCount);
                builder.AppendLine(" more");
            }
        }

        private static void AppendCullReadinessSummary(StringBuilder builder, CullCandidateStats stats) // Explains why passes are not cullable yet.
        {
            builder.AppendLine("Cull Readiness:");

            if (stats == null || stats.TotalPasses == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            builder.Append("  Summary: Ready=");
            builder.Append(stats.Candidates.Count);
            builder.Append(" NeedsMetadata=");
            builder.Append(stats.NeedsMetadataCount);
            builder.Append(" CullingDisabled=");
            builder.Append(stats.BlockedByCullingDisabled);
            builder.Append(" SideEffects=");
            builder.Append(stats.SideEffectCount);
            builder.Append(" FeedsFutureRead=");
            builder.Append(stats.FeedsFutureReadCount);
            builder.Append(" NoFutureConsumer=");
            builder.Append(stats.NoFutureConsumerCount);
            builder.Append(" ExternalWrite=");
            builder.Append(stats.ExternalWriteCount);
            builder.Append(" TerminalWrite=");
            builder.Append(stats.TerminalWriteCount);
            builder.Append(" NoDeclaredOutputs=");
            builder.Append(stats.NoDeclaredOutputsCount);
            builder.Append(" NullUsages=");
            builder.AppendLine(stats.NullUsageCount.ToString());

            builder.AppendLine("  Note: Readiness is diagnostic only; RenderGraph still executes every pass.");

            if (stats.ReadinessRecords.Count == 0)
            {
                builder.AppendLine("  <none>");
                return;
            }

            var lineCount = stats.ReadinessRecords.Count < MaxCullReadinessDumpLines ? stats.ReadinessRecords.Count : MaxCullReadinessDumpLines;
            for (var recordIndex = 0; recordIndex < lineCount; recordIndex++)
            {
                AppendCullReadinessRecord(builder, stats.ReadinessRecords[recordIndex]);
            }

            if (stats.ReadinessRecords.Count > lineCount)
            {
                builder.Append("  ... ");
                builder.Append(stats.ReadinessRecords.Count - lineCount);
                builder.AppendLine(" more");
            }
        }

        private static void AppendCullReadinessRecord(StringBuilder builder, CullReadinessRecord record)
        {
            if (record == null)
            {
                return;
            }

            builder.Append("  ");
            builder.Append(record.PassLabel);
            builder.Append(" Ready=");
            builder.Append(record.IsCandidate);
            builder.Append(" Why=");
            AppendInlineStringList(builder, record.Reasons);
            builder.Append(" Outputs=");
            AppendInlineStringList(builder, record.Outputs);
            builder.AppendLine();
        }

        private static Dictionary<string, List<int>> BuildFutureReadPasses(IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages)
        {
            var readPasses = new Dictionary<string, List<int>>();
            if (resourceUsages == null)
            {
                return readPasses;
            }

            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++)
            {
                var usage = resourceUsages[usageIndex];
                if (usage == null)
                {
                    continue;
                }

                var passIndex = GetEffectivePassIndex(usageIndex, usage);
                AddReadPasses(readPasses, usage.RenderTargetAccesses, passIndex);
                AddReadPasses(readPasses, usage.BufferAccesses, passIndex);
                AddReadPasses(readPasses, usage.GlobalResourceAccesses, passIndex);
            }

            return readPasses;
        }

        private static void AddReadPasses(
            Dictionary<string, List<int>> readPasses,
            IReadOnlyList<BurtRenderTargetResourceAccess> accesses,
            int passIndex)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                if (access.AccessType == BurtRenderResourceAccessType.Read)
                {
                    AddReadPass(readPasses, FormatResourceName(access.Handle.Name), passIndex);
                }
            }
        }

        private static void AddReadPasses(
            Dictionary<string, List<int>> readPasses,
            IReadOnlyList<BurtRenderBufferResourceAccess> accesses,
            int passIndex)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                if (access.AccessType == BurtRenderResourceAccessType.Read)
                {
                    AddReadPass(readPasses, FormatResourceName(access.Handle.Name), passIndex);
                }
            }
        }

        private static void AddReadPasses(
            Dictionary<string, List<int>> readPasses,
            IReadOnlyList<BurtGlobalResourceAccess> accesses,
            int passIndex)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                if (access.AccessType == BurtRenderResourceAccessType.Read)
                {
                    AddReadPass(readPasses, FormatResourceName(access.ResourceName), passIndex);
                }
            }
        }

        private static void AddReadPass(Dictionary<string, List<int>> readPasses, string resourceName, int passIndex)
        {
            if (!readPasses.TryGetValue(resourceName, out var passes))
            {
                passes = new List<int>();
                readPasses.Add(resourceName, passes);
            }

            passes.Add(passIndex);
        }

        private static void CollectCullProducedResources(List<string> producedResources, BurtRenderPassResourceUsage usage)
        {
            if (producedResources == null || usage == null)
            {
                return;
            }

            AddCullProducedRenderTargets(producedResources, usage.RenderTargetAccesses);
            AddCullProducedBuffers(producedResources, usage.BufferAccesses);
            AddCullProducedGlobals(producedResources, usage.GlobalResourceAccesses);
        }

        private static void AddCullProducedRenderTargets(List<string> producedResources, IReadOnlyList<BurtRenderTargetResourceAccess> accesses)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                if (IsCullProducingAccess(access.AccessType))
                {
                    AddUnique(producedResources, FormatResourceName(access.Handle.Name));
                }
            }
        }

        private static void AddCullProducedBuffers(List<string> producedResources, IReadOnlyList<BurtRenderBufferResourceAccess> accesses)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                if (IsCullProducingAccess(access.AccessType))
                {
                    AddUnique(producedResources, FormatResourceName(access.Handle.Name));
                }
            }
        }

        private static void AddCullProducedGlobals(List<string> producedResources, IReadOnlyList<BurtGlobalResourceAccess> accesses)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                if (IsCullProducingAccess(access.AccessType))
                {
                    AddUnique(producedResources, FormatResourceName(access.ResourceName));
                }
            }
        }

        private static bool IsCullProducingAccess(BurtRenderResourceAccessType accessType)
        {
            return accessType == BurtRenderResourceAccessType.Write ||
                   accessType == BurtRenderResourceAccessType.Clear ||
                   accessType == BurtRenderResourceAccessType.Copy;
        }

        private static bool HasTerminalWriteMarker(BurtRenderPassResourceUsage usage)
        {
            return usage != null && usage.AllowUnconsumedWriteResources != null && usage.AllowUnconsumedWriteResources.Count > 0;
        }

        private static bool HasExternalProducedResource(IReadOnlyList<string> producedResources, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (producedResources == null || resourceRegistry == null)
            {
                return false;
            }

            for (var resourceIndex = 0; resourceIndex < producedResources.Count; resourceIndex++)
            {
                var resourceName = producedResources[resourceIndex];
                if (resourceRegistry.IsExternalRenderTarget(resourceName) || resourceRegistry.IsExternalBuffer(resourceName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasFutureConsumer(
            IReadOnlyList<string> producedResources,
            int passIndex,
            Dictionary<string, List<int>> readPasses)
        {
            if (producedResources == null || readPasses == null)
            {
                return false;
            }

            for (var resourceIndex = 0; resourceIndex < producedResources.Count; resourceIndex++)
            {
                var resourceName = producedResources[resourceIndex];
                if (!readPasses.TryGetValue(resourceName, out var passes))
                {
                    continue;
                }

                for (var readIndex = 0; readIndex < passes.Count; readIndex++)
                {
                    if (passes[readIndex] > passIndex)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AppendInlineStringList(StringBuilder builder, IReadOnlyList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                builder.Append("<none>");
                return;
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                if (valueIndex > 0)
                {
                    builder.Append(",");
                }

                builder.Append(values[valueIndex]);
            }
        }

        private static void AppendResourceRegistry( // Dumps the resources registered before pass execution.
            StringBuilder builder,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            builder.AppendLine("Resource Registry:");

            if (resourceRegistry == null)
            {
                builder.AppendLine("  <none>");
                return;
            }

            AppendRegistryNameList(builder, "  RenderTargets", resourceRegistry.RenderTargetNames, resourceRegistry, false);
            AppendRegistryNameList(builder, "  Buffers", resourceRegistry.BufferNames, resourceRegistry, true);
            AppendBufferRegistryDetails(builder, resourceRegistry);
        }

        private static void AppendRegistryNameList( // Writes a sorted registry name list with external markers.
            StringBuilder builder,
            string label,
            IEnumerable<string> names,
            BurtRenderGraphResourceRegistry resourceRegistry,
            bool isBuffer)
        {
            builder.Append(label);
            builder.Append(": ");

            var sortedNames = new List<string>();
            if (names != null)
            {
                foreach (var name in names)
                {
                    sortedNames.Add(name);
                }
            }

            if (sortedNames.Count == 0)
            {
                builder.AppendLine("<none>");
                return;
            }

            sortedNames.Sort();

            for (var nameIndex = 0; nameIndex < sortedNames.Count; nameIndex++)
            {
                if (nameIndex > 0)
                {
                    builder.Append(", ");
                }

                var name = sortedNames[nameIndex];
                builder.Append(FormatResourceName(name));

                if (resourceRegistry != null && IsRegistryResourceExternal(resourceRegistry, name, isBuffer))
                {
                    builder.Append("(External)");
                }
            }

            builder.AppendLine();
        }

        private static bool IsRegistryResourceExternal(BurtRenderGraphResourceRegistry resourceRegistry, string name, bool isBuffer)
        {
            return isBuffer ? resourceRegistry.IsExternalBuffer(name) : resourceRegistry.IsExternalRenderTarget(name);
        }

        private static void AppendBufferRegistryDetails(StringBuilder builder, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (resourceRegistry == null)
            {
                return;
            }

            var bufferNames = new List<string>();
            foreach (var name in resourceRegistry.BufferNames)
            {
                bufferNames.Add(name);
            }

            if (bufferNames.Count == 0)
            {
                return;
            }

            bufferNames.Sort();
            for (var nameIndex = 0; nameIndex < bufferNames.Count; nameIndex++)
            {
                var name = bufferNames[nameIndex];
                builder.Append("  Buffer ");
                builder.Append(FormatResourceName(name));
                builder.Append(": Allocated=");
                builder.Append(resourceRegistry.IsBufferAllocated(name));

                if (resourceRegistry.TryGetBufferDescriptor(name, out var descriptor) && descriptor.IsValid)
                {
                    builder.Append(" Count=");
                    builder.Append(descriptor.Count);
                    builder.Append(" Stride=");
                    builder.Append(descriptor.Stride);
                    builder.Append(" Target=");
                    builder.Append(descriptor.Target);
                }
                else if (!resourceRegistry.IsExternalBuffer(name))
                {
                    builder.Append(" Descriptor=<none>");
                }

                if (resourceRegistry.IsExternalBuffer(name))
                {
                    builder.Append(" External=True");
                }

                builder.AppendLine();
            }
        }

        private static void AppendPassUsages( // 写入所有 Pass 的资源使用记录。
            StringBuilder builder, // 接收要写入的字符串构建器。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages) // 接收资源使用记录列表，可能为空。
        {
            if (resourceUsages == null || resourceUsages.Count == 0) // 如果没有资源使用记录，说明图尚未配置或没有有效 Pass。
            {
                builder.AppendLine("  <none>"); // 写入空列表占位，避免 Passes 标题下面什么都没有。

                return; // 没有记录可遍历，直接结束。
            }

            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++) // 按 RenderGraph 收集顺序遍历每条 Pass 资源记录。
            {
                var usage = resourceUsages[usageIndex]; // 取出当前索引对应的资源使用记录。

                AppendPassUsage(builder, usageIndex, usage); // 写入当前 Pass 的名称、读取资源和写入资源。
            }
        }

        private static void AppendPassUsage( // 写入单个 Pass 的资源使用详情。
            StringBuilder builder, // 接收要写入的字符串构建器。
            int usageIndex, // 接收资源使用记录的顺序索引，对应 RenderGraph 配置阶段的 Pass 顺序。
            BurtRenderPassResourceUsage usage) // 接收当前 Pass 的资源使用记录，可能为空。
        {
            builder.Append("  "); // 写入缩进，让 Pass 记录从标题下方缩进显示。

            if (usage == null) // 如果资源使用记录为空，说明配置阶段存在异常条目。
            {
                builder.Append("Pass #"); // 写入 Pass 编号前缀。

                builder.Append(usageIndex); // 写入当前索引，保留定位信息。

                builder.AppendLine(": <null usage>"); // 写入空记录占位，保留索引方便定位问题。

                return; // 空记录没有 Read/Write 列表，直接结束当前 Pass 输出。
            }

            builder.Append(FormatPassLabel(usageIndex, usage)); // 写入统一 Pass 标签，包含 #Index 和 Pass 名称。

            builder.AppendLine(); // Pass 标题独占一行，读写列表更容易扫描。

            AppendPassMetadata(builder, usage); // Show pass classification and future culling metadata next to the pass boundary.

            AppendOptionalStringList(builder, "    Allow Unconsumed Write", usage.AllowUnconsumedWriteResources); // Shows resources intentionally ending as side effects.

            AppendRenderTargetAccessList(builder, "    Access", usage.RenderTargetAccesses); // Writes typed RT accesses, separating Allocate/Bind/Clear/Write/Release.

            AppendBufferAccessList(builder, "    Buffer Access", usage.BufferAccesses); // Writes typed logical buffer accesses for future tiled/cluster passes.

            AppendGlobalResourceAccessList(builder, "    Global Access", usage.GlobalResourceAccesses); // Writes typed global resource accesses.

            AppendRenderTargetList(builder, "    Read", usage.ReadRenderTargets); // 写入当前 Pass 声明读取的 RenderTarget 列表。

            AppendRenderTargetList(builder, "    Write", usage.WriteRenderTargets); // 写入当前 Pass 声明写入的 RenderTarget 列表。

            AppendBufferList(builder, "    Read Buffer", usage.ReadBuffers); // Writes logical buffers read by this pass.

            AppendBufferList(builder, "    Write Buffer", usage.WriteBuffers); // Writes logical buffers written by this pass.

            AppendOptionalStringList(builder, "    Read Global", usage.ReadGlobalResources); // 只在非空时写入当前 Pass 声明读取的逻辑全局资源列表。

            AppendOptionalStringList(builder, "    Write Global", usage.WriteGlobalResources); // 只在非空时写入当前 Pass 声明写入的逻辑全局资源列表。
        }

        private static void AppendPassMetadata(StringBuilder builder, BurtRenderPassResourceUsage usage) // Writes lightweight scheduling metadata for one pass.
        {
            if (usage == null)
            {
                return;
            }

            builder.Append("    Kind=");
            builder.Append(usage.PassKind);
            builder.Append(" HasSideEffects=");
            builder.Append(usage.HasSideEffects);
            builder.Append(" AllowCulling=");
            builder.AppendLine(usage.AllowCulling.ToString());
        }

        private static void AppendRenderTargetAccessList( // Writes typed render target accesses for one pass.
            StringBuilder builder,
            string label,
            IReadOnlyList<BurtRenderTargetResourceAccess> accesses)
        {
            builder.Append(label);
            builder.Append(": ");

            if (accesses == null || accesses.Count == 0)
            {
                builder.AppendLine("<none>");
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                if (accessIndex > 0)
                {
                    builder.Append(", ");
                }

                var access = accesses[accessIndex];
                AppendRenderTarget(builder, access.Handle);
                builder.Append("(");
                builder.Append(access.AccessType);
                builder.Append(")");
            }

            builder.AppendLine();
        }

        private static void AppendBufferAccessList( // Writes typed logical buffer accesses for one pass.
            StringBuilder builder,
            string label,
            IReadOnlyList<BurtRenderBufferResourceAccess> accesses)
        {
            if (accesses == null || accesses.Count == 0)
            {
                return;
            }

            builder.Append(label);
            builder.Append(": ");

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                if (accessIndex > 0)
                {
                    builder.Append(", ");
                }

                var access = accesses[accessIndex];
                AppendBuffer(builder, access.Handle);
                builder.Append("(");
                builder.Append(access.AccessType);
                builder.Append(")");
            }

            builder.AppendLine();
        }

        private static void AppendGlobalResourceAccessList( // Writes typed global resource accesses for one pass.
            StringBuilder builder,
            string label,
            IReadOnlyList<BurtGlobalResourceAccess> accesses)
        {
            if (accesses == null || accesses.Count == 0)
            {
                return;
            }

            builder.Append(label);
            builder.Append(": ");

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                if (accessIndex > 0)
                {
                    builder.Append(", ");
                }

                var access = accesses[accessIndex];
                builder.Append(FormatResourceName(access.ResourceName));
                builder.Append("(");
                builder.Append(access.AccessType);
                builder.Append(")");
            }

            builder.AppendLine();
        }

        private static void AppendRenderTargetList( // 写入一个方向的渲染目标列表，例如 Read 或 Write。
            StringBuilder builder, // 接收要写入的字符串构建器。
            string label, // 接收列表标签，包含缩进，例如 "    Read"。
            IReadOnlyList<BurtRenderTargetHandle> handles) // 接收渲染目标句柄列表，可能为空。
        {
            builder.Append(label); // 写入列表标签，让读资源和写资源分开显示。

            builder.Append(": "); // 写入标签和值之间的分隔符。

            if (handles == null || handles.Count == 0) // 如果列表为空，说明这个 Pass 没有声明该方向的资源依赖。
            {
                builder.AppendLine("<none>"); // 写入空列表标记，避免读者误以为日志被截断。

                return; // 没有句柄可遍历，直接结束当前列表。
            }

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++) // 按声明顺序遍历所有渲染目标句柄。
            {
                if (handleIndex > 0) // 如果不是第一个句柄，就需要在前面补分隔符。
                {
                    builder.Append(", "); // 使用逗号分隔多个资源，让同一方向的资源保持单行显示。
                }

                AppendRenderTarget(builder, handles[handleIndex]); // 写入当前渲染目标的名称和有效性标记。
            }

            builder.AppendLine(); // 当前资源方向写完后换行，下一行输出另一个方向或下一个 Pass。
        }

        private static void AppendBufferList( // Writes a logical buffer list when non-empty to keep dumps compact.
            StringBuilder builder,
            string label,
            IReadOnlyList<BurtRenderBufferHandle> handles)
        {
            if (handles == null || handles.Count == 0)
            {
                return;
            }

            builder.Append(label);
            builder.Append(": ");

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++)
            {
                if (handleIndex > 0)
                {
                    builder.Append(", ");
                }

                AppendBuffer(builder, handles[handleIndex]);
            }

            builder.AppendLine();
        }

        private static void AppendResourceLifetimes( // Writes first/last resource access ranges for the configured graph.
            StringBuilder builder,
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var lifetimes = BuildResourceLifetimes(resourceUsages, resourceRegistry);

            if (lifetimes.Count == 0)
            {
                builder.AppendLine("  <none>");

                return;
            }

            foreach (var pair in lifetimes)
            {
                var lifetime = pair.Value;

                builder.Append("  ");
                builder.Append(lifetime.Name);

                if (lifetime.IsExternal)
                {
                    builder.Append(" (External)");
                }

                if (lifetime.HasMissingDeclaration)
                {
                    builder.Append(" (Missing)");
                }

                builder.Append(": First=#");
                builder.Append(lifetime.FirstPassIndex);
                builder.Append(" Last=#");
                builder.Append(lifetime.LastPassIndex);
                builder.Append(" Span=");
                builder.Append(lifetime.LastPassIndex - lifetime.FirstPassIndex);
                AppendLifetimeAccessCounts(builder, lifetime);
            }
        }

        private static Dictionary<string, ResourceLifetime> BuildResourceLifetimes( // Builds resource lifetime records from pass read/write declarations.
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var lifetimes = new Dictionary<string, ResourceLifetime>();

            if (resourceUsages == null)
            {
                return lifetimes;
            }

            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++)
            {
                var usage = resourceUsages[usageIndex];

                if (usage == null)
                {
                    continue;
                }

                var passIndex = GetEffectivePassIndex(usageIndex, usage);

                AddRenderTargetLifetimeAccesses(lifetimes, usage.RenderTargetAccesses, passIndex, resourceRegistry);
                AddBufferLifetimeAccesses(lifetimes, usage.BufferAccesses, passIndex, resourceRegistry);
                AddGlobalResourceLifetimeAccesses(lifetimes, usage.GlobalResourceAccesses, passIndex);
            }

            return lifetimes;
        }

        private static void AddRenderTargetLifetimeAccesses( // Adds typed render target accesses into resource lifetime records.
            Dictionary<string, ResourceLifetime> lifetimes,
            IReadOnlyList<BurtRenderTargetResourceAccess> accesses,
            int passIndex,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var handle = access.Handle;
                var lifetime = GetOrCreateLifetime(lifetimes, FormatResourceName(handle.Name));

                lifetime.RecordAccess(passIndex, access.AccessType);

                if (resourceRegistry != null && resourceRegistry.IsExternalRenderTarget(handle.Name))
                {
                    lifetime.IsExternal = true;
                }

                if (!handle.IsValid)
                {
                    lifetime.HasMissingDeclaration = true;
                }
            }
        }

        private static void AddBufferLifetimeAccesses( // Adds typed logical buffer accesses into resource lifetime records.
            Dictionary<string, ResourceLifetime> lifetimes,
            IReadOnlyList<BurtRenderBufferResourceAccess> accesses,
            int passIndex,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var handle = access.Handle;
                var lifetime = GetOrCreateLifetime(lifetimes, FormatResourceName(handle.Name));

                lifetime.RecordAccess(passIndex, access.AccessType);

                if (resourceRegistry != null && resourceRegistry.IsExternalBuffer(handle.Name))
                {
                    lifetime.IsExternal = true;
                }

                if (!handle.IsValid)
                {
                    lifetime.HasMissingDeclaration = true;
                }
            }
        }

        private static void AddGlobalResourceLifetimeAccesses( // Adds typed global resource accesses into lifetime records.
            Dictionary<string, ResourceLifetime> lifetimes,
            IReadOnlyList<BurtGlobalResourceAccess> accesses,
            int passIndex)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var lifetime = GetOrCreateLifetime(lifetimes, FormatResourceName(access.ResourceName));

                lifetime.RecordAccess(passIndex, access.AccessType);
            }
        }

        private static void AppendLifetimeAccessCounts(StringBuilder builder, ResourceLifetime lifetime) // Appends compact typed access counters for a lifetime line.
        {
            builder.Append(" Accesses=");

            var hasAny = false;
            AppendAccessCount(builder, "Allocate", lifetime.AllocateCount, ref hasAny);
            AppendAccessCount(builder, "Bind", lifetime.BindCount, ref hasAny);
            AppendAccessCount(builder, "Clear", lifetime.ClearCount, ref hasAny);
            AppendAccessCount(builder, "Write", lifetime.WriteCount, ref hasAny);
            AppendAccessCount(builder, "Copy", lifetime.CopyCount, ref hasAny);
            AppendAccessCount(builder, "Read", lifetime.ReadCount, ref hasAny);
            AppendAccessCount(builder, "Release", lifetime.ReleaseCount, ref hasAny);

            if (!hasAny)
            {
                builder.Append("<none>");
            }

            builder.AppendLine();
        }

        private static void AppendAccessCount(StringBuilder builder, string label, int count, ref bool hasAny)
        {
            if (count <= 0)
            {
                return;
            }

            if (hasAny)
            {
                builder.Append(" ");
            }

            builder.Append(label);
            builder.Append(":");
            builder.Append(count);
            hasAny = true;
        }

        private static ResourceLifetime GetOrCreateLifetime( // Fetches or creates a lifetime record by resource name.
            Dictionary<string, ResourceLifetime> lifetimes,
            string resourceName)
        {
            if (!lifetimes.TryGetValue(resourceName, out var lifetime))
            {
                lifetime = new ResourceLifetime(resourceName);
                lifetimes.Add(resourceName, lifetime);
            }

            return lifetime;
        }

        private static void AppendResourceSummary( // 从资源视角写入生产者、消费者和缺失提示。
            StringBuilder builder, // 接收要写入的字符串构建器。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages, // 接收 Pass 资源使用记录。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收资源注册表，用于判断外部资源。
        {
            var summaries = BuildResourceSummaries(resourceUsages, resourceRegistry); // 汇总每个资源的读写关系。

            if (summaries.Count == 0) // 没有任何资源声明时输出占位。
            {
                builder.AppendLine("  <none>"); // 避免 Resources 标题下方为空。

                return; // 没有资源可输出。
            }

            foreach (var pair in summaries) // 遍历每个资源摘要。
            {
                var summary = pair.Value; // 取出当前资源摘要对象。

                builder.Append("  "); // 写入资源行缩进。

                builder.Append(summary.Name); // 写入资源名。

                if (summary.HasMissingDeclaration) // 如果有无效句柄，提示资源注册缺失。
                {
                    builder.Append(" (Missing)"); // 缺失资源在资源摘要里醒目标记。
                }

                if (summary.IsExternal) // 外部导入资源单独标记，避免误以为没有图内生产者就是错误。
                {
                    builder.Append(" (External)"); // 标记资源来自相机或外部系统。
                }

                builder.AppendLine(); // 结束资源标题行。

                AppendOptionalStringList(builder, "    Allocate", summary.Allocators);

                AppendOptionalStringList(builder, "    Bind", summary.Binders);

                AppendOptionalStringList(builder, "    Clear", summary.Clearers);

                AppendOptionalStringList(builder, "    Write", summary.Writers);

                AppendOptionalStringList(builder, "    Copy", summary.Copiers);

                AppendOptionalStringList(builder, "    Read", summary.Readers);

                AppendOptionalStringList(builder, "    Allow Unconsumed Write", summary.AllowedUnconsumedWriters);

                AppendOptionalStringList(builder, "    Release", summary.Releasers);

                if (summary.HasMissingDeclaration) // 缺失资源给出额外提示。
                {
                    builder.AppendLine("    Hint: 检查资源注册名称，或确认该资源是否应在 ImportRequestResources 中注册。"); // 提示下一步排查方向。
                }
            }
        }

        private static Dictionary<string, ResourceSummary> BuildResourceSummaries( // 构建资源摘要表。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages, // 接收资源使用记录。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收资源注册表，用于判断外部资源。
        {
            var summaries = new Dictionary<string, ResourceSummary>(); // 用资源名聚合生产者和消费者。

            if (resourceUsages == null) // 没有资源使用记录时直接返回空表。
            {
                return summaries; // 返回空摘要。
            }

            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++) // 遍历每个 Pass 的资源声明。
            {
                var usage = resourceUsages[usageIndex]; // 取出当前 Usage。

                if (usage == null) // 空记录无法提供资源关系。
                {
                    continue; // 跳过空记录。
                }

                AddResourceAccesses(summaries, usage.RenderTargetAccesses, FormatPassLabel(usageIndex, usage), resourceRegistry); // Records typed RT accesses.

                AddBufferResourceAccesses(summaries, usage.BufferAccesses, FormatPassLabel(usageIndex, usage), resourceRegistry); // Records typed logical buffer accesses.

                AddGlobalResourceAccesses(summaries, usage.GlobalResourceAccesses, FormatPassLabel(usageIndex, usage)); // Records typed global resource accesses.

                AddTerminalWriteResources(summaries, usage.AllowUnconsumedWriteResources, FormatPassLabel(usageIndex, usage)); // Records explicit terminal side-effect writes.
            }

            return summaries; // 返回完整资源摘要。
        }

        private static void AddResourceAccesses( // Adds typed render target accesses to resource summaries.
            Dictionary<string, ResourceSummary> summaries,
            IReadOnlyList<BurtRenderTargetResourceAccess> accesses,
            string passLabel,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var handle = access.Handle;
                var summary = GetOrCreateSummary(summaries, FormatResourceName(handle.Name));

                if (resourceRegistry != null && resourceRegistry.IsExternalRenderTarget(handle.Name))
                {
                    summary.IsExternal = true;
                }

                if (!handle.IsValid)
                {
                    summary.HasMissingDeclaration = true;
                }

                AddTypedAccess(summary, passLabel, access.AccessType);
            }
        }

        private static void AddBufferResourceAccesses( // Adds typed logical buffer accesses to resource summaries.
            Dictionary<string, ResourceSummary> summaries,
            IReadOnlyList<BurtRenderBufferResourceAccess> accesses,
            string passLabel,
            BurtRenderGraphResourceRegistry resourceRegistry)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var handle = access.Handle;
                var summary = GetOrCreateSummary(summaries, FormatResourceName(handle.Name));

                if (resourceRegistry != null && resourceRegistry.IsExternalBuffer(handle.Name))
                {
                    summary.IsExternal = true;
                }

                if (!handle.IsValid)
                {
                    summary.HasMissingDeclaration = true;
                }

                AddTypedAccess(summary, passLabel, access.AccessType);
            }
        }

        private static void AddGlobalResourceAccesses( // Adds typed global resource accesses to resource summaries.
            Dictionary<string, ResourceSummary> summaries,
            IReadOnlyList<BurtGlobalResourceAccess> accesses,
            string passLabel)
        {
            if (accesses == null)
            {
                return;
            }

            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var summary = GetOrCreateSummary(summaries, FormatResourceName(access.ResourceName));

                AddTypedAccess(summary, passLabel, access.AccessType);
            }
        }

        private static void AddTerminalWriteResources( // Adds explicit terminal-write markers to resource summaries.
            Dictionary<string, ResourceSummary> summaries,
            IReadOnlyList<string> resourceNames,
            string passLabel)
        {
            if (resourceNames == null)
            {
                return;
            }

            for (var resourceIndex = 0; resourceIndex < resourceNames.Count; resourceIndex++)
            {
                var resourceName = resourceNames[resourceIndex];
                if (string.IsNullOrEmpty(resourceName))
                {
                    continue;
                }

                var summary = GetOrCreateSummary(summaries, FormatResourceName(resourceName));
                AddUnique(summary.AllowedUnconsumedWriters, passLabel);
            }
        }

        private static ResourceSummary GetOrCreateSummary(Dictionary<string, ResourceSummary> summaries, string resourceName)
        {
            if (!summaries.TryGetValue(resourceName, out var summary))
            {
                summary = new ResourceSummary(resourceName);
                summaries.Add(resourceName, summary);
            }

            return summary;
        }

        private static void AddTypedAccess(ResourceSummary summary, string passLabel, BurtRenderResourceAccessType accessType)
        {
            switch (accessType)
            {
                case BurtRenderResourceAccessType.Allocate:
                    AddUnique(summary.Allocators, passLabel);
                    break;
                case BurtRenderResourceAccessType.Bind:
                    AddUnique(summary.Binders, passLabel);
                    break;
                case BurtRenderResourceAccessType.Clear:
                    AddUnique(summary.Clearers, passLabel);
                    break;
                case BurtRenderResourceAccessType.Copy:
                    AddUnique(summary.Copiers, passLabel);
                    break;
                case BurtRenderResourceAccessType.Read:
                    AddUnique(summary.Readers, passLabel);
                    break;
                case BurtRenderResourceAccessType.Release:
                    AddUnique(summary.Releasers, passLabel);
                    break;
                default:
                    AddUnique(summary.Writers, passLabel);
                    break;
            }
        }

        private static void AppendStringList( // 写入字符串列表。
            StringBuilder builder, // 接收要写入的字符串构建器。
            string label, // 接收列表标签。
            IReadOnlyList<string> values) // 接收要写入的值列表。
        {
            builder.Append(label); // 写入标签。

            builder.Append(": "); // 写入分隔符。

            if (values == null || values.Count == 0) // 如果列表为空就写占位。
            {
                builder.AppendLine("<none>"); // 明确表示没有生产者或消费者。

                return; // 结束当前列表。
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++) // 遍历所有值。
            {
                if (valueIndex > 0) // 多个值之间用逗号分隔。
                {
                    builder.Append(", "); // 写入分隔符。
                }

                builder.Append(values[valueIndex]); // 写入当前值。
            }

            builder.AppendLine(); // 当前列表结束后换行。
        }

        private static void AppendOptionalStringList( // 按需写入字符串列表，避免每个 Pass 都打印空的全局资源行。
            StringBuilder builder, // 接收要写入的字符串构建器。
            string label, // 接收列表标签。
            IReadOnlyList<string> values) // 接收要写入的值列表。
        {
            if (values == null || values.Count == 0) // 如果列表为空，说明这个 Pass 没有声明对应逻辑全局资源。
            {
                return; // 直接跳过，保持 RenderGraph Debug 输出紧凑。
            }

            AppendStringList(builder, label, values); // 列表非空时复用普通字符串列表输出函数。
        }

        private static void AppendRenderTarget( // 写入单个渲染目标句柄的可读文本。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderTargetHandle handle) // 接收 RenderGraph 资源句柄，里面包含逻辑名称和有效性。
        {
            builder.Append(FormatResourceName(handle.Name)); // 写入资源逻辑名称，例如 CameraColor、CameraDepth 或 MainLightShadowMap。

            if (!handle.IsValid) // 如果句柄无效，说明 Pass 声明了一个资源表里没有的目标。
            {
                builder.Append(" (Invalid/Missing)"); // 在资源名后标记缺失，让资源注册问题在 dump 中非常醒目。
            }
        }

        private static void AppendBuffer( // Writes one logical buffer handle.
            StringBuilder builder,
            BurtRenderBufferHandle handle)
        {
            builder.Append(FormatResourceName(handle.Name));

            if (!handle.IsValid)
            {
                builder.Append(" (Invalid/Missing)");
            }
        }

        private static int GetEffectivePassIndex(int usageIndex, BurtRenderPassResourceUsage usage) // Returns the real graph pass index when available.
        {
            return usage != null && usage.PassIndex >= 0 ? usage.PassIndex : usageIndex;
        }

        private static string FormatPassLabel( // 生成统一 Pass 标签。
            int usageIndex, // 接收列表索引，作为缺省 Pass Index。
            BurtRenderPassResourceUsage usage) // 接收资源使用记录，可能为空。
        {
            var passIndex = GetEffectivePassIndex(usageIndex, usage); // Prefer the real RenderGraph pass index when available.

            var passName = usage != null && !string.IsNullOrEmpty(usage.PassName) ? usage.PassName : "<unnamed pass>"; // 读取 Pass 名称，缺失时用占位符。

            return "Pass #" + passIndex + " " + passName; // 输出包含 Pass Index 和名称的标签。
        }

        private static string FormatResourceName(string resourceName) // 把资源名转换为可读文本。
        {
            return string.IsNullOrEmpty(resourceName) ? "<unnamed target>" : resourceName; // 空名使用占位符，方便发现声明问题。
        }

        private static void AddUnique( // 向列表追加唯一字符串。
            List<string> values, // 接收要写入的列表。
            string value) // 接收要追加的值。
        {
            if (values.Contains(value)) // 如果已经存在相同值就跳过。
            {
                return; // 避免同一个 Pass 重复声明导致摘要刷屏。
            }

            values.Add(value); // 追加新值。
        }

        private sealed class ResourceLifetime // Resource lifetime span used by debug dump only.
        {
            public readonly string Name;

            public int FirstPassIndex = int.MaxValue;

            public int LastPassIndex = int.MinValue;

            public int AllocateCount;

            public int BindCount;

            public int ClearCount;

            public int ReadCount;

            public int WriteCount;

            public int CopyCount;

            public int ReleaseCount;

            public bool HasMissingDeclaration;

            public bool IsExternal;

            public ResourceLifetime(string name)
            {
                Name = name;
            }

            public void RecordAccess(int passIndex, BurtRenderResourceAccessType accessType)
            {
                if (passIndex < FirstPassIndex)
                {
                    FirstPassIndex = passIndex;
                }

                if (passIndex > LastPassIndex)
                {
                    LastPassIndex = passIndex;
                }

                switch (accessType)
                {
                    case BurtRenderResourceAccessType.Allocate:
                        AllocateCount++;
                        break;
                    case BurtRenderResourceAccessType.Bind:
                        BindCount++;
                        break;
                    case BurtRenderResourceAccessType.Clear:
                        ClearCount++;
                        break;
                    case BurtRenderResourceAccessType.Copy:
                        CopyCount++;
                        break;
                    case BurtRenderResourceAccessType.Read:
                        ReadCount++;
                        break;
                    case BurtRenderResourceAccessType.Release:
                        ReleaseCount++;
                        break;
                    default:
                        WriteCount++;
                        break;
                }
            }
        }

        private sealed class ResourceRiskCounters // Compact validation counters for the Resource Risks section.
        {
            public int ReadBeforeWrite;

            public int Unregistered;

            public int Missing;

            public int DuplicateDeclarations;

            public int SamePassReadWrite;

            public int ReleaseIssues;

            public int GlobalStateIssues;

            public int CullingIssues;

            public int TerminalWriteIssues;

            public int NoConsumer;

            public int Other;

            public int Total => ReadBeforeWrite + Unregistered + Missing + DuplicateDeclarations + SamePassReadWrite + ReleaseIssues + GlobalStateIssues + CullingIssues + TerminalWriteIssues + NoConsumer + Other;
        }

        private sealed class CullCandidateStats // Debug-only summary for future pass-culling metadata.
        {
            public int TotalPasses;

            public int NullUsageCount;

            public int AllowCullingCount;

            public int SideEffectCount;

            public int NoSideEffectCount;

            public int MetadataReadyCount;

            public int NeedsMetadataCount;

            public int BlockedByCullingDisabled;

            public int BlockedBySideEffects;

            public int BlockedByFutureConsumer;

            public int BlockedByExternalWrite;

            public int BlockedByTerminalWrite;

            public int FeedsFutureReadCount;

            public int NoFutureConsumerCount;

            public int ExternalWriteCount;

            public int TerminalWriteCount;

            public int NoDeclaredOutputsCount;

            public readonly List<CullCandidate> Candidates = new List<CullCandidate>();

            public readonly List<CullReadinessRecord> ReadinessRecords = new List<CullReadinessRecord>();
        }

        private sealed class CullCandidate // One diagnostic-only future culling candidate.
        {
            public readonly string PassLabel;

            public readonly string Reason;

            public readonly List<string> Outputs = new List<string>();

            public CullCandidate(string passLabel, string reason, IReadOnlyList<string> outputs)
            {
                PassLabel = string.IsNullOrEmpty(passLabel) ? "<unnamed pass>" : passLabel;
                Reason = string.IsNullOrEmpty(reason) ? "<none>" : reason;

                if (outputs == null)
                {
                    return;
                }

                for (var outputIndex = 0; outputIndex < outputs.Count; outputIndex++)
                {
                    Outputs.Add(outputs[outputIndex]);
                }
            }
        }

        private sealed class CullReadinessRecord // One diagnostic row explaining current culling blockers for a pass.
        {
            public readonly string PassLabel;

            public readonly bool IsCandidate;

            public readonly List<string> Reasons = new List<string>();

            public readonly List<string> Outputs = new List<string>();

            public CullReadinessRecord(string passLabel, bool isCandidate, IReadOnlyList<string> reasons, IReadOnlyList<string> outputs)
            {
                PassLabel = string.IsNullOrEmpty(passLabel) ? "<unnamed pass>" : passLabel;
                IsCandidate = isCandidate;
                CopyStrings(reasons, Reasons);
                CopyStrings(outputs, Outputs);
            }

            private static void CopyStrings(IReadOnlyList<string> source, List<string> destination)
            {
                if (source == null || destination == null)
                {
                    return;
                }

                for (var valueIndex = 0; valueIndex < source.Count; valueIndex++)
                {
                    if (!string.IsNullOrEmpty(source[valueIndex]))
                    {
                        destination.Add(source[valueIndex]);
                    }
                }
            }
        }

        private sealed class ResourceSummary // 定义资源视角的调试摘要，只在 DebugUtility 内部使用。
        {
            public readonly string Name; // 保存资源名。

            public readonly List<string> Allocators = new List<string>(); // Passes that allocate this resource.

            public readonly List<string> Binders = new List<string>(); // Passes that bind this resource as a target only.

            public readonly List<string> Clearers = new List<string>(); // Passes that clear this resource.

            public readonly List<string> Writers = new List<string>(); // Passes that write this resource content.

            public readonly List<string> Copiers = new List<string>(); // Passes that copy into this resource.

            public readonly List<string> Readers = new List<string>(); // Passes that read this resource.

            public readonly List<string> AllowedUnconsumedWriters = new List<string>(); // Passes that intentionally leave this write without a consumer.

            public readonly List<string> Releasers = new List<string>(); // Passes that release this resource.

            public bool HasMissingDeclaration; // 标记是否有无效句柄引用该资源。

            public bool IsExternal; // 标记资源是否来自 RenderGraph 外部。

            public ResourceSummary(string name) // 定义构造函数。
            {
                Name = name; // 保存资源名。
            }
        }
    }
}
