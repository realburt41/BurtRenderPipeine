// 引入泛型集合命名空间，用来使用 List<Camera>。
using System.Collections.Generic;

// 引入 UnityEngine 命名空间，用来使用 Camera。
using UnityEngine;

// 引入 UnityEngine.Rendering 命名空间，用来使用 RenderPipeline 和 ScriptableRenderContext。
using UnityEngine.Rendering;

// 定义 Burt 自己的渲染管线命名空间。
namespace Burt.RenderPipeline
{
    // BurtRP 主渲染管线类，负责接收 Unity 渲染入口并分发相机。
    public sealed class BurtRenderPipeline : UnityEngine.Rendering.RenderPipeline
    {
        // 保存管线配置资产。
        private readonly BurtRenderPipelineAsset asset;

        private static readonly int PreIntegratedFGTextureId = Shader.PropertyToID("_BurtPreIntegratedFG"); // 缓存预积分 FG LUT 全局纹理 ID，供 PBR IBL 和能量补偿采样。
        private static readonly int PreIntegratedFGEnabledId = Shader.PropertyToID("_BurtPreIntegratedFGEnabled"); // 缓存 LUT 是否有效的开关，未绑定时 shader 会回退到解析近似。

        // 创建单相机渲染器。
        private readonly BurtCameraRenderer cameraRenderer = new();

        private readonly BurtRenderGraphAssembler forwardGraphAssembler = new BurtForwardGraphAssembler(); // 创建 Forward 组装器，默认模式和稳定画面都使用它。

        private readonly BurtRenderGraphAssembler deferredGraphAssembler = new BurtDeferredGraphAssembler(); // 创建 Deferred 组装器，当前实验模式只接入 GBuffer 生命周期并临时复用 Forward 输出。

        private readonly List<BurtRenderRequest> requests = new(); // 缓存当前帧创建出的所有有效 request，后续先排序再执行。

        private readonly BurtRenderFrame renderFrame = new(); // 缓存当前帧的 Frame/Stack 分组快照；现阶段只用于诊断，不改变渲染顺序。

        // BurtRenderPipeline 构造函数。
        public BurtRenderPipeline(BurtRenderPipelineAsset asset)
        {
            // 保存传入的配置资产。
            this.asset = asset;

            // 开启 SRP Batcher。
            GraphicsSettings.useScriptableRenderPipelineBatching = true;

            // 构造时先绑定一次，避免第一帧 shader 采样到未初始化的全局纹理。
            BindPreIntegratedFGLut();
            BindSubsurfacePreIntegratedLut();
        }

#pragma warning disable 0618

        // Unity 旧版渲染入口。
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            // 执行共享的渲染逻辑。
            RenderCameras(context, cameras);
        }

#pragma warning restore 0618

        // Unity 新版渲染入口。
        protected override void Render(ScriptableRenderContext context, List<Camera> cameras)
        {
            // 执行共享的渲染逻辑。
            RenderCameras(context, cameras);
        }

        protected override void Dispose(bool disposing)
        {
            BurtAtmosphereReflectionUtility.Release();
            BurtImageBasedFilterUtility.Release();
            base.Dispose(disposing);
        }

        private void RenderCameras(ScriptableRenderContext context, Camera[] cameras)
        {
            // 清空上一帧的 request 列表。
            requests.Clear();

            // 遍历 Unity 传入的相机数组。
            foreach (var camera in cameras)
            {
                // 从当前相机创建 BurtRenderRequest。
                var request = BurtRenderRequest.CreateCameraRequest(context, camera, asset);

                // 如果 request 无效，就不加入列表。
                if (!request.IsValid)
                {
                    // 跳过当前相机。
                    continue;
                }

                request.SetGraphAssembler(ResolveGraphAssembler(request)); // 根据管线资产和 request 类型给当前 request 指定对应渲染图组装器。

                // 把有效 request 加入列表。
                requests.Add(request);
            }

            // 执行所有 request。
            ExecuteRequests(context);
        }

        // 渲染 List<Camera> 的共享逻辑。
        private void RenderCameras(ScriptableRenderContext context, List<Camera> cameras)
        {
            // 清空上一帧的 request 列表。
            requests.Clear();

            // 遍历 Unity 传入的相机列表。
            foreach (var camera in cameras)
            {
                // 从当前相机创建 BurtRenderRequest。
                var request = BurtRenderRequest.CreateCameraRequest(context, camera, asset);

                // 如果 request 无效，就不加入列表。
                if (!request.IsValid)
                {
                    // 跳过当前相机。
                    continue;
                }

                request.SetGraphAssembler(ResolveGraphAssembler(request)); // 根据管线资产和 request 类型给当前 request 指定对应渲染图组装器。

                // 把有效 request 加入列表。
                requests.Add(request);
            }

            // 执行所有 request。
            ExecuteRequests(context);
        }

