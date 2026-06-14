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
        private static readonly GUIContent NormalLabel = new GUIContent("Normal");
        private static readonly GUIContent EmissionLabel = new GUIContent("Emission");

        private static readonly GUIContent BaseMapLabel = new GUIContent("Base Map", "Albedo RGB and alpha for clipping or transparent materials.");
        private static readonly GUIContent MaskMapLabel = new GUIContent("Mask Map", "R Metallic, G Occlusion, B Reserved, A Smoothness.");
        private static readonly GUIContent NormalMapLabel = new GUIContent("Normal Map");
        private static readonly GUIContent ClearCoatNormalMapLabel = new GUIContent("Clear Coat Normal Map");
        private static readonly GUIContent EmissionMapLabel = new GUIContent("Emission Map");
        private static readonly GUIContent ShadingModelLabel = new GUIContent("Shading Model");
        private static readonly GUIContent SubsurfaceScatteringModeLabel = new GUIContent("SSS Algorithm", "Choose 5S Burley, 4S Separable screen-space skin scattering, or 3S Preintegrated skin shading.");
        private static readonly GUIContent SubsurfaceProfileLabel = new GUIContent("Subsurface Profile", "Drag a BurtSubsurfaceProfile asset here. The material stores the resolved profile slot for the shader.");
        private static readonly GUIContent SubsurfaceProfileIndexLabel = new GUIContent("Subsurface Profile Index", "Runtime slot used by the shader. 0 is the default profile, 1-7 are the pipeline profile list.");
        private static readonly GUIContent Subsurface3SCurvatureLabel = new GUIContent("3S Curvature", "Controls the preintegrated 3S LUT curvature. Stored as inverse Subsurface Thickness.");
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
        private static readonly string[] SubsurfaceScatteringModeNames = { "5S Burley", "4S Separable", "3S Preintegrated" };
        private const string SubsurfaceProfileGuidTag = "BurtSubsurfaceProfileGuid";
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
        private MaterialProperty anisotropy;
        private MaterialProperty smoothness;
        private MaterialProperty occlusionStrength;
        private MaterialProperty reflectance;
        private MaterialProperty clearCoatMask;
        private MaterialProperty clearCoatRoughness;
        private MaterialProperty clearCoatNormalMap;
        private MaterialProperty clearCoatNormalScale;
        private MaterialProperty subsurfaceStrength;
        private MaterialProperty subsurfaceThickness;
        private MaterialProperty subsurfacePower;
        private MaterialProperty subsurfaceDistortion;
        private MaterialProperty subsurfaceAmbient;
        private MaterialProperty subsurfaceScatteringMode;
        private MaterialProperty subsurfaceProfileIndex;
        private MaterialProperty subsurfaceTint;
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
            DrawPbrInputs(material);
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
            ClampSubsurfaceScatteringMode(material);
            ApplySubsurfaceMaterialDefaults(material);
            ApplySurfaceOptions(material);
        }

        private void CacheProperties()
        {
            baseMap = Find("_BaseMap");
            baseColor = Find("_BaseColor");
            maskMap = Find("_MaskMap");
            metallic = Find("_Metallic");
            anisotropy = Find("_Anisotropy");
            smoothness = Find("_Smoothness");
            occlusionStrength = Find("_OcclusionStrength");
            reflectance = Find("_Reflectance");
            clearCoatMask = Find("_ClearCoatMask");
            clearCoatRoughness = Find("_ClearCoatRoughness");
            clearCoatNormalMap = Find("_ClearCoatNormalMap");
            clearCoatNormalScale = Find("_ClearCoatNormalScale");
            subsurfaceStrength = Find("_SubsurfaceStrength");
            subsurfaceThickness = Find("_SubsurfaceThickness");
            subsurfacePower = Find("_SubsurfacePower");
            subsurfaceDistortion = Find("_SubsurfaceDistortion");
            subsurfaceAmbient = Find("_SubsurfaceAmbient");
            subsurfaceScatteringMode = Find("_SubsurfaceScatteringMode");
            subsurfaceProfileIndex = Find("_SubsurfaceProfileIndex");
            subsurfaceTint = Find("_SubsurfaceTint");
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
            DrawAlphaClipProperty();

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

        private void DrawPbrInputs(Material material)
        {
            if (!BurtShaderGUIUtility.BeginSection(PbrMaskLabel, ref showPbrMaskInputs))
            {
                return;
            }

            DrawTexture(MaskMapLabel, maskMap);
            if (maskMap != null)
            {
                BurtShaderGUIUtility.DrawChannelHint(IsSubsurfaceShader(material)
                    ? "Channels: G Occlusion | A Smoothness. R Metallic is ignored by Subsurface skin."
                    : "Channels: R Metallic | G Occlusion | B Reserved | A Smoothness");
            }

            if (!IsSubsurfaceShader(material))
            {
                BurtShaderGUIUtility.DrawSubHeader("Lit");
                DrawProperty(metallic);
                DrawProperty(anisotropy);
            }

            BurtShaderGUIUtility.DrawSubHeader("Shared");
            DrawProperty(smoothness);
            DrawProperty(occlusionStrength);
            if (!IsSubsurfaceShader(material))
            {
                DrawProperty(reflectance);
            }

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
            EditorGUILayout.HelpBox("Clear Coat writes stencil/model 2, keeps base metallic in GBuffer1.b, stores coat normal / mask / roughness in GBuffer3, and stores base tangent / anisotropy in GBuffer4.", MessageType.None);
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

            DrawSubsurfaceScatteringMode();
            bool is3SMode = IsSubsurface3SMode();
            DrawProperty(subsurfaceStrength);
            if (is3SMode)
            {
                DrawSubsurface3SCurvature();
            }
            else
            {
                DrawProperty(subsurfaceThickness);
            }

            if (!is3SMode)
            {
                DrawProperty(subsurfacePower);
                DrawProperty(subsurfaceDistortion);
                DrawProperty(subsurfaceAmbient);
            }

            DrawSubsurfaceProfilePicker();
            if (!is3SMode)
            {
                DrawProperty(subsurfaceTint);
            }
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

        private void DrawSubsurface3SCurvature()
        {
            if (subsurfaceThickness == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = subsurfaceThickness.hasMixedValue;
            float curvature = 1.0f - Mathf.Clamp01(subsurfaceThickness.floatValue);
            curvature = EditorGUILayout.Slider(Subsurface3SCurvatureLabel, curvature, 0.0f, 1.0f);
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                materialEditor.RegisterPropertyChangeUndo(Subsurface3SCurvatureLabel.text);
                subsurfaceThickness.floatValue = 1.0f - Mathf.Clamp01(curvature);
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

        private static void ApplySubsurfaceMaterialDefaults(Material material)
        {
            if (!IsSubsurfaceShader(material))
            {
                return;
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0.0f);
            }

            if (material.HasProperty("_Anisotropy"))
            {
                material.SetFloat("_Anisotropy", 0.0f);
            }

            if (material.HasProperty("_Reflectance"))
            {
                material.SetFloat("_Reflectance", 0.42f);
            }
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
            BurtShaderGUIUtility.ApplyAlphaClipKeyword(material);

            material.SetOverrideTag("RenderType", transparent ? "Transparent" : "Opaque");
            material.SetOverrideTag("Queue", transparent ? "Transparent" : string.Empty);
            material.renderQueue = transparent ? (int)RenderQueue.Transparent : (int)RenderQueue.Geometry;
            material.SetShaderPassEnabled("BurtDepthOnly", !transparent);
            material.SetShaderPassEnabled("BurtGBuffer", !transparent);
            material.SetShaderPassEnabled("BurtSubsurfaceForward", !transparent && material.shader != null && material.shader.name == "BurtRP/Subsurface");
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
