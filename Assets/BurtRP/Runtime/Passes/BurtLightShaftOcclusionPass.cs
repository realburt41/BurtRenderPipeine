using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtLightShaftOcclusionPass : BurtRenderPass
    {
        private const int SetupPassIndex = 0;
        private const int RadiusBlurPassIndex = 1;
        private const int FinalPassIndex = 2;

        private static readonly int CameraDepthTextureId =
            BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int OcclusionTextureId =
            BurtRenderGraphResourceRegistry.LightShaftOcclusionTextureId;
        private static readonly int InputTextureId =
            Shader.PropertyToID("_BurtLightShaftInputTexture");
        private static readonly int ParametersId =
            Shader.PropertyToID("_BurtLightShaftParameters");
        private static readonly int TextureSpaceOriginId =
            Shader.PropertyToID("_BurtLightShaftTextureSpaceOrigin");
        private static readonly int AspectRatioId =
            Shader.PropertyToID("_BurtLightShaftAspectRatioAndInvAspectRatio");
        private static readonly int BlurParametersId =
            Shader.PropertyToID("_BurtLightShaftBlurParameters");
        private static readonly int BlurUvMinMaxId =
            Shader.PropertyToID("_BurtLightShaftBlurUVMinMax");

        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Light Shaft Occlusion";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Allocate;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtLightShaftOcclusionUtility.ShouldUseLightShaftOcclusion(builder.Request))
            {
                return;
            }

            builder.ReadCameraDepth();
            builder.ReadLightingGlobals();
            builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
            builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionTempName);
            builder.AllowUnconsumedRenderTargetWrite(BurtRenderGraphResourceRegistry.LightShaftOcclusionTempName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null ||
                !BurtLightShaftOcclusionUtility.ShouldUseLightShaftOcclusion(context.Request) ||
                context.ResourceRegistry == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var depthTarget = context.CameraDepthTarget;
            if (camera == null ||
                !depthTarget.IsValid ||
                !BurtLightShaftOcclusionUtility.TryResolveTextureSpaceSunOrigin(
                    context.Request,
                    out var textureSpaceOrigin))
            {
                return;
            }

            var drawMaterial = GetMaterial();
            if (drawMaterial == null)
            {
                return;
            }

            var settings = BurtLightShaftOcclusionUtility.ResolveSettings();
            var descriptor = CreateDescriptor(camera);
            context.ResourceRegistry.SetRenderTargetDescriptor(
                BurtRenderGraphResourceRegistry.LightShaftOcclusionName,
                descriptor,
                FilterMode.Bilinear,
                "Burt Light Shaft Occlusion");
            context.ResourceRegistry.SetRenderTargetDescriptor(
                BurtRenderGraphResourceRegistry.LightShaftOcclusionTempName,
                descriptor,
                FilterMode.Bilinear,
                "Burt Light Shaft Occlusion Temp");
            var occlusionTarget = context.ResourceRegistry.AllocateRenderTarget(
                BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
            var occlusionTempTarget = context.ResourceRegistry.AllocateRenderTarget(
                BurtRenderGraphResourceRegistry.LightShaftOcclusionTempName);
            if (!occlusionTarget.IsValid || !occlusionTempTarget.IsValid)
            {
                context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionTempName);
                context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
                return;
            }

            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var aspectRatio = width / (float)height;
            var uvMinMax = new Vector4(
                0.5f / width,
                0.5f / height,
                1f - 0.5f / width,
                1f - 0.5f / height);

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetGlobalTexture(CameraDepthTextureId, depthTarget.Identifier);
            cmd.SetGlobalVector(ParametersId, new Vector4(
                1f / Mathf.Max(settings.DepthRange, 0.0001f),
                settings.MaskDarkness,
                0f,
                0f));
            cmd.SetGlobalVector(TextureSpaceOriginId, new Vector4(
                textureSpaceOrigin.x,
                textureSpaceOrigin.y,
                0f,
                0f));
            cmd.SetGlobalVector(AspectRatioId, new Vector4(
                1f,
                aspectRatio,
                1f,
                1f / Mathf.Max(aspectRatio, 0.0001f)));
            cmd.SetGlobalVector(BlurUvMinMaxId, uvMinMax);

            DrawFullscreen(
                cmd,
                drawMaterial,
                SetupPassIndex,
                occlusionTarget.Identifier,
                width,
                height);

            var source = occlusionTarget.Identifier;
            var destination = occlusionTempTarget.Identifier;
            for (var passIndex = 0;
                 passIndex < BurtLightShaftOcclusionUtility.BlurPassCount;
                 passIndex++)
            {
                cmd.SetGlobalTexture(InputTextureId, source);
                cmd.SetGlobalVector(BlurParametersId, new Vector4(
                    BurtLightShaftOcclusionUtility.BlurSampleCount,
                    BurtLightShaftOcclusionUtility.FirstPassDistance,
                    passIndex,
                    0f));
                DrawFullscreen(
                    cmd,
                    drawMaterial,
                    RadiusBlurPassIndex,
                    destination,
                    width,
                    height);

                var swap = source;
                source = destination;
                destination = swap;
            }

            cmd.SetGlobalTexture(InputTextureId, source);
            DrawFullscreen(
                cmd,
                drawMaterial,
                FinalPassIndex,
                occlusionTarget.Identifier,
                width,
                height);
            cmd.SetGlobalTexture(OcclusionTextureId, occlusionTarget.Identifier);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
            context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionTempName);
            BurtLightShaftOcclusionUtility.MarkProduced(context.Request);
        }

        private static RenderTextureDescriptor CreateDescriptor(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            descriptor.width = Mathf.Max(1, descriptor.width / 2);
            descriptor.height = Mathf.Max(1, descriptor.height / 2);
            descriptor.graphicsFormat = GraphicsFormat.R8_UNorm;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;
            descriptor.bindMS = false;
            descriptor.useMipMap = false;
            descriptor.autoGenerateMips = false;
            descriptor.enableRandomWrite = false;
            return descriptor;
        }

        private static void DrawFullscreen(
            CommandBuffer cmd,
            Material drawMaterial,
            int shaderPass,
            RenderTargetIdentifier target,
            int width,
            int height)
        {
            cmd.SetRenderTarget(target);
            cmd.SetViewport(new Rect(0f, 0f, width, height));
            cmd.DrawProcedural(
                Matrix4x4.identity,
                drawMaterial,
                shaderPass,
                MeshTopology.Triangles,
                3,
                1);
        }

        private Material GetMaterial()
        {
            if (material != null)
            {
                return material;
            }

            if (!BurtLightShaftOcclusionUtility.TryGetSupportedShader(
                    out var shader))
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning(
                        "BurtRP cannot use shader: " +
                        BurtLightShaftOcclusionUtility.ShaderName);
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
    }

    internal sealed class BurtReleaseLightShaftOcclusionPass : BurtRenderPass
    {
        public override string Name => "Burt Release Light Shaft Occlusion";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Release;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtLightShaftOcclusionUtility.ShouldUseLightShaftOcclusion(builder.Request))
            {
                return;
            }

            builder.ReadRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            var releaseOcclusion = BurtLightShaftOcclusionUtility.WasProducedForRequest(context.Request);
            BurtLightShaftOcclusionUtility.EndCameraRequest(
                cmd,
                context.Request);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
            if (releaseOcclusion)
            {
                context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
            }
        }
    }

    internal sealed class BurtLightShaftBloomPass : BurtRenderPass
    {
        private const int RadiusBlurPassIndex = 1;
        private const int BloomSetupPassIndex = 3;
        private const int BloomFinalPassIndex = 4;

        private static readonly int CameraDepthTextureId =
            BurtRenderGraphResourceRegistry.CameraDepthTextureId;
        private static readonly int SceneColorTextureId =
            Shader.PropertyToID("_BurtLightShaftSceneColorTexture");
        private static readonly int InputTextureId =
            Shader.PropertyToID("_BurtLightShaftInputTexture");
        private static readonly int ParametersId =
            Shader.PropertyToID("_BurtLightShaftParameters");
        private static readonly int TextureSpaceOriginId =
            Shader.PropertyToID("_BurtLightShaftTextureSpaceOrigin");
        private static readonly int AspectRatioId =
            Shader.PropertyToID("_BurtLightShaftAspectRatioAndInvAspectRatio");
        private static readonly int BlurParametersId =
            Shader.PropertyToID("_BurtLightShaftBlurParameters");
        private static readonly int BlurUvMinMaxId =
            Shader.PropertyToID("_BurtLightShaftBlurUVMinMax");
        private static readonly int BloomTintAndThresholdId =
            Shader.PropertyToID("_BurtLightShaftBloomTintAndThreshold");

        private Material material;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Light Shaft Bloom";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.PostProcess;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!BurtLightShaftOcclusionUtility.ShouldUseLightShaftBloom(builder.Request))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.LightShaftBloomName);
            builder.WriteRenderTarget(BurtRenderGraphResourceRegistry.LightShaftBloomTempName);
            builder.AllowUnconsumedRenderTargetWrite(BurtRenderGraphResourceRegistry.LightShaftBloomName);
            builder.AllowUnconsumedRenderTargetWrite(BurtRenderGraphResourceRegistry.LightShaftBloomTempName);
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null ||
                !BurtLightShaftOcclusionUtility.ShouldUseLightShaftBloom(context.Request))
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var cameraColor = context.CameraColorTarget;
            var cameraDepth = context.CameraDepthTarget;
            if (camera == null || !cameraColor.IsValid || !cameraDepth.IsValid ||
                !BurtLightShaftOcclusionUtility.TryResolveTextureSpaceSunOrigin(
                    context.Request,
                    out var textureSpaceOrigin))
            {
                return;
            }

            var settings = BurtLightShaftOcclusionUtility.ResolveSettings();
            var drawMaterial = GetMaterial();
            var bloomFormat = BurtLightShaftOcclusionUtility.ResolveBloomGraphicsFormat();
            if (!settings.BloomEnabled || drawMaterial == null || bloomFormat == GraphicsFormat.None)
            {
                return;
            }

            var fullDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var halfDescriptor = fullDescriptor;
            halfDescriptor.width = Mathf.Max(1, halfDescriptor.width / 2);
            halfDescriptor.height = Mathf.Max(1, halfDescriptor.height / 2);
            halfDescriptor.graphicsFormat = bloomFormat;
            halfDescriptor.depthBufferBits = 0;
            halfDescriptor.msaaSamples = 1;
            halfDescriptor.bindMS = false;
            halfDescriptor.useMipMap = false;
            halfDescriptor.autoGenerateMips = false;
            halfDescriptor.enableRandomWrite = false;

            context.ResourceRegistry.SetRenderTargetDescriptor(
                BurtRenderGraphResourceRegistry.LightShaftBloomName,
                halfDescriptor,
                FilterMode.Bilinear,
                "Burt Light Shaft Bloom");
            context.ResourceRegistry.SetRenderTargetDescriptor(
                BurtRenderGraphResourceRegistry.LightShaftBloomTempName,
                halfDescriptor,
                FilterMode.Bilinear,
                "Burt Light Shaft Bloom Temp");
            var bloomTarget = context.ResourceRegistry.AllocateRenderTarget(
                BurtRenderGraphResourceRegistry.LightShaftBloomName);
            var bloomTempTarget = context.ResourceRegistry.AllocateRenderTarget(
                BurtRenderGraphResourceRegistry.LightShaftBloomTempName);
            if (!bloomTarget.IsValid || !bloomTempTarget.IsValid)
            {
                context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftBloomTempName);
                context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftBloomName);
                return;
            }

            var halfWidth = halfDescriptor.width;
            var halfHeight = halfDescriptor.height;
            var fullWidth = Mathf.Max(1, fullDescriptor.width);
            var fullHeight = Mathf.Max(1, fullDescriptor.height);
            var aspectRatio = fullWidth / (float)fullHeight;
            var uvMinMax = new Vector4(
                0.5f / halfWidth,
                0.5f / halfHeight,
                1f - 0.5f / halfWidth,
                1f - 0.5f / halfHeight);
            var linearBloomTint = settings.BloomTint.linear;

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetGlobalTexture(CameraDepthTextureId, cameraDepth.Identifier);
            cmd.SetGlobalTexture(SceneColorTextureId, cameraColor.Identifier);
            cmd.SetGlobalVector(ParametersId, new Vector4(
                1f / Mathf.Max(settings.DepthRange, 0.0001f),
                settings.MaskDarkness,
                settings.BloomScale,
                settings.BloomMaxBrightness));
            cmd.SetGlobalVector(BloomTintAndThresholdId, new Vector4(
                linearBloomTint.r,
                linearBloomTint.g,
                linearBloomTint.b,
                settings.BloomThreshold));
            cmd.SetGlobalVector(TextureSpaceOriginId, new Vector4(
                textureSpaceOrigin.x,
                textureSpaceOrigin.y,
                0f,
                0f));
            cmd.SetGlobalVector(AspectRatioId, new Vector4(
                1f,
                aspectRatio,
                1f,
                1f / Mathf.Max(aspectRatio, 0.0001f)));
            cmd.SetGlobalVector(BlurUvMinMaxId, uvMinMax);

            DrawFullscreen(
                cmd,
                drawMaterial,
                BloomSetupPassIndex,
                bloomTarget.Identifier,
                halfWidth,
                halfHeight);

            var source = bloomTarget.Identifier;
            var destination = bloomTempTarget.Identifier;
            for (var passIndex = 0;
                 passIndex < BurtLightShaftOcclusionUtility.BlurPassCount;
                 passIndex++)
            {
                cmd.SetGlobalTexture(InputTextureId, source);
                cmd.SetGlobalVector(BlurParametersId, new Vector4(
                    BurtLightShaftOcclusionUtility.BlurSampleCount,
                    BurtLightShaftOcclusionUtility.FirstPassDistance,
                    passIndex,
                    0f));
                DrawFullscreen(
                    cmd,
                    drawMaterial,
                    RadiusBlurPassIndex,
                    destination,
                    halfWidth,
                    halfHeight);

                var swap = source;
                source = destination;
                destination = swap;
            }

            cmd.SetGlobalTexture(InputTextureId, source);
            DrawFullscreen(
                cmd,
                drawMaterial,
                BloomFinalPassIndex,
                cameraColor.Identifier,
                fullWidth,
                fullHeight);
            context.ExecuteLegacyCommandBuffer(cmd);
            context.ReleaseCommandBuffer(cmd);
            context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftBloomTempName);
            context.ResourceRegistry.ReleaseRenderTarget(BurtRenderGraphResourceRegistry.LightShaftBloomName);
        }

        private static void DrawFullscreen(
            CommandBuffer cmd,
            Material drawMaterial,
            int shaderPass,
            RenderTargetIdentifier target,
            int width,
            int height)
        {
            cmd.SetRenderTarget(target);
            cmd.SetViewport(new Rect(0f, 0f, width, height));
            cmd.DrawProcedural(
                Matrix4x4.identity,
                drawMaterial,
                shaderPass,
                MeshTopology.Triangles,
                3,
                1);
        }

        private Material GetMaterial()
        {
            if (material != null)
            {
                return material;
            }

            if (!BurtLightShaftOcclusionUtility.TryGetSupportedShader(out var shader))
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning(
                        "BurtRP cannot use shader: " +
                        BurtLightShaftOcclusionUtility.ShaderName);
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
    }
}
