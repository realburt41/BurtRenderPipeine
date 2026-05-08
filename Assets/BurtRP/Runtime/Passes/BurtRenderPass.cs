namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个 Pass 基类和其他 BurtRP 代码处在同一个模块里。
{
    public abstract class BurtRenderPass // 定义 BurtRP 的渲染 Pass 基类，所有具体渲染步骤都继承它。
    {
        public abstract string Name { get; } // 定义 Pass 名称，后面用于 Frame Debugger、Profiler 或日志显示。

        public virtual void Configure(BurtRenderPassBuilder builder) // 定义 Pass 配置阶段入口，用来声明资源读写关系。
        {
        } // 默认实现为空，表示这个 Pass 暂时没有声明任何资源读写。

        public abstract void Execute( // 定义 Pass 的执行入口，每个具体 Pass 都必须实现这个函数。
            BurtRenderGraphContext context); // 接收 RenderGraph 执行上下文，里面包含 ScriptableContext、Request、Asset、资源表。
    }
}
