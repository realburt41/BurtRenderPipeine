using System.Text;
using UnityEngine;

namespace Burt.RenderPipeline
{
    public static class BurtIBLDiagnostics
    {
        public static string NumericValidationStatus => BurtImageBasedFilterUtility.NumericValidationStatus;

        public static void AppendXRenderComparisonReport(StringBuilder builder, Camera camera, BurtRenderPipelineAsset asset)
        {
            BurtIndirectLightingUtility.AppendXRenderComparisonReport(builder, camera, asset);
        }

        public static string ValidatePreIntegratedFGLut(Texture2D lut)
        {
            return BurtImageBasedFilterUtility.ValidatePreIntegratedFGLut(lut);
        }
    }
}
