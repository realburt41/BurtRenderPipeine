using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类可以直接访问 BurtRenderPass、BurtRenderRequest 等类型。
{
    public sealed class BurtForwardGraphAssembler : BurtRenderGraphAssembler
    {
        private readonly BurtRenderPass setRenderTargetPass = new BurtSetRenderTargetPass(); // 创建设置渲染目标 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass clearRenderTargetPass = new BurtClearRenderTargetPass(); // 创建清屏 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawOpaquePass = new BurtDrawOpaquePass(); // 创建不透明物体绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawSkyboxPass = new BurtDrawSkyboxPass(); // 创建天空盒绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawTransparentPass = new BurtDrawTransparentPass(); // 创建透明物体绘制 Pass，并在整个管线生命周期内复用它。
        
        public override string Name => "Burt Forward Graph Assembler"; // 返回当前组装器名称，方便后续调试和性能标记。

        public override void Assemble( // 实现基类定义的组装函数。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset) // 接收管线资产配置，当前阶段暂时不使用它。
        { // 开始 Assemble 函数体。
            if (graph == null) // 如果 graph 为空，说明调用方没有提供可写入的 RenderGraph。
            { // 开始空 graph 保护分支。
                return; // 直接结束组装，避免空引用错误。
            } // 结束空 graph 保护分支。

            if (request == null) // 如果 request 为空，说明调用方传入了异常数据。
            { // 开始空 request 保护分支。
                return; // 直接结束组装。
            } // 结束空 request 保护分支。

            if (!request.IsValid) // 如果 request 被标记为无效，说明它不应该被渲染。
            { // 开始无效 request 保护分支。
                return; // 直接结束组装，不添加任何 Pass。
            } // 结束无效 request 保护分支。

            if (request.Camera == null) // 如果 request 没有关联 Camera，当前 Forward 流程无法执行。
            { // 开始空 Camera 保护分支。
                return; // 直接结束组装。
            } // 结束空 Camera 保护分支。

            graph.AddPass(setRenderTargetPass); // 把设置渲染目标 Pass 添加到 RenderGraph，保证后续 Pass 画到正确目标。

            graph.AddPass(clearRenderTargetPass); // 把清屏 Pass 添加到 RenderGraph，保证颜色和深度状态可控。

            graph.AddPass(drawOpaquePass); // 把不透明物体绘制 Pass 添加到 RenderGraph。

            graph.AddPass(drawSkyboxPass); // 把天空盒 Pass 添加到 RenderGraph，由 Pass 自己决定是否真正绘制。

            graph.AddPass(drawTransparentPass); // 把透明物体绘制 Pass 添加到 RenderGraph。
        } // 结束 Assemble 函数体
    }
}