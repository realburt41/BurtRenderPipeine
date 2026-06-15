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
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.WriteFurBlurTemporal();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
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

    internal sealed class BurtClearFurBlurPropertyPass : BurtRenderPass
    {
        public override string Name => "Burt Clear Fur Blur Property";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Clear;

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
            var propertyTarget = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            var cameraDepthTarget = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            if (!propertyTarget.IsValid || !cameraDepthTarget.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(propertyTarget.Identifier, cameraDepthTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.ClearRenderTarget(false, true, BurtFurBlurPassUtility.FurBlurPropertyClearColor);
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
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(target.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, source.Identifier);
            cmd.SetGlobalVector(BurtFurBlurPassUtility.ScreenSizeId, BurtFurBlurPassUtility.CreateScreenSizeVector(descriptor.width, descriptor.height));
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, DilatePassIndex, MeshTopology.Triangles, 3, 1);
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
            builder.ReadFurBlurProperty();
            builder.WriteFurBlurColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out var cameraColorTarget, out _, out var propertyTarget, out var colorTarget, out _))
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurColorDescriptor(context.Request != null ? context.Request.Camera : null);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(colorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraColorTextureId, cameraColorTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            cmd.SetGlobalVector(BurtFurBlurPassUtility.ScreenSizeId, BurtFurBlurPassUtility.CreateScreenSizeVector(descriptor.width, descriptor.height));
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, BlurPassIndex, MeshTopology.Triangles, 3, 1);
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
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Fur Blur Temporal";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurColor();
            builder.ReadFurBlurProperty();
            builder.WriteFurBlurTemporal();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out _, out _, out var propertyTarget, out var colorTarget, out var temporalTarget))
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
            var history = BurtFurBlurHistoryUtility.EnsureHistoryTextures(context.Request, out var historyValid);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(temporalTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, colorTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurHistoryTextureId, history.Color != null ? new RenderTargetIdentifier(history.Color) : colorTarget.Identifier);
            cmd.SetGlobalVector(BurtFurBlurPassUtility.ScreenSizeId, BurtFurBlurPassUtility.CreateScreenSizeVector(descriptor.width, descriptor.height));
            cmd.SetGlobalVector(BurtFurBlurPassUtility.HistoryParamsId, new Vector4(historyValid ? 1f : 0f, BurtFurBlurHistoryUtility.DefaultFeedback, history.HistoryAge, 0f));
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, TemporalPassIndex, MeshTopology.Triangles, 3, 1);
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
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurTemporal();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var temporalTarget = context != null ? context.FurBlurTemporalTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurTemporalName);
            if (context == null || !temporalTarget.IsValid)
            {
                return;
            }

            var history = BurtFurBlurHistoryUtility.GetPendingHistoryTextures(context.Request);
            if (history.Color == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.CopyTexture(temporalTarget.Identifier, new RenderTargetIdentifier(history.Color));
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

            builder.ReadFurBlurTemporal();
            builder.ReadCameraColor();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out _, out var cameraColorTarget, out _, out _, out var temporalTarget))
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
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurTemporalTextureId, temporalTarget.Identifier);
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
            builder.ReadFurBlurProperty();
            builder.ReadFurBlurColor();
            builder.ReadFurBlurTemporal();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.ShouldUseFurBlurDebugView(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out _, out var cameraColorTarget, out var propertyTarget, out var colorTarget, out var temporalTarget))
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
            var history = BurtFurBlurHistoryUtility.GetCurrentHistoryTexture(context.Request, out _, out _);
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, propertyTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, colorTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurTemporalTextureId, temporalTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurHistoryTextureId, history != null ? new RenderTargetIdentifier(history) : temporalTarget.Identifier);
            cmd.SetGlobalVector(BurtFurBlurPassUtility.ScreenSizeId, BurtFurBlurPassUtility.CreateScreenSizeVector(descriptor.width, descriptor.height));
            cmd.SetGlobalVector(BurtFurBlurPassUtility.HistoryParamsId, new Vector4(status.HasHistory ? 1f : 0f, BurtFurBlurHistoryUtility.DefaultFeedback, status.HistoryAge, 0f));
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
            if (!BurtFurBlurPassUtility.ShouldUseFurBlur(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadFurBlurTemporal();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
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

    internal static class BurtFurBlurPassUtility
    {
        public const string ShaderName = "Hidden/BurtRP/FurBlur";
        public const float DefaultTemporalFeedback = BurtFurBlurHistoryUtility.DefaultFeedback;

        private const int OpaqueRenderQueueMax = 2500;
        private const string FurBlurPropertyShaderPassName = "Burt Multipass Fur Blur Property";
        private const string FurBlurEnabledPropertyName = "_FurBlurEnabled";
        private const float CameraCutPositionThreshold = 0.5f;
        private const float CameraCutRotationThreshold = 5f;

        public static readonly int ScreenSizeId = Shader.PropertyToID("_BurtFurBlurScreenSize");
        public static readonly int HistoryParamsId = Shader.PropertyToID("_BurtFurBlurHistoryParams");
        public static readonly int DebugModeId = Shader.PropertyToID("_BurtFurBlurDebugMode");

        private static readonly Color ReversedZFarClearColor = new Color(0f, 0f, 0f, 1f);
        private static readonly Color ForwardZFarClearColor = new Color(0f, 1f, 0f, 1f);
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
                mode == BurtShadingDebugMode.FurBlurDiagnostic;
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

            return asset.RendererMode == BurtRendererMode.Deferred;
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
        public int HistoryAge { get; }

        public BurtFurBlurHistoryTextures(RenderTexture color, int historyAge)
        {
            Color = color;
            HistoryAge = historyAge;
        }
    }

    internal readonly struct BurtFurBlurHistoryStatus
    {
        public bool HasHistory { get; }
        public bool DescriptorMatches { get; }
        public int HistoryAge { get; }
        public int FrameIndex { get; }
        public RenderTextureFormat Format { get; }
        public int Width { get; }
        public int Height { get; }
        public string Reason { get; }

        public BurtFurBlurHistoryStatus(
            bool hasHistory,
            bool descriptorMatches,
            int historyAge,
            int frameIndex,
            RenderTextureFormat format,
            int width,
            int height,
            string reason)
        {
            HasHistory = hasHistory;
            DescriptorMatches = descriptorMatches;
            HistoryAge = historyAge;
            FrameIndex = frameIndex;
            Format = format;
            Width = width;
            Height = height;
            Reason = reason;
        }
    }

    internal static class BurtFurBlurHistoryUtility
    {
        public const float DefaultFeedback = 0.85f;

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
                return new BurtFurBlurHistoryTextures(null, 0);
            }

            var state = GetOrCreateState(camera.GetInstanceID());
            state.Camera = camera;
            PruneDisposedCameraStates();
            var descriptor = CreateHistoryDescriptor(camera);
            var descriptorMatches = state.ColorHistory != null && Matches(state.Descriptor, descriptor);
            GetTargetSize(camera, out var targetWidth, out var targetHeight);
            var invalidationReason = ResolveInvalidationReason(camera, state, camera.projectionMatrix, targetWidth, targetHeight, descriptorMatches);

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

            if (!string.IsNullOrEmpty(invalidationReason))
            {
                InvalidateState(state, invalidationReason);
            }

            state.FrameIndex++;
            state.CurrentProjectionMatrix = camera.projectionMatrix;
            CaptureCurrentCameraState(camera, state, targetWidth, targetHeight);

            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            historyValid = state.HasValidHistory && state.HasPreviousCameraState && state.ColorHistory != null;
            return new BurtFurBlurHistoryTextures(state.ColorHistory, historyAge);
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
                return new BurtFurBlurHistoryTextures(null, 0);
            }

            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtFurBlurHistoryTextures(state.ColorHistory, historyAge);
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
                return new BurtFurBlurHistoryStatus(false, false, 0, 0, RenderTextureFormat.Default, 0, 0, "NoCameraOrHistory");
            }

            var descriptor = CreateHistoryDescriptor(camera);
            var hasHistory = state.ColorHistory != null;
            var historyAge = state.HasValidHistory && state.FirstValidFrameIndex > 0 ? Mathf.Max(0, state.FrameIndex - state.FirstValidFrameIndex + 1) : 0;
            return new BurtFurBlurHistoryStatus(
                state.HasValidHistory && hasHistory,
                hasHistory && Matches(state.Descriptor, descriptor),
                historyAge,
                state.FrameIndex,
                hasHistory ? state.ColorHistory.format : RenderTextureFormat.Default,
                hasHistory ? state.ColorHistory.width : 0,
                hasHistory ? state.ColorHistory.height : 0,
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
            state.ColorHistory = null;
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
            public RenderTextureDescriptor Descriptor;
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
