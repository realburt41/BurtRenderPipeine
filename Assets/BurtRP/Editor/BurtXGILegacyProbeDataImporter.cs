using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Burt.RenderPipeline.Editor
{
    public static class BurtXGILegacyProbeDataImporter
    {
        private const string MenuPath = "BurtRP/XGI/Import Selected Legacy XRender Probe Baked Data";
        private const string ExternalMenuPath = "BurtRP/XGI/Import External Legacy XRender Probe Baked Data...";
        private const int LegacyBrickByteSize = 16;

        [MenuItem(MenuPath, false, 2509)]
        private static void ImportSelectedLegacyXRenderProbeData()
        {
            if (!(Selection.activeObject is BurtXGIProbeBakingConfig config))
            {
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", "Select a BurtXGIProbeBakingConfig asset first.", "OK");
                return;
            }

            var assetPath = AssetDatabase.GetAssetPath(config);
            var fullPath = Path.GetFullPath(assetPath);
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(fullPath))
            {
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", "Selected config asset is missing on disk.", "OK");
                return;
            }

            if (!TryLoadLegacySource(fullPath, out var source, out var error))
            {
                Debug.LogWarning(error, config);
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", error, "OK");
                return;
            }

            if (!TryImport(config, assetPath, fullPath, source, out var report, out var importedAsset))
            {
                Debug.LogWarning(report, config);
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", TrimForDialog(report), "Needs Attention");
                return;
            }

            Selection.activeObject = importedAsset;
            Debug.Log(report, importedAsset);
            EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", TrimForDialog(report), "OK");
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateImportSelectedLegacyXRenderProbeData()
        {
            return !Application.isPlaying && Selection.activeObject is BurtXGIProbeBakingConfig;
        }

        [MenuItem(ExternalMenuPath, false, 2510)]
        private static void ImportExternalLegacyXRenderProbeData()
        {
            var sourcePath = EditorUtility.OpenFilePanel(
                "Select Legacy XRender XGIProbeBakingConfig",
                Application.dataPath,
                "asset");
            if (string.IsNullOrEmpty(sourcePath))
            {
                return;
            }

            if (!TryLoadLegacySource(sourcePath, out var source, out var error))
            {
                Debug.LogWarning(error);
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", error, "OK");
                return;
            }

            var defaultName = BuildImportedConfigAssetName(source, sourcePath);
            var targetAssetPath = EditorUtility.SaveFilePanelInProject(
                "Create Burt XGI Probe Baking Config",
                defaultName,
                "asset",
                "Choose where to create the BRP config that will receive the imported legacy XRender baked data.",
                "Assets");
            if (string.IsNullOrEmpty(targetAssetPath))
            {
                return;
            }

            if (!TryImportExternalAsset(sourcePath, targetAssetPath, out var report, out var importedAsset))
            {
                Debug.LogWarning(report);
                EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", TrimForDialog(report), "Needs Attention");
                return;
            }

            Selection.activeObject = importedAsset;
            EditorGUIUtility.PingObject(importedAsset);
            Debug.Log(report, importedAsset);
            EditorUtility.DisplayDialog("BurtRP XGI Legacy Import", TrimForDialog(report), "OK");
        }

        [MenuItem(ExternalMenuPath, true)]
        private static bool ValidateImportExternalLegacyXRenderProbeData()
        {
            return !Application.isPlaying;
        }

        public static void ImportExternalFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var sourcePath = GetCommandLineValue(args, "-burtXGILegacySource");
            var targetAssetPath = GetCommandLineValue(args, "-burtXGILegacyTarget");
            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(targetAssetPath))
            {
                Debug.LogError("Burt XGI legacy import requires -burtXGILegacySource <path> and -burtXGILegacyTarget <Assets/...asset>.");
                EditorApplication.Exit(2);
                return;
            }

            if (!TryImportExternalAsset(sourcePath, targetAssetPath, out var report, out _))
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateExternalFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var sourcePath = GetCommandLineValue(args, "-burtXGILegacySource");
            if (string.IsNullOrEmpty(sourcePath))
            {
                Debug.LogError("Burt XGI legacy validation requires -burtXGILegacySource <path>.");
                EditorApplication.Exit(2);
                return;
            }

            if (!TryValidateExternalAsset(sourcePath, out var report))
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        internal static bool TryImportExternalAsset(
            string sourcePath,
            string targetAssetPath,
            out string report,
            out BurtXGIProbeBakedDataAsset importedAsset)
        {
            importedAsset = null;
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                report = "Legacy XRender import failed: source asset path does not exist: " + sourcePath;
                return false;
            }

            if (string.IsNullOrEmpty(targetAssetPath) || !targetAssetPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                report = "Legacy XRender import failed: target asset path must be project-relative under Assets/: " + targetAssetPath;
                return false;
            }

            if (!TryLoadLegacySource(sourcePath, out var source, out var error))
            {
                report = error;
                return false;
            }

            var config = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(targetAssetPath);
            if (config == null)
            {
                EnsureAssetFolder(Path.GetDirectoryName(targetAssetPath)?.Replace('\\', '/'));
                config = ScriptableObject.CreateInstance<BurtXGIProbeBakingConfig>();
                AssetDatabase.CreateAsset(config, targetAssetPath);
            }

            ApplyLegacyMetadata(config, source, sourcePath);
            return TryImport(config, targetAssetPath, sourcePath, source, out report, out importedAsset);
        }

        internal static bool TryValidateExternalAsset(string sourcePath, out string report)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                report = "Legacy XRender validation failed: source asset path does not exist: " + sourcePath;
                return false;
            }

            if (!TryLoadLegacySource(sourcePath, out var source, out var error))
            {
                report = error;
                return false;
            }

            if (source.cells == null || source.cells.Count == 0)
            {
                report = "Legacy XRender validation failed: no cells parsed.";
                return false;
            }

            if (!source.streamingAssets.TryGetValue("cellBricksDataAsset", out var brickAsset) ||
                !TryResolveRawFile(sourcePath, brickAsset.assetPath, out var brickRawPath))
            {
                report = "Legacy XRender validation failed: cellBricksDataAsset raw file was not found.";
                return false;
            }

            var firstCell = source.cells[0];
            if (!brickAsset.descs.TryGetValue(firstCell.index, out var brickDesc) ||
                !TryReadCellBytes(brickRawPath, brickAsset, brickDesc, out var brickBytes, out error))
            {
                report = "Legacy XRender validation failed: " + error;
                return false;
            }

            var timeSliceAsset = FindFirstStreamingAsset(source, "cellDataAsset");
            var optionalTimeSliceAsset = FindFirstStreamingAsset(source, "cellOptionalDataAsset");
            var sharedAsset = FindFirstStreamingAsset(source, "cellSharedDataAsset");
            TryResolveRawFile(sourcePath, timeSliceAsset.assetPath, out var timeSliceRawPath);
            TryResolveRawFile(sourcePath, optionalTimeSliceAsset.assetPath, out var optionalTimeSliceRawPath);
            TryResolveRawFile(sourcePath, sharedAsset.assetPath, out var sharedRawPath);
            if (source.l0ChunkSize > 0 && string.IsNullOrEmpty(timeSliceRawPath))
            {
                report = "Legacy XRender validation failed: source declares SH chunks but cellDataAsset raw file was not found.";
                return false;
            }

            if (source.l2TextureChunkSize > 0 && string.IsNullOrEmpty(optionalTimeSliceRawPath))
            {
                report = "Legacy XRender validation failed: source declares L2 SH chunks but cellOptionalDataAsset raw file was not found.";
                return false;
            }

            if ((source.HasSharedSkyVisibility || source.HasSharedSkyShadingDirection) && string.IsNullOrEmpty(sharedRawPath))
            {
                report = "Legacy XRender validation failed: source declares shared sky data but cellSharedDataAsset raw file was not found.";
                return false;
            }

            if (!TryReadLegacyCellChunkData(
                    firstCell,
                    source,
                    timeSliceAsset,
                    optionalTimeSliceAsset,
                    sharedAsset,
                    timeSliceRawPath,
                    optionalTimeSliceRawPath,
                    sharedRawPath,
                    out var chunkData,
                    out error))
            {
                report = "Legacy XRender validation failed: " + error;
                return false;
            }

            report = "Legacy XRender validation completed.\n" +
                "Source: " + sourcePath + "\n" +
                "Scene: " + source.sceneName + " (" + source.platform + ")\n" +
                "Cells: " + source.cells.Count + "\n" +
                "FirstCell: index=" + firstCell.index +
                ", bricks=" + firstCell.bricksCount +
                ", probes=" + firstCell.probeCount +
                ", chunks=" + firstCell.shChunkCount + "\n" +
                "FirstCellBytes: bricks=" + (brickBytes?.Length ?? 0) +
                ", sh=" + (chunkData.timeSliceBytes?.Length ?? 0) +
                ", l2=" + (chunkData.optionalBytes?.Length ?? 0) +
                ", shared=" + (chunkData.sharedBytes?.Length ?? 0) + "\n" +
                "Raw: " + brickRawPath;
            return true;
        }

        private static string GetCommandLineValue(string[] args, string name)
        {
            if (args == null)
            {
                return null;
            }

            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[index + 1];
                }
            }

            return null;
        }

        private static bool TryImport(
            BurtXGIProbeBakingConfig config,
            string configAssetPath,
            string configFullPath,
            LegacySource source,
            out string report,
            out BurtXGIProbeBakedDataAsset importedAsset)
        {
            importedAsset = null;
            if (source.cells.Count == 0)
            {
                report = "Legacy XRender import failed: source asset has no parseable cellDescs.";
                return false;
            }

            if (!source.streamingAssets.TryGetValue("cellBricksDataAsset", out var brickAsset) ||
                !TryResolveRawFile(configFullPath, brickAsset.assetPath, out var brickRawPath))
            {
                report = "Legacy XRender import failed: cellBricksDataAsset raw file was not found.";
                return false;
            }

            var timeSliceAsset = FindFirstStreamingAsset(source, "cellDataAsset");
            var optionalTimeSliceAsset = FindFirstStreamingAsset(source, "cellOptionalDataAsset");
            var sharedAsset = FindFirstStreamingAsset(source, "cellSharedDataAsset");
            TryResolveRawFile(configFullPath, timeSliceAsset.assetPath, out var timeSliceRawPath);
            TryResolveRawFile(configFullPath, optionalTimeSliceAsset.assetPath, out var optionalTimeSliceRawPath);
            TryResolveRawFile(configFullPath, sharedAsset.assetPath, out var sharedRawPath);
            if (source.l0ChunkSize > 0 && string.IsNullOrEmpty(timeSliceRawPath))
            {
                report = "Legacy XRender import failed: source declares SH chunks but cellDataAsset raw file was not found.";
                return false;
            }

            if (source.l2TextureChunkSize > 0 && string.IsNullOrEmpty(optionalTimeSliceRawPath))
            {
                report = "Legacy XRender import failed: source declares L2 SH chunks but cellOptionalDataAsset raw file was not found.";
                return false;
            }

            if ((source.HasSharedSkyVisibility || source.HasSharedSkyShadingDirection) && string.IsNullOrEmpty(sharedRawPath))
            {
                report = "Legacy XRender import failed: source declares shared sky data but cellSharedDataAsset raw file was not found.";
                return false;
            }

            var sortedCells = new List<LegacyCellDesc>(source.cells);
            sortedCells.Sort((left, right) => left.index.CompareTo(right.index));
            var placedCells = new List<BurtXGIProbePlacedCell>(sortedCells.Count);
            var placedBricks = new List<BurtXGIProbePlacedBrick>();
            var finalizedCells = new BurtXGIProbeFinalizedCell[sortedCells.Count];
            var chunksByCellIndex = new Dictionary<int, LegacyCellChunkData>();
            var probeStart = 0;

            for (var cellListIndex = 0; cellListIndex < sortedCells.Count; cellListIndex++)
            {
                var cell = sortedCells[cellListIndex];
                if (!brickAsset.descs.TryGetValue(cell.index, out var brickDesc))
                {
                    report = "Legacy XRender import failed: missing brick stream desc for cell " + cell.index + ".";
                    return false;
                }

                if (!TryReadCellBytes(brickRawPath, brickAsset, brickDesc, out var brickBytes, out var readError))
                {
                    report = "Legacy XRender import failed: " + readError;
                    return false;
                }

                var brickStart = placedBricks.Count;
                AppendBricks(placedBricks, brickBytes, cell.index);
                if (cell.bricksCount > 0 && placedBricks.Count - brickStart != cell.bricksCount)
                {
                    report = "Legacy XRender import failed: brick count mismatch for cell " + cell.index +
                        " expected=" + cell.bricksCount + " actual=" + (placedBricks.Count - brickStart) + ".";
                    return false;
                }

                var bounds = CreateCellBounds(config, cell.position);
                var sceneGuids = ResolveSceneGuids(config);
                placedCells.Add(new BurtXGIProbePlacedCell
                {
                    index = cell.index,
                    position = cell.position,
                    bounds = bounds,
                    brickStartIndex = brickStart,
                    brickCount = placedBricks.Count - brickStart,
                    probeStartIndex = probeStart,
                    probeCount = cell.probeCount,
                    sceneGuids = sceneGuids
                });

                finalizedCells[cellListIndex] = new BurtXGIProbeFinalizedCell
                {
                    cellIndex = cell.index,
                    position = cell.position,
                    bounds = bounds,
                    minSubdivisionLevel = cell.minSubdiv,
                    shChunkCount = Mathf.Max(1, cell.shChunkCount),
                    brickStartIndex = brickStart,
                    brickCount = placedBricks.Count - brickStart,
                    probeStartIndex = probeStart,
                    probeCount = cell.probeCount,
                    sceneGuids = sceneGuids,
                    hasSkyVisibility = source.HasSharedSkyVisibility,
                    hasSkyShadingDirection = source.HasSharedSkyShadingDirection,
                    hasTimeSliceSH = !string.IsNullOrEmpty(timeSliceRawPath)
                };

                if (!TryReadLegacyCellChunkData(
                    cell,
                    source,
                    timeSliceAsset,
                    optionalTimeSliceAsset,
                    sharedAsset,
                    timeSliceRawPath,
                    optionalTimeSliceRawPath,
                    sharedRawPath,
                    out var chunkData,
                    out readError))
                {
                    report = "Legacy XRender import failed: " + readError;
                    return false;
                }

                chunksByCellIndex[cell.index] = chunkData;
                probeStart += Mathf.Max(0, cell.probeCount);
            }

            config.CapturePlacement(placedCells.ToArray(), placedBricks.ToArray(), new Vector3[Mathf.Max(0, probeStart)]);
            config.CaptureFinalizedCells(finalizedCells);
            config.bakedUseTimeSlice = !string.IsNullOrEmpty(timeSliceRawPath);
            config.bakedSkyVisibility = source.HasSharedSkyVisibility && !string.IsNullOrEmpty(sharedRawPath);
            config.bakedSkyShadingDirection = source.HasSharedSkyShadingDirection && !string.IsNullOrEmpty(sharedRawPath);
            config.bakedTimeSliceType = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(config.timeSliceType);

            importedAsset = CreateOrLoadLegacyImportedAsset(configAssetPath, config);
            BurtXGIProbeBakingProcessor.PopulateBakedDataAsset(
                config,
                importedAsset,
                finalizedCells,
                (bakingConfig, cell, chunkIndex, physicalChunkIndex) =>
                    BuildLegacyChunk(bakingConfig, cell, chunkIndex, physicalChunkIndex, chunksByCellIndex),
                out _,
                out var chunkCount,
                out var pageTableEntryCount,
                out var indirectionEntryCount);

            importedAsset.name = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(importedAsset));
            importedAsset.perSceneCellLists = new List<BurtXGIProbePerSceneCellList>
            {
                new BurtXGIProbePerSceneCellList
                {
                    sceneGuid = string.IsNullOrEmpty(config.sceneGuid) ? "LegacyXRender" : config.sceneGuid,
                    cellIndices = new List<int>(Array.ConvertAll(finalizedCells, cell => cell.cellIndex))
                }
            };
            importedAsset.chunkCount = chunkCount;
            importedAsset.pageTableEntryCount = pageTableEntryCount;
            importedAsset.indirectionEntryCount = indirectionEntryCount;
            config.CaptureSerializedData(importedAsset);
            EditorUtility.SetDirty(importedAsset);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            report = "Legacy XRender import completed.\n" +
                "Config: " + configAssetPath + "\n" +
                "Asset: " + AssetDatabase.GetAssetPath(importedAsset) + "\n" +
                "Cells/Bricks/Probes: " + finalizedCells.Length + "/" + placedBricks.Count + "/" + probeStart + "\n" +
                "Chunks/PageTable/Indirection: " + chunkCount + "/" + pageTableEntryCount + "/" + indirectionEntryCount + "\n" +
                "TimeSliceRaw: " + (!string.IsNullOrEmpty(timeSliceRawPath) ? timeSliceRawPath : "Missing") + "\n" +
                "SharedRaw: " + (!string.IsNullOrEmpty(sharedRawPath) ? sharedRawPath : "Missing");
            return true;
        }

        private static void ApplyLegacyMetadata(BurtXGIProbeBakingConfig config, LegacySource source, string sourcePath)
        {
            config.sceneGuid = source.sceneGuid ?? string.Empty;
            config.scenePath = sourcePath.Replace('\\', '/');
            config.sceneName = source.sceneName;
            config.platform = source.platform;
            config.probeOffset = source.probeOffset;
            config.minDistanceBetweenProbes = source.minDistanceBetweenProbes > 0f ? source.minDistanceBetweenProbes : config.minDistanceBetweenProbes;
            config.simplificationLevels = Mathf.Clamp(source.simplificationLevels > 0 ? source.simplificationLevels : config.simplificationLevels, 2, 4);
            config.streamerType = source.streamerType;
            config.useTimeSliceData = source.bakedUseTimeSlice || source.l0ChunkSize > 0;
            config.timeSliceType = source.timeSliceType;
            config.skyVisibility = source.bakedSkyVisibility || source.HasSharedSkyVisibility;
            config.skyVisibilityShadingDirection = source.bakedSkyShadingDirection || source.HasSharedSkyShadingDirection;
            config.chunkSizeInBricks = source.chunkSizeInBricks > 0 ? source.chunkSizeInBricks : config.chunkSizeInBricks;
            config.minCellPosition = source.minCellPosition;
            config.maxCellPosition = source.maxCellPosition;
            config.globalBounds = HasValidBounds(source.globalBounds) ? source.globalBounds : BuildBoundsFromCells(config, source.cells);
            config.bakedProbeOffset = source.bakedProbeOffset;
            config.bakedMinDistanceBetweenProbes = source.bakedMinDistanceBetweenProbes > 0f
                ? source.bakedMinDistanceBetweenProbes
                : config.minDistanceBetweenProbes;
            config.bakedSimplificationLevels = source.bakedSimplificationLevels >= 0
                ? source.bakedSimplificationLevels
                : config.simplificationLevels;
            config.bakedStreamerType = source.bakedStreamerType;
            config.bakedUseTimeSlice = source.bakedUseTimeSlice || source.l0ChunkSize > 0;
            config.bakedSkyVisibility = source.bakedSkyVisibility || source.HasSharedSkyVisibility;
            config.bakedSkyShadingDirection = source.bakedSkyShadingDirection || source.HasSharedSkyShadingDirection;
            config.bakedTimeSliceType = source.timeSliceType;
            config.bakedTimeSliceMainLightIntensity = Mathf.Max(0.0001f, source.bakedTimeSliceMainLightIntensity);
            config.systemParameters.enable = true;
            config.systemParameters.shBands = ResolveLegacySHBands(source);
            EditorUtility.SetDirty(config);
        }

        private static BurtXGIProbeSHBands ResolveLegacySHBands(LegacySource source)
        {
            if (source.l2TextureChunkSize > 0)
            {
                return BurtXGIProbeSHBands.SphericalHarmonicsL2;
            }

            if (source.l1ChunkSize > 0)
            {
                return BurtXGIProbeSHBands.SphericalHarmonicsL1;
            }

            return source.l0ChunkSize > 0 ? BurtXGIProbeSHBands.L0 : BurtXGIProbeSHBands.None;
        }

        private static Bounds BuildBoundsFromCells(BurtXGIProbeBakingConfig config, List<LegacyCellDesc> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return default;
            }

            var bounds = CreateCellBounds(config, cells[0].position);
            for (var index = 1; index < cells.Count; index++)
            {
                bounds.Encapsulate(CreateCellBounds(config, cells[index].position));
            }

            return bounds;
        }

        private static bool HasValidBounds(Bounds bounds)
        {
            return bounds.size.x > 0.0001f && bounds.size.y > 0.0001f && bounds.size.z > 0.0001f;
        }

        private static bool TryLoadLegacySource(string configFullPath, out LegacySource source, out string error)
        {
            source = default;
            error = null;
            var lines = File.ReadAllLines(configFullPath);
            var cells = ParseCellDescs(lines);
            var streamingAssets = ParseStreamingAssets(lines);
            if (cells.Count == 0)
            {
                error = "Legacy XRender source has no parseable cellDescs.";
                return false;
            }

            source = new LegacySource
            {
                cells = cells,
                streamingAssets = streamingAssets,
                sourcePath = configFullPath,
                sceneGuid = FindString(lines, "m_SceneGUID"),
                sceneName = ResolveLegacySceneName(configFullPath, streamingAssets),
                platform = ParsePlatform(FindInt(lines, "m_Platform")),
                probeOffset = FindVector3(lines, "probeOffset"),
                minDistanceBetweenProbes = FindFloat(lines, "minDistanceBetweenProbes"),
                simplificationLevels = FindInt(lines, "simplificationLevels"),
                streamerType = ParseStreamerType(FindInt(lines, "streamerType")),
                chunkSizeInBricks = FindInt(lines, "chunkSizeInBricks"),
                minCellPosition = FindVector3Int(lines, "minCellPosition"),
                maxCellPosition = FindVector3Int(lines, "maxCellPosition"),
                globalBounds = FindBounds(lines, "globalBounds"),
                bakedProbeOffset = FindVector3(lines, "bakedProbeOffset"),
                bakedMinDistanceBetweenProbes = FindFloat(lines, "bakedMinDistanceBetweenProbes"),
                bakedSimplificationLevels = FindInt(lines, "bakedSimplificationLevels"),
                bakedStreamerType = ParseStreamerType(FindInt(lines, "bakedStreamerType")),
                bakedUseTimeSlice = FindInt(lines, "bakedUseTimeSliceValue") > 0,
                timeSliceType = ParseTimeSlice(FindInt(lines, "timeSliceType")),
                bakedTimeSliceMainLightIntensity = FindFloat(lines, "bakedMainLightIntensity", 1f),
                bakedSkyVisibility = FindInt(lines, "bakedSkyVisibilityValue") > 0,
                bakedSkyShadingDirection = FindInt(lines, "bakedSkyShadingDirectionValue") > 0,
                l0ChunkSize = FindInt(lines, "l0ChunkSize"),
                l1ChunkSize = FindInt(lines, "l1ChunkSize"),
                l2TextureChunkSize = FindInt(lines, "l2TextureChunkSize"),
                sharedSkyVisibilityL0L1ChunkSize = FindInt(lines, "sharedSkyVisibilityL0L1ChunkSize"),
                sharedSkyShadingDirectionIndicesChunkSize = FindInt(lines, "sharedSkyShadingDirectionIndicesChunkSize")
            };
            return true;
        }

        private static List<LegacyCellDesc> ParseCellDescs(string[] lines)
        {
            var result = new List<LegacyCellDesc>();
            if (!TryFindBlock(lines, "cellDescs", 0, out var start, out var end))
            {
                return result;
            }

            for (var index = start + 1; index < end; index++)
            {
                if (!TryReadVector3IntField(lines[index], "position", out var position))
                {
                    continue;
                }

                var cell = new LegacyCellDesc { position = position, index = int.MinValue };
                for (var scan = index + 1; scan < Mathf.Min(end, index + 80); scan++)
                {
                    if (TryReadVector3IntField(lines[scan], "position", out _) && scan > index + 1)
                    {
                        break;
                    }

                    if (TryReadIntField(lines[scan], "index", out var cellIndex)) cell.index = cellIndex;
                    else if (TryReadIntField(lines[scan], "minSubdiv", out var minSubdiv)) cell.minSubdiv = minSubdiv;
                    else if (TryReadIntField(lines[scan], "bricksCount", out var bricksCount)) cell.bricksCount = bricksCount;
                    else if (TryReadIntField(lines[scan], "probeCount", out var probeCount)) cell.probeCount = probeCount;
                    else if (TryReadIntField(lines[scan], "shChunkCount", out var shChunkCount)) cell.shChunkCount = shChunkCount;
                }

                if (cell.index != int.MinValue && cell.bricksCount >= 0 && cell.probeCount >= 0)
                {
                    result.Add(cell);
                }
            }

            return result;
        }

        private static Dictionary<string, StreamingAssetInfo> ParseStreamingAssets(string[] lines)
        {
            var result = new Dictionary<string, StreamingAssetInfo>(StringComparer.Ordinal);
            for (var index = 0; index < lines.Length; index++)
            {
                var field = TryGetKnownStreamingAssetField(lines[index]);
                if (string.IsNullOrEmpty(field))
                {
                    continue;
                }

                if (!TryFindBlock(lines, field, index, out var start, out var end))
                {
                    continue;
                }

                var info = new StreamingAssetInfo { label = field, elementSize = -1 };
                for (var scan = start + 1; scan < end; scan++)
                {
                    if (TryReadStringField(lines[scan], "m_AssetPath", out var assetPath)) info.assetPath = assetPath;
                    else if (TryReadIntField(lines[scan], "m_ElementSize", out var elementSize)) info.elementSize = elementSize;
                }

                info.descs = ParseStreamableCellDescs(lines, start, end);
                var key = result.ContainsKey(field) ? field + "#" + result.Count : field;
                result[key] = info;
            }

            return result;
        }

        private static Dictionary<int, StreamableCellDesc> ParseStreamableCellDescs(string[] lines, int start, int end)
        {
            var keys = new List<int>();
            var values = new List<StreamableCellDesc>();
            for (var index = start; index < end; index++)
            {
                if (IsSequenceField(lines[index], "m_Keys") || IsSequenceField(lines[index], "_keys") || IsSequenceField(lines[index], "keys"))
                {
                    ReadIntList(lines, index, end, keys);
                }
                else if (TryReadIntField(lines[index], "offset", out var offset))
                {
                    var desc = new StreamableCellDesc { offset = offset };
                    for (var scan = index + 1; scan < Mathf.Min(end, index + 12); scan++)
                    {
                        if (TryReadIntField(lines[scan], "offset", out _)) break;
                        if (TryReadIntField(lines[scan], "elementCount", out var elementCount)) desc.elementCount = elementCount;
                        else if (TryReadIntField(lines[scan], "compressedSize", out var compressedSize)) desc.compressedSize = compressedSize;
                    }

                    values.Add(desc);
                }
            }

            var result = new Dictionary<int, StreamableCellDesc>();
            for (var index = 0; index < Mathf.Min(keys.Count, values.Count); index++)
            {
                result[keys[index]] = values[index];
            }

            if (result.Count == 0 && values.Count > 0)
            {
                for (var index = 0; index < values.Count; index++)
                {
                    result[index] = values[index];
                }
            }

            return result;
        }

        private static bool TryReadLegacyCellChunkData(
            LegacyCellDesc cell,
            LegacySource source,
            StreamingAssetInfo timeSliceAsset,
            StreamingAssetInfo optionalTimeSliceAsset,
            StreamingAssetInfo sharedAsset,
            string timeSliceRawPath,
            string optionalTimeSliceRawPath,
            string sharedRawPath,
            out LegacyCellChunkData data,
            out string error)
        {
            data = new LegacyCellChunkData
            {
                l0ChunkSize = source.l0ChunkSize,
                l1ChunkSize = source.l1ChunkSize,
                l2TextureChunkSize = source.l2TextureChunkSize,
                sharedSkyVisibilityL0L1ChunkSize = source.sharedSkyVisibilityL0L1ChunkSize,
                sharedSkyShadingDirectionIndicesChunkSize = source.sharedSkyShadingDirectionIndicesChunkSize
            };
            error = null;

            if (source.l0ChunkSize > 0)
            {
                if (timeSliceAsset.descs == null || !timeSliceAsset.descs.TryGetValue(cell.index, out var shDesc))
                {
                    error = "missing SH stream desc for cell " + cell.index + ".";
                    return false;
                }

                if (!TryReadCellBytes(timeSliceRawPath, timeSliceAsset, shDesc, out data.timeSliceBytes, out error))
                {
                    return false;
                }
            }

            if (source.l2TextureChunkSize > 0)
            {
                if (optionalTimeSliceAsset.descs == null || !optionalTimeSliceAsset.descs.TryGetValue(cell.index, out var l2Desc))
                {
                    error = "missing L2 SH stream desc for cell " + cell.index + ".";
                    return false;
                }

                if (!TryReadCellBytes(optionalTimeSliceRawPath, optionalTimeSliceAsset, l2Desc, out data.optionalBytes, out error))
                {
                    return false;
                }
            }

            if (source.HasSharedSkyVisibility || source.HasSharedSkyShadingDirection)
            {
                if (sharedAsset.descs == null || !sharedAsset.descs.TryGetValue(cell.index, out var sharedDesc))
                {
                    error = "missing shared stream desc for cell " + cell.index + ".";
                    return false;
                }

                if (!TryReadCellBytes(sharedRawPath, sharedAsset, sharedDesc, out data.sharedBytes, out error))
                {
                    return false;
                }
            }

            return true;
        }

        private static BurtXGIProbeBakedChunk BuildLegacyChunk(
            BurtXGIProbeBakingConfig config,
            BurtXGIProbeFinalizedCell cell,
            int cellChunkIndex,
            int physicalChunkIndex,
            Dictionary<int, LegacyCellChunkData> chunksByCellIndex)
        {
            if (!chunksByCellIndex.TryGetValue(cell.cellIndex, out var legacy))
            {
                throw new InvalidOperationException("Legacy XRender import missing chunk data for cell " + cell.cellIndex + ".");
            }

            var chunk = new BurtXGIProbeBakedChunk
            {
                physicalChunkIndex = physicalChunkIndex,
                sharedPhysicalChunkIndex = physicalChunkIndex,
                validity = BuildValidity(cell, cellChunkIndex, config.ChunkProbeCount)
            };

            var shChunkCount = Mathf.Max(1, cell.shChunkCount);
            chunk.l0L1Rx = Slice(legacy.timeSliceBytes, cellChunkIndex * legacy.l0ChunkSize, legacy.l0ChunkSize);
            var l1Base = legacy.l0ChunkSize * shChunkCount;
            chunk.l1GL1Ry = Slice(legacy.timeSliceBytes, l1Base + cellChunkIndex * legacy.l1ChunkSize, legacy.l1ChunkSize);
            chunk.l1BL1Rz = Slice(legacy.timeSliceBytes, l1Base + legacy.l1ChunkSize * shChunkCount + cellChunkIndex * legacy.l1ChunkSize, legacy.l1ChunkSize);
            chunk.l20 = Slice(legacy.optionalBytes, cellChunkIndex * legacy.l2TextureChunkSize, legacy.l2TextureChunkSize);
            chunk.l21 = Slice(legacy.optionalBytes, legacy.l2TextureChunkSize * shChunkCount + cellChunkIndex * legacy.l2TextureChunkSize, legacy.l2TextureChunkSize);
            chunk.l22 = Slice(legacy.optionalBytes, legacy.l2TextureChunkSize * shChunkCount * 2 + cellChunkIndex * legacy.l2TextureChunkSize, legacy.l2TextureChunkSize);
            chunk.l23 = Slice(legacy.optionalBytes, legacy.l2TextureChunkSize * shChunkCount * 3 + cellChunkIndex * legacy.l2TextureChunkSize, legacy.l2TextureChunkSize);
            chunk.skyVisibilityL0L1 = Slice(legacy.sharedBytes, cellChunkIndex * legacy.sharedSkyVisibilityL0L1ChunkSize, legacy.sharedSkyVisibilityL0L1ChunkSize);
            chunk.skyShadingDirectionIndices = Slice(
                legacy.sharedBytes,
                legacy.sharedSkyVisibilityL0L1ChunkSize * shChunkCount + cellChunkIndex * legacy.sharedSkyShadingDirectionIndicesChunkSize,
                legacy.sharedSkyShadingDirectionIndicesChunkSize);
            return chunk;
        }

        private static bool TryReadCellBytes(
            string rawPath,
            StreamingAssetInfo asset,
            StreamableCellDesc desc,
            out byte[] bytes,
            out string error)
        {
            bytes = null;
            error = null;
            if (string.IsNullOrEmpty(rawPath) || !File.Exists(rawPath))
            {
                error = "raw file missing: " + rawPath;
                return false;
            }

            var byteCount = desc.compressedSize > 0
                ? desc.compressedSize
                : desc.elementCount * Mathf.Max(1, asset.elementSize);
            if (byteCount <= 0)
            {
                error = "invalid stream byte count for " + asset.label;
                return false;
            }

            using (var stream = File.OpenRead(rawPath))
            {
                if (desc.offset < 0 || desc.offset + byteCount > stream.Length)
                {
                    error = "stream range is outside raw file for " + asset.label;
                    return false;
                }

                stream.Position = desc.offset;
                bytes = new byte[byteCount];
                var read = stream.Read(bytes, 0, byteCount);
                if (read != byteCount)
                {
                    error = "failed to read raw stream bytes for " + asset.label;
                    return false;
                }
            }

            if (desc.compressedSize <= 0)
            {
                return true;
            }

            var expectedByteLength = desc.elementCount * Mathf.Max(1, asset.elementSize);
            if (TryDecompressZstd(bytes, expectedByteLength, out var decompressedBytes, out var decompressError))
            {
                bytes = decompressedBytes;
                return true;
            }

            error = string.Format(
                "failed to decompress zstd stream for {0}: rawPath={1}, offset={2}, compressedSize={3}, elementCount={4}, elementSize={5}, expectedBytes={6}, streamBytes={7}, prefix={8}, error={9}",
                asset.label,
                rawPath,
                desc.offset,
                desc.compressedSize,
                desc.elementCount,
                asset.elementSize,
                expectedByteLength,
                byteCount,
                FormatBytePrefix(bytes, 12),
                decompressError);
            return false;
        }

        private static void AppendBricks(List<BurtXGIProbePlacedBrick> placedBricks, byte[] brickBytes, int cellIndex)
        {
            if (brickBytes == null)
            {
                return;
            }

            var brickCount = brickBytes.Length / LegacyBrickByteSize;
            for (var brickIndex = 0; brickIndex < brickCount; brickIndex++)
            {
                var offset = brickIndex * LegacyBrickByteSize;
                placedBricks.Add(new BurtXGIProbePlacedBrick
                {
                    position = new Vector3Int(
                        BitConverter.ToInt32(brickBytes, offset),
                        BitConverter.ToInt32(brickBytes, offset + 4),
                        BitConverter.ToInt32(brickBytes, offset + 8)),
                    subdivisionLevel = BitConverter.ToInt32(brickBytes, offset + 12),
                    cellIndex = cellIndex
                });
            }
        }

        private static byte[] BuildValidity(BurtXGIProbeFinalizedCell cell, int cellChunkIndex, int chunkProbeCount)
        {
            var bytes = new byte[Mathf.Max(1, chunkProbeCount)];
            var remaining = Mathf.Max(0, cell.probeCount - cellChunkIndex * chunkProbeCount);
            var validCount = Mathf.Min(bytes.Length, remaining);
            for (var index = 0; index < validCount; index++)
            {
                bytes[index] = 255;
            }

            return bytes;
        }

        private static byte[] Slice(byte[] source, int offset, int length)
        {
            if (source == null || length <= 0 || offset < 0 || offset >= source.Length)
            {
                return Array.Empty<byte>();
            }

            var safeLength = Mathf.Min(length, source.Length - offset);
            var result = new byte[safeLength];
            Buffer.BlockCopy(source, offset, result, 0, safeLength);
            return result;
        }

        private static BurtXGIProbeBakedDataAsset CreateOrLoadLegacyImportedAsset(string configAssetPath, BurtXGIProbeBakingConfig config)
        {
            var directory = Path.GetDirectoryName(configAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory))
            {
                directory = "Assets";
            }

            var fileName = Path.GetFileNameWithoutExtension(configAssetPath) + "_LegacyImportedBakedData.asset";
            var assetPath = directory + "/" + fileName;
            if (config.bakedDataAsset != null)
            {
                var existingPath = AssetDatabase.GetAssetPath(config.bakedDataAsset);
                if (!string.IsNullOrEmpty(existingPath) && existingPath.EndsWith("_LegacyImportedBakedData.asset", StringComparison.Ordinal))
                {
                    return config.bakedDataAsset;
                }
            }

            var existingAsset = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakedDataAsset>(assetPath);
            if (existingAsset != null)
            {
                return existingAsset;
            }

            var asset = ScriptableObject.CreateInstance<BurtXGIProbeBakedDataAsset>();
            AssetDatabase.CreateAsset(asset, assetPath);
            return asset;
        }

        private static StreamingAssetInfo FindFirstStreamingAsset(LegacySource source, string name)
        {
            if (source.streamingAssets.TryGetValue(name, out var info))
            {
                return info;
            }

            foreach (var pair in source.streamingAssets)
            {
                if (pair.Key.StartsWith(name + "#", StringComparison.Ordinal))
                {
                    return pair.Value;
                }
            }

            return default;
        }

        private static bool TryResolveRawFile(string configFullPath, string relativePath, out string fullPath)
        {
            fullPath = null;
            if (string.IsNullOrEmpty(relativePath))
            {
                return false;
            }

            var candidates = new List<string>();
            var relativePathVariants = BuildRawRelativePathVariants(relativePath);
            if (Path.IsPathRooted(relativePath))
            {
                candidates.Add(relativePath);
            }

            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (!string.IsNullOrEmpty(projectRoot))
            {
                AddRawCandidates(candidates, projectRoot, relativePathVariants);
                AddRawCandidates(candidates, Path.Combine(projectRoot, "Raw"), relativePathVariants);
                AddRawCandidates(candidates, Path.Combine(projectRoot, "raw"), relativePathVariants);
                AddRawCandidates(candidates, Path.Combine(projectRoot, "AssetsExtra", "raw"), relativePathVariants);
                AddRawCandidates(candidates, Path.Combine(projectRoot, "StreamingAssets"), relativePathVariants);
            }

            var configDirectory = Path.GetDirectoryName(configFullPath);
            if (!string.IsNullOrEmpty(configDirectory))
            {
                AddRawCandidates(candidates, configDirectory, relativePathVariants);
                AddConfigProjectRawCandidates(candidates, configDirectory, relativePathVariants);
            }

            for (var index = 0; index < candidates.Count; index++)
            {
                var candidate = Path.GetFullPath(candidates[index]);
                if (File.Exists(candidate))
                {
                    fullPath = candidate;
                    return true;
                }
            }

            if (Application.isBatchMode)
            {
                return false;
            }

            var root = EditorUtility.OpenFolderPanel("Select XRender raw file root for " + relativePath, projectRoot, string.Empty);
            if (!string.IsNullOrEmpty(root))
            {
                for (var index = 0; index < relativePathVariants.Count; index++)
                {
                    var candidate = Path.GetFullPath(Path.Combine(root, relativePathVariants[index]));
                    if (File.Exists(candidate))
                    {
                        fullPath = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static void AddConfigProjectRawCandidates(List<string> candidates, string configDirectory, List<string> relativePathVariants)
        {
            var directory = new DirectoryInfo(configDirectory);
            while (directory != null)
            {
                if (string.Equals(directory.Name, "Assets", StringComparison.OrdinalIgnoreCase))
                {
                    var configProjectRoot = directory.Parent?.FullName;
                    if (!string.IsNullOrEmpty(configProjectRoot))
                    {
                        AddRawCandidates(candidates, Path.Combine(configProjectRoot, "raw"), relativePathVariants);
                        AddRawCandidates(candidates, Path.Combine(configProjectRoot, "Raw"), relativePathVariants);
                        AddRawCandidates(candidates, Path.Combine(configProjectRoot, "AssetsExtra", "raw"), relativePathVariants);
                    }

                    return;
                }

                directory = directory.Parent;
            }
        }

        private static void AddRawCandidates(List<string> candidates, string root, List<string> relativePathVariants)
        {
            if (string.IsNullOrEmpty(root) || relativePathVariants == null)
            {
                return;
            }

            for (var index = 0; index < relativePathVariants.Count; index++)
            {
                candidates.Add(Path.Combine(root, relativePathVariants[index]));
            }
        }

        private static List<string> BuildRawRelativePathVariants(string relativePath)
        {
            var result = new List<string>();
            AddUnique(result, relativePath);
            if (string.IsNullOrEmpty(relativePath))
            {
                return result;
            }

            var normalized = relativePath.Replace('\\', '/');
            var parts = normalized.Split('/');
            if (parts.Length < 4 ||
                (parts[0] != "xrenderPC" && parts[0] != "xrenderMobile") ||
                parts[2] != "XGIProbe")
            {
                return result;
            }

            var sceneName = parts[1];
            if (sceneName.StartsWith("Level_", StringComparison.Ordinal))
            {
                AddSceneVariant(result, parts, "Art" + sceneName);
                AddSceneVariant(result, parts, "Entity" + sceneName);
                AddSceneVariant(result, parts, "Runtime" + sceneName);
            }
            else if (sceneName.StartsWith("ArtLevel_", StringComparison.Ordinal) ||
                sceneName.StartsWith("EntityLevel_", StringComparison.Ordinal) ||
                sceneName.StartsWith("RuntimeLevel_", StringComparison.Ordinal))
            {
                var levelIndex = sceneName.IndexOf("Level_", StringComparison.Ordinal);
                if (levelIndex >= 0)
                {
                    AddSceneVariant(result, parts, sceneName.Substring(levelIndex));
                }
            }

            return result;
        }

        private static void AddSceneVariant(List<string> paths, string[] sourceParts, string sceneName)
        {
            var parts = (string[])sourceParts.Clone();
            parts[1] = sceneName;
            AddUnique(paths, string.Join("/", parts));
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static Bounds CreateCellBounds(BurtXGIProbeBakingConfig config, Vector3Int cellPosition)
        {
            var cellSize = Mathf.Max(0.0001f, config.BakedCellSizeInMeters);
            var min = config.BakedProbeOffset + new Vector3(cellPosition.x, cellPosition.y, cellPosition.z) * cellSize;
            return new Bounds(min + Vector3.one * (cellSize * 0.5f), Vector3.one * cellSize);
        }

        private static string[] ResolveSceneGuids(BurtXGIProbeBakingConfig config)
        {
            return string.IsNullOrEmpty(config.sceneGuid)
                ? new[] { "LegacyXRender" }
                : new[] { config.sceneGuid };
        }

        private static string BuildImportedConfigAssetName(LegacySource source, string sourcePath)
        {
            var sceneName = !string.IsNullOrEmpty(source.sceneName)
                ? source.sceneName
                : Path.GetFileNameWithoutExtension(sourcePath);
            return SanitizeAssetName(sceneName) + "_" + source.platform + "_LegacyXRender_BakingConfig";
        }

        private static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "LegacyXRender";
            }

            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static bool TryFindBlock(string[] lines, string fieldName, int searchStart, out int start, out int end)
        {
            start = -1;
            end = -1;
            for (var index = Mathf.Max(0, searchStart); index < lines.Length; index++)
            {
                if (IsFieldLine(lines[index], fieldName))
                {
                    start = index;
                    break;
                }
            }

            if (start < 0)
            {
                return false;
            }

            var indent = CountIndent(lines[start]);
            end = lines.Length;
            for (var index = start + 1; index < lines.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(lines[index]) && CountIndent(lines[index]) <= indent)
                {
                    end = index;
                    break;
                }
            }

            return true;
        }

        private static void ReadIntList(string[] lines, int start, int end, List<int> values)
        {
            if ((TryReadStringField(lines[start], "m_Keys", out var inlineKeys) ||
                    TryReadStringField(lines[start], "_keys", out inlineKeys) ||
                    TryReadStringField(lines[start], "keys", out inlineKeys)) &&
                TryReadLittleEndianIntHexList(inlineKeys, values))
            {
                return;
            }

            var indent = CountIndent(lines[start]);
            for (var index = start + 1; index < end; index++)
            {
                var line = lines[index];
                if (!string.IsNullOrWhiteSpace(line) && CountIndent(line) <= indent)
                {
                    break;
                }

                var trimmed = line.Trim();
                if (trimmed.StartsWith("-", StringComparison.Ordinal) &&
                    int.TryParse(trimmed.Substring(1).Trim(), out var value))
                {
                    values.Add(value);
                }
            }
        }

        private static bool TryReadLittleEndianIntHexList(string text, List<int> values)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var hex = text.Trim().Trim('"');
            if (hex.Length == 0 || hex.Length % 8 != 0)
            {
                return false;
            }

            for (var index = 0; index < hex.Length; index++)
            {
                if (!Uri.IsHexDigit(hex[index]))
                {
                    return false;
                }
            }

            for (var offset = 0; offset < hex.Length; offset += 8)
            {
                var b0 = Convert.ToByte(hex.Substring(offset, 2), 16);
                var b1 = Convert.ToByte(hex.Substring(offset + 2, 2), 16);
                var b2 = Convert.ToByte(hex.Substring(offset + 4, 2), 16);
                var b3 = Convert.ToByte(hex.Substring(offset + 6, 2), 16);
                values.Add(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
            }

            return true;
        }

        private static string TryGetKnownStreamingAssetField(string line)
        {
            string[] names =
            {
                "cellBricksDataAsset",
                "cellSupportDataAsset",
                "cellSharedDataAsset",
                "cellDataAsset",
                "cellOptionalDataAsset"
            };

            for (var index = 0; index < names.Length; index++)
            {
                if (IsFieldLine(line, names[index]))
                {
                    return names[index];
                }
            }

            return null;
        }

        private static int FindInt(string[] lines, string fieldName)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (TryReadIntField(lines[index], fieldName, out var value))
                {
                    return value;
                }
            }

            return 0;
        }

        private static float FindFloat(string[] lines, string fieldName, float fallback = 0f)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (TryReadFloatField(lines[index], fieldName, out var value))
                {
                    return value;
                }
            }

            return fallback;
        }

        private static string FindString(string[] lines, string fieldName)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (TryReadStringField(lines[index], fieldName, out var value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static Vector3 FindVector3(string[] lines, string fieldName)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (TryReadVector3Field(lines[index], fieldName, out var value))
                {
                    return value;
                }
            }

            return Vector3.zero;
        }

        private static Vector3Int FindVector3Int(string[] lines, string fieldName)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (TryReadVector3IntField(lines[index], fieldName, out var value))
                {
                    return value;
                }
            }

            return Vector3Int.zero;
        }

        private static Bounds FindBounds(string[] lines, string fieldName)
        {
            if (!TryFindBlock(lines, fieldName, 0, out var start, out var end))
            {
                return default;
            }

            var center = Vector3.zero;
            var extents = Vector3.zero;
            for (var index = start + 1; index < end; index++)
            {
                if (TryReadVector3Field(lines[index], "m_Center", out var parsedCenter))
                {
                    center = parsedCenter;
                }
                else if (TryReadVector3Field(lines[index], "m_Extent", out var parsedExtents))
                {
                    extents = parsedExtents;
                }
            }

            return new Bounds(center, extents * 2f);
        }

        private static BurtXGIProbeBakingPlatform ParsePlatform(int value)
        {
            return Enum.IsDefined(typeof(BurtXGIProbeBakingPlatform), value)
                ? (BurtXGIProbeBakingPlatform)value
                : BurtXGIProbeBakingPlatform.PC;
        }

        private static BurtXGIProbeStreamerType ParseStreamerType(int value)
        {
            return Enum.IsDefined(typeof(BurtXGIProbeStreamerType), value)
                ? (BurtXGIProbeStreamerType)value
                : BurtXGIProbeStreamerType.AsyncRead;
        }

        private static BurtGIProbeTimeSlice ParseTimeSlice(int value)
        {
            return BurtGIProbeTimeSliceUtility.TryParseXRenderValue(value, out var slice)
                ? slice
                : BurtGIProbeTimeSlice.Day;
        }

        private static string ResolveLegacySceneName(string sourcePath, Dictionary<string, StreamingAssetInfo> streamingAssets)
        {
            foreach (var pair in streamingAssets)
            {
                if (string.IsNullOrEmpty(pair.Value.assetPath))
                {
                    continue;
                }

                var parts = pair.Value.assetPath.Replace('\\', '/').Split('/');
                if (parts.Length >= 4 && (parts[0] == "xrenderPC" || parts[0] == "xrenderMobile") && parts[2] == "XGIProbe")
                {
                    return parts[1];
                }
            }

            return Path.GetFileNameWithoutExtension(sourcePath);
        }

        private static bool TryReadIntField(string line, string fieldName, out int value)
        {
            value = default;
            return TryReadStringField(line, fieldName, out var text) && int.TryParse(text, out value);
        }

        private static bool TryReadFloatField(string line, string fieldName, out float value)
        {
            value = default;
            return TryReadStringField(line, fieldName, out var text) && float.TryParse(text, out value);
        }

        private static bool TryReadVector3Field(string line, string fieldName, out Vector3 value)
        {
            value = default;
            if (!TryReadStringField(line, fieldName, out var text))
            {
                return false;
            }

            return TryReadYamlFloat(text, "x", out value.x) &&
                TryReadYamlFloat(text, "y", out value.y) &&
                TryReadYamlFloat(text, "z", out value.z);
        }

        private static bool TryReadVector3IntField(string line, string fieldName, out Vector3Int value)
        {
            value = default;
            if (!TryReadStringField(line, fieldName, out var text))
            {
                return false;
            }

            if (!TryReadYamlInt(text, "x", out var x) ||
                !TryReadYamlInt(text, "y", out var y) ||
                !TryReadYamlInt(text, "z", out var z))
            {
                return false;
            }

            value = new Vector3Int(x, y, z);
            return true;
        }

        private static bool TryReadStringField(string line, string fieldName, out string value)
        {
            value = null;
            if (line == null)
            {
                return false;
            }

            var trimmed = line.Trim();
            var listPrefix = "- " + fieldName + ":";
            var prefix = fieldName + ":";
            if (trimmed.StartsWith(listPrefix, StringComparison.Ordinal))
            {
                value = trimmed.Substring(listPrefix.Length).Trim().Trim('"');
                return true;
            }

            if (!trimmed.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            value = trimmed.Substring(prefix.Length).Trim().Trim('"');
            return true;
        }

        private static bool TryReadYamlInt(string text, string key, out int value)
        {
            value = default;
            var pattern = key + ":";
            var index = text.IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            index += pattern.Length;
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            var end = index;
            while (end < text.Length && (char.IsDigit(text[end]) || text[end] == '-')) end++;
            return int.TryParse(text.Substring(index, end - index), out value);
        }

        private static bool TryReadYamlFloat(string text, string key, out float value)
        {
            value = default;
            var pattern = key + ":";
            var index = text.IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0)
            {
                return false;
            }

            index += pattern.Length;
            while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
            var end = index;
            while (end < text.Length &&
                (char.IsDigit(text[end]) || text[end] == '-' || text[end] == '+' ||
                    text[end] == '.' || text[end] == 'e' || text[end] == 'E'))
            {
                end++;
            }

            return float.TryParse(text.Substring(index, end - index), out value);
        }

        private static bool IsSequenceField(string line, string fieldName)
        {
            return IsFieldLine(line, fieldName);
        }

        private static bool IsFieldLine(string line, string fieldName)
        {
            return line != null && line.TrimStart().StartsWith(fieldName + ":", StringComparison.Ordinal);
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

        private static bool TryDecompressZstd(byte[] compressedBytes, int expectedByteLength, out byte[] decompressedBytes, out string error)
        {
            decompressedBytes = null;
            error = null;
            var decompressorType = ResolveZstdDecompressorType();
            if (decompressorType == null)
            {
                error = "ZstdSharp.Decompressor type was not resolved";
                return false;
            }

            var constructor = decompressorType?.GetConstructor(Type.EmptyTypes);
            var unwrap = decompressorType?.GetMethod(
                "Unwrap",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(byte[]), typeof(int), typeof(int), typeof(byte[]), typeof(int), typeof(int) },
                null);
            if (constructor == null)
            {
                error = "ZstdSharp.Decompressor default constructor was not found in " + decompressorType.Assembly.FullName;
                return false;
            }

            if (unwrap == null)
            {
                error = "ZstdSharp.Decompressor.Unwrap(byte[], int, int, byte[], int, int) was not found in " + decompressorType.Assembly.FullName;
                return false;
            }

            if (expectedByteLength <= 0)
            {
                error = "expected decompressed byte length is invalid: " + expectedByteLength;
                return false;
            }

            object decompressor = null;
            try
            {
                decompressor = constructor.Invoke(null);
                var destination = new byte[expectedByteLength];
                var decodedLength = (int)unwrap.Invoke(
                    decompressor,
                    new object[] { compressedBytes, 0, compressedBytes.Length, destination, 0, destination.Length });
                if (decodedLength != expectedByteLength)
                {
                    error = "decoded length mismatch: decoded=" + decodedLength + ", expected=" + expectedByteLength;
                    return false;
                }

                decompressedBytes = destination;
                return true;
            }
            catch (TargetInvocationException exception)
            {
                error = exception.InnerException != null
                    ? exception.InnerException.GetType().Name + ": " + exception.InnerException.Message
                    : exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            catch (Exception exception)
            {
                error = exception.GetType().Name + ": " + exception.Message;
                return false;
            }
            finally
            {
                (decompressor as IDisposable)?.Dispose();
            }
        }

        private static string FormatBytePrefix(byte[] bytes, int count)
        {
            if (bytes == null || bytes.Length == 0 || count <= 0)
            {
                return string.Empty;
            }

            var actualCount = Mathf.Min(bytes.Length, count);
            var parts = new string[actualCount];
            for (var index = 0; index < actualCount; index++)
            {
                parts[index] = bytes[index].ToString("X2");
            }

            return string.Join("-", parts);
        }

        private static Type ResolveZstdDecompressorType()
        {
            var type = Type.GetType("ZstdSharp.Decompressor, ZstdSharp");
            if (type != null)
            {
                return type;
            }

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var index = 0; index < assemblies.Length; index++)
            {
                type = assemblies[index].GetType("ZstdSharp.Decompressor", false);
                if (type != null)
                {
                    return type;
                }
            }

            try
            {
                type = Assembly.Load("ZstdSharp").GetType("ZstdSharp.Decompressor", false);
                if (type != null)
                {
                    return type;
                }
            }
            catch
            {
                // Fall through to explicit plugin path loading for batch-mode import.
            }

            var pluginPath = Path.Combine(Application.dataPath, "BurtRP", "Runtime", "Plugins", "ZstdSharp.dll");
            if (!File.Exists(pluginPath))
            {
                return null;
            }

            try
            {
                return Assembly.LoadFile(pluginPath).GetType("ZstdSharp.Decompressor", false);
            }
            catch
            {
                return null;
            }
        }

        private static string TrimForDialog(string report)
        {
            const int maxLength = 1800;
            return string.IsNullOrEmpty(report) || report.Length <= maxLength
                ? report
                : report.Substring(0, maxLength) + "\n...\nFull report was written to the Console.";
        }

        private struct LegacySource
        {
            public string sourcePath;
            public string sceneGuid;
            public string sceneName;
            public BurtXGIProbeBakingPlatform platform;
            public Vector3 probeOffset;
            public float minDistanceBetweenProbes;
            public int simplificationLevels;
            public BurtXGIProbeStreamerType streamerType;
            public int chunkSizeInBricks;
            public Vector3Int minCellPosition;
            public Vector3Int maxCellPosition;
            public Bounds globalBounds;
            public Vector3 bakedProbeOffset;
            public float bakedMinDistanceBetweenProbes;
            public int bakedSimplificationLevels;
            public BurtXGIProbeStreamerType bakedStreamerType;
            public bool bakedUseTimeSlice;
            public BurtGIProbeTimeSlice timeSliceType;
            public float bakedTimeSliceMainLightIntensity;
            public bool bakedSkyVisibility;
            public bool bakedSkyShadingDirection;
            public List<LegacyCellDesc> cells;
            public Dictionary<string, StreamingAssetInfo> streamingAssets;
            public int l0ChunkSize;
            public int l1ChunkSize;
            public int l2TextureChunkSize;
            public int sharedSkyVisibilityL0L1ChunkSize;
            public int sharedSkyShadingDirectionIndicesChunkSize;

            public bool HasSharedSkyVisibility => sharedSkyVisibilityL0L1ChunkSize > 0;
            public bool HasSharedSkyShadingDirection => sharedSkyShadingDirectionIndicesChunkSize > 0;
        }

        private struct LegacyCellDesc
        {
            public Vector3Int position;
            public int index;
            public int minSubdiv;
            public int bricksCount;
            public int probeCount;
            public int shChunkCount;
        }

        private struct StreamingAssetInfo
        {
            public string label;
            public string assetPath;
            public int elementSize;
            public Dictionary<int, StreamableCellDesc> descs;
        }

        private struct StreamableCellDesc
        {
            public int offset;
            public int elementCount;
            public int compressedSize;
        }

        private struct LegacyCellChunkData
        {
            public byte[] timeSliceBytes;
            public byte[] optionalBytes;
            public byte[] sharedBytes;
            public int l0ChunkSize;
            public int l1ChunkSize;
            public int l2TextureChunkSize;
            public int sharedSkyVisibilityL0L1ChunkSize;
            public int sharedSkyShadingDirectionIndicesChunkSize;
        }
    }
}
