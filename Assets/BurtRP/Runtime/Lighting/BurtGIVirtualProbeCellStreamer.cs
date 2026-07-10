using System;
using System.Collections.Generic;
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

        public BurtGIProbeVolume probeVolume;
        public BurtGIVirtualProbePhysicalPool physicalPool;
        public bool automaticStreaming = true;
        [Min(1)] public int maxCellsToLoadPerFrame = 1;
        [Min(1)] public int runtimePageTableEntryCount = 243;
        [Min(1)] public int runtimeIndirectionEntryCount = 1;
        public List<Cell> cells = new List<Cell>();

        private readonly HashSet<int> loadedCells = new HashSet<int>();
        private readonly Dictionary<int, int> loadedChunkOwners = new Dictionary<int, int>();
        private readonly Dictionary<BinarySlice, byte[]> resolvedSlices = new Dictionary<BinarySlice, byte[]>();
        private static readonly List<BurtGIVirtualProbeCellStreamer> ActiveStreamers = new List<BurtGIVirtualProbeCellStreamer>();
        private bool initialized;

        internal int LoadedCellCount => loadedCells.Count;
        internal int ConfiguredCellCount => cells != null ? cells.Count : 0;
        internal bool IsInitialized => initialized;

        private void OnEnable()
        {
            if (!ActiveStreamers.Contains(this)) ActiveStreamers.Add(this);
        }

        private void OnDisable()
        {
            ActiveStreamers.Remove(this);
            UnloadAllCells();
            loadedChunkOwners.Clear();
            resolvedSlices.Clear();
            initialized = false;
            probeVolume?.ClearVirtualProbeRuntimeBuffers();
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

                if (streamer.probeVolume == null || !streamer.probeVolume.IsActiveForCurrentTimeSlice)
                {
                    continue;
                }

                streamer.UpdateStreamingForCamera(camera);
            }
        }

        internal static bool TryGetForProbeVolume(BurtGIProbeVolume volume, out BurtGIVirtualProbeCellStreamer streamer)
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

        public bool InitializeStreaming()
        {
            if (probeVolume == null || physicalPool == null || physicalPool.probeVolume != probeVolume)
            {
                return false;
            }

            if (loadedCells.Count > 0)
            {
                UnloadAllCells();
            }

            if (!physicalPool.IsInitialized && !physicalPool.InitializePool())
            {
                return false;
            }

            var pageTableEntryCount = ResolveRuntimePageTableEntryCount();
            var indirectionEntryCount = ResolveRuntimeIndirectionEntryCount();
            if (!probeVolume.TryAllocateVirtualProbeRuntimeBuffers(pageTableEntryCount, indirectionEntryCount))
            {
                return false;
            }

            var emptyPageTable = new uint[pageTableEntryCount];
            for (var index = 0; index < emptyPageTable.Length; ++index) emptyPageTable[index] = uint.MaxValue;
            var emptyIndirection = new Vector3Int[indirectionEntryCount];
            for (var index = 0; index < emptyIndirection.Length; ++index) emptyIndirection[index] = new Vector3Int(-1, 0, 0);
            loadedCells.Clear();
            loadedChunkOwners.Clear();
            initialized = probeVolume.TryUpdateVirtualPageTable(emptyPageTable, 0, 0, emptyPageTable.Length) &&
                probeVolume.TryUpdateVirtualIndirection(emptyIndirection, 0, 0, emptyIndirection.Length);
            return initialized;
        }

        private int ResolveRuntimePageTableEntryCount()
        {
            var entryCount = Mathf.Max(1, runtimePageTableEntryCount);
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

            return entryCount;
        }

        private int ResolveRuntimeIndirectionEntryCount()
        {
            var entryCount = Mathf.Max(1, runtimeIndirectionEntryCount);
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

            return entryCount;
        }

        public bool TryLoadCell(int cellIndex)
        {
            var cell = FindCell(cellIndex);
            if (cell == null || loadedCells.Contains(cellIndex) || cell.chunks == null || cell.chunks.Count == 0)
            {
                return false;
            }

            var pageTable = GetPageTableEntries(cell);
            var indirection = GetIndirectionEntries(cell);
            if (!CanLoadCell(cell, pageTable.Length, indirection.Length))
            {
                return false;
            }

            var uploadedChunks = new List<int>(cell.chunks.Count);
            foreach (var chunk in cell.chunks)
            {
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
                    ResolveBytes(chunk.skyShadingDirectionIndices, chunk.skyShadingDirectionIndicesSlice)))
                {
                    foreach (var uploadedChunk in uploadedChunks) physicalPool.TryClearChunk(uploadedChunk);
                    return false;
                }
                uploadedChunks.Add(chunk.physicalChunkIndex);
            }

            if (!probeVolume.TryUpdateVirtualPageTable(pageTable, 0, cell.pageTableDestinationIndex, pageTable.Length))
            {
                foreach (var uploadedChunk in uploadedChunks) physicalPool.TryClearChunk(uploadedChunk);
                return false;
            }

            if (!probeVolume.TryUpdateVirtualIndirection(indirection, 0, cell.indirectionDestinationIndex, indirection.Length))
            {
                Array.Fill(pageTable, uint.MaxValue);
                probeVolume.TryUpdateVirtualPageTable(pageTable, 0, cell.pageTableDestinationIndex, pageTable.Length);
                foreach (var uploadedChunk in uploadedChunks) physicalPool.TryClearChunk(uploadedChunk);
                return false;
            }

            loadedCells.Add(cellIndex);
            foreach (var chunk in cell.chunks)
            {
                loadedChunkOwners.Add(chunk.physicalChunkIndex, cellIndex);
            }
            return true;
        }

        public bool TryUnloadCell(int cellIndex)
        {
            var cell = FindCell(cellIndex);
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
                succeeded &= chunk != null && physicalPool.TryClearChunk(chunk.physicalChunkIndex);
                if (chunk != null) loadedChunkOwners.Remove(chunk.physicalChunkIndex);
            }
            return succeeded;
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
                if (chunk == null || !cellChunkIndices.Add(chunk.physicalChunkIndex) || loadedChunkOwners.ContainsKey(chunk.physicalChunkIndex))
                {
                    return false;
                }
            }

            return true;
        }

        private bool HasLoadedRangeOverlap(Cell candidate, int candidatePageTableEntryCount, int candidateIndirectionEntryCount)
        {
            foreach (var loadedCellIndex in loadedCells)
            {
                var loadedCell = FindCell(loadedCellIndex);
                if (loadedCell == null) continue;
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

            return false;
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
            if (loadedCells.Count > 0)
            {
                UnloadAllCells();
            }

            resolvedSlices.Clear();
            initialized = false;
        }

        private static bool IsValidRange(int start, int count, int capacity)
        {
            return start >= 0 && count >= 0 && start <= capacity && count <= capacity - start;
        }

        private static bool RangesOverlap(int firstStart, int firstCount, int secondStart, int secondCount)
        {
            return firstCount > 0 && secondCount > 0 && firstStart < secondStart + secondCount && secondStart < firstStart + firstCount;
        }

        private Cell FindCell(int cellIndex)
        {
            return cells.Find(cell => cell != null && cell.index == cellIndex);
        }

        private void UpdateStreamingForCamera(Camera camera)
        {
            if (!automaticStreaming)
            {
                return;
            }

            if (!initialized && !InitializeStreaming())
            {
                return;
            }

            var desiredCells = new HashSet<int>();
            var cellsToLoad = new List<Cell>();
            var cameraPosition = camera.transform.position;
            var cameraForward = camera.transform.forward;
            foreach (var cell in cells)
            {
                if (cell == null || cell.loadDistance <= 0f) continue;
                if ((cell.worldPosition - cameraPosition).sqrMagnitude <= cell.loadDistance * cell.loadDistance)
                {
                    desiredCells.Add(cell.index);
                    if (!loadedCells.Contains(cell.index)) cellsToLoad.Add(cell);
                }
            }

            var unloadCells = new List<int>();
            foreach (var loadedCell in loadedCells)
            {
                if (!desiredCells.Contains(loadedCell)) unloadCells.Add(loadedCell);
            }
            foreach (var unloadCell in unloadCells) TryUnloadCell(unloadCell);

            cellsToLoad.Sort((left, right) => CompareStreamingPriority(left, right, cameraPosition, cameraForward));
            var loadCount = Mathf.Min(Mathf.Max(1, maxCellsToLoadPerFrame), cellsToLoad.Count);
            for (var index = 0; index < loadCount; ++index)
            {
                TryLoadCell(cellsToLoad[index].index);
            }
        }

        private static int CompareStreamingPriority(Cell left, Cell right, Vector3 cameraPosition, Vector3 cameraForward)
        {
            var leftScore = GetStreamingScore(left.worldPosition, cameraPosition, cameraForward);
            var rightScore = GetStreamingScore(right.worldPosition, cameraPosition, cameraForward);
            var scoreComparison = leftScore.CompareTo(rightScore);
            return scoreComparison != 0 ? scoreComparison : left.index.CompareTo(right.index);
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
        private static readonly Type DecompressorType = Type.GetType("ZstdSharp.Decompressor, ZstdSharp");
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
    }
}
