using System.Collections.Generic; // 引入泛型集合命名空间，用 IReadOnlyList、Dictionary 和 List 组织 Pass 与资源关系。
using System.Text; // 引入文本构建命名空间，用 StringBuilder 组合多行 RenderGraph dump。

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
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RT 生命周期选项，用来输出 Allocate/FinalBlit/Release 决策。
        {
            var usageCount = resourceUsages != null ? resourceUsages.Count : 0; // 读取资源使用记录数量；列表为空时按 0 处理。

            var validationCount = validationMessages != null ? validationMessages.Count : 0; // 读取图级校验消息数量，帮助估算容量。

            var renderOptionsCapacity = renderOptions != null ? 256 : 48; // RT 生命周期行较长，预留额外空间减少 StringBuilder 扩容。

            var capacity = BaseDumpCapacity + renderOptionsCapacity + usageCount * PerPassDumpCapacity + validationCount * 96; // 根据 Pass、校验和 RT 选项数量估算字符串容量。

            var builder = BurtDebugStringBuilderPool.Get(capacity); // 从调试 StringBuilder 池租借构建器，避免每帧开启日志时频繁分配。

            try // 使用 try/finally 保证构建器一定归还池中。
            {
                AppendDump(builder, request, passCount, resourceUsages, validationMessages, resourceRegistry, renderOptions); // 把实际排版逻辑写到构建器里，BuildDump 只负责生命周期管理。

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
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RT 生命周期选项。
        {
            if (builder == null) // 如果没有构建器，就没有安全写入目标。
            {
                return; // 直接返回，避免调试格式化影响渲染主流程。
            }

            BurtDebugLogUtility.AppendScopedHeaderLine(builder, BurtDebugLogUtility.RenderGraphPrefix); // 写入统一标题 [BurtRP][BurtRenderGraph]，方便 Console 过滤。

            AppendRequestInfo(builder, request); // 写入 Request 和 Camera 基础信息，让 dump 一眼能看出来自哪次渲染请求。

            AppendRenderOptions(builder, renderOptions); // 写入 RT 生命周期决策，让你不用只靠 Pass 列表反推 Allocate、FinalBlit 和 Release。

            BurtDebugLogUtility.AppendKeyValueLine(builder, "Pass Count", passCount); // 写入 RenderGraph 中的 Pass 数量，和实际执行列表保持一致。

            AppendValidationMessages(builder, validationMessages, resourceUsages); // 写入图级和 Pass 级校验消息，优先展示可能的问题。

            builder.AppendLine("Passes:"); // 写入 Pass 列表标题，把后面的资源读写详情归为同一组。

            AppendPassUsages(builder, resourceUsages); // 写入每个 Pass 的 Read/Write 资源列表，帮助定位资源声明缺失或顺序问题。

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

            AppendRenderTargetList(builder, "    Read", usage.ReadRenderTargets); // 写入当前 Pass 声明读取的 RenderTarget 列表。

            AppendRenderTargetList(builder, "    Write", usage.WriteRenderTargets); // 写入当前 Pass 声明写入的 RenderTarget 列表。

            AppendOptionalStringList(builder, "    Read Global", usage.ReadGlobalResources); // 只在非空时写入当前 Pass 声明读取的逻辑全局资源列表。

            AppendOptionalStringList(builder, "    Write Global", usage.WriteGlobalResources); // 只在非空时写入当前 Pass 声明写入的逻辑全局资源列表。
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

                AppendStringList(builder, "    Producers", summary.Producers); // 写入生产者列表。

                AppendStringList(builder, "    Consumers", summary.Consumers); // 写入消费者列表。

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

                AddResourceAccesses(summaries, usage.ReadRenderTargets, FormatPassLabel(usageIndex, usage), false, resourceRegistry); // 记录消费者。

                AddResourceAccesses(summaries, usage.WriteRenderTargets, FormatPassLabel(usageIndex, usage), true, resourceRegistry); // 记录生产者。

                AddGlobalResourceAccesses(summaries, usage.ReadGlobalResources, FormatPassLabel(usageIndex, usage), false); // 记录逻辑全局资源消费者。

                AddGlobalResourceAccesses(summaries, usage.WriteGlobalResources, FormatPassLabel(usageIndex, usage), true); // 记录逻辑全局资源生产者。
            }

            return summaries; // 返回完整资源摘要。
        }

        private static void AddResourceAccesses( // 把一组资源访问写入摘要表。
            Dictionary<string, ResourceSummary> summaries, // 接收资源摘要表。
            IReadOnlyList<BurtRenderTargetHandle> handles, // 接收某个方向的资源句柄列表。
            string passLabel, // 接收当前 Pass 标签。
            bool isProducer, // 标记当前访问是否为写入生产者。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收资源注册表，用于标记外部资源。
        {
            if (handles == null) // 资源列表为空时没有可记录内容。
            {
                return; // 直接返回。
            }

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++) // 遍历资源句柄列表。
            {
                var handle = handles[handleIndex]; // 取出当前句柄。

                var resourceName = FormatResourceName(handle.Name); // 统一资源名显示，空名使用占位符。

                if (!summaries.TryGetValue(resourceName, out var summary)) // 如果还没有该资源的摘要，就创建一个。
                {
                    summary = new ResourceSummary(resourceName); // 创建资源摘要对象。

                    summaries.Add(resourceName, summary); // 加入摘要表。
                }

                if (resourceRegistry != null && resourceRegistry.IsExternalRenderTarget(handle.Name)) // 判断该资源是否为外部导入。
                {
                    summary.IsExternal = true; // 标记外部资源。
                }

                if (!handle.IsValid) // 无效句柄表示声明了缺失资源。
                {
                    summary.HasMissingDeclaration = true; // 在摘要中标记缺失。
                }

                if (isProducer) // 写入资源对应生产者。
                {
                    AddUnique(summary.Producers, passLabel); // 记录生产者 Pass。
                }
                else // 读取资源对应消费者。
                {
                    AddUnique(summary.Consumers, passLabel); // 记录消费者 Pass。
                }
            }
        }

        private static void AddGlobalResourceAccesses( // 把一组逻辑全局资源访问写入摘要表。
            Dictionary<string, ResourceSummary> summaries, // 接收资源摘要表。
            IReadOnlyList<string> resourceNames, // 接收某个方向的逻辑全局资源名列表。
            string passLabel, // 接收当前 Pass 标签。
            bool isProducer) // 标记当前访问是否为写入生产者。
        {
            if (resourceNames == null) // 资源列表为空时没有可记录内容。
            {
                return; // 直接返回。
            }

            for (var resourceIndex = 0; resourceIndex < resourceNames.Count; resourceIndex++) // 遍历逻辑全局资源名列表。
            {
                var resourceName = FormatResourceName(resourceNames[resourceIndex]); // 统一资源名显示，空名使用占位符。

                if (!summaries.TryGetValue(resourceName, out var summary)) // 如果还没有该资源的摘要，就创建一个。
                {
                    summary = new ResourceSummary(resourceName); // 创建资源摘要对象。

                    summaries.Add(resourceName, summary); // 加入摘要表。
                }

                if (isProducer) // 写入逻辑全局资源对应生产者。
                {
                    AddUnique(summary.Producers, passLabel); // 记录生产者 Pass。
                }
                else // 读取逻辑全局资源对应消费者。
                {
                    AddUnique(summary.Consumers, passLabel); // 记录消费者 Pass。
                }
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

        private static string FormatPassLabel( // 生成统一 Pass 标签。
            int usageIndex, // 接收列表索引，作为缺省 Pass Index。
            BurtRenderPassResourceUsage usage) // 接收资源使用记录，可能为空。
        {
            var passIndex = usage != null && usage.PassIndex >= 0 ? usage.PassIndex : usageIndex; // 优先使用 RenderGraph 传入的真实 PassIndex。

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

        private sealed class ResourceSummary // 定义资源视角的调试摘要，只在 DebugUtility 内部使用。
        {
            public readonly string Name; // 保存资源名。

            public readonly List<string> Producers = new List<string>(); // 保存写入该资源的 Pass 列表。

            public readonly List<string> Consumers = new List<string>(); // 保存读取该资源的 Pass 列表。

            public bool HasMissingDeclaration; // 标记是否有无效句柄引用该资源。

            public bool IsExternal; // 标记资源是否来自 RenderGraph 外部。

            public ResourceSummary(string name) // 定义构造函数。
            {
                Name = name; // 保存资源名。
            }
        }
    }
}
