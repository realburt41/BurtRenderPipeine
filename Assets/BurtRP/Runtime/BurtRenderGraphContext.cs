using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 ScriptableRenderContext。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个上下文类和其他 BurtRP 代码处在同一个模块里。
{
    public sealed class BurtRenderGraphContext // 定义 RenderGraph 执行上下文，用来打包一次图执行需要的公共数据和资源表。
    {
        public ScriptableRenderContext ScriptableContext { get; } // 保存 Unity SRP 的渲染上下文，Pass 通过它提交绘制命令。

        public BurtRenderRequest Request { get; } // 保存当前正在执行的渲染请求，Pass 通过它读取 Camera、CullingResults 等任务数据。

        public BurtRenderPipelineAsset Asset { get; } // 保存当前管线资产，Pass 通过它读取默认清屏色等全局配置。

        public BurtRenderGraphResourceRegistry ResourceRegistry { get; } // 保存当前 RenderGraph 的资源注册表，Pass 通过它读取图资源。

        public BurtRenderTargetHandle CameraColorTarget // 定义读取 CameraColor 的快捷属性，方便 Pass 不直接操作资源名。
        {
            get // 定义属性 getter，每次访问时从资源注册表读取最新的 CameraColor。
            {
                if (ResourceRegistry == null) // 如果资源注册表为空，说明当前上下文没有可用资源表。
                {
                    return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName); // 返回无效 CameraColor 句柄，避免 Pass 绑定错误目标。
                }

                return ResourceRegistry.GetCameraColor(); // 从资源注册表读取 CameraColor 句柄。
            }
        }

        public BurtRenderGraphContext( // 定义构造函数，用来创建一次 RenderGraph 执行上下文。
            ScriptableRenderContext scriptableContext, // 接收 Unity SRP 传入的渲染上下文。
            BurtRenderRequest request, // 接收当前正在执行的 Burt 渲染请求。
            BurtRenderPipelineAsset asset, // 接收 BurtRP 管线资产配置。
            BurtRenderGraphResourceRegistry resourceRegistry) // 接收当前 RenderGraph 的资源注册表。
        {
            ScriptableContext = scriptableContext; // 把 Unity SRP 渲染上下文保存到 ScriptableContext 属性里。

            Request = request; // 把当前渲染请求保存到 Request 属性里。

            Asset = asset; // 把管线资产保存到 Asset 属性里。

            ResourceRegistry = resourceRegistry; // 把 RenderGraph 的资源注册表保存到 ResourceRegistry 属性里。
        }
    }
}
