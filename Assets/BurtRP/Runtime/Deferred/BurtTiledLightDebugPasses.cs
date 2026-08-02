using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Burt.RenderPipeline
{
    internal static class BurtTileLightDebugViewUtility
    {
        public static bool UseCpuDebugColorTextureFallback
        {
            get => BurtShadingDebugSettings.UseTileLightCpuDebugColorTextureFallback;
            set => BurtShadingDebugSettings.UseTileLightCpuDebugColorTextureFallback = value;
        }

        public static bool ShouldUseTileLightDebugView(bool hasLocalDeferredTargets)
        {
            return hasLocalDeferredTargets && IsTileLightDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool ShouldUseClusterLightDebugView(bool hasLocalDeferredTargets)
        {
            return hasLocalDeferredTargets && IsClusterLightDebugMode(BurtShadingDebugSettings.Mode);
        }

        public static bool IsTileLightDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.TileLightCount ||
                mode == BurtShadingDebugMode.TileLightOccupancy;
        }

        public static bool IsClusterLightDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.ClusterLightCount ||
                mode == BurtShadingDebugMode.ClusterLightOccupancy;
        }

        public static int ResolveShaderDebugMode()
        {
            switch (BurtShadingDebugSettings.Mode)
            {
                case BurtShadingDebugMode.TileLightCount:
                    return 1;
                case BurtShadingDebugMode.TileLightOccupancy:
                    return 2;
                default:
                    return 0;
            }
        }

        public static string ResolveGpuPathLabel()
        {
            return UseCpuDebugColorTextureFallback ? "CpuTextureFallback" : "StructuredBuffer";
        }
    }

    internal sealed class BurtBuildTileLightListPass : BurtRenderPass
    {
        private const float LocalLightCullingPaddingMin = 0.02f;
        private const float LocalLightCullingPaddingScale = 0.01f;

        private uint[] tileLightCountData;
        private uint[] tileLightListData;
        private BurtTileLightOffsetRange[] tileLightOffsetData;
        private uint[] clusterLightCountData;
        private uint[] clusterLightListData;
        private BurtTileLightOffsetRange[] clusterLightOffsetData;
        private bool[] clusterHasShadowLightData;
        private int clusterLightListUploadCount;
        private uint shadowedAdditionalLightMask;
        private byte[] punctualTileBinClassifications;
        private uint[] punctualTileIdData;
        private readonly int[] punctualTileBinOffsets = new int[BurtTiledLightData.PunctualTileBinCount];
        private readonly int[] punctualTileBinCounts = new int[BurtTiledLightData.PunctualTileBinCount];
        private readonly int[] punctualTileBinWriteOffsets = new int[BurtTiledLightData.PunctualTileBinCount];
        private int punctualTileHitCount;

        public override string Name => "Burt Build Tile Light List";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!ShouldBuild(builder.Request, builder.Asset, builder.ResourceRegistry))
            {
                return;
            }

            var shouldBuildTileData = ShouldBuildTileData(builder.Request, builder.Asset, builder.ResourceRegistry);
            var shouldBuildListData = ShouldBuildListData(builder.Request, builder.Asset, builder.ResourceRegistry);
            var shouldBuildClusterData = ShouldBuildClusterData(builder.Request, builder.Asset, builder.ResourceRegistry);
            builder.ReadLightingGlobals();
            if (shouldBuildTileData)
            {
                builder.WriteTileLightCountBuffer();
            }

            if (shouldBuildListData)
            {
                builder.WriteTileLightListBuffer();
                builder.WriteTileLightOffsetBuffer();
            }

            if (shouldBuildClusterData)
            {
                builder.WriteClusterLightCountBuffer();
                builder.WriteClusterLightListBuffer();
                builder.WriteClusterLightOffsetBuffer();
                if (builder.ResourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.PunctualTileIdBufferName))
                {
                    builder.WritePunctualTileIdBuffer();
                }
            }
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !ShouldBuild(context.Request, context.Asset, context.ResourceRegistry))
            {
                return;
            }

            var request = context.Request;
            var lightingData = request != null ? request.LightingData : null;
            if (lightingData == null)
            {
                UploadDisabledGlobals(context);
                return;
            }

            var layout = BurtTiledLightData.CalculateLayout(request.Camera);
            var clusterLayout = BurtTiledLightData.CalculateClusterLayout(request.Camera);
            var shouldBuildTileData = ShouldBuildTileData(request, context.Asset, context.ResourceRegistry);
            var shouldBuildListData = ShouldBuildListData(request, context.Asset, context.ResourceRegistry);
            var shouldBuildClusterData = ShouldBuildClusterData(request, context.Asset, context.ResourceRegistry);
            var useRuntimeTileList = BurtTiledLightData.ShouldUseRuntimeTiledLightingResources(request, context.Asset, shouldBuildListData);
            var maxLightsPerTile = BurtTiledLightData.ResolveMaxLightsPerTile(useRuntimeTileList);
            var maxLightsPerCluster = ResolveMaxLightsPerCluster(lightingData);
            EnsureCapacity(layout, shouldBuildTileData, shouldBuildListData, maxLightsPerTile, clusterLayout, shouldBuildClusterData, maxLightsPerCluster);
            ClearWorkingData(layout, shouldBuildTileData, shouldBuildListData, clusterLayout, shouldBuildClusterData);
            BuildCpuTileLightLists(request.Camera, lightingData, layout, shouldBuildTileData, shouldBuildListData, maxLightsPerTile, shouldBuildClusterData, clusterLayout, maxLightsPerCluster);
            BuildPunctualTileBins(clusterLayout, shouldBuildClusterData);
            var stats = FinalizeTileMetadata(layout, shouldBuildTileData, shouldBuildListData, maxLightsPerTile);
            var clusterStats = FinalizeClusterMetadata(clusterLayout, shouldBuildClusterData, maxLightsPerCluster);

            var countBuffer = context.TileLightCountBuffer;
            var listBuffer = context.TileLightListBuffer;
            var offsetBuffer = context.TileLightOffsetBuffer;
            var uploaded = UploadBuffers(countBuffer, listBuffer, offsetBuffer, layout, shouldBuildTileData, shouldBuildListData, maxLightsPerTile);
            var clusterUploaded = UploadClusterBuffers(context.ClusterLightCountBuffer, context.ClusterLightListBuffer, context.ClusterLightOffsetBuffer, clusterLayout, shouldBuildClusterData, maxLightsPerCluster);
            var punctualTileIdsUploaded = UploadPunctualTileIds(context.PunctualTileIdBuffer, shouldBuildClusterData);

            lightingData.SetTileLightDebugState(
                shouldBuildTileData,
                uploaded,
                useRuntimeTileList ? BurtTiledLightData.RuntimeBuildModeLabel : BurtTiledLightData.DebugBuildModeLabel,
                shouldBuildTileData ? layout.TileSize : 0,
                shouldBuildTileData ? layout.TileCountX : 0,
                shouldBuildTileData ? layout.TileCountY : 0,
                shouldBuildTileData ? layout.TileCount : 0,
                maxLightsPerTile,
                shouldBuildListData ? layout.TileCount * maxLightsPerTile : 0,
                stats.MinCount,
                stats.MaxCount,
                stats.AverageCount,
                stats.OverflowTileCount,
                stats.MaxOverflowExtraCount);
            lightingData.SetTileLightDebugCountSnapshot(shouldBuildTileData ? tileLightCountData : null, shouldBuildTileData ? layout.TileCount : 0);
            lightingData.SetClusterLightDebugCountSnapshot(shouldBuildClusterData ? clusterLightCountData : null, shouldBuildClusterData ? clusterLayout.ClusterCount : 0);
            lightingData.SetClusterLightState(
                clusterUploaded,
                clusterLayout.TileSize,
                clusterLayout.TileCountX,
                clusterLayout.TileCountY,
                clusterLayout.DepthSliceCount,
                clusterLayout.ClusterCount,
                maxLightsPerCluster,
                clusterUploaded ? clusterLightListUploadCount : 0,
                clusterStats.MinCount,
                clusterStats.MaxCount,
                clusterStats.AverageCount,
                clusterStats.OverflowTileCount,
                clusterStats.MaxOverflowExtraCount,
                request.Camera != null ? request.Camera.nearClipPlane : 0.0001f,
                request.Camera != null ? request.Camera.farClipPlane : 1f,
                CreateWorldToViewZRow(request.Camera));
            lightingData.SetPunctualTileDrawState(
                clusterUploaded && punctualTileIdsUploaded,
                clusterLayout.TileCount,
                punctualTileHitCount,
                punctualTileBinOffsets,
                punctualTileBinCounts);

            UploadGlobals(context, countBuffer, listBuffer, offsetBuffer, layout, stats, uploaded, shouldBuildTileData, shouldBuildListData, maxLightsPerTile);
            UploadClusterGlobals(context, clusterLayout, clusterUploaded, shouldBuildClusterData, maxLightsPerCluster);
        }

        private static bool ShouldBuild(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            return ShouldBuildTileData(request, asset, resourceRegistry) ||
                ShouldBuildClusterData(request, asset, resourceRegistry);
        }

        private static bool ShouldBuildTileData(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var hasTileResources = resourceRegistry != null && resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName);
            return BurtTiledLightData.ShouldUseTiledLightResources(request, asset, hasTileResources);
        }

        private static bool ShouldBuildListData(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var hasListResources = resourceRegistry != null &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightListBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightOffsetBufferName);
            return BurtTiledLightData.ShouldUseTileLightListResources(request, asset, hasListResources);
        }

        private static bool ShouldBuildClusterData(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRenderGraphResourceRegistry resourceRegistry)
        {
            var hasClusterResources = resourceRegistry != null &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightListBufferName) &&
                resourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightOffsetBufferName);
            return BurtTiledLightData.ShouldUseClusterLightResources(request, asset, hasClusterResources);
        }

        private void EnsureCapacity(
            BurtTileLightLayout layout,
            bool includeTileData,
            bool includeListData,
            int maxLightsPerTile,
            BurtClusterLightLayout clusterLayout,
            bool includeClusterData,
            int maxLightsPerCluster)
        {
            var tileCount = Mathf.Max(1, layout.TileCount);

            if (includeTileData && (tileLightCountData == null || tileLightCountData.Length < tileCount))
            {
                tileLightCountData = new uint[tileCount];
            }

            if (includeListData)
            {
                var listCapacity = tileCount * maxLightsPerTile;
                if (tileLightListData == null || tileLightListData.Length < listCapacity)
                {
                    tileLightListData = new uint[listCapacity];
                }

                if (tileLightOffsetData == null || tileLightOffsetData.Length < tileCount)
                {
                    tileLightOffsetData = new BurtTileLightOffsetRange[tileCount];
                }
            }

            if (!includeClusterData)
            {
                return;
            }

            var clusterCount = Mathf.Max(1, clusterLayout.ClusterCount);
            if (clusterLightCountData == null || clusterLightCountData.Length < clusterCount)
            {
                clusterLightCountData = new uint[clusterCount];
            }

            var clusterListCapacity = Mathf.Max(1, clusterCount * maxLightsPerCluster);
            if (clusterLightListData == null || clusterLightListData.Length < clusterListCapacity)
            {
                clusterLightListData = new uint[clusterListCapacity];
            }

            if (clusterLightOffsetData == null || clusterLightOffsetData.Length < clusterCount)
            {
                clusterLightOffsetData = new BurtTileLightOffsetRange[clusterCount];
            }

            if (clusterHasShadowLightData == null || clusterHasShadowLightData.Length < clusterCount)
            {
                clusterHasShadowLightData = new bool[clusterCount];
            }

            var punctualTileCount = Mathf.Max(1, clusterLayout.TileCount);
            if (punctualTileBinClassifications == null || punctualTileBinClassifications.Length < punctualTileCount)
            {
                punctualTileBinClassifications = new byte[punctualTileCount];
            }

            if (punctualTileIdData == null || punctualTileIdData.Length < punctualTileCount)
            {
                punctualTileIdData = new uint[punctualTileCount];
            }
        }

        private static int ResolveMaxLightsPerCluster(BurtLightingData lightingData)
        {
            var additionalLightCount = lightingData != null ? Mathf.Clamp(lightingData.AdditionalLightCount, 0, BurtLightingData.MaxAdditionalLights) : BurtLightingData.MaxAdditionalLights;
            if (additionalLightCount <= 0)
            {
                return 1;
            }

            return Mathf.Max(1, Mathf.Min(BurtTiledLightData.ResolveRuntimeMaxLightsPerCluster(), additionalLightCount));
        }

        private void ClearWorkingData(
            BurtTileLightLayout layout,
            bool includeTileData,
            bool includeListData,
            BurtClusterLightLayout clusterLayout,
            bool includeClusterData)
        {
            if (includeTileData)
            {
                Array.Clear(tileLightCountData, 0, layout.TileCount);
            }

            if (includeListData)
            {
                Array.Clear(tileLightOffsetData, 0, layout.TileCount);
            }

            if (includeClusterData)
            {
                Array.Clear(clusterLightCountData, 0, clusterLayout.ClusterCount);
                Array.Clear(clusterLightOffsetData, 0, clusterLayout.ClusterCount);
                Array.Clear(clusterHasShadowLightData, 0, clusterLayout.ClusterCount);
            }

            clusterLightListUploadCount = 0;
            shadowedAdditionalLightMask = 0u;
            punctualTileHitCount = 0;
            Array.Clear(punctualTileBinOffsets, 0, punctualTileBinOffsets.Length);
            Array.Clear(punctualTileBinCounts, 0, punctualTileBinCounts.Length);
            Array.Clear(punctualTileBinWriteOffsets, 0, punctualTileBinWriteOffsets.Length);
            if (includeClusterData && punctualTileBinClassifications != null)
            {
                Array.Clear(punctualTileBinClassifications, 0, clusterLayout.TileCount);
            }
        }

        private void BuildCpuTileLightLists(
            Camera camera,
            BurtLightingData lightingData,
            BurtTileLightLayout layout,
            bool includeTileData,
            bool includeListData,
            int maxLightsPerTile,
            bool includeClusterData,
            BurtClusterLightLayout clusterLayout,
            int maxLightsPerCluster)
        {
            var cullingContext = BurtTileLightCullingContext.Create(camera, layout);
            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);
            shadowedAdditionalLightMask = 0u;
            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                if (lightingData.AdditionalLightShadowData[lightIndex].x > 0.5f)
                {
                    shadowedAdditionalLightMask |= 1u << lightIndex;
                }
            }

            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                var colorAndType = lightingData.AdditionalLightColorAndType[lightIndex];
                if (IsDirectionalLight(colorAndType.w))
                {
                    AddLightToTileRect(lightIndex, 0, 0, layout.TileCountX - 1, layout.TileCountY - 1, layout, includeTileData, includeListData, maxLightsPerTile);
                    AddLightToClusterBounds(lightIndex, 0, 0, 0, clusterLayout.TileCountX - 1, clusterLayout.TileCountY - 1, clusterLayout.DepthSliceCount - 1, clusterLayout, includeClusterData, maxLightsPerCluster);
                    continue;
                }

                var positionAndRange = lightingData.AdditionalLightPositionAndRange[lightIndex];
                if (!TryProjectLocalLightBounds(camera, positionAndRange, layout, out var minTileX, out var minTileY, out var maxTileX, out var maxTileY))
                {
                    continue;
                }

                AddLocalLightToCandidateTiles(lightIndex, positionAndRange, minTileX, minTileY, maxTileX, maxTileY, layout, cullingContext, includeTileData, includeListData, maxLightsPerTile);
                if (includeClusterData && TryGetViewSphere(positionAndRange, cullingContext, out var viewSphere))
                {
                    var spotCone = default(BurtViewSpotCone);
                    var useSpotCone = IsSpotLight(colorAndType.w) && TryGetViewSpotCone(lightingData, lightIndex, cullingContext, out spotCone);
                    ResolveClusterDepthBounds(viewSphere, cullingContext, clusterLayout, out var minSlice, out var maxSlice);
                    AddLocalLightToCandidateClusters(lightIndex, viewSphere, useSpotCone, spotCone, minTileX, minTileY, maxTileX, maxTileY, minSlice, maxSlice, clusterLayout, cullingContext, maxLightsPerCluster);
                }
            }
        }

        private void AddLocalLightToCandidateTiles(
            int lightIndex,
            Vector4 positionAndRange,
            int minTileX,
            int minTileY,
            int maxTileX,
            int maxTileY,
            BurtTileLightLayout layout,
            BurtTileLightCullingContext cullingContext,
            bool includeTileData,
            bool includeListData,
            int maxLightsPerTile)
        {
            if (!includeTileData)
            {
                return;
            }

            if (!TryGetViewSphere(positionAndRange, cullingContext, out var viewSphere))
            {
                AddLightToTileRect(lightIndex, minTileX, minTileY, maxTileX, maxTileY, layout, includeTileData, includeListData, maxLightsPerTile);
                return;
            }

            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    if (!TileIntersectsViewSphere(tileX, tileY, cullingContext, viewSphere))
                    {
                        continue;
                    }

                    AddLightToTile(lightIndex, tileX, tileY, layout, includeTileData, includeListData, maxLightsPerTile);
                }
            }
        }

        private void AddLightToTileRect(int lightIndex, int minTileX, int minTileY, int maxTileX, int maxTileY, BurtTileLightLayout layout, bool includeTileData, bool includeListData, int maxLightsPerTile)
        {
            if (!includeTileData)
            {
                return;
            }

            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    AddLightToTile(lightIndex, tileX, tileY, layout, includeTileData, includeListData, maxLightsPerTile);
                }
            }
        }

        private void AddLightToTile(int lightIndex, int tileX, int tileY, BurtTileLightLayout layout, bool includeTileData, bool includeListData, int maxLightsPerTile)
        {
            if (!includeTileData)
            {
                return;
            }

            var tileIndex = tileY * layout.TileCountX + tileX;
            var currentCount = tileLightCountData[tileIndex];
            if (includeListData && currentCount < (uint)maxLightsPerTile)
            {
                tileLightListData[tileIndex * maxLightsPerTile + (int)currentCount] = (uint)lightIndex;
            }

            tileLightCountData[tileIndex] = currentCount + 1u;
        }

        private static void ResolveClusterDepthBounds(
            BurtViewSphere viewSphere,
            BurtTileLightCullingContext cullingContext,
            BurtClusterLightLayout clusterLayout,
            out int minSlice,
            out int maxSlice)
        {
            if (!cullingContext.IsValid || clusterLayout.DepthSliceCount <= 0)
            {
                minSlice = 0;
                maxSlice = Mathf.Max(0, clusterLayout.DepthSliceCount - 1);
                return;
            }

            var nearPlane = cullingContext.NearPlane;
            var farPlane = Mathf.Max(cullingContext.FarPlane, nearPlane + 0.0001f);
            var minDepth = Mathf.Max(viewSphere.Center.z - viewSphere.Radius, nearPlane);
            var maxDepth = Mathf.Min(viewSphere.Center.z + viewSphere.Radius, farPlane);
            if (maxDepth < nearPlane || minDepth > farPlane)
            {
                minSlice = 0;
                maxSlice = -1;
                return;
            }

            minSlice = BurtTiledLightData.CalculateClusterDepthSlice(minDepth, nearPlane, farPlane, clusterLayout.DepthSliceCount);
            maxSlice = BurtTiledLightData.CalculateClusterDepthSlice(maxDepth, nearPlane, farPlane, clusterLayout.DepthSliceCount);
            minSlice = Mathf.Max(0, minSlice - 1);
            maxSlice = Mathf.Min(clusterLayout.DepthSliceCount - 1, maxSlice + 1);
        }

        private void AddLocalLightToCandidateClusters(
            int lightIndex,
            BurtViewSphere viewSphere,
            bool useSpotCone,
            BurtViewSpotCone spotCone,
            int minTileX,
            int minTileY,
            int maxTileX,
            int maxTileY,
            int minSlice,
            int maxSlice,
            BurtClusterLightLayout clusterLayout,
            BurtTileLightCullingContext cullingContext,
            int maxLightsPerCluster)
        {
            if (maxTileX < minTileX || maxTileY < minTileY || maxSlice < minSlice)
            {
                return;
            }

            minTileX = Mathf.Clamp(minTileX - 1, 0, clusterLayout.TileCountX - 1);
            minTileY = Mathf.Clamp(minTileY - 1, 0, clusterLayout.TileCountY - 1);
            maxTileX = Mathf.Clamp(maxTileX + 1, 0, clusterLayout.TileCountX - 1);
            maxTileY = Mathf.Clamp(maxTileY + 1, 0, clusterLayout.TileCountY - 1);
            minSlice = Mathf.Clamp(minSlice, 0, clusterLayout.DepthSliceCount - 1);
            maxSlice = Mathf.Clamp(maxSlice, 0, clusterLayout.DepthSliceCount - 1);

            if (maxTileX < minTileX || maxTileY < minTileY || maxSlice < minSlice)
            {
                return;
            }

            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    for (var slice = minSlice; slice <= maxSlice; slice++)
                    {
                        if (!ClusterIntersectsViewSphere(tileX, tileY, slice, clusterLayout, cullingContext, viewSphere))
                        {
                            continue;
                        }

                        if (useSpotCone && !ClusterIntersectsSpotCone(tileX, tileY, slice, clusterLayout, cullingContext, spotCone))
                        {
                            continue;
                        }

                        AddLightToCluster(lightIndex, tileX, tileY, slice, clusterLayout, maxLightsPerCluster);
                    }
                }
            }
        }

        private void AddLightToClusterBounds(
            int lightIndex,
            int minTileX,
            int minTileY,
            int minSlice,
            int maxTileX,
            int maxTileY,
            int maxSlice,
            BurtClusterLightLayout clusterLayout,
            bool includeClusterData,
            int maxLightsPerCluster)
        {
            if (!includeClusterData)
            {
                return;
            }

            minTileX = Mathf.Clamp(minTileX, 0, clusterLayout.TileCountX - 1);
            minTileY = Mathf.Clamp(minTileY, 0, clusterLayout.TileCountY - 1);
            maxTileX = Mathf.Clamp(maxTileX, 0, clusterLayout.TileCountX - 1);
            maxTileY = Mathf.Clamp(maxTileY, 0, clusterLayout.TileCountY - 1);
            minSlice = Mathf.Clamp(minSlice, 0, clusterLayout.DepthSliceCount - 1);
            maxSlice = Mathf.Clamp(maxSlice, 0, clusterLayout.DepthSliceCount - 1);

            if (maxTileX < minTileX || maxTileY < minTileY || maxSlice < minSlice)
            {
                return;
            }

            for (var slice = minSlice; slice <= maxSlice; slice++)
            {
                for (var tileY = minTileY; tileY <= maxTileY; tileY++)
                {
                    for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                    {
                        AddLightToCluster(lightIndex, tileX, tileY, slice, clusterLayout, maxLightsPerCluster);
                    }
                }
            }
        }

        private void AddLightToCluster(int lightIndex, int tileX, int tileY, int depthSlice, BurtClusterLightLayout clusterLayout, int maxLightsPerCluster)
        {
            var tileIndex = tileY * clusterLayout.TileCountX + tileX;
            var clusterIndex = depthSlice * clusterLayout.TileCount + tileIndex;
            var currentCount = clusterLightCountData[clusterIndex];
            if (currentCount < (uint)maxLightsPerCluster)
            {
                clusterLightListData[clusterIndex * maxLightsPerCluster + (int)currentCount] = (uint)lightIndex;
            }

            clusterLightCountData[clusterIndex] = currentCount + 1u;
            if (lightIndex >= 0 &&
                lightIndex < 32 &&
                (shadowedAdditionalLightMask & (1u << lightIndex)) != 0u)
            {
                clusterHasShadowLightData[clusterIndex] = true;
            }
        }

        private void BuildPunctualTileBins(
            BurtClusterLightLayout layout,
            bool includeClusterData)
        {
            if (!includeClusterData ||
                clusterLightCountData == null ||
                clusterHasShadowLightData == null ||
                punctualTileBinClassifications == null ||
                punctualTileIdData == null)
            {
                return;
            }

            // Match XRender's CollectPunctualTileBinsJob: collapse each XY
            // cluster column across Z, keep the worst light count, and route
            // any column containing a shadowed light to the exclusive shadow bin.
            for (var tileIndex = 0; tileIndex < layout.TileCount; tileIndex++)
            {
                var maxLights = 0;
                var hasShadow = false;
                for (var depthSlice = 0; depthSlice < layout.DepthSliceCount; depthSlice++)
                {
                    var clusterIndex = depthSlice * layout.TileCount + tileIndex;
                    var rawCount = (int)clusterLightCountData[clusterIndex];
                    maxLights = Mathf.Max(maxLights, rawCount);
                    hasShadow |= clusterHasShadowLightData[clusterIndex];
                }

                if (maxLights <= 0)
                {
                    continue;
                }

                var binIndex = hasShadow
                    ? BurtTiledLightData.PunctualTileBinShadow
                    : maxLights >= 9
                        ? BurtTiledLightData.PunctualTileBin9Plus
                        : maxLights >= 3
                            ? BurtTiledLightData.PunctualTileBin3To8
                            : BurtTiledLightData.PunctualTileBin1To2;
                punctualTileBinClassifications[tileIndex] = (byte)(binIndex + 1);
                punctualTileBinCounts[binIndex]++;
                punctualTileHitCount++;
            }

            var runningOffset = 0;
            for (var binIndex = 0; binIndex < BurtTiledLightData.PunctualTileBinCount; binIndex++)
            {
                punctualTileBinOffsets[binIndex] = runningOffset;
                punctualTileBinWriteOffsets[binIndex] = runningOffset;
                runningOffset += punctualTileBinCounts[binIndex];
            }

            for (var tileIndex = 0; tileIndex < layout.TileCount; tileIndex++)
            {
                var classification = punctualTileBinClassifications[tileIndex];
                if (classification == 0)
                {
                    continue;
                }

                var binIndex = classification - 1;
                var tileX = tileIndex % layout.TileCountX;
                var tileY = tileIndex / layout.TileCountX;
                var packedTileId = (uint)tileX | ((uint)tileY << 16);
                punctualTileIdData[punctualTileBinWriteOffsets[binIndex]++] = packedTileId;
            }
        }

        private BurtTileLightStats FinalizeTileMetadata(BurtTileLightLayout layout, bool includeTileData, bool includeListData, int maxLightsPerTile)
        {
            var minCount = int.MaxValue;
            var maxCount = 0;
            var sumCount = 0L;
            var overflowTileCount = 0;
            var maxOverflowExtraCount = 0;

            if (!includeTileData || tileLightCountData == null)
            {
                return new BurtTileLightStats(0, 0, 0f, 0, 0);
            }

            for (var tileIndex = 0; tileIndex < layout.TileCount; tileIndex++)
            {
                var rawCount = (int)tileLightCountData[tileIndex];
                var clampedCount = Mathf.Min(rawCount, maxLightsPerTile);
                if (includeListData)
                {
                    tileLightOffsetData[tileIndex] = new BurtTileLightOffsetRange
                    {
                        Offset = (uint)(tileIndex * maxLightsPerTile),
                        Count = (uint)clampedCount
                    };
                }

                if (rawCount > maxLightsPerTile)
                {
                    overflowTileCount++;
                    maxOverflowExtraCount = Mathf.Max(maxOverflowExtraCount, rawCount - maxLightsPerTile);
                }

                minCount = Mathf.Min(minCount, rawCount);
                maxCount = Mathf.Max(maxCount, rawCount);
                sumCount += rawCount;
            }

            if (layout.TileCount <= 0)
            {
                minCount = 0;
            }

            var averageCount = layout.TileCount > 0 ? (float)sumCount / layout.TileCount : 0f;
            return new BurtTileLightStats(minCount == int.MaxValue ? 0 : minCount, maxCount, averageCount, overflowTileCount, maxOverflowExtraCount);
        }

        private BurtTileLightStats FinalizeClusterMetadata(BurtClusterLightLayout layout, bool includeClusterData, int maxLightsPerCluster)
        {
            var minCount = int.MaxValue;
            var maxCount = 0;
            var sumCount = 0L;
            var overflowClusterCount = 0;
            var maxOverflowExtraCount = 0;
            var packedWriteOffset = 0;

            if (!includeClusterData || clusterLightCountData == null)
            {
                clusterLightListUploadCount = 0;
                return new BurtTileLightStats(0, 0, 0f, 0, 0);
            }

            for (var clusterIndex = 0; clusterIndex < layout.ClusterCount; clusterIndex++)
            {
                var rawCount = (int)clusterLightCountData[clusterIndex];
                var clampedCount = Mathf.Min(rawCount, maxLightsPerCluster);
                var sourceOffset = clusterIndex * maxLightsPerCluster;
                clusterLightOffsetData[clusterIndex] = new BurtTileLightOffsetRange
                {
                    Offset = (uint)packedWriteOffset,
                    Count = (uint)clampedCount
                };

                if (clampedCount > 0)
                {
                    if (sourceOffset != packedWriteOffset)
                    {
                        Array.Copy(clusterLightListData, sourceOffset, clusterLightListData, packedWriteOffset, clampedCount);
                    }

                    packedWriteOffset += clampedCount;
                }

                if (rawCount > maxLightsPerCluster)
                {
                    overflowClusterCount++;
                    maxOverflowExtraCount = Mathf.Max(maxOverflowExtraCount, rawCount - maxLightsPerCluster);
                }

                minCount = Mathf.Min(minCount, rawCount);
                maxCount = Mathf.Max(maxCount, rawCount);
                sumCount += rawCount;
            }

            clusterLightListUploadCount = Mathf.Max(1, packedWriteOffset);
            var averageCount = layout.ClusterCount > 0 ? (float)sumCount / layout.ClusterCount : 0f;
            return new BurtTileLightStats(minCount == int.MaxValue ? 0 : minCount, maxCount, averageCount, overflowClusterCount, maxOverflowExtraCount);
        }

        private bool UploadBuffers(
            BurtRenderBufferHandle countBuffer,
            BurtRenderBufferHandle listBuffer,
            BurtRenderBufferHandle offsetBuffer,
            BurtTileLightLayout layout,
            bool includeTileData,
            bool includeListData,
            int maxLightsPerTile)
        {
            if (!includeTileData)
            {
                return false;
            }

            var countUploaded = countBuffer.IsValid && countBuffer.HasBuffer;

            if (countUploaded)
            {
                countBuffer.Buffer.SetData(tileLightCountData, 0, 0, layout.TileCount);
            }

            if (!includeListData)
            {
                return countUploaded;
            }

            var listUploaded = listBuffer.IsValid && listBuffer.HasBuffer;
            var offsetUploaded = offsetBuffer.IsValid && offsetBuffer.HasBuffer;
            if (listUploaded)
            {
                listBuffer.Buffer.SetData(tileLightListData, 0, 0, layout.TileCount * maxLightsPerTile);
            }

            if (offsetUploaded)
            {
                offsetBuffer.Buffer.SetData(tileLightOffsetData, 0, 0, layout.TileCount);
            }

            return countUploaded && listUploaded && offsetUploaded;
        }

        private bool UploadClusterBuffers(
            BurtRenderBufferHandle countBuffer,
            BurtRenderBufferHandle listBuffer,
            BurtRenderBufferHandle offsetBuffer,
            BurtClusterLightLayout layout,
            bool includeClusterData,
            int maxLightsPerCluster)
        {
            if (!includeClusterData)
            {
                return false;
            }

            var countUploaded = countBuffer.IsValid && countBuffer.HasBuffer;
            var listUploaded = listBuffer.IsValid && listBuffer.HasBuffer;
            var offsetUploaded = offsetBuffer.IsValid && offsetBuffer.HasBuffer;

            if (countUploaded)
            {
                countBuffer.Buffer.SetData(clusterLightCountData, 0, 0, layout.ClusterCount);
            }

            if (listUploaded)
            {
                var listUploadCount = Mathf.Min(clusterLightListUploadCount, clusterLightListData != null ? clusterLightListData.Length : 0);
                if (listUploadCount > 0)
                {
                    listBuffer.Buffer.SetData(clusterLightListData, 0, 0, listUploadCount);
                }
                else
                {
                    listUploaded = false;
                }
            }

            if (offsetUploaded)
            {
                offsetBuffer.Buffer.SetData(clusterLightOffsetData, 0, 0, layout.ClusterCount);
            }

            return countUploaded && listUploaded && offsetUploaded;
        }

        private bool UploadPunctualTileIds(BurtRenderBufferHandle buffer, bool includeClusterData)
        {
            if (!includeClusterData || !buffer.IsValid || !buffer.HasBuffer)
            {
                return false;
            }

            if (punctualTileHitCount > 0)
            {
                buffer.Buffer.SetData(punctualTileIdData, 0, 0, punctualTileHitCount);
            }

            return true;
        }

        private static void UploadGlobals(
            BurtRenderGraphContext context,
            BurtRenderBufferHandle countBuffer,
            BurtRenderBufferHandle listBuffer,
            BurtRenderBufferHandle offsetBuffer,
            BurtTileLightLayout layout,
            BurtTileLightStats stats,
            bool uploaded,
            bool includeTileData,
            bool includeListData,
            int maxLightsPerTile)
        {
            var cmd = context.AcquireCommandBuffer("Burt Upload Tile Light Globals");
            var tileListUsable = uploaded && includeListData;
            var additionalLightCount = context != null && context.Request != null && context.Request.LightingData != null
                ? context.Request.LightingData.AdditionalLightCount
                : 0;
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, tileListUsable ? 1f : 0f);
            cmd.SetGlobalVector(
                BurtTiledLightData.TileLightGridParamsId,
                includeTileData ? new Vector4(layout.TileCountX, layout.TileCountY, layout.TileSize, maxLightsPerTile) : Vector4.zero);
            cmd.SetGlobalVector(BurtTiledLightData.TileLightDebugStatsId, new Vector4(stats.MinCount, stats.MaxCount, stats.AverageCount, additionalLightCount));

            if (includeTileData && countBuffer.IsValid && countBuffer.HasBuffer)
            {
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightCountBufferId, countBuffer.Buffer);
            }

            if (includeListData && listBuffer.IsValid && listBuffer.HasBuffer)
            {
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightListBufferId, listBuffer.Buffer);
            }

            if (includeListData && offsetBuffer.IsValid && offsetBuffer.HasBuffer)
            {
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightOffsetBufferId, offsetBuffer.Buffer);
            }

            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static void UploadClusterGlobals(
            BurtRenderGraphContext context,
            BurtClusterLightLayout layout,
            bool uploaded,
            bool includeClusterData,
            int maxLightsPerCluster)
        {
            if (context == null)
            {
                return;
            }

            var camera = context.Request != null ? context.Request.Camera : null;
            var nearPlane = camera != null ? Mathf.Max(camera.nearClipPlane, 0.0001f) : 0.0001f;
            var farPlane = camera != null ? Mathf.Max(camera.farClipPlane, nearPlane + 0.0001f) : 1f;
            var invDepthRange = BurtTiledLightData.CalculateClusterInvLogDepthRange(nearPlane, farPlane);
            var clusterListUsable = uploaded && includeClusterData;
            var cmd = context.AcquireCommandBuffer("Burt Upload Cluster Light Globals");
            cmd.SetGlobalFloat(BurtTiledLightData.ClusterLightBufferEnabledId, clusterListUsable ? 1f : 0f);
            cmd.SetGlobalVector(BurtTiledLightData.ClusterLightGridParamsId, new Vector4(layout.TileCountX, layout.TileCountY, layout.DepthSliceCount, maxLightsPerCluster));
            cmd.SetGlobalVector(BurtTiledLightData.ClusterLightDepthParamsId, new Vector4(nearPlane, farPlane, invDepthRange, layout.DepthSliceCount));
            cmd.SetGlobalVector(BurtTiledLightData.ClusterLightWorldToViewZId, CreateWorldToViewZRow(camera));

            if (clusterListUsable)
            {
                var countBuffer = context.ClusterLightCountBuffer;
                var listBuffer = context.ClusterLightListBuffer;
                var offsetBuffer = context.ClusterLightOffsetBuffer;
                if (countBuffer.IsValid && countBuffer.HasBuffer)
                {
                    cmd.SetGlobalBuffer(BurtTiledLightData.ClusterLightCountBufferId, countBuffer.Buffer);
                }

                if (listBuffer.IsValid && listBuffer.HasBuffer)
                {
                    cmd.SetGlobalBuffer(BurtTiledLightData.ClusterLightListBufferId, listBuffer.Buffer);
                }

                if (offsetBuffer.IsValid && offsetBuffer.HasBuffer)
                {
                    cmd.SetGlobalBuffer(BurtTiledLightData.ClusterLightOffsetBufferId, offsetBuffer.Buffer);
                }
            }

            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static void UploadDisabledGlobals(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer("Burt Disable Tile Light Globals");
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, 0f);
            cmd.SetGlobalVector(BurtTiledLightData.TileLightGridParamsId, Vector4.zero);
            cmd.SetGlobalVector(BurtTiledLightData.TileLightDebugStatsId, Vector4.zero);
            cmd.SetGlobalFloat(BurtTiledLightData.ClusterLightBufferEnabledId, 0f);
            cmd.SetGlobalVector(BurtTiledLightData.ClusterLightGridParamsId, Vector4.zero);
            cmd.SetGlobalVector(BurtTiledLightData.ClusterLightDepthParamsId, Vector4.zero);
            cmd.SetGlobalVector(BurtTiledLightData.ClusterLightWorldToViewZId, Vector4.zero);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static Vector4 CreateWorldToViewZRow(Camera camera)
        {
            if (camera == null)
            {
                return new Vector4(0f, 0f, 1f, 0f);
            }

            var worldToCamera = camera.worldToCameraMatrix;
            return new Vector4(-worldToCamera.m20, -worldToCamera.m21, -worldToCamera.m22, -worldToCamera.m23);
        }

        private static bool TryProjectLocalLightBounds(
            Camera camera,
            Vector4 positionAndRange,
            BurtTileLightLayout layout,
            out int minTileX,
            out int minTileY,
            out int maxTileX,
            out int maxTileY)
        {
            minTileX = 0;
            minTileY = 0;
            maxTileX = 0;
            maxTileY = 0;

            if (camera == null)
            {
                return false;
            }

            var range = Mathf.Max(positionAndRange.w, 0f);
            if (range <= 0.0001f)
            {
                return false;
            }

            range += CalculateLocalLightCullingPadding(range);
            var worldPosition = new Vector3(positionAndRange.x, positionAndRange.y, positionAndRange.z);
            var viewPosition = camera.worldToCameraMatrix.MultiplyPoint(worldPosition);
            if (!IsFinite(viewPosition.x) || !IsFinite(viewPosition.y) || !IsFinite(viewPosition.z))
            {
                return false;
            }

            var viewDepth = -viewPosition.z;
            var nearPlane = Mathf.Max(camera.nearClipPlane, 0.0001f);

            if (viewDepth + range <= nearPlane)
            {
                return false;
            }

            float minU;
            float minV;
            float maxU;
            float maxV;

            if (viewDepth <= nearPlane)
            {
                minU = 0f;
                minV = 0f;
                maxU = 1f;
                maxV = 1f;
            }
            else
            {
                var viewportPosition = camera.WorldToViewportPoint(worldPosition);
                if (!IsFinite(viewportPosition.x) || !IsFinite(viewportPosition.y))
                {
                    return false;
                }

                float radiusU;
                float radiusV;
                if (camera.orthographic)
                {
                    var halfHeight = Mathf.Max(camera.orthographicSize, 0.0001f);
                    var halfWidth = Mathf.Max(halfHeight * camera.aspect, 0.0001f);
                    radiusU = range / (halfWidth * 2f);
                    radiusV = range / (halfHeight * 2f);
                    minU = viewportPosition.x - radiusU;
                    maxU = viewportPosition.x + radiusU;
                    minV = viewportPosition.y - radiusV;
                    maxV = viewportPosition.y + radiusV;
                }
                else
                {
                    var projection = camera.projectionMatrix;
                    if (Mathf.Abs(projection.m00) <= 0.0001f ||
                        Mathf.Abs(projection.m11) <= 0.0001f ||
                        !IsFinite(projection.m00) ||
                        !IsFinite(projection.m02) ||
                        !IsFinite(projection.m11) ||
                        !IsFinite(projection.m12))
                    {
                        minU = 0f;
                        minV = 0f;
                        maxU = 1f;
                        maxV = 1f;
                    }
                    else
                    {
                        var nearDepth = Mathf.Max(viewDepth - range, nearPlane);
                        var farDepth = Mathf.Max(viewDepth + range, nearDepth);
                        CalculateConservativeSlopeRange(viewPosition.x, range, nearDepth, farDepth, out var minXSlope, out var maxXSlope);
                        CalculateConservativeSlopeRange(viewPosition.y, range, nearDepth, farDepth, out var minYSlope, out var maxYSlope);

                        minU = 0.5f * (projection.m00 * minXSlope - projection.m02 + 1f);
                        maxU = 0.5f * (projection.m00 * maxXSlope - projection.m02 + 1f);
                        minV = 0.5f * (projection.m11 * minYSlope - projection.m12 + 1f);
                        maxV = 0.5f * (projection.m11 * maxYSlope - projection.m12 + 1f);
                        if (minU > maxU)
                        {
                            var swap = minU;
                            minU = maxU;
                            maxU = swap;
                        }

                        if (minV > maxV)
                        {
                            var swap = minV;
                            minV = maxV;
                            maxV = swap;
                        }
                    }
                }
            }

            if (maxU <= 0f || minU >= 1f || maxV <= 0f || minV >= 1f)
            {
                return false;
            }

            minU = Mathf.Clamp01(minU);
            maxU = Mathf.Clamp01(maxU);
            minV = Mathf.Clamp01(minV);
            maxV = Mathf.Clamp01(maxV);

            minTileX = Mathf.Clamp(Mathf.FloorToInt(minU * layout.TileCountX) - 1, 0, layout.TileCountX - 1);
            minTileY = Mathf.Clamp(Mathf.FloorToInt(minV * layout.TileCountY) - 1, 0, layout.TileCountY - 1);
            maxTileX = Mathf.Clamp(Mathf.CeilToInt(maxU * layout.TileCountX), 0, layout.TileCountX - 1);
            maxTileY = Mathf.Clamp(Mathf.CeilToInt(maxV * layout.TileCountY), 0, layout.TileCountY - 1);

            return maxTileX >= minTileX && maxTileY >= minTileY;
        }

        private static void CalculateConservativeSlopeRange(float center, float radius, float nearDepth, float farDepth, out float minSlope, out float maxSlope)
        {
            var minPosition = center - radius;
            var maxPosition = center + radius;
            var safeNearDepth = Mathf.Max(nearDepth, 0.0001f);
            var safeFarDepth = Mathf.Max(farDepth, safeNearDepth);
            var slope0 = minPosition / safeNearDepth;
            var slope1 = minPosition / safeFarDepth;
            var slope2 = maxPosition / safeNearDepth;
            var slope3 = maxPosition / safeFarDepth;
            minSlope = Mathf.Min(Mathf.Min(slope0, slope1), Mathf.Min(slope2, slope3));
            maxSlope = Mathf.Max(Mathf.Max(slope0, slope1), Mathf.Max(slope2, slope3));
        }

        private static bool TryGetViewSphere(Vector4 positionAndRange, BurtTileLightCullingContext cullingContext, out BurtViewSphere viewSphere)
        {
            viewSphere = default;
            if (!cullingContext.IsValid)
            {
                return false;
            }

            var worldPosition = new Vector3(positionAndRange.x, positionAndRange.y, positionAndRange.z);
            var cameraViewPosition = cullingContext.WorldToCameraMatrix.MultiplyPoint(worldPosition);
            var radius = Mathf.Max(positionAndRange.w, 0f);
            radius += CalculateLocalLightCullingPadding(radius);
            if (!IsFinite(cameraViewPosition.x) || !IsFinite(cameraViewPosition.y) || !IsFinite(cameraViewPosition.z) || radius <= 0.0001f)
            {
                return false;
            }

            viewSphere = new BurtViewSphere(new Vector3(cameraViewPosition.x, cameraViewPosition.y, -cameraViewPosition.z), radius);
            return true;
        }

        private static bool TryGetViewSpotCone(BurtLightingData lightingData, int lightIndex, BurtTileLightCullingContext cullingContext, out BurtViewSpotCone spotCone)
        {
            spotCone = default;
            if (lightingData == null || !cullingContext.IsValid || lightIndex < 0 || lightIndex >= lightingData.AdditionalLightCount)
            {
                return false;
            }

            var positionAndRange = lightingData.AdditionalLightPositionAndRange[lightIndex];
            var worldPosition = new Vector3(positionAndRange.x, positionAndRange.y, positionAndRange.z);
            var cameraViewPosition = cullingContext.WorldToCameraMatrix.MultiplyPoint(worldPosition);
            if (!IsFinite(cameraViewPosition.x) || !IsFinite(cameraViewPosition.y) || !IsFinite(cameraViewPosition.z))
            {
                return false;
            }

            var directionAndSpot = lightingData.AdditionalLightDirectionAndSpot[lightIndex];
            var worldDirection = new Vector3(directionAndSpot.x, directionAndSpot.y, directionAndSpot.z);
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            var cameraViewDirection = cullingContext.WorldToCameraMatrix.MultiplyVector(worldDirection.normalized);
            var viewDirection = new Vector3(cameraViewDirection.x, cameraViewDirection.y, -cameraViewDirection.z);
            if (!IsFinite(viewDirection.x) || !IsFinite(viewDirection.y) || !IsFinite(viewDirection.z) || viewDirection.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            var spotParams = lightingData.AdditionalLightSpotParams[lightIndex];
            var outerCos = Mathf.Clamp(spotParams.y, -0.9999f, 0.9999f);
            var halfAngle = Mathf.Acos(outerCos);
            var range = Mathf.Max(positionAndRange.w, 0.0001f);
            range += CalculateLocalLightCullingPadding(range);
            var apex = new Vector3(cameraViewPosition.x, cameraViewPosition.y, -cameraViewPosition.z);
            spotCone = new BurtViewSpotCone(apex, viewDirection.normalized, range, Mathf.Tan(halfAngle));
            return true;
        }

        private static float CalculateLocalLightCullingPadding(float range)
        {
            return Mathf.Max(LocalLightCullingPaddingMin, Mathf.Max(range, 0f) * LocalLightCullingPaddingScale);
        }

        private static bool TileIntersectsViewSphere(int tileX, int tileY, BurtTileLightCullingContext cullingContext, BurtViewSphere viewSphere)
        {
            if (!cullingContext.IsValid)
            {
                return true;
            }

            return cullingContext.Orthographic
                ? OrthographicTileIntersectsViewSphere(tileX, tileY, cullingContext, viewSphere)
                : PerspectiveTileIntersectsViewSphere(tileX, tileY, cullingContext, viewSphere);
        }

        private static bool PerspectiveTileIntersectsViewSphere(int tileX, int tileY, BurtTileLightCullingContext cullingContext, BurtViewSphere viewSphere)
        {
            if (viewSphere.Center.magnitude <= viewSphere.Radius)
            {
                return true;
            }

            var u0 = (float)tileX / cullingContext.TileCountX;
            var u1 = (float)(tileX + 1) / cullingContext.TileCountX;
            var v0 = (float)tileY / cullingContext.TileCountY;
            var v1 = (float)(tileY + 1) / cullingContext.TileCountY;

            var xSlope0 = cullingContext.CalculateViewXSlope(u0);
            var xSlope1 = cullingContext.CalculateViewXSlope(u1);
            var ySlope0 = cullingContext.CalculateViewYSlope(v0);
            var ySlope1 = cullingContext.CalculateViewYSlope(v1);
            var leftSlope = Mathf.Min(xSlope0, xSlope1);
            var rightSlope = Mathf.Max(xSlope0, xSlope1);
            var bottomSlope = Mathf.Min(ySlope0, ySlope1);
            var topSlope = Mathf.Max(ySlope0, ySlope1);

            if (SphereOutsidePlane(viewSphere, new Vector3(1f, 0f, -leftSlope)))
            {
                return false;
            }

            if (SphereOutsidePlane(viewSphere, new Vector3(-1f, 0f, rightSlope)))
            {
                return false;
            }

            if (SphereOutsidePlane(viewSphere, new Vector3(0f, 1f, -bottomSlope)))
            {
                return false;
            }

            if (SphereOutsidePlane(viewSphere, new Vector3(0f, -1f, topSlope)))
            {
                return false;
            }

            return viewSphere.Center.z + viewSphere.Radius >= cullingContext.NearPlane;
        }

        private static bool OrthographicTileIntersectsViewSphere(int tileX, int tileY, BurtTileLightCullingContext cullingContext, BurtViewSphere viewSphere)
        {
            var u0 = (float)tileX / cullingContext.TileCountX;
            var u1 = (float)(tileX + 1) / cullingContext.TileCountX;
            var v0 = (float)tileY / cullingContext.TileCountY;
            var v1 = (float)(tileY + 1) / cullingContext.TileCountY;
            var minX = Mathf.Lerp(-cullingContext.OrthographicHalfWidth, cullingContext.OrthographicHalfWidth, u0);
            var maxX = Mathf.Lerp(-cullingContext.OrthographicHalfWidth, cullingContext.OrthographicHalfWidth, u1);
            var minY = Mathf.Lerp(-cullingContext.OrthographicHalfHeight, cullingContext.OrthographicHalfHeight, v0);
            var maxY = Mathf.Lerp(-cullingContext.OrthographicHalfHeight, cullingContext.OrthographicHalfHeight, v1);
            var closestX = Mathf.Clamp(viewSphere.Center.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
            var closestY = Mathf.Clamp(viewSphere.Center.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
            var dx = viewSphere.Center.x - closestX;
            var dy = viewSphere.Center.y - closestY;
            return dx * dx + dy * dy <= viewSphere.Radius * viewSphere.Radius;
        }

        private static bool ClusterIntersectsViewSphere(
            int tileX,
            int tileY,
            int depthSlice,
            BurtClusterLightLayout clusterLayout,
            BurtTileLightCullingContext cullingContext,
            BurtViewSphere viewSphere)
        {
            if (!cullingContext.IsValid || clusterLayout.DepthSliceCount <= 0)
            {
                return true;
            }

            CalculateClusterViewBounds(tileX, tileY, depthSlice, clusterLayout, cullingContext, out var min, out var max);
            return SphereIntersectsViewAabb(viewSphere.Center, viewSphere.Radius, min, max);
        }

        private static bool ClusterIntersectsSpotCone(int tileX, int tileY, int depthSlice, BurtClusterLightLayout clusterLayout, BurtTileLightCullingContext cullingContext, BurtViewSpotCone spotCone)
        {
            if (!cullingContext.IsValid || clusterLayout.DepthSliceCount <= 0)
            {
                return true;
            }

            CalculateClusterViewBounds(tileX, tileY, depthSlice, clusterLayout, cullingContext, out var min, out var max);
            if (ViewAabbContainsPoint(min, max, spotCone.Apex))
            {
                return true;
            }

            if (!SphereIntersectsViewAabb(spotCone.Apex, spotCone.Range, min, max))
            {
                return false;
            }

            if (SpotConeAxisIntersectsViewAabb(spotCone, min, max))
            {
                return true;
            }

            return ViewAabbIntersectsSpotCone(min, max, spotCone);
        }

        private static void CalculateClusterViewBounds(
            int tileX,
            int tileY,
            int depthSlice,
            BurtClusterLightLayout clusterLayout,
            BurtTileLightCullingContext cullingContext,
            out Vector3 min,
            out Vector3 max)
        {
            var nearPlane = cullingContext.NearPlane;
            var farPlane = Mathf.Max(cullingContext.FarPlane, nearPlane + 0.0001f);
            var depth0 = BurtTiledLightData.CalculateClusterSliceDepth(nearPlane, farPlane, depthSlice, clusterLayout.DepthSliceCount);
            var depth1 = BurtTiledLightData.CalculateClusterSliceDepth(nearPlane, farPlane, depthSlice + 1, clusterLayout.DepthSliceCount);
            var u0 = (float)tileX / Mathf.Max(1, clusterLayout.TileCountX);
            var u1 = (float)(tileX + 1) / Mathf.Max(1, clusterLayout.TileCountX);
            var v0 = (float)tileY / Mathf.Max(1, clusterLayout.TileCountY);
            var v1 = (float)(tileY + 1) / Mathf.Max(1, clusterLayout.TileCountY);

            min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            EncapsulateClusterCorner(cullingContext, u0, v0, depth0, ref min, ref max);
            EncapsulateClusterCorner(cullingContext, u1, v0, depth0, ref min, ref max);
            EncapsulateClusterCorner(cullingContext, u0, v1, depth0, ref min, ref max);
            EncapsulateClusterCorner(cullingContext, u1, v1, depth0, ref min, ref max);
            EncapsulateClusterCorner(cullingContext, u0, v0, depth1, ref min, ref max);
            EncapsulateClusterCorner(cullingContext, u1, v0, depth1, ref min, ref max);
            EncapsulateClusterCorner(cullingContext, u0, v1, depth1, ref min, ref max);
            EncapsulateClusterCorner(cullingContext, u1, v1, depth1, ref min, ref max);
        }

        private static void EncapsulateClusterCorner(BurtTileLightCullingContext cullingContext, float u, float v, float depth, ref Vector3 min, ref Vector3 max)
        {
            Vector3 point;
            if (cullingContext.Orthographic)
            {
                point = new Vector3(
                    Mathf.Lerp(-cullingContext.OrthographicHalfWidth, cullingContext.OrthographicHalfWidth, u),
                    Mathf.Lerp(-cullingContext.OrthographicHalfHeight, cullingContext.OrthographicHalfHeight, v),
                    depth);
            }
            else
            {
                point = new Vector3(
                    cullingContext.CalculateViewXSlope(u) * depth,
                    cullingContext.CalculateViewYSlope(v) * depth,
                    depth);
            }

            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        private static bool ViewAabbIntersectsSpotCone(Vector3 min, Vector3 max, BurtViewSpotCone spotCone)
        {
            for (var z = 0; z < 2; z++)
            {
                for (var y = 0; y < 2; y++)
                {
                    for (var x = 0; x < 2; x++)
                    {
                        if (PointInsideSpotCone(new Vector3(x == 0 ? min.x : max.x, y == 0 ? min.y : max.y, z == 0 ? min.z : max.z), spotCone))
                        {
                            return true;
                        }
                    }
                }
            }

            if (ViewAabbEdgesIntersectSpotCone(min, max, spotCone))
            {
                return true;
            }

            var center = (min + max) * 0.5f;
            var axisDistance = Mathf.Clamp(Vector3.Dot(center - spotCone.Apex, spotCone.Direction), 0f, spotCone.Range);
            var axisPoint = spotCone.Apex + spotCone.Direction * axisDistance;
            var closest = ClampPointToViewAabb(axisPoint, min, max);
            return PointInsideSpotCone(closest, spotCone);
        }

        private static bool PointInsideSpotCone(Vector3 point, BurtViewSpotCone spotCone)
        {
            var toPoint = point - spotCone.Apex;
            var distanceSq = toPoint.sqrMagnitude;
            if (distanceSq > spotCone.Range * spotCone.Range)
            {
                return false;
            }

            var axisDistance = Vector3.Dot(toPoint, spotCone.Direction);
            if (axisDistance < 0f || axisDistance > spotCone.Range)
            {
                return false;
            }

            var radialSq = Mathf.Max(0f, distanceSq - axisDistance * axisDistance);
            var coneRadius = axisDistance * spotCone.TanHalfAngle;
            return radialSq <= coneRadius * coneRadius + 0.0001f;
        }

        private static bool ViewAabbContainsPoint(Vector3 min, Vector3 max, Vector3 point)
        {
            return point.x >= min.x && point.x <= max.x &&
                point.y >= min.y && point.y <= max.y &&
                point.z >= min.z && point.z <= max.z;
        }

        private static bool SphereIntersectsViewAabb(Vector3 center, float radius, Vector3 min, Vector3 max)
        {
            var closest = ClampPointToViewAabb(center, min, max);
            var delta = closest - center;
            return delta.sqrMagnitude <= radius * radius;
        }

        private static Vector3 ClampPointToViewAabb(Vector3 point, Vector3 min, Vector3 max)
        {
            return new Vector3(
                Mathf.Clamp(point.x, min.x, max.x),
                Mathf.Clamp(point.y, min.y, max.y),
                Mathf.Clamp(point.z, min.z, max.z));
        }

        private static bool SpotConeAxisIntersectsViewAabb(BurtViewSpotCone spotCone, Vector3 min, Vector3 max)
        {
            var end = spotCone.Apex + spotCone.Direction * spotCone.Range;
            return SegmentIntersectsViewAabb(spotCone.Apex, end, min, max);
        }

        private static bool ViewAabbEdgesIntersectSpotCone(Vector3 min, Vector3 max, BurtViewSpotCone spotCone)
        {
            var c000 = new Vector3(min.x, min.y, min.z);
            var c100 = new Vector3(max.x, min.y, min.z);
            var c010 = new Vector3(min.x, max.y, min.z);
            var c110 = new Vector3(max.x, max.y, min.z);
            var c001 = new Vector3(min.x, min.y, max.z);
            var c101 = new Vector3(max.x, min.y, max.z);
            var c011 = new Vector3(min.x, max.y, max.z);
            var c111 = new Vector3(max.x, max.y, max.z);

            return SegmentIntersectsSpotCone(c000, c100, spotCone) ||
                SegmentIntersectsSpotCone(c010, c110, spotCone) ||
                SegmentIntersectsSpotCone(c001, c101, spotCone) ||
                SegmentIntersectsSpotCone(c011, c111, spotCone) ||
                SegmentIntersectsSpotCone(c000, c010, spotCone) ||
                SegmentIntersectsSpotCone(c100, c110, spotCone) ||
                SegmentIntersectsSpotCone(c001, c011, spotCone) ||
                SegmentIntersectsSpotCone(c101, c111, spotCone) ||
                SegmentIntersectsSpotCone(c000, c001, spotCone) ||
                SegmentIntersectsSpotCone(c100, c101, spotCone) ||
                SegmentIntersectsSpotCone(c010, c011, spotCone) ||
                SegmentIntersectsSpotCone(c110, c111, spotCone);
        }

        private static bool SegmentIntersectsSpotCone(Vector3 start, Vector3 end, BurtViewSpotCone spotCone)
        {
            if (PointInsideSpotCone(start, spotCone) || PointInsideSpotCone(end, spotCone))
            {
                return true;
            }

            var segment = end - start;
            var toStart = start - spotCone.Apex;
            var axisStart = Vector3.Dot(toStart, spotCone.Direction);
            var axisDelta = Vector3.Dot(segment, spotCone.Direction);
            var tanSq = spotCone.TanHalfAngle * spotCone.TanHalfAngle;
            var coneScale = 1f + tanSq;
            var a = Vector3.Dot(segment, segment) - coneScale * axisDelta * axisDelta;
            var b = 2f * (Vector3.Dot(toStart, segment) - coneScale * axisStart * axisDelta);
            var c = Vector3.Dot(toStart, toStart) - coneScale * axisStart * axisStart;

            if (TestSpotConeQuadraticCandidates(a, b, c, start, segment, spotCone))
            {
                return true;
            }

            if (Mathf.Abs(axisDelta) > 0.0001f)
            {
                if (TestSpotConeSegmentCandidate((0f - axisStart) / axisDelta, start, segment, spotCone) ||
                    TestSpotConeSegmentCandidate((spotCone.Range - axisStart) / axisDelta, start, segment, spotCone))
                {
                    return true;
                }
            }

            var sphereA = Vector3.Dot(segment, segment);
            var sphereB = 2f * Vector3.Dot(toStart, segment);
            var sphereC = Vector3.Dot(toStart, toStart) - spotCone.Range * spotCone.Range;
            return TestSpotConeQuadraticCandidates(sphereA, sphereB, sphereC, start, segment, spotCone);
        }

        private static bool TestSpotConeQuadraticCandidates(float a, float b, float c, Vector3 start, Vector3 segment, BurtViewSpotCone spotCone)
        {
            if (Mathf.Abs(a) <= 0.0001f)
            {
                return Mathf.Abs(b) > 0.0001f && TestSpotConeSegmentCandidate(-c / b, start, segment, spotCone);
            }

            var discriminant = b * b - 4f * a * c;
            if (discriminant < 0f)
            {
                return false;
            }

            var sqrtDiscriminant = Mathf.Sqrt(discriminant);
            var invDenominator = 0.5f / a;
            return TestSpotConeSegmentCandidate((-b - sqrtDiscriminant) * invDenominator, start, segment, spotCone) ||
                TestSpotConeSegmentCandidate((-b + sqrtDiscriminant) * invDenominator, start, segment, spotCone);
        }

        private static bool TestSpotConeSegmentCandidate(float t, Vector3 start, Vector3 segment, BurtViewSpotCone spotCone)
        {
            if (t < -0.0001f || t > 1.0001f)
            {
                return false;
            }

            return PointInsideSpotCone(start + segment * Mathf.Clamp01(t), spotCone);
        }

        private static bool SegmentIntersectsViewAabb(Vector3 start, Vector3 end, Vector3 min, Vector3 max)
        {
            var direction = end - start;
            var tMin = 0f;
            var tMax = 1f;
            return ClipSegmentAxis(start.x, direction.x, min.x, max.x, ref tMin, ref tMax) &&
                ClipSegmentAxis(start.y, direction.y, min.y, max.y, ref tMin, ref tMax) &&
                ClipSegmentAxis(start.z, direction.z, min.z, max.z, ref tMin, ref tMax);
        }

        private static bool ClipSegmentAxis(float start, float direction, float min, float max, ref float tMin, ref float tMax)
        {
            if (Mathf.Abs(direction) <= 0.0001f)
            {
                return start >= min && start <= max;
            }

            var invDirection = 1f / direction;
            var t0 = (min - start) * invDirection;
            var t1 = (max - start) * invDirection;
            if (t0 > t1)
            {
                var swap = t0;
                t0 = t1;
                t1 = swap;
            }

            tMin = Mathf.Max(tMin, t0);
            tMax = Mathf.Min(tMax, t1);
            return tMin <= tMax;
        }

        private static bool SphereOutsidePlane(BurtViewSphere viewSphere, Vector3 planeNormal)
        {
            var length = planeNormal.magnitude;
            if (length <= 0.0001f)
            {
                return false;
            }

            var signedDistance = Vector3.Dot(planeNormal, viewSphere.Center) / length;
            return signedDistance < -viewSphere.Radius - 0.0001f;
        }

        private static bool IsDirectionalLight(float lightType)
        {
            return Mathf.Abs(lightType) < 0.5f;
        }

        private static bool IsSpotLight(float lightType)
        {
            return lightType > 1.5f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct BurtViewSphere
        {
            public Vector3 Center { get; }
            public float Radius { get; }

            public BurtViewSphere(Vector3 center, float radius)
            {
                Center = center;
                Radius = radius;
            }
        }

        private readonly struct BurtViewSpotCone
        {
            public Vector3 Apex { get; }
            public Vector3 Direction { get; }
            public float Range { get; }
            public float TanHalfAngle { get; }

            public BurtViewSpotCone(Vector3 apex, Vector3 direction, float range, float tanHalfAngle)
            {
                Apex = apex;
                Direction = direction;
                Range = range;
                TanHalfAngle = tanHalfAngle;
            }
        }

        private readonly struct BurtTileLightCullingContext
        {
            public bool IsValid { get; }
            public bool Orthographic { get; }
            public Matrix4x4 WorldToCameraMatrix { get; }
            public float ProjectionM00 { get; }
            public float ProjectionM02 { get; }
            public float ProjectionM11 { get; }
            public float ProjectionM12 { get; }
            public float NearPlane { get; }
            public float FarPlane { get; }
            public float OrthographicHalfWidth { get; }
            public float OrthographicHalfHeight { get; }
            public int TileCountX { get; }
            public int TileCountY { get; }

            private BurtTileLightCullingContext(
                bool isValid,
                bool orthographic,
                Matrix4x4 worldToCameraMatrix,
                float projectionM00,
                float projectionM02,
                float projectionM11,
                float projectionM12,
                float nearPlane,
                float farPlane,
                float orthographicHalfWidth,
                float orthographicHalfHeight,
                int tileCountX,
                int tileCountY)
            {
                IsValid = isValid;
                Orthographic = orthographic;
                WorldToCameraMatrix = worldToCameraMatrix;
                ProjectionM00 = projectionM00;
                ProjectionM02 = projectionM02;
                ProjectionM11 = projectionM11;
                ProjectionM12 = projectionM12;
                NearPlane = nearPlane;
                FarPlane = farPlane;
                OrthographicHalfWidth = orthographicHalfWidth;
                OrthographicHalfHeight = orthographicHalfHeight;
                TileCountX = tileCountX;
                TileCountY = tileCountY;
            }

            public static BurtTileLightCullingContext Create(Camera camera, BurtTileLightLayout layout)
            {
                if (camera == null || layout.TileCountX <= 0 || layout.TileCountY <= 0)
                {
                    return default;
                }

                var projection = camera.projectionMatrix;
                if (!camera.orthographic &&
                    (Mathf.Abs(projection.m00) <= 0.0001f ||
                    Mathf.Abs(projection.m11) <= 0.0001f ||
                    !IsFinite(projection.m00) ||
                    !IsFinite(projection.m02) ||
                    !IsFinite(projection.m11) ||
                    !IsFinite(projection.m12)))
                {
                    return default;
                }

                var orthographicHalfHeight = camera.orthographic ? Mathf.Max(camera.orthographicSize, 0.0001f) : 0f;
                var orthographicHalfWidth = camera.orthographic ? Mathf.Max(orthographicHalfHeight * camera.aspect, 0.0001f) : 0f;
                return new BurtTileLightCullingContext(
                    true,
                    camera.orthographic,
                    camera.worldToCameraMatrix,
                    projection.m00,
                    projection.m02,
                    projection.m11,
                    projection.m12,
                    Mathf.Max(camera.nearClipPlane, 0.0001f),
                    Mathf.Max(camera.farClipPlane, Mathf.Max(camera.nearClipPlane, 0.0001f) + 0.0001f),
                    orthographicHalfWidth,
                    orthographicHalfHeight,
                    layout.TileCountX,
                    layout.TileCountY);
            }

            public float CalculateViewXSlope(float viewportX)
            {
                return ((viewportX * 2f) - 1f + ProjectionM02) / ProjectionM00;
            }

            public float CalculateViewYSlope(float viewportY)
            {
                return ((viewportY * 2f) - 1f + ProjectionM12) / ProjectionM11;
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct BurtTileLightOffsetRange
        {
            public uint Offset;
            public uint Count;
        }

        private struct BurtTileLightStats
        {
            public int MinCount { get; }
            public int MaxCount { get; }
            public float AverageCount { get; }
            public int OverflowTileCount { get; }
            public int MaxOverflowExtraCount { get; }

            public BurtTileLightStats(int minCount, int maxCount, float averageCount, int overflowTileCount, int maxOverflowExtraCount)
            {
                MinCount = minCount;
                MaxCount = maxCount;
                AverageCount = averageCount;
                OverflowTileCount = overflowTileCount;
                MaxOverflowExtraCount = maxOverflowExtraCount;
            }
        }
    }

    internal sealed class BurtDebugTileLightViewPass : BurtRenderPass
    {
        private const string DebugTileLightShaderName = "Hidden/BurtRP/DebugTileLightList";
        private Material debugTileLightMaterial;
        private Texture2D debugTileLightTexture;
        private Color32[] debugTileLightTextureData;
        private int debugTileLightTextureWidth;
        private int debugTileLightTextureHeight;
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Debug Tile Light List";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.FullScreen;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (builder.ResourceRegistry == null || !builder.ResourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.TileLightCountBufferName))
            {
                return;
            }

            builder.ReadTileLightCountBuffer();
            if (BurtShadingDebugSettings.Mode == BurtShadingDebugMode.TileLightOccupancy)
            {
                builder.ReadTileLightListBuffer();
                builder.ReadTileLightOffsetBuffer();
            }

            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            var debugMode = BurtTileLightDebugViewUtility.ResolveShaderDebugMode();
            if (debugMode <= 0 || context == null)
            {
                return;
            }

            var cameraColor = context.CameraColorTarget;
            var countBuffer = context.TileLightCountBuffer;
            var listBuffer = context.TileLightListBuffer;
            var offsetBuffer = context.TileLightOffsetBuffer;
            if (!cameraColor.IsValid)
            {
                return;
            }

            if (!countBuffer.IsValid || !countBuffer.HasBuffer)
            {
                ClearDiagnostic(context, cameraColor, new Color(1f, 0f, 1f, 1f));
                return;
            }

            if (debugMode == 2 && (!listBuffer.IsValid || !listBuffer.HasBuffer || !offsetBuffer.IsValid || !offsetBuffer.HasBuffer))
            {
                ClearDiagnostic(context, cameraColor, new Color(1f, 0.5f, 0f, 1f));
                return;
            }

            var material = GetDebugTileLightMaterial();
            if (material == null)
            {
                ClearDiagnostic(context, cameraColor, new Color(1f, 0f, 1f, 1f));
                return;
            }

            var lightingData = context.Request != null ? context.Request.LightingData : null;
            var layout = ResolveLayout(context, lightingData);
            var maxLightsPerTile = ResolveMaxLightsPerTile(lightingData);
            var maxCount = lightingData != null ? lightingData.TileLightMaxCount : 0;
            var averageCount = lightingData != null ? lightingData.TileLightAverageCount : 0f;
            var additionalCount = lightingData != null ? lightingData.AdditionalLightCount : 0;
            var hasCpuDebugTexture = BurtTileLightDebugViewUtility.UseCpuDebugColorTextureFallback && TryUpdateDebugColorTexture(lightingData, layout, debugMode, maxLightsPerTile);

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetRenderTarget(cameraColor.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.SetGlobalBuffer(BurtTiledLightData.TileLightCountBufferId, countBuffer.Buffer);
            if (listBuffer.IsValid && listBuffer.HasBuffer)
            {
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightListBufferId, listBuffer.Buffer);
            }

            if (offsetBuffer.IsValid && offsetBuffer.HasBuffer)
            {
                cmd.SetGlobalBuffer(BurtTiledLightData.TileLightOffsetBufferId, offsetBuffer.Buffer);
            }

            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, 1f);
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightDebugColorTextureEnabledId, hasCpuDebugTexture ? 1f : 0f);
            if (hasCpuDebugTexture)
            {
                cmd.SetGlobalTexture(BurtTiledLightData.TileLightDebugColorTextureId, debugTileLightTexture);
            }

            cmd.SetGlobalFloat(BurtTiledLightData.TileLightDebugModeId, debugMode);
            cmd.SetGlobalVector(BurtTiledLightData.TileLightGridParamsId, new Vector4(layout.TileCountX, layout.TileCountY, layout.TileSize, maxLightsPerTile));
            cmd.SetGlobalVector(BurtTiledLightData.TileLightDebugStatsId, new Vector4(0f, maxCount, averageCount, additionalCount));
            cmd.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3, 1);
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, 0f);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static void ClearDiagnostic(BurtRenderGraphContext context, BurtRenderTargetHandle cameraColor, Color color)
        {
            var cmd = context.AcquireCommandBuffer("Burt Debug Tile Light List Missing Resource");
            cmd.SetRenderTarget(cameraColor.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.ClearRenderTarget(false, true, color);
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, 0f);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private static BurtTileLightLayout ResolveLayout(BurtRenderGraphContext context, BurtLightingData lightingData)
        {
            if (lightingData != null && lightingData.TileLightTileCount > 0)
            {
                return new BurtTileLightLayout(
                    lightingData.TileLightGridX * Mathf.Max(1, lightingData.TileLightTileSize),
                    lightingData.TileLightGridY * Mathf.Max(1, lightingData.TileLightTileSize),
                    Mathf.Max(1, lightingData.TileLightTileSize));
            }

            return BurtTiledLightData.CalculateLayout(context.Request != null ? context.Request.Camera : null);
        }

        private static int ResolveMaxLightsPerTile(BurtLightingData lightingData)
        {
            return lightingData != null && lightingData.TileLightMaxLightsPerTile > 0
                ? lightingData.TileLightMaxLightsPerTile
                : BurtTiledLightData.ResolveMaxLightsPerTile();
        }

        private bool TryUpdateDebugColorTexture(BurtLightingData lightingData, BurtTileLightLayout layout, int debugMode, int maxLightsPerTile)
        {
            if (lightingData == null ||
                lightingData.TileLightDebugCountSnapshot == null ||
                lightingData.TileLightDebugCountSnapshotLength < layout.TileCount ||
                layout.TileCountX <= 0 ||
                layout.TileCountY <= 0)
            {
                return false;
            }

            EnsureDebugColorTexture(layout.TileCountX, layout.TileCountY);

            for (var tileIndex = 0; tileIndex < layout.TileCount; tileIndex++)
            {
                debugTileLightTextureData[tileIndex] = ResolveDebugColor(lightingData.TileLightDebugCountSnapshot[tileIndex], debugMode, maxLightsPerTile);
            }

            debugTileLightTexture.SetPixels32(debugTileLightTextureData);
            debugTileLightTexture.Apply(false, false);
            return true;
        }

        private void EnsureDebugColorTexture(int width, int height)
        {
            if (debugTileLightTexture != null &&
                debugTileLightTextureWidth == width &&
                debugTileLightTextureHeight == height &&
                debugTileLightTextureData != null &&
                debugTileLightTextureData.Length >= width * height)
            {
                return;
            }

            debugTileLightTextureWidth = width;
            debugTileLightTextureHeight = height;
            debugTileLightTextureData = new Color32[width * height];
            debugTileLightTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        private static Color32 ResolveDebugColor(uint lightCount, int debugMode, int maxLightsPerTile)
        {
            if (debugMode == 2)
            {
                if (lightCount > (uint)maxLightsPerTile)
                {
                    return new Color32(255, 0, 255, 255);
                }

                var heat = Mathf.Clamp01((float)lightCount / Mathf.Max(1, maxLightsPerTile));
                return ToColor32(BurtTileHeatColor(heat));
            }

            if (lightCount == 0u)
            {
                return new Color32(4, 6, 20, 255);
            }

            if (lightCount == 1u)
            {
                return new Color32(0, 107, 255, 255);
            }

            if (lightCount == 2u)
            {
                return new Color32(0, 242, 87, 255);
            }

            if (lightCount == 3u)
            {
                return new Color32(255, 235, 13, 255);
            }

            if (lightCount == 4u)
            {
                return new Color32(255, 115, 0, 255);
            }

            return new Color32(255, 0, 31, 255);
        }

        private static Color BurtTileHeatColor(float heat)
        {
            var cold = new Color(0.02f, 0.08f, 0.32f, 1f);
            var mid = new Color(0f, 0.75f, 0.55f, 1f);
            var warm = new Color(1f, 0.78f, 0.08f, 1f);
            var hot = new Color(1f, 0.08f, 0.02f, 1f);
            var low = Color.Lerp(cold, mid, Mathf.Clamp01(heat * 2f));
            var high = Color.Lerp(warm, hot, Mathf.Clamp01((heat - 0.5f) * 2f));
            return Color.Lerp(low, high, heat >= 0.5f ? 1f : 0f);
        }

        private static Color32 ToColor32(Color color)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                255);
        }

        private Material GetDebugTileLightMaterial()
        {
            if (debugTileLightMaterial != null)
            {
                return debugTileLightMaterial;
            }

            var shader = Shader.Find(DebugTileLightShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugTileLightShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            debugTileLightMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return debugTileLightMaterial;
        }
    }

    internal sealed class BurtDebugClusterLightVolumePass : BurtRenderPass
    {
        private const string DebugClusterLightShaderName = "Hidden/BurtRP/DebugClusterLightVolume";
        private const int MaxDebugClusters = 4096;
        private const int CubeCornerCount = 8;
        private const int CubeLineVertexCount = 24;
        private const int CubeSolidVertexCount = 36;

        private static readonly int[] LineCornerIndices =
        {
            0, 1, 1, 3, 3, 2, 2, 0,
            4, 5, 5, 7, 7, 6, 6, 4,
            0, 4, 1, 5, 2, 6, 3, 7
        };

        private static readonly int[] SolidCornerIndices =
        {
            0, 2, 3, 0, 3, 1,
            4, 5, 7, 4, 7, 6,
            0, 1, 5, 0, 5, 4,
            2, 6, 7, 2, 7, 3,
            0, 4, 6, 0, 6, 2,
            1, 3, 7, 1, 7, 5
        };

        private Material debugClusterLightMaterial;
        private Mesh lineMesh;
        private Mesh solidMesh;
        private Vector3[] lineVertices;
        private Color32[] lineColors;
        private int[] lineIndices;
        private Vector3[] solidVertices;
        private Color32[] solidColors;
        private int[] solidIndices;
        private readonly Vector3[] clusterCorners = new Vector3[CubeCornerCount];
        private bool hasLoggedMissingShader;

        public override string Name => "Burt Debug Cluster Light Volume";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.Debug;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (builder.ResourceRegistry == null || !builder.ResourceRegistry.ContainsBuffer(BurtRenderGraphResourceRegistry.ClusterLightCountBufferName))
            {
                return;
            }

            builder.ReadCameraColor();
            builder.ReadCameraDepth();
            builder.ReadClusterLightCountBuffer();
            builder.WriteCameraColor();
        }

        public override void Execute(BurtRenderGraphContext context)
        {
            if (context == null || !BurtTileLightDebugViewUtility.IsClusterLightDebugMode(BurtShadingDebugSettings.Mode))
            {
                return;
            }

            var request = context.Request;
            var camera = request != null ? request.Camera : null;
            var lightingData = request != null ? request.LightingData : null;
            if (camera == null || lightingData == null || !lightingData.ClusterLightUploaded)
            {
                return;
            }

            var cameraColor = context.CameraColorTarget;
            var cameraDepth = context.CameraDepthTarget;
            if (!cameraColor.IsValid || !cameraDepth.IsValid)
            {
                return;
            }

            if (!TryBuildMeshes(camera, lightingData, BurtShadingDebugSettings.Mode))
            {
                return;
            }

            var material = GetDebugClusterLightMaterial();
            if (material == null)
            {
                return;
            }

            var cmd = context.AcquireCommandBuffer(Name);
            cmd.SetRenderTarget(cameraColor.Identifier, cameraDepth.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, camera);
            cmd.DrawMesh(solidMesh, Matrix4x4.identity, material, 0, 0);
            cmd.DrawMesh(lineMesh, Matrix4x4.identity, material, 0, 1);
            context.ExecuteAndReleaseCommandBuffer(cmd);
        }

        private bool TryBuildMeshes(Camera camera, BurtLightingData lightingData, BurtShadingDebugMode mode)
        {
            var counts = lightingData.ClusterLightDebugCountSnapshot;
            var validCount = Mathf.Min(lightingData.ClusterLightDebugCountSnapshotLength, lightingData.ClusterLightClusterCount);
            var fallbackLayout = BurtTiledLightData.CalculateLayout(camera);
            var tileCountX = lightingData.ClusterLightGridX > 0 ? lightingData.ClusterLightGridX : fallbackLayout.TileCountX;
            var tileCountY = lightingData.ClusterLightGridY > 0 ? lightingData.ClusterLightGridY : fallbackLayout.TileCountY;
            var depthSliceCount = lightingData.ClusterLightDepthSliceCount > 0 ? lightingData.ClusterLightDepthSliceCount : BurtTiledLightData.ClusterDepthSliceCount;
            var tileCount = tileCountX * tileCountY;
            if (counts == null || validCount <= 0 || tileCountX <= 0 || tileCountY <= 0 || depthSliceCount <= 0 || tileCount <= 0)
            {
                return false;
            }

            var maxLightsPerCluster = Mathf.Max(1, lightingData.ClusterLightMaxLightsPerCluster);
            var maxVisibleCount = Mathf.Max(1, lightingData.ClusterLightMaxCount);
            var selectedCount = CountVisibleClusters(counts, validCount);
            if (selectedCount <= 0)
            {
                ClearMeshes();
                return false;
            }

            var visibleClusterCount = selectedCount;
            selectedCount = Mathf.Min(selectedCount, MaxDebugClusters);
            EnsureMeshCapacity(selectedCount);

            var cameraToWorld = camera.cameraToWorldMatrix;
            var nearPlane = Mathf.Max(lightingData.ClusterLightNearPlane, 0.0001f);
            var farPlane = Mathf.Max(lightingData.ClusterLightFarPlane, nearPlane + 0.0001f);
            var invTileCountX = 1f / tileCountX;
            var invTileCountY = 1f / tileCountY;
            var writtenClusters = 0;
            var visibleClusterOrdinal = 0;
            var step = Mathf.Max(1, Mathf.CeilToInt((float)visibleClusterCount / MaxDebugClusters));

            for (var clusterIndex = 0; clusterIndex < validCount && writtenClusters < selectedCount; clusterIndex++)
            {
                var rawCount = counts[clusterIndex];
                if (rawCount == 0u)
                {
                    continue;
                }

                if (visibleClusterOrdinal++ % step != 0)
                {
                    continue;
                }

                var slice = clusterIndex / tileCount;
                var tileIndex = clusterIndex - slice * tileCount;
                if (slice >= depthSliceCount)
                {
                    continue;
                }

                var tileX = tileIndex % tileCountX;
                var tileY = tileIndex / tileCountX;
                var depth0 = BurtTiledLightData.CalculateClusterSliceDepth(nearPlane, farPlane, slice, depthSliceCount);
                var depth1 = BurtTiledLightData.CalculateClusterSliceDepth(nearPlane, farPlane, slice + 1, depthSliceCount);
                var u0 = tileX * invTileCountX;
                var u1 = (tileX + 1) * invTileCountX;
                var v0 = tileY * invTileCountY;
                var v1 = (tileY + 1) * invTileCountY;
                BuildClusterCorners(camera, cameraToWorld, depth0, depth1, u0, u1, v0, v1, clusterCorners);
                var baseColor = ResolveClusterColor(rawCount, mode, maxLightsPerCluster, maxVisibleCount);
                WriteCluster(writtenClusters, clusterCorners, baseColor);
                writtenClusters++;
            }

            ApplyMeshData(writtenClusters);
            return writtenClusters > 0;
        }

        private static int CountVisibleClusters(uint[] counts, int validCount)
        {
            var visibleCount = 0;
            for (var i = 0; i < validCount; i++)
            {
                if (counts[i] > 0u)
                {
                    visibleCount++;
                }
            }

            return visibleCount;
        }

        private static void BuildClusterCorners(Camera camera, Matrix4x4 cameraToWorld, float depth0, float depth1, float u0, float u1, float v0, float v1, Vector3[] corners)
        {
            corners[0] = ViewToWorld(camera, cameraToWorld, u0, v0, depth0);
            corners[1] = ViewToWorld(camera, cameraToWorld, u1, v0, depth0);
            corners[2] = ViewToWorld(camera, cameraToWorld, u0, v1, depth0);
            corners[3] = ViewToWorld(camera, cameraToWorld, u1, v1, depth0);
            corners[4] = ViewToWorld(camera, cameraToWorld, u0, v0, depth1);
            corners[5] = ViewToWorld(camera, cameraToWorld, u1, v0, depth1);
            corners[6] = ViewToWorld(camera, cameraToWorld, u0, v1, depth1);
            corners[7] = ViewToWorld(camera, cameraToWorld, u1, v1, depth1);
        }

        private static Vector3 ViewToWorld(Camera camera, Matrix4x4 cameraToWorld, float u, float v, float depth)
        {
            if (camera.orthographic)
            {
                var halfHeight = Mathf.Max(camera.orthographicSize, 0.0001f);
                var halfWidth = Mathf.Max(halfHeight * camera.aspect, 0.0001f);
                var view = new Vector3(Mathf.Lerp(-halfWidth, halfWidth, u), Mathf.Lerp(-halfHeight, halfHeight, v), -depth);
                return cameraToWorld.MultiplyPoint(view);
            }

            var projection = camera.projectionMatrix;
            var xSlope = ((u * 2f) - 1f + projection.m02) / ResolveSafeProjectionScale(projection.m00);
            var ySlope = ((v * 2f) - 1f + projection.m12) / ResolveSafeProjectionScale(projection.m11);
            return cameraToWorld.MultiplyPoint(new Vector3(xSlope * depth, ySlope * depth, -depth));
        }

        private static float ResolveSafeProjectionScale(float value)
        {
            return Mathf.Abs(value) > 0.0001f ? value : (value < 0f ? -0.0001f : 0.0001f);
        }

        private void EnsureMeshCapacity(int clusterCapacity)
        {
            var lineVertexCapacity = clusterCapacity * CubeLineVertexCount;
            var solidVertexCapacity = clusterCapacity * CubeSolidVertexCount;
            if (lineVertices == null || lineVertices.Length < lineVertexCapacity)
            {
                lineVertices = new Vector3[lineVertexCapacity];
                lineColors = new Color32[lineVertexCapacity];
                lineIndices = new int[lineVertexCapacity];
                for (var i = 0; i < lineIndices.Length; i++)
                {
                    lineIndices[i] = i;
                }
            }

            if (solidVertices == null || solidVertices.Length < solidVertexCapacity)
            {
                solidVertices = new Vector3[solidVertexCapacity];
                solidColors = new Color32[solidVertexCapacity];
                solidIndices = new int[solidVertexCapacity];
                for (var i = 0; i < solidIndices.Length; i++)
                {
                    solidIndices[i] = i;
                }
            }

            if (lineMesh == null)
            {
                lineMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "Burt Debug Cluster Light Lines" };
                lineMesh.indexFormat = IndexFormat.UInt32;
                lineMesh.MarkDynamic();
            }

            if (solidMesh == null)
            {
                solidMesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, name = "Burt Debug Cluster Light Solids" };
                solidMesh.indexFormat = IndexFormat.UInt32;
                solidMesh.MarkDynamic();
            }
        }

        private void WriteCluster(int clusterWriteIndex, Vector3[] corners, Color color)
        {
            var lineBase = clusterWriteIndex * CubeLineVertexCount;
            var solidBase = clusterWriteIndex * CubeSolidVertexCount;
            var lineColor = ToColor32(new Color(color.r, color.g, color.b, 0.82f));
            var solidColor = ToColor32(new Color(color.r, color.g, color.b, 0.08f));

            for (var i = 0; i < CubeLineVertexCount; i++)
            {
                lineVertices[lineBase + i] = corners[LineCornerIndices[i]];
                lineColors[lineBase + i] = lineColor;
            }

            for (var i = 0; i < CubeSolidVertexCount; i++)
            {
                solidVertices[solidBase + i] = corners[SolidCornerIndices[i]];
                solidColors[solidBase + i] = solidColor;
            }
        }

        private void ApplyMeshData(int clusterCount)
        {
            var lineVertexCount = clusterCount * CubeLineVertexCount;
            var solidVertexCount = clusterCount * CubeSolidVertexCount;
            lineMesh.Clear();
            lineMesh.SetVertices(lineVertices, 0, lineVertexCount);
            lineMesh.SetColors(lineColors, 0, lineVertexCount);
            lineMesh.SetIndices(lineIndices, 0, lineVertexCount, MeshTopology.Lines, 0, false);
            lineMesh.RecalculateBounds();

            solidMesh.Clear();
            solidMesh.SetVertices(solidVertices, 0, solidVertexCount);
            solidMesh.SetColors(solidColors, 0, solidVertexCount);
            solidMesh.SetIndices(solidIndices, 0, solidVertexCount, MeshTopology.Triangles, 0, false);
            solidMesh.RecalculateBounds();
        }

        private void ClearMeshes()
        {
            if (lineMesh != null)
            {
                lineMesh.Clear();
            }

            if (solidMesh != null)
            {
                solidMesh.Clear();
            }
        }

        private static Color ResolveClusterColor(uint lightCount, BurtShadingDebugMode mode, int maxLightsPerCluster, int maxVisibleCount)
        {
            if (mode == BurtShadingDebugMode.ClusterLightOccupancy)
            {
                if (lightCount > (uint)maxLightsPerCluster)
                {
                    return new Color(1f, 0f, 1f, 1f);
                }

                return BurtTileHeatColor((float)lightCount / Mathf.Max(1, maxLightsPerCluster));
            }

            if (lightCount == 1u)
            {
                return new Color(0f, 0.42f, 1f, 1f);
            }

            if (lightCount == 2u)
            {
                return new Color(0f, 0.95f, 0.34f, 1f);
            }

            if (lightCount == 3u)
            {
                return new Color(1f, 0.92f, 0.05f, 1f);
            }

            if (lightCount == 4u)
            {
                return new Color(1f, 0.45f, 0f, 1f);
            }

            return Color.Lerp(new Color(1f, 0.45f, 0f, 1f), new Color(1f, 0f, 0.12f, 1f), Mathf.Clamp01((float)lightCount / Mathf.Max(1, maxVisibleCount)));
        }

        private static Color BurtTileHeatColor(float heat)
        {
            heat = Mathf.Clamp01(heat);
            var cold = new Color(0.02f, 0.08f, 0.32f, 1f);
            var mid = new Color(0f, 0.75f, 0.55f, 1f);
            var warm = new Color(1f, 0.78f, 0.08f, 1f);
            var hot = new Color(1f, 0.08f, 0.02f, 1f);
            var low = Color.Lerp(cold, mid, Mathf.Clamp01(heat * 2f));
            var high = Color.Lerp(warm, hot, Mathf.Clamp01((heat - 0.5f) * 2f));
            return Color.Lerp(low, high, heat >= 0.5f ? 1f : 0f);
        }

        private static Color32 ToColor32(Color color)
        {
            return new Color32(
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
                (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255));
        }

        private Material GetDebugClusterLightMaterial()
        {
            if (debugClusterLightMaterial != null)
            {
                return debugClusterLightMaterial;
            }

            var shader = Shader.Find(DebugClusterLightShaderName);
            if (shader == null)
            {
                if (!hasLoggedMissingShader)
                {
                    Debug.LogWarning("BurtRP could not find shader: " + DebugClusterLightShaderName);
                    hasLoggedMissingShader = true;
                }

                return null;
            }

            debugClusterLightMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return debugClusterLightMaterial;
        }
    }
}
