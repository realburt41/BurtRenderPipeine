using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal static class BurtDeferredStencilTextureUtility
    {
        private static RenderTexture fallbackStencilTexture;

        public static void BindGlobal(CommandBuffer cmd, BurtRenderTargetHandle cameraDepthTarget, Camera camera)
        {
            if (cmd == null)
            {
                return;
            }

            SetGlobalTexelSize(cmd, camera);
            cmd.SetGlobalFloat(BurtRenderGraphResourceRegistry.DeferredStencilTextureAvailableId, 0f);
            cmd.SetGlobalTexture(BurtRenderGraphResourceRegistry.DeferredStencilTextureId, GetFallbackStencilTexture());
        }

        public static void BindCompute(CommandBuffer cmd, ComputeShader shader, int kernel, BurtRenderTargetHandle cameraDepthTarget, Camera camera)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            var texelSize = GetTexelSize(camera);
            cmd.SetComputeVectorParam(shader, BurtRenderGraphResourceRegistry.DeferredStencilTexelSizeId, texelSize);
            cmd.SetComputeFloatParam(shader, BurtRenderGraphResourceRegistry.DeferredStencilTextureAvailableId, 0f);
            cmd.SetComputeTextureParam(shader, kernel, BurtRenderGraphResourceRegistry.DeferredStencilTextureId, GetFallbackStencilTexture());
        }

        private static void SetGlobalTexelSize(CommandBuffer cmd, Camera camera)
        {
            cmd.SetGlobalVector(BurtRenderGraphResourceRegistry.DeferredStencilTexelSizeId, GetTexelSize(camera));
        }

        private static Vector4 GetTexelSize(Camera camera)
        {
            var descriptor = BurtRenderTargetDescriptorUtility.CreateCameraColorDescriptor(camera);
            var width = Mathf.Max(1, descriptor.width);
            var height = Mathf.Max(1, descriptor.height);
            return new Vector4(1f / width, 1f / height, width, height);
        }

        private static Texture GetFallbackStencilTexture()
        {
            if (fallbackStencilTexture != null)
            {
                return fallbackStencilTexture;
            }

            var format = SelectFallbackStencilFormat();
            if (format == GraphicsFormat.None)
            {
                return Texture2D.blackTexture;
            }

            var descriptor = new RenderTextureDescriptor(1, 1)
            {
                graphicsFormat = format,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false,
                dimension = TextureDimension.Tex2D
            };

            fallbackStencilTexture = new RenderTexture(descriptor)
            {
                name = "Burt Deferred Stencil Fallback",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            fallbackStencilTexture.Create();
            return fallbackStencilTexture;
        }

        private static GraphicsFormat SelectFallbackStencilFormat()
        {
            if (SystemInfo.IsFormatSupported(GraphicsFormat.R32G32_UInt, FormatUsage.Sample))
            {
                return GraphicsFormat.R32G32_UInt;
            }

            if (SystemInfo.IsFormatSupported(GraphicsFormat.R16G16_UInt, FormatUsage.Sample))
            {
                return GraphicsFormat.R16G16_UInt;
            }

            return SystemInfo.IsFormatSupported(GraphicsFormat.R8G8_UInt, FormatUsage.Sample)
                ? GraphicsFormat.R8G8_UInt
                : GraphicsFormat.None;
        }
    }
}
