using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtEasyFogShaderGUI : ShaderGUI
    {
        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Easy Fog Surface Options");
        private static readonly GUIContent DensityLabel = new GUIContent("Density");
        private static readonly GUIContent FadeLabel = new GUIContent("Fade");
        private static readonly GUIContent FlowLabel = new GUIContent("Flowmap");
        private static readonly GUIContent NormalLabel = new GUIContent("Normal");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent CullLabel = new GUIContent("Cull");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static readonly GUIContent OpacityMapLabel = new GUIContent("Opacity Map");
        private static readonly GUIContent FlowmapLabel = new GUIContent("Flowmap");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map");

        private static bool showSurfaceOptions = true;
        private static bool showDensityInputs = true;
        private static bool showFadeInputs = true;
        private static bool showFlowInputs = true;
        private static bool showNormalInputs = false;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty normalMap;
        private MaterialProperty normalScale;
        private MaterialProperty opacityMap;
        private MaterialProperty baseColorTint;
        private MaterialProperty fogIntensity;
        private MaterialProperty emissiveIntensity;
        private MaterialProperty cameraFadingDistance;
        private MaterialProperty depthFadeDistance;
        private MaterialProperty depthFadePower;
        private MaterialProperty enableBillboard;
        private MaterialProperty flowmap;
        private MaterialProperty flowmapSpeed;
        private MaterialProperty flowmapIntensity;
        private MaterialProperty cull;
        private MaterialProperty responsiveAA;
        private MaterialProperty ignoreFog;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();

            Material material = materialEditor.target as Material;
            DrawSurfaceOptions(material);
            DrawDensityInputs();
            DrawFadeInputs();
            DrawFlowInputs();
            DrawNormalInputs();
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

            SetFloatIfPresent(material, "_Surface", 1.0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.One);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0.0f);
            SetFloatIfPresent(material, "_ZTest", (float)CompareFunction.LessEqual);
            SetFloatIfPresent(material, "_Cull", (float)CullMode.Off);
            if (material.HasProperty("_IgnoreFog") && material.GetFloat("_IgnoreFog") >= 0.5f)
            {
                material.EnableKeyword("BURT_IGNORE_FOG");
            }
            else
            {
                material.DisableKeyword("BURT_IGNORE_FOG");
            }

            material.SetOverrideTag("RenderType", "Transparent");
            material.SetOverrideTag("Queue", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            material.SetShaderPassEnabled("BurtDepthOnly", false);
            material.SetShaderPassEnabled("BurtMotionVectors", false);
            material.SetShaderPassEnabled("BurtTransparentMotionVectors", true);
            material.SetShaderPassEnabled("BurtResponsiveAAMask", true);
            material.SetShaderPassEnabled("BurtDepthNormals", false);
            material.SetShaderPassEnabled("BurtGBuffer", false);
            material.SetShaderPassEnabled("BurtSubsurfaceForward", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
            material.SetShaderPassEnabled("BurtForwardOnly", false);
            material.SetShaderPassEnabled("BurtForward", true);
        }

        private void CacheProperties()
        {
            normalMap = Find("_NormalMap");
            normalScale = Find("_NormalScale");
            opacityMap = Find("_OpacityMap");
            baseColorTint = Find("_BaseColorTink");
            fogIntensity = Find("_FogIntensity");
            emissiveIntensity = Find("_EmissiveIntensity");
            cameraFadingDistance = Find("_CameraFadingDistance");
            depthFadeDistance = Find("_DepthFadeDistance");
            depthFadePower = Find("_DepthFadePower");
            enableBillboard = Find("_EnableBillboard");
            flowmap = Find("_Flowmap");
            flowmapSpeed = Find("_FlowmapSpeed");
            flowmapIntensity = Find("_FlowmapIntensity");
            cull = Find("_Cull");
            responsiveAA = Find("_ResponsiveAA");
            ignoreFog = Find("_IgnoreFog");
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
                EditorGUILayout.TextField(ShadingModelLabel, "Easy Fog / Forward Emissive");

                if (material != null)
                {
                    BurtShaderGUIUtility.DrawSeparator();
                    BurtShaderGUIUtility.DrawSubHeader("Resolved State");
                    EditorGUILayout.LabelField(RenderQueueLabel, new GUIContent(material.renderQueue.ToString()));
                    if (cull != null)
                    {
                        EditorGUILayout.TextField(CullLabel, ((CullMode)Mathf.RoundToInt(cull.floatValue)).ToString());
                    }

                    EditorGUILayout.TextField(EnabledPassesLabel, "BurtForward");
                }
            }

            BurtShaderGUIUtility.DrawProperty(materialEditor, ignoreFog);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawDensityInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(DensityLabel, ref showDensityInputs))
            {
                return;
            }

            BurtShaderGUIUtility.DrawTexture(materialEditor, OpacityMapLabel, opacityMap);
            BurtShaderGUIUtility.DrawProperty(materialEditor, baseColorTint);
            BurtShaderGUIUtility.DrawProperty(materialEditor, fogIntensity);
            BurtShaderGUIUtility.DrawProperty(materialEditor, emissiveIntensity);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawFadeInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(FadeLabel, ref showFadeInputs))
            {
                return;
            }

            BurtShaderGUIUtility.DrawProperty(materialEditor, cameraFadingDistance);
            BurtShaderGUIUtility.DrawProperty(materialEditor, depthFadeDistance);
            BurtShaderGUIUtility.DrawProperty(materialEditor, depthFadePower);
            BurtShaderGUIUtility.DrawProperty(materialEditor, enableBillboard);
            BurtShaderGUIUtility.DrawProperty(materialEditor, responsiveAA);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawFlowInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(FlowLabel, ref showFlowInputs))
            {
                return;
            }

            BurtShaderGUIUtility.DrawTexture(materialEditor, FlowmapLabel, flowmap);
            BurtShaderGUIUtility.DrawProperty(materialEditor, flowmapSpeed);
            BurtShaderGUIUtility.DrawProperty(materialEditor, flowmapIntensity);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawNormalInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(NormalLabel, ref showNormalInputs))
            {
                return;
            }

            if (normalMap != null && normalScale != null)
            {
                materialEditor.TexturePropertySingleLine(NormalMapLabel, normalMap, normalScale);
            }
            else
            {
                BurtShaderGUIUtility.DrawTexture(materialEditor, NormalMapLabel, normalMap);
                BurtShaderGUIUtility.DrawProperty(materialEditor, normalScale);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }
    }
}
