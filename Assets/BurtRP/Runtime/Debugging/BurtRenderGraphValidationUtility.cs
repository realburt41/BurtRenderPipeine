using System;
using System.Collections.Generic;

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让诊断工具可以直接读取 RenderGraph 的 Pass 和资源声明。
{
    internal static class BurtRenderGraphValidationUtility // 定义 RenderGraph 轻量校验工具，只生成诊断消息，不修改 Pass 顺序或渲染目标绑定。
    {
        public static void ValidateConfiguredGraph( // 校验 Configure 阶段收集到的读写声明，帮助发现非 PBR 渲染图的明显资源异常。
            IReadOnlyList<BurtRenderPass> passes, // 接收当前 RenderGraph 的 Pass 列表，用来检查 Pass 数和 Usage 数是否一致。
            IReadOnlyList<BurtRenderPassResourceUsage> resourceUsages, // 接收每个 Pass 的资源读写记录。
            BurtRenderGraphResourceRegistry resources, // 接收当前图的资源注册表，用来判断资源是否为外部导入。
            Action<string> addGraphMessage) // 接收图级别消息写入函数，避免工具直接拥有 RenderGraph 内部状态。
        {
            var graphLog = new GraphValidationLog(addGraphMessage); // 包一层去空保护，保证工具内部追加消息时不会因为回调为空而打断渲染。
            var passCount = passes != null ? passes.Count : 0; // 读取 Pass 数量；列表为空时按 0 处理。
            var usageCount = resourceUsages != null ? resourceUsages.Count : 0; // 读取 Usage 数量；列表为空时按 0 处理。

            ValidateGraphShape(passCount, usageCount, graphLog); // 先检查图结构本身是否完整，例如空图或 Usage 数量不匹配。

            if (resourceUsages == null) // 没有 Usage 列表时无法继续做资源级检查。
            {
                return; // 直接结束校验，避免诊断工具引入空引用风险。
            }

            ValidateCoreResources(resources, graphLog); // 检查基础相机资源是否已经注册，帮助定位 ImportRequestResources 漏调。

            var state = new ResourceValidationState(resources); // 创建资源状态表，用来跟踪前序生产者、消费者和孤立写入。
            for (var usageIndex = 0; usageIndex < resourceUsages.Count; usageIndex++) // 按 RenderGraph 实际执行顺序扫描每个 Pass 的声明。
            {
                var usage = resourceUsages[usageIndex]; // 取出当前 Pass 的资源声明记录。
                if (usage == null) // 空 Usage 表示 Configure 阶段没有留下有效诊断对象。
                {
                    graphLog.Add("ResourceUsage #" + usageIndex + " 为空。"); // 记录图级别结构异常，方便定位收集阶段问题。
                    continue; // 空 Usage 没有可继续检查的读写列表。
                }

                ValidateUsageDeclarations(usage); // 检查单个 Pass 是否完全没有声明资源，或声明了明显无效的句柄。
                ValidateReadResources(usage, state); // 检查读资源是否来自外部导入或前序写入。
                ValidateWriteResources(usage, state); // 记录写资源生产者，并检查外部目标写入等需要关注的行为。
            }

            ValidateFinalState(state, graphLog); // 扫描结束后补充跨 Pass 结论，例如写了但无人消费的内部资源。
        }

        private static void ValidateGraphShape( // 检查 RenderGraph 壳层结构是否和 Configure 收集结果一致。
            int passCount, // 接收 Pass 数量。
            int usageCount, // 接收 Usage 数量。
            GraphValidationLog graphLog) // 接收图级别日志写入器。
        {
            if (passCount == 0) // 如果图里没有 Pass，说明组装器没有产生任何可执行步骤。
            {
                graphLog.Add("RenderGraph 没有有效 Pass。"); // 记录空图问题，Debug 输出时给出明确原因。
            }

            if (passCount != usageCount) // 正常情况下每个 Pass 都应该对应一条 Usage。
            {
                graphLog.Add("RenderGraph Pass 数与资源声明数不一致: Pass=" + passCount + ", Usage=" + usageCount + "。"); // 记录数量不匹配，提示 Configure 收集流程异常。
            }
        }

        private static void ValidateCoreResources( // 检查 RenderGraph 最基础的相机资源是否存在。
            BurtRenderGraphResourceRegistry resources, // 接收资源注册表。
            GraphValidationLog graphLog) // 接收图级别日志写入器。
        {
            if (resources == null) // 资源表为空时后续所有资源声明都无法可靠解析。
            {
                graphLog.Add("RenderGraph 资源注册表为空。"); // 提示调用链没有正确传入资源表。
                return; // 没有资源表时不能继续检查核心资源。
            }

            RequireRegisteredResource(resources, BurtRenderGraphResourceRegistry.FinalCameraTargetName, graphLog); // 最终输出目标必须由 request 导入。
            RequireRegisteredResource(resources, BurtRenderGraphResourceRegistry.CameraColorName, graphLog); // 中间颜色目标是非 PBR 主流程的基础资源。
            RequireRegisteredResource(resources, BurtRenderGraphResourceRegistry.CameraDepthName, graphLog); // 中间深度目标用于深度预通道、天空盒和透明等非 PBR Pass。
        }

        private static void RequireRegisteredResource( // 检查某个资源名是否已经注册。
            BurtRenderGraphResourceRegistry resources, // 接收资源注册表。
            string resourceName, // 接收需要检查的资源名。
            GraphValidationLog graphLog) // 接收图级别日志写入器。
        {
            if (!resources.ContainsRenderTarget(resourceName)) // 资源不存在时，后续 Pass 只能拿到无效句柄。
            {
                graphLog.Add("核心资源未注册: " + resourceName + "。"); // 输出明确资源名，便于回查 ImportRequestResources。
            }
        }

        private static void ValidateUsageDeclarations(BurtRenderPassResourceUsage usage) // 检查单个 Pass 的基础声明完整性。
        {
            if (!usage.HasResourceDeclarations) // 没有任何读写声明的 Pass 对依赖图不可见。
            {
                usage.AddValidationMessage("Pass 未声明任何资源读写。"); // 记录空 Pass/空声明问题，帮助补齐 Configure。
            }
        }

        private static void ValidateReadResources( // 检查单个 Pass 的读取资源是否已经存在生产者。
            BurtRenderPassResourceUsage usage, // 接收当前 Pass 的资源使用记录。
            ResourceValidationState state) // 接收跨 Pass 的资源状态。
        {
            var reads = usage.ReadRenderTargets; // 缓存读取列表，减少属性访问并提升可读性。
            for (var readIndex = 0; readIndex < reads.Count; readIndex++) // 遍历当前 Pass 声明读取的所有资源。
            {
                var handle = reads[readIndex]; // 取出当前读取资源。
                var resourceName = handle.Name; // 读取资源逻辑名，后续用于生产者集合查询。

                if (!CanTrackResource(handle, resourceName)) // 无效资源和空名问题已经由 Usage 记录，这里避免重复刷屏。
                {
                    continue; // 跳过不可追踪资源。
                }

                state.AddConsumer(resourceName); // 记录消费者，用于最终资源摘要和孤立写入检查。

                if (state.IsExternal(resourceName)) // 外部导入资源不需要图内生产者，例如 FinalCameraTarget。
                {
                    continue; // 外部资源视为已经可读。
                }

                if (!state.HasProducer(resourceName)) // 没有外部生产者，也没有前序 Pass 写入，就是读前未写。
                {
                    usage.AddValidationMessage("Read-before-Write: " + resourceName + " 在读取前没有前序生产者。"); // 记录资源顺序问题但不改变执行顺序。
                }
            }
        }

        private static void ValidateWriteResources( // 记录单个 Pass 的写入资源并做轻量异常提示。
            BurtRenderPassResourceUsage usage, // 接收当前 Pass 的资源使用记录。
            ResourceValidationState state) // 接收跨 Pass 的资源状态。
        {
            var writes = usage.WriteRenderTargets; // 缓存写入列表，减少属性访问并提升可读性。
            for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++) // 遍历当前 Pass 声明写入的所有资源。
            {
                var handle = writes[writeIndex]; // 取出当前写入资源。
                var resourceName = handle.Name; // 写入资源逻辑名。

                if (!CanTrackResource(handle, resourceName)) // 无效或空名资源不能作为可靠生产者。
                {
                    continue; // 跳过不可用资源，相关问题已由 Usage 记录。
                }

                if (state.IsExternal(resourceName) && resourceName != BurtRenderGraphResourceRegistry.FinalCameraTargetName) // 当前只预期最终输出目标是可写外部目标。
                {
                    usage.AddValidationMessage("写入外部资源: " + resourceName + "，请确认它不是漏注册的图内临时资源。"); // 外部写入可能代表资源生命周期声明错误。
                }

                state.AddProducer(resourceName); // 记录资源已经被当前或前序 Pass 写入，供后续读取校验。
            }
        }

        private static void ValidateFinalState( // 在扫描完所有 Pass 后补充跨 Pass 诊断。
            ResourceValidationState state, // 接收资源状态表。
            GraphValidationLog graphLog) // 接收图级别日志写入器。
        {
            if (!state.HasProducer(BurtRenderGraphResourceRegistry.FinalCameraTargetName)) // 最终输出没有任何写入时，画面很可能不会被提交到 request 目标。
            {
                graphLog.Add("FinalCameraTarget 没有图内写入者，请确认最终输出 Pass 已组装。"); // 提示 FinalBlit 或等价 Pass 可能缺失。
            }

            foreach (var resourceName in state.ProducedResources) // 遍历所有被写入过的资源。
            {
                if (state.IsExternal(resourceName)) // 外部资源可能就是最终输出，不要求后续再消费。
                {
                    continue; // 跳过外部资源。
                }

                if (!state.HasConsumer(resourceName)) // 图内临时资源写了但从未被读取，通常是多余 Pass 或漏声明读依赖。
                {
                    graphLog.Add("资源写入后没有消费者: " + resourceName + "。"); // 图级提示，避免归咎于某一个具体 Pass。
                }
            }
        }

        private static bool CanTrackResource( // 判断资源句柄是否适合参与跨 Pass 依赖追踪。
            BurtRenderTargetHandle handle, // 接收资源句柄。
            string resourceName) // 接收资源名，避免重复访问属性。
        {
            return handle.IsValid && !string.IsNullOrEmpty(resourceName); // 无效句柄或空名已在 Usage 里记录，跨 Pass 校验不重复报告。
        }

        private sealed class ResourceValidationState // 定义跨 Pass 资源状态表，用来追踪生产者和消费者。
        {
            private readonly BurtRenderGraphResourceRegistry resources; // 保存资源注册表，用于判断外部导入资源。
            private readonly HashSet<string> producedResources = new HashSet<string>(); // 保存已经由图内 Pass 写入过的资源名。
            private readonly HashSet<string> consumedResources = new HashSet<string>(); // 保存已经被图内 Pass 读取过的资源名。

            public IEnumerable<string> ProducedResources => producedResources; // 暴露已生产资源枚举，供最终孤立写入检查使用。

            public ResourceValidationState(BurtRenderGraphResourceRegistry resources) // 创建状态表。
            {
                this.resources = resources; // 保存资源注册表引用；为空时所有资源都按图内资源处理。
            }

            public bool IsExternal(string resourceName) // 判断资源是否由 RenderGraph 外部导入。
            {
                return resources != null && resources.IsExternalRenderTarget(resourceName); // 资源表为空时不把任何资源误判为外部资源。
            }

            public bool HasProducer(string resourceName) // 判断资源是否已有图内生产者。
            {
                return producedResources.Contains(resourceName); // 使用 HashSet 保持每次检查为常数级。
            }

            public bool HasConsumer(string resourceName) // 判断资源是否已有图内消费者。
            {
                return consumedResources.Contains(resourceName); // 使用 HashSet 去重并快速查询。
            }

            public void AddProducer(string resourceName) // 记录资源生产者。
            {
                producedResources.Add(resourceName); // 当前只关心是否存在生产者，具体 Pass 明细已在 dump 的 Passes 段落里展示。
            }

            public void AddConsumer(string resourceName) // 记录资源消费者。
            {
                consumedResources.Add(resourceName); // 当前只关心是否存在消费者，避免诊断状态额外分配复杂结构。
            }
        }

        private readonly struct GraphValidationLog // 定义图级日志写入包装，集中处理空回调和空消息。
        {
            private readonly Action<string> addMessage; // 保存外部传入的消息写入函数。

            public GraphValidationLog(Action<string> addMessage) // 创建日志包装。
            {
                this.addMessage = addMessage; // 保存回调，后续 Add 时再做空保护。
            }

            public void Add(string message) // 写入一条图级诊断消息。
            {
                if (string.IsNullOrEmpty(message) || addMessage == null) // 空消息或空回调都没有可执行动作。
                {
                    return; // 直接跳过，避免诊断路径影响渲染主流程。
                }

                addMessage(message); // 交给 RenderGraph 自身的去重逻辑保存消息。
            }
        }
    }
}
