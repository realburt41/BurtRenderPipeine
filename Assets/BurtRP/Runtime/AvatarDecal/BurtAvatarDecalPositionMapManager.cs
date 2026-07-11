using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    public enum BurtAvatarDecalPreSkinPositionMode
    {
        Auto,
        MeshPosition,
        PackedUV3,
    }

    public enum BurtAvatarDecalPositionMapStatus
    {
        Pending,
        Ready,
        Failed,
        Released,
    }

    public sealed class BurtAvatarDecalPositionMapResult
    {
        public BurtAvatarDecalPositionMapStatus Status { get; internal set; }
        public RenderTexture PositionMap { get; internal set; }
        public string FailureReason { get; internal set; }
    }

    /// <summary>
    /// Builds UV-space position maps for avatar decal baking. Every texel stores the target mesh's
    /// pre-skin object-space position. XRender parity uses mesh POSITION for normal meshes and packed UV3
    /// for compressed meshes; this deliberately does not use camera GBuffer data.
    /// </summary>
    public sealed class BurtAvatarDecalPositionMapManager
    {
        public const int MinimumPositionMapSize = 16;
        public const int MaximumPositionMapSize = 4096;

        private const string PositionMapShaderName = "Hidden/BurtRP/AvatarDecal/GeneratePositionMap";
        private const string GeneratePositionMapProfilingSampleName = "Burt Avatar Decal / Generate PositionMap";
        private readonly List<PendingRequest> pendingRequests = new();
        private readonly HashSet<BurtAvatarDecalPositionMapResult> activeResults = new();
        private Material meshPositionMaterial;
        private Material packedUV3Material;

        private sealed class PendingRequest
        {
            public SkinnedMeshRenderer Renderer;
            public Matrix4x4 BodyMatrix;
            public BurtAvatarDecalPreSkinPositionMode PositionMode;
            public BurtAvatarDecalPositionMapResult Result;
        }

        public BurtAvatarDecalPositionMapResult RequestPositionMap(
            Renderer renderer,
            Matrix4x4 bodyMatrix,
            int size = 1024,
            BurtAvatarDecalPreSkinPositionMode positionMode = BurtAvatarDecalPreSkinPositionMode.Auto)
        {
            var result = new BurtAvatarDecalPositionMapResult
            {
                Status = BurtAvatarDecalPositionMapStatus.Pending,
            };

            if (renderer is not SkinnedMeshRenderer skinnedRenderer || skinnedRenderer.sharedMesh == null)
            {
                Fail(result, "Avatar decal PositionMap requires a SkinnedMeshRenderer with a shared mesh.");
                return result;
            }

            var positionMapSize = Mathf.Clamp(size, MinimumPositionMapSize, MaximumPositionMapSize);
            result.PositionMap = CreatePositionMap(positionMapSize, skinnedRenderer.name);
            activeResults.Add(result);
            pendingRequests.Add(new PendingRequest
            {
                Renderer = skinnedRenderer,
                BodyMatrix = bodyMatrix,
                PositionMode = positionMode,
                Result = result,
            });
            return result;
        }

        public void ExecutePending(ScriptableRenderContext context)
        {
            if (pendingRequests.Count == 0)
            {
                return;
            }

            EnsureMaterials();
            var commandBuffer = CommandBufferPool.Get("Burt Avatar Decal PositionMap");
            try
            {
                for (var index = 0; index < pendingRequests.Count; index++)
                {
                    var request = pendingRequests[index];
                    var result = request.Result;
                    if (result == null || result.Status != BurtAvatarDecalPositionMapStatus.Pending)
                    {
                        continue;
                    }

                    if (request.Renderer == null || request.Renderer.sharedMesh == null || result.PositionMap == null)
                    {
                        Fail(result, "Avatar decal PositionMap request lost its renderer, mesh, or target texture before execution.");
                        continue;
                    }

                    var resolvedMode = ResolvePositionMode(request.Renderer.sharedMesh, request.PositionMode);
                    var material = resolvedMode == BurtAvatarDecalPreSkinPositionMode.PackedUV3
                        ? packedUV3Material
                        : meshPositionMaterial;
                    if (material == null)
                    {
                        Fail(result, "Avatar decal PositionMap shader is unavailable.");
                        continue;
                    }

                    commandBuffer.BeginSample(GeneratePositionMapProfilingSampleName);
                    commandBuffer.SetRenderTarget(result.PositionMap);
                    commandBuffer.ClearRenderTarget(false, true, Color.black);
                    commandBuffer.DrawMesh(request.Renderer.sharedMesh, request.BodyMatrix, material, 0, 0);
                    commandBuffer.EndSample(GeneratePositionMapProfilingSampleName);
                    result.Status = BurtAvatarDecalPositionMapStatus.Ready;
                }

                context.ExecuteCommandBuffer(commandBuffer);
            }
            finally
            {
                pendingRequests.Clear();
                commandBuffer.Clear();
                CommandBufferPool.Release(commandBuffer);
            }
        }

        public void Release(BurtAvatarDecalPositionMapResult result)
        {
            if (result == null || result.Status == BurtAvatarDecalPositionMapStatus.Released)
            {
                return;
            }

            pendingRequests.RemoveAll(request => request.Result == result);
            ReleaseResultTexture(result);
            result.Status = BurtAvatarDecalPositionMapStatus.Released;
            activeResults.Remove(result);
        }

        public void ReleaseAll()
        {
            foreach (var result in activeResults)
            {
                ReleaseResultTexture(result);
                result.Status = BurtAvatarDecalPositionMapStatus.Released;
            }

            pendingRequests.Clear();
            activeResults.Clear();
            CoreUtils.Destroy(meshPositionMaterial);
            CoreUtils.Destroy(packedUV3Material);
            meshPositionMaterial = null;
            packedUV3Material = null;
        }

        private static RenderTexture CreatePositionMap(int size, string rendererName)
        {
            var descriptor = new RenderTextureDescriptor(size, size, GraphicsFormat.R32G32B32A32_SFloat, 0)
            {
                msaaSamples = 1,
                sRGB = false,
                useMipMap = false,
                autoGenerateMips = false,
            };
            var positionMap = new RenderTexture(descriptor)
            {
                name = "BurtAvatarDecalPositionMap_" + rendererName,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            positionMap.Create();
            return positionMap;
        }

        private void EnsureMaterials()
        {
            if (meshPositionMaterial != null && packedUV3Material != null)
            {
                return;
            }

            var shader = Shader.Find(PositionMapShaderName);
            if (shader == null)
            {
                return;
            }

            meshPositionMaterial ??= CreatePositionMapMaterial(shader, false);
            packedUV3Material ??= CreatePositionMapMaterial(shader, true);
        }

        private static Material CreatePositionMapMaterial(Shader shader, bool packedUV3)
        {
            var material = CoreUtils.CreateEngineMaterial(shader);
            material.name = packedUV3
                ? "Burt Avatar Decal PositionMap (Packed UV3)"
                : "Burt Avatar Decal PositionMap (Mesh POSITION)";
            if (packedUV3)
            {
                material.EnableKeyword("BURT_PRESKIN_POSITION_PACKED");
            }
            return material;
        }

        private static BurtAvatarDecalPreSkinPositionMode ResolvePositionMode(
            Mesh mesh,
            BurtAvatarDecalPreSkinPositionMode requestedMode)
        {
            if (requestedMode != BurtAvatarDecalPreSkinPositionMode.Auto)
            {
                return requestedMode;
            }

            var attributes = mesh.GetVertexAttributes();
            for (var index = 0; index < attributes.Length; index++)
            {
                var attribute = attributes[index];
                if (attribute.attribute == VertexAttribute.TexCoord3 &&
                    attribute.format == VertexAttributeFormat.UInt32 &&
                    attribute.dimension >= 2)
                {
                    return BurtAvatarDecalPreSkinPositionMode.PackedUV3;
                }
            }

            return BurtAvatarDecalPreSkinPositionMode.MeshPosition;
        }

        private static void ReleaseResultTexture(BurtAvatarDecalPositionMapResult result)
        {
            if (result.PositionMap == null)
            {
                return;
            }

            result.PositionMap.Release();
            CoreUtils.Destroy(result.PositionMap);
            result.PositionMap = null;
        }

        private static void Fail(BurtAvatarDecalPositionMapResult result, string reason)
        {
            if (result == null)
            {
                return;
            }

            result.FailureReason = reason;
            result.Status = BurtAvatarDecalPositionMapStatus.Failed;
        }
    }
}
