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
        private static readonly int OcclusionTempTextureId =
            Shader.PropertyToID("_BurtLightShaftOcclusionTempTexture");
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
            var occlusionTarget = context.ResourceRegistry.GetRenderTarget(
                BurtRenderGraphResourceRegistry.LightShaftOcclusionName);
            if (camera == null ||
                !depthTarget.IsValid ||
                !occlusionTarget.IsValid ||
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
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            var aspectRatio = width / (float)height;
            var uvMinMax = new Vector4(
                0.5f / width,
                0.5f / height,
                1f - 0.5f / width,
                1f - 0.5f / height);

            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(OcclusionTextureId, descriptor, FilterMode.Bilinear);
            cmd.GetTemporaryRT(OcclusionTempTextureId, descriptor, FilterMode.Bilinear);
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
            var destination = new RenderTargetIdentifier(OcclusionTempTextureId);
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
            cmd.ReleaseTemporaryRT(OcclusionTempTextureId);
            cmd.SetGlobalTexture(OcclusionTextureId, occlusionTarget.Identifier);
            context.ExecuteLegacyCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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

            var cmd = CommandBufferPool.Get(Name);
            if (BurtLightShaftOcclusionUtility.WasProducedForRequest(
                    context.Request))
            {
                cmd.ReleaseTemporaryRT(
                    BurtRenderGraphResourceRegistry.LightShaftOcclusionTextureId);
            }

            BurtLightShaftOcclusionUtility.EndCameraRequest(
                cmd,
                context.Request);
            context.ExecuteLegacyCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
