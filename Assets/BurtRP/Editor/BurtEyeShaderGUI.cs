using Burt.RenderPipeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtEyeShaderGUI : ShaderGUI
    {
        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Eye Surface Options");
        private static readonly GUIContent IrisLabel = new GUIContent("Iris");
        private static readonly GUIContent ScleraLabel = new GUIContent("Sclera");
        private static readonly GUIContent CorneaLabel = new GUIContent("Cornea / Refraction");
        private static readonly GUIContent EmissionMatcapLabel = new GUIContent("Emission / Matcap");

        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent DoubleSidedLabel = new GUIContent("Double Sided");
        private static readonly GUIContent DoubleSidedNormalModeLabel = new GUIContent("Double Sided Normal Mode");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent CullLabel = new GUIContent("Cull");
        private static readonly GUIContent ResponsiveAALabel = new GUIContent("Responsive AA", "Reduces Temporal AA history on the eye's thin, refractive detail.");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static readonly GUIContent IrisColorMapLabel = new GUIContent("Iris Color Map");
        private static readonly GUIContent ScleraMapLabel = new GUIContent("Sclera Map");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Cornea Normal Map");
        private static readonly GUIContent EyeDirectionMapLabel = new GUIContent("Eye Direction Map");
        private static readonly GUIContent MidPlaneHeightMapLabel = new GUIContent("Mid Plane Height Map");
        private static readonly GUIContent EmissiveMapLabel = new GUIContent("Emissive Map");
        private static readonly GUIContent MatcapLabel = new GUIContent("Matcap");

        private static bool showSurfaceOptions = true;
        private static bool showIrisInputs = true;
        private static bool showScleraInputs = true;
        private static bool showCorneaInputs = true;
        private static bool showEmissionMatcapInputs = true;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty doubleSidedEnable;
        private MaterialProperty doubleSidedNormalMode;
        private MaterialProperty cull;
        private MaterialProperty responsiveAA;
        private MaterialProperty scaleByCenter;
        private MaterialProperty pupilScale;
        private MaterialProperty limbusScale;
        private MaterialProperty limbusPow;
        private MaterialProperty inverseUV;
        private MaterialProperty irisColor;
        private MaterialProperty irisColorMap;
        private MaterialProperty irisColorRotate;
        private MaterialProperty irisColorRotateSpeed;
        private MaterialProperty irisRadius;
        private MaterialProperty irisMaskBlurIntensity;
        private MaterialProperty irisFrontDirectionOS;
        private MaterialProperty irisFrontHemisphereFade;
        private MaterialProperty irisConcavityScale;
        private MaterialProperty irisConcavityPow;
        private MaterialProperty scleraColor;
        private MaterialProperty scleraMap;
        private MaterialProperty scleraSpecular;
        private MaterialProperty scleraRoughness;
        private MaterialProperty normalMap;
        private MaterialProperty normalMapScale;
        private MaterialProperty corneaSpecular;
        private MaterialProperty corneaRoughness;
        private MaterialProperty ior;
        private MaterialProperty eyeDirectionMap;
        private MaterialProperty midPlaneHeightMap;
        private MaterialProperty irisDepthScale;
        private MaterialProperty emissiveMap;
        private MaterialProperty eyeEmissiveColor;
        private MaterialProperty matcap;
        private MaterialProperty matcapColor;
        private MaterialProperty matcapSizeOffset;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();

            Material material = materialEditor.target as Material;
            DrawSurfaceOptions(material);
            DrawIrisInputs();
            DrawScleraInputs();
            DrawCorneaInputs();
            DrawEmissionMatcapInputs();
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

            ApplyEyeMaterialOptions(material);
            UpdateEyeEmissionFlag(material);
        }

        private void CacheProperties()
        {
            doubleSidedEnable = Find("_DoubleSidedEnable");
            doubleSidedNormalMode = Find("_DoubleSidedNormalMode");
            cull = Find("_Cull");
            responsiveAA = Find("_ResponsiveAA");
            scaleByCenter = Find("_ScalebyCenter");
            pupilScale = Find("_PupilScale");
            limbusScale = Find("_LimbusScale");
            limbusPow = Find("_LimbusPow");
            inverseUV = Find("_InverseUV");
            irisColor = Find("_IrisColor");
            irisColorMap = Find("_IrisColorMap");
            irisColorRotate = Find("_IrisColorRotate");
            irisColorRotateSpeed = Find("_IrisColorRotateSpeed");
            irisRadius = Find("_IrisRadius");
            irisMaskBlurIntensity = Find("_IrisMaskBlurIntensity");
            irisFrontDirectionOS = Find("_IrisFrontDirectionOS");
            irisFrontHemisphereFade = Find("_IrisFrontHemisphereFade");
            irisConcavityScale = Find("_IrisConcavityScale");
            irisConcavityPow = Find("_IrisConcavityPow");
            scleraColor = Find("_ScleraColor");
            scleraMap = Find("_ScleraMap");
            scleraSpecular = Find("_ScleraSpecular");
            scleraRoughness = Find("_ScleraRoughness");
            normalMap = Find("_NormalMap");
            normalMapScale = Find("_NormalMapScale");
            corneaSpecular = Find("_CorneaSpecular");
            corneaRoughness = Find("_CorneaRoughness");
            ior = Find("_IOR");
            eyeDirectionMap = Find("_EyeDirectionMap");
            midPlaneHeightMap = Find("_MidPlaneHeightMap");
            irisDepthScale = Find("_IrisDepthScale");
            emissiveMap = Find("_EmissiveMap");
            eyeEmissiveColor = Find("_EyeEmissiveColor");
            matcap = Find("_Matcap");
            matcapColor = Find("_MatcapColor");
            matcapSizeOffset = Find("_MatcapSizeOffset");
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
                EditorGUILayout.TextField(SurfaceTypeLabel, "Opaque");
                EditorGUILayout.TextField(ShadingModelLabel, "Eye / Deferred GBuffer");
            }

            DrawDoubleSidedOptions();
            DrawProperty(responsiveAA);

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

                    EditorGUILayout.TextField(EnabledPassesLabel, "BurtDepthOnly, BurtMotionVectors, BurtDepthNormals, ShadowCaster, BurtForwardOnly");
                }
            }

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
                    ApplyEyeMaterialOptions(target as Material);
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
                        ApplyEyeMaterialOptions(target as Material);
                    }
                }
            }
        }

        private void DrawIrisInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(IrisLabel, ref showIrisInputs))
            {
                return;
            }

            DrawProperty(scaleByCenter);
            DrawProperty(pupilScale);
            DrawProperty(limbusScale);
            DrawProperty(limbusPow);
            DrawProperty(inverseUV);
            DrawTextureWithColor(IrisColorMapLabel, irisColorMap, irisColor);
            DrawProperty(irisColorRotate);
            DrawProperty(irisColorRotateSpeed);
            DrawProperty(irisRadius);
            DrawProperty(irisMaskBlurIntensity);
            DrawProperty(irisFrontDirectionOS);
            DrawProperty(irisFrontHemisphereFade);
            DrawProperty(irisConcavityScale);
            DrawProperty(irisConcavityPow);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawScleraInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(ScleraLabel, ref showScleraInputs))
            {
                return;
            }

            DrawTextureWithColor(ScleraMapLabel, scleraMap, scleraColor);
            DrawProperty(scleraSpecular);
            DrawProperty(scleraRoughness);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawCorneaInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(CorneaLabel, ref showCorneaInputs))
            {
                return;
            }

            DrawTextureWithExtra(NormalMapLabel, normalMap, normalMapScale);
            DrawProperty(corneaSpecular);
            DrawProperty(corneaRoughness);
            DrawProperty(ior);
            DrawTexture(EyeDirectionMapLabel, eyeDirectionMap);
            DrawTexture(MidPlaneHeightMapLabel, midPlaneHeightMap);
            DrawProperty(irisDepthScale);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawEmissionMatcapInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(EmissionMatcapLabel, ref showEmissionMatcapInputs))
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawTextureWithColor(EmissiveMapLabel, emissiveMap, eyeEmissiveColor);
            DrawTextureWithColor(MatcapLabel, matcap, matcapColor);
            DrawProperty(matcapSizeOffset);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    UpdateEyeEmissionFlag(target as Material);
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

        private static void ApplyEyeMaterialOptions(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 0.0f);
            }

            if (material.HasProperty("_AlphaClip"))
            {
                material.SetFloat("_AlphaClip", 0.0f);
            }

            BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);
            BurtShaderGUIUtility.ApplyDoubleSidedState(material, false);
            ApplyGBufferStencilState(material);
            BurtShadingModelIds.ApplyMotionVectorStencilProperties(material);
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetOverrideTag("Queue", string.Empty);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("BurtDepthOnly", true);
            material.SetShaderPassEnabled("BurtMotionVectors", true);
            material.SetShaderPassEnabled("BurtResponsiveAAMask", true);
            material.SetShaderPassEnabled("BurtDepthNormals", true);
            material.SetShaderPassEnabled("BurtGBuffer", false);
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetShaderPassEnabled("BurtForward", false);
            material.SetShaderPassEnabled("BurtForwardOnly", true);
        }

        private static void ApplyGBufferStencilState(Material material)
        {
            if (material == null || !material.HasProperty("_BurtGBufferStencilRef") || !material.HasProperty("_BurtGBufferStencilWriteMask"))
            {
                return;
            }

            material.SetFloat("_BurtGBufferStencilRef", BurtShadingModelIds.DeferredStencilDefaultLitRef);
            if (material.HasProperty("_BurtGBufferStencilReadMask"))
            {
                material.SetFloat("_BurtGBufferStencilReadMask", BurtShadingModelIds.DeferredStencilShadingModelMask);
            }

            material.SetFloat("_BurtGBufferStencilWriteMask", BurtShadingModelIds.DeferredStencilShadingModelMask);
        }

        private static void UpdateEyeEmissionFlag(Material material)
        {
            if (material == null)
            {
                return;
            }

            bool emissive = false;
            if (material.HasProperty("_EyeEmissiveColor"))
            {
                Color color = material.GetColor("_EyeEmissiveColor");
                emissive |= color.maxColorComponent > 0.0f;
                if (material.HasProperty("_EmissionColor"))
                {
                    material.SetColor("_EmissionColor", color);
                }
            }

            if (material.HasProperty("_MatcapColor") && material.GetTexture("_Matcap") != null)
            {
                emissive |= material.GetColor("_MatcapColor").maxColorComponent > 0.0f;
            }

            MaterialGlobalIlluminationFlags flags = material.globalIlluminationFlags;
            material.globalIlluminationFlags = emissive
                ? flags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack
                : flags | MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            if (emissive)
            {
                material.EnableKeyword(BurtShaderGUIUtility.EmissionKeyword);
            }
            else
            {
                material.DisableKeyword(BurtShaderGUIUtility.EmissionKeyword);
            }
        }
    }
}
