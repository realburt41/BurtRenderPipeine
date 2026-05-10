namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让组装器基类和具体组装器处在同一个模块里。
{
    public abstract class BurtRenderGraphAssembler // 定义 RenderGraph 组装器基类，负责把一次 request 转换成一串 RenderPass。
    {
        public abstract string Name { get; } // 定义组装器名称，用于调试、Frame Debugger、Profiler 或日志显示。

        public abstract void Assemble( // 保留旧组装入口，保证已有调用方不必立刻关心栈级执行选项。
            BurtRenderGraph graph, // 接收要被填充的 BurtRenderGraph。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断这次渲染任务是什么。
            BurtRenderPipelineAsset asset); // 接收管线资产配置，用来读取管线级开关和默认参数。

        public virtual void Assemble( // 定义新的组装入口，把相机栈计算出的 RT 生命周期选项传给具体组装器。
            BurtRenderGraph graph, // 接收要被填充的 BurtRenderGraph。
            BurtRenderRequest request, // 接收当前渲染请求，用来判断这次渲染任务是什么。
            BurtRenderPipelineAsset asset, // 接收管线资产配置，用来读取管线级开关和默认参数。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RenderTarget 生命周期选项。
        {
            Assemble(graph, request, asset); // 默认实现回退到旧入口，避免非 Forward 组装器在未适配时改变行为。
        }
    }
}
