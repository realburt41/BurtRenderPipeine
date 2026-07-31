using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtClearCrystalShaderGUI : ShaderGUI
    {
        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Surface Options");
        private static readonly GUIContent BaseLayerLabel = new GUIContent("Base Layer");
        private static readonly GUIContent NormalLayerLabel = new GUIContent("Normal Layer");
        private static readonly GUIContent PbrLayerLabel = new GUIContent("PBR Layer");
        private static readonly GUIContent EmissiveLayerLabel = new GUIContent("Emissive Layer");
        private static readonly GUIContent ParallaxLayerLabel = new GUIContent("Parallax Layers");
        private static readonly GUIContent TransmissionLabel = new GUIContent("Transmission Layer");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static readonly GUIContent MaskMapLabel = new GUIContent("Mask Map");
        private static readonly GUIContent TransmissionMapLabel = new GUIContent("Transmission Mask");
        private static readonly GUIContent ParallaxMapLabel = new GUIContent("Parallax Map");

        private static bool showSurfaceOptions = true;
        private static bool showBaseLayer = true;
        private static bool showNormalLayer = true;
        private static bool showPbrLayer = true;
        private static bool showEmissiveLayer = false;
        private static bool showParallaxLayer = true;
        private static bool showTransmission = true;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty baseMap;
        private MaterialProperty baseColor;
        private MaterialProperty baseColorFlowSpeed;
        private MaterialProperty shadowIntensity;
        private MaterialProperty normalMap;
        private MaterialProperty normalScale;
        private MaterialProperty detailNormalMap;
        private MaterialProperty detailNormalScale;
        private MaterialProperty detailNormalTiling;
        private MaterialProperty detailNormalRotate;
        private MaterialProperty maskMap;
        private MaterialProperty metallic;
        private MaterialProperty occlusion;
        private MaterialProperty roughness;
        private MaterialProperty ior;
        private MaterialProperty roughnessRefractionWeight;
        private MaterialProperty reflectance;
        private MaterialProperty emissiveMap;
        private MaterialProperty emissiveColor;
        private MaterialProperty emissiveTillingPanner;
        private MaterialProperty emissiveUseViewSpaceUV;
        private MaterialProperty viewSpaceUVNormalIntensity;
        private MaterialProperty parallaxMap;
        private MaterialProperty parallaxStrength;
        private MaterialProperty[] parallaxColors = new MaterialProperty[3];
        private MaterialProperty[] parallaxBrightness = new MaterialProperty[3];
        private MaterialProperty[] parallaxTilingOffset = new MaterialProperty[3];
        private MaterialProperty[] parallaxFlowSpeed = new MaterialProperty[3];
        private MaterialProperty[] parallaxBaseColorBlend = new MaterialProperty[3];
        private MaterialProperty[] useObjectSpaceParallax = new MaterialProperty[3];
        private MaterialProperty weight;
        private MaterialProperty transmissionColorMap;
        private MaterialProperty transmissionColor;
        private MaterialProperty mfpScale;
        private MaterialProperty thickness;
        private MaterialProperty phaseAniso;
        private MaterialProperty alphaClip;
        private MaterialProperty cutoff;
        private MaterialProperty refraction;
        private MaterialProperty zWrite;
        private MaterialProperty responsiveAA;
        private MaterialProperty ignoreFog;
        private MaterialProperty cull;
        private MaterialProperty doubleSidedNormalMode;
        private MaterialProperty transparentSortPriority;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();

            EditorGUI.BeginChangeCheck();
            DrawSurfaceOptions(materialEditor.target as Material);
            DrawBaseLayer();
            DrawNormalLayer();
            DrawPbrLayer();
            DrawEmissiveLayer();
            DrawParallaxLayer();
            DrawTransmissionLayer();
            if (EditorGUI.EndChangeCheck())
            {
                ApplyToTargets();
            }
        }

        public override void ValidateMaterial(Material material)
        {
            ApplyMaterialState(material);
        }

        private void CacheProperties()
        {
            baseMap = Find("_BaseMap");
            baseColor = Find("_BaseColor");
            baseColorFlowSpeed = Find("_BaseColorFlowSpeed");
            shadowIntensity = Find("_ShadowIntensity");
            normalMap = Find("_NormalMap");
            normalScale = Find("_NormalScale");
            detailNormalMap = Find("_DetailNormalMap");
            detailNormalScale = Find("_DetailNormalScale");
            detailNormalTiling = Find("_DetailNormalTiling");
            detailNormalRotate = Find("_DetailNormalRotate");
            maskMap = Find("_MaskMap");
            metallic = Find("_Metallic");
            occlusion = Find("_Occlusion");
            roughness = Find("_Roughness");
            ior = Find("_IOR");
            roughnessRefractionWeight = Find("_RoughnessRefractionWeight");
            reflectance = Find("_Reflectance");
            emissiveMap = Find("_EmissiveMap");
            emissiveColor = Find("_EmissiveColor");
            emissiveTillingPanner = Find("_EmissiveTillingPanner");
            emissiveUseViewSpaceUV = Find("_EmissiveUseViewSpaceUV");
            viewSpaceUVNormalIntensity = Find("_ViewSpaceUVNormalIntensity");
            parallaxMap = Find("_ParallaxMap");
            parallaxStrength = Find("_ParallaxStrength");
            weight = Find("_Weight");
            transmissionColorMap = Find("_TransmissionColorMap");
            transmissionColor = Find("_TransmissionColor");
            mfpScale = Find("_MFPScale");
            thickness = Find("_Thickness");
            phaseAniso = Find("_PhaseAniso");
            alphaClip = Find("_AlphaClip");
            cutoff = Find("_Cutoff");
            refraction = Find("_Refraction");
            zWrite = Find("_ZWrite");
            responsiveAA = Find("_ResponsiveAA");
            ignoreFog = Find("_IgnoreFog");
            cull = Find("_Cull");
            doubleSidedNormalMode = Find("_DoubleSidedNormalMode");
            transparentSortPriority = Find("_TransparentSortPriority");

            for (int index = 0; index < 3; ++index)
            {
                int layer = index + 1;
                parallaxColors[index] = Find("_ParallaxColor" + layer);
                parallaxBrightness[index] = Find("_ParallaxBrightness" + layer);
                parallaxTilingOffset[index] = Find("_ParallaxTilingOffset" + layer);
                parallaxFlowSpeed[index] = Find("_ParallaxFlowSpeed" + layer);
                parallaxBaseColorBlend[index] = Find("_ParallaxBaseColorBlend" + layer);
                useObjectSpaceParallax[index] = Find("_UseObjectSpaceParallax" + layer);
            }
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
                EditorGUILayout.TextField(ShadingModelLabel, "Clear Crystal / Forward");
                if (material != null)
                {
                    BurtShaderGUIUtility.DrawSeparator();
                    BurtShaderGUIUtility.DrawSubHeader("Resolved State");
                    EditorGUILayout.LabelField(RenderQueueLabel, new GUIContent(material.renderQueue.ToString()));
                    EditorGUILayout.TextField(EnabledPassesLabel, "ShadowCaster, BurtRefractionDistortion, BurtForward");
                }
            }

            DrawProperty(refraction);
            DrawProperty(zWrite);
            DrawProperty(responsiveAA);
            DrawProperty(ignoreFog);
            DrawProperty(cull);
            DrawProperty(doubleSidedNormalMode);
            DrawProperty(transparentSortPriority);
            DrawProperty(alphaClip);
            if (alphaClip != null && alphaClip.floatValue >= 0.5f)
            {
                DrawProperty(cutoff);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawBaseLayer()
        {
            if (!BurtShaderGUIUtility.BeginSection(BaseLayerLabel, ref showBaseLayer))
            {
                return;
            }

            if (baseMap != null && baseColor != null)
            {
                materialEditor.TexturePropertySingleLine(new GUIContent("Base Map"), baseMap, baseColor);
                materialEditor.TextureScaleOffsetProperty(baseMap);
            }
            else
            {
                DrawTexture(new GUIContent("Base Map"), baseMap);
                DrawProperty(baseColor);
            }

            DrawProperty(baseColorFlowSpeed);
            DrawProperty(shadowIntensity);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawNormalLayer()
        {
            if (!BurtShaderGUIUtility.BeginSection(NormalLayerLabel, ref showNormalLayer))
            {
                return;
            }

            DrawTextureWithExtra(new GUIContent("Normal Map"), normalMap, normalScale);
            DrawTextureWithExtra(new GUIContent("Detail Normal Map"), detailNormalMap, detailNormalScale);
            DrawProperty(detailNormalTiling);
            DrawProperty(detailNormalRotate);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawPbrLayer()
        {
            if (!BurtShaderGUIUtility.BeginSection(PbrLayerLabel, ref showPbrLayer))
            {
                return;
            }

            DrawTexture(MaskMapLabel, maskMap);
            BurtShaderGUIUtility.DrawChannelHint("R metallic, G occlusion, B height, A roughness.");
            DrawProperty(metallic);
            DrawProperty(occlusion);
            DrawProperty(roughness);
            DrawProperty(ior);
            DrawProperty(roughnessRefractionWeight);
            DrawProperty(reflectance);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawEmissiveLayer()
        {
            if (!BurtShaderGUIUtility.BeginSection(EmissiveLayerLabel, ref showEmissiveLayer))
            {
                return;
            }

            DrawTextureWithExtra(new GUIContent("Emissive Map"), emissiveMap, emissiveColor);
            DrawProperty(emissiveTillingPanner);
            DrawProperty(emissiveUseViewSpaceUV);
            if (emissiveUseViewSpaceUV != null && emissiveUseViewSpaceUV.floatValue >= 0.5f)
            {
                DrawProperty(viewSpaceUVNormalIntensity);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawParallaxLayer()
        {
            if (!BurtShaderGUIUtility.BeginSection(ParallaxLayerLabel, ref showParallaxLayer))
            {
                return;
            }

            DrawTexture(ParallaxMapLabel, parallaxMap);
            BurtShaderGUIUtility.DrawChannelHint("R is layer 1, G is layer 2, and B is layer 3.");
            DrawProperty(parallaxStrength);
            for (int index = 0; index < 3; ++index)
            {
                BurtShaderGUIUtility.DrawSeparator();
                BurtShaderGUIUtility.DrawSubHeader("Layer " + (index + 1));
                DrawProperty(parallaxColors[index]);
                DrawProperty(parallaxBrightness[index]);
                DrawProperty(parallaxTilingOffset[index]);
                DrawProperty(parallaxFlowSpeed[index]);
                DrawProperty(parallaxBaseColorBlend[index]);
                DrawProperty(useObjectSpaceParallax[index]);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawTransmissionLayer()
        {
            if (!BurtShaderGUIUtility.BeginSection(TransmissionLabel, ref showTransmission))
            {
                return;
            }

            DrawProperty(weight);
            DrawTexture(TransmissionMapLabel, transmissionColorMap);
            BurtShaderGUIUtility.DrawChannelHint("R controls thickness and G controls transmission weight.");
            DrawProperty(transmissionColor);
            DrawProperty(mfpScale);
            DrawProperty(thickness);
            DrawProperty(phaseAniso);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawTexture(GUIContent label, MaterialProperty texture)
        {
            if (texture != null)
            {
                materialEditor.TexturePropertySingleLine(label, texture);
            }
        }

        private void DrawTextureWithExtra(GUIContent label, MaterialProperty texture, MaterialProperty extra)
        {
            if (texture != null && extra != null)
            {
                materialEditor.TexturePropertySingleLine(label, texture, extra);
                return;
            }

            DrawTexture(label, texture);
            DrawProperty(extra);
        }

        private void DrawProperty(MaterialProperty property)
        {
            BurtShaderGUIUtility.DrawProperty(materialEditor, property);
        }

        private void ApplyToTargets()
        {
            foreach (Object target in materialEditor.targets)
            {
                ApplyMaterialState(target as Material);
            }
        }

        private static void ApplyMaterialState(Material material)
        {
            if (material == null)
            {
                return;
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_Cull")), (int)CullMode.Off, (int)CullMode.Back));
            }

            if (material.HasProperty("_DoubleSidedNormalMode"))
            {
                material.SetFloat("_DoubleSidedNormalMode", Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_DoubleSidedNormalMode")), 0, 2));
            }

            if (material.HasProperty("_DoubleSidedNormalModeConstants"))
            {
                bool doubleSided = material.HasProperty("_Cull") && Mathf.RoundToInt(material.GetFloat("_Cull")) == (int)CullMode.Off;
                int normalMode = material.HasProperty("_DoubleSidedNormalMode") ? Mathf.RoundToInt(material.GetFloat("_DoubleSidedNormalMode")) : 0;
                material.SetVector("_DoubleSidedNormalModeConstants", BurtShaderGUIUtility.GetDoubleSidedNormalModeConstants(doubleSided ? normalMode : 0));
            }

            BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);
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
            int priority = material.HasProperty("_TransparentSortPriority")
                ? Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_TransparentSortPriority")), -2, 2)
                : 0;
            material.renderQueue = (int)RenderQueue.Transparent + priority;
            material.SetShaderPassEnabled("BurtDepthOnly", false);
            material.SetShaderPassEnabled("BurtDepthNormals", false);
            material.SetShaderPassEnabled("BurtGBuffer", false);
            material.SetShaderPassEnabled("BurtMotionVectors", false);
            material.SetShaderPassEnabled("BurtTransparentMotionVectors", true);
            material.SetShaderPassEnabled("BurtResponsiveAAMask", true);
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetShaderPassEnabled("BurtForward", true);
            bool refractionEnabled = material.HasProperty("_Refraction") && material.GetFloat("_Refraction") > 1.0e-4f;
            material.SetShaderPassEnabled("BurtRefractionDistortion", refractionEnabled);
        }
    }
}
