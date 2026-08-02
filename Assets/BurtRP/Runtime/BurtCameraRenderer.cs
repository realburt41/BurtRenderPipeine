using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Debug 输出 RenderGraph 调试信息。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 ScriptableRenderContext。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，和其他 BurtRP 运行时代码保持一致。
{
    public sealed class BurtCameraRenderer // 定义单个 request 的执行器，它负责驱动 Assembler 和 RenderGraph。
    {
        private const int MaxCameraProfilingNameCount = 256;
        private static readonly ProfilerMarker AssembleMarker = new ProfilerMarker("BRP.Camera.Assemble");
        private static readonly ProfilerMarker SubmitMarker = new ProfilerMarker("BRP.Camera.Submit");
        private static readonly Dictionary<int, string> CameraProfilingNames = new Dictionary<int, string>();
        private static readonly int HairDitherFrameIndexId = Shader.PropertyToID("_BurtHairDitherFrameIndex");
        private const string BlueNoiseScalarTextureResourcePath = "BlueNoise/STBlueNoise_scalar_128x128x64";
        private static readonly int BlueNoiseScalarTextureId = Shader.PropertyToID("_BurtBlueNoiseScalarTexture");
        private static readonly int BlueNoiseDimensionsId = Shader.PropertyToID("_BurtBlueNoiseDimensions");
        private static readonly int BlueNoiseModuloMasksId = Shader.PropertyToID("_BurtBlueNoiseModuloMasks");
        private static readonly int BlueNoiseScalarTextureValidId = Shader.PropertyToID("_BurtBlueNoiseScalarTextureValid");
        private static readonly int BlueNoiseFrameIndexId = Shader.PropertyToID("_BurtBlueNoiseFrameIndex");
        private static Texture2D blueNoiseScalarTexture;
        private static bool blueNoiseScalarTextureLoaded;
        private static Vector4 blueNoiseDimensions = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
        private static Vector4 blueNoiseModuloMasks = Vector4.zero;

        private readonly BurtRenderGraph renderGraph = new BurtRenderGraph(); // 创建一个可复用的 RenderGraph，用来承载当前 request 的 Pass 列表和资源表。
        private readonly BurtRenderPass shadingDebugPreparePass = new BurtShadingDebugPreparePass();

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
            BurtRequestRenderOptions renderOptions, // 接收当前 request 的栈级 RenderTarget 生命周期选项。
            bool submitImmediately = true)
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


            var temporalAA = BurtTemporalAAUtility.PrepareRequest(request, asset, safeRenderOptions);
            request.SetTemporalAA(temporalAA);
            Shader.SetGlobalFloat(HairDitherFrameIndexId, temporalAA.Enabled ? temporalAA.FrameIndex : 0);
            BindBlueNoiseGlobals();
            var submitted = false;
            var deferredSuccessfulSubmit = false;
            var renderSucceeded = false;

            try
            {
                if (temporalAA.Enabled)
                {
                    request.Camera.nonJitteredProjectionMatrix = temporalAA.NonJitteredProjectionMatrix;
                    request.Camera.projectionMatrix = temporalAA.JitteredProjectionMatrix;
                }

                context.SetupCameraProperties(request.Camera); // 设置当前相机的矩阵、裁剪参数和 Unity 内置 shader 变量。


                renderGraph.Clear(); // 清空上一次 request 留下的 Pass 和资源，准备组装当前 request 的图。
                renderGraph.SetProfilingMode(asset != null
                    ? asset.RenderGraphProfilingMode
                    : BurtRenderGraphProfilingMode.Off);

                renderGraph.ImportRequestResources(request, asset); // 把 request 的基础渲染目标导入 RenderGraph 资源表，并让资源注册使用当前管线资产配置。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                var cameraProfilingName = BuildCameraProfilingName(request);
                renderGraph.BeginProfilingScope(cameraProfilingName);
                try
                {
                    renderGraph.AddPass(shadingDebugPreparePass);
                    using (AssembleMarker.Auto())
                    {
                        request.GraphAssembler.Assemble(renderGraph, request, asset, safeRenderOptions); // 让当前 request 指定的 Assembler 按栈级 RT 选项把 Pass 添加到 RenderGraph。
                    }
                }
                finally
                {
                    renderGraph.EndProfilingScope(cameraProfilingName);
                }
#else
                renderGraph.AddPass(shadingDebugPreparePass);

                using (AssembleMarker.Auto())
                {
                    request.GraphAssembler.Assemble(renderGraph, request, asset, safeRenderOptions); // 让当前 request 指定的 Assembler 按栈级 RT 选项把 Pass 添加到 RenderGraph。
                }
#endif

                var captureRenderGraphDebug = ShouldCaptureRenderGraphDebug(request, asset);
                renderGraph.SetCompilationMode(captureRenderGraphDebug
                    ? BurtRenderGraph.BurtRenderGraphCompilationMode.Full
                    : BurtRenderGraph.BurtRenderGraphCompilationMode.Lightweight);

                var graphCommandBuffer = CommandBufferPool.Get("BRP.RenderGraph/Shared");
                BurtRenderGraphContext graphContext = null;
                try
                {
                    graphContext = BurtRenderGraphContext.Acquire(
                        context,
                        request,
                        asset,
                        renderGraph.Resources,
                        safeRenderOptions,
                        graphCommandBuffer); // 创建 RenderGraph 执行上下文，并把资源表、执行选项和图级共享命令缓冲传给每个 Pass。

                    renderGraph.Execute(graphContext); // 执行 RenderGraph 里已经组装好的所有 Pass。
                }
                finally
                {
                    BurtRenderGraphContext.Release(graphContext);
                    graphCommandBuffer.Clear();
                    CommandBufferPool.Release(graphCommandBuffer);
                }

                if (captureRenderGraphDebug) // 如果资产开启了常驻捕获，或用户点击了匹配当前 request 的下一帧复制按钮，就生成一份 RenderGraph Debug 文本。
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

                if (submitImmediately || temporalAA.Enabled || renderGraph.RequiresImmediateSubmit)
                {
                    using (SubmitMarker.Auto())
                    {
                        context.Submit(); // Keep the jittered camera projection alive until SRP has submitted the queued draw commands.
                    }
                    submitted = true;
                }
                else
                {
                    deferredSuccessfulSubmit = true;
                }
                renderSucceeded = true;
            }
            finally
            {
                if (temporalAA.Enabled)
                {
                    BurtTemporalAAUtility.RestoreCameraProjectionAfterJitter(request.Camera);
                }

                if (!submitted && !deferredSuccessfulSubmit)
                {
                    try
                    {
                        using (SubmitMarker.Auto())
                        {
                            context.Submit(); // Flush any commands queued before an exception so graph-owned buffers can be released safely.
                        }
                        submitted = true;
                    }
                    catch (System.Exception submitException)
                    {
                        Debug.LogException(submitException);
                    }
                }

                if (renderSucceeded)
                {
                    BurtTemporalAAUtility.CommitRequest(request);
                }
                else
                {
                    BurtTemporalAAUtility.InvalidateHistory(request.Camera, "RenderGraphExecutionFailed");
                }

                if (submitted)
                {
                    renderGraph.FlushDeferredResourceReleases(); // Release deferred GraphicsBuffers only after queued commands have been submitted.
                }
            }
        }

        internal void FlushDeferredResourceReleases()
        {
            renderGraph.FlushDeferredResourceReleases();
        }

        internal void Dispose()
        {
            renderGraph.DisposeResources();
        }

        private static void BindBlueNoiseGlobals()
        {
            if (!blueNoiseScalarTextureLoaded)
            {
                blueNoiseScalarTextureLoaded = true;
                blueNoiseScalarTexture = Resources.Load<Texture2D>(BlueNoiseScalarTextureResourcePath);

                if (blueNoiseScalarTexture != null)
                {
                    var width = Mathf.Max(1, blueNoiseScalarTexture.width);
                    var height = Mathf.Max(1, blueNoiseScalarTexture.height);
                    var depth = Mathf.Max(1, height / width);
                    blueNoiseDimensions = new Vector4(width, width, depth, 0.0f);
                    blueNoiseModuloMasks = new Vector4(
                        CreateModuloMask(width),
                        CreateModuloMask(width),
                        CreateModuloMask(depth),
                        0.0f);
                }
            }

            Shader.SetGlobalTexture(BlueNoiseScalarTextureId, blueNoiseScalarTexture != null ? blueNoiseScalarTexture : Texture2D.grayTexture);
            Shader.SetGlobalVector(BlueNoiseDimensionsId, blueNoiseDimensions);
            Shader.SetGlobalVector(BlueNoiseModuloMasksId, blueNoiseModuloMasks);
            Shader.SetGlobalFloat(BlueNoiseScalarTextureValidId, blueNoiseScalarTexture != null ? 1.0f : 0.0f);
            Shader.SetGlobalFloat(BlueNoiseFrameIndexId, Time.frameCount);
        }

        private static int CreateModuloMask(int dimension)
        {
            var powerOfTwo = Mathf.NextPowerOfTwo(Mathf.Max(1, dimension));
            if (powerOfTwo > dimension)
            {
                powerOfTwo >>= 1;
            }

            return Mathf.Max(0, powerOfTwo - 1);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string BuildCameraProfilingName(BurtRenderRequest request)
        {
            var assemblerName = request != null && request.GraphAssembler != null
                ? request.GraphAssembler.Name
                : "UnknownPath";
            var renderPath = assemblerName.IndexOf("Deferred", System.StringComparison.OrdinalIgnoreCase) >= 0
                ? "Deferred"
                : "Forward";
            if (request == null || request.Camera == null)
            {
                return "BRP.Camera/UnnamedCamera [" + renderPath + "]";
            }

            var cacheKey = unchecked((request.Camera.GetInstanceID() * 397) ^ (renderPath == "Deferred" ? 1 : 0));
            if (CameraProfilingNames.TryGetValue(cacheKey, out var cachedName))
            {
                return cachedName;
            }

            if (CameraProfilingNames.Count >= MaxCameraProfilingNameCount)
            {
                return "BRP.Camera/Overflow [" + renderPath + "]";
            }

            var cameraName = !string.IsNullOrEmpty(request.Camera.name) ? request.Camera.name : "UnnamedCamera";
            cachedName = "BRP.Camera/" + cameraName + " [" + renderPath + "]";
            CameraProfilingNames.Add(cacheKey, cachedName);
            return cachedName;
        }
#endif

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
