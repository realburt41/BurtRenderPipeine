using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtXGILegacyProbeDataDiagnostics
    {
        private const string MenuPath = "BurtRP/XGI/Diagnose Selected Legacy XRender Probe Baked Data";

        [MenuItem(MenuPath, false, 2508)]
        private static void DiagnoseSelectedLegacyXRenderProbeData()
        {
            var selected = Selection.activeObject;
            if (selected == null)
            {
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Probe Data", "Select a baking config asset first.", "OK");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(selected);
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Probe Data", "The selected object is not a project asset.", "OK");
                return;
            }

            var fullPath = Path.GetFullPath(assetPath);
            var lines = File.Exists(fullPath) ? File.ReadAllLines(fullPath) : Array.Empty<string>();
            var report = BuildReport(selected, assetPath, fullPath, lines);
            var hasIssue = report.IndexOf("Missing", StringComparison.OrdinalIgnoreCase) >= 0 ||
                report.IndexOf("Not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                report.IndexOf("No legacy", StringComparison.OrdinalIgnoreCase) >= 0;

            if (hasIssue)
            {
                Debug.LogWarning(report, selected);
            }
            else
            {
                Debug.Log(report, selected);
            }

            EditorUtility.DisplayDialog(
                "BurtRP XGI Legacy Probe Data",
                TrimForDialog(report),
                hasIssue ? "Needs Attention" : "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateDiagnoseSelectedLegacyXRenderProbeData()
        {
            var selected = Selection.activeObject;
            if (selected == null || Application.isPlaying)
            {
                return false;
            }

            var path = AssetDatabase.GetAssetPath(selected);
            return !string.IsNullOrEmpty(path) &&
                string.Equals(Path.GetExtension(path), ".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildReport(UnityEngine.Object selected, string assetPath, string fullPath, string[] lines)
        {
            var builder = new StringBuilder(4096);
            builder.AppendLine("BurtRP XGI Legacy XRender Probe Baked Data Diagnostics");
            builder.AppendLine("Asset: " + assetPath);
            builder.AppendLine("FullPath: " + fullPath);
            builder.AppendLine();

            if (selected is BurtXGIProbeBakingConfig config)
            {
                AppendLoadedConfigReport(builder, config);
                builder.AppendLine();
            }

            if (lines == null || lines.Length == 0)
            {
                builder.AppendLine("YAML: Missing or unreadable asset text.");
                return builder.ToString();
            }

            AppendYamlScalarReport(builder, lines);
            builder.AppendLine();
            AppendStreamingAssetReport(builder, lines, fullPath);
            builder.AppendLine();
            AppendImportReadiness(builder, selected, lines);
            return builder.ToString();
        }

        private static void AppendLoadedConfigReport(StringBuilder builder, BurtXGIProbeBakingConfig config)
        {
            builder.AppendLine("Loaded BRP Config:");
            builder.AppendLine("  SceneGuid: " + NullSafe(config.sceneGuid));
            builder.AppendLine("  Platform: " + config.platform);
            builder.AppendLine("  Cells/Bricks/Probes: " + config.bakedCellCount + "/" + config.bakedBrickCount + "/" + config.bakedProbeCount);
            builder.AppendLine("  Serialized Cells/Chunks: " + config.bakedSerializedCellCount + "/" + config.bakedSerializedChunkCount);
            builder.AppendLine("  Has BRP Baked Asset: " + (config.bakedDataAsset != null));
            builder.AppendLine("  Has Legacy XRender Chunk Layout: " + config.HasLegacyXRenderStreamingLayout);
            if (config.HasLegacyXRenderStreamingLayout)
            {
                builder.AppendLine("  Legacy maxSHChunkCount: " + config.LegacyXRenderMaxSHChunkCount);
                builder.AppendLine("  Legacy support chunks: position=" + config.LegacyXRenderSupportPositionChunkSize +
                    ", offsets=" + config.LegacyXRenderSupportOffsetsChunkSize +
                    ", total=" + config.LegacyXRenderSupportDataChunkSize);
                builder.AppendLine("  Legacy shared chunks: skyL0L1=" + config.LegacyXRenderSharedSkyVisibilityL0L1ChunkSize +
                    ", skyDir=" + config.LegacyXRenderSharedSkyShadingDirectionIndicesChunkSize +
                    ", total=" + config.LegacyXRenderSharedDataChunkSize);
                builder.AppendLine("  Legacy SH chunks: l0=" + config.LegacyXRenderL0ChunkSize +
                    ", l1=" + config.LegacyXRenderL1ChunkSize +
                    ", l2Tex=" + config.LegacyXRenderL2TextureChunkSize);
            }
        }

        private static void AppendYamlScalarReport(StringBuilder builder, string[] lines)
        {
            builder.AppendLine("Legacy YAML Scalars:");
            AppendInt(builder, lines, "chunkSizeInBricks");
            AppendInt(builder, lines, "maxSHChunkCount");
            AppendInt(builder, lines, "supportPositionChunkSize");
            AppendInt(builder, lines, "supportOffsetsChunkSize");
            AppendInt(builder, lines, "supportDataChunkSize");
            AppendInt(builder, lines, "sharedSkyVisibilityL0L1ChunkSize");
            AppendInt(builder, lines, "sharedSkyShadingDirectionIndicesChunkSize");
            AppendInt(builder, lines, "sharedDataChunkSize");
            AppendInt(builder, lines, "l0ChunkSize");
            AppendInt(builder, lines, "l1ChunkSize");
            AppendInt(builder, lines, "l2TextureChunkSize");
            AppendInt(builder, lines, "bakedUseTimeSliceValue");
            AppendInt(builder, lines, "bakedSkyVisibilityValue");
            AppendInt(builder, lines, "bakedSkyShadingDirectionValue");
        }

        private static void AppendStreamingAssetReport(StringBuilder builder, string[] lines, string configFullPath)
        {
            builder.AppendLine("Legacy Streaming Assets:");
            var infos = new List<StreamingAssetInfo>();
            AddStreamingAssetInfos(infos, lines, "cellBricksDataAsset");
            AddStreamingAssetInfos(infos, lines, "cellSupportDataAsset");
            AddStreamingAssetInfos(infos, lines, "cellSharedDataAsset");
            AddStreamingAssetInfos(infos, lines, "cellDataAsset");
            AddStreamingAssetInfos(infos, lines, "cellOptionalDataAsset");

            if (infos.Count == 0)
            {
                builder.AppendLine("  No legacy XGIProbeStreamingAsset fields found in asset text.");
                return;
            }

            for (var index = 0; index < infos.Count; index++)
            {
                var info = infos[index];
                builder.AppendLine("  " + info.label + ":");
                builder.AppendLine("    assetPath: " + NullSafe(info.assetPath));
                builder.AppendLine("    elementSize: " + FormatOptionalInt(info.elementSize));
                builder.AppendLine("    streamableDescHints: " + info.streamableDescHintCount);
                builder.AppendLine("    compressedDescHints: " + info.compressedDescHintCount);
                builder.AppendLine("    resolvedRawCandidate: " + ResolveRawCandidate(configFullPath, info.assetPath));
            }
        }

        private static void AppendImportReadiness(StringBuilder builder, UnityEngine.Object selected, string[] lines)
        {
            var hasCellDesc = ContainsField(lines, "cellDescs");
            var hasBrickAsset = ContainsField(lines, "cellBricksDataAsset");
            var hasSupportAsset = ContainsField(lines, "cellSupportDataAsset");
            var hasSharedAsset = ContainsField(lines, "cellSharedDataAsset");
            var hasTimeSliceAsset = ContainsField(lines, "cellDataAsset");
            var hasBrpAsset = selected is BurtXGIProbeBakingConfig config && config.bakedDataAsset != null;

            builder.AppendLine("Import Readiness:");
            builder.AppendLine("  cellDescs: " + FoundOrMissing(hasCellDesc));
            builder.AppendLine("  cellBricksDataAsset: " + FoundOrMissing(hasBrickAsset));
            builder.AppendLine("  cellSupportDataAsset: " + FoundOrMissing(hasSupportAsset));
            builder.AppendLine("  cellSharedDataAsset: " + FoundOrMissing(hasSharedAsset));
            builder.AppendLine("  timeSlice cellDataAsset: " + FoundOrMissing(hasTimeSliceAsset));
            builder.AppendLine("  current BRP bakedDataAsset: " + (hasBrpAsset ? "Found" : "Missing"));
            if (!hasCellDesc || !hasBrickAsset)
            {
                builder.AppendLine("  Next: legacy binary conversion needs cellDescs and cellBricksDataAsset in the source asset text.");
            }
            else if (!hasTimeSliceAsset)
            {
                builder.AppendLine("  Next: placement can be recovered, but time-slice SH data is not present in this source asset.");
            }
            else
            {
                builder.AppendLine("  Next: source has the required legacy handles for a BRP baked-data conversion pass.");
            }
        }

        private static void AddStreamingAssetInfos(List<StreamingAssetInfo> infos, string[] lines, string fieldName)
        {
            var occurrence = 0;
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (!IsFieldLine(lines[lineIndex], fieldName))
                {
                    continue;
                }

                occurrence++;
                var label = occurrence == 1 ? fieldName : fieldName + "[" + occurrence + "]";
                infos.Add(ReadStreamingAssetBlock(lines, lineIndex, label));
            }
        }

        private static StreamingAssetInfo ReadStreamingAssetBlock(string[] lines, int startIndex, string label)
        {
            var info = new StreamingAssetInfo
            {
                label = label,
                elementSize = -1
            };
            var baseIndent = CountIndent(lines[startIndex]);
            for (var index = startIndex + 1; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!string.IsNullOrWhiteSpace(line) && CountIndent(line) <= baseIndent)
                {
                    break;
                }

                if (TryReadStringField(line, "m_AssetPath", out var assetPath))
                {
                    info.assetPath = assetPath;
                }
                else if (TryReadIntField(line, "m_ElementSize", out var elementSize))
                {
                    info.elementSize = elementSize;
                }
                else if (IsFieldLine(line, "offset") || IsFieldLine(line, "elementCount"))
                {
                    info.streamableDescHintCount++;
                }
                else if (TryReadIntField(line, "compressedSize", out var compressedSize))
                {
                    info.compressedDescHintCount++;
                    info.totalCompressedBytes += Mathf.Max(0, compressedSize);
                }
            }

            if (info.compressedDescHintCount > info.streamableDescHintCount)
            {
                info.streamableDescHintCount = info.compressedDescHintCount;
            }

            return info;
        }

        private static void AppendInt(StringBuilder builder, string[] lines, string fieldName)
        {
            builder.AppendLine("  " + fieldName + ": " +
                (TryFindInt(lines, fieldName, out var value) ? value.ToString() : "Missing"));
        }

        private static bool TryFindInt(string[] lines, string fieldName, out int value)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (TryReadIntField(lines[index], fieldName, out value))
                {
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static bool ContainsField(string[] lines, string fieldName)
        {
            if (lines == null)
            {
                return false;
            }

            for (var index = 0; index < lines.Length; index++)
            {
                if (IsFieldLine(lines[index], fieldName))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadIntField(string line, string fieldName, out int value)
        {
            value = default;
            if (!TryReadStringField(line, fieldName, out var text))
            {
                return false;
            }

            return int.TryParse(text, out value);
        }

        private static bool TryReadStringField(string line, string fieldName, out string value)
        {
            value = null;
            if (line == null)
            {
                return false;
            }

            var trimmed = line.Trim();
            var prefix = fieldName + ":";
            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            value = trimmed.Substring(prefix.Length).Trim().Trim('"');
            return true;
        }

        private static bool IsFieldLine(string line, string fieldName)
        {
            if (line == null)
            {
                return false;
            }

            return line.TrimStart().StartsWith(fieldName + ":", StringComparison.Ordinal);
        }

        private static int CountIndent(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                return 0;
            }

            var count = 0;
            while (count < line.Length && char.IsWhiteSpace(line[count]))
            {
                count++;
            }

            return count;
        }

        private static string ResolveRawCandidate(string configFullPath, string legacyRelativePath)
        {
            if (string.IsNullOrEmpty(legacyRelativePath))
            {
                return "Missing";
            }

            if (Path.IsPathRooted(legacyRelativePath))
            {
                return legacyRelativePath;
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                var candidate = Path.GetFullPath(Path.Combine(projectRoot, legacyRelativePath));
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }

            var configDirectory = Path.GetDirectoryName(configFullPath);
            if (!string.IsNullOrEmpty(configDirectory))
            {
                var besideConfig = Path.GetFullPath(Path.Combine(configDirectory, legacyRelativePath));
                if (File.Exists(besideConfig))
                {
                    return besideConfig;
                }
            }

            return "Not found under project/config roots: " + legacyRelativePath;
        }

        private static string TrimForDialog(string report)
        {
            const int maxLength = 1800;
            if (string.IsNullOrEmpty(report) || report.Length <= maxLength)
            {
                return report;
            }

            return report.Substring(0, maxLength) + "\n...\nFull report was written to the Console.";
        }

        private static string FormatOptionalInt(int value)
        {
            return value >= 0 ? value.ToString() : "Missing";
        }

        private static string FoundOrMissing(bool value)
        {
            return value ? "Found" : "Missing";
        }

        private static string NullSafe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<empty>" : value;
        }

        private struct StreamingAssetInfo
        {
            public string label;
            public string assetPath;
            public int elementSize;
            public int streamableDescHintCount;
            public int compressedDescHintCount;
            public int totalCompressedBytes;
        }
    }
}
