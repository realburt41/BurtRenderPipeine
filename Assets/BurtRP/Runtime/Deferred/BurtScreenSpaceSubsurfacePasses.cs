using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceTextureId,
                context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName),
                BurtScreenSpaceSubsurfacePassUtility.CreateSourceDescriptor(context),
                FilterMode.Bilinear);
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceBaseColorPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Base Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceBaseColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorTextureId,
                context != null ? context.ScreenSpaceSubsurfaceBaseColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName),
                BurtScreenSpaceSubsurfacePassUtility.CreateBaseColorDescriptor(context),
                FilterMode.Point);
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceEmissionPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Emission";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceEmission();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionTextureId,
                context != null ? context.ScreenSpaceSubsurfaceEmissionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName),
                BurtScreenSpaceSubsurfacePassUtility.CreateEmissionDescriptor(context),
                FilterMode.Point);
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
                context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName),
                BurtScreenSpaceSubsurfacePassUtility.CreateComputeColorDescriptor(context),
                FilterMode.Bilinear);
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

    internal sealed class BurtAllocateScreenSpaceSubsurfaceProfileIDAndTypePass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Profile ID And Type";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceProfileIDAndType();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeTextureId,
                context != null ? context.ScreenSpaceSubsurfaceProfileIDAndTypeTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName),
                BurtScreenSpaceSubsurfacePassUtility.CreateProfileIDAndTypeDescriptor(context),
                FilterMode.Point);
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceMaskPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Mask";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceMask();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskTextureId,
                context.ScreenSpaceSubsurfaceMaskTarget,
                BurtScreenSpaceSubsurfacePassUtility.CreateMaskDescriptor(context),
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
                context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName),
                BurtScreenSpaceSubsurfacePassUtility.CreateComputeColorDescriptor(context),
                FilterMode.Bilinear);
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
                FilterMode.Point);
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceHistoryPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface History";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceHistory();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceHistoryTextureId,
                context.ScreenSpaceSubsurfaceHistoryTarget,
                BurtScreenSpaceSubsurfacePassUtility.CreateHistoryDescriptor(context),
                FilterMode.Point);
        }
    }

    internal sealed class BurtAllocateScreenSpaceSubsurfaceVelocityPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate Screen Space Subsurface Velocity";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteScreenSpaceSubsurfaceVelocity();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Allocate(
                context,
                Name,
                BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityTextureId,
                context.ScreenSpaceSubsurfaceVelocityTarget,
                BurtScreenSpaceSubsurfacePassUtility.CreateVelocityDescriptor(context),
                FilterMode.Point);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceCopySourcePass : BurtRenderPass
    {
        public override string Name => "Burt Screen Space Subsurface Copy Source";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Copy;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.WriteScreenSpaceSubsurfaceSource();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var target))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.CopyTexture(cameraColorTarget.Identifier, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraColorTarget,
            out BurtRenderTargetHandle target)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            target = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                cameraColorTarget.IsValid &&
                target.IsValid;
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceForwardPass : BurtRenderPass
    {
        public override string Name => "Burt Screen Space Subsurface Forward";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.DrawRenderers;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.WriteScreenSpaceSubsurfaceBaseColor();
            builder.WriteScreenSpaceSubsurfaceEmission();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var baseColor, out var emission, out var cameraDepth))
            {
                return;
            }

            var request = context.Request;
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            ClearForwardTargets(cmd, context, baseColor, emission, cameraDepth, camera);
            var colorTargets = new[]
            {
                baseColor.Identifier,
                emission.Identifier
            };
            cmd.SetRenderTarget(colorTargets, cameraDepth.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSettings = BurtDrawingSettingsUtility.CreateSubsurfaceForwardDrawingSettings(sortingSettings);
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, camera.cullingMask);

            var drawScopeCmd = CommandBufferPool.Get(Name + " Draw Scope");
            drawScopeCmd.BeginSample(Name);
            context.ScriptableContext.ExecuteCommandBuffer(drawScopeCmd);
            CommandBufferPool.Release(drawScopeCmd);

            context.ScriptableContext.DrawRenderers(request.CullingResults, ref drawingSettings, ref filteringSettings);

            drawScopeCmd = CommandBufferPool.Get(Name + " Draw Scope");
            drawScopeCmd.EndSample(Name);
            context.ScriptableContext.ExecuteCommandBuffer(drawScopeCmd);
            CommandBufferPool.Release(drawScopeCmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle baseColor,
            out BurtRenderTargetHandle emission,
            out BurtRenderTargetHandle cameraDepth)
        {
            baseColor = context != null ? context.ScreenSpaceSubsurfaceBaseColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
            emission = context != null ? context.ScreenSpaceSubsurfaceEmissionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                baseColor.IsValid &&
                emission.IsValid &&
                cameraDepth.IsValid;
        }

        private static void ClearForwardTargets(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle baseColor,
            BurtRenderTargetHandle emission,
            BurtRenderTargetHandle cameraDepth,
            Camera camera)
        {
            cmd.SetRenderTarget(baseColor.Identifier, cameraDepth.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.SetRenderTarget(emission.Identifier, cameraDepth.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.ClearRenderTarget(false, true, new Color(0f, 0f, 0f, 0.5f));
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceBuildVelocityPass : BurtRenderPass
    {
        private Material postProcessMaterial;
        private Material motionVectorMaterial;
        private bool hasLoggedMissingPostProcessShader;
        private bool hasLoggedMissingMotionVectorShader;

        public override string Name => "Burt Screen Space Subsurface Build Velocity";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.WriteScreenSpaceSubsurfaceVelocity();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraDepth, out var velocity))
            {
                return;
            }

            var camera = context.Request.Camera;
            var descriptor = BurtScreenSpaceSubsurfacePassUtility.CreateVelocityDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var cmd = CommandBufferPool.Get(Name);
            var temporalAA = context.Request.TemporalAA;
            var postMaterial = GetPostProcessMaterial();
            var canBuildVelocity = temporalAA != null && temporalAA.Enabled && temporalAA.HistoryValid && postMaterial != null;

            if (!canBuildVelocity)
            {
                ClearVelocityTarget(cmd, context, velocity);
                context.ScriptableContext.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                return;
            }

            BurtScreenSpaceSubsurfacePassUtility.SetTemporalAAVelocityGlobals(cmd, temporalAA, width, height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepth.Identifier);

            cmd.SetRenderTarget(velocity.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.TemporalAACurrentDepthTextureId, cameraDepth.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, postMaterial, BurtScreenSpaceSubsurfacePassUtility.TemporalAACameraVelocityPassIndex, MeshTopology.Triangles, 3, 1);

            var motionMaterial = GetMotionVectorMaterial();
            var drewObjectVelocity = BurtScreenSpaceSubsurfacePassUtility.DrawObjectMotionVectors(context, cmd, camera, velocity.Identifier, cameraDepth, motionMaterial);
            temporalAA.ObjectMotionVectorPassDrawn = temporalAA.ObjectMotionVectorPassDrawn || drewObjectVelocity;
            if (drewObjectVelocity)
            {
                temporalAA.VelocityMode = BurtTemporalAAVelocityMode.CameraAndObject;
            }
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle velocity)
        {
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            velocity = context != null ? context.ScreenSpaceSubsurfaceVelocityTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                context.Request.Camera != null &&
                cameraDepth.IsValid &&
                velocity.IsValid;
        }

        private void ClearVelocityTarget(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle velocity)
        {
            cmd.SetRenderTarget(velocity.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.ClearRenderTarget(false, true, Color.clear);
        }

        private Material GetPostProcessMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(
                ref postProcessMaterial,
                BurtScreenSpaceSubsurfacePassUtility.PostProcessShaderName,
                ref hasLoggedMissingPostProcessShader);
        }

        private Material GetMotionVectorMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(
                ref motionVectorMaterial,
                BurtScreenSpaceSubsurfacePassUtility.MotionVectorShaderName,
                ref hasLoggedMissingMotionVectorShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceBuildMaskPass : BurtRenderPass
    {
        private const int MaskPassIndex = 7;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Build Mask";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.ReadScreenSpaceSubsurfaceBaseColor();
            builder.ReadGBuffer1();
            builder.WriteScreenSpaceSubsurfaceMask();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var mask = context != null ? context.ScreenSpaceSubsurfaceMaskTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
            var cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            var baseColor = context != null ? context.ScreenSpaceSubsurfaceBaseColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
            var gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !mask.IsValid ||
                !cameraDepth.IsValid ||
                !baseColor.IsValid ||
                !gbuffer1.IsValid)
            {
                return;
            }

            var maskMaterial = GetMaterial();
            if (maskMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindMaskInputs(cmd, context, baseColor, gbuffer1);
            cmd.SetRenderTarget(mask.Identifier, cameraDepth.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.ClearRenderTarget(false, true, Color.clear);
            cmd.DrawProcedural(Matrix4x4.identity, maskMaterial, MaskPassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceInitBurleyArgsPass : BurtRenderPass
    {
        private ComputeShader computeShader;
        private bool hasLoggedMissingComputeShader;
        private bool hasLoggedMissingKernel;

        public override string Name => "Burt Screen Space Subsurface Init Burley Args";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context.Request, context.Asset))
            {
                return;
            }

            var args = context.ScreenSpaceSubsurfaceBurleyArgsBuffer;
            if (!args.IsValid || !args.HasBuffer)
            {
                return;
            }

            var shader = GetComputeShader();
            if (shader == null || !BurtScreenSpaceSubsurfacePassUtility.TryFindComputeKernel(shader, "InitArgsBufferCS", ref hasLoggedMissingKernel, out var kernel))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetComputeBufferParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.BurleyArgsBufferId, args.Buffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private ComputeShader GetComputeShader()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetComputeShader(ref computeShader, ref hasLoggedMissingComputeShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceSetupPass : BurtRenderPass
    {
        private ComputeShader computeShader;
        private bool hasLoggedMissingComputeShader;
        private bool hasLoggedMissingKernel;

        public override string Name => "Burt Screen Space Subsurface Setup";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadCameraDepth();
            builder.ReadScreenSpaceSubsurfaceBaseColor();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadScreenSpaceSubsurfaceEmission();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(builder.Request, builder.Asset))
            {
                builder.ReadScreenSpaceSubsurfaceMask();
            }

            builder.WriteScreenSpaceSubsurfaceSetup();
            builder.WriteScreenSpaceSubsurfaceProfileIDAndType();
            builder.WriteScreenSpaceSubsurfaceTemp();
            builder.WriteScreenSpaceSubsurfaceBlur();
            if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                builder.ReadBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
                builder.WriteBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
                builder.WriteBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName);
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var source, out var cameraDepth, out var baseColor, out var gbuffer0, out var gbuffer1, out var emission, out var gbuffer3, out var gbuffer4, out var mask, out var setup, out var profileIDAndType, out var temp, out var blur, out var args, out var groups))
            {
                return;
            }

            var shader = GetComputeShader();
            var useBurley = BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context.Request, context.Asset);
            var kernelName = useBurley ? "SetupMainCS" : "SetupMainNoBurleyGroupsCS";
            if (shader == null || !BurtScreenSpaceSubsurfacePassUtility.TryFindComputeKernel(shader, kernelName, ref hasLoggedMissingKernel, out var kernel))
            {
                return;
            }

            var descriptor = BurtScreenSpaceSubsurfacePassUtility.CreateDescriptor(context);
            var groupsX = Mathf.CeilToInt(Mathf.Max(1, descriptor.width) / (float)BurtScreenSpaceSubsurfacePassUtility.TileThreadSize);
            var groupsY = Mathf.CeilToInt(Mathf.Max(1, descriptor.height) / (float)BurtScreenSpaceSubsurfacePassUtility.TileThreadSize);

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfaceHistoryUtility.GetCurrentHistoryTexture(context.Request, out var setupHistoryValid, out _);
            BurtScreenSpaceSubsurfacePassUtility.BindComputeCommonInputs(cmd, shader, kernel, context, source, cameraDepth, baseColor, gbuffer0, gbuffer1, emission, gbuffer3, gbuffer4, useBurley && !setupHistoryValid);
            BurtScreenSpaceSubsurfacePassUtility.BindComputeMaskInputs(cmd, shader, kernel, mask);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.SetupTextureId, setup.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.ProfileIDAndTypeRWTextureId, profileIDAndType.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.TempTextureId, temp.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.BlurTextureId, blur.Identifier);
            if (useBurley)
            {
                cmd.SetComputeBufferParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.BurleyArgsBufferId, args.Buffer);
                cmd.SetComputeBufferParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.BurleyGroupBufferId, groups.Buffer);
            }
            cmd.DispatchCompute(shader, kernel, groupsX, groupsY, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle source,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle baseColor,
            out BurtRenderTargetHandle gbuffer0,
            out BurtRenderTargetHandle gbuffer1,
            out BurtRenderTargetHandle emission,
            out BurtRenderTargetHandle gbuffer3,
            out BurtRenderTargetHandle gbuffer4,
            out BurtRenderTargetHandle mask,
            out BurtRenderTargetHandle setup,
            out BurtRenderTargetHandle profileIDAndType,
            out BurtRenderTargetHandle temp,
            out BurtRenderTargetHandle blur,
            out BurtRenderBufferHandle args,
            out BurtRenderBufferHandle groups)
        {
            source = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            baseColor = context != null ? context.ScreenSpaceSubsurfaceBaseColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            emission = context != null ? context.ScreenSpaceSubsurfaceEmissionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            mask = context != null ? context.ScreenSpaceSubsurfaceMaskTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
            setup = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
            profileIDAndType = context != null ? context.ScreenSpaceSubsurfaceProfileIDAndTypeTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
            temp = context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
            blur = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
            args = context != null ? context.ScreenSpaceSubsurfaceBurleyArgsBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
            groups = context != null ? context.ScreenSpaceSubsurfaceBurleyGroupBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                source.IsValid &&
                cameraDepth.IsValid &&
                baseColor.IsValid &&
                gbuffer0.IsValid &&
                gbuffer1.IsValid &&
                emission.IsValid &&
                gbuffer3.IsValid &&
                gbuffer4.IsValid &&
                BurtScreenSpaceSubsurfacePassUtility.IsMaskTargetReady(context, mask) &&
                setup.IsValid &&
                profileIDAndType.IsValid &&
                temp.IsValid &&
                blur.IsValid &&
                (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                    (args.IsValid &&
                        args.HasBuffer &&
                        groups.IsValid &&
                        groups.HasBuffer));
        }

        private ComputeShader GetComputeShader()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetComputeShader(ref computeShader, ref hasLoggedMissingComputeShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceBurleyPass : BurtRenderPass
    {
        private ComputeShader computeShader;
        private bool hasLoggedMissingComputeShader;
        private bool hasLoggedMissingKernel;

        public override string Name => "Burt Screen Space Subsurface Burley";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadScreenSpaceSubsurfaceSetup();
            builder.ReadScreenSpaceSubsurfaceProfileIDAndType();
            if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(builder.Request, builder.Asset))
            {
                builder.ReadScreenSpaceSubsurfaceMask();
            }

            builder.ReadScreenSpaceSubsurfaceVelocity();
            builder.ReadCameraDepth();
            builder.ReadScreenSpaceSubsurfaceBaseColor();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadScreenSpaceSubsurfaceEmission();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.ReadBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
            builder.ReadBuffer(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName);
            builder.WriteScreenSpaceSubsurfaceBlur();
            builder.WriteScreenSpaceSubsurfaceHistory();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var source, out var setup, out var profileIDAndType, out var mask, out var velocity, out var cameraDepth, out var baseColor, out var gbuffer0, out var gbuffer1, out var emission, out var gbuffer3, out var gbuffer4, out var blur, out var historyTarget, out var args, out var groups))
            {
                return;
            }

            var shader = GetComputeShader();
            if (shader == null || !BurtScreenSpaceSubsurfacePassUtility.TryFindComputeKernel(shader, "BurleySinglePass", ref hasLoggedMissingKernel, out var kernel))
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            var history = BurtScreenSpaceSubsurfaceHistoryUtility.EnsureHistoryTextures(context != null ? context.Request : null, context != null ? context.Asset : null, out var historyValid);
            if (BurtScreenSpaceSubsurfacePassUtility.IsDebuggingScreenSpaceSubsurface())
            {
                historyValid = false;
            }
            var temporalAA = context != null && context.Request != null ? context.Request.TemporalAA : null;
            var historyJitter = temporalAA != null && temporalAA.Enabled
                ? new Vector2(temporalAA.Jitter.x * 0.5f, temporalAA.Jitter.y * -0.5f)
                : Vector2.zero;
            cmd.SetRenderTarget(historyTarget.Identifier);
            cmd.ClearRenderTarget(false, true, Color.clear);
            BurtScreenSpaceSubsurfacePassUtility.BindComputeCommonInputs(cmd, shader, kernel, context, source, cameraDepth, baseColor, gbuffer0, gbuffer1, emission, gbuffer3, gbuffer4, !historyValid);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.SetupTextureId, setup.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.ProfileIDAndTypeTextureId, profileIDAndType.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.BindComputeMaskInputs(cmd, shader, kernel, mask);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.BlurTextureId, blur.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.VelocityTextureId, velocity.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.HistoryTextureId, historyValid && history.Input != null ? (Texture)history.Input : Texture2D.blackTexture);
            cmd.SetComputeTextureParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.HistoryOutputTextureId, historyTarget.Identifier);
            cmd.SetComputeVectorParam(shader, BurtScreenSpaceSubsurfacePassUtility.HistoryParamsId, new Vector4(historyValid ? 1f : 0f, BurtScreenSpaceSubsurfaceHistoryUtility.ExponentialWeight, historyJitter.x, historyJitter.y));
            cmd.SetComputeBufferParam(shader, kernel, BurtScreenSpaceSubsurfacePassUtility.BurleyGroupBufferId, groups.Buffer);
            cmd.DispatchCompute(shader, kernel, args.Buffer, 0);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle source,
            out BurtRenderTargetHandle setup,
            out BurtRenderTargetHandle profileIDAndType,
            out BurtRenderTargetHandle mask,
            out BurtRenderTargetHandle velocity,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle baseColor,
            out BurtRenderTargetHandle gbuffer0,
            out BurtRenderTargetHandle gbuffer1,
            out BurtRenderTargetHandle emission,
            out BurtRenderTargetHandle gbuffer3,
            out BurtRenderTargetHandle gbuffer4,
            out BurtRenderTargetHandle blur,
            out BurtRenderTargetHandle historyTarget,
            out BurtRenderBufferHandle args,
            out BurtRenderBufferHandle groups)
        {
            source = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            setup = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
            profileIDAndType = context != null ? context.ScreenSpaceSubsurfaceProfileIDAndTypeTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
            mask = context != null ? context.ScreenSpaceSubsurfaceMaskTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
            velocity = context != null ? context.ScreenSpaceSubsurfaceVelocityTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            baseColor = context != null ? context.ScreenSpaceSubsurfaceBaseColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            emission = context != null ? context.ScreenSpaceSubsurfaceEmissionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            blur = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
            historyTarget = context != null ? context.ScreenSpaceSubsurfaceHistoryTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceHistoryName);
            args = context != null ? context.ScreenSpaceSubsurfaceBurleyArgsBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyArgsBufferName);
            groups = context != null ? context.ScreenSpaceSubsurfaceBurleyGroupBuffer : BurtRenderBufferHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBurleyGroupBufferName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                source.IsValid &&
                setup.IsValid &&
                profileIDAndType.IsValid &&
                BurtScreenSpaceSubsurfacePassUtility.IsMaskTargetReady(context, mask) &&
                velocity.IsValid &&
                cameraDepth.IsValid &&
                baseColor.IsValid &&
                gbuffer0.IsValid &&
                gbuffer1.IsValid &&
                emission.IsValid &&
                gbuffer3.IsValid &&
                gbuffer4.IsValid &&
                blur.IsValid &&
                historyTarget.IsValid &&
                args.IsValid &&
                args.HasBuffer &&
                groups.IsValid &&
                groups.HasBuffer;
        }

        private ComputeShader GetComputeShader()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetComputeShader(ref computeShader, ref hasLoggedMissingComputeShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceSeparableHorizontalPass : BurtRenderPass
    {
        private const int HorizontalPassIndex = 1;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Separable Horizontal";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceSeparable(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSetup();
            builder.ReadScreenSpaceSubsurfaceProfileIDAndType();
            if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(builder.Request, builder.Asset))
            {
                builder.ReadScreenSpaceSubsurfaceMask();
            }

            builder.ReadScreenSpaceSubsurfaceBlur();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteScreenSpaceSubsurfaceTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var setup, out var profileIDAndType, out var mask, out var blur, out var cameraDepth, out var gbuffer0, out var gbuffer1, out var gbuffer2, out var gbuffer3, out var gbuffer4, out var temp))
            {
                return;
            }

            var separableMaterial = GetMaterial();
            if (separableMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindCommonInputs(cmd, context, blur, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            BurtScreenSpaceSubsurfacePassUtility.BindSetupInputs(cmd, context, setup, profileIDAndType, mask);
            cmd.SetRenderTarget(temp.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, separableMaterial, HorizontalPassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle setup,
            out BurtRenderTargetHandle profileIDAndType,
            out BurtRenderTargetHandle mask,
            out BurtRenderTargetHandle blur,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle gbuffer0,
            out BurtRenderTargetHandle gbuffer1,
            out BurtRenderTargetHandle gbuffer2,
            out BurtRenderTargetHandle gbuffer3,
            out BurtRenderTargetHandle gbuffer4,
            out BurtRenderTargetHandle temp)
        {
            setup = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
            profileIDAndType = context != null ? context.ScreenSpaceSubsurfaceProfileIDAndTypeTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
            mask = context != null ? context.ScreenSpaceSubsurfaceMaskTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
            blur = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2 = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            temp = context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceSeparable(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                setup.IsValid &&
                profileIDAndType.IsValid &&
                BurtScreenSpaceSubsurfacePassUtility.IsMaskTargetReady(context, mask) &&
                blur.IsValid &&
                cameraDepth.IsValid &&
                gbuffer0.IsValid &&
                gbuffer1.IsValid &&
                gbuffer2.IsValid &&
                gbuffer3.IsValid &&
                gbuffer4.IsValid &&
                temp.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceSeparableVerticalPass : BurtRenderPass
    {
        private const int VerticalPassIndex = 2;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Separable Vertical";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceSeparable(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSetup();
            builder.ReadScreenSpaceSubsurfaceProfileIDAndType();
            if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(builder.Request, builder.Asset))
            {
                builder.ReadScreenSpaceSubsurfaceMask();
            }

            builder.ReadScreenSpaceSubsurfaceTemp();
            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadScreenSpaceSubsurfaceBaseColor();
            builder.ReadScreenSpaceSubsurfaceEmission();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteScreenSpaceSubsurfaceBlur();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var setup, out var profileIDAndType, out var mask, out var temp, out var original, out var baseColor, out var emission, out var cameraDepth, out var gbuffer0, out var gbuffer1, out var gbuffer2, out var gbuffer3, out var gbuffer4, out var blur))
            {
                return;
            }

            var separableMaterial = GetMaterial();
            if (separableMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindCommonInputs(cmd, context, temp, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            BurtScreenSpaceSubsurfacePassUtility.BindSetupInputs(cmd, context, setup, profileIDAndType, mask);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.OriginalTextureId, original.Identifier);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.SourceTextureId, temp.Identifier);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.SeparableInputTextureId, temp.Identifier);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.BaseColorTextureId, baseColor.Identifier);
            cmd.SetGlobalTexture(BurtScreenSpaceSubsurfacePassUtility.EmissionTextureId, emission.Identifier);
            cmd.SetRenderTarget(blur.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, separableMaterial, VerticalPassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static bool TryGetTargets(
            BurtRenderGraphContext context,
            out BurtRenderTargetHandle setup,
            out BurtRenderTargetHandle profileIDAndType,
            out BurtRenderTargetHandle mask,
            out BurtRenderTargetHandle temp,
            out BurtRenderTargetHandle original,
            out BurtRenderTargetHandle baseColor,
            out BurtRenderTargetHandle emission,
            out BurtRenderTargetHandle cameraDepth,
            out BurtRenderTargetHandle gbuffer0,
            out BurtRenderTargetHandle gbuffer1,
            out BurtRenderTargetHandle gbuffer2,
            out BurtRenderTargetHandle gbuffer3,
            out BurtRenderTargetHandle gbuffer4,
            out BurtRenderTargetHandle blur)
        {
            setup = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
            profileIDAndType = context != null ? context.ScreenSpaceSubsurfaceProfileIDAndTypeTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
            mask = context != null ? context.ScreenSpaceSubsurfaceMaskTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskName);
            temp = context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);
            original = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            baseColor = context != null ? context.ScreenSpaceSubsurfaceBaseColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
            emission = context != null ? context.ScreenSpaceSubsurfaceEmissionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2 = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            blur = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceSeparable(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                setup.IsValid &&
                profileIDAndType.IsValid &&
                BurtScreenSpaceSubsurfacePassUtility.IsMaskTargetReady(context, mask) &&
                temp.IsValid &&
                original.IsValid &&
                baseColor.IsValid &&
                emission.IsValid &&
                cameraDepth.IsValid &&
                gbuffer0.IsValid &&
                gbuffer1.IsValid &&
                gbuffer2.IsValid &&
                gbuffer3.IsValid &&
                gbuffer4.IsValid &&
                blur.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceCombinePass : BurtRenderPass
    {
        private const int CombinePassIndex = 5;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Combine";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadScreenSpaceSubsurfaceBlur();
            builder.ReadScreenSpaceSubsurfaceSetup();
            builder.ReadScreenSpaceSubsurfaceProfileIDAndType();
            builder.ReadScreenSpaceSubsurfaceBaseColor();
            builder.ReadScreenSpaceSubsurfaceEmission();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadGBuffer3();
            builder.ReadGBuffer4();
            builder.WriteScreenSpaceSubsurfaceCombine();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var blur = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
            var original = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            var setup = context != null ? context.ScreenSpaceSubsurfaceSetupTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSetupName);
            var profileIDAndType = context != null ? context.ScreenSpaceSubsurfaceProfileIDAndTypeTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeName);
            var combine = context != null ? context.ScreenSpaceSubsurfaceCombineTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
            var cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            var baseColor = context != null ? context.ScreenSpaceSubsurfaceBaseColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorName);
            var emission = context != null ? context.ScreenSpaceSubsurfaceEmissionTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionName);
            var gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            var gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            var gbuffer2 = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            var gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            var gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !blur.IsValid ||
                !original.IsValid ||
                !setup.IsValid ||
                !profileIDAndType.IsValid ||
                !combine.IsValid ||
                !cameraDepth.IsValid ||
                !baseColor.IsValid ||
                !emission.IsValid ||
                !gbuffer0.IsValid ||
                !gbuffer1.IsValid ||
                !gbuffer2.IsValid ||
                !gbuffer3.IsValid ||
                !gbuffer4.IsValid)
            {
                return;
            }

            var combineMaterial = GetMaterial();
            if (combineMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindCombineInputs(cmd, context, original, setup, profileIDAndType, blur, cameraDepth, baseColor, emission, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            cmd.SetRenderTarget(combine.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, combineMaterial, CombinePassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceStoreHistoryPass : BurtRenderPass
    {
        public override string Name => "Burt Screen Space Subsurface Store History";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Copy;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceHistory();
            builder.WriteGlobalResource(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfacePersistentHistoryName);
            builder.AllowUnconsumedGlobalWrite(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfacePersistentHistoryName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var historyTarget = context != null ? context.ScreenSpaceSubsurfaceHistoryTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceHistoryName);
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !historyTarget.IsValid)
            {
                return;
            }

            var history = BurtScreenSpaceSubsurfaceHistoryUtility.GetPendingHistoryTextures(context.Request);
            if (history.Output == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.CopyTexture(historyTarget.Identifier, new RenderTargetIdentifier(history.Output));
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            BurtScreenSpaceSubsurfaceHistoryUtility.MarkHistoryValid(context.Request != null ? context.Request.Camera : null);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceFinalCopyPass : BurtRenderPass
    {
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Final Copy";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Copy;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceCombine();
            builder.ReadCameraDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var combine = context != null ? context.ScreenSpaceSubsurfaceCombineTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceCombineName);
            var cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            var target = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) ||
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

    internal sealed class BurtReleaseScreenSpaceSubsurfaceBaseColorPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Base Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceBaseColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBaseColorTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceEmissionPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Emission";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceEmission();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceEmissionTextureId);
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

    internal sealed class BurtReleaseScreenSpaceSubsurfaceProfileIDAndTypePass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Profile ID And Type";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceProfileIDAndType();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceProfileIDAndTypeTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceMaskPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Mask";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceMask();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceMaskTextureId);
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

    internal sealed class BurtReleaseScreenSpaceSubsurfaceHistoryPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface History";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceHistory();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceHistoryTextureId);
        }
    }

    internal sealed class BurtReleaseScreenSpaceSubsurfaceVelocityPass : BurtRenderPass
    {
        public override string Name => "Burt Release Screen Space Subsurface Velocity";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceVelocity();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceBurley(context.Request, context.Asset))
            {
                return;
            }

            BurtScreenSpaceSubsurfaceRenderTargetUtility.Release(context, Name, BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceVelocityTextureId);
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
            builder.ReadScreenSpaceSubsurfaceProfileIDAndType();
            if (BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurfaceMaskTexture(builder.Request, builder.Asset))
            {
                builder.ReadScreenSpaceSubsurfaceMask();
            }

            builder.ReadScreenSpaceSubsurfaceBlur();
            builder.ReadScreenSpaceSubsurfaceTemp();
            builder.ReadScreenSpaceSubsurfaceCombine();
            builder.ReadScreenSpaceSubsurfaceSource();
            builder.ReadCameraDepth();
            builder.ReadGBuffer0();
            builder.ReadScreenSpaceSubsurfaceBaseColor();
            builder.ReadGBuffer1();
            builder.ReadGBuffer2();
            builder.ReadScreenSpaceSubsurfaceEmission();
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
            var profileIDAndType = context.ScreenSpaceSubsurfaceProfileIDAndTypeTarget;
            var mask = context.ScreenSpaceSubsurfaceMaskTarget;
            var blur = context.ScreenSpaceSubsurfaceBlurTarget;
            var temp = context.ScreenSpaceSubsurfaceTempTarget;
            var combine = context.ScreenSpaceSubsurfaceCombineTarget;
            var source = context.ScreenSpaceSubsurfaceSourceTarget;
            var cameraDepth = context.CameraDepthTarget;
            var gbuffer0 = context.GBuffer0Target;
            var baseColor = context.ScreenSpaceSubsurfaceBaseColorTarget;
            var gbuffer1 = context.GBuffer1Target;
            var gbuffer2 = context.GBuffer2Target;
            var emission = context.ScreenSpaceSubsurfaceEmissionTarget;
            var gbuffer3 = context.GBuffer3Target;
            var gbuffer4 = context.GBuffer4Target;
            var cameraColor = context.CameraColorTarget;
            if (!setup.IsValid || !profileIDAndType.IsValid || !BurtScreenSpaceSubsurfacePassUtility.IsMaskTargetReady(context, mask) || !blur.IsValid || !temp.IsValid || !combine.IsValid || !source.IsValid || !cameraDepth.IsValid || !gbuffer0.IsValid || !baseColor.IsValid || !gbuffer1.IsValid || !gbuffer2.IsValid || !emission.IsValid || !gbuffer3.IsValid || !gbuffer4.IsValid || !cameraColor.IsValid)
            {
                return;
            }

            var debugMaterial = GetMaterial();
            if (debugMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BurtScreenSpaceSubsurfacePassUtility.BindDebugInputs(cmd, context, setup, profileIDAndType, mask, blur, temp, combine, source, cameraDepth, gbuffer0, baseColor, gbuffer1, gbuffer2, emission, gbuffer3, gbuffer4);
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
        public const string ScreenSpaceSubsurfaceComputeShaderResourcePath = "BurtScreenSpaceSubsurface";
        public const string PostProcessShaderName = "Hidden/BurtRP/PostProcessCopy";
        public const string MotionVectorShaderName = "Hidden/BurtRP/TemporalAAMotionVectors";
        public const int TileThreadSize = 8;
        public const int TemporalAACameraVelocityPassIndex = 6;
        public const int TemporalAAObjectMotionVectorPassIndex = 0;

        public static readonly int SourceTextureId = Shader.PropertyToID("_BurtSSSSourceTexture");
        public static readonly int SeparableInputTextureId = Shader.PropertyToID("_BurtSSSSeparableInputTexture");
        public static readonly int OriginalTextureId = Shader.PropertyToID("_BurtSSSOriginalTexture");
        public static readonly int SetupTextureId = Shader.PropertyToID("_BurtSSSSetupTexture");
        public static readonly int ProfileIDAndTypeTextureId = Shader.PropertyToID("_BurtSSSProfileIDAndTypeTexture");
        public static readonly int ProfileIDAndTypeRWTextureId = Shader.PropertyToID("_BurtSSSProfileIDAndTypeRWTexture");
        public static readonly int MaskTextureId = Shader.PropertyToID("_BurtSSSMaskTexture");
        public static readonly int BlurTextureId = Shader.PropertyToID("_BurtSSSBlurTexture");
        public static readonly int CombineTextureId = Shader.PropertyToID("_BurtSSSCombineTexture");
        public static readonly int TempTextureId = Shader.PropertyToID("_BurtSSSTempTexture");
        public static readonly int VelocityTextureId = Shader.PropertyToID("_BurtSSSVelocityTexture");
        public static readonly int BaseColorTextureId = Shader.PropertyToID("_BurtScreenSpaceSubsurfaceBaseColorTexture");
        public static readonly int EmissionTextureId = Shader.PropertyToID("_BurtScreenSpaceSubsurfaceEmissionTexture");
        public static readonly int BurleyArgsBufferId = Shader.PropertyToID("_BurtSSSBurleyArgsBuffer");
        public static readonly int BurleyGroupBufferId = Shader.PropertyToID("_BurtSSSBurleyGroupBuffer");
        public static readonly int HistoryTextureId = Shader.PropertyToID("_BurtSSSHistoryTexture");
        public static readonly int HistoryOutputTextureId = Shader.PropertyToID("_BurtSSSHistoryOutputTexture");
        public static readonly int HistoryParamsId = Shader.PropertyToID("_BurtSSSHistoryParams");
        public static readonly int TemporalAACurrentDepthTextureId = Shader.PropertyToID("_BurtTAACurrentDepthTexture");
        public static readonly int TemporalAAVelocityTextureId = Shader.PropertyToID("_BurtTAAVelocityTexture");
        public static readonly int TemporalAAPreviousViewProjectionId = Shader.PropertyToID("_BurtTAAPreviousViewProjection");
        public static readonly int TemporalAAPreviousNonJitteredViewProjectionId = Shader.PropertyToID("_BurtTAAPreviousNonJitteredViewProjection");
        public static readonly int TemporalAACurrentViewProjectionId = Shader.PropertyToID("_BurtTAACurrentViewProjection");
        public static readonly int TemporalAACurrentNonJitteredViewProjectionId = Shader.PropertyToID("_BurtTAACurrentNonJitteredViewProjection");
        public static readonly int TemporalAAInverseCurrentViewProjectionId = Shader.PropertyToID("_BurtTAAInverseCurrentViewProjection");
        public static readonly int TemporalAATexelSizeId = Shader.PropertyToID("_BurtTAATexelSize");

        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int ScreenSizeId = Shader.PropertyToID("_BurtSSSScreenSize");
        private static readonly int DebugModeId = Shader.PropertyToID("_BurtSSSDebugMode");
        private static readonly int DebugHistoryTextureId = Shader.PropertyToID("_BurtSSSHistoryDebugTexture");
        private static readonly int DebugHistoryParamsId = Shader.PropertyToID("_BurtSSSHistoryDebugParams");
        private static readonly int ParamsId = Shader.PropertyToID("_BurtSSSParams");
        private static readonly int Params2Id = Shader.PropertyToID("_BurtSSSParams2");
        private static readonly int ProjectionParamsId = Shader.PropertyToID("_BurtSSSProjectionParams");
        private static readonly int FrameParamsId = Shader.PropertyToID("_BurtSSSFrameParams");
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
        private static readonly int ComputeScreenSizeId = Shader.PropertyToID("_BurtSSSScreenSize");
        private static readonly int ComputeParamsId = Shader.PropertyToID("_BurtSSSParams");
        private static readonly int ComputeParams2Id = Shader.PropertyToID("_BurtSSSParams2");
        private static readonly int ComputeSurfaceAlbedoId = Shader.PropertyToID("_BurtSSSSurfaceAlbedo");
        private static readonly int ComputeMeanFreePathId = Shader.PropertyToID("_BurtSSSMeanFreePath");
        private static readonly int ComputeProfileTintId = Shader.PropertyToID("_BurtSSSProfileTint");
        private static readonly int ComputeBoundaryColorBleedId = Shader.PropertyToID("_BurtSSSBoundaryColorBleed");
        private static readonly int ComputeFrameParamsId = Shader.PropertyToID("_BurtSSSFrameParams");
        private static readonly int ComputeProjectionParamsId = Shader.PropertyToID("_BurtSSSProjectionParams");
        private static readonly int ComputeProfileCountId = Shader.PropertyToID("_BurtSSSProfileCount");
        private static readonly int ComputeProfileParamsId = Shader.PropertyToID("_BurtSSSProfileParams");
        private static readonly int ComputeProfileParams2Id = Shader.PropertyToID("_BurtSSSProfileParams2");
        private static readonly int ComputeProfileSurfaceAlbedosId = Shader.PropertyToID("_BurtSSSProfileSurfaceAlbedos");
        private static readonly int ComputeProfileMeanFreePathsId = Shader.PropertyToID("_BurtSSSProfileMeanFreePaths");
        private static readonly int ComputeProfileTintsId = Shader.PropertyToID("_BurtSSSProfileTints");
        private static readonly int ComputeProfileBoundaryColorBleedsId = Shader.PropertyToID("_BurtSSSProfileBoundaryColorBleeds");
        private static readonly int ComputeProfileTransmissionsId = Shader.PropertyToID("_BurtSSSProfileTransmissions");
        private static readonly int ComputeProfileTransmissionTintsId = Shader.PropertyToID("_BurtSSSProfileTransmissionTints");
        private static readonly int ComputeProfileParamLutId = BurtSubsurfaceProfileShaderUtility.ProfileParamLutId;
        private static readonly int ComputeProfileParamLutEnabledId = BurtSubsurfaceProfileShaderUtility.ProfileParamLutEnabledId;
        private static readonly int ComputeProfileParamLutSizeId = BurtSubsurfaceProfileShaderUtility.ProfileParamLutSizeId;
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
        private const int OpaqueRenderQueueMax = 2500;
        private const string SubsurfaceMaterialShaderName = "BurtRP/Subsurface";
        private const int SubsurfaceScatteringMode5SBurley = 0;
        private const int SubsurfaceScatteringMode4SSeparable = 1;
        private static readonly int SubsurfaceStrengthMaterialId = Shader.PropertyToID("_SubsurfaceStrength");
        private static readonly int SubsurfaceProfileIndexMaterialId = Shader.PropertyToID("_SubsurfaceProfileIndex");
        private static readonly int SubsurfaceScatteringModeMaterialId = Shader.PropertyToID("_SubsurfaceScatteringMode");
        private static readonly ShaderTagId LightModeTag = new ShaderTagId("LightMode");
        private static readonly ShaderTagId BurtGBufferLightMode = new ShaderTagId("BurtGBuffer");
        private static int materialGateFrame = -1;
        private static int materialGateCameraId;
        private static int materialGateAssetId;
        private static int materialGateCullingMask;
        private static MaterialGateResult materialGateResult = MaterialGateResult.Disabled("Uninitialized");

        public static bool ShouldUseScreenSpaceSubsurface(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (IsScreenSpaceSubsurfaceSuppressedByShadingDebug())
            {
                return false;
            }

            if (!ShouldUseScreenSpaceSubsurfaceBase(request, asset))
            {
                return false;
            }

            if (IsDebuggingScreenSpaceSubsurface())
            {
                return true;
            }

            return ResolveMaterialGate(request, asset).HasCandidate;
        }

        public static bool ShouldUseScreenSpaceSubsurfaceBurley(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (IsScreenSpaceSubsurfaceSuppressedByShadingDebug())
            {
                return false;
            }

            if (!ShouldUseScreenSpaceSubsurfaceBase(request, asset))
            {
                return false;
            }

            return ResolveMaterialGate(request, asset).HasBurleyCandidate;
        }

        public static bool ShouldUseScreenSpaceSubsurfaceSeparable(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (IsScreenSpaceSubsurfaceSuppressedByShadingDebug())
            {
                return false;
            }

            if (!ShouldUseScreenSpaceSubsurfaceBase(request, asset))
            {
                return false;
            }

            return ResolveMaterialGate(request, asset).HasSeparableCandidate;
        }

        public static bool HasScreenSpaceSubsurfaceBurleyMaterialCandidate(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ShouldUseScreenSpaceSubsurfaceBase(request, asset))
            {
                return false;
            }

            return ResolveMaterialGate(request, asset).HasBurleyCandidate;
        }

        public static bool HasScreenSpaceSubsurfaceSeparableMaterialCandidate(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ShouldUseScreenSpaceSubsurfaceBase(request, asset))
            {
                return false;
            }

            return ResolveMaterialGate(request, asset).HasSeparableCandidate;
        }

        public static string ResolveScreenSpaceSubsurfaceAlgorithmModeLabel(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ShouldUseScreenSpaceSubsurfaceBase(request, asset))
            {
                return "Disabled";
            }

            return ResolveMaterialGate(request, asset).AlgorithmModeLabel;
        }

        public static string ResolveScreenSpaceSubsurfaceMaterialGateDebugLabel(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ShouldUseScreenSpaceSubsurfaceBase(request, asset))
            {
                return "Disabled";
            }

            return ResolveMaterialGate(request, asset).DebugLabel;
        }

        public static bool IsScreenSpaceSubsurfaceSuppressedByShadingDebug()
        {
            return BurtShadingDebugSettings.IsDebugging &&
                !IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugSettings.Mode);
        }

        private static bool ShouldUseScreenSpaceSubsurfaceBase(BurtRenderRequest request, BurtRenderPipelineAsset asset)
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

        private static MaterialGateResult ResolveMaterialGate(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            var camera = request != null ? request.Camera : null;
            var frame = Time.frameCount;
            var cameraId = camera != null ? camera.GetInstanceID() : 0;
            var assetId = asset != null ? asset.GetInstanceID() : 0;
            var cullingMask = camera != null ? camera.cullingMask : 0;
            if (materialGateFrame == frame &&
                materialGateCameraId == cameraId &&
                materialGateAssetId == assetId &&
                materialGateCullingMask == cullingMask)
            {
                return materialGateResult;
            }

            materialGateFrame = frame;
            materialGateCameraId = cameraId;
            materialGateAssetId = assetId;
            materialGateCullingMask = cullingMask;
            materialGateResult = ScanVisibleSubsurfaceMaterials(camera, asset);
            return materialGateResult;
        }

        private static MaterialGateResult ScanVisibleSubsurfaceMaterials(Camera camera, BurtRenderPipelineAsset asset)
        {
            if (camera == null)
            {
                return MaterialGateResult.Disabled("NoCamera");
            }

            Renderer[] renderers;
            Plane[] frustumPlanes;
            try
            {
                renderers = FindActiveRenderers();
                frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            }
            catch
            {
                return MaterialGateResult.Active("ScanFailed");
            }

            var result = new MaterialGateResult();
            result.RendererCount = renderers != null ? renderers.Length : 0;
            if (renderers == null)
            {
                return result;
            }

            try
            {
                for (var rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    var renderer = renderers[rendererIndex];
                    if (!IsActiveRenderer(renderer) ||
                        !IsRendererInCameraLayer(renderer, camera))
                    {
                        continue;
                    }

                    result.LayerMatchedRendererCount++;
                    if (!IsRendererInFrustum(renderer, frustumPlanes))
                    {
                        continue;
                    }

                    result.FrustumMatchedRendererCount++;
                    if (RendererHasSubsurfaceMaterial(renderer, asset, ref result))
                    {
                        result.HasCandidate = true;
                    }
                }
            }
            catch
            {
                result.HasCandidate = true;
                result.Reason = "ScanFailed";
                return result;
            }

            return result;
        }

        private static Renderer[] FindActiveRenderers()
        {
#if UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<Renderer>();
#endif
        }

        private static bool IsActiveRenderer(Renderer renderer)
        {
            return renderer != null &&
                renderer.enabled &&
                renderer.gameObject != null &&
                renderer.gameObject.activeInHierarchy;
        }

        private static bool IsRendererInCameraLayer(Renderer renderer, Camera camera)
        {
            return renderer != null &&
                camera != null &&
                renderer.gameObject != null &&
                (camera.cullingMask & (1 << renderer.gameObject.layer)) != 0;
        }

        private static bool IsRendererInFrustum(Renderer renderer, Plane[] frustumPlanes)
        {
            if (renderer == null)
            {
                return false;
            }

            return frustumPlanes == null || frustumPlanes.Length == 0 || GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds);
        }

        private static bool RendererHasSubsurfaceMaterial(Renderer renderer, BurtRenderPipelineAsset asset, ref MaterialGateResult result)
        {
            var materials = renderer != null ? renderer.sharedMaterials : null;
            if (materials == null)
            {
                return false;
            }

            var hasCandidate = false;
            for (var materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                var material = materials[materialIndex];
                if (material == null)
                {
                    continue;
                }

                result.MaterialSlotCount++;
                if (!IsSubsurfaceMaterialCandidate(material, asset, out var scatteringMode))
                {
                    continue;
                }

                var isSeparable = scatteringMode == SubsurfaceScatteringMode4SSeparable;
                hasCandidate = true;
                result.HasCandidate = true;
                result.CandidateMaterialSlotCount++;
                if (isSeparable)
                {
                    result.SeparableMaterialSlotCount++;
                    result.HasSeparableCandidate = true;
                }
                else
                {
                    result.BurleyMaterialSlotCount++;
                    result.HasBurleyCandidate = true;
                }

                if (string.IsNullOrEmpty(result.FirstCandidateMaterialName))
                {
                    result.FirstCandidateMaterialName = material.name;
                    result.FirstCandidateShaderName = material.shader != null ? material.shader.name : "<none>";
                    result.FirstCandidateModeName = isSeparable ? "4S" : "5S";
                }
            }

            return hasCandidate;
        }

        private static bool IsSubsurfaceMaterialCandidate(Material material, BurtRenderPipelineAsset asset, out int scatteringMode)
        {
            scatteringMode = SubsurfaceScatteringMode5SBurley;
            if (material == null || material.shader == null)
            {
                return false;
            }

            if (!IsOpaqueMaterial(material) || !ShaderHasLightMode(material.shader, BurtGBufferLightMode))
            {
                return false;
            }

            var shaderLooksSubsurface = material.shader.name == SubsurfaceMaterialShaderName ||
                material.shader.name.IndexOf("Subsurface", System.StringComparison.OrdinalIgnoreCase) >= 0;
            if (!material.HasProperty(SubsurfaceStrengthMaterialId))
            {
                return shaderLooksSubsurface;
            }

            scatteringMode = ResolveSubsurfaceScatteringMode(material);
            if (scatteringMode == SubsurfaceScatteringMode4SSeparable)
            {
                return material.GetFloat(SubsurfaceStrengthMaterialId) > 0f;
            }

            var profileIndex = material.HasProperty(SubsurfaceProfileIndexMaterialId)
                ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(SubsurfaceProfileIndexMaterialId)), 0, BurtSubsurfaceProfilePalette.MaxProfiles - 1)
                : 0;
            var minStrength = ResolveMaterialProfileMinStrength(asset, profileIndex);
            return material.GetFloat(SubsurfaceStrengthMaterialId) > minStrength;
        }

        private static int ResolveSubsurfaceScatteringMode(Material material)
        {
            if (material == null || !material.HasProperty(SubsurfaceScatteringModeMaterialId))
            {
                return SubsurfaceScatteringMode5SBurley;
            }

            return Mathf.Clamp(Mathf.RoundToInt(material.GetFloat(SubsurfaceScatteringModeMaterialId)), SubsurfaceScatteringMode5SBurley, SubsurfaceScatteringMode4SSeparable);
        }

        private static float ResolveMaterialProfileMinStrength(BurtRenderPipelineAsset asset, int profileIndex)
        {
            var palette = asset != null
                ? asset.ScreenSpaceSubsurfaceProfilePalette
                : BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null);
            var count = Mathf.Clamp(palette.Count, 1, BurtSubsurfaceProfilePalette.MaxProfiles);
            var safeIndex = profileIndex >= 0 && profileIndex < count ? profileIndex : 0;
            return palette.GetSettings(safeIndex).MinStrength;
        }

        private static bool IsOpaqueMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            var renderQueue = material.renderQueue;
            if (renderQueue >= 0)
            {
                return renderQueue <= OpaqueRenderQueueMax;
            }

            var queueTag = material.GetTag("Queue", true, "Geometry");
            return !queueTag.StartsWith("Transparent") && !queueTag.StartsWith("Overlay");
        }

        private static bool ShaderHasLightMode(Shader shader, ShaderTagId expectedLightMode)
        {
            if (shader == null)
            {
                return false;
            }

            for (var passIndex = 0; passIndex < shader.passCount; passIndex++)
            {
                var lightMode = shader.FindPassTagValue(passIndex, LightModeTag);
                if (lightMode.Equals(expectedLightMode))
                {
                    return true;
                }
            }

            return false;
        }

        private struct MaterialGateResult
        {
            public bool HasCandidate;
            public string Reason;
            public int RendererCount;
            public int LayerMatchedRendererCount;
            public int FrustumMatchedRendererCount;
            public int MaterialSlotCount;
            public int CandidateMaterialSlotCount;
            public int BurleyMaterialSlotCount;
            public int SeparableMaterialSlotCount;
            public bool HasBurleyCandidate;
            public bool HasSeparableCandidate;
            public string FirstCandidateMaterialName;
            public string FirstCandidateShaderName;
            public string FirstCandidateModeName;

            public string AlgorithmModeLabel
            {
                get
                {
                    if (!HasCandidate)
                    {
                        return "None";
                    }

                    if (HasBurleyCandidate && HasSeparableCandidate)
                    {
                        return "Mixed4S5S";
                    }

                    return HasSeparableCandidate ? "4SSeparable" : "5SBurley";
                }
            }

            public string DebugLabel
            {
                get
                {
                    var state = HasCandidate ? "Active" : "Skipped";
                    var reason = string.IsNullOrEmpty(Reason) ? (HasCandidate ? "CandidateMaterial" : "NoCandidateMaterial") : Reason;
                    var label = state +
                        "(" + reason +
                        ",renderers=" + RendererCount +
                        ",layer=" + LayerMatchedRendererCount +
                        ",frustum=" + FrustumMatchedRendererCount +
                        ",slots=" + MaterialSlotCount +
                        ",candidates=" + CandidateMaterialSlotCount +
                        ",5s=" + BurleyMaterialSlotCount +
                        ",4s=" + SeparableMaterialSlotCount +
                        ",mode=" + AlgorithmModeLabel;
                    if (!string.IsNullOrEmpty(FirstCandidateMaterialName))
                    {
                        label += ",first=" + FirstCandidateMaterialName + ",shader=" + FirstCandidateShaderName + ",firstMode=" + FirstCandidateModeName;
                    }

                    return label + ")";
                }
            }

            public static MaterialGateResult Active(string reason)
            {
                return new MaterialGateResult
                {
                    HasCandidate = true,
                    HasBurleyCandidate = true,
                    HasSeparableCandidate = true,
                    Reason = reason
                };
            }

            public static MaterialGateResult Disabled(string reason)
            {
                return new MaterialGateResult
                {
                    HasCandidate = false,
                    Reason = reason
                };
            }
        }

        public static bool ShouldUseScreenSpaceSubsurfaceDebugView(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            return ShouldUseScreenSpaceSubsurface(request, asset) && IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool ShouldUseScreenSpaceSubsurfaceMaskTexture(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (!ShouldUseScreenSpaceSubsurface(request, asset))
            {
                return false;
            }

            return !ShouldUseStencilTexture(request) ||
                BurtShadingDebugSettings.Mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceMask;
        }

        public static bool ShouldUseStencilTexture(BurtRenderRequest request)
        {
            return false;
        }

        public static bool IsMaskTargetReady(BurtRenderGraphContext context, BurtRenderTargetHandle mask)
        {
            return !ShouldUseScreenSpaceSubsurfaceMaskTexture(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                mask.IsValid;
        }

        public static bool IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSetup ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceMask ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceCoarseMask ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBlur ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceCombine ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceThickness ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileIndex ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTint ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintRaw ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileSurfaceAlbedo ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileMeanFreePath ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedLighting ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedFinal ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurAlpha ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurRadius ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableSampleGate ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableHorizontalDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurNormalized ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurSignedDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSetupDiffuse ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableHorizontal ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableHorizontalDepth ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceXRenderCombineFactors ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileKernel ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSSSColorDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileKernelColor ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceFinalDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceFinalDiffuseDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSSSColorSignedDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableValidity ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableIO ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableStages ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableChain ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceXRenderCombineTriplet ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceAlgorithm ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceTransmission ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceDiffuse ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSpecular ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSourceColor ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSourceAlpha ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceBaseColor ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceEmission ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceDiffuseWithBaseColor ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSpecularRaw ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceCombineDelta ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceStability ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceSampleCount ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceVariance ||
                mode == BurtShadingDebugMode.ScreenSpaceSubsurfaceHistory;
        }

        public static int ResolveScreenSpaceSubsurfaceShaderDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSetup:
                    return 1;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceMask:
                    return 2;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceCoarseMask:
                    return 3;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlur:
                    return 4;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceCombine:
                    return 5;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceThickness:
                    return 6;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileIndex:
                    return 7;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTint:
                    return 23;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintRaw:
                    return 44;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileSurfaceAlbedo:
                    return 24;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileMeanFreePath:
                    return 25;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedLighting:
                    return 26;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedFinal:
                    return 27;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurAlpha:
                    return 28;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurRadius:
                    return 29;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurDelta:
                    return 30;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableSampleGate:
                    return 31;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableHorizontalDelta:
                    return 32;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurNormalized:
                    return 33;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBlurSignedDelta:
                    return 34;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSetupDiffuse:
                    return 35;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableHorizontal:
                    return 36;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableHorizontalDepth:
                    return 37;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceXRenderCombineFactors:
                    return 38;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileKernel:
                    return 39;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSSSColorDelta:
                    return 40;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileKernelColor:
                    return 41;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceFinalDelta:
                    return 42;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceFinalDiffuseDelta:
                    return 43;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSSSColorSignedDelta:
                    return 45;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceProfileTintedDelta:
                    return 46;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableValidity:
                    return 47;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableIO:
                    return 48;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableStages:
                    return 49;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSeparableChain:
                    return 50;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceXRenderCombineTriplet:
                    return 51;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceAlgorithm:
                    return 15;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceTransmission:
                    return 8;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceDiffuse:
                    return 9;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSpecular:
                    return 10;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSourceColor:
                    return 16;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSourceAlpha:
                    return 17;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceBaseColor:
                    return 18;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceEmission:
                    return 19;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceDiffuseWithBaseColor:
                    return 20;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSpecularRaw:
                    return 21;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceCombineDelta:
                    return 22;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceStability:
                    return 11;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceSampleCount:
                    return 12;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceVariance:
                    return 13;
                case BurtShadingDebugMode.ScreenSpaceSubsurfaceHistory:
                    return 14;
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

        public static ComputeShader GetComputeShader(ref ComputeShader shader, ref bool hasLoggedMissingComputeShader)
        {
            if (shader != null)
            {
                return shader;
            }

            shader = Resources.Load<ComputeShader>(ScreenSpaceSubsurfaceComputeShaderResourcePath);
            if (shader == null && !hasLoggedMissingComputeShader)
            {
                Debug.LogWarning("BurtRP could not find compute shader resource: " + ScreenSpaceSubsurfaceComputeShaderResourcePath);
                hasLoggedMissingComputeShader = true;
            }

            return shader;
        }

        public static bool TryFindComputeKernel(ComputeShader shader, string kernelName, ref bool hasLoggedMissingKernel, out int kernel)
        {
            kernel = -1;
            if (shader == null || string.IsNullOrEmpty(kernelName))
            {
                return false;
            }

            if (!shader.HasKernel(kernelName))
            {
                if (!hasLoggedMissingKernel)
                {
                    Debug.LogWarning("BurtRP compute shader missing kernel: " + kernelName);
                    hasLoggedMissingKernel = true;
                }

                return false;
            }

            kernel = shader.FindKernel(kernelName);
            return kernel >= 0;
        }

        public static Material GetMaterial(ref Material material, ref bool hasLoggedMissingShader)
        {
            return GetMaterial(ref material, ScreenSpaceSubsurfaceShaderName, ref hasLoggedMissingShader);
        }

        public static Material GetMaterial(ref Material material, string shaderName, ref bool hasLoggedMissingShader)
        {
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + shaderName);
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
            cmd.SetGlobalTexture(SeparableInputTextureId, source.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepth.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4.Identifier);
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalVector(ProjectionParamsId, ResolveProjectionParams(context != null ? context.Request : null));
            var stableSampling = ShouldUseStableScreenSpaceSubsurfaceSampling(context);
            var debugSampling = IsDebuggingScreenSpaceSubsurface();
            cmd.SetGlobalVector(FrameParamsId, new Vector4(Time.frameCount, Time.frameCount & 1023, stableSampling ? 1f : 0f, debugSampling ? 1f : 0f));
            var profileSettings = ResolveProfileSettings(context != null ? context.Asset : null);
            cmd.SetGlobalVector(ParamsId, profileSettings.Params);
            cmd.SetGlobalVector(Params2Id, profileSettings.Params2);
            cmd.SetGlobalVector(SurfaceAlbedoId, profileSettings.SurfaceAlbedoVector);
            cmd.SetGlobalVector(MeanFreePathId, profileSettings.MeanFreePathVector);
            cmd.SetGlobalVector(ProfileTintId, profileSettings.TintVector);
            cmd.SetGlobalVector(BoundaryColorBleedId, profileSettings.BoundaryColorBleedVector);
            BindProfilePalette(cmd, context != null ? context.Asset : null);
        }

        public static void BindComputeCommonInputs(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle source,
            BurtRenderTargetHandle cameraDepth,
            BurtRenderTargetHandle baseColor,
            BurtRenderTargetHandle gbuffer0,
            BurtRenderTargetHandle gbuffer1,
            BurtRenderTargetHandle emission,
            BurtRenderTargetHandle gbuffer3,
            BurtRenderTargetHandle gbuffer4,
            bool forceStableSampling = false)
        {
            var descriptor = CreateDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);

            cmd.SetComputeTextureParam(shader, kernel, SourceTextureId, source.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, CameraDepthTextureId, cameraDepth.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, GBuffer0Id, gbuffer0.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, GBuffer1Id, gbuffer1.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, GBuffer3Id, gbuffer3.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, GBuffer4Id, gbuffer4.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, BaseColorTextureId, baseColor.Identifier);
            cmd.SetComputeTextureParam(shader, kernel, EmissionTextureId, emission.Identifier);
            cmd.SetComputeVectorParam(shader, ComputeScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));

            var profileSettings = ResolveProfileSettings(context != null ? context.Asset : null);
            cmd.SetComputeVectorParam(shader, ComputeParamsId, profileSettings.Params);
            cmd.SetComputeVectorParam(shader, ComputeParams2Id, profileSettings.Params2);
            cmd.SetComputeVectorParam(shader, ComputeSurfaceAlbedoId, profileSettings.SurfaceAlbedoVector);
            cmd.SetComputeVectorParam(shader, ComputeMeanFreePathId, profileSettings.MeanFreePathVector);
            cmd.SetComputeVectorParam(shader, ComputeProfileTintId, profileSettings.TintVector);
            cmd.SetComputeVectorParam(shader, ComputeBoundaryColorBleedId, profileSettings.BoundaryColorBleedVector);
            var stableSampling = forceStableSampling || ShouldUseStableScreenSpaceSubsurfaceSampling(context);
            var debugSampling = IsDebuggingScreenSpaceSubsurface();
            cmd.SetComputeVectorParam(shader, ComputeFrameParamsId, new Vector4(Time.frameCount, Time.frameCount & 1023, stableSampling ? 1f : 0f, debugSampling ? 1f : 0f));
            cmd.SetComputeVectorParam(shader, ComputeProjectionParamsId, ResolveProjectionParams(context != null ? context.Request : null));
            BindComputeProfilePalette(cmd, shader, kernel, context != null ? context.Asset : null);
        }

        public static bool ShouldUseStableScreenSpaceSubsurfaceSampling(BurtRenderGraphContext context)
        {
            if (IsDebuggingScreenSpaceSubsurface())
            {
                return true;
            }

            var temporalAA = context != null && context.Request != null ? context.Request.TemporalAA : null;
            return temporalAA == null || !temporalAA.Enabled || !temporalAA.HistoryValid || temporalAA.VelocityMode == BurtTemporalAAVelocityMode.Disabled;
        }

        public static bool IsDebuggingScreenSpaceSubsurface()
        {
            return BurtShadingDebugSettings.IsDebugging && IsScreenSpaceSubsurfaceDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static void BindComputeMaskInputs(
            CommandBuffer cmd,
            ComputeShader shader,
            int kernel,
            BurtRenderTargetHandle mask)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            if (mask.IsValid)
            {
                cmd.SetComputeTextureParam(shader, kernel, MaskTextureId, mask.Identifier);
            }
            else
            {
                cmd.SetComputeTextureParam(shader, kernel, MaskTextureId, (Texture)Texture2D.whiteTexture);
            }
        }

        private static Vector4 ResolveProjectionParams(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            var temporalAA = request != null ? request.TemporalAA : null;
            var projection = temporalAA != null && temporalAA.Enabled
                ? temporalAA.NonJitteredProjectionMatrix
                : camera != null ? camera.projectionMatrix : Matrix4x4.identity;
            const float kernelSize = 3f;
            var projectionM00 = Mathf.Abs(projection.m00);
            var projectionScale = projectionM00 / kernelSize * 0.5f;
            return new Vector4(Mathf.Max(0.0001f, projectionScale), projectionM00, kernelSize, 0f);
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

        public static void BindMaskInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle baseColor,
            BurtRenderTargetHandle gbuffer1)
        {
            var descriptor = CreateDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);

            cmd.SetGlobalTexture(BaseColorTextureId, baseColor.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1.Identifier);
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            BindProfilePalette(cmd, context != null ? context.Asset : null);
        }

        public static void BindSetupInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle setup,
            BurtRenderTargetHandle profileIDAndType,
            BurtRenderTargetHandle mask)
        {
            var descriptor = CreateDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            cmd.SetGlobalTexture(SetupTextureId, setup.Identifier);
            cmd.SetGlobalTexture(ProfileIDAndTypeTextureId, profileIDAndType.Identifier);
            if (mask.IsValid)
            {
                cmd.SetGlobalTexture(MaskTextureId, mask.Identifier);
            }
            else
            {
                cmd.SetGlobalTexture(MaskTextureId, (Texture)Texture2D.whiteTexture);
            }

            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
        }

        public static void BindCombineInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle source,
            BurtRenderTargetHandle setup,
            BurtRenderTargetHandle profileIDAndType,
            BurtRenderTargetHandle blur,
            BurtRenderTargetHandle cameraDepth,
            BurtRenderTargetHandle baseColor,
            BurtRenderTargetHandle emission,
            BurtRenderTargetHandle gbuffer0,
            BurtRenderTargetHandle gbuffer1,
            BurtRenderTargetHandle gbuffer2,
            BurtRenderTargetHandle gbuffer3,
            BurtRenderTargetHandle gbuffer4)
        {
            var descriptor = CreateDescriptor(context);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            cmd.SetGlobalTexture(SetupTextureId, setup.Identifier);
            cmd.SetGlobalTexture(ProfileIDAndTypeTextureId, profileIDAndType.Identifier);
            cmd.SetGlobalVector(ScreenSizeId, new Vector4(width, height, 1f / width, 1f / height));
            cmd.SetGlobalTexture(SourceTextureId, source.Identifier);
            cmd.SetGlobalTexture(OriginalTextureId, source.Identifier);
            cmd.SetGlobalTexture(SeparableInputTextureId, source.Identifier);
            cmd.SetGlobalTexture(BlurTextureId, blur.Identifier);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepth.Identifier);
            cmd.SetGlobalTexture(BaseColorTextureId, baseColor.Identifier);
            cmd.SetGlobalTexture(EmissionTextureId, emission.Identifier);
            cmd.SetGlobalTexture(GBuffer0Id, gbuffer0.Identifier);
            cmd.SetGlobalTexture(GBuffer1Id, gbuffer1.Identifier);
            cmd.SetGlobalTexture(GBuffer2Id, gbuffer2.Identifier);
            cmd.SetGlobalTexture(GBuffer3Id, gbuffer3.Identifier);
            cmd.SetGlobalTexture(GBuffer4Id, gbuffer4.Identifier);
            BindProfilePalette(cmd, context != null ? context.Asset : null);
        }

        public static void BindDebugInputs(
            CommandBuffer cmd,
            BurtRenderGraphContext context,
            BurtRenderTargetHandle setup,
            BurtRenderTargetHandle profileIDAndType,
            BurtRenderTargetHandle mask,
            BurtRenderTargetHandle blur,
            BurtRenderTargetHandle temp,
            BurtRenderTargetHandle combine,
            BurtRenderTargetHandle original,
            BurtRenderTargetHandle cameraDepth,
            BurtRenderTargetHandle gbuffer0,
            BurtRenderTargetHandle baseColor,
            BurtRenderTargetHandle gbuffer1,
            BurtRenderTargetHandle gbuffer2,
            BurtRenderTargetHandle emission,
            BurtRenderTargetHandle gbuffer3,
            BurtRenderTargetHandle gbuffer4)
        {
            BindSetupInputs(cmd, context, setup, profileIDAndType, mask);
            BindGBufferInputs(cmd, context, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            cmd.SetGlobalTexture(OriginalTextureId, original.Identifier);
            cmd.SetGlobalTexture(SourceTextureId, original.Identifier);
            cmd.SetGlobalTexture(SeparableInputTextureId, temp.Identifier);
            cmd.SetGlobalTexture(BlurTextureId, blur.Identifier);
            cmd.SetGlobalTexture(TempTextureId, temp.Identifier);
            cmd.SetGlobalTexture(CombineTextureId, combine.Identifier);
            cmd.SetGlobalTexture(BaseColorTextureId, baseColor.Identifier);
            cmd.SetGlobalTexture(EmissionTextureId, emission.Identifier);
            var history = BurtScreenSpaceSubsurfaceHistoryUtility.GetCurrentHistoryTexture(context != null ? context.Request : null, out var historyValid, out var historyAge);
            cmd.SetGlobalTexture(DebugHistoryTextureId, history != null ? (Texture)history : Texture2D.blackTexture);
            cmd.SetGlobalVector(DebugHistoryParamsId, new Vector4(historyValid ? 1f : 0f, historyAge, BurtScreenSpaceSubsurfaceHistoryUtility.MaxBurleySampleCount, BurtScreenSpaceSubsurfaceHistoryUtility.HistoryVarianceTarget));
            cmd.SetGlobalFloat(DebugModeId, ResolveScreenSpaceSubsurfaceShaderDebugMode());
        }

        public static void SetViewport(CommandBuffer cmd, BurtRenderGraphContext context)
        {
            var descriptor = CreateDescriptor(context);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
        }

        public static RenderTextureDescriptor CreateDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceColorDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateSourceDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceSourceDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateBaseColorDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceBaseColorDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateEmissionDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceEmissionDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateComputeColorDescriptor(BurtRenderGraphContext context)
        {
            var descriptor = CreateDescriptor(context);
            ApplySeparableIntermediateFormat(context, ref descriptor);
            descriptor.enableRandomWrite = true;
            return descriptor;
        }

        public static RenderTextureDescriptor CreateSetupDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceSetupDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateProfileIDAndTypeDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceProfileIDAndTypeDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateMaskDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceMaskDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateCombineDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceCombineDescriptor(camera);
            ApplySeparableIntermediateFormat(context, ref descriptor);
            return descriptor;
        }

        public static RenderTextureDescriptor CreateVelocityDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceVelocityDescriptor(camera);
        }

        public static RenderTextureDescriptor CreateHistoryDescriptor(BurtRenderGraphContext context)
        {
            var camera = context != null && context.Request != null ? context.Request.Camera : null;
            return BurtScreenSpaceSubsurfaceHistoryUtility.CreateHistoryDescriptor(camera);
        }

        public static void SetTemporalAAVelocityGlobals(CommandBuffer cmd, BurtTemporalAARequestState temporalAA, int width, int height)
        {
            if (cmd == null || temporalAA == null)
            {
                return;
            }

            cmd.SetGlobalMatrix(TemporalAAPreviousViewProjectionId, temporalAA.PreviousViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAAPreviousNonJitteredViewProjectionId, temporalAA.PreviousNonJitteredViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAACurrentViewProjectionId, temporalAA.CurrentViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAACurrentNonJitteredViewProjectionId, temporalAA.CurrentNonJitteredViewProjectionMatrix);
            cmd.SetGlobalMatrix(TemporalAAInverseCurrentViewProjectionId, temporalAA.InverseCurrentViewProjectionMatrix);
            cmd.SetGlobalVector(TemporalAATexelSizeId, new Vector4(1f / Mathf.Max(1, width), 1f / Mathf.Max(1, height), width, height));
        }

        public static bool DrawObjectMotionVectors(
            BurtRenderGraphContext context,
            CommandBuffer cmd,
            Camera camera,
            RenderTargetIdentifier velocityTarget,
            BurtRenderTargetHandle cameraDepthTarget,
            Material motionVectorMaterial)
        {
            if (context == null || context.Request == null || cmd == null || camera == null || motionVectorMaterial == null || !cameraDepthTarget.IsValid)
            {
                return false;
            }

            cmd.SetRenderTarget(velocityTarget, cameraDepthTarget.Identifier);
            SetViewport(cmd, context);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            var sortingSettings = new SortingSettings(camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(new ShaderTagId("BurtGBuffer"), sortingSettings)
            {
                overrideMaterial = motionVectorMaterial,
                overrideMaterialPassIndex = TemporalAAObjectMotionVectorPassIndex,
                perObjectData = PerObjectData.MotionVectors,
                enableDynamicBatching = false,
                enableInstancing = false
            };
            drawingSettings.SetShaderPassName(1, new ShaderTagId("BurtForward"));
            drawingSettings.SetShaderPassName(2, new ShaderTagId("BurtForwardOnly"));
            drawingSettings.SetShaderPassName(3, new ShaderTagId("SRPDefaultUnlit"));

            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, camera.cullingMask);
            context.ScriptableContext.DrawRenderers(context.Request.CullingResults, ref drawingSettings, ref filteringSettings);
            return true;
        }

        public static BurtRenderBufferDescriptor CreateBurleyArgsBufferDescriptor()
        {
            return new BurtRenderBufferDescriptor(
                3,
                sizeof(uint),
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured,
                "_BurtSSSBurleyArgsBuffer");
        }

        public static BurtRenderBufferDescriptor CreateBurleyGroupBufferDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            var groupCountX = Mathf.CeilToInt(Mathf.Max(1, descriptor.width) / (float)TileThreadSize);
            var groupCountY = Mathf.CeilToInt(Mathf.Max(1, descriptor.height) / (float)TileThreadSize);
            var groupCount = Mathf.Max(1, groupCountX * groupCountY);
            return new BurtRenderBufferDescriptor(
                groupCount * 2,
                sizeof(uint),
                GraphicsBuffer.Target.Structured,
                "_BurtSSSBurleyGroupBuffer");
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
                ProfileTints[i] = CreateProfileTint(profile);
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
            cmd.SetGlobalTexture(ProfileParamLutId, profileParamLut != null ? profileParamLut : BurtSubsurfaceLutUtility.GetFallbackProfileParamLut());
            cmd.SetGlobalFloat(ProfileParamLutEnabledId, profileParamLut != null ? 1f : 0f);
            cmd.SetGlobalVector(ProfileParamLutSizeId, BurtSubsurfaceLutUtility.ProfileParamLutSizeVector);
        }

        private static void BindComputeProfilePalette(CommandBuffer cmd, ComputeShader shader, int kernel, BurtRenderPipelineAsset asset)
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
                ProfileMeanFreePaths[i] = CreateComputeProfileMeanFreePath(profile);
                ProfileTints[i] = CreateProfileTint(profile);
                ProfileBoundaryColorBleeds[i] = profile.BoundaryColorBleedVector;
                ProfileTransmissions[i] = profile.TransmissionVector;
                ProfileTransmissionTints[i] = profile.TransmissionTintVector;
            }

            cmd.SetComputeFloatParam(shader, ComputeProfileCountId, count);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileParamsId, ProfileParams);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileParams2Id, ProfileParams2);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileSurfaceAlbedosId, ProfileSurfaceAlbedos);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileMeanFreePathsId, ProfileMeanFreePaths);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileTintsId, ProfileTints);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileBoundaryColorBleedsId, ProfileBoundaryColorBleeds);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileTransmissionsId, ProfileTransmissions);
            cmd.SetComputeVectorArrayParam(shader, ComputeProfileTransmissionTintsId, ProfileTransmissionTints);

            var profileParamLut = BurtSubsurfaceLutUtility.GetOrCreateProfileParamLut(palette);
            cmd.SetComputeTextureParam(shader, kernel, ComputeProfileParamLutId, profileParamLut != null ? profileParamLut : BurtSubsurfaceLutUtility.GetFallbackProfileParamLut());

            cmd.SetComputeFloatParam(shader, ComputeProfileParamLutEnabledId, profileParamLut != null ? 1f : 0f);
            cmd.SetComputeVectorParam(shader, ComputeProfileParamLutSizeId, BurtSubsurfaceLutUtility.ProfileParamLutSizeVector);
        }

        private static Vector4 CreateComputeProfileMeanFreePath(BurtSubsurfaceProfileSettings profile)
        {
            var effectiveDiffuseMeanFreePath = BurtSubsurfaceLutUtility.GetEffectiveDiffuseMeanFreePathForLut(profile);
            var decodeScale = 1f / Mathf.Max(profile.WorldUnitScale * 0.1f, 0.0001f);
            var diffuseMeanFreePath = effectiveDiffuseMeanFreePath * decodeScale;
            var dominant = Mathf.Max(diffuseMeanFreePath.x, Mathf.Max(diffuseMeanFreePath.y, diffuseMeanFreePath.z));
            return new Vector4(
                Mathf.Max(0.01f, diffuseMeanFreePath.x),
                Mathf.Max(0.01f, diffuseMeanFreePath.y),
                Mathf.Max(0.01f, diffuseMeanFreePath.z),
                Mathf.Max(0.01f, dominant));
        }

        private static Vector4 CreateProfileTint(BurtSubsurfaceProfileSettings profile)
        {
            return new Vector4(
                Mathf.Clamp01(profile.Tint.r),
                Mathf.Clamp01(profile.Tint.g),
                Mathf.Clamp01(profile.Tint.b),
                Mathf.Max(0.01f, profile.WorldUnitScale));
        }

        private static void ApplySeparableIntermediateFormat(BurtRenderGraphContext context, ref RenderTextureDescriptor descriptor)
        {
            if (!ShouldUseScreenSpaceSubsurfaceSeparable(context != null ? context.Request : null, context != null ? context.Asset : null))
            {
                return;
            }

            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            descriptor.sRGB = false;
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

    internal readonly struct BurtScreenSpaceSubsurfaceHistoryTextures
    {
        public RenderTexture Input { get; }
        public RenderTexture Output { get; }
        public Matrix4x4 PreviousViewProjectionMatrix { get; }
        public Matrix4x4 CurrentInverseViewProjectionMatrix { get; }

        public BurtScreenSpaceSubsurfaceHistoryTextures(
            RenderTexture input,
            RenderTexture output,
            Matrix4x4 previousViewProjectionMatrix,
            Matrix4x4 currentInverseViewProjectionMatrix)
        {
            Input = input;
            Output = output;
            PreviousViewProjectionMatrix = previousViewProjectionMatrix;
            CurrentInverseViewProjectionMatrix = currentInverseViewProjectionMatrix;
        }

        public static BurtScreenSpaceSubsurfaceHistoryTextures CreateInvalid(Matrix4x4 viewProjectionMatrix, Matrix4x4 inverseViewProjectionMatrix)
        {
            return new BurtScreenSpaceSubsurfaceHistoryTextures(null, null, viewProjectionMatrix, inverseViewProjectionMatrix);
        }
    }

    internal static class BurtScreenSpaceSubsurfaceHistoryUtility
    {
        public const float ExponentialWeight = 0.12f;
        public const float HistoryVarianceTarget = 0.0001f;
        public const int MaxBurleySampleCount = 64;

        private const int HistoryAlgorithmVersion = 8;
        private const int CameraStatePruneInterval = 128;
        private const float ProjectionChangeEpsilon = 0.0001f;
        private const float ProfileSignatureEpsilon = 0.0001f;

        private sealed class CameraState
        {
            public Camera Camera;
            public RenderTexture HistoryA;
            public RenderTexture HistoryB;
            public RenderTextureDescriptor Descriptor;
            public int AlgorithmVersion;
            public bool HasValidHistory;
            public bool HasPreviousCameraState;
            public bool WriteToA;
            public int FrameIndex;
            public int FirstValidFrameIndex;
            public int LastInvalidationFrameIndex;
            public Matrix4x4 CurrentViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentInverseViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 CurrentNonJitteredProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousViewProjectionMatrix = Matrix4x4.identity;
            public Matrix4x4 PreviousNonJitteredProjectionMatrix = Matrix4x4.identity;
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
            public Vector4 CurrentProfileSignature0;
            public Vector4 CurrentProfileSignature1;
            public Vector4 CurrentProfileSignature2;
            public Vector4 PreviousProfileSignature0;
            public Vector4 PreviousProfileSignature1;
            public Vector4 PreviousProfileSignature2;
            public string LastInvalidationReason = "NeverAllocated";
        }

        private static readonly System.Collections.Generic.Dictionary<int, CameraState> CameraStates = new System.Collections.Generic.Dictionary<int, CameraState>();
        private static readonly System.Collections.Generic.List<int> CameraStateRemovalKeys = new System.Collections.Generic.List<int>();
        private static int cameraStatePruneCounter;

        public static BurtScreenSpaceSubsurfaceHistoryTextures EnsureHistoryTextures(BurtRenderRequest request, BurtRenderPipelineAsset asset, out bool historyValid)
        {
            historyValid = false;
            var camera = request != null ? request.Camera : null;
            var matrices = CreateCurrentMatrices(request);
            var profileSignature = CreateProfileSignature(asset);
            if (camera == null)
            {
                return BurtScreenSpaceSubsurfaceHistoryTextures.CreateInvalid(matrices.ViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            state.Camera = camera;
            PruneDisposedCameraStates();
            if (state.AlgorithmVersion != HistoryAlgorithmVersion)
            {
                ReleaseHistory(state);
                state.AlgorithmVersion = HistoryAlgorithmVersion;
                SetAllocationInvalidationReason(state, "AlgorithmChanged");
            }

            var descriptor = CreateHistoryDescriptor(camera);
            var descriptorsMatch = state.HistoryA != null && state.HistoryB != null && Matches(state.Descriptor, descriptor);
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            var invalidationReason = ResolveHistoryInvalidationReason(camera, state, matrices.NonJitteredProjectionMatrix, profileSignature, targetWidth, targetHeight, descriptorsMatch);

            if (!descriptorsMatch)
            {
                ReleaseHistory(state);
            }

            if (state.HistoryA == null || state.HistoryB == null)
            {
                state.Descriptor = descriptor;
                ReleaseTexture(state.HistoryA);
                ReleaseTexture(state.HistoryB);
                state.HistoryA = CreateHistoryTexture(descriptor, "Burt SSS History A " + camera.GetInstanceID());
                state.HistoryB = CreateHistoryTexture(descriptor, "Burt SSS History B " + camera.GetInstanceID());
                state.WriteToA = false;
                SetAllocationInvalidationReason(state, "HistoryAllocated");
            }

            if (!string.IsNullOrEmpty(invalidationReason))
            {
                InvalidateState(state, invalidationReason);
            }

            state.FrameIndex++;
            state.CurrentViewProjectionMatrix = matrices.ViewProjectionMatrix;
            state.CurrentInverseViewProjectionMatrix = matrices.InverseViewProjectionMatrix;
            state.CurrentNonJitteredProjectionMatrix = matrices.NonJitteredProjectionMatrix;
            state.CurrentProfileSignature0 = profileSignature.Signature0;
            state.CurrentProfileSignature1 = profileSignature.Signature1;
            state.CurrentProfileSignature2 = profileSignature.Signature2;
            CaptureCurrentCameraState(camera, state, targetWidth, targetHeight);

            var input = state.WriteToA ? state.HistoryB : state.HistoryA;
            var output = state.WriteToA ? state.HistoryA : state.HistoryB;
            historyValid = state.HasValidHistory && state.HasPreviousCameraState && input != null && output != null;
            var previousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : matrices.ViewProjectionMatrix;
            return new BurtScreenSpaceSubsurfaceHistoryTextures(input, output, previousViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
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
            state.PreviousViewProjectionMatrix = state.CurrentViewProjectionMatrix;
            state.PreviousNonJitteredProjectionMatrix = state.CurrentNonJitteredProjectionMatrix;
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
            state.PreviousProfileSignature0 = state.CurrentProfileSignature0;
            state.PreviousProfileSignature1 = state.CurrentProfileSignature1;
            state.PreviousProfileSignature2 = state.CurrentProfileSignature2;
            state.HasPreviousCameraState = true;
            state.WriteToA = !state.WriteToA;
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

            var history = state.WriteToA ? state.HistoryB : state.HistoryA;
            historyValid = state.HasValidHistory && history != null;
            historyAge = state.HasValidHistory ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex) : 0;
            return history;
        }

        public static BurtScreenSpaceSubsurfaceHistoryStatus GetHistoryStatus(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return new BurtScreenSpaceSubsurfaceHistoryStatus(false, false, 0, 0, RenderTextureFormat.Default, 0, 0, "NoCameraOrHistory");
            }

            var descriptor = CreateHistoryDescriptor(camera);
            var input = state.WriteToA ? state.HistoryB : state.HistoryA;
            var output = state.WriteToA ? state.HistoryA : state.HistoryB;
            var hasHistory = input != null && output != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtScreenSpaceSubsurfaceHistoryStatus(
                state.HasValidHistory && state.HasPreviousCameraState && hasHistory,
                hasHistory && Matches(state.Descriptor, descriptor),
                input != null ? input.width : 0,
                input != null ? input.height : 0,
                input != null ? input.format : RenderTextureFormat.Default,
                state.FrameIndex,
                historyAge,
                state.LastInvalidationReason);
        }

        public static BurtScreenSpaceSubsurfaceHistoryTextures GetPendingHistoryTextures(BurtRenderRequest request)
        {
            var matrices = CreateCurrentMatrices(request);
            var camera = request != null ? request.Camera : null;
            if (camera == null || !CameraStates.TryGetValue(camera.GetInstanceID(), out var state))
            {
                return BurtScreenSpaceSubsurfaceHistoryTextures.CreateInvalid(matrices.ViewProjectionMatrix, matrices.InverseViewProjectionMatrix);
            }

            var input = state.WriteToA ? state.HistoryB : state.HistoryA;
            var output = state.WriteToA ? state.HistoryA : state.HistoryB;
            var previousViewProjectionMatrix = state.HasPreviousCameraState ? state.PreviousViewProjectionMatrix : state.CurrentViewProjectionMatrix;
            return new BurtScreenSpaceSubsurfaceHistoryTextures(
                input,
                output,
                previousViewProjectionMatrix,
                state.CurrentInverseViewProjectionMatrix);
        }

        public static RenderTextureDescriptor CreateHistoryDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateScreenSpaceSubsurfaceColorDescriptor(camera);
            descriptor.colorFormat = RenderTextureFormat.ARGBHalf;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.mipCount = 1;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = true;
            descriptor.sRGB = false;
            return descriptor;
        }

        private static BurtScreenSpaceSubsurfaceHistoryMatrices CreateCurrentMatrices(BurtRenderRequest request)
        {
            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return new BurtScreenSpaceSubsurfaceHistoryMatrices(Matrix4x4.identity, Matrix4x4.identity, Matrix4x4.identity);
            }

            var temporalAA = request.TemporalAA;
            var useTemporalAA = temporalAA != null && temporalAA.Enabled;
            var viewMatrix = useTemporalAA ? temporalAA.ViewMatrix : camera.worldToCameraMatrix;
            var projectionMatrix = useTemporalAA ? temporalAA.JitteredProjectionMatrix : camera.projectionMatrix;
            var nonJitteredProjectionMatrix = useTemporalAA ? temporalAA.NonJitteredProjectionMatrix : camera.projectionMatrix;
            var viewProjectionMatrix = useTemporalAA
                ? temporalAA.CurrentViewProjectionMatrix
                : GL.GetGPUProjectionMatrix(projectionMatrix, true) * viewMatrix;
            return new BurtScreenSpaceSubsurfaceHistoryMatrices(viewProjectionMatrix, viewProjectionMatrix.inverse, nonJitteredProjectionMatrix);
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
            if (cameraStatePruneCounter < CameraStatePruneInterval)
            {
                return;
            }

            cameraStatePruneCounter = 0;
            CameraStateRemovalKeys.Clear();
            foreach (var pair in CameraStates)
            {
                if (pair.Value.Camera != null)
                {
                    continue;
                }

                ReleaseHistory(pair.Value);
                CameraStateRemovalKeys.Add(pair.Key);
            }

            for (var i = 0; i < CameraStateRemovalKeys.Count; i++)
            {
                CameraStates.Remove(CameraStateRemovalKeys[i]);
            }

            CameraStateRemovalKeys.Clear();
        }

        private static RenderTexture CreateHistoryTexture(RenderTextureDescriptor descriptor, string name)
        {
            var texture = new RenderTexture(descriptor)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.Create();
            return texture;
        }

        private static bool Matches(RenderTextureDescriptor left, RenderTextureDescriptor right)
        {
            return left.width == right.width &&
                left.height == right.height &&
                left.colorFormat == right.colorFormat &&
                left.depthBufferBits == right.depthBufferBits &&
                left.msaaSamples == right.msaaSamples &&
                left.useMipMap == right.useMipMap &&
                left.autoGenerateMips == right.autoGenerateMips &&
                left.mipCount == right.mipCount &&
                left.enableRandomWrite == right.enableRandomWrite &&
                left.sRGB == right.sRGB;
        }

        private static string ResolveHistoryInvalidationReason(
            Camera camera,
            CameraState state,
            Matrix4x4 nonJitteredProjectionMatrix,
            BurtScreenSpaceSubsurfaceProfileSignature profileSignature,
            int targetWidth,
            int targetHeight,
            bool descriptorsMatch)
        {
            if (state == null)
            {
                return "NoCameraState";
            }

            if (!state.HasPreviousCameraState)
            {
                return descriptorsMatch ? null : "DescriptorChanged";
            }

            if (GetTargetTextureId(camera) != state.PreviousTargetTextureId)
            {
                return "TargetTextureChanged";
            }

            if (targetWidth != state.PreviousTargetWidth || targetHeight != state.PreviousTargetHeight)
            {
                return GetTargetTextureId(camera) != 0 ? "TargetTextureSizeChanged" : "CameraResolutionChanged";
            }

            if (!descriptorsMatch)
            {
                return "DescriptorChanged";
            }

            if (ProfileSignatureChanged(profileSignature, state))
            {
                return "ProfileChanged";
            }

            if (camera != null)
            {
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
                    return "FOVChanged";
                }

                if (FloatChanged(camera.nearClipPlane, state.PreviousNearClipPlane, 0.0001f))
                {
                    return "NearClipChanged";
                }

                if (FloatChanged(camera.farClipPlane, state.PreviousFarClipPlane, 0.001f))
                {
                    return "FarClipChanged";
                }
            }

            if (ProjectionChanged(nonJitteredProjectionMatrix, state.PreviousNonJitteredProjectionMatrix))
            {
                return "ProjectionChanged";
            }

            return CameraCutDetected(camera, state) ? "CameraCut" : null;
        }

        private static void CaptureCurrentCameraState(Camera camera, CameraState state, int targetWidth, int targetHeight)
        {
            if (camera == null || state == null)
            {
                return;
            }

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

        private static BurtScreenSpaceSubsurfaceProfileSignature CreateProfileSignature(BurtRenderPipelineAsset asset)
        {
            var palette = asset != null
                ? asset.ScreenSpaceSubsurfaceProfilePalette
                : BurtSubsurfaceProfilePalette.Resolve(BurtSubsurfaceProfileSettings.Default, null);
            var count = Mathf.Clamp(palette.Count, 1, BurtSubsurfaceProfilePalette.MaxProfiles);
            var signature0 = new Vector4(count, 0f, 0f, 0f);
            var signature1 = Vector4.zero;
            var signature2 = Vector4.zero;
            for (var i = 0; i < count; i++)
            {
                var profile = palette.GetSettings(i);
                var weight = i + 1f;
                signature0 += profile.Params * weight;
                signature1 += profile.Params2 * weight;
                signature2.x += profile.SurfaceAlbedoVector.sqrMagnitude * weight;
                signature2.y += profile.MeanFreePathVector.sqrMagnitude * weight;
                signature2.z += (profile.TransmissionVector.sqrMagnitude + profile.TransmissionTintVector.sqrMagnitude) * weight;
                signature2.w += (profile.TintVector.sqrMagnitude + profile.BoundaryColorBleedVector.sqrMagnitude) * weight;
            }

            return new BurtScreenSpaceSubsurfaceProfileSignature(signature0, signature1, signature2);
        }

        private static bool ProfileSignatureChanged(BurtScreenSpaceSubsurfaceProfileSignature profileSignature, CameraState state)
        {
            return VectorChanged(profileSignature.Signature0, state.PreviousProfileSignature0) ||
                VectorChanged(profileSignature.Signature1, state.PreviousProfileSignature1) ||
                VectorChanged(profileSignature.Signature2, state.PreviousProfileSignature2);
        }

        private static void InvalidateState(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            state.HasValidHistory = false;
            state.FirstValidFrameIndex = 0;
            state.LastInvalidationFrameIndex = state.FrameIndex;
            state.LastInvalidationReason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        }

        private static void SetAllocationInvalidationReason(CameraState state, string reason)
        {
            if (state == null)
            {
                return;
            }

            if (!state.HasPreviousCameraState ||
                state.LastInvalidationReason == "NeverAllocated" ||
                state.LastInvalidationReason == "HistoryAllocated")
            {
                state.LastInvalidationReason = reason;
            }
        }

        private static int GetTargetTextureId(Camera camera)
        {
            return camera != null && camera.targetTexture != null ? camera.targetTexture.GetInstanceID() : 0;
        }

        private static void GetTargetSize(Camera camera, out int width, out int height)
        {
            if (camera != null && camera.targetTexture != null)
            {
                width = Mathf.Max(1, camera.targetTexture.width);
                height = Mathf.Max(1, camera.targetTexture.height);
                return;
            }

            width = Mathf.Max(1, camera != null ? camera.pixelWidth : 1);
            height = Mathf.Max(1, camera != null ? camera.pixelHeight : 1);
        }

        private static bool FloatChanged(float current, float previous, float epsilon)
        {
            return Mathf.Abs(current - previous) > epsilon;
        }

        private static bool ProjectionChanged(Matrix4x4 current, Matrix4x4 previous)
        {
            for (var i = 0; i < 16; i++)
            {
                if (Mathf.Abs(current[i] - previous[i]) > ProjectionChangeEpsilon)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool VectorChanged(Vector4 current, Vector4 previous)
        {
            return Mathf.Abs(current.x - previous.x) > ProfileSignatureEpsilon ||
                Mathf.Abs(current.y - previous.y) > ProfileSignatureEpsilon ||
                Mathf.Abs(current.z - previous.z) > ProfileSignatureEpsilon ||
                Mathf.Abs(current.w - previous.w) > ProfileSignatureEpsilon;
        }

        private static bool CameraCutDetected(Camera camera, CameraState state)
        {
            if (camera == null || state == null)
            {
                return false;
            }

            var positionDelta = camera.transform.position - state.PreviousCameraPosition;
            var rotationDelta = Quaternion.Angle(camera.transform.rotation, state.PreviousCameraRotation);
            var farClip = Mathf.Max(camera.farClipPlane, 1f);
            var cutDistance = Mathf.Clamp(farClip * 0.25f, 25f, 250f);
            return positionDelta.sqrMagnitude > cutDistance * cutDistance || rotationDelta > 60f;
        }

        private static void ReleaseHistory(CameraState state)
        {
            if (state == null)
            {
                return;
            }

            ReleaseTexture(state.HistoryA);
            ReleaseTexture(state.HistoryB);
            state.HistoryA = null;
            state.HistoryB = null;
            state.HasValidHistory = false;
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

        private readonly struct BurtScreenSpaceSubsurfaceHistoryMatrices
        {
            public Matrix4x4 ViewProjectionMatrix { get; }
            public Matrix4x4 InverseViewProjectionMatrix { get; }
            public Matrix4x4 NonJitteredProjectionMatrix { get; }

            public BurtScreenSpaceSubsurfaceHistoryMatrices(
                Matrix4x4 viewProjectionMatrix,
                Matrix4x4 inverseViewProjectionMatrix,
                Matrix4x4 nonJitteredProjectionMatrix)
            {
                ViewProjectionMatrix = viewProjectionMatrix;
                InverseViewProjectionMatrix = inverseViewProjectionMatrix;
                NonJitteredProjectionMatrix = nonJitteredProjectionMatrix;
            }
        }

        private readonly struct BurtScreenSpaceSubsurfaceProfileSignature
        {
            public Vector4 Signature0 { get; }
            public Vector4 Signature1 { get; }
            public Vector4 Signature2 { get; }

            public BurtScreenSpaceSubsurfaceProfileSignature(Vector4 signature0, Vector4 signature1, Vector4 signature2)
            {
                Signature0 = signature0;
                Signature1 = signature1;
                Signature2 = signature2;
            }
        }
    }

    internal readonly struct BurtScreenSpaceSubsurfaceHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public int Width { get; }
        public int Height { get; }
        public RenderTextureFormat Format { get; }
        public int FrameIndex { get; }
        public int HistoryAge { get; }
        public string Reason { get; }

        public BurtScreenSpaceSubsurfaceHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            int width,
            int height,
            RenderTextureFormat format,
            int frameIndex,
            int historyAge,
            string reason)
        {
            HasHistory = hasHistory;
            DescriptorMatches = descriptorMatches;
            Width = width;
            Height = height;
            Format = format;
            FrameIndex = frameIndex;
            HistoryAge = historyAge;
            Reason = string.IsNullOrEmpty(reason) ? "Unknown" : reason;
        }
    }
}
