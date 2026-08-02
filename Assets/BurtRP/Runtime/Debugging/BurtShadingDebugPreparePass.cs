using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtShadingDebugPreparePass : BurtRenderPass
    {
        public override string Name => "Burt Shading Debug Prepare";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtShadingDebugSettings.RecordGlobalShaderProperties(cmd, context.Request);
            context.ExecuteLegacyCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
