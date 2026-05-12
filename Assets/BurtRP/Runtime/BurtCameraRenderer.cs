using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Debug 输出 RenderGraph 调试信息。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 ScriptableRenderContext。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，和其他 BurtRP 运行时代码保持一致。
{
    public sealed class BurtCameraRenderer // 定义单个 request 的执行器，它负责驱动 Assembler 和 RenderGraph。
    {
        private readonly BurtRenderGraph renderGraph = new BurtRenderGraph(); // 创建一个可复用的 RenderGraph，用来承载当前 request 的 Pass 列表和资源表。

        public void Render( // 保留旧渲染入口，未传入 options 时保持每个 request 独立分配、FinalBlit 和释放 RT。
            ScriptableRenderContext context, // 接收 Unity SRP 提供的渲染上下文。
            BurtRenderRequest request, // 接收已经构建好的 Burt 渲染请求。
            BurtRenderPipelineAsset asset) // 接收 BurtRP 管线资产配置。
        {
            Render(context, request, asset, BurtRequestRenderOptions.CreateSingleRequest()); // 把旧入口转发到新入口，并使用旧单 request 生命周期。
        }

        public void Render( // 定义带执行选项的新渲染入口，让 Camera Stack 可以控制 RT 生命周期。
            ScriptableRenderContext context, // 接收 Unity SRP 提供的渲染上下文。
            BurtRenderRequest request, // 接收已经构建好的 Burt 渲染请求。
            BurtRenderPipelineAsset asset, // 接收 BurtRP 管线资产配置。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RenderTarget 生命周期选项。
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

            var safeRenderOptions = renderOptions ?? BurtRequestRenderOptions.CreateSingleRequest(); // 传入空 options 时回退旧行为，避免调用方漏传导致 RT 不分配。

            BurtPostProcessUtility.UpdateVolumeStack(request, asset); // 每个 request 渲染前刷新 VolumeStack，让后处理 Pass 能读取当前 Global Volume 参数。

            var temporalAA = BurtTemporalAAUtility.PrepareRequest(request, asset);
            request.SetTemporalAA(temporalAA);

            try
            {
                if (temporalAA.Enabled)
                {
                    request.Camera.nonJitteredProjectionMatrix = temporalAA.NonJitteredProjectionMatrix;
                    request.Camera.projectionMatrix = temporalAA.JitteredProjectionMatrix;
                }

                context.SetupCameraProperties(request.Camera); // 设置当前相机的矩阵、裁剪参数和 Unity 内置 shader 变量。

                BurtShadingDebugSettings.ApplyGlobalShaderProperties(); // 每个相机渲染前刷新 Shading Debug 全局参数，避免编辑器切换或域重载后 shader 读到旧值。

                renderGraph.Clear(); // 清空上一次 request 留下的 Pass 和资源，准备组装当前 request 的图。

                renderGraph.ImportRequestResources(request, asset); // 把 request 的基础渲染目标导入 RenderGraph 资源表，并让资源注册使用当前管线资产配置。

                request.GraphAssembler.Assemble(renderGraph, request, asset, safeRenderOptions); // 让当前 request 指定的 Assembler 按栈级 RT 选项把 Pass 添加到 RenderGraph。

                var graphContext = new BurtRenderGraphContext(context, request, asset, renderGraph.Resources, safeRenderOptions); // 创建 RenderGraph 执行上下文，并把资源表与执行选项传给每个 Pass。

                renderGraph.Execute(graphContext); // 执行 RenderGraph 里已经组装好的所有 Pass。

                if (ShouldCaptureRenderGraphDebug(request, asset)) // 如果资产开启了常驻捕获，或用户点击了匹配当前 request 的下一帧复制按钮，就生成一份 RenderGraph Debug 文本。
                {
                    var renderGraphDebugDump = renderGraph.DumpDebugInfo(request, asset, safeRenderOptions); // 生成完整 RenderGraph Debug 文本，包含 Pass 顺序、资源读写关系、RT 生命周期和当前管线调试状态。

                    BurtRenderGraphDebugClipboardUtility.StoreLatestDump(request, renderGraphDebugDump); // 缓存最近一次 dump，Inspector 按钮可以直接复制到剪切板。

                    if (BurtRenderGraphDebugClipboardUtility.ConsumeCopyNextDumpRequest(request)) // 如果用户点击过“下一帧复制”且当前 request 命中过滤条件，就在本次 dump 生成后立刻消费请求。
                    {
                        BurtRenderGraphDebugClipboardUtility.CopyLatestDumpToClipboardAndLog(request.Type); // 把刚生成的目标类型 dump 写进剪切板，并输出一条短确认日志。
                    }

                    if (asset != null && asset.EnableRenderGraphDebugConsoleLog) // 只有显式打开 Console Log 时，才继续把完整长文本打印到 Console。
                    {
                        Debug.Log(renderGraphDebugDump); // 输出完整 RenderGraph Debug 文本；默认关闭，避免每帧刷屏。
                    }
                }
            }
            finally
            {
                if (temporalAA.Enabled)
                {
                    BurtTemporalAAUtility.RestoreCameraProjectionAfterJitter(request.Camera);
                }
            }

            BurtTemporalAAUtility.CommitRequest(request);
            context.Submit(); // 把当前 request 累积的所有渲染命令提交给 Unity 执行。
        }

        private static bool ShouldCaptureRenderGraphDebug(BurtRenderRequest request, BurtRenderPipelineAsset asset) // 判断当前 request 是否需要生成 RenderGraph Debug 文本。
        {
            if (asset != null && asset.EnableRenderGraphDebug) // 如果资产上开启了常驻 RenderGraph Debug 捕获。
            {
                return true; // 返回 true，每帧都缓存最近一次 dump，供按钮随时复制。
            }

            return BurtRenderGraphDebugClipboardUtility.ShouldCaptureNextDumpForRequest(request); // 如果用户点击了“一帧复制”，只让匹配 request 生成一次 dump。
        }
    }
}
