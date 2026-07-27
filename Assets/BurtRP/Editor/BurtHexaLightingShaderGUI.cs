using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtHexaLightingShaderGUI : ShaderGUI
    {
        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Surface Options");
        private static readonly GUIContent AxesLightmapsLabel = new GUIContent("Axes Lightmaps");
        private static readonly GUIContent FlipbookLabel = new GUIContent("Flipbook");
        private static readonly GUIContent ScatteringLabel = new GUIContent("Scattering");
        private static readonly GUIContent RenderingLabel = new GUIContent("Rendering");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static readonly GUIContent PositiveAxesLabel = new GUIContent("Positive Axes Lightmap");
        private static readonly GUIContent NegativeAxesLabel = new GUIContent("Negative Axes Lightmap");
        private static readonly GUIContent MotionVectorLabel = new GUIContent("Motion Vector Map");
        private static readonly GUIContent ColumnsLabel = new GUIContent("Columns");
        private static readonly GUIContent RowsLabel = new GUIContent("Rows");
        private static readonly GUIContent PlaySpeedLabel = new GUIContent("Play Speed");
        private static readonly GUIContent MotionVectorScaleLabel = new GUIContent("Motion Vector Scale");

        private static bool showSurfaceOptions = true;
        private static bool showAxesLightmaps = true;
        private static bool showFlipbook = true;
        private static bool showScattering = true;
        private static bool showRendering = false;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty positiveAxesLightmap;
        private MaterialProperty negativeAxesLightmap;
        private MaterialProperty columns;
        private MaterialProperty rows;
        private MaterialProperty playSpeed;
        private MaterialProperty motionVectorMap;
        private MaterialProperty motionVectorScale;
        private MaterialProperty basicColor;
        private MaterialProperty density;
        private MaterialProperty overallAlpha;
        private MaterialProperty cull;
        private MaterialProperty zWrite;
        private MaterialProperty responsiveAA;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();

            DrawSurfaceOptions(materialEditor.target as Material);
            DrawAxesLightmaps();
            DrawFlipbook();
            DrawScattering();
            DrawRendering();
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

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetOverrideTag("Queue", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetShaderPassEnabled("BurtForward", true);
            material.SetShaderPassEnabled("BurtTransparentMotionVectors", true);
            material.SetShaderPassEnabled("BurtResponsiveAAMask", true);
        }

        private void CacheProperties()
        {
            positiveAxesLightmap = Find("_PositiveAxesLightmap");
            negativeAxesLightmap = Find("_NegativeAxesLightmap");
            columns = Find("_Rows");
            rows = Find("_Columns");
            playSpeed = Find("_PlaySpeed");
            motionVectorMap = Find("_MotionVectorMap");
            motionVectorScale = Find("_MotionVectorScale");
            basicColor = Find("_BasicColor");
            density = Find("_Density");
            overallAlpha = Find("_OverallAlpha");
            cull = Find("_Cull");
            zWrite = Find("_ZWrite");
            responsiveAA = Find("_ResponsiveAA");
        }

        private MaterialProperty Find(string propertyName)
        {
            return FindProperty(propertyName, properties, false);
        }

        private void DrawSurfaceOptions(Material material)
        {
            if (!BurtShaderGUIUtility.BeginSection(SurfaceOptionsLabel, ref showSurfaceOptions))
            {
                return;
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(SurfaceTypeLabel, "Transparent");
                EditorGUILayout.TextField(ShadingModelLabel, "Hexa Lighting / Forward");

                if (material != null)
                {
                    BurtShaderGUIUtility.DrawSeparator();
                    BurtShaderGUIUtility.DrawSubHeader("Resolved State");
                    EditorGUILayout.LabelField(RenderQueueLabel, new GUIContent(material.renderQueue.ToString()));
                    EditorGUILayout.TextField(EnabledPassesLabel, "BurtForward");
                }
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawAxesLightmaps()
        {
            if (!BurtShaderGUIUtility.BeginSection(AxesLightmapsLabel, ref showAxesLightmaps))
            {
                return;
            }

            DrawTexture(PositiveAxesLabel, positiveAxesLightmap);
            DrawTexture(NegativeAxesLabel, negativeAxesLightmap);
            BurtShaderGUIUtility.DrawChannelHint("RGB stores right, top, and back scattering. Alpha controls opacity.");
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawFlipbook()
        {
            if (!BurtShaderGUIUtility.BeginSection(FlipbookLabel, ref showFlipbook))
            {
                return;
            }

            materialEditor.ShaderProperty(columns, ColumnsLabel);
            materialEditor.ShaderProperty(rows, RowsLabel);
            materialEditor.ShaderProperty(playSpeed, PlaySpeedLabel);
            DrawTexture(MotionVectorLabel, motionVectorMap);
            materialEditor.ShaderProperty(motionVectorScale, MotionVectorScaleLabel);
            BurtShaderGUIUtility.DrawChannelHint("Motion vectors use RG in the 0 to 1 range and are decoded to -1 to 1.");
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawScattering()
        {
            if (!BurtShaderGUIUtility.BeginSection(ScatteringLabel, ref showScattering))
            {
                return;
            }

            BurtShaderGUIUtility.DrawProperty(materialEditor, basicColor);
            BurtShaderGUIUtility.DrawProperty(materialEditor, density);
            BurtShaderGUIUtility.DrawProperty(materialEditor, overallAlpha);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawRendering()
        {
            if (!BurtShaderGUIUtility.BeginSection(RenderingLabel, ref showRendering))
            {
                return;
            }

            BurtShaderGUIUtility.DrawProperty(materialEditor, cull);
            BurtShaderGUIUtility.DrawProperty(materialEditor, zWrite);
            BurtShaderGUIUtility.DrawProperty(materialEditor, responsiveAA);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawTexture(GUIContent label, MaterialProperty texture)
        {
            if (texture != null)
            {
                materialEditor.TexturePropertySingleLine(label, texture);
            }
        }
    }
}
