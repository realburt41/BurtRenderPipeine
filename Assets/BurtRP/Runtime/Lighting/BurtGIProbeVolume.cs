using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtGIProbeTimeSlice
    {
        Morning = 0,
        Day = 1,
        Sunset = 2,
        Night = 3
    }

    [DisallowMultipleComponent]
    public sealed class BurtGIProbeVolume : MonoBehaviour
    {
        [Tooltip("Diffuse irradiance in world-space volume coordinates. Supports Texture3D assets and Tex3D RenderTextures.")]
        public Texture irradiance;

        [Tooltip("World-space half extent represented by the irradiance texture.")]
        [Min(0.01f)] public float extent = 12f;

        [Tooltip("Fade width, as a fraction of the volume half extent.")]
        [Range(0.001f, 1f)] public float edgeFade = 0.1f;

        [Tooltip("Diffuse irradiance multiplier.")]
        [Min(0f)] public float intensity = 1f;

        [Tooltip("Higher-priority volumes win before distance is considered.")]
        public int priority;

        [Header("XGI Time Slice")]
        [Tooltip("Restrict this volume to one XGI time slice. Use one volume per baked slice; the active slice is selected through BurtGIProbeVolume.SetActiveTimeSlice.")]
        public bool useTimeSlice;

        public BurtGIProbeTimeSlice timeSlice = BurtGIProbeTimeSlice.Day;

        [Header("XGI Virtual Probe Data")]
        [Tooltip("Use XRender-compatible virtual probe addressing instead of the direct irradiance texture.")]
        public bool useVirtualProbeData;

        [Tooltip("Little-endian uint page table. Each virtual chunk occupies 243 entries.")]
        public TextAsset virtualPageTable;

        [Tooltip("Little-endian uint3 indirection records, stored as 12 bytes per entry.")]
        public TextAsset virtualIndirection;

        [Tooltip("Physical-pool SH texture: RGB L0, alpha L1R.x.")]
        public Texture virtualL0L1Rx;

        [Tooltip("Physical-pool SH texture: RGB L1G.xyz, alpha L1R.y.")]
        public Texture virtualL1GL1Ry;

        [Tooltip("Physical-pool SH texture: RGB L1B.xyz, alpha L1R.z.")]
        public Texture virtualL1BL1Rz;

        [Tooltip("Optional physical-pool L2 SH textures, matching XRender's L2_0 through L2_3 layout.")]
        public Texture virtualL20;
        public Texture virtualL21;
        public Texture virtualL22;
        public Texture virtualL23;

        [Tooltip("Optional physical-pool sky visibility texture: L0/L1 coefficients in XRender layout.")]
        public Texture virtualSkyVisibilityL0L1;

        [Tooltip("Optional physical-pool R8 texture whose values index the 255 XGI precomputed sky shading directions. Value 255 keeps the surface normal.")]
        public Texture virtualSkyShadingDirectionIndices;

        [Tooltip("Optional little-endian float3 array with the 255 XGI precomputed sky shading directions. When omitted, BRP regenerates XRender's default direction table.")]
        public TextAsset virtualSkyShadingDirections;

        [Tooltip("Sky visibility tint and intensity, matching the baked XGI probe settings.")]
        public Color virtualSkyVisibilityTint = Color.white;
        [Min(0f)] public float virtualSkyVisibilityIntensity = 1f;

        [Tooltip("Physical pool dimensions in texels.")]
        public Vector3Int virtualPhysicalPoolDimensions = new Vector3Int(4, 4, 4);

        [Tooltip("Dimensions of the indirection entry grid.")]
        public Vector3Int virtualIndirectionDimensions = Vector3Int.one;

        [Tooltip("Inclusive bounds of the loaded entry region in world entry coordinates.")]
        public Vector3Int virtualMinLoadedEntry;
        public Vector3Int virtualMaxLoadedEntry;

        [Tooltip("World entry coordinate represented by indirection buffer element zero.")]
        public Vector3Int virtualMinEntryIndex;

        [Tooltip("World size of one indirection entry and the smallest brick size.")]
        [Min(0.0001f)] public float virtualIndirectionEntrySize = 1f;
        [Min(0.0001f)] public float virtualMinBrickSize = 1f;

        [Tooltip("World-space offset, normal bias, and view bias matching the baked XGI data.")]
        public Vector3 virtualPositionOffset;
        public float virtualNormalBias;
        public float virtualViewBias;

        private static readonly List<BurtGIProbeVolume> ActiveVolumes = new List<BurtGIProbeVolume>();
        private static BurtGIProbeTimeSlice activeTimeSlice = BurtGIProbeTimeSlice.Day;

        private GraphicsBuffer pageTableBuffer;
        private GraphicsBuffer indirectionBuffer;
        private GraphicsBuffer skyShadingDirectionBuffer;
        private GraphicsBuffer runtimePageTableBuffer;
        private GraphicsBuffer runtimeIndirectionBuffer;
        private bool ownsRuntimeVirtualBuffers;
        private TextAsset pageTableSource;
        private TextAsset indirectionSource;
        private TextAsset skyShadingDirectionSource;

        internal bool IsReady => isActiveAndEnabled && extent > 0.01f && intensity > 0f &&
            (IsVirtualReady || IsDirectIrradianceReady);

        internal bool IsActiveForCurrentTimeSlice => !useTimeSlice || timeSlice == activeTimeSlice;

        public static BurtGIProbeTimeSlice ActiveTimeSlice => activeTimeSlice;

        public static void SetActiveTimeSlice(BurtGIProbeTimeSlice slice)
        {
            activeTimeSlice = slice;
        }

        internal bool IsDirectIrradianceReady => irradiance != null && irradiance.dimension == TextureDimension.Tex3D;

        internal bool IsVirtualReady => useVirtualProbeData &&
            (HasRuntimeVirtualBuffers || (virtualPageTable != null && virtualIndirection != null)) &&
            IsTexture3D(virtualL0L1Rx) && IsTexture3D(virtualL1GL1Ry) && IsTexture3D(virtualL1BL1Rz) &&
            virtualPhysicalPoolDimensions.x > 0 && virtualPhysicalPoolDimensions.y > 0 && virtualPhysicalPoolDimensions.z > 0 &&
            virtualIndirectionDimensions.x > 0 && virtualIndirectionDimensions.y > 0 && virtualIndirectionDimensions.z > 0 &&
            virtualIndirectionEntrySize > 0.0001f && virtualMinBrickSize > 0.0001f;

        internal bool HasVirtualL2 => IsTexture3D(virtualL20) && IsTexture3D(virtualL21) && IsTexture3D(virtualL22) && IsTexture3D(virtualL23);

        internal bool HasVirtualSkyVisibility => IsTexture3D(virtualSkyVisibilityL0L1) && virtualSkyVisibilityIntensity > 0f;

        internal bool HasVirtualSkyShadingDirection => HasVirtualSkyVisibility &&
            IsTexture3D(virtualSkyShadingDirectionIndices) &&
            skyShadingDirectionBuffer != null && skyShadingDirectionBuffer.IsValid();

        internal GraphicsBuffer PageTableBuffer => HasRuntimeVirtualBuffers ? runtimePageTableBuffer : pageTableBuffer;
        internal GraphicsBuffer IndirectionBuffer => HasRuntimeVirtualBuffers ? runtimeIndirectionBuffer : indirectionBuffer;

        private bool HasRuntimeVirtualBuffers => runtimePageTableBuffer != null && runtimePageTableBuffer.IsValid() &&
            runtimeIndirectionBuffer != null && runtimeIndirectionBuffer.IsValid();

        internal Vector4 CenterExtent
        {
            get
            {
                var center = transform.position;
                return new Vector4(center.x, center.y, center.z, extent);
            }
        }

        private void OnEnable()
        {
            if (!ActiveVolumes.Contains(this))
            {
                ActiveVolumes.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveVolumes.Remove(this);
            ReleaseVirtualBuffers();
        }

        private void OnDestroy()
        {
            ReleaseVirtualBuffers();
        }

        internal static bool TryGetBestForCamera(Camera camera, out BurtGIProbeVolume volume)
        {
            volume = null;
            if (camera == null)
            {
                return false;
            }

            var cameraPosition = camera.transform.position;
            var bestPriority = int.MinValue;
            var bestDistanceSq = float.PositiveInfinity;
            for (var index = ActiveVolumes.Count - 1; index >= 0; --index)
            {
                var candidate = ActiveVolumes[index];
                if (candidate == null)
                {
                    ActiveVolumes.RemoveAt(index);
                    continue;
                }

                if (!candidate.IsReady || !candidate.IsActiveForCurrentTimeSlice || !Contains(candidate, cameraPosition))
                {
                    continue;
                }

                var distanceSq = (candidate.transform.position - cameraPosition).sqrMagnitude;
                if (candidate.priority < bestPriority || (candidate.priority == bestPriority && distanceSq >= bestDistanceSq))
                {
                    continue;
                }

                volume = candidate;
                bestPriority = candidate.priority;
                bestDistanceSq = distanceSq;
            }

            return volume != null;
        }

        private static bool Contains(BurtGIProbeVolume volume, Vector3 position)
        {
            var delta = position - volume.transform.position;
            return Mathf.Abs(delta.x) <= volume.extent &&
                Mathf.Abs(delta.y) <= volume.extent &&
                Mathf.Abs(delta.z) <= volume.extent;
        }

        internal bool TryEnsureVirtualBuffers()
        {
            if (!IsVirtualReady)
            {
                return false;
            }

            if (HasRuntimeVirtualBuffers)
            {
                return TryEnsureVirtualSkyShadingDirectionBuffer();
            }

            var pageTableBytes = virtualPageTable.bytes;
            var indirectionBytes = virtualIndirection.bytes;
            if (pageTableBytes.Length < sizeof(uint) || pageTableBytes.Length % sizeof(uint) != 0 ||
                indirectionBytes.Length < sizeof(uint) * 3 || indirectionBytes.Length % (sizeof(uint) * 3) != 0)
            {
                return false;
            }

            if (pageTableBuffer != null && pageTableBuffer.IsValid() &&
                indirectionBuffer != null && indirectionBuffer.IsValid() &&
                pageTableSource == virtualPageTable && indirectionSource == virtualIndirection)
            {
                return TryEnsureVirtualSkyShadingDirectionBuffer();
            }

            ReleaseSerializedVirtualBuffers();
            var pageTableData = new uint[pageTableBytes.Length / sizeof(uint)];
            Buffer.BlockCopy(pageTableBytes, 0, pageTableData, 0, pageTableBytes.Length);

            var indirectionWords = new uint[indirectionBytes.Length / sizeof(uint)];
            Buffer.BlockCopy(indirectionBytes, 0, indirectionWords, 0, indirectionBytes.Length);
            var indirectionData = new Vector3Int[indirectionWords.Length / 3];
            var requiredPageTableEntryCount = 0L;
            for (var index = 0; index < indirectionData.Length; ++index)
            {
                var wordOffset = index * 3;
                var metadataX = indirectionWords[wordOffset];
                indirectionData[index] = new Vector3Int(
                    unchecked((int)metadataX),
                    unchecked((int)indirectionWords[wordOffset + 1]),
                    unchecked((int)indirectionWords[wordOffset + 2]));

                if (metadataX != uint.MaxValue)
                {
                    requiredPageTableEntryCount = Math.Max(requiredPageTableEntryCount, ((long)(metadataX & 0x1fffffffu) + 1L) * 243L);
                }
            }

            var requiredIndirectionEntryCount = (long)virtualIndirectionDimensions.x * virtualIndirectionDimensions.y * virtualIndirectionDimensions.z;
            if (indirectionData.Length < requiredIndirectionEntryCount || pageTableData.Length < requiredPageTableEntryCount)
            {
                return false;
            }

            pageTableBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pageTableData.Length, sizeof(uint));
            pageTableBuffer.name = name + " BurtGI Virtual Page Table";
            pageTableBuffer.SetData(pageTableData);

            indirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indirectionData.Length, sizeof(uint) * 3);
            indirectionBuffer.name = name + " BurtGI Virtual Indirection";
            indirectionBuffer.SetData(indirectionData);
            pageTableSource = virtualPageTable;
            indirectionSource = virtualIndirection;
            return TryEnsureVirtualSkyShadingDirectionBuffer();
        }

        public void SetVirtualProbeRuntimeBuffers(GraphicsBuffer pageTable, GraphicsBuffer indirection)
        {
            if (!AreValidVirtualRuntimeBuffers(pageTable, indirection))
            {
                ClearVirtualProbeRuntimeBuffers();
                return;
            }

            ReleaseSerializedVirtualBuffers();
            ReleaseOwnedRuntimeVirtualBuffers();
            runtimePageTableBuffer = pageTable;
            runtimeIndirectionBuffer = indirection;
        }

        public bool TryAllocateVirtualProbeRuntimeBuffers(int pageTableEntryCount, int indirectionEntryCount)
        {
            if (pageTableEntryCount <= 0 || indirectionEntryCount <= 0)
            {
                return false;
            }

            ReleaseSerializedVirtualBuffers();
            ReleaseOwnedRuntimeVirtualBuffers();
            runtimePageTableBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, pageTableEntryCount, sizeof(uint));
            runtimeIndirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, indirectionEntryCount, sizeof(uint) * 3);
            runtimePageTableBuffer.name = name + " BurtGI Runtime Page Table";
            runtimeIndirectionBuffer.name = name + " BurtGI Runtime Indirection";
            ownsRuntimeVirtualBuffers = true;
            return true;
        }

        public bool TryUpdateVirtualPageTable(uint[] entries, int sourceIndex, int destinationIndex, int count)
        {
            var buffer = PageTableBuffer;
            if (!TryValidateBufferUpdate(buffer, sizeof(uint), entries, sourceIndex, destinationIndex, count))
            {
                return false;
            }

            buffer.SetData(entries, sourceIndex, destinationIndex, count);
            return true;
        }

        public bool TryUpdateVirtualIndirection(Vector3Int[] entries, int sourceIndex, int destinationIndex, int count)
        {
            var buffer = IndirectionBuffer;
            if (!TryValidateBufferUpdate(buffer, sizeof(uint) * 3, entries, sourceIndex, destinationIndex, count))
            {
                return false;
            }

            buffer.SetData(entries, sourceIndex, destinationIndex, count);
            return true;
        }

        internal int VirtualPageTableEntryCount => PageTableBuffer != null && PageTableBuffer.IsValid() ? PageTableBuffer.count : 0;

        internal int VirtualIndirectionEntryCount => IndirectionBuffer != null && IndirectionBuffer.IsValid() ? IndirectionBuffer.count : 0;

        public void ClearVirtualProbeRuntimeBuffers()
        {
            ReleaseOwnedRuntimeVirtualBuffers();
            runtimePageTableBuffer = null;
            runtimeIndirectionBuffer = null;
        }

        private void ReleaseVirtualBuffers()
        {
            ClearVirtualProbeRuntimeBuffers();
            ReleaseSerializedVirtualBuffers();
        }

        private void ReleaseSerializedVirtualBuffers()
        {
            pageTableBuffer?.Release();
            indirectionBuffer?.Release();
            skyShadingDirectionBuffer?.Release();
            pageTableBuffer = null;
            indirectionBuffer = null;
            skyShadingDirectionBuffer = null;
            pageTableSource = null;
            indirectionSource = null;
            skyShadingDirectionSource = null;
        }

        private void ReleaseOwnedRuntimeVirtualBuffers()
        {
            if (!ownsRuntimeVirtualBuffers)
            {
                return;
            }

            runtimePageTableBuffer?.Release();
            runtimeIndirectionBuffer?.Release();
            ownsRuntimeVirtualBuffers = false;
        }

        private bool TryEnsureVirtualSkyShadingDirectionBuffer()
        {
            if (!IsTexture3D(virtualSkyShadingDirectionIndices))
            {
                ReleaseVirtualSkyShadingDirectionBuffer();
                return true;
            }

            const int directionStride = sizeof(float) * 3;
            const int requiredDirectionCount = 255;

            if (skyShadingDirectionBuffer != null && skyShadingDirectionBuffer.IsValid() &&
                skyShadingDirectionSource == virtualSkyShadingDirections)
            {
                return true;
            }

            ReleaseVirtualSkyShadingDirectionBuffer();
            Vector3[] directions;
            if (virtualSkyShadingDirections == null)
            {
                directions = CreateDefaultXGISkyShadingDirections(requiredDirectionCount);
            }
            else
            {
                var directionBytes = virtualSkyShadingDirections.bytes;
                if (directionBytes.Length < directionStride * requiredDirectionCount || directionBytes.Length % directionStride != 0)
                {
                    return true;
                }

                var directionValues = new float[directionBytes.Length / sizeof(float)];
                Buffer.BlockCopy(directionBytes, 0, directionValues, 0, directionBytes.Length);
                directions = new Vector3[directionValues.Length / 3];
                for (var index = 0; index < directions.Length; ++index)
                {
                    var valueOffset = index * 3;
                    directions[index] = new Vector3(
                        directionValues[valueOffset],
                        directionValues[valueOffset + 1],
                        directionValues[valueOffset + 2]);
                }
            }

            skyShadingDirectionBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, directions.Length, directionStride);
            skyShadingDirectionBuffer.name = name + " BurtGI Virtual Sky Shading Directions";
            skyShadingDirectionBuffer.SetData(directions);
            skyShadingDirectionSource = virtualSkyShadingDirections;
            return true;
        }

        private static Vector3[] CreateDefaultXGISkyShadingDirections(int directionCount)
        {
            var directions = new Vector3[directionCount];
            var sqrtDirectionCount = Mathf.Sqrt(directionCount);
            var phi = 0f;
            for (var index = 0; index < directionCount; ++index)
            {
                var h = -1f + 2f * index / (directionCount - 1f);
                var theta = Mathf.Acos(h);
                if (index == 0 || index == directionCount - 1)
                {
                    phi = 0f;
                }
                else
                {
                    phi += 3.6f / sqrtDirectionCount / Mathf.Sqrt(1f - h * h);
                }

                directions[index] = new Vector3(
                    Mathf.Sin(theta) * Mathf.Cos(phi),
                    Mathf.Sin(theta) * Mathf.Sin(phi),
                    Mathf.Cos(theta));
            }

            return directions;
        }

        private void ReleaseVirtualSkyShadingDirectionBuffer()
        {
            skyShadingDirectionBuffer?.Release();
            skyShadingDirectionBuffer = null;
            skyShadingDirectionSource = null;
        }

        internal GraphicsBuffer SkyShadingDirectionBuffer => skyShadingDirectionBuffer;

        private static bool IsTexture3D(Texture texture)
        {
            return texture != null && texture.dimension == TextureDimension.Tex3D;
        }

        private static bool AreValidVirtualRuntimeBuffers(GraphicsBuffer pageTable, GraphicsBuffer indirection)
        {
            return pageTable != null && pageTable.IsValid() && pageTable.stride == sizeof(uint) &&
                indirection != null && indirection.IsValid() && indirection.stride == sizeof(uint) * 3;
        }

        private static bool TryValidateBufferUpdate<T>(GraphicsBuffer buffer, int requiredStride, T[] source, int sourceIndex, int destinationIndex, int count) where T : struct
        {
            return buffer != null && buffer.IsValid() && buffer.stride == requiredStride &&
                source != null && sourceIndex >= 0 && destinationIndex >= 0 && count >= 0 &&
                sourceIndex <= source.Length - count && destinationIndex <= buffer.count - count;
        }
    }

    internal static class BurtGIProbeVolumeUtility
    {
        private static readonly int IrradianceTextureId = Shader.PropertyToID("_BurtGIProbeVolumeIrradianceTexture");
        private static readonly int CenterExtentId = Shader.PropertyToID("_BurtGIProbeVolumeCenterExtent");
        private static readonly int ParamsId = Shader.PropertyToID("_BurtGIProbeVolumeParams");
        private static readonly int VirtualPageTableId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPageTable");
        private static readonly int VirtualIndirectionId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualIndirection");
        private static readonly int VirtualL0L1RxId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL0L1Rx");
        private static readonly int VirtualL1GL1RyId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL1GL1Ry");
        private static readonly int VirtualL1BL1RzId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL1BL1Rz");
        private static readonly int VirtualL20Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL20");
        private static readonly int VirtualL21Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL21");
        private static readonly int VirtualL22Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL22");
        private static readonly int VirtualL23Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualL23");
        private static readonly int VirtualSkyVisibilityL0L1Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyVisibilityL0L1");
        private static readonly int VirtualSkyShadingDirectionIndicesId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyShadingDirectionIndices");
        private static readonly int VirtualSkyShadingDirectionsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyShadingDirections");
        private static readonly int VirtualPosOffsetMinBrickSizeId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPosOffsetMinBrickSize");
        private static readonly int VirtualIndirectionDimensionsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualIndirectionDimensions");
        private static readonly int VirtualMinLoadedEntryId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualMinLoadedEntry");
        private static readonly int VirtualMaxLoadedEntryId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualMaxLoadedEntry");
        private static readonly int VirtualMinEntryIndexEntrySizeId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualMinEntryIndexEntrySize");
        private static readonly int VirtualPhysicalPoolDimensionsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPhysicalPoolDimensions");
        private static readonly int VirtualPhysicalPoolDimensionsRcpId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualPhysicalPoolDimensionsRcp");
        private static readonly int VirtualBiasL2Id = Shader.PropertyToID("_BurtGIProbeVolumeVirtualBiasL2");
        private static readonly int VirtualSkyVisibilityParamsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyVisibilityParams");
        private static readonly int VirtualSkyShadingDirectionEnabledId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualSkyShadingDirectionEnabled");
        private static readonly int VirtualBufferCountsId = Shader.PropertyToID("_BurtGIProbeVolumeVirtualBufferCounts");

        public static void Upload(CommandBuffer cmd, Camera camera)
        {
            if (cmd == null)
            {
                return;
            }

            if (BurtGIProbeVolume.TryGetBestForCamera(camera, out var volume))
            {
                cmd.SetGlobalVector(CenterExtentId, volume.CenterExtent);
                if (volume.TryEnsureVirtualBuffers())
                {
                    UploadVirtualProbeData(cmd, volume);
                    return;
                }

                if (volume.IsDirectIrradianceReady)
                {
                    cmd.SetGlobalTexture(IrradianceTextureId, volume.irradiance);
                    cmd.SetGlobalVector(ParamsId, new Vector4(1f, volume.intensity, 1f / Mathf.Max(volume.edgeFade, 0.001f), 1f));
                    return;
                }
            }

            cmd.SetGlobalTexture(IrradianceTextureId, BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalVector(CenterExtentId, Vector4.zero);
            cmd.SetGlobalVector(ParamsId, Vector4.zero);
        }

        private static void UploadVirtualProbeData(CommandBuffer cmd, BurtGIProbeVolume volume)
        {
            var pool = volume.virtualPhysicalPoolDimensions;
            var reciprocalPool = new Vector4(1f / pool.x, 1f / pool.y, 1f / pool.z, 1f / (pool.x * pool.y));
            cmd.SetGlobalTexture(IrradianceTextureId, BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalBuffer(VirtualPageTableId, volume.PageTableBuffer);
            cmd.SetGlobalBuffer(VirtualIndirectionId, volume.IndirectionBuffer);
            cmd.SetGlobalTexture(VirtualL0L1RxId, volume.virtualL0L1Rx);
            cmd.SetGlobalTexture(VirtualL1GL1RyId, volume.virtualL1GL1Ry);
            cmd.SetGlobalTexture(VirtualL1BL1RzId, volume.virtualL1BL1Rz);
            cmd.SetGlobalTexture(VirtualL20Id, volume.HasVirtualL2 ? volume.virtualL20 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL21Id, volume.HasVirtualL2 ? volume.virtualL21 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL22Id, volume.HasVirtualL2 ? volume.virtualL22 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualL23Id, volume.HasVirtualL2 ? volume.virtualL23 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualSkyVisibilityL0L1Id, volume.HasVirtualSkyVisibility ? volume.virtualSkyVisibilityL0L1 : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            cmd.SetGlobalTexture(VirtualSkyShadingDirectionIndicesId, volume.HasVirtualSkyShadingDirection ? volume.virtualSkyShadingDirectionIndices : BurtGITranslucencyVolumeFallbackUtility.BlackVolumeTexture);
            if (volume.HasVirtualSkyShadingDirection)
            {
                cmd.SetGlobalBuffer(VirtualSkyShadingDirectionsId, volume.SkyShadingDirectionBuffer);
            }
            cmd.SetGlobalVector(VirtualPosOffsetMinBrickSizeId, new Vector4(volume.virtualPositionOffset.x, volume.virtualPositionOffset.y, volume.virtualPositionOffset.z, volume.virtualMinBrickSize));
            cmd.SetGlobalVector(VirtualIndirectionDimensionsId, new Vector4(volume.virtualIndirectionDimensions.x, volume.virtualIndirectionDimensions.y, volume.virtualIndirectionDimensions.z, 0f));
            cmd.SetGlobalVector(VirtualMinLoadedEntryId, new Vector4(volume.virtualMinLoadedEntry.x, volume.virtualMinLoadedEntry.y, volume.virtualMinLoadedEntry.z, 0f));
            cmd.SetGlobalVector(VirtualMaxLoadedEntryId, new Vector4(volume.virtualMaxLoadedEntry.x, volume.virtualMaxLoadedEntry.y, volume.virtualMaxLoadedEntry.z, 0f));
            cmd.SetGlobalVector(VirtualMinEntryIndexEntrySizeId, new Vector4(volume.virtualMinEntryIndex.x, volume.virtualMinEntryIndex.y, volume.virtualMinEntryIndex.z, volume.virtualIndirectionEntrySize));
            cmd.SetGlobalVector(VirtualPhysicalPoolDimensionsId, new Vector4(pool.x, pool.y, pool.z, 0f));
            cmd.SetGlobalVector(VirtualPhysicalPoolDimensionsRcpId, reciprocalPool);
            cmd.SetGlobalVector(VirtualBiasL2Id, new Vector4(volume.virtualNormalBias, volume.virtualViewBias, volume.HasVirtualL2 ? 1f : 0f, volume.HasVirtualSkyVisibility ? 1f : 0f));
            cmd.SetGlobalVector(VirtualSkyVisibilityParamsId, new Vector4(volume.virtualSkyVisibilityTint.r, volume.virtualSkyVisibilityTint.g, volume.virtualSkyVisibilityTint.b, volume.virtualSkyVisibilityIntensity));
            cmd.SetGlobalFloat(VirtualSkyShadingDirectionEnabledId, volume.HasVirtualSkyShadingDirection ? 1f : 0f);
            cmd.SetGlobalVector(VirtualBufferCountsId, new Vector4(volume.VirtualIndirectionEntryCount, volume.VirtualPageTableEntryCount, 0f, 0f));
            cmd.SetGlobalVector(ParamsId, new Vector4(1f, volume.intensity, 1f / Mathf.Max(volume.edgeFade, 0.001f), 2f));
        }
    }
}
