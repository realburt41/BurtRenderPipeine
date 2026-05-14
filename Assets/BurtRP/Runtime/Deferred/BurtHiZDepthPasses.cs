using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal sealed class BurtAllocateHiZDepthPass : BurtRenderPass
    {
        public override string Name => "Burt Allocate HiZ Depth";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.WriteHiZDepth();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.HiZDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.HiZDepthName);
            if (!target.IsValid)
            {
                return;
            }

            var camera = BurtHiZDepthPassUtility.ResolveCamera(context);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera);
            var cmd = CommandBufferPool.Get(Name);
            cmd.GetTemporaryRT(BurtRenderGraphResourceRegistry.HiZDepthTextureId, descriptor, FilterMode.Point);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.HiZDepthTextureId, target.Identifier);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtBuildHiZDepthPass : BurtRenderPass
    {
        private const string HiZDepthShaderName = BurtHiZDepthPassUtility.HiZDepthShaderName;
        private static readonly int HiZSourceTextureId = Shader.PropertyToID("_BurtHiZSourceTexture");
        private static readonly int HiZSourceTexelSizeId = Shader.PropertyToID("_BurtHiZSourceTexelSize");
        private static readonly int HiZMipCountId = Shader.PropertyToID("_BurtHiZMipCount");
        private static readonly int HiZMaxMipId = Shader.PropertyToID("_BurtHiZMaxMip");
        private Material hiZDepthMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Build HiZ Depth";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadCameraDepth();
            builder.WriteHiZDepth();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return;
            }

            var cameraDepthTarget = context.CameraDepthTarget;
            var hiZDepthTarget = context.HiZDepthTarget;
            if (!cameraDepthTarget.IsValid || !hiZDepthTarget.IsValid)
            {
                return;
            }

            var material = GetHiZDepthMaterial();
            if (material == null)
            {
                return;
            }

            var camera = BurtHiZDepthPassUtility.ResolveCamera(context);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera);
            var mipCount = BurtRenderTargetDescriptorUtility.CalculateMipCount(descriptor.width, descriptor.height);
            var cmd = CommandBufferPool.Get(Name);

            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.CameraDepthTextureId, cameraDepthTarget.Identifier);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.HiZDepthTextureId, hiZDepthTarget.Identifier);
            cmd.SetGlobalFloat(HiZMipCountId, mipCount);
            cmd.SetGlobalFloat(HiZMaxMipId, Mathf.Max(0, mipCount - 1));

            SetHiZMipRenderTarget(cmd, 0, descriptor.width, descriptor.height);
            cmd.SetGlobalVector(HiZSourceTexelSizeId, CreateTexelSize(descriptor.width, descriptor.height));
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);

            var sourceWidth = descriptor.width;
            var sourceHeight = descriptor.height;
            for (var mip = 1; mip < mipCount; mip++)
            {
                var targetWidth = Mathf.Max(1, (sourceWidth + 1) / 2);
                var targetHeight = Mathf.Max(1, (sourceHeight + 1) / 2);
                var sourceMip = new RenderTargetIdentifier(BurtRenderGraphResourceRegistry.HiZDepthTextureId, mip - 1, CubemapFace.Unknown, 0);

                cmd.SetGlobalTexture(HiZSourceTextureId, sourceMip);
                cmd.SetGlobalVector(HiZSourceTexelSizeId, CreateTexelSize(sourceWidth, sourceHeight));
                SetHiZMipRenderTarget(cmd, mip, targetWidth, targetHeight);
                cmd.DrawProcedural(Matrix4x4.identity, material, 1, MeshTopology.Triangles, 3, 1);

                sourceWidth = targetWidth;
                sourceHeight = targetHeight;
            }

            RestoreCameraTargets(cmd, context, descriptor.width, descriptor.height);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static Vector4 CreateTexelSize(int width, int height)
        {
            var safeWidth = Mathf.Max(1, width);
            var safeHeight = Mathf.Max(1, height);
            return new Vector4(1f / safeWidth, 1f / safeHeight, safeWidth, safeHeight);
        }

        private static void SetHiZMipRenderTarget(CommandBuffer cmd, int mip, int width, int height)
        {
            var target = new RenderTargetIdentifier(BurtRenderGraphResourceRegistry.HiZDepthTextureId, mip, CubemapFace.Unknown, 0);
            cmd.SetRenderTarget(target);
            cmd.SetViewport(new Rect(0f, 0f, Mathf.Max(1, width), Mathf.Max(1, height)));
        }

        private static void RestoreCameraTargets(CommandBuffer cmd, BurtRenderGraphContext context, int width, int height)
        {
            var cameraColorTarget = context.CameraColorTarget;
            var cameraDepthTarget = context.CameraDepthTarget;
            if (!cameraColorTarget.IsValid || !cameraDepthTarget.IsValid)
            {
                return;
            }

            cmd.SetRenderTarget(cameraColorTarget.Identifier, cameraDepthTarget.Identifier);
            cmd.SetViewport(new Rect(0f, 0f, Mathf.Max(1, width), Mathf.Max(1, height)));
        }

        private Material GetHiZDepthMaterial()
        {
            if (hiZDepthMaterial != null)
            {
                return hiZDepthMaterial;
            }

            var shader = Shader.Find(HiZDepthShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogError("BurtRP missing HiZ shader: " + HiZDepthShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            hiZDepthMaterial = new Material(shader);
            hiZDepthMaterial.hideFlags = HideFlags.HideAndDontSave;
            return hiZDepthMaterial;
        }
    }

    internal sealed class BurtReleaseHiZDepthPass : BurtRenderPass
    {
        public override string Name => "Burt Release HiZ Depth";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            builder.ReadHiZDepth();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var target = context != null ? context.HiZDepthTarget : BurtRenderTargetHandle.Invalid(BurtRenderGraphResourceRegistry.HiZDepthName);
            if (!target.IsValid)
            {
                return;
            }

            var cmd = CommandBufferPool.Get(Name);
            cmd.ReleaseTemporaryRT(BurtRenderGraphResourceRegistry.HiZDepthTextureId);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    internal sealed class BurtDebugHiZDepthPass : BurtRenderPass
    {
        private const string DebugHiZDepthShaderName = "Hidden/BurtRP/DebugHiZDepth";
        private static readonly int HiZDepthTextureId = BurtRenderGraphResourceRegistry.HiZDepthTextureId;
        private static readonly int HiZDebugMipId = Shader.PropertyToID("_BurtHiZDebugMip");
        private static readonly int HiZDebugScaleId = Shader.PropertyToID("_BurtHiZDebugScale");
        private static readonly int HiZDebugMaxMipId = Shader.PropertyToID("_BurtHiZDebugMaxMip");
        private Material debugHiZDepthMaterial;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Debug HiZ Depth";

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!ShouldUseHiZDebugView(builder.Asset))
            {
                return;
            }

            builder.ReadHiZDepth();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !ShouldUseHiZDebugView(context.Asset))
            {
                return;
            }

            var cameraColorTarget = context.CameraColorTarget;
            var hiZDepthTarget = context.HiZDepthTarget;
            if (!cameraColorTarget.IsValid || !hiZDepthTarget.IsValid)
            {
                return;
            }

            var material = GetDebugHiZDepthMaterial();
            if (material == null)
            {
                return;
            }

            var camera = BurtHiZDepthPassUtility.ResolveCamera(context);
            var descriptor = BurtRenderTargetDescriptorUtility.CreateHiZDepthDescriptor(camera);
            var cameraColorDescriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var mipCount = BurtRenderTargetDescriptorUtility.CalculateMipCount(descriptor.width, descriptor.height);
            var maxMip = Mathf.Max(0, mipCount - 1);
            var selectedMip = Mathf.Clamp(context.Asset.HiZDebugMip, 0, maxMip);
            var cmd = CommandBufferPool.Get(Name);

            cmd.SetRenderTarget(cameraColorTarget.Identifier);
            cmd.SetViewport(new Rect(0f, 0f, Mathf.Max(1, cameraColorDescriptor.width), Mathf.Max(1, cameraColorDescriptor.height)));
            cmd.SetGlobalTexture(HiZDepthTextureId, hiZDepthTarget.Identifier);
            cmd.SetGlobalFloat(HiZDebugMipId, selectedMip);
            cmd.SetGlobalFloat(HiZDebugScaleId, context.Asset.HiZDebugScale);
            cmd.SetGlobalFloat(HiZDebugMaxMipId, maxMip);
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);

            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public static bool ShouldUseHiZDebugView(BurtRenderPipelineAsset asset)
        {
            return asset != null && asset.RendererMode == BurtRendererMode.Deferred && asset.EnableHiZDebugView;
        }

        private Material GetDebugHiZDepthMaterial()
        {
            if (debugHiZDepthMaterial != null)
            {
                return debugHiZDepthMaterial;
            }

            var shader = Shader.Find(DebugHiZDepthShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugHiZDepthShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            debugHiZDepthMaterial = new Material(shader);
            debugHiZDepthMaterial.hideFlags = HideFlags.HideAndDontSave;
            return debugHiZDepthMaterial;
        }
    }

    internal static class BurtHiZDepthPassUtility
    {
        public const string HiZDepthShaderName = "Hidden/BurtRP/HiZDepthPyramid";
        private static int shaderAvailabilityFrame = -1;
        private static bool shaderAvailable;

        public static Camera ResolveCamera(BurtRenderGraphContext context)
        {
            var request = context != null ? context.Request : null;
            return request != null ? request.Camera : null;
        }

        public static bool IsHiZDepthShaderAvailable()
        {
            var frame = Time.frameCount;
            if (shaderAvailabilityFrame == frame)
            {
                return shaderAvailable;
            }

            shaderAvailabilityFrame = frame;
            shaderAvailable = Shader.Find(HiZDepthShaderName) != null;
            return shaderAvailable;
        }

        public static bool ShouldUseHiZDepth(BurtRenderRequest request, BurtRenderPipelineAsset asset)
        {
            if (request == null || !request.IsValid || asset == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            if (asset.RendererMode != BurtRendererMode.Deferred)
            {
                return false;
            }

            return BurtDebugHiZDepthPass.ShouldUseHiZDebugView(asset) ||
                BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflectionHiZTrace(request, asset) ||
                BurtScreenSpaceReflectionPassUtility.ShouldUseScreenSpaceReflectionHiZDiagnostics();
        }
    }
}
