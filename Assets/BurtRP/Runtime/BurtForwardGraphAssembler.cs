using UnityEngine; // 引入 UnityEngine 命名空间，当前文件保持 Unity 运行时代码依赖一致。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类可以直接访问 BurtRenderPass、BurtRenderRequest 等类型。
{
    public sealed class BurtForwardGraphAssembler : BurtRenderGraphAssembler // 定义 BurtRP 的前向渲染图组装器，负责决定普通相机要执行哪些 Pass。
    {
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
        private readonly BurtRenderPass debugCameraDepthPass = new BurtDebugCameraDepthPass(); // 创建 CameraDepth 调试 Pass，用来把深度纹理画到最终颜色目标上。

        private readonly BurtRenderPass releaseMainLightShadowMapPass = new BurtReleaseMainLightShadowMapPass(); // 创建主光阴影图释放 Pass，用来在当前相机渲染结束后释放 shadow map 临时 RT。


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

            graph.AddPass(allocateCameraDepthPass); // 先把 CameraDepth 分配 Pass 添加到 RenderGraph，保证后续绑定深度目标时 RT 已经存在。

            if (BurtShadowUtility.ShouldUseMainLightShadow(request)) // 如果当前 request 的主光需要阴影，就把 shadow map 生命周期加入图里。
            {
                graph.AddPass(allocateMainLightShadowMapPass); // 在相机颜色目标绑定前申请主光阴影图，后续 ShadowCaster Pass 会先写它。
                graph.AddPass(drawMainLightShadowCasterPass); // 立刻绘制主光 ShadowCaster，把阴影深度写进刚申请的 MainLightShadowMap。
            }


            graph.AddPass(setRenderTargetPass); // 把设置渲染目标 Pass 添加到 RenderGraph，保证后续 Pass 画到正确目标。

            graph.AddPass(clearRenderTargetPass); // 把清屏 Pass 添加到 RenderGraph，保证颜色和深度状态可控。


            graph.AddPass(setupLightingPass); // Uploads main light and ambient light globals before depth and color drawing use them.
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

            if (BurtShadowUtility.ShouldUseMainLightShadow(request)) // 如果当前 request 申请过主光阴影图，就在相机渲染结束前释放它。
            {
                graph.AddPass(releaseMainLightShadowMapPass); // 释放主光阴影图临时 RT，确保阴影资源生命周期被 RenderGraph 明确管理。
            }

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
    }
}