        // 绑定 PBR 预积分 FG LUT，保证 Lit shader 能读取资产上的间接高光查找表。
        private void BindPreIntegratedFGLut() // 把管线资产上的 PreintegratedFG 绑定给所有 BurtRP Lit shader。
        {
            // 没有配置 LUT 时绑定白贴图并关闭开关，shader 会完全使用解析 DFG fallback。
            Texture2D lut = asset != null ? asset.PreintegratedFGLut : null;
            Shader.SetGlobalTexture(PreIntegratedFGTextureId, lut != null ? lut : Texture2D.whiteTexture);
            Shader.SetGlobalFloat(PreIntegratedFGEnabledId, lut != null ? 1.0f : 0.0f);
        }

        private void BindSubsurfacePreIntegratedLut()
        {
            BurtSubsurfaceLutUtility.BeginPaletteBinding();
            var palette = asset != null
                ? asset.ScreenSpaceSubsurfaceProfilePalette
                : BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null);
            var lut = BurtSubsurfaceLutUtility.GetOrCreatePreIntegratedLut(palette);
            Shader.SetGlobalTexture(BurtSubsurfaceLutUtility.TextureId, lut != null ? lut : BurtSubsurfaceLutUtility.GetFallbackPreIntegratedLut());
            Shader.SetGlobalFloat(BurtSubsurfaceLutUtility.EnabledId, lut != null ? 1.0f : 0.0f);
            BurtSubsurfaceProfileShaderUtility.BindGlobals(asset);
        }

        private BurtRenderGraphAssembler ResolveGraphAssembler(BurtRenderRequest request) // 根据当前管线资产和 request 类型选择本次 request 使用的 RenderGraph 组装器。
        {
            if (request != null && (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)) // Unity Inspector/Asset Preview 和 ReflectionProbe 捕获不适合走 Deferred GBuffer 路径。
            {
                return forwardGraphAssembler; // 预览/反射探针窗口强制使用稳定 Forward 路径，避免 Cubemap/ReflectionProbe 被 Deferred 管线破坏。
            }

            if (asset == null) // 如果资产为空，说明没有 Renderer Mode 配置来源。
            {
                return forwardGraphAssembler; // 返回 Forward 组装器，保持最安全的默认行为。
            }

            if (asset.RendererMode == BurtRendererMode.Deferred) // 如果用户在资产上选择 Deferred，就进入实验 Deferred 组装器。
            {
                return deferredGraphAssembler; // 返回 Deferred 组装器；当前阶段它会额外申请 GBuffer 并复用 Forward 输出。
            }

            return forwardGraphAssembler; // 其他情况使用 Forward，保证新增 Deferred 代码不改变默认画面。
        }

        // 排序、构建 Frame 快照，并按 Frame/Stack/Request 三层调度执行所有 BurtRenderRequest。
        private void ExecuteRequests(ScriptableRenderContext context)
        {
            // 每帧同步一次 LUT，允许用户在 Inspector 中替换 PreintegratedFG 后立即生效。
            BindPreIntegratedFGLut();
            BindSubsurfacePreIntegratedLut();

            // 按 request 的 SortLayer 从小到大排序，比较规则集中放在 BurtCameraSortUtility 里维护。
            requests.Sort(BurtCameraSortUtility.CompareRequests);

            // 根据已经排序好的 request 构建 Frame/Stack 快照，为后续栈级 RT、后处理和 Deferred 做结构准备。
            renderFrame.BuildFromSortedRequests(requests);

            // 如果资产上打开了相机排序日志，就在真正执行渲染前输出当前帧的排序快照。
            if (asset != null && asset.EnableCameraSortDebugLog)
            {
                // 把已经排序完成的 request 列表交给调试工具，确保日志顺序和实际执行顺序一致。
                BurtCameraDebugUtility.LogSortedRequests(requests);
            }

            // 如果资产上打开了 Frame 调试日志，就输出当前帧识别出的相机栈结构。
            if (asset != null && asset.EnableRenderFrameDebugLog)
            {
                // 这一步只打印诊断信息，不参与真实渲染，所以不会改变现有画面。
                BurtRenderFrameDebugUtility.LogFrame(renderFrame);
            }

            // 按 Frame -> Stack -> Request 的层级执行当前帧；这里会把相机栈信息传给后续 RenderTarget 生命周期决策。
            ExecuteRenderFrame(context);
        }

        // 执行当前帧里的所有相机栈；这是从 request 驱动走向 stack/frame 驱动的第一层调度。
        private void ExecuteRenderFrame(ScriptableRenderContext context)
        {
            // 从 Frame 快照里取出已经按真实渲染顺序排列好的相机栈列表。
            var stackGroups = renderFrame.StackGroups;

            // 如果 Frame 没有构建出任何相机栈，就直接结束；没有栈也就没有需要渲染的 request。
            if (stackGroups == null || stackGroups.Count == 0)
            {
                // 结束当前帧执行，保持空帧安全。
                return;
            }

            // 按 Frame 分组顺序遍历每一个相机栈；当前顺序来自排序后的 requests，所以画面顺序保持不变。
            for (var stackIndex = 0; stackIndex < stackGroups.Count; stackIndex++)
            {
                // 取出当前相机栈，后面交给栈级执行函数处理。
                var stackGroup = stackGroups[stackIndex];

                // 如果 Frame 里出现空栈对象，就跳过它，避免调度层空引用影响后续有效相机。
                if (stackGroup == null)
                {
                    // 跳过异常空栈，继续执行下一个栈。
                    continue;
                }

                // 执行当前相机栈；栈内仍然逐个 request 渲染，但 RenderTarget 的申请、输出和释放已经由栈级选项控制。
                ExecuteCameraStack(context, stackGroup);
            }
        }

        // 执行一个逻辑相机栈；这里开始把 Frame/Stack 诊断结果真正转成栈级 RenderTarget 生命周期。
        private void ExecuteCameraStack(ScriptableRenderContext context, BurtCameraStackGroup stackGroup)
        {
            // 如果调用方传入空栈，就直接结束，保证调度层不会因为诊断数据异常而崩溃。
            if (stackGroup == null)
            {
                // 结束空栈执行。
                return;
            }

            // 读取栈内 request 列表；这个列表顺序等于当前真实渲染顺序。
            var stackRequests = stackGroup.Requests;

            // 如果栈内没有 request，就直接结束；这种情况通常只会来自异常或空 Frame。
            if (stackRequests == null || stackRequests.Count == 0)
            {
                // 结束空 request 栈执行。
                return;
            }

            // 缓存栈内 request 数量，后续首尾判断、FinalBlit 和 Release 都用同一份数量快照。
            var requestCount = stackRequests.Count;

            // 遍历当前栈内所有 request；共享栈时第一个 request 申请 RT，最后一个 request 输出并释放 RT。
            for (var requestIndex = 0; requestIndex < requestCount; requestIndex++)
            {
                // 取出栈内当前 request，后面交给单 request 执行函数处理。
                var request = stackRequests[requestIndex];

                // 根据当前 request 的栈内位置生成 RT 生命周期选项，让 Assembler 决定是否插入 Allocate、FinalBlit 和 Release。
                var renderOptions = stackGroup.CreateRenderOptions(requestIndex);

                // 执行当前 request，并把栈级 RT 生命周期选项传入单 request 渲染器。
                ExecuteRequest(context, request, renderOptions);
            }
        }

        // 执行单个 BurtRenderRequest；这里负责发出相机渲染事件，并把栈级执行选项交给 BurtCameraRenderer。
        private void ExecuteRequest(
            ScriptableRenderContext context, // 接收 Unity SRP 提供的渲染上下文。
            BurtRenderRequest request, // 接收当前要执行的 Burt 渲染请求。
            BurtRequestRenderOptions renderOptions) // 接收当前 request 的栈级 RenderTarget 生命周期选项。
        {
            // 如果 request 对象为空，说明列表里有异常数据，直接跳过。
            if (request == null)
            {
                // 跳过空 request。
                return;
            }

            // 如果 request 被标记为无效，说明它不应该参与渲染。
            if (!request.IsValid)
            {
                // 跳过无效 request。
                return;
            }

            // 从 request 里取出这次渲染任务对应的 Unity 原生 Camera。
            var camera = request.Camera;

            // 如果 request 没有关联相机，当前阶段 BurtRP 暂时无法执行它。
            if (camera == null)
            {
                // 跳过这个没有相机的 request。
                return;
            }

            // 通知 Unity 和外部监听者：这个相机开始渲染。
            BeginCameraRendering(context, camera);

            // 使用 try/finally，保证即使渲染过程中报错，也能发出 EndCameraRendering。
            try
            {
                // 把当前 request 和栈级 RT 生命周期选项交给 BurtCameraRenderer 执行。
                cameraRenderer.Render(context, request, asset, renderOptions);
            }
            finally
            {
                // 通知 Unity 和外部监听者：这个相机结束渲染。
                EndCameraRendering(context, camera);
            }
        }

    }
}
