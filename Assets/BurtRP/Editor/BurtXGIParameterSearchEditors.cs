using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtXGIParameterSearchUtility
    {
        private static readonly Dictionary<string, string[]> SearchAliases =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "enabled", new[] { "Enable GI", "Global Illumination", "开启", "启用", "全局光照" } },
                { "quality", new[] { "GI Quality", "质量", "Custom", "Ultra" } },
                { "resolution", new[] { "GI Resolution", "Full Resolution", "分辨率", "全分辨率" } },
                { "screenProbeLite", new[] { "Screen Probe", "ScreenProbe", "屏幕探针" } },
                { "screenProbeApplyStrength", new[] { "Screen Probe Apply", "Apply Strength", "应用强度" } },
                { "finalGather", new[] { "XGI Final Gather", "Final Gather", "最终采集" } },
                { "xgiUseProbeFirst", new[] { "Use Probe First", "Probe First", "优先探针" } },
                { "useProbeFirst", new[] { "XGI Use Probe First", "Probe First", "优先探针" } },
                { "screenProbeTraceSources", new[] { "Trace Sources", "Trace Source", "All Trace Sources", "追踪源" } },
                { "screenProbeTraceCompact", new[] { "Trace Compact", "Compact Trace", "紧凑追踪" } },
                { "screenProbeTraceUseWorldRadianceClipMap", new[] { "Trace Use World Radiance Clip Map", "World Radiance ClipMap", "世界辐射缓存" } },
                { "screenProbeTraceHierarchically", new[] { "Trace Hierarchically", "Hierarchical Trace", "分层追踪" } },
                { "screenProbeTraceHardwareRay", new[] { "Screen Probe Hardware Ray Tracing", "Screen Probe DXR", "硬件光追" } },
                { "screenProbeImportanceSampling", new[] { "Importance Sampling", "重要性采样" } },
                { "screenProbeImportanceSampleLighting", new[] { "Importance Sample Lighting", "Light PDF", "灯光重要性采样" } },
                { "screenProbeImportanceSampleProbeRadianceHistory", new[] { "Importance Sample Probe History", "Probe Radiance History", "历史辐射重要性采样" } },
                { "screenProbeTemporalFilter", new[] { "Temporal Filter", "时域过滤" } },
                { "screenProbeTemporalReprojection", new[] { "Temporal Reprojection", "时域重投影" } },
                { "screenProbeSpatialFilter", new[] { "Spatial Filter", "空域过滤" } },
                { "screenProbeFixupBorders", new[] { "Fixup Borders", "Border Fixup", "边界修复" } },
                { "screenProbeIrradianceFormat", new[] { "Irradiance Format", "SH3", "Octahedral", "辐照度格式" } },
                { "screenProbeIntegrateType", new[] { "Integrate Type", "Tile Classification", "Simple Integrate", "积分类型" } },
                { "screenProbeRadianceCacheType", new[] { "Radiance Cache Type", "ClipMap", "HashGrid", "辐射缓存类型" } },
                { "radianceCacheType", new[] { "Screen Probe Radiance Cache Type", "ClipMap", "HashGrid", "辐射缓存类型" } },
                { "screenProbeRadianceCacheForceFullUpdate", new[] { "Radiance Cache Force Full Update", "Force Full Update", "强制全量更新" } },
                { "radianceCacheForceFullUpdate", new[] { "Screen Probe Radiance Cache Force Full Update", "Force Full Update", "强制全量更新" } },
                { "screenProbeRadianceCacheTraceHardwareRay", new[] { "Radiance Cache Trace Hardware Ray", "Radiance Cache DXR", "缓存硬件光追" } },
                { "radianceCacheTraceHardwareRay", new[] { "Screen Probe Radiance Cache Trace Hardware Ray", "Radiance Cache DXR", "缓存硬件光追" } },
                { "screenProbeRadianceCacheCalculateIrradiance", new[] { "Radiance Cache Calculate Irradiance", "Calculate Irradiance", "计算辐照度" } },
                { "radianceCacheCalculateIrradiance", new[] { "Screen Probe Radiance Cache Calculate Irradiance", "Calculate Irradiance", "计算辐照度" } },
                { "screenProbeRadianceCacheEnableMultiBounceFromRadianceCache", new[] { "Enable Multi Bounce From Radiance Cache", "Radiance Cache Multi Bounce", "缓存多次反弹" } },
                { "radianceCacheEnableMultiBounceFromRadianceCache", new[] { "Screen Probe Enable Multi Bounce From Radiance Cache", "Radiance Cache Multi Bounce", "缓存多次反弹" } },
                { "screenProbeRadianceCacheFilterProbes", new[] { "Radiance Cache Filter Probes", "Filter Probes", "探针过滤" } },
                { "radianceCacheFilterProbes", new[] { "Screen Probe Radiance Cache Filter Probes", "Filter Probes", "探针过滤" } },
                { "screenProbeRadianceCacheClipMapCount", new[] { "Radiance Cache Clip Map Count", "ClipMap Count", "级联数量" } },
                { "radianceCacheClipMapCount", new[] { "Screen Probe Radiance Cache Clip Map Count", "ClipMap Count", "级联数量" } },
                { "screenProbeRadianceCacheClipMapResolution", new[] { "Radiance Cache Clip Map Resolution", "ClipMap Resolution", "缓存分辨率" } },
                { "radianceCacheClipMapResolution", new[] { "Screen Probe Radiance Cache Clip Map Resolution", "ClipMap Resolution", "缓存分辨率" } },
                { "screenProbeRadianceCacheClipMapWorldExtent", new[] { "Radiance Cache Clip Map World Extent", "ClipMap World Extent", "世界范围" } },
                { "radianceCacheClipMapWorldExtent", new[] { "Screen Probe Radiance Cache Clip Map World Extent", "ClipMap World Extent", "世界范围" } },
                { "screenProbeRadianceCacheNumProbesToTraceBudget", new[] { "Radiance Cache Num Probes To Trace Budget", "Probe Trace Budget", "探针追踪预算" } },
                { "radianceCacheNumProbesToTraceBudget", new[] { "Screen Probe Radiance Cache Num Probes To Trace Budget", "Probe Trace Budget", "探针追踪预算" } },
                { "enableBackfaceDiffuse", new[] { "Backface Diffuse", "Back Face Diffuse", "背面漫反射" } },
                { "enableRoughSpecular", new[] { "Rough Specular", "Glossy GI", "粗糙镜面反射" } },
                { "useTranslucencyVolume", new[] { "Translucency Volume", "Translucent GI", "半透明体积" } },
                { "shortRangeAO", new[] { "Short Range AO", "Near Field AO", "近场遮蔽" } },
                { "sceneVoxelAlwaysUpdate", new[] { "Scene Voxel Always Update", "Always Update Voxel", "体素始终更新" } },
                { "sceneVoxelDrawGrass", new[] { "Scene Voxel Draw Grass", "Voxel Grass", "体素草" } },
                { "sceneVoxelMultiBounce", new[] { "Scene Voxel Multi Bounce", "Voxel Multi Bounce", "体素多次反弹" } },
                { "sceneVoxelEnableSkyVisibility", new[] { "Scene Voxel Sky Visibility", "Enable Sky Visibility", "天空可见性" } },
                { "overrideConfig", new[] { "Override Config", "Override Volume", "覆盖配置" } },
                { "enable", new[] { "Enable XGI Light", "XGI Enabled", "开启", "启用" } }
            };

        public static string[] GetAliases(string fieldName)
        {
            return SearchAliases.TryGetValue(fieldName, out var aliases) ? aliases : Array.Empty<string>();
        }

        public static string GetDisplayName(string fieldName)
        {
            return ObjectNames.NicifyVariableName(fieldName)
                .Replace("Xgi", "XGI")
                .Replace(" Gi ", " GI ")
                .Replace("Ao", "AO")
                .Replace("Dxr", "DXR")
                .Replace("Pdf", "PDF")
                .Replace("Sh3", "SH3")
                .Replace("Clip Map", "ClipMap");
        }

        public static bool Matches(string searchText, params string[] searchableValues)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return true;
            }

            var haystack = string.Join(" ", searchableValues.Where(value => !string.IsNullOrEmpty(value)));
            var tokens = searchText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return tokens.All(token => haystack.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        public static string BuildTooltip(string fieldName, IEnumerable<string> aliases)
        {
            var aliasText = string.Join(", ", aliases);
            return string.IsNullOrEmpty(aliasText)
                ? "Code field: " + fieldName
                : "Code field: " + fieldName + "\nSearch aliases: " + aliasText;
        }

        public static void DrawSearchBar(SearchField searchField, ref string searchText, int matchCount, int totalCount)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                searchText = searchField.OnToolbarGUI(searchText, GUILayout.MinWidth(100f));
                GUILayout.Label(matchCount + "/" + totalCount, EditorStyles.miniLabel, GUILayout.Width(52f));
            }
        }
    }

    [CustomEditor(typeof(ScreenSpaceGlobalIlluminationVolumeComponent))]
    internal sealed class BurtScreenSpaceGlobalIlluminationVolumeComponentEditor : VolumeComponentEditor
    {
        private sealed class ParameterEntry
        {
            public string FieldName;
            public string DisplayName;
            public string GroupName;
            public string[] Aliases;
            public SerializedDataParameter Parameter;
            public GUIContent Label;
        }

        private static readonly Dictionary<string, string> GroupStarts =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "enabled", "BurtGI — Base" },
                { "blur", "Spatial Denoise" },
                { "leakGuardStrength", "Leak Guard" },
                { "temporalAccumulation", "Temporal Denoise" },
                { "screenProbeLite", "Screen Probe" },
                { "screenProbeRadianceCacheType", "Radiance Cache" },
                { "finalGather", "XGI Final Gather" },
                { "enableBackfaceDiffuse", "XGI Indirect Channels" },
                { "useTranslucencyVolume", "Translucency Volume" },
                { "sceneVoxelClipMapFirstWorldExtent", "Scene Voxel" },
                { "localSkyProbeCameraDistance", "Local Sky / Skylight" },
                { "shortRangeAO", "Short Range AO" }
            };

        private readonly List<ParameterEntry> entries = new List<ParameterEntry>();
        private SearchField searchField;
        private string searchText = string.Empty;

        public override void OnEnable()
        {
            entries.Clear();
            searchField = new SearchField();

            var currentGroup = "BurtGI — Base";
            var fields = typeof(ScreenSpaceGlobalIlluminationVolumeComponent)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(field => field.FieldType.IsSubclassOf(typeof(VolumeParameter)))
                .OrderBy(field => field.MetadataToken);

            foreach (var field in fields)
            {
                if (GroupStarts.TryGetValue(field.Name, out var nextGroup))
                {
                    currentGroup = nextGroup;
                }

                var property = serializedObject.FindProperty(field.Name);
                if (property == null)
                {
                    continue;
                }

                var displayName = BurtXGIParameterSearchUtility.GetDisplayName(field.Name);
                var aliases = BurtXGIParameterSearchUtility.GetAliases(field.Name);
                entries.Add(new ParameterEntry
                {
                    FieldName = field.Name,
                    DisplayName = displayName,
                    GroupName = currentGroup,
                    Aliases = aliases,
                    Parameter = Unpack(property),
                    Label = new GUIContent(displayName, BurtXGIParameterSearchUtility.BuildTooltip(field.Name, aliases))
                });
            }
        }

        public override void OnInspectorGUI()
        {
            var matches = entries.Where(MatchesSearch).ToList();
            BurtXGIParameterSearchUtility.DrawSearchBar(searchField, ref searchText, matches.Count, entries.Count);

            EditorGUILayout.HelpBox(
                "Search accepts Inspector names, code field names, recommended-setting aliases, and common Chinese terms. " +
                "Use Quality = Custom when manually enabling every trace source; Ultra applies its own reduced source preset.",
                MessageType.Info);

            if (matches.Count == 0)
            {
                EditorGUILayout.HelpBox("No XGI parameter matches \"" + searchText + "\".", MessageType.Warning);
                return;
            }

            string lastGroup = null;
            foreach (var entry in matches)
            {
                if (!string.Equals(lastGroup, entry.GroupName, StringComparison.Ordinal))
                {
                    if (lastGroup != null)
                    {
                        EditorGUILayout.Space(4f);
                    }

                    EditorGUILayout.LabelField(entry.GroupName, EditorStyles.boldLabel);
                    lastGroup = entry.GroupName;
                }

                PropertyField(entry.Parameter, entry.Label);
            }
        }

        private bool MatchesSearch(ParameterEntry entry)
        {
            return BurtXGIParameterSearchUtility.Matches(
                searchText,
                entry.DisplayName,
                entry.FieldName,
                entry.GroupName,
                string.Join(" ", entry.Aliases));
        }
    }

    [CustomEditor(typeof(BurtXGILightComponent))]
    [CanEditMultipleObjects]
    internal sealed class BurtXGILightComponentEditor : UnityEditor.Editor
    {
        private sealed class PropertyEntry
        {
            public string FieldName;
            public string DisplayName;
            public string GroupName;
            public string[] Aliases;
            public GUIContent Label;
        }

        private readonly List<PropertyEntry> entries = new List<PropertyEntry>();
        private SearchField searchField;
        private string searchText = string.Empty;
        private SerializedProperty scriptProperty;

        private void OnEnable()
        {
            entries.Clear();
            searchField = new SearchField();
            scriptProperty = serializedObject.FindProperty("m_Script");

            var currentGroup = "Base";
            var fields = typeof(BurtXGILightComponent)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(IsVisibleSerializedField)
                .OrderBy(field => field.MetadataToken);

            foreach (var field in fields)
            {
                var header = field.GetCustomAttribute<HeaderAttribute>();
                if (header != null && !string.IsNullOrEmpty(header.header))
                {
                    currentGroup = header.header;
                }

                if (serializedObject.FindProperty(field.Name) == null)
                {
                    continue;
                }

                var displayName = BurtXGIParameterSearchUtility.GetDisplayName(field.Name);
                var aliases = BurtXGIParameterSearchUtility.GetAliases(field.Name);
                entries.Add(new PropertyEntry
                {
                    FieldName = field.Name,
                    DisplayName = displayName,
                    GroupName = currentGroup,
                    Aliases = aliases,
                    Label = new GUIContent(displayName, BurtXGIParameterSearchUtility.BuildTooltip(field.Name, aliases))
                });
            }
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (scriptProperty != null)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(scriptProperty);
                }
            }

            var matches = entries.Where(MatchesSearch).ToList();
            BurtXGIParameterSearchUtility.DrawSearchBar(searchField, ref searchText, matches.Count, entries.Count);
            EditorGUILayout.HelpBox(
                "This component can override or constrain the GI Volume. Search accepts Inspector names, code field names, " +
                "recommended-setting aliases, and common Chinese terms.",
                MessageType.Info);

            if (matches.Count == 0)
            {
                EditorGUILayout.HelpBox("No XGI Light parameter matches \"" + searchText + "\".", MessageType.Warning);
            }
            else
            {
                foreach (var entry in matches)
                {
                    var property = serializedObject.FindProperty(entry.FieldName);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, entry.Label, true);
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        private bool MatchesSearch(PropertyEntry entry)
        {
            return BurtXGIParameterSearchUtility.Matches(
                searchText,
                entry.DisplayName,
                entry.FieldName,
                entry.GroupName,
                string.Join(" ", entry.Aliases));
        }

        private static bool IsVisibleSerializedField(FieldInfo field)
        {
            if (field.IsStatic ||
                field.IsNotSerialized ||
                field.GetCustomAttribute<HideInInspector>() != null)
            {
                return false;
            }

            return field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
        }
    }
}
