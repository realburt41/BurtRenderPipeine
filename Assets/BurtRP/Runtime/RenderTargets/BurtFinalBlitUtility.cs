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

            if (camera.cameraType == CameraType.SceneView)
            {
                return 1f;
            }

            if (camera.cameraType == CameraType.Preview)
            {
                return 1f;
            }

            // User cameras that render into a RenderTexture stay in RT orientation.
            // SceneView/Preview can also expose editor-owned target textures, so those
            // editor camera types must be handled before this generic RT branch.
            if (camera.targetTexture != null)
            {
                return 0f;
            }

            if (camera.cameraType == CameraType.Game)
            {
                return 0f;
            }

            return 0f;
        }
    }
}
