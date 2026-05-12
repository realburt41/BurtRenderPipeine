using UnityEngine; // 引入 UnityEngine 命名空间，当前文件保持 Unity 运行时代码依赖一致。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个类可以直接访问 BurtRenderPass、BurtRenderRequest 等类型。
{
    public sealed class BurtForwardGraphAssembler : BurtRenderGraphAssembler // 定义 BurtRP 的前向渲染图组装器，负责决定普通相机要执行哪些 Pass。
    {
        private readonly BurtRenderPass allocateCameraColorPass = new BurtAllocateCameraColorPass(); // 创建 CameraColor 分配 Pass，用来为当前相机或当前相机栈申请中间颜色 RT。

        private readonly BurtRenderPass allocateCameraDepthPass = new BurtAllocateCameraDepthPass(); // 创建 CameraDepth 分配 Pass，用来为当前相机或当前相机栈申请独立深度 RT。

        private readonly BurtRenderPass allocateMainLightShadowMapPass = new BurtAllocateMainLightShadowMapPass(); // 创建主光阴影图分配 Pass，用来为开启阴影的主光申请 shadow map。

        private readonly BurtRenderPass drawMainLightShadowCasterPass = new BurtDrawMainLightShadowCasterPass(); // 创建主光阴影投射 Pass，用来把 ShadowCaster 物体写入主光 shadow map。

        private readonly BurtRenderPass setRenderTargetPass = new BurtSetRenderTargetPass(); // 创建设置渲染目标 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass seedOverlayCameraColorPass = new BurtSeedOverlayCameraColorPass(); // 创建 Overlay 颜色继承 Pass，让非共享 RT 的 Overlay 在不清颜色时先拿到最终目标内容。

        private readonly BurtRenderPass clearRenderTargetPass = new BurtClearRenderTargetPass(); // 创建清屏 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass setupLightingPass = new BurtSetupLightingPass(); // 创建灯光上传 Pass，用来在场景绘制前设置 BurtRP 全局灯光和阴影参数。

        private readonly BurtRenderPass depthPrepass = new BurtDepthPrepass(); // 创建深度预写 Pass，用来在颜色绘制前先建立 CameraDepth。

        private readonly BurtRenderPass drawOpaquePass = new BurtDrawOpaquePass(); // 创建不透明物体绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawEditorPreviewPass = new BurtDrawEditorPreviewPass(); // 创建编辑器 Preview 专用绘制 Pass，兼容 Unity 内部资产预览 shader。

        private readonly BurtRenderPass drawSkyboxPass = new BurtDrawSkyboxPass(); // 创建天空盒绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawTransparentPass = new BurtDrawTransparentPass(); // 创建透明物体绘制 Pass，并在整个管线生命周期内复用它。

        private readonly BurtRenderPass drawUnsupportedShadersPass = new BurtDrawUnsupportedShadersPass(); // 创建不支持 Shader 的调试 Pass，让非 BurtRP 材质显示为明显的错误材质。

        private readonly BurtRenderPass drawPreImageEffectsGizmosPass = new BurtDrawPreImageEffectsGizmosPass(); // 创建编辑器 Gizmos 绘制 Pass，恢复 SRP Scene/Game View 的 Gizmos 显示。

        private readonly BurtRenderPass drawPostImageEffectsGizmosPass = new BurtDrawPostImageEffectsGizmosPass(); // 创建后处理后的编辑器 Gizmos Pass，避免直接画到外部最终目标。

        private readonly BurtRenderPass allocatePostProcessColorPass = new BurtAllocatePostProcessColorPass(); // 创建后处理颜色分配 Pass，用来申请 PostProcessColor 中间 RT。

        private readonly BurtRenderPass postProcessPass = new BurtPostProcessPass(); // 创建第一版后处理 Pass，用来执行 No-op Copy 或 Tonemapping。

        private readonly BurtRenderPass releasePostProcessColorPass = new BurtReleasePostProcessColorPass(); // 创建后处理颜色释放 Pass，用来在后处理完成后释放 PostProcessColor。

        private readonly BurtRenderPass debugCameraDepthPass = new BurtDebugCameraDepthPass(); // 创建 CameraDepth 调试 Pass，用来把深度纹理画到中间颜色目标上。

        private readonly BurtRenderPass debugMainLightShadowMapPass = new BurtDebugMainLightShadowMapPass(); // 创建主光 shadow map 调试 Pass，用来检查阴影图是否写入了内容。

        private readonly BurtRenderPass finalBlitPass = new BurtFinalBlitPass(); // 创建最终拷贝 Pass，用来把中间 CameraColor 输出到 request 指定的最终目标。

        private readonly BurtRenderPass releaseMainLightShadowMapPass = new BurtReleaseMainLightShadowMapPass(); // 创建主光阴影图释放 Pass，用来在当前 request 渲染结束后释放 shadow map 临时 RT。

        private readonly BurtRenderPass releaseCameraColorPass = new BurtReleaseCameraColorPass(); // 创建 CameraColor 释放 Pass，用来在 FinalBlit 完成后释放临时颜色 RT。

        private readonly BurtRenderPass releaseCameraDepthPass = new BurtReleaseCameraDepthPass(); // 创建 CameraDepth 释放 Pass，用来在当前 request 或当前相机栈结束后释放临时深度 RT。

        public override string Name => "Burt Forward Graph Assembler"; // 返回当前组装器名称，方便后续调试和性能标记。

        public override void Assemble( // 实现旧组装入口，保证外部未传入执行选项时仍然使用旧的单 request RT 生命周期。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset) // 接收管线资产配置，用来决定是否启用 Depth Prepass 等功能。
        {
            Assemble(graph, request, asset, BurtRequestRenderOptions.CreateSingleRequest()); // 把旧入口转发到新入口，并使用旧行为默认选项。
        }

        public override void Assemble( // 实现带栈级执行选项的组装函数。
            BurtRenderGraph graph, // 接收要写入 Pass 的 RenderGraph。
            BurtRenderRequest request, // 接收当前正在组装的渲染请求。
            BurtRenderPipelineAsset asset, // 接收管线资产配置，用来决定是否启用 Depth Prepass 等功能。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RenderTarget 生命周期选项。
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

            var safeRenderOptions = renderOptions ?? BurtRequestRenderOptions.CreateSingleRequest(); // options 为空时使用旧行为，避免 RT 生命周期决策全部为 false。

            var useMainLightShadow = BurtShadowUtility.ShouldUseMainLightShadow(request, asset); // 合并 Light 与 PipelineAsset 设置后判断本相机是否需要主光阴影。

            var mainLightShadowMapIsValid = graph.Resources.GetMainLightShadowMap().IsValid; // 读取 RenderGraph 中 MainLightShadowMap 句柄是否有效，诊断资源注册是否符合阴影决策。

            BurtShadowUtility.LogMainLightShadowDiagnostics(request, asset, useMainLightShadow, mainLightShadowMapIsValid); // 在阴影启用决策完成后输出一次受资产开关控制的结构化诊断日志。

            if (safeRenderOptions.ShouldAllocateCameraColor) // 只有独立 request 或共享栈的第一个 request 才申请 CameraColor。
            {
                graph.AddPass(allocateCameraColorPass); // 把 CameraColor 分配 Pass 添加到 RenderGraph，保证后续场景绘制有中间颜色 RT 可写。
            }

            if (safeRenderOptions.ShouldAllocateCameraDepth) // 只有独立 request 或共享栈的第一个 request 才申请 CameraDepth。
            {
                graph.AddPass(allocateCameraDepthPass); // 把 CameraDepth 分配 Pass 添加到 RenderGraph，保证后续绑定深度目标时 RT 已经存在。
            }

            graph.AddPass(setupLightingPass); // 在阴影 Pass 前上传灯光和阴影默认全局参数，避免上一帧或上一相机的阴影状态残留。

            if (useMainLightShadow) // 如果当前 request 的主光需要阴影，就把 shadow map 生命周期加入图里。
            {
                graph.AddPass(allocateMainLightShadowMapPass); // 在相机颜色目标绑定前申请主光阴影图，后续 ShadowCaster Pass 会先写它。

                graph.AddPass(drawMainLightShadowCasterPass); // 立刻绘制主光 ShadowCaster，把阴影深度写进刚申请的 MainLightShadowMap。
            }

            if (ShouldSeedOverlayCameraColor(request, safeRenderOptions)) // 非共享 RT 的 Overlay 不清颜色时，才需要从最终目标复制一份底图。
            {
                graph.AddPass(seedOverlayCameraColorPass); // 把 Base 已经写入的最终目标作为 Overlay 的颜色底图，形成旧版保守合成。
            }

            graph.AddPass(setRenderTargetPass); // 把设置渲染目标 Pass 添加到 RenderGraph，保证后续 Pass 画到正确目标。

            graph.AddPass(clearRenderTargetPass); // 把清屏 Pass 添加到 RenderGraph，保证颜色和深度状态可控。

            if (IsPreviewRequest(request)) // Unity Inspector/Asset Preview 需要宽松 LightMode，不能走普通 BurtForward-only 场景绘制。
            {
                graph.AddPass(drawEditorPreviewPass); // 只绘制 Preview 专用 Pass，避免 Cubemap/ReflectionProbe 预览被普通场景路径吞掉。
            }
            else // 非 Preview 保持原来的 Forward 场景绘制路径。
            {
                if (ShouldUseDepthPrepass(asset)) // 如果管线资产允许 Depth Prepass，就把深度预写阶段加入图中。
                {
                    graph.AddPass(depthPrepass); // 把深度预写 Pass 添加到 RenderGraph，让不透明物体先写入 CameraDepth。
                }

                graph.AddPass(drawOpaquePass); // 把不透明物体绘制 Pass 添加到 RenderGraph，让它在已有深度基础上写入颜色。

                graph.AddPass(drawSkyboxPass); // 把天空盒 Pass 添加到 RenderGraph，由 Pass 自己决定是否真正绘制。

                graph.AddPass(drawTransparentPass); // 把透明物体绘制 Pass 添加到 RenderGraph，让透明物体最后做混合。

                if (ShouldUseUnsupportedShaderDebug(request, asset)) // 如果开启了不支持 Shader 调试，就在普通场景绘制后插入错误材质绘制。
                {
                    graph.AddPass(drawUnsupportedShadersPass); // 添加不支持 Shader 调试 Pass，让非 BurtRP 材质容易被发现。
                }
            }

            if (BurtEditorGizmoUtility.ShouldRenderGizmos(request)) // 编辑器里 Scene/Game View 打开 Gizmos 时，在后处理前绘制 PreImageEffects Gizmos。
            {
                graph.AddPass(drawPreImageEffectsGizmosPass); // 添加 PreImageEffects Gizmos Pass，让 Gizmos 参与常规 CameraColor 输出链路。
            }

            if (ShouldUsePostProcessFramework(request, asset, safeRenderOptions)) // 如果后处理框架启用且当前 request 会 FinalBlit，就插入全屏后处理链路。
            {
                graph.AddPass(allocatePostProcessColorPass); // 申请后处理中间颜色 RT，避免 CameraColor 自读自写导致平台不稳定。
                graph.AddPass(postProcessPass); // 执行 CameraColor -> PostProcessColor -> CameraColor，必要时在第一段拷贝里应用 Tonemapping。
                graph.AddPass(releasePostProcessColorPass); // 释放后处理中间 RT，确保临时资源生命周期清晰。
            }

            if (!IsPreviewOrReflectionRequest(request) && ShouldUseDepthDebugView(asset)) // Preview/Reflection 不叠加场景调试视图，避免资产预览或 Probe 捕获被深度图覆盖。
            {
                graph.AddPass(debugCameraDepthPass); // 把 CameraDepth 调试 Pass 添加到 RenderGraph，让它读取深度并覆盖 CameraColor。
            }

            if (!IsPreviewOrReflectionRequest(request) && ShouldUseMainLightShadowDebugView(asset, useMainLightShadow)) // Preview/Reflection 不叠加场景阴影调试视图，避免辅助渲染被覆盖。
            {
                graph.AddPass(debugMainLightShadowMapPass); // 把主光 shadow map 调试 Pass 添加到 RenderGraph，方便直接检查阴影图内容。
            }

            if (BurtEditorGizmoUtility.ShouldRenderGizmos(request)) // 后处理和 Debug 覆盖之后，把 PostImageEffects Gizmos 画回 CameraColor。
            {
                graph.AddPass(drawPostImageEffectsGizmosPass); // 让最终 FinalBlit 统一输出 Gizmos，避免 RenderDoc 下直接写外部目标不稳定。
            }

            if (safeRenderOptions.ShouldFinalBlit) // 只有独立 request 或共享栈的最后一个 request 才输出到最终相机目标。
            {
                graph.AddPass(finalBlitPass); // 把中间 CameraColor 拷贝到 request.TargetIdentifier，完成 BurtRP 内部 RT 到最终输出目标的交接。
            }

            if (useMainLightShadow) // 如果当前 request 申请过主光阴影图，就在相机渲染结束前释放它。
            {
                graph.AddPass(releaseMainLightShadowMapPass); // 释放主光阴影图临时 RT，确保阴影资源生命周期被 RenderGraph 明确管理。
            }

            if (safeRenderOptions.ShouldReleaseCameraColor) // 只有独立 request 或共享栈的最后一个 request 才释放 CameraColor。
            {
                graph.AddPass(releaseCameraColorPass); // 在 FinalBlit 之后释放 CameraColor 临时颜色 RT，避免下一帧误用旧内容。
            }

            if (safeRenderOptions.ShouldReleaseCameraDepth) // 只有独立 request 或共享栈的最后一个 request 才释放 CameraDepth。
            {
                graph.AddPass(releaseCameraDepthPass); // 最后把 CameraDepth 释放 Pass 添加到 RenderGraph，避免临时 RT 泄漏到下一次 request。
            }
        }

        private static bool ShouldSeedOverlayCameraColor( // 定义 Overlay 是否需要从最终目标继承颜色的辅助函数。
            BurtRenderRequest request, // 接收当前 request，用来判断是否为 Overlay 和是否清颜色。
            BurtRequestRenderOptions renderOptions) // 接收栈级执行选项，用来判断当前是否已经共享 CameraColor。
        {
            if (request == null) // 如果 request 为空，说明调用方传入了异常数据。
            {
                return false; // 返回 false，避免为异常 request 添加额外 Pass。
            }

            if (renderOptions != null && renderOptions.UseSharedRenderTargets) // 共享栈级 RT 时，Base 结果已经留在 CameraColor 里。
            {
                return false; // 返回 false，避免从还没 FinalBlit 的最终目标复制旧画面覆盖 Base 结果。
            }

            return request.Type == BurtRenderRequestType.OverlayCamera && !request.OverlayClearsColor; // 非共享 Overlay 默认不清颜色时需要复制最终目标作为底图。
        }

        private static bool IsPreviewRequest(BurtRenderRequest request) // 判断当前 request 是否来自 Unity 编辑器 Preview。
        {
            return request != null && request.Type == BurtRenderRequestType.Preview; // Preview 包括 Cubemap、ReflectionProbe、材质和 Camera 预览窗口。
        }

        private static bool IsPreviewOrReflectionRequest(BurtRenderRequest request) // 判断当前 request 是否来自 Unity 辅助预览或 ReflectionProbe 捕获。
        {
            return request != null && (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection); // 这些 request 不应叠加场景调试视图。
        }

        private static bool ShouldUseDepthPrepass(BurtRenderPipelineAsset asset) // 定义判断是否启用 Depth Prepass 的辅助函数。
        {
            if (asset == null) // 如果资产为空，说明当前没有配置来源。
            {
                return true; // 资产缺失时默认开启 Depth Prepass，保持教程管线的安全行为。
            }

            return asset.EnableDepthPrepass; // 返回资产 Inspector 上配置的 Depth Prepass 开关。
        }

        private static bool ShouldUseUnsupportedShaderDebug(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 定义判断是否插入不支持 Shader 调试 Pass 的辅助函数。
        {
            if (IsPreviewOrReflectionRequest(request)) // Unity 资产预览和 ReflectionProbe 捕获经常使用内部 shader，不应该被错误材质调试覆盖。
            {
                return false; // 保持辅助渲染稳定，避免 Cubemap/ReflectionProbe 被画成错误材质。
            }

            if (asset == null) // 如果资产为空，说明当前没有 Inspector 配置来源。
            {
                return true; // 默认开启错误材质显示，避免不支持材质静默消失。
            }

            return asset.EnableUnsupportedShaderDebug; // 返回资产 Inspector 上配置的不支持 Shader 调试开关。
        }

        private static bool ShouldUsePostProcessFramework( // 定义判断是否启用后处理框架的辅助函数。
            BurtRenderRequest request, // 接收当前渲染请求，用来确认相机任务是否有效。
            BurtRenderPipelineAsset asset, // 接收管线资产，用来读取后处理设置。
            BurtRequestRenderOptions renderOptions) // 接收栈级 RT 生命周期选项，用来避免在共享相机栈的中间 request 上提前执行后处理。
        {
            if (renderOptions != null && !renderOptions.ShouldFinalBlit) // 如果当前 request 不是最终输出点，说明后面还会有 Overlay 或同栈相机继续写入 CameraColor。
            {
                return false; // 返回 false，把后处理推迟到真正 FinalBlit 之前，避免相机栈里重复执行效果。
            }

            return BurtPostProcessUtility.ShouldUsePostProcessFramework(request, asset); // 复用后处理工具逻辑，保证 ForwardGraph 和资源注册条件一致。
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
