using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    internal sealed class BurtPhysicalLightBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            // Play-mode scene processing must not migrate or dirty the user's
            // authoring scene. Only the serialized build scene copy is updated.
            if (report != null)
                CaptureAreaSizes(scene);
        }

        internal static void CaptureAreaSizes(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            foreach (var physicalLight in root.GetComponentsInChildren<BurtPhysicalLight>(true))
                physicalLight.CaptureAreaSizeForPlayer();
        }
    }
}
