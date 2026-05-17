namespace Burt.RenderPipeline // 定义 BurtRP 的命名空间，让这个 Pass 基类和其他 BurtRP 代码处在同一个模块里。
{
    public enum BurtRenderPassKind // Coarse pass category for debug output and future culling analysis.
    {
        Generic,
        Allocate,
        Release,
        SetRenderTarget,
        Clear,
        DrawRenderers,
        FullScreen,
        PostProcess,
        Debug,
        GlobalState,
        Copy,
    }

    internal static class BurtRenderPassKindUtility // Keeps fallback name-based pass classification in one place.
    {
        public static BurtRenderPassKind InferKind(string passName)
        {
            if (Contains(passName, "Allocate"))
            {
                return BurtRenderPassKind.Allocate;
            }

            if (Contains(passName, "Release"))
            {
                return BurtRenderPassKind.Release;
            }

            if (Contains(passName, "Debug"))
            {
                return BurtRenderPassKind.Debug;
            }

            if (Contains(passName, "Set Render Target") || Contains(passName, "Set GBuffer Render Targets"))
            {
                return BurtRenderPassKind.SetRenderTarget;
            }

            if (IsDeferredLightingPass(passName) ||
                Contains(passName, "HiZ") ||
                Contains(passName, "Screen Space Reflections") ||
                Contains(passName, "Screen Space Subsurface"))
            {
                return BurtRenderPassKind.FullScreen;
            }

            if (Contains(passName, "Clear"))
            {
                return BurtRenderPassKind.Clear;
            }

            if (Contains(passName, "Setup Lighting"))
            {
                return BurtRenderPassKind.GlobalState;
            }

            if (Contains(passName, "Final Blit") || Contains(passName, "Seed Overlay") || Contains(passName, "Copy"))
            {
                return BurtRenderPassKind.Copy;
            }

            if (Contains(passName, "Post Process"))
            {
                return BurtRenderPassKind.PostProcess;
            }

            if (Contains(passName, "Draw") || Contains(passName, "Depth Prepass"))
            {
                return BurtRenderPassKind.DrawRenderers;
            }

            return BurtRenderPassKind.Generic;
        }

        private static bool Contains(string passName, string token)
        {
            return !string.IsNullOrEmpty(passName) && passName.IndexOf(token, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDeferredLightingPass(string passName)
        {
            return Contains(passName, "Deferred") &&
                Contains(passName, "Lighting") &&
                !Contains(passName, "Clear Deferred Lighting Target");
        }
    }

    public abstract class BurtRenderPass // 定义 BurtRP 的渲染 Pass 基类，所有具体渲染步骤都继承它。
    {
        public abstract string Name { get; } // 定义 Pass 名称，后面用于 Frame Debugger、Profiler 或日志显示。

        public virtual BurtRenderPassKind Kind => BurtRenderPassKindUtility.InferKind(Name); // Default to conservative name-based classification.

        public virtual bool HasSideEffects => true; // Keep all existing passes conservative until real culling is implemented.

        public virtual bool AllowCulling => false; // Metadata only for now; execution order and pass count stay unchanged.

        public virtual void Configure(BurtRenderPassBuilder builder) // 定义 Pass 配置阶段入口，用来声明资源读写关系。
        {
        } // 默认实现为空，表示这个 Pass 暂时没有声明任何资源读写。

        public abstract void Execute( // 定义 Pass 的执行入口，每个具体 Pass 都必须实现这个函数。
            BurtRenderGraphContext context); // 接收 RenderGraph 执行上下文，里面包含 ScriptableContext、Request、Asset、资源表。
    }

    internal sealed class BurtAllocateRenderBufferPass : BurtRenderPass // Generic RenderGraph buffer allocation pass used by tiled/cluster setup.
    {
        private readonly string bufferName;

        public BurtAllocateRenderBufferPass(string bufferName)
        {
            this.bufferName = bufferName;
        }

        public override string Name => "Burt Allocate Buffer " + (string.IsNullOrEmpty(bufferName) ? "<unnamed>" : bufferName);

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Allocate;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteBuffer(bufferName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || context.ResourceRegistry == null)
            {
                return;
            }

            context.ResourceRegistry.AllocateBuffer(bufferName);
        }
    }

    internal sealed class BurtReleaseRenderBufferPass : BurtRenderPass // Generic RenderGraph buffer release pass used by tiled/cluster cleanup.
    {
        private readonly string bufferName;

        public BurtReleaseRenderBufferPass(string bufferName)
        {
            this.bufferName = bufferName;
        }

        public override string Name => "Burt Release Buffer " + (string.IsNullOrEmpty(bufferName) ? "<unnamed>" : bufferName);

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Release;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadBuffer(bufferName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || context.ResourceRegistry == null)
            {
                return;
            }

            context.ResourceRegistry.ReleaseBuffer(bufferName);
        }
    }
}
