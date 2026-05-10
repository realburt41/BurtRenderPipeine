namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Deferred 组装器和其他运行时代码处在同一模块。
{
    public sealed class BurtDeferredGraphAssembler : BurtRenderGraphAssembler // 定义 BurtRP 的 Deferred 渲染图组装器，当前阶段先搭 GBuffer 生命周期骨架。
    {
        private readonly BurtRenderGraphAssembler forwardFallbackAssembler = new BurtForwardGraphAssembler(); // 临时复用 Forward 组装器，保证 Deferred 实验模式当前仍能输出稳定画面。

        private readonly BurtRenderPass allocateGBuffer0Pass = new BurtAllocateGBuffer0Pass(); // 创建 GBuffer0 分配 Pass，用来申请 Deferred 第一张材质缓存。

        private readonly BurtRenderPass allocateGBuffer1Pass = new BurtAllocateGBuffer1Pass(); // 创建 GBuffer1 分配 Pass，用来申请 Deferred 第二张材质缓存。

        private readonly BurtRenderPass allocateGBuffer2Pass = new BurtAllocateGBuffer2Pass(); // 创建 GBuffer2 分配 Pass，用来申请 Deferred 第三张材质缓存。

        private readonly BurtRenderPass releaseGBuffer0Pass = new BurtReleaseGBuffer0Pass(); // 创建 GBuffer0 释放 Pass，用来结束 Deferred 第一张材质缓存生命周期。

        private readonly BurtRenderPass releaseGBuffer1Pass = new BurtReleaseGBuffer1Pass(); // 创建 GBuffer1 释放 Pass，用来结束 Deferred 第二张材质缓存生命周期。

        private readonly BurtRenderPass releaseGBuffer2Pass = new BurtReleaseGBuffer2Pass(); // 创建 GBuffer2 释放 Pass，用来结束 Deferred 第三张材质缓存生命周期。

        public override string Name => "Burt Deferred Graph Assembler"; // 返回当前组装器名称，方便后续调试和性能标记。

        public override void Assemble( // 实现旧组装入口，保证外部未传入执行选项时仍然能组装 Deferred 实验图。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset) // 接收管线资产配置，用来决定是否启用后处理、阴影等功能。
        {
            Assemble(graph, request, asset, BurtRequestRenderOptions.CreateSingleRequest()); // 把旧入口转发到新入口，并使用旧行为默认选项。
        }

        public override void Assemble( // 实现带栈级执行选项的组装函数。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset, // 接收管线资产配置，用来决定 Renderer Mode 和后续开关。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RenderTarget 生命周期选项。
        {
            if (graph == null) // 如果 graph 为空，说明调用方没有提供可写入的 RenderGraph。
            {
                return; // 直接结束组装，避免空引用错误。
            }

            if (request == null) // 如果 request 为空，说明调用方传入了异常数据。
            {
                return; // 直接结束组装，不添加任何 Pass。
            }

            if (!request.IsValid) // 如果 request 被标记为无效，说明它不应该被渲染。
            {
                return; // 直接结束组装，不添加任何 Pass。
            }

            if (request.Camera == null) // 如果 request 没有关联 Camera，当前 Deferred 实验流程无法执行。
            {
                return; // 直接结束组装，避免后续 Pass 访问空相机。
            }

            AddGBufferAllocatePasses(graph); // 先插入 GBuffer 分配 Pass，让资源生命周期在 RenderGraph Debug 里可见。

            forwardFallbackAssembler.Assemble(graph, request, asset, renderOptions); // 当前阶段仍复用 Forward 完成真实画面输出，避免 Deferred 骨架影响现有渲染结果。

            AddGBufferReleasePasses(graph); // 最后插入 GBuffer 释放 Pass，保证实验资源不会泄漏到下一帧。
        }

        private void AddGBufferAllocatePasses(BurtRenderGraph graph) // 添加三张 GBuffer 的分配 Pass。
        {
            graph.AddPass(allocateGBuffer0Pass); // 添加 GBuffer0 分配 Pass，后续会保存 baseColor 和 occlusion。

            graph.AddPass(allocateGBuffer1Pass); // 添加 GBuffer1 分配 Pass，后续会保存 normal、metallic 和 smoothness。

            graph.AddPass(allocateGBuffer2Pass); // 添加 GBuffer2 分配 Pass，后续会保存 emission 和 reflectance。
        }

        private void AddGBufferReleasePasses(BurtRenderGraph graph) // 添加三张 GBuffer 的释放 Pass。
        {
            graph.AddPass(releaseGBuffer2Pass); // 先释放 GBuffer2，释放顺序和申请顺序相反，便于未来排查生命周期。

            graph.AddPass(releaseGBuffer1Pass); // 释放 GBuffer1，结束第二张 Deferred 材质缓存生命周期。

            graph.AddPass(releaseGBuffer0Pass); // 最后释放 GBuffer0，结束第一张 Deferred 材质缓存生命周期。
        }
    }
}
