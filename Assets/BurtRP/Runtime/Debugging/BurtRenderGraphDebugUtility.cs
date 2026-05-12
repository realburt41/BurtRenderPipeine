using System.Collections.Generic; // 引入泛型集合命名空间，用 IReadOnlyList、Dictionary 和 List 组织 Pass 与资源关系。
using System.Text; // 引入文本构建命名空间，用 StringBuilder 组合多行 RenderGraph dump。
using UnityEngine; // 引入 UnityEngine 命名空间，用 Camera、RenderTexture 和 RenderTextureDescriptor 输出 RT 诊断状态。

namespace Burt.RenderPipeline // 定义 BurtRP 的运行时命名空间，让工具能直接访问 BurtRenderRequest 和资源使用类型。
{
    internal static class BurtRenderGraphDebugUtility // 定义 RenderGraph dump 格式化工具，把日志排版细节从 BurtRenderGraph 执行类中拆出来。
    {
        private const int BaseDumpCapacity = 768; // 定义 dump 基础容量，覆盖标题、Request、Camera、校验和资源摘要等固定内容。

        private const int PerPassDumpCapacity = 220; // 定义每个 Pass 的估算容量，减少多 Pass 场景下 StringBuilder 扩容次数。

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

            var pipelineStateCapacity = 1650; // Pipeline/Camera/RT/PostProcess/Deferred/Material 状态会额外打印多行诊断信息，预留固定容量。

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

            AppendPipelineState(builder, request, asset); // 写入管线和调试状态，方便对齐 Forward / Deferred 时确认当前到底由哪个开关驱动画面。

            AppendCameraState(builder, request); // 写入当前相机的 HDR、尺寸和 targetTexture 状态，用来排查 Game/Scene/Preview 差异。

            AppendRenderTargetState(builder, request, asset, resourceRegistry); // 写入 CameraColor、CameraDepth、PostProcessColor、GBuffer 和最终目标的格式状态。

            AppendPostProcessState(builder, request, asset); // 写入后处理框架、Volume Tonemapping 和 Color Adjustments 的运行状态。

            AppendDeferredState(builder, request, asset, resourceRegistry); // 写入 Deferred GBuffer 注册和调试模式状态，方便确认 Deferred 分支是否真的生效。

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
            BurtRenderPipelineAsset asset) // 接收当前管线资产，可能为空。
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

            builder.AppendLine(); // 结束第二行调试状态。

            builder.Append("  DepthDebugView=").Append(asset.EnableDepthDebugView); // 写入 CameraDepth 全屏调试开关。

            builder.Append(" MainLightShadowDebugView=").Append(asset.EnableMainLightShadowDebugView); // 写入主光 shadow map 全屏调试开关。

            builder.Append(" MainLightShadowDebugLog=").Append(asset.EnableMainLightShadowDebugLog); // 写入主光阴影结构化日志开关。

            builder.Append(" UnsupportedShaderDebug=").Append(asset.EnableUnsupportedShaderDebug); // 写入错误材质绘制开关。

            builder.Append(" HiZDebugView=").Append(asset.EnableHiZDebugView);

            builder.Append(" HiZDebugMip=").Append(asset.HiZDebugMip);

            builder.Append(" HiZDebugScale=").Append(asset.HiZDebugScale.ToString("0.###"));

            builder.AppendLine(); // 结束第三行全屏调试状态。

            var ssrSettings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(request, asset);
            var ssrSuppressedByShadingDebug = BurtScreenSpaceReflectionPassUtility.IsScreenSpaceReflectionSuppressedByShadingDebug();
            var ssrHistory = BurtScreenSpaceReflectionHistoryUtility.GetHistoryStatus(request != null ? request.Camera : null);

            builder.Append("  SSREnabled=").Append(ssrSettings.Enabled);

            builder.Append(" SSRSuppressedByShadingDebug=").Append(ssrSuppressedByShadingDebug);

