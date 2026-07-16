using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    internal static class BurtXGIResourceValidationMenu
    {
        private const string MenuPath = "BurtRP/Diagnostics/Validate XGI Resources";
        private const string ProbeBakeSceneMenuPath = "BurtRP/Diagnostics/Validate XGI Probe Baking Scene";
        private const string CreateOrRefreshSceneBindingMenuPath = "BurtRP/XGI/Create Or Refresh Probe Scene Binding";
        private const string ProbeBakeMenuPath = "BurtRP/XGI/Bake Probe Data";
        private const string ProbeBakeAllTimeSlicesMenuPath = "BurtRP/XGI/Bake All Probe Time Slices";
        private const string ValidationAssetFolder = "Assets/BurtRP/XGIImportValidation";
        private const string DefaultLegacyImportConfigPath = ValidationAssetFolder + "/Level_BW_Novice_LegacyImport.asset";
        private const string DefaultLegacyImportBakedAssetPath = ValidationAssetFolder + "/Level_BW_Novice_LegacyImport_LegacyImportedBakedData.asset";
        private static readonly BurtGIProbeTimeSlice[] ValidationAllTimeSlices =
        {
            BurtGIProbeTimeSlice.Morning,
            BurtGIProbeTimeSlice.Day,
            BurtGIProbeTimeSlice.Sunset,
            BurtGIProbeTimeSlice.Night
        };
        private static readonly BurtShadingDebugMode[] ValidationXGIDebugModes =
        {
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRadianceCacheStats,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRadianceCacheVisualize,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRadianceCacheStatus,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationScreenProbeTraceVisualize,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationSceneVoxelOccupancy,
            BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHashGridDebug
        };

        [MenuItem(MenuPath, false, 2400)]
        private static void ValidateXGIResources()
        {
            var report = BurtScreenSpaceGlobalIlluminationDiagnosticsUtility.ResolveXGIResourceStatusReport();
            var hasIssue = ContainsStatusIssue(report);
            if (hasIssue)
            {
                Debug.LogWarning(report);
            }
            else
            {
                Debug.Log(report);
            }

            EditorUtility.DisplayDialog(
                "BurtRP XGI Resources",
                report,
                hasIssue ? "Needs Attention" : "OK");
        }

        public static void ValidateXGIResourcesFromCommandLine()
        {
            var report = AppendXGIEditorValidationCommands(
                BurtScreenSpaceGlobalIlluminationDiagnosticsUtility.ResolveXGIResourceStatusReport());
            var hasIssue = ContainsStatusIssue(report);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGILightComponentCoverageFromCommandLine()
        {
            var report = ValidateXGILightComponentCoverage(out var hasIssue);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGILightSettingsMappingFromCommandLine()
        {
            var report = ValidateXGILightSettingsMapping(out var hasIssue);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGIProbeRuntimeSettingsMappingFromCommandLine()
        {
            var report = ValidateXGIProbeRuntimeSettingsMapping(out var hasIssue);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGIProbeLegacyConfigImportGuidanceFromCommandLine()
        {
            var report = ValidateXGIProbeLegacyConfigImportGuidance(out var hasIssue);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGIControlSurfaceFromCommandLine()
        {
            var reports = new List<string>();
            var hasIssue = false;

            reports.Add(ValidateXGILightComponentCoverage(out var lightCoverageIssue));
            hasIssue |= lightCoverageIssue;

            reports.Add(ValidateXGILightSettingsMapping(out var lightSettingsIssue));
            hasIssue |= lightSettingsIssue;

            reports.Add(ValidateXGILightLegacyTraceTypeMigration(out var legacyTraceMigrationIssue));
            hasIssue |= legacyTraceMigrationIssue;

            reports.Add(ValidateXGIProbeRuntimeSettingsMapping(out var probeRuntimeSettingsIssue));
            hasIssue |= probeRuntimeSettingsIssue;

            reports.Add(ValidateXGIProbeLegacyConfigImportGuidance(out var probeLegacyImportGuidanceIssue));
            hasIssue |= probeLegacyImportGuidanceIssue;

            reports.Add(BurtXGIProbeBakingWindow.ValidateXGIToolsApply(out var toolsApplyIssue));
            hasIssue |= toolsApplyIssue;

            reports.Add(BurtGIProbeVolumeEditor.ValidateXGIProbeGizmoSettings(out var probeGizmoIssue));
            hasIssue |= probeGizmoIssue;

            var report = "Burt XGI control surface validation completed.\n" +
                "LightCoverageIssue=" + lightCoverageIssue + "\n" +
                "LightSettingsIssue=" + lightSettingsIssue + "\n" +
                "LegacyTraceMigrationIssue=" + legacyTraceMigrationIssue + "\n" +
                "ProbeRuntimeSettingsIssue=" + probeRuntimeSettingsIssue + "\n" +
                "ProbeLegacyImportGuidanceIssue=" + probeLegacyImportGuidanceIssue + "\n" +
                "ToolsApplyIssue=" + toolsApplyIssue + "\n" +
                "ProbeGizmoIssue=" + probeGizmoIssue + "\n\n" +
                string.Join("\n\n", reports);
            if (hasIssue)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGIProbeBakingWorkflowFromCommandLine()
        {
            BurtXGIProbeBakingConfig config = null;
            Texture3D irradiance = null;
            string tempScenePath = null;
            var cleanedUp = false;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                tempScenePath = CreateTemporaryValidationSceneAsset(scene);
                config = CreateSmokeBakingConfig();
                irradiance = CreateSmokeIrradianceTexture();
                var volume = CreateSmokeProbeVolume(irradiance);
                var physicalPool = CreateSmokePhysicalPool(volume);
                var physicalPoolInitialized = physicalPool.InitializePool();
                var validation = BurtXGIProbeBakeAPI.ValidateScene(config);
                var binding = BurtXGIProbeSceneBindingUtility.CreateOrRefresh(scene, config, null, false, false);
                var bindingValid = binding != null &&
                    binding.GetActiveBakingConfig() == config &&
                    binding.probeVolume != null &&
                    binding.physicalPool != null &&
                    binding.streamer != null;
                var configHasSceneBakeData = HasUpdatedSceneBakeData(config, scene);
                var report = "Burt XGI probe baking workflow validation completed.\n" +
                    "HasBakingConfig=" + validation.hasBakingConfig + "\n" +
                    "ProbeVolumes=" + validation.probeVolumeCount + ",Ready=" + validation.readyProbeVolumeCount + "\n" +
                    "PhysicalPools=" + validation.physicalPoolCount + ",Initialized=" + validation.initializedPhysicalPoolCount + ",InitCall=" + physicalPoolInitialized + "\n" +
                    "RuntimeProbeData=" + validation.HasRuntimeProbeData + "\n" +
                    "SceneBakeDataUpdated=" + configHasSceneBakeData + "\n" +
                    "SceneBakeDataEntries=" + DescribeSceneBakeData(config) + "\n" +
                    "SceneBindingValid=" + bindingValid + "\n" +
                    validation.report;
                if (!validation.hasBakingConfig ||
                    !validation.hasProbeVolume ||
                    !validation.hasReadyProbeVolume ||
                    !validation.HasRuntimeProbeData ||
                    !physicalPoolInitialized ||
                    !configHasSceneBakeData ||
                    !bindingValid ||
                    ContainsStatusIssue(validation.report))
                {
                    Debug.LogError("Burt XGI probe baking workflow validation failed.\n" + report);
                    CleanupProbeBakingWorkflowValidation(ref config, ref irradiance, tempScenePath);
                    cleanedUp = true;
                    EditorApplication.Exit(3);
                    return;
                }

                Debug.Log(report);
                CleanupProbeBakingWorkflowValidation(ref config, ref irradiance, tempScenePath);
                cleanedUp = true;
                EditorApplication.Exit(0);
            }
            finally
            {
                if (!cleanedUp)
                {
                    CleanupProbeBakingWorkflowValidation(ref config, ref irradiance, tempScenePath);
                }
            }
        }

        public static void ValidateXGIProbeBakeChainFromCommandLine()
        {
            ValidateXGIProbeBakeChainFromCommandLine(false);
        }

        public static void ValidateXGIProbeBakeChainTimeSliceFromCommandLine()
        {
            ValidateXGIProbeBakeChainFromCommandLine(true);
        }

        public static void ValidateXGIProbeBakeAllTimeSlicesFromCommandLine()
        {
            BurtXGIProbeBakingConfig config = null;
            Texture3D irradiance = null;
            string tempScenePath = null;
            string tempConfigPath = null;
            var cleanedUp = false;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                tempScenePath = CreateTemporaryValidationSceneAsset(scene);
                CreateSmokeGeometry();
                CreateSmokeTimeOfDayController();
                config = CreateSmokeBakingConfig();
                ConfigureBakeChainSmokeConfig(config, true);
                config.name = "__BurtXGIProbeBakeAllTimeSlicesSmoke";
                tempConfigPath = CreateTemporaryValidationConfigAsset(config);
                irradiance = CreateSmokeIrradianceTexture();
                CreateSmokeProbeVolume(irradiance);

                var progressLog = new List<string>();
                BurtXGIProbeBakeAPI.BakeResult bakeResult = default;
                var bakeCompleted = false;
                var bakeStarted = BurtXGIProbeBakeAPI.BakeAllTimeSlicesAsync(
                    config,
                    progress => progressLog.Add(progress.stepName + "=" + progress.progress.ToString("0.###") + "\n" + progress.description),
                    result =>
                    {
                        bakeCompleted = true;
                        bakeResult = result;
                    });
                var generatedAssetPaths = CollectGeneratedBakedAssetPaths(config);
                var allSlicesValid = ValidateAllTimeSliceBakedAssets(config, out var sliceReport);
                var runtimeBindingValid = ValidateAllTimeSliceRuntimeBinding(scene, config, out var runtimeBindingReport);
                var bakeValid = bakeStarted &&
                    bakeCompleted &&
                    bakeResult.status == BurtXGIProbeBakeAPI.BakeStatus.Success &&
                    allSlicesValid &&
                    runtimeBindingValid;
                var report = "Burt XGI probe bake all time slices validation completed.\n" +
                    "BakeStarted=" + bakeStarted + "\n" +
                    "BakeCompleted=" + bakeCompleted + "\n" +
                    "BakeStatus=" + bakeResult.status + "\n" +
                    "BakeError=" + bakeResult.error + "\n" +
                    "AllSlicesValid=" + allSlicesValid + "\n" +
                    "RuntimeBindingValid=" + runtimeBindingValid + "\n" +
                    "GeneratedAssets=" + string.Join("|", generatedAssetPaths) + "\n" +
                    sliceReport + "\n" +
                    runtimeBindingReport + "\n" +
                    string.Join("\n", progressLog);
                if (!bakeValid)
                {
                    Debug.LogError("Burt XGI probe bake all time slices validation failed.\n" + report);
                    CleanupProbeBakeChainValidation(ref config, ref irradiance, tempScenePath, tempConfigPath, generatedAssetPaths);
                    cleanedUp = true;
                    EditorApplication.Exit(3);
                    return;
                }

                Debug.Log(report);
                CleanupProbeBakeChainValidation(ref config, ref irradiance, tempScenePath, tempConfigPath, generatedAssetPaths);
                cleanedUp = true;
                EditorApplication.Exit(0);
            }
            finally
            {
                if (!cleanedUp)
                {
                    CleanupProbeBakeChainValidation(ref config, ref irradiance, tempScenePath, tempConfigPath, CollectGeneratedBakedAssetPaths(config));
                }
            }
        }

        private static void ValidateXGIProbeBakeChainFromCommandLine(bool requireTimeSlice)
        {
            BurtXGIProbeBakingConfig config = null;
            Texture3D irradiance = null;
            string tempScenePath = null;
            string tempConfigPath = null;
            string bakedAssetPath = null;
            var cleanedUp = false;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                tempScenePath = CreateTemporaryValidationSceneAsset(scene);
                CreateSmokeGeometry();
                config = CreateSmokeBakingConfig();
                ConfigureBakeChainSmokeConfig(config, requireTimeSlice);
                tempConfigPath = CreateTemporaryValidationConfigAsset(config);
                irradiance = CreateSmokeIrradianceTexture();
                CreateSmokeProbeVolume(irradiance);
                var progressLog = new List<string>();
                BurtXGIProbeBakeAPI.BakeResult bakeResult = default;
                var bakeCompleted = false;
                var bakeStarted = BurtXGIProbeBakeAPI.BakeAsync(
                    config,
                    progress => progressLog.Add(progress.stepName + "=" + progress.progress.ToString("0.###") + "\n" + progress.description),
                    result =>
                    {
                        bakeCompleted = true;
                        bakeResult = result;
                    });
                var bakedAsset = config.bakedDataAsset;
                bakedAssetPath = bakedAsset != null ? AssetDatabase.GetAssetPath(bakedAsset) : null;
                var bakeValid = bakeStarted &&
                    bakeCompleted &&
                    bakeResult.status == BurtXGIProbeBakeAPI.BakeStatus.Success &&
                    bakedAsset != null &&
                    bakedAsset.cellCount > 0 &&
                    bakedAsset.chunkCount > 0 &&
                    bakedAsset.pageTableEntryCount > 0 &&
                    bakedAsset.indirectionEntryCount > 0;
                var timeSliceValid = !requireTimeSlice ||
                    bakedAsset != null &&
                    bakedAsset.hasTimeSliceSH &&
                    bakedAsset.timeSliceType == BurtGIProbeTimeSlice.Day &&
                    config.bakedTimeSliceSHCount >= config.bakedProbeCount;
                bakeValid &= timeSliceValid;
                var report = "Burt XGI probe bake chain validation completed.\n" +
                    "RequireTimeSlice=" + requireTimeSlice + "\n" +
                    "BakeStarted=" + bakeStarted + "\n" +
                    "BakeCompleted=" + bakeCompleted + "\n" +
                    "BakeStatus=" + bakeResult.status + "\n" +
                    "BakeError=" + bakeResult.error + "\n" +
                    "BakedAsset=" + (string.IsNullOrEmpty(bakedAssetPath) ? "<none>" : bakedAssetPath) + "\n" +
                    "Cells=" + (bakedAsset != null ? bakedAsset.cellCount : -1) +
                    ",Chunks=" + (bakedAsset != null ? bakedAsset.chunkCount : -1) +
                    ",PageTable=" + (bakedAsset != null ? bakedAsset.pageTableEntryCount : -1) +
                    ",Indirection=" + (bakedAsset != null ? bakedAsset.indirectionEntryCount : -1) + "\n" +
                    "TimeSliceValid=" + timeSliceValid +
                    ",AssetHasTimeSliceSH=" + (bakedAsset != null && bakedAsset.hasTimeSliceSH) +
                    ",AssetTimeSlice=" + (bakedAsset != null ? bakedAsset.timeSliceType.ToString() : "<none>") +
                    ",ConfigTimeSliceSH=" + (config != null ? config.bakedTimeSliceSHCount : -1) +
                    ",BakedProbes=" + (config != null ? config.bakedProbeCount : -1) + "\n" +
                    string.Join("\n", progressLog);
                if (!bakeValid)
                {
                    Debug.LogError("Burt XGI probe bake chain validation failed.\n" + report);
                    CleanupProbeBakeChainValidation(ref config, ref irradiance, tempScenePath, tempConfigPath, bakedAssetPath);
                    cleanedUp = true;
                    EditorApplication.Exit(3);
                    return;
                }

                Debug.Log(report);
                CleanupProbeBakeChainValidation(ref config, ref irradiance, tempScenePath, tempConfigPath, bakedAssetPath);
                cleanedUp = true;
                EditorApplication.Exit(0);
            }
            finally
            {
                if (!cleanedUp)
                {
                    CleanupProbeBakeChainValidation(ref config, ref irradiance, tempScenePath, tempConfigPath, bakedAssetPath);
                }
            }
        }

        private static void CleanupProbeBakingWorkflowValidation(
            ref BurtXGIProbeBakingConfig config,
            ref Texture3D irradiance,
            string tempScenePath)
        {
            if (irradiance != null)
            {
                UnityEngine.Object.DestroyImmediate(irradiance);
                irradiance = null;
            }

            if (config != null)
            {
                UnityEngine.Object.DestroyImmediate(config);
                config = null;
            }

            if (string.IsNullOrEmpty(tempScenePath))
            {
                return;
            }

            AssetDatabase.DeleteAsset(tempScenePath);
            FileUtil.DeleteFileOrDirectory(tempScenePath);
            FileUtil.DeleteFileOrDirectory(tempScenePath + ".meta");
            AssetDatabase.Refresh();
        }

        private static string CreateTemporaryValidationSceneAsset(Scene scene)
        {
            EnsureValidationAssetFolder();

            var path = ValidationAssetFolder + "/__BurtXGIProbeBakingWorkflowSmoke.unity";
            AssetDatabase.DeleteAsset(path);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                return null;
            }

            return path;
        }

        private static string CreateTemporaryValidationConfigAsset(BurtXGIProbeBakingConfig config)
        {
            EnsureValidationAssetFolder();
            var path = ValidationAssetFolder + "/__BurtXGIProbeBakeChainSmoke.asset";
            DeleteValidationAsset(path);
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static void ConfigureBakeChainSmokeConfig(BurtXGIProbeBakingConfig config, bool enableTimeSlice)
        {
            if (config == null)
            {
                return;
            }

            config.name = enableTimeSlice
                ? "__BurtXGIProbeBakeChainTimeSliceSmoke"
                : "__BurtXGIProbeBakeChainSmoke";
            config.useTimeSliceData = enableTimeSlice;
            config.timeSliceType = BurtGIProbeTimeSlice.Day;
            config.skyVisibilityBakingSamples = 4;
            config.skyVisibilitySampleCountPerStep = 4;
            config.skyVisibilityBakingBounces = 1;
            config.timeSliceBakingSamples = 4;
            config.timeSliceSampleCountPerStep = 4;
            config.timeSliceBakingBounces = 1;
        }

        private static void CleanupProbeBakeChainValidation(
            ref BurtXGIProbeBakingConfig config,
            ref Texture3D irradiance,
            string tempScenePath,
            string tempConfigPath,
            string bakedAssetPath)
        {
            if (irradiance != null)
            {
                UnityEngine.Object.DestroyImmediate(irradiance);
                irradiance = null;
            }

            DeleteValidationAsset(bakedAssetPath);
            DeleteValidationAsset(tempConfigPath);
            DeleteValidationAsset(tempScenePath);
            config = null;
            AssetDatabase.Refresh();
        }

        private static void CleanupProbeBakeChainValidation(
            ref BurtXGIProbeBakingConfig config,
            ref Texture3D irradiance,
            string tempScenePath,
            string tempConfigPath,
            IReadOnlyList<string> bakedAssetPaths)
        {
            if (irradiance != null)
            {
                UnityEngine.Object.DestroyImmediate(irradiance);
                irradiance = null;
            }

            if (bakedAssetPaths != null)
            {
                for (var index = 0; index < bakedAssetPaths.Count; index++)
                {
                    DeleteValidationAsset(bakedAssetPaths[index]);
                }
            }

            DeleteValidationAsset(tempConfigPath);
            DeleteValidationAsset(tempScenePath);
            config = null;
            AssetDatabase.Refresh();
        }

        private static List<string> CollectGeneratedBakedAssetPaths(BurtXGIProbeBakingConfig config)
        {
            var paths = new List<string>();
            if (config == null)
            {
                return paths;
            }

            AddAssetPath(paths, config.bakedDataAsset);
            if (config.timeSliceBakedDataAssets != null)
            {
                for (var index = 0; index < config.timeSliceBakedDataAssets.Count; index++)
                {
                    AddAssetPath(paths, config.timeSliceBakedDataAssets[index]?.asset);
                }
            }

            return paths;
        }

        private static void AddAssetPath(List<string> paths, UnityEngine.Object asset)
        {
            if (asset == null || paths == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
            {
                paths.Add(path);
            }
        }

        private static bool ValidateAllTimeSliceBakedAssets(BurtXGIProbeBakingConfig config, out string report)
        {
            var lines = new List<string>();
            var valid = config != null;
            for (var index = 0; index < ValidationAllTimeSlices.Length; index++)
            {
                var slice = ValidationAllTimeSlices[index];
                BurtXGIProbeBakedDataAsset asset = null;
                var hasAsset = config != null && config.TryGetBakedDataAssetForTimeSlice(slice, out asset);
                var sliceValid = hasAsset &&
                    asset != null &&
                    asset.hasTimeSliceSH &&
                    asset.timeSliceType == slice &&
                    asset.cellCount > 0 &&
                    asset.chunkCount > 0 &&
                    asset.pageTableEntryCount > 0 &&
                    asset.indirectionEntryCount > 0;
                valid &= sliceValid;
                lines.Add("Slice=" + slice +
                    ",Valid=" + sliceValid +
                    ",Asset=" + (asset != null ? AssetDatabase.GetAssetPath(asset) : "<none>") +
                    ",HasTimeSliceSH=" + (asset != null && asset.hasTimeSliceSH) +
                    ",Cells=" + (asset != null ? asset.cellCount : -1) +
                    ",Chunks=" + (asset != null ? asset.chunkCount : -1) +
                    ",PageTable=" + (asset != null ? asset.pageTableEntryCount : -1) +
                    ",Indirection=" + (asset != null ? asset.indirectionEntryCount : -1));
            }

            report = string.Join("\n", lines);
            return valid;
        }

        private static bool ValidateAllTimeSliceRuntimeBinding(
            Scene scene,
            BurtXGIProbeBakingConfig config,
            out string report)
        {
            var lines = new List<string>();
            if (config == null || !scene.IsValid())
            {
                report = "RuntimeBinding=Invalid(ConfigOrSceneMissing)";
                return false;
            }

            config.TryGetBakedDataAssetForTimeSlice(BurtGIProbeTimeSlice.Day, out var dayAsset);
            var binding = BurtXGIProbeSceneBindingUtility.CreateOrRefresh(scene, config, dayAsset, false, false);
            binding?.ApplyConfiguration();
            var streamer = binding != null ? binding.streamer : null;
            var entries = streamer != null ? streamer.timeSliceBakedDataAssets : null;
            var valid = binding != null &&
                streamer != null &&
                streamer.HasTimeSliceBakedDataAssets &&
                CountValidTimeSliceEntries(entries) >= ValidationAllTimeSlices.Length;

            lines.Add("RuntimeBinding=" + (binding != null ? binding.GetDebugStatus() : "<none>"));
            lines.Add("RuntimeStreamer=" + (streamer != null
                ? "HasTimeSlices=" + streamer.HasTimeSliceBakedDataAssets +
                    ",EntryCount=" + CountValidTimeSliceEntries(entries) +
                    ",SceneGuid=" + (string.IsNullOrEmpty(streamer.streamingSceneGuid) ? "<none>" : streamer.streamingSceneGuid)
                : "<none>"));

            var previousSlice = BurtGIProbeVolume.ActiveTimeSlice;
            try
            {
                for (var index = 0; index < ValidationAllTimeSlices.Length; index++)
                {
                    var slice = ValidationAllTimeSlices[index];
                    config.TryGetBakedDataAssetForTimeSlice(slice, out var expectedAsset);
                    BurtGIProbeVolume.SetActiveTimeSlice(slice);
                    var activeAsset = streamer != null ? streamer.ActiveBakedDataAsset : null;
                    var registeredAsset = FindRegisteredTimeSliceAsset(entries, slice);
                    var sliceValid = expectedAsset != null &&
                        registeredAsset == expectedAsset &&
                        activeAsset == expectedAsset;
                    valid &= sliceValid;
                    lines.Add("RuntimeSlice=" + slice +
                        ",Valid=" + sliceValid +
                        ",Expected=" + (expectedAsset != null ? expectedAsset.name : "<none>") +
                        ",Registered=" + (registeredAsset != null ? registeredAsset.name : "<none>") +
                        ",Active=" + (activeAsset != null ? activeAsset.name : "<none>"));
                }
            }
            finally
            {
                BurtGIProbeVolume.SetActiveTimeSlice(previousSlice);
            }

            report = string.Join("\n", lines);
            return valid;
        }

        private static int CountValidTimeSliceEntries(
            List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> entries)
        {
            if (entries == null)
            {
                return 0;
            }

            var count = 0;
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index]?.asset != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static BurtXGIProbeBakedDataAsset FindRegisteredTimeSliceAsset(
            List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> entries,
            BurtGIProbeTimeSlice slice)
        {
            if (entries == null)
            {
                return null;
            }

            var normalizedSlice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(slice);
            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                if (entry != null &&
                    entry.asset != null &&
                    BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice) == normalizedSlice)
                {
                    return entry.asset;
                }
            }

            return null;
        }

        private static void EnsureValidationAssetFolder()
        {
            if (!AssetDatabase.IsValidFolder(ValidationAssetFolder))
            {
                AssetDatabase.CreateFolder("Assets/BurtRP", "XGIImportValidation");
            }
        }

        private static void DeleteValidationAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            AssetDatabase.DeleteAsset(assetPath);
            FileUtil.DeleteFileOrDirectory(assetPath);
            FileUtil.DeleteFileOrDirectory(assetPath + ".meta");
        }

        private static bool HasUpdatedSceneBakeData(BurtXGIProbeBakingConfig config, Scene scene)
        {
            if (config == null || config.sceneBakeData == null || config.sceneBakeData.Count == 0)
            {
                return false;
            }

            var sceneKey = !string.IsNullOrEmpty(config.sceneGuid)
                ? config.sceneGuid
                : !string.IsNullOrEmpty(scene.path)
                    ? scene.path
                    : scene.name;
            for (var index = 0; index < config.sceneBakeData.Count; index++)
            {
                var data = config.sceneBakeData[index];
                if (!HasValidSceneBakeData(data))
                {
                    continue;
                }

                if (string.IsNullOrEmpty(sceneKey) ||
                    string.Equals(data.sceneGuid, sceneKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(data.sceneGuid, scene.name, StringComparison.OrdinalIgnoreCase) ||
                    config.sceneBakeData.Count == 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasValidSceneBakeData(BurtXGIProbeSceneBakeData data)
        {
            return data != null &&
                data.hasProbeVolume &&
                data.bounds.size.sqrMagnitude > 0.0001f;
        }

        private static string DescribeSceneBakeData(BurtXGIProbeBakingConfig config)
        {
            if (config == null || config.sceneBakeData == null)
            {
                return "None";
            }

            if (config.sceneBakeData.Count == 0)
            {
                return "Empty";
            }

            var entries = new List<string>(config.sceneBakeData.Count);
            for (var index = 0; index < config.sceneBakeData.Count; index++)
            {
                var data = config.sceneBakeData[index];
                entries.Add(data == null
                    ? index + ":Null"
                    : index + ":Guid=" + data.sceneGuid +
                        ",Has=" + data.hasProbeVolume +
                        ",Size=" + data.bounds.size);
            }

            return string.Join("|", entries);
        }

        public static void ValidateImportedBakedDataRuntimeFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var assetPath = GetCommandLineValue(args, "-burtXGIBakedAsset");
            var configPath = GetCommandLineValue(args, "-burtXGIConfig");
            var sampleCount = Mathf.Max(1, GetCommandLineInt(args, "-burtXGISampleCount", 3));
            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("Burt XGI imported baked data runtime validation requires -burtXGIBakedAsset <Assets/...asset>.");
                EditorApplication.Exit(2);
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var asset = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakedDataAsset>(assetPath);
            if (asset == null)
            {
                Debug.LogError("Burt XGI imported baked data runtime validation failed: baked asset was not found: " + assetPath);
                EditorApplication.Exit(3);
                return;
            }

            var config = !string.IsNullOrEmpty(configPath)
                ? AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(configPath)
                : asset.sourceConfig;
            var binding = BurtXGIProbeSceneBindingUtility.CreateOrRefresh(scene, config, asset, false, false);
            if (binding == null || binding.probeVolume == null || binding.physicalPool == null || binding.streamer == null)
            {
                Debug.LogError("Burt XGI imported baked data runtime validation failed: scene binding components were not created.");
                EditorApplication.Exit(3);
                return;
            }

            if (asset.perSceneCellLists != null && asset.perSceneCellLists.Count == 1 && !string.IsNullOrEmpty(asset.perSceneCellLists[0].sceneGuid))
            {
                binding.sceneGuid = asset.perSceneCellLists[0].sceneGuid;
            }

            binding.platformMode = BurtGIProbeSceneBinding.PlatformMode.PC;
            binding.bakedDataAsset = asset;
            binding.pcBakedDataAsset = asset;
            if (config != null)
            {
                binding.bakingConfig = config;
                binding.pcBakingConfig = config;
            }

            binding.applyOnEnable = false;
            binding.initializeStreamingOnPlay = false;
            binding.automaticStreaming = false;
            binding.ApplyConfiguration();
            if (asset.HasBakedL2Data && !asset.AllowsRuntimeL2Data)
            {
                Debug.LogError("Burt XGI imported baked data runtime validation failed: baked asset contains L2 data but RuntimeSHBands does not allow L2. RuntimeSHBands=" + asset.RuntimeSHBands);
                EditorApplication.Exit(3);
                return;
            }

            if (!binding.streamer.InitializeStreaming())
            {
                Debug.LogError("Burt XGI imported baked data runtime validation failed: " + binding.streamer.LastStreamingStatus + "\n" + binding.GetDebugStatus());
                EditorApplication.Exit(3);
                return;
            }

            var cellIndex = ResolveFirstRuntimeCellIndex(asset, binding.sceneGuid);
            if (cellIndex == int.MinValue)
            {
                Debug.LogError("Burt XGI imported baked data runtime validation failed: no runtime cell index was found.\n" + BuildImportedBakedDataDiagnostic(asset, binding));
                EditorApplication.Exit(3);
                return;
            }

            var sampledCellIndices = BuildRuntimeCellSamples(asset, binding.sceneGuid, sampleCount);
            if (sampledCellIndices.Count == 0)
            {
                sampledCellIndices.Add(cellIndex);
            }

            for (var sampleIndex = 0; sampleIndex < sampledCellIndices.Count; sampleIndex++)
            {
                var sampledCellIndex = sampledCellIndices[sampleIndex];
                if (!binding.streamer.TryLoadCell(sampledCellIndex))
                {
                    Debug.LogError("Burt XGI imported baked data runtime validation failed: " + binding.streamer.LastStreamingStatus + "\n" + binding.GetDebugStatus());
                    EditorApplication.Exit(3);
                    return;
                }

                if (sampleIndex < sampledCellIndices.Count - 1 && !binding.streamer.TryUnloadCell(sampledCellIndex))
                {
                    Debug.LogError("Burt XGI imported baked data runtime validation failed: " + binding.streamer.LastStreamingStatus + "\n" + binding.GetDebugStatus());
                    EditorApplication.Exit(3);
                    return;
                }
            }

            var report = "Burt XGI imported baked data runtime validation completed.\n" +
                "Asset: " + assetPath + "\n" +
                "Config: " + (config != null ? AssetDatabase.GetAssetPath(config) : "<none>") + "\n" +
                "SceneGuid: " + (string.IsNullOrEmpty(binding.sceneGuid) ? "<none>" : binding.sceneGuid) + "\n" +
                "Cells/Bricks/Probes/Chunks: " + asset.cellCount + "/" + asset.brickCount + "/" + asset.probeCount + "/" + asset.chunkCount + "\n" +
                "RuntimeSHBands: " + asset.RuntimeSHBands + ",HasBakedL2=" + asset.HasBakedL2Data + ",AllowsRuntimeL2=" + asset.AllowsRuntimeL2Data + "\n" +
                "InitializedStatus: Initialized\n" +
                "LoadedCell: " + cellIndex + "\n" +
                "SampledCells: " + string.Join(",", sampledCellIndices) + "\n" +
                "StreamingStatus: " + binding.streamer.LastStreamingStatus + "\n" +
                "LoadedCells/Chunks/SharedChunks: " + binding.streamer.LoadedCellCount + "/" + binding.streamer.LoadedPhysicalChunkCount + "/" + binding.streamer.LoadedSharedChunkCount + "\n" +
                "Pool: " + binding.physicalPool.PhysicalPoolDimensions + ",Capacity=" + binding.physicalPool.ChunkCapacity + ",LastUploadStatus=" + binding.physicalPool.LastUploadStatus;
            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGIRenderSmokeFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var pipelineAssetPath = GetCommandLineValue(args, "-burtPipelineAsset");
            if (string.IsNullOrEmpty(pipelineAssetPath))
            {
                pipelineAssetPath = "Assets/BurtRP/BurtRenderPipelineAsset.asset";
            }

            var configPath = GetCommandLineValue(args, "-burtXGIConfig");
            var bakedAssetPath = GetCommandLineValue(args, "-burtXGIBakedAsset");
            var smokeMode = GetCommandLineValue(args, "-burtXGISmokeMode");
            if (string.IsNullOrEmpty(smokeMode))
            {
                smokeMode = "lite";
            }

            var width = Mathf.Clamp(GetCommandLineInt(args, "-burtXGIRenderWidth", 128), 32, 512);
            var height = Mathf.Clamp(GetCommandLineInt(args, "-burtXGIRenderHeight", 128), 32, 512);
            var shadingDebugMode = ParseShadingDebugMode(GetCommandLineValue(args, "-burtShadingDebugMode"));
            ValidateXGIRenderSmoke(pipelineAssetPath, configPath, bakedAssetPath, smokeMode, width, height, shadingDebugMode);
        }

        public static void ValidateXGILegacyImportedRenderSmokeFromCommandLine()
        {
            ValidateXGIRenderSmoke(
                "Assets/BurtRP/BurtRenderPipelineAsset.asset",
                DefaultLegacyImportConfigPath,
                DefaultLegacyImportBakedAssetPath,
                "expanded",
                128,
                128,
                BurtShadingDebugMode.None);
        }

        public static void ValidateXGIDebugRenderSmokeFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var debugMode = ParseShadingDebugMode(GetCommandLineValue(args, "-burtShadingDebugMode"));
            if (debugMode == BurtShadingDebugMode.None)
            {
                debugMode = BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRadianceCacheStats;
            }

            ValidateXGIRenderSmoke(
                "Assets/BurtRP/BurtRenderPipelineAsset.asset",
                DefaultLegacyImportConfigPath,
                DefaultLegacyImportBakedAssetPath,
                "expanded",
                128,
                128,
                debugMode);
        }

        private static void ValidateXGIRenderSmoke(
            string pipelineAssetPath,
            string configPath,
            string bakedAssetPath,
            string smokeMode,
            int width,
            int height,
            BurtShadingDebugMode shadingDebugMode)
        {
            var result = RunXGIRenderSmoke(pipelineAssetPath, configPath, bakedAssetPath, smokeMode, width, height, shadingDebugMode);
            if (!result.Success)
            {
                Debug.LogError(result.Report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(result.Report);
            EditorApplication.Exit(0);
        }

        public static void ValidateXGIDebugRenderSmokeAllModesFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var requestedModes = ParseShadingDebugModes(GetCommandLineValue(args, "-burtShadingDebugModes"));
            var modes = requestedModes.Count > 0 ? requestedModes : new List<BurtShadingDebugMode>(ValidationXGIDebugModes);
            var reports = new List<string>();
            var failed = false;
            for (var index = 0; index < modes.Count; index++)
            {
                var mode = modes[index];
                var result = RunXGIRenderSmoke(
                    "Assets/BurtRP/BurtRenderPipelineAsset.asset",
                    DefaultLegacyImportConfigPath,
                    DefaultLegacyImportBakedAssetPath,
                    "expanded",
                    128,
                    128,
                    mode);
                reports.Add(result.Report);
                failed |= !result.Success;
                if (!result.Success)
                {
                    break;
                }
            }

            var report = "Burt XGI debug render smoke all modes completed.\n" +
                "Modes: " + string.Join(",", modes) + "\n" +
                string.Join("\n\n", reports);
            if (failed)
            {
                Debug.LogError(report);
                EditorApplication.Exit(3);
                return;
            }

            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        private static BurtXGIRenderSmokeResult RunXGIRenderSmoke(
            string pipelineAssetPath,
            string configPath,
            string bakedAssetPath,
            string smokeMode,
            int width,
            int height,
            BurtShadingDebugMode shadingDebugMode)
        {
            var originalGraphicsPipeline = GraphicsSettings.renderPipelineAsset;
            var originalQualityPipeline = QualitySettings.renderPipeline;
            var originalShadingDebugMode = BurtShadingDebugSettings.Mode;
            RenderTexture renderTexture = null;
            BurtRenderPipelineAsset runtimeAsset = null;
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var pipelineAsset = AssetDatabase.LoadAssetAtPath<BurtRenderPipelineAsset>(pipelineAssetPath);
                if (pipelineAsset == null)
                {
                    return BurtXGIRenderSmokeResult.Failed("Burt XGI render smoke failed: pipeline asset was not found: " + pipelineAssetPath);
                }

                var config = !string.IsNullOrEmpty(configPath)
                    ? AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(configPath)
                    : null;
                var bakedAsset = !string.IsNullOrEmpty(bakedAssetPath)
                    ? AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakedDataAsset>(bakedAssetPath)
                    : config != null
                        ? ResolveBestBakedAsset(config)
                        : null;
                renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf)
                {
                    name = "Burt XGI Render Smoke Target",
                    enableRandomWrite = false
                };

                CreateSmokeGeometry();
                if (IsExpandedSmokeMode(smokeMode))
                {
                    CreateSmokeLocalSkyProbe();
                }

                CreateSmokeVolume(smokeMode);
                CreateSmokeCamera(renderTexture);
                if (bakedAsset != null || config != null)
                {
                    CreateSmokeProbeBinding(scene, config, bakedAsset);
                }

                runtimeAsset = UnityEngine.Object.Instantiate(pipelineAsset);
                runtimeAsset.name = pipelineAsset.name + " XGI Render Smoke";
                SetSerializedValue(runtimeAsset, "rendererMode", (int)BurtRendererMode.Deferred);
                SetSerializedValue(runtimeAsset, "enableRenderGraphDebug", true);
                SetSerializedValue(runtimeAsset, "enableRenderGraphDebugConsoleLog", false);
                GraphicsSettings.renderPipelineAsset = runtimeAsset;
                QualitySettings.renderPipeline = runtimeAsset;
                BurtShadingDebugSettings.Mode = shadingDebugMode;
                BurtRenderGraphDebugClipboardUtility.ClearLatestDump();

                var camera = Camera.main;
                if (camera == null)
                {
                    return BurtXGIRenderSmokeResult.Failed("Burt XGI render smoke failed: smoke camera was not created.");
                }

                camera.Render();
                var imageStats = ValidateXGIRenderSmokeImage(renderTexture);
                if (!string.IsNullOrEmpty(imageStats.Failure))
                {
                    return BurtXGIRenderSmokeResult.Failed("Burt XGI render smoke failed: " + imageStats.Failure + "\n" + imageStats);
                }

                var dump = BurtRenderGraphDebugClipboardUtility.GetLatestDump(BurtRenderRequestType.BaseCamera) ??
                    BurtRenderGraphDebugClipboardUtility.LatestDump;
                var failure = ValidateXGIRenderSmokeDump(dump, smokeMode, shadingDebugMode);
                if (!string.IsNullOrEmpty(failure))
                {
                    return BurtXGIRenderSmokeResult.Failed("Burt XGI render smoke failed: " + failure + "\nSummary=" +
                        BurtRenderGraphDebugClipboardUtility.LatestDumpSummary + "\n" +
                        CreateDumpPreview(dump));
                }

                return BurtXGIRenderSmokeResult.Succeeded("Burt XGI render smoke completed.\n" +
                    "SmokeMode: " + smokeMode + "\n" +
                    "PipelineAsset: " + pipelineAssetPath + "\n" +
                    "Config: " + (!string.IsNullOrEmpty(configPath) ? configPath : "<none>") + "\n" +
                    "BakedAsset: " + (!string.IsNullOrEmpty(bakedAssetPath) ? bakedAssetPath : "<none>") + "\n" +
                    "ShadingDebugMode: " + shadingDebugMode + "\n" +
                    "Target: " + width + "x" + height + "\n" +
                    "ImageStats: " + imageStats + "\n" +
                    "DumpSummary: " + BurtRenderGraphDebugClipboardUtility.LatestDumpSummary);
            }
            catch (Exception exception)
            {
                return BurtXGIRenderSmokeResult.Failed("Burt XGI render smoke failed with exception:\n" + exception);
            }
            finally
            {
                BurtShadingDebugSettings.Mode = originalShadingDebugMode;
                GraphicsSettings.renderPipelineAsset = originalGraphicsPipeline;
                QualitySettings.renderPipeline = originalQualityPipeline;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }

                if (runtimeAsset != null)
                {
                    UnityEngine.Object.DestroyImmediate(runtimeAsset);
                }
            }
        }

        private static BurtShadingDebugMode ParseShadingDebugMode(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return BurtShadingDebugMode.None;
            }

            if (Enum.TryParse(value, true, out BurtShadingDebugMode mode))
            {
                return mode;
            }

            return int.TryParse(value, out var numericMode) &&
                Enum.IsDefined(typeof(BurtShadingDebugMode), numericMode)
                ? (BurtShadingDebugMode)numericMode
                : BurtShadingDebugMode.None;
        }

        private static List<BurtShadingDebugMode> ParseShadingDebugModes(string value)
        {
            var modes = new List<BurtShadingDebugMode>();
            if (string.IsNullOrEmpty(value))
            {
                return modes;
            }

            var parts = value.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < parts.Length; index++)
            {
                var mode = ParseShadingDebugMode(parts[index].Trim());
                if (mode != BurtShadingDebugMode.None && !modes.Contains(mode))
                {
                    modes.Add(mode);
                }
            }

            return modes;
        }

        private static BurtXGIRenderSmokeImageStats ValidateXGIRenderSmokeImage(RenderTexture target)
        {
            if (target == null)
            {
                return BurtXGIRenderSmokeImageStats.Failed("render target was not created.");
            }

            var previous = RenderTexture.active;
            Texture2D readback = null;
            try
            {
                RenderTexture.active = target;
                readback = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
                readback.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0, false);
                readback.Apply(false, false);
                var pixels = readback.GetPixels();
                if (pixels == null || pixels.Length == 0)
                {
                    return BurtXGIRenderSmokeImageStats.Failed("render target readback returned no pixels.");
                }

                var minLuminance = float.PositiveInfinity;
                var maxLuminance = float.NegativeInfinity;
                double sumLuminance = 0.0;
                var finitePixels = 0;
                var nonBlackPixels = 0;
                for (var index = 0; index < pixels.Length; index++)
                {
                    var pixel = pixels[index];
                    if (!IsFinite(pixel.r) || !IsFinite(pixel.g) || !IsFinite(pixel.b) || !IsFinite(pixel.a))
                    {
                        return BurtXGIRenderSmokeImageStats.Failed("render target contains NaN or Inf at pixel " + index + ".");
                    }

                    var luminance = Mathf.Max(0f, pixel.r * 0.2126f + pixel.g * 0.7152f + pixel.b * 0.0722f);
                    minLuminance = Mathf.Min(minLuminance, luminance);
                    maxLuminance = Mathf.Max(maxLuminance, luminance);
                    sumLuminance += luminance;
                    finitePixels++;
                    if (luminance > 0.0001f)
                    {
                        nonBlackPixels++;
                    }
                }

                if (finitePixels <= 0)
                {
                    return BurtXGIRenderSmokeImageStats.Failed("render target contains no finite pixels.");
                }

                var meanLuminance = (float)(sumLuminance / finitePixels);
                var stats = new BurtXGIRenderSmokeImageStats(
                    finitePixels,
                    nonBlackPixels,
                    minLuminance,
                    maxLuminance,
                    meanLuminance,
                    null);
                return nonBlackPixels > 0 && maxLuminance > 0.0001f
                    ? stats
                    : new BurtXGIRenderSmokeImageStats(
                        finitePixels,
                        nonBlackPixels,
                        minLuminance,
                        maxLuminance,
                        meanLuminance,
                        "render target is fully black.");
            }
            finally
            {
                RenderTexture.active = previous;
                if (readback != null)
                {
                    UnityEngine.Object.DestroyImmediate(readback);
                }
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct BurtXGIRenderSmokeResult
        {
            private BurtXGIRenderSmokeResult(bool success, string report)
            {
                Success = success;
                Report = report;
            }

            public bool Success { get; }
            public string Report { get; }

            public static BurtXGIRenderSmokeResult Succeeded(string report)
            {
                return new BurtXGIRenderSmokeResult(true, report);
            }

            public static BurtXGIRenderSmokeResult Failed(string report)
            {
                return new BurtXGIRenderSmokeResult(false, report);
            }
        }

        private readonly struct BurtXGIRenderSmokeImageStats
        {
            public BurtXGIRenderSmokeImageStats(int finitePixels, int nonBlackPixels, float minLuminance, float maxLuminance, float meanLuminance, string failure)
            {
                FinitePixels = finitePixels;
                NonBlackPixels = nonBlackPixels;
                MinLuminance = minLuminance;
                MaxLuminance = maxLuminance;
                MeanLuminance = meanLuminance;
                Failure = failure;
            }

            public int FinitePixels { get; }
            public int NonBlackPixels { get; }
            public float MinLuminance { get; }
            public float MaxLuminance { get; }
            public float MeanLuminance { get; }
            public string Failure { get; }

            public static BurtXGIRenderSmokeImageStats Failed(string failure)
            {
                return new BurtXGIRenderSmokeImageStats(0, 0, 0f, 0f, 0f, failure);
            }

            public override string ToString()
            {
                return "FinitePixels=" + FinitePixels +
                    ",NonBlackPixels=" + NonBlackPixels +
                    ",MinLuma=" + MinLuminance.ToString("0.######") +
                    ",MaxLuma=" + MaxLuminance.ToString("0.######") +
                    ",MeanLuma=" + MeanLuminance.ToString("0.######") +
                    (string.IsNullOrEmpty(Failure) ? string.Empty : ",Failure=" + Failure);
            }
        }

        private static BurtXGIProbeBakingConfig CreateSmokeBakingConfig()
        {
            var config = ScriptableObject.CreateInstance<BurtXGIProbeBakingConfig>();
            config.name = "Burt XGI Smoke Baking Config";
            config.platform = BurtXGIProbeBakingPlatform.PC;
            config.useHardWareRayTracing = false;
            config.useTimeSliceData = false;
            config.skyVisibility = true;
            config.skyVisibilityShadingDirection = false;
            config.minDistanceBetweenProbes = 1f;
            config.simplificationLevels = 2;
            config.systemParameters.enable = true;
            config.systemParameters.shBands = BurtXGIProbeSHBands.SphericalHarmonicsL2;
            return config;
        }

        private static Texture3D CreateSmokeIrradianceTexture()
        {
            var texture = new Texture3D(4, 4, 4, TextureFormat.RGBAHalf, false, true)
            {
                name = "Burt XGI Smoke Baking Irradiance",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[4 * 4 * 4];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = new Color(0.08f, 0.1f, 0.12f, 1f);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static BurtGIProbeVolume CreateSmokeProbeVolume(Texture irradiance)
        {
            var volumeObject = new GameObject("Burt XGI Smoke Baking Probe Volume");
            volumeObject.transform.position = new Vector3(0f, 1f, 0f);
            var volume = volumeObject.AddComponent<BurtGIProbeVolume>();
            volume.mode = BurtGIProbeVolumeMode.Local;
            volume.size = new Vector3(4f, 4f, 4f);
            volume.extent = 2f;
            volume.intensity = 1f;
            volume.irradiance = irradiance;
            return volume;
        }

        private static BurtGIVirtualProbePhysicalPool CreateSmokePhysicalPool(BurtGIProbeVolume volume)
        {
            var poolObject = new GameObject("Burt XGI Smoke Baking Physical Pool");
            var pool = poolObject.AddComponent<BurtGIVirtualProbePhysicalPool>();
            pool.probeVolume = volume;
            pool.chunkDimensions = Vector3Int.one;
            pool.allocateValidity = true;
            pool.allocateSkyVisibility = true;
            pool.allocateSkyShadingDirection = false;
            pool.allocateL2 = true;
            return pool;
        }

        private static void CreateSmokeGeometry()
        {
            var material = CreateSmokeMaterial();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Burt XGI Smoke Floor";
            floor.transform.position = new Vector3(0f, -0.5f, 3f);
            floor.transform.localScale = new Vector3(6f, 0.1f, 6f);
            AssignSharedMaterial(floor, material);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Burt XGI Smoke Cube";
            cube.transform.position = new Vector3(0f, 0.25f, 3f);
            cube.transform.localScale = Vector3.one;
            AssignSharedMaterial(cube, material);

            var lightObject = new GameObject("Burt XGI Smoke Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        private static void CreateSmokeTimeOfDayController()
        {
            var controllerObject = new GameObject("Burt XGI Smoke Time Of Day");
            var controller = controllerObject.AddComponent<BurtGIProbeTimeOfDayController>();
            controller.sourceMode = BurtGIProbeTimeOfDayController.SourceMode.ManualSlice;
            controller.slice = BurtGIProbeTimeSlice.Day;
            controller.priority = 1000;
            controller.updateEveryFrame = false;
            controller.updateInEditMode = true;
            controller.Apply();
        }

        private static void CreateSmokeLocalSkyProbe()
        {
            var probeObject = new GameObject("Burt XGI Smoke Local Sky Probe");
            probeObject.transform.position = new Vector3(0f, 1.2f, 1f);
            var probe = probeObject.AddComponent<BurtLocalSkyProbe>();
            probe.shape = BurtLocalSkyProbeShape.Sphere;
            probe.probeOffsetDistanceMax = 50f;
            probe.probeSampleLerpDistanceMax = 30f;
            probe.intensity = 1f;
            probe.colorCubemap = CreateSmokeCubemap("Burt XGI Smoke Local Sky Color", new Color(0.22f, 0.32f, 0.48f, 1f));
            probe.depthCubemap = CreateSmokeCubemap("Burt XGI Smoke Local Sky Depth", Color.white);
        }

        private static Cubemap CreateSmokeCubemap(string name, Color color)
        {
            var cubemap = new Cubemap(4, TextureFormat.RGBAHalf, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color[cubemap.width * cubemap.height];
            for (var index = 0; index < pixels.Length; index++)
            {
                pixels[index] = color;
            }

            for (var face = CubemapFace.PositiveX; face <= CubemapFace.NegativeZ; face++)
            {
                cubemap.SetPixels(pixels, face);
            }

            cubemap.Apply(false, true);
            return cubemap;
        }

        private static Material CreateSmokeMaterial()
        {
            var shader = Shader.Find("BurtRP/Lit") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return shader != null
                ? new Material(shader) { name = "Burt XGI Smoke Material" }
                : null;
        }

        private static void AssignSharedMaterial(GameObject target, Material material)
        {
            if (target != null && material != null && target.TryGetComponent<Renderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static void CreateSmokeVolume(string smokeMode)
        {
            var fullMode = IsFullSmokeMode(smokeMode);
            var expandedMode = IsExpandedSmokeMode(smokeMode);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Burt XGI Smoke Volume Profile";
            var component = profile.Add<ScreenSpaceGlobalIlluminationVolumeComponent>(true);
            component.enabled.overrideState = true;
            component.enabled.value = true;
            component.quality.overrideState = true;
            component.quality.value = ScreenSpaceGlobalIlluminationQuality.Low;
            component.resolution.overrideState = true;
            component.resolution.value = ScreenSpaceGlobalIlluminationResolution.Half;
            component.temporalAccumulation.overrideState = true;
            component.temporalAccumulation.value = false;
            component.screenProbeLite.overrideState = true;
            component.screenProbeLite.value = true;
            component.screenProbeTemporalFilter.overrideState = true;
            component.screenProbeTemporalFilter.value = false;
            component.screenProbeTemporalReprojection.overrideState = true;
            component.screenProbeTemporalReprojection.value = false;
            component.screenProbeTraceSources.overrideState = true;
            component.screenProbeTraceSources.value = fullMode
                ? expandedMode
                    ? ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.HashGridCache | ScreenProbeTraceSource.VoxelOctree | ScreenProbeTraceSource.RadianceCacheClipMap | ScreenProbeTraceSource.LocalSkyProbe | ScreenProbeTraceSource.SkyCubemap
                    : ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.VoxelOctree | ScreenProbeTraceSource.RadianceCacheClipMap | ScreenProbeTraceSource.SkyCubemap
                : ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.SkyCubemap;
            component.screenProbeTraceHardwareRay.overrideState = true;
            component.screenProbeTraceHardwareRay.value = expandedMode;
            component.screenProbeTraceUseWorldRadianceClipMap.overrideState = true;
            component.screenProbeTraceUseWorldRadianceClipMap.value = fullMode;
            component.screenProbeRadianceCacheType.overrideState = true;
            component.screenProbeRadianceCacheType.value = fullMode ? ScreenProbeRadianceCacheType.ClipMap : ScreenProbeRadianceCacheType.None;
            component.screenProbeRadianceCacheTraceHardwareRay.overrideState = true;
            component.screenProbeRadianceCacheTraceHardwareRay.value = expandedMode;
            component.screenProbeRadianceCacheCalculateIrradiance.overrideState = true;
            component.screenProbeRadianceCacheCalculateIrradiance.value = fullMode;
            component.screenProbeRadianceCacheClipMapCount.overrideState = true;
            component.screenProbeRadianceCacheClipMapCount.value = fullMode ? 1 : component.screenProbeRadianceCacheClipMapCount.value;
            component.screenProbeRadianceCacheClipMapResolution.overrideState = true;
            component.screenProbeRadianceCacheClipMapResolution.value = fullMode ? 16 : component.screenProbeRadianceCacheClipMapResolution.value;
            component.screenProbeRadianceCacheClipMapWorldExtent.overrideState = true;
            component.screenProbeRadianceCacheClipMapWorldExtent.value = fullMode ? 12f : component.screenProbeRadianceCacheClipMapWorldExtent.value;
            component.screenProbeRadianceCacheNumProbesToTraceBudget.overrideState = true;
            component.screenProbeRadianceCacheNumProbesToTraceBudget.value = fullMode ? 8 : component.screenProbeRadianceCacheNumProbesToTraceBudget.value;
            component.screenProbeRadianceCacheIrradianceClipMapCount.overrideState = true;
            component.screenProbeRadianceCacheIrradianceClipMapCount.value = fullMode ? 1 : component.screenProbeRadianceCacheIrradianceClipMapCount.value;
            component.screenProbeRadianceCacheIrradianceClipMapResolution.overrideState = true;
            component.screenProbeRadianceCacheIrradianceClipMapResolution.value = fullMode ? 16 : component.screenProbeRadianceCacheIrradianceClipMapResolution.value;
            component.screenProbeRadianceCacheIrradianceNumProbesToTraceBudget.overrideState = true;
            component.screenProbeRadianceCacheIrradianceNumProbesToTraceBudget.value = fullMode ? 8 : component.screenProbeRadianceCacheIrradianceNumProbesToTraceBudget.value;
            component.useTranslucencyVolume.overrideState = true;
            component.useTranslucencyVolume.value = fullMode;
            component.translucencyVolumeUseTemporalReprojection.overrideState = true;
            component.translucencyVolumeUseTemporalReprojection.value = false;
            component.translucencyVolumeSpatialFilter.overrideState = true;
            component.translucencyVolumeSpatialFilter.value = fullMode;
            component.translucencyVolumeGridPixelSize.overrideState = true;
            component.translucencyVolumeGridPixelSize.value = fullMode ? 128 : component.translucencyVolumeGridPixelSize.value;
            component.sceneVoxelAlwaysUpdate.overrideState = true;
            component.sceneVoxelAlwaysUpdate.value = fullMode;
            component.sceneVoxelClipMapCount.overrideState = true;
            component.sceneVoxelClipMapCount.value = fullMode ? 1 : component.sceneVoxelClipMapCount.value;
            component.sceneVoxelClipMapResolution.overrideState = true;
            component.sceneVoxelClipMapResolution.value = fullMode ? 16 : component.sceneVoxelClipMapResolution.value;
            component.sceneVoxelClipMapFirstWorldExtent.overrideState = true;
            component.sceneVoxelClipMapFirstWorldExtent.value = fullMode ? 12f : component.sceneVoxelClipMapFirstWorldExtent.value;
            component.sceneVoxelMaterialBudget.overrideState = true;
            component.sceneVoxelMaterialBudget.value = fullMode ? SceneVoxelMaterialMemoryBudget.Low : component.sceneVoxelMaterialBudget.value;
            component.sceneVoxelDrawVegetation.overrideState = true;
            component.sceneVoxelDrawVegetation.value = false;
            component.sceneVoxelDrawGrass.overrideState = true;
            component.sceneVoxelDrawGrass.value = false;
            component.sceneVoxelMaxSampleCount.overrideState = true;
            component.sceneVoxelMaxSampleCount.value = fullMode ? 1 : component.sceneVoxelMaxSampleCount.value;
            component.finalGather.overrideState = true;
            component.finalGather.value = expandedMode ? ScreenSpaceGlobalIlluminationFinalGather.IrradianceField : ScreenSpaceGlobalIlluminationFinalGather.ScreenProbe;
            component.localSkyProbeCameraDistance.overrideState = true;
            component.localSkyProbeCameraDistance.value = expandedMode ? 2f : component.localSkyProbeCameraDistance.value;
            component.localSkyProbeShowDebugSphere.overrideState = true;
            component.localSkyProbeShowDebugSphere.value = false;

            var volumeObject = new GameObject("Burt XGI Smoke Global Volume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1000f;
            volume.sharedProfile = profile;
        }

        private static void CreateSmokeCamera(RenderTexture target)
        {
            var cameraObject = new GameObject("Burt XGI Smoke Camera")
            {
                tag = "MainCamera"
            };
            cameraObject.transform.position = new Vector3(0f, 1.2f, -2.5f);
            cameraObject.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 200f;
            camera.fieldOfView = 55f;
            camera.targetTexture = target;
        }

        private static void CreateSmokeProbeBinding(Scene scene, BurtXGIProbeBakingConfig config, BurtXGIProbeBakedDataAsset bakedAsset)
        {
            var binding = BurtXGIProbeSceneBindingUtility.CreateOrRefresh(scene, config, bakedAsset, false, false);
            if (binding == null || binding.streamer == null)
            {
                throw new InvalidOperationException("XGI probe scene binding was not created.");
            }

            if (bakedAsset != null && bakedAsset.perSceneCellLists != null && bakedAsset.perSceneCellLists.Count == 1 && !string.IsNullOrEmpty(bakedAsset.perSceneCellLists[0].sceneGuid))
            {
                binding.sceneGuid = bakedAsset.perSceneCellLists[0].sceneGuid;
            }

            binding.platformMode = BurtGIProbeSceneBinding.PlatformMode.PC;
            binding.bakedDataAsset = bakedAsset;
            binding.pcBakedDataAsset = bakedAsset;
            binding.bakingConfig = config;
            binding.pcBakingConfig = config;
            binding.applyOnEnable = false;
            binding.initializeStreamingOnPlay = false;
            binding.automaticStreaming = false;
            binding.ApplyConfiguration();
            if (bakedAsset == null)
            {
                return;
            }

            if (!binding.streamer.InitializeStreaming())
            {
                throw new InvalidOperationException("XGI probe streamer init failed: " + binding.streamer.LastStreamingStatus);
            }

            var cellIndex = ResolveFirstRuntimeCellIndex(bakedAsset, binding.sceneGuid);
            if (cellIndex != int.MinValue && !binding.streamer.TryLoadCell(cellIndex))
            {
                throw new InvalidOperationException("XGI probe streamer load failed: " + binding.streamer.LastStreamingStatus);
            }
        }

        private static string ValidateXGIRenderSmokeDump(
            string dump,
            string smokeMode,
            BurtShadingDebugMode shadingDebugMode)
        {
            if (string.IsNullOrEmpty(dump))
            {
                return "RenderGraph dump was not captured.";
            }

            if (dump.IndexOf("Request: BaseCamera", StringComparison.OrdinalIgnoreCase) < 0 &&
                dump.IndexOf("Request: MainCamera", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "RenderGraph dump is missing base camera request marker.";
            }

            var required = new[]
            {
                "Screen Space Global Illumination",
                "ScreenProbe",
                "BurtGI"
            };
            var fullRequired = IsFullSmokeMode(smokeMode)
                ? new[]
                {
                    "RadianceCache",
                    "SceneVoxel",
                    "TranslucencyVolume"
                }
                : Array.Empty<string>();
            var expandedRequired = IsExpandedSmokeMode(smokeMode)
                ? new[]
                {
                    "HashGrid",
                    "LocalSkyProbe",
                    "IrradianceField"
                }
                : Array.Empty<string>();
            for (var index = 0; index < required.Length; index++)
            {
                if (dump.IndexOf(required[index], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return "RenderGraph dump is missing marker: " + required[index];
                }
            }

            for (var index = 0; index < fullRequired.Length; index++)
            {
                if (dump.IndexOf(fullRequired[index], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return "RenderGraph dump is missing full smoke marker: " + fullRequired[index];
                }
            }

            for (var index = 0; index < expandedRequired.Length; index++)
            {
                if (dump.IndexOf(expandedRequired[index], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return "RenderGraph dump is missing expanded smoke marker: " + expandedRequired[index];
                }
            }

            var debugFailure = ValidateXGIRenderSmokeDebugModeDump(dump, shadingDebugMode);
            if (!string.IsNullOrEmpty(debugFailure))
            {
                return debugFailure;
            }

            return null;
        }

        private static string ValidateXGIRenderSmokeDebugModeDump(string dump, BurtShadingDebugMode shadingDebugMode)
        {
            if (shadingDebugMode == BurtShadingDebugMode.None)
            {
                return null;
            }

            if (dump.IndexOf("Burt Debug Screen Space Global Illumination", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "RenderGraph dump is missing BurtGI debug pass marker for mode: " + shadingDebugMode;
            }

            if (dump.IndexOf(shadingDebugMode.ToString(), StringComparison.OrdinalIgnoreCase) < 0 &&
                dump.IndexOf("ShadingDebugMode", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return "RenderGraph dump is missing shading debug mode marker: " + shadingDebugMode;
            }

            var required = ResolveXGIDebugModeDumpMarkers(shadingDebugMode);
            for (var index = 0; index < required.Length; index++)
            {
                if (dump.IndexOf(required[index], StringComparison.OrdinalIgnoreCase) < 0)
                {
                    return "RenderGraph dump is missing debug marker for " + shadingDebugMode + ": " + required[index];
                }
            }

            return null;
        }

        private static string[] ResolveXGIDebugModeDumpMarkers(BurtShadingDebugMode shadingDebugMode)
        {
            switch (shadingDebugMode)
            {
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRadianceCacheStats:
                    return new[]
                    {
                        "BurtGIRadianceCacheStats",
                        "RadianceCacheStats"
                    };
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRadianceCacheVisualize:
                    return new[]
                    {
                        "RadianceCacheVisualize",
                        "BurtGIRadianceCacheClipMapFinalRadianceAtlas",
                        "BurtGIRadianceCacheClipMapIndirection"
                    };
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationRadianceCacheStatus:
                    return new[]
                    {
                        "RadianceCacheStatus",
                        "BurtGIRadianceCacheClipMapProbeLastUsedFrameBuffer",
                        "BurtGIRadianceCacheClipMapProbeLastTracedFrameBuffer"
                    };
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationScreenProbeTraceVisualize:
                    return new[]
                    {
                        "ScreenProbeTraceVisualize",
                        "BurtGIScreenProbeTraceRadiance",
                        "BurtGIScreenProbeTraceHit"
                    };
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationSceneVoxelOccupancy:
                    return new[]
                    {
                        "SceneVoxelOccupancy",
                        "BurtGISceneVoxelOccupancyMip"
                    };
                case BurtShadingDebugMode.ScreenSpaceGlobalIlluminationHashGridDebug:
                    return new[]
                    {
                        "HashGridDebug",
                        "BurtGIRadianceCacheHashGridDebugCellBuffer",
                        "BurtGIRadianceCacheHashGridDebugDrawArgsBuffer"
                    };
                default:
                    return Array.Empty<string>();
            }
        }

        private static bool IsFullSmokeMode(string smokeMode)
        {
            return string.Equals(smokeMode, "full", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(smokeMode, "expanded", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExpandedSmokeMode(string smokeMode)
        {
            return string.Equals(smokeMode, "expanded", StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateDumpPreview(string dump)
        {
            if (string.IsNullOrEmpty(dump))
            {
                return "<empty dump>";
            }

            var length = Mathf.Min(2000, dump.Length);
            return dump.Substring(0, length);
        }

        private static void SetSerializedValue(UnityEngine.Object target, string propertyName, bool value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.boolValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetSerializedValue(UnityEngine.Object target, string propertyName, int value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property != null)
            {
                property.intValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static int ResolveFirstRuntimeCellIndex(BurtXGIProbeBakedDataAsset asset, string sceneGuid)
        {
            if (ReferenceEquals(asset, null))
            {
                return int.MinValue;
            }

            var indices = asset.GetRuntimeSceneCellIndices(sceneGuid);
            if (indices != null && indices.Count > 0)
            {
                return indices[0];
            }

            if (asset.cells != null)
            {
                for (var index = 0; index < asset.cells.Length; index++)
                {
                    var cell = asset.cells[index];
                    if (cell != null)
                    {
                        return cell.cellIndex;
                    }
                }
            }

            return int.MinValue;
        }

        private static List<int> BuildRuntimeCellSamples(BurtXGIProbeBakedDataAsset asset, string sceneGuid, int sampleCount)
        {
            var samples = new List<int>(Mathf.Max(1, sampleCount));
            if (ReferenceEquals(asset, null))
            {
                return samples;
            }

            var indices = asset.GetRuntimeSceneCellIndices(sceneGuid);
            if (indices != null && indices.Count > 0)
            {
                AddEvenlySpacedSamples(indices, sampleCount, samples);
                return samples;
            }

            if (asset.cells == null || asset.cells.Length == 0)
            {
                return samples;
            }

            var fallbackIndices = new List<int>(asset.cells.Length);
            for (var index = 0; index < asset.cells.Length; index++)
            {
                var cell = asset.cells[index];
                if (cell != null)
                {
                    fallbackIndices.Add(cell.cellIndex);
                }
            }

            AddEvenlySpacedSamples(fallbackIndices, sampleCount, samples);
            return samples;
        }

        private static void AddEvenlySpacedSamples(IReadOnlyList<int> source, int sampleCount, List<int> destination)
        {
            if (source == null || source.Count == 0 || destination == null)
            {
                return;
            }

            var count = Mathf.Min(Mathf.Max(1, sampleCount), source.Count);
            if (count == 1)
            {
                AddUnique(destination, source[0]);
                return;
            }

            for (var index = 0; index < count; index++)
            {
                var sourceIndex = Mathf.RoundToInt(index * (source.Count - 1) / (float)(count - 1));
                AddUnique(destination, source[Mathf.Clamp(sourceIndex, 0, source.Count - 1)]);
            }
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (values != null && !values.Contains(value))
            {
                values.Add(value);
            }
        }

        private static string BuildImportedBakedDataDiagnostic(BurtXGIProbeBakedDataAsset asset, BurtGIProbeSceneBinding binding)
        {
            var cellsLength = asset?.cells != null ? asset.cells.Length : -1;
            var sceneListCount = asset?.perSceneCellLists != null ? asset.perSceneCellLists.Count : -1;
            var firstSceneListCount = asset?.perSceneCellLists != null && asset.perSceneCellLists.Count > 0 && asset.perSceneCellLists[0]?.cellIndices != null
                ? asset.perSceneCellLists[0].cellIndices.Count
                : -1;
            return "AssetCellCount=" + (!ReferenceEquals(asset, null) ? asset.cellCount : -1) +
                ",CellsLength=" + cellsLength +
                ",PerSceneLists=" + sceneListCount +
                ",FirstSceneListCells=" + firstSceneListCount +
                ",Binding=" + (binding != null ? binding.GetDebugStatus() : "<none>") +
                ",StreamerAsset=" + (binding != null && binding.streamer != null && binding.streamer.ActiveBakedDataAsset != null
                    ? binding.streamer.ActiveBakedDataAsset.name
                    : "<none>");
        }

        private static string AppendXGIEditorValidationCommands(string report)
        {
            return (string.IsNullOrEmpty(report) ? string.Empty : report + "\n") +
                "BurtXGIControlSurfaceValidationCommand=Burt.RenderPipeline.Editor.BurtXGIResourceValidationMenu.ValidateXGIControlSurfaceFromCommandLine\n" +
                "BurtXGILightSettingsMappingValidationCommand=Burt.RenderPipeline.Editor.BurtXGIResourceValidationMenu.ValidateXGILightSettingsMappingFromCommandLine\n" +
                "BurtXGIProbeRuntimeSettingsValidationCommand=Burt.RenderPipeline.Editor.BurtXGIResourceValidationMenu.ValidateXGIProbeRuntimeSettingsMappingFromCommandLine\n" +
                "BurtXGIProbeLegacyConfigImportGuidanceValidationCommand=Burt.RenderPipeline.Editor.BurtXGIResourceValidationMenu.ValidateXGIProbeLegacyConfigImportGuidanceFromCommandLine\n" +
                "BurtXGILegacyImportValidationCommand=Burt.RenderPipeline.Editor.BurtXGILegacyProbeDataImporter.ValidateExternalFromCommandLine -burtXGILegacySource <legacy XGIProbeBakingConfig asset path>\n" +
                "BurtXGILegacyImportCommand=Burt.RenderPipeline.Editor.BurtXGILegacyProbeDataImporter.ImportExternalFromCommandLine -burtXGILegacySource <legacy XGIProbeBakingConfig asset path> -burtXGILegacyTarget <Assets/...asset>\n" +
                "BurtXGIToolsApplyValidationCommand=Burt.RenderPipeline.Editor.BurtXGIProbeBakingWindow.ValidateXGIToolsApplyFromCommandLine\n" +
                "BurtXGIProbeGizmoValidationCommand=Burt.RenderPipeline.Editor.BurtGIProbeVolumeEditor.ValidateXGIProbeGizmoSettingsFromCommandLine";
        }

        private static string ValidateXGILightComponentCoverage(out bool hasIssue)
        {
            var ignoredVolumeFields = CreateXGILightCoverageIgnoredVolumeFields();
            var aliases = CreateXGILightCoverageAliases();
            var componentFields = new HashSet<string>();
            var componentPublicFieldInfos = typeof(BurtXGILightComponent).GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (var index = 0; index < componentPublicFieldInfos.Length; index++)
            {
                componentFields.Add(componentPublicFieldInfos[index].Name);
            }

            var checkedCount = 0;
            var missing = new List<string>();
            var volumeFieldInfos = typeof(ScreenSpaceGlobalIlluminationVolumeComponent).GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (var index = 0; index < volumeFieldInfos.Length; index++)
            {
                var field = volumeFieldInfos[index];
                if (field.FieldType == null ||
                    !field.FieldType.Name.EndsWith("Parameter", StringComparison.Ordinal) ||
                    ignoredVolumeFields.Contains(field.Name))
                {
                    continue;
                }

                checkedCount++;
                var componentFieldName = aliases.TryGetValue(field.Name, out var alias)
                    ? alias
                    : field.Name;
                if (!componentFields.Contains(componentFieldName))
                {
                    missing.Add(field.Name + " -> " + componentFieldName);
                }
            }

            hasIssue = missing.Count > 0;
            return "Burt XGI light component coverage validation completed.\n" +
                "VolumeParametersChecked=" + checkedCount + "\n" +
                "ComponentPublicFields=" + componentFields.Count + "\n" +
                "MissingCount=" + missing.Count + "\n" +
                "Missing=" + (missing.Count > 0 ? string.Join("|", missing) : "<none>");
        }

        private static string ValidateXGILightSettingsMapping(out bool hasIssue)
        {
            var failures = new List<string>();
            var component = new GameObject("Burt XGI Light Settings Mapping Validation").AddComponent<BurtXGILightComponent>();
            ScreenSpaceGlobalIlluminationVolumeComponent volumeComponent = null;
            try
            {
                component.overrideConfig = true;
                component.intensity = 1.25f;
                component.characterIntensity = 1.75f;
                component.enableBackfaceDiffuse = true;
                component.enableRoughSpecular = false;
                component.useTranslucencyVolume = false;
                component.shortRangeAO = false;
                component.shortRangeAOWeight = 0.35f;
                component.shortRangeAOApplyWeight = 0.45f;
                component.shortRangeAOSlopeCompareToleranceScale = 2.5f;
                component.screenRatio = 0.62f;
                component.screenRatioSpeed = 0.37f;
                component.sceneVoxelAlwaysUpdate = true;
                component.sceneVoxelOriginUpdateDistance = 17f;
                component.sceneVoxelClipMapCount = 4;
                component.sceneVoxelClipMapDistributionBase = 2.25f;
                component.sceneVoxelClipMapOffset03 = new Vector4(11f, 22f, 33f, 44f);
                component.sceneVoxelClipMapUpdateDistance03 = new Vector4(55f, 66f, 77f, 88f);
                component.sceneVoxelClipMapOffset47 = new Vector4(101f, 202f, 303f, 404f);
                component.sceneVoxelClipMapUpdateDistance47 = new Vector4(505f, 606f, 707f, 808f);
                component.sceneVoxelClipMapResolution = 32;
                component.sceneVoxelMaterialBudget = SceneVoxelMaterialMemoryBudget.High;
                component.sceneVoxelMaterialGenerateMethod = SceneVoxelMaterialGenerateMethod.PendingList;
                component.sceneVoxelDrawVegetation = false;
                component.sceneVoxelDrawGrass = true;
                component.sceneVoxelLightingType = SceneVoxelLightingType.Direct;
                component.sceneVoxelLightingDirectionalShadow = false;
                component.sceneVoxelLightingPunctualShadow = false;
                component.sceneVoxelLightingSkyLight = false;
                component.sceneVoxelMaxSampleCount = 31;
                component.sceneVoxelMultiBounce = false;
                component.sceneVoxelDirectionCount = 5;
                component.sceneVoxelTraceMaxSteps = 43;
                component.sceneVoxelTraceStepFactor = 1.7f;
                component.screenProbeSkylightLeaking = 0.23f;
                component.screenProbeSkylightLeakingRoughness = 0.75f;
                component.screenProbeFullSkylightLeakingDistance = 9f;
                component.screenProbeTraceSkyCubemap = false;
                component.diffuseColorBoost = 2.25f;
                component.avoidBleeding = 0.22f;
                component.sceneVoxelDirectLightIntensity = 2.4f;
                component.sceneVoxelDirectLightTint = new Color(0.25f, 0.5f, 0.75f, 1f);
                component.sceneVoxelIndirectLightIntensity = 0.33f;
                component.sceneVoxelIndirectLightTint = new Color(0.75f, 0.25f, 0.5f, 1f);
                component.sceneVoxelEnableSkyVisibility = true;
                component.sceneVoxelDebugExpandView = true;
                component.sceneVoxelDebugExpandViewDistance = 1234f;
                component.sceneVoxelDebugShowMipmapID = 3;
                component.sceneVoxelDebugLayer = BurtXGIToolsVoxelDebugLayer.Lighting_Indirect;
                component.sceneVoxelDebugByTrace = true;
                component.sceneVoxelDebugDrawProbe = true;
                component.sceneVoxelDebugProbeSizeWS = 0.75f;
                component.localSkyProbeShowDebugSphere = false;
                component.localSkyProbeCameraDistance = 24f;
                component.useIrradianceFieldGather = true;
                component.irradianceFieldStrength = 1.4f;
                component.useIrradianceFieldBaked = true;
                component.screenProbeSpacingPixels = 24;
                component.screenProbeAdaptiveAllocationFraction = 0.67f;
                component.screenProbeAdaptiveMinDownSampleFactor = 16;
                component.screenProbeTraceOctahedronResolution = 6;
                component.screenProbeTraceDistance = 123f;
                component.screenProbeTraceScreenDistance = 3.5f;
                component.screenProbeTraceVoxelMaxTraceSteps = 37;
                component.screenProbeTraceVoxelStepFactor = 2.25f;
                component.screenProbeTraceHierarchically = false;
                component.screenProbeTraceHierarchicalMaxIterations = 11;
                component.screenProbeTraceRelativeDepthThickness = 0.033f;
                component.screenProbeTraceHistoryDepthTestRelativeThickness = 0.044f;
                component.screenProbeScreenTraceThicknessScaleWhenNoFallback = 1.5f;
                component.screenProbeGatherMaxRayIntensity = 3.75f;
                component.intensityScale = 2.5f;
                component.screenProbeSampleCount = 12;
                component.screenProbeTemporalFeedback = 0.82f;
                component.screenProbeTemporalFilterHistoryWeight = 0.42f;
                component.screenProbeTemporalFilter = false;
                component.screenProbeTemporalReprojection = false;
                component.screenProbeReprojectionMaxFramesAccumulated = 7;
                component.screenProbeHistoryDistanceThreshold = 0.12f;
                component.screenProbeTemporalHistoryNormalThreshold = 35f;
                component.screenProbeReprojectionDepthRejectParamsA = 9f;
                component.screenProbeReprojectionDepthRejectParamsB = 4f;
                component.screenProbeTemporalExposureCheckThreshold = 0.22f;
                component.screenProbeTemporalPlayerVelocityThreshold = 0.8f;
                component.screenProbeApplyStrength = 0.73f;
                component.screenProbeTraceCompact = false;
                component.screenProbeTraceHardwareRay = true;
                component.screenProbeTraceUseWorldRadianceClipMap = true;
                component.screenProbeTraceSources = ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.SceneVoxel | ScreenProbeTraceSource.SkyCubemap;
                component.screenProbeImportanceSampling = false;
                component.screenProbeImportanceSampleLighting = false;
                component.screenProbeImportanceSampleProbeRadianceHistory = false;
                component.screenProbeImportanceSamplingHistoryDistanceThreshold = 1.25f;
                component.screenProbeFixedJitterIndex = 5;
                component.screenProbeSpatialFilter = false;
                component.screenProbeSpatialFilterPasses = 5;
                component.screenProbeSpatialFilterHalfKernelSize = 2;
                component.screenProbeSpatialFilterMaxRadianceHitAngle = 20f;
                component.screenProbeSpatialFilterPositionWeightScale = 800f;
                component.screenProbeFixupBorders = false;
                component.screenProbeIrradianceFormat = ScreenProbeIrradianceFormat.Octahedral;
                component.screenProbeIntegrateType = ScreenProbeIntegrateType.TileClassification;
                component.screenProbeIntegrateMethod = ScreenProbeIntegrateMethod.SphericalHarmonic;
                component.radianceCacheType = ScreenProbeRadianceCacheType.ClipMap;
                component.radianceCacheForceFullUpdate = true;
                component.radianceCacheTraceHardwareRay = true;
                component.radianceCacheCalculateIrradiance = true;
                component.radianceCacheEnableMultiBounceFromRadianceCache = true;
                component.radianceCacheRadianceProbeResolution = 48;
                component.radianceCacheIrradianceProbeResolution = 8;
                component.radianceCacheOcclusionProbeResolution = 12;
                component.radianceCacheFilterProbes = true;
                component.radianceCacheFilterMaxRadianceHitAngle = 0.45f;
                component.radianceCacheReprojectionRadiusScale = 2.75f;
                component.radianceCacheClipMapCount = 4;
                component.radianceCacheClipMapResolution = 72;
                component.radianceCacheClipMapWorldExtent = 55f;
                component.radianceCacheNumProbesToTraceBudget = 345;
                component.radianceCacheIrradianceRadianceProbeResolution = 24;
                component.radianceCacheIrradianceClipMapCount = 3;
                component.radianceCacheIrradianceClipMapResolution = 80;
                component.radianceCacheIrradianceClipMapWorldExtent = 75f;
                component.radianceCacheIrradianceNumProbesToTraceBudget = 456;
                component.radianceCacheVisualizeRadiusScale = 0.12f;
                component.radianceCacheVisualizeClipmapIndex = 2;
                component.radianceCacheHashGridDebugMaxCellDecay = 1234;
                component.translucencyVolumeGridPixelSize = 32;
                component.translucencyVolumeEndDistanceFromCamera = 160f;
                component.translucencyVolumeGridDistributionZScale = 2.5f;
                component.translucencyVolumeTracingOctahedronResolution = 5;
                component.translucencyVolumeJitter = false;
                component.translucencyVolumeUseTemporalReprojection = false;
                component.translucencyVolumeHistoryWeight = 0.93f;
                component.translucencyVolumeTemporalMaxRayDirections = 4;
                component.translucencyVolumeSpatialFilter = false;
                component.translucencyVolumeSpatialFilterSampleCount = 4;
                component.translucencyVolumeSpatialFilterStandardDeviation = 7f;
                component.translucencyVolumeGridCenterOffsetFromDepthBuffer = 1.2f;
                component.translucencyVolumeOffsetThresholdToAcceptDepthBufferOffset = 2.3f;
                component.translucencyVolumeTraceStepFactor = 1.4f;
                component.translucencyVolumeMaxTraceDistance = 321f;
                component.translucencyVolumeVoxelTraceStartDistanceScale = 1.8f;
                component.translucencyVolumeMaxRayIntensity = 33f;
                component.sceneVoxelClipMapFirstWorldExtent = 44f;
                component.sceneVoxelFollowCamera = false;
                component.sceneVoxelCameraForward = 22f;
                component.sceneVoxelOrigin = new Vector3(1f, 2f, 3f);

                volumeComponent = ScriptableObject.CreateInstance<ScreenSpaceGlobalIlluminationVolumeComponent>();
                volumeComponent.enabled.value = true;
                volumeComponent.intensity.value = 1f;
                volumeComponent.radius.value = 2f;
                volumeComponent.sampleCount.value = 12;
                var passUtilityType = typeof(BurtRenderPipelineAsset).Assembly.GetType("Burt.RenderPipeline.BurtScreenSpaceGlobalIlluminationPassUtility");
                var createSettingsMethod = passUtilityType != null
                    ? passUtilityType.GetMethod("CreateScreenSpaceGlobalIlluminationSettings", BindingFlags.Static | BindingFlags.NonPublic)
                    : null;
                var applySettingsMethod = typeof(BurtXGILightComponent).GetMethod("ApplyToSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                var applyScreenProbeSettingsMethod = typeof(BurtXGILightComponent).GetMethod("ApplyToScreenProbeSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                if (createSettingsMethod == null)
                {
                    failures.Add("MissingCreateScreenSpaceGlobalIlluminationSettingsMethod");
                }
                if (applySettingsMethod == null)
                {
                    failures.Add("MissingBurtXGILightComponent.ApplyToSettingsMethod");
                }
                if (applyScreenProbeSettingsMethod == null)
                {
                    failures.Add("MissingBurtXGILightComponent.ApplyToScreenProbeSettingsMethod");
                }

                if (createSettingsMethod != null && applySettingsMethod != null)
                {
                    var baseSettings = createSettingsMethod.Invoke(null, new object[] { volumeComponent });
                    var settings = applySettingsMethod.Invoke(component, new[] { baseSettings });
                    ValidateSettingEqual(failures, settings, "Intensity", component.intensity);
                    ValidateSettingEqual(failures, settings, "XGICharacterIntensity", component.characterIntensity);
                    ValidateSettingEqual(failures, settings, "EnableBackfaceDiffuse", component.enableBackfaceDiffuse);
                    ValidateSettingEqual(failures, settings, "EnableRoughSpecular", component.enableRoughSpecular);
                    ValidateSettingEqual(failures, settings, "UseTranslucencyVolume", component.useTranslucencyVolume);
                    ValidateSettingEqual(failures, settings, "ShortRangeAO", component.shortRangeAO);
                    ValidateSettingEqual(failures, settings, "ShortRangeAOWeight", component.shortRangeAOWeight);
                    ValidateSettingEqual(failures, settings, "ShortRangeAOApplyWeight", component.shortRangeAOApplyWeight);
                    ValidateSettingEqual(failures, settings, "ShortRangeAOSlopeCompareToleranceScale", component.shortRangeAOSlopeCompareToleranceScale);
                    ValidateSettingEqual(failures, settings, "FinalGather", ScreenSpaceGlobalIlluminationFinalGather.IrradianceField);
                    ValidateSettingEqual(failures, settings, "XGIScreenRatio", component.screenRatio);
                    ValidateSettingEqual(failures, settings, "XGIScreenRatioSpeed", component.screenRatioSpeed);
                    ValidateSettingEqual(failures, settings, "SceneVoxelAlwaysUpdate", component.sceneVoxelAlwaysUpdate);
                    ValidateSettingEqual(failures, settings, "SceneVoxelOriginUpdateDistance", component.sceneVoxelOriginUpdateDistance);
                    ValidateSettingEqual(failures, settings, "SceneVoxelClipMapCount", component.sceneVoxelClipMapCount);
                    ValidateSettingEqual(failures, settings, "SceneVoxelClipMapDistributionBase", component.sceneVoxelClipMapDistributionBase);
                    ValidateSettingEqual(failures, settings, "SceneVoxelClipMapOffset03", component.sceneVoxelClipMapOffset03);
                    ValidateSettingEqual(failures, settings, "SceneVoxelClipMapUpdateDistance03", component.sceneVoxelClipMapUpdateDistance03);
                    ValidateSettingEqual(failures, settings, "SceneVoxelClipMapOffset47", component.sceneVoxelClipMapOffset47);
                    ValidateSettingEqual(failures, settings, "SceneVoxelClipMapUpdateDistance47", component.sceneVoxelClipMapUpdateDistance47);
                    ValidateSettingEqual(failures, settings, "SceneVoxelClipMapResolution", component.sceneVoxelClipMapResolution);
                    ValidateSettingEqual(failures, settings, "SceneVoxelMaterialBudget", component.sceneVoxelMaterialBudget);
                    ValidateSettingEqual(failures, settings, "SceneVoxelMaterialGenerateMethod", SceneVoxelMaterialGenerateMethod.Atomic);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDrawVegetation", component.sceneVoxelDrawVegetation);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDrawGrass", component.sceneVoxelDrawGrass);
                    ValidateSettingEqual(failures, settings, "SceneVoxelLightingType", component.sceneVoxelLightingType);
                    ValidateSettingEqual(failures, settings, "SceneVoxelLightingDirectionalShadow", component.sceneVoxelLightingDirectionalShadow);
                    ValidateSettingEqual(failures, settings, "SceneVoxelLightingPunctualShadow", component.sceneVoxelLightingPunctualShadow);
                    ValidateSettingEqual(failures, settings, "SceneVoxelLightingSkyLight", component.sceneVoxelLightingSkyLight);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDiffuseColorBoost", component.diffuseColorBoost);
                    ValidateSettingEqual(failures, settings, "SceneVoxelAvoidBleeding", component.avoidBleeding);
                    ValidateSettingEqual(failures, settings, "SceneVoxelMaxSampleCount", component.sceneVoxelMaxSampleCount);
                    ValidateSettingEqual(failures, settings, "SceneVoxelMultiBounce", component.sceneVoxelMultiBounce);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDirectionCount", component.sceneVoxelDirectionCount);
                    ValidateSettingEqual(failures, settings, "SceneVoxelTraceMaxSteps", component.sceneVoxelTraceMaxSteps);
                    ValidateSettingEqual(failures, settings, "SceneVoxelTraceStepFactor", component.sceneVoxelTraceStepFactor);
                    ValidateSettingEqual(failures, settings, "ScreenProbeSkylightLeaking", component.screenProbeSkylightLeaking);
                    ValidateSettingEqual(failures, settings, "ScreenProbeSkylightLeakingRoughness", component.screenProbeSkylightLeakingRoughness);
                    ValidateSettingEqual(failures, settings, "ScreenProbeFullSkylightLeakingDistance", component.screenProbeFullSkylightLeakingDistance);
                    ValidateSettingEqual(failures, settings, "ScreenProbeTraceSkyCubemap", component.screenProbeTraceSkyCubemap);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDirectLightIntensity", component.sceneVoxelDirectLightIntensity);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDirectLightTint", new Vector3(component.sceneVoxelDirectLightTint.linear.r, component.sceneVoxelDirectLightTint.linear.g, component.sceneVoxelDirectLightTint.linear.b));
                    ValidateSettingEqual(failures, settings, "SceneVoxelIndirectLightIntensity", component.sceneVoxelIndirectLightIntensity);
                    ValidateSettingEqual(failures, settings, "SceneVoxelIndirectLightTint", new Vector3(component.sceneVoxelIndirectLightTint.linear.r, component.sceneVoxelIndirectLightTint.linear.g, component.sceneVoxelIndirectLightTint.linear.b));
                    ValidateSettingEqual(failures, settings, "SceneVoxelEnableSkyVisibility", component.sceneVoxelEnableSkyVisibility);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDebugExpandView", component.sceneVoxelDebugExpandView);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDebugExpandViewDistance", component.sceneVoxelDebugExpandViewDistance);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDebugShowMipmapID", component.sceneVoxelDebugShowMipmapID);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDebugLayer", (int)component.sceneVoxelDebugLayer);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDebugByTrace", component.sceneVoxelDebugByTrace);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDebugDrawProbe", component.sceneVoxelDebugDrawProbe);
                    ValidateSettingEqual(failures, settings, "SceneVoxelDebugProbeSizeWS", component.sceneVoxelDebugProbeSizeWS);
                    ValidateSettingEqual(failures, settings, "LocalSkyProbeShowDebugSphere", component.localSkyProbeShowDebugSphere);
                    ValidateSettingEqual(failures, settings, "IrradianceFieldStrength", component.irradianceFieldStrength);
                    ValidateSettingEqual(failures, settings, "IrradianceFieldBaked", component.useIrradianceFieldBaked);
                }

                if (applyScreenProbeSettingsMethod != null)
                {
                    var baseScreenProbeSettings = CreateXGILightValidationScreenProbeSettings(failures);
                    if (baseScreenProbeSettings != null)
                    {
                        var screenProbeSettings = applyScreenProbeSettingsMethod.Invoke(component, new[] { baseScreenProbeSettings });
                        ValidateSettingEqual(failures, screenProbeSettings, "SpacingPixels", component.screenProbeSpacingPixels);
                        ValidateSettingEqual(failures, screenProbeSettings, "AdaptiveAllocationFraction", component.screenProbeAdaptiveAllocationFraction);
                        ValidateSettingEqual(failures, screenProbeSettings, "AdaptiveMinDownSampleFactor", component.screenProbeAdaptiveMinDownSampleFactor);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceOctahedronResolution", component.screenProbeTraceOctahedronResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceDistance", component.screenProbeTraceDistance);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceScreenDistance", component.screenProbeTraceScreenDistance);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceVoxelMaxTraceSteps", component.screenProbeTraceVoxelMaxTraceSteps);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceVoxelStepFactor", component.screenProbeTraceVoxelStepFactor);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceHierarchically", component.screenProbeTraceHierarchically);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceHierarchicalMaxIterations", component.screenProbeTraceHierarchicalMaxIterations);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceRelativeDepthThickness", component.screenProbeTraceRelativeDepthThickness);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceHistoryDepthTestRelativeThickness", component.screenProbeTraceHistoryDepthTestRelativeThickness);
                        ValidateSettingEqual(failures, screenProbeSettings, "ScreenTraceThicknessScaleWhenNoFallback", 2f);
                        ValidateSettingEqual(failures, screenProbeSettings, "GatherMaxRayIntensity", component.screenProbeGatherMaxRayIntensity);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceRadianceIntensityScale", component.intensityScale);
                        ValidateSettingEqual(failures, screenProbeSettings, "SampleCount", component.screenProbeSampleCount);
                        ValidateSettingEqual(failures, screenProbeSettings, "TemporalFeedback", component.screenProbeTemporalFeedback);
                        ValidateSettingEqual(failures, screenProbeSettings, "TemporalFilterHistoryWeight", component.screenProbeTemporalFilterHistoryWeight);
                        ValidateSettingEqual(failures, screenProbeSettings, "TemporalFilter", component.screenProbeTemporalFilter);
                        ValidateSettingEqual(failures, screenProbeSettings, "TemporalReprojection", component.screenProbeTemporalReprojection);
                        ValidateSettingEqual(failures, screenProbeSettings, "ReprojectionMaxFramesAccumulated", component.screenProbeReprojectionMaxFramesAccumulated);
                        ValidateSettingEqual(failures, screenProbeSettings, "HistoryDistanceThreshold", component.screenProbeHistoryDistanceThreshold);
                        ValidateSettingEqual(failures, screenProbeSettings, "TemporalHistoryNormalThreshold", component.screenProbeTemporalHistoryNormalThreshold);
                        ValidateSettingEqual(failures, screenProbeSettings, "ReprojectionDepthRejectParamsA", component.screenProbeReprojectionDepthRejectParamsA);
                        ValidateSettingEqual(failures, screenProbeSettings, "ReprojectionDepthRejectParamsB", component.screenProbeReprojectionDepthRejectParamsB);
                        ValidateSettingEqual(failures, screenProbeSettings, "TemporalExposureCheckThreshold", component.screenProbeTemporalExposureCheckThreshold);
                        ValidateSettingEqual(failures, screenProbeSettings, "TemporalPlayerVelocityThreshold", component.screenProbeTemporalPlayerVelocityThreshold);
                        ValidateSettingEqual(failures, screenProbeSettings, "ApplyStrength", component.screenProbeApplyStrength);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceHardwareRay", component.screenProbeTraceHardwareRay);
                        ValidateSettingEqual(failures, screenProbeSettings, "TraceSources", component.screenProbeTraceSources);
                        ValidateSettingEqual(failures, screenProbeSettings, "ImportanceSampling", component.screenProbeImportanceSampling);
                        ValidateSettingEqual(failures, screenProbeSettings, "ImportanceSampleLighting", component.screenProbeImportanceSampleLighting);
                        ValidateSettingEqual(failures, screenProbeSettings, "ImportanceSampleProbeRadianceHistory", component.screenProbeImportanceSampleProbeRadianceHistory);
                        ValidateSettingEqual(failures, screenProbeSettings, "ImportanceSamplingHistoryDistanceThreshold", component.screenProbeImportanceSamplingHistoryDistanceThreshold);
                        ValidateSettingEqual(failures, screenProbeSettings, "FixedJitterIndex", component.screenProbeFixedJitterIndex);
                        ValidateSettingEqual(failures, screenProbeSettings, "SpatialFilter", component.screenProbeSpatialFilter);
                        ValidateSettingEqual(failures, screenProbeSettings, "SpatialFilterPasses", component.screenProbeSpatialFilterPasses);
                        ValidateSettingEqual(failures, screenProbeSettings, "SpatialFilterHalfKernelSize", component.screenProbeSpatialFilterHalfKernelSize);
                        ValidateSettingEqual(failures, screenProbeSettings, "SpatialFilterMaxRadianceHitAngle", component.screenProbeSpatialFilterMaxRadianceHitAngle * Mathf.Deg2Rad);
                        ValidateSettingEqual(failures, screenProbeSettings, "SpatialFilterPositionWeightScale", component.screenProbeSpatialFilterPositionWeightScale);
                        ValidateSettingEqual(failures, screenProbeSettings, "FixupBorders", component.screenProbeFixupBorders);
                        ValidateSettingEqual(failures, screenProbeSettings, "IrradianceFormat", component.screenProbeIrradianceFormat);
                        ValidateSettingEqual(failures, screenProbeSettings, "IntegrateType", component.screenProbeIntegrateType);
                        ValidateSettingEqual(failures, screenProbeSettings, "IntegrateMethod", component.screenProbeIntegrateMethod);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheType", component.radianceCacheType);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheForceFullUpdate", component.radianceCacheForceFullUpdate);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheTraceHardwareRay", component.radianceCacheTraceHardwareRay);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheCalculateIrradiance", component.radianceCacheCalculateIrradiance);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheEnableMultiBounceFromRadianceCache", component.radianceCacheEnableMultiBounceFromRadianceCache);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheRadianceProbeResolution", component.radianceCacheRadianceProbeResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheIrradianceProbeResolution", component.radianceCacheIrradianceProbeResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheOcclusionProbeResolution", component.radianceCacheOcclusionProbeResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheFilterProbes", component.radianceCacheFilterProbes);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheFilterMaxRadianceHitAngle", component.radianceCacheFilterMaxRadianceHitAngle);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheReprojectionRadiusScale", component.radianceCacheReprojectionRadiusScale);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheClipMapCount", component.radianceCacheClipMapCount);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheClipMapResolution", component.radianceCacheClipMapResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheClipMapWorldExtent", component.radianceCacheClipMapWorldExtent);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheNumProbesToTraceBudget", component.radianceCacheNumProbesToTraceBudget);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheIrradianceRadianceProbeResolution", component.radianceCacheIrradianceRadianceProbeResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheIrradianceClipMapCount", component.radianceCacheIrradianceClipMapCount);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheIrradianceClipMapResolution", component.radianceCacheIrradianceClipMapResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheIrradianceClipMapWorldExtent", component.radianceCacheIrradianceClipMapWorldExtent);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheIrradianceNumProbesToTraceBudget", component.radianceCacheIrradianceNumProbesToTraceBudget);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheVisualizeRadiusScale", component.radianceCacheVisualizeRadiusScale);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheVisualizeClipmapIndex", component.radianceCacheVisualizeClipmapIndex);
                        ValidateSettingEqual(failures, screenProbeSettings, "RadianceCacheHashGridDebugMaxCellDecay", component.radianceCacheHashGridDebugMaxCellDecay);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeGridPixelSize", component.translucencyVolumeGridPixelSize);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeEndDistanceFromCamera", component.translucencyVolumeEndDistanceFromCamera);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeGridDistributionZScale", component.translucencyVolumeGridDistributionZScale);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeTracingOctahedronResolution", component.translucencyVolumeTracingOctahedronResolution);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeJitter", component.translucencyVolumeJitter);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeUseTemporalReprojection", component.translucencyVolumeUseTemporalReprojection);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeHistoryWeight", component.translucencyVolumeHistoryWeight);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeTemporalMaxRayDirections", component.translucencyVolumeTemporalMaxRayDirections);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeSpatialFilter", component.translucencyVolumeSpatialFilter);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeSpatialFilterSampleCount", component.translucencyVolumeSpatialFilterSampleCount);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeSpatialFilterStandardDeviation", component.translucencyVolumeSpatialFilterStandardDeviation);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeGridCenterOffsetFromDepthBuffer", component.translucencyVolumeGridCenterOffsetFromDepthBuffer);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeOffsetThresholdToAcceptDepthBufferOffset", component.translucencyVolumeOffsetThresholdToAcceptDepthBufferOffset);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeTraceStepFactor", component.translucencyVolumeTraceStepFactor);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeMaxTraceDistance", component.translucencyVolumeMaxTraceDistance);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeVoxelTraceStartDistanceScale", component.translucencyVolumeVoxelTraceStartDistanceScale);
                        ValidateSettingEqual(failures, screenProbeSettings, "TranslucencyVolumeMaxRayIntensity", component.translucencyVolumeMaxRayIntensity);
                        ValidateSettingEqual(failures, screenProbeSettings, "SceneVoxelClipMapFirstWorldExtent", component.sceneVoxelClipMapFirstWorldExtent);
                        ValidateSettingEqual(failures, screenProbeSettings, "SceneVoxelClipMapDistributionBase", component.sceneVoxelClipMapDistributionBase);
                        ValidateSettingEqual(failures, screenProbeSettings, "SceneVoxelFollowCamera", component.sceneVoxelFollowCamera);
                        ValidateSettingEqual(failures, screenProbeSettings, "SceneVoxelCameraForward", component.sceneVoxelCameraForward);
                        ValidateSettingEqual(failures, screenProbeSettings, "SceneVoxelOrigin", component.sceneVoxelOrigin);
                    }
                }
            }
            finally
            {
                if (volumeComponent != null)
                {
                    UnityEngine.Object.DestroyImmediate(volumeComponent);
                }

                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }

            hasIssue = failures.Count > 0;
            return "Burt XGI light settings mapping validation completed.\n" +
                "CasesChecked=1\n" +
                "FailureCount=" + failures.Count + "\n" +
                "Failures=" + (failures.Count > 0 ? string.Join("|", failures) : "<none>");
        }

        private static string ValidateXGILightLegacyTraceTypeMigration(out bool hasIssue)
        {
            const int legacyScreen = 0x0001;
            const int legacyVoxelOctree = 0x0004;
            const int legacyVoxels = 0x0008;
            const int legacyLocalSkyProbe = 0x0010;
            const int legacyHardwareRayTracing = 0x0020;
            const int legacySkyCubemap = 0x1000;

            var failures = new List<string>();
            var legacyField = typeof(BurtXGILightComponent).GetField(
                "legacyScreenProbeTraceTypes",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var disableCompactField = typeof(BurtXGILightComponent).GetField(
                "m_ScreenProbeTraceDisableCompact",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var disableSkyCubemapField = typeof(BurtXGILightComponent).GetField(
                "m_DisableTraceSkyCubemap",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (legacyField == null)
            {
                failures.Add("MissingLegacyScreenProbeTraceTypesField");
            }
            else
            {
                ValidateLegacyTraceTypeCase(
                    failures,
                    legacyField,
                    legacyScreen | legacyVoxelOctree | legacySkyCubemap,
                    ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.VoxelOctree | ScreenProbeTraceSource.SkyCubemap,
                    hardwareRayTracing: false,
                    skyCubemap: true,
                    "ScreenVoxelOctreeSky");

                ValidateLegacyTraceTypeCase(
                    failures,
                    legacyField,
                    legacyScreen | legacyVoxels | legacyLocalSkyProbe | legacyHardwareRayTracing,
                    ScreenProbeTraceSource.Screen | ScreenProbeTraceSource.VoxelOctree | ScreenProbeTraceSource.LocalSkyProbe,
                    hardwareRayTracing: true,
                    skyCubemap: false,
                    "LegacyVoxelsLocalSkyHardwareRay");

                ValidateLegacyTraceTypeCase(
                    failures,
                    legacyField,
                    legacySkyCubemap,
                    ScreenProbeTraceSource.SkyCubemap,
                    hardwareRayTracing: false,
                    skyCubemap: true,
                    "SkyCubemapOnly");
            }

            ValidateLegacyTraceDisableCase(failures, disableCompactField, disableSkyCubemapField);

            hasIssue = failures.Count > 0;
            return "Burt XGI legacy trace type migration validation completed.\n" +
                "CasesChecked=4\n" +
                "FailureCount=" + failures.Count + "\n" +
                "Failures=" + (failures.Count > 0 ? string.Join("|", failures) : "<none>");
        }

        private static string ValidateXGIProbeRuntimeSettingsMapping(out bool hasIssue)
        {
            var failures = new List<string>();
            ValidateCapturedProbeRuntimeSettingsMapping(failures);
            ValidateSourceConfigProbeRuntimeSettingsFallback(failures);

            hasIssue = failures.Count > 0;
            return "Burt XGI probe runtime settings mapping validation completed.\n" +
                "CasesChecked=2\n" +
                "FailureCount=" + failures.Count + "\n" +
                "Failures=" + (failures.Count > 0 ? string.Join("|", failures) : "<none>");
        }

        private static string ValidateXGIProbeLegacyConfigImportGuidance(out bool hasIssue)
        {
            var failures = new List<string>();
            var legacyMaxChunkField = typeof(BurtXGIProbeBakingConfig).GetField(
                "legacyXRenderMaxSHChunkCount",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (legacyMaxChunkField == null)
            {
                failures.Add("MissingLegacyXRenderMaxSHChunkCountField");
            }
            else
            {
                ValidateLegacyConfigImportRequiredCase(failures, legacyMaxChunkField);
                ValidateLegacyConfigImportedBakedDataCase(failures, legacyMaxChunkField);
            }

            hasIssue = failures.Count > 0;
            return "Burt XGI probe legacy config import guidance validation completed.\n" +
                "CasesChecked=2\n" +
                "FailureCount=" + failures.Count + "\n" +
                "Failures=" + (failures.Count > 0 ? string.Join("|", failures) : "<none>");
        }

        private static void ValidateLegacyConfigImportRequiredCase(List<string> failures, FieldInfo legacyMaxChunkField)
        {
            var config = ScriptableObject.CreateInstance<BurtXGIProbeBakingConfig>();
            try
            {
                legacyMaxChunkField.SetValue(config, 1);
                if (!config.HasLegacyXRenderStreamingLayout)
                {
                    failures.Add("LegacyConfigImportRequired.HasLegacyXRenderStreamingLayout.ExpectedTrue");
                }

                if (!config.RequiresLegacyXRenderProbeDataImport)
                {
                    failures.Add("LegacyConfigImportRequired.RequiresImport.ExpectedTrue");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void ValidateLegacyConfigImportedBakedDataCase(List<string> failures, FieldInfo legacyMaxChunkField)
        {
            var config = ScriptableObject.CreateInstance<BurtXGIProbeBakingConfig>();
            var asset = ScriptableObject.CreateInstance<BurtXGIProbeBakedDataAsset>();
            try
            {
                legacyMaxChunkField.SetValue(config, 1);
                config.bakedDataAsset = asset;
                if (!config.HasLegacyXRenderStreamingLayout)
                {
                    failures.Add("LegacyConfigImported.HasLegacyXRenderStreamingLayout.ExpectedTrue");
                }

                if (config.RequiresLegacyXRenderProbeDataImport)
                {
                    failures.Add("LegacyConfigImported.RequiresImport.ExpectedFalse");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void ValidateCapturedProbeRuntimeSettingsMapping(List<string> failures)
        {
            var config = ScriptableObject.CreateInstance<BurtXGIProbeBakingConfig>();
            var asset = ScriptableObject.CreateInstance<BurtXGIProbeBakedDataAsset>();
            var volume = new GameObject("Burt XGI Probe Runtime Settings Validation").AddComponent<BurtGIProbeVolume>();
            try
            {
                config.enableShading = true;
                config.normalBias = 0.125f;
                config.viewBias = 0.25f;
                config.lightIntensity = 2.75f;
                config.skyVisibilityIntensity = 0.375f;
                config.skyVisibilityTint = new Color(0.25f, 0.5f, 0.75f, 1f);
                config.skyVisibilityOffset = -0.125f;
                config.mainLightSHIntensity = 1.875f;
                config.mainLightSHTint = new Color(0.6f, 0.4f, 0.2f, 1f);
                config.mainLightSHUsesPreExposure = false;
                config.systemParameters = new BurtXGIProbeSystemParameters
                {
                    enable = true,
                    memoryBudget = BurtXGIProbeTextureMemoryBudget.High,
                    shBands = BurtXGIProbeSHBands.SphericalHarmonicsL2
                };

                asset.CaptureRuntimeSettings(config);
                asset.ApplyRuntimeSettings(volume);

                ValidateProbeRuntimeSettingsVolume(
                    failures,
                    "Captured",
                    volume,
                    expectedEnableShading: true,
                    expectedSHBands: BurtXGIProbeSHBands.SphericalHarmonicsL2,
                    config.normalBias,
                    config.viewBias,
                    config.lightIntensity,
                    config.skyVisibilityTint,
                    config.skyVisibilityIntensity,
                    config.skyVisibilityOffset,
                    config.mainLightSHTint,
                    config.mainLightSHIntensity,
                    config.mainLightSHUsesPreExposure);

                if (!asset.hasRuntimeSettings)
                {
                    failures.Add("Captured.AssetHasRuntimeSettings.ExpectedTrue");
                }

                if (!asset.hasRuntimeSystemParameters)
                {
                    failures.Add("Captured.AssetHasRuntimeSystemParameters.ExpectedTrue");
                }

                if (asset.RuntimeMemoryBudget != BurtXGIProbeTextureMemoryBudget.High)
                {
                    failures.Add("Captured.RuntimeMemoryBudget Expected=High Actual=" + asset.RuntimeMemoryBudget);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(volume.gameObject);
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void ValidateSourceConfigProbeRuntimeSettingsFallback(List<string> failures)
        {
            var config = ScriptableObject.CreateInstance<BurtXGIProbeBakingConfig>();
            var asset = ScriptableObject.CreateInstance<BurtXGIProbeBakedDataAsset>();
            var volume = new GameObject("Burt XGI Probe Source Runtime Settings Validation").AddComponent<BurtGIProbeVolume>();
            try
            {
                config.enableShading = true;
                config.normalBias = 0.0625f;
                config.viewBias = 0.5f;
                config.lightIntensity = 0.75f;
                config.skyVisibilityIntensity = 0.25f;
                config.skyVisibilityTint = new Color(0.1f, 0.2f, 0.3f, 1f);
                config.skyVisibilityOffset = 0.3125f;
                config.mainLightSHIntensity = 0.625f;
                config.mainLightSHTint = new Color(0.9f, 0.8f, 0.7f, 1f);
                config.mainLightSHUsesPreExposure = true;
                config.systemParameters = new BurtXGIProbeSystemParameters
                {
                    enable = false,
                    memoryBudget = BurtXGIProbeTextureMemoryBudget.Film,
                    shBands = BurtXGIProbeSHBands.L0
                };
                asset.sourceConfig = config;

                asset.ApplyRuntimeSettings(volume);

                ValidateProbeRuntimeSettingsVolume(
                    failures,
                    "SourceFallback",
                    volume,
                    expectedEnableShading: false,
                    expectedSHBands: BurtXGIProbeSHBands.L0,
                    config.normalBias,
                    config.viewBias,
                    config.lightIntensity,
                    config.skyVisibilityTint,
                    config.skyVisibilityIntensity,
                    config.skyVisibilityOffset,
                    config.mainLightSHTint,
                    config.mainLightSHIntensity,
                    config.mainLightSHUsesPreExposure);

                if (asset.RuntimeMemoryBudget != BurtXGIProbeTextureMemoryBudget.Film)
                {
                    failures.Add("SourceFallback.RuntimeMemoryBudget Expected=Film Actual=" + asset.RuntimeMemoryBudget);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(volume.gameObject);
                UnityEngine.Object.DestroyImmediate(asset);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static void ValidateProbeRuntimeSettingsVolume(
            List<string> failures,
            string caseName,
            BurtGIProbeVolume volume,
            bool expectedEnableShading,
            BurtXGIProbeSHBands expectedSHBands,
            float expectedNormalBias,
            float expectedViewBias,
            float expectedLightIntensity,
            Color expectedSkyVisibilityTint,
            float expectedSkyVisibilityIntensity,
            float expectedSkyVisibilityOffset,
            Color expectedMainLightSHTint,
            float expectedMainLightSHIntensity,
            bool expectedMainLightSHUsesPreExposure)
        {
            ValidateEqual(failures, caseName + ".EnableShading", expectedEnableShading, volume.virtualEnableShading);
            ValidateEqual(failures, caseName + ".SHBands", expectedSHBands, volume.virtualSHBands);
            ValidateApproximately(failures, caseName + ".NormalBias", expectedNormalBias, volume.virtualNormalBias);
            ValidateApproximately(failures, caseName + ".ViewBias", expectedViewBias, volume.virtualViewBias);
            ValidateApproximately(failures, caseName + ".LightIntensity", expectedLightIntensity, volume.virtualLightIntensity);
            ValidateEqual(failures, caseName + ".SkyVisibilityTint", expectedSkyVisibilityTint, volume.virtualSkyVisibilityTint);
            ValidateApproximately(failures, caseName + ".SkyVisibilityIntensity", expectedSkyVisibilityIntensity, volume.virtualSkyVisibilityIntensity);
            ValidateApproximately(failures, caseName + ".SkyVisibilityOffset", expectedSkyVisibilityOffset, volume.virtualSkyVisibilityOffset);
            ValidateEqual(failures, caseName + ".MainLightSHTint", expectedMainLightSHTint, volume.virtualMainLightSHTint);
            ValidateApproximately(failures, caseName + ".MainLightSHIntensity", expectedMainLightSHIntensity, volume.virtualMainLightSHIntensity);
            ValidateEqual(failures, caseName + ".MainLightSHUsesPreExposure", expectedMainLightSHUsesPreExposure, volume.virtualMainLightSHUsesPreExposure);
        }

        private static object CreateXGILightValidationScreenProbeSettings(List<string> failures)
        {
            var settingsType = typeof(BurtRenderPipelineAsset).Assembly.GetType(
                "Burt.RenderPipeline.BurtScreenSpaceGlobalIlluminationScreenProbeSettings");
            if (settingsType == null)
            {
                failures.Add("MissingBurtScreenSpaceGlobalIlluminationScreenProbeSettingsType");
                return null;
            }

            var disabledField = settingsType.GetField("Disabled", BindingFlags.Static | BindingFlags.Public);
            if (disabledField == null)
            {
                failures.Add("MissingBurtScreenSpaceGlobalIlluminationScreenProbeSettings.Disabled");
                return null;
            }

            try
            {
                return disabledField.GetValue(null);
            }
            catch (Exception exception)
            {
                failures.Add("CreateScreenProbeSettingsFailed=" + exception.GetType().Name);
                return null;
            }
        }

        private static void ValidateSettingEqual<T>(List<string> failures, object settings, string propertyName, T expected)
        {
            if (settings == null)
            {
                failures.Add(propertyName + ".SettingsNull");
                return;
            }

            var property = settings.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null)
            {
                failures.Add(propertyName + ".MissingProperty");
                return;
            }

            var actualValue = property.GetValue(settings);
            if (typeof(T) == typeof(float))
            {
                ValidateApproximately(
                    failures,
                    propertyName,
                    (float)(object)expected,
                    actualValue is float actualFloat ? actualFloat : float.NaN);
                return;
            }

            if (actualValue is T typedActual)
            {
                ValidateEqual(failures, propertyName, expected, typedActual);
                return;
            }

            failures.Add(propertyName + " Expected=" + expected + " Actual=" + (actualValue ?? "<null>"));
        }

        private static void ValidateEqual<T>(List<string> failures, string name, T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                failures.Add(name + " Expected=" + expected + " Actual=" + actual);
            }
        }

        private static void ValidateApproximately(List<string> failures, string name, float expected, float actual)
        {
            if (!Mathf.Approximately(expected, actual))
            {
                failures.Add(name + " Expected=" + expected + " Actual=" + actual);
            }
        }

        private static void ValidateLegacyTraceTypeCase(
            List<string> failures,
            FieldInfo legacyField,
            int legacyTraceTypes,
            ScreenProbeTraceSource expectedSources,
            bool hardwareRayTracing,
            bool skyCubemap,
            string caseName)
        {
            var component = new GameObject("Burt XGI Legacy Trace Migration Validation").AddComponent<BurtXGILightComponent>();
            try
            {
                component.screenProbeTraceSources = ScreenProbeTraceSource.None;
                component.screenProbeTraceHardwareRay = false;
                component.screenProbeTraceSkyCubemap = false;
                legacyField.SetValue(component, legacyTraceTypes);
                ((ISerializationCallbackReceiver)component).OnAfterDeserialize();

                if (component.screenProbeTraceSources != expectedSources)
                {
                    failures.Add(caseName + ".TraceSources Expected=" + expectedSources + " Actual=" + component.screenProbeTraceSources);
                }

                if (component.screenProbeTraceHardwareRay != hardwareRayTracing)
                {
                    failures.Add(caseName + ".HardwareRay Expected=" + hardwareRayTracing + " Actual=" + component.screenProbeTraceHardwareRay);
                }

                if (component.screenProbeTraceSkyCubemap != skyCubemap)
                {
                    failures.Add(caseName + ".SkyCubemap Expected=" + skyCubemap + " Actual=" + component.screenProbeTraceSkyCubemap);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }

        private static void ValidateLegacyTraceDisableCase(
            List<string> failures,
            FieldInfo disableCompactField,
            FieldInfo disableSkyCubemapField)
        {
            if (disableCompactField == null)
            {
                failures.Add("MissingScreenProbeTraceDisableCompactField");
            }

            if (disableSkyCubemapField == null)
            {
                failures.Add("MissingDisableTraceSkyCubemapField");
            }

            if (disableCompactField == null || disableSkyCubemapField == null)
            {
                return;
            }

            var component = new GameObject("Burt XGI Legacy Trace Disable Validation").AddComponent<BurtXGILightComponent>();
            try
            {
                component.screenProbeTraceCompact = true;
                component.screenProbeTraceSkyCubemap = true;
                disableCompactField.SetValue(component, true);
                disableSkyCubemapField.SetValue(component, true);
                ((ISerializationCallbackReceiver)component).OnAfterDeserialize();

                if (component.screenProbeTraceCompact)
                {
                    failures.Add("LegacyDisableTraceCompact.ExpectedFalse");
                }

                if (component.screenProbeTraceSkyCubemap)
                {
                    failures.Add("LegacyDisableTraceSkyCubemap.ExpectedFalse");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(component.gameObject);
            }
        }

        private static HashSet<string> CreateXGILightCoverageIgnoredVolumeFields()
        {
            return new HashSet<string>
            {
                "enabled",
                "quality",
                "resolution",
                "radius",
                "sampleCount",
                "maxSteps",
                "thickness",
                "skyFallback",
                "radianceClamp",
                "normalWeight",
                "distanceFade",
                "blur",
                "blurSharpness",
                "spatialDenoiseRadius",
                "spatialDenoiseStrength",
                "leakGuardStrength",
                "edgeFadeStrength",
                "normalConeTightness",
                "skyEdgeSuppression",
                "temporalAccumulation",
                "temporalFeedback",
                "temporalDepthRejection",
                "temporalNormalRejection",
                "temporalClamp",
                "temporalVarianceClamp",
                "temporalHitRejection",
                "screenProbeLite",
                "screenProbeMaxRoughnessToEvaluateRoughSpecular"
            };
        }

        private static Dictionary<string, string> CreateXGILightCoverageAliases()
        {
            return new Dictionary<string, string>
            {
                { "xgiIntensityScale", "intensityScale" },
                { "xgiCharacterIntensity", "characterIntensity" },
                { "xgiScreenRatio", "screenRatio" },
                { "xgiScreenRatioSpeed", "screenRatioSpeed" },
                { "xgiUseProbeFirst", "useProbeFirst" },
                { "screenProbeRadianceCacheType", "radianceCacheType" },
                { "screenProbeRadianceCacheForceFullUpdate", "radianceCacheForceFullUpdate" },
                { "screenProbeRadianceCacheTraceHardwareRay", "radianceCacheTraceHardwareRay" },
                { "screenProbeRadianceCacheCalculateIrradiance", "radianceCacheCalculateIrradiance" },
                { "screenProbeRadianceCacheEnableMultiBounceFromRadianceCache", "radianceCacheEnableMultiBounceFromRadianceCache" },
                { "screenProbeRadianceCacheRadianceProbeResolution", "radianceCacheRadianceProbeResolution" },
                { "screenProbeRadianceCacheIrradianceProbeResolution", "radianceCacheIrradianceProbeResolution" },
                { "screenProbeRadianceCacheOcclusionProbeResolution", "radianceCacheOcclusionProbeResolution" },
                { "screenProbeRadianceCacheFilterProbes", "radianceCacheFilterProbes" },
                { "screenProbeRadianceCacheFilterMaxRadianceHitAngle", "radianceCacheFilterMaxRadianceHitAngle" },
                { "screenProbeRadianceCacheReprojectionRadiusScale", "radianceCacheReprojectionRadiusScale" },
                { "screenProbeRadianceCacheClipMapCount", "radianceCacheClipMapCount" },
                { "screenProbeRadianceCacheClipMapResolution", "radianceCacheClipMapResolution" },
                { "screenProbeRadianceCacheClipMapWorldExtent", "radianceCacheClipMapWorldExtent" },
                { "screenProbeRadianceCacheNumProbesToTraceBudget", "radianceCacheNumProbesToTraceBudget" },
                { "screenProbeRadianceCacheIrradianceRadianceProbeResolution", "radianceCacheIrradianceRadianceProbeResolution" },
                { "screenProbeRadianceCacheIrradianceClipMapCount", "radianceCacheIrradianceClipMapCount" },
                { "screenProbeRadianceCacheIrradianceClipMapResolution", "radianceCacheIrradianceClipMapResolution" },
                { "screenProbeRadianceCacheIrradianceClipMapWorldExtent", "radianceCacheIrradianceClipMapWorldExtent" },
                { "screenProbeRadianceCacheIrradianceNumProbesToTraceBudget", "radianceCacheIrradianceNumProbesToTraceBudget" },
                { "screenProbeRadianceCacheVisualizeRadiusScale", "radianceCacheVisualizeRadiusScale" },
                { "screenProbeRadianceCacheVisualizeClipmapIndex", "radianceCacheVisualizeClipmapIndex" },
                { "screenProbeRadianceCacheHashGridDebugMaxCellDecay", "radianceCacheHashGridDebugMaxCellDecay" },
                { "sceneVoxelDiffuseColorBoost", "diffuseColorBoost" },
                { "sceneVoxelAvoidBleeding", "avoidBleeding" },
                { "finalGather", "useIrradianceFieldGather" },
                { "irradianceFieldBaked", "useIrradianceFieldBaked" }
            };
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

        private static int GetCommandLineInt(string[] args, string name, int fallback)
        {
            var value = GetCommandLineValue(args, name);
            return int.TryParse(value, out var parsed)
                ? parsed
                : fallback;
        }

        private static bool ContainsStatusIssue(string report)
        {
            return ContainsNonRuntimeMissingStatus(report) ||
                report.IndexOf("MissingKernel(", StringComparison.Ordinal) >= 0 ||
                report.IndexOf("InvalidKernel(", StringComparison.Ordinal) >= 0 ||
                report.IndexOf("Unsupported(", StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsNonRuntimeMissingStatus(string report)
        {
            if (string.IsNullOrEmpty(report))
            {
                return false;
            }

            var lines = report.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (line.IndexOf("RuntimeStatus=", StringComparison.Ordinal) >= 0)
                {
                    continue;
                }

                if (line.IndexOf("Missing(", StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        [MenuItem(ProbeBakeSceneMenuPath, false, 2401)]
        private static void ValidateXGIProbeBakingScene()
        {
            var validation = BurtXGIProbeBakeAPI.ValidateScene();
            var hasIssue = !validation.HasRuntimeProbeData || ContainsStatusIssue(validation.report);
            if (hasIssue)
            {
                Debug.LogWarning(validation.report);
            }
            else
            {
                Debug.Log(validation.report);
            }

            EditorUtility.DisplayDialog(
                "BurtRP XGI Probe Baking Scene",
                validation.report,
                hasIssue ? "Needs Attention" : "OK");
        }

        [MenuItem(CreateOrRefreshSceneBindingMenuPath, false, 2495)]
        private static void CreateOrRefreshXGIProbeSceneBinding()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                EditorUtility.DisplayDialog("BurtRP XGI", "Active scene is not valid.", "OK");
                return;
            }

            var primaryConfig = ResolveSelectedBakingConfig();
            if (primaryConfig != null && !primaryConfig.MatchesScene(scene))
            {
                primaryConfig = null;
            }

            var pcConfig = BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, BurtXGIProbeBakingPlatform.PC, out var resolvedPcConfig)
                ? resolvedPcConfig
                : null;
            var mobileConfig = BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, BurtXGIProbeBakingPlatform.Mobile, out var resolvedMobileConfig)
                ? resolvedMobileConfig
                : null;
            primaryConfig ??= pcConfig != null ? pcConfig : mobileConfig;
            var binding = BurtXGIProbeSceneBindingUtility.CreateOrRefresh(
                scene,
                primaryConfig,
                ResolveBestBakedAsset(primaryConfig),
                true,
                true);
            if (binding == null)
            {
                EditorUtility.DisplayDialog("BurtRP XGI", "Unable to create Burt XGI Probe Scene Binding.", "OK");
                return;
            }

            if (pcConfig != null)
            {
                binding.pcBakingConfig = pcConfig;
                binding.bakingConfig ??= pcConfig;
            }

            if (mobileConfig != null)
            {
                binding.mobileBakingConfig = mobileConfig;
            }

            binding.ApplyConfiguration();
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeObject = binding.gameObject;
            Debug.Log("Burt XGI probe scene binding refreshed for scene '" + scene.name + "'.");
        }

        private static BurtXGIProbeBakedDataAsset ResolveBestBakedAsset(BurtXGIProbeBakingConfig config)
        {
            if (config == null)
            {
                return null;
            }

            return config.TryGetBakedDataAssetForTimeSlice(BurtGIProbeVolume.ActiveTimeSlice, out var timeSliceAsset)
                ? timeSliceAsset
                : config.bakedDataAsset;
        }

        [MenuItem(CreateOrRefreshSceneBindingMenuPath, true)]
        private static bool ValidateCreateOrRefreshXGIProbeSceneBinding()
        {
            var scene = SceneManager.GetActiveScene();
            return scene.IsValid() && !Application.isPlaying;
        }

        [MenuItem(ProbeBakeMenuPath, false, 2500)]
        private static void BakeXGIProbeData()
        {
            var config = ResolveSelectedBakingConfig();
            BurtXGIProbeBakeAPI.BakeAsync(config, null, LogBakeResult);
        }

        [MenuItem(ProbeBakeMenuPath, true)]
        private static bool ValidateBakeXGIProbeData()
        {
            return !Application.isPlaying && !BurtXGIProbeBakeAPI.IsRunning;
        }

        [MenuItem(ProbeBakeAllTimeSlicesMenuPath, false, 2501)]
        private static void BakeAllXGIProbeTimeSlices()
        {
            var config = ResolveSelectedBakingConfig();
            BurtXGIProbeBakeAPI.BakeAllTimeSlicesAsync(config, null, LogBakeResult);
        }

        [MenuItem(ProbeBakeAllTimeSlicesMenuPath, true)]
        private static bool ValidateBakeAllXGIProbeTimeSlices()
        {
            return !Application.isPlaying && !BurtXGIProbeBakeAPI.IsRunning;
        }

        private static BurtXGIProbeBakingConfig ResolveSelectedBakingConfig()
        {
            if (Selection.activeObject is BurtXGIProbeBakingConfig selectedConfig)
            {
                return selectedConfig;
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                return null;
            }

            if (BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, BurtXGIProbeBakingPlatform.PC, out var pcConfig))
            {
                return pcConfig;
            }

            return BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, BurtXGIProbeBakingPlatform.Mobile, out var mobileConfig)
                ? mobileConfig
                : null;
        }

        private static void LogBakeResult(BurtXGIProbeBakeAPI.BakeResult result)
        {
            var message = "Burt XGI probe bake " + result.status + " in " +
                result.elapsedSeconds.ToString("0.###") + "s. " + result.error;
            if (result.status == BurtXGIProbeBakeAPI.BakeStatus.Success)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogWarning(message);
            }
        }

    }
}
