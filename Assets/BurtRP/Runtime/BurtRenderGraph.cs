using System.Collections.Generic; // 引入泛型集合命名空间，用来使用 List 保存 Pass 和资源使用记录。
using System.Text; // 引入文本构建命名空间，用来高效拼接 RenderGraph 调试字符串。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraph // 定义 BurtRP 的最小渲染图类，当前阶段负责保存 Pass、资源表和资源读写声明。
    {
        private readonly List<BurtRenderPass> passes = new List<BurtRenderPass>(); // 创建一个可复用的 Pass 列表，避免每帧重复分配 List。

        private readonly List<BurtRenderPassResourceUsage> resourceUsages = new List<BurtRenderPassResourceUsage>(); // 创建一个可复用的资源使用记录列表，用来保存每个 Pass 的读写声明。

        private readonly BurtRenderGraphResourceRegistry resources = new BurtRenderGraphResourceRegistry(); // 创建一个可复用的资源注册表，用来保存当前图里的渲染目标资源。

        public int PassCount => passes.Count; // 暴露当前图里有多少个 Pass，方便后面调试或判断图是否为空。

        public BurtRenderGraphResourceRegistry Resources => resources; // 暴露当前 RenderGraph 的资源注册表，让 Context 和 Assembler 可以读取资源。

        public IReadOnlyList<BurtRenderPassResourceUsage> ResourceUsages => resourceUsages; // 暴露只读资源使用记录，方便后面调试或做依赖分析。

        public void Clear() // 定义清空函数，每次组装新 request 前都要调用。
        {
            passes.Clear(); // 清空上一轮 request 组装出来的 Pass，避免 Pass 残留到下一次渲染。

            resourceUsages.Clear(); // 清空上一轮 Pass 的资源读写声明，避免调试数据残留。

            resources.Clear(); // 清空上一轮 request 注册的资源，避免 CameraColor 和 CameraDepth 等资源残留到下一次渲染。
        }

        public void ImportRequestResources(BurtRenderRequest request) // 定义从 request 导入基础资源的函数。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务可以导入资源。
            {
                return; // 直接结束导入，资源表保持为空。
            }

            if (!request.IsValid) // 如果 request 无效，说明它不应该提供可执行的 RenderGraph 资源。
            {
                return; // 直接结束导入，避免把无效 request 的目标注册进资源表。
            }

            resources.RegisterCameraColor(request.TargetIdentifier); // 把 request 的原始输出目标注册成 RenderGraph 的 CameraColor 资源。

            resources.RegisterCameraDepthTexture(); // 把 BurtRP 自己的临时深度 RT 注册成 CameraDepth，让颜色目标和深度目标真正分离。

            if (ShouldRegisterMainLightShadowMap(request)) // 如果当前 request 的主光需要阴影，就把主光阴影图纳入资源表。
            {
                resources.RegisterMainLightShadowMapTexture(); // 注册主光阴影图临时 RT，让后续分配、绘制和释放 Pass 使用同一个资源句柄。
            }
        }

        private static bool ShouldRegisterMainLightShadowMap(BurtRenderRequest request) // 定义判断当前 request 是否需要注册主光阴影图的辅助函数。
        {
            return BurtShadowUtility.ShouldUseMainLightShadow(request); // 复用阴影工具的判定逻辑，保证资源注册和 Pass 组装使用同一套条件。
        }

        public void AddPass(BurtRenderPass pass) // 定义添加 Pass 的函数，Assembler 会通过它把 Pass 放进图里。
        {
            if (pass == null) // 如果传入的 Pass 是空，说明组装器传入了异常数据。
            {
                return; // 直接跳过，不把空 Pass 加进图里。
            }

            passes.Add(pass); // 把有效 Pass 加入当前 RenderGraph 的执行列表。
        }

        public void Execute(BurtRenderGraphContext context) // 定义执行函数，用来先收集资源声明再顺序执行所有 Pass。
        {
            if (context == null) // 如果执行上下文为空，说明调用方传入了异常数据。
            {
                return; // 直接结束执行，避免后面访问空对象。
            }

            ConfigurePasses(context); // 在真正执行前收集所有 Pass 的资源读写声明，并把当前上下文传给配置阶段。

            for (var passIndex = 0; passIndex < passes.Count; passIndex++) // 从前到后遍历当前图里的所有 Pass。
            {
                var pass = passes[passIndex]; // 取出当前索引对应的 Pass。

                if (pass == null) // 如果当前 Pass 是空，说明列表里存在异常数据。
                {
                    continue; // 跳过这个空 Pass，继续执行后面的 Pass。
                }

                pass.Execute(context); // 执行当前 Pass，并把 RenderGraphContext 传给它。
            }
        }

        public string DumpDebugInfo(BurtRenderRequest request) // 定义生成 RenderGraph 调试文本的函数，用来输出 Pass 和资源读写声明。
        {
            var builder = new StringBuilder(); // 创建字符串构建器，避免多次字符串相加产生额外 GC。

            builder.AppendLine("[BurtRenderGraph]"); // 写入调试信息标题，方便你在 Console 里搜索。

            AppendRequestInfo(builder, request); // 写入当前 request 的基础信息，例如类型和相机名。

            builder.Append("Pass Count: "); // 写入 Pass 数量标签。

            builder.AppendLine(passes.Count.ToString()); // 写入当前 RenderGraph 中的 Pass 数量。

            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++) // 遍历所有 Pass 的资源使用记录。
            {
                var usage = resourceUsages[usageIndex]; // 取出当前索引对应的资源使用记录。

                if (usage == null) // 如果资源使用记录为空，说明收集阶段存在异常数据。
                {
                    continue; // 跳过空记录，继续输出后面的记录。
                }

                builder.Append("Pass #"); // 写入 Pass 顺序编号标签，方便你在 Console 里确认执行顺序。

                builder.Append(usageIndex); // 写入当前资源使用记录的索引，这个索引对应当前 RenderGraph 的 Pass 顺序。

                builder.Append(": "); // 写入编号和 Pass 名称之间的分隔符。

                builder.AppendLine(usage.PassName); // 写入 Pass 名称。

                AppendRenderTargetList(builder, "  Read", usage.ReadRenderTargets); // 写入当前 Pass 声明读取的渲染目标列表。

                AppendRenderTargetList(builder, "  Write", usage.WriteRenderTargets); // 写入当前 Pass 声明写入的渲染目标列表。
            }

            return builder.ToString(); // 返回完整调试文本给调用方打印。
        }

        private void ConfigurePasses(BurtRenderGraphContext context) // 定义资源声明收集函数，用来调用每个 Pass 的 Configure，并给 Builder 提供当前上下文。
        {
            resourceUsages.Clear(); // 每次执行前清空旧声明，保证 ResourceUsages 只描述当前图。

            for (var passIndex = 0; passIndex < passes.Count; passIndex++) // 遍历当前图里的所有 Pass。
            {
                var pass = passes[passIndex]; // 取出当前索引对应的 Pass。

                if (pass == null) // 如果 Pass 为空，说明当前图里存在异常条目。
                {
                    continue; // 跳过空 Pass，避免创建无意义资源声明。
                }

                var builder = new BurtRenderPassBuilder(pass, context.Request, context.Asset, resources); // 为当前 Pass 创建资源声明 Builder，并注入当前 request 与 asset。

                pass.Configure(builder); // 让当前 Pass 声明自己读取和写入哪些资源。

                resourceUsages.Add(builder.Usage); // 把当前 Pass 的资源使用记录保存到 RenderGraph。
            }
        }

        private static void AppendRequestInfo(StringBuilder builder, BurtRenderRequest request) // 定义写入 request 基础信息的辅助函数。
        {
            if (request == null) // 如果 request 为空，说明当前调试信息没有对应渲染任务。
            {
                builder.AppendLine("Request: null"); // 写入空 request 标记。

                return; // 结束 request 信息输出。
            }

            builder.Append("Request: "); // 写入 request 标签。

            builder.Append(request.Type); // 写入 request 类型，例如 MainCamera 或 Preview。

            builder.Append(" | Camera: "); // 写入相机标签分隔符。

            builder.AppendLine(request.Camera != null ? request.Camera.name : "null"); // 写入相机名称，如果相机为空就写 null。
        }

        private static void AppendRenderTargetList( // 定义写入渲染目标列表的辅助函数。
            StringBuilder builder, // 接收字符串构建器，用来追加文本。
            string label, // 接收列表标签，例如 Read 或 Write。
            IReadOnlyList<BurtRenderTargetHandle> handles) // 接收要输出的渲染目标句柄列表。
        {
            builder.Append(label); // 写入列表标签。

            builder.Append(": "); // 写入标签和值之间的分隔符。

            if (handles == null || handles.Count == 0) // 如果列表为空，说明这个 Pass 没有声明该方向的资源。
            {
                builder.AppendLine("<none>"); // 写入空列表标记。

                return; // 结束列表输出。
            }

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++) // 遍历当前资源列表里的所有句柄。
            {
                if (handleIndex > 0) // 如果不是第一个资源，就需要先写分隔符。
                {
                    builder.Append(", "); // 写入多个资源之间的分隔符。
                }

                var handle = handles[handleIndex]; // 取出当前索引对应的渲染目标句柄。

                builder.Append(handle.Name); // 写入资源名称，例如 CameraColor 或 CameraDepth。

                if (!handle.IsValid) // 如果句柄无效，说明资源表里没有找到对应资源。
                {
                    builder.Append("(Invalid)"); // 在资源名后标记 Invalid，方便你定位资源注册问题。
                }
            }

            builder.AppendLine(); // 当前资源列表写完后换行。
        }
    }
}