            builder.Append(" SSRDebugMode=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionDebugModeLabel());

            builder.Append(" SSRMaxSteps=").Append(ssrSettings.MaxSteps);

            builder.Append(" SSRMaxDistance=").Append(ssrSettings.MaxDistance.ToString("0.###"));

            builder.Append(" SSRThickness=").Append(ssrSettings.Thickness.ToString("0.###"));

            builder.Append(" SSRIntensity=").Append(ssrSettings.Intensity.ToString("0.###"));

            builder.Append(" SSRRoughnessFade=").Append(ssrSettings.RoughnessFade.ToString("0.###"));

            builder.Append(" SSRTemporal=").Append(ssrSettings.TemporalAccumulation);

            builder.Append(" SSRTemporalFeedback=").Append(ssrSettings.TemporalFeedback.ToString("0.###"));

            builder.Append(" SSRHistoryValid=").Append(ssrHistory.HasHistory);

            builder.Append(" SSRHistoryAllocated=").Append(ssrHistory.HasHistory || ssrHistory.HasDepthHistory);

            builder.Append(" SSRHistoryMatches=").Append(ssrHistory.DescriptorMatches);

            builder.Append(" SSRDepthHistoryAllocated=").Append(ssrHistory.HasDepthHistory);

            builder.Append(" SSRDepthHistoryMatches=").Append(ssrHistory.DepthDescriptorMatches);

            builder.Append(" SSRHistoryAge=").Append(ssrHistory.HistoryAge);

            builder.Append(" SSRFrame=").Append(ssrHistory.FrameIndex);

            builder.Append(" SSRHistoryReason=").Append(ssrHistory.LastInvalidationReason);

            builder.AppendLine();

            BurtIndirectLightingUtility.AppendDebugState(builder, request != null ? request.Camera : null); // 写入 BurtRP 全局间接光数据源状态，方便确认 Deferred 不再依赖 Forward DrawRenderers 副作用。
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

            if (BurtPostProcessUtility.ShouldUsePostProcessFramework(request, asset)) // 只有当前 request 实际会使用后处理框架时，才输出 PostProcessColor 描述。
            {
                AppendDescriptorLine(builder, "PostProcessColor", BurtRenderTargetDescriptorUtility.CreatePostProcessColorDescriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.PostProcessColorName); // 输出后处理 ping-pong RT 描述。
            }
            else // 当前 request 不执行后处理框架。
            {
                AppendSkippedRenderTargetLine(builder, "PostProcessColor", resourceRegistry, BurtRenderGraphResourceRegistry.PostProcessColorName); // 写出跳过状态，方便确认不是资源丢失。
            }

            if (IsDeferredRequest(request, asset)) // 当前 request 真正走 Deferred 时才会申请 GBuffer。
            {
                AppendDescriptorLine(builder, "GBuffer0", BurtRenderTargetDescriptorUtility.CreateGBuffer0Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer0Name); // 输出 GBuffer0 格式，第一版保存 baseColor/occlusion。

                AppendDescriptorLine(builder, "GBuffer1", BurtRenderTargetDescriptorUtility.CreateGBuffer1Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer1Name); // 输出 GBuffer1 格式，当前保存 direction/material-channel/smoothness。

                AppendDescriptorLine(builder, "GBuffer2", BurtRenderTargetDescriptorUtility.CreateGBuffer2Descriptor(camera), resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer2Name); // 输出 GBuffer2 格式，第一版保存 emission/reflectance。

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
            }
            else // Forward 模式不注册 GBuffer。
            {
                AppendSkippedRenderTargetLine(builder, "GBuffer0", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer0Name); // 写出 GBuffer0 跳过状态。

                AppendSkippedRenderTargetLine(builder, "GBuffer1", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer1Name); // 写出 GBuffer1 跳过状态。

                AppendSkippedRenderTargetLine(builder, "GBuffer2", resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer2Name); // 写出 GBuffer2 跳过状态。

                AppendSkippedRenderTargetLine(builder, "HiZDepth", resourceRegistry, BurtRenderGraphResourceRegistry.HiZDepthName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionDenoisedColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName);

                AppendSkippedRenderTargetLine(builder, "ScreenSpaceReflectionTemporalColor", resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName);
            }

            builder.Append("  FinalCameraTarget Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.FinalCameraTargetName)); // 写入最终输出目标是否已注册。

            builder.Append(" Identifier=").Append(request != null ? request.TargetIdentifier.ToString() : "<none>"); // 写入最终目标 RenderTargetIdentifier，定位 CameraTarget 或具体 RenderTexture。

            builder.AppendLine(); // 结束最终输出目标行。
        }

        private static void AppendPostProcessState( // 写入后处理框架和 Volume 效果状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断后处理是否应该执行。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来读取后处理框架设置。
        {
            builder.AppendLine("PostProcess State:"); // 单独成段输出，方便排查 No-op、Tonemapping 和 Color Adjustments。

            var settings = asset != null ? asset.PostProcessSettings : null; // 从资产安全读取后处理框架设置。

            var assetEnabled = settings != null && settings.EnablePostProcessing; // 判断资产上的后处理总开关。

            var noOpCopy = settings != null && settings.EnableNoOpCopy; // 判断资产上的 No-op Copy 验证开关。

            var shouldRunFramework = BurtPostProcessUtility.ShouldUsePostProcessFramework(request, asset); // 解析当前 request 最终是否会插入后处理链。

            var suppressedByShadingDebug = BurtPostProcessUtility.IsPostProcessSuppressedByShadingDebug();

            var tonemappingMode = BurtPostProcessUtility.ResolveTonemappingMode(asset); // 解析当前 Volume 中真正生效的 Tonemapping 模式。

            var postExposureMultiplier = BurtPostProcessUtility.ResolvePostExposureMultiplier(asset); // 解析当前 Volume 中 Tonemapping 前曝光倍率。

            var useColorAdjustments = BurtPostProcessUtility.ShouldUseColorAdjustments(request, asset); // 判断当前 Volume 是否启用基础颜色调整。

            var bloomSettings = BurtPostProcessUtility.ResolveBloomSettings(asset); // 解析当前 Volume 中真正生效的 Bloom 参数。

            var bloomEnabled = BurtPostProcessUtility.ShouldUseBloom(request, asset); // 判断当前 request 是否启用 Bloom。

            var bloomMipCount = BurtPostProcessUtility.ResolveBloomMipCount(request, asset); // 解析当前 request 实际会使用的 Bloom mip 数。

            var temporalAA = request != null ? request.TemporalAA : null;
            var temporalAASettings = BurtPostProcessUtility.ResolveTemporalAASettings(request, asset);
            var temporalHistory = BurtTemporalAAUtility.GetHistoryStatus(request != null ? request.Camera : null);

            builder.Append("  AssetEnabled=").Append(assetEnabled); // 写入后处理总开关。

            builder.Append(" NoOpCopy=").Append(noOpCopy); // 写入 No-op Copy 开关。

            builder.Append(" ShouldRunFramework=").Append(shouldRunFramework); // 写入当前 request 是否真正执行后处理链。

            builder.Append(" SuppressedByShadingDebug=").Append(suppressedByShadingDebug);

            builder.Append(" Tonemapping=").Append(tonemappingMode); // 写入 Volume 解析后的 Tonemapping 模式。

            builder.Append(" PostExposureMul=").Append(postExposureMultiplier.ToString("0.###")); // 写入 EV 转换后的线性曝光倍率。

            builder.Append(" ColorAdjustments=").Append(useColorAdjustments); // 写入是否启用颜色调整。

            builder.Append(" BloomEnabled=").Append(bloomEnabled); // 写入是否启用 Bloom。

            builder.Append(" BloomMips=").Append(bloomMipCount); // 写入当前 request 实际使用的 Bloom mip 数。

            builder.Append(" BloomThreshold=").Append(bloomSettings.Threshold.ToString("0.###")); // 写入 Bloom 阈值。

            builder.Append(" BloomIntensity=").Append(bloomSettings.Intensity.ToString("0.###")); // 写入 Bloom 强度。

            builder.Append(" BloomScatter=").Append(bloomSettings.Scatter.ToString("0.###")); // 写入 Bloom 散布。

            builder.Append(" TAAEnabled=").Append(temporalAA != null && temporalAA.Enabled);
            builder.Append(" TAAHistoryValid=").Append(temporalAA != null && temporalAA.HistoryValid);
            builder.Append(" TAAHistoryAllocated=").Append(temporalHistory.HasHistory);
            builder.Append(" TAAHistoryMatches=").Append(temporalHistory.DescriptorMatches);
            builder.Append(" TAADepthHistoryAllocated=").Append(temporalHistory.HasDepthHistory);
            builder.Append(" TAADepthHistoryMatches=").Append(temporalHistory.DepthDescriptorMatches);
            builder.Append(" TAAConfidenceHistoryAllocated=").Append(temporalHistory.HasConfidenceHistory);
            builder.Append(" TAAConfidenceHistoryMatches=").Append(temporalHistory.ConfidenceDescriptorMatches);
            builder.Append(" TAAAntiFlickerHistoryAllocated=").Append(temporalHistory.HasAntiFlickerHistory);
            builder.Append(" TAAAntiFlickerHistoryMatches=").Append(temporalHistory.AntiFlickerDescriptorMatches);
            builder.Append(" TAAHistoryAge=").Append(temporalHistory.HistoryAge);
            builder.Append(" TAAFrame=").Append(temporalAA != null ? temporalAA.FrameIndex.ToString() : temporalHistory.FrameIndex.ToString());
            builder.Append(" TAAHistoryReason=").Append(temporalHistory.LastInvalidationReason);
            builder.Append(" TAAJitter=").Append(temporalAA != null ? temporalAA.JitterPixels.ToString("F3") : "<none>");
            builder.Append(" TAAFeedback=").Append(temporalAASettings.Feedback.ToString("0.###"));
            builder.Append(" TAAJitterScale=").Append(temporalAASettings.JitterScale.ToString("0.###"));
            builder.Append(" TAAClamp=").Append(temporalAASettings.ClampStrength.ToString("0.###"));
            builder.Append(" TAASharpness=").Append(temporalAASettings.Sharpness.ToString("0.###"));
            builder.Append(" TAAStaticRelax=").Append(temporalAASettings.StaticEdgeRelaxation.ToString("0.###"));
            builder.Append(" TAAAntiFlicker=").Append(temporalAASettings.AntiFlickering.ToString("0.###"));
            builder.Append(" TAAMVReject=").Append(temporalAASettings.MotionVectorRejection.ToString("0.###"));
            builder.Append(" TAABaseBlend=").Append(temporalAASettings.BaseBlendFactor.ToString("0.###"));
            builder.Append(" TAAMotionEdge=").Append(temporalAASettings.MotionEdgeResponsiveStrength.ToString("0.###"));
            builder.Append(" TAADepthEdge=").Append(temporalAASettings.DepthEdgeResponsiveStrength.ToString("0.###"));
            builder.Append(" TAAClampTight=").Append(temporalAASettings.HistoryClampTightness.ToString("0.###"));
            builder.Append(" TAADepthFilterFloor=").Append(temporalAASettings.DepthWeightedFilterFloor.ToString("0.###"));
            builder.Append(" TAAVelocity=").Append(temporalAA != null ? temporalAA.VelocityMode.ToString() : BurtTemporalAAVelocityMode.Disabled.ToString());
            builder.Append(" TAAObjectMVPass=").Append(temporalAA != null && temporalAA.ObjectMotionVectorPassDrawn);
            builder.Append(" TAADebugMode=").Append(BurtTemporalAAUtility.IsTemporalAADebugMode(BurtShadingDebugSettings.Mode) ? BurtShadingDebugSettings.Mode.ToString() : "Disabled");
            builder.Append(" TAANote=XRenderTSRStyleVelocityDepthClampV7");

            builder.Append(" VolumeLayerMask=").Append(asset != null ? asset.PostProcessVolumeLayerMask.value.ToString() : "<none>"); // 写入 Volume 查询层，排查 Volume 不生效时很有用。

            builder.AppendLine(); // 结束后处理状态行。
        }

        private static void AppendDeferredState( // 写入 Deferred 相关资源和调试状态。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断 Preview 是否强制走 Forward。
            BurtRenderPipelineAsset asset, // 接收管线资产，用来读取 Deferred 开关。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收资源表，用来确认 GBuffer 是否注册。
        {
            builder.AppendLine("Deferred State:"); // 单独成段输出，让 Deferred 分支是否生效一眼可见。

            var isDeferred = IsDeferredRequest(request, asset); // 判断当前 request 是否真的处于 Deferred 路径。

            builder.Append("  Enabled=").Append(isDeferred); // 写入 Deferred 是否启用。

            builder.Append(" ForwardOnlyFallback=").Append(asset != null && asset.EnableDeferredForwardOpaqueFallback); // 写入 Deferred 后 ForwardOnly 兜底开关。

            builder.Append(" GBuffer0Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer0Name)); // 写入 GBuffer0 是否已注册。

            builder.Append(" GBuffer1Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer1Name)); // 写入 GBuffer1 是否已注册。

            builder.Append(" GBuffer2Registered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.GBuffer2Name)); // 写入 GBuffer2 是否已注册。

            builder.Append(" HiZDepthRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.HiZDepthName));

            builder.Append(" SSRColorRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionColorName));

            builder.Append(" SSRDenoisedColorRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionDenoisedColorName));

            builder.Append(" SSRTemporalColorRegistered=").Append(IsRegistered(resourceRegistry, BurtRenderGraphResourceRegistry.ScreenSpaceReflectionTemporalColorName));

            if (isDeferred)
            {
                var ssrSettings = BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionSettings(request, asset);
                var ssrSuppressedByShadingDebug = BurtScreenSpaceReflectionPassUtility.IsScreenSpaceReflectionSuppressedByShadingDebug();
                var ssrHistory = BurtScreenSpaceReflectionHistoryUtility.GetHistoryStatus(request != null ? request.Camera : null);
                var shouldUseHiZDepth = BurtHiZDepthPassUtility.ShouldUseHiZDepth(request, asset);
                builder.Append(" HiZNeeded=").Append(shouldUseHiZDepth);

                if (shouldUseHiZDepth)
                {
                    var hiZDescriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(request != null ? request.Camera : null);
                    builder.Append(" HiZMips=").Append(BurtRenderTargetDescriptorUtility.CalculateMipCount(hiZDescriptor.width, hiZDescriptor.height));
                    builder.Append(" HiZMode=FurthestRawDepth");
                }

                builder.Append(" HiZDebugView=").Append(asset != null && asset.EnableHiZDebugView);
                builder.Append(" HiZDebugMip=").Append(asset != null ? asset.HiZDebugMip.ToString() : "<none>");
                builder.Append(" SSREnabled=").Append(ssrSettings.Enabled);
                builder.Append(" SSRSuppressedByShadingDebug=").Append(ssrSuppressedByShadingDebug);
                builder.Append(" SSRDebugMode=").Append(BurtScreenSpaceReflectionPassUtility.ResolveScreenSpaceReflectionDebugModeLabel());
                builder.Append(" SSRMaxSteps=").Append(ssrSettings.MaxSteps);
                builder.Append(" SSRTemporal=").Append(ssrSettings.TemporalAccumulation);
                builder.Append(" SSRHistoryValid=").Append(ssrHistory.HasHistory);
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
        }

        private static string FormatRenderTextureName(RenderTexture renderTexture) // 生成 RenderTexture 的简短名称。
        {
            if (renderTexture == null) // 如果没有 RenderTexture。
            {
                return "<none>"; // 返回空占位。
            }

            return string.IsNullOrEmpty(renderTexture.name) ? "InstanceID " + renderTexture.GetInstanceID() : renderTexture.name; // 有名称时输出名称，没有名称时输出 InstanceID。
        }

        private static bool IsRegistered( // 判断资源表里是否注册了某个逻辑资源。
            BurtRenderGraphResourceRegistry resourceRegistry, // 接收资源表，可能为空。
            string resourceName) // 接收资源逻辑名。
        {
            return resourceRegistry != null && resourceRegistry.ContainsRenderTarget(resourceName); // 资源表存在且包含该名称时返回 true。
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

            AppendRenderTargetAccessList(builder, "    Access", usage.RenderTargetAccesses); // Writes typed RT accesses, separating Allocate/Bind/Clear/Write/Release.

            AppendGlobalResourceAccessList(builder, "    Global Access", usage.GlobalResourceAccesses); // Writes typed global resource accesses.

            AppendRenderTargetList(builder, "    Read", usage.ReadRenderTargets); // 写入当前 Pass 声明读取的 RenderTarget 列表。

            AppendRenderTargetList(builder, "    Write", usage.WriteRenderTargets); // 写入当前 Pass 声明写入的 RenderTarget 列表。

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

                AddGlobalResourceAccesses(summaries, usage.GlobalResourceAccesses, FormatPassLabel(usageIndex, usage)); // Records typed global resource accesses.
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

        private sealed class ResourceSummary // 定义资源视角的调试摘要，只在 DebugUtility 内部使用。
        {
            public readonly string Name; // 保存资源名。

            public readonly List<string> Allocators = new List<string>(); // Passes that allocate this resource.

            public readonly List<string> Binders = new List<string>(); // Passes that bind this resource as a target only.

            public readonly List<string> Clearers = new List<string>(); // Passes that clear this resource.

            public readonly List<string> Writers = new List<string>(); // Passes that write this resource content.

            public readonly List<string> Copiers = new List<string>(); // Passes that copy into this resource.

            public readonly List<string> Readers = new List<string>(); // Passes that read this resource.

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
