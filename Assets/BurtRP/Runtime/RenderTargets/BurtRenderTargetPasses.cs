using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、Material、Matrix4x4、MeshTopology 和 Shader 等 Unity 类型。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering 命名空间，用来使用 CommandBufferPool 和渲染目标绑定相关 API。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 RenderTarget Pass 可以继续被 ForwardGraphAssembler 直接实例化。
{
    internal sealed class BurtAllocateCameraColorPass : BurtRenderPass // 定义 CameraColor 分配 Pass，负责为当前 request 创建中间颜色 RT。
    {
        public override string Name => "Burt Allocate Camera Color"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraColor(); // 声明这个 Pass 会创建并写入 CameraColor 资源的生命周期状态。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 CameraColor 分配 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来决定中间颜色 RT 的尺寸和格式。

            var cameraColorTarget = context.CameraColorTarget; // 从 GraphContext 中取出 CameraColor 资源句柄。

            if (!cameraColorTarget.IsValid) // 如果 CameraColor 句柄无效，说明资源表没有注册中间颜色目标。
            {
                return; // 直接结束这个 Pass，避免申请一个无法被后续 Pass 找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera); // 根据当前相机创建中间颜色 RT 描述。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.CameraColorTextureId, descriptor, FilterMode.Bilinear); // 申请一个临时颜色 RT，并绑定到 CameraColor 的全局 ID。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, cameraColorTarget.Identifier); // 把 CameraColor 暴露成全局纹理，FinalBlit 和后续全屏 Pass 都可以采样它。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtAllocateCameraDepthPass : BurtRenderPass // 定义 CameraDepth 分配 Pass，负责为当前 request 创建真实深度 RT。
    {
        public override string Name => "Burt Allocate Camera Depth"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraDepth(); // 声明这个 Pass 会创建并写入 CameraDepth 资源的生命周期状态。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 CameraDepth 分配 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来决定深度 RT 尺寸。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄。

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 句柄无效，说明资源表没有注册深度目标。
            {
                return; // 直接结束这个 Pass，避免申请一个无法被后续 Pass 找到的 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraDepthDescriptor(camera); // 根据当前相机创建深度 RT 描述。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.CameraDepthTextureId, descriptor, FilterMode.Point); // 申请一个临时深度 RT，并绑定到 CameraDepth 的全局 ID。

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier); // 把 CameraDepth 暴露成全局纹理，方便后续 shader 或 pass 采样。

            renderContext.ExecuteCommandBuffer(cmd); // 把申请 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtSetRenderTargetPass : BurtRenderPass // 定义设置渲染目标的 Pass，负责把 CameraColorTarget 和 CameraDepthTarget 绑定到 GPU。
    {
        public override string Name => "Burt Set Render Target"; // 返回这个 Pass 的名称，方便 CommandBuffer 和 Frame Debugger 显示。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraColor(); // 声明这个 Pass 会把 CameraColor 设置为后续颜色绘制目标。

            builder.WriteCameraDepth(); // 声明这个 Pass 会让后续绘制使用当前相机深度缓冲。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var cameraColorTarget = context.CameraColorTarget; // 从 GraphContext 中取出 RenderGraph 的相机颜色目标句柄。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 RenderGraph 的相机深度目标句柄。

            if (!cameraColorTarget.IsValid) // 如果 CameraColorTarget 无效，说明当前图没有可绑定的颜色输出目标。
            {
                return; // 直接结束这个 Pass，避免绑定 default RenderTargetIdentifier。
            }

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepthTarget 无效，说明当前图没有可用的深度目标。
            {
                return; // 直接结束这个 Pass，避免后续绘制缺失深度缓冲语义。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier); // 同时绑定颜色目标和 BurtRP 自己的深度目标，让后续绘制真正写入独立 CameraDepth。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);

            renderContext.ExecuteCommandBuffer(cmd); // 把 CommandBuffer 里的设置渲染目标命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtSeedOverlayCameraColorPass : BurtRenderPass // 定义 Overlay 颜色继承 Pass，负责在不清颜色时把当前最终目标复制进中间颜色 RT。
    {
        public override string Name => "Burt Seed Overlay Camera Color"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadFinalCameraTarget(); // 声明读取当前最终目标，通常里面已经有 Base 相机输出。

            builder.WriteCameraColor(); // 声明写入中间 CameraColor，让 Overlay 后续绘制叠加在这个底图上。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 Overlay 颜色继承 Pass 的执行函数。
        {
            var request = context.Request; // 从 GraphContext 中取出当前渲染请求，用来确认是否仍然是 Overlay。

            if (request == null || request.Type != BurtRenderRequestType.OverlayCamera || request.OverlayClearsColor) // 只服务于不清颜色的 Overlay request。
            {
                return; // 其他 request 不需要复制最终目标，直接跳过。
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var cameraColorTarget = context.CameraColorTarget; // 读取 Overlay 将要绘制的中间颜色目标。

            var finalCameraTarget = context.FinalCameraTarget; // 读取当前最终目标，Base 相机通常已经把结果写在这里。

            if (!cameraColorTarget.IsValid || !finalCameraTarget.IsValid) // 如果任一目标无效，就不能安全执行复制。
            {
                return; // 直接跳过，避免绑定无效 RenderTargetIdentifier。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.Blit(finalCameraTarget.Identifier, cameraColorTarget.Identifier); // 把已有最终颜色复制到 Overlay 的中间颜色 RT，作为不清颜色叠加的底图。

            renderContext.ExecuteCommandBuffer(cmd); // 把复制命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtClearRenderTargetPass : BurtRenderPass // 定义清屏 Pass，负责根据 BurtCameraData 清理颜色和深度缓冲。
    {
        public override string Name => "Burt Clear Render Target"; // 返回这个 Pass 的名称，方便 CommandBuffer 和 Frame Debugger 显示。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraColor(); // 声明这个 Pass 会清理并写入 CameraColor。

            builder.WriteCameraDepth(); // 声明这个 Pass 会清理并写入 CameraDepth。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var asset = context.Asset; // 从 GraphContext 中取出当前管线资产配置。

            var camera = request.Camera; // 从 request 中取出当前相机。

            var clearMode = BurtCameraClearUtility.ResolveClearMode(request); // 统一解析清屏模式，让 SceneView/Preview 没有 BurtCameraData 时也能跟随 Unity clearFlags。

            var isOverlayRequest = request.Type == BurtRenderRequestType.OverlayCamera; // Overlay 相机使用显式清屏意图，不再直接套用 Base 的清屏模式。

            var clearDepth = true; // Base/SceneView/Preview 保持旧行为：只要不是 DontClear，就清理深度。

            var clearColorBuffer = false; // 默认不清理颜色缓冲，后面根据清屏模式或 Overlay 意图决定是否改为 true。

            if (isOverlayRequest) // Overlay 的清屏行为由 OverlayClearsColor/OverlayClearsDepth 决定。
            {
                clearDepth = request.OverlayClearsDepth; // Overlay 是否清深度只看相机栈意图，默认会清深度。

                clearColorBuffer = request.OverlayClearsColor; // Overlay 默认不清颜色，只有显式勾选时才清颜色。

                if (!clearDepth && !clearColorBuffer) // 如果 Overlay 两个缓冲都不清，就不需要发 ClearRenderTarget。
                {
                    return; // 直接结束这个 Pass，保留前面 Seed Pass 复制来的颜色和已有深度状态。
                }
            }
            else
            {
                if (clearMode == BurtCameraClearMode.DontClear) // 如果非 Overlay 清屏模式是不清屏，就不需要执行 ClearRenderTarget。
                {
                    return; // 直接结束这个 Pass，让后续绘制保留当前目标里的旧内容。
                }

                if (clearMode == BurtCameraClearMode.SolidColor) // 如果是纯色清屏模式，就需要清理颜色缓冲。
                {
                    clearColorBuffer = true; // 标记需要清理颜色缓冲。
                }

                if (clearMode == BurtCameraClearMode.Skybox) // 如果是天空盒模式，也先清理颜色缓冲作为天空盒绘制前的底色。
                {
                    clearColorBuffer = true; // 标记需要清理颜色缓冲。
                }
            }

            var clearColor = BurtCameraClearUtility.ResolveClearColor(request, asset, clearMode); // 统一解析清屏颜色，保证 Skybox 和编辑器相机使用正确背景色。

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ClearRenderTarget(clearDepth, clearColorBuffer, clearColor); // 向 CommandBuffer 写入清理深度和颜色缓冲的命令。

            renderContext.ExecuteCommandBuffer(cmd); // 把清屏命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtFinalBlitPass : BurtRenderPass // 定义最终拷贝 Pass，负责把中间 CameraColor 输出到 request 指定的最终目标。
    {
        private const string FinalBlitShaderName = "Hidden/BurtRP/FinalBlit"; // 定义 FinalBlit shader 的查找名称，必须和 shader 文件里的 Shader 名称一致。

        private static readonly int FinalBlitYFlipId = Shader.PropertyToID("_BurtFinalBlitYFlip"); // 缓存 FinalBlit Y 翻转开关的 shader 属性 ID，避免每帧字符串查找。

        private Material finalBlitMaterial; // 缓存 FinalBlit 材质，避免每帧重复创建 Material。

        private bool hasLoggedMissingShader; // 记录是否已经输出过缺失 shader 警告，避免 Console 每帧刷屏。

        public override string Name => "Burt Final Blit"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraColor(); // 声明这个 Pass 会读取中间 CameraColor。

            builder.WriteFinalCameraTarget(); // 声明这个 Pass 会写入 request.TargetIdentifier 对应的最终输出目标。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 FinalBlit Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var cameraColorTarget = context.CameraColorTarget; // 从 GraphContext 中取出中间 CameraColor 资源句柄。

            var finalCameraTarget = context.FinalCameraTarget; // 从 GraphContext 中取出最终相机输出目标句柄。

            if (!cameraColorTarget.IsValid) // 如果 CameraColor 无效，说明当前图没有可读取的中间颜色 RT。
            {
                return; // 直接结束这个 Pass，避免 shader 采样无效纹理。
            }

            if (!finalCameraTarget.IsValid) // 如果最终输出目标无效，说明当前 request 没有可写入的 backbuffer 或 targetTexture。
            {
                return; // 直接结束这个 Pass，避免绑定无效最终目标。
            }

            var material = GetFinalBlitMaterial(); // 获取或创建 FinalBlit 材质。

            if (material == null) // 如果材质为空，说明 shader 没有找到或者创建失败。
            {
                return; // 直接结束这个 Pass，避免向 CommandBuffer 提交无效绘制。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.SetRenderTarget(finalCameraTarget.Identifier); // 绑定最终输出目标，后续全屏三角形会写到 request.TargetIdentifier。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, cameraColorTarget.Identifier); // 确保 _BurtCameraColorTexture 指向当前 request 的中间 CameraColor。

            var finalBlitYFlip = BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request); // 调用 RenderTarget 工具类计算 Y 翻转开关，保持 FinalBlit Pass 只负责上传参数和绘制。

            cmd.SetGlobalFloat(FinalBlitYFlipId, finalBlitYFlip); // 把翻转开关上传给 FinalBlit shader，让 Scene/Game 输出方向由 C# 明确控制。

            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1); // 绘制一个全屏三角形，把中间颜色纹理采样并输出到最终目标。

            renderContext.ExecuteCommandBuffer(cmd); // 把 FinalBlit 绘制命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }

        private Material GetFinalBlitMaterial() // 定义获取 FinalBlit 材质的内部辅助函数。
        {
            if (finalBlitMaterial != null) // 如果之前已经创建过材质，就直接复用它。
            {
                return finalBlitMaterial; // 返回缓存的 FinalBlit 材质。
            }

            var shader = Shader.Find(FinalBlitShaderName); // 通过 shader 名称查找 FinalBlit shader。

            if (shader == null) // 如果 shader 查找失败，说明 shader 文件没有被 Unity 导入或名称不一致。
            {
                if (!hasLoggedMissingShader) // 如果还没有输出过警告，就输出一次。
                {
                    Debug.LogWarning("BurtRP could not find shader: " + FinalBlitShaderName); // 输出缺失 shader 警告，方便定位资源问题。

                    hasLoggedMissingShader = true; // 标记警告已经输出过，避免每帧重复打印。
                }

                return null; // 返回空材质，调用方会跳过这个 Pass。
            }

            finalBlitMaterial = new Material(shader); // 使用找到的 shader 创建一个运行时材质。

            finalBlitMaterial.hideFlags = HideFlags.HideAndDontSave; // 隐藏运行时材质并避免它被保存到场景或资产中。

            return finalBlitMaterial; // 返回创建好的 FinalBlit 材质。
        }
    }

    internal sealed class BurtReleaseCameraColorPass : BurtRenderPass // 定义 CameraColor 释放 Pass，负责在 FinalBlit 后释放中间颜色 RT。
    {
        public override string Name => "Burt Release Camera Color"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraColor(); // 声明这个 Pass 依赖 CameraColor，表示它要结束这个颜色资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 CameraColor 释放 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var cameraColorTarget = context.CameraColorTarget; // 从 GraphContext 中取出 CameraColor 资源句柄。

            if (!cameraColorTarget.IsValid) // 如果 CameraColor 句柄无效，说明当前图没有申请过这个资源。
            {
                return; // 直接结束这个 Pass，避免释放不存在的临时 RT。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.CameraColorTextureId); // 释放前面申请的 CameraColor 临时 RT，避免资源泄漏。

            renderContext.ExecuteCommandBuffer(cmd); // 把释放 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtReleaseCameraDepthPass : BurtRenderPass // 定义 CameraDepth 释放 Pass，负责在当前 request 渲染结束后释放临时深度 RT。
    {
        public override string Name => "Burt Release Camera Depth"; // 返回这个 Pass 的名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明这个 Pass 依赖 CameraDepth，表示它要结束这个深度资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 CameraDepth 释放 Pass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var cameraDepthTarget = context.CameraDepthTarget; // 从 GraphContext 中取出 CameraDepth 资源句柄。

            if (!cameraDepthTarget.IsValid) // 如果 CameraDepth 句柄无效，说明当前图没有申请过这个资源。
            {
                return; // 直接结束这个 Pass，避免释放不存在的临时 RT。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.CameraDepthTextureId); // 释放前面申请的 CameraDepth 临时 RT，避免资源泄漏。

            renderContext.ExecuteCommandBuffer(cmd); // 把释放 RT 的命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }
}
