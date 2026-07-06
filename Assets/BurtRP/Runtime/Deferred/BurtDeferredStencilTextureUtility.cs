using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal static class BurtDeferredStencilTextureUtility
    {
        public static void BindGlobal(CommandBuffer cmd, BurtRenderTargetHandle cameraDepthTarget, Camera camera)
        {
            if (cmd == null)
            {
                return;
            }

            SetGlobalTexelSize(cmd, camera);
        }

        public static void BindCompute(CommandBuffer cmd, ComputeShader shader, int kernel, BurtRenderTargetHandle cameraDepthTarget, Camera camera)
        {
            if (cmd == null || shader == null)
            {
                return;
            }

            var texelSize = GetTexelSize(camera);
            cmd.SetComputeVectorParam(shader, BurtRenderGraphResourceRegistry.DeferredStencilTexelSizeId, texelSize);
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
    }
}
