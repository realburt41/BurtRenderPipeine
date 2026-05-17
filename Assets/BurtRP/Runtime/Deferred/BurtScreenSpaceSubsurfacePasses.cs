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
            builder.WriteScreenSpaceSubsurfaceSource();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var cameraColorTarget, out var target))
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
            Draw(cmd, copyMaterial, target.Identifier, context, CopyPassIndex);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private bool TryGetTargets(BurtRenderGraphContext context, out BurtRenderTargetHandle cameraColorTarget, out BurtRenderTargetHandle target)
        {
            cameraColorTarget = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            target = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                cameraColorTarget.IsValid &&
                target.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }

        private static void Draw(CommandBuffer cmd, Material material, RenderTargetIdentifier target, BurtRenderGraphContext context, int passIndex)
        {
            cmd.SetRenderTarget(target);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1);
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
            builder.WriteScreenSpaceSubsurfaceTemp();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var source, out var cameraDepth, out var gbuffer0, out var gbuffer1, out var gbuffer2, out var gbuffer3, out var gbuffer4, out var target))
            {
                return;
            }

            var blurMaterial = GetMaterial();
            if (blurMaterial == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            BindInputs(cmd, context, source, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
            Draw(cmd, blurMaterial, target.Identifier, context, BlurHorizontalPassIndex);
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
            out BurtRenderTargetHandle target)
        {
            source = context != null ? context.ScreenSpaceSubsurfaceSourceTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceSourceName);
            cameraDepth = context != null ? context.CameraDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraDepthName);
            gbuffer0 = context != null ? context.GBuffer0Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer0Name);
            gbuffer1 = context != null ? context.GBuffer1Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer1Name);
            gbuffer2 = context != null ? context.GBuffer2Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer2Name);
            gbuffer3 = context != null ? context.GBuffer3Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer3Name);
            gbuffer4 = context != null ? context.GBuffer4Target : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.GBuffer4Name);
            target = context != null ? context.ScreenSpaceSubsurfaceTempTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceTempName);

            return BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) &&
                source.IsValid &&
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

        private static void BindInputs(
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
            BurtScreenSpaceSubsurfacePassUtility.BindCommonInputs(cmd, context, source, cameraDepth, gbuffer0, gbuffer1, gbuffer2, gbuffer3, gbuffer4);
        }

        private static void Draw(CommandBuffer cmd, Material material, RenderTargetIdentifier target, BurtRenderGraphContext context, int passIndex)
        {
            cmd.SetRenderTarget(target);
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
            builder.WriteScreenSpaceSubsurfaceBlur();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (!TryGetTargets(context, out var source, out var original, out var cameraDepth, out var gbuffer0, out var gbuffer1, out var gbuffer2, out var gbuffer3, out var gbuffer4, out var target))
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
            Draw(cmd, blurMaterial, target.Identifier, context, BlurVerticalPassIndex);
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
                target.IsValid;
        }

        private Material GetMaterial()
        {
            return BurtScreenSpaceSubsurfacePassUtility.GetMaterial(ref material, ref hasLoggedMissingShader);
        }

        private static void Draw(CommandBuffer cmd, Material material, RenderTargetIdentifier target, BurtRenderGraphContext context, int passIndex)
        {
            cmd.SetRenderTarget(target);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1);
        }
    }

    internal sealed class BurtScreenSpaceSubsurfaceCopyToCameraColorPass : BurtRenderPass
    {
        private const int CopyPassIndex = 0;
        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Screen Space Subsurface Copy To Camera Color";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(builder.Request, builder.Asset))
            {
                return;
            }

            builder.ReadScreenSpaceSubsurfaceBlur();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var source = context != null ? context.ScreenSpaceSubsurfaceBlurTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.ScreenSpaceSubsurfaceBlurName);
            var target = context != null ? context.CameraColorTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.CameraColorName);
            if (!BurtScreenSpaceSubsurfacePassUtility.ShouldUseScreenSpaceSubsurface(context != null ? context.Request : null, context != null ? context.Asset : null) ||
                !source.IsValid ||
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
            cmd.SetRenderTarget(target.Identifier);
            BurtScreenSpaceSubsurfacePassUtility.SetViewport(cmd, context);
            cmd.DrawProcedural(Matrix4x4.identity, copyMaterial, CopyPassIndex, MeshTopology.Triangles, 3, 1);
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

    internal static class BurtScreenSpaceSubsurfacePassUtility
    {
        public const string ScreenSpaceSubsurfaceShaderName = "Hidden/BurtRP/ScreenSpaceSubsurface";

        public static readonly int SourceTextureId = Shader.PropertyToID("_BurtSSSSourceTexture");
        public static readonly int OriginalTextureId = Shader.PropertyToID("_BurtSSSOriginalTexture");

        private static readonly int CameraDepthTextureId = BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int GBuffer0Id = BurtRenderGraphResourceRegistry.GBuffer0Id;
        private static readonly int GBuffer1Id = BurtRenderGraphResourceRegistry.GBuffer1Id;
        private static readonly int GBuffer2Id = BurtRenderGraphResourceRegistry.GBuffer2Id;
        private static readonly int GBuffer3Id = BurtRenderGraphResourceRegistry.GBuffer3Id;
        private static readonly int GBuffer4Id = BurtRenderGraphResourceRegistry.GBuffer4Id;
        private static readonly int ScreenSizeId = Shader.PropertyToID("_BurtSSSScreenSize");
        private static readonly int ParamsId = Shader.PropertyToID("_BurtSSSParams");
        private static readonly int Params2Id = Shader.PropertyToID("_BurtSSSParams2");
        private static readonly int SurfaceAlbedoId = Shader.PropertyToID("_BurtSSSSurfaceAlbedo");
        private static readonly int MeanFreePathId = Shader.PropertyToID("_BurtSSSMeanFreePath");
        private static readonly int ProfileTintId = Shader.PropertyToID("_BurtSSSProfileTint");
        private static readonly int BoundaryColorBleedId = Shader.PropertyToID("_BurtSSSBoundaryColorBleed");

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

        private static BurtSubsurfaceProfileSettings ResolveProfileSettings(BurtRenderPipelineAsset asset)
        {
            return asset != null
                ? asset.ScreenSpaceSubsurfaceProfileSettings
                : BurtSubsurfaceProfileSettings.Default;
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
            var cmd = CommandBufferPool.Get(passName);
            cmd.GetTemporaryRT(textureId, descriptor, FilterMode.Bilinear);
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
