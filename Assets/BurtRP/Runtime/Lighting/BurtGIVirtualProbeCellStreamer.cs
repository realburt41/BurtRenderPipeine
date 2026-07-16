using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace Burt.RenderPipeline
{
    [DisallowMultipleComponent]
    public sealed class BurtGIVirtualProbeCellStreamer : MonoBehaviour
    {
        public enum BinaryCompression
        {
            None = 0,
            Zstd = 1
        }

        [Serializable]
        public sealed class BinarySlice
        {
            [Tooltip("Optional shared cell blob. Leave empty to use the legacy TextAsset field beside this slice.")]
            public TextAsset source;
            [Min(0)] public int byteOffset;
            [Min(0)] public int byteLength;
            public BinaryCompression compression;
            [Min(0)] public int decompressedByteLength;
        }

        [Serializable]
        public sealed class Chunk
        {
            public int physicalChunkIndex;
            [Tooltip("Optional XRender shared-data chunk index for validity, sky visibility, and sky shading direction. Negative values reuse physicalChunkIndex.")]
            public int sharedPhysicalChunkIndex = -1;
            public TextAsset l0L1Rx;
            public BinarySlice l0L1RxSlice = new BinarySlice();
            public TextAsset l1GL1Ry;
            public BinarySlice l1GL1RySlice = new BinarySlice();
            public TextAsset l1BL1Rz;
            public BinarySlice l1BL1RzSlice = new BinarySlice();
            public TextAsset l20;
            public BinarySlice l20Slice = new BinarySlice();
            public TextAsset l21;
            public BinarySlice l21Slice = new BinarySlice();
            public TextAsset l22;
            public BinarySlice l22Slice = new BinarySlice();
            public TextAsset l23;
            public BinarySlice l23Slice = new BinarySlice();
            public TextAsset validity;
            public BinarySlice validitySlice = new BinarySlice();
            public TextAsset skyVisibilityL0L1;
            public BinarySlice skyVisibilityL0L1Slice = new BinarySlice();
            public TextAsset skyShadingDirectionIndices;
            public BinarySlice skyShadingDirectionIndicesSlice = new BinarySlice();
        }

        [Serializable]
        public sealed class Cell
        {
            public int index;
            public Vector3 worldPosition;
            [Min(0.01f)] public float loadDistance = 64f;
            public List<Chunk> chunks = new List<Chunk>();
            public int pageTableDestinationIndex;
            public TextAsset pageTableEntries;
            public BinarySlice pageTableEntriesSlice = new BinarySlice();
            public int indirectionDestinationIndex;
            public TextAsset indirectionEntries;
            public BinarySlice indirectionEntriesSlice = new BinarySlice();
        }

        [Serializable]
        public sealed class TimeSliceBakedData
        {
            public BurtGIProbeTimeSlice timeSlice = BurtGIProbeTimeSlice.Day;
            public BurtXGIProbeBakedDataAsset asset;
        }

        public BurtGIProbeVolume probeVolume;
        public BurtGIVirtualProbePhysicalPool physicalPool;
        public BurtXGIProbeBakedDataAsset bakedDataAsset;
        public string streamingSceneGuid = string.Empty;
        public List<TimeSliceBakedData> timeSliceBakedDataAssets = new List<TimeSliceBakedData>();
        [Min(0.01f)] public float bakedDataLoadDistance = 64f;
        public bool automaticStreaming = true;
        [Min(1)] public int maxCellsToLoadPerFrame = 1;
        [Min(1)] public int runtimePageTableEntryCount = 243;
        [Min(1)] public int runtimeIndirectionEntryCount = 1;

        [Header("Runtime Platform Clamp")]
        public bool runtimeSupportXGIProbe = true;
        public bool runtimeEnableShading = true;
        public bool runtimeEnableSkyVisibility = true;
        public BurtXGIProbeTextureMemoryBudget runtimeMemoryBudgetLimit = BurtXGIProbeTextureMemoryBudget.Film;
        public BurtXGIProbeSHBands runtimeSHBandsLimit = BurtXGIProbeSHBands.SphericalHarmonicsL2;
        public bool runtimeOverrideNormalBias;
        public float runtimeNormalBias;
        public bool runtimeOverrideViewBias;
        public float runtimeViewBias;
        public bool runtimeOverrideLightIntensity;
        [Min(0f)] public float runtimeLightIntensity = 1.5f;
        public bool runtimeOverrideSkyVisibilityIntensity;
        [Range(0f, 1f)] public float runtimeSkyVisibilityIntensity = 1f;
        public bool runtimeOverrideSkyVisibilityOffset;
        public float runtimeSkyVisibilityOffset;

        public List<Cell> cells = new List<Cell>();

        private readonly HashSet<int> loadedCells = new HashSet<int>();
        private readonly Dictionary<int, int> loadedChunkOwners = new Dictionary<int, int>();
        private readonly Dictionary<int, int> loadedSharedChunkOwners = new Dictionary<int, int>();
        private readonly Dictionary<int, List<RuntimeChunkMapping>> loadedBakedChunkMappings = new Dictionary<int, List<RuntimeChunkMapping>>();
        private readonly Dictionary<BinarySlice, byte[]> resolvedSlices = new Dictionary<BinarySlice, byte[]>();
        private static readonly List<BurtGIVirtualProbeCellStreamer> ActiveStreamers = new List<BurtGIVirtualProbeCellStreamer>();
        private BurtXGIProbeBakedDataAsset initializedBakedDataAsset;
        private string initializedStreamingSceneGuid = string.Empty;
        private bool initialized;
        private string lastStreamingStatus = "Idle";

        public int LoadedCellCount => loadedCells.Count;
        public int ConfiguredCellCount => (cells != null ? cells.Count : 0) + CountRuntimeBakedCells(ActiveBakedDataAsset);
        public int LoadedPhysicalChunkCount => loadedChunkOwners.Count;
        public int LoadedSharedChunkCount => loadedSharedChunkOwners.Count;
        public int OccupiedRuntimeChunkCount => CountOccupiedRuntimeChunks();
        public int ResolvedSliceCount => resolvedSlices.Count;
        public bool IsInitialized => initialized;
        public string LastStreamingStatus => lastStreamingStatus;
        public bool HasTimeSliceBakedDataAssets => timeSliceBakedDataAssets != null && timeSliceBakedDataAssets.Exists(entry => entry != null && entry.asset != null);
        public BurtXGIProbeBakedDataAsset ActiveBakedDataAsset => ResolveActiveBakedDataAsset();
        public bool IsCellLoaded(int cellIndex) => loadedCells.Contains(cellIndex);
        internal bool TryResolveRuntimePhysicalChunkIndex(int cellIndex, int bakedChunkIndex, out int runtimeChunkIndex)
        {
            runtimeChunkIndex = -1;
            if (bakedChunkIndex < 0)
            {
                return false;
            }

            if (loadedBakedChunkMappings.TryGetValue(cellIndex, out var mappings) &&
                TryGetRuntimeChunkIndex(mappings, bakedChunkIndex, out runtimeChunkIndex))
            {
                return true;
            }

            if (loadedChunkOwners.TryGetValue(bakedChunkIndex, out var ownerCellIndex) && ownerCellIndex == cellIndex)
            {
                runtimeChunkIndex = bakedChunkIndex;
                return true;
            }

            return false;
        }

        private readonly struct StreamingCandidate
        {
            internal readonly int Index;
            internal readonly Vector3 WorldPosition;
            internal readonly Vector3 ScorePosition;

            internal StreamingCandidate(int index, Vector3 worldPosition)
                : this(index, worldPosition, worldPosition)
            {
            }

            internal StreamingCandidate(int index, Vector3 worldPosition, Vector3 scorePosition)
            {
                Index = index;
                WorldPosition = worldPosition;
                ScorePosition = scorePosition;
            }
        }

        private readonly struct RuntimeChunkMapping
        {
            internal readonly int BakedChunkIndex;
            internal readonly int RuntimeChunkIndex;
            internal readonly int BakedSharedChunkIndex;
            internal readonly int RuntimeSharedChunkIndex;

            internal RuntimeChunkMapping(int bakedChunkIndex, int runtimeChunkIndex, int bakedSharedChunkIndex, int runtimeSharedChunkIndex)
            {
                BakedChunkIndex = bakedChunkIndex;
                RuntimeChunkIndex = runtimeChunkIndex;
                BakedSharedChunkIndex = bakedSharedChunkIndex;
                RuntimeSharedChunkIndex = runtimeSharedChunkIndex;
            }
        }

        private void OnEnable()
        {
            if (!ActiveStreamers.Contains(this)) ActiveStreamers.Add(this);
        }

        private void OnDisable()
        {
            ActiveStreamers.Remove(this);
            UnloadAllCells();
            loadedChunkOwners.Clear();
            loadedSharedChunkOwners.Clear();
            loadedBakedChunkMappings.Clear();
            resolvedSlices.Clear();
            initialized = false;
            initializedBakedDataAsset = null;
            initializedStreamingSceneGuid = string.Empty;
            probeVolume?.ClearVirtualProbeRuntimeBuffers();
        }

        private BurtXGIProbeBakedDataAsset ResolveActiveBakedDataAsset()
        {
            if (HasTimeSliceBakedDataAssets)
            {
                var activeSlice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(BurtGIProbeVolume.ActiveTimeSlice);
                BurtXGIProbeBakedDataAsset fallbackAsset = null;
                for (var i = 0; i < timeSliceBakedDataAssets.Count; i++)
                {
                    var entry = timeSliceBakedDataAssets[i];
                    if (entry == null || entry.asset == null)
                    {
                        continue;
                    }

                    fallbackAsset ??= entry.asset;
                    if (BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice) == activeSlice)
                    {
                        return entry.asset;
                    }
                }

                if (fallbackAsset != null)
                {
                    return fallbackAsset;
                }
            }

            return bakedDataAsset;
        }

        private void ApplyActiveBakedDataAssetToProbeVolume(BurtXGIProbeBakedDataAsset asset)
        {
            if (probeVolume == null || asset == null)
            {
                return;
            }

            probeVolume.useVirtualProbeData = true;
            probeVolume.virtualIndirectionDimensions = Vector3Int.Max(Vector3Int.one, asset.virtualIndirectionDimensions);
            probeVolume.virtualMinEntryIndex = asset.virtualMinEntryPosition;
            probeVolume.virtualIndirectionEntrySize = asset.entriesPerCellDimension > 0
                ? asset.ResolvedCellSizeInMeters / Mathf.Max(1, asset.entriesPerCellDimension)
                : asset.minBrickSize;
            probeVolume.virtualMinBrickSize = asset.minBrickSize;
            probeVolume.virtualPositionOffset = asset.probeOffset;
            probeVolume.virtualTimeSliceMainLightIntensity = asset.timeSliceMainLightIntensity;
            asset.ApplyRuntimeSettings(probeVolume);
            ApplyRuntimePlatformClampToProbeVolume(asset);
            if (HasTimeSliceBakedDataAssets)
            {
                probeVolume.useTimeSlice = false;
            }
            else if (asset.hasTimeSliceSH)
            {
                probeVolume.useTimeSlice = true;
                probeVolume.timeSlice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(asset.timeSliceType);
            }
        }

        private void ApplyRuntimePlatformClampToProbeVolume(BurtXGIProbeBakedDataAsset asset)
        {
            if (probeVolume == null || asset == null)
            {
                return;
            }

            probeVolume.virtualEnableShading = probeVolume.virtualEnableShading && runtimeSupportXGIProbe && runtimeEnableShading;
            probeVolume.virtualSHBands = ResolveRuntimeSHBands(asset);
            var runtimeSkyVisibilityEnabled = ResolveRuntimeEnableSkyVisibility(asset);

            if (runtimeOverrideNormalBias)
            {
                probeVolume.virtualNormalBias = runtimeNormalBias;
            }

            if (runtimeOverrideViewBias)
            {
                probeVolume.virtualViewBias = runtimeViewBias;
            }

            if (runtimeOverrideLightIntensity)
            {
                probeVolume.virtualLightIntensity = Mathf.Max(0f, runtimeLightIntensity);
            }

            if (runtimeOverrideSkyVisibilityIntensity && runtimeSkyVisibilityEnabled)
            {
                probeVolume.virtualSkyVisibilityIntensity = Mathf.Clamp01(runtimeSkyVisibilityIntensity);
            }

            if (runtimeOverrideSkyVisibilityOffset)
            {
                probeVolume.virtualSkyVisibilityOffset = runtimeSkyVisibilityOffset;
            }

            if (!runtimeSkyVisibilityEnabled)
            {
                probeVolume.virtualSkyVisibilityIntensity = 0f;
            }
        }

        internal static void UpdateForCamera(Camera camera)
        {
            if (camera == null) return;
            for (var index = ActiveStreamers.Count - 1; index >= 0; --index)
            {
                var streamer = ActiveStreamers[index];
                if (streamer == null)
                {
                    ActiveStreamers.RemoveAt(index);
                    continue;
                }

                if (streamer.probeVolume == null ||
                    (!streamer.HasTimeSliceBakedDataAssets && !streamer.probeVolume.IsActiveForCurrentTimeSlice))
                {
                    continue;
                }

                streamer.UpdateStreamingForCamera(camera);
            }
        }

        internal static int CopyActiveStreamers(List<BurtGIVirtualProbeCellStreamer> destination)
        {
            if (destination == null)
            {
                return 0;
            }

            destination.Clear();
            for (var index = ActiveStreamers.Count - 1; index >= 0; --index)
            {
                var streamer = ActiveStreamers[index];
                if (streamer == null)
                {
                    ActiveStreamers.RemoveAt(index);
                    continue;
                }

                if (streamer.probeVolume == null ||
                    (!streamer.HasTimeSliceBakedDataAssets && !streamer.probeVolume.IsActiveForCurrentTimeSlice))
                {
                    continue;
                }

                destination.Add(streamer);
            }

            return destination.Count;
        }

        public static bool TryGetForProbeVolume(BurtGIProbeVolume volume, out BurtGIVirtualProbeCellStreamer streamer)
        {
            streamer = null;
            if (volume == null)
            {
                return false;
            }

            for (var index = ActiveStreamers.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveStreamers[index];
                if (candidate == null)
                {
                    ActiveStreamers.RemoveAt(index);
                    continue;
                }

                if (candidate.probeVolume == volume)
                {
                    streamer = candidate;
                    return true;
                }
            }

            return false;
        }

        internal static void InvalidateActiveTimeSliceStreaming()
        {
            for (var index = ActiveStreamers.Count - 1; index >= 0; --index)
            {
                var streamer = ActiveStreamers[index];
                if (streamer == null)
                {
                    ActiveStreamers.RemoveAt(index);
                    continue;
                }

                if (streamer.HasTimeSliceBakedDataAssets)
                {
                    streamer.InvalidateStreamingState();
                    streamer.ApplyActiveBakedDataAssetToProbeVolume(streamer.ActiveBakedDataAsset);
                }
            }
        }

        public bool InitializeStreaming()
        {
            if (probeVolume == null || physicalPool == null || physicalPool.probeVolume != probeVolume)
            {
                initialized = false;
                initializedBakedDataAsset = null;
                initializedStreamingSceneGuid = string.Empty;
                lastStreamingStatus = "InitFailed(MissingVolumeOrPoolBinding)";
                return false;
            }

            var activeAsset = ActiveBakedDataAsset;
            if (loadedCells.Count > 0)
            {
                UnloadAllCells();
            }

            initialized = false;
            initializedBakedDataAsset = null;
            initializedStreamingSceneGuid = string.Empty;
            ApplyActiveBakedDataAssetToProbeVolume(activeAsset);
            if (activeAsset == null && HasTimeSliceBakedDataAssets)
            {
                lastStreamingStatus = "InitSkipped(MissingTimeSliceAsset=" + BurtGIProbeVolume.ActiveTimeSlice + ")";
                return false;
            }

            if (activeAsset != null && (!activeAsset.RuntimeSystemEnabled || !runtimeSupportXGIProbe))
            {
                lastStreamingStatus = !runtimeSupportXGIProbe
                    ? "InitSkipped(RuntimeSupportXGIProbe=false)"
                    : "InitSkipped(SystemParametersEnable=false)";
                return false;
            }

            if (activeAsset != null &&
                !activeAsset.TryValidateRuntimeLoadData(
                    streamingSceneGuid,
                    ResolveRuntimeSHBands(activeAsset),
                    runtimeSupportXGIProbe && runtimeEnableSkyVisibility,
                    runtimeSupportXGIProbe && runtimeEnableSkyVisibility,
                    out _,
                    out var bakedDataFailReason))
            {
                lastStreamingStatus = "InitFailed(BakedDataInvalid " + bakedDataFailReason + ")";
                return false;
            }

            if (activeAsset != null && activeAsset.chunkCount > 0)
            {
                physicalPool.chunkDimensions = ResolveRuntimePhysicalPoolChunkDimensions(activeAsset);
            }

            var physicalPoolLayoutChanged = ConfigurePhysicalPoolForActiveData(activeAsset);
            if (physicalPool.IsInitialized &&
                (physicalPoolLayoutChanged || probeVolume.virtualPhysicalPoolDimensions != physicalPool.PhysicalPoolDimensions))
            {
                physicalPool.ReleasePool();
            }

            if (!physicalPool.IsInitialized && !physicalPool.InitializePool())
            {
                lastStreamingStatus = "InitFailed(PhysicalPool)";
                return false;
            }

            var pageTableEntryCount = ResolveRuntimePageTableEntryCount();
            var indirectionEntryCount = ResolveRuntimeIndirectionEntryCount();
            if (!probeVolume.TryAllocateVirtualProbeRuntimeBuffers(pageTableEntryCount, indirectionEntryCount))
            {
                lastStreamingStatus = "InitFailed(RuntimeBuffers PageTable=" + pageTableEntryCount + " Indirection=" + indirectionEntryCount + ")";
                return false;
            }

            var emptyPageTable = new uint[pageTableEntryCount];
            for (var index = 0; index < emptyPageTable.Length; ++index) emptyPageTable[index] = uint.MaxValue;
            var emptyIndirection = new Vector3Int[indirectionEntryCount];
            for (var index = 0; index < emptyIndirection.Length; ++index) emptyIndirection[index] = new Vector3Int(-1, 0, 0);
            loadedCells.Clear();
            loadedChunkOwners.Clear();
            loadedSharedChunkOwners.Clear();
            loadedBakedChunkMappings.Clear();
            initialized = probeVolume.TryUpdateVirtualPageTable(emptyPageTable, 0, 0, emptyPageTable.Length) &&
                probeVolume.TryUpdateVirtualIndirection(emptyIndirection, 0, 0, emptyIndirection.Length);
            initializedBakedDataAsset = initialized ? activeAsset : null;
            initializedStreamingSceneGuid = initialized ? NormalizeStreamingSceneGuid(streamingSceneGuid) : string.Empty;
            UpdateProbeVolumeLoadedEntryBounds();
            lastStreamingStatus = initialized
                ? "Initialized(PageTable=" + pageTableEntryCount + ",Indirection=" + indirectionEntryCount + ",PoolChunks=" + physicalPool.ChunkCapacity + ")"
                : "InitFailed(ClearVirtualBuffers)";
            return initialized;
        }

        private bool ConfigurePhysicalPoolForActiveData(BurtXGIProbeBakedDataAsset activeAsset)
        {
            var changed = false;
            var requiresValidity = RequiresValidityTexture(activeAsset);
            var requiresSkyVisibility = runtimeSupportXGIProbe && runtimeEnableSkyVisibility && RequiresSkyVisibilityTexture(activeAsset);
            var requiresSkyShadingDirection = requiresSkyVisibility && RequiresSkyShadingDirectionTexture(activeAsset);
            var requiresL2 = RequiresL2Textures(activeAsset);

            if (physicalPool.allocateValidity != requiresValidity)
            {
                physicalPool.allocateValidity = requiresValidity;
                changed = true;
            }

            if (physicalPool.allocateSkyVisibility != requiresSkyVisibility)
            {
                physicalPool.allocateSkyVisibility = requiresSkyVisibility;
                changed = true;
            }

            if (physicalPool.allocateSkyShadingDirection != requiresSkyShadingDirection)
            {
                physicalPool.allocateSkyShadingDirection = requiresSkyShadingDirection;
                changed = true;
            }

            if (physicalPool.allocateL2 != requiresL2)
            {
                physicalPool.allocateL2 = requiresL2;
                changed = true;
            }

            return changed;
        }

        private Vector3Int ResolveRuntimePhysicalPoolChunkDimensions(BurtXGIProbeBakedDataAsset activeAsset)
        {
            if (activeAsset == null)
            {
                return Vector3Int.one;
            }

            var budgetDimensions = ResolveMemoryBudgetChunkDimensions(ResolveRuntimeMemoryBudget(activeAsset));
            var largestCellRuntimeChunkCount = ResolveLargestCellRuntimeChunkCount(activeAsset);
            var requiredCapacity = Mathf.Max(1, largestCellRuntimeChunkCount);
            if (GetChunkCapacity(budgetDimensions) >= requiredCapacity)
            {
                return budgetDimensions;
            }

            return GrowChunkDimensionsToCapacity(budgetDimensions, requiredCapacity);
        }

        private static Vector3Int ResolveMemoryBudgetChunkDimensions(BurtXGIProbeTextureMemoryBudget memoryBudget)
        {
            var textureSize = ResolveMemoryBudgetTextureSize(memoryBudget);
            var maxTextureSize = Mathf.Max(1, SystemInfo.maxTexture3DSize);
            var width = Mathf.Min(textureSize, maxTextureSize);
            var height = Mathf.Min(textureSize, maxTextureSize);
            var depth = Mathf.Min(BurtGIVirtualProbePhysicalPool.ChunkDepth, maxTextureSize);
            return new Vector3Int(
                Mathf.Max(1, width / BurtGIVirtualProbePhysicalPool.ChunkWidth),
                Mathf.Max(1, height / BurtGIVirtualProbePhysicalPool.ChunkHeight),
                Mathf.Max(1, depth / BurtGIVirtualProbePhysicalPool.ChunkDepth));
        }

        private static int ResolveMemoryBudgetTextureSize(BurtXGIProbeTextureMemoryBudget memoryBudget)
        {
            switch (memoryBudget)
            {
                case BurtXGIProbeTextureMemoryBudget.Low:
                    return 512;
                case BurtXGIProbeTextureMemoryBudget.High:
                    return 1024;
                case BurtXGIProbeTextureMemoryBudget.Ultra:
                    return 1448;
                case BurtXGIProbeTextureMemoryBudget.Film:
                    return 2048;
                case BurtXGIProbeTextureMemoryBudget.Medium:
                default:
                    return 724;
            }
        }

        private static int ResolveLargestCellRuntimeChunkCount(BurtXGIProbeBakedDataAsset activeAsset)
        {
            var largest = 1;
            if (activeAsset?.cells == null)
            {
                return largest;
            }

            for (var index = 0; index < activeAsset.cells.Length; index++)
            {
                largest = Mathf.Max(largest, CountUniqueBakedPhysicalChunks(activeAsset.cells[index]));
            }

            return largest;
        }

        private static Vector3Int GrowChunkDimensionsToCapacity(Vector3Int baseDimensions, int requiredCapacity)
        {
            var dimensions = Vector3Int.Max(Vector3Int.one, baseDimensions);
            while (GetChunkCapacity(dimensions) < requiredCapacity)
            {
                dimensions.x++;
            }

            return dimensions;
        }

        private static int GetChunkCapacity(Vector3Int dimensions)
        {
            var safeDimensions = Vector3Int.Max(Vector3Int.one, dimensions);
            return safeDimensions.x * safeDimensions.y * safeDimensions.z;
        }

        private bool RequiresValidityTexture(BurtXGIProbeBakedDataAsset activeAsset)
        {
            if (activeAsset != null && activeAsset.HasBakedValidityData)
            {
                return true;
            }

            return HasManualChunkData(chunk => HasChunkSource(chunk?.validity, chunk?.validitySlice));
        }

        private bool RequiresSkyVisibilityTexture(BurtXGIProbeBakedDataAsset activeAsset)
        {
            if (activeAsset != null && activeAsset.HasBakedSkyVisibilityData)
            {
                return true;
            }

            return HasManualChunkData(chunk => HasChunkSource(chunk?.skyVisibilityL0L1, chunk?.skyVisibilityL0L1Slice));
        }

        private bool RequiresSkyShadingDirectionTexture(BurtXGIProbeBakedDataAsset activeAsset)
        {
            if (activeAsset != null && activeAsset.HasBakedSkyShadingDirectionData)
            {
                return true;
            }

            return HasManualChunkData(chunk => HasChunkSource(chunk?.skyShadingDirectionIndices, chunk?.skyShadingDirectionIndicesSlice));
        }

        private bool RequiresL2Textures(BurtXGIProbeBakedDataAsset activeAsset)
        {
            if (activeAsset != null && activeAsset.HasBakedL2Data && ResolveRuntimeAllowsL2Data(activeAsset))
            {
                return true;
            }

            return HasManualChunkData(chunk =>
                HasChunkSource(chunk?.l20, chunk?.l20Slice) ||
                HasChunkSource(chunk?.l21, chunk?.l21Slice) ||
                HasChunkSource(chunk?.l22, chunk?.l22Slice) ||
                HasChunkSource(chunk?.l23, chunk?.l23Slice));
        }

        private BurtXGIProbeTextureMemoryBudget ResolveRuntimeMemoryBudget(BurtXGIProbeBakedDataAsset activeAsset)
        {
            var assetBudget = activeAsset != null
                ? activeAsset.RuntimeMemoryBudget
                : BurtXGIProbeTextureMemoryBudget.Medium;
            return (BurtXGIProbeTextureMemoryBudget)Mathf.Min((int)assetBudget, (int)runtimeMemoryBudgetLimit);
        }

        private BurtXGIProbeSHBands ResolveRuntimeSHBands(BurtXGIProbeBakedDataAsset activeAsset)
        {
            var assetBands = activeAsset != null
                ? activeAsset.RuntimeSHBands
                : BurtXGIProbeSHBands.SphericalHarmonicsL1;
            return BurtXGIProbeSHBandsUtility.MinQuality(assetBands, runtimeSHBandsLimit);
        }

        private bool ResolveRuntimeAllowsL2Data(BurtXGIProbeBakedDataAsset activeAsset)
        {
            var bands = ResolveRuntimeSHBands(activeAsset);
            return bands.HasL2();
        }

        private bool ResolveRuntimeEnableSkyVisibility(BurtXGIProbeBakedDataAsset activeAsset)
        {
            return runtimeSupportXGIProbe && runtimeEnableSkyVisibility && activeAsset != null && activeAsset.HasBakedSkyVisibilityData;
        }

        private bool HasManualChunkData(Func<Chunk, bool> predicate)
        {
            if (cells == null || predicate == null)
            {
                return false;
            }

            for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                var chunks = cells[cellIndex]?.chunks;
                if (chunks == null)
                {
                    continue;
                }

                for (var chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
                {
                    if (predicate(chunks[chunkIndex]))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasChunkSource(TextAsset legacyAsset, BinarySlice slice)
        {
            return legacyAsset != null || slice != null && slice.source != null;
        }

        private int ResolveRuntimePageTableEntryCount()
        {
            var entryCount = Mathf.Max(1, runtimePageTableEntryCount);
            var activeAsset = ActiveBakedDataAsset;
            if (activeAsset != null)
            {
                entryCount = Mathf.Max(entryCount, activeAsset.pageTableEntryCount);
            }

            foreach (var cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                var cellEntryCount = GetPageTableEntries(cell).Length;
                if (cellEntryCount > 0 && cell.pageTableDestinationIndex >= 0)
                {
                    var requiredCount = (long)cell.pageTableDestinationIndex + cellEntryCount;
                    if (requiredCount <= int.MaxValue)
                    {
                        entryCount = Mathf.Max(entryCount, (int)requiredCount);
                    }
                }
            }

            foreach (var cell in EnumerateBakedCells())
            {
                var cellEntryCount = cell.pageTableEntries != null ? cell.pageTableEntries.Length : 0;
                if (cellEntryCount > 0 && cell.pageTableDestinationIndex >= 0)
                {
                    var requiredCount = (long)cell.pageTableDestinationIndex + cellEntryCount;
                    if (requiredCount <= int.MaxValue)
                    {
                        entryCount = Mathf.Max(entryCount, (int)requiredCount);
                    }
                }
            }

            return entryCount;
        }

        private int ResolveRuntimeIndirectionEntryCount()
        {
            var entryCount = Mathf.Max(1, runtimeIndirectionEntryCount);
            var activeAsset = ActiveBakedDataAsset;
            if (activeAsset != null)
            {
                entryCount = Mathf.Max(entryCount, activeAsset.indirectionEntryCount);
            }

            foreach (var cell in cells)
            {
                if (cell == null)
                {
                    continue;
                }

                var cellEntryCount = GetIndirectionEntries(cell).Length;
                if (cellEntryCount > 0 && cell.indirectionDestinationIndex >= 0)
                {
                    var requiredCount = (long)cell.indirectionDestinationIndex + cellEntryCount;
                    if (requiredCount <= int.MaxValue)
                    {
                        entryCount = Mathf.Max(entryCount, (int)requiredCount);
                    }
                }
            }

            foreach (var cell in EnumerateBakedCells())
            {
                var cellEntryCount = cell.indirectionEntries != null ? cell.indirectionEntries.Length : 0;
                if (cellEntryCount > 0 && cell.indirectionDestinationIndex >= 0)
                {
                    var requiredCount = (long)cell.indirectionDestinationIndex + cellEntryCount;
                    if (requiredCount <= int.MaxValue)
                    {
                        entryCount = Mathf.Max(entryCount, (int)requiredCount);
                    }
                }
            }

            return entryCount;
        }

        public bool TryLoadCell(int cellIndex)
        {
            var cell = FindCell(cellIndex);
            if (cell == null)
            {
                var bakedCell = FindBakedCell(cellIndex);
                if (bakedCell == null)
                {
                    lastStreamingStatus = "LoadFailed(CellNotFound Index=" + cellIndex + ")";
                    return false;
                }

                return TryLoadBakedCell(bakedCell);
            }

            if (cell == null || loadedCells.Contains(cellIndex) || cell.chunks == null || cell.chunks.Count == 0)
            {
                lastStreamingStatus = loadedCells.Contains(cellIndex)
                    ? "LoadSkipped(AlreadyLoaded Index=" + cellIndex + ")"
                    : "LoadFailed(InvalidManualCell Index=" + cellIndex + ")";
                return false;
            }

            var pageTable = GetPageTableEntries(cell);
            var indirection = GetIndirectionEntries(cell);
            if (!CanLoadCell(cell, pageTable.Length, indirection.Length))
            {
                lastStreamingStatus = "LoadFailed(CannotLoadManualCell Index=" + cellIndex + ")";
                return false;
            }

            var uploadedChunks = new List<int>(cell.chunks.Count);
            var sharedUploadSources = BuildSharedChunkUploadSources(cell);
            foreach (var chunk in cell.chunks)
            {
                var sharedChunkIndex = ResolveSharedPhysicalChunkIndex(chunk);
                var updateSharedData = sharedUploadSources.TryGetValue(sharedChunkIndex, out var sharedSource) &&
                    ReferenceEquals(sharedSource, chunk);
                if (chunk == null || !physicalPool.TryUploadChunk(
                    chunk.physicalChunkIndex,
                    ResolveBytes(chunk.l0L1Rx, chunk.l0L1RxSlice),
                    ResolveBytes(chunk.l1GL1Ry, chunk.l1GL1RySlice),
                    ResolveBytes(chunk.l1BL1Rz, chunk.l1BL1RzSlice),
                    ResolveBytes(chunk.l20, chunk.l20Slice),
                    ResolveBytes(chunk.l21, chunk.l21Slice),
                    ResolveBytes(chunk.l22, chunk.l22Slice),
                    ResolveBytes(chunk.l23, chunk.l23Slice),
                    ResolveBytes(chunk.skyVisibilityL0L1, chunk.skyVisibilityL0L1Slice),
                    ResolveBytes(chunk.skyShadingDirectionIndices, chunk.skyShadingDirectionIndicesSlice),
                    ResolveBytes(chunk.validity, chunk.validitySlice),
                    sharedChunkIndex,
                    updateSharedData))
                {
                    foreach (var uploadedChunk in uploadedChunks)
                    {
                        physicalPool.TryClearChunk(uploadedChunk, ResolveSharedPhysicalChunkIndex(FindUploadedChunk(cell, uploadedChunk)));
                    }
                    lastStreamingStatus = "LoadFailed(UploadManualChunk Cell=" + cellIndex + ")";
                    return false;
                }
                uploadedChunks.Add(chunk.physicalChunkIndex);
            }

            if (!probeVolume.TryUpdateVirtualPageTable(pageTable, 0, cell.pageTableDestinationIndex, pageTable.Length))
            {
                foreach (var uploadedChunk in uploadedChunks)
                {
                    physicalPool.TryClearChunk(uploadedChunk, ResolveSharedPhysicalChunkIndex(FindUploadedChunk(cell, uploadedChunk)));
                }
                lastStreamingStatus = "LoadFailed(ManualPageTable Cell=" + cellIndex + ")";
                return false;
            }

            if (!probeVolume.TryUpdateVirtualIndirection(indirection, 0, cell.indirectionDestinationIndex, indirection.Length))
            {
                Array.Fill(pageTable, uint.MaxValue);
                probeVolume.TryUpdateVirtualPageTable(pageTable, 0, cell.pageTableDestinationIndex, pageTable.Length);
                foreach (var uploadedChunk in uploadedChunks)
                {
                    physicalPool.TryClearChunk(uploadedChunk, ResolveSharedPhysicalChunkIndex(FindUploadedChunk(cell, uploadedChunk)));
                }
                lastStreamingStatus = "LoadFailed(ManualIndirection Cell=" + cellIndex + ")";
                return false;
            }

            loadedCells.Add(cellIndex);
            foreach (var chunk in cell.chunks)
            {
                if (chunk == null)
                {
                    continue;
                }

                loadedChunkOwners[chunk.physicalChunkIndex] = cellIndex;
                loadedSharedChunkOwners[ResolveSharedPhysicalChunkIndex(chunk)] = cellIndex;
            }
            UpdateProbeVolumeLoadedEntryBounds();
            lastStreamingStatus = "LoadedManualCell(Index=" + cellIndex + ",Chunks=" + uploadedChunks.Count + ")";
            return true;
        }

        public bool TryUnloadCell(int cellIndex)
        {
            var cell = FindCell(cellIndex);
            if (cell == null)
            {
                return TryUnloadBakedCell(FindBakedCell(cellIndex));
            }

            if (cell == null || !loadedCells.Contains(cellIndex))
            {
                return false;
            }

            var pageTable = GetPageTableEntries(cell);
            var indirection = GetIndirectionEntries(cell);
            var clearedPageTable = (uint[])pageTable.Clone();
            Array.Fill(clearedPageTable, uint.MaxValue);
            if (!probeVolume.TryUpdateVirtualPageTable(clearedPageTable, 0, cell.pageTableDestinationIndex, clearedPageTable.Length))
            {
                return false;
            }

            var clearedIndirection = (Vector3Int[])indirection.Clone();
            Array.Fill(clearedIndirection, new Vector3Int(-1, 0, 0));
            if (!probeVolume.TryUpdateVirtualIndirection(clearedIndirection, 0, cell.indirectionDestinationIndex, clearedIndirection.Length))
            {
                probeVolume.TryUpdateVirtualPageTable(pageTable, 0, cell.pageTableDestinationIndex, pageTable.Length);
                return false;
            }

            loadedCells.Remove(cellIndex);
            var succeeded = true;
            foreach (var chunk in cell.chunks)
            {
                succeeded &= chunk != null && physicalPool.TryClearChunk(chunk.physicalChunkIndex, ResolveSharedPhysicalChunkIndex(chunk));
                if (chunk != null)
                {
                    loadedChunkOwners.Remove(chunk.physicalChunkIndex);
                    loadedSharedChunkOwners.Remove(ResolveSharedPhysicalChunkIndex(chunk));
                }
            }
            UpdateProbeVolumeLoadedEntryBounds();
            return succeeded;
        }

        private void UpdateProbeVolumeLoadedEntryBounds()
        {
            if (probeVolume == null)
            {
                return;
            }

            var dimensions = probeVolume.virtualIndirectionDimensions;
            if (dimensions.x <= 0 || dimensions.y <= 0 || dimensions.z <= 0 || loadedCells.Count == 0)
            {
                probeVolume.virtualMinLoadedEntry = probeVolume.virtualMinEntryIndex;
                probeVolume.virtualMaxLoadedEntry = probeVolume.virtualMinEntryIndex - Vector3Int.one;
                return;
            }

            var minEntry = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var maxEntry = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var loadedCell in loadedCells)
            {
                var cell = FindCell(loadedCell);
                var bakedCell = cell == null ? FindBakedCell(loadedCell) : null;
                if (cell == null && bakedCell == null)
                {
                    continue;
                }

                if (bakedCell != null && IsValidBakedIndirectionBlock(bakedCell, dimensions))
                {
                    for (var z = 0; z < bakedCell.entryBlockDimensions.z; z++)
                    {
                        for (var y = 0; y < bakedCell.entryBlockDimensions.y; y++)
                        {
                            for (var x = 0; x < bakedCell.entryBlockDimensions.x; x++)
                            {
                                var entry = bakedCell.entryBlockMin + new Vector3Int(x, y, z) + probeVolume.virtualMinEntryIndex;
                                minEntry = Vector3Int.Min(minEntry, entry);
                                maxEntry = Vector3Int.Max(maxEntry, entry);
                            }
                        }
                    }
                }
                else
                {
                    var entryCount = cell != null ? GetIndirectionEntries(cell).Length : bakedCell.indirectionEntries.Length;
                    var firstFlatIndex = cell != null ? cell.indirectionDestinationIndex : bakedCell.indirectionDestinationIndex;
                    if (entryCount <= 0 || firstFlatIndex < 0)
                    {
                        continue;
                    }

                    var capacity = dimensions.x * dimensions.y * dimensions.z;
                    var endFlatIndex = Mathf.Min(firstFlatIndex + entryCount, capacity);
                    for (var flatIndex = firstFlatIndex; flatIndex < endFlatIndex; ++flatIndex)
                    {
                        var entry = DecodeIndirectionFlatIndex(flatIndex, dimensions) + probeVolume.virtualMinEntryIndex;
                        minEntry = Vector3Int.Min(minEntry, entry);
                        maxEntry = Vector3Int.Max(maxEntry, entry);
                    }
                }
            }

            if (minEntry.x == int.MaxValue)
            {
                probeVolume.virtualMinLoadedEntry = probeVolume.virtualMinEntryIndex;
                probeVolume.virtualMaxLoadedEntry = probeVolume.virtualMinEntryIndex - Vector3Int.one;
                return;
            }

            probeVolume.virtualMinLoadedEntry = minEntry;
            probeVolume.virtualMaxLoadedEntry = maxEntry;
        }

        private static Vector3Int DecodeIndirectionFlatIndex(int flatIndex, Vector3Int dimensions)
        {
            var z = flatIndex / (dimensions.x * dimensions.y);
            flatIndex -= z * dimensions.x * dimensions.y;
            var y = flatIndex / dimensions.x;
            var x = flatIndex - y * dimensions.x;
            return new Vector3Int(x, y, z);
        }

        private bool CanLoadCell(Cell cell, int pageTableEntryCount, int indirectionEntryCount)
        {
            if (probeVolume == null || physicalPool == null || pageTableEntryCount <= 0 || indirectionEntryCount <= 0 ||
                !IsValidRange(cell.pageTableDestinationIndex, pageTableEntryCount, probeVolume.VirtualPageTableEntryCount) ||
                !IsValidRange(cell.indirectionDestinationIndex, indirectionEntryCount, probeVolume.VirtualIndirectionEntryCount) ||
                HasLoadedRangeOverlap(cell, pageTableEntryCount, indirectionEntryCount))
            {
                return false;
            }

            var cellChunkIndices = new HashSet<int>();
            foreach (var chunk in cell.chunks)
            {
                var sharedChunkIndex = ResolveSharedPhysicalChunkIndex(chunk);
                if (chunk == null ||
                    !physicalPool.CanAddressChunk(chunk.physicalChunkIndex) ||
                    !physicalPool.CanAddressChunk(sharedChunkIndex) ||
                    !cellChunkIndices.Add(chunk.physicalChunkIndex) ||
                    loadedSharedChunkOwners.ContainsKey(chunk.physicalChunkIndex) ||
                    loadedChunkOwners.ContainsKey(chunk.physicalChunkIndex))
                {
                    return false;
                }

                if (loadedChunkOwners.ContainsKey(sharedChunkIndex) ||
                    loadedSharedChunkOwners.ContainsKey(sharedChunkIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private bool CanLoadBakedCell(BurtXGIProbeBakedCellData cell)
        {
            if (probeVolume == null || physicalPool == null || cell == null ||
                cell.pageTableEntries == null || cell.pageTableEntries.Length == 0 ||
                cell.indirectionEntries == null || cell.indirectionEntries.Length == 0 ||
                cell.chunks == null || cell.chunks.Length == 0 ||
                !IsValidRange(cell.pageTableDestinationIndex, cell.pageTableEntries.Length, probeVolume.VirtualPageTableEntryCount) ||
                !IsValidBakedIndirectionBlock(cell, probeVolume.virtualIndirectionDimensions) ||
                HasLoadedRangeOverlap(cell, cell.pageTableEntries.Length, cell.indirectionEntries.Length))
            {
                return false;
            }

            return true;
        }

        private bool HasLoadedRangeOverlap(Cell candidate, int candidatePageTableEntryCount, int candidateIndirectionEntryCount)
        {
            foreach (var loadedCellIndex in loadedCells)
            {
                var loadedCell = FindCell(loadedCellIndex);
                var loadedBakedCell = loadedCell == null ? FindBakedCell(loadedCellIndex) : null;
                if (loadedCell != null)
                {
                    if (RangesOverlap(
                            candidate.pageTableDestinationIndex, candidatePageTableEntryCount,
                            loadedCell.pageTableDestinationIndex, GetPageTableEntries(loadedCell).Length) ||
                        RangesOverlap(
                            candidate.indirectionDestinationIndex, candidateIndirectionEntryCount,
                            loadedCell.indirectionDestinationIndex, GetIndirectionEntries(loadedCell).Length))
                    {
                        return true;
                    }
                }
                else if (loadedBakedCell != null)
                {
                    if (RangesOverlap(
                            candidate.pageTableDestinationIndex, candidatePageTableEntryCount,
                            loadedBakedCell.pageTableDestinationIndex, loadedBakedCell.pageTableEntries.Length) ||
                        BakedEntryBlockOverlapsRange(
                            loadedBakedCell,
                            candidate.indirectionDestinationIndex,
                            candidateIndirectionEntryCount,
                            probeVolume.virtualIndirectionDimensions))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool HasLoadedRangeOverlap(BurtXGIProbeBakedCellData candidate, int candidatePageTableEntryCount, int candidateIndirectionEntryCount)
        {
            foreach (var loadedCellIndex in loadedCells)
            {
                var loadedCell = FindCell(loadedCellIndex);
                var loadedBakedCell = loadedCell == null ? FindBakedCell(loadedCellIndex) : null;
                if (loadedCell != null)
                {
                    if (RangesOverlap(
                            candidate.pageTableDestinationIndex, candidatePageTableEntryCount,
                            loadedCell.pageTableDestinationIndex, GetPageTableEntries(loadedCell).Length) ||
                        RangesOverlap(
                            candidate.indirectionDestinationIndex, candidateIndirectionEntryCount,
                            loadedCell.indirectionDestinationIndex, GetIndirectionEntries(loadedCell).Length))
                    {
                        return true;
                    }
                }
                else if (loadedBakedCell != null)
                {
                    if (RangesOverlap(
                            candidate.pageTableDestinationIndex, candidatePageTableEntryCount,
                            loadedBakedCell.pageTableDestinationIndex, loadedBakedCell.pageTableEntries.Length) ||
                        BakedEntryBlocksOverlap(candidate, loadedBakedCell))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryLoadBakedCell(BurtXGIProbeBakedCellData cell)
        {
            if (cell == null || loadedCells.Contains(cell.cellIndex) || cell.chunks == null || cell.chunks.Length == 0 ||
                !CanLoadBakedCell(cell))
            {
                lastStreamingStatus = cell == null
                    ? "LoadFailed(BakedCellMissing)"
                    : loadedCells.Contains(cell.cellIndex)
                        ? "LoadSkipped(BakedAlreadyLoaded Index=" + cell.cellIndex + ")"
                        : "LoadFailed(CannotLoadBakedCell Index=" + cell.cellIndex + ")";
                return false;
            }

            if (!TryBuildRuntimeChunkMappings(cell, out var chunkMappings) ||
                !TryRebuildBakedCellPageTableForRuntimeChunks(cell, chunkMappings, out var runtimePageTable))
            {
                lastStreamingStatus = "LoadFailed(BakedRuntimeRemap Index=" + cell.cellIndex + ",BakedChunks=" + cell.chunks.Length + ",PoolFree=" + GetFreePhysicalChunkCount() + ")";
                return false;
            }

            var sharedUploadSources = BuildSharedChunkUploadSources(cell, chunkMappings);
            var uploadedChunks = new List<RuntimeChunkMapping>(chunkMappings.Count);
            foreach (var chunk in cell.chunks)
            {
                if (chunk == null || !TryGetRuntimeChunkMapping(chunkMappings, chunk.physicalChunkIndex, out var runtimeMapping))
                {
                    lastStreamingStatus = "LoadFailed(BakedChunkMappingMissing Index=" + cell.cellIndex + ")";
                    foreach (var uploadedChunk in uploadedChunks)
                    {
                        physicalPool.TryClearChunk(uploadedChunk.RuntimeChunkIndex, uploadedChunk.RuntimeSharedChunkIndex);
                    }
                    return false;
                }

                var updateSharedData = sharedUploadSources.TryGetValue(runtimeMapping.RuntimeSharedChunkIndex, out var sharedSource) &&
                    ReferenceEquals(sharedSource, chunk);
                if (chunk == null || !physicalPool.TryUploadChunk(
                    runtimeMapping.RuntimeChunkIndex,
                    EmptyToNull(chunk.l0L1Rx),
                    EmptyToNull(chunk.l1GL1Ry),
                    EmptyToNull(chunk.l1BL1Rz),
                    EmptyToNull(chunk.l20),
                    EmptyToNull(chunk.l21),
                    EmptyToNull(chunk.l22),
                    EmptyToNull(chunk.l23),
                    EmptyToNull(chunk.skyVisibilityL0L1),
                    EmptyToNull(chunk.skyShadingDirectionIndices),
                    EmptyToNull(chunk.validity),
                    runtimeMapping.RuntimeSharedChunkIndex,
                    updateSharedData))
                {
                    foreach (var uploadedChunk in uploadedChunks)
                    {
                        physicalPool.TryClearChunk(uploadedChunk.RuntimeChunkIndex, uploadedChunk.RuntimeSharedChunkIndex);
                    }

                    lastStreamingStatus = "LoadFailed(UploadBakedChunk Cell=" + cell.cellIndex + ",RuntimeChunk=" + runtimeMapping.RuntimeChunkIndex + ",Reason=" + (physicalPool != null ? physicalPool.LastUploadStatus : "MissingPool") + ")";
                    return false;
                }

                uploadedChunks.Add(runtimeMapping);
            }

            if (!probeVolume.TryUpdateVirtualPageTable(runtimePageTable, 0, cell.pageTableDestinationIndex, runtimePageTable.Length))
            {
                foreach (var uploadedChunk in uploadedChunks)
                {
                    physicalPool.TryClearChunk(uploadedChunk.RuntimeChunkIndex, uploadedChunk.RuntimeSharedChunkIndex);
                }

                lastStreamingStatus = "LoadFailed(BakedPageTable Cell=" + cell.cellIndex + ")";
                return false;
            }

            if (!TryUpdateBakedCellIndirection(cell, cell.indirectionEntries))
            {
                var clearedPageTable = (uint[])cell.pageTableEntries.Clone();
                Array.Fill(clearedPageTable, uint.MaxValue);
                probeVolume.TryUpdateVirtualPageTable(clearedPageTable, 0, cell.pageTableDestinationIndex, clearedPageTable.Length);
                foreach (var uploadedChunk in uploadedChunks)
                {
                    physicalPool.TryClearChunk(uploadedChunk.RuntimeChunkIndex, uploadedChunk.RuntimeSharedChunkIndex);
                }

                lastStreamingStatus = "LoadFailed(BakedIndirection Cell=" + cell.cellIndex + ")";
                return false;
            }

            loadedCells.Add(cell.cellIndex);
            loadedBakedChunkMappings[cell.cellIndex] = chunkMappings;
            foreach (var mapping in chunkMappings)
            {
                loadedChunkOwners[mapping.RuntimeChunkIndex] = cell.cellIndex;
                loadedSharedChunkOwners[mapping.RuntimeSharedChunkIndex] = cell.cellIndex;
            }

            UpdateProbeVolumeLoadedEntryBounds();
            lastStreamingStatus = "LoadedBakedCell(Index=" + cell.cellIndex + ",Chunks=" + uploadedChunks.Count + ",RuntimeRemap=" + chunkMappings.Count + ")";
            return true;
        }

        private bool TryUnloadBakedCell(BurtXGIProbeBakedCellData cell)
        {
            if (cell == null || !loadedCells.Contains(cell.cellIndex))
            {
                return false;
            }

            var clearedPageTable = (uint[])cell.pageTableEntries.Clone();
            Array.Fill(clearedPageTable, uint.MaxValue);
            if (!probeVolume.TryUpdateVirtualPageTable(clearedPageTable, 0, cell.pageTableDestinationIndex, clearedPageTable.Length))
            {
                return false;
            }

            var clearedIndirection = (Vector3Int[])cell.indirectionEntries.Clone();
            Array.Fill(clearedIndirection, new Vector3Int(-1, 0, 0));
            if (!TryUpdateBakedCellIndirection(cell, clearedIndirection))
            {
                if (loadedBakedChunkMappings.TryGetValue(cell.cellIndex, out var existingMappings) &&
                    TryRebuildBakedCellPageTableForRuntimeChunks(cell, existingMappings, out var runtimePageTable))
                {
                    probeVolume.TryUpdateVirtualPageTable(runtimePageTable, 0, cell.pageTableDestinationIndex, runtimePageTable.Length);
                }
                return false;
            }

            loadedCells.Remove(cell.cellIndex);
            var succeeded = true;
            if (!loadedBakedChunkMappings.TryGetValue(cell.cellIndex, out var chunkMappings))
            {
                chunkMappings = new List<RuntimeChunkMapping>();
            }

            foreach (var mapping in chunkMappings)
            {
                succeeded &= physicalPool.TryClearChunk(mapping.RuntimeChunkIndex, mapping.RuntimeSharedChunkIndex);
                loadedChunkOwners.Remove(mapping.RuntimeChunkIndex);
                loadedSharedChunkOwners.Remove(mapping.RuntimeSharedChunkIndex);
            }

            loadedBakedChunkMappings.Remove(cell.cellIndex);

            UpdateProbeVolumeLoadedEntryBounds();
            lastStreamingStatus = succeeded
                ? "UnloadedBakedCell(Index=" + cell.cellIndex + ")"
                : "UnloadBakedCellPartial(Index=" + cell.cellIndex + ")";
            return succeeded;
        }

        private bool TryBuildRuntimeChunkMappings(BurtXGIProbeBakedCellData cell, out List<RuntimeChunkMapping> mappings)
        {
            mappings = new List<RuntimeChunkMapping>();
            if (cell?.chunks == null || physicalPool == null)
            {
                return false;
            }

            var reservedRuntimeChunks = new HashSet<int>();
            var bakedToRuntime = new Dictionary<int, int>();
            for (var index = 0; index < cell.chunks.Length; index++)
            {
                var chunk = cell.chunks[index];
                if (chunk == null || HasRuntimeChunkMapping(mappings, chunk.physicalChunkIndex))
                {
                    continue;
                }

                if (!TryAssignRuntimeChunk(chunk.physicalChunkIndex, bakedToRuntime, reservedRuntimeChunks, out var runtimeChunkIndex))
                {
                    mappings.Clear();
                    return false;
                }

                var bakedSharedChunkIndex = ResolveSharedPhysicalChunkIndex(chunk);
                if (!TryAssignRuntimeChunk(bakedSharedChunkIndex, bakedToRuntime, reservedRuntimeChunks, out var runtimeSharedChunkIndex))
                {
                    mappings.Clear();
                    return false;
                }

                mappings.Add(new RuntimeChunkMapping(chunk.physicalChunkIndex, runtimeChunkIndex, bakedSharedChunkIndex, runtimeSharedChunkIndex));
            }

            return mappings.Count > 0;
        }

        private static bool HasRuntimeChunkMapping(List<RuntimeChunkMapping> mappings, int bakedChunkIndex)
        {
            if (mappings == null)
            {
                return false;
            }

            for (var index = 0; index < mappings.Count; index++)
            {
                if (mappings[index].BakedChunkIndex == bakedChunkIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static Dictionary<int, Chunk> BuildSharedChunkUploadSources(Cell cell)
        {
            var sources = new Dictionary<int, Chunk>();
            if (cell?.chunks == null)
            {
                return sources;
            }

            for (var index = 0; index < cell.chunks.Count; index++)
            {
                var chunk = cell.chunks[index];
                if (chunk == null)
                {
                    continue;
                }

                var sharedChunkIndex = ResolveSharedPhysicalChunkIndex(chunk);
                if (!sources.TryGetValue(sharedChunkIndex, out var existing) ||
                    !HasSharedChunkData(existing) && HasSharedChunkData(chunk))
                {
                    sources[sharedChunkIndex] = chunk;
                }
            }

            return sources;
        }

        private static Dictionary<int, BurtXGIProbeBakedChunk> BuildSharedChunkUploadSources(
            BurtXGIProbeBakedCellData cell,
            List<RuntimeChunkMapping> mappings)
        {
            var sources = new Dictionary<int, BurtXGIProbeBakedChunk>();
            if (cell?.chunks == null || mappings == null)
            {
                return sources;
            }

            for (var index = 0; index < cell.chunks.Length; index++)
            {
                var chunk = cell.chunks[index];
                if (chunk == null || !TryGetRuntimeChunkMapping(mappings, chunk.physicalChunkIndex, out var mapping))
                {
                    continue;
                }

                var runtimeSharedChunkIndex = mapping.RuntimeSharedChunkIndex;
                if (!sources.TryGetValue(runtimeSharedChunkIndex, out var existing) ||
                    !HasSharedChunkData(existing) && HasSharedChunkData(chunk))
                {
                    sources[runtimeSharedChunkIndex] = chunk;
                }
            }

            return sources;
        }

        private bool TryAssignRuntimeChunk(int bakedChunkIndex, Dictionary<int, int> bakedToRuntime, HashSet<int> reservedRuntimeChunks, out int runtimeChunkIndex)
        {
            if (bakedToRuntime != null && bakedToRuntime.TryGetValue(bakedChunkIndex, out runtimeChunkIndex))
            {
                return true;
            }

            runtimeChunkIndex = -1;
            if (bakedChunkIndex < 0 || physicalPool == null)
            {
                return false;
            }

            if (physicalPool.CanAddressChunk(bakedChunkIndex) &&
                !loadedChunkOwners.ContainsKey(bakedChunkIndex) &&
                !loadedSharedChunkOwners.ContainsKey(bakedChunkIndex) &&
                (reservedRuntimeChunks == null || reservedRuntimeChunks.Add(bakedChunkIndex)))
            {
                runtimeChunkIndex = bakedChunkIndex;
            }
            else if (TryFindFreeRuntimeChunk(reservedRuntimeChunks, out var freeChunkIndex))
            {
                runtimeChunkIndex = freeChunkIndex;
                reservedRuntimeChunks?.Add(runtimeChunkIndex);
            }

            if (runtimeChunkIndex < 0)
            {
                return false;
            }

            bakedToRuntime?.Add(bakedChunkIndex, runtimeChunkIndex);
            return true;
        }

        private bool TryFindFreeRuntimeChunk(HashSet<int> reservedRuntimeChunks, out int runtimeChunkIndex)
        {
            var capacity = physicalPool != null ? physicalPool.ChunkCapacity : 0;
            for (var index = 0; index < capacity; index++)
            {
                if (!loadedChunkOwners.ContainsKey(index) &&
                    !loadedSharedChunkOwners.ContainsKey(index) &&
                    (reservedRuntimeChunks == null || !reservedRuntimeChunks.Contains(index)))
                {
                    runtimeChunkIndex = index;
                    return true;
                }
            }

            runtimeChunkIndex = -1;
            return false;
        }

        private int GetFreePhysicalChunkCount()
        {
            return physicalPool != null ? Mathf.Max(0, physicalPool.ChunkCapacity - CountOccupiedRuntimeChunks()) : 0;
        }

        private int CountOccupiedRuntimeChunks()
        {
            if (loadedSharedChunkOwners.Count == 0)
            {
                return loadedChunkOwners.Count;
            }

            var occupied = new HashSet<int>(loadedChunkOwners.Keys);
            foreach (var sharedChunk in loadedSharedChunkOwners.Keys)
            {
                occupied.Add(sharedChunk);
            }

            return occupied.Count;
        }

        private bool TryRebuildBakedCellPageTableForRuntimeChunks(
            BurtXGIProbeBakedCellData cell,
            List<RuntimeChunkMapping> chunkMappings,
            out uint[] runtimePageTable)
        {
            runtimePageTable = null;
            var activeAsset = ActiveBakedDataAsset;
            if (cell?.pageTableEntries == null || activeAsset == null || chunkMappings == null || chunkMappings.Count == 0)
            {
                return false;
            }

            runtimePageTable = new uint[cell.pageTableEntries.Length];
            var bakedToRuntime = new Dictionary<int, int>(chunkMappings.Count);
            for (var index = 0; index < chunkMappings.Count; index++)
            {
                var mapping = chunkMappings[index];
                bakedToRuntime[mapping.BakedChunkIndex] = mapping.RuntimeChunkIndex;
            }

            var bakedPoolChunkDimensions = Vector3Int.Max(Vector3Int.one, activeAsset.physicalPoolChunkDimensions);
            var runtimePoolChunkDimensions = Vector3Int.Max(Vector3Int.one, physicalPool.chunkDimensions);
            for (var index = 0; index < cell.pageTableEntries.Length; index++)
            {
                var entry = cell.pageTableEntries[index];
                if (entry == uint.MaxValue)
                {
                    runtimePageTable[index] = uint.MaxValue;
                    continue;
                }

                if (!TryDecodePackedPhysicalPageTableLocation(
                        entry,
                        bakedPoolChunkDimensions,
                        out var bakedChunkIndex,
                        out var brickIndexInChunk,
                        out var subdivisionLevel) ||
                    !bakedToRuntime.TryGetValue(bakedChunkIndex, out var runtimeChunkIndex))
                {
                    runtimePageTable = null;
                    return false;
                }

                runtimePageTable[index] = PackPhysicalPageTableLocation(runtimePoolChunkDimensions, runtimeChunkIndex, brickIndexInChunk, subdivisionLevel);
            }

            return true;
        }

        private static bool TryGetRuntimeChunkIndex(List<RuntimeChunkMapping> mappings, int bakedChunkIndex, out int runtimeChunkIndex)
        {
            if (mappings != null)
            {
                for (var index = 0; index < mappings.Count; index++)
                {
                    if (mappings[index].BakedChunkIndex == bakedChunkIndex)
                    {
                        runtimeChunkIndex = mappings[index].RuntimeChunkIndex;
                        return true;
                    }
                }
            }

            runtimeChunkIndex = -1;
            return false;
        }

        private static bool TryGetRuntimeChunkMapping(List<RuntimeChunkMapping> mappings, int bakedChunkIndex, out RuntimeChunkMapping mapping)
        {
            if (mappings != null)
            {
                for (var index = 0; index < mappings.Count; index++)
                {
                    if (mappings[index].BakedChunkIndex == bakedChunkIndex)
                    {
                        mapping = mappings[index];
                        return true;
                    }
                }
            }

            mapping = default;
            return false;
        }

        private static bool TryDecodePackedPhysicalPageTableLocation(
            uint packedLocation,
            Vector3Int physicalPoolChunkDimensions,
            out int physicalChunkIndex,
            out int brickIndexInChunk,
            out int subdivisionLevel)
        {
            subdivisionLevel = (int)((packedLocation >> 28) & 0xfu);
            var physicalLocation = (int)(packedLocation & 0x0fffffffu);
            var safeDimensions = Vector3Int.Max(Vector3Int.one, physicalPoolChunkDimensions);
            var poolWidth = safeDimensions.x * BurtGIVirtualProbePhysicalPool.ChunkWidth;
            var poolHeight = safeDimensions.y * BurtGIVirtualProbePhysicalPool.ChunkHeight;
            var poolLayerSize = poolWidth * poolHeight;
            if (physicalLocation < 0 || poolLayerSize <= 0)
            {
                physicalChunkIndex = -1;
                brickIndexInChunk = -1;
                return false;
            }

            var z = physicalLocation / poolLayerSize;
            var remainder = physicalLocation - z * poolLayerSize;
            var y = remainder / poolWidth;
            var x = remainder - y * poolWidth;
            var chunkX = x / BurtGIVirtualProbePhysicalPool.ChunkWidth;
            var chunkY = y / BurtGIVirtualProbePhysicalPool.ChunkHeight;
            var chunkZ = z / BurtGIVirtualProbePhysicalPool.ChunkDepth;
            var brickOffsetX = x - chunkX * BurtGIVirtualProbePhysicalPool.ChunkWidth;
            brickIndexInChunk = brickOffsetX / BurtGIVirtualProbePhysicalPool.BrickProbeCountPerDimension;
            physicalChunkIndex = chunkX + chunkY * safeDimensions.x + chunkZ * safeDimensions.x * safeDimensions.y;
            return brickIndexInChunk >= 0 &&
                brickIndexInChunk < BurtGIVirtualProbePhysicalPool.BricksPerChunk &&
                chunkX >= 0 && chunkX < safeDimensions.x &&
                chunkY >= 0 && chunkY < safeDimensions.y &&
                chunkZ >= 0 && chunkZ < safeDimensions.z;
        }

        private static uint PackPhysicalPageTableLocation(Vector3Int physicalPoolChunkDimensions, int physicalChunkIndex, int brickIndexInChunk, int subdivisionLevel)
        {
            var physicalLocation = BurtGIVirtualProbePhysicalPool.GetChunkBrickPhysicalLocation(
                physicalChunkIndex,
                brickIndexInChunk,
                physicalPoolChunkDimensions);
            return (physicalLocation & 0x0fffffffu) | (((uint)subdivisionLevel & 0xfu) << 28);
        }

        private void UnloadAllCells()
        {
            var cellsToUnload = new List<int>(loadedCells);
            foreach (var cellIndex in cellsToUnload)
            {
                TryUnloadCell(cellIndex);
            }
        }

        [ContextMenu("Invalidate Cached XGI Cell Data")]
        public void InvalidateCachedCellData()
        {
            InvalidateStreamingState();
        }

        private void InvalidateStreamingState()
        {
            if (loadedCells.Count > 0)
            {
                UnloadAllCells();
            }

            loadedChunkOwners.Clear();
            loadedSharedChunkOwners.Clear();
            loadedBakedChunkMappings.Clear();
            resolvedSlices.Clear();
            initialized = false;
            initializedBakedDataAsset = null;
            initializedStreamingSceneGuid = string.Empty;
            probeVolume?.ClearVirtualProbeRuntimeBuffers();
            lastStreamingStatus = "Invalidated";
        }

        private bool TryUpdateBakedCellIndirection(BurtXGIProbeBakedCellData cell, Vector3Int[] entries)
        {
            if (probeVolume == null || cell == null || entries == null ||
                !IsValidBakedIndirectionBlock(cell, probeVolume.virtualIndirectionDimensions))
            {
                return false;
            }

            var blockDimensions = cell.entryBlockDimensions;
            var indirectionDimensions = probeVolume.virtualIndirectionDimensions;
            for (var z = 0; z < blockDimensions.z; z++)
            {
                for (var y = 0; y < blockDimensions.y; y++)
                {
                    var localRowStart = GetFlatEntryIndex(new Vector3Int(0, y, z), blockDimensions);
                    var globalRowStart = GetFlatEntryIndex(cell.entryBlockMin + new Vector3Int(0, y, z), indirectionDimensions);
                    if (!probeVolume.TryUpdateVirtualIndirection(entries, localRowStart, globalRowStart, blockDimensions.x))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsValidBakedIndirectionBlock(BurtXGIProbeBakedCellData cell, Vector3Int indirectionDimensions)
        {
            if (cell == null || cell.indirectionEntries == null ||
                indirectionDimensions.x <= 0 || indirectionDimensions.y <= 0 || indirectionDimensions.z <= 0 ||
                cell.entryBlockDimensions.x <= 0 || cell.entryBlockDimensions.y <= 0 || cell.entryBlockDimensions.z <= 0 ||
                cell.entryBlockMin.x < 0 || cell.entryBlockMin.y < 0 || cell.entryBlockMin.z < 0)
            {
                return false;
            }

            var blockEntryCount = cell.entryBlockDimensions.x * cell.entryBlockDimensions.y * cell.entryBlockDimensions.z;
            return blockEntryCount == cell.indirectionEntries.Length &&
                cell.entryBlockMin.x + cell.entryBlockDimensions.x <= indirectionDimensions.x &&
                cell.entryBlockMin.y + cell.entryBlockDimensions.y <= indirectionDimensions.y &&
                cell.entryBlockMin.z + cell.entryBlockDimensions.z <= indirectionDimensions.z;
        }

        private static bool IsValidRange(int start, int count, int capacity)
        {
            return start >= 0 && count >= 0 && start <= capacity && count <= capacity - start;
        }

        private static bool RangesOverlap(int firstStart, int firstCount, int secondStart, int secondCount)
        {
            return firstCount > 0 && secondCount > 0 && firstStart < secondStart + secondCount && secondStart < firstStart + firstCount;
        }

        private static bool BakedEntryBlocksOverlap(BurtXGIProbeBakedCellData first, BurtXGIProbeBakedCellData second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            return first.entryBlockDimensions.x > 0 && first.entryBlockDimensions.y > 0 && first.entryBlockDimensions.z > 0 &&
                second.entryBlockDimensions.x > 0 && second.entryBlockDimensions.y > 0 && second.entryBlockDimensions.z > 0 &&
                first.entryBlockMin.x < second.entryBlockMin.x + second.entryBlockDimensions.x &&
                second.entryBlockMin.x < first.entryBlockMin.x + first.entryBlockDimensions.x &&
                first.entryBlockMin.y < second.entryBlockMin.y + second.entryBlockDimensions.y &&
                second.entryBlockMin.y < first.entryBlockMin.y + first.entryBlockDimensions.y &&
                first.entryBlockMin.z < second.entryBlockMin.z + second.entryBlockDimensions.z &&
                second.entryBlockMin.z < first.entryBlockMin.z + first.entryBlockDimensions.z;
        }

        private static bool BakedEntryBlockOverlapsRange(BurtXGIProbeBakedCellData cell, int rangeStart, int rangeCount, Vector3Int dimensions)
        {
            if (!IsValidBakedIndirectionBlock(cell, dimensions) || rangeCount <= 0)
            {
                return false;
            }

            for (var z = 0; z < cell.entryBlockDimensions.z; z++)
            {
                for (var y = 0; y < cell.entryBlockDimensions.y; y++)
                {
                    var rowStart = GetFlatEntryIndex(cell.entryBlockMin + new Vector3Int(0, y, z), dimensions);
                    if (RangesOverlap(rangeStart, rangeCount, rowStart, cell.entryBlockDimensions.x))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static int GetFlatEntryIndex(Vector3Int entryIndex, Vector3Int dimensions)
        {
            return entryIndex.x + entryIndex.y * dimensions.x + entryIndex.z * dimensions.x * dimensions.y;
        }

        private static byte[] EmptyToNull(byte[] bytes)
        {
            return bytes != null && bytes.Length > 0 ? bytes : null;
        }

        private static bool HasSharedChunkData(BurtXGIProbeBakedChunk chunk)
        {
            return chunk != null &&
                (HasBytes(chunk.validity) ||
                    HasBytes(chunk.skyVisibilityL0L1) ||
                    HasBytes(chunk.skyShadingDirectionIndices));
        }

        private static bool HasSharedChunkData(Chunk chunk)
        {
            return chunk != null &&
                (HasChunkSource(chunk.validity, chunk.validitySlice) ||
                    HasChunkSource(chunk.skyVisibilityL0L1, chunk.skyVisibilityL0L1Slice) ||
                    HasChunkSource(chunk.skyShadingDirectionIndices, chunk.skyShadingDirectionIndicesSlice));
        }

        private static bool HasBytes(byte[] bytes)
        {
            return bytes != null && bytes.Length > 0;
        }

        private Cell FindCell(int cellIndex)
        {
            return cells.Find(cell => cell != null && cell.index == cellIndex);
        }

        private BurtXGIProbeBakedCellData FindBakedCell(int cellIndex)
        {
            foreach (var cell in EnumerateBakedCells())
            {
                if (cell.cellIndex == cellIndex)
                {
                    return cell;
                }
            }

            return null;
        }

        private IEnumerable<BurtXGIProbeBakedCellData> EnumerateBakedCells()
        {
            var activeAsset = ActiveBakedDataAsset;
            if (activeAsset == null || activeAsset.cells == null)
            {
                yield break;
            }

            var sceneCellIndices = activeAsset.GetRuntimeSceneCellIndices(streamingSceneGuid);
            if (sceneCellIndices != null)
            {
                if (sceneCellIndices.Count == 0)
                {
                    yield break;
                }

                var sceneCellSet = new HashSet<int>(sceneCellIndices);
                for (var i = 0; i < activeAsset.cells.Length; i++)
                {
                    var cell = activeAsset.cells[i];
                    if (cell != null && sceneCellSet.Contains(cell.cellIndex))
                    {
                        yield return cell;
                    }
                }

                yield break;
            }

            for (var i = 0; i < activeAsset.cells.Length; i++)
            {
                var cell = activeAsset.cells[i];
                if (cell != null)
                {
                    yield return cell;
                }
            }
        }

        private int CountRuntimeBakedCells(BurtXGIProbeBakedDataAsset activeAsset)
        {
            if (activeAsset == null || activeAsset.cells == null)
            {
                return 0;
            }

            var sceneCellIndices = activeAsset.GetRuntimeSceneCellIndices(streamingSceneGuid);
            return sceneCellIndices != null ? sceneCellIndices.Count : activeAsset.cells.Length;
        }

        private static int ResolveSharedPhysicalChunkIndex(Chunk chunk)
        {
            if (chunk == null)
            {
                return -1;
            }

            return chunk.sharedPhysicalChunkIndex >= 0 ? chunk.sharedPhysicalChunkIndex : chunk.physicalChunkIndex;
        }

        private static int ResolveSharedPhysicalChunkIndex(BurtXGIProbeBakedChunk chunk)
        {
            if (chunk == null)
            {
                return -1;
            }

            return chunk.sharedPhysicalChunkIndex >= 0 ? chunk.sharedPhysicalChunkIndex : chunk.physicalChunkIndex;
        }

        private static Chunk FindUploadedChunk(Cell cell, int physicalChunkIndex)
        {
            return cell?.chunks?.Find(chunk => chunk != null && chunk.physicalChunkIndex == physicalChunkIndex);
        }

        private static BurtXGIProbeBakedChunk FindUploadedChunk(BurtXGIProbeBakedCellData cell, int physicalChunkIndex)
        {
            if (cell?.chunks == null)
            {
                return null;
            }

            for (var i = 0; i < cell.chunks.Length; i++)
            {
                var chunk = cell.chunks[i];
                if (chunk != null && chunk.physicalChunkIndex == physicalChunkIndex)
                {
                    return chunk;
                }
            }

            return null;
        }

        private void UpdateStreamingForCamera(Camera camera)
        {
            if (!automaticStreaming)
            {
                return;
            }

            var activeAsset = ActiveBakedDataAsset;
            if (initialized && initializedBakedDataAsset != activeAsset)
            {
                InvalidateStreamingState();
            }

            if (initialized && !string.Equals(
                    initializedStreamingSceneGuid,
                    NormalizeStreamingSceneGuid(streamingSceneGuid),
                    StringComparison.OrdinalIgnoreCase))
            {
                InvalidateStreamingState();
            }

            if (!initialized && !InitializeStreaming())
            {
                return;
            }

            var desiredCells = new HashSet<int>();
            var cellsToLoad = new List<StreamingCandidate>();
            if (!BurtGIProbeStreamingPivot.TryGetBest(camera, out var pivot))
            {
                return;
            }

            var streamingPosition = pivot.Position;
            var streamingForward = ResolveStreamingForward(camera, pivot.Forward);
            var streamingScorePosition = ResolveStreamingScorePosition(activeAsset, streamingPosition);
            foreach (var cell in cells)
            {
                if (cell == null || cell.loadDistance <= 0f) continue;
                if ((cell.worldPosition - streamingPosition).sqrMagnitude <= cell.loadDistance * cell.loadDistance)
                {
                    desiredCells.Add(cell.index);
                    if (!loadedCells.Contains(cell.index))
                    {
                        cellsToLoad.Add(new StreamingCandidate(
                            cell.index,
                            cell.worldPosition,
                            ResolveStreamingScorePosition(activeAsset, cell.worldPosition)));
                    }
                }
            }

            foreach (var cell in EnumerateBakedCells())
            {
                var distance = Mathf.Max(0.01f, bakedDataLoadDistance);
                var worldPosition = cell.bounds.center;
                if ((worldPosition - streamingPosition).sqrMagnitude <= distance * distance)
                {
                    desiredCells.Add(cell.cellIndex);
                    if (!loadedCells.Contains(cell.cellIndex))
                    {
                        cellsToLoad.Add(new StreamingCandidate(
                            cell.cellIndex,
                            worldPosition,
                            ResolveBakedStreamingScorePosition(cell)));
                    }
                }
            }

            var unloadCells = new List<int>();
            foreach (var loadedCell in loadedCells)
            {
                if (!desiredCells.Contains(loadedCell)) unloadCells.Add(loadedCell);
            }
            foreach (var unloadCell in unloadCells) TryUnloadCell(unloadCell);

            cellsToLoad.Sort((left, right) => CompareStreamingPriority(left, right, streamingScorePosition, streamingForward));
            var loadCount = Mathf.Min(Mathf.Max(1, maxCellsToLoadPerFrame), cellsToLoad.Count);
            var loadedThisFrame = 0;
            for (var index = 0; index < cellsToLoad.Count && loadedThisFrame < loadCount; ++index)
            {
                if (TryLoadCell(cellsToLoad[index].Index) ||
                    TryLoadByReplacingWorseBlockers(cellsToLoad[index], activeAsset, streamingScorePosition, streamingForward))
                {
                    loadedThisFrame++;
                }
            }
        }

        private static Vector3 ResolveStreamingForward(Camera camera, Vector3 fallbackForward)
        {
            var forward = camera != null ? camera.transform.forward : fallbackForward;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 ResolveStreamingScorePosition(BurtXGIProbeBakedDataAsset activeAsset, Vector3 worldPosition)
        {
            if (activeAsset == null)
            {
                return worldPosition;
            }

            return (worldPosition - activeAsset.probeOffset) / Mathf.Max(0.0001f, activeAsset.ResolvedCellSizeInMeters) - Vector3.one * 0.5f;
        }

        private static Vector3 ResolveBakedStreamingScorePosition(BurtXGIProbeBakedCellData cell)
        {
            return cell != null ? (Vector3)cell.position : Vector3.zero;
        }

        private static string NormalizeStreamingSceneGuid(string value)
        {
            return value ?? string.Empty;
        }

        private bool TryLoadByReplacingWorseBlockers(
            StreamingCandidate candidate,
            BurtXGIProbeBakedDataAsset activeAsset,
            Vector3 streamingScorePosition,
            Vector3 streamingForward)
        {
            var blockers = new HashSet<int>();
            CollectLoadBlockers(candidate.Index, blockers);
            if (blockers.Count == 0)
            {
                return false;
            }

            var candidateScore = GetStreamingScore(candidate.ScorePosition, streamingScorePosition, streamingForward);
            var worseBlockers = new List<StreamingCandidate>();
            foreach (var blocker in blockers)
            {
                if (blocker == candidate.Index || !TryGetStreamingScorePosition(blocker, activeAsset, out var blockerPosition))
                {
                    continue;
                }

                if (GetStreamingScore(blockerPosition, streamingScorePosition, streamingForward) > candidateScore)
                {
                    worseBlockers.Add(new StreamingCandidate(blocker, Vector3.zero, blockerPosition));
                }
            }

            worseBlockers.Sort((left, right) => CompareStreamingPriority(right, left, streamingScorePosition, streamingForward));
            for (var index = 0; index < worseBlockers.Count; ++index)
            {
                if (!TryUnloadCell(worseBlockers[index].Index))
                {
                    continue;
                }

                if (TryLoadCell(candidate.Index))
                {
                    return true;
                }
            }

            return false;
        }

        private void CollectLoadBlockers(int candidateIndex, HashSet<int> blockers)
        {
            if (blockers == null)
            {
                return;
            }

            var cell = FindCell(candidateIndex);
            if (cell != null)
            {
                CollectChunkBlockers(candidateIndex, cell.chunks, blockers);
                CollectRangeBlockersForCellCandidate(candidateIndex, cell, GetPageTableEntries(cell).Length, GetIndirectionEntries(cell).Length, blockers);
                return;
            }

            var bakedCell = FindBakedCell(candidateIndex);
            if (bakedCell != null)
            {
                CollectRuntimeCapacityBlockersForBakedCandidate(candidateIndex, bakedCell, blockers);
                CollectRangeBlockersForBakedCandidate(candidateIndex, bakedCell, blockers);
            }
        }

        private void CollectChunkBlockers(int candidateIndex, List<Chunk> chunks, HashSet<int> blockers)
        {
            if (chunks == null)
            {
                return;
            }

            for (var index = 0; index < chunks.Count; ++index)
            {
                var chunk = chunks[index];
                if (chunk == null)
                {
                    continue;
                }

                AddChunkBlocker(candidateIndex, chunk.physicalChunkIndex, blockers);
                AddSharedChunkBlocker(candidateIndex, ResolveSharedPhysicalChunkIndex(chunk), blockers);
            }
        }

        private void CollectChunkBlockers(int candidateIndex, BurtXGIProbeBakedChunk[] chunks, HashSet<int> blockers)
        {
            if (chunks == null)
            {
                return;
            }

            for (var index = 0; index < chunks.Length; ++index)
            {
                var chunk = chunks[index];
                if (chunk == null)
                {
                    continue;
                }

                AddChunkBlocker(candidateIndex, chunk.physicalChunkIndex, blockers);
                AddSharedChunkBlocker(candidateIndex, ResolveSharedPhysicalChunkIndex(chunk), blockers);
            }
        }

        private void CollectRuntimeCapacityBlockersForBakedCandidate(int candidateIndex, BurtXGIProbeBakedCellData cell, HashSet<int> blockers)
        {
            var requiredChunks = CountUniqueBakedPhysicalChunks(cell);
            var freeChunks = GetFreePhysicalChunkCount();
            if (requiredChunks <= freeChunks)
            {
                return;
            }

            foreach (var loadedCell in loadedCells)
            {
                if (loadedCell != candidateIndex)
                {
                    blockers.Add(loadedCell);
                }
            }
        }

        private static int CountUniqueBakedPhysicalChunks(BurtXGIProbeBakedCellData cell)
        {
            if (cell?.chunks == null)
            {
                return 0;
            }

            var chunks = new HashSet<int>();
            for (var index = 0; index < cell.chunks.Length; index++)
            {
                var chunk = cell.chunks[index];
                if (chunk != null)
                {
                    chunks.Add(chunk.physicalChunkIndex);
                    chunks.Add(ResolveSharedPhysicalChunkIndex(chunk));
                }
            }

            return chunks.Count;
        }

        private void AddChunkBlocker(int candidateIndex, int chunkIndex, HashSet<int> blockers)
        {
            if (loadedChunkOwners.TryGetValue(chunkIndex, out var owner) && owner != candidateIndex)
            {
                blockers.Add(owner);
            }
        }

        private void AddSharedChunkBlocker(int candidateIndex, int sharedChunkIndex, HashSet<int> blockers)
        {
            if (loadedSharedChunkOwners.TryGetValue(sharedChunkIndex, out var owner) && owner != candidateIndex)
            {
                blockers.Add(owner);
            }
        }

        private void CollectRangeBlockersForCellCandidate(int candidateIndex, Cell candidate, int pageTableEntryCount, int indirectionEntryCount, HashSet<int> blockers)
        {
            foreach (var loadedCellIndex in loadedCells)
            {
                if (loadedCellIndex == candidateIndex)
                {
                    continue;
                }

                var loadedCell = FindCell(loadedCellIndex);
                var loadedBakedCell = loadedCell == null ? FindBakedCell(loadedCellIndex) : null;
                if (loadedCell != null)
                {
                    if (RangesOverlap(candidate.pageTableDestinationIndex, pageTableEntryCount, loadedCell.pageTableDestinationIndex, GetPageTableEntries(loadedCell).Length) ||
                        RangesOverlap(candidate.indirectionDestinationIndex, indirectionEntryCount, loadedCell.indirectionDestinationIndex, GetIndirectionEntries(loadedCell).Length))
                    {
                        blockers.Add(loadedCellIndex);
                    }
                }
                else if (loadedBakedCell != null)
                {
                    if (RangesOverlap(candidate.pageTableDestinationIndex, pageTableEntryCount, loadedBakedCell.pageTableDestinationIndex, loadedBakedCell.pageTableEntries.Length) ||
                        BakedEntryBlockOverlapsRange(loadedBakedCell, candidate.indirectionDestinationIndex, indirectionEntryCount, probeVolume.virtualIndirectionDimensions))
                    {
                        blockers.Add(loadedCellIndex);
                    }
                }
            }
        }

        private void CollectRangeBlockersForBakedCandidate(int candidateIndex, BurtXGIProbeBakedCellData candidate, HashSet<int> blockers)
        {
            foreach (var loadedCellIndex in loadedCells)
            {
                if (loadedCellIndex == candidateIndex)
                {
                    continue;
                }

                var loadedCell = FindCell(loadedCellIndex);
                var loadedBakedCell = loadedCell == null ? FindBakedCell(loadedCellIndex) : null;
                if (loadedCell != null)
                {
                    if (RangesOverlap(candidate.pageTableDestinationIndex, candidate.pageTableEntries.Length, loadedCell.pageTableDestinationIndex, GetPageTableEntries(loadedCell).Length) ||
                        BakedEntryBlockOverlapsRange(candidate, loadedCell.indirectionDestinationIndex, GetIndirectionEntries(loadedCell).Length, probeVolume.virtualIndirectionDimensions))
                    {
                        blockers.Add(loadedCellIndex);
                    }
                }
                else if (loadedBakedCell != null)
                {
                    if (RangesOverlap(candidate.pageTableDestinationIndex, candidate.pageTableEntries.Length, loadedBakedCell.pageTableDestinationIndex, loadedBakedCell.pageTableEntries.Length) ||
                        BakedEntryBlocksOverlap(candidate, loadedBakedCell))
                    {
                        blockers.Add(loadedCellIndex);
                    }
                }
            }
        }

        private bool TryGetStreamingScorePosition(int cellIndex, BurtXGIProbeBakedDataAsset activeAsset, out Vector3 scorePosition)
        {
            var cell = FindCell(cellIndex);
            if (cell != null)
            {
                scorePosition = ResolveStreamingScorePosition(activeAsset, cell.worldPosition);
                return true;
            }

            var bakedCell = FindBakedCell(cellIndex);
            if (bakedCell != null)
            {
                scorePosition = ResolveBakedStreamingScorePosition(bakedCell);
                return true;
            }

            scorePosition = default;
            return false;
        }

        private static int CompareStreamingPriority(StreamingCandidate left, StreamingCandidate right, Vector3 cameraPosition, Vector3 cameraForward)
        {
            var leftScore = GetStreamingScore(left.ScorePosition, cameraPosition, cameraForward);
            var rightScore = GetStreamingScore(right.ScorePosition, cameraPosition, cameraForward);
            var scoreComparison = leftScore.CompareTo(rightScore);
            return scoreComparison != 0 ? scoreComparison : left.Index.CompareTo(right.Index);
        }

        private static float GetStreamingScore(Vector3 cellPosition, Vector3 cameraPosition, Vector3 cameraForward)
        {
            var cameraToCell = cellPosition - cameraPosition;
            var distance = cameraToCell.magnitude;
            var forwardWeight = distance > 0.0001f ? Vector3.Dot(cameraForward, cameraToCell / distance) : 1f;
            return distance * (2f - forwardWeight);
        }

        private uint[] GetPageTableEntries(Cell cell)
        {
            return DecodeUInt32(cell != null ? ResolveBytes(cell.pageTableEntries, cell.pageTableEntriesSlice) : null);
        }

        private Vector3Int[] GetIndirectionEntries(Cell cell)
        {
            return DecodeUInt3(cell != null ? ResolveBytes(cell.indirectionEntries, cell.indirectionEntriesSlice) : null);
        }

        private byte[] ResolveBytes(TextAsset legacyAsset, BinarySlice slice)
        {
            var sourceAsset = slice != null && slice.source != null ? slice.source : legacyAsset;
            if (sourceAsset == null)
            {
                return null;
            }

            if (slice == null)
            {
                return sourceAsset.bytes;
            }

            if (resolvedSlices.TryGetValue(slice, out var cachedBytes))
            {
                return cachedBytes;
            }

            var sourceBytes = sourceAsset.bytes;
            if (sourceBytes == null || slice.byteOffset < 0 || slice.byteOffset > sourceBytes.Length)
            {
                return null;
            }

            var byteLength = slice.byteLength == 0 ? sourceBytes.Length - slice.byteOffset : slice.byteLength;
            if (byteLength < 0 || byteLength > sourceBytes.Length - slice.byteOffset)
            {
                return null;
            }

            byte[] resolvedBytes;
            if (slice.byteOffset == 0 && byteLength == sourceBytes.Length)
            {
                resolvedBytes = sourceBytes;
            }
            else
            {
                resolvedBytes = new byte[byteLength];
                Buffer.BlockCopy(sourceBytes, slice.byteOffset, resolvedBytes, 0, byteLength);
            }

            var finalBytes = slice.compression == BinaryCompression.None
                ? resolvedBytes
                : slice.compression == BinaryCompression.Zstd && BurtGIZstdDecoder.TryDecompress(resolvedBytes, slice.decompressedByteLength, out var decompressedBytes)
                    ? decompressedBytes
                    : null;
            if (finalBytes != null)
            {
                resolvedSlices.Add(slice, finalBytes);
            }

            return finalBytes;
        }

        private static uint[] DecodeUInt32(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return Array.Empty<uint>();
            if (bytes.Length % sizeof(uint) != 0) return Array.Empty<uint>();
            var values = new uint[bytes.Length / sizeof(uint)];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        private static Vector3Int[] DecodeUInt3(byte[] bytes)
        {
            var words = DecodeUInt32(bytes);
            if (words.Length == 0 || words.Length % 3 != 0) return Array.Empty<Vector3Int>();
            var values = new Vector3Int[words.Length / 3];
            for (var index = 0; index < values.Length; ++index)
            {
                var offset = index * 3;
                values[index] = new Vector3Int(unchecked((int)words[offset]), unchecked((int)words[offset + 1]), unchecked((int)words[offset + 2]));
            }
            return values;
        }
    }

    internal static class BurtGIZstdDecoder
    {
        private static readonly Type DecompressorType = ResolveDecompressorType();
        private static readonly ConstructorInfo DecompressorConstructor = DecompressorType?.GetConstructor(Type.EmptyTypes);
        private static readonly MethodInfo UnwrapMethod = DecompressorType?.GetMethod(
            "Unwrap",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(byte[]), typeof(int), typeof(int), typeof(byte[]), typeof(int), typeof(int) },
            null);

        internal static bool TryDecompress(byte[] compressedBytes, int expectedByteLength, out byte[] decompressedBytes)
        {
            decompressedBytes = null;
            if (compressedBytes == null || compressedBytes.Length == 0 || expectedByteLength <= 0 ||
                DecompressorConstructor == null || UnwrapMethod == null)
            {
                return false;
            }

            object decompressor = null;
            try
            {
                decompressor = DecompressorConstructor.Invoke(null);
                var destination = new byte[expectedByteLength];
                var decodedLength = (int)UnwrapMethod.Invoke(
                    decompressor,
                    new object[] { compressedBytes, 0, compressedBytes.Length, destination, 0, destination.Length });
                if (decodedLength != expectedByteLength)
                {
                    return false;
                }

                decompressedBytes = destination;
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                (decompressor as IDisposable)?.Dispose();
            }
        }

        private static Type ResolveDecompressorType()
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
                // Player builds normally load the plugin assembly up front; this covers editor/batch edge cases below.
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
    }
}
