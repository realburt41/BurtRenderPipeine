using UnityEngine; // 引入 UnityEngine 命名空间，当前文件保持 Unity 运行时代码依赖一致。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类可以直接访问 BurtRenderPass、BurtRenderRequest 等类型。
{
    public sealed class BurtForwardGraphAssembler : BurtRenderGraphAssembler // 定义 BurtRP 的前向渲染图组装器，负责决定普通相机要执行哪些 Pass。
    {
        private readonly BurtRenderPass allocateCameraColorPass = new BurtAllocateCameraColorPass(); // 创建 CameraColor 分配 Pass，用来为当前相机申请 BurtRP 自己管理的中间颜色 RT。

        private readonly BurtRenderPass allocateCameraDepthPass = new BurtAllocateCameraDepthPass(); // 创建 CameraDepth 分配 Pass，用来为当前相机申请独立深度 RT。

        private readonly BurtRenderPass allocateMainLightShadowMapPass = new BurtAllocateMainLightShadowMapPass(); // 创建主光阴影图分配 Pass，用来为开启阴影的主光申请 shadow map。

        private readonly BurtRenderPass drawMainLightShadowCasterPass = new BurtDrawMainLightShadowCasterPass(); // 创建主光阴影投射 Pass，用来把 ShadowCaster 物体写入主光 shadow map。



        private readonly BurtRenderPass setRenderTargetPass = new BurtSetRenderTargetPass(); // 创建设置渲染目标 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass clearRenderTargetPass = new BurtClearRenderTargetPass(); // 创建清屏 Pass，并在整个管线生命周期内复用它。


        private readonly BurtRenderPass setupLightingPass = new BurtSetupLightingPass(); // Reuses the pass that uploads BurtRP global lighting data before scene drawing.
        private readonly BurtRenderPass depthPrepass = new BurtDepthPrepass(); // 创建深度预写 Pass，用来在颜色绘制前先建立 CameraDepth。

        private readonly BurtRenderPass drawOpaquePass = new BurtDrawOpaquePass(); // 创建不透明物体绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawSkyboxPass = new BurtDrawSkyboxPass(); // 创建天空盒绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawTransparentPass = new BurtDrawTransparentPass(); // 创建透明物体绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawUnsupportedShadersPass = new BurtDrawUnsupportedShadersPass(); // Reuses the pass that renders unsupported shaders with an obvious error material.
        private readonly BurtRenderPass debugCameraDepthPass = new BurtDebugCameraDepthPass(); // 创建 CameraDepth 调试 Pass，用来把深度纹理画到中间颜色目标上。

        private readonly BurtRenderPass debugMainLightShadowMapPass = new BurtDebugMainLightShadowMapPass(); // 创建主光 shadow map 调试 Pass，用来检查阴影图是否写入了内容。

        private readonly BurtRenderPass finalBlitPass = new BurtFinalBlitPass(); // 创建最终拷贝 Pass，用来把中间 CameraColor 输出到 request 指定的最终目标。

        private readonly BurtRenderPass releaseMainLightShadowMapPass = new BurtReleaseMainLightShadowMapPass(); // 创建主光阴影图释放 Pass，用来在当前相机渲染结束后释放 shadow map 临时 RT。


        private readonly BurtRenderPass releaseCameraColorPass = new BurtReleaseCameraColorPass(); // 创建 CameraColor 释放 Pass，用来在 FinalBlit 完成后释放临时颜色 RT。

        private readonly BurtRenderPass releaseCameraDepthPass = new BurtReleaseCameraDepthPass(); // 创建 CameraDepth 释放 Pass，用来在当前相机渲染结束后释放临时深度 RT。

        public override string Name => "Burt Forward Graph Assembler"; // 返回当前组装器名称，方便后续调试和性能标记。

        public override void Assemble( // 实现基类定义的组装函数。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset) // 接收管线资产配置，用来决定是否启用 Depth Prepass 等功能。
        {
            if (graph == null) // 如果 graph 为空，说明调用方没有提供可写入的 RenderGraph。
            {
                return; // 直接结束组装，避免空引用错误。
            }

            if (request == null) // 如果 request 为空，说明调用方传入了异常数据。
            {
                return; // 直接结束组装。
            }

            if (!request.IsValid) // 如果 request 被标记为无效，说明它不应该被渲染。
            {
                return; // 直接结束组装，不添加任何 Pass。
            }

            if (request.Camera == null) // 如果 request 没有关联 Camera，当前 Forward 流程无法执行。
            {
                return; // 直接结束组装。
            }

            var useMainLightShadow = BurtShadowUtility.ShouldUseMainLightShadow(request, asset); // 合并 Light 与 PipelineAsset 设置后判断本相机是否需要主光阴影。

            var mainLightShadowMapIsValid = graph.Resources.GetMainLightShadowMap().IsValid; // 读取 RenderGraph 中 MainLightShadowMap 句柄是否有效，诊断资源注册是否符合阴影决策。

            BurtShadowUtility.LogMainLightShadowDiagnostics(request, asset, useMainLightShadow, mainLightShadowMapIsValid); // 在阴影启用决策完成后输出一次受资产开关控制的结构化诊断日志。

            graph.AddPass(allocateCameraColorPass); // 先把 CameraColor 分配 Pass 添加到 RenderGraph，保证后续场景绘制写入 BurtRP 管理的中间颜色 RT。

            graph.AddPass(allocateCameraDepthPass); // 再把 CameraDepth 分配 Pass 添加到 RenderGraph，保证后续绑定深度目标时 RT 已经存在。

            graph.AddPass(setupLightingPass); // 在阴影 Pass 前上传灯光和阴影默认全局参数，避免上一帧或上一相机的阴影状态残留。

            if (useMainLightShadow) // 如果当前 request 的主光需要阴影，就把 shadow map 生命周期加入图里。
            {
                graph.AddPass(allocateMainLightShadowMapPass); // 在相机颜色目标绑定前申请主光阴影图，后续 ShadowCaster Pass 会先写它。
                graph.AddPass(drawMainLightShadowCasterPass); // 立刻绘制主光 ShadowCaster，把阴影深度写进刚申请的 MainLightShadowMap。
            }


            graph.AddPass(setRenderTargetPass); // 把设置渲染目标 Pass 添加到 RenderGraph，保证后续 Pass 画到正确目标。

            graph.AddPass(clearRenderTargetPass); // 把清屏 Pass 添加到 RenderGraph，保证颜色和深度状态可控。

            if (ShouldUseDepthPrepass(asset)) // 如果管线资产允许 Depth Prepass，就把深度预写阶段加入图中。
            {
                graph.AddPass(depthPrepass); // 把深度预写 Pass 添加到 RenderGraph，让不透明物体先写入 CameraDepth。
            }

            graph.AddPass(drawOpaquePass); // 把不透明物体绘制 Pass 添加到 RenderGraph，让它在已有深度基础上写入颜色。

            graph.AddPass(drawSkyboxPass); // 把天空盒 Pass 添加到 RenderGraph，由 Pass 自己决定是否真正绘制。

            graph.AddPass(drawTransparentPass); // 把透明物体绘制 Pass 添加到 RenderGraph，让透明物体最后做混合。

            if (ShouldUseUnsupportedShaderDebug(asset)) // Adds unsupported-shader debugging after normal scene rendering when the asset enables it.
            {
                graph.AddPass(drawUnsupportedShadersPass); // Adds the unsupported-shader pass so non-BurtRP materials become easy to find.
            }

            if (ShouldUseDepthDebugView(asset)) // 如果管线资产开启了深度调试视图，就在释放深度 RT 之前插入可视化 Pass。
            {
                graph.AddPass(debugCameraDepthPass); // 把 CameraDepth 调试 Pass 添加到 RenderGraph，让它读取深度并覆盖 CameraColor。
            }

            if (ShouldUseMainLightShadowDebugView(asset, useMainLightShadow)) // 如果资产开启了主光阴影调试视图，并且当前相机真的生成了 shadow map。
            {
                graph.AddPass(debugMainLightShadowMapPass); // 把主光 shadow map 调试 Pass 添加到 RenderGraph，方便直接检查阴影图内容。
            }

            graph.AddPass(finalBlitPass); // 把中间 CameraColor 拷贝到 request.TargetIdentifier，完成 BurtRP 内部 RT 到最终输出目标的交接。

            if (useMainLightShadow) // 如果当前 request 申请过主光阴影图，就在相机渲染结束前释放它。
            {
                graph.AddPass(releaseMainLightShadowMapPass); // 释放主光阴影图临时 RT，确保阴影资源生命周期被 RenderGraph 明确管理。
            }

            graph.AddPass(releaseCameraColorPass); // 在 FinalBlit 之后释放 CameraColor 临时颜色 RT，避免下一次 request 误用旧内容。

            graph.AddPass(releaseCameraDepthPass); // 最后把 CameraDepth 释放 Pass 添加到 RenderGraph，避免临时 RT 泄漏到下一次 request。
        }

        private static bool ShouldUseDepthPrepass(BurtRenderPipelineAsset asset) // 定义判断是否启用 Depth Prepass 的辅助函数。
        {
            if (asset == null) // 如果资产为空，说明当前没有配置来源。
            {
                return true; // Defaults depth prepass on when the asset is missing, matching the tutorial pipeline behavior.
            }

            return asset.EnableDepthPrepass; // 返回资产 Inspector 上配置的 Depth Prepass 开关。
        }

        private static bool ShouldUseUnsupportedShaderDebug(BurtRenderPipelineAsset asset) // Returns whether unsupported shader debugging should be inserted into the graph.
        {
            if (asset == null) // Handles a missing asset as a safe debug-friendly fallback.
            {
                return true; // Keeps unsupported shaders visible instead of letting them disappear silently.
            }

            return asset.EnableUnsupportedShaderDebug; // Uses the Inspector toggle stored on the pipeline asset.
        }

        private static bool ShouldUseDepthDebugView(BurtRenderPipelineAsset asset) // 定义判断是否启用 CameraDepth 调试视图的辅助函数。
        {
            if (asset == null) // 如果资产为空，说明当前没有 Inspector 配置来源。
            {
                return false; // 默认关闭调试视图，避免在异常配置下覆盖正常颜色输出。
            }

            return asset.EnableDepthDebugView; // 返回资产 Inspector 上配置的深度调试开关。
        }

        private static bool ShouldUseMainLightShadowDebugView( // 判断是否启用主光 shadow map 调试视图。
            BurtRenderPipelineAsset asset, // 接收管线资产，读取调试开关。
            bool useMainLightShadow) // 接收已经计算好的主光阴影启用状态，避免重复合并阴影数据。
        {
            if (!useMainLightShadow) // 如果当前相机没有生成 shadow map，就没有可视化目标。
            {
                return false; // 返回 false，避免 debug pass 读取无效资源。
            }

            if (asset == null) // 如果资产为空，说明没有 Inspector 开关来源。
            {
                return false; // 默认关闭 shadow map 调试，避免覆盖正常画面。
            }

            return asset.EnableMainLightShadowDebugView; // 使用资产上的主光 shadow map 调试开关。
        }
    }
}
