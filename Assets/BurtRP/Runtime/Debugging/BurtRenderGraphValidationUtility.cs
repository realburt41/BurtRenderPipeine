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
                ValidatePassMetadata(usage); // 检查未来裁剪元数据是否和副作用标记互相矛盾。
                ValidateGlobalResourceConsistency(usage); // 检查全局状态声明是否同步声明对应的图资源。
                ValidateRegisteredResourceAccesses(usage, resources); // 检查 Configure 是否访问了未登记到资源表的有效句柄。
                ValidateTerminalWriteDeclarations(usage); // 检查有意终端写入标记是否真的对应当前 Pass 的写入声明。
                ValidateSamePassReadWriteResources(usage); // 提示少数未分类 Pass 的同资源读写风险，避免误把 in-place 操作藏起来。
                ValidateReadResources(usage, state); // 检查读资源是否来自外部导入或前序写入。
                ValidateReadBufferResources(usage, state); // 检查逻辑 Buffer 是否来自外部导入或前序写入。
                ValidateReadGlobalResources(usage, state); // 检查逻辑全局资源是否来自前序写入。
                ValidateWriteResources(usage, state); // 记录写资源生产者，并检查外部目标写入等需要关注的行为。
                ValidateWriteBufferResources(usage, state); // 记录逻辑 Buffer 生产者，供后续 tiled/cluster pass 读取。
                ValidateWriteGlobalResources(usage, state); // 记录逻辑全局资源生产者，便于后续 Pass 声明读取依赖。
                ValidateTypedRenderTargetLifecycle(usage, state); // 基于 Allocate/Release 等 typed access 检查资源生命周期风险。
                ValidateTypedBufferLifecycle(usage, state); // 给未来 Buffer 分配/释放建立同样的生命周期诊断。
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

        private static void ValidatePassMetadata(BurtRenderPassResourceUsage usage) // Checks conservative culling metadata before a real culler exists.
        {
            if (usage.AllowCulling && usage.HasSideEffects)
            {
                usage.AddValidationMessage("Pass 同时声明 AllowCulling=true 和 HasSideEffects=true，未来启用裁剪前请先拆分副作用或关闭裁剪。");
            }
        }

        private static void ValidateGlobalResourceConsistency(BurtRenderPassResourceUsage usage) // Keeps logical globals paired with the backing graph resources they imply.
        {
            if (ContainsString(usage.ReadGlobalResources, BurtRenderGraphResourceRegistry.LightingGlobalsName) &&
                !ContainsBufferResourceName(usage.ReadBuffers, BurtRenderGraphResourceRegistry.AdditionalLightBufferName))
            {
                usage.AddValidationMessage("读取 LightingGlobals 但未声明读取 AdditionalLightBuffer，全局状态和 Buffer 依赖可能不一致。");
            }

            if (ContainsString(usage.WriteGlobalResources, BurtRenderGraphResourceRegistry.LightingGlobalsName) &&
                !ContainsBufferResourceName(usage.WriteBuffers, BurtRenderGraphResourceRegistry.AdditionalLightBufferName))
            {
                usage.AddValidationMessage("写入 LightingGlobals 但未声明写入 AdditionalLightBuffer，全局状态和 Buffer 依赖可能不一致。");
            }
        }

        private static void ValidateRegisteredResourceAccesses( // Detects manually-created handles that bypassed the registry.
            BurtRenderPassResourceUsage usage,
            BurtRenderGraphResourceRegistry resources)
        {
            if (resources == null)
            {
                return;
            }

            ValidateRegisteredRenderTargets(usage, usage.ReadRenderTargets, resources, "Read");
            ValidateRegisteredRenderTargets(usage, usage.WriteRenderTargets, resources, "Write");
            ValidateRegisteredBuffers(usage, usage.ReadBuffers, resources, "Read");
            ValidateRegisteredBuffers(usage, usage.WriteBuffers, resources, "Write");
        }

        private static void ValidateRegisteredRenderTargets(
            BurtRenderPassResourceUsage usage,
            IReadOnlyList<BurtRenderTargetHandle> handles,
            BurtRenderGraphResourceRegistry resources,
            string accessType)
        {
            if (handles == null)
            {
                return;
            }

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++)
            {
                var handle = handles[handleIndex];
                var resourceName = handle.Name;

                if (!handle.IsValid || string.IsNullOrEmpty(resourceName))
                {
                    continue;
                }

                if (!resources.ContainsRenderTarget(resourceName))
                {
                    usage.AddValidationMessage(accessType + " 访问未注册 RenderTarget: " + resourceName + "。");
                }
            }
        }

        private static void ValidateRegisteredBuffers(
            BurtRenderPassResourceUsage usage,
            IReadOnlyList<BurtRenderBufferHandle> handles,
            BurtRenderGraphResourceRegistry resources,
            string accessType)
        {
            if (handles == null)
            {
                return;
            }

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++)
            {
                var handle = handles[handleIndex];
                var resourceName = handle.Name;

                if (!handle.IsValid || string.IsNullOrEmpty(resourceName))
                {
                    continue;
                }

                if (!resources.ContainsBuffer(resourceName))
                {
                    usage.AddValidationMessage(accessType + " 访问未注册 Buffer: " + resourceName + "。");
                }
            }
        }

        private static void ValidateTerminalWriteDeclarations(BurtRenderPassResourceUsage usage) // Ensures terminal-write exceptions cannot hide missing declarations.
        {
            var resources = usage.AllowUnconsumedWriteResources;
            for (var resourceIndex = 0; resourceIndex < resources.Count; resourceIndex++)
            {
                var resourceName = resources[resourceIndex];
                if (string.IsNullOrEmpty(resourceName))
                {
                    continue;
                }

                if (!ContainsWrittenResourceName(usage, resourceName))
                {
                    usage.AddValidationMessage("AllowUnconsumedWrite 标记未匹配当前 Pass 的写入声明: " + resourceName + "。");
                }
            }
        }

        private static void ValidateSamePassReadWriteResources(BurtRenderPassResourceUsage usage) // Flags suspicious same-pass read/write when the pass kind is not an expected in-place category.
        {
            if (!ShouldWarnSamePassReadWrite(usage))
            {
                return;
            }

            ValidateSamePassRenderTargetReadWrite(usage);
            ValidateSamePassBufferReadWrite(usage);
            ValidateSamePassGlobalReadWrite(usage);
        }

        private static bool ShouldWarnSamePassReadWrite(BurtRenderPassResourceUsage usage) // Keeps known blending/full-screen passes from turning debug output into noise.
        {
            if (usage == null)
            {
                return false;
            }

            switch (usage.PassKind)
            {
                case BurtRenderPassKind.DrawRenderers:
                case BurtRenderPassKind.FullScreen:
                case BurtRenderPassKind.PostProcess:
                case BurtRenderPassKind.Debug:
                case BurtRenderPassKind.Copy:
                    return false;
                default:
                    return true;
            }
        }

        private static void ValidateSamePassRenderTargetReadWrite(BurtRenderPassResourceUsage usage) // Checks same-pass RT read/write declarations.
        {
            var reads = usage.ReadRenderTargets;
            var writes = usage.WriteRenderTargets;

            for (var readIndex = 0; readIndex < reads.Count; readIndex++)
            {
                var read = reads[readIndex];
                if (!CanTrackResource(read, read.Name))
                {
                    continue;
                }

                for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++)
                {
                    var write = writes[writeIndex];
                    if (CanTrackResource(write, write.Name) && read.Name == write.Name)
                    {
                        usage.AddValidationMessage("同一 Pass 同时 Read/Write RenderTarget: " + read.Name + "，请确认这是有意的 in-place 操作。");
                    }
                }
            }
        }

        private static void ValidateSamePassBufferReadWrite(BurtRenderPassResourceUsage usage) // Checks same-pass logical buffer read/write declarations.
        {
            var reads = usage.ReadBuffers;
            var writes = usage.WriteBuffers;

            for (var readIndex = 0; readIndex < reads.Count; readIndex++)
            {
                var read = reads[readIndex];
                if (!CanTrackBuffer(read, read.Name))
                {
                    continue;
                }

                for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++)
                {
                    var write = writes[writeIndex];
                    if (CanTrackBuffer(write, write.Name) && read.Name == write.Name)
                    {
                        if (IsKnownIntentionalSamePassBufferReadWrite(usage, read.Name))
                        {
                            continue;
                        }

                        usage.AddValidationMessage("同一 Pass 同时 Read/Write Buffer: " + read.Name + "，请确认这是有意的 in-place 操作。");
                    }
                }
            }
        }

        private static bool IsKnownIntentionalSamePassBufferReadWrite(BurtRenderPassResourceUsage usage, string resourceName)
        {
            return usage != null &&
                usage.PassName == "Burt Screen Space Subsurface Setup Tiles" &&
                resourceName == BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName;
        }

        private static void ValidateSamePassGlobalReadWrite(BurtRenderPassResourceUsage usage) // Checks same-pass logical global read/write declarations.
        {
            var reads = usage.ReadGlobalResources;
            var writes = usage.WriteGlobalResources;

            for (var readIndex = 0; readIndex < reads.Count; readIndex++)
            {
                var resourceName = reads[readIndex];
                if (string.IsNullOrEmpty(resourceName))
                {
                    continue;
                }

                if (ContainsString(writes, resourceName))
                {
                    usage.AddValidationMessage("同一 Pass 同时 Read/Write Global: " + resourceName + "，请确认全局状态更新顺序是有意的。");
                }
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

                if (usage.PassKind != BurtRenderPassKind.Release) // Release pass 只是结束生命周期，不应掩盖“写了没人真正使用”的资源。
                {
                    state.AddConsumer(resourceName); // 记录消费者，用于最终资源摘要和孤立写入检查。
                }

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
                if (ContainsString(usage.AllowUnconsumedWriteResources, resourceName))
                {
                    state.AllowUnconsumedWrite(resourceName);
                }
            }
        }

        private static void ValidateReadBufferResources( // Checks logical buffer reads against prior producers or external imports.
            BurtRenderPassResourceUsage usage,
            ResourceValidationState state)
        {
            var reads = usage.ReadBuffers;
            for (var readIndex = 0; readIndex < reads.Count; readIndex++)
            {
                var handle = reads[readIndex];
                var resourceName = handle.Name;

                if (!CanTrackBuffer(handle, resourceName))
                {
                    continue;
                }

                if (usage.PassKind != BurtRenderPassKind.Release)
                {
                    state.AddConsumer(resourceName);
                }

                if (state.IsExternal(resourceName))
                {
                    continue;
                }

                if (!state.HasProducer(resourceName))
                {
                    usage.AddValidationMessage("Read-before-Write Buffer: " + resourceName + " 在读取前没有前序生产者。");
                }
            }
        }

        private static void ValidateWriteBufferResources( // Records logical buffer producers for future tiled/cluster passes.
            BurtRenderPassResourceUsage usage,
            ResourceValidationState state)
        {
            var writes = usage.WriteBuffers;
            for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++)
            {
                var handle = writes[writeIndex];
                var resourceName = handle.Name;

                if (!CanTrackBuffer(handle, resourceName))
                {
                    continue;
                }

                if (state.IsExternal(resourceName))
                {
                    usage.AddValidationMessage("写入外部 Buffer: " + resourceName + "，请确认它不是漏注册的图内临时 Buffer。");
                }

                state.AddProducer(resourceName);
                if (ContainsString(usage.AllowUnconsumedWriteResources, resourceName))
                {
                    state.AllowUnconsumedWrite(resourceName);
                }
            }
        }

        private static void ValidateReadGlobalResources( // 检查单个 Pass 的逻辑全局资源读取是否已经有前序生产者。
            BurtRenderPassResourceUsage usage, // 接收当前 Pass 的资源使用记录。
            ResourceValidationState state) // 接收跨 Pass 的资源状态。
        {
            var reads = usage.ReadGlobalResources; // 缓存逻辑全局资源读取列表，减少属性访问并提升可读性。

            for (var readIndex = 0; readIndex < reads.Count; readIndex++) // 遍历当前 Pass 声明读取的所有全局资源。
            {
                var resourceName = reads[readIndex]; // 取出当前逻辑全局资源名。

                if (string.IsNullOrEmpty(resourceName)) // 空名问题已经在 Usage 记录，这里避免重复刷屏。
                {
                    continue; // 跳过不可追踪资源。
                }

                state.AddConsumer(resourceName); // 记录消费者，用于最终资源摘要和孤立写入检查。

                if (!state.HasProducer(resourceName)) // 逻辑全局资源没有外部生产者，必须由前序 Pass 写入。
                {
                    usage.AddValidationMessage("Read-before-Write Global: " + resourceName + " 在读取前没有前序生产者。"); // 记录全局状态顺序问题但不改变执行顺序。
                }
            }
        }

        private static void ValidateWriteGlobalResources( // 记录单个 Pass 的逻辑全局资源写入。
            BurtRenderPassResourceUsage usage, // 接收当前 Pass 的资源使用记录。
            ResourceValidationState state) // 接收跨 Pass 的资源状态。
        {
            var writes = usage.WriteGlobalResources; // 缓存逻辑全局资源写入列表，减少属性访问并提升可读性。

            for (var writeIndex = 0; writeIndex < writes.Count; writeIndex++) // 遍历当前 Pass 声明写入的所有全局资源。
            {
                var resourceName = writes[writeIndex]; // 取出当前逻辑全局资源名。

                if (string.IsNullOrEmpty(resourceName)) // 空名问题已经在 Usage 记录，这里避免重复刷屏。
                {
                    continue; // 跳过不可追踪资源。
                }

                state.AddProducer(resourceName); // 记录全局资源已经被当前或前序 Pass 写入，供后续读取校验。
                if (ContainsString(usage.AllowUnconsumedWriteResources, resourceName))
                {
                    state.AllowUnconsumedWrite(resourceName);
                }
            }
        }

        private static void ValidateTypedRenderTargetLifecycle( // Checks typed RT access order without changing execution.
            BurtRenderPassResourceUsage usage,
            ResourceValidationState state)
        {
            var accesses = usage.RenderTargetAccesses;
            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var handle = access.Handle;
                var resourceName = handle.Name;

                if (!CanTrackResource(handle, resourceName))
                {
                    continue;
                }

                ValidateTypedAccessLifecycle(usage, state, resourceName, "RenderTarget", access.AccessType);
            }
        }

        private static void ValidateTypedBufferLifecycle( // Checks typed logical buffer access order without changing execution.
            BurtRenderPassResourceUsage usage,
            ResourceValidationState state)
        {
            var accesses = usage.BufferAccesses;
            for (var accessIndex = 0; accessIndex < accesses.Count; accessIndex++)
            {
                var access = accesses[accessIndex];
                var handle = access.Handle;
                var resourceName = handle.Name;

                if (!CanTrackBuffer(handle, resourceName))
                {
                    continue;
                }

                if (access.AccessType == BurtRenderResourceAccessType.Allocate && !state.HasValidBufferDescriptor(resourceName))
                {
                    usage.AddValidationMessage("Buffer Allocate 缺少有效 Descriptor: " + resourceName + "。");
                }

                ValidateTypedAccessLifecycle(usage, state, resourceName, "Buffer", access.AccessType);
            }
        }

        private static void ValidateTypedAccessLifecycle( // Central lifecycle rules shared by RTs and logical buffers.
            BurtRenderPassResourceUsage usage,
            ResourceValidationState state,
            string resourceName,
            string resourceKind,
            BurtRenderResourceAccessType accessType)
        {
            switch (accessType)
            {
                case BurtRenderResourceAccessType.Allocate:
                    if (state.IsAllocated(resourceName) && !state.IsReleased(resourceName))
                    {
                        usage.AddValidationMessage("重复 Allocate " + resourceKind + ": " + resourceName + "，前一次生命周期尚未 Release。");
                    }

                    state.MarkAllocated(resourceName);
                    state.MarkProduced(resourceName);
                    break;
                case BurtRenderResourceAccessType.Release:
                    if (!state.IsExternal(resourceName) && !state.HasProducer(resourceName))
                    {
                        usage.AddValidationMessage("Release-before-Producer " + resourceKind + ": " + resourceName + "。");
                    }

                    if (!state.IsExternal(resourceName) &&
                        !state.IsAllocated(resourceName) &&
                        !IsStackSharedRenderTargetResource(resourceName))
                    {
                        usage.AddValidationMessage("Release-without-Allocate " + resourceKind + ": " + resourceName + "。");
                    }

                    if (state.IsReleased(resourceName))
                    {
                        usage.AddValidationMessage("重复 Release " + resourceKind + ": " + resourceName + "。");
                    }

                    state.MarkReleased(resourceName);
                    break;
                case BurtRenderResourceAccessType.Read:
                    if (state.IsReleased(resourceName))
                    {
                        usage.AddValidationMessage("Read-after-Release " + resourceKind + ": " + resourceName + "。");
                    }
                    break;
                case BurtRenderResourceAccessType.Bind:
                case BurtRenderResourceAccessType.Clear:
                case BurtRenderResourceAccessType.Copy:
                case BurtRenderResourceAccessType.Write:
                    if (state.IsReleased(resourceName))
                    {
                        usage.AddValidationMessage("Write-after-Release " + resourceKind + ": " + resourceName + "，请确认是否缺少重新 Allocate。");
                    }

                    state.MarkProduced(resourceName);
                    break;
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
                    if (state.HasAllowedUnconsumedWrite(resourceName))
                    {
                        continue;
                    }

                    graphLog.Add("资源写入后没有消费者: " + resourceName + "。"); // 图级提示，避免归咎于某一个具体 Pass。
                }
            }

            foreach (var resourceName in state.AllocatedResources) // 遍历明确 Allocate 过的内部资源，检查生命周期是否闭合。
            {
                if (state.IsExternal(resourceName) || IsStackSharedRenderTargetResource(resourceName)) // 相机栈共享资源可由栈末 request 释放，当前图不强制要求闭合。
                {
                    continue;
                }

                if (!state.IsReleased(resourceName))
                {
                    graphLog.Add("资源 Allocate 后没有 Release: " + resourceName + "。"); // 提示临时 RT/Buffer 生命周期可能泄漏。
                }
            }
        }

        private static bool IsStackSharedRenderTargetResource(string resourceName) // CameraColor/Depth can intentionally live across stacked camera requests.
        {
            return resourceName == BurtRenderGraphResourceRegistry.CameraColorName ||
                   resourceName == BurtRenderGraphResourceRegistry.CameraDepthName;
        }

        private static bool CanTrackResource( // 判断资源句柄是否适合参与跨 Pass 依赖追踪。
            BurtRenderTargetHandle handle, // 接收资源句柄。
            string resourceName) // 接收资源名，避免重复访问属性。
        {
            return handle.IsValid && !string.IsNullOrEmpty(resourceName); // 无效句柄或空名已在 Usage 里记录，跨 Pass 校验不重复报告。
        }

        private static bool CanTrackBuffer(BurtRenderBufferHandle handle, string resourceName) // Checks whether a logical buffer can participate in dependency tracking.
        {
            return handle.IsValid && !string.IsNullOrEmpty(resourceName);
        }

        private static bool ContainsString(IReadOnlyList<string> values, string expectedValue)
        {
            if (values == null)
            {
                return false;
            }

            for (var valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                if (values[valueIndex] == expectedValue)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsBufferResourceName(IReadOnlyList<BurtRenderBufferHandle> handles, string resourceName)
        {
            if (handles == null)
            {
                return false;
            }

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++)
            {
                if (handles[handleIndex].Name == resourceName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsRenderTargetResourceName(IReadOnlyList<BurtRenderTargetHandle> handles, string resourceName)
        {
            if (handles == null)
            {
                return false;
            }

            for (var handleIndex = 0; handleIndex < handles.Count; handleIndex++)
            {
                if (handles[handleIndex].Name == resourceName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsWrittenResourceName(BurtRenderPassResourceUsage usage, string resourceName)
        {
            return ContainsRenderTargetResourceName(usage.WriteRenderTargets, resourceName) ||
                   ContainsBufferResourceName(usage.WriteBuffers, resourceName) ||
                   ContainsString(usage.WriteGlobalResources, resourceName);
        }

        private sealed class ResourceValidationState // 定义跨 Pass 资源状态表，用来追踪生产者和消费者。
        {
            private readonly BurtRenderGraphResourceRegistry resources; // 保存资源注册表，用于判断外部导入资源。
            private readonly HashSet<string> producedResources = new HashSet<string>(); // 保存已经由图内 Pass 写入过的资源名。
            private readonly HashSet<string> consumedResources = new HashSet<string>(); // 保存已经被图内 Pass 读取过的资源名。
            private readonly HashSet<string> allocatedResources = new HashSet<string>(); // Tracks resources that have entered an allocated lifetime.
            private readonly HashSet<string> releasedResources = new HashSet<string>(); // Tracks resources already released in the current lifetime.

            private readonly HashSet<string> allowedUnconsumedWrites = new HashSet<string>(); // Resources intentionally ending as side-effect writes.

            public IEnumerable<string> ProducedResources => producedResources; // 暴露已生产资源枚举，供最终孤立写入检查使用。

            public IEnumerable<string> AllocatedResources => allocatedResources; // Exposes allocated resources for final lifecycle closure checks.

            public ResourceValidationState(BurtRenderGraphResourceRegistry resources) // 创建状态表。
            {
                this.resources = resources; // 保存资源注册表引用；为空时所有资源都按图内资源处理。
            }

            public bool IsExternal(string resourceName) // 判断资源是否由 RenderGraph 外部导入。
            {
                return resources != null && (resources.IsExternalRenderTarget(resourceName) || resources.IsExternalBuffer(resourceName)); // 资源表为空时不把任何资源误判为外部资源。
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
                MarkProduced(resourceName); // 当前只关心是否存在生产者，具体 Pass 明细已在 dump 的 Passes 段落里展示。
            }

            public void AddConsumer(string resourceName) // 记录资源消费者。
            {
                consumedResources.Add(resourceName); // 当前只关心是否存在消费者，避免诊断状态额外分配复杂结构。
            }

            public void AllowUnconsumedWrite(string resourceName)
            {
                allowedUnconsumedWrites.Add(resourceName);
            }

            public bool HasAllowedUnconsumedWrite(string resourceName)
            {
                return allowedUnconsumedWrites.Contains(resourceName);
            }

            public bool IsAllocated(string resourceName)
            {
                return allocatedResources.Contains(resourceName);
            }

            public bool IsReleased(string resourceName)
            {
                return releasedResources.Contains(resourceName);
            }

            public bool HasValidBufferDescriptor(string resourceName)
            {
                return resources != null && resources.HasValidBufferDescriptor(resourceName);
            }

            public void MarkAllocated(string resourceName)
            {
                allocatedResources.Add(resourceName);
                releasedResources.Remove(resourceName);
            }

            public void MarkProduced(string resourceName)
            {
                producedResources.Add(resourceName);
            }

            public void MarkReleased(string resourceName)
            {
                releasedResources.Add(resourceName);
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
