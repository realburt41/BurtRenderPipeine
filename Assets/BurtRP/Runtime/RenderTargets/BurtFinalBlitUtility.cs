using UnityEngine;

namespace Burt.RenderPipeline
{
    internal static class BurtFinalBlitUtility
    {
        public static float ResolveFinalBlitYFlip(BurtRenderRequest request)
        {
            if (!SystemInfo.graphicsUVStartsAtTop)
            {
                return 0f;
            }

            var camera = request != null ? request.Camera : null;
            if (camera == null)
            {
                return 0f;
            }

            // SceneView, Preview, and user cameras that render into a RenderTexture are
            // already in RT orientation. Treating them like a backbuffer display blit
            // flips the final image vertically.
            if (camera.targetTexture != null)
            {
                return 0f;
            }

            if (camera.cameraType == CameraType.SceneView)
            {
                return 1f;
            }

            if (camera.cameraType == CameraType.Preview)
            {
                return 1f;
            }

            if (camera.cameraType == CameraType.Game)
            {
                return 0f;
            }

            return 0f;
        }
    }
}
