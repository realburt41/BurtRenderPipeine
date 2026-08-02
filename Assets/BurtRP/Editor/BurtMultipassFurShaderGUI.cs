using Burt.RenderPipeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtMultipassFurShaderGUI : ShaderGUI
    {
        private const string DirectionMapKeyword = "BURT_MULTIPASS_FUR_USE_DIRECTION_MAP";

        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Multipass Fur Surface Options");
        private static readonly GUIContent BaseInputsLabel = new GUIContent("Base Inputs");
        private static readonly GUIContent PbrMaskLabel = new GUIContent("PBR / Mask Inputs");
        private static readonly GUIContent NormalLabel = new GUIContent("Normal");
        private static readonly GUIContent FurShellLabel = new GUIContent("Fur Shell");
        private static readonly GUIContent FurDirectionLabel = new GUIContent("Fur Direction");
        private static readonly GUIContent FurBlurLabel = new GUIContent("Fur Blur");
        private static readonly GUIContent EmissionRimLabel = new GUIContent("Emission / Rim");

        private static readonly GUIContent BaseMapLabel = new GUIContent("Base Map", "Fur albedo RGB and alpha mask.");
        private static readonly GUIContent MaskMapLabel = new GUIContent("Mask Map", "R Metallic, G Occlusion, B Height, A Roughness.");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map");
        private static readonly GUIContent FlowTexLabel = new GUIContent("Flow Noise", "Noise texture used to thin outer shell layers.");
        private static readonly GUIContent FlowDirectionMapLabel = new GUIContent("Direction Map", "RGB direction in tangent space, A fur length.");
        private static readonly GUIContent FlowDirectionMapSegmentArrayLabel = new GUIContent("Direction Segment Array", "Texture array for three direction and length segments.");
        private static readonly GUIContent EmissiveMapLabel = new GUIContent("Emissive Map");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent CullLabel = new GUIContent("Cull");
        private static readonly GUIContent ZWriteLabel = new GUIContent("ZWrite");
        private static readonly GUIContent ZTestLabel = new GUIContent("ZTest");
        private static readonly GUIContent BlendLabel = new GUIContent("Blend");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static readonly GUIContent DirectionKeywordLabel = new GUIContent("Direction Keyword");

        private static bool showSurfaceOptions = true;
        private static bool showBaseInputs = true;
        private static bool showPbrMaskInputs = true;
        private static bool showNormalInputs = true;
        private static bool showFurShellInputs = true;
        private static bool showFurDirectionInputs = true;
        private static bool showFurBlurInputs = true;
        private static bool showEmissionRimInputs = true;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty baseMap;
        private MaterialProperty baseColor;
        private MaterialProperty darkColor;
        private MaterialProperty baseMapPanner;
        private MaterialProperty normalMap;
        private MaterialProperty normalScale;
        private MaterialProperty maskMap;
        private MaterialProperty occlusion;
        private MaterialProperty roughness;
        private MaterialProperty reflectance;
        private MaterialProperty anisotropy;
        private MaterialProperty emissiveMap;
        private MaterialProperty emissiveColor;
        private MaterialProperty emissiveTillingPanner;
        private MaterialProperty emissiveUseViewSpaceUV;
        private MaterialProperty viewSpaceUVNormalIntensity;
        private MaterialProperty furRimIntensity;
        private MaterialProperty furRimPower;
        private MaterialProperty furAttenuation;
        private MaterialProperty furTickness;
        private MaterialProperty furTicknessCurve;
        private MaterialProperty furExpand;
        private MaterialProperty furSpacing;
        private MaterialProperty furSpacingMax;
        private MaterialProperty flowTex;
        private MaterialProperty flowTexUV2;
        private MaterialProperty flowTilling;
        private MaterialProperty flowPanner;
        private MaterialProperty flowDirectionMap;
        private MaterialProperty useDirectionMap;
        private MaterialProperty flowDirectionMapSegmentArray;
        private MaterialProperty useDirectionMapSegment;
        private MaterialProperty flowDirectionUV2;
        private MaterialProperty flowDirectionIntensity;
        private MaterialProperty flowDirectionIntensitySegment1;
        private MaterialProperty flowDirectionIntensitySegment2;
        private MaterialProperty flowDirectionIntensitySegment3;
        private MaterialProperty furGravityDirection;
        private MaterialProperty furGravityIntensity;
        private MaterialProperty furBlurEnabled;
        private MaterialProperty furBlurDistance;
        private MaterialProperty alphaClip;
        private MaterialProperty responsiveAA;
        private MaterialProperty cutoff;
        private MaterialProperty furScale;
        private MaterialProperty furMaxCount;
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
            DrawSurfaceOptions(material);
            DrawBaseInputs();
            DrawPbrInputs();
            DrawNormalInputs();
            DrawFurShellInputs();
            DrawFurDirectionInputs();
            DrawFurBlurInputs();
            DrawEmissionRimInputs();
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

            ApplyFurMaterialOptions(material);
            UpdateEmissiveFlag(material);
            ApplyDirectionMapKeyword(material);
            ApplyGBufferStencilState(material);
        }

        private static void ApplyGBufferStencilState(Material material)
        {
            if (material == null || !material.HasProperty("_BurtGBufferStencilRef") || !material.HasProperty("_BurtGBufferStencilWriteMask"))
            {
                return;
            }

            material.SetFloat("_BurtGBufferStencilRef", BurtShadingModelIds.DeferredStencilFurRef);
            if (material.HasProperty("_BurtGBufferStencilReadMask"))
            {
                material.SetFloat("_BurtGBufferStencilReadMask", BurtShadingModelIds.DeferredStencilShadingModelMask);
            }

            material.SetFloat("_BurtGBufferStencilWriteMask", BurtShadingModelIds.DeferredStencilShadingModelMask);
        }

        private void CacheProperties()
        {
            baseMap = Find("_BaseMap");
            baseColor = Find("_BaseColor");
            darkColor = Find("_DarkColor");
            baseMapPanner = Find("_BaseMapPanner");
            normalMap = Find("_NormalMap");
            normalScale = Find("_NormalScale");
            maskMap = Find("_MaskMap");
            occlusion = Find("_Occlusion");
            roughness = Find("_Roughness");
            reflectance = Find("_Reflectance");
            anisotropy = Find("_Anisotropy");
            emissiveMap = Find("_EmissiveMap");
            emissiveColor = Find("_EmissiveColor");
            emissiveTillingPanner = Find("_EmissiveTillingPanner");
            emissiveUseViewSpaceUV = Find("_EmissiveUseViewSpaceUV");
            viewSpaceUVNormalIntensity = Find("_ViewSpaceUVNormalIntensity");
            furRimIntensity = Find("_FurRimIntensity");
            furRimPower = Find("_FurRimPower");
            furAttenuation = Find("_FurAttenuation");
            furTickness = Find("_FurTickness");
            furTicknessCurve = Find("_FurTicknessCurve");
            furExpand = Find("_FurExpand");
            furSpacing = Find("_FurSpacing");
            furSpacingMax = Find("_FurSpacingMax");
            flowTex = Find("_FlowTex");
            flowTexUV2 = Find("_FlowTexUV2");
            flowTilling = Find("_FlowTilling");
            flowPanner = Find("_FlowPanner");
            flowDirectionMap = Find("_FlowDirectionMap");
            useDirectionMap = Find("_UseDirectionMap");
            flowDirectionMapSegmentArray = Find("_FlowDirectionMapSegmentArray");
            useDirectionMapSegment = Find("_UseDirectionMapSegment");
            flowDirectionUV2 = Find("_FlowDirectionUV2");
            flowDirectionIntensity = Find("_FlowDirectionIntensity");
            flowDirectionIntensitySegment1 = Find("_FlowDirectionIntensitySegment1");
            flowDirectionIntensitySegment2 = Find("_FlowDirectionIntensitySegment2");
            flowDirectionIntensitySegment3 = Find("_FlowDirectionIntensitySegment3");
            furGravityDirection = Find("_FurGravityDirection");
            furGravityIntensity = Find("_FurGravityIntensity");
            furBlurEnabled = Find("_FurBlurEnabled");
            furBlurDistance = Find("_FurBlurDistance");
            alphaClip = Find("_AlphaClip");
            responsiveAA = Find("_ResponsiveAA");
            cutoff = Find("_Cutoff");
            furScale = Find("_FurScale");
            furMaxCount = Find("_FurMaxCount");
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

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(SurfaceTypeLabel, "AlphaTest Opaque");
                EditorGUILayout.TextField(ShadingModelLabel, "Multipass Fur / Hair GBuffer");
            }

            DrawAlphaClipProperty();
            DrawProperty(responsiveAA);
            if (cutoff != null)
            {
                bool showCutoff = alphaClip == null || alphaClip.floatValue >= 0.5f;
                using (new EditorGUI.DisabledScope(!showCutoff))
                {
                    DrawProperty(cutoff);
                }
            }

            DrawResolvedState(material);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawResolvedState(Material material)
        {
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

                if (zWrite != null)
                {
                    EditorGUILayout.Toggle(ZWriteLabel, zWrite.floatValue >= 0.5f);
                }

                if (zTest != null)
                {
                    EditorGUILayout.TextField(ZTestLabel, ((CompareFunction)Mathf.RoundToInt(zTest.floatValue)).ToString());
                }

                if (srcBlend != null && dstBlend != null)
                {
                    EditorGUILayout.TextField(BlendLabel, GetBlendStateName());
                }

                EditorGUILayout.TextField(EnabledPassesLabel, "BurtDepthOnly, BurtGBuffer, ShadowCaster, BurtForward");
                EditorGUILayout.Toggle(DirectionKeywordLabel, IsDirectionMapKeywordEnabled(material));
            }
        }

        private void DrawBaseInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(BaseInputsLabel, ref showBaseInputs))
            {
                return;
            }

            DrawTextureWithColor(BaseMapLabel, baseMap, baseColor);
            DrawProperty(darkColor);
            DrawProperty(baseMapPanner);
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
                BurtShaderGUIUtility.DrawChannelHint("Channels: R Metallic | G Occlusion | B Height | A Roughness");
            }

            DrawProperty(occlusion);
            DrawProperty(roughness);
            DrawProperty(reflectance);
            DrawProperty(anisotropy);
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

        private void DrawFurShellInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(FurShellLabel, ref showFurShellInputs))
            {
                return;
            }

            DrawProperty(furAttenuation);
            DrawProperty(furTickness);
            DrawProperty(furTicknessCurve);
            DrawProperty(furExpand);
            DrawProperty(furSpacing);
            DrawProperty(furSpacingMax);

            BurtShaderGUIUtility.DrawSubHeader("Flow Noise");
            DrawTexture(FlowTexLabel, flowTex);
            DrawProperty(flowTexUV2);
            DrawProperty(flowTilling);
            DrawProperty(flowPanner);

            DrawRuntimeMultipassProperties();
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawRuntimeMultipassProperties()
        {
            if (furScale == null && furMaxCount == null)
            {
                return;
            }

            BurtShaderGUIUtility.DrawSubHeader("Runtime Multipass");
            using (new EditorGUI.DisabledScope(true))
            {
                DrawProperty(furScale);
                DrawProperty(furMaxCount);
            }
        }

        private void DrawFurDirectionInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(FurDirectionLabel, ref showFurDirectionInputs))
            {
                return;
            }

            DrawDirectionToggle(useDirectionMap);
            DrawProperty(flowDirectionUV2);
            DrawTexture(FlowDirectionMapLabel, flowDirectionMap);
            DrawDirectionToggle(useDirectionMapSegment);

            bool useSegment = useDirectionMapSegment != null && !useDirectionMapSegment.hasMixedValue && useDirectionMapSegment.floatValue >= 0.5f;
            using (new EditorGUI.DisabledScope(!useSegment))
            {
                DrawTexture(FlowDirectionMapSegmentArrayLabel, flowDirectionMapSegmentArray);
                DrawProperty(flowDirectionIntensitySegment1);
                DrawProperty(flowDirectionIntensitySegment2);
                DrawProperty(flowDirectionIntensitySegment3);
            }

            using (new EditorGUI.DisabledScope(useSegment))
            {
                DrawProperty(flowDirectionIntensity);
            }

            BurtShaderGUIUtility.DrawSubHeader("Gravity");
            DrawProperty(furGravityDirection);
            DrawProperty(furGravityIntensity);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawFurBlurInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(FurBlurLabel, ref showFurBlurInputs))
            {
                return;
            }

            DrawProperty(furBlurEnabled);
            bool blurEnabled = furBlurEnabled == null || furBlurEnabled.hasMixedValue || furBlurEnabled.floatValue >= 0.5f;
            using (new EditorGUI.DisabledScope(!blurEnabled))
            {
                DrawProperty(furBlurDistance);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawEmissionRimInputs()
        {
            if (!BurtShaderGUIUtility.BeginSection(EmissionRimLabel, ref showEmissionRimInputs))
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawTextureWithColor(EmissiveMapLabel, emissiveMap, emissiveColor);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (Object target in materialEditor.targets)
                {
                    UpdateEmissiveFlag(target as Material);
                }
            }

            DrawProperty(emissiveTillingPanner);
            DrawProperty(emissiveUseViewSpaceUV);
            bool useViewSpaceUV = emissiveUseViewSpaceUV != null && !emissiveUseViewSpaceUV.hasMixedValue && emissiveUseViewSpaceUV.floatValue >= 0.5f;
            using (new EditorGUI.DisabledScope(!useViewSpaceUV))
            {
                DrawProperty(viewSpaceUVNormalIntensity);
            }

            BurtShaderGUIUtility.DrawSubHeader("Rim");
            DrawProperty(furRimIntensity);
            DrawProperty(furRimPower);
            BurtShaderGUIUtility.EndSection();
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
                ApplyFurMaterialOptions(target as Material);
            }
        }

        private void DrawDirectionToggle(MaterialProperty property)
        {
            if (property == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            DrawProperty(property);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            foreach (Object target in materialEditor.targets)
            {
                ApplyDirectionMapKeyword(target as Material);
            }
        }

        private void DrawTextureWithColor(GUIContent label, MaterialProperty texture, MaterialProperty color)
        {
            if (texture != null && color != null)
            {
                materialEditor.TexturePropertySingleLine(label, texture, color);
                return;
            }

            DrawTexture(label, texture);
            DrawProperty(color);
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

        private void DrawTexture(GUIContent label, MaterialProperty texture)
        {
            if (texture == null)
            {
                return;
            }

            materialEditor.TexturePropertySingleLine(label, texture);
        }

        private void DrawProperty(MaterialProperty property)
        {
            BurtShaderGUIUtility.DrawProperty(materialEditor, property);
        }

        private string GetBlendStateName()
        {
            BlendMode sourceBlend = (BlendMode)Mathf.RoundToInt(srcBlend.floatValue);
            BlendMode destinationBlend = (BlendMode)Mathf.RoundToInt(dstBlend.floatValue);
            return sourceBlend + " / " + destinationBlend;
        }

        private static bool IsDirectionMapKeywordEnabled(Material material)
        {
            if (material == null)
            {
                return false;
            }

            return material.IsKeywordEnabled(DirectionMapKeyword);
        }

        private static void ApplyFurMaterialOptions(Material material)
        {
            if (material == null)
            {
                return;
            }

            BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);
            ClampCull(material);

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 1.0f);
            }

            if (material.HasProperty("_ZTest"))
            {
                material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
            }

            if (material.HasProperty("_DoubleSidedNormalModeConstants") && IsCullOff(material))
            {
                material.SetVector("_DoubleSidedNormalModeConstants", BurtShaderGUIUtility.GetDoubleSidedNormalModeConstants(2));
            }

            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.SetOverrideTag("Queue", "AlphaTest");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.enableInstancing = true;
            BurtShadingModelIds.ApplyMotionVectorStencilProperties(material);
            material.SetShaderPassEnabled("BurtDepthOnly", true);
            material.SetShaderPassEnabled("BurtMultipassMotionVectors", true);
            material.SetShaderPassEnabled("BurtMultipassResponsiveAAMask", true);
            material.SetShaderPassEnabled("BurtGBuffer", true);
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetShaderPassEnabled("BurtForward", true);
        }

        private static void ClampCull(Material material)
        {
            if (!material.HasProperty("_Cull"))
            {
                return;
            }

            int cullMode = Mathf.RoundToInt(material.GetFloat("_Cull"));
            if (cullMode < (int)CullMode.Off || cullMode > (int)CullMode.Back)
            {
                cullMode = (int)CullMode.Back;
            }

            material.SetFloat("_Cull", cullMode);
        }

        private static bool IsCullOff(Material material)
        {
            return material.HasProperty("_Cull") && Mathf.RoundToInt(material.GetFloat("_Cull")) == (int)CullMode.Off;
        }

        private static void ApplyDirectionMapKeyword(Material material)
        {
            if (material == null)
            {
                return;
            }

            bool enabled =
                (material.HasProperty("_UseDirectionMap") && material.GetFloat("_UseDirectionMap") >= 0.5f) ||
                (material.HasProperty("_UseDirectionMapSegment") && material.GetFloat("_UseDirectionMapSegment") >= 0.5f);

            if (enabled)
            {
                material.EnableKeyword(DirectionMapKeyword);
            }
            else
            {
                material.DisableKeyword(DirectionMapKeyword);
            }
        }

        private static void UpdateEmissiveFlag(Material material)
        {
            if (material == null || !material.HasProperty("_EmissiveColor"))
            {
                return;
            }

            Color emission = material.GetColor("_EmissiveColor");
            MaterialGlobalIlluminationFlags flags = material.globalIlluminationFlags;
            material.globalIlluminationFlags = emission.maxColorComponent <= 0.0f
                ? flags | MaterialGlobalIlluminationFlags.EmissiveIsBlack
                : flags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }
    }
}
