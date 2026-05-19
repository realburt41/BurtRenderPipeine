using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateScreenSpaceSubsurfaceSourcePass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Source";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceSource();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceTextureId,
                context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName));
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceTempPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Temp";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempTextureId,
                context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName));
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceSetupPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Setup";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceSetup();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupTextureId,
                context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName),
                BurtScreenSpaceSubsurfacePassUtility.CreateSetupDescriptor(context),
                FilterMode.Point);
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceTilePass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Tile";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceTile();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTileTextureId,
                context != null ? context.ScreenSpaceSubsurfaceTileTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTileName),
                BurtScreenSpaceSubsurfacePassUtility.CreateTileDescriptor(context),
                FilterMode.Point);
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceBlurPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Blur";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceBlur();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurTextureId,
                context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName));
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceCombinePass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Combine";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceCombine();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineTextureId,
                context != null ? context.ScreenSpaceSubsurfaceCombineTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName),
                BurtScreenSpaceSubsurfacePassUtility.CreateCombineDescriptor(context),
                FilterMode.Bilinear);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceCopySourcePass : BurtRenderPass
    {
        private const int CopyPassIndex = 0;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Copy Source";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.WriteScreenSpaceSubsurfaceSource();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var cameraDepthTarget, out var target))
            {
                return;
            }

            var copyMaterial = GetMaterial();
            if (copyMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, cameraColorTarget.Identifier);
            Draw(cmd, copyMaterial, target.Identifier, cameraDepthTarget.Identifier, context, CopyPassIndex);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraColorTarget,
            out BurtRenderTargetHandle cameraDepthTarget,
            out BurtRenderTargetHandle target)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            target = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                cameraColorTarget.IsValid &&
                cameraDepthTarget.IsValid &&
                target.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }

        private static void Draw(CommandBuffer cmd, Material material, RenderTargetIdentifier target, RenderTargetIdentifier depthStencil, BurtRenderGraphContext context, int passIndex)
        {
            cmd.SetRenderTarget(target, depthStencil);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceSetupPass : BurtRenderPass
    {
        private const int SetupPassIndex = 3;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Setup";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteScreenSpaceSubsurfaceSetup();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepth, out var gbuffer0, out var gbuffer1, out var gbuffer2, out var gbuffer3, out var gbuffer4, out var target))
            {
                return;
            }

            var setupMaterial = GetMaterial();
            if (setupMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindGBufferInputs(cmd, context, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            Draw(cmd, setupMaterial, target.Identifier, cameraDepth.Identifier, context, SetupPassIndex);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle gbuffer0,
            out BurtRenderTargetHandle gbuffer1,
            out BurtRenderTargetHandle gbuffer2,
            out BurtRenderTargetHandle gbuffer3,
            out BurtRenderTargetHandle gbuffer4,
            out BurtRenderTargetHandle target)
        {
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2 = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            target = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                cameraDepth.IsValid &&
                gbuffer0.IsValid &&
                gbuffer1.IsValid &&
                gbuffer2.IsValid &&
                gbuffer3.IsValid &&
                gbuffer4.IsValid &&
                target.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }

        private static void Draw(CommandBuffer cmd, Material material, RenderTargetIdentifier target, RenderTargetIdentifier depthStencil, BurtRenderGraphContext context, int passIndex)
        {
            cmd.SetRenderTarget(target, depthStencil);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceTilePass : BurtRenderPass
    {
        private const int TilePassIndex = 4;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Tile";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSetup();
            builder.WriteScreenSpaceSubsurfaceTile();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var setup = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
            var target = context != null ? context.ScreenSpaceSubsurfaceTileTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTileName);
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !setup.IsValid ||
                !target.IsValid)
            {
                return;
            }

            var tileMaterial = GetMaterial();
            if (tileMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindSetupTileInputs(cmd, context, setup, target);
            cmd.SetRenderTarget(target.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetTileViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, tileMaterial, TilePassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceBlurHorizontalPass : BurtRenderPass
    {
        private const int BlurHorizontalPassIndex = 1;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Blur Horizontal";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.ReadScreenSpaceSubsurfaceTile();
            builder.WriteScreenSpaceSubsurfaceTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var source, out var cameraDepth, out var gbuffer0, out var gbuffer1, out var gbuffer2, out var gbuffer3, out var gbuffer4, out var tile, out var target))
            {
                return;
            }

            var blurMaterial = GetMaterial();
            if (blurMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BindInputs(cmd, context, source, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4, tile);
            Draw(cmd, blurMaterial, target.Identifier, cameraDepth.Identifier, context, BlurHorizontalPassIndex);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle source,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle gbuffer0,
            out BurtRenderTargetHandle gbuffer1,
            out BurtRenderTargetHandle gbuffer2,
            out BurtRenderTargetHandle gbuffer3,
            out BurtRenderTargetHandle gbuffer4,
            out BurtRenderTargetHandle tile,
            out BurtRenderTargetHandle target)
        {
            source = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2 = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            tile = context != null ? context.ScreenSpaceSubsurfaceTileTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTileName);
            target = context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                source.IsValid &&
                cameraDepth.IsValid &&
                gbuffer0.IsValid &&
                gbuffer1.IsValid &&
                gbuffer2.IsValid &&
                gbuffer3.IsValid &&
                gbuffer4.IsValid &&
                tile.IsValid &&
                target.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }

        private static void BindInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle source,
            BurtRenderTargetHandle cameraDepth,
            BurtRenderTargetHandle gbuffer0,
            BurtRenderTargetHandle gbuffer1,
            BurtRenderTargetHandle gbuffer2,
            BurtRenderTargetHandle gbuffer3,
            BurtRenderTargetHandle gbuffer4,
            BurtRenderTargetHandle tile)
        {
            BurtScreenSpaceSubsurfacePassUtility.BindCommonInputs(cmd, context, source, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.TileTextureId, tile.Identifier);
        }

        private static void Draw(CommandBuffer cmd, Material material, RenderTargetIdentifier target, RenderTargetIdentifier depthStencil, BurtRenderGraphContext context, int passIndex)
        {
            cmd.SetRenderTarget(target, depthStencil);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceBlurVerticalPass : BurtRenderPass
    {
        private const int BlurVerticalPassIndex = 2;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Blur Vertical";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceTemp();
            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.ReadScreenSpaceSubsurfaceTile();
            builder.WriteScreenSpaceSubsurfaceBlur();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var source, out var original, out var cameraDepth, out var gbuffer0, out var gbuffer1, out var gbuffer2, out var gbuffer3, out var gbuffer4, out var tile, out var target))
            {
                return;
            }

            var blurMaterial = GetMaterial();
            if (blurMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindCommonInputs(cmd, context, source, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.OriginalTextureId, original.Identifier);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.TileTextureId, tile.Identifier);
            Draw(cmd, blurMaterial, target.Identifier, cameraDepth.Identifier, context, BlurVerticalPassIndex);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle source,
            out BurtRenderTargetHandle original,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle gbuffer0,
            out BurtRenderTargetHandle gbuffer1,
            out BurtRenderTargetHandle gbuffer2,
            out BurtRenderTargetHandle gbuffer3,
            out BurtRenderTargetHandle gbuffer4,
            out BurtRenderTargetHandle tile,
            out BurtRenderTargetHandle target)
        {
            source = context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
            original = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2 = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            tile = context != null ? context.ScreenSpaceSubsurfaceTileTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTileName);
            target = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                source.IsValid &&
                original.IsValid &&
                cameraDepth.IsValid &&
                gbuffer0.IsValid &&
                gbuffer1.IsValid &&
                gbuffer2.IsValid &&
                gbuffer3.IsValid &&
                gbuffer4.IsValid &&
                tile.IsValid &&
                target.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }

        private static void Draw(CommandBuffer cmd, Material material, RenderTargetIdentifier target, RenderTargetIdentifier depthStencil, BurtRenderGraphContext context, int passIndex)
        {
            cmd.SetRenderTarget(target, depthStencil);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceCopyToCameraColorPass : BurtRenderPass
    {
        private const int CombinePassIndex = 5;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Combine";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceBlur();
            builder.ReadScreenSpaceSubsurfaceSetup();
            builder.ReadScreenSpaceSubsurfaceTile();
            builder.ReadCameraDepth();
            builder.WriteScreenSpaceSubsurfaceCombine();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var source = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
            var setup = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
            var tile = context != null ? context.ScreenSpaceSubsurfaceTileTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTileName);
            var combine = context != null ? context.ScreenSpaceSubsurfaceCombineTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
            var cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            var target = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !source.IsValid ||
                !setup.IsValid ||
                !tile.IsValid ||
                !combine.IsValid ||
                !cameraDepth.IsValid ||
                !target.IsValid)
            {
                return;
            }

            var copyMaterial = GetMaterial();
            if (copyMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, source.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.BindSetupTileInputs(cmd, context, setup, tile);
            cmd.SetRenderTarget(combine.Identifier, cameraDepth.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, copyMaterial, CombinePassIndex, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, combine.Identifier);
            cmd.SetRenderTarget(target.Identifier, cameraDepth.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, copyMaterial, 0, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceSourcePass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Source";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSource();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceSetupPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Setup";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSetup();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceTilePass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Tile";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceTile();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTileTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceTempPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Temp";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceBlurPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Blur";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceBlur();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceCombinePass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Combine";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceCombine();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineTextureId);
        }
    }

    internal sealed class BurtDebugScreenSpaceSubsurfacePass : BurtRenderPass
    {
        private const int DebugPassIndex = 6;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Debug Screen Space Subsurface";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceDebugView(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSetup();
            builder.ReadScreenSpaceSubsurfaceTile();
            builder.ReadScreenSpaceSubsurfaceBlur();
            builder.ReadScreenSpaceSubsurfaceCombine();
            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceDebugView(context.Request, context.Asset))
            {
                return;
            }

            var setup = context.ScreenSpaceSubsurfaceSetupTarget;
            var tile = context.ScreenSpaceSubsurfaceTileTarget;
            var blur = context.ScreenSpaceSubsurfaceBlurTarget;
            var combine = context.ScreenSpaceSubsurfaceCombineTarget;
            var source = context.ScreenSpaceSubsurfaceSourceTarget;
            var cameraDepth = context.CameraDepthTarget;
            var gbuffer0 = context.GBuffer0Target;
            var gbuffer1 = context.GBuffer1Target;
            var gbuffer2 = context.GBuffer2Target;
            var gbuffer3 = context.GBuffer3Target;
            var gbuffer4 = context.GBuffer4Target;
            var cameraColor = context.CameraColorTarget;
            if (!setup.IsValid || !tile.IsValid || !blur.IsValid || !combine.IsValid || !source.IsValid || !cameraDepth.IsValid || !gbuffer0.IsValid || !gbuffer1.IsValid || !gbuffer2.IsValid || !gbuffer3.IsValid || !gbuffer4.IsValid || !cameraColor.IsValid)
            {
                return;
            }

            var debugMaterial = GetMaterial();
            if (debugMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindDebugInputs(cmd, context, setup, tile, blur, combine, source, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            cmd.SetRenderTarget(cameraColor.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, DebugPassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal static class BurtScreenSpaceSubsurfacePassUtility
    {
        public const string ScreenSpaceSubsurfaceShaderName = "Hidden/BurtRP/ScreenSpaceSubsurface";

        public static readonly int SourceTextureId = Shader.PropertyToID("_BurtSSSSourceTexture");
        public static readonly int OriginalTextureId = Shader.PropertyToID("_BurtSSSOriginalTexture");
        public static readonly int SetupTextureId = Shader.PropertyToID("_BurtSSSSetupTexture");
        public static readonly int TileTextureId = Shader.PropertyToID("_BurtSSSTileTexture");
        public static readonly int BlurTextureId = Shader.PropertyToID("_BurtSSSBlurTexture");
        public static readonly int CombineTextureId = Shader.PropertyToID("_BurtSSSCombineTexture");

        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int ScreenSizeId = Shader.PropertyToID("_BurtSSSScreenSize");
        private static readonly int TileSizeId = Shader.PropertyToID("_BurtSSSTileSize");
        private static readonly int DebugModeId = Shader.PropertyToID("_BurtSSSDebugMode");
        private static readonly int ParamsId = Shader.PropertyToID("_BurtSSSParams");
        private static readonly int Params2Id = Shader.PropertyToID("_BurtSSSParams2");
        private static readonly int SurfaceAlbedoId = Shader.PropertyToID("_BurtSSSSurfaceAlbedo");
        private static readonly int MeanFreePathId = Shader.PropertyToID("_BurtSSSMeanFreePath");
        private static readonly int ProfileTintId = Shader.PropertyToID("_BurtSSSProfileTint");
        private static readonly int BoundaryColorBleedId = Shader.PropertyToID("_BurtSSSBoundaryColorBleed");
        private static readonly int ProfileCountId = Shader.PropertyToID("_BurtSSSProfileCount");
        private static readonly int ProfileParamsId = Shader.PropertyToID("_BurtSSSProfileParams");
        private static readonly int ProfileParams2Id = Shader.PropertyToID("_BurtSSSProfileParams2");
        private static readonly int ProfileSurfaceAlbedosId = Shader.PropertyToID("_BurtSSSProfileSurfaceAlbedos");
        private static readonly int ProfileMeanFreePathsId = Shader.PropertyToID("_BurtSSSProfileMeanFreePaths");
        private static readonly int ProfileTintsId = Shader.PropertyToID("_BurtSSSProfileTints");
        private static readonly int ProfileBoundaryColorBleedsId = Shader.PropertyToID("_BurtSSSProfileBoundaryColorBleeds");
        private static readonly int ProfileDualSpecularsId = BurtSubsurfaceProfileShaderUtility.ProfileDualSpecularsId;
        private static readonly int ProfileTransmissionsId = Shader.PropertyToID("_BurtSSSProfileTransmissions");
        private static readonly int ProfileTransmissionTintsId = Shader.PropertyToID("_BurtSSSProfileTransmissionTints");
        private static readonly int ProfileParamLutId = BurtSubsurfaceProfileShaderUtility.ProfileParamLutId;
        private static readonly int ProfileParamLutEnabledId = BurtSubsurfaceProfileShaderUtility.ProfileParamLutEnabledId;
        private static readonly int ProfileParamLutSizeId = BurtSubsurfaceProfileShaderUtility.ProfileParamLutSizeId;
        private static readonly Vector4[] ProfileParams = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileParams2 = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileSurfaceAlbedos = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileMeanFreePaths = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileTints = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileBoundaryColorBleeds = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileDualSpeculars = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileTransmissions = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];
        private static readonly Vector4[] ProfileTransmissionTints = new Vector4[BurtSubsurfaceProfilePalette.MaxProfiles];

        private static int shaderAvailabilityFrame = -1;
        private static bool shaderAvailable;

        public static bool ShouldUseScreenSpaceSubsurface(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || request.Camera == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            return asset != null &&
                asset.RendererMode == BurtRendererMode.Deferred &&
                asset.EnableScreenSpaceSubsurface &&
                IsShaderAvailable();
        }

        public static bool ShouldUseScreenSpaceSubsurfaceDebugView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseScreenSpaceSubsurface(request, asset) && IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSetup ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceTileMask ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBlur ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceCombine ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceThickness ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileIndex ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceTransmission ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceDiffuse ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSpecular ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceStability;
        }

        public static int ResolveScreenSpaceSubsurfaceShaderDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSetup:
                    return 1;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceTileMask:
                    return 2;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlur:
                    return 3;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceCombine:
                    return 4;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceThickness:
                    return 5;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileIndex:
                    return 6;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceTransmission:
                    return 7;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceDiffuse:
                    return 8;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSpecular:
                    return 9;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceStability:
                    return 10;
                default:
                    return 0;
            }
        }

        public static bool IsShaderAvailable()
        {
            var frame = Time.frameCount;
            if (shaderAvailabilityFrame == frame)
            {
                return shaderAvailable;
            }

            shaderAvailabilityFrame = frame;
            shaderAvailable = Shader.Find(ScreenSpaceSubsurfaceShaderName) != null;
            return shaderAvailable;
        }

        public static Material GetMaterial(ref Material material, ref bool hasLoggedMissingShader)
        {
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(ScreenSpaceSubsurfaceShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ScreenSpaceSubsurfaceShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            material = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return material;
        }

        public static void BindCommonInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle source,
            BurtRenderTargetHandle cameraDepth,
            BurtRenderTargetHandle gbuffer0,
            BurtRenderTargetHandle gbuffer1,
            BurtRenderTargetHandle gbuffer2,
            BurtRenderTargetHandle gbuffer3,
            BurtRenderTargetHandle gbuffer4)
        {
            var descriptor = CreateDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);

            cmd.SetGlobalTexture(SourceTextureId, source.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepth.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4.Identifier);
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            var profileSettings = ResolveProfileSettings(context != null ? context.Asset : null);
            cmd.SetGlobalVector(ParamsId, profileSettings.Params);
            cmd.SetGlobalVector(Params2Id, profileSettings.Params2);
            cmd.SetGlobalVector(SurfaceAlbedoId, profileSettings.SurfaceAlbedoVector);
            cmd.SetGlobalVector(MeanFreePathId, profileSettings.MeanFreePathVector);
            cmd.SetGlobalVector(ProfileTintId, profileSettings.TintVector);
            cmd.SetGlobalVector(BoundaryColorBleedId, profileSettings.BoundaryColorBleedVector);
            BindProfilePalette(cmd, context != null ? context.Asset : null);
        }

        public static void BindGBufferInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle cameraDepth,
            BurtRenderTargetHandle gbuffer0,
            BurtRenderTargetHandle gbuffer1,
            BurtRenderTargetHandle gbuffer2,
            BurtRenderTargetHandle gbuffer3,
            BurtRenderTargetHandle gbuffer4)
        {
            var descriptor = CreateDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);

            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepth.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4.Identifier);
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            BindProfilePalette(cmd, context != null ? context.Asset : null);
        }

        public static void BindSetupTileInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle setup,
            BurtRenderTargetHandle tile)
        {
            var descriptor = CreateDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var tileDescriptor = CreateTileDescriptor(context);
            var tileWidth = Mathf.Max(1, tileDescriptor.width);
            var tileHeight = Mathf.Max(1, tileDescriptor.height);
            cmd.SetGlobalTexture(SetupTextureId, setup.Identifier);
            cmd.SetGlobalTexture(TileTextureId, tile.Identifier);
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(TileSizeId, new Vector4(tileWidth, tileHeight, 1f / tileWidth, 1f / tileHeight));
        }

        public static void BindDebugInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle setup,
            BurtRenderTargetHandle tile,
            BurtRenderTargetHandle blur,
            BurtRenderTargetHandle combine,
            BurtRenderTargetHandle original,
            BurtRenderTargetHandle cameraDepth,
            BurtRenderTargetHandle gbuffer0,
            BurtRenderTargetHandle gbuffer1,
            BurtRenderTargetHandle gbuffer2,
            BurtRenderTargetHandle gbuffer3,
            BurtRenderTargetHandle gbuffer4)
        {
            BindSetupTileInputs(cmd, context, setup, tile);
            BindGBufferInputs(cmd, context, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            cmd.SetGlobalTexture(OriginalTextureId, original.Identifier);
            cmd.SetGlobalTexture(BlurTextureId, blur.Identifier);
            cmd.SetGlobalTexture(CombineTextureId, combine.Identifier);
            cmd.SetGlobalFloat(DebugModeId, ResolveScreenSpaceSubsurfaceShaderDebugMode());
        }

        public static void SetViewport(CommandBuffer cmd, BurtRenderGraphContext context)
        {
            var descriptor = CreateDescriptor(context);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
        }

        public static void SetTileViewport(CommandBuffer cmd, BurtRenderGraphContext context)
        {
            var descriptor = CreateTileDescriptor(context);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
        }

        public static RenderTextureDescriptor CreateDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceColorDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateSetupDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceSetupDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateTileDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceTileDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateCombineDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceCombineDescriptor(camera);
        }

        private static BurtSubsurfaceProfileSettings ResolveProfileSettings(BurtRenderPipelineAsset asset)
        {
            return asset != null
                ? asset.ScreenSpaceSubsurfaceProfileSettings
                : BurtSubsurfaceProfileSettings.Default;
        }

        private static void BindProfilePalette(CommandBuffer cmd, BurtRenderPipelineAsset asset)
        {
            BurtSubsurfaceLutUtility.BeginPaletteBinding();
            var palette = asset != null
                ? asset.ScreenSpaceSubsurfaceProfilePalette
                : BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null);

            var count = Mathf.Clamp(palette.Count, 1, BurtSubsurfaceProfilePalette.MaxProfiles);
            var fallback = palette.GetSettings(0);
            for (var i = 0; i < BurtSubsurfaceProfilePalette.MaxProfiles; i++)
            {
                var profile = i < count ? palette.GetSettings(i) : fallback;
                ProfileParams[i] = profile.Params;
                ProfileParams2[i] = profile.Params2;
                ProfileSurfaceAlbedos[i] = profile.SurfaceAlbedoVector;
                ProfileMeanFreePaths[i] = profile.MeanFreePathVector;
                ProfileTints[i] = profile.TintVector;
                ProfileBoundaryColorBleeds[i] = profile.BoundaryColorBleedVector;
                ProfileDualSpeculars[i] = profile.DualSpecularVector;
                ProfileTransmissions[i] = profile.TransmissionVector;
                ProfileTransmissionTints[i] = profile.TransmissionTintVector;
            }

            cmd.SetGlobalFloat(ProfileCountId, count);
            cmd.SetGlobalFloat(BurtSubsurfaceProfileShaderUtility.ProfileCountId, count);
            cmd.SetGlobalVectorArray(ProfileParamsId, ProfileParams);
            cmd.SetGlobalVectorArray(ProfileParams2Id, ProfileParams2);
            cmd.SetGlobalVectorArray(ProfileSurfaceAlbedosId, ProfileSurfaceAlbedos);
            cmd.SetGlobalVectorArray(ProfileMeanFreePathsId, ProfileMeanFreePaths);
            cmd.SetGlobalVectorArray(ProfileTintsId, ProfileTints);
            cmd.SetGlobalVectorArray(ProfileBoundaryColorBleedsId, ProfileBoundaryColorBleeds);
            cmd.SetGlobalVectorArray(ProfileDualSpecularsId, ProfileDualSpeculars);
            cmd.SetGlobalVectorArray(ProfileTransmissionsId, ProfileTransmissions);
            cmd.SetGlobalVectorArray(ProfileTransmissionTintsId, ProfileTransmissionTints);

            var profileParamLut = BurtSubsurfaceLutUtility.GetOrCreateProfileParamLut(palette);
            cmd.SetGlobalTexture(ProfileParamLutId, profileParamLut);
            cmd.SetGlobalFloat(ProfileParamLutEnabledId, profileParamLut != null ? 1f : 0f);
            cmd.SetGlobalVector(ProfileParamLutSizeId, BurtSubsurfaceLutUtility.ProfileParamLutSizeVector);
        }
    }

    internal static class BurtScreenSpaceSubsurfaceRenderTargetUtility
    {
        public static void Allocate(BurtRenderGraphContext context, string passName, int textureId, BurtRenderTargetHandle target)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context.Request, context.Asset) || !target.IsValid)
            {
                return;
            }

            var descriptor = BurtScreenSpaceSubsurfacePassUtility.CreateDescriptor(context);
            Allocate(context, passName, textureId, target, descriptor, FilterMode.Bilinear);
        }

        public static void Allocate(BurtRenderGraphContext context, string passName, int textureId, BurtRenderTargetHandle target, RenderTextureDescriptor descriptor, FilterMode filterMode)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context.Request, context.Asset) || !target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(passName);
            cmd.GetTemporaryRT(textureId, descriptor, filterMode);
            cmd.SetGlobalTexture(textureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public static void Release(BurtRenderGraphContext context, string passName, int textureId)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context.Request, context.Asset))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(passName);
            cmd.ReleaseTemporaryRT(textureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
