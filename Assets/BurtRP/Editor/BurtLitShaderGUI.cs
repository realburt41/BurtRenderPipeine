using Burt.RenderPipeline;
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
        private static readonly GUIContent FabricLabel = new GUIContent("Fabric");
        private static readonly GUIContent SilkLabel = new GUIContent("Silk");
        private static readonly GUIContent FoliageLabel = new GUIContent("Foliage");
        private static readonly GUIContent TrunkLabel = new GUIContent("Trunk");
        private static readonly GUIContent NormalLabel = new GUIContent("Normal");
        private static readonly GUIContent EmissionLabel = new GUIContent("Emission");

        private static readonly GUIContent BaseMapLabel = new GUIContent("Base Map", "Albedo RGB and alpha for clipping or transparent materials.");
        private static readonly GUIContent MaskMapLabel = new GUIContent("Mask Map", "R Metallic, G Occlusion, B Reserved, A Smoothness or Roughness for Fabric/Silk.");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map");
        private static readonly GUIContent FoliageNSRMapLabel = new GUIContent("NSR Map", "XRender foliage packed map: R/G normal, B thickness, A roughness.");
        private static readonly GUIContent ClearCoatNormalMapLabel = new GUIContent("Clear Coat Normal Map");
        private static readonly GUIContent FuzzMapLabel = new GUIContent("Fuzz Map");
        private static readonly GUIContent FuzzMaskLabel = new GUIContent("Fuzz Amount Mask");
        private static readonly GUIContent AlphaMapLabel = new GUIContent("Alpha Map");
        private static readonly GUIContent TintPaletteLabel = new GUIContent("Global Tint Palette");
        private static readonly GUIContent LocalTintPaletteLabel = new GUIContent("Local Tint Palette");
        private static readonly GUIContent NoiseMapLabel = new GUIContent("Noise Map");
        private static readonly GUIContent EmissionMapLabel = new GUIContent("Emission Map");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent SubsurfaceScatteringModeLabel = new GUIContent("SSS Algorithm", "Choose 5S Burley, 4S Separable screen-space skin scattering, or 3S Preintegrated skin shading.");
        private static readonly GUIContent SubsurfaceProfileLabel = new GUIContent("Subsurface Profile", "Drag a BurtSubsurfaceProfile asset here. The material stores the resolved profile slot for the shader.");
        private static readonly GUIContent SubsurfaceProfileIndexLabel = new GUIContent("Subsurface Profile Index", "Runtime slot used by the shader. 0 is the default profile, 1-7 are the pipeline profile list.");
        private static readonly GUIContent SubsurfaceThicknessMapLabel = new GUIContent("Subsurface Thickness Map", "R controls local skin thickness for 4S/5S transmission and profile lookup.");
        private static readonly GUIContent DoubleSidedLabel = new GUIContent("Double Sided", "Render both front and back faces by switching culling off.");
        private static readonly GUIContent DoubleSidedNormalModeLabel = new GUIContent("Double Sided Normal Mode", "Back-face normal mode, matching XRender: None, Flip, or Mirror in tangent space.");
        private static readonly GUIContent SurfaceTypeLabel = new GUIContent("Surface Type");
        private static readonly GUIContent FoliageTintModeLabel = new GUIContent("Tint Type");
        private static readonly GUIContent FoliageUseBakedNormalsLabel = new GUIContent("Use Baked Normals", "Enable the XRender foliage baked-normal path and its ShadowCaster scale adjustment.");
        private static readonly GUIContent TrunkMaskMapLabel = new GUIContent("MOHR Map", "XRender trunk packed map: G occlusion and A roughness. R metallic and B thickness are ignored by the current trunk material model.");
        private static readonly GUIContent RenderQueueLabel = new GUIContent("Render Queue");
        private static readonly GUIContent CullLabel = new GUIContent("Cull");
        private static readonly GUIContent ZWriteLabel = new GUIContent("ZWrite");
        private static readonly GUIContent ZTestLabel = new GUIContent("ZTest");
        private static readonly GUIContent BlendLabel = new GUIContent("Blend");
        private static readonly GUIContent EnabledPassesLabel = new GUIContent("Enabled Passes");
        private static readonly GUIContent ResponsiveAALabel = new GUIContent("Responsive AA", "Marks this material in stencil bit 16 so Temporal AA lowers history feedback on thin or fast-changing surfaces.");
        private static readonly string[] SurfaceTypeNames = { "Opaque", "Transparent" };
        private static readonly string[] DoubleSidedNormalModeNames = { "None", "Flip", "Mirror" };
        private static readonly string[] SubsurfaceScatteringModeNames = { "5S Burley", "4S Separable", "3S Preintegrated" };
        private static readonly string[] FoliageTintModeNames = { "Only Local", "Constant Tint", "Tint Map" };
        private const string SubsurfaceProfileGuidTag = "BurtSubsurfaceProfileGuid";
        private const int GBufferStencilShadingModelMask = 0xE0;
        private const int GBufferStencilResponsiveAABit = 0x10;
        private const int GBufferStencilDefaultLitRef = 0x20;
        private const int GBufferStencilSubsurfaceRef = 0x40;
        private const int GBufferStencilClearCoatRef = 0x80;
        private const int GBufferStencilFabricRef = 0xA0;
        private const int GBufferStencilFoliageRef = 0xC0;
        private static bool showSurfaceOptions = true;
        private static bool showBaseInputs = true;
        private static bool showPbrMaskInputs = true;
        private static bool showClearCoatInputs = true;
        private static bool showSubsurfaceInputs = true;
        private static bool showFabricInputs = true;
        private static bool showSilkInputs = true;
        private static bool showFoliageInputs = true;
        private static bool showTrunkInputs = true;
        private static bool showNormalInputs = true;
        private static bool showEmissionInputs = true;

        private MaterialEditor materialEditor;
        private MaterialProperty[] properties;

        private MaterialProperty baseMap;
        private MaterialProperty baseColor;
        private MaterialProperty maskMap;
        private MaterialProperty metallic;
        private MaterialProperty anisotropy;
        private MaterialProperty smoothness;
        private MaterialProperty roughness;
        private MaterialProperty occlusionStrength;
        private MaterialProperty reflectance;
        private MaterialProperty clearCoatMask;
        private MaterialProperty clearCoatRoughness;
        private MaterialProperty clearCoatNormalMap;
        private MaterialProperty clearCoatNormalScale;
        private MaterialProperty subsurfaceThickness;
        private MaterialProperty subsurfaceThicknessMap;
        private MaterialProperty subsurfacePower;
        private MaterialProperty subsurfaceDistortion;
        private MaterialProperty subsurfaceAmbient;
        private MaterialProperty subsurfaceScatteringMode;
        private MaterialProperty subsurfaceProfileIndex;
        private MaterialProperty subsurface3SCurvatureScale;
        private MaterialProperty subsurface3SCurvatureBias;
        private MaterialProperty fuzzMap;
        private MaterialProperty fuzzColor;
        private MaterialProperty fuzzMask;
        private MaterialProperty fuzzAmount;
        private MaterialProperty fuzzRoughness;
        private MaterialProperty facingColor;
        private MaterialProperty foliageTransmissionColor;
        private MaterialProperty foliageTransmissionWeight;
        private MaterialProperty foliageThickness;
        private MaterialProperty foliageBackLight;
        private MaterialProperty foliageSubsurfaceColorSaturate;
        private MaterialProperty alphaMap;
        private MaterialProperty alphaIncrease;
        private MaterialProperty subsurfaceColor;
        private MaterialProperty subsurfaceColorSaturate;
        private MaterialProperty thicknessScale;
        private MaterialProperty roughnessScale;
        private MaterialProperty reflectanceScale;
        private MaterialProperty transmissionNdotL;
        private MaterialProperty vertexAORemap;
        private MaterialProperty tintPalette;
        private MaterialProperty localTintPalette;
        private MaterialProperty tintValue;
        private MaterialProperty tintScale;
        private MaterialProperty localTintColor;
        private MaterialProperty tintAOHeightRatio;
        private MaterialProperty tintAORemap;
        private MaterialProperty tintHeightContrast;
        private MaterialProperty treeHeight;
        private MaterialProperty foliageTintMode;
        private MaterialProperty foliageTintModeLegacy;
        private MaterialProperty foliageUseBakedNormals;
        private MaterialProperty maxBendAngle;
        private MaterialProperty swayIntensity;
        private MaterialProperty flutterTipFrequency;
        private MaterialProperty flutterTipIntensity;
        private MaterialProperty bendMaskPow;
        private MaterialProperty toTrunkMaskPow;
        private MaterialProperty terrainBlendTerrainTog;
        private MaterialProperty terrainBlendBlendHeight;
        private MaterialProperty sssIntensity;
        private MaterialProperty fresnelIntensity;
        private MaterialProperty fresnelExp;
        private MaterialProperty grassSpecular;
        private MaterialProperty baseColorTip;
        private MaterialProperty tipMaskPow;
        private MaterialProperty heightAO;
        private MaterialProperty heightAOFallOff;
        private MaterialProperty tlNormalWeight;
        private MaterialProperty ssShadowIntensity;
        private MaterialProperty ssShadowDistance;
        private MaterialProperty groundFadeIntensity;
        private MaterialProperty noiseMap;
        private MaterialProperty variationIntensity01;
        private MaterialProperty variationIntensity02;
        private MaterialProperty variation01Height;
        private MaterialProperty variation02Height;
        private MaterialProperty variation01;
        private MaterialProperty variation02;
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
        private MaterialProperty responsiveAA;

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
        {
            this.materialEditor = materialEditor;
            properties = props;
            CacheProperties();

            Material material = materialEditor.target as Material;
            MigrateLegacyTransparentShader(material);
            SyncFoliageTintModeProperties(material);
            SyncTrunkXRenderCompatibilityProperties(material);
            DrawSurfaceOptions(material);
            DrawBaseInputs(material);
            DrawPbrInputs(material);
            DrawNormalInputs(material);
            DrawClearCoatInputs(material);
            DrawSubsurfaceInputs(material);
            DrawFabricInputs(material);
            DrawSilkInputs(material);
            DrawFoliageInputs(material);
            DrawTrunkInputs(material);
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

            MigrateFabricRoughness(material);
            SyncFoliageTintModeProperties(material);
            SyncTrunkXRenderCompatibilityProperties(material);
            BurtShaderGUIUtility.UpdateEmissionFlag(material);
            ClampSubsurfaceScatteringMode(material);
            ApplySurfaceOptions(material);
        }

        private static void MigrateFabricRoughness(Material material)
        {
            if ((!IsFabricShader(material) && !IsSilkShader(material)) || !material.HasProperty("_Roughness"))
            {
                return;
            }

            if (HasSerializedFloat(material, "_Roughness") || !TryGetSerializedFloat(material, "_Smoothness", out var legacySmoothness))
            {
                return;
            }

            material.SetFloat("_Roughness", 1f - Mathf.Clamp01(legacySmoothness));
            EditorUtility.SetDirty(material);
        }

        private static void SyncFoliageTintModeProperties(Material material)
        {
            if (material == null || !IsFoliageShader(material) || IsGrassShader(material) || !material.HasProperty("_CustomEnum"))
            {
                return;
            }

            float tintMode = Mathf.Clamp(Mathf.Round(material.GetFloat("_CustomEnum")), 0.0f, 2.0f);
            if (!HasSerializedFloat(material, "_CustomEnum") &&
                material.HasProperty("_FoliageTintMode") &&
                TryGetSerializedFloat(material, "_FoliageTintMode", out var legacyTintMode))
            {
                tintMode = Mathf.Clamp(Mathf.Round(legacyTintMode), 0.0f, 2.0f);
            }

            bool changed = SetMaterialFloatIfDifferent(material, "_CustomEnum", tintMode);
            changed |= SetMaterialFloatIfDifferent(material, "_FoliageTintMode", tintMode);
            if (changed)
            {
                EditorUtility.SetDirty(material);
            }
        }

        private static void SyncTrunkXRenderCompatibilityProperties(Material material)
        {
            if (material == null || !IsTrunkShader(material))
            {
                return;
            }

            bool changed = false;
            if (!HasSerializedFloat(material, "_AlphaClip") &&
                material.HasProperty("_AlphaClip") &&
                TryGetSerializedFloat(material, "_AlphaCutoffEnable", out var alphaCutoffEnable))
            {
                changed |= SetMaterialFloatIfDifferent(material, "_AlphaClip", alphaCutoffEnable >= 0.5f ? 1.0f : 0.0f);
            }

            if (!HasSerializedFloat(material, "_Cutoff") &&
                material.HasProperty("_Cutoff") &&
                TryGetSerializedFloat(material, "_AlphaCutoff", out var alphaCutoff))
            {
                changed |= SetMaterialFloatIfDifferent(material, "_Cutoff", Mathf.Clamp01(alphaCutoff));
            }

            if (!HasSerializedFloat(material, "_TerrainBlend_TerrainTog") &&
                material.HasProperty("_TerrainBlend_TerrainTog") &&
                TryGetSerializedFloat(material, "_TerrainBlendEnable", out var terrainBlendEnable))
            {
                changed |= SetMaterialFloatIfDifferent(material, "_TerrainBlend_TerrainTog", terrainBlendEnable >= 0.5f ? 1.0f : 0.0f);
            }

            if (!HasSerializedFloat(material, "_TerrainBlend_BlendHeight") &&
                material.HasProperty("_TerrainBlend_BlendHeight") &&
                TryGetSerializedFloat(material, "_TerrainBlendHeight", out var terrainBlendHeight))
            {
                changed |= SetMaterialFloatIfDifferent(material, "_TerrainBlend_BlendHeight", Mathf.Max(0.001f, terrainBlendHeight));
            }

            if (changed)
            {
                BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);
                EditorUtility.SetDirty(material);
            }
        }

        private static bool SetMaterialFloatIfDifferent(Material material, string propertyName, float value)
        {
            if (material == null || !material.HasProperty(propertyName))
            {
                return false;
            }

            if (Mathf.Approximately(material.GetFloat(propertyName), value))
            {
                return false;
            }

            material.SetFloat(propertyName, value);
            return true;
        }

        private static bool HasSerializedFloat(Material material, string propertyName)
        {
            return TryGetSerializedFloat(material, propertyName, out _);
        }

        private static bool TryGetSerializedFloat(Material material, string propertyName, out float value)
        {
            value = 0f;
            if (material == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            var serializedObject = new SerializedObject(material);
            var floats = serializedObject.FindProperty("m_SavedProperties.m_Floats");
            if (floats == null || !floats.isArray)
            {
                return false;
            }

            for (var i = 0; i < floats.arraySize; i++)
            {
                var entry = floats.GetArrayElementAtIndex(i);
                var name = entry.FindPropertyRelative("first");
                if (name == null || name.stringValue != propertyName)
                {
                    continue;
                }

                var floatValue = entry.FindPropertyRelative("second");
                if (floatValue == null)
                {
                    return false;
                }

                value = floatValue.floatValue;
                return true;
            }

            return false;
        }

        private void CacheProperties()
        {
            baseMap = Find("_BaseMap");
            baseColor = Find("_BaseColor");
            maskMap = Find("_MaskMap");
            metallic = Find("_Metallic");
            anisotropy = Find("_Anisotropy");
            smoothness = Find("_Smoothness");
            roughness = Find("_Roughness");
            occlusionStrength = Find("_OcclusionStrength");
            reflectance = Find("_Reflectance");
            clearCoatMask = Find("_ClearCoatMask");
            clearCoatRoughness = Find("_ClearCoatRoughness");
            clearCoatNormalMap = Find("_ClearCoatNormalMap");
            clearCoatNormalScale = Find("_ClearCoatNormalScale");
            subsurfaceThickness = Find("_SubsurfaceThickness");
            subsurfaceThicknessMap = Find("_SubsurfaceThicknessMap");
            subsurfacePower = Find("_SubsurfacePower");
            subsurfaceDistortion = Find("_SubsurfaceDistortion");
            subsurfaceAmbient = Find("_SubsurfaceAmbient");
            subsurfaceScatteringMode = Find("_SubsurfaceScatteringMode");
            subsurfaceProfileIndex = Find("_SubsurfaceProfileIndex");
            subsurface3SCurvatureScale = Find("_Subsurface3SCurvatureScale");
            subsurface3SCurvatureBias = Find("_Subsurface3SCurvatureBias");
            fuzzMap = Find("_FuzzMap");
            fuzzColor = Find("_FuzzColor");
            fuzzMask = Find("_FuzzMask");
            fuzzAmount = Find("_FuzzAmount");
            fuzzRoughness = Find("_FuzzRoughness");
            facingColor = Find("_FacingColor");
            foliageTransmissionColor = Find("_FoliageTransmissionColor");
            foliageTransmissionWeight = Find("_FoliageTransmissionWeight");
            foliageThickness = Find("_FoliageThickness");
            foliageBackLight = Find("_FoliageBackLight");
            foliageSubsurfaceColorSaturate = Find("_FoliageSubsurfaceColorSaturate");
            alphaMap = Find("_AlphaMap");
            alphaIncrease = Find("_AlphaIncrease");
            subsurfaceColor = Find("_SubsurfaceColor");
            subsurfaceColorSaturate = Find("_SubsurfaceColorSaturate");
            thicknessScale = Find("_ThicknessScale");
            roughnessScale = Find("_RoughnessScale");
            reflectanceScale = Find("_ReflectanceScale");
            transmissionNdotL = Find("_TransmissionNdotL");
            vertexAORemap = Find("_VertexAORemap");
            tintPalette = Find("_TintPalette");
            localTintPalette = Find("_LocalTintPalette");
            tintValue = Find("_TintValue");
            tintScale = Find("_TintScale");
            localTintColor = Find("_LocalTintColor");
            tintAOHeightRatio = Find("_TintAOHeightRatio");
            tintAORemap = Find("_TintAORemap");
            tintHeightContrast = Find("_TintHeightContrast");
            treeHeight = Find("_TreeHeight");
            foliageTintMode = Find("_CustomEnum");
            foliageTintModeLegacy = Find("_FoliageTintMode");
            foliageUseBakedNormals = Find("_FoliageUseBakedNormals");
            maxBendAngle = Find("_MaxBendAngle");
            swayIntensity = Find("_SwayIntensity");
            flutterTipFrequency = Find("_FlutterTipFrequency");
            flutterTipIntensity = Find("_FlutterTipIntensity");
            bendMaskPow = Find("_BendMaskPow");
            toTrunkMaskPow = Find("_ToTrunkMaskPow");
            terrainBlendTerrainTog = Find("_TerrainBlend_TerrainTog");
            terrainBlendBlendHeight = Find("_TerrainBlend_BlendHeight");
            sssIntensity = Find("_SSSIntensity");
            fresnelIntensity = Find("_FresnelIntensity");
            fresnelExp = Find("_FresnelExp");
            grassSpecular = Find("_Specular");
            baseColorTip = Find("_BaseColorTip");
            tipMaskPow = Find("_TipMaskPow");
            heightAO = Find("_HeightAO");
            heightAOFallOff = Find("_HeightAOFallOff");
            tlNormalWeight = Find("_TLNormalWeight");
            ssShadowIntensity = Find("_SSShadowIntensity");
            ssShadowDistance = Find("_SSShadowDistance");
            groundFadeIntensity = Find("_GroundFadeIntensity");
            noiseMap = Find("_NoiseMap");
            variationIntensity01 = Find("_VariationIntensity01");
            variationIntensity02 = Find("_VariationIntensity02");
            variation01Height = Find("_Variation01Height");
            variation02Height = Find("_Variation02Height");
            variation01 = Find("_Variation01");
            variation02 = Find("_Variation02");
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

            bool fixedOpaqueCutout = UsesFixedOpaqueCutoutSurface(material);
            bool transparent = IsTransparentMaterial(material) && !fixedOpaqueCutout;
            if (surface != null)
            {
                if (fixedOpaqueCutout)
                {
                    using (new EditorGUI.DisabledScope(true))
                    {
                        EditorGUILayout.Popup(SurfaceTypeLabel, 0, SurfaceTypeNames);
                    }
                }
                else
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
            DrawAlphaClipProperty();
            DrawResponsiveAAProperty();

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

        private void DrawBaseInputs(Material material)
        {
            if (!BurtShaderGUIUtility.BeginSection(BaseInputsLabel, ref showBaseInputs))
            {
                return;
            }

            if (IsFoliageShader(material) && !IsGrassShader(material))
            {
                DrawTexture(BaseMapLabel, baseMap);
            }
            else
            {
                DrawTextureWithColor(BaseMapLabel, baseMap, baseColor);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawPbrInputs(Material material)
        {
            if (!BurtShaderGUIUtility.BeginSection(PbrMaskLabel, ref showPbrMaskInputs))
            {
                return;
            }

            bool usesTrunk = IsTrunkShader(material);
            bool usesRoughness = IsFabricShader(material) || IsSilkShader(material) || usesTrunk;
            bool usesFoliage = IsFoliageShader(material);
            bool usesTreeFoliage = usesFoliage && !IsGrassShader(material);
            if (!usesTreeFoliage)
            {
                DrawTexture(usesTrunk ? TrunkMaskMapLabel : MaskMapLabel, maskMap);
            }

            if (usesTreeFoliage)
            {
                BurtShaderGUIUtility.DrawChannelHint("Tree Foliage uses the NSR Map for R/G normal, B thickness, and A roughness; vertex color A drives AO.");
            }
            else if (maskMap != null)
            {
                BurtShaderGUIUtility.DrawChannelHint(IsSubsurfaceShader(material)
                    ? "Channels: G Occlusion / 3S Curvature | A Smoothness. Thickness uses Subsurface Thickness Map R."
                    : usesTrunk
                        ? "Channels: G Occlusion | A Roughness. R Metallic and B Thickness are ignored; vertex color A is remapped as AO."
                    : usesFoliage
                        ? "Channels: G Occlusion | B Thickness | A Roughness. R Metallic is ignored by Foliage."
                    : usesRoughness
                        ? "Channels: R Metallic | G Occlusion | B Reserved | A Roughness"
                        : "Channels: R Metallic | G Occlusion | B Reserved | A Smoothness");
            }

            if (!IsSubsurfaceShader(material) && !usesFoliage && !usesTrunk)
            {
                BurtShaderGUIUtility.DrawSubHeader("Lit");
                DrawProperty(metallic);
                DrawProperty(anisotropy);
            }

            if (!usesTreeFoliage)
            {
                BurtShaderGUIUtility.DrawSubHeader("Shared");
                if (!usesFoliage && !usesTrunk)
                {
                    DrawProperty(usesRoughness ? roughness : smoothness);
                }

                if (!usesTrunk)
                {
                    DrawProperty(occlusionStrength);
                }
            }
            if (!IsSubsurfaceShader(material) && !usesFoliage && !usesTrunk)
            {
                DrawProperty(reflectance);
            }

            if (!usesTreeFoliage && maskMap != null)
            {
                EditorGUILayout.HelpBox("Mask channels are multiplied by scalar values below, matching BurtRP GBuffer and Forward sampling.", MessageType.None);
            }

            BurtShaderGUIUtility.EndSection();
        }

        private void DrawFabricInputs(Material material)
        {
            if (!IsFabricShader(material) || IsSilkShader(material))
            {
                return;
            }

            if (!BurtShaderGUIUtility.BeginSection(FabricLabel, ref showFabricInputs))
            {
                return;
            }

            DrawTextureWithColor(FuzzMapLabel, fuzzMap, fuzzColor);
            DrawTexture(FuzzMaskLabel, fuzzMask);
            DrawProperty(fuzzAmount);
            DrawProperty(fuzzRoughness);
            EditorGUILayout.HelpBox("Fabric writes stencil/model 4, stores fuzz color and weight in GBuffer3, and stores tangent, anisotropy, fuzz roughness, and the silk flag in GBuffer4.", MessageType.None);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawSilkInputs(Material material)
        {
            if (!IsSilkShader(material))
            {
                return;
            }

            if (!BurtShaderGUIUtility.BeginSection(SilkLabel, ref showSilkInputs))
            {
                return;
            }

            DrawProperty(facingColor);
            EditorGUILayout.HelpBox("Silk uses the Fabric deferred model with the silk flag set; Facing Color tints the anisotropic silk specular.", MessageType.None);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawFoliageInputs(Material material)
        {
            if (!IsFoliageShader(material))
            {
                return;
            }

            if (!BurtShaderGUIUtility.BeginSection(FoliageLabel, ref showFoliageInputs))
            {
                return;
            }

            bool hasXRenderFoliageInputs = subsurfaceColor != null ||
                subsurfaceColorSaturate != null ||
                thicknessScale != null ||
                roughnessScale != null ||
                reflectanceScale != null ||
                transmissionNdotL != null;

            if (hasXRenderFoliageInputs)
            {
                BurtShaderGUIUtility.DrawSubHeader("XRender Foliage");
                DrawTexture(AlphaMapLabel, alphaMap);
                DrawProperty(alphaIncrease);
                if (!IsGrassShader(material))
                {
                    DrawFoliageUseBakedNormalsProperty(material);
                }

                DrawProperty(subsurfaceColor);
                DrawProperty(subsurfaceColorSaturate);
                DrawProperty(thicknessScale);
                DrawProperty(roughnessScale);
                DrawProperty(reflectanceScale);
                DrawProperty(transmissionNdotL);

                if (!IsGrassShader(material))
                {
                    BurtShaderGUIUtility.DrawSubHeader("Foliage Tint");
                    DrawFoliageTintMode(material);
                    int tintMode = GetFoliageTintModeValue(material);
                    if (tintMode > 0)
                    {
                        DrawTexture(TintPaletteLabel, tintPalette);
                        DrawTexture(LocalTintPaletteLabel, localTintPalette);
                        if (tintMode == 1)
                        {
                            DrawProperty(tintValue);
                        }
                    }

                    DrawProperty(tintScale);
                    if (tintMode == 0)
                    {
                        DrawProperty(localTintColor);
                    }

                    DrawProperty(tintAOHeightRatio);
                    DrawProperty(tintAORemap);
                    DrawProperty(tintHeightContrast);
                    DrawProperty(vertexAORemap);
                    DrawProperty(treeHeight);
                    BurtShaderGUIUtility.DrawSubHeader("Wind");
                    DrawProperty(maxBendAngle);
                    DrawProperty(swayIntensity);
                    DrawProperty(flutterTipFrequency);
                    DrawProperty(flutterTipIntensity);
                    DrawProperty(bendMaskPow);
                    DrawProperty(toTrunkMaskPow);
                }
            }
            else
            {
                BurtShaderGUIUtility.DrawSubHeader("Legacy Foliage");
                DrawProperty(foliageTransmissionColor);
                DrawProperty(foliageTransmissionWeight);
                DrawProperty(foliageThickness);
                DrawProperty(foliageBackLight);
                DrawProperty(foliageSubsurfaceColorSaturate);
            }

            if (IsGrassShader(material))
            {
                BurtShaderGUIUtility.DrawSubHeader("Grass Surface");
                DrawProperty(baseColorTip);
                DrawProperty(tipMaskPow);
                DrawProperty(roughness);
                DrawProperty(heightAO);
                DrawProperty(heightAOFallOff);
                DrawProperty(tlNormalWeight);
                DrawProperty(ssShadowIntensity);
                DrawProperty(ssShadowDistance);
                DrawProperty(groundFadeIntensity);

                BurtShaderGUIUtility.DrawSubHeader("Grass Variation");
                DrawTexture(NoiseMapLabel, noiseMap);
                DrawProperty(variationIntensity01);
                DrawProperty(variation01Height);
                DrawProperty(variation01);
                DrawProperty(variationIntensity02);
                DrawProperty(variation02Height);
                DrawProperty(variation02);

                BurtShaderGUIUtility.DrawSubHeader("Grass SSS");
                DrawProperty(sssIntensity);
                DrawProperty(fresnelIntensity);
                DrawProperty(fresnelExp);
                DrawProperty(grassSpecular);
                DrawProperty(reflectance);
            }

            EditorGUILayout.HelpBox("Foliage uses Mask B as thickness and Mask A as roughness. Deferred GBuffer keeps transmission color, weight, thickness, NdotL, screen-space shadow intensity, and specular scale for the lighting pass. Back Light is legacy material data only.", MessageType.None);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawTrunkInputs(Material material)
        {
            if (!IsTrunkShader(material))
            {
                return;
            }

            if (!BurtShaderGUIUtility.BeginSection(TrunkLabel, ref showTrunkInputs))
            {
                return;
            }

            BurtShaderGUIUtility.DrawSubHeader("Surface");
            DrawProperty(grassSpecular);
            DrawProperty(vertexAORemap);
            BurtShaderGUIUtility.DrawChannelHint("XRender EV_Trunk uses non-metallic DefaultLit, Mask A as roughness, and min(Mask G, Vertex A remap) for AO.");

            BurtShaderGUIUtility.DrawSubHeader("Wind");
            DrawProperty(treeHeight);
            DrawProperty(maxBendAngle);
            DrawProperty(swayIntensity);
            DrawProperty(bendMaskPow);
            DrawProperty(toTrunkMaskPow);

            BurtShaderGUIUtility.DrawSubHeader("Terrain Blend");
            DrawProperty(terrainBlendTerrainTog);
            using (new EditorGUI.DisabledScope(terrainBlendTerrainTog != null && terrainBlendTerrainTog.floatValue < 0.5f))
            {
                DrawProperty(terrainBlendBlendHeight);
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
            EditorGUILayout.HelpBox("Clear Coat writes stencil/model 2, keeps base metallic in GBuffer1.b, stores coat normal / mask / roughness in GBuffer3, and stores base tangent / anisotropy in GBuffer4.", MessageType.None);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawSubsurfaceInputs(Material material)
        {
            if (!IsSubsurfaceShader(material))
            {
                return;
            }

            if (!BurtShaderGUIUtility.BeginSection(SubsurfaceLabel, ref showSubsurfaceInputs))
            {
                return;
            }

            DrawSubsurfaceScatteringMode();
            bool is3SMode = IsSubsurface3SMode();
            if (is3SMode)
            {
                DrawProperty(subsurface3SCurvatureScale);
                DrawProperty(subsurface3SCurvatureBias);
            }
            else
            {
                DrawTexture(SubsurfaceThicknessMapLabel, subsurfaceThicknessMap);
                DrawProperty(subsurfaceThickness);
            }

            if (!is3SMode)
            {
                DrawProperty(subsurfacePower);
                DrawProperty(subsurfaceDistortion);
                DrawProperty(subsurfaceAmbient);
            }

            DrawSubsurfaceProfilePicker();
            EditorGUILayout.HelpBox("Materials pick a BurtSubsurfaceProfile asset. BurtRP resolves it to the pipeline profile slots used by deferred SSS and profile-driven skin specular.", MessageType.None);
            BurtShaderGUIUtility.EndSection();
        }

        private void DrawSubsurfaceScatteringMode()
        {
            if (subsurfaceScatteringMode == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = subsurfaceScatteringMode.hasMixedValue;
            var mode = EditorGUILayout.Popup(SubsurfaceScatteringModeLabel, Mathf.Clamp(Mathf.RoundToInt(subsurfaceScatteringMode.floatValue), 0, SubsurfaceScatteringModeNames.Length - 1), SubsurfaceScatteringModeNames);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(SubsurfaceScatteringModeLabel.text);
                subsurfaceScatteringMode.floatValue = mode;
                foreach (Object target in materialEditor.targets)
                {
                    ClampSubsurfaceScatteringMode(target as Material);
                }
            }
        }

        private bool IsSubsurface3SMode()
        {
            return subsurfaceScatteringMode != null &&
                !subsurfaceScatteringMode.hasMixedValue &&
                Mathf.RoundToInt(subsurfaceScatteringMode.floatValue) == 2;
        }

        private static void ClampSubsurfaceScatteringMode(Material material)
        {
            if (material == null || !material.HasProperty("_SubsurfaceScatteringMode"))
            {
                return;
            }

            material.SetFloat("_SubsurfaceScatteringMode", Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_SubsurfaceScatteringMode")), 0, SubsurfaceScatteringModeNames.Length - 1));
        }

        private void DrawSubsurfaceProfilePicker()
        {
            if (subsurfaceProfileIndex == null)
            {
                return;
            }

            var pipelineAsset = GetActiveBurtAsset();
            if (pipelineAsset == null)
            {
                materialEditor.ShaderProperty(subsurfaceProfileIndex, SubsurfaceProfileIndexLabel);
                EditorGUILayout.HelpBox("No active BurtRenderPipelineAsset was found, so the material shows the raw profile slot.", MessageType.None);
                return;
            }

            var currentIndex = Mathf.Clamp(Mathf.RoundToInt(subsurfaceProfileIndex.floatValue), 0, BurtSubsurfaceProfilePalette.MaxProfiles - 1);
            var material = materialEditor.target as Material;
            var currentProfile = subsurfaceProfileIndex.hasMixedValue ? null : ResolveMaterialSubsurfaceProfile(material, pipelineAsset, currentIndex);
            var profileCanResolve = SyncMaterialSubsurfaceProfileSlot(material, pipelineAsset, currentProfile);

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = subsurfaceProfileIndex.hasMixedValue;
            var selectedProfile = (BurtSubsurfaceProfile)EditorGUILayout.ObjectField(SubsurfaceProfileLabel, currentProfile, typeof(BurtSubsurfaceProfile), false);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(SubsurfaceProfileLabel.text);
                Undo.RecordObject(pipelineAsset, "Register Burt SSS Profile");
                var resolvedIndex = pipelineAsset.EnsureScreenSpaceSubsurfaceProfileSlot(selectedProfile);
                if (selectedProfile != null && resolvedIndex == 0 && selectedProfile != pipelineAsset.ScreenSpaceSubsurfaceProfile)
                {
                    EditorUtility.DisplayDialog("BurtRP Subsurface Profile", "The active pipeline profile list is full. Remove or replace a slot in the BurtRenderPipelineAsset before assigning another profile.", "OK");
                    return;
                }

                subsurfaceProfileIndex.floatValue = resolvedIndex;
                foreach (Object target in materialEditor.targets)
                {
                    if (target is Material targetMaterial && targetMaterial.HasProperty("_SubsurfaceProfileIndex"))
                    {
                        Undo.RecordObject(targetMaterial, SubsurfaceProfileLabel.text);
                        targetMaterial.SetFloat("_SubsurfaceProfileIndex", resolvedIndex);
                        StoreMaterialSubsurfaceProfileGuid(targetMaterial, resolvedIndex == 0 ? null : selectedProfile);
                        EditorUtility.SetDirty(targetMaterial);
                    }
                }

                EditorUtility.SetDirty(pipelineAsset);
            }

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.IntField(SubsurfaceProfileIndexLabel, Mathf.Clamp(Mathf.RoundToInt(subsurfaceProfileIndex.floatValue), 0, BurtSubsurfaceProfilePalette.MaxProfiles - 1));
            }

            if (!profileCanResolve && currentProfile != null)
            {
                EditorGUILayout.HelpBox("This material keeps a stored profile file, but it is not resolved to a slot in the active pipeline yet.", MessageType.Warning);
                if (GUILayout.Button("Register Profile In Active Pipeline"))
                {
                    RegisterSubsurfaceProfileForCurrentMaterial(material, pipelineAsset, currentProfile);
                }
            }
        }

        private static BurtSubsurfaceProfile ResolveMaterialSubsurfaceProfile(Material material, BurtRenderPipelineAsset pipelineAsset, int profileIndex)
        {
            var storedProfile = LoadStoredMaterialSubsurfaceProfile(material);
            return storedProfile != null ? storedProfile : pipelineAsset.GetScreenSpaceSubsurfaceProfileAsset(profileIndex);
        }

        private static BurtSubsurfaceProfile LoadStoredMaterialSubsurfaceProfile(Material material)
        {
            if (material == null)
            {
                return null;
            }

            var guid = material.GetTag(SubsurfaceProfileGuidTag, false, string.Empty);
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            var path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<BurtSubsurfaceProfile>(path);
        }

        private static void StoreMaterialSubsurfaceProfileGuid(Material material, BurtSubsurfaceProfile profile)
        {
            if (material == null)
            {
                return;
            }

            var path = profile != null ? AssetDatabase.GetAssetPath(profile) : string.Empty;
            material.SetOverrideTag(SubsurfaceProfileGuidTag, string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path));
        }

        private static bool SyncMaterialSubsurfaceProfileSlot(Material material, BurtRenderPipelineAsset pipelineAsset, BurtSubsurfaceProfile profile)
        {
            if (material == null || pipelineAsset == null || profile == null || !material.HasProperty("_SubsurfaceProfileIndex"))
            {
                return true;
            }

            var resolvedIndex = pipelineAsset.GetScreenSpaceSubsurfaceProfileIndex(profile);
            if (resolvedIndex == 0 && profile != pipelineAsset.ScreenSpaceSubsurfaceProfile)
            {
                return false;
            }

            if (Mathf.RoundToInt(material.GetFloat("_SubsurfaceProfileIndex")) != resolvedIndex)
            {
                material.SetFloat("_SubsurfaceProfileIndex", resolvedIndex);
                EditorUtility.SetDirty(material);
            }

            return true;
        }

        private static void RegisterSubsurfaceProfileForCurrentMaterial(Material material, BurtRenderPipelineAsset pipelineAsset, BurtSubsurfaceProfile profile)
        {
            if (material == null || pipelineAsset == null || profile == null || !material.HasProperty("_SubsurfaceProfileIndex"))
            {
                return;
            }

            Undo.RecordObject(pipelineAsset, "Register Burt SSS Profile");
            Undo.RecordObject(material, SubsurfaceProfileLabel.text);
            var resolvedIndex = pipelineAsset.EnsureScreenSpaceSubsurfaceProfileSlot(profile);
            if (resolvedIndex == 0 && profile != pipelineAsset.ScreenSpaceSubsurfaceProfile)
            {
                EditorUtility.DisplayDialog("BurtRP Subsurface Profile", "The active pipeline profile list is full. Remove or replace a slot in the BurtRenderPipelineAsset before assigning another profile.", "OK");
                return;
            }

            material.SetFloat("_SubsurfaceProfileIndex", resolvedIndex);
            StoreMaterialSubsurfaceProfileGuid(material, resolvedIndex == 0 ? null : profile);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(pipelineAsset);
        }

        private void DrawNormalInputs(Material material)
        {
            if (!BurtShaderGUIUtility.BeginSection(NormalLabel, ref showNormalInputs))
            {
                return;
            }

            bool usesTreeFoliage = IsFoliageShader(material) && !IsGrassShader(material);
            DrawTextureWithExtra(usesTreeFoliage ? FoliageNSRMapLabel : NormalMapLabel, normalMap, normalScale);
            if (usesTreeFoliage && normalMap != null)
            {
                BurtShaderGUIUtility.DrawChannelHint("Channels: R/G normal | B thickness | A roughness. Use the XRender packed texture, not a Unity normal-map import.");
            }

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

        private void DrawFoliageTintMode(Material material)
        {
            MaterialProperty tintModeProperty = foliageTintMode ?? foliageTintModeLegacy;
            if (tintModeProperty == null)
            {
                return;
            }

            EditorGUI.showMixedValue = tintModeProperty.hasMixedValue;
            int tintMode = GetFoliageTintModeValue(material);
            EditorGUI.BeginChangeCheck();
            int newTintMode = EditorGUILayout.Popup(FoliageTintModeLabel, tintMode, FoliageTintModeNames);
            EditorGUI.showMixedValue = false;

            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            materialEditor.RegisterPropertyChangeUndo(FoliageTintModeLabel.text);
            if (foliageTintMode != null)
            {
                foliageTintMode.floatValue = newTintMode;
            }

            if (foliageTintModeLegacy != null)
            {
                foliageTintModeLegacy.floatValue = newTintMode;
            }

            foreach (Object target in materialEditor.targets)
            {
                Material targetMaterial = target as Material;
                if (targetMaterial == null)
                {
                    continue;
                }

                bool changed = SetMaterialFloatIfDifferent(targetMaterial, "_CustomEnum", newTintMode);
                changed |= SetMaterialFloatIfDifferent(targetMaterial, "_FoliageTintMode", newTintMode);
                if (changed)
                {
                    EditorUtility.SetDirty(targetMaterial);
                }
            }
        }

        private int GetFoliageTintModeValue(Material material)
        {
            if (material != null && material.HasProperty("_CustomEnum"))
            {
                return Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_CustomEnum")), 0, FoliageTintModeNames.Length - 1);
            }

            if (material != null && material.HasProperty("_FoliageTintMode"))
            {
                return Mathf.Clamp(Mathf.RoundToInt(material.GetFloat("_FoliageTintMode")), 0, FoliageTintModeNames.Length - 1);
            }

            MaterialProperty tintModeProperty = foliageTintMode ?? foliageTintModeLegacy;
            if (tintModeProperty != null && !tintModeProperty.hasMixedValue)
            {
                return Mathf.Clamp(Mathf.RoundToInt(tintModeProperty.floatValue), 0, FoliageTintModeNames.Length - 1);
            }

            return 0;
        }

        private void DrawProperty(MaterialProperty property)
        {
            BurtShaderGUIUtility.DrawProperty(materialEditor, property);
        }

        private void DrawFoliageUseBakedNormalsProperty(Material material)
        {
            if (foliageUseBakedNormals != null)
            {
                EditorGUI.BeginChangeCheck();
                EditorGUI.showMixedValue = foliageUseBakedNormals.hasMixedValue;
                bool useBakedNormals = EditorGUILayout.Toggle(FoliageUseBakedNormalsLabel, foliageUseBakedNormals.floatValue >= 0.5f);
                EditorGUI.showMixedValue = false;
                if (!EditorGUI.EndChangeCheck())
                {
                    return;
                }

                materialEditor.RegisterPropertyChangeUndo(FoliageUseBakedNormalsLabel.text);
                foliageUseBakedNormals.floatValue = useBakedNormals ? 1.0f : 0.0f;
                foreach (Object target in materialEditor.targets)
                {
                    ApplySurfaceOptions(target as Material);
                }

                return;
            }

            if (material == null || !material.HasProperty("_FoliageUseBakedNormals"))
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            bool fallbackUseBakedNormals = EditorGUILayout.Toggle(FoliageUseBakedNormalsLabel, material.GetFloat("_FoliageUseBakedNormals") >= 0.5f);
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            materialEditor.RegisterPropertyChangeUndo(FoliageUseBakedNormalsLabel.text);
            foreach (Object target in materialEditor.targets)
            {
                if (target is not Material targetMaterial || !targetMaterial.HasProperty("_FoliageUseBakedNormals"))
                {
                    continue;
                }

                targetMaterial.SetFloat("_FoliageUseBakedNormals", fallbackUseBakedNormals ? 1.0f : 0.0f);
                ApplySurfaceOptions(targetMaterial);
                EditorUtility.SetDirty(targetMaterial);
            }
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

        private void DrawResponsiveAAProperty()
        {
            if (responsiveAA == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = responsiveAA.hasMixedValue;
            bool responsive = EditorGUILayout.Toggle(ResponsiveAALabel, responsiveAA.floatValue >= 0.5f);
            EditorGUI.showMixedValue = false;
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            materialEditor.RegisterPropertyChangeUndo(ResponsiveAALabel.text);
            responsiveAA.floatValue = responsive ? 1.0f : 0.0f;
            foreach (Object target in materialEditor.targets)
            {
                ApplySurfaceOptions(target as Material);
            }
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

        private static bool UsesFixedOpaqueCutoutSurface(Material material)
        {
            return IsFoliageShader(material) || IsTrunkShader(material);
        }

        private static bool IsAlphaClippedMaterial(Material material)
        {
            return material != null && material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") >= 0.5f;
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

            if (IsSilkShader(material))
            {
                return IsTransparentMaterial(material) ? "PBR Transparent Silk" : "PBR Silk / Deferred Fabric";
            }

            if (IsFabricShader(material))
            {
                return IsTransparentMaterial(material) ? "PBR Transparent Fabric" : "PBR Fabric / Deferred GBuffer";
            }

            if (IsFoliageShader(material))
            {
                return IsTransparentMaterial(material) ? "PBR Transparent Foliage" : "PBR Foliage / Deferred GBuffer";
            }

            if (IsTrunkShader(material))
            {
                return IsTransparentMaterial(material) ? "PBR Transparent Trunk" : "PBR Trunk / DefaultLit GBuffer";
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

        private static bool IsFabricShader(Material material)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name.IndexOf("Fabric", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsSilkShader(Material material)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name.IndexOf("Silk", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsFoliageShader(Material material)
        {
            if (material == null || material.shader == null)
            {
                return false;
            }

            var shaderName = material.shader.name;
            return shaderName.IndexOf("Foliage", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsGrassShader(Material material)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name.IndexOf("Grass", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsTrunkShader(Material material)
        {
            return material != null &&
                material.shader != null &&
                material.shader.name.IndexOf("Trunk", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static BurtRenderPipelineAsset GetActiveBurtAsset()
        {
            var asset = GraphicsSettings.currentRenderPipeline as BurtRenderPipelineAsset;
            return asset != null ? asset : QualitySettings.renderPipeline as BurtRenderPipelineAsset;
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

            bool transparent = IsTransparentMaterial(material) && !UsesFixedOpaqueCutoutSurface(material);
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
            ApplyGBufferStencilState(material);
            ApplyFoliageKeywords(material);
            BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);

            bool alphaClipped = IsAlphaClippedMaterial(material);
            material.SetOverrideTag("RenderType", transparent ? "Transparent" : alphaClipped ? "TransparentCutout" : "Opaque");
            material.SetOverrideTag("Queue", transparent ? "Transparent" : alphaClipped ? "AlphaTest" : string.Empty);
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : alphaClipped ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("BurtDepthOnly", !transparent);
            material.SetShaderPassEnabled("BurtGBuffer", !transparent);
            if (IsSubsurfaceShader(material))
            {
                material.SetShaderPassEnabled("BurtSubsurfaceForward", !transparent);
            }

            material.SetShaderPassEnabled("ShadowCaster", !transparent);
            material.SetShaderPassEnabled("BurtForward", true);
        }

        private static void ApplyGBufferStencilState(Material material)
        {
            if (material == null || !material.HasProperty("_BurtGBufferStencilRef") || !material.HasProperty("_BurtGBufferStencilWriteMask"))
            {
                return;
            }

            int stencilRef = ResolveGBufferStencilModel(material);
            int writeMask = GBufferStencilShadingModelMask;
            if (material.HasProperty("_ResponsiveAA") && material.GetFloat("_ResponsiveAA") >= 0.5f)
            {
                stencilRef |= GBufferStencilResponsiveAABit;
                writeMask |= GBufferStencilResponsiveAABit;
            }

            material.SetFloat("_BurtGBufferStencilRef", stencilRef);
            material.SetFloat("_BurtGBufferStencilWriteMask", writeMask);
        }

        private static void ApplyFoliageKeywords(Material material)
        {
            if (material == null || !IsFoliageShader(material))
            {
                return;
            }

            bool useBakedNormals = material.HasProperty("_FoliageUseBakedNormals") && material.GetFloat("_FoliageUseBakedNormals") >= 0.5f;
            SetKeyword(material, "BURT_FOLIAGE_USE_BAKED_NORMALS", useBakedNormals);
        }

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static int ResolveGBufferStencilModel(Material material)
        {
            if (IsClearCoatShader(material))
            {
                return GBufferStencilClearCoatRef;
            }

            if (IsSubsurfaceShader(material))
            {
                return GBufferStencilSubsurfaceRef;
            }

            if (IsFoliageShader(material))
            {
                return GBufferStencilFoliageRef;
            }

            return IsFabricShader(material) || IsSilkShader(material) ? GBufferStencilFabricRef : GBufferStencilDefaultLitRef;
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
