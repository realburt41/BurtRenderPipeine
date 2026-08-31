using System.Collections.Generic;
using UnityEngine;

namespace Burt.RenderPipeline
{
    /// <summary>
    /// Tracks XRender's DRS ownership transition. Scene passes render at input
    /// resolution; after TAAU, image-space passes operate at output resolution.
    /// </summary>
    internal static class BurtRenderResolutionStageUtility
    {
        private static readonly HashSet<int> OutputResolutionCameras = new HashSet<int>();

        public static bool IsOutputResolutionStage(Camera camera)
        {
            return camera != null && OutputResolutionCameras.Contains(camera.GetInstanceID());
        }

        public static void BeginInputResolutionStage(Camera camera)
        {
            if (camera != null)
            {
                OutputResolutionCameras.Remove(camera.GetInstanceID());
            }
        }

        public static void BeginOutputResolutionStage(Camera camera)
        {
            if (camera != null && BurtRenderTargetDescriptorUtility.ResolveInputRenderScale(camera) < 0.9999f)
            {
                OutputResolutionCameras.Add(camera.GetInstanceID());
            }
        }

        public static void ForceInputResolution(ref RenderTextureDescriptor descriptor, Camera camera)
        {
            var scale = BurtRenderTargetDescriptorUtility.ResolveInputRenderScale(camera);
            descriptor.width = Mathf.Max(1, Mathf.RoundToInt(BurtRenderTargetDescriptorUtility.ResolveOutputTargetWidth(camera) * scale));
            descriptor.height = Mathf.Max(1, Mathf.RoundToInt(BurtRenderTargetDescriptorUtility.ResolveOutputTargetHeight(camera) * scale));
        }
    }
}
