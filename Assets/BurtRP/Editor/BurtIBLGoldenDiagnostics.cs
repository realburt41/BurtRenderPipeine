using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtIBLGoldenDiagnostics
    {
        private const float PureSpecularRoughness = 0.06f;
        private const float DefaultSSRMaxMip = 4f;
        private static readonly float[] RoughnessSamples = { 0f, 0.03f, 0.06f, 0.12f, 0.25f, 0.5f, 0.75f, 1f };
        private static readonly float[] NoVSamples = { 0.02f, 0.25f, 0.5f, 0.75f, 0.98f };

        [MenuItem("BurtRP/Diagnostics/Print IBL SSR Golden Metrics")]
        public static void PrintGoldenMetrics()
        {
            var builder = new StringBuilder(2048);
            builder.AppendLine("[BurtRP][GoldenDiagnostics]");
            builder.AppendLine("IBL=" + BurtIBLDiagnostics.NumericValidationStatus);
            BurtIBLDiagnostics.AppendXRenderComparisonReport(builder, SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView.camera : null, GetActiveBurtAsset());
            builder.AppendLine("SSRApply=XRenderPureReflectMip0Alpha pureSpecular=" + Format(PureSpecularRoughness) + " maxMip=" + Format(DefaultSSRMaxMip));

            builder.Append("SSRMipCurve=");
            for (var i = 0; i < RoughnessSamples.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(",");
                }

                var roughness = RoughnessSamples[i];
                builder.Append(Format(roughness)).Append("->").Append(Format(ComputeSSRApplyMip(roughness, 0.6f, DefaultSSRMaxMip)));
            }

            builder.AppendLine();
            builder.Append("FGApprox=");
            var maxA = 0f;
            var maxB = 0f;
            var minA = float.PositiveInfinity;
            var minB = float.PositiveInfinity;
            var maxEnergy = 0f;
            for (var r = 0; r < RoughnessSamples.Length; r++)
            {
                for (var n = 0; n < NoVSamples.Length; n++)
                {
                    var fg = PrefilteredDFGApprox(RoughnessSamples[r], NoVSamples[n]);
                    minA = Mathf.Min(minA, fg.x);
                    minB = Mathf.Min(minB, fg.y);
                    maxA = Mathf.Max(maxA, fg.x);
                    maxB = Mathf.Max(maxB, fg.y);
                    maxEnergy = Mathf.Max(maxEnergy, fg.x + fg.y);
                }
            }

            builder.Append("A[").Append(Format(minA)).Append(",").Append(Format(maxA)).Append("]");
            builder.Append(" B[").Append(Format(minB)).Append(",").Append(Format(maxB)).Append("]");
            builder.Append(" MaxAPlusB=").Append(Format(maxEnergy));
            builder.AppendLine();

            Debug.Log(builder.ToString());
        }

        private static float ComputeSSRApplyMip(float roughness, float roughnessMask, float maxMip)
        {
            if (roughness < PureSpecularRoughness)
            {
                return 0f;
            }

            var range = Mathf.Max(roughnessMask - PureSpecularRoughness, 0.00001f);
            return Mathf.Clamp01((roughness - PureSpecularRoughness) / range) * Mathf.Max(0f, maxMip);
        }

        private static Vector2 PrefilteredDFGApprox(float roughness, float noV)
        {
            var r0 = -roughness + 1f;
            var r1 = -0.0275f * roughness + 0.0425f;
            var r2 = -0.572f * roughness + 1.04f;
            var r3 = 0.022f * roughness - 0.04f;
            var a004 = Mathf.Min(r0 * r0, Mathf.Pow(2f, -9.28f * Mathf.Clamp01(noV))) * r0 + r1;
            return new Vector2(-1.04f * a004 + r2, 1.04f * a004 + r3);
        }

        private static string Format(float value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static BurtRenderPipelineAsset GetActiveBurtAsset()
        {
            var asset = GraphicsSettings.currentRenderPipeline as BurtRenderPipelineAsset;
            return asset != null ? asset : QualitySettings.renderPipeline as BurtRenderPipelineAsset;
        }
    }
}
