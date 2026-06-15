using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtShaderGUIUtility
    {
        public const string AlphaClipKeyword = "BURT_ALPHA_CLIP";
        public const string EmissionKeyword = "_EMISSION";
        private const string LegacyAlphaClipKeyword = "_ALPHACLIP_ON";
        public static readonly string[] DoubleSidedNormalModeNames = { "None", "Flip", "Mirror" };
        private static GUIStyle sectionHeaderStyle;
        private static GUIStyle sectionBodyStyle;
        private static GUIStyle sectionLineStyle;
        private static GUIStyle channelHintStyle;

        public static bool BeginSection(GUIContent label, ref bool expanded)
        {
            EnsureStyles();

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            Rect headerRect = EditorGUILayout.GetControlRect(false, 22f);
            GUI.Box(headerRect, GUIContent.none, sectionHeaderStyle);

            Rect foldoutRect = new Rect(headerRect.x + 6f, headerRect.y + 2f, headerRect.width - 12f, headerRect.height - 4f);
            expanded = EditorGUI.Foldout(foldoutRect, expanded, label, true, EditorStyles.foldoutHeader);
            if (!expanded)
            {
                EditorGUILayout.EndVertical();
                return false;
            }

            DrawSeparator();
            EditorGUILayout.BeginVertical(sectionBodyStyle);
            return true;
        }

        public static void EndSection()
        {
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
        }

        public static void DrawSubHeader(string title)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        }

        public static void DrawSeparator()
        {
            EnsureStyles();
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            GUI.Box(rect, GUIContent.none, sectionLineStyle);
        }

        public static void DrawChannelHint(string hint)
        {
            EnsureStyles();
            Rect rect = EditorGUILayout.GetControlRect(false, 18f);
            EditorGUI.LabelField(rect, hint, channelHintStyle);
        }

        public static void DrawTextureWithColor(MaterialEditor materialEditor, GUIContent label, MaterialProperty texture, MaterialProperty color)
        {
            if (texture != null && color != null)
            {
                materialEditor.TexturePropertySingleLine(label, texture, color);
                materialEditor.TextureScaleOffsetProperty(texture);
                return;
            }

            DrawTexture(materialEditor, label, texture);
            DrawProperty(materialEditor, color);
        }

        public static void DrawTextureWithExtra(MaterialEditor materialEditor, GUIContent label, MaterialProperty texture, MaterialProperty extra)
        {
            if (texture != null && extra != null)
            {
                materialEditor.TexturePropertySingleLine(label, texture, extra);
                materialEditor.TextureScaleOffsetProperty(texture);
                return;
            }

            DrawTexture(materialEditor, label, texture);
            DrawProperty(materialEditor, extra);
        }

        public static void DrawTexture(MaterialEditor materialEditor, GUIContent label, MaterialProperty texture)
        {
            if (texture == null)
            {
                return;
            }

            materialEditor.TexturePropertySingleLine(label, texture);
            materialEditor.TextureScaleOffsetProperty(texture);
        }

        public static void DrawProperty(MaterialEditor materialEditor, MaterialProperty property)
        {
            if (property == null)
            {
                return;
            }

            materialEditor.ShaderProperty(property, property.displayName);
        }

        public static Vector4 GetDoubleSidedNormalModeConstants(int mode)
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

        public static void ApplyDoubleSidedState(Material material, bool defaultDoubleSided)
        {
            if (material == null)
            {
                return;
            }

            bool doubleSided = defaultDoubleSided;
            if (material.HasProperty("_DoubleSidedEnable"))
            {
                doubleSided = material.GetFloat("_DoubleSidedEnable") >= 0.5f;
                material.SetFloat("_DoubleSidedEnable", doubleSided ? 1.0f : 0.0f);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", doubleSided ? (float)UnityEngine.Rendering.CullMode.Off : (float)UnityEngine.Rendering.CullMode.Back);
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

        public static void UpdateEmissionFlag(Material material)
        {
            if (material == null || !material.HasProperty("_EmissionColor"))
            {
                return;
            }

            Color emission = material.GetColor("_EmissionColor");
            MaterialGlobalIlluminationFlags flags = material.globalIlluminationFlags;
            bool emissionEnabled = emission.maxColorComponent > 0.0f;
            material.globalIlluminationFlags = emissionEnabled
                ? flags & ~MaterialGlobalIlluminationFlags.EmissiveIsBlack
                : flags | MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            if (emissionEnabled)
            {
                material.EnableKeyword(EmissionKeyword);
            }
            else
            {
                material.DisableKeyword(EmissionKeyword);
            }
        }

        public static void ApplyAlphaClipKeyword(Material material)
        {
            if (material == null)
            {
                return;
            }

            bool enabled = material.HasProperty("_AlphaClip") && material.GetFloat("_AlphaClip") >= 0.5f;
            material.DisableKeyword(LegacyAlphaClipKeyword);
            if (enabled)
            {
                material.EnableKeyword(AlphaClipKeyword);
            }
            else
            {
                material.DisableKeyword(AlphaClipKeyword);
            }
        }

        private static void EnsureStyles()
        {
            if (sectionHeaderStyle != null)
            {
                return;
            }

            sectionHeaderStyle = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(6, 6, 2, 2),
                margin = new RectOffset(0, 0, 0, 0)
            };

            sectionBodyStyle = new GUIStyle
            {
                padding = new RectOffset(8, 8, 5, 7)
            };

            sectionLineStyle = new GUIStyle
            {
                normal = { background = Texture2D.grayTexture },
                margin = new RectOffset(0, 0, 2, 4)
            };

            channelHintStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                padding = new RectOffset(2, 2, 2, 2)
            };
        }
    }
}
