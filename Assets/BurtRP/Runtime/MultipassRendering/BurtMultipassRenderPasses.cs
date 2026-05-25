using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal abstract class BurtDrawMultipassRenderPassBase : BurtRenderPass
    {
        private readonly BurtMultipassShaderPass multipassPass;
        private readonly RenderQueueRange renderQueueRange;

        protected BurtDrawMultipassRenderPassBase(
            BurtMultipassShaderPass multipassPass,
            RenderQueueRange renderQueueRange)
        {
            this.multipassPass = multipassPass;
            this.renderQueueRange = renderQueueRange;
        }

        public override BurtRenderPassKind Kind => BurtRenderPassKind.DrawRenderers;

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BindTargets(context))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.BeginSample(Name);
            BurtMultipassRenderer.DrawAll(cmd, context, multipassPass, renderQueueRange);
            cmd.EndSample(Name);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        protected abstract bool BindTargets(BurtRenderGraphContext context);
    }

    internal sealed class BurtDrawMultipassDepthPrepass : BurtDrawMultipassRenderPassBase
    {
        public BurtDrawMultipassDepthPrepass()
            : base(BurtMultipassShaderPass.DepthOnly, RenderQueueRange.opaque)
        {
        }

        public override string Name => "Burt Draw Multipass Depth Prepass";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteCameraDepth();
        }

        protected override bool BindTargets(BurtRenderGraphContext context)
        {
            if (context == null || !context.CameraDepthTarget.IsValid)
            {
                return false;
            }

            var cmd = CommandBufferPool.Get(Name + " Bind");
            cmd.SetRenderTarget(context.CameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            return true;
        }
    }

    internal sealed class BurtDrawMultipassForwardOpaquePass : BurtDrawMultipassRenderPassBase
    {
        public BurtDrawMultipassForwardOpaquePass()
            : base(BurtMultipassShaderPass.Forward, RenderQueueRange.opaque)
        {
        }

        public override string Name => "Burt Draw Multipass Forward Opaque";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (builder.Asset == null || builder.Asset.EnableDepthPrepass)
            {
                builder.ReadCameraDepth();
            }

            builder.ReadLightingGlobals();
            builder.ReadShadowGlobals();
            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset))
            {
                builder.ReadMainLightShadowMap();
            }

            builder.WriteCameraColor();
            builder.WriteCameraDepth();
        }

        protected override bool BindTargets(BurtRenderGraphContext context)
        {
            return BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name + " Bind");
        }
    }

    internal sealed class BurtDrawMultipassForwardOnlyOpaquePass : BurtDrawMultipassRenderPassBase
    {
        public BurtDrawMultipassForwardOnlyOpaquePass()
            : base(BurtMultipassShaderPass.ForwardOnly, RenderQueueRange.opaque)
        {
        }

        public override string Name => "Burt Draw Multipass Forward Only Opaque";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadCameraDepth();
            builder.ReadLightingGlobals();
            builder.ReadShadowGlobals();
            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset))
            {
                builder.ReadMainLightShadowMap();
            }

            builder.WriteCameraColor();
            builder.WriteCameraDepth();
        }

        protected override bool BindTargets(BurtRenderGraphContext context)
        {
            return BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name + " Bind");
        }
    }

    internal sealed class BurtDrawMultipassTransparentPass : BurtDrawMultipassRenderPassBase
    {
        public BurtDrawMultipassTransparentPass()
            : base(BurtMultipassShaderPass.Forward, RenderQueueRange.transparent)
        {
        }

        public override string Name => "Burt Draw Multipass Transparent";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.ReadLightingGlobals();
            builder.ReadShadowGlobals();
            if (BurtShadowUtility.ShouldUseMainLightShadow(builder.Request, builder.Asset))
            {
                builder.ReadMainLightShadowMap();
            }

            builder.WriteCameraColor();
        }

        protected override bool BindTargets(BurtRenderGraphContext context)
        {
            return BurtDrawingSettingsUtility.BindCameraColorAndDepth(context, Name + " Bind");
        }
    }

    internal sealed class BurtDrawMultipassGBufferOpaquePass : BurtDrawMultipassRenderPassBase
    {
        public BurtDrawMultipassGBufferOpaquePass()
            : base(BurtMultipassShaderPass.GBuffer, RenderQueueRange.opaque)
        {
        }

        public override string Name => "Burt Draw Multipass GBuffer Opaque";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (builder.Asset == null || builder.Asset.EnableDepthPrepass)
            {
                builder.ReadCameraDepth();
            }

            builder.WriteGBuffer0();
            builder.WriteGBuffer1();
            builder.WriteGBuffer2();
            builder.WriteGBuffer3();
            builder.WriteGBuffer4();
            builder.WriteCameraDepth();
        }

        protected override bool BindTargets(BurtRenderGraphContext context)
        {
            if (!BurtGBufferRenderTargetPassUtility.TryGetGBufferAndDepthTargets(
                    context,
                    out var gbuffer0Target,
                    out var gbuffer1Target,
                    out var gbuffer2Target,
                    out var gbuffer3Target,
                    out var gbuffer4Target,
                    out var cameraDepthTarget))
            {
                return false;
            }

            var cmd = CommandBufferPool.Get(Name + " Bind");
            BurtGBufferRenderTargetPassUtility.SetGBufferRenderTargets(
                cmd,
                gbuffer0Target,
                gbuffer1Target,
                gbuffer2Target,
                gbuffer3Target,
                gbuffer4Target,
                cameraDepthTarget);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            return true;
        }
    }
}
