using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtLitShaderGUI : ShaderGUI
    {
        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Surface Options");
        private static readonly GUIContent BaseInputsLabel = new GUIContent("Base Inputs");
        private static readonly GUIContent PbrMaskLabel = new GUIContent("PBR / Mask Inputs");
        private static readonly GUIContent ClearCoatLabel = new GUIContent("Clear Coat");
        private static readonly GUIContent SubsurfaceLabel = new GUIContent("Subsurface");
        private static readonly GUIContent NormalLabel = new GUIContent("Normal");
        private static readonly GUIContent EmissionLabel = new GUIContent("Emission");

        private static readonly GUIContent BaseMapLabel = new GUIContent("Base Map", "Albedo RGB and alpha for clipping or transparent materials.");
        private static readonly GUIContent MaskMapLabel = new GUIContent("Mask Map", "R Metallic, G Occlusion, B Reserved, A Smoothness.");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map");
        private static readonly GUIContent ClearCoatNormalMapLabel = new GUIContent("Clear Coat Normal Map");
        private static readonly GUIContent EmissionMapLabel = new GUIContent("Emission Map");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent DoubleSidedLabel = new GUIContent("Double Sided", "Render both front and back faces by switching culling off.");
        private static readonly GUIContent DoubleSidedNormalModeLabel = new GUIContent("Double Sided Normal Mode", "Back-face normal mode, matching XRender: None, Flip, or Mirror in tangent space.");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent CullLabel = new GUIContent("Cull");
        private static readonly GUIContent ZWriteLabel = new GUIContent("ZWrite");
        private static readonly GUIContent ZTestLabel = new GUIContent("ZTest");
        private static readonly GUIContent BlendLabel = new GUIContent("Blend");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static readonly string[] SurfaceTypeNames = { "Opaque", "Transparent" };
        private static readonly string[] DoubleSidedNormalModeNames = { "None", "Flip", "Mirror" };
        private static bool showSurfaceOptions = true;
        private static bool showBaseInputs = true;
        private static bool showPbrMaskInputs = true;
        private static bool showClearCoatInputs = true;
        private static bool showSubsurfaceInputs = true;
        private static bool showNormalInputs = true;
        private static bool showEmissionInputs = true;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty baseMap;
        private MaterialProperty baseColor;
        private MaterialProperty maskMap;
        private MaterialProperty metallic;
        private MaterialProperty smoothness;
        private MaterialProperty occlusionStrength;
        private MaterialProperty reflectance;
        private MaterialProperty clearCoatMask;
        private MaterialProperty clearCoatRoughness;
        private MaterialProperty clearCoatNormalMap;
        private MaterialProperty clearCoatNormalScale;
        private MaterialProperty subsurfaceStrength;
        private MaterialProperty normalMap;
        private MaterialProperty normalScale;
        private MaterialProperty emissionMap;
        private MaterialProperty emissionColor;
        private MaterialProperty alphaClip;
        private MaterialProperty cutoff;
        private MaterialProperty surface;
        private MaterialProperty doubleSidedEnable;
        private MaterialProperty doubleSidedNormalMode;
        private MaterialProperty cull;
        private MaterialProperty srcBlend;
        private MaterialProperty dstBlend;
        private MaterialProperty zWrite;
        private MaterialProperty zTest;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();

            Material material = materialEditor.target as Material;
            MigrateLegacyTransparentShader(material);
            DrawSurfaceOptions(material);
            DrawBaseInputs();
            DrawPbrInputs();
            DrawNormalInputs();
            DrawClearCoatInputs(material);
            DrawSubsurfaceInputs(material);
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

            BurtShaderGUIUtility.UpdateEmissionFlag(material);
            ApplySurfaceOptions(material);
        }

        private void CacheProperties()
        {
            baseMap = Find("_BaseMap");
            baseColor = Find("_BaseColor");
            maskMap = Find("_MaskMap");
            metallic = Find("_Metallic");
            smoothness = Find("_Smoothness");
            occlusionStrength = Find("_OcclusionStrength");
            reflectance = Find("_Reflectance");
            clearCoatMask = Find("_ClearCoatMask");
            clearCoatRoughness = Find("_ClearCoatRoughness");
            clearCoatNormalMap = Find("_ClearCoatNormalMap");
            clearCoatNormalScale = Find("_ClearCoatNormalScale");
            subsurfaceStrength = Find("_SubsurfaceStrength");
            normalMap = Find("_NormalMap");
            normalScale = Find("_NormalScale");
            emissionMap = Find("_EmissionMap");
            emissionColor = Find("_EmissionColor");
            alphaClip = Find("_AlphaClip");
            cutoff = Find("_Cutoff");
            surface = Find("_Surface");
            doubleSidedEnable = Find("_DoubleSidedEnable");
            doubleSidedNormalMode = Find("_DoubleSidedNormalMode");
            cull = Find("_Cull");
            srcBlend = Find("_SrcBlend");
            dstBlend = Find("_DstBlend");
            zWrite = Find("_ZWrite");
            zTest = Find("_ZTest");
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

            bool transparent = IsTransparentMaterial(material);
            if (surface != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = surface.hasMixedValue;
                int surfaceValue = EditorGUILayout.Popup(SurfaceTypeLabel, Mathf.Clamp((int)surface.floatValue, 0, SurfaceTypeNames.Length - 1), SurfaceTypeNames);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                {
                    materialEditor.RegisterPropertyChangeUndo(SurfaceTypeLabel.text);
                    surface.floatValue = surfaceValue;
                    foreach (Object target in materialEditor.targets)
                    {
                        ApplySurfaceOptions(target as Material);
                    }
                }
            }
            else
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(SurfaceTypeLabel, transparent ? "Transparent" : "Opaque");
                }
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(ShadingModelLabel, GetShadingModelName(material));
            }

            DrawDoubleSidedOptions(material);
            DrawProperty(alphaClip);

            if (cutoff != null)
            {
                bool showCutoff = alphaClip == null || alphaClip.floatValue >= 0.5f;
                using (new EditorGUI.DisabledScope(!showCutoff))
                {
                    DrawProperty(cutoff);
                }
            }

            DrawResolvedState(material, transparent);

            if (transparent)
            {
                BurtShaderGUIUtility.DrawSeparator();
                EditorGUILayout.HelpBox("Transparent Lit uses the same BurtForward pass with alpha blending. Deferred GBuffer, depth prepass, and shadow caster are disabled by material state.", MessageType.Info);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawDoubleSidedOptions(Material material)
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
                    ApplySurfaceOptions(target as Material);
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
                int normalMode = EditorGUILayout.Popup(DoubleSidedNormalModeLabel, Mathf.Clamp((int)doubleSidedNormalMode.floatValue, 0, DoubleSidedNormalModeNames.Length - 1), DoubleSidedNormalModeNames);
                EditorGUI.showMixedValue = false;
                if (EditorGUI.EndChangeCheck())
                {
                    materialEditor.RegisterPropertyChangeUndo(DoubleSidedNormalModeLabel.text);
                    doubleSidedNormalMode.floatValue = normalMode;
                    foreach (Object target in materialEditor.targets)
                    {
                        ApplySurfaceOptions(target as Material);
                    }
                }
            }
        }

        private void DrawResolvedState(Material material, bool transparent)
        {
            if (!ShouldDrawResolvedState())
            {
                return;
            }

            BurtShaderGUIUtility.DrawSeparator();
            BurtShaderGUIUtility.DrawSubHeader("Resolved State");

            using (new EditorGUI.DisabledScope(true))
            {
                if (material != null)
                {
                    EditorGUILayout.LabelField(RenderQueueLabel, new GUIContent(material.renderQueue.ToString()));
                }

                if (cull != null)
                {
                    EditorGUILayout.TextField(CullLabel, ((CullMode)Mathf.RoundToInt(cull.floatValue)).ToString());
                }

                EditorGUILayout.Toggle(ZWriteLabel, zWrite.floatValue >= 0.5f);
                EditorGUILayout.TextField(ZTestLabel, ((CompareFunction)Mathf.RoundToInt(zTest.floatValue)).ToString());
                EditorGUILayout.TextField(BlendLabel, GetBlendStateName());
                EditorGUILayout.TextField(EnabledPassesLabel, transparent ? "BurtForward" : "BurtDepthOnly, BurtGBuffer, ShadowCaster, BurtForward");
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

        private void DrawPbrInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(PbrMaskLabel, ref showPbrMaskInputs))
            {
                return;
            }

            DrawTexture(MaskMapLabel, maskMap);
            if (maskMap != null)
            {
                BurtShaderGUIUtility.DrawChannelHint("Channels: R Metallic | G Occlusion | B Reserved | A Smoothness");
            }

            BurtShaderGUIUtility.DrawSubHeader("Lit");
            DrawProperty(metallic);

            BurtShaderGUIUtility.DrawSubHeader("Shared");
            DrawProperty(smoothness);
            DrawProperty(occlusionStrength);
            DrawProperty(reflectance);

            if (maskMap != null)
            {
                EditorGUILayout.HelpBox("Mask channels are multiplied by scalar values below, matching BurtRP GBuffer and Forward sampling.", MessageType.None);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawClearCoatInputs(Material material)
        {
            if (!IsClearCoatShader(material) || clearCoatMask == null)
            {
                return;
            }

            if (!BurtShaderGUIUtility.BeginSection(ClearCoatLabel, ref showClearCoatInputs))
            {
                return;
            }

            DrawProperty(clearCoatMask);
            DrawProperty(clearCoatRoughness);
            DrawTextureWithExtra(ClearCoatNormalMapLabel, clearCoatNormalMap, clearCoatNormalScale);
            EditorGUILayout.HelpBox("Clear Coat writes shading model 2 and packs coat mask / roughness into the GBuffer material channel.", MessageType.None);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawSubsurfaceInputs(Material material)
        {
            if (!IsSubsurfaceShader(material) || subsurfaceStrength == null)
            {
                return;
            }

            if (!BurtShaderGUIUtility.BeginSection(SubsurfaceLabel, ref showSubsurfaceInputs))
            {
                return;
            }

            DrawProperty(subsurfaceStrength);
            EditorGUILayout.HelpBox("Subsurface writes shading model 3 and stores strength in the GBuffer material channel.", MessageType.None);
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

        private bool ShouldDrawResolvedState()
        {
            return surface != null && srcBlend != null && dstBlend != null && zWrite != null && zTest != null;
        }

        private string GetBlendStateName()
        {
            BlendMode sourceBlend = (BlendMode)Mathf.RoundToInt(srcBlend.floatValue);
            BlendMode destinationBlend = (BlendMode)Mathf.RoundToInt(dstBlend.floatValue);
            return sourceBlend + " / " + destinationBlend;
        }

        private static bool IsTransparentMaterial(Material material)
        {
            if (material == null)
            {
                return false;
            }

            if (material.HasProperty("_Surface"))
            {
                return material.GetFloat("_Surface") >= 0.5f;
            }

            return material.shader != null && material.shader.name.IndexOf("Transparent", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetShadingModelName(Material material)
        {
            if (IsClearCoatShader(material))
            {
                return IsTransparentMaterial(material) ? "PBR Transparent Clear Coat" : "PBR Clear Coat / Deferred GBuffer";
            }

            if (IsSubsurfaceShader(material))
            {
                return IsTransparentMaterial(material) ? "PBR Transparent Subsurface" : "PBR Subsurface / Deferred GBuffer";
            }

            return IsTransparentMaterial(material) ? "PBR Transparent" : "PBR Lit / Deferred GBuffer";
        }

        private static bool IsClearCoatShader(Material material)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name.IndexOf("Clear Coat", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSubsurfaceShader(Material material)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name.IndexOf("Subsurface", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsDoubleSidedMaterial(Material material)
        {
            return material != null && material.HasProperty("_DoubleSidedEnable") && material.GetFloat("_DoubleSidedEnable") >= 0.5f;
        }

        private static void ApplyDoubleSidedNormalState(Material material)
        {
            bool doubleSided = IsDoubleSidedMaterial(material);
            if (material.HasProperty("_DoubleSidedEnable"))
            {
                material.SetFloat("_DoubleSidedEnable", doubleSided ? 1.0f : 0.0f);
            }

            if (material.HasProperty("_DoubleSidedNormalMode"))
            {
                material.SetFloat("_DoubleSidedNormalMode", Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_DoubleSidedNormalMode")), 0, 2));
            }

            if (material.HasProperty("_DoubleSidedNormalModeConstants"))
            {
                int mode = material.HasProperty("_DoubleSidedNormalMode") ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_DoubleSidedNormalMode")), 0, 2) : 0;
                material.SetVector("_DoubleSidedNormalModeConstants", GetDoubleSidedNormalModeConstants(doubleSided ? mode : 0));
            }
        }

        private static Vector4 GetDoubleSidedNormalModeConstants(int mode)
        {
            switch (mode)
            {
                case 1:
                    return new Vector4(-1.0f, -1.0f, -1.0f, 0.0f);
                case 2:
                    return new Vector4(1.0f, 1.0f, -1.0f, 0.0f);
                default:
                    return new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
            }
        }

        internal static void ApplySurfaceOptions(Material material)
        {
            if (material == null)
            {
                return;
            }

            bool transparent = IsTransparentMaterial(material);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", transparent ? 1.0f : 0.0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", IsDoubleSidedMaterial(material) ? (float)CullMode.Off : (float)CullMode.Back);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", transparent ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", transparent ? (float)BlendMode.OneMinusSrcAlpha : (float)BlendMode.Zero);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", transparent ? 0.0f : 1.0f);
            }

            if (material.HasProperty("_ZTest"))
            {
                material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            }

            ApplyDoubleSidedNormalState(material);

            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.SetOverrideTag("Queue", transparent ? "Transparent" : string.Empty);
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("BurtDepthOnly", !transparent);
            material.SetShaderPassEnabled("BurtGBuffer", !transparent);
            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            material.SetShaderPassEnabled("BurtForward", true);
        }

        private static void MigrateLegacyTransparentShader(Material material)
        {
            if (material == null || material.shader == null || material.shader.name.IndexOf("Lit Transparent", System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return;
            }

            Shader litShader = Shader.Find("BurtRP/Lit");
            if (litShader == null)
            {
                return;
            }

            material.shader = litShader;
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1.0f);
            }

            ApplySurfaceOptions(material);
            EditorUtility.SetDirty(material);
        }
    }
}
