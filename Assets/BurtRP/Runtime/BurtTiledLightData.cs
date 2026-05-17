using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal struct BurtTileLightLayout
    {
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public int TileSize { get; }
        public int TileCountX { get; }
        public int TileCountY { get; }
        public int TileCount { get; }

        public BurtTileLightLayout(int pixelWidth, int pixelHeight, int tileSize)
        {
            PixelWidth = Mathf.Max(1, pixelWidth);
            PixelHeight = Mathf.Max(1, pixelHeight);
            TileSize = Mathf.Max(1, tileSize);
            TileCountX = Mathf.Max(1, (PixelWidth + TileSize - 1) / TileSize);
            TileCountY = Mathf.Max(1, (PixelHeight + TileSize - 1) / TileSize);
            TileCount = TileCountX * TileCountY;
        }
    }

    internal struct BurtClusterLightLayout
    {
        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public int TileSize { get; }
        public int TileCountX { get; }
        public int TileCountY { get; }
        public int DepthSliceCount { get; }
        public int TileCount { get; }
        public int ClusterCount { get; }

        public BurtClusterLightLayout(BurtTileLightLayout tileLayout, int depthSliceCount)
        {
            PixelWidth = tileLayout.PixelWidth;
            PixelHeight = tileLayout.PixelHeight;
            TileSize = tileLayout.TileSize;
            TileCountX = tileLayout.TileCountX;
            TileCountY = tileLayout.TileCountY;
            DepthSliceCount = Mathf.Max(1, depthSliceCount);
            TileCount = tileLayout.TileCount;
            ClusterCount = TileCount * DepthSliceCount;
        }
    }

    internal static class BurtTiledLightData
    {
        public const int TileSize = 16;
        public const int ClusterDepthSliceCount = 16;
        public const int MaxLightsPerTile = BurtLightingData.MaxAdditionalLights;
        public const int MaxLightsPerCluster = BurtLightingData.MaxAdditionalLights;
        public const int TileLightCountStride = 4;
        public const int TileLightIndexStride = 4;
        public const int TileLightOffsetStride = 8;
        public const string DebugBuildModeLabel = "CPUTiledDebug";
        public const string RuntimeBuildModeLabel = "CPUClusteredRuntime";

        public const string TileLightCountBufferShaderName = "_BurtTileLightCountBuffer";
        public const string TileLightListBufferShaderName = "_BurtTileLightListBuffer";
        public const string TileLightOffsetBufferShaderName = "_BurtTileLightOffsetBuffer";
        public const string TileLightGridParamsShaderName = "_BurtTileLightGridParams";
        public const string ClusterLightCountBufferShaderName = "_BurtClusterLightCountBuffer";
        public const string ClusterLightListBufferShaderName = "_BurtClusterLightListBuffer";
        public const string ClusterLightOffsetBufferShaderName = "_BurtClusterLightOffsetBuffer";
        public const string ClusterLightGridParamsShaderName = "_BurtClusterLightGridParams";
        public const string ClusterLightDepthParamsShaderName = "_BurtClusterLightDepthParams";
        public const string ClusterLightWorldToViewZShaderName = "_BurtClusterLightWorldToViewZ";
        public const string ClusterLightBufferEnabledShaderName = "_BurtClusterLightBufferEnabled";
        public const string TileLightDebugStatsShaderName = "_BurtTileLightDebugStats";
        public const string TileLightDebugModeShaderName = "_BurtTileLightDebugMode";
        public const string TileLightCountBufferEnabledShaderName = "_BurtTileLightCountBufferEnabled";
        public const string TileLightDebugColorTextureShaderName = "_BurtTileLightDebugColorTexture";
        public const string TileLightDebugColorTextureEnabledShaderName = "_BurtTileLightDebugColorTextureEnabled";

        public static readonly int TileLightCountBufferId = Shader.PropertyToID(TileLightCountBufferShaderName);
        public static readonly int TileLightListBufferId = Shader.PropertyToID(TileLightListBufferShaderName);
        public static readonly int TileLightOffsetBufferId = Shader.PropertyToID(TileLightOffsetBufferShaderName);
        public static readonly int TileLightGridParamsId = Shader.PropertyToID(TileLightGridParamsShaderName);
        public static readonly int ClusterLightCountBufferId = Shader.PropertyToID(ClusterLightCountBufferShaderName);
        public static readonly int ClusterLightListBufferId = Shader.PropertyToID(ClusterLightListBufferShaderName);
        public static readonly int ClusterLightOffsetBufferId = Shader.PropertyToID(ClusterLightOffsetBufferShaderName);
        public static readonly int ClusterLightGridParamsId = Shader.PropertyToID(ClusterLightGridParamsShaderName);
        public static readonly int ClusterLightDepthParamsId = Shader.PropertyToID(ClusterLightDepthParamsShaderName);
        public static readonly int ClusterLightWorldToViewZId = Shader.PropertyToID(ClusterLightWorldToViewZShaderName);
        public static readonly int ClusterLightBufferEnabledId = Shader.PropertyToID(ClusterLightBufferEnabledShaderName);
        public static readonly int TileLightDebugStatsId = Shader.PropertyToID(TileLightDebugStatsShaderName);
        public static readonly int TileLightDebugModeId = Shader.PropertyToID(TileLightDebugModeShaderName);
        public static readonly int TileLightCountBufferEnabledId = Shader.PropertyToID(TileLightCountBufferEnabledShaderName);
        public static readonly int TileLightDebugColorTextureId = Shader.PropertyToID(TileLightDebugColorTextureShaderName);
        public static readonly int TileLightDebugColorTextureEnabledId = Shader.PropertyToID(TileLightDebugColorTextureEnabledShaderName);

        public static BurtTileLightLayout CalculateLayout(Camera camera)
        {
            if (camera == null)
            {
                return new BurtTileLightLayout(1, 1, TileSize);
            }

            var width = camera.pixelWidth;
            var height = camera.pixelHeight;

            if (camera.targetTexture != null)
            {
                width = Mathf.Max(width, camera.targetTexture.width);
                height = Mathf.Max(height, camera.targetTexture.height);
            }

            return new BurtTileLightLayout(width, height, TileSize);
        }

        public static BurtClusterLightLayout CalculateClusterLayout(Camera camera)
        {
            return new BurtClusterLightLayout(CalculateLayout(camera), ClusterDepthSliceCount);
        }

        public static float CalculateClusterInvLogDepthRange(float nearPlane, float farPlane)
        {
            nearPlane = Mathf.Max(nearPlane, 0.0001f);
            farPlane = Mathf.Max(farPlane, nearPlane + 0.0001f);
            return 1f / Mathf.Max(Mathf.Log(farPlane / nearPlane), 0.0001f);
        }

        public static float CalculateClusterDepth01(float viewDepth, float nearPlane, float farPlane)
        {
            nearPlane = Mathf.Max(nearPlane, 0.0001f);
            var safeDepth = Mathf.Max(viewDepth, nearPlane);
            return Mathf.Clamp01(Mathf.Log(safeDepth / nearPlane) * CalculateClusterInvLogDepthRange(nearPlane, farPlane));
        }

        public static int CalculateClusterDepthSlice(float viewDepth, float nearPlane, float farPlane, int depthSliceCount)
        {
            depthSliceCount = Mathf.Max(1, depthSliceCount);
            var depth01 = CalculateClusterDepth01(viewDepth, nearPlane, farPlane);
            return Mathf.Clamp(Mathf.FloorToInt(depth01 * depthSliceCount), 0, depthSliceCount - 1);
        }

        public static float CalculateClusterSliceDepth(float nearPlane, float farPlane, int sliceIndex, int depthSliceCount)
        {
            nearPlane = Mathf.Max(nearPlane, 0.0001f);
            farPlane = Mathf.Max(farPlane, nearPlane + 0.0001f);
            depthSliceCount = Mathf.Max(1, depthSliceCount);
            var t = Mathf.Clamp01((float)sliceIndex / depthSliceCount);
            return nearPlane * Mathf.Exp(Mathf.Log(farPlane / nearPlane) * t);
        }

        public static int ResolveRuntimeMaxLightsPerTile()
        {
            return MaxLightsPerTile;
        }

        public static int ResolveRuntimeMaxLightsPerCluster()
        {
            return MaxLightsPerCluster;
        }

        public static int ResolveDebugMaxLightsPerTile()
        {
            return Mathf.Clamp(BurtShadingDebugSettings.TileLightDebugMaxLightsPerTile, 1, MaxLightsPerTile);
        }

        public static int ResolveMaxLightsPerTile()
        {
            return ResolveDebugMaxLightsPerTile();
        }

        public static int ResolveMaxLightsPerTile(bool useRuntimeCapacity)
        {
            return useRuntimeCapacity ? ResolveRuntimeMaxLightsPerTile() : ResolveDebugMaxLightsPerTile();
        }

        public static BurtRenderBufferDescriptor CreateTileLightCountBufferDescriptor(Camera camera)
        {
            var layout = CalculateLayout(camera);
            return new BurtRenderBufferDescriptor(layout.TileCount, TileLightCountStride, GraphicsBuffer.Target.Structured, TileLightCountBufferShaderName);
        }

        public static BurtRenderBufferDescriptor CreateTileLightListBufferDescriptor(Camera camera)
        {
            var layout = CalculateLayout(camera);
            return new BurtRenderBufferDescriptor(layout.TileCount * ResolveRuntimeMaxLightsPerTile(), TileLightIndexStride, GraphicsBuffer.Target.Structured, TileLightListBufferShaderName);
        }

        public static BurtRenderBufferDescriptor CreateTileLightOffsetBufferDescriptor(Camera camera)
        {
            var layout = CalculateLayout(camera);
            return new BurtRenderBufferDescriptor(layout.TileCount, TileLightOffsetStride, GraphicsBuffer.Target.Structured, TileLightOffsetBufferShaderName);
        }

        public static BurtRenderBufferDescriptor CreateClusterLightCountBufferDescriptor(Camera camera)
        {
            var layout = CalculateClusterLayout(camera);
            return new BurtRenderBufferDescriptor(layout.ClusterCount, TileLightCountStride, GraphicsBuffer.Target.Structured, ClusterLightCountBufferShaderName);
        }

        public static BurtRenderBufferDescriptor CreateClusterLightListBufferDescriptor(Camera camera)
        {
            var layout = CalculateClusterLayout(camera);
            return new BurtRenderBufferDescriptor(layout.ClusterCount * ResolveRuntimeMaxLightsPerCluster(), TileLightIndexStride, GraphicsBuffer.Target.Structured, ClusterLightListBufferShaderName);
        }

        public static BurtRenderBufferDescriptor CreateClusterLightOffsetBufferDescriptor(Camera camera)
        {
            var layout = CalculateClusterLayout(camera);
            return new BurtRenderBufferDescriptor(layout.ClusterCount, TileLightOffsetStride, GraphicsBuffer.Target.Structured, ClusterLightOffsetBufferShaderName);
        }

        public static bool ShouldUseTiledLightDebugResources(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool hasLocalDeferredTargets)
        {
            if (!hasLocalDeferredTargets || request == null || !request.IsValid || asset == null)
            {
                return false;
            }

            return asset.RendererMode == BurtRendererMode.Deferred &&
                BurtTileLightDebugViewUtility.IsTileLightDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool ShouldUseTileLightListDebugResources(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool hasLocalDeferredTargets)
        {
            return ShouldUseTiledLightDebugResources(request, asset, hasLocalDeferredTargets) &&
                BurtShadingDebugSettings.Mode == BurtShadingDebugMode.TileLightOccupancy;
        }

        public static bool ShouldUseRuntimeTiledLightingResources(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool hasLocalDeferredTargets)
        {
            if (!hasLocalDeferredTargets || request == null || !request.IsValid || asset == null)
            {
                return false;
            }

            if (request.Type == BurtRenderRequestType.Preview || request.Type == BurtRenderRequestType.Reflection)
            {
                return false;
            }

            return asset.RendererMode == BurtRendererMode.Deferred;
        }

        public static bool ShouldUseRuntimeClusteredLightingResources(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool hasLocalDeferredTargets)
        {
            return ShouldUseRuntimeTiledLightingResources(request, asset, hasLocalDeferredTargets);
        }

        public static bool ShouldUseTiledLightResources(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool hasLocalDeferredTargets)
        {
            return ShouldUseRuntimeTiledLightingResources(request, asset, hasLocalDeferredTargets) ||
                ShouldUseTiledLightDebugResources(request, asset, hasLocalDeferredTargets);
        }

        public static bool ShouldUseTileLightListResources(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool hasLocalDeferredTargets)
        {
            return ShouldUseRuntimeTiledLightingResources(request, asset, hasLocalDeferredTargets) ||
                ShouldUseTileLightListDebugResources(request, asset, hasLocalDeferredTargets);
        }

        public static bool ShouldUseClusterLightResources(BurtRenderRequest request, BurtRenderPipelineAsset asset, bool hasLocalDeferredTargets)
        {
            return ShouldUseRuntimeClusteredLightingResources(request, asset, hasLocalDeferredTargets);
        }
    }
}
