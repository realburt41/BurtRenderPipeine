using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 ScriptableRenderContext。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，和其他 BurtRP 运行时代码保持一致。
{
    public sealed class BurtCameraRenderer // 定义单个 request 的执行器，它负责驱动 Assembler 和 RenderGraph。
    {
        private readonly BurtRenderGraph renderGraph = new BurtRenderGraph(); // 创建一个可复用的 RenderGraph，用来承载当前 request 的 Pass 列表和资源表。

        public void Render( // 定义渲染入口函数。
            ScriptableRenderContext context, // 接收 Unity SRP 提供的渲染上下文。
            BurtRenderRequest request, // 接收已经构建好的 Burt 渲染请求。
            BurtRenderPipelineAsset asset) // 接收 BurtRP 管线资产配置。
        {
            if (request == null) // 如果 request 为空，说明调用方传入了异常数据。
            {
                return; // 直接结束函数，避免后续访问空对象。
            }

            if (!request.IsValid) // 如果 request 被标记为无效，说明它不应该被执行。
            {
                return; // 直接结束函数，不执行任何渲染。
            }

            if (request.Camera == null) // 如果 request 没有关联相机，当前阶段无法渲染。
            {
                return; // 直接结束函数。
            }

            if (request.GraphAssembler == null) // 如果 request 没有设置组装器，说明管线还不知道如何渲染它。
            {
                return; // 直接结束函数，避免执行未知渲染流程。
            }

            context.SetupCameraProperties(request.Camera); // 设置当前相机的矩阵、裁剪参数和 Unity 内置 shader 变量。

            renderGraph.Clear(); // 清空上一次 request 留下的 Pass 和资源，准备组装当前 request 的图。

            renderGraph.ImportRequestResources(request); // 把 request 的基础渲染目标导入 RenderGraph 资源表，例如 CameraColor。

            request.GraphAssembler.Assemble(renderGraph, request, asset); // 让当前 request 指定的 Assembler 把 Pass 添加到 RenderGraph。

            var graphContext = new BurtRenderGraphContext(context, request, asset, renderGraph.Resources); // 创建 RenderGraph 执行上下文，并把资源注册表传给每个 Pass。

            renderGraph.Execute(graphContext); // 执行 RenderGraph 里已经组装好的所有 Pass。

            context.Submit(); // 把当前 request 累积的所有渲染命令提交给 Unity 执行。
        }
    }
}
