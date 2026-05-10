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

    internal static class BurtGBufferRenderTargetPassUtility // 定义 GBuffer 分配和释放共用工具，避免六个 Pass 重复命令缓冲代码。
    {
        public static Camera ResolveCamera(BurtRenderGraphContext context) // 从 RenderGraph 上下文安全读取当前相机。
        {
            var request = context != null ? context.Request : null; // 先读取 request，context 为空时保持 request 为空。

            return request != null ? request.Camera : null; // request 有效时返回相机，否则返回 null 让描述工具使用 1x1 兜底尺寸。
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

            var cmd = CommandBufferPool.Get(passName); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.GetTemporaryRT(textureId, descriptor, FilterMode.Point); // 申请 GBuffer 临时 RT，使用 Point 过滤避免材质数据被线性插值。

            cmd.SetGlobalTexture(textureId, target.Identifier); // 把 GBuffer 暴露给 shader，后续 Deferred Lighting 或 Debug Pass 可以采样。

            context.ScriptableContext.ExecuteCommandBuffer(cmd); // 把申请 RT 和绑定全局纹理的命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
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

            var cmd = CommandBufferPool.Get(passName); // 从 Unity 命令缓冲池获取一个 CommandBuffer，并用 Pass 名称命名它。

            cmd.ReleaseTemporaryRT(textureId); // 释放前面申请的 GBuffer 临时 RT，避免资源泄漏到下一帧。

            context.ScriptableContext.ExecuteCommandBuffer(cmd); // 把释放 RT 的命令提交给 Unity 渲染上下文。

            CommandBufferPool.Release(cmd); // 把 CommandBuffer 释放回池子，避免每帧产生 GC。
        }
    }
}

