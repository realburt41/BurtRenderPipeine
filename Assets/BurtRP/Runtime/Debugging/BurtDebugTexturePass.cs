using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal enum BurtDebugTextureSource
    {
        CameraDepth,
        HiZDepth,
        MainLightShadow,
        PerObjectShadowAtlas
    }

    /// <summary>
    /// XRender-style shared presenter for simple full-screen texture diagnostics.
    /// Specialized diagnostics keep their own passes; raw depth-like resources share
    /// this material and shader.
    /// </summary>
    internal sealed class BurtDebugTexturePass : BurtRenderPass
    {
        private const string ShaderName = "Hidden/BurtRP/DebugTexture";
        private static readonly int DebugTextureId = Shader.PropertyToID("_BurtDebugTexture");
        private static readonly int DebugParamsId = Shader.PropertyToID("_BurtDebugTextureParams");

        private readonly BurtDebugTextureSource source;
        private Material material;
        private bool loggedMissingShader;

        public BurtDebugTexturePass(BurtDebugTextureSource source)
        {
            this.source = source;
        }

        public override string Name => "Burt Debug Texture - " + source;

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            switch (source)
            {
                case BurtDebugTextureSource.CameraDepth:
                    builder.ReadCameraDepth();
                    break;
                case BurtDebugTextureSource.HiZDepth:
                    builder.ReadHiZDepth();
                    break;
                case BurtDebugTextureSource.MainLightShadow:
                    builder.ReadMainLightShadowMap();
                    break;
                case BurtDebugTextureSource.PerObjectShadowAtlas:
                    builder.ReadPerObjectShadowAtlas();
                    break;
            }

            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !TryGetSource(context, out var sourceTarget))
            {
                return;
            }

            var cameraColor = context.CameraColorTarget;
            var debugMaterial = GetMaterial(context.Asset);
            if (!cameraColor.IsValid || debugMaterial == null)
            {
                return;
            }

            var linearize = source == BurtDebugTextureSource.CameraDepth ||
                source == BurtDebugTextureSource.HiZDepth
                ? 1f
                : 0f;
            var scale = ResolveScale(context);
            var mip = ResolveMip(context);
            var flipY = BurtFinalBlitUtility.ResolveFinalBlitYFlip(context.Request);
            var shaderPass = source == BurtDebugTextureSource.HiZDepth ? 1 : 0;

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetRenderTarget(cameraColor.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(
                cmd,
                context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalTexture(DebugTextureId, sourceTarget.Identifier);
            cmd.SetGlobalVector(DebugParamsId, new Vector4(scale, flipY, linearize, mip));
            cmd.DrawProcedural(Matrix4x4.identity, debugMaterial, shaderPass, MeshTopology.Triangles, 3, 1);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private bool TryGetSource(BurtRenderGraphContext context, out BurtRenderTargetHandle target)
        {
            switch (source)
            {
                case BurtDebugTextureSource.CameraDepth:
                    target = context.CameraDepthTarget;
                    break;
                case BurtDebugTextureSource.HiZDepth:
                    target = context.HiZDepthTarget;
                    break;
                case BurtDebugTextureSource.MainLightShadow:
                    target = context.MainLightShadowMapTarget;
                    break;
                case BurtDebugTextureSource.PerObjectShadowAtlas:
                    target = context.PerObjectShadowAtlasTarget;
                    break;
                default:
                    target = BurtRenderTargetHandle.Invalid("BurtDebugTexture");
                    break;
            }

            return target.IsValid;
        }

        private float ResolveScale(BurtRenderGraphContext context)
        {
            if (context.Asset == null)
            {
                return 1f;
            }

            return source == BurtDebugTextureSource.CameraDepth
                ? context.Asset.DepthDebugScale
                : source == BurtDebugTextureSource.HiZDepth
                    ? context.Asset.HiZDebugScale
                    : 1f;
        }

        private float ResolveMip(BurtRenderGraphContext context)
        {
            if (source != BurtDebugTextureSource.HiZDepth || context.Asset == null)
            {
                return 0f;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var descriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera);
            var mipCount = BurtRenderTargetDescriptorUtility.CalculateMipCount(descriptor.width, descriptor.height);
            return Mathf.Clamp(context.Asset.HiZDebugMip, 0, Mathf.Max(0, mipCount - 1));
        }

        private Material GetMaterial(BurtRenderPipelineAsset asset)
        {
            if (material != null)
            {
                return material;
            }

            var shader = asset != null && asset.RuntimeResources != null
                ? asset.RuntimeResources.DebugTextureShader
                : null;
            if (shader == null)
            {
                shader = Shader.Find(ShaderName);
            }
            if (shader == null)
            {
                if (!loggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + ShaderName);
                    loggedMissingShader = true;
                }

                return null;
            }

            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return material;
        }
    }
}
