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

        [Tooltip("Allocate XRender-compatible optional R8 probe validity data.")]
        public bool allocateValidity = true;

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
        private RenderTexture validity;
        private RenderTexture skyVisibilityL0L1;
        private RenderTexture skyShadingDirectionIndices;
        private GraphicsBuffer uploadBuffer;
        private uint[] uploadWords;
        private ComputeShader uploadCompute;
        private int uploadHalfKernel = -1;
        private int uploadRgba8Kernel = -1;
        private int uploadR8Kernel = -1;
        private int clearRgbaKernel = -1;
        private int clearRgba8NeutralKernel = -1;
        private int clearR8Kernel = -1;
        private int clearR8OneKernel = -1;
        private readonly HashSet<int> l1LoadedChunks = new HashSet<int>();
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

        public string LastUploadStatus { get; private set; } = "Idle";

        public bool CanAddressChunk(int chunkIndex) => IsValidChunkIndex(chunkIndex);

        public static string ResolveUploadComputeStatusLabel()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                return "Unsupported(SystemInfo.supportsComputeShaders=false)";
            }

            var shader = Resources.Load<ComputeShader>(UploadComputeResourcePath);
            if (shader == null)
            {
                return "Missing(" + UploadComputeResourcePath + ")";
            }

            const string kernels = "UploadRGBAHalf,UploadRGBA8,UploadR8,ClearRGBA,ClearRGBA8Neutral,ClearR8,ClearR8One,UploadData,UploadDataL2,ProbeDebugRuntimeInfoCS";
            var missing = string.Empty;
            var missingCount = 0;
            var kernelNames = kernels.Split(',');
            for (var i = 0; i < kernelNames.Length; i++)
            {
                var kernelName = kernelNames[i];
                if (shader.HasKernel(kernelName))
                {
                    continue;
                }

                if (missingCount > 0)
                {
                    missing += ",";
                }

                missing += kernelName;
                missingCount++;
            }

            return missingCount == 0
                ? "Ready(Kernels=" + kernelNames.Length + ",L0L1+L2+Validity+SkyVisibilityUpload+XRenderAlias)"
                : "MissingKernel(" + missing + ")";
        }

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
            if (allocateValidity)
            {
                validity = CreateTexture(dimensions, GraphicsFormat.R8_UNorm, "BurtGI XGI Physical Validity");
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
            UpdateProbeVolumeL1Textures();
            UpdateProbeVolumeL2Textures();
            probeVolume.virtualValidity = validity;
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
            byte[] sourceSkyShadingDirectionIndices = null,
            byte[] sourceValidity = null,
            int sharedChunkIndex = -1,
            bool updateSharedData = true)
        {
            var resolvedSharedChunkIndex = sharedChunkIndex >= 0 ? sharedChunkIndex : chunkIndex;
            var hasL0Data = sourceL0L1Rx != null;
            var hasL1Data = sourceL1GL1Ry != null || sourceL1BL1Rz != null;
            var hasL2Data = sourceL20 != null || sourceL21 != null || sourceL22 != null || sourceL23 != null;
            if (!IsInitialized) return FailUpload("NotInitialized");
            if (!IsValidChunkIndex(chunkIndex)) return FailUpload("InvalidChunkIndex(" + chunkIndex + "/" + ChunkCapacity + ")");
            if (!IsValidChunkIndex(resolvedSharedChunkIndex)) return FailUpload("InvalidSharedChunkIndex(" + resolvedSharedChunkIndex + "/" + ChunkCapacity + ")");
            if (hasL0Data && !HasChunkSize(sourceL0L1Rx, 8)) return FailUpload("InvalidL0L1RxBytes(" + ByteLength(sourceL0L1Rx) + ")");
            if (hasL1Data && (sourceL1GL1Ry == null || sourceL1BL1Rz == null)) return FailUpload("IncompleteL1Bytes(G=" + ByteLength(sourceL1GL1Ry) + ",B=" + ByteLength(sourceL1BL1Rz) + ")");
            if (sourceL1GL1Ry != null && !HasChunkSize(sourceL1GL1Ry, 4)) return FailUpload("InvalidL1GL1RyBytes(" + ByteLength(sourceL1GL1Ry) + ")");
            if (sourceL1BL1Rz != null && !HasChunkSize(sourceL1BL1Rz, 4)) return FailUpload("InvalidL1BL1RzBytes(" + ByteLength(sourceL1BL1Rz) + ")");
            if (hasL2Data && !hasL1Data) return FailUpload("L2WithoutL1");
            if (hasL2Data && (sourceL20 == null || sourceL21 == null || sourceL22 == null || sourceL23 == null)) return FailUpload("IncompleteL2Bytes");
            if (!HasOptionalL2ChunkSize(sourceL20, l20)) return FailUpload("InvalidL20Bytes(" + ByteLength(sourceL20) + ",Texture=" + (l20 != null) + ")");
            if (!HasOptionalL2ChunkSize(sourceL21, l21)) return FailUpload("InvalidL21Bytes(" + ByteLength(sourceL21) + ",Texture=" + (l21 != null) + ")");
            if (!HasOptionalL2ChunkSize(sourceL22, l22)) return FailUpload("InvalidL22Bytes(" + ByteLength(sourceL22) + ",Texture=" + (l22 != null) + ")");
            if (!HasOptionalL2ChunkSize(sourceL23, l23)) return FailUpload("InvalidL23Bytes(" + ByteLength(sourceL23) + ",Texture=" + (l23 != null) + ")");
            if (updateSharedData && sourceSkyVisibilityL0L1 != null && (skyVisibilityL0L1 == null || !HasChunkSize(sourceSkyVisibilityL0L1, 8))) return FailUpload("InvalidSkyVisibilityBytes(" + ByteLength(sourceSkyVisibilityL0L1) + ",Texture=" + (skyVisibilityL0L1 != null) + ")");
            if (updateSharedData && sourceSkyShadingDirectionIndices != null && (skyShadingDirectionIndices == null || !HasChunkSize(sourceSkyShadingDirectionIndices, 1))) return FailUpload("InvalidSkyDirectionBytes(" + ByteLength(sourceSkyShadingDirectionIndices) + ",Texture=" + (skyShadingDirectionIndices != null) + ")");
            if (updateSharedData && sourceValidity != null && (validity == null || !HasChunkSize(sourceValidity, 1))) return FailUpload("InvalidValidityBytes(" + ByteLength(sourceValidity) + ",Texture=" + (validity != null) + ")");

            var origin = GetChunkOrigin(chunkIndex);
            var sharedOrigin = GetChunkOrigin(resolvedSharedChunkIndex);
            if (hasL0Data) UploadChunk(uploadHalfKernel, sourceL0L1Rx, l0L1Rx, origin);
            else ClearChunkTexture(clearRgbaKernel, l0L1Rx, origin);
            if (hasL1Data)
            {
                UploadChunk(uploadRgba8Kernel, sourceL1GL1Ry, l1GL1Ry, origin);
                UploadChunk(uploadRgba8Kernel, sourceL1BL1Rz, l1BL1Rz, origin);
                l1LoadedChunks.Add(chunkIndex);
            }
            else
            {
                ClearL1Chunk(origin);
                l1LoadedChunks.Remove(chunkIndex);
            }
            UpdateProbeVolumeL1Textures();
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

            if (updateSharedData && validity != null)
            {
                if (sourceValidity != null) UploadChunk(uploadR8Kernel, sourceValidity, validity, sharedOrigin, true);
                else ClearChunkTexture(clearR8Kernel, validity, sharedOrigin);
            }

            if (updateSharedData && skyVisibilityL0L1 != null)
            {
                if (sourceSkyVisibilityL0L1 != null) UploadChunk(uploadHalfKernel, sourceSkyVisibilityL0L1, skyVisibilityL0L1, sharedOrigin);
                else ClearChunkTexture(clearRgbaKernel, skyVisibilityL0L1, sharedOrigin);
            }

            if (updateSharedData && skyShadingDirectionIndices != null)
            {
                if (sourceSkyShadingDirectionIndices != null) UploadChunk(uploadR8Kernel, sourceSkyShadingDirectionIndices, skyShadingDirectionIndices, sharedOrigin, true);
                else ClearChunkTexture(clearR8OneKernel, skyShadingDirectionIndices, sharedOrigin);
            }

            LastUploadStatus = "Uploaded(Chunk=" + chunkIndex + ",Shared=" + resolvedSharedChunkIndex + ",L0=" + hasL0Data + ",L1=" + hasL1Data + ",L2=" + hasL2Data + ")";
            return true;
        }

        private bool FailUpload(string reason)
        {
            LastUploadStatus = reason;
            return false;
        }

        private static int ByteLength(byte[] bytes)
        {
            return bytes != null ? bytes.Length : -1;
        }

        public bool TryClearChunk(int chunkIndex) => TryClearChunk(chunkIndex, -1);

        public bool TryClearChunk(int chunkIndex, int sharedChunkIndex)
        {
            var resolvedSharedChunkIndex = sharedChunkIndex >= 0 ? sharedChunkIndex : chunkIndex;
            if (!IsInitialized || !IsValidChunkIndex(chunkIndex) || !IsValidChunkIndex(resolvedSharedChunkIndex))
            {
                return false;
            }

            var origin = GetChunkOrigin(chunkIndex);
            var sharedOrigin = GetChunkOrigin(resolvedSharedChunkIndex);
            ClearChunkTexture(clearRgbaKernel, l0L1Rx, origin);
            ClearChunkTexture(clearRgba8NeutralKernel, l1GL1Ry, origin);
            ClearChunkTexture(clearRgba8NeutralKernel, l1BL1Rz, origin);
            l1LoadedChunks.Remove(chunkIndex);
            UpdateProbeVolumeL1Textures();
            if (l20 != null) ClearChunkTexture(clearRgba8NeutralKernel, l20, origin);
            if (l21 != null) ClearChunkTexture(clearRgba8NeutralKernel, l21, origin);
            if (l22 != null) ClearChunkTexture(clearRgba8NeutralKernel, l22, origin);
            if (l23 != null) ClearChunkTexture(clearRgba8NeutralKernel, l23, origin);
            l2LoadedChunks.Remove(chunkIndex);
            UpdateProbeVolumeL2Textures();
            ClearSharedChunk(sharedOrigin);
            return true;
        }

        public Vector3Int GetChunkOrigin(int chunkIndex)
        {
            return GetChunkOrigin(chunkIndex, chunkDimensions);
        }

        public static Vector3Int GetChunkOrigin(int chunkIndex, Vector3Int chunkDimensions)
        {
            var safeDimensions = Vector3Int.Max(Vector3Int.one, chunkDimensions);
            var x = chunkIndex % safeDimensions.x;
            var y = (chunkIndex / safeDimensions.x) % safeDimensions.y;
            var z = chunkIndex / (safeDimensions.x * safeDimensions.y);
            return new Vector3Int(x * ChunkWidth, y * ChunkHeight, z * ChunkDepth);
        }

        public static uint GetChunkBrickPhysicalLocation(int chunkIndex, int brickIndexInChunk, Vector3Int chunkDimensions)
        {
            var safeDimensions = Vector3Int.Max(Vector3Int.one, chunkDimensions);
            var origin = GetChunkOrigin(chunkIndex, safeDimensions);
            var poolWidth = safeDimensions.x * ChunkWidth;
            var poolHeight = safeDimensions.y * ChunkHeight;
            var brickOffset = Mathf.Clamp(brickIndexInChunk, 0, BricksPerChunk - 1) * BrickProbeCountPerDimension;
            var physicalLocation =
                origin.x + brickOffset +
                origin.y * poolWidth +
                origin.z * poolWidth * poolHeight;
            return (uint)Mathf.Max(0, physicalLocation);
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
            DestroyTexture(ref validity);
            DestroyTexture(ref skyVisibilityL0L1);
            DestroyTexture(ref skyShadingDirectionIndices);
            uploadBuffer?.Release();
            uploadBuffer = null;
            uploadWords = null;
            l1LoadedChunks.Clear();
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
            if (probeVolume.virtualValidity == validity) probeVolume.virtualValidity = null;
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
                if (uploadHalfKernel >= 0 && uploadRgba8Kernel >= 0 && uploadR8Kernel >= 0 &&
                    clearRgbaKernel >= 0 && clearRgba8NeutralKernel >= 0 && clearR8Kernel >= 0 && clearR8OneKernel >= 0)
                {
                    return true;
                }

                uploadCompute = null;
                uploadHalfKernel = uploadRgba8Kernel = uploadR8Kernel = clearRgbaKernel = clearRgba8NeutralKernel = clearR8Kernel = clearR8OneKernel = -1;
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
                clearRgba8NeutralKernel = uploadCompute.FindKernel("ClearRGBA8Neutral");
                clearR8Kernel = uploadCompute.FindKernel("ClearR8");
                clearR8OneKernel = uploadCompute.FindKernel("ClearR8One");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("BurtRP could not initialize XGI physical-pool upload kernels: " + exception.Message);
                uploadCompute = null;
                uploadHalfKernel = uploadRgba8Kernel = uploadR8Kernel = clearRgbaKernel = clearRgba8NeutralKernel = clearR8Kernel = clearR8OneKernel = -1;
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
            ClearTexture(clearRgba8NeutralKernel, l1GL1Ry, dimensions);
            ClearTexture(clearRgba8NeutralKernel, l1BL1Rz, dimensions);
            if (l20 != null) ClearTexture(clearRgba8NeutralKernel, l20, dimensions);
            if (l21 != null) ClearTexture(clearRgba8NeutralKernel, l21, dimensions);
            if (l22 != null) ClearTexture(clearRgba8NeutralKernel, l22, dimensions);
            if (l23 != null) ClearTexture(clearRgba8NeutralKernel, l23, dimensions);
            if (validity != null) ClearTexture(clearR8Kernel, validity, dimensions);
            if (skyVisibilityL0L1 != null) ClearTexture(clearRgbaKernel, skyVisibilityL0L1, dimensions);
            if (skyShadingDirectionIndices != null) ClearTexture(clearR8OneKernel, skyShadingDirectionIndices, dimensions);
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
            if (l20 != null) ClearChunkTexture(clearRgba8NeutralKernel, l20, origin);
            if (l21 != null) ClearChunkTexture(clearRgba8NeutralKernel, l21, origin);
            if (l22 != null) ClearChunkTexture(clearRgba8NeutralKernel, l22, origin);
            if (l23 != null) ClearChunkTexture(clearRgba8NeutralKernel, l23, origin);
        }

        private void ClearL1Chunk(Vector3Int origin)
        {
            ClearChunkTexture(clearRgba8NeutralKernel, l1GL1Ry, origin);
            ClearChunkTexture(clearRgba8NeutralKernel, l1BL1Rz, origin);
        }

        private void ClearSharedChunk(Vector3Int origin)
        {
            if (validity != null) ClearChunkTexture(clearR8Kernel, validity, origin);
            if (skyVisibilityL0L1 != null) ClearChunkTexture(clearRgbaKernel, skyVisibilityL0L1, origin);
            if (skyShadingDirectionIndices != null) ClearChunkTexture(clearR8OneKernel, skyShadingDirectionIndices, origin);
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

        private void UpdateProbeVolumeL1Textures()
        {
            if (probeVolume == null)
            {
                return;
            }

            var hasL1Data = l1LoadedChunks.Count > 0;
            probeVolume.virtualL1GL1Ry = hasL1Data ? l1GL1Ry : null;
            probeVolume.virtualL1BL1Rz = hasL1Data ? l1BL1Rz : null;
        }

        private void ClearChunkTexture(int kernel, RenderTexture destination, Vector3Int origin)
        {
            uploadCompute.SetTexture(kernel, kernel == clearR8Kernel || kernel == clearR8OneKernel ? UploadR8TextureId : UploadRgbaTextureId, destination);
            uploadCompute.SetVector(UploadOriginId, new Vector4(origin.x, origin.y, origin.z, 0f));
            uploadCompute.SetVector(UploadPoolDimensionsId, new Vector4(PhysicalPoolDimensions.x, PhysicalPoolDimensions.y, PhysicalPoolDimensions.z, 0f));
            uploadCompute.Dispatch(kernel, ChunkWidth / UploadThreadCountX, ChunkHeight / UploadThreadCountY, ChunkDepth / UploadThreadCountZ);
        }

        private void ClearTexture(int kernel, RenderTexture destination, Vector3Int dimensions)
        {
            uploadCompute.SetTexture(kernel, kernel == clearR8Kernel || kernel == clearR8OneKernel ? UploadR8TextureId : UploadRgbaTextureId, destination);
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
