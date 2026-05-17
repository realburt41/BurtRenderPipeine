using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtHairShaderGUI : ShaderGUI
    {
        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Hair Surface Options");
        private static readonly GUIContent BaseInputsLabel = new GUIContent("Base Inputs");
        private static readonly GUIContent HairInputsLabel = new GUIContent("Hair Inputs");
        private static readonly GUIContent NormalLabel = new GUIContent("Normal / Debug");
        private static readonly GUIContent EmissionLabel = new GUIContent("Emission");

        private static readonly GUIContent BaseMapLabel = new GUIContent("Base Map", "Hair tint RGB and alpha for card cutout.");
        private static readonly GUIContent MaskMapLabel = new GUIContent("Hair Mask Map", "R Scatter, G Occlusion, B Longitudinal lobe shift scale, A Smoothness.");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map", "Currently used for forward material debug; deferred Hair stores strand direction in the GBuffer vector slot.");
        private static readonly GUIContent EmissionMapLabel = new GUIContent("Emission Map");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent DoubleSidedLabel = new GUIContent("Double Sided", "Render both front and back faces by switching culling off.");
        private static readonly GUIContent DoubleSidedNormalModeLabel = new GUIContent("Double Sided Normal Mode", "Back-face normal mode, matching XRender: None, Flip, or Mirror in tangent space.");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent CullLabel = new GUIContent("Cull");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static bool showSurfaceOptions = true;
        private static bool showBaseInputs = true;
        private static bool showHairInputs = true;
        private static bool showNormalInputs = true;
        private static bool showEmissionInputs = true;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty baseMap;
        private MaterialProperty baseColor;
        private MaterialProperty maskMap;
        private MaterialProperty smoothness;
        private MaterialProperty occlusionStrength;
        private MaterialProperty reflectance;
        private MaterialProperty normalMap;
        private MaterialProperty normalScale;
        private MaterialProperty emissionMap;
        private MaterialProperty emissionColor;
        private MaterialProperty alphaClip;
        private MaterialProperty cutoff;
        private MaterialProperty doubleSidedEnable;
        private MaterialProperty doubleSidedNormalMode;
        private MaterialProperty cull;
        private MaterialProperty hairScatter;
        private MaterialProperty hairScatterBoost;
        private MaterialProperty hairSpecularScale;
        private MaterialProperty hairShiftScale;
        private MaterialProperty hairRoughnessOffset;
        private MaterialProperty hairTangentFlip;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();

            Material material = materialEditor.target as Material;
            DrawSurfaceOptions(material);
            DrawBaseInputs();
            DrawHairInputs();
            DrawNormalInputs();
            DrawEmissionInputs();
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

            ApplyHairMaterialOptions(material);
            BurtShaderGUIUtility.UpdateEmissionFlag(material);
        }

        private void CacheProperties()
        {
            baseMap = Find("_BaseMap");
            baseColor = Find("_BaseColor");
            maskMap = Find("_MaskMap");
            smoothness = Find("_Smoothness");
            occlusionStrength = Find("_OcclusionStrength");
            reflectance = Find("_Reflectance");
            normalMap = Find("_NormalMap");
            normalScale = Find("_NormalScale");
            emissionMap = Find("_EmissionMap");
            emissionColor = Find("_EmissionColor");
            alphaClip = Find("_AlphaClip");
            cutoff = Find("_Cutoff");
            doubleSidedEnable = Find("_DoubleSidedEnable");
            doubleSidedNormalMode = Find("_DoubleSidedNormalMode");
            cull = Find("_Cull");
            hairScatter = Find("_HairScatter");
            hairScatterBoost = Find("_HairScatterBoost");
            hairSpecularScale = Find("_HairSpecularScale");
            hairShiftScale = Find("_HairShiftScale");
            hairRoughnessOffset = Find("_HairRoughnessOffset");
            hairTangentFlip = Find("_HairTangentFlip");
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
                EditorGUILayout.TextField(SurfaceTypeLabel, "Opaque Hair Cards");
                EditorGUILayout.TextField(ShadingModelLabel, "Hair");
            }

            DrawDoubleSidedOptions();
            DrawAlphaClipProperty();

            if (cutoff != null)
            {
                bool showCutoff = alphaClip == null || alphaClip.floatValue >= 0.5f;
                using (new EditorGUI.DisabledScope(!showCutoff))
                {
                    DrawProperty(cutoff);
                }
            }

            if (material != null)
            {
                BurtShaderGUIUtility.DrawSeparator();
                BurtShaderGUIUtility.DrawSubHeader("Resolved State");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.LabelField(RenderQueueLabel, new GUIContent(material.renderQueue.ToString()));
                    if (cull != null)
                    {
                        EditorGUILayout.TextField(CullLabel, ((CullMode)Mathf.RoundToInt(cull.floatValue)).ToString());
                    }

                    EditorGUILayout.TextField(EnabledPassesLabel, "BurtDepthOnly, BurtGBuffer, ShadowCaster, BurtForward");
                }
            }

            BurtShaderGUIUtility.DrawSeparator();
            EditorGUILayout.HelpBox("Hair is double-sided and writes strand direction into the GBuffer vector slot. Use GBuffer / Hair Strand and Hair Scatter debug views when tuning cards.", MessageType.Info);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawDoubleSidedOptions()
        {
            if (doubleSidedEnable == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = doubleSidedEnable.hasMixedValue;
            bool doubleSided = EditorGUILayout.Toggle(DoubleSidedLabel, doubleSidedEnable.floatValue >= 0.5f);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(DoubleSidedLabel.text);
                doubleSidedEnable.floatValue = doubleSided ? 1.0f : 0.0f;
                foreach (Object target in materialEditor.targets)
                {
                    ApplyHairMaterialOptions(target as Material);
                }
            }

            if (doubleSidedNormalMode == null)
            {
                return;
            }

            using (new EditorGUI.DisabledScope(!doubleSided))
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = doubleSidedNormalMode.hasMixedValue;
                int normalMode = EditorGUILayout.Popup(DoubleSidedNormalModeLabel, Mathf.Clamp((int)doubleSidedNormalMode.floatValue, 0, BurtShaderGUIUtility.DoubleSidedNormalModeNames.Length - 1), BurtShaderGUIUtility.DoubleSidedNormalModeNames);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                {
                    materialEditor.RegisterPropertyChangeUndo(DoubleSidedNormalModeLabel.text);
                    doubleSidedNormalMode.floatValue = normalMode;
                    foreach (Object target in materialEditor.targets)
                    {
                        ApplyHairMaterialOptions(target as Material);
                    }
                }
            }
        }

        private void DrawBaseInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(BaseInputsLabel, ref showBaseInputs))
            {
                return;
            }

            DrawTextureWithColor(BaseMapLabel, baseMap, baseColor);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawHairInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(HairInputsLabel, ref showHairInputs))
            {
                return;
            }

            DrawTexture(MaskMapLabel, maskMap);
            if (maskMap != null)
            {
                BurtShaderGUIUtility.DrawChannelHint("Channels: R Scatter | G Occlusion | B Shift Scale | A Smoothness");
            }

            BurtShaderGUIUtility.DrawSubHeader("Hair");
            DrawProperty(hairScatter);
            DrawProperty(hairScatterBoost);
            DrawProperty(hairSpecularScale);
            DrawProperty(hairShiftScale);
            DrawProperty(hairRoughnessOffset);
            DrawProperty(hairTangentFlip);
            BurtShaderGUIUtility.DrawSubHeader("Shared");
            DrawProperty(smoothness);
            DrawProperty(occlusionStrength);
            DrawProperty(reflectance);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawNormalInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(NormalLabel, ref showNormalInputs))
            {
                return;
            }

            DrawTextureWithExtra(NormalMapLabel, normalMap, normalScale);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawEmissionInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(EmissionLabel, ref showEmissionInputs))
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawTextureWithColor(EmissionMapLabel, emissionMap, emissionColor);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    BurtShaderGUIUtility.UpdateEmissionFlag(target as Material);
                }
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawTextureWithColor(GUIContent label, MaterialProperty texture, MaterialProperty color)
        {
            BurtShaderGUIUtility.DrawTextureWithColor(materialEditor, label, texture, color);
        }

        private void DrawTextureWithExtra(GUIContent label, MaterialProperty texture, MaterialProperty extra)
        {
            BurtShaderGUIUtility.DrawTextureWithExtra(materialEditor, label, texture, extra);
        }

        private void DrawTexture(GUIContent label, MaterialProperty texture)
        {
            BurtShaderGUIUtility.DrawTexture(materialEditor, label, texture);
        }

        private void DrawProperty(MaterialProperty property)
        {
            BurtShaderGUIUtility.DrawProperty(materialEditor, property);
        }

        private void DrawAlphaClipProperty()
        {
            if (alphaClip == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawProperty(alphaClip);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            foreach (Object target in materialEditor.targets)
            {
                BurtShaderGUIUtility.ApplyAlphaClipKeyword(target as Material);
            }
        }

        internal static void ApplyHairMaterialOptions(Material material)
        {
            if (material == null)
            {
                return;
            }

            BurtShaderGUIUtility.ApplyDoubleSidedState(material, true);
            BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetOverrideTag("Queue", string.Empty);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("BurtDepthOnly", true);
            material.SetShaderPassEnabled("BurtGBuffer", true);
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetShaderPassEnabled("BurtForward", true);
        }
    }
}
