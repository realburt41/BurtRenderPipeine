using Burt.RenderPipeline;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    public sealed class BurtHairShaderGUI : ShaderGUI
    {
        private const float DefaultHairCutoff = 0.33f;
        private const float CutoffMigrationEpsilon = 0.0001f;

        private static readonly GUIContent SurfaceOptionsLabel = new GUIContent("Hair Surface Options");
        private static readonly GUIContent BaseInputsLabel = new GUIContent("Base Inputs");
        private static readonly GUIContent HairInputsLabel = new GUIContent("Hair Inputs");
        private static readonly GUIContent NormalLabel = new GUIContent("Normal / Debug");
        private static readonly GUIContent EmissionLabel = new GUIContent("Emission");

        private static readonly GUIContent BaseMapLabel = new GUIContent("Base Map", "Hair tint RGB and alpha for card cutout.");
        private static readonly GUIContent MaskMapLabel = new GUIContent("Hair Mask Map", "AvatarHair: R Reflectance, G Occlusion, B Height.");
        private static readonly GUIContent IdMapLabel = new GUIContent("ID Map", "R Strand direction, G structure mask, A gradient color mask.");
        private static readonly GUIContent GradientMapLabel = new GUIContent("Gradient Map", "AvatarHair gradient color lookup.");
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
        private MaterialProperty rootColor;
        private MaterialProperty rootGradient;
        private MaterialProperty rootGradientEnable;
        private MaterialProperty rootGradientReverse;
        private MaterialProperty rootGradientPosEnable;
        private MaterialProperty gradientDirection;
        private MaterialProperty gradientPosOffset;
        private MaterialProperty maskMap;
        private MaterialProperty idMap;
        private MaterialProperty idXTilling;
        private MaterialProperty idIntensity;
        private MaterialProperty tangentA;
        private MaterialProperty tangentB;
        private MaterialProperty reflectance;
        private MaterialProperty normalMap;
        private MaterialProperty normalScale;
        private MaterialProperty emissionMap;
        private MaterialProperty emissionColor;
        private MaterialProperty alphaClip;
        private MaterialProperty cutoff;
        private MaterialProperty shadowCutOff;
        private MaterialProperty opacityMaskValue;
        private MaterialProperty opacityMaskOffset;
        private MaterialProperty doubleSidedEnable;
        private MaterialProperty doubleSidedNormalMode;
        private MaterialProperty cull;
        private MaterialProperty hairScatter;
        private MaterialProperty hairScatterBoost;
        private MaterialProperty hairSpecularScale;
        private MaterialProperty hairShiftScale;
        private MaterialProperty hairRoughnessOffset;
        private MaterialProperty hairTangentFlip;
        private MaterialProperty scatterUseFullRange;
        private MaterialProperty scatter;
        private MaterialProperty scatterFull;
        private MaterialProperty occlusion;
        private MaterialProperty albedoOcclusion;
        private MaterialProperty albedoOcclusionColor;
        private MaterialProperty backLightIntensity;
        private MaterialProperty backLightMask;
        private MaterialProperty backLightMaskRange;
        private MaterialProperty hairShadowFillStrength;
        private MaterialProperty hairBrightColor;
        private MaterialProperty hairBrightIntensity;
        private MaterialProperty hairShadowColor;
        private MaterialProperty hairShadowIntensity;
        private MaterialProperty hairShadowPower;
        private MaterialProperty hairRotate;
        private MaterialProperty specularShift;
        private MaterialProperty secondarySpecularShift;
        private MaterialProperty roughParameter;
        private MaterialProperty edgeRoughRimPower;
        private MaterialProperty specularColor;
        private MaterialProperty specularSecondColor;
        private MaterialProperty gradientColorEnable;
        private MaterialProperty gradientMap;
        private MaterialProperty gradientRowIndex;
        private MaterialProperty gradientSoftLight;
        private MaterialProperty gradientOverlay;
        private MaterialProperty gradientReplace;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();
            SyncCutoffPropertiesForInspector();

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
            ApplyGBufferStencilState(material);
            BurtShaderGUIUtility.UpdateEmissionFlag(material);
        }

        private static void ApplyGBufferStencilState(Material material)
        {
            if (material == null || !material.HasProperty("_BurtGBufferStencilRef") || !material.HasProperty("_BurtGBufferStencilWriteMask"))
            {
                return;
            }

            material.SetFloat("_BurtGBufferStencilRef", BurtShadingModelIds.DeferredStencilHairRef);
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
            rootColor = Find("_RootColor");
            rootGradient = Find("_RootGradient");
            rootGradientEnable = Find("_RootGradientEnable");
            rootGradientReverse = Find("_RootGradientReverse");
            rootGradientPosEnable = Find("_RootGradientPosEnable");
            gradientDirection = Find("_GradientDirection");
            gradientPosOffset = Find("_GradientPosOffset");
            maskMap = Find("_MaskMap");
            idMap = Find("_IDMap");
            idXTilling = Find("_IDXTilling");
            idIntensity = Find("_IDIntensity");
            tangentA = Find("_TangentA");
            tangentB = Find("_TangentB");
            reflectance = Find("_Reflectance");
            normalMap = Find("_NormalMap");
            normalScale = Find("_NormalScale");
            emissionMap = Find("_EmissionMap");
            emissionColor = Find("_EmissionColor");
            alphaClip = Find("_AlphaClip");
            cutoff = Find("_Cutoff");
            shadowCutOff = Find("_ShadowCutOff");
            opacityMaskValue = Find("_OpacityMaskValue");
            opacityMaskOffset = Find("_OpacityMaskOffset");
            doubleSidedEnable = Find("_DoubleSidedEnable");
            doubleSidedNormalMode = Find("_DoubleSidedNormalMode");
            cull = Find("_Cull");
            hairScatter = Find("_HairScatter");
            hairScatterBoost = Find("_HairScatterBoost");
            hairSpecularScale = Find("_HairSpecularScale");
            hairShiftScale = Find("_HairShiftScale");
            hairRoughnessOffset = Find("_HairRoughnessOffset");
            hairTangentFlip = Find("_HairTangentFlip");
            scatterUseFullRange = Find("_ScatterUseFullRange");
            scatter = Find("_Scatter");
            scatterFull = Find("_ScatterFull");
            occlusion = Find("_Occlusion");
            albedoOcclusion = Find("_AlbedoOcclusion");
            albedoOcclusionColor = Find("_AlbedoOcclusionColor");
            backLightIntensity = Find("_BackLightIntensity");
            backLightMask = Find("_BackLightMask");
            backLightMaskRange = Find("_BackLightMaskRange");
            hairShadowFillStrength = Find("_HairShadowFillStrength");
            hairBrightColor = Find("_HairBrightColor");
            hairBrightIntensity = Find("_HairBrightIntensity");
            hairShadowColor = Find("_HairShadowColor");
            hairShadowIntensity = Find("_HairShadowIntensity");
            hairShadowPower = Find("_HairShadowPower");
            hairRotate = Find("_HairRotate");
            specularShift = Find("_SpecularShift");
            secondarySpecularShift = Find("_SecondarySpecularShift");
            roughParameter = Find("_RoughParameter");
            edgeRoughRimPower = Find("_EdgeRoughRimPower");
            specularColor = Find("_SpecularColor");
            specularSecondColor = Find("_SpecularSecondColor");
            gradientColorEnable = Find("_GradientColorEnable");
            gradientMap = Find("_GradientMap");
            gradientRowIndex = Find("_GradientRowIndex");
            gradientSoftLight = Find("_GradientSoftLight");
            gradientOverlay = Find("_GradientOverlay");
            gradientReplace = Find("_GradientReplace");
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
                    EditorGUI.BeginChangeCheck();
                    DrawProperty(cutoff);
                    if (EditorGUI.EndChangeCheck())
                    {
                        SyncOpacityMaskValueToCutoff();
                    }
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
            EditorGUILayout.HelpBox("Hair is double-sided and writes AvatarHair strand direction and lobe controls into the Hair GBuffer payload.", MessageType.Info);
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
            DrawProperty(rootGradientEnable);
            DrawProperty(rootGradientReverse);
            DrawProperty(rootColor);
            DrawProperty(rootGradient);
            DrawProperty(rootGradientPosEnable);
            DrawProperty(gradientDirection);
            DrawProperty(gradientPosOffset);
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
                BurtShaderGUIUtility.DrawChannelHint("Channels: R Reflectance | G Occlusion | B Height | A unused");
            }

            BurtShaderGUIUtility.DrawSubHeader("Alpha");
            DrawProperty(hairShadowFillStrength);
            DrawProperty(shadowCutOff);
            using (new EditorGUI.DisabledScope(true))
            {
                DrawProperty(opacityMaskValue);
            }
            DrawProperty(opacityMaskOffset);

            BurtShaderGUIUtility.DrawSubHeader("PBR");
            DrawProperty(scatterUseFullRange);
            if (scatterUseFullRange == null || scatterUseFullRange.floatValue < 0.5f)
            {
                DrawProperty(scatter);
            }
            else
            {
                DrawProperty(scatterFull);
            }
            DrawProperty(occlusion);
            DrawProperty(albedoOcclusion);
            DrawProperty(albedoOcclusionColor);
            DrawProperty(backLightIntensity);
            DrawProperty(backLightMask);
            DrawProperty(backLightMaskRange);
            DrawProperty(reflectance);
            DrawProperty(hairSpecularScale);

            BurtShaderGUIUtility.DrawSubHeader("ID / Strand");
            DrawTexture(IdMapLabel, idMap);
            DrawProperty(idXTilling);
            DrawProperty(idIntensity);
            DrawProperty(tangentA);
            DrawProperty(tangentB);
            DrawProperty(hairTangentFlip);
            DrawProperty(hairRotate);

            BurtShaderGUIUtility.DrawSubHeader("Structure");
            DrawProperty(hairBrightColor);
            DrawProperty(hairBrightIntensity);
            DrawProperty(hairShadowColor);
            DrawProperty(hairShadowIntensity);
            DrawProperty(hairShadowPower);

            BurtShaderGUIUtility.DrawSubHeader("Specular");
            DrawProperty(specularShift);
            DrawProperty(secondarySpecularShift);
            DrawProperty(roughParameter);
            DrawProperty(edgeRoughRimPower);
            DrawProperty(specularColor);
            DrawProperty(specularSecondColor);

            BurtShaderGUIUtility.DrawSubHeader("Gradient Color");
            DrawProperty(gradientColorEnable);
            DrawTexture(GradientMapLabel, gradientMap);
            DrawProperty(gradientRowIndex);
            DrawProperty(gradientSoftLight);
            DrawProperty(gradientOverlay);
            DrawProperty(gradientReplace);

            BurtShaderGUIUtility.DrawSubHeader("Legacy Compat");
            DrawProperty(hairScatter);
            DrawProperty(hairScatterBoost);
            DrawProperty(hairShiftScale);
            DrawProperty(hairRoughnessOffset);
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

        private void SyncOpacityMaskValueToCutoff()
        {
            if (cutoff == null || opacityMaskValue == null)
            {
                return;
            }

            opacityMaskValue.floatValue = cutoff.floatValue;
        }

        private void SyncCutoffPropertiesForInspector()
        {
            if (cutoff == null || opacityMaskValue == null || cutoff.hasMixedValue || opacityMaskValue.hasMixedValue)
            {
                return;
            }

            float cutoffValue = cutoff.floatValue;
            float opacityMask = opacityMaskValue.floatValue;
            if (Mathf.Abs(cutoffValue - DefaultHairCutoff) <= CutoffMigrationEpsilon &&
                Mathf.Abs(opacityMask - cutoffValue) > CutoffMigrationEpsilon)
            {
                cutoffValue = opacityMask;
                cutoff.floatValue = cutoffValue;
            }

            opacityMaskValue.floatValue = cutoffValue;
        }

        private static void SyncHairOpacityCutoff(Material material)
        {
            if (!material.HasProperty("_Cutoff") || !material.HasProperty("_OpacityMaskValue"))
            {
                return;
            }

            float cutoffValue = material.GetFloat("_Cutoff");
            float opacityMaskValue = material.GetFloat("_OpacityMaskValue");
            if (Mathf.Abs(cutoffValue - DefaultHairCutoff) <= CutoffMigrationEpsilon &&
                Mathf.Abs(opacityMaskValue - cutoffValue) > CutoffMigrationEpsilon)
            {
                cutoffValue = opacityMaskValue;
                material.SetFloat("_Cutoff", cutoffValue);
            }

            material.SetFloat("_OpacityMaskValue", cutoffValue);
        }

        internal static void ApplyHairMaterialOptions(Material material)
        {
            if (material == null)
            {
                return;
            }

            BurtShaderGUIUtility.ApplyDoubleSidedState(material, true);
            BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);
            BurtShadingModelIds.ApplyMotionVectorStencilProperties(material);
            SyncHairOpacityCutoff(material);
            material.SetOverrideTag("RenderType", "Opaque");
            material.SetOverrideTag("Queue", string.Empty);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("BurtDepthOnly", true);
            material.SetShaderPassEnabled("BurtMotionVectors", true);
            material.SetShaderPassEnabled("BurtGBuffer", true);
            material.SetShaderPassEnabled("ShadowCaster", true);
            material.SetShaderPassEnabled("BurtForward", true);
        }
    }
}
