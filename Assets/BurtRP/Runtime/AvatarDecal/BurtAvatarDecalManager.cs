using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtAvatarDecalCombineStatus
    {
        Pending,
        Combined,
        PendingCompression,
        Compressed,
        Failed,
        Released,
    }

    public enum BurtAvatarDecalCompressionFormat
    {
        BC3,
        BC7,
    }

    public sealed class BurtAvatarDecalBaseLayer
    {
        public Texture BaseMap;
        public Color BaseColor = Color.white;
        public Vector3 Position = Vector3.zero;
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Scale = Vector3.one;

        internal Matrix4x4 GetInverseTransformMatrix()
        {
            return Matrix4x4.TRS(Position, Rotation, Scale).inverse;
        }
    }

    public sealed class BurtAvatarDecalCombineParams
    {
        public SkinnedMeshRenderer TargetRenderer;
        public Matrix4x4 BodyMatrix;
        public Texture BaseMap;
        public int PositionMapSize = 1024;
        public BurtAvatarDecalPreSkinPositionMode PreSkinPositionMode = BurtAvatarDecalPreSkinPositionMode.Auto;
        public List<BurtAvatarDecalBaseLayer> Layers = new();
    }

    public sealed class BurtAvatarDecalCombineResult
    {
        public BurtAvatarDecalCombineStatus Status { get; internal set; }
        public RenderTexture CombinedBaseMap { get; internal set; }
        public Texture2D CompressedBaseMap { get; internal set; }
        public BurtAvatarDecalCompressionFormat CompressionFormat { get; internal set; }
        public RenderTexture ProjectionDebugMap { get; internal set; }
        public string FailureReason { get; internal set; }

        internal RenderTexture TemporaryBaseMap;
    }

    /// <summary>
    /// First AvatarDecal implementation: projects one or more decal BaseMaps through a UV-space
    /// pre-skin PositionMap and writes a combined BaseMap. Mask, normal, and compression are separate stages.
    /// </summary>
    public sealed class BurtAvatarDecalManager
    {
        private const string CombineComputeResourcePath = "BurtAvatarDecalCombine";
        private const string CombineBaseMapKernelName = "BurtAvatarDecalCombineBaseMap";
        private const string CombineBaseMapProfilingSampleName = "Burt Avatar Decal / Combine BaseMap";
        private const string CompressionComputeResourcePath = "BurtAvatarDecalCompress";
        private const string CompressBaseMapBC3KernelName = "BurtAvatarDecalCompressBC3";
        private const string CompressBaseMapBC7KernelName = "BurtAvatarDecalCompressBC7Mode6";
        private const int ComputeThreadGroupSize = 8;

        private static readonly int BaseMapId = Shader.PropertyToID("_BurtAvatarBaseMap");
        private static readonly int PositionMapId = Shader.PropertyToID("_BurtAvatarPositionMap");
        private static readonly int DecalBaseMapId = Shader.PropertyToID("_BurtAvatarDecalBaseMap");
        private static readonly int ResultBaseMapId = Shader.PropertyToID("_BurtAvatarResultBaseMap");
        private static readonly int ProjectionDebugMapId = Shader.PropertyToID("_BurtAvatarProjectionDebugMap");
        private static readonly int ResultSizeId = Shader.PropertyToID("_BurtAvatarResultSize");
        private static readonly int BodyMatrixId = Shader.PropertyToID("_BurtAvatarBodyMatrix");
        private static readonly int DecalInverseMatrixId = Shader.PropertyToID("_BurtAvatarDecalInverseMatrix");
        private static readonly int DecalBaseColorId = Shader.PropertyToID("_BurtAvatarDecalBaseColor");
        private static readonly int CompressionSourceTextureId = Shader.PropertyToID("_BurtAvatarCompressionSource");
        private static readonly int CompressionTargetTextureId = Shader.PropertyToID("_BurtAvatarCompressionTarget");
        private static readonly int CompressionBlockSizeId = Shader.PropertyToID("_BurtAvatarCompressionBlockSize");

        private readonly BurtAvatarDecalPositionMapManager positionMapManager;
        private readonly List<PendingCombine> pendingCombines = new();
        private readonly List<BurtAvatarDecalCombineResult> pendingCompressions = new();
        private readonly HashSet<BurtAvatarDecalCombineResult> activeResults = new();
        private readonly List<BurtAvatarDecalPositionMapResult> positionMapsPendingRelease = new();
        private readonly List<RenderTexture> texturesPendingRelease = new();

        private ComputeShader combineCompute;
        private int combineBaseMapKernel = -1;
        private ComputeShader compressionCompute;
        private int compressBaseMapBC3Kernel = -1;
        private int compressBaseMapBC7Kernel = -1;

        private sealed class PendingCombine
        {
            public BurtAvatarDecalCombineParams Params;
            public BurtAvatarDecalCombineResult Result;
            public BurtAvatarDecalPositionMapResult PositionMapResult;
        }

        public BurtAvatarDecalManager(BurtAvatarDecalPositionMapManager positionMapManager)
        {
            this.positionMapManager = positionMapManager;
        }

        public BurtAvatarDecalCombineResult CombineBaseMaps(BurtAvatarDecalCombineParams combineParams)
        {
            var result = new BurtAvatarDecalCombineResult
            {
                Status = BurtAvatarDecalCombineStatus.Pending,
            };

            if (!ValidateCombineParams(combineParams, out var failureReason))
            {
                Fail(result, failureReason);
                return result;
            }

            result.CombinedBaseMap = CreateWritableColorTexture(
                combineParams.BaseMap.width,
                combineParams.BaseMap.height,
                "BurtAvatarDecalCombinedBaseMap");
            result.ProjectionDebugMap = CreateWritableColorTexture(
                combineParams.BaseMap.width,
                combineParams.BaseMap.height,
                "BurtAvatarDecalProjectionDebugMap");
            if (combineParams.Layers.Count > 1)
            {
                result.TemporaryBaseMap = CreateWritableColorTexture(
                    combineParams.BaseMap.width,
                    combineParams.BaseMap.height,
                    "BurtAvatarDecalTemporaryBaseMap");
            }

            var positionMapResult = positionMapManager.RequestPositionMap(
                combineParams.TargetRenderer,
                combineParams.BodyMatrix,
                combineParams.PositionMapSize,
                combineParams.PreSkinPositionMode);
            if (positionMapResult.Status == BurtAvatarDecalPositionMapStatus.Failed)
            {
                ReleaseResultTexturesImmediately(result);
                Fail(result, positionMapResult.FailureReason);
                return result;
            }

            activeResults.Add(result);
            pendingCombines.Add(new PendingCombine
            {
                Params = combineParams,
                Result = result,
                PositionMapResult = positionMapResult,
            });
            return result;
        }

        /// <summary>Queues GPU BC7 compression for a completed combined BaseMap, with BC3/DXT5 fallback.</summary>
        public bool CompressBaseMap(BurtAvatarDecalCombineResult result)
        {
            if (result == null || result.Status != BurtAvatarDecalCombineStatus.Combined || result.CombinedBaseMap == null)
            {
                return false;
            }

            if (!SystemInfo.supportsComputeShaders ||
                result.CombinedBaseMap.width % 4 != 0 || result.CombinedBaseMap.height % 4 != 0)
            {
                Fail(result, "Avatar decal compression requires compute-shader support and BaseMap dimensions divisible by four.");
                return false;
            }

            var compressionFormat = SystemInfo.SupportsTextureFormat(TextureFormat.BC7)
                ? BurtAvatarDecalCompressionFormat.BC7
                : SystemInfo.SupportsTextureFormat(TextureFormat.DXT5)
                    ? BurtAvatarDecalCompressionFormat.BC3
                    : (BurtAvatarDecalCompressionFormat?)null;
            if (!compressionFormat.HasValue)
            {
                Fail(result, "Avatar decal compression requires BC7 or DXT5 texture-format support.");
                return false;
            }

            ReleaseCompressedBaseMap(result);
            result.CompressionFormat = compressionFormat.Value;
            result.CompressedBaseMap = new Texture2D(
                result.CombinedBaseMap.width,
                result.CombinedBaseMap.height,
                result.CompressionFormat == BurtAvatarDecalCompressionFormat.BC7 ? TextureFormat.BC7 : TextureFormat.DXT5,
                false,
                true)
            {
                name = "BurtAvatarDecalCompressedBaseMap_" + result.CompressionFormat,
                filterMode = result.CombinedBaseMap.filterMode,
                wrapMode = result.CombinedBaseMap.wrapMode,
            };
            result.CompressedBaseMap.Apply(false, true);
            result.Status = BurtAvatarDecalCombineStatus.PendingCompression;
            pendingCompressions.Add(result);
            return true;
        }

        public void ExecutePending(ScriptableRenderContext context)
        {
            if (pendingCombines.Count == 0 && pendingCompressions.Count == 0)
            {
                return;
            }

            if (pendingCombines.Count > 0)
            {
                ExecutePendingCombines(context);
            }
            if (pendingCompressions.Count > 0)
            {
                ExecutePendingCompressions(context);
            }
        }

        private void ExecutePendingCombines(ScriptableRenderContext context)
        {
            if (!EnsureCompute(out var failureReason))
            {
                for (var index = 0; index < pendingCombines.Count; index++)
                {
                    var pending = pendingCombines[index];
                    QueuePositionMapRelease(pending.PositionMapResult);
                    ReleaseResultTexturesImmediately(pending.Result);
                    Fail(pending.Result, failureReason);
                }
                pendingCombines.Clear();
                return;
            }

            var commandBuffer = CommandBufferPool.Get("Burt Avatar Decal Combine BaseMap");
            try
            {
                for (var index = 0; index < pendingCombines.Count; index++)
                {
                    var pending = pendingCombines[index];
                    if (pending.Result == null || pending.Result.Status != BurtAvatarDecalCombineStatus.Pending)
                    {
                        QueuePositionMapRelease(pending.PositionMapResult);
                        continue;
                    }

                    if (pending.PositionMapResult == null ||
                        pending.PositionMapResult.Status != BurtAvatarDecalPositionMapStatus.Ready ||
                        pending.PositionMapResult.PositionMap == null)
                    {
                        QueuePositionMapRelease(pending.PositionMapResult);
                        ReleaseResultTexturesImmediately(pending.Result);
                        Fail(pending.Result, "Avatar decal PositionMap did not become ready before BaseMap combine.");
                        continue;
                    }

                    if (!DispatchBaseMapCombine(commandBuffer, pending, out failureReason))
                    {
                        QueuePositionMapRelease(pending.PositionMapResult);
                        ReleaseResultTexturesImmediately(pending.Result);
                        Fail(pending.Result, failureReason);
                        continue;
                    }

                    QueuePositionMapRelease(pending.PositionMapResult);
                    if (pending.Result.TemporaryBaseMap != null)
                    {
                        texturesPendingRelease.Add(pending.Result.TemporaryBaseMap);
                        pending.Result.TemporaryBaseMap = null;
                    }
                    pending.Result.Status = BurtAvatarDecalCombineStatus.Combined;
                }

                context.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                pendingCombines.Clear();
                commandBuffer.Clear();
                CommandBufferPool.Release(commandBuffer);
            }
        }

        private void ExecutePendingCompressions(ScriptableRenderContext context)
        {
            if (!EnsureCompressionCompute(out var failureReason))
            {
                for (var index = 0; index < pendingCompressions.Count; index++)
                {
                    Fail(pendingCompressions[index], failureReason);
                }
                pendingCompressions.Clear();
                return;
            }

            var commandBuffer = CommandBufferPool.Get("Burt Avatar Decal Compress BaseMap");
            try
            {
                for (var index = 0; index < pendingCompressions.Count; index++)
                {
                    var result = pendingCompressions[index];
                    if (result == null || result.Status != BurtAvatarDecalCombineStatus.PendingCompression)
                    {
                        continue;
                    }
                    if (result.CombinedBaseMap == null || result.CompressedBaseMap == null)
                    {
                        Fail(result, "Avatar decal compression lost its source or destination texture before execution.");
                        continue;
                    }

                    var blockWidth = result.CombinedBaseMap.width / 4;
                    var blockHeight = result.CombinedBaseMap.height / 4;
                    var encodedBlocks = CreateWritableEncodedBlocks(blockWidth, blockHeight);
                    var kernel = result.CompressionFormat == BurtAvatarDecalCompressionFormat.BC7
                        ? compressBaseMapBC7Kernel
                        : compressBaseMapBC3Kernel;
                    var profilingSampleName = result.CompressionFormat == BurtAvatarDecalCompressionFormat.BC7
                        ? "Burt Avatar Decal / Compress BaseMap BC7"
                        : "Burt Avatar Decal / Compress BaseMap BC3";
                    commandBuffer.BeginSample(profilingSampleName);
                    commandBuffer.SetComputeTextureParam(compressionCompute, kernel, CompressionSourceTextureId, result.CombinedBaseMap);
                    commandBuffer.SetComputeTextureParam(compressionCompute, kernel, CompressionTargetTextureId, encodedBlocks);
                    commandBuffer.SetComputeVectorParam(compressionCompute, CompressionBlockSizeId, new Vector4(blockWidth, blockHeight, 0.0f, 0.0f));
                    commandBuffer.DispatchCompute(
                        compressionCompute,
                        kernel,
                        Mathf.CeilToInt(blockWidth / (float)ComputeThreadGroupSize),
                        Mathf.CeilToInt(blockHeight / (float)ComputeThreadGroupSize),
                        1);
                    commandBuffer.CopyTexture(encodedBlocks, 0, 0, 0, 0, blockWidth, blockHeight, result.CompressedBaseMap, 0, 0, 0, 0);
                    commandBuffer.EndSample(profilingSampleName);
                    texturesPendingRelease.Add(encodedBlocks);
                    result.Status = BurtAvatarDecalCombineStatus.Compressed;
                }

                context.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                pendingCompressions.Clear();
                commandBuffer.Clear();
                CommandBufferPool.Release(commandBuffer);
            }
        }

        /// <summary>Call only after the frame's queued commands have been submitted.</summary>
        public void FlushDeferredReleases()
        {
            for (var index = 0; index < positionMapsPendingRelease.Count; index++)
            {
                positionMapManager.Release(positionMapsPendingRelease[index]);
            }
            positionMapsPendingRelease.Clear();

            for (var index = 0; index < texturesPendingRelease.Count; index++)
            {
                ReleaseTexture(texturesPendingRelease[index]);
            }
            texturesPendingRelease.Clear();
        }

        public void Release(BurtAvatarDecalCombineResult result)
        {
            if (result == null || result.Status == BurtAvatarDecalCombineStatus.Released)
            {
                return;
            }

            for (var index = pendingCombines.Count - 1; index >= 0; index--)
            {
                var pending = pendingCombines[index];
                if (pending.Result == result)
                {
                    positionMapManager.Release(pending.PositionMapResult);
                    pendingCombines.RemoveAt(index);
                }
            }
            pendingCompressions.Remove(result);

            ReleaseResultTexturesImmediately(result);
            result.Status = BurtAvatarDecalCombineStatus.Released;
            activeResults.Remove(result);
        }

        public void ReleaseAll()
        {
            for (var index = 0; index < pendingCombines.Count; index++)
            {
                positionMapManager.Release(pendingCombines[index].PositionMapResult);
            }
            pendingCombines.Clear();
            pendingCompressions.Clear();

            foreach (var result in activeResults)
            {
                ReleaseResultTexturesImmediately(result);
                result.Status = BurtAvatarDecalCombineStatus.Released;
            }
            activeResults.Clear();

            FlushDeferredReleases();
            combineCompute = null;
            combineBaseMapKernel = -1;
            compressionCompute = null;
            compressBaseMapBC3Kernel = -1;
            compressBaseMapBC7Kernel = -1;
        }

        private bool DispatchBaseMapCombine(CommandBuffer commandBuffer, PendingCombine pending, out string failureReason)
        {
            failureReason = null;
            var layers = pending.Params.Layers;
            var result = pending.Result;
            Texture sourceBaseMap = pending.Params.BaseMap;
            RenderTexture targetBaseMap = layers.Count % 2 == 0 ? result.TemporaryBaseMap : result.CombinedBaseMap;

            for (var layerIndex = 0; layerIndex < layers.Count; layerIndex++)
            {
                var layer = layers[layerIndex];
                if (layer == null || layer.BaseMap == null)
                {
                    failureReason = "Avatar decal BaseMap layer is null or has no texture.";
                    return false;
                }

                commandBuffer.BeginSample(CombineBaseMapProfilingSampleName);
                commandBuffer.SetComputeTextureParam(combineCompute, combineBaseMapKernel, BaseMapId, sourceBaseMap);
                commandBuffer.SetComputeTextureParam(combineCompute, combineBaseMapKernel, PositionMapId, pending.PositionMapResult.PositionMap);
                commandBuffer.SetComputeTextureParam(combineCompute, combineBaseMapKernel, DecalBaseMapId, layer.BaseMap);
                commandBuffer.SetComputeTextureParam(combineCompute, combineBaseMapKernel, ResultBaseMapId, targetBaseMap);
                commandBuffer.SetComputeTextureParam(combineCompute, combineBaseMapKernel, ProjectionDebugMapId, result.ProjectionDebugMap);
                commandBuffer.SetComputeVectorParam(combineCompute, ResultSizeId, new Vector4(targetBaseMap.width, targetBaseMap.height, 0.0f, 0.0f));
                commandBuffer.SetComputeMatrixParam(combineCompute, BodyMatrixId, pending.Params.BodyMatrix);
                commandBuffer.SetComputeMatrixParam(combineCompute, DecalInverseMatrixId, layer.GetInverseTransformMatrix());
                commandBuffer.SetComputeVectorParam(combineCompute, DecalBaseColorId, layer.BaseColor);
                commandBuffer.DispatchCompute(
                    combineCompute,
                    combineBaseMapKernel,
                    Mathf.CeilToInt(targetBaseMap.width / (float)ComputeThreadGroupSize),
                    Mathf.CeilToInt(targetBaseMap.height / (float)ComputeThreadGroupSize),
                    1);
                commandBuffer.EndSample(CombineBaseMapProfilingSampleName);

                sourceBaseMap = targetBaseMap;
                targetBaseMap = targetBaseMap == result.CombinedBaseMap
                    ? result.TemporaryBaseMap
                    : result.CombinedBaseMap;
            }

            return true;
        }

        private bool EnsureCompute(out string failureReason)
        {
            failureReason = null;
            if (combineCompute == null)
            {
                combineCompute = Resources.Load<ComputeShader>(CombineComputeResourcePath);
            }

            if (combineCompute == null)
            {
                failureReason = "Avatar decal combine compute shader could not be loaded from Resources/BurtAvatarDecalCombine.";
                return false;
            }

            if (combineBaseMapKernel < 0)
            {
                if (!combineCompute.HasKernel(CombineBaseMapKernelName))
                {
                    failureReason = "Avatar decal combine compute shader has no BurtAvatarDecalCombineBaseMap kernel.";
                    return false;
                }
                combineBaseMapKernel = combineCompute.FindKernel(CombineBaseMapKernelName);
            }

            return true;
        }

        private bool EnsureCompressionCompute(out string failureReason)
        {
            failureReason = null;
            if (compressionCompute == null)
            {
                compressionCompute = Resources.Load<ComputeShader>(CompressionComputeResourcePath);
            }
            if (compressionCompute == null)
            {
                failureReason = "Avatar decal BC3 compression compute shader could not be loaded from Resources/BurtAvatarDecalCompress.";
                return false;
            }
            if (compressBaseMapBC3Kernel < 0)
            {
                if (!compressionCompute.HasKernel(CompressBaseMapBC3KernelName))
                {
                    failureReason = "Avatar decal compression compute shader has no BurtAvatarDecalCompressBC3 kernel.";
                    return false;
                }
                compressBaseMapBC3Kernel = compressionCompute.FindKernel(CompressBaseMapBC3KernelName);
            }
            if (compressBaseMapBC7Kernel < 0)
            {
                if (!compressionCompute.HasKernel(CompressBaseMapBC7KernelName))
                {
                    failureReason = "Avatar decal compression compute shader has no BurtAvatarDecalCompressBC7Mode6 kernel.";
                    return false;
                }
                compressBaseMapBC7Kernel = compressionCompute.FindKernel(CompressBaseMapBC7KernelName);
            }
            return true;
        }

        private static bool ValidateCombineParams(BurtAvatarDecalCombineParams combineParams, out string failureReason)
        {
            failureReason = null;
            if (combineParams == null)
            {
                failureReason = "Avatar decal combine params are null.";
                return false;
            }
            if (combineParams.TargetRenderer == null || combineParams.TargetRenderer.sharedMesh == null)
            {
                failureReason = "Avatar decal combine requires a SkinnedMeshRenderer with a shared mesh.";
                return false;
            }
            if (combineParams.BaseMap == null || combineParams.BaseMap.width <= 0 || combineParams.BaseMap.height <= 0)
            {
                failureReason = "Avatar decal combine requires a readable BaseMap texture with valid dimensions.";
                return false;
            }
            if (combineParams.Layers == null || combineParams.Layers.Count == 0)
            {
                failureReason = "Avatar decal combine requires at least one BaseMap layer.";
                return false;
            }
            for (var layerIndex = 0; layerIndex < combineParams.Layers.Count; layerIndex++)
            {
                var layer = combineParams.Layers[layerIndex];
                if (layer == null || layer.BaseMap == null)
                {
                    failureReason = "Avatar decal BaseMap layer " + layerIndex + " is null or has no texture.";
                    return false;
                }
            }
            return true;
        }

        private static RenderTexture CreateWritableColorTexture(int width, int height, string name)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = name,
                enableRandomWrite = true,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            return texture;
        }

        private static RenderTexture CreateWritableEncodedBlocks(int width, int height)
        {
            var descriptor = new RenderTextureDescriptor(width, height, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_UInt, 0)
            {
                enableRandomWrite = true,
                msaaSamples = 1,
                sRGB = false,
            };
            var texture = new RenderTexture(descriptor)
            {
                name = "BurtAvatarDecalCompressedBlocks_BC3",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            texture.Create();
            return texture;
        }

        private void QueuePositionMapRelease(BurtAvatarDecalPositionMapResult positionMapResult)
        {
            if (positionMapResult != null && !positionMapsPendingRelease.Contains(positionMapResult))
            {
                positionMapsPendingRelease.Add(positionMapResult);
            }
        }

        private static void ReleaseResultTexturesImmediately(BurtAvatarDecalCombineResult result)
        {
            if (result == null)
            {
                return;
            }

            ReleaseTexture(result.CombinedBaseMap);
            ReleaseCompressedBaseMap(result);
            ReleaseTexture(result.ProjectionDebugMap);
            ReleaseTexture(result.TemporaryBaseMap);
            result.CombinedBaseMap = null;
            result.ProjectionDebugMap = null;
            result.TemporaryBaseMap = null;
        }

        private static void ReleaseTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }

            texture.Release();
            CoreUtils.Destroy(texture);
        }

        private static void ReleaseCompressedBaseMap(BurtAvatarDecalCombineResult result)
        {
            if (result?.CompressedBaseMap == null)
            {
                return;
            }
            CoreUtils.Destroy(result.CompressedBaseMap);
            result.CompressedBaseMap = null;
        }

        private static void Fail(BurtAvatarDecalCombineResult result, string reason)
        {
            if (result == null)
            {
                return;
            }

            result.FailureReason = reason;
            result.Status = BurtAvatarDecalCombineStatus.Failed;
        }
    }
}
