using UnityEngine; // 引入 UnityEngine 命名空间，用来使用 Camera、FilterMode 和 RenderTextureDescriptor 等 Unity 类型。
using UnityEngine.Rendering; // 引入 Unity 渲染命名空间，用来使用 CommandBufferPool。

namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让 Deferred Pass 可以访问 RenderGraph、Context 和资源注册表。
{
    internal sealed class BurtAllocateGBuffer0Pass : BurtRenderPass // 定义 GBuffer0 分配 Pass，负责申请 Deferred 第一张颜色缓存。
    {
        public override string Name => "Burt Allocate GBuffer0"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteGBuffer0(); // 声明这个 Pass 会创建并写入 GBuffer0 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer0 临时 RT 的申请。
        {
            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context); // 从上下文安全读取当前相机，用来创建匹配尺寸的 GBuffer。

            var target = context.GBuffer0Target; // 从资源表读取 GBuffer0 句柄。

            if (!target.IsValid) // 如果句柄无效，说明资源表没有注册 GBuffer0。
            {
                return; // 直接返回，避免申请后续 Pass 找不到的临时 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer0Descriptor(camera); // 创建 GBuffer0 的 RenderTexture 描述。

            BurtGBufferRenderTargetPassUtility.AllocateTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer0Id, target, descriptor); // 申请 GBuffer0 并暴露成全局纹理。
        }
    }

    internal sealed class BurtAllocateGBuffer1Pass : BurtRenderPass // 定义 GBuffer1 分配 Pass，负责申请 Deferred 第二张颜色缓存。
    {
        public override string Name => "Burt Allocate GBuffer1"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteGBuffer1(); // 声明这个 Pass 会创建并写入 GBuffer1 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer1 临时 RT 的申请。
        {
            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context); // 从上下文安全读取当前相机，用来创建匹配尺寸的 GBuffer。

            var target = context.GBuffer1Target; // 从资源表读取 GBuffer1 句柄。

            if (!target.IsValid) // 如果句柄无效，说明资源表没有注册 GBuffer1。
            {
                return; // 直接返回，避免申请后续 Pass 找不到的临时 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer1Descriptor(camera); // 创建 GBuffer1 的 RenderTexture 描述。

            BurtGBufferRenderTargetPassUtility.AllocateTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer1Id, target, descriptor); // 申请 GBuffer1 并暴露成全局纹理。
        }
    }

    internal sealed class BurtAllocateGBuffer2Pass : BurtRenderPass // 定义 GBuffer2 分配 Pass，负责申请 Deferred 第三张颜色缓存。
    {
        public override string Name => "Burt Allocate GBuffer2"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.WriteGBuffer2(); // 声明这个 Pass 会创建并写入 GBuffer2 资源。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer2 临时 RT 的申请。
        {
            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context); // 从上下文安全读取当前相机，用来创建匹配尺寸的 GBuffer。

            var target = context.GBuffer2Target; // 从资源表读取 GBuffer2 句柄。

            if (!target.IsValid) // 如果句柄无效，说明资源表没有注册 GBuffer2。
            {
                return; // 直接返回，避免申请后续 Pass 找不到的临时 RT。
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer2Descriptor(camera); // 创建 GBuffer2 的 RenderTexture 描述。

            BurtGBufferRenderTargetPassUtility.AllocateTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer2Id, target, descriptor); // 申请 GBuffer2 并暴露成全局纹理。
        }
    }

    internal sealed class BurtAllocateGBuffer3Pass : BurtRenderPass
    {
        public override string Name => "Burt Allocate GBuffer3";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteGBuffer3();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context);
            var target = context.GBuffer3Target;
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer3Descriptor(camera);
            BurtGBufferRenderTargetPassUtility.AllocateTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer3Id, target, descriptor);
        }
    }

    internal sealed class BurtAllocateGBuffer4Pass : BurtRenderPass
    {
        public override string Name => "Burt Allocate GBuffer4";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteGBuffer4();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context);
            var target = context.GBuffer4Target;
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer4Descriptor(camera);
            BurtGBufferRenderTargetPassUtility.AllocateTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer4Id, target, descriptor);
        }
    }

    internal sealed class BurtAllocateGBuffer5Pass : BurtRenderPass
    {
        public override string Name => "Burt Allocate GBuffer5";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteGBuffer5();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context);
            var target = context.GBuffer5Target;
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBuffer5Descriptor(camera);
            BurtGBufferRenderTargetPassUtility.AllocateTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer5Id, target, descriptor);
        }
    }

    internal sealed class BurtAllocateGBufferObjectIndexPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate GBuffer Object Index";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteGBufferObjectIndex();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context);
            var target = context.GBufferObjectIndexTarget;
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateGBufferObjectIndexDescriptor(camera);
            BurtGBufferRenderTargetPassUtility.AllocateTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBufferObjectIndexId, target, descriptor);
        }
    }

    internal sealed class BurtReleaseGBuffer0Pass : BurtRenderPass // 定义 GBuffer0 释放 Pass，负责结束 Deferred 第一张颜色缓存生命周期。
    {
        public override string Name => "Burt Release GBuffer0"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadGBuffer0(); // 声明这个 Pass 依赖 GBuffer0，表示它要结束这个临时资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer0 临时 RT 的释放。
        {
            var target = context.GBuffer0Target; // 从资源表读取 GBuffer0 句柄。

            if (!target.IsValid) // 如果句柄无效，说明当前图没有申请过 GBuffer0。
            {
                return; // 直接返回，避免释放不存在的临时 RT。
            }

            BurtGBufferRenderTargetPassUtility.ReleaseTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer0Id); // 释放 GBuffer0 临时 RT。
        }
    }

    internal sealed class BurtReleaseGBuffer1Pass : BurtRenderPass // 定义 GBuffer1 释放 Pass，负责结束 Deferred 第二张颜色缓存生命周期。
    {
        public override string Name => "Burt Release GBuffer1"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadGBuffer1(); // 声明这个 Pass 依赖 GBuffer1，表示它要结束这个临时资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer1 临时 RT 的释放。
        {
            var target = context.GBuffer1Target; // 从资源表读取 GBuffer1 句柄。

            if (!target.IsValid) // 如果句柄无效，说明当前图没有申请过 GBuffer1。
            {
                return; // 直接返回，避免释放不存在的临时 RT。
            }

            BurtGBufferRenderTargetPassUtility.ReleaseTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer1Id); // 释放 GBuffer1 临时 RT。
        }
    }

    internal sealed class BurtReleaseGBuffer2Pass : BurtRenderPass // 定义 GBuffer2 释放 Pass，负责结束 Deferred 第三张颜色缓存生命周期。
    {
        public override string Name => "Burt Release GBuffer2"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源使用关系。
        {
            builder.ReadGBuffer2(); // 声明这个 Pass 依赖 GBuffer2，表示它要结束这个临时资源的生命周期。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer2 临时 RT 的释放。
        {
            var target = context.GBuffer2Target; // 从资源表读取 GBuffer2 句柄。

            if (!target.IsValid) // 如果句柄无效，说明当前图没有申请过 GBuffer2。
            {
                return; // 直接返回，避免释放不存在的临时 RT。
            }

            BurtGBufferRenderTargetPassUtility.ReleaseTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer2Id); // 释放 GBuffer2 临时 RT。
        }
    }

    internal sealed class BurtReleaseGBuffer3Pass : BurtRenderPass
    {
        public override string Name => "Burt Release GBuffer3";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadGBuffer3();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context.GBuffer3Target;
            if (!target.IsValid)
            {
                return;
            }

            BurtGBufferRenderTargetPassUtility.ReleaseTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer3Id);
        }
    }

    internal sealed class BurtReleaseGBuffer4Pass : BurtRenderPass
    {
        public override string Name => "Burt Release GBuffer4";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadGBuffer4();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context.GBuffer4Target;
            if (!target.IsValid)
            {
                return;
            }

            BurtGBufferRenderTargetPassUtility.ReleaseTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer4Id);
        }
    }

    internal sealed class BurtReleaseGBuffer5Pass : BurtRenderPass
    {
        public override string Name => "Burt Release GBuffer5";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadGBuffer5();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context.GBuffer5Target;
            if (!target.IsValid)
            {
                return;
            }

            BurtGBufferRenderTargetPassUtility.ReleaseTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBuffer5Id);
        }
    }

    internal sealed class BurtReleaseGBufferObjectIndexPass : BurtRenderPass
    {
        public override string Name => "Burt Release GBuffer Object Index";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadGBufferObjectIndex();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context.GBufferObjectIndexTarget;
            if (!target.IsValid)
            {
                return;
            }

            BurtGBufferRenderTargetPassUtility.ReleaseTemporaryRenderTarget(context, Name, BurtRenderGraphResourceRegistry.GBufferObjectIndexId);
        }
    }



    internal sealed class BurtSetGBufferRenderTargetsPass : BurtRenderPass // 定义 Deferred GBuffer MRT 绑定 Pass，负责把全部 GBuffer 和 CameraDepth 同时设为当前渲染目标。
    {
        public override string Name => "Burt Set GBuffer Render Targets"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别 MRT 绑定阶段。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 会让后续绘制写入哪些目标。
        {
            builder.WriteGBuffer0(); // 声明后续 MRT 绑定会允许 shader 写入 GBuffer0。
            builder.WriteGBuffer1(); // 声明后续 MRT 绑定会允许 shader 写入 GBuffer1。
            builder.WriteGBuffer2(); // 声明后续 MRT 绑定会允许 shader 写入 GBuffer2。
            builder.WriteGBuffer3();
            builder.WriteGBuffer4();
            builder.WriteGBuffer5();
            builder.WriteGBufferObjectIndex();
            builder.WriteCameraDepth(); // 声明后续 MRT 绑定会继续使用 BurtRP 自己的 CameraDepth。
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 MRT 绑定命令。
        {
            if (!BurtGBufferRenderTargetPassUtility.TryGetGBufferAndDepthTargets(context, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var gbuffer5Target, out var gbufferObjectIndexTarget, out var cameraDepthTarget)) // 先确认全部 GBuffer 和深度目标都已经注册。
            {
                return; // 资源缺失时直接跳过，避免绑定默认 RenderTargetIdentifier 导致画面不可控。
            }

            var cmd = context.AcquireCommandBuffer(Name); // 录制到图级共享 CommandBuffer，保持 Pass 和 MRT 命令连续。

            BurtGBufferRenderTargetPassUtility.SetGBufferRenderTargets(cmd, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target, gbuffer5Target, gbufferObjectIndexTarget, cameraDepthTarget); // 把 GBuffer0/1/2/3/4 作为 MRT color attachments，把 CameraDepth 作为 depth attachment。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, BurtGBufferRenderTargetPassUtility.ResolveCamera(context));
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);

            context.ExecuteLegacyCommandBuffer(cmd); // 把 MRT 绑定命令提交给 Unity 渲染上下文。

            context.ReleaseCommandBuffer(cmd); // 图级共享缓冲由 RenderGraph 持有；本地缓冲才真正归还池。
        }
    }

    internal sealed class BurtClearGBufferRenderTargetsPass : BurtRenderPass // 定义 Deferred GBuffer 清理 Pass，负责给全部 GBuffer 写入可预测的默认值。
    {
        private static readonly Color GBuffer0ClearColor = new Color(1f, 1f, 119f / 255f, 1f);
        private static readonly Color GBuffer1ClearColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color GBuffer2ClearColor = new Color(0f, 0f, 0f, 0.5f);
        private static readonly Color GBuffer3ClearColor = new Color(0.5f, 0.5f, 0f, 0f);
        private static readonly Color GBuffer4ClearColor = Color.black;
        private static readonly Color GBuffer5ClearColor = new Color(0.5f, 0.5f, 0.5f, 0f);
        private static readonly Color GBufferObjectIndexClearColor = Color.black;
        public override string Name => "Burt Clear GBuffer Render Targets"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别清理阶段。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 会清理并写入哪些 GBuffer 资源。
        {
            builder.WriteGBuffer0(); // 声明清理会写入 GBuffer0。
            builder.WriteGBuffer1(); // 声明清理会写入 GBuffer1。
            builder.WriteGBuffer2(); // 声明清理会写入 GBuffer2。
            builder.WriteGBuffer3();
            builder.WriteGBuffer4();
            builder.WriteGBuffer5();
            builder.WriteGBufferObjectIndex();
        }

        public override void Execute(BurtRenderGraphContext context) // 执行 GBuffer 清理命令。
        {
            if (!BurtGBufferRenderTargetPassUtility.TryGetGBufferAndDepthTargets(context, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var gbuffer5Target, out var gbufferObjectIndexTarget, out var cameraDepthTarget)) // 先确认全部 GBuffer 和深度目标都已经注册。
            {
                return; // 资源缺失时直接跳过，避免清理无效目标。
            }

            var cmd = context.AcquireCommandBuffer(Name); // 录制到图级共享 CommandBuffer，保持 Clear 和 Pass 连续。

            var camera = BurtGBufferRenderTargetPassUtility.ResolveCamera(context);
            BurtGBufferRenderTargetPassUtility.ClearSingleGBufferColor(cmd, gbuffer0Target, cameraDepthTarget, GBuffer0ClearColor, camera); // 单独清理 GBuffer0，给 normal 编码和 roughness 提供稳定默认值。
            BurtGBufferRenderTargetPassUtility.ClearSingleGBufferColor(cmd, gbuffer1Target, cameraDepthTarget, GBuffer1ClearColor, camera); // 单独清理 GBuffer1，给 baseColor 黑色和 occlusion=1 提供默认值。
            BurtGBufferRenderTargetPassUtility.ClearSingleGBufferColor(cmd, gbuffer2Target, cameraDepthTarget, GBuffer2ClearColor, camera); // 单独清理 GBuffer2，这样可以给 reflectance.a 写入稳定默认值。
            BurtGBufferRenderTargetPassUtility.ClearSingleGBufferColor(cmd, gbuffer3Target, cameraDepthTarget, GBuffer3ClearColor, camera);
            BurtGBufferRenderTargetPassUtility.ClearSingleGBufferColor(cmd, gbuffer4Target, cameraDepthTarget, GBuffer4ClearColor, camera);
            BurtGBufferRenderTargetPassUtility.ClearSingleGBufferColor(cmd, gbuffer5Target, cameraDepthTarget, GBuffer5ClearColor, camera);
            BurtGBufferRenderTargetPassUtility.ClearSingleGBufferColor(cmd, gbufferObjectIndexTarget, cameraDepthTarget, GBufferObjectIndexClearColor, camera);
            BurtGBufferRenderTargetPassUtility.SetGBufferRenderTargets(cmd, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target, gbuffer5Target, gbufferObjectIndexTarget, cameraDepthTarget); // 清理完成后重新绑定 MRT，方便后续 Draw GBuffer Pass 直接绘制。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);

            context.ExecuteLegacyCommandBuffer(cmd); // 把清理和最终 MRT 绑定命令提交给 Unity 渲染上下文。

            context.ReleaseCommandBuffer(cmd); // 图级共享缓冲由 RenderGraph 持有；本地缓冲才真正归还池。
        }
    }

    internal sealed class BurtDepthNormalPrepass : BurtRenderPass
    {
        public override string Name => "Burt Depth Normal Prepass";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteCameraDepth();
            builder.WriteGBuffer0();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var request = context != null ? context.Request : null;
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var cameraDepthTarget = context.CameraDepthTarget;
            var gbuffer0Target = context.GBuffer0Target;
            if (!cameraDepthTarget.IsValid || !gbuffer0Target.IsValid)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetRenderTarget(gbuffer0Target.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);

            var sortingSettings = new SortingSettings(camera);
            sortingSettings.criteria = SortingCriteria.CommonOpaque;

            var drawingSettings = BurtDrawingSettingsUtility.CreateDepthNormalsDrawingSettings(sortingSettings);
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRendererList(request.CullingResults, ref drawingSettings, ref filteringSettings);
        }
    }

    internal sealed class BurtDrawGBufferOpaquePass : BurtRenderPass // 定义 Deferred 不透明 GBuffer 绘制 Pass，保留 prepass GBuffer0 并写入剩余材质 MRT。
    {
        public override string Name => "Burt Draw GBuffer Opaque"; // 返回 Pass 名称，方便 RenderGraph Debug 和 Frame Debugger 识别真正的 GBuffer 绘制阶段。

        public override void Configure(BurtRenderPassBuilder builder) // 声明这个 Pass 的资源读写关系。
        {
            builder.ReadCameraDepth(); // GBuffer 绘制使用 DepthNormals prepass 写好的深度做 ZTest Equal。
            builder.ReadGBuffer0(); // GBuffer0 的 normal/roughness 来自 DepthNormals prepass，普通 GBuffer pass 只保留它。
            builder.WriteGBuffer1(); // 声明这个 Pass 会写入 GBuffer1，后续保存 baseColor 和 occlusion。
            builder.WriteGBuffer2(); // 声明这个 Pass 会写入 GBuffer2，后续保存 packed material properties 和 reflectance。
            builder.WriteGBuffer3();
            builder.WriteGBuffer4();
            builder.WriteGBuffer5();
            builder.WriteGBufferObjectIndex();
        }

        public override void Execute(BurtRenderGraphContext context) // 执行不透明物体的 GBuffer 绘制。
        {
            if (!BurtGBufferRenderTargetPassUtility.TryGetGBufferAndDepthTargets(context, out var gbuffer0Target, out var gbuffer1Target, out var gbuffer2Target, out var gbuffer3Target, out var gbuffer4Target, out var gbuffer5Target, out var gbufferObjectIndexTarget, out var cameraDepthTarget)) // 先确认全部 GBuffer 和深度目标都有效。
            {
                return; // 任意目标无效时直接跳过，避免 DrawRenderers 写入错误目标。
            }

            var renderContext = context.ScriptableContext; // 从 GraphContext 中取出 Unity SRP 渲染上下文。

            var request = context.Request; // 从 GraphContext 中取出当前渲染请求。

            var camera = request != null ? request.Camera : null; // 从 request 中安全读取当前相机，用来创建排序设置。

            if (camera == null) // 如果没有相机，就无法创建正确的排序设置。
            {
                return; // 直接结束这个 Pass，避免后续 DrawRenderers 访问空相机。
            }

            var cmd = context.AcquireCommandBuffer(Name); // GBuffer 状态和 RendererList 进入同一个图级命令流。

            BurtGBufferRenderTargetPassUtility.SetGBufferRenderTargets(cmd, gbuffer0Target, gbuffer1Target, gbuffer2Target, gbuffer3Target, gbuffer4Target, gbuffer5Target, gbufferObjectIndexTarget, cameraDepthTarget); // 绘制前重新绑定 GBuffer MRT，避免前一个 Pass 改过渲染目标。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            BurtDrawingSettingsUtility.RestoreCameraMatricesForMainDraw(context, cmd);

            context.ExecuteLegacyCommandBuffer(cmd); // Preserve graph ordering before the legacy local command buffer is submitted.

            context.ReleaseCommandBuffer(cmd); // 图级共享缓冲由 RenderGraph 持有；本地缓冲才真正归还池。

            var sortingSettings = new SortingSettings(camera); // 创建排序设置，Unity 会根据当前相机计算不透明排序参数。

            sortingSettings.criteria = SortingCriteria.CommonOpaque; // GBuffer 只绘制不透明物体，使用 CommonOpaque 保持和 Forward Opaque 接近的排序规则。

            var drawingSettings = BurtDrawingSettingsUtility.CreateGBufferDrawingSettings(sortingSettings); // 创建只匹配 LightMode=BurtGBuffer 的绘制设置。

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque); // 创建过滤设置，只允许 opaque 渲染队列进入 GBuffer。

            context.DrawRendererList(request.CullingResults, ref drawingSettings, ref filteringSettings); // 把 GBuffer renderer list 录进图级命令流，使实际 Draw 保持在当前 Pass 标记内。
        }
    }


    internal static class BurtGBufferRenderTargetPassUtility // 定义 GBuffer 分配和释放共用工具，避免六个 Pass 重复命令缓冲代码。
    {
        public static Camera ResolveCamera(BurtRenderGraphContext context) // 从 RenderGraph 上下文安全读取当前相机。
        {
            var request = context != null ? context.Request : null; // 先读取 request，context 为空时保持 request 为空。

            return request != null ? request.Camera : null; // request 有效时返回相机，否则返回 null 让描述工具使用 1x1 兜底尺寸。
        }

        public static bool TryGetGBufferAndDepthTargets( // 安全读取 Deferred MRT 需要的全部渲染目标。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            out BurtRenderTargetHandle gbuffer0Target, // 输出 GBuffer0 句柄。
            out BurtRenderTargetHandle gbuffer1Target, // 输出 GBuffer1 句柄。
            out BurtRenderTargetHandle gbuffer2Target, // 输出 GBuffer2 句柄。
            out BurtRenderTargetHandle gbuffer3Target,
            out BurtRenderTargetHandle gbuffer4Target,
            out BurtRenderTargetHandle gbuffer5Target,
            out BurtRenderTargetHandle cameraDepthTarget)
        {
            return TryGetGBufferAndDepthTargets(
                context,
                out gbuffer0Target,
                out gbuffer1Target,
                out gbuffer2Target,
                out gbuffer3Target,
                out gbuffer4Target,
                out gbuffer5Target,
                out _,
                out cameraDepthTarget);
        }

        public static bool TryGetGBufferAndDepthTargets( // 安全读取 Deferred MRT 需要的全部渲染目标。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            out BurtRenderTargetHandle gbuffer0Target, // 输出 GBuffer0 句柄。
            out BurtRenderTargetHandle gbuffer1Target, // 输出 GBuffer1 句柄。
            out BurtRenderTargetHandle gbuffer2Target, // 输出 GBuffer2 句柄。
            out BurtRenderTargetHandle gbuffer3Target,
            out BurtRenderTargetHandle gbuffer4Target,
            out BurtRenderTargetHandle gbuffer5Target,
            out BurtRenderTargetHandle gbufferObjectIndexTarget,
            out BurtRenderTargetHandle cameraDepthTarget) // 输出 CameraDepth 句柄。
        {
            gbuffer0Target = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name); // context 有效时读取 GBuffer0，否则返回无效句柄。
            gbuffer1Target = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name); // context 有效时读取 GBuffer1，否则返回无效句柄。
            gbuffer2Target = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name); // context 有效时读取 GBuffer2，否则返回无效句柄。
            gbuffer3Target = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4Target = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            gbuffer5Target = context != null ? context.GBuffer5Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer5Name);
            gbufferObjectIndexTarget = context != null ? context.GBufferObjectIndexTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBufferObjectIndexName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName); // context 有效时读取 CameraDepth，否则返回无效句柄。

            return gbuffer0Target.IsValid && gbuffer1Target.IsValid && gbuffer2Target.IsValid && gbuffer3Target.IsValid && gbuffer4Target.IsValid && gbuffer5Target.IsValid && gbufferObjectIndexTarget.IsValid && cameraDepthTarget.IsValid; // 只有五个 GBuffer 目标和深度目标全部有效时才允许绑定 MRT。
        }

        public static void SetGBufferRenderTargets(
            CommandBuffer cmd,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle gbuffer3Target,
            BurtRenderTargetHandle gbuffer4Target,
            BurtRenderTargetHandle gbuffer5Target,
            BurtRenderTargetHandle cameraDepthTarget)
        {
            var colorTargets = new[]
            {
                gbuffer0Target.Identifier,
                gbuffer1Target.Identifier,
                gbuffer2Target.Identifier,
                gbuffer3Target.Identifier,
                gbuffer4Target.Identifier,
                gbuffer5Target.Identifier
            };
            cmd.SetRenderTarget(colorTargets, cameraDepthTarget.Identifier);
        }

        public static void SetGBufferRenderTargets(
            CommandBuffer cmd,
            BurtRenderTargetHandle gbuffer0Target,
            BurtRenderTargetHandle gbuffer1Target,
            BurtRenderTargetHandle gbuffer2Target,
            BurtRenderTargetHandle gbuffer3Target,
            BurtRenderTargetHandle gbuffer4Target,
            BurtRenderTargetHandle gbuffer5Target,
            BurtRenderTargetHandle gbufferObjectIndexTarget,
            BurtRenderTargetHandle cameraDepthTarget)
        {
            var colorTargets = new[]
            {
                gbuffer0Target.Identifier,
                gbuffer1Target.Identifier,
                gbuffer2Target.Identifier,
                gbuffer3Target.Identifier,
                gbuffer4Target.Identifier,
                gbuffer5Target.Identifier,
                gbufferObjectIndexTarget.Identifier
            };
            cmd.SetRenderTarget(colorTargets, cameraDepthTarget.Identifier);
        }
        public static void ClearSingleGBufferColor( // 单独清理某一张 GBuffer 颜色目标。
            CommandBuffer cmd, // 接收要写入命令的 CommandBuffer。
            BurtRenderTargetHandle colorTarget, // 接收需要清理的 GBuffer 句柄。
            BurtRenderTargetHandle cameraDepthTarget, // 接收 CameraDepth 句柄，仅用于满足 SetRenderTarget 的 depth attachment。
            Color clearColor, // 接收这张 GBuffer 对应的清理颜色。
            Camera camera)
        {
            cmd.SetRenderTarget(colorTarget.Identifier, cameraDepthTarget.Identifier); // 先把单张 GBuffer 绑定为当前 color attachment。
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);

            cmd.ClearRenderTarget(false, true, clearColor); // 只清理颜色，不清理 CameraDepth，避免当前测试阶段改变 Forward fallback 的深度行为。
        }

        public static void AllocateTemporaryRenderTarget( // 申请一个 GBuffer 临时 RT 并绑定全局纹理。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            string passName, // 接收当前 Pass 名称，用来命名 CommandBuffer。
            int textureId, // 接收 GBuffer 对应的全局纹理 ID。
            BurtRenderTargetHandle target, // 接收 GBuffer 对应的 RenderGraph 句柄。
            RenderTextureDescriptor descriptor) // 接收 GBuffer 的 RT 描述。
        {
            if (context == null) // 如果上下文为空，就没有命令提交目标。
            {
                return; // 直接返回，避免空引用。
            }

            var cmd = context.AcquireCommandBuffer(passName); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(textureId, descriptor, FilterMode.Point); // 申请 GBuffer 临时 RT，使用 Point 过滤避免材质数据被线性插值。

            cmd.SetGlobalTexture(textureId, target.Identifier); // 把 GBuffer 暴露给 shader，后续 Deferred Lighting 或 Debug Pass 可以采样。

            context.ExecuteLegacyCommandBuffer(cmd); // 把申请 RT 和绑定全局纹理的命令提交给 Unity 渲染上下文。

            context.ReleaseCommandBuffer(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }

        public static void ReleaseTemporaryRenderTarget( // 释放一个 GBuffer 临时 RT。
            BurtRenderGraphContext context, // 接收当前 RenderGraph 执行上下文。
            string passName, // 接收当前 Pass 名称，用来命名 CommandBuffer。
            int textureId) // 接收要释放的 GBuffer 全局纹理 ID。
        {
            if (context == null) // 如果上下文为空，就没有命令提交目标。
            {
                return; // 直接返回，避免空引用。
            }

            var cmd = context.AcquireCommandBuffer(passName); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ReleaseTemporaryRT(textureId); // 释放前面申请的 GBuffer 临时 RT，避免资源泄漏到下一帧。

            context.ExecuteLegacyCommandBuffer(cmd); // 把释放 RT 的命令提交给 Unity 渲染上下文。

            context.ReleaseCommandBuffer(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }
}
