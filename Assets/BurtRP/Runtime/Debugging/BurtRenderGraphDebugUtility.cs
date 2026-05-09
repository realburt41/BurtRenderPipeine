using System.Collections.Generic; // 引入泛型集合命名空间，用 IReadOnlyList 接收 Pass 资源使用记录和渲染目标列表。
using System.Text; // 引入文本构建命名空间，用 StringBuilder 组合多行 RenderGraph dump。

namespace Burt.RenderPipeline // 定义 BurtRP 的运行时命名空间，让工具能直接访问 BurtRenderRequest 和资源使用类型。
{
    internal static class BurtRenderGraphDebugUtility // 定义 RenderGraph dump 格式化工具，把日志排版细节从 BurtRenderGraph 执行类中拆出来。
    {
        private const int BaseDumpCapacity = 512; // 定义 dump 基础容量，覆盖标题、Request、Camera 和 Pass Count 等固定内容。

        private const int PerPassDumpCapacity = 160; // 定义每个 Pass 的估算容量，减少多 Pass 场景下 StringBuilder 扩容次数。

        public static string BuildDump( // 构建完整 RenderGraph 调试文本，调用方仍然决定是否真正输出到 Console。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出 Request 类型和 Camera 名称。
            int passCount, // 接收 RenderGraph 当前 Pass 数量，确保 dump 中的 Pass Count 和图本身一致。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages) // 接收每个 Pass 的资源读写声明，由 RenderGraph 配置阶段收集。
        {
            var usageCount = resourceUsages != null ? resourceUsages.Count : 0; // 读取资源使用记录数量；列表为空时按 0 处理。

            var capacity = BaseDumpCapacity + usageCount * PerPassDumpCapacity; // 根据 Pass 数量估算字符串容量，减少生成 dump 时的临时扩容。

            var builder = BurtDebugStringBuilderPool.Get(capacity); // 从调试 StringBuilder 池租借构建器，避免每帧开启日志时频繁分配。

            try // 使用 try/finally 保证构建器一定归还池中。
            {
                AppendDump(builder, request, passCount, resourceUsages); // 把实际排版逻辑写到构建器里，BuildDump 只负责生命周期管理。

                return builder.ToString(); // 返回完整 dump 字符串，后续是否 Debug.Log 仍由外层 asset 开关控制。
            }
            finally // 无论 ToString 之前是否发生异常，都执行归还逻辑。
            {
                BurtDebugStringBuilderPool.Release(builder); // 归还构建器，避免 debug 工具引入额外长期分配。
            }
        }

        public static void AppendDump( // 把 RenderGraph dump 追加到调用方提供的构建器，方便未来组合更大的诊断文本。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderRequest request, // 接收当前渲染请求，用来输出任务和相机信息。
            int passCount, // 接收当前 RenderGraph 的 Pass 数量。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages) // 接收当前 RenderGraph 的资源读写记录。
        {
            if (builder == null) // 如果没有构建器，就没有安全写入目标。
            {
                return; // 直接返回，避免调试格式化影响渲染主流程。
            }

            BurtDebugLogUtility.AppendScopedHeaderLine(builder, BurtDebugLogUtility.RenderGraphPrefix); // 写入统一标题 [BurtRP][BurtRenderGraph]，方便 Console 过滤。

            AppendRequestInfo(builder, request); // 写入 Request 和 Camera 基础信息，让 dump 一眼能看出来自哪次渲染请求。

            BurtDebugLogUtility.AppendKeyValueLine(builder, "Pass Count", passCount); // 写入 RenderGraph 中的 Pass 数量，和实际执行列表保持一致。

            builder.AppendLine("Passes:"); // 写入 Pass 列表标题，把后面的资源读写详情归为同一组。

            AppendPassUsages(builder, resourceUsages); // 写入每个 Pass 的 Read/Write 资源列表，帮助定位资源声明缺失或顺序问题。
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
            builder.Append("  Pass #"); // 写入缩进和 Pass 编号前缀，让多条 Pass 记录更容易在 Console 中区分。

            builder.Append(usageIndex); // 写入当前 Pass 的顺序编号，用来对齐实际执行顺序。

            builder.Append(": "); // 写入编号和名称之间的分隔符。

            if (usage == null) // 如果资源使用记录为空，说明配置阶段存在异常条目。
            {
                builder.AppendLine("<null usage>"); // 写入空记录占位，保留索引方便定位问题。

                return; // 空记录没有 Read/Write 列表，直接结束当前 Pass 输出。
            }

            builder.AppendLine(string.IsNullOrEmpty(usage.PassName) ? "<unnamed pass>" : usage.PassName); // 写入 Pass 名称；名称缺失时给出占位文本。

            AppendRenderTargetList(builder, "    Read", usage.ReadRenderTargets); // 写入当前 Pass 声明读取的 RenderTarget 列表。

            AppendRenderTargetList(builder, "    Write", usage.WriteRenderTargets); // 写入当前 Pass 声明写入的 RenderTarget 列表。
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

        private static void AppendRenderTarget( // 写入单个渲染目标句柄的可读文本。
            StringBuilder builder, // 接收要写入的字符串构建器。
            BurtRenderTargetHandle handle) // 接收 RenderGraph 资源句柄，里面包含逻辑名称和有效性。
        {
            var resourceName = string.IsNullOrEmpty(handle.Name) ? "<unnamed target>" : handle.Name; // 读取资源名；名称缺失时写占位，帮助发现注册问题。

            builder.Append(resourceName); // 写入资源逻辑名称，例如 CameraColor、CameraDepth 或 MainLightShadowMap。

            if (!handle.IsValid) // 如果句柄无效，说明 Pass 声明了一个资源表里没有的目标。
            {
                builder.Append(" (Invalid)"); // 在资源名后标记 Invalid，让资源注册缺失在 dump 中非常醒目。
            }
        }
    }
}