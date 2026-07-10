using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    [DisallowMultipleComponent]
    public sealed class BurtGIVirtualProbePhysicalPool : MonoBehaviour
    {
        public const int BrickCellCount = 3;
        public const int BrickProbeCountPerDimension = BrickCellCount + 1;
        public const int BricksPerChunk = 128;
        public const int ChunkWidth = BricksPerChunk * BrickProbeCountPerDimension;
        public const int ChunkHeight = BrickProbeCountPerDimension;
        public const int ChunkDepth = BrickProbeCountPerDimension;
        public const int ChunkProbeCount = ChunkWidth * ChunkHeight * ChunkDepth;

        private const string UploadComputeResourcePath = "BurtGIProbePhysicalPoolUpload";
        private const int UploadThreadCountX = 8;
        private const int UploadThreadCountY = 4;
        private const int UploadThreadCountZ = 4;

        [Tooltip("Virtual probe volume that samples this physical pool.")]
        public BurtGIProbeVolume probeVolume;

        [Tooltip("Number of XGI physical chunks reserved in each dimension.")]
        public Vector3Int chunkDimensions = Vector3Int.one;

        [Tooltip("Allocate optional sky-visibility and sky-shading-direction textures.")]
        public bool allocateSkyVisibility = true;
        public bool allocateSkyShadingDirection = true;
        [Tooltip("Allocate XRender-compatible L2 SH coefficient textures.")]
        public bool allocateL2 = true;

        private RenderTexture l0L1Rx;
        private RenderTexture l1GL1Ry;
        private RenderTexture l1BL1Rz;
        private RenderTexture l20;
        private RenderTexture l21;
        private RenderTexture l22;
        private RenderTexture l23;
        private RenderTexture skyVisibilityL0L1;
        private RenderTexture skyShadingDirectionIndices;
        private GraphicsBuffer uploadBuffer;
        private uint[] uploadWords;
        private ComputeShader uploadCompute;
        private int uploadHalfKernel = -1;
        private int uploadRgba8Kernel = -1;
        private int uploadR8Kernel = -1;
        private int clearRgbaKernel = -1;
        private int clearR8Kernel = -1;
        private readonly HashSet<int> l2LoadedChunks = new HashSet<int>();

        private static readonly int UploadSourceId = Shader.PropertyToID("_BurtGIProbeUploadSource");
        private static readonly int UploadOriginId = Shader.PropertyToID("_BurtGIProbeUploadOrigin");
        private static readonly int UploadPoolDimensionsId = Shader.PropertyToID("_BurtGIProbeUploadPoolDimensions");
        private static readonly int UploadRgbaTextureId = Shader.PropertyToID("_BurtGIProbeUploadRgbaTexture");
        private static readonly int UploadR8TextureId = Shader.PropertyToID("_BurtGIProbeUploadR8Texture");

        public Vector3Int PhysicalPoolDimensions => new Vector3Int(
            chunkDimensions.x * ChunkWidth,
            chunkDimensions.y * ChunkHeight,
            chunkDimensions.z * ChunkDepth);

        public int ChunkCapacity => chunkDimensions.x * chunkDimensions.y * chunkDimensions.z;

        public bool IsInitialized => l0L1Rx != null && l1GL1Ry != null && l1BL1Rz != null;

        private void OnDisable() => ReleasePool();

        private void OnDestroy() => ReleasePool();

        public bool InitializePool()
        {
            if (probeVolume == null || chunkDimensions.x <= 0 || chunkDimensions.y <= 0 || chunkDimensions.z <= 0 ||
                !SystemInfo.supports3DTextures || !SystemInfo.supportsComputeShaders || !TryInitializeUploadCompute())
            {
                return false;
            }

            var dimensions = PhysicalPoolDimensions;
            if (dimensions.x > SystemInfo.maxTexture3DSize || dimensions.y > SystemInfo.maxTexture3DSize || dimensions.z > SystemInfo.maxTexture3DSize)
            {
                return false;
            }

            ReleasePool();
            l0L1Rx = CreateTexture(dimensions, GraphicsFormat.R16G16B16A16_SFloat, "BurtGI XGI Physical L0 L1Rx");
            l1GL1Ry = CreateTexture(dimensions, GraphicsFormat.R8G8B8A8_UNorm, "BurtGI XGI Physical L1G L1Ry");
            l1BL1Rz = CreateTexture(dimensions, GraphicsFormat.R8G8B8A8_UNorm, "BurtGI XGI Physical L1B L1Rz");
            if (allocateL2)
            {
                l20 = CreateTexture(dimensions, GraphicsFormat.R8G8B8A8_UNorm, "BurtGI XGI Physical L2 0");
                l21 = CreateTexture(dimensions, GraphicsFormat.R8G8B8A8_UNorm, "BurtGI XGI Physical L2 1");
                l22 = CreateTexture(dimensions, GraphicsFormat.R8G8B8A8_UNorm, "BurtGI XGI Physical L2 2");
                l23 = CreateTexture(dimensions, GraphicsFormat.R8G8B8A8_UNorm, "BurtGI XGI Physical L2 3");
            }
            if (allocateSkyVisibility)
            {
                skyVisibilityL0L1 = CreateTexture(dimensions, GraphicsFormat.R16G16B16A16_SFloat, "BurtGI XGI Physical Sky Visibility");
            }

            if (allocateSkyShadingDirection)
            {
                skyShadingDirectionIndices = CreateTexture(dimensions, GraphicsFormat.R8_UNorm, "BurtGI XGI Physical Sky Shading Direction");
            }

            ClearAllTextures();
            probeVolume.useVirtualProbeData = true;
            probeVolume.virtualPhysicalPoolDimensions = dimensions;
            probeVolume.virtualL0L1Rx = l0L1Rx;
            probeVolume.virtualL1GL1Ry = l1GL1Ry;
            probeVolume.virtualL1BL1Rz = l1BL1Rz;
            UpdateProbeVolumeL2Textures();
            probeVolume.virtualSkyVisibilityL0L1 = skyVisibilityL0L1;
            probeVolume.virtualSkyShadingDirectionIndices = skyShadingDirectionIndices;
            return true;
        }

        public bool TryUploadChunk(
            int chunkIndex,
            byte[] sourceL0L1Rx,
            byte[] sourceL1GL1Ry,
            byte[] sourceL1BL1Rz,
            byte[] sourceL20 = null,
            byte[] sourceL21 = null,
            byte[] sourceL22 = null,
            byte[] sourceL23 = null,
            byte[] sourceSkyVisibilityL0L1 = null,
            byte[] sourceSkyShadingDirectionIndices = null)
        {
            var hasL2Data = sourceL20 != null || sourceL21 != null || sourceL22 != null || sourceL23 != null;
            if (!IsInitialized || !IsValidChunkIndex(chunkIndex) ||
                !HasChunkSize(sourceL0L1Rx, 8) || !HasChunkSize(sourceL1GL1Ry, 4) || !HasChunkSize(sourceL1BL1Rz, 4) ||
                (hasL2Data && (sourceL20 == null || sourceL21 == null || sourceL22 == null || sourceL23 == null)) ||
                !HasOptionalL2ChunkSize(sourceL20, l20) || !HasOptionalL2ChunkSize(sourceL21, l21) ||
                !HasOptionalL2ChunkSize(sourceL22, l22) || !HasOptionalL2ChunkSize(sourceL23, l23) ||
                (sourceSkyVisibilityL0L1 != null && (skyVisibilityL0L1 == null || !HasChunkSize(sourceSkyVisibilityL0L1, 8))) ||
                (sourceSkyShadingDirectionIndices != null && (skyShadingDirectionIndices == null || !HasChunkSize(sourceSkyShadingDirectionIndices, 1))))
            {
                return false;
            }

            var origin = GetChunkOrigin(chunkIndex);
            UploadChunk(uploadHalfKernel, sourceL0L1Rx, l0L1Rx, origin);
            UploadChunk(uploadRgba8Kernel, sourceL1GL1Ry, l1GL1Ry, origin);
            UploadChunk(uploadRgba8Kernel, sourceL1BL1Rz, l1BL1Rz, origin);
            if (hasL2Data)
            {
                UploadChunk(uploadRgba8Kernel, sourceL20, l20, origin);
                UploadChunk(uploadRgba8Kernel, sourceL21, l21, origin);
                UploadChunk(uploadRgba8Kernel, sourceL22, l22, origin);
                UploadChunk(uploadRgba8Kernel, sourceL23, l23, origin);
                l2LoadedChunks.Add(chunkIndex);
            }
            else
            {
                ClearL2Chunk(origin);
                l2LoadedChunks.Remove(chunkIndex);
            }
            UpdateProbeVolumeL2Textures();

            if (skyVisibilityL0L1 != null)
            {
                if (sourceSkyVisibilityL0L1 != null) UploadChunk(uploadHalfKernel, sourceSkyVisibilityL0L1, skyVisibilityL0L1, origin);
                else ClearChunkTexture(clearRgbaKernel, skyVisibilityL0L1, origin);
            }

            if (skyShadingDirectionIndices != null)
            {
                if (sourceSkyShadingDirectionIndices != null) UploadChunk(uploadR8Kernel, sourceSkyShadingDirectionIndices, skyShadingDirectionIndices, origin, true);
                else ClearChunkTexture(clearR8Kernel, skyShadingDirectionIndices, origin);
            }

            return true;
        }

        public bool TryClearChunk(int chunkIndex)
        {
            if (!IsInitialized || !IsValidChunkIndex(chunkIndex))
            {
                return false;
            }

            var origin = GetChunkOrigin(chunkIndex);
            ClearChunkTexture(clearRgbaKernel, l0L1Rx, origin);
            ClearChunkTexture(clearRgbaKernel, l1GL1Ry, origin);
            ClearChunkTexture(clearRgbaKernel, l1BL1Rz, origin);
            if (l20 != null) ClearChunkTexture(clearRgbaKernel, l20, origin);
            if (l21 != null) ClearChunkTexture(clearRgbaKernel, l21, origin);
            if (l22 != null) ClearChunkTexture(clearRgbaKernel, l22, origin);
            if (l23 != null) ClearChunkTexture(clearRgbaKernel, l23, origin);
            l2LoadedChunks.Remove(chunkIndex);
            UpdateProbeVolumeL2Textures();
            if (skyVisibilityL0L1 != null) ClearChunkTexture(clearRgbaKernel, skyVisibilityL0L1, origin);
            if (skyShadingDirectionIndices != null) ClearChunkTexture(clearR8Kernel, skyShadingDirectionIndices, origin);
            return true;
        }

        public Vector3Int GetChunkOrigin(int chunkIndex)
        {
            var x = chunkIndex % chunkDimensions.x;
            var y = (chunkIndex / chunkDimensions.x) % chunkDimensions.y;
            var z = chunkIndex / (chunkDimensions.x * chunkDimensions.y);
            return new Vector3Int(x * ChunkWidth, y * ChunkHeight, z * ChunkDepth);
        }

        public void ReleasePool()
        {
            ClearProbeVolumeTextureReferences();
            DestroyTexture(ref l0L1Rx);
            DestroyTexture(ref l1GL1Ry);
            DestroyTexture(ref l1BL1Rz);
            DestroyTexture(ref l20);
            DestroyTexture(ref l21);
            DestroyTexture(ref l22);
            DestroyTexture(ref l23);
            DestroyTexture(ref skyVisibilityL0L1);
            DestroyTexture(ref skyShadingDirectionIndices);
            uploadBuffer?.Release();
            uploadBuffer = null;
            uploadWords = null;
            l2LoadedChunks.Clear();
        }

        private void ClearProbeVolumeTextureReferences()
        {
            if (probeVolume == null)
            {
                return;
            }

            if (probeVolume.virtualL0L1Rx == l0L1Rx) probeVolume.virtualL0L1Rx = null;
            if (probeVolume.virtualL1GL1Ry == l1GL1Ry) probeVolume.virtualL1GL1Ry = null;
            if (probeVolume.virtualL1BL1Rz == l1BL1Rz) probeVolume.virtualL1BL1Rz = null;
            if (probeVolume.virtualL20 == l20) probeVolume.virtualL20 = null;
            if (probeVolume.virtualL21 == l21) probeVolume.virtualL21 = null;
            if (probeVolume.virtualL22 == l22) probeVolume.virtualL22 = null;
            if (probeVolume.virtualL23 == l23) probeVolume.virtualL23 = null;
            if (probeVolume.virtualSkyVisibilityL0L1 == skyVisibilityL0L1) probeVolume.virtualSkyVisibilityL0L1 = null;
            if (probeVolume.virtualSkyShadingDirectionIndices == skyShadingDirectionIndices) probeVolume.virtualSkyShadingDirectionIndices = null;
        }

        private bool IsValidChunkIndex(int chunkIndex) => chunkIndex >= 0 && chunkIndex < ChunkCapacity;

        private static bool HasChunkSize(byte[] source, int bytesPerProbe) => source != null && source.Length == ChunkProbeCount * bytesPerProbe;

        private static bool HasOptionalL2ChunkSize(byte[] source, RenderTexture destination) => source == null || (destination != null && HasChunkSize(source, 4));

        private bool TryInitializeUploadCompute()
        {
            if (uploadCompute != null)
            {
                return uploadHalfKernel >= 0 && uploadRgba8Kernel >= 0 && uploadR8Kernel >= 0 && clearRgbaKernel >= 0 && clearR8Kernel >= 0;
            }

            uploadCompute = Resources.Load<ComputeShader>(UploadComputeResourcePath);
            if (uploadCompute == null)
            {
                Debug.LogError("BurtRP could not find XGI physical-pool upload compute shader: " + UploadComputeResourcePath);
                return false;
            }

            try
            {
                uploadHalfKernel = uploadCompute.FindKernel("UploadRGBAHalf");
                uploadRgba8Kernel = uploadCompute.FindKernel("UploadRGBA8");
                uploadR8Kernel = uploadCompute.FindKernel("UploadR8");
                clearRgbaKernel = uploadCompute.FindKernel("ClearRGBA");
                clearR8Kernel = uploadCompute.FindKernel("ClearR8");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("BurtRP could not initialize XGI physical-pool upload kernels: " + exception.Message);
                uploadCompute = null;
                uploadHalfKernel = uploadRgba8Kernel = uploadR8Kernel = clearRgbaKernel = clearR8Kernel = -1;
                return false;
            }
        }

        private static RenderTexture CreateTexture(Vector3Int dimensions, GraphicsFormat format, string textureName)
        {
            var descriptor = new RenderTextureDescriptor(dimensions.x, dimensions.y, format, 0)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = dimensions.z,
                msaaSamples = 1,
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                sRGB = false
            };
            var texture = new RenderTexture(descriptor)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            texture.Create();
            return texture;
        }

        private void ClearAllTextures()
        {
            var dimensions = PhysicalPoolDimensions;
            ClearTexture(clearRgbaKernel, l0L1Rx, dimensions);
            ClearTexture(clearRgbaKernel, l1GL1Ry, dimensions);
            ClearTexture(clearRgbaKernel, l1BL1Rz, dimensions);
            if (l20 != null) ClearTexture(clearRgbaKernel, l20, dimensions);
            if (l21 != null) ClearTexture(clearRgbaKernel, l21, dimensions);
            if (l22 != null) ClearTexture(clearRgbaKernel, l22, dimensions);
            if (l23 != null) ClearTexture(clearRgbaKernel, l23, dimensions);
            if (skyVisibilityL0L1 != null) ClearTexture(clearRgbaKernel, skyVisibilityL0L1, dimensions);
            if (skyShadingDirectionIndices != null) ClearTexture(clearR8Kernel, skyShadingDirectionIndices, dimensions);
        }

        private void UploadChunk(int kernel, byte[] source, RenderTexture destination, Vector3Int origin)
        {
            EnsureUploadBuffer(source);
            uploadCompute.SetBuffer(kernel, UploadSourceId, uploadBuffer);
            uploadCompute.SetTexture(kernel, UploadRgbaTextureId, destination);
            uploadCompute.SetVector(UploadOriginId, new Vector4(origin.x, origin.y, origin.z, 0f));
            uploadCompute.SetVector(UploadPoolDimensionsId, new Vector4(PhysicalPoolDimensions.x, PhysicalPoolDimensions.y, PhysicalPoolDimensions.z, 0f));
            uploadCompute.Dispatch(kernel, ChunkWidth / UploadThreadCountX, ChunkHeight / UploadThreadCountY, ChunkDepth / UploadThreadCountZ);
        }

        private void UploadChunk(int kernel, byte[] source, RenderTexture destination, Vector3Int origin, bool r8Destination)
        {
            EnsureUploadBuffer(source);
            uploadCompute.SetBuffer(kernel, UploadSourceId, uploadBuffer);
            uploadCompute.SetTexture(kernel, r8Destination ? UploadR8TextureId : UploadRgbaTextureId, destination);
            uploadCompute.SetVector(UploadOriginId, new Vector4(origin.x, origin.y, origin.z, 0f));
            uploadCompute.SetVector(UploadPoolDimensionsId, new Vector4(PhysicalPoolDimensions.x, PhysicalPoolDimensions.y, PhysicalPoolDimensions.z, 0f));
            uploadCompute.Dispatch(kernel, ChunkWidth / UploadThreadCountX, ChunkHeight / UploadThreadCountY, ChunkDepth / UploadThreadCountZ);
        }

        private void ClearL2Chunk(Vector3Int origin)
        {
            if (l20 != null) ClearChunkTexture(clearRgbaKernel, l20, origin);
            if (l21 != null) ClearChunkTexture(clearRgbaKernel, l21, origin);
            if (l22 != null) ClearChunkTexture(clearRgbaKernel, l22, origin);
            if (l23 != null) ClearChunkTexture(clearRgbaKernel, l23, origin);
        }

        private void UpdateProbeVolumeL2Textures()
        {
            if (probeVolume == null)
            {
                return;
            }

            var hasL2Data = l2LoadedChunks.Count > 0;
            probeVolume.virtualL20 = hasL2Data ? l20 : null;
            probeVolume.virtualL21 = hasL2Data ? l21 : null;
            probeVolume.virtualL22 = hasL2Data ? l22 : null;
            probeVolume.virtualL23 = hasL2Data ? l23 : null;
        }

        private void ClearChunkTexture(int kernel, RenderTexture destination, Vector3Int origin)
        {
            uploadCompute.SetTexture(kernel, kernel == clearR8Kernel ? UploadR8TextureId : UploadRgbaTextureId, destination);
            uploadCompute.SetVector(UploadOriginId, new Vector4(origin.x, origin.y, origin.z, 0f));
            uploadCompute.SetVector(UploadPoolDimensionsId, new Vector4(PhysicalPoolDimensions.x, PhysicalPoolDimensions.y, PhysicalPoolDimensions.z, 0f));
            uploadCompute.Dispatch(kernel, ChunkWidth / UploadThreadCountX, ChunkHeight / UploadThreadCountY, ChunkDepth / UploadThreadCountZ);
        }

        private void ClearTexture(int kernel, RenderTexture destination, Vector3Int dimensions)
        {
            uploadCompute.SetTexture(kernel, kernel == clearR8Kernel ? UploadR8TextureId : UploadRgbaTextureId, destination);
            uploadCompute.SetVector(UploadOriginId, Vector4.zero);
            uploadCompute.SetVector(UploadPoolDimensionsId, new Vector4(dimensions.x, dimensions.y, dimensions.z, 0f));
            uploadCompute.Dispatch(
                kernel,
                Mathf.CeilToInt(dimensions.x / (float)UploadThreadCountX),
                Mathf.CeilToInt(dimensions.y / (float)UploadThreadCountY),
                Mathf.CeilToInt(dimensions.z / (float)UploadThreadCountZ));
        }

        private void EnsureUploadBuffer(byte[] source)
        {
            var wordCount = source.Length / sizeof(uint);
            if (uploadBuffer == null || uploadBuffer.count != wordCount)
            {
                uploadBuffer?.Release();
                uploadBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Raw, wordCount, sizeof(uint));
                uploadWords = new uint[wordCount];
            }

            Buffer.BlockCopy(source, 0, uploadWords, 0, source.Length);
            uploadBuffer.SetData(uploadWords);
        }

        private static void DestroyTexture(ref RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
            texture = null;
        }
    }
}
