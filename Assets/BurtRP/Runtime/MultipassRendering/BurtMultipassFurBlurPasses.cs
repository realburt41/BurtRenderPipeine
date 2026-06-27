using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateFurBlurPropertyPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Fur Blur Property";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Allocate;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteFurBlurProperty();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(context.Request != null ? context.Request.Camera : null);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, descriptor, FilterMode.Point);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtAllocateFurBlurPropertyTempPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Fur Blur Property Temp";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Allocate;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteFurBlurPropertyTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurPropertyTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(context.Request != null ? context.Request.Camera : null);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurPropertyTempTextureId, descriptor, FilterMode.Point);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTempTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtAllocateFurBlurColorPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Fur Blur Color";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Allocate;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteFurBlurColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurColorName);
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurColorDescriptor(context.Request != null ? context.Request.Camera : null);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtAllocateFurBlurTemporalPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Fur Blur Temporal";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Allocate;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteFurBlurTemporal();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(context.Request, context.Asset))
            {
                return;
            }

            var target = context != null ? context.FurBlurTemporalTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurTemporalName);
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurTemporalDescriptor(context.Request != null ? context.Request.Camera : null);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurTemporalTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurTemporalTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtAllocateFurBlurVelocityPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Fur Blur Velocity";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Allocate;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteFurBlurVelocity();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurVelocityTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
            if (!target.IsValid)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurVelocityDescriptor(context.Request != null ? context.Request.Camera : null);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurVelocityTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurVelocityTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtClearFurBlurPropertyPass : BurtRenderPass
    {
        private const int SetupDepthPassIndex = 8;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Clear Fur Blur Property";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Clear;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.WriteFurBlurProperty();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var propertyTarget = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            var cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            if (context == null || !propertyTarget.IsValid || !cameraDepthTarget.IsValid)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(camera);
            var drawMaterial = GetMaterial();
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(propertyTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            if (drawMaterial != null)
            {
                cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
                cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, SetupDepthPassIndex, MeshTopology.Triangles, 3, 1);
            }
            else
            {
                cmd.ClearRenderTarget(false, true, BurtFurBlurPassUtility.FurBlurPropertyClearColor);
            }

            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtClearFurBlurVelocityPass : BurtRenderPass
    {
        public override string Name => "Burt Clear Fur Blur Velocity";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Clear;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteFurBlurVelocity();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var velocityTarget = context != null ? context.FurBlurVelocityTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
            if (!velocityTarget.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(velocityTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.ClearRenderTarget(false, true, Color.clear);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtDrawMultipassFurBlurPropertyPass : BurtRenderPass
    {
        public override string Name => "Burt Draw Multipass Fur Blur Property";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.DrawRenderers;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.WriteFurBlurProperty();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var propertyTarget = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            var cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            if (!propertyTarget.IsValid || !cameraDepthTarget.IsValid || context == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.BeginSample(Name);
            cmd.SetRenderTarget(propertyTarget.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            BurtMultipassRenderer.DrawAll(cmd, context, BurtMultipassShaderPass.FurBlurProperty, RenderQueueRange.opaque);
            cmd.EndSample(Name);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtDrawMultipassFurBlurVelocityPass : BurtRenderPass
    {
        public override string Name => "Burt Draw Multipass Fur Blur Velocity";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.DrawRenderers;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.WriteFurBlurVelocity();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var velocityTarget = context != null ? context.FurBlurVelocityTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
            var cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            if (!velocityTarget.IsValid || !cameraDepthTarget.IsValid || context == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurVelocityDescriptor(camera);
            var cmd = CommandBufferPool.Get(Name);
            cmd.BeginSample(Name);
            cmd.SetRenderTarget(velocityTarget.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            BurtFurBlurPassUtility.UploadMotionVectorGlobals(cmd, context.Request, descriptor.width, descriptor.height);
            BurtMultipassRenderer.DrawAll(cmd, context, BurtMultipassShaderPass.FurBlurVelocity, RenderQueueRange.opaque);
            cmd.EndSample(Name);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtFurBlurDilatePass : BurtRenderPass
    {
        private const int DilatePassIndex = 2;
        private readonly bool writeTemp;
        private Material material;
        private bool hasLoggedMissingShader;

        public BurtFurBlurDilatePass(bool writeTemp)
        {
            this.writeTemp = writeTemp;
        }

        public override string Name => writeTemp ? "Burt Fur Blur Property Dilate A" : "Burt Fur Blur Property Dilate B";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            if (writeTemp)
            {
                builder.ReadFurBlurProperty();
                builder.WriteFurBlurPropertyTemp();
            }
            else
            {
                builder.ReadFurBlurPropertyTemp();
                builder.WriteFurBlurProperty();
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var source = writeTemp
                ? (context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName))
                : (context != null ? context.FurBlurPropertyTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName));
            var target = writeTemp
                ? (context != null ? context.FurBlurPropertyTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName))
                : (context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName));
            if (context == null || !source.IsValid || !target.IsValid)
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(context.Request != null ? context.Request.Camera : null);
            var settings = BurtFurBlurPassUtility.ResolveSettings();
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(target.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, source.Identifier);
            BurtFurBlurPassUtility.UploadCommonGlobals(cmd, context.Request, descriptor.width, descriptor.height, settings, false, false, 0);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, DilatePassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtFurBlurInitTileArgsPass : BurtRenderPass
    {
        private ComputeShader computeShader;
        private bool hasLoggedMissingComputeShader;
        private bool hasLoggedMissingKernel;

        public override string Name => "Burt Fur Blur Init Tile Args";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseTiledFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFurBlurPassUtility.ShouldUseTiledFurBlur(context.Request, context.Asset))
            {
                return;
            }

            var args = context.FurBlurArgsBuffer;
            if (!args.IsValid || !args.HasBuffer)
            {
                return;
            }

            var shader = GetComputeShader();
            if (shader == null || !BurtFurBlurPassUtility.TryFindTiledComputeKernel(shader, "InitArgsCS", ref hasLoggedMissingKernel, out var kernel))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetComputeBufferParam(shader, kernel, BurtFurBlurPassUtility.TileArgsBufferId, args.Buffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private ComputeShader GetComputeShader()
        {
            return BurtFurBlurPassUtility.GetTiledComputeShader(ref computeShader, ref hasLoggedMissingComputeShader);
        }
    }

    internal sealed class BurtFurBlurTiledSetupPass : BurtRenderPass
    {
        private ComputeShader computeShader;
        private bool hasLoggedMissingComputeShader;
        private bool hasLoggedMissingKernel;

        public override string Name => "Burt Fur Blur Tiled Setup";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseTiledFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurProperty();
            builder.WriteFurBlurPropertyTemp();
            builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
            builder.WriteBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
            builder.WriteBuffer(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var property, out var propertyTemp, out var args, out var tiles))
            {
                return;
            }

            var shader = GetComputeShader();
            if (shader == null || !BurtFurBlurPassUtility.TryFindTiledComputeKernel(shader, "SetupCS", ref hasLoggedMissingKernel, out var kernel))
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(camera);
            var groupsX = Mathf.CeilToInt(Mathf.Max(1, descriptor.width) / (float)BurtFurBlurPassUtility.TileThreadSize);
            var groupsY = Mathf.CeilToInt(Mathf.Max(1, descriptor.height) / (float)BurtFurBlurPassUtility.TileThreadSize);
            var settings = BurtFurBlurPassUtility.ResolveSettings();

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetComputeTextureParam(shader, kernel, BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, property.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtFurBlurPassUtility.PropertyTempRWTextureId, propertyTemp.Identifier);
            cmd.SetComputeBufferParam(shader, kernel, BurtFurBlurPassUtility.TileArgsBufferId, args.Buffer);
            cmd.SetComputeBufferParam(shader, kernel, BurtFurBlurPassUtility.TileDataBufferId, tiles.Buffer);
            cmd.SetComputeVectorParam(shader, BurtFurBlurPassUtility.ScreenSizeId, BurtFurBlurPassUtility.CreateScreenSizeVector(descriptor.width, descriptor.height));
            cmd.SetComputeVectorParam(shader, BurtFurBlurPassUtility.ParamsId, new Vector4(settings.RadiusCm, settings.DepthThresholdEye, settings.DirectionDilationThreshold, settings.ThetaFeedback));
            cmd.DispatchCompute(shader, kernel, groupsX, groupsY, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle property,
            out BurtRenderTargetHandle propertyTemp,
            out BurtRenderBufferHandle args,
            out BurtRenderBufferHandle tiles)
        {
            property = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            propertyTemp = context != null ? context.FurBlurPropertyTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
            args = context != null ? context.FurBlurArgsBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
            tiles = context != null ? context.FurBlurTileDataBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName);
            return BurtFurBlurPassUtility.ShouldUseTiledFurBlur(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                property.IsValid &&
                propertyTemp.IsValid &&
                args.IsValid &&
                args.HasBuffer &&
                tiles.IsValid &&
                tiles.HasBuffer;
        }

        private ComputeShader GetComputeShader()
        {
            return BurtFurBlurPassUtility.GetTiledComputeShader(ref computeShader, ref hasLoggedMissingComputeShader);
        }
    }

    internal sealed class BurtFurBlurFillTileArgsPass : BurtRenderPass
    {
        private ComputeShader computeShader;
        private bool hasLoggedMissingComputeShader;
        private bool hasLoggedMissingKernel;

        public override string Name => "Burt Fur Blur Fill Tile Args";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseTiledFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
            builder.WriteBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFurBlurPassUtility.ShouldUseTiledFurBlur(context.Request, context.Asset))
            {
                return;
            }

            var args = context.FurBlurArgsBuffer;
            if (!args.IsValid || !args.HasBuffer)
            {
                return;
            }

            var shader = GetComputeShader();
            if (shader == null || !BurtFurBlurPassUtility.TryFindTiledComputeKernel(shader, "FillArgsPS", ref hasLoggedMissingKernel, out var kernel))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetComputeBufferParam(shader, kernel, BurtFurBlurPassUtility.TileArgsBufferId, args.Buffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private ComputeShader GetComputeShader()
        {
            return BurtFurBlurPassUtility.GetTiledComputeShader(ref computeShader, ref hasLoggedMissingComputeShader);
        }
    }

    internal sealed class BurtFurBlurTiledDilatePass : BurtRenderPass
    {
        private ComputeShader computeShader;
        private bool hasLoggedMissingComputeShader;
        private bool hasLoggedMissingKernel;

        public override string Name => "Burt Fur Blur Tiled Dilate";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseTiledFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurPropertyTemp();
            builder.WriteFurBlurProperty();
            builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
            builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var property, out var propertyTemp, out var args, out var tiles))
            {
                return;
            }

            var shader = GetComputeShader();
            if (shader == null || !BurtFurBlurPassUtility.TryFindTiledComputeKernel(shader, "DilateCS", ref hasLoggedMissingKernel, out var kernel))
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(camera);
            var settings = BurtFurBlurPassUtility.ResolveSettings();

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetComputeTextureParam(shader, kernel, BurtRenderGraphResourceRegistry.FurBlurPropertyTempTextureId, propertyTemp.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtFurBlurPassUtility.PropertyRWTextureId, property.Identifier);
            cmd.SetComputeBufferParam(shader, kernel, BurtFurBlurPassUtility.TileArgsBufferId, args.Buffer);
            cmd.SetComputeBufferParam(shader, kernel, BurtFurBlurPassUtility.TileDataBufferId, tiles.Buffer);
            cmd.SetComputeVectorParam(shader, BurtFurBlurPassUtility.ScreenSizeId, BurtFurBlurPassUtility.CreateScreenSizeVector(descriptor.width, descriptor.height));
            cmd.SetComputeVectorParam(shader, BurtFurBlurPassUtility.ParamsId, new Vector4(settings.RadiusCm, settings.DepthThresholdEye, settings.DirectionDilationThreshold, settings.ThetaFeedback));
            cmd.DispatchCompute(shader, kernel, args.Buffer, 0);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle property,
            out BurtRenderTargetHandle propertyTemp,
            out BurtRenderBufferHandle args,
            out BurtRenderBufferHandle tiles)
        {
            property = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            propertyTemp = context != null ? context.FurBlurPropertyTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
            args = context != null ? context.FurBlurArgsBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
            tiles = context != null ? context.FurBlurTileDataBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName);
            return BurtFurBlurPassUtility.ShouldUseTiledFurBlur(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                property.IsValid &&
                propertyTemp.IsValid &&
                args.IsValid &&
                args.HasBuffer &&
                tiles.IsValid &&
                tiles.HasBuffer;
        }

        private ComputeShader GetComputeShader()
        {
            return BurtFurBlurPassUtility.GetTiledComputeShader(ref computeShader, ref hasLoggedMissingComputeShader);
        }
    }

    internal sealed class BurtFurBlurThetaTemporalPass : BurtRenderPass
    {
        private const int ThetaTemporalPassIndex = 5;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Fur Blur Theta Temporal";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurProperty();
            builder.ReadFurBlurVelocity();
            builder.WriteFurBlurPropertyTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(context.Request, context.Asset))
            {
                return;
            }

            var propertyTarget = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            var propertyTempTarget = context != null ? context.FurBlurPropertyTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
            if (context == null || !propertyTarget.IsValid || !propertyTempTarget.IsValid)
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(camera);
            var history = BurtFurBlurHistoryUtility.EnsureHistoryTextures(context.Request, out var historyValid);
            var settings = BurtFurBlurPassUtility.ResolveSettings();
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(propertyTempTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            BurtFurBlurPassUtility.BindVelocityTexture(cmd, context);
            cmd.SetGlobalTexture(BurtFurBlurPassUtility.PropertyHistoryTextureId, history.Property != null ? new RenderTargetIdentifier(history.Property) : propertyTarget.Identifier);
            BurtFurBlurPassUtility.UploadCommonGlobals(cmd, context.Request, descriptor.width, descriptor.height, settings, historyValid, historyValid, history.HistoryAge);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, ThetaTemporalPassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtFurBlurPass : BurtRenderPass
    {
        private const int BlurPassIndex = 0;
        private const int TiledBlurPassIndex = 6;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Fur Blur";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraColor();
            if (BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurPropertyTemp();
            }
            else
            {
                builder.ReadFurBlurProperty();
            }

            builder.WriteFurBlurColor();
            if (BurtFurBlurPassUtility.ShouldUseTiledBlurDraw(builder.Request, builder.Asset))
            {
                builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
                builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName);
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out var cameraColorTarget, out _, out _, out var colorTarget, out _))
            {
                return;
            }

            var propertyTarget = BurtFurBlurPassUtility.ResolveActiveFurBlurPropertyTarget(context);
            if (!propertyTarget.IsValid)
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurColorDescriptor(context.Request != null ? context.Request.Camera : null);
            var settings = BurtFurBlurPassUtility.ResolveSettings();
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(colorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            if (BurtFurBlurPassUtility.ShouldUseTiledBlurDraw(context.Request, context.Asset))
            {
                cmd.ClearRenderTarget(false, true, Color.clear);
            }

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            BurtFurBlurPassUtility.UploadCommonGlobals(cmd, context.Request, descriptor.width, descriptor.height, settings, false, false, 0);
            if (BurtFurBlurPassUtility.ShouldUseTiledBlurDraw(context.Request, context.Asset) &&
                BurtFurBlurPassUtility.TryBindTileBuffers(cmd, context))
            {
                cmd.DrawProceduralIndirect(Matrix4x4.identity, drawMaterial, TiledBlurPassIndex, MeshTopology.Triangles, context.FurBlurArgsBuffer.Buffer, 3 * sizeof(uint));
            }
            else
            {
                cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, BlurPassIndex, MeshTopology.Triangles, 3, 1);
            }
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtFurBlurTemporalPass : BurtRenderPass
    {
        private const int TemporalPassIndex = 3;
        private const int TiledTemporalPassIndex = 7;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Fur Blur Temporal";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurColor();
            if (BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurPropertyTemp();
            }
            else
            {
                builder.ReadFurBlurProperty();
            }

            builder.ReadFurBlurVelocity();
            builder.WriteFurBlurTemporal();
            if (BurtFurBlurPassUtility.ShouldUseTiledBlurDraw(builder.Request, builder.Asset))
            {
                builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurArgsBufferName);
                builder.ReadBuffer(BurtRenderGraphResourceRegistry.FurBlurTileDataBufferName);
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(context.Request, context.Asset))
            {
                return;
            }

            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out _, out _, out _, out var colorTarget, out var temporalTarget))
            {
                return;
            }

            var propertyTarget = BurtFurBlurPassUtility.ResolveActiveFurBlurPropertyTarget(context);
            if (!propertyTarget.IsValid)
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurTemporalDescriptor(camera);
            BurtFurBlurHistoryTextures history;
            BurtFurBlurHistoryStatus status;
            if (BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(context.Request, context.Asset))
            {
                history = BurtFurBlurHistoryUtility.GetPendingHistoryTextures(context.Request);
                status = BurtFurBlurHistoryUtility.GetHistoryStatus(camera);
            }
            else
            {
                history = BurtFurBlurHistoryUtility.EnsureHistoryTextures(context.Request, out _);
                status = BurtFurBlurHistoryUtility.GetHistoryStatus(camera);
            }

            var colorHistoryValid = status.HasHistory && history.Color != null;
            var propertyHistoryValid = status.HasPropertyHistory && history.Property != null;
            var settings = BurtFurBlurPassUtility.ResolveSettings();
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(temporalTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            if (BurtFurBlurPassUtility.ShouldUseTiledBlurDraw(context.Request, context.Asset))
            {
                cmd.ClearRenderTarget(false, true, Color.clear);
            }

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, colorTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            BurtFurBlurPassUtility.BindVelocityTexture(cmd, context);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurHistoryTextureId, history.Color != null ? new RenderTargetIdentifier(history.Color) : colorTarget.Identifier);
            cmd.SetGlobalTexture(BurtFurBlurPassUtility.PropertyHistoryTextureId, history.Property != null ? new RenderTargetIdentifier(history.Property) : propertyTarget.Identifier);
            BurtFurBlurPassUtility.UploadCommonGlobals(cmd, context.Request, descriptor.width, descriptor.height, settings, colorHistoryValid, propertyHistoryValid, history.HistoryAge);
            if (BurtFurBlurPassUtility.ShouldUseTiledBlurDraw(context.Request, context.Asset) &&
                BurtFurBlurPassUtility.TryBindTileBuffers(cmd, context))
            {
                cmd.DrawProceduralIndirect(Matrix4x4.identity, drawMaterial, TiledTemporalPassIndex, MeshTopology.Triangles, context.FurBlurArgsBuffer.Buffer, 3 * sizeof(uint));
            }
            else
            {
                cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, TemporalPassIndex, MeshTopology.Triangles, 3, 1);
            }
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtFurBlurStoreHistoryPass : BurtRenderPass
    {
        public override string Name => "Burt Fur Blur Store History";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Copy;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurAnyTemporal(builder.Request, builder.Asset))
            {
                return;
            }

            if (BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurTemporal();
            }
            else
            {
                builder.ReadFurBlurColor();
            }

            if (BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurPropertyTemp();
            }
            else
            {
                builder.ReadFurBlurProperty();
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFurBlurPassUtility.ShouldUseFurBlurAnyTemporal(context.Request, context.Asset))
            {
                return;
            }

            var colorSourceTarget = BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(context.Request, context.Asset)
                ? context.FurBlurTemporalTarget
                : context.FurBlurColorTarget;
            var propertyTarget = BurtFurBlurPassUtility.ResolveActiveFurBlurPropertyTarget(context);
            if (!colorSourceTarget.IsValid || !propertyTarget.IsValid)
            {
                return;
            }

            var history = BurtFurBlurHistoryUtility.GetPendingHistoryTextures(context.Request);
            if (history.Color == null || history.Property == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.CopyTexture(colorSourceTarget.Identifier, new RenderTargetIdentifier(history.Color));
            cmd.CopyTexture(propertyTarget.Identifier, new RenderTargetIdentifier(history.Property));
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            BurtFurBlurHistoryUtility.MarkHistoryValid(context.Request != null ? context.Request.Camera : null);
        }
    }

    internal sealed class BurtFurBlurCompositePass : BurtRenderPass
    {
        private const int CompositePassIndex = 1;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Fur Blur Composite";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Copy;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            if (BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurTemporal();
            }
            else
            {
                builder.ReadFurBlurColor();
            }

            if (BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurPropertyTemp();
            }
            else
            {
                builder.ReadFurBlurProperty();
            }

            builder.ReadCameraColor();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out _, out var cameraColorTarget, out _, out var colorTarget, out var temporalTarget))
            {
                return;
            }

            var propertyTarget = BurtFurBlurPassUtility.ResolveActiveFurBlurPropertyTarget(context);
            if (!propertyTarget.IsValid)
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(context.Request != null ? context.Request.Camera : null);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            var compositeSource = BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(context.Request, context.Asset) ? temporalTarget : colorTarget;
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurTemporalTextureId, compositeSource.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, CompositePassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtFurBlurDebugPass : BurtRenderPass
    {
        private const int DebugPassIndex = 4;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Fur Blur Debug";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurDebugView(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraColor();
            if (BurtFurBlurPassUtility.ShouldUseFurBlurThetaTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurPropertyTemp();
            }
            else
            {
                builder.ReadFurBlurProperty();
            }

            builder.ReadFurBlurColor();
            if (BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(builder.Request, builder.Asset))
            {
                builder.ReadFurBlurTemporal();
            }

            builder.ReadFurBlurVelocity();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurDebugView(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out _, out var cameraColorTarget, out _, out var colorTarget, out var temporalTarget))
            {
                return;
            }

            var propertyTarget = BurtFurBlurPassUtility.ResolveActiveFurBlurPropertyTarget(context);
            if (!propertyTarget.IsValid)
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var status = BurtFurBlurHistoryUtility.GetHistoryStatus(camera);
            var history = BurtFurBlurHistoryUtility.GetPendingHistoryTextures(context.Request);
            var settings = BurtFurBlurPassUtility.ResolveSettings();
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, colorTarget.Identifier);
            var temporalSource = BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(context.Request, context.Asset) ? temporalTarget : colorTarget;
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurTemporalTextureId, temporalSource.Identifier);
            BurtFurBlurPassUtility.BindVelocityTexture(cmd, context);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurHistoryTextureId, history.Color != null ? new RenderTargetIdentifier(history.Color) : temporalSource.Identifier);
            cmd.SetGlobalTexture(BurtFurBlurPassUtility.PropertyHistoryTextureId, history.Property != null ? new RenderTargetIdentifier(history.Property) : propertyTarget.Identifier);
            BurtFurBlurPassUtility.UploadCommonGlobals(cmd, context.Request, descriptor.width, descriptor.height, settings, status.HasHistory, status.HasPropertyHistory, status.HistoryAge);
            cmd.SetGlobalInt(BurtFurBlurPassUtility.DebugModeId, BurtFurBlurPassUtility.ResolveFurBlurShaderDebugMode());
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, DebugPassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtReleaseFurBlurTemporalPass : BurtRenderPass
    {
        public override string Name => "Burt Release Fur Blur Temporal";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Release;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurTemporal();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtFurBlurPassUtility.ShouldUseFurBlurColorTemporal(context.Request, context.Asset))
            {
                return;
            }

            var target = context != null ? context.FurBlurTemporalTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurTemporalName);
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurTemporalTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtReleaseFurBlurVelocityPass : BurtRenderPass
    {
        public override string Name => "Burt Release Fur Blur Velocity";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Release;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurVelocity();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurVelocityTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurVelocityTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtReleaseFurBlurColorPass : BurtRenderPass
    {
        public override string Name => "Burt Release Fur Blur Color";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Release;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurColorName);
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurColorTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtReleaseFurBlurPropertyTempPass : BurtRenderPass
    {
        public override string Name => "Burt Release Fur Blur Property Temp";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Release;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurPropertyTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurPropertyTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyTempName);
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurPropertyTempTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtReleaseFurBlurPropertyPass : BurtRenderPass
    {
        public override string Name => "Burt Release Fur Blur Property";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Release;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurProperty();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal readonly struct BurtFurBlurSettings
    {
        public const float DefaultRadiusCm = 2.0f;
        public const float DefaultDepthThresholdEye = 0.001f;
        public const float DefaultDirectionDilationThreshold = 0.5f;
        public const float DefaultThetaFeedback = 0.96f;
        public const float DefaultTemporalFeedback = 0.96f;
        public const bool DefaultTiledBlur = false;
        public const bool DefaultThetaTemporal = false;
        public const bool DefaultColorTemporal = false;

        public static readonly BurtFurBlurSettings Default = new BurtFurBlurSettings(true, DefaultTiledBlur, DefaultRadiusCm, DefaultDepthThresholdEye, DefaultDirectionDilationThreshold, DefaultThetaTemporal, DefaultColorTemporal, DefaultThetaFeedback, DefaultTemporalFeedback);

        public bool Enabled { get; }
        public bool TiledBlur { get; }
        public float RadiusCm { get; }
        public float DepthThresholdEye { get; }
        public float DirectionDilationThreshold { get; }
        public bool ThetaTemporal { get; }
        public bool ColorTemporal { get; }
        public float ThetaFeedback { get; }
        public float TemporalFeedback { get; }

        public BurtFurBlurSettings(bool enabled, bool tiledBlur, float radiusCm, float depthThresholdEye, float directionDilationThreshold, bool thetaTemporal, bool colorTemporal, float thetaFeedback, float temporalFeedback)
        {
            Enabled = enabled;
            TiledBlur = tiledBlur;
            RadiusCm = Mathf.Clamp(radiusCm, 0f, 8f);
            DepthThresholdEye = Mathf.Clamp(depthThresholdEye, 0.0001f, 0.2f);
            DirectionDilationThreshold = Mathf.Clamp01(directionDilationThreshold);
            ThetaTemporal = thetaTemporal;
            ColorTemporal = colorTemporal;
            ThetaFeedback = Mathf.Clamp(thetaFeedback, 0f, 0.98f);
            TemporalFeedback = Mathf.Clamp(temporalFeedback, 0f, 0.98f);
        }
    }

    internal static class BurtFurBlurPassUtility
    {
        public const string ShaderName = "Hidden/BurtRP/FurBlur";
        public const string TiledComputeShaderResourcePath = "BurtFurBlurTiled";
        public const float DefaultTemporalFeedback = BurtFurBlurSettings.DefaultTemporalFeedback;
        public const int TileThreadSize = 8;

        private const int OpaqueRenderQueueMax = 2500;
        private const string FurBlurPropertyShaderPassName = "Burt Multipass Fur Blur Property";
        private const string FurBlurEnabledPropertyName = "_FurBlurEnabled";
        private const float CameraCutPositionThreshold = 0.5f;
        private const float CameraCutRotationThreshold = 5f;

        public static readonly int ScreenSizeId = Shader.PropertyToID("_BurtFurBlurScreenSize");
        public static readonly int HistoryParamsId = Shader.PropertyToID("_BurtFurBlurHistoryParams");
        public static readonly int ParamsId = Shader.PropertyToID("_BurtFurBlurParams");
        public static readonly int TemporalParamsId = Shader.PropertyToID("_BurtFurBlurTemporalParams");
        public static readonly int PropertyHistoryTextureId = Shader.PropertyToID("_BurtFurBlurPropertyHistoryTexture");
        public static readonly int PreviousNonJitteredViewProjectionId = Shader.PropertyToID("_BurtFurBlurPreviousNonJitteredViewProjection");
        public static readonly int CurrentNonJitteredViewProjectionId = Shader.PropertyToID("_BurtFurBlurCurrentNonJitteredViewProjection");
        public static readonly int InverseCurrentNonJitteredViewProjectionId = Shader.PropertyToID("_BurtFurBlurInverseCurrentNonJitteredViewProjection");
        public static readonly int JitterId = Shader.PropertyToID("_BurtFurBlurJitter");
        public static readonly int DebugModeId = Shader.PropertyToID("_BurtFurBlurDebugMode");
        public static readonly int PreviousObjectToWorldId = Shader.PropertyToID("_BurtFurBlurPreviousObjectToWorld");
        public static readonly int PropertyRWTextureId = Shader.PropertyToID("_BurtFurBlurPropertyRWTexture");
        public static readonly int PropertyTempRWTextureId = Shader.PropertyToID("_BurtFurBlurPropertyTempRWTexture");
        public static readonly int TileArgsBufferId = Shader.PropertyToID("_BurtFurBlurArgsBuffer");
        public static readonly int TileDataBufferId = Shader.PropertyToID("_BurtFurBlurTileDataBuffer");

        private static readonly Color ReversedZFarClearColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color ForwardZFarClearColor = new Color(0f, 1f, 0f, 1f);
        private static int tiledComputeAvailabilityFrame = -1;
        private static bool tiledComputeAvailable;
        private static int cachedFurBlurFeatureFrame = -1;
        private static int cachedFurBlurFeatureCameraId;
        private static int cachedFurBlurFeatureCullingMask;
        private static bool cachedHasVisibleFurBlur;
        private static string cachedCandidateGateLabel = "Unknown";

        public static Color FurBlurPropertyClearColor
        {
            get
            {
                return SystemInfo.usesReversedZBuffer ? ReversedZFarClearColor : ForwardZFarClearColor;
            }
        }

        public static bool ShouldUseFurBlur(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ShouldUseFurBlurBase(request, asset))
            {
                return false;
            }

            return HasVisibleFurBlurCandidate(request);
        }

        public static bool ShouldUseTiledFurBlur(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFurBlur(request, asset) && SupportsTiledPropertyFormat() && IsTiledComputeAvailable();
        }

        public static bool ShouldUseTiledBlurDraw(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ResolveSettings().TiledBlur && ShouldUseTiledFurBlur(request, asset);
        }

        public static bool ShouldUseFurBlurThetaTemporal(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFurBlur(request, asset) && ResolveSettings().ThetaTemporal;
        }

        public static bool ShouldUseFurBlurColorTemporal(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFurBlur(request, asset) && ResolveSettings().ColorTemporal;
        }

        public static bool ShouldUseFurBlurAnyTemporal(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFurBlur(request, asset) && (ResolveSettings().ThetaTemporal || ResolveSettings().ColorTemporal);
        }

        public static bool ShouldUseFurBlurDebugView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseFurBlur(request, asset) && IsFurBlurDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool IsFurBlurDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.FurBlurDirection ||
                mode == BurtShadingDebugMode.FurBlurPropertyDepth ||
                mode == BurtShadingDebugMode.FurBlurCurrent ||
                mode == BurtShadingDebugMode.FurBlurTemporal ||
                mode == BurtShadingDebugMode.FurBlurHistory ||
                mode == BurtShadingDebugMode.FurBlurDiagnostic ||
                mode == BurtShadingDebugMode.FurBlurReprojection;
        }

        public static int ResolveFurBlurShaderDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.FurBlurDirection:
                    return 1;
                case BurtShadingDebugMode.FurBlurPropertyDepth:
                    return 2;
                case BurtShadingDebugMode.FurBlurCurrent:
                    return 3;
                case BurtShadingDebugMode.FurBlurTemporal:
                    return 4;
                case BurtShadingDebugMode.FurBlurHistory:
                    return 5;
                case BurtShadingDebugMode.FurBlurDiagnostic:
                    return 6;
                case BurtShadingDebugMode.FurBlurReprojection:
                    return 7;
                default:
                    return 0;
            }
        }

        public static string ResolveFurBlurDebugModeLabel()
        {
            return IsFurBlurDebugMode(BurtShadingDebugSettings.Mode) ? BurtShadingDebugSettings.Mode.ToString() : "Disabled";
        }

        public static string ResolveFurBlurShaderStatusLabel()
        {
            return Shader.Find(ShaderName) != null ? "Ready" : "Missing(" + ShaderName + ")";
        }

        public static string ResolveFurBlurTiledStatusLabel()
        {
            if (!SupportsTiledPropertyFormat())
            {
                return "FullscreenDilateFallbackMissingRGFloatUAV";
            }

            if (!IsTiledComputeAvailable())
            {
                return "FullscreenDilateFallbackMissing(" + TiledComputeShaderResourcePath + ")";
            }

            return ResolveSettings().TiledBlur ? "TiledSetupFillArgsIndirectDilateIndirectBlur" : "TiledSetupFillArgsIndirectDilate";
        }

        public static string ResolveFurBlurCandidateGateLabel(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ShouldUseFurBlurBase(request, asset))
            {
                return "Disabled";
            }

            HasVisibleFurBlurCandidate(request);
            return cachedCandidateGateLabel;
        }

        public static Vector4 CreateScreenSizeVector(int width, int height)
        {
            var safeWidth = Mathf.Max(1, width);
            var safeHeight = Mathf.Max(1, height);
            return new Vector4(safeWidth, safeHeight, 1f / safeWidth, 1f / safeHeight);
        }

        public static BurtRenderBufferDescriptor CreateTileArgsBufferDescriptor()
        {
            return new BurtRenderBufferDescriptor(
                8,
                sizeof(uint),
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured,
                "_BurtFurBlurArgsBuffer");
        }

        public static BurtRenderBufferDescriptor CreateTileDataBufferDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(camera);
            var tileCountX = Mathf.CeilToInt(Mathf.Max(1, descriptor.width) / (float)TileThreadSize);
            var tileCountY = Mathf.CeilToInt(Mathf.Max(1, descriptor.height) / (float)TileThreadSize);
            var tileCount = Mathf.Max(1, tileCountX * tileCountY);
            return new BurtRenderBufferDescriptor(
                tileCount * 2,
                sizeof(uint),
                GraphicsBuffer.Target.Structured,
                "_BurtFurBlurTileDataBuffer");
        }

        public static BurtFurBlurSettings ResolveSettings()
        {
            var component = GetFurBlurVolumeComponent();
            if (component == null || !component.active)
            {
                return BurtFurBlurSettings.Default;
            }

            return new BurtFurBlurSettings(
                component.enabled.value,
                component.tiledBlur.value,
                component.radiusCm.value,
                component.depthThresholdEye.value,
                component.directionDilationThreshold.value,
                component.thetaTemporal.value,
                component.colorTemporal.value,
                component.thetaFeedback.value,
                component.temporalFeedback.value);
        }

        public static void UploadCommonGlobals(
            CommandBuffer cmd,
            BurtRenderRequest request,
            int width,
            int height,
            BurtFurBlurSettings settings,
            bool colorHistoryValid,
            bool propertyHistoryValid,
            int historyAge)
        {
            if (cmd == null)
            {
                return;
            }

            ResolveTemporalMatrices(request, out var previousNonJitteredViewProjection, out var inverseCurrentNonJitteredViewProjection, out var jitter, out var jitterPixels);
            var temporalAA = request != null ? request.TemporalAA : null;
            var historyExposureCorrection = temporalAA != null ? Mathf.Max(temporalAA.HistoryExposureCorrection, 0f) : 1f;
            cmd.SetGlobalVector(ScreenSizeId, CreateScreenSizeVector(width, height));
            cmd.SetGlobalVector(ParamsId, new Vector4(settings.RadiusCm, settings.DepthThresholdEye, settings.DirectionDilationThreshold, settings.ThetaFeedback));
            cmd.SetGlobalVector(HistoryParamsId, new Vector4(colorHistoryValid ? 1f : 0f, settings.TemporalFeedback, historyAge, historyExposureCorrection));
            cmd.SetGlobalVector(TemporalParamsId, new Vector4(colorHistoryValid ? 1f : 0f, propertyHistoryValid ? 1f : 0f, historyAge, settings.Enabled ? 1f : 0f));
            cmd.SetGlobalMatrix(PreviousNonJitteredViewProjectionId, previousNonJitteredViewProjection);
            cmd.SetGlobalMatrix(InverseCurrentNonJitteredViewProjectionId, inverseCurrentNonJitteredViewProjection);
            cmd.SetGlobalVector(JitterId, new Vector4(jitter.x, jitter.y, jitterPixels.x, jitterPixels.y));
        }

        public static void UploadMotionVectorGlobals(CommandBuffer cmd, BurtRenderRequest request, int width, int height)
        {
            if (cmd == null)
            {
                return;
            }

            ResolveMotionVectorMatrices(request, out var currentNonJitteredViewProjection, out var previousNonJitteredViewProjection);
            cmd.SetGlobalVector(ScreenSizeId, CreateScreenSizeVector(width, height));
            cmd.SetGlobalMatrix(CurrentNonJitteredViewProjectionId, currentNonJitteredViewProjection);
            cmd.SetGlobalMatrix(PreviousNonJitteredViewProjectionId, previousNonJitteredViewProjection);
        }

        public static void BindVelocityTexture(CommandBuffer cmd, BurtRenderGraphContext context)
        {
            if (cmd == null)
            {
                return;
            }

            var velocityTarget = context != null ? context.FurBlurVelocityTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurVelocityName);
            if (velocityTarget.IsValid)
            {
                cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurVelocityTextureId, velocityTarget.Identifier);
            }
        }

        public static BurtRenderTargetHandle ResolveActiveFurBlurPropertyTarget(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            }

            return ShouldUseFurBlurThetaTemporal(context.Request, context.Asset)
                ? context.FurBlurPropertyTempTarget
                : context.FurBlurPropertyTarget;
        }

        public static bool TryBindTileBuffers(CommandBuffer cmd, BurtRenderGraphContext context)
        {
            if (cmd == null || context == null)
            {
                return false;
            }

            var args = context.FurBlurArgsBuffer;
            var tiles = context.FurBlurTileDataBuffer;
            if (!args.IsValid || !args.HasBuffer || !tiles.IsValid || !tiles.HasBuffer)
            {
                return false;
            }

            cmd.SetGlobalBuffer(TileDataBufferId, tiles.Buffer);
            return true;
        }

        public static bool TryGetFullScreenTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraColorReadTarget,
            out BurtRenderTargetHandle cameraColorWriteTarget,
            out BurtRenderTargetHandle propertyTarget,
            out BurtRenderTargetHandle colorTarget,
            out BurtRenderTargetHandle temporalTarget)
        {
            cameraColorReadTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraColorWriteTarget = cameraColorReadTarget;
            propertyTarget = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            colorTarget = context != null ? context.FurBlurColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurColorName);
            temporalTarget = context != null ? context.FurBlurTemporalTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurTemporalName);
            return context != null &&
                ShouldUseFurBlur(context.Request, context.Asset) &&
                cameraColorReadTarget.IsValid &&
                cameraColorWriteTarget.IsValid &&
                propertyTarget.IsValid &&
                colorTarget.IsValid &&
                temporalTarget.IsValid;
        }

        public static Material GetMaterial(ref Material cachedMaterial, ref bool hasLoggedMissingShader)
        {
            if (cachedMaterial != null)
            {
                return cachedMaterial;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            cachedMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            return cachedMaterial;
        }

        public static ComputeShader GetTiledComputeShader(ref ComputeShader shader, ref bool hasLoggedMissingComputeShader)
        {
            if (shader != null)
            {
                return shader;
            }

            shader = Resources.Load<ComputeShader>(TiledComputeShaderResourcePath);
            if (shader == null && !hasLoggedMissingComputeShader)
            {
                Debug.LogWarning("BurtRP could not find compute shader resource: " + TiledComputeShaderResourcePath);
                hasLoggedMissingComputeShader = true;
            }

            return shader;
        }

        public static bool TryFindTiledComputeKernel(ComputeShader shader, string kernelName, ref bool hasLoggedMissingKernel, out int kernel)
        {
            kernel = -1;
            if (shader == null)
            {
                return false;
            }

            if (!shader.HasKernel(kernelName))
            {
                if (!hasLoggedMissingKernel)
                {
                    Debug.LogWarning("BurtRP could not find FurBlur tiled compute kernel: " + kernelName);
                    hasLoggedMissingKernel = true;
                }

                return false;
            }

            kernel = shader.FindKernel(kernelName);
            return kernel >= 0;
        }

        private static bool ShouldUseFurBlurBase(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || asset == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            return asset.RendererMode == BurtRendererMode.Deferred && ResolveSettings().Enabled;
        }

        private static bool IsTiledComputeAvailable()
        {
            var frame = Time.frameCount;
            if (tiledComputeAvailabilityFrame == frame)
            {
                return tiledComputeAvailable;
            }

            tiledComputeAvailabilityFrame = frame;
            var shader = Resources.Load<ComputeShader>(TiledComputeShaderResourcePath);
            tiledComputeAvailable = shader != null &&
                shader.HasKernel("InitArgsCS") &&
                shader.HasKernel("SetupCS") &&
                shader.HasKernel("FillArgsPS") &&
                shader.HasKernel("DilateCS");
            return tiledComputeAvailable;
        }

        private static bool SupportsTiledPropertyFormat()
        {
            return SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGFloat);
        }

        private static BurtFurBlurVolumeComponent GetFurBlurVolumeComponent()
        {
            var volumeManager = VolumeManager.instance;
            var stack = volumeManager != null ? volumeManager.stack : null;
            return stack != null ? stack.GetComponent<BurtFurBlurVolumeComponent>() : null;
        }

        private static void ResolveTemporalMatrices(
            BurtRenderRequest request,
            out Matrix4x4 previousNonJitteredViewProjection,
            out Matrix4x4 inverseCurrentNonJitteredViewProjection,
            out Vector2 jitter,
            out Vector2 jitterPixels)
        {
            var temporalAA = request != null ? request.TemporalAA : null;
            if (temporalAA != null && !object.ReferenceEquals(temporalAA, BurtTemporalAARequestState.Disabled))
            {
                previousNonJitteredViewProjection = temporalAA.PreviousNonJitteredViewProjectionMatrix;
                inverseCurrentNonJitteredViewProjection = temporalAA.InverseCurrentNonJitteredViewProjectionMatrix;
                jitter = temporalAA.Enabled ? temporalAA.Jitter : Vector2.zero;
                jitterPixels = temporalAA.Enabled ? temporalAA.JitterPixels : Vector2.zero;
                return;
            }

            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                previousNonJitteredViewProjection = Matrix4x4.identity;
                inverseCurrentNonJitteredViewProjection = Matrix4x4.identity;
                jitter = Vector2.zero;
                jitterPixels = Vector2.zero;
                return;
            }

            var projectionMatrix = camera.nonJitteredProjectionMatrix;
            if (projectionMatrix == Matrix4x4.zero)
            {
                projectionMatrix = camera.projectionMatrix;
            }

            var viewProjection = GL.GetGPUProjectionMatrix(projectionMatrix, true) * camera.worldToCameraMatrix;
            previousNonJitteredViewProjection = viewProjection;
            inverseCurrentNonJitteredViewProjection = viewProjection.inverse;
            jitter = Vector2.zero;
            jitterPixels = Vector2.zero;
        }

        private static void ResolveMotionVectorMatrices(
            BurtRenderRequest request,
            out Matrix4x4 currentNonJitteredViewProjection,
            out Matrix4x4 previousNonJitteredViewProjection)
        {
            var temporalAA = request != null ? request.TemporalAA : null;
            if (temporalAA != null && !object.ReferenceEquals(temporalAA, BurtTemporalAARequestState.Disabled))
            {
                currentNonJitteredViewProjection = temporalAA.CurrentNonJitteredViewProjectionMatrix;
                previousNonJitteredViewProjection = temporalAA.PreviousNonJitteredViewProjectionMatrix;
                return;
            }

            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                currentNonJitteredViewProjection = Matrix4x4.identity;
                previousNonJitteredViewProjection = Matrix4x4.identity;
                return;
            }

            var projectionMatrix = camera.nonJitteredProjectionMatrix;
            if (projectionMatrix == Matrix4x4.zero)
            {
                projectionMatrix = camera.projectionMatrix;
            }

            currentNonJitteredViewProjection = GL.GetGPUProjectionMatrix(projectionMatrix, true) * camera.worldToCameraMatrix;
            previousNonJitteredViewProjection = currentNonJitteredViewProjection;
        }

        private static bool HasVisibleFurBlurCandidate(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                cachedCandidateGateLabel = "FallbackNoCamera";
                return true;
            }

            var frame = Time.frameCount;
            var cameraId = camera.GetInstanceID();
            if (cachedFurBlurFeatureFrame == frame &&
                cachedFurBlurFeatureCameraId == cameraId &&
                cachedFurBlurFeatureCullingMask == camera.cullingMask)
            {
                return cachedHasVisibleFurBlur;
            }

            cachedFurBlurFeatureFrame = frame;
            cachedFurBlurFeatureCameraId = cameraId;
            cachedFurBlurFeatureCullingMask = camera.cullingMask;
            cachedHasVisibleFurBlur = TryScanVisibleFurBlurCandidate(camera, out var hasVisibleFurBlur)
                ? hasVisibleFurBlur
                : true;
            cachedCandidateGateLabel = cachedHasVisibleFurBlur
                ? (hasVisibleFurBlur ? "ActiveVisibleMultipassFur" : "FallbackScanFailed")
                : "InactiveNoVisibleMultipassFur";
            return cachedHasVisibleFurBlur;
        }

        private static bool TryScanVisibleFurBlurCandidate(Camera camera, out bool hasVisibleFurBlur)
        {
            hasVisibleFurBlur = false;
            Plane[] frustumPlanes;
            try
            {
                frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            }
            catch (System.Exception)
            {
                return false;
            }

            var renderers = BurtMultipassRenderer.RegisteredRenderers;
            if (renderers == null)
            {
                return true;
            }

            for (var rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
            {
                var multipassRenderer = renderers[rendererIndex];
                if (!IsActiveMultipassRenderer(multipassRenderer) ||
                    !IsRendererInCameraLayer(multipassRenderer.m_Renderer, camera) ||
                    !IsRendererInFrustum(multipassRenderer.m_Renderer, frustumPlanes))
                {
                    continue;
                }

                if (RendererHasFurBlurCandidate(multipassRenderer))
                {
                    hasVisibleFurBlur = true;
                    return true;
                }
            }

            return true;
        }

        private static bool IsActiveMultipassRenderer(BurtMultipassRenderer multipassRenderer)
        {
            var renderer = multipassRenderer != null ? multipassRenderer.m_Renderer : null;
            return multipassRenderer != null &&
                multipassRenderer.isActiveAndEnabled &&
                renderer != null &&
                renderer.enabled &&
                renderer.gameObject != null &&
                renderer.gameObject.activeInHierarchy;
        }

        private static bool IsRendererInCameraLayer(Renderer renderer, Camera camera)
        {
            var gameObject = renderer != null ? renderer.gameObject : null;
            return gameObject != null && camera != null && (camera.cullingMask & (1 << gameObject.layer)) != 0;
        }

        private static bool IsRendererInFrustum(Renderer renderer, Plane[] frustumPlanes)
        {
            return renderer != null &&
                (frustumPlanes == null ||
                frustumPlanes.Length == 0 ||
                GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds));
        }

        private static bool RendererHasFurBlurCandidate(BurtMultipassRenderer multipassRenderer)
        {
            var renderer = multipassRenderer != null ? multipassRenderer.m_Renderer : null;
            var materials = renderer != null ? renderer.sharedMaterials : null;
            if (materials == null)
            {
                return false;
            }

            for (var submeshIndex = 0; submeshIndex < materials.Length; submeshIndex++)
            {
                if (!MaterialEnablesFurBlur(materials[submeshIndex]))
                {
                    continue;
                }

                var passList = multipassRenderer.GetSupportPass(submeshIndex, BurtMultipassShaderPass.FurBlurProperty);
                if (passList != null && passList.Count > 0)
                {
                    return true;
                }

                if (materials[submeshIndex].FindPass(FurBlurPropertyShaderPassName) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MaterialEnablesFurBlur(Material material)
        {
            return material != null &&
                material.HasProperty(FurBlurEnabledPropertyName) &&
                material.GetFloat(FurBlurEnabledPropertyName) > 0.5f &&
                IsOpaqueMaterial(material);
        }

        private static bool IsOpaqueMaterial(Material material)
        {
            var renderQueue = material.renderQueue;
            if (renderQueue >= 0)
            {
                return renderQueue <= OpaqueRenderQueueMax;
            }

            var queueTag = material.GetTag("Queue", true, "Geometry");
            return !queueTag.StartsWith("Transparent", System.StringComparison.OrdinalIgnoreCase) &&
                !queueTag.StartsWith("Overlay", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    internal readonly struct BurtFurBlurHistoryTextures
    {
        public RenderTexture Color { get; }
        public RenderTexture Property { get; }
        public int HistoryAge { get; }

        public BurtFurBlurHistoryTextures(RenderTexture color, RenderTexture property, int historyAge)
        {
            Color = color;
            Property = property;
            HistoryAge = historyAge;
        }
    }

    internal readonly struct BurtFurBlurHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public bool HasPropertyHistory { get; }
        public bool PropertyDescriptorMatches { get; }
        public int HistoryAge { get; }
        public int FrameIndex { get; }
        public RenderTextureFormat Format { get; }
        public RenderTextureFormat PropertyFormat { get; }
        public int Width { get; }
        public int Height { get; }
        public int PropertyWidth { get; }
        public int PropertyHeight { get; }
        public string Reason { get; }

        public BurtFurBlurHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            bool hasPropertyHistory,
            bool propertyDescriptorMatches,
            int historyAge,
            int frameIndex,
            RenderTextureFormat format,
            RenderTextureFormat propertyFormat,
            int width,
            int height,
            int propertyWidth,
            int propertyHeight,
            string reason)
        {
            HasHistory = hasHistory;
            DescriptorMatches = descriptorMatches;
            HasPropertyHistory = hasPropertyHistory;
            PropertyDescriptorMatches = propertyDescriptorMatches;
            HistoryAge = historyAge;
            FrameIndex = frameIndex;
            Format = format;
            PropertyFormat = propertyFormat;
            Width = width;
            Height = height;
            PropertyWidth = propertyWidth;
            PropertyHeight = propertyHeight;
            Reason = reason;
        }
    }

    internal static class BurtFurBlurHistoryUtility
    {
        public const float DefaultFeedback = 0.96f;

        private const float ProjectionEpsilon = 0.0001f;
        private const float CameraCutPositionThreshold = 0.5f;
        private const float CameraCutRotationThreshold = 5f;

        private static readonly Dictionary<int, CameraState> CameraStates = new Dictionary<int, CameraState>();
        private static readonly List<int> CameraStateRemovalKeys = new List<int>();
        private static int cameraStatePruneCounter;

        public static BurtFurBlurHistoryTextures EnsureHistoryTextures(BurtRenderRequest request, out bool historyValid)
        {
            historyValid = false;
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return new BurtFurBlurHistoryTextures(null, null, 0);
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            state.Camera = camera;
            PruneDisposedCameraStates();
            var descriptor = CreateHistoryDescriptor(camera);
            var propertyDescriptor = CreatePropertyHistoryDescriptor(camera);
            var colorDescriptorMatches = state.ColorHistory != null && Matches(state.Descriptor, descriptor);
            var propertyDescriptorMatches = state.PropertyHistory != null && Matches(state.PropertyDescriptor, propertyDescriptor);
            var descriptorMatches = colorDescriptorMatches && propertyDescriptorMatches;
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            var projectionMatrix = ResolveHistoryProjectionMatrix(request, camera);
            var invalidationReason = ResolveInvalidationReason(camera, state, projectionMatrix, targetWidth, targetHeight, descriptorMatches);

            if (!descriptorMatches)
            {
                ReleaseHistory(state);
            }

            if (state.ColorHistory == null)
            {
                state.Descriptor = descriptor;
                state.ColorHistory = CreateHistoryTexture(descriptor, "Burt Fur Blur History " + camera.GetInstanceID(), FilterMode.Bilinear);
                SetAllocationInvalidationReason(state, "HistoryAllocated");
            }

            if (state.PropertyHistory == null)
            {
                state.PropertyDescriptor = propertyDescriptor;
                state.PropertyHistory = CreateHistoryTexture(propertyDescriptor, "Burt Fur Blur Property History " + camera.GetInstanceID(), FilterMode.Point);
                SetAllocationInvalidationReason(state, "PropertyHistoryAllocated");
            }

            if (!string.IsNullOrEmpty(invalidationReason))
            {
                InvalidateState(state, invalidationReason);
            }

            state.FrameIndex++;
            state.CurrentProjectionMatrix = projectionMatrix;
            CaptureCurrentCameraState(camera, state, targetWidth, targetHeight);

            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            historyValid = state.HasValidHistory && state.HasPreviousCameraState && state.ColorHistory != null && state.PropertyHistory != null;
            return new BurtFurBlurHistoryTextures(state.ColorHistory, state.PropertyHistory, historyAge);
        }

        public static RenderTexture GetCurrentHistoryTexture(BurtRenderRequest request, out bool historyValid, out int historyAge)
        {
            historyValid = false;
            historyAge = 0;
            var camera = request != null ? request.Camera : null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return null;
            }

            historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            historyValid = state.HasValidHistory && state.ColorHistory != null;
            return state.ColorHistory;
        }

        public static BurtFurBlurHistoryTextures GetPendingHistoryTextures(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return new BurtFurBlurHistoryTextures(null, null, 0);
            }

            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtFurBlurHistoryTextures(state.ColorHistory, state.PropertyHistory, historyAge);
        }

        public static void MarkHistoryValid(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return;
            }

            if (!state.HasValidHistory)
            {
                state.FirstValidFrameIndex = state.FrameIndex;
            }

            state.HasValidHistory = true;
            state.PreviousProjectionMatrix = state.CurrentProjectionMatrix;
            state.PreviousCameraPosition = state.CurrentCameraPosition;
            state.PreviousCameraRotation = state.CurrentCameraRotation;
            state.PreviousOrthographic = state.CurrentOrthographic;
            state.PreviousFieldOfView = state.CurrentFieldOfView;
            state.PreviousOrthographicSize = state.CurrentOrthographicSize;
            state.PreviousNearClipPlane = state.CurrentNearClipPlane;
            state.PreviousFarClipPlane = state.CurrentFarClipPlane;
            state.PreviousTargetTextureId = state.CurrentTargetTextureId;
            state.PreviousTargetWidth = state.CurrentTargetWidth;
            state.PreviousTargetHeight = state.CurrentTargetHeight;
            state.HasPreviousCameraState = true;
            state.LastInvalidationReason = "None";
        }

        public static BurtFurBlurHistoryStatus GetHistoryStatus(Camera camera)
        {
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return new BurtFurBlurHistoryStatus(false, false, false, false, 0, 0, RenderTextureFormat.Default, RenderTextureFormat.Default, 0, 0, 0, 0, "NoCameraOrHistory");
            }

            var descriptor = CreateHistoryDescriptor(camera);
            var propertyDescriptor = CreatePropertyHistoryDescriptor(camera);
            var hasHistory = state.ColorHistory != null;
            var hasPropertyHistory = state.PropertyHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtFurBlurHistoryStatus(
                state.HasValidHistory && hasHistory,
                hasHistory && Matches(state.Descriptor, descriptor),
                state.HasValidHistory && hasPropertyHistory,
                hasPropertyHistory && Matches(state.PropertyDescriptor, propertyDescriptor),
                historyAge,
                state.FrameIndex,
                hasHistory ? state.ColorHistory.format : RenderTextureFormat.Default,
                hasPropertyHistory ? state.PropertyHistory.format : RenderTextureFormat.Default,
                hasHistory ? state.ColorHistory.width : 0,
                hasHistory ? state.ColorHistory.height : 0,
                hasPropertyHistory ? state.PropertyHistory.width : 0,
                hasPropertyHistory ? state.PropertyHistory.height : 0,
                string.IsNullOrEmpty(state.LastInvalidationReason) ? "None" : state.LastInvalidationReason);
        }

        private static RenderTextureDescriptor CreateHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurTemporalDescriptor(camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static RenderTextureDescriptor CreatePropertyHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(camera);
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            return descriptor;
        }

        private static Matrix4x4 ResolveHistoryProjectionMatrix(BurtRenderRequest request, Camera camera)
        {
            var temporalAA = request != null ? request.TemporalAA : null;
            if (temporalAA != null && !object.ReferenceEquals(temporalAA, BurtTemporalAARequestState.Disabled))
            {
                return temporalAA.NonJitteredProjectionMatrix;
            }

            return camera != null ? camera.projectionMatrix : Matrix4x4.identity;
        }

        private static CameraState GetOrCreateState(int cameraId)
        {
            if (!CameraStates.TryGetValue(cameraId, out var state))
            {
                state = new CameraState();
                CameraStates.Add(cameraId, state);
            }

            return state;
        }

        private static void PruneDisposedCameraStates()
        {
            cameraStatePruneCounter++;
            if (cameraStatePruneCounter < 120)
            {
                return;
            }

            cameraStatePruneCounter = 0;
            CameraStateRemovalKeys.Clear();
            foreach (var pair in CameraStates)
            {
                if (pair.Value.Camera == null)
                {
                    CameraStateRemovalKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < CameraStateRemovalKeys.Count; i++)
            {
                if (CameraStates.TryGetValue(CameraStateRemovalKeys[i], out var state))
                {
                    ReleaseHistory(state);
                }

                CameraStates.Remove(CameraStateRemovalKeys[i]);
            }
        }

        private static void ReleaseHistory(CameraState state)
        {
            if (state == null)
            {
                return;
            }

            ReleaseTexture(state.ColorHistory);
            ReleaseTexture(state.PropertyHistory);
            state.ColorHistory = null;
            state.PropertyHistory = null;
            state.HasValidHistory = false;
            state.HasPreviousCameraState = false;
            state.FirstValidFrameIndex = 0;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            Object.DestroyImmediate(texture);
        }

        private static RenderTexture CreateHistoryTexture(RenderTextureDescriptor descriptor, string name, FilterMode filterMode)
        {
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private static bool Matches(RenderTextureDescriptor previous, RenderTextureDescriptor current)
        {
            return previous.width == current.width &&
                previous.height == current.height &&
                previous.colorFormat == current.colorFormat &&
                previous.depthBufferBits == current.depthBufferBits &&
                previous.msaaSamples == current.msaaSamples &&
                previous.sRGB == current.sRGB;
        }

        private static string ResolveInvalidationReason(
            Camera camera,
            CameraState state,
            Matrix4x4 projectionMatrix,
            int targetWidth,
            int targetHeight,
            bool descriptorMatches)
        {
            if (!descriptorMatches)
            {
                return "DescriptorChanged";
            }

            if (!state.HasPreviousCameraState)
            {
                return state.HasValidHistory ? "NoPreviousCameraState" : null;
            }

            if (GetTargetTextureId(camera) != state.PreviousTargetTextureId)
            {
                return "TargetTextureChanged";
            }

            if (targetWidth != state.PreviousTargetWidth || targetHeight != state.PreviousTargetHeight)
            {
                return "TargetSizeChanged";
            }

            if (camera.orthographic != state.PreviousOrthographic)
            {
                return "ProjectionModeChanged";
            }

            if (camera.orthographic)
            {
                if (FloatChanged(camera.orthographicSize, state.PreviousOrthographicSize, 0.0001f))
                {
                    return "OrthographicSizeChanged";
                }
            }
            else if (FloatChanged(camera.fieldOfView, state.PreviousFieldOfView, 0.0001f))
            {
                return "FieldOfViewChanged";
            }

            if (FloatChanged(camera.nearClipPlane, state.PreviousNearClipPlane, 0.0001f))
            {
                return "NearClipChanged";
            }

            if (FloatChanged(camera.farClipPlane, state.PreviousFarClipPlane, 0.001f))
            {
                return "FarClipChanged";
            }

            if (ProjectionChanged(projectionMatrix, state.PreviousProjectionMatrix))
            {
                return "ProjectionMatrixChanged";
            }

            return CameraCutDetected(camera, state) ? "CameraCut" : null;
        }

        private static void CaptureCurrentCameraState(Camera camera, CameraState state, int targetWidth, int targetHeight)
        {
            state.CurrentCameraPosition = camera.transform.position;
            state.CurrentCameraRotation = camera.transform.rotation;
            state.CurrentOrthographic = camera.orthographic;
            state.CurrentFieldOfView = camera.fieldOfView;
            state.CurrentOrthographicSize = camera.orthographicSize;
            state.CurrentNearClipPlane = camera.nearClipPlane;
            state.CurrentFarClipPlane = camera.farClipPlane;
            state.CurrentTargetTextureId = GetTargetTextureId(camera);
            state.CurrentTargetWidth = targetWidth;
            state.CurrentTargetHeight = targetHeight;
        }

        private static void GetTargetSize(Camera camera, out int width, out int height)
        {
            width = Mathf.Max(1, camera.pixelWidth);
            height = Mathf.Max(1, camera.pixelHeight);
            if (camera.targetTexture != null)
            {
                width = Mathf.Max(1, camera.targetTexture.width);
                height = Mathf.Max(1, camera.targetTexture.height);
            }
        }

        private static int GetTargetTextureId(Camera camera)
        {
            return camera != null && camera.targetTexture != null ? camera.targetTexture.GetInstanceID() : 0;
        }

        private static bool FloatChanged(float current, float previous, float epsilon)
        {
            return Mathf.Abs(current - previous) > epsilon;
        }

        private static bool ProjectionChanged(Matrix4x4 current, Matrix4x4 previous)
        {
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    if (Mathf.Abs(current[row, column] - previous[row, column]) > ProjectionEpsilon)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool CameraCutDetected(Camera camera, CameraState state)
        {
            var positionDelta = camera.transform.position - state.PreviousCameraPosition;
            var rotationDelta = Quaternion.Angle(camera.transform.rotation, state.PreviousCameraRotation);
            return positionDelta.sqrMagnitude > CameraCutPositionThreshold * CameraCutPositionThreshold ||
                rotationDelta > CameraCutRotationThreshold;
        }

        private static void SetAllocationInvalidationReason(CameraState state, string reason)
        {
            if (state != null &&
                (string.IsNullOrEmpty(state.LastInvalidationReason) ||
                state.LastInvalidationReason == "NotAllocated" ||
                state.LastInvalidationReason == "None"))
            {
                state.LastInvalidationReason = reason;
            }
        }

        private static void InvalidateState(CameraState state, string reason)
        {
            state.HasValidHistory = false;
            state.FirstValidFrameIndex = 0;
            state.LastInvalidationReason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        }

        private sealed class CameraState
        {
            public Camera Camera;
            public RenderTexture ColorHistory;
            public RenderTexture PropertyHistory;
            public RenderTextureDescriptor Descriptor;
            public RenderTextureDescriptor PropertyDescriptor;
            public bool HasValidHistory;
            public bool HasPreviousCameraState;
            public int FrameIndex;
            public int FirstValidFrameIndex;
            public string LastInvalidationReason = "NotAllocated";
            public Matrix4x4 CurrentProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousProjectionMatrix = Matrix4x4.identity;
            public Vector3 CurrentCameraPosition;
            public Vector3 PreviousCameraPosition;
            public Quaternion CurrentCameraRotation = Quaternion.identity;
            public Quaternion PreviousCameraRotation = Quaternion.identity;
            public bool CurrentOrthographic;
            public bool PreviousOrthographic;
            public float CurrentFieldOfView;
            public float PreviousFieldOfView;
            public float CurrentOrthographicSize;
            public float PreviousOrthographicSize;
            public float CurrentNearClipPlane;
            public float PreviousNearClipPlane;
            public float CurrentFarClipPlane;
            public float PreviousFarClipPlane;
            public int CurrentTargetTextureId;
            public int PreviousTargetTextureId;
            public int CurrentTargetWidth;
            public int CurrentTargetHeight;
            public int PreviousTargetWidth;
            public int PreviousTargetHeight;
        }
    }
}
