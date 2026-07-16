using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Burt.RenderPipeline
{
    [ExecuteAlways]
    [AddComponentMenu("Rendering/BurtRP/XGI Probe Adjust Volume")]
    [MovedFrom(true, "UnityEngine.Rendering", "FunPlus.WorldX.XRender.Runtime", "XGIProbeAdjustVolume")]
    public sealed class BurtXGIProbeAdjustVolume : MonoBehaviour
    {
        public enum VolumeShape
        {
            Box,
            Sphere
        }

        public enum AdjustmentMode
        {
            ApplyVirtualOffset,
            OverrideVirtualOffsetSettings
        }

        public VolumeShape shape = VolumeShape.Box;
        [Min(0f)] public Vector3 size = new Vector3(10f, 10f, 10f);
        [Min(0f)] public float radius = 1f;
        public AdjustmentMode mode = AdjustmentMode.ApplyVirtualOffset;

        public Vector3 virtualOffsetRotation = Vector3.zero;
        [Min(0f)] public float virtualOffsetDistance = 1f;

        [Range(0f, 1f)] public float geometryBias = 0.01f;
        [Range(0f, 0.95f)] public float virtualOffsetThreshold = 0.75f;
        [Range(-0.05f, 0f)] public float rayOriginBias = -0.001f;

        public Vector3 GetVirtualOffset()
        {
            if (mode != AdjustmentMode.ApplyVirtualOffset)
            {
                return Vector3.zero;
            }

            return (transform.rotation * Quaternion.Euler(virtualOffsetRotation) * Vector3.forward) * virtualOffsetDistance;
        }

        public float ComputeVolume()
        {
            if (shape == VolumeShape.Sphere)
            {
                var effectiveRadius = Mathf.Max(0f, radius);
                return (4f / 3f) * Mathf.PI * effectiveRadius * effectiveRadius * effectiveRadius;
            }

            var effectiveSize = Vector3.Max(Vector3.zero, size);
            return effectiveSize.x * effectiveSize.y * effectiveSize.z;
        }

        public void GetOBBAndAABB(out BurtGIProbeVolumeBounds obb, out Bounds bounds)
        {
            if (shape == VolumeShape.Box)
            {
                obb = new BurtGIProbeVolumeBounds(Matrix4x4.TRS(transform.position, transform.rotation, Vector3.Max(Vector3.zero, size)));
                bounds = obb.bounds;
                return;
            }

            obb = default;
            var effectiveRadius = Mathf.Max(0f, radius);
            bounds = new Bounds(transform.position, Vector3.one * (effectiveRadius * 2f));
        }

        public bool IntersectsVolume(Bounds volumeBounds)
        {
            GetOBBAndAABB(out var obb, out var bounds);
            if (shape == VolumeShape.Sphere)
            {
                return volumeBounds.SqrDistance(bounds.center) < Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
            }

            return BurtGIProbeVolumePositioning.OBBAABBIntersect(in obb, volumeBounds, bounds);
        }

        public bool ContainsPoint(Vector3 position)
        {
            if (shape == VolumeShape.Sphere)
            {
                var effectiveRadius = Mathf.Max(0f, radius);
                return (transform.position - position).sqrMagnitude < effectiveRadius * effectiveRadius;
            }

            GetOBBAndAABB(out var obb, out _);
            return BurtGIProbeVolumePositioning.OBBContains(in obb, position);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.35f);
            if (shape == VolumeShape.Sphere)
            {
                Gizmos.DrawWireSphere(transform.position, Mathf.Max(0f, radius));
                return;
            }

            var previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, Vector3.Max(Vector3.zero, size));
            Gizmos.matrix = previousMatrix;
        }
    }

    public enum BurtXGIProbeBakingPlatform
    {
        PC,
        Mobile
    }

    public enum BurtXGIProbeStreamerType
    {
        AsyncRead,
        Synchronous
    }

    public enum BurtXGIProbeTextureMemoryBudget
    {
        Low,
        Medium,
        High,
        Ultra,
        Film
    }

    [Flags]
    public enum BurtXGIProbeSHBands
    {
        None = 0,
        L0 = 1 << 0,
        L1 = 1 << 1,
        L2 = 1 << 2,
        SphericalHarmonicsL1 = L0 | L1,
        SphericalHarmonicsL2 = L0 | L1 | L2
    }

    public static class BurtXGIProbeSHBandsUtility
    {
        public static bool HasL0(this BurtXGIProbeSHBands bands) => GetQualityLevel(bands) >= 1;

        public static bool HasL1(this BurtXGIProbeSHBands bands) => GetQualityLevel(bands) >= 2;

        public static bool HasL2(this BurtXGIProbeSHBands bands) => GetQualityLevel(bands) >= 3;

        public static int GetQualityLevel(this BurtXGIProbeSHBands bands)
        {
            if ((bands & BurtXGIProbeSHBands.L2) != 0)
            {
                return 3;
            }

            if ((bands & BurtXGIProbeSHBands.L1) != 0)
            {
                return 2;
            }

            if ((bands & BurtXGIProbeSHBands.L0) != 0)
            {
                return 1;
            }

            return 0;
        }

        public static BurtXGIProbeSHBands FromQualityLevel(int qualityLevel)
        {
            if (qualityLevel >= 3)
            {
                return BurtXGIProbeSHBands.SphericalHarmonicsL2;
            }

            if (qualityLevel >= 2)
            {
                return BurtXGIProbeSHBands.SphericalHarmonicsL1;
            }

            if (qualityLevel >= 1)
            {
                return BurtXGIProbeSHBands.L0;
            }

            return BurtXGIProbeSHBands.None;
        }

        public static BurtXGIProbeSHBands NormalizedQuality(this BurtXGIProbeSHBands bands)
        {
            return FromQualityLevel(GetQualityLevel(bands));
        }

        public static BurtXGIProbeSHBands MinQuality(BurtXGIProbeSHBands lhs, BurtXGIProbeSHBands rhs)
        {
            return FromQualityLevel(Mathf.Min(lhs.GetQualityLevel(), rhs.GetQualityLevel()));
        }
    }

    [Serializable]
    public struct BurtXGIProbeSystemParameters
    {
        public bool enable;
        public BurtXGIProbeTextureMemoryBudget memoryBudget;
        public BurtXGIProbeSHBands shBands;

        public static BurtXGIProbeSystemParameters Default => new BurtXGIProbeSystemParameters
        {
            enable = true,
            memoryBudget = BurtXGIProbeTextureMemoryBudget.Medium,
            shBands = BurtXGIProbeSHBands.SphericalHarmonicsL1
        };

        public void NormalizeLegacySerializedValues()
        {
            var memoryValue = (int)memoryBudget;
            var legacyXRenderMemoryBudget = true;
            memoryBudget = memoryValue switch
            {
                512 => BurtXGIProbeTextureMemoryBudget.Low,
                724 => BurtXGIProbeTextureMemoryBudget.Medium,
                1024 => BurtXGIProbeTextureMemoryBudget.High,
                1448 => BurtXGIProbeTextureMemoryBudget.Ultra,
                2048 => BurtXGIProbeTextureMemoryBudget.Film,
                _ => NormalizeMemoryBudget(memoryBudget, out legacyXRenderMemoryBudget)
            };

            shBands = NormalizeSHBands(shBands, legacyXRenderMemoryBudget);
        }

        private static BurtXGIProbeTextureMemoryBudget NormalizeMemoryBudget(
            BurtXGIProbeTextureMemoryBudget value,
            out bool legacyXRenderMemoryBudget)
        {
            legacyXRenderMemoryBudget = false;
            return Enum.IsDefined(typeof(BurtXGIProbeTextureMemoryBudget), value)
                ? value
                : BurtXGIProbeTextureMemoryBudget.Medium;
        }

        private static BurtXGIProbeSHBands NormalizeSHBands(BurtXGIProbeSHBands value, bool legacyXRenderMemoryBudget)
        {
            var rawValue = (int)value;
            if (legacyXRenderMemoryBudget)
            {
                return rawValue switch
                {
                    0 => BurtXGIProbeSHBands.None,
                    1 => BurtXGIProbeSHBands.SphericalHarmonicsL1,
                    2 => BurtXGIProbeSHBands.SphericalHarmonicsL2,
                    3 => BurtXGIProbeSHBands.L0,
                    _ => BurtXGIProbeSHBands.SphericalHarmonicsL1
                };
            }

            return value.NormalizedQuality();
        }
    }

    [Serializable]
    public struct BurtXGIProbePlacedCell
    {
        public int index;
        public Vector3Int position;
        public Bounds bounds;
        public int brickStartIndex;
        public int brickCount;
        public int probeStartIndex;
        public int probeCount;
        public string[] sceneGuids;
    }

    [Serializable]
    public struct BurtXGIProbePlacedBrick
    {
        public Vector3Int position;
        public int subdivisionLevel;
        public int cellIndex;
    }

    [Serializable]
    public struct BurtXGIProbeBakedSphericalHarmonicsL2
    {
        public Vector3 c0;
        public Vector3 c1;
        public Vector3 c2;
        public Vector3 c3;
        public Vector3 c4;
        public Vector3 c5;
        public Vector3 c6;
        public Vector3 c7;
        public Vector3 c8;

        public static BurtXGIProbeBakedSphericalHarmonicsL2 Ambient(Vector3 color)
        {
            return new BurtXGIProbeBakedSphericalHarmonicsL2
            {
                c0 = color,
                c1 = Vector3.one * 0.5f,
                c2 = Vector3.one * 0.5f,
                c3 = Vector3.one * 0.5f,
                c4 = Vector3.one * 0.5f,
                c5 = Vector3.one * 0.5f,
                c6 = Vector3.one * 0.5f,
                c7 = Vector3.one * 0.5f,
                c8 = Vector3.one * 0.5f
            };
        }
    }

    [Serializable]
    public struct BurtXGIProbeFinalizedCell
    {
        public int cellIndex;
        public Vector3Int position;
        public Bounds bounds;
        public int minSubdivisionLevel;
        public int shChunkCount;
        public int brickStartIndex;
        public int brickCount;
        public int probeStartIndex;
        public int probeCount;
        public string[] sceneGuids;
        public bool hasVirtualOffset;
        public bool hasSkyVisibility;
        public bool hasSkyShadingDirection;
        public bool hasTimeSliceSH;
    }

    [Serializable]
    public sealed class BurtXGIProbeTimeSliceBakedDataAsset
    {
        public BurtGIProbeTimeSlice timeSlice = BurtGIProbeTimeSlice.Day;
        public BurtXGIProbeBakedDataAsset asset;
    }

    [Serializable]
    public sealed class BurtXGIProbeSceneBakeData
    {
        public string sceneGuid = string.Empty;
        public bool bakeScene = true;
        public bool hasProbeVolume;
        public Bounds bounds;
    }

    [Serializable]
    public sealed class BurtXGIProbePerSceneCellList
    {
        public string sceneGuid = string.Empty;
        public List<int> cellIndices = new List<int>();
    }

    [Serializable]
    public sealed class BurtXGIProbeBakedChunk
    {
        public int physicalChunkIndex;
        public int sharedPhysicalChunkIndex = -1;
        public byte[] l0L1Rx = Array.Empty<byte>();
        public byte[] l1GL1Ry = Array.Empty<byte>();
        public byte[] l1BL1Rz = Array.Empty<byte>();
        public byte[] l20 = Array.Empty<byte>();
        public byte[] l21 = Array.Empty<byte>();
        public byte[] l22 = Array.Empty<byte>();
        public byte[] l23 = Array.Empty<byte>();
        public byte[] skyVisibilityL0L1 = Array.Empty<byte>();
        public byte[] skyShadingDirectionIndices = Array.Empty<byte>();
        public byte[] validity = Array.Empty<byte>();
    }

    [Serializable]
    public sealed class BurtXGIProbeBakedCellData
    {
        public int cellIndex;
        public Vector3Int position;
        public Bounds bounds;
        public int minSubdivisionLevel;
        public int shChunkCount;
        public int probeStartIndex;
        public int probeCount;
        public string[] sceneGuids;
        public int pageTableDestinationIndex;
        public int indirectionDestinationIndex;
        public Vector3Int entryBlockMin;
        public Vector3Int entryBlockDimensions = Vector3Int.one;
        public uint[] pageTableEntries = Array.Empty<uint>();
        public Vector3Int[] indirectionEntries = Array.Empty<Vector3Int>();
        public BurtXGIProbeBakedChunk[] chunks = Array.Empty<BurtXGIProbeBakedChunk>();
    }

    [CreateAssetMenu(menuName = "Rendering/BurtRP/XGI Probe Baked Data", fileName = "BurtXGIProbeBakedData")]
    public sealed partial class BurtXGIProbeBakedDataAsset : ScriptableObject, ISerializationCallbackReceiver
    {
        public BurtXGIProbeBakingConfig sourceConfig;
        public Bounds globalBounds;
        public Vector3Int minCellPosition;
        public Vector3Int maxCellPosition;
        public Vector3 probeOffset;
        public Vector3 bakedProbeOffset;
        public float bakedMinDistanceBetweenProbes = 1f;
        public int bakedSimplificationLevels = 3;
        public BurtXGIProbeStreamerType bakedStreamerType = BurtXGIProbeStreamerType.AsyncRead;
        public int cellSizeInBricks = 1;
        public float cellSizeInMeters = 1f;
        public float minBrickSize = 1f;
        public int chunkSizeInBricks = BurtGIVirtualProbePhysicalPool.BricksPerChunk;
        public int chunkProbeCount = BurtGIVirtualProbePhysicalPool.ChunkProbeCount;
        public int cellCount;
        public int brickCount;
        public int probeCount;
        public int chunkCount;
        public Vector3Int physicalPoolChunkDimensions = Vector3Int.one;
        public int pageTableEntryCount;
        public int indirectionEntryCount;
        public Vector3Int virtualIndirectionDimensions = Vector3Int.one;
        public Vector3Int virtualMinEntryPosition;
        public int entriesPerCellDimension = 1;
        public bool hasVirtualOffset;
        public bool hasValidity;
        public bool hasSkyVisibility;
        public bool hasSkyShadingDirection;
        public bool hasTimeSliceSH;
        public bool hasRuntimeSettings;
        public bool hasRuntimeSystemParameters;
        public BurtXGIProbeSystemParameters runtimeSystemParameters = BurtXGIProbeSystemParameters.Default;
        public BurtGIProbeTimeSlice timeSliceType = BurtGIProbeTimeSlice.Day;
        [Min(0.0001f)] public float timeSliceMainLightIntensity = 1f;
        public bool enableShading = true;
        public float normalBias;
        public float viewBias;
        [Min(0f)] public float lightIntensity = 1.5f;
        [Range(0f, 1f)] public float skyVisibilityIntensity = 1f;
        public Color skyVisibilityTint = Color.white;
        public float skyVisibilityOffset;
        [Min(0f)] public float mainLightSHIntensity = 1f;
        public Color mainLightSHTint = Color.white;
        public bool mainLightSHUsesPreExposure = true;
        public BurtXGIProbeBakedCellData[] cells = Array.Empty<BurtXGIProbeBakedCellData>();
        public List<BurtXGIProbePerSceneCellList> perSceneCellLists =
            new List<BurtXGIProbePerSceneCellList>();

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            timeSliceType = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(timeSliceType);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        public Vector3Int ResolvedPhysicalPoolChunkDimensions
        {
            get
            {
                var safeChunkCount = Mathf.Max(1, chunkCount);
                var dimensions = Vector3Int.Max(Vector3Int.one, physicalPoolChunkDimensions);
                var capacity = (long)dimensions.x * dimensions.y * dimensions.z;
                if (capacity >= safeChunkCount)
                {
                    return dimensions;
                }

                return new Vector3Int(safeChunkCount, 1, 1);
            }
        }

        public float ResolvedCellSizeInMeters
        {
            get
            {
                if (cellSizeInMeters > 0.0001f)
                {
                    return cellSizeInMeters;
                }

                var resolvedCellSizeInBricks = cellSizeInBricks > 0
                    ? cellSizeInBricks
                    : BurtXGIProbeBakingConfig.GetCellSizeInBricks(Mathf.Max(0, bakedSimplificationLevels));
                var resolvedMinBrickSize = minBrickSize > 0.0001f
                    ? minBrickSize
                    : BurtXGIProbeBakingConfig.GetMinBrickSize(bakedMinDistanceBetweenProbes);
                return Mathf.Max(0.0001f, resolvedCellSizeInBricks * resolvedMinBrickSize);
            }
        }

        public bool HasBakedValidityData => hasValidity || HasChunkBytes(chunk => chunk.validity);
        public bool HasBakedSkyVisibilityData => hasSkyVisibility || HasChunkBytes(chunk => chunk.skyVisibilityL0L1);
        public bool HasBakedSkyShadingDirectionData => hasSkyShadingDirection || HasChunkBytes(chunk => chunk.skyShadingDirectionIndices);
        public bool HasBakedL2Data => HasChunkBytes(chunk => chunk.l20) &&
            HasChunkBytes(chunk => chunk.l21) &&
            HasChunkBytes(chunk => chunk.l22) &&
            HasChunkBytes(chunk => chunk.l23);
        public bool RuntimeSystemEnabled => hasRuntimeSystemParameters
            ? runtimeSystemParameters.enable
            : sourceConfig == null || sourceConfig.systemParameters.enable;
        public BurtXGIProbeSHBands RuntimeSHBands => (hasRuntimeSystemParameters
            ? runtimeSystemParameters.shBands
            : sourceConfig != null
                ? sourceConfig.systemParameters.shBands
                : BurtXGIProbeSystemParameters.Default.shBands).NormalizedQuality();
        public bool AllowsRuntimeL1Data => RuntimeSHBands.HasL1();
        public bool AllowsRuntimeL2Data => RuntimeSHBands.HasL2();
        public BurtXGIProbeTextureMemoryBudget RuntimeMemoryBudget => hasRuntimeSystemParameters
            ? runtimeSystemParameters.memoryBudget
            : sourceConfig != null
                ? sourceConfig.systemParameters.memoryBudget
                : BurtXGIProbeTextureMemoryBudget.Medium;

        public bool TryValidateRuntimeLoadData(
            string guid,
            BurtXGIProbeSHBands shBands,
            bool requireSkyVisibility,
            bool requireSkyShadingDirection,
            out List<int> cellIndices,
            out string failReason)
        {
            cellIndices = null;
            failReason = null;

            if (cells == null || cells.Length == 0)
            {
                failReason = "missing baked cells";
                return false;
            }

            cellIndices = GetRuntimeSceneCellIndices(guid);
            if (cellIndices == null)
            {
                if (perSceneCellLists != null && perSceneCellLists.Count > 0)
                {
                    failReason = string.IsNullOrEmpty(guid)
                        ? "empty scene guid"
                        : "scene has no baked cell list: " + guid;
                    return false;
                }

                cellIndices = new List<int>(cells.Length);
                for (var i = 0; i < cells.Length; i++)
                {
                    if (cells[i] != null)
                    {
                        cellIndices.Add(cells[i].cellIndex);
                    }
                }
            }

            if (cellIndices.Count == 0)
            {
                failReason = "runtime scene cell list is empty";
                return false;
            }

            var needsL0 = hasTimeSliceSH && shBands.HasL0();
            var needsL1 = hasTimeSliceSH && shBands.HasL1();
            var needsL2 = hasTimeSliceSH && shBands.HasL2();
            var needsValidity = HasBakedValidityData;
            var needsSkyVisibility = requireSkyVisibility && HasBakedSkyVisibilityData;
            var needsSkyShadingDirection = requireSkyShadingDirection && HasBakedSkyShadingDirectionData;
            for (var index = 0; index < cellIndices.Count; index++)
            {
                var cellIndex = cellIndices[index];
                var cell = FindBakedCell(cellIndex);
                if (cell == null)
                {
                    failReason = "missing baked cell: " + cellIndex;
                    return false;
                }

                if (!ValidateRuntimeCellData(
                        cell,
                        needsL0,
                        needsL1,
                        needsL2,
                        needsValidity,
                        needsSkyVisibility,
                        needsSkyShadingDirection,
                        out failReason))
                {
                    return false;
                }
            }

            return true;
        }

        public List<int> GetSceneCellIndices(string guid)
        {
            if (string.IsNullOrEmpty(guid) || perSceneCellLists == null)
            {
                return null;
            }

            for (var index = 0; index < perSceneCellLists.Count; index++)
            {
                var entry = perSceneCellLists[index];
                if (entry != null && string.Equals(entry.sceneGuid, guid, StringComparison.OrdinalIgnoreCase))
                {
                    return entry.cellIndices;
                }
            }

            return null;
        }

        public List<int> GetRuntimeSceneCellIndices(string guid)
        {
            var cellIndices = GetSceneCellIndices(guid);
            if (cellIndices != null)
            {
                return cellIndices;
            }

            return perSceneCellLists != null && perSceneCellLists.Count == 1
                ? perSceneCellLists[0]?.cellIndices
                : null;
        }

        private BurtXGIProbeBakedCellData FindBakedCell(int cellIndex)
        {
            if (cells == null)
            {
                return null;
            }

            for (var i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                if (cell != null && cell.cellIndex == cellIndex)
                {
                    return cell;
                }
            }

            return null;
        }

        private static bool ValidateRuntimeCellData(
            BurtXGIProbeBakedCellData cell,
            bool needsL0,
            bool needsL1,
            bool needsL2,
            bool needsValidity,
            bool needsSkyVisibility,
            bool needsSkyShadingDirection,
            out string failReason)
        {
            failReason = null;
            if (cell.pageTableEntries == null || cell.pageTableEntries.Length == 0)
            {
                failReason = "missing page table entries: " + cell.cellIndex;
                return false;
            }

            if (cell.indirectionEntries == null || cell.indirectionEntries.Length == 0)
            {
                failReason = "missing indirection entries: " + cell.cellIndex;
                return false;
            }

            if (cell.chunks == null || cell.chunks.Length == 0)
            {
                failReason = "missing chunks: " + cell.cellIndex;
                return false;
            }

            for (var chunkIndex = 0; chunkIndex < cell.chunks.Length; chunkIndex++)
            {
                var chunk = cell.chunks[chunkIndex];
                if (chunk == null)
                {
                    failReason = "missing chunk: " + cell.cellIndex + "/" + chunkIndex;
                    return false;
                }

                if (needsL0 && !HasBytes(chunk.l0L1Rx))
                {
                    failReason = "missing L0L1Rx chunk: " + cell.cellIndex + "/" + chunkIndex;
                    return false;
                }

                if (needsL1 && (!HasBytes(chunk.l1GL1Ry) || !HasBytes(chunk.l1BL1Rz)))
                {
                    failReason = "missing L1 chunk: " + cell.cellIndex + "/" + chunkIndex;
                    return false;
                }

                if (needsL2 && (!HasBytes(chunk.l20) || !HasBytes(chunk.l21) || !HasBytes(chunk.l22) || !HasBytes(chunk.l23)))
                {
                    failReason = "missing L2 chunk: " + cell.cellIndex + "/" + chunkIndex;
                    return false;
                }

                if (needsValidity && !HasSharedChunkBytes(cell, chunk, candidate => candidate.validity))
                {
                    failReason = "missing shared validity chunk: " + cell.cellIndex + "/" + ResolveSharedPhysicalChunkIndex(chunk);
                    return false;
                }

                if (needsSkyVisibility && !HasSharedChunkBytes(cell, chunk, candidate => candidate.skyVisibilityL0L1))
                {
                    failReason = "missing shared sky visibility chunk: " + cell.cellIndex + "/" + ResolveSharedPhysicalChunkIndex(chunk);
                    return false;
                }

                if (needsSkyShadingDirection && !HasSharedChunkBytes(cell, chunk, candidate => candidate.skyShadingDirectionIndices))
                {
                    failReason = "missing shared sky shading direction chunk: " + cell.cellIndex + "/" + ResolveSharedPhysicalChunkIndex(chunk);
                    return false;
                }
            }

            return true;
        }

        private static bool HasSharedChunkBytes(
            BurtXGIProbeBakedCellData cell,
            BurtXGIProbeBakedChunk chunk,
            Func<BurtXGIProbeBakedChunk, byte[]> selector)
        {
            if (cell?.chunks == null || chunk == null || selector == null)
            {
                return false;
            }

            var sharedChunkIndex = ResolveSharedPhysicalChunkIndex(chunk);
            for (var index = 0; index < cell.chunks.Length; index++)
            {
                var candidate = cell.chunks[index];
                if (candidate == null)
                {
                    continue;
                }

                if (ResolveSharedPhysicalChunkIndex(candidate) != sharedChunkIndex &&
                    candidate.physicalChunkIndex != sharedChunkIndex)
                {
                    continue;
                }

                if (HasBytes(selector(candidate)))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ResolveSharedPhysicalChunkIndex(BurtXGIProbeBakedChunk chunk)
        {
            if (chunk == null)
            {
                return -1;
            }

            return chunk.sharedPhysicalChunkIndex >= 0 ? chunk.sharedPhysicalChunkIndex : chunk.physicalChunkIndex;
        }

        private static bool HasBytes(byte[] bytes)
        {
            return bytes != null && bytes.Length > 0;
        }

        public void CaptureRuntimeSettings(BurtXGIProbeBakingConfig config)
        {
            if (config == null)
            {
                return;
            }

            enableShading = config.enableShading;
            runtimeSystemParameters = config.systemParameters;
            hasRuntimeSystemParameters = true;
            normalBias = config.normalBias;
            viewBias = config.viewBias;
            lightIntensity = config.lightIntensity;
            skyVisibilityIntensity = config.skyVisibilityIntensity;
            skyVisibilityTint = config.skyVisibilityTint;
            skyVisibilityOffset = config.skyVisibilityOffset;
            mainLightSHIntensity = config.mainLightSHIntensity;
            mainLightSHTint = config.mainLightSHTint;
            mainLightSHUsesPreExposure = config.mainLightSHUsesPreExposure;
            hasRuntimeSettings = true;
        }

        public void ApplyRuntimeSettings(BurtGIProbeVolume probeVolume)
        {
            if (probeVolume == null)
            {
                return;
            }

            if (!hasRuntimeSettings && sourceConfig != null)
            {
                probeVolume.virtualEnableShading = sourceConfig.enableShading && sourceConfig.systemParameters.enable;
                probeVolume.virtualSHBands = sourceConfig.systemParameters.shBands.NormalizedQuality();
                probeVolume.virtualNormalBias = sourceConfig.normalBias;
                probeVolume.virtualViewBias = sourceConfig.viewBias;
                probeVolume.virtualLightIntensity = sourceConfig.lightIntensity;
                probeVolume.virtualSkyVisibilityTint = sourceConfig.skyVisibilityTint;
                probeVolume.virtualSkyVisibilityIntensity = sourceConfig.skyVisibilityIntensity;
                probeVolume.virtualSkyVisibilityOffset = sourceConfig.skyVisibilityOffset;
                probeVolume.virtualMainLightSHTint = sourceConfig.mainLightSHTint;
                probeVolume.virtualMainLightSHIntensity = sourceConfig.mainLightSHIntensity;
                probeVolume.virtualMainLightSHUsesPreExposure = sourceConfig.mainLightSHUsesPreExposure;
                return;
            }

            probeVolume.virtualEnableShading = enableShading && RuntimeSystemEnabled;
            probeVolume.virtualSHBands = RuntimeSHBands;
            probeVolume.virtualNormalBias = normalBias;
            probeVolume.virtualViewBias = viewBias;
            probeVolume.virtualLightIntensity = lightIntensity;
            probeVolume.virtualSkyVisibilityTint = skyVisibilityTint;
            probeVolume.virtualSkyVisibilityIntensity = skyVisibilityIntensity;
            probeVolume.virtualSkyVisibilityOffset = skyVisibilityOffset;
            probeVolume.virtualMainLightSHTint = mainLightSHTint;
            probeVolume.virtualMainLightSHIntensity = mainLightSHIntensity;
            probeVolume.virtualMainLightSHUsesPreExposure = mainLightSHUsesPreExposure;
        }

        private bool HasChunkBytes(Func<BurtXGIProbeBakedChunk, byte[]> selector)
        {
            if (cells == null || selector == null)
            {
                return false;
            }

            for (var cellIndex = 0; cellIndex < cells.Length; cellIndex++)
            {
                var chunks = cells[cellIndex]?.chunks;
                if (chunks == null)
                {
                    continue;
                }

                for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
                {
                    var bytes = chunks[chunkIndex] != null ? selector(chunks[chunkIndex]) : null;
                    if (bytes != null && bytes.Length > 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    [CreateAssetMenu(menuName = "Rendering/BurtRP/XGI Probe Baking Config", fileName = "BurtXGIProbeBakingConfig")]
    [MovedFrom(true, "UnityEngine.Rendering", "FunPlus.WorldX.XRender.Runtime", "XGIProbeBakingConfig")]
    public sealed class BurtXGIProbeBakingConfig : ScriptableObject, ISerializationCallbackReceiver
    {
        [Serializable]
        private struct LegacySerializedPerSceneCellList
        {
            public string sceneGUID;
            public List<int> cellList;
        }

        [Header("Scene")]
        [FormerlySerializedAs("m_SceneGUID")]
        public string sceneGuid = string.Empty;
        public string scenePath = string.Empty;
        public string sceneName = string.Empty;
        public List<BurtXGIProbeSceneBakeData> sceneBakeData =
            new List<BurtXGIProbeSceneBakeData>();
        public List<BurtXGIProbePerSceneCellList> perSceneCellLists =
            new List<BurtXGIProbePerSceneCellList>();
        [SerializeField, HideInInspector]
        private List<LegacySerializedPerSceneCellList> m_SerializedPerSceneCellList;

        [Header("Placement")]
        public Vector3 probeOffset = Vector3.zero;
        [Range(0.5f, 64f)] public float minDistanceBetweenProbes = 1f;
        [Range(2, 4)] public int simplificationLevels = 3;
        public BurtXGIProbeStreamerType streamerType = BurtXGIProbeStreamerType.AsyncRead;

        [Header("Baking")]
        [FormerlySerializedAs("m_Platform")]
        public BurtXGIProbeBakingPlatform platform = BurtXGIProbeBakingPlatform.PC;
        public bool useHardWareRayTracing = true;
        [Min(1)] public int maxJobSize = 256;

        [Header("Virtual Offset")]
        public bool virtualOffset = true;
        [Range(0f, 5f)] public float virtualOffsetSearchMultiplier = 3f;
        [Range(0f, 0.95f)] public float virtualOffsetValidityThreshold = 0.25f;
        [Range(0f, 1f)] public float virtualOffsetOutOfGeoOffset = 0.01f;
        [Range(-0.05f, 0f)] public float virtualOffsetRayOriginBias = -0.01f;

        [Header("Time Slice")]
        public bool useTimeSliceData = true;
        public BurtGIProbeTimeSlice timeSliceType = BurtGIProbeTimeSlice.Day;
        public bool bakeAllTimeSlices;
        [Min(1)] public int timeSliceBakingSamples = 4096;
        [Min(1)] public int timeSliceBakingBounces = 10;
        [Min(0f)] public float timeSliceOffsetRay = 0.2f;
        public bool timeSliceRayCullBackFace;
        [Min(1)] public int timeSliceSampleCountPerStep = 16;

        [Header("Sky Visibility")]
        public bool skyVisibility = true;
        [Min(1)] public int skyVisibilityBakingSamples = 4096;
        [Min(1)] public int skyVisibilityBakingBounces = 4;
        [Range(0f, 1f)] public float skyVisibilityAverageAlbedo = 0.4f;
        [Min(0f)] public float skyVisibilityOffsetRay = 0.2f;
        public bool skyVisibilityShadingDirection;
        public bool skyVisibilityRayCullBackFace;
        [Min(1)] public int skyVisibilitySampleCountPerStep = 16;

        [Header("Runtime Shading")]
        public bool enableShading = true;
        public float normalBias;
        public float viewBias;
        [Min(0f)] public float lightIntensity = 1.5f;
        [Range(0f, 1f)] public float skyVisibilityIntensity = 1f;
        public Color skyVisibilityTint = Color.white;
        public float skyVisibilityOffset;
        [Min(0f)] public float mainLightSHIntensity = 1f;
        public Color mainLightSHTint = Color.white;
        public bool mainLightSHUsesPreExposure = true;
        public BurtXGIProbeSystemParameters systemParameters = BurtXGIProbeSystemParameters.Default;

        [Header("Baked Metadata")]
        public int chunkSizeInBricks = BurtGIVirtualProbePhysicalPool.BricksPerChunk;
        public Vector3Int minCellPosition;
        public Vector3Int maxCellPosition;
        public Bounds globalBounds;
        public Vector3 bakedProbeOffset = Vector3.zero;
        public float bakedMinDistanceBetweenProbes = -1f;
        public int bakedSimplificationLevels = -1;
        public BurtXGIProbeStreamerType bakedStreamerType = BurtXGIProbeStreamerType.AsyncRead;
        public bool bakedUseTimeSlice;
        [SerializeField, HideInInspector]
        private int bakedUseTimeSliceValue = -1;
        public BurtGIProbeTimeSlice bakedTimeSliceType = BurtGIProbeTimeSlice.Day;
        [Min(0.0001f)] public float bakedTimeSliceMainLightIntensity = 1f;
        public bool bakedSkyVisibility;
        [SerializeField, HideInInspector]
        private int bakedSkyVisibilityValue = -1;
        public bool bakedSkyShadingDirection;
        [SerializeField, HideInInspector]
        private int bakedSkyShadingDirectionValue = -1;
        [FormerlySerializedAs("maxSHChunkCount")]
        [SerializeField, HideInInspector]
        private int legacyXRenderMaxSHChunkCount = -1;
        [FormerlySerializedAs("supportPositionChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderSupportPositionChunkSize;
        [FormerlySerializedAs("supportOffsetsChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderSupportOffsetsChunkSize;
        [FormerlySerializedAs("supportDataChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderSupportDataChunkSize;
        [FormerlySerializedAs("sharedSkyVisibilityL0L1ChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderSharedSkyVisibilityL0L1ChunkSize;
        [FormerlySerializedAs("sharedSkyShadingDirectionIndicesChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderSharedSkyShadingDirectionIndicesChunkSize;
        [FormerlySerializedAs("sharedDataChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderSharedDataChunkSize;
        [FormerlySerializedAs("l0ChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderL0ChunkSize;
        [FormerlySerializedAs("l1ChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderL1ChunkSize;
        [FormerlySerializedAs("l2TextureChunkSize")]
        [SerializeField, HideInInspector]
        private int legacyXRenderL2TextureChunkSize;
        public int bakedCellCount;
        public int bakedBrickCount;
        public int bakedProbeCount;
        public int bakedVirtualOffsetCount;
        public int bakedVirtualOffsetInvalidCount;
        public int bakedSkyVisibilityCount;
        public int bakedSkyShadingDirectionCount;
        public int bakedTimeSliceSHCount;
        public int bakedFinalizedCellCount;
        public int bakedSerializedCellCount;
        public int bakedSerializedChunkCount;
        public bool bakedVirtualOffsetApplied;
        public BurtXGIProbeBakedDataAsset bakedDataAsset;
        public List<BurtXGIProbeTimeSliceBakedDataAsset> timeSliceBakedDataAssets =
            new List<BurtXGIProbeTimeSliceBakedDataAsset>();
        public BurtXGIProbePlacedCell[] bakedPlacedCells = Array.Empty<BurtXGIProbePlacedCell>();
        public BurtXGIProbePlacedBrick[] bakedPlacedBricks = Array.Empty<BurtXGIProbePlacedBrick>();
        public Vector3[] bakedProbePositions = Array.Empty<Vector3>();
        public Vector3[] bakedVirtualOffsets = Array.Empty<Vector3>();
        public Vector3[] bakedVirtualOffsetProbePositions = Array.Empty<Vector3>();
        public Vector4[] bakedSkyVisibilityL0L1 = Array.Empty<Vector4>();
        public byte[] bakedSkyShadingDirectionIndices = Array.Empty<byte>();
        public BurtXGIProbeBakedSphericalHarmonicsL2[] bakedTimeSliceSH = Array.Empty<BurtXGIProbeBakedSphericalHarmonicsL2>();
        public BurtXGIProbeFinalizedCell[] bakedFinalizedCells = Array.Empty<BurtXGIProbeFinalizedCell>();

        public float MinBrickSize => GetMinBrickSize(minDistanceBetweenProbes);
        public int CellSizeInBricks => GetCellSizeInBricks(simplificationLevels);
        public float CellSizeInMeters => CellSizeInBricks * MinBrickSize;
        public bool HasCapturedBakedPlacementMetadata => bakedMinDistanceBetweenProbes > 0f && bakedSimplificationLevels >= 0;
        public Vector3 BakedProbeOffset => HasCapturedBakedPlacementMetadata ? bakedProbeOffset : probeOffset;
        public float BakedMinDistanceBetweenProbes => HasCapturedBakedPlacementMetadata ? bakedMinDistanceBetweenProbes : minDistanceBetweenProbes;
        public int BakedSimplificationLevels => HasCapturedBakedPlacementMetadata ? bakedSimplificationLevels : simplificationLevels;
        public BurtXGIProbeStreamerType BakedStreamerType => HasCapturedBakedPlacementMetadata ? bakedStreamerType : streamerType;
        public float BakedMinBrickSize => GetMinBrickSize(BakedMinDistanceBetweenProbes);
        public int BakedCellSizeInBricks => GetCellSizeInBricks(BakedSimplificationLevels);
        public float BakedCellSizeInMeters => BakedCellSizeInBricks * BakedMinBrickSize;
        public int ChunkProbeCount => BurtGIVirtualProbePhysicalPool.ChunkProbeCount;
        public int L0L1RxChunkSize => GetL0L1RxChunkSize(systemParameters.shBands);
        public int L1ChunkSize => GetL1ChunkSize(systemParameters.shBands);
        public int L2TextureChunkSize => GetL2TextureChunkSize(systemParameters.shBands);
        public int SharedDataChunkSize => GetSharedDataChunkSize(skyVisibility, skyVisibilityShadingDirection);
        public int ChunkGPUMemoryBytes => GetChunkGPUMemory(systemParameters.shBands, skyVisibility, skyVisibilityShadingDirection);
        public bool HasLegacyXRenderStreamingLayout => legacyXRenderMaxSHChunkCount >= 0 ||
            legacyXRenderSupportDataChunkSize > 0 ||
            legacyXRenderSharedDataChunkSize > 0 ||
            legacyXRenderL0ChunkSize > 0 ||
            legacyXRenderL1ChunkSize > 0 ||
            legacyXRenderL2TextureChunkSize > 0;
        public bool RequiresLegacyXRenderProbeDataImport => HasLegacyXRenderStreamingLayout &&
            bakedDataAsset == null &&
            !HasTimeSliceBakedDataAssets;
        public int LegacyXRenderMaxSHChunkCount => legacyXRenderMaxSHChunkCount;
        public int LegacyXRenderSupportPositionChunkSize => legacyXRenderSupportPositionChunkSize;
        public int LegacyXRenderSupportOffsetsChunkSize => legacyXRenderSupportOffsetsChunkSize;
        public int LegacyXRenderSupportDataChunkSize => legacyXRenderSupportDataChunkSize;
        public int LegacyXRenderSharedSkyVisibilityL0L1ChunkSize => legacyXRenderSharedSkyVisibilityL0L1ChunkSize;
        public int LegacyXRenderSharedSkyShadingDirectionIndicesChunkSize => legacyXRenderSharedSkyShadingDirectionIndicesChunkSize;
        public int LegacyXRenderSharedDataChunkSize => legacyXRenderSharedDataChunkSize;
        public int LegacyXRenderL0ChunkSize => legacyXRenderL0ChunkSize;
        public int LegacyXRenderL1ChunkSize => legacyXRenderL1ChunkSize;
        public int LegacyXRenderL2TextureChunkSize => legacyXRenderL2TextureChunkSize;

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            NormalizeLegacySerializedValues();
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_SerializedPerSceneCellList = null;
            bakedUseTimeSliceValue = -1;
            bakedSkyVisibilityValue = -1;
            bakedSkyShadingDirectionValue = -1;
        }

        private void NormalizeLegacySerializedValues()
        {
            systemParameters.NormalizeLegacySerializedValues();
            NormalizeLegacyTimeSlice(ref timeSliceType);
            NormalizeLegacyTimeSlice(ref bakedTimeSliceType);
            NormalizeTimeSliceBakedDataAssets();
            MigrateLegacyBakedFlags();
            MigrateLegacyPerSceneCellLists();
        }

        private static void NormalizeLegacyTimeSlice(ref BurtGIProbeTimeSlice slice)
        {
            slice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(slice);
        }

        private void NormalizeTimeSliceBakedDataAssets()
        {
            if (timeSliceBakedDataAssets == null)
            {
                return;
            }

            for (var index = 0; index < timeSliceBakedDataAssets.Count; index++)
            {
                var entry = timeSliceBakedDataAssets[index];
                if (entry == null)
                {
                    continue;
                }

                entry.timeSlice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice);
                if (entry.asset != null)
                {
                    entry.asset.timeSliceType = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.asset.timeSliceType);
                }
            }
        }

        private void MigrateLegacyBakedFlags()
        {
            if (bakedUseTimeSliceValue >= 0)
            {
                bakedUseTimeSlice = bakedUseTimeSliceValue > 0;
            }

            if (bakedSkyVisibilityValue >= 0)
            {
                bakedSkyVisibility = bakedSkyVisibilityValue > 0;
            }

            if (bakedSkyShadingDirectionValue >= 0)
            {
                bakedSkyShadingDirection = bakedSkyShadingDirectionValue > 0;
            }
        }

        private void MigrateLegacyPerSceneCellLists()
        {
            if ((perSceneCellLists != null && perSceneCellLists.Count > 0) ||
                m_SerializedPerSceneCellList == null ||
                m_SerializedPerSceneCellList.Count == 0)
            {
                return;
            }

            perSceneCellLists = new List<BurtXGIProbePerSceneCellList>(m_SerializedPerSceneCellList.Count);
            for (var index = 0; index < m_SerializedPerSceneCellList.Count; index++)
            {
                var legacyEntry = m_SerializedPerSceneCellList[index];
                perSceneCellLists.Add(new BurtXGIProbePerSceneCellList
                {
                    sceneGuid = legacyEntry.sceneGUID ?? string.Empty,
                    cellIndices = legacyEntry.cellList != null
                        ? new List<int>(legacyEntry.cellList)
                        : new List<int>()
                });
            }
        }

        public void CaptureSceneMetadata(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            scenePath = scene.path ?? string.Empty;
            sceneName = scene.name ?? string.Empty;
#if UNITY_EDITOR
            sceneGuid = !string.IsNullOrEmpty(scenePath) ? AssetDatabase.AssetPathToGUID(scenePath) : string.Empty;
#endif
        }

        public bool MatchesScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            var resolvedScenePath = scene.path ?? string.Empty;
            if (!string.IsNullOrEmpty(scenePath) &&
                string.Equals(scenePath, resolvedScenePath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

#if UNITY_EDITOR
            var resolvedSceneGuid = !string.IsNullOrEmpty(resolvedScenePath)
                ? AssetDatabase.AssetPathToGUID(resolvedScenePath)
                : string.Empty;
            if (!string.IsNullOrEmpty(sceneGuid) &&
                string.Equals(sceneGuid, resolvedSceneGuid, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
#endif

            return !string.IsNullOrEmpty(sceneName) &&
                string.Equals(sceneName, scene.name, StringComparison.OrdinalIgnoreCase);
        }

        public BurtXGIProbeSceneBakeData GetSceneBakeData(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            sceneBakeData ??= new List<BurtXGIProbeSceneBakeData>();
            for (var index = 0; index < sceneBakeData.Count; index++)
            {
                var data = sceneBakeData[index];
                if (data != null && string.Equals(data.sceneGuid, guid, StringComparison.OrdinalIgnoreCase))
                {
                    return data;
                }
            }

            var newData = new BurtXGIProbeSceneBakeData
            {
                sceneGuid = guid
            };
            sceneBakeData.Add(newData);
            return newData;
        }

        public bool TryGetSceneBakeData(string guid, out BurtXGIProbeSceneBakeData data)
        {
            data = null;
            if (string.IsNullOrEmpty(guid) || sceneBakeData == null)
            {
                return false;
            }

            for (var index = 0; index < sceneBakeData.Count; index++)
            {
                var candidate = sceneBakeData[index];
                if (candidate != null && string.Equals(candidate.sceneGuid, guid, StringComparison.OrdinalIgnoreCase))
                {
                    data = candidate;
                    return true;
                }
            }

            return false;
        }

        public BurtXGIProbePerSceneCellList GetPerSceneCellList(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return null;
            }

            perSceneCellLists ??= new List<BurtXGIProbePerSceneCellList>();
            for (var index = 0; index < perSceneCellLists.Count; index++)
            {
                var entry = perSceneCellLists[index];
                if (entry != null && string.Equals(entry.sceneGuid, guid, StringComparison.OrdinalIgnoreCase))
                {
                    entry.cellIndices ??= new List<int>();
                    return entry;
                }
            }

            var newEntry = new BurtXGIProbePerSceneCellList
            {
                sceneGuid = guid
            };
            perSceneCellLists.Add(newEntry);
            return newEntry;
        }

        public List<int> GetRuntimeSceneCellIndices(string guid)
        {
            if (!string.IsNullOrEmpty(guid) && perSceneCellLists != null)
            {
                for (var index = 0; index < perSceneCellLists.Count; index++)
                {
                    var entry = perSceneCellLists[index];
                    if (entry != null && string.Equals(entry.sceneGuid, guid, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry.cellIndices;
                    }
                }
            }

            return perSceneCellLists != null && perSceneCellLists.Count == 1
                ? perSceneCellLists[0]?.cellIndices
                : null;
        }

        public void SetSceneCellIndices(Scene scene, IEnumerable<int> cellIndices)
        {
            if (!scene.IsValid())
            {
                return;
            }

            CaptureSceneMetadata(scene);
            var guid = ResolveSceneGuid(scene);
            if (string.IsNullOrEmpty(guid))
            {
                guid = !string.IsNullOrEmpty(sceneGuid) ? sceneGuid : scene.path;
            }

            if (string.IsNullOrEmpty(guid))
            {
                guid = scene.name;
            }

            var entry = GetPerSceneCellList(guid);
            if (entry == null)
            {
                return;
            }

            entry.cellIndices ??= new List<int>();
            entry.cellIndices.Clear();
            if (cellIndices == null)
            {
                return;
            }

            foreach (var cellIndex in cellIndices)
            {
                entry.cellIndices.Add(cellIndex);
            }
        }

        public bool UpdateSceneBakeData(Scene scene, bool hasProbeVolume, Bounds bounds)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            CaptureSceneMetadata(scene);
            var guid = ResolveSceneGuid(scene);
            if (string.IsNullOrEmpty(guid))
            {
                guid = !string.IsNullOrEmpty(sceneGuid) ? sceneGuid : scene.path;
            }

            if (string.IsNullOrEmpty(guid))
            {
                guid = scene.name;
            }

            var data = GetSceneBakeData(guid);
            if (data == null)
            {
                return false;
            }

            var changed = !string.Equals(data.sceneGuid, guid, StringComparison.OrdinalIgnoreCase) ||
                data.hasProbeVolume != hasProbeVolume ||
                data.bounds != bounds;
            data.sceneGuid = guid;
            data.hasProbeVolume = hasProbeVolume;
            if (hasProbeVolume)
            {
                data.bounds = bounds;
            }

            return changed;
        }

        public static BurtXGIProbeBakingConfig GetBakingConfigForScene(Scene scene)
        {
            return GetBakingConfigForScene(scene, GetCurrentPlatform());
        }

        public static BurtXGIProbeBakingConfig GetBakingConfigForScene(Scene scene, BurtXGIProbeBakingPlatform platform)
        {
            return TryGetBakingConfigForScene(scene, platform, out var config) ? config : null;
        }

        public static bool TryGetBakingConfigForScene(Scene scene, out BurtXGIProbeBakingConfig config)
        {
            return TryGetBakingConfigForScene(scene, GetCurrentPlatform(), out config);
        }

        public static bool TryGetBakingConfigForScene(Scene scene, BurtXGIProbeBakingPlatform platform, out BurtXGIProbeBakingConfig config)
        {
            config = null;
            if (!scene.IsValid())
            {
                return false;
            }

#if UNITY_EDITOR
            var scenePathToMatch = scene.path ?? string.Empty;
            var sceneGuidToMatch = !string.IsNullOrEmpty(scenePathToMatch)
                ? AssetDatabase.AssetPathToGUID(scenePathToMatch)
                : string.Empty;
            var guids = AssetDatabase.FindAssets("t:BurtXGIProbeBakingConfig");
            var bestScore = int.MinValue;
            for (var index = 0; index < guids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                var candidate = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(assetPath);
                if (candidate == null || candidate.platform != platform)
                {
                    continue;
                }

                var score = candidate.ScoreSceneMatch(scene, sceneGuidToMatch, scenePathToMatch, assetPath);
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                config = candidate;
            }

            return config != null && bestScore > 0;
#else
            return false;
#endif
        }

        public static bool TryGetBakingConfigForScene(string guid, BurtXGIProbeBakingPlatform platform, out BurtXGIProbeBakingConfig config)
        {
            config = null;
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            var guids = AssetDatabase.FindAssets("t:BurtXGIProbeBakingConfig");
            for (var index = 0; index < guids.Length; index++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[index]);
                var candidate = AssetDatabase.LoadAssetAtPath<BurtXGIProbeBakingConfig>(assetPath);
                if (candidate == null || candidate.platform != platform ||
                    !string.Equals(candidate.sceneGuid, guid, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                config = candidate;
                return true;
            }
#endif

            return false;
        }

        public static bool SceneHasProbeVolumes(string guid)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return false;
            }

            return TryGetBakingConfigForScene(guid, GetCurrentPlatform(), out var config) &&
                config.TryGetSceneBakeData(guid, out var data) &&
                data.hasProbeVolume;
        }

        public static bool SceneHasProbeVolumes(Scene scene)
        {
            if (!scene.IsValid())
            {
                return false;
            }

            var guid = ResolveSceneGuid(scene);
            if (string.IsNullOrEmpty(guid))
            {
                guid = scene.path;
            }

            return SceneHasProbeVolumes(guid);
        }

        public const string GeneratedConfigRootPath = "Assets/BurtRP/Generated/XGI";

        public static BurtXGIProbeBakingPlatform GetCurrentPlatform()
        {
#if UNITY_EDITOR
            if (HasCurrentPlatformOverride)
            {
                return CurrentPlatformOverride;
            }

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            return buildTarget == BuildTarget.Android || buildTarget == BuildTarget.iOS
                ? BurtXGIProbeBakingPlatform.Mobile
                : BurtXGIProbeBakingPlatform.PC;
#else
            return Application.isMobilePlatform ? BurtXGIProbeBakingPlatform.Mobile : BurtXGIProbeBakingPlatform.PC;
#endif
        }

#if UNITY_EDITOR
        private static bool HasCurrentPlatformOverride;
        private static BurtXGIProbeBakingPlatform CurrentPlatformOverride;

        public static void SetCurrentPlatformOverride(BurtXGIProbeBakingPlatform platform)
        {
            HasCurrentPlatformOverride = true;
            CurrentPlatformOverride = platform;
        }

        public static void ClearCurrentPlatformOverride()
        {
            HasCurrentPlatformOverride = false;
        }
#endif

        public static string GetBakingConfigDirectory(string sceneName)
        {
            return GeneratedConfigRootPath + "/" + SanitizeAssetName(ResolveSceneName(sceneName));
        }

        public static string GetBakingConfigName(string sceneName, BurtXGIProbeBakingPlatform platform)
        {
            return SanitizeAssetName(ResolveSceneName(sceneName)) + "_" + platform + "_BakingConfig";
        }

        public static string GetRuntimeBakingConfigAssetPath(string sceneName, BurtXGIProbeBakingPlatform platform)
        {
            return GetBakingConfigDirectory(sceneName) + "/" + GetBakingConfigName(sceneName, platform) + ".asset";
        }

        public static string ResolveSceneName(Scene scene)
        {
            return ResolveSceneName(scene.IsValid() ? scene.name : string.Empty);
        }

        public static string ResolveSceneName(string sceneName)
        {
            return string.IsNullOrEmpty(sceneName) ? "BurtXGI" : sceneName;
        }

        public static string SanitizeAssetName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "BurtXGI";
            }

            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            for (var index = 0; index < invalidChars.Length; index++)
            {
                value = value.Replace(invalidChars[index], '_');
            }

            return value;
        }

        private static string ResolveSceneGuid(Scene scene)
        {
            if (!scene.IsValid())
            {
                return string.Empty;
            }

#if UNITY_EDITOR
            return !string.IsNullOrEmpty(scene.path) ? AssetDatabase.AssetPathToGUID(scene.path) : string.Empty;
#else
            return string.Empty;
#endif
        }

#if UNITY_EDITOR
        public static bool IsInExpectedDirectory(BurtXGIProbeBakingConfig config, string scenePath, string sceneName)
        {
            if (config == null)
            {
                return false;
            }

            if (string.IsNullOrEmpty(sceneName) && !string.IsNullOrEmpty(scenePath))
            {
                sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            }

            var assetPath = AssetDatabase.GetAssetPath(config);
            if (string.IsNullOrEmpty(assetPath))
            {
                return true;
            }

            var actualDirectory = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/') ?? string.Empty;
            var expectedDirectory = GetBakingConfigDirectory(sceneName);
            return string.Equals(
                actualDirectory.TrimEnd('/'),
                expectedDirectory.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        }
#endif

#if UNITY_EDITOR
        private int ScoreSceneMatch(Scene scene, string sceneGuidToMatch, string scenePathToMatch, string assetPath)
        {
            var score = 0;
            if (!string.IsNullOrEmpty(sceneGuid) &&
                string.Equals(sceneGuid, sceneGuidToMatch, StringComparison.OrdinalIgnoreCase))
            {
                score += 1000;
            }

            if (!string.IsNullOrEmpty(scenePath) &&
                string.Equals(scenePath, scenePathToMatch, StringComparison.OrdinalIgnoreCase))
            {
                score += 500;
            }

            if (!string.IsNullOrEmpty(sceneName) &&
                string.Equals(sceneName, scene.name, StringComparison.OrdinalIgnoreCase))
            {
                score += 250;
            }

            var lowerSceneName = (scene.name ?? string.Empty).ToLowerInvariant();
            var lowerAssetPath = (assetPath ?? string.Empty).ToLowerInvariant();
            var lowerAssetName = name.ToLowerInvariant();
            if (!string.IsNullOrEmpty(lowerSceneName) && lowerAssetName.Contains(lowerSceneName))
            {
                score += 100;
            }

            if (!string.IsNullOrEmpty(lowerSceneName) && lowerAssetPath.Contains(lowerSceneName))
            {
                score += 50;
            }

            if (bakedDataAsset != null || HasTimeSliceBakedDataAssets)
            {
                score += 10;
            }

            return score;
        }
#endif

        public static int GetCellSizeInBricks(int simplificationLevel)
        {
            var level = Mathf.Clamp(simplificationLevel, 0, 7);
            var size = 1;
            for (var i = 0; i < level; i++)
            {
                size *= BurtGIVirtualProbePhysicalPool.BrickCellCount;
            }

            return size;
        }

        public static float GetMinBrickSize(float minDistanceBetweenProbes)
        {
            return Mathf.Max(0.01f, minDistanceBetweenProbes * BurtGIVirtualProbePhysicalPool.BrickCellCount);
        }

        public int GetL0L1RxChunkSize(BurtXGIProbeSHBands shBands)
        {
            return shBands.HasL0() ? ChunkProbeCount * 8 : 0;
        }

        public int GetL1ChunkSize(BurtXGIProbeSHBands shBands)
        {
            return shBands.HasL1() ? ChunkProbeCount * 4 : 0;
        }

        public int GetL2TextureChunkSize(BurtXGIProbeSHBands shBands)
        {
            return shBands.HasL2() ? ChunkProbeCount * 4 : 0;
        }

        public int GetSharedDataChunkSize(bool enableSkyVisibility, bool enableSkyShadingDirection)
        {
            var size = 0;
            if (enableSkyVisibility)
            {
                size += ChunkProbeCount * 8;
                if (enableSkyShadingDirection)
                {
                    size += ChunkProbeCount;
                }
            }

            return size;
        }

        public int GetChunkGPUMemory(BurtXGIProbeSHBands shBands, bool enableSkyVisibility, bool enableSkyShadingDirection)
        {
            return GetL0L1RxChunkSize(shBands) +
                GetL1ChunkSize(shBands) * 2 +
                GetL2TextureChunkSize(shBands) * 4 +
                GetSharedDataChunkSize(enableSkyVisibility, enableSkyShadingDirection);
        }

        public static bool IsDayTimeSlice(BurtGIProbeTimeSlice slice)
        {
            return slice == BurtGIProbeTimeSlice.Day;
        }

        public static bool HasValidTimeOfDaySource()
        {
            return BurtGIProbeTimeOfDayController.HasValidTimeOfDaySource();
        }

        public bool SupportsCurrentTimeSliceBake(out string errorMessage)
        {
            return SupportsTimeSliceBake(timeSliceType, out errorMessage);
        }

        public bool SupportsTimeSliceBake(BurtGIProbeTimeSlice slice, out string errorMessage)
        {
            if (!Enum.IsDefined(typeof(BurtGIProbeTimeSlice), slice))
            {
                errorMessage = "Unsupported Burt XGI probe time slice: " + slice;
                return false;
            }

            if (!useTimeSliceData || IsDayTimeSlice(slice) || HasValidTimeOfDaySource())
            {
                errorMessage = string.Empty;
                return true;
            }

            errorMessage = "Current scene has no valid Burt XGI time-of-day source. Only Day time slice can be baked.";
            return false;
        }

#if UNITY_EDITOR
        public bool EnsureSupportedTimeSliceForCurrentScene()
        {
            if (SupportsCurrentTimeSliceBake(out _))
            {
                return false;
            }

            SetActiveTimeSlice(BurtGIProbeTimeSlice.Day);
            EditorUtility.SetDirty(this);
            return true;
        }
#endif

        public void SetActiveTimeSlice(BurtGIProbeTimeSlice slice)
        {
            timeSliceType = slice;
            BurtGIProbeVolume.SetActiveTimeSlice(slice);
        }

        public void SetActiveTimeSlice(int xrenderTimeSliceValue)
        {
            if (!BurtGIProbeTimeSliceUtility.TryParseXRenderValue(xrenderTimeSliceValue, out var slice))
            {
                return;
            }

            SetActiveTimeSlice(slice);
        }

        public bool IsEquivalent(BurtXGIProbeBakingConfig other)
        {
            return other != null &&
                Mathf.Approximately(minDistanceBetweenProbes, other.minDistanceBetweenProbes) &&
                Mathf.Approximately(CellSizeInMeters, other.CellSizeInMeters) &&
                simplificationLevels == other.simplificationLevels;
        }

        public Vector3Int PositionToCell(Vector3 position)
        {
            return Vector3Int.FloorToInt((position - probeOffset) / Mathf.Max(CellSizeInMeters, 0.0001f));
        }

        public void CaptureBakedMetadata(Bounds bounds)
        {
            CaptureBakedMetadata(bounds, PositionToCell(bounds.min), PositionToCell(bounds.max));
        }

        public void CaptureBakedMetadata(Bounds bounds, Vector3Int minCell, Vector3Int maxCell)
        {
            chunkSizeInBricks = BurtGIVirtualProbePhysicalPool.BricksPerChunk;
            globalBounds = bounds;
            minCellPosition = minCell;
            maxCellPosition = maxCell;
            bakedProbeOffset = probeOffset;
            bakedMinDistanceBetweenProbes = minDistanceBetweenProbes;
            bakedSimplificationLevels = simplificationLevels;
            bakedStreamerType = streamerType;
            bakedUseTimeSlice = useTimeSliceData;
            bakedTimeSliceType = timeSliceType;
            bakedTimeSliceMainLightIntensity = 1f;
            bakedSkyVisibility = skyVisibility;
            bakedSkyShadingDirection = skyVisibilityShadingDirection;
        }

        public void CapturePlacement(
            BurtXGIProbePlacedCell[] cells,
            BurtXGIProbePlacedBrick[] bricks,
            Vector3[] probePositions)
        {
            bakedPlacedCells = cells ?? Array.Empty<BurtXGIProbePlacedCell>();
            bakedPlacedBricks = bricks ?? Array.Empty<BurtXGIProbePlacedBrick>();
            bakedProbePositions = probePositions ?? Array.Empty<Vector3>();
            bakedCellCount = bakedPlacedCells.Length;
            bakedBrickCount = bakedPlacedBricks.Length;
            bakedProbeCount = bakedProbePositions.Length;
            bakedVirtualOffsets = Array.Empty<Vector3>();
            bakedVirtualOffsetProbePositions = Array.Empty<Vector3>();
            bakedVirtualOffsetCount = 0;
            bakedVirtualOffsetInvalidCount = 0;
            bakedSkyVisibilityL0L1 = Array.Empty<Vector4>();
            bakedSkyShadingDirectionIndices = Array.Empty<byte>();
            bakedSkyVisibilityCount = 0;
            bakedSkyShadingDirectionCount = 0;
            bakedTimeSliceSH = Array.Empty<BurtXGIProbeBakedSphericalHarmonicsL2>();
            bakedTimeSliceSHCount = 0;
            bakedFinalizedCells = Array.Empty<BurtXGIProbeFinalizedCell>();
            bakedFinalizedCellCount = 0;
            bakedSerializedCellCount = 0;
            bakedSerializedChunkCount = 0;
            bakedDataAsset = null;
            bakedVirtualOffsetApplied = false;
        }

        public void CaptureVirtualOffsets(
            Vector3[] offsets,
            Vector3[] adjustedProbePositions,
            int invalidCount,
            bool applied)
        {
            bakedVirtualOffsets = offsets ?? Array.Empty<Vector3>();
            bakedVirtualOffsetProbePositions = adjustedProbePositions ?? Array.Empty<Vector3>();
            bakedVirtualOffsetCount = bakedVirtualOffsets.Length;
            bakedVirtualOffsetInvalidCount = Mathf.Max(0, invalidCount);
            bakedVirtualOffsetApplied = applied;
        }

        public void CaptureSkyVisibility(
            Vector4[] skyVisibilityL0L1,
            byte[] skyShadingDirectionIndices)
        {
            bakedSkyVisibilityL0L1 = skyVisibilityL0L1 ?? Array.Empty<Vector4>();
            bakedSkyShadingDirectionIndices = skyShadingDirectionIndices ?? Array.Empty<byte>();
            bakedSkyVisibilityCount = bakedSkyVisibilityL0L1.Length;
            bakedSkyShadingDirectionCount = bakedSkyShadingDirectionIndices.Length;
            bakedSkyVisibility = skyVisibility && bakedSkyVisibilityCount > 0;
            bakedSkyShadingDirection = bakedSkyVisibility && skyVisibilityShadingDirection && bakedSkyShadingDirectionCount > 0;
        }

        public void CaptureTimeSliceSH(BurtXGIProbeBakedSphericalHarmonicsL2[] timeSliceSH, float mainLightIntensity = 1f)
        {
            bakedTimeSliceSH = timeSliceSH ?? Array.Empty<BurtXGIProbeBakedSphericalHarmonicsL2>();
            bakedTimeSliceSHCount = bakedTimeSliceSH.Length;
            bakedUseTimeSlice = useTimeSliceData && bakedTimeSliceSHCount > 0;
            bakedTimeSliceType = timeSliceType;
            bakedTimeSliceMainLightIntensity = Mathf.Max(0.0001f, mainLightIntensity);
        }

        public void CaptureFinalizedCells(BurtXGIProbeFinalizedCell[] cells)
        {
            bakedFinalizedCells = cells ?? Array.Empty<BurtXGIProbeFinalizedCell>();
            bakedFinalizedCellCount = bakedFinalizedCells.Length;
        }

        public void CaptureSerializedData(BurtXGIProbeBakedDataAsset asset)
        {
            bakedDataAsset = asset;
            bakedSerializedCellCount = asset != null ? asset.cellCount : 0;
            bakedSerializedChunkCount = asset != null ? asset.chunkCount : 0;
            if (asset != null && asset.hasTimeSliceSH)
            {
                RegisterTimeSliceBakedDataAsset(asset.timeSliceType, asset);
            }
        }

        public bool HasTimeSliceBakedDataAssets
        {
            get
            {
                if (timeSliceBakedDataAssets == null)
                {
                    return false;
                }

                for (var index = 0; index < timeSliceBakedDataAssets.Count; index++)
                {
                    if (timeSliceBakedDataAssets[index]?.asset != null)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool TryGetBakedDataAssetForTimeSlice(BurtGIProbeTimeSlice slice, out BurtXGIProbeBakedDataAsset asset)
        {
            asset = null;
            slice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(slice);
            if (timeSliceBakedDataAssets == null)
            {
                return false;
            }

            for (var index = 0; index < timeSliceBakedDataAssets.Count; index++)
            {
                var entry = timeSliceBakedDataAssets[index];
                if (entry == null || BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice) != slice || entry.asset == null)
                {
                    continue;
                }

                asset = entry.asset;
                return true;
            }

            return false;
        }

        public List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData> GetTimeSliceBakedDataAssets()
        {
            var result = new List<BurtGIVirtualProbeCellStreamer.TimeSliceBakedData>();
            if (timeSliceBakedDataAssets == null)
            {
                return result;
            }

            for (var index = 0; index < timeSliceBakedDataAssets.Count; index++)
            {
                var entry = timeSliceBakedDataAssets[index];
                if (entry?.asset == null)
                {
                    continue;
                }

                result.Add(new BurtGIVirtualProbeCellStreamer.TimeSliceBakedData
                {
                    timeSlice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice),
                    asset = entry.asset
                });
            }

            return result;
        }

        private void RegisterTimeSliceBakedDataAsset(BurtGIProbeTimeSlice slice, BurtXGIProbeBakedDataAsset asset)
        {
            slice = BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(slice);
            timeSliceBakedDataAssets ??= new List<BurtXGIProbeTimeSliceBakedDataAsset>();
            for (var index = 0; index < timeSliceBakedDataAssets.Count; index++)
            {
                var entry = timeSliceBakedDataAssets[index];
                if (entry == null || BurtGIProbeTimeSliceUtility.NormalizeLegacyValue(entry.timeSlice) != slice)
                {
                    continue;
                }

                entry.timeSlice = slice;
                entry.asset = asset;
                return;
            }

            timeSliceBakedDataAssets.Add(new BurtXGIProbeTimeSliceBakedDataAsset
            {
                timeSlice = slice,
                asset = asset
            });
        }

    }
}
