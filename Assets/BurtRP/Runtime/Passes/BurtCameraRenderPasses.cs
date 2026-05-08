using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Color、ShaderTagId 等 Unity 类型。
using UnityEngine.Rendering; // 引入 UnityEngine.Rendering 命名空间，用来使用 CommandBufferPool、DrawingSettings、FilteringSettings 等 SRP 类型。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这些 Pass 能直接访问 BurtRenderRequest、BurtCameraData 等类型。
{
    internal static class BurtDrawingSettingsUtility // 定义一个内部工具类，用来集中创建 DrawingSettings。
    {
        private static readonly ShaderTagId BurtForward = new ShaderTagId("BurtForward"); // 定义 BurtRP 自己的主前向 Pass 名称，对应 shader 里的 LightMode。

        private static readonly ShaderTagId SRPDefaultUnlit = new ShaderTagId("SRPDefaultUnlit"); // 定义 Unity SRP 默认 Unlit Pass 名称，用来兼容简单 SRP shader。

        private static readonly ShaderTagId ForwardBase = new ShaderTagId("ForwardBase"); // 定义 Built-in 管线常见 ForwardBase Pass 名称，作为过渡期兼容。

        public static DrawingSettings CreateDrawingSettings(SortingSettings sortingSettings) // 定义创建 DrawingSettings 的函数，输入排序设置。
        {
            var drawingSettings = new DrawingSettings(BurtForward, sortingSettings); // 创建 DrawingSettings，并把 BurtForward 作为第一优先级 Shader Pass。

            drawingSettings.SetShaderPassName(1, SRPDefaultUnlit); // 设置第二优先级 Shader Pass，让 SRPDefaultUnlit shader 也能被 BurtRP 绘制。

            drawingSettings.SetShaderPassName(2, ForwardBase); // 设置第三优先级 Shader Pass，让部分 Built-in 风格 shader 过渡期也能被尝试绘制。

            return drawingSettings; // 返回创建好的 DrawingSettings。
        }
    }

    internal sealed class BurtSetRenderTargetPass : BurtRenderPass // 定义设置渲染目标的 Pass，负责把 CameraColorTarget 绑定到 GPU。
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

            cmd.SetRenderTarget(cameraColorTarget.Identifier); // 当前阶段颜色和深度都来自 CameraTarget，所以先只显式绑定 CameraColor。

            renderContext.ExecuteCommandBuffer(cmd); // 把 CommandBuffer 里的设置渲染目标命令提交给 ScriptableRenderContext。

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

            var cameraData = request.CameraData; // 从 request 中取出当前相机的 BurtCameraData。

            var clearMode = BurtCameraClearMode.SolidColor; // 定义默认清屏模式，如果没有 BurtCameraData 就使用纯色清屏。

            if (cameraData != null) // 如果当前相机挂了 BurtCameraData，就使用相机自己的清屏配置。
            {
                clearMode = cameraData.ClearMode; // 从 BurtCameraData 中读取清屏模式。
            }

            if (clearMode == BurtCameraClearMode.DontClear) // 如果清屏模式是不清屏，就不需要执行 ClearRenderTarget。
            {
                return; // 直接结束这个 Pass，让后续绘制保留当前目标里的旧内容。
            }

            var clearDepth = true; // 当前只要不是 DontClear，就清理深度，避免上一相机的深度影响当前相机。

            var clearColorBuffer = false; // 默认不清理颜色缓冲，后面根据清屏模式决定是否改为 true。

            if (clearMode == BurtCameraClearMode.SolidColor) // 如果是纯色清屏模式，就需要清理颜色缓冲。
            {
                clearColorBuffer = true; // 标记需要清理颜色缓冲。
            }

            if (clearMode == BurtCameraClearMode.Skybox) // 如果是天空盒模式，也先清理颜色缓冲作为天空盒绘制前的底色。
            {
                clearColorBuffer = true; // 标记需要清理颜色缓冲。
            }

            var clearColor = Color.black; // 定义默认清屏颜色，避免 asset 或 cameraData 为空时没有兜底颜色。

            if (cameraData != null) // 如果相机有 BurtCameraData，就优先使用相机自己的清屏颜色。
            {
                clearColor = cameraData.ClearColor; // 从 BurtCameraData 中读取清屏颜色。
            }
            else if (asset != null) // 如果没有 BurtCameraData，但是管线资产存在，就使用管线资产的默认清屏颜色。
            {
                clearColor = asset.ClearColor; // 从 BurtRenderPipelineAsset 中读取默认清屏颜色。
            }

            if (clearMode == BurtCameraClearMode.Skybox && camera != null) // 如果是天空盒模式，并且当前相机有效，就使用相机背景色作为天空盒前的底色。
            {
                clearColor = camera.backgroundColor; // 使用 Unity Camera 的 backgroundColor 作为清屏颜色。
            }

            var cmd = CommandBufferPool.Get(Name); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ClearRenderTarget(clearDepth, clearColorBuffer, clearColor); // 向 CommandBuffer 写入清理深度和颜色缓冲的命令。

            renderContext.ExecuteCommandBuffer(cmd); // 把清屏命令提交给 ScriptableRenderContext。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }

    internal sealed class BurtDrawOpaquePass : BurtRenderPass // 定义不透明物体绘制 Pass。
    {
        public override string Name => "Burt Draw Opaque"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteCameraColor(); // 声明这个 Pass 会把不透明物体颜色写入 CameraColor。

            builder.WriteCameraDepth(); // 声明这个 Pass 会通过深度测试和 ZWrite 写入 CameraDepth。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // 设置不透明物体排序规则，通常有利于 early-z 和减少 overdraw。

            var drawingSettings = BurtDrawingSettingsUtility.CreateDrawingSettings(sortingSettings); // 创建绘制设置，里面包含 BurtForward 等 Shader Pass 匹配规则。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 创建过滤设置，只允许渲染队列属于 opaque 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制所有可见不透明物体。
        }
    }

    internal sealed class BurtDrawSkyboxPass : BurtRenderPass // 定义天空盒绘制 Pass。
    {
        public override string Name => "Burt Draw Skybox"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraDepth(); // 声明这个 Pass 会参考当前深度状态来把天空盒放在场景背景位置。

            builder.WriteCameraColor(); // 声明这个 Pass 在 Skybox 模式下会把天空盒颜色写入 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来绘制天空盒。

            var cameraData = request.CameraData; // 从 request 中取出 BurtCameraData，用来判断是否需要绘制天空盒。

            if (camera == null) // 如果当前相机为空，就没有办法绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            if (cameraData == null) // 如果当前相机没有 BurtCameraData，就按当前规则不绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            if (cameraData.ClearMode != BurtCameraClearMode.Skybox) // 如果当前清屏模式不是 Skybox，就不绘制天空盒。
            {
                return; // 直接结束这个 Pass。
            }

            renderContext.DrawSkybox(camera); // 使用 Unity SRP 上下文绘制当前相机的天空盒。
        }
    }

    internal sealed class BurtDrawTransparentPass : BurtRenderPass // 定义透明物体绘制 Pass。
    {
        public override string Name => "Burt Draw Transparent"; // 返回这个 Pass 的名称，后面可以用于调试和性能分析。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadCameraColor(); // 声明透明物体需要读取已有 CameraColor 作为混合背景。

            builder.ReadCameraDepth(); // 声明透明物体需要读取当前 CameraDepth 参与深度测试。

            builder.WriteCameraColor(); // 声明透明混合结果会写回 CameraColor。
        }

        public override void Execute(BurtRenderGraphContext context) // 实现 BurtRenderPass 的执行函数。
        {
            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request.Camera; // 从 request 中取出当前相机，用来创建排序设置。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据相机信息计算透明排序参数。

            sortingSettings.criteria = SortingCriteria.CommonTransparent; // 设置透明物体排序规则，通常从后往前绘制以保证混合正确。

            var drawingSettings = BurtDrawingSettingsUtility.CreateDrawingSettings(sortingSettings); // 创建绘制设置，里面包含 BurtForward 等 Shader Pass 匹配规则。

            var filteringSettings = new FilteringSettings(RenderQueueRange.transparent); // 创建过滤设置，只允许渲染队列属于 transparent 范围的物体通过。

            renderContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings); // 使用 request 的剔除结果绘制所有可见透明物体。
        }
    }
}
