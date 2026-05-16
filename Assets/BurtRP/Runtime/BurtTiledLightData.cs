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

    internal static class BurtTiledLightData
    {
        public const int TileSize = 16;
        public const int MaxLightsPerTile = BurtLightingData.MaxAdditionalLights;
        public const int TileLightCountStride = 4;
        public const int TileLightIndexStride = 4;
        public const int TileLightOffsetStride = 8;
        public const string DebugBuildModeLabel = "CPUApproxDebugOnly";
        public const string RuntimeBuildModeLabel = "CPUApproxRuntime";
        private const bool EnableExperimentalRuntimeTiledLighting = false;

        public const string TileLightCountBufferShaderName = "_BurtTileLightCountBuffer";
        public const string TileLightListBufferShaderName = "_BurtTileLightListBuffer";
        public const string TileLightOffsetBufferShaderName = "_BurtTileLightOffsetBuffer";
        public const string TileLightGridParamsShaderName = "_BurtTileLightGridParams";
        public const string TileLightDebugStatsShaderName = "_BurtTileLightDebugStats";
        public const string TileLightDebugModeShaderName = "_BurtTileLightDebugMode";
        public const string TileLightCountBufferEnabledShaderName = "_BurtTileLightCountBufferEnabled";
        public const string TileLightDebugColorTextureShaderName = "_BurtTileLightDebugColorTexture";
        public const string TileLightDebugColorTextureEnabledShaderName = "_BurtTileLightDebugColorTextureEnabled";

        public static readonly int TileLightCountBufferId = Shader.PropertyToID(TileLightCountBufferShaderName);
        public static readonly int TileLightListBufferId = Shader.PropertyToID(TileLightListBufferShaderName);
        public static readonly int TileLightOffsetBufferId = Shader.PropertyToID(TileLightOffsetBufferShaderName);
        public static readonly int TileLightGridParamsId = Shader.PropertyToID(TileLightGridParamsShaderName);
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

        public static int ResolveRuntimeMaxLightsPerTile()
        {
            return MaxLightsPerTile;
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

            return EnableExperimentalRuntimeTiledLighting &&
                asset.RendererMode == BurtRendererMode.Deferred;
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
    }
}
