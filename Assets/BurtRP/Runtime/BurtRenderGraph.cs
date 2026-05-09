using System; // 引入基础命名空间，用来捕获 Configure 阶段异常并写入诊断信息。
using System.Collections.Generic; // 引入泛型集合命名空间，用来使用 List、Dictionary 和 HashSet 保存 Pass 与资源校验状态。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraph // 定义 BurtRP 的最小渲染图类，当前阶段负责保存 Pass、资源表和资源读写声明。
    {
        private readonly List<BurtRenderPass> passes = new List<BurtRenderPass>(); // 创建一个可复用的 Pass 列表，避免每帧重复分配 List。

        private readonly List<BurtRenderPassResourceUsage> resourceUsages = new List<BurtRenderPassResourceUsage>(); // 创建一个可复用的资源使用记录列表，用来保存每个 Pass 的读写声明。

        private readonly List<string> validationMessages = new List<string>(); // 保存当前图级别的轻量校验消息，只用于 Debug dump，不改变实际渲染顺序。

        private readonly BurtRenderGraphResourceRegistry resources = new BurtRenderGraphResourceRegistry(); // 创建一个可复用的资源注册表，用来保存当前图里的渲染目标资源。

        public int PassCount => passes.Count; // 暴露当前图里有多少个 Pass，方便后面调试或判断图是否为空。

        public BurtRenderGraphResourceRegistry Resources => resources; // 暴露当前 RenderGraph 的资源注册表，让 Context 和 Assembler 可以读取资源。

        public IReadOnlyList<BurtRenderPassResourceUsage> ResourceUsages => resourceUsages; // 暴露只读资源使用记录，方便后面调试或做依赖分析。

        public IReadOnlyList<string> ValidationMessages => validationMessages; // 暴露只读校验消息，供调试工具集中输出 RenderGraph 问题。

        public void Clear() // 定义清空函数，每次组装新 request 前都要调用。
        {
            passes.Clear(); // 清空上一轮 request 组装出来的 Pass，避免 Pass 残留到下一次渲染。

            resourceUsages.Clear(); // 清空上一轮 Pass 的资源读写声明，避免调试数据残留。

            validationMessages.Clear(); // 清空上一轮图校验消息，避免不同相机或 request 互相污染。

            resources.Clear(); // 清空上一轮 request 注册的资源，避免 CameraColor 和 CameraDepth 等资源残留到下一次渲染。
        }

        public void ImportRequestResources(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 定义从 request 导入基础资源的函数，并允许资源注册使用管线资产配置。
        {
            if (request == null) // 如果 request 为空，说明没有合法渲染任务可以导入资源。
            {
                AddValidationMessage("ImportRequestResources 收到空 request。"); // 记录异常输入，便于 Debug 时定位调用链。

                return; // 直接结束导入，资源表保持为空。
            }

            if (!request.IsValid) // 如果 request 无效，说明它不应该提供可执行的 RenderGraph 资源。
            {
                AddValidationMessage("ImportRequestResources 收到无效 request。"); // 记录无效请求，避免 dump 里看不出资源为空的原因。

                return; // 直接结束导入，避免把无效 request 的目标注册进资源表。
            }

            resources.RegisterFinalCameraTarget(request.TargetIdentifier); // 把 request 的原始输出目标注册为 FinalCameraTarget，FinalBlit 最后会把中间颜色拷贝到这里。

            resources.RegisterCameraColorTexture(); // 把 BurtRP 自己的临时颜色 RT 注册成 CameraColor，让场景绘制不再直接写 backbuffer。

            resources.RegisterCameraDepthTexture(); // 把 BurtRP 自己的临时深度 RT 注册成 CameraDepth，让颜色目标和深度目标真正分离。

            if (ShouldRegisterMainLightShadowMap(request, asset)) // 如果当前 request 的主光需要阴影，就把主光阴影图纳入资源表。
            {
                resources.RegisterMainLightShadowMapTexture(); // 注册主光阴影图临时 RT，让后续分配、绘制和释放 Pass 使用同一个资源句柄。
            }
        }

        private static bool ShouldRegisterMainLightShadowMap( // 定义判断当前 request 是否需要注册主光阴影图的辅助函数。
            BurtRenderRequest request, // 接收当前渲染请求，用来读取 Light 解析出的阴影数据。
            BurtRenderPipelineAsset asset) // 接收当前管线资产，用来让资源注册尊重主光阴影总开关和默认配置。
        {
            return BurtShadowUtility.ShouldUseMainLightShadow(request, asset); // 复用阴影工具的判定逻辑，保证资源注册和 Pass 组装使用同一套条件。
        }

        public void AddPass(BurtRenderPass pass) // 定义添加 Pass 的函数，Assembler 会通过它把 Pass 放进图里。
        {
            if (pass == null) // 如果传入的 Pass 是空，说明组装器传入了异常数据。
            {
                AddValidationMessage("AddPass 收到空 Pass，已跳过。"); // 记录空 Pass，便于定位组装器问题。

                return; // 直接跳过，不把空 Pass 加进图里。
            }

            passes.Add(pass); // 把有效 Pass 加入当前 RenderGraph 的执行列表。
        }

        public void Execute(BurtRenderGraphContext context) // 定义执行函数，用来先收集资源声明再顺序执行所有 Pass。
        {
            if (context == null) // 如果执行上下文为空，说明调用方传入了异常数据。
            {
                AddValidationMessage("Execute 收到空 RenderGraphContext。"); // 记录异常上下文，便于 Debug dump 解释为什么没有执行。

                return; // 直接结束执行，避免后面访问空对象。
            }

            ConfigurePasses(context); // 在真正执行前收集所有 Pass 的资源读写声明，并把当前上下文传给配置阶段。

            ValidateConfiguredGraph(); // 对配置结果做轻量校验，只记录问题，不重排 Pass，也不改变 RenderTarget 绑定逻辑。

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

        public string DumpDebugInfo(BurtRenderRequest request) // 定义生成 RenderGraph 调试文本的函数，具体格式交给 Debugging 工具维护。
        {
            return BurtRenderGraphDebugUtility.BuildDump(request, passes.Count, resourceUsages, validationMessages, resources); // 把 request、Pass 数量、资源声明和校验结果交给统一工具格式化。
        }

        private void ConfigurePasses(BurtRenderGraphContext context) // 定义资源声明收集函数，用来调用每个 Pass 的 Configure，并给 Builder 提供当前上下文。
        {
            resourceUsages.Clear(); // 每次执行前清空旧声明，保证 ResourceUsages 只描述当前图。
            // 不在这里清空 validationMessages，保留 Import/AddPass 阶段已经记录的图级问题。

            for (var passIndex = 0; passIndex < passes.Count; passIndex++) // 遍历当前图里的所有 Pass。
            {
                var pass = passes[passIndex]; // 取出当前索引对应的 Pass。

                if (pass == null) // 如果 Pass 为空，说明当前图里存在异常条目。
                {
                    AddValidationMessage("Pass #" + passIndex + " 为空，配置阶段已跳过。"); // 记录空 Pass 的索引，便于修复组装器。

                    continue; // 跳过空 Pass，避免创建无意义资源声明。
                }

                var builder = new BurtRenderPassBuilder(passIndex, pass, context.Request, context.Asset, resources); // 为当前 Pass 创建资源声明 Builder，并注入当前 request 与 asset。

                try // Configure 只负责声明依赖，异常不应该直接阻断后续 Debug 信息收集。
                {
                    pass.Configure(builder); // 让当前 Pass 声明自己读取和写入哪些资源。
                }
                catch (Exception exception) // 捕获配置阶段异常，保留渲染执行顺序但让 dump 能指出具体 Pass。
                {
                    builder.Usage.AddValidationMessage("Configure 异常: " + exception.GetType().Name + " - " + exception.Message); // 把异常摘要写入当前 Pass 的校验消息。

                    AddValidationMessage("Pass #" + passIndex + " (" + GetPassName(pass) + ") Configure 抛出异常，已继续收集后续 Pass。"); // 写入图级别摘要。
                }

                resourceUsages.Add(builder.Usage); // 把当前 Pass 的资源使用记录保存到 RenderGraph。
            }
        }

        private void ValidateConfiguredGraph() // 对已收集的资源声明做轻量校验，当前阶段只产生日志，不改变实际渲染行为。
        {
            if (passes.Count == 0) // 如果图里没有 Pass，说明组装器没有产生任何可执行步骤。
            {
                AddValidationMessage("RenderGraph 没有有效 Pass。"); // 记录空图问题，Debug 输出时给出明确原因。
            }

            var writtenResources = new HashSet<string>(); // 记录已经由前序 Pass 写入过的资源，用于检测 Read-before-Write。

            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++) // 按执行顺序检查每个 Pass 的声明。
            {
                var usage = resourceUsages[usageIndex]; // 取出当前 Pass 的资源使用记录。

                if (usage == null) // 正常情况下不会为空，但保留防御逻辑。
                {
                    AddValidationMessage("ResourceUsage #" + usageIndex + " 为空。"); // 记录空资源使用记录。

                    continue; // 空记录没有可校验内容。
                }

                if (!usage.HasResourceDeclarations) // 没有任何读写声明的 Pass 对依赖图不可见。
                {
                    usage.AddValidationMessage("Pass 未声明任何资源读写。"); // 记录空 Pass/空声明问题，帮助补齐 Configure。
                }

                ValidateReadResources(usage, writtenResources); // 先检查读取，确保读资源来自外部或已经被前序 Pass 写过。

                AddWrittenResources(usage, writtenResources); // 再记录写入，让后续 Pass 可以把这些资源视为已有生产者。
            }
        }

        private void ValidateReadResources( // 检查单个 Pass 的读取资源是否已经存在生产者。
            BurtRenderPassResourceUsage usage, // 接收当前 Pass 的资源使用记录。
            HashSet<string> writtenResources) // 接收前序 Pass 已写入资源集合。
        {
            var reads = usage.ReadRenderTargets; // 缓存读取列表，减少属性访问并提升可读性。

            for (var readIndex = 0; readIndex < reads.Count; readIndex++) // 遍历当前 Pass 声明读取的所有资源。
            {
                var handle = reads[readIndex]; // 取出当前读取资源。

                if (!handle.IsValid) // 无效资源已经由 Usage 记录缺失提示，这里避免继续做生产者判断。
                {
                    continue; // 跳过无效资源，避免同一问题重复刷屏。
                }

                var resourceName = handle.Name; // 读取资源逻辑名，后续用于生产者集合查询。

                if (string.IsNullOrEmpty(resourceName)) // 空资源名无法可靠参与依赖校验。
                {
                    continue; // 空名问题已在 Usage 里记录，这里不重复提示。
                }

                if (resources.IsExternalRenderTarget(resourceName)) // 外部导入资源不需要图内生产者，例如 FinalCameraTarget。
                {
                    continue; // 外部资源视为已经可读。
                }

                if (!writtenResources.Contains(resourceName)) // 没有外部生产者，也没有前序 Pass 写入，就是读前未写。
                {
                    usage.AddValidationMessage("Read-before-Write: " + resourceName + " 在读取前没有前序生产者。"); // 记录资源顺序问题但不改变执行顺序。
                }
            }
        }

        private static void AddWrittenResources( // 把当前 Pass 的写入资源加入生产者集合。
            BurtRenderPassResourceUsage usage, // 接收当前 Pass 的资源使用记录。
            HashSet<string> writtenResources) // 接收要更新的已写资源集合。
        {
            var writes = usage.WriteRenderTargets; // 缓存写入列表，减少属性访问并提升可读性。

            for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++) // 遍历当前 Pass 声明写入的所有资源。
            {
                var handle = writes[writeIndex]; // 取出当前写入资源。

                if (!handle.IsValid || string.IsNullOrEmpty(handle.Name)) // 无效或空名资源不能作为可靠生产者。
                {
                    continue; // 跳过不可用资源，相关问题已由 Usage 记录。
                }

                writtenResources.Add(handle.Name); // 记录资源已经被当前或前序 Pass 写入，供后续读取校验。
            }
        }

        private void AddValidationMessage(string message) // 定义图级别校验消息追加函数，带简单去重避免 Debug 开关打开时噪音过大。
        {
            if (string.IsNullOrEmpty(message)) // 空消息没有诊断价值。
            {
                return; // 直接忽略。
            }

            if (validationMessages.Contains(message)) // 相同图级消息只保留一次。
            {
                return; // 已存在时不重复添加。
            }

            validationMessages.Add(message); // 追加到当前图的校验消息列表。
        }

        private static string GetPassName(BurtRenderPass pass) // 安全读取 Pass 名称，避免异常路径再次触发空引用。
        {
            return pass != null && !string.IsNullOrEmpty(pass.Name) ? pass.Name : "UnnamedPass"; // 名称缺失时使用兜底文本。
        }
    }
}

