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

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurPropertyDescriptor(camera);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, descriptor, FilterMode.Point);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurPropertyTextureId, target.Identifier);
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

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurColorDescriptor(camera);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, descriptor, FilterMode.Bilinear);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, target.Identifier);
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
            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out var cameraColorTarget, out _, out var propertyTarget, out var colorTarget))
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateFurBlurColorDescriptor(camera);
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

            builder.ReadFurBlurColor();
            builder.ReadCameraColor();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!BurtFurBlurPassUtility.TryGetFullScreenTargets(context, out _, out var cameraColorTarget, out _, out var colorTarget))
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
            var cmd = CommandBufferPool.Get(Name);
            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            BurtRenderTargetDescriptorUtility.SetViewport(cmd, descriptor.width, descriptor.height);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.FurBlurColorTextureId, colorTarget.Identifier);
            cmd.DrawProcedural(Matrix4x4.identity, drawMaterial, CompositePassIndex, MeshTopology.Triangles, 3, 1);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private Material GetMaterial()
        {
            return BurtFurBlurPassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
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

        public static readonly int ScreenSizeId = Shader.PropertyToID("_BurtFurBlurScreenSize");

        public static bool ShouldUseFurBlur(BurtRenderRequest request, BurtRenderPipelineAsset asset)
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
            out BurtRenderTargetHandle colorTarget)
        {
            cameraColorReadTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            cameraColorWriteTarget = cameraColorReadTarget;
            propertyTarget = context != null ? context.FurBlurPropertyTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurPropertyName);
            colorTarget = context != null ? context.FurBlurColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.FurBlurColorName);
            return context != null &&
                ShouldUseFurBlur(context.Request, context.Asset) &&
                cameraColorReadTarget.IsValid &&
                cameraColorWriteTarget.IsValid &&
                propertyTarget.IsValid &&
                colorTarget.IsValid;
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
    }
}
