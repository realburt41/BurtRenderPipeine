using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Burt.RenderPipeline.Editor
{
    public static class BurtXGIProbeBakeAPI
    {
        internal const string TimeSliceDataRequiredMessage =
            "Burt XGI probe baking needs Time Slice Data for baked SH. Disable it only for structure/sky-visibility-only probe assets.";

        public enum BakeStatus
        {
            Success,
            Failed,
            Cancelled,
            Unsupported
        }

        public struct BakeResult
        {
            public BakeStatus status;
            public string error;
            public double elapsedSeconds;
        }

        public struct BakeProgress
        {
            public float progress;
            public string stepName;
            public string description;
        }

        public struct BakeValidation
        {
            public bool hasProbeVolume;
            public bool hasReadyProbeVolume;
            public bool hasVirtualProbeVolume;
            public bool hasPhysicalPool;
            public bool hasInitializedPhysicalPool;
            public bool hasBakingConfig;
            public bool hasProbeVolumeBounds;
            public int probeVolumeCount;
            public int readyProbeVolumeCount;
            public int virtualProbeVolumeCount;
            public int physicalPoolCount;
            public int initializedPhysicalPoolCount;
            public int bakingConfigCount;
            public Bounds probeVolumeBounds;
            public BurtXGIProbeBakingConfig bakingConfig;
            public string report;

            public bool HasRuntimeProbeData => hasReadyProbeVolume || hasInitializedPhysicalPool;
        }

        public static event Action<BakeResult> OnBakeCompleted;
        public static event Action<BakeProgress> OnBakeProgress;

        private static bool isRunning;
        private static readonly Stopwatch Stopwatch = new Stopwatch();
        private static readonly BurtGIProbeTimeSlice[] AllTimeSlices =
        {
            BurtGIProbeTimeSlice.Morning,
            BurtGIProbeTimeSlice.Day,
            BurtGIProbeTimeSlice.Sunset,
            BurtGIProbeTimeSlice.Night
        };

        public static bool IsRunning => isRunning;

        public static BakeValidation ValidateScene()
        {
            return ValidateScene(null);
        }

        public static BakeValidation ValidateScene(BurtXGIProbeBakingConfig config)
        {
            var volumes = UnityEngine.Object.FindObjectsOfType<BurtGIProbeVolume>(true);
            var physicalPools = UnityEngine.Object.FindObjectsOfType<BurtGIVirtualProbePhysicalPool>(true);
            var activeScene = SceneManager.GetActiveScene();
            var configs = FindBakingConfigs(config);
            var validation = new BakeValidation
            {
                bakingConfigCount = configs.Length,
                bakingConfig = config != null ? config : (configs.Length > 0 ? configs[0] : null)
            };
            validation.hasBakingConfig = validation.bakingConfig != null;
            var hasBounds = false;
            var bounds = default(Bounds);

            for (var i = 0; i < volumes.Length; i++)
            {
                var volume = volumes[i];
                if (volume == null)
                {
                    continue;
                }

                if (!IsObjectInScene(volume, activeScene))
                {
                    continue;
                }

                validation.probeVolumeCount++;
                validation.hasProbeVolume = true;
                if (BurtGIProbeVolumePositioning.TryGetVolumeBounds(volume, out var volumeBounds))
                {
                    if (!hasBounds)
                    {
                        bounds = volumeBounds.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(volumeBounds.bounds);
                    }
                }

                if (IsVirtualProbeVolume(volume))
                {
                    validation.hasVirtualProbeVolume = true;
                    validation.virtualProbeVolumeCount++;
                }

                if (IsReadyProbeVolume(volume))
                {
                    validation.hasReadyProbeVolume = true;
                    validation.readyProbeVolumeCount++;
                }
            }

            for (var i = 0; i < physicalPools.Length; i++)
            {
                var pool = physicalPools[i];
                if (pool == null)
                {
                    continue;
                }

                if (!IsObjectInScene(pool, activeScene))
                {
                    continue;
                }

                validation.physicalPoolCount++;
                validation.hasPhysicalPool = true;
                if (pool.IsInitialized)
                {
                    validation.hasInitializedPhysicalPool = true;
                    validation.initializedPhysicalPoolCount++;
                }
            }

            validation.hasProbeVolumeBounds = hasBounds;
            validation.probeVolumeBounds = bounds;
            if (validation.bakingConfig != null &&
                validation.bakingConfig.UpdateSceneBakeData(activeScene, validation.hasProbeVolume && hasBounds, bounds))
            {
                EditorUtility.SetDirty(validation.bakingConfig);
            }

            validation.report = BuildValidationReport(validation);
            return validation;
        }

        public static bool BakeAsync(
            Action<BakeProgress> onProgress = null,
            Action<BakeResult> onCompleted = null)
        {
            return BakeAsync(null, onProgress, onCompleted);
        }

        public static bool BakeAsync(
            BurtXGIProbeBakingConfig config,
            Action<BakeProgress> onProgress = null,
            Action<BakeResult> onCompleted = null)
        {
            if (isRunning)
            {
                Complete(BakeStatus.Failed, "Burt XGI probe bake is already running.", onCompleted);
                return false;
            }

            var validation = ValidateScene(config);
            var progress = new BakeProgress
            {
                progress = 0f,
                stepName = "Validate",
                description = validation.report
            };
            onProgress?.Invoke(progress);
            OnBakeProgress?.Invoke(progress);

            Stopwatch.Restart();
            isRunning = true;
            return RunBakeChain(validation, onProgress, onCompleted, 0f, 1f, string.Empty, true);
        }

        public static bool BakeAllTimeSlicesAsync(
            Action<BakeProgress> onProgress = null,
            Action<BakeResult> onCompleted = null)
        {
            return BakeAllTimeSlicesAsync(null, onProgress, onCompleted);
        }

        public static bool BakeAllTimeSlicesAsync(
            BurtXGIProbeBakingConfig config,
            Action<BakeProgress> onProgress = null,
            Action<BakeResult> onCompleted = null)
        {
            if (isRunning)
            {
                Complete(BakeStatus.Failed, "Burt XGI probe bake is already running.", onCompleted);
                return false;
            }

            var validation = ValidateScene(config);
            var validateProgress = new BakeProgress
            {
                progress = 0f,
                stepName = "Validate",
                description = validation.report
            };
            onProgress?.Invoke(validateProgress);
            OnBakeProgress?.Invoke(validateProgress);
            if (!validation.hasBakingConfig)
            {
                Stopwatch.Restart();
                isRunning = true;
                Complete(BakeStatus.Failed, "Burt XGI probe bake requires a BurtXGIProbeBakingConfig asset.", onCompleted);
                return false;
            }

            if (!validation.bakingConfig.useTimeSliceData)
            {
                Stopwatch.Restart();
                isRunning = true;
                Complete(BakeStatus.Unsupported, TimeSliceDataRequiredMessage, onCompleted);
                return false;
            }

            for (var i = 0; i < AllTimeSlices.Length; i++)
            {
                if (!validation.bakingConfig.SupportsTimeSliceBake(AllTimeSlices[i], out var timeSliceError))
                {
                    Stopwatch.Restart();
                    isRunning = true;
                    Complete(BakeStatus.Unsupported, timeSliceError, onCompleted);
                    return false;
                }
            }

            var originalSlice = validation.bakingConfig.timeSliceType;
            Stopwatch.Restart();
            isRunning = true;
            try
            {
                for (var i = 0; i < AllTimeSlices.Length; i++)
                {
                    var slice = AllTimeSlices[i];
                    validation.bakingConfig.SetActiveTimeSlice(slice);
                    EditorUtility.SetDirty(validation.bakingConfig);

                    var sliceProgressBase = i / (float)AllTimeSlices.Length;
                    var sliceProgressScale = 1f / AllTimeSlices.Length;
                    var sliceName = BurtGIProbeTimeSliceUtility.ToXRenderName(slice);
                    var slicePrefix = "TimeSlice " + sliceName + " ";
                    var sliceStarted = new BakeProgress
                    {
                        progress = sliceProgressBase,
                        stepName = slicePrefix + "Start",
                        description = "Bake Burt XGI probe data for " + sliceName + " (" + (i + 1) + "/" + AllTimeSlices.Length + ")."
                    };
                    onProgress?.Invoke(sliceStarted);
                    OnBakeProgress?.Invoke(sliceStarted);

                    if (!RunBakeChain(validation, onProgress, onCompleted, sliceProgressBase, sliceProgressScale, slicePrefix, false))
                    {
                        return false;
                    }
                }
            }
            finally
            {
                validation.bakingConfig.SetActiveTimeSlice(originalSlice);
                EditorUtility.SetDirty(validation.bakingConfig);
                AssetDatabase.SaveAssets();
            }

            Complete(
                BakeStatus.Success,
                "Burt XGI probe baking completed for all time slices and serialized to BurtXGIProbeBakedDataAsset assets.",
                onCompleted);
            return true;
        }

        private static bool RunBakeChain(
            BakeValidation validation,
            Action<BakeProgress> onProgress,
            Action<BakeResult> onCompleted,
            float progressBase,
            float progressScale,
            string stepPrefix,
            bool completeOnSuccess)
        {
            if (!validation.hasBakingConfig)
            {
                Complete(BakeStatus.Failed, "Burt XGI probe bake requires a BurtXGIProbeBakingConfig asset.", onCompleted);
                return false;
            }

            var prepareSucceeded = BurtXGIProbeBakingProcessor.Instance.Prepare(validation.bakingConfig, out var prepareResult);
            var prepareProgress = new BakeProgress
            {
                progress = ScaleProgress(progressBase, progressScale, prepareSucceeded ? 0.12f : 0f),
                stepName = stepPrefix + "Prepare",
                description = prepareResult.report
            };
            onProgress?.Invoke(prepareProgress);
            OnBakeProgress?.Invoke(prepareProgress);
            if (!prepareSucceeded)
            {
                Complete(BakeStatus.Failed, prepareResult.error, onCompleted);
                return false;
            }

            var placementSucceeded = BurtXGIProbeBakingProcessor.Instance.RunPlacementXRenderPath(validation.bakingConfig, out var placementResult);
            var placementProgress = new BakeProgress
            {
                progress = ScaleProgress(progressBase, progressScale, placementSucceeded ? 0.24f : 0.12f),
                stepName = stepPrefix + "Placement",
                description = placementResult.report
            };
            onProgress?.Invoke(placementProgress);
            OnBakeProgress?.Invoke(placementProgress);
            if (!placementSucceeded)
            {
                Complete(BakeStatus.Failed, placementResult.error, onCompleted);
                return false;
            }

            var virtualOffsetSucceeded = BurtXGIProbeBakingProcessor.Instance.RunVirtualOffsetXRenderPath(validation.bakingConfig, out var virtualOffsetResult);
            var virtualOffsetProgress = new BakeProgress
            {
                progress = ScaleProgress(progressBase, progressScale, virtualOffsetSucceeded ? 0.36f : 0.24f),
                stepName = stepPrefix + "VirtualOffset",
                description = virtualOffsetResult.report
            };
            onProgress?.Invoke(virtualOffsetProgress);
            OnBakeProgress?.Invoke(virtualOffsetProgress);
            if (!virtualOffsetSucceeded)
            {
                Complete(BakeStatus.Failed, virtualOffsetResult.error, onCompleted);
                return false;
            }

            var skyVisibilitySucceeded = BurtXGIProbeBakingProcessor.Instance.RunSkyVisibilityXRenderPath(validation.bakingConfig, out var skyVisibilityResult);
            var skyVisibilityProgress = new BakeProgress
            {
                progress = ScaleProgress(progressBase, progressScale, skyVisibilitySucceeded ? 0.48f : 0.36f),
                stepName = stepPrefix + "SkyVisibility",
                description = skyVisibilityResult.report
            };
            onProgress?.Invoke(skyVisibilityProgress);
            OnBakeProgress?.Invoke(skyVisibilityProgress);
            if (!skyVisibilitySucceeded)
            {
                Complete(BakeStatus.Failed, skyVisibilityResult.error, onCompleted);
                return false;
            }

            if (validation.bakingConfig.useTimeSliceData)
            {
                var timeSliceSucceeded = BurtXGIProbeBakingProcessor.Instance.RunTimeSliceXRenderPath(validation.bakingConfig, out var timeSliceResult);
                var timeSliceProgress = new BakeProgress
                {
                    progress = ScaleProgress(progressBase, progressScale, timeSliceSucceeded ? 0.6f : 0.48f),
                    stepName = stepPrefix + "TimeSliceData",
                    description = timeSliceResult.report
                };
                onProgress?.Invoke(timeSliceProgress);
                OnBakeProgress?.Invoke(timeSliceProgress);
                if (!timeSliceSucceeded)
                {
                    Complete(BakeStatus.Failed, timeSliceResult.error, onCompleted);
                    return false;
                }
            }
            else
            {
                var timeSliceProgress = new BakeProgress
                {
                    progress = ScaleProgress(progressBase, progressScale, 0.6f),
                    stepName = stepPrefix + "TimeSliceData",
                    description = "[BurtRP][XGIProbeBakingTimeSlice]\nEnabled=False\nStatus=SkippedNoTimeSliceData"
                };
                onProgress?.Invoke(timeSliceProgress);
                OnBakeProgress?.Invoke(timeSliceProgress);
            }

            var finalizeCellsSucceeded = BurtXGIProbeBakingProcessor.Instance.RunFinalizeCellsLite(validation.bakingConfig, out var finalizeCellsResult);
            var finalizeCellsProgress = new BakeProgress
            {
                progress = ScaleProgress(progressBase, progressScale, finalizeCellsSucceeded ? 0.72f : 0.6f),
                stepName = stepPrefix + "FinalizeCells",
                description = finalizeCellsResult.report
            };
            onProgress?.Invoke(finalizeCellsProgress);
            OnBakeProgress?.Invoke(finalizeCellsProgress);
            if (!finalizeCellsSucceeded)
            {
                Complete(BakeStatus.Failed, finalizeCellsResult.error, onCompleted);
                return false;
            }

            var serializationSucceeded = BurtXGIProbeBakingProcessor.Instance.RunSerializationLite(validation.bakingConfig, out var serializationResult);
            var serializationProgress = new BakeProgress
            {
                progress = ScaleProgress(progressBase, progressScale, serializationSucceeded ? 1f : 0.72f),
                stepName = stepPrefix + "Serialization",
                description = serializationResult.report
            };
            onProgress?.Invoke(serializationProgress);
            OnBakeProgress?.Invoke(serializationProgress);
            if (!serializationSucceeded)
            {
                Complete(BakeStatus.Failed, serializationResult.error, onCompleted);
                return false;
            }

            if (completeOnSuccess)
            {
                Complete(
                    BakeStatus.Success,
                    "Burt XGI probe baking completed and serialized to a BurtXGIProbeBakedDataAsset.",
                    onCompleted);
            }

            return true;
        }

        private static float ScaleProgress(float progressBase, float progressScale, float localProgress)
        {
            return Mathf.Clamp01(progressBase + progressScale * Mathf.Clamp01(localProgress));
        }

        public static bool Cancel()
        {
            if (!isRunning)
            {
                return false;
            }

            Complete(BakeStatus.Cancelled, "Burt XGI probe bake cancelled.", null);
            return true;
        }

        private static void Complete(BakeStatus status, string error, Action<BakeResult> onCompleted)
        {
            Stopwatch.Stop();
            var result = new BakeResult
            {
                status = status,
                error = error,
                elapsedSeconds = Stopwatch.Elapsed.TotalSeconds
            };
            isRunning = false;
            onCompleted?.Invoke(result);
            OnBakeCompleted?.Invoke(result);
        }

        private static bool IsReadyProbeVolume(BurtGIProbeVolume volume)
        {
            if (volume == null || !volume.isActiveAndEnabled || volume.extent <= 0.01f || volume.intensity <= 0f)
            {
                return false;
            }

            if (volume.irradiance != null)
            {
                return true;
            }

            return IsVirtualProbeVolume(volume) &&
                volume.virtualPageTable != null &&
                volume.virtualIndirection != null &&
                volume.virtualL0L1Rx != null &&
                volume.virtualL1GL1Ry != null &&
                volume.virtualL1BL1Rz != null;
        }

        private static bool IsVirtualProbeVolume(BurtGIProbeVolume volume)
        {
            return volume != null && volume.useVirtualProbeData;
        }

        private static bool IsObjectInScene(Component component, Scene scene)
        {
            return component != null && (!scene.IsValid() || component.gameObject.scene == scene);
        }

        private static BurtXGIProbeBakingConfig[] FindBakingConfigs(BurtXGIProbeBakingConfig preferredConfig)
        {
            if (preferredConfig != null)
            {
                return new[] { preferredConfig };
            }

            var configs = new List<BurtXGIProbeBakingConfig>();
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
            {
                if (BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, BurtXGIProbeBakingPlatform.PC, out var pcConfig))
                {
                    AddUniqueConfig(configs, pcConfig);
                }

                if (BurtXGIProbeBakingConfig.TryGetBakingConfigForScene(scene, BurtXGIProbeBakingPlatform.Mobile, out var mobileConfig))
                {
                    AddUniqueConfig(configs, mobileConfig);
                }
            }

            var guids = AssetDatabase.FindAssets("t:BurtXGIProbeBakingConfig");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var config = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(path);
                if (config == null)
                {
                    continue;
                }

                AddUniqueConfig(configs, config);
            }

            return configs.ToArray();
        }

        private static void AddUniqueConfig(List<BurtXGIProbeBakingConfig> configs, BurtXGIProbeBakingConfig config)
        {
            if (config != null && !configs.Contains(config))
            {
                configs.Add(config);
            }
        }

        private static string BuildValidationReport(BakeValidation validation)
        {
            var builder = new StringBuilder(2048);
            builder.AppendLine("[BurtRP][XGIProbeBakeValidation]");
            builder.AppendLine("BakingConfigs=" + validation.bakingConfigCount +
                " Active=" + (validation.bakingConfig != null ? validation.bakingConfig.name : "None"));
            if (validation.bakingConfig != null)
            {
                var config = validation.bakingConfig;
                builder.AppendLine("ConfigLayout=CellSizeMeters=" + config.CellSizeInMeters.ToString("0.###") +
                    ",ChunkProbes=" + config.ChunkProbeCount +
                    ",SH=" + config.systemParameters.shBands +
                    ",GPUChunkBytes=" + config.ChunkGPUMemoryBytes +
                    ",SkyVisibility=" + config.skyVisibility +
                    ",TimeSlice=" + config.useTimeSliceData);
                builder.AppendLine("ConfigLegacyXRender=StreamingLayout=" + config.HasLegacyXRenderStreamingLayout +
                    ",ImportRequired=" + config.RequiresLegacyXRenderProbeDataImport +
                    ",ImportedBakedData=" + (config.bakedDataAsset != null || config.HasTimeSliceBakedDataAssets));
                if (config.RequiresLegacyXRenderProbeDataImport)
                {
                    builder.AppendLine("ConfigLegacyXRender=Run Burt.RenderPipeline.Editor.BurtXGILegacyProbeDataImporter.ImportExternalFromCommandLine with -burtXGILegacySource and -burtXGILegacyTarget to convert raw XRender probe streams into BurtXGIProbeBakedDataAsset.");
                }
                if (!config.SupportsCurrentTimeSliceBake(out var timeSliceError))
                {
                    builder.AppendLine("ConfigTimeSlice=Unsupported(" + timeSliceError + ")");
                }
                if (!config.useTimeSliceData)
                {
                    builder.AppendLine("ConfigTimeSlice=Disabled(StructureAndSkyVisibilityOnly)");
                }
            }
            builder.AppendLine("ProbeVolumes=" + validation.probeVolumeCount +
                " Ready=" + validation.readyProbeVolumeCount +
                " Virtual=" + validation.virtualProbeVolumeCount);
            builder.AppendLine("SceneBakeData=HasBounds=" + validation.hasProbeVolumeBounds +
                ",BoundsCenter=" + validation.probeVolumeBounds.center +
                ",BoundsSize=" + validation.probeVolumeBounds.size);
            builder.AppendLine("PhysicalPools=" + validation.physicalPoolCount +
                " Initialized=" + validation.initializedPhysicalPoolCount);
            builder.AppendLine("RuntimeProbeData=" + (validation.HasRuntimeProbeData ? "Ready" : "Missing"));
            builder.AppendLine("PlacementGpu=" + BurtXGIProbeBakingProcessor.ResolvePlacementGpuStatusLabel());
            builder.AppendLine(BurtScreenSpaceGlobalIlluminationDiagnosticsUtility.ResolveXGIResourceStatusReport());
            builder.Append("BakeProcessor=PreparePlacementVirtualOffsetRayTracingSkyVisibilityRayTracingOrGpuComputeTimeSliceRayTracingOrGpuComputeFinalizeCellsSerializationReady");
            return builder.ToString();
        }
    }
}
