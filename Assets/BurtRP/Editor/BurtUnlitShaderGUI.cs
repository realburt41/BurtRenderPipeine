using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtUnlitShaderGUI : ShaderGUI
    {
        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Surface Options");
        private static readonly GUIContent BaseInputsLabel = new GUIContent("Base Inputs");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static bool showSurfaceOptions = true;
        private static bool showBaseInputs = true;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;
        private MaterialProperty baseColor;
        private MaterialProperty responsiveAA;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            baseColor = FindProperty("_BaseColor", properties, false);
            responsiveAA = FindProperty("_ResponsiveAA", properties, false);

            Material material = materialEditor.target as Material;
            DrawSurfaceOptions(material);
            DrawBaseInputs();
        }

        public override void ValidateMaterial(Material material)
        {
            ValidateMaterialState(material);
        }

        internal static void ValidateMaterialState(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetOverrideTag("RenderType", "Opaque");
            material.SetOverrideTag("Queue", string.Empty);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("BurtDepthOnly", true);
            BurtShadingModelIds.ApplyMotionVectorStencilProperties(material);
            material.SetShaderPassEnabled("BurtMotionVectors", true);
            material.SetShaderPassEnabled("BurtResponsiveAAMask", true);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("BurtForward", true);
            material.SetShaderPassEnabled("BurtForwardOnly", true);
        }

        private void DrawSurfaceOptions(Material material)
        {
            if (!BurtShaderGUIUtility.BeginSection(SurfaceOptionsLabel, ref showSurfaceOptions))
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(SurfaceTypeLabel, "Opaque");
                EditorGUILayout.TextField(ShadingModelLabel, "Unlit");

                if (material != null)
                {
                    BurtShaderGUIUtility.DrawSeparator();
                    BurtShaderGUIUtility.DrawSubHeader("Resolved State");
                    EditorGUILayout.LabelField(RenderQueueLabel, new GUIContent(material.renderQueue.ToString()));
                    EditorGUILayout.TextField(EnabledPassesLabel, "BurtDepthOnly, BurtMotionVectors, BurtForward, BurtForwardOnly");
                }
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawBaseInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(BaseInputsLabel, ref showBaseInputs))
            {
                return;
            }

            BurtShaderGUIUtility.DrawProperty(materialEditor, baseColor);
            BurtShaderGUIUtility.DrawProperty(materialEditor, responsiveAA);
            BurtShaderGUIUtility.EndSection();
        }
    }
}
