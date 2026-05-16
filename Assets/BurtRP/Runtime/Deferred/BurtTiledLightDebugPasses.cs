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

        public static bool IsTileLightDebugMode(BurtShadingDebugMode mode)
        {
            return mode == BurtShadingDebugMode.TileLightCount || mode == BurtShadingDebugMode.TileLightOccupancy;
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

    internal sealed class BurtBuildTileLightListDebugPass : BurtRenderPass
    {
        private uint[] tileLightCountData;
        private uint[] tileLightListData;
        private BurtTileLightOffsetRange[] tileLightOffsetData;

        public override string Name => "Burt Build Tile Light List";

        public override BurtRenderPassKind Kind => BurtRenderPassKind.GlobalState;

        public override void Configure(BurtRenderPassBuilder builder)
        {
            if (!ShouldBuild(builder.Request, builder.Asset, builder.ResourceRegistry))
            {
                return;
            }

            var shouldBuildListData = ShouldBuildListData(builder.Request, builder.Asset, builder.ResourceRegistry);
            builder.ReadLightingGlobals();
            builder.WriteTileLightCountBuffer();
            if (shouldBuildListData)
            {
                builder.WriteTileLightListBuffer();
                builder.WriteTileLightOffsetBuffer();
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
            var shouldBuildListData = ShouldBuildListData(request, context.Asset, context.ResourceRegistry);
            var useRuntimeTileList = BurtTiledLightData.ShouldUseRuntimeTiledLightingResources(request, context.Asset, shouldBuildListData);
            var maxLightsPerTile = BurtTiledLightData.ResolveMaxLightsPerTile(useRuntimeTileList);
            EnsureCapacity(layout, shouldBuildListData, maxLightsPerTile);
            ClearWorkingData(layout, shouldBuildListData, maxLightsPerTile);
            BuildCpuApproxTileLists(request.Camera, lightingData, layout, shouldBuildListData, maxLightsPerTile);
            var stats = FinalizeTileMetadata(layout, shouldBuildListData, maxLightsPerTile);

            var countBuffer = context.TileLightCountBuffer;
            var listBuffer = context.TileLightListBuffer;
            var offsetBuffer = context.TileLightOffsetBuffer;
            var uploaded = UploadBuffers(countBuffer, listBuffer, offsetBuffer, layout, shouldBuildListData, maxLightsPerTile);

            lightingData.SetTileLightDebugState(
                true,
                uploaded,
                useRuntimeTileList ? BurtTiledLightData.RuntimeBuildModeLabel : BurtTiledLightData.DebugBuildModeLabel,
                layout.TileSize,
                layout.TileCountX,
                layout.TileCountY,
                layout.TileCount,
                maxLightsPerTile,
                shouldBuildListData ? layout.TileCount * maxLightsPerTile : 0,
                stats.MinCount,
                stats.MaxCount,
                stats.AverageCount,
                stats.OverflowTileCount,
                stats.MaxOverflowExtraCount);
            lightingData.SetTileLightDebugCountSnapshot(tileLightCountData, layout.TileCount);

            UploadGlobals(context, countBuffer, listBuffer, offsetBuffer, layout, stats, uploaded, shouldBuildListData, maxLightsPerTile);
        }

        private static bool ShouldBuild(BurtRenderRequest request, BurtRenderPipelineAsset asset, BurtRenderGraphResourceRegistry resourceRegistry)
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

        private void EnsureCapacity(BurtTileLightLayout layout, bool includeListData, int maxLightsPerTile)
        {
            var tileCount = Mathf.Max(1, layout.TileCount);

            if (tileLightCountData == null || tileLightCountData.Length < tileCount)
            {
                tileLightCountData = new uint[tileCount];
            }

            if (!includeListData)
            {
                return;
            }

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

        private void ClearWorkingData(BurtTileLightLayout layout, bool includeListData, int maxLightsPerTile)
        {
            Array.Clear(tileLightCountData, 0, layout.TileCount);
            if (includeListData)
            {
                Array.Clear(tileLightListData, 0, layout.TileCount * maxLightsPerTile);
                Array.Clear(tileLightOffsetData, 0, layout.TileCount);
            }
        }

        private void BuildCpuApproxTileLists(Camera camera, BurtLightingData lightingData, BurtTileLightLayout layout, bool includeListData, int maxLightsPerTile)
        {
            var additionalLightCount = Mathf.Min(lightingData.AdditionalLightCount, BurtLightingData.MaxAdditionalLights);
            for (var lightIndex = 0; lightIndex < additionalLightCount; lightIndex++)
            {
                var colorAndType = lightingData.AdditionalLightColorAndType[lightIndex];
                if (IsDirectionalLight(colorAndType.w))
                {
                    AddLightToTileRect(lightIndex, 0, 0, layout.TileCountX - 1, layout.TileCountY - 1, layout, includeListData, maxLightsPerTile);
                    continue;
                }

                var positionAndRange = lightingData.AdditionalLightPositionAndRange[lightIndex];
                if (!TryProjectLocalLightBounds(camera, positionAndRange, layout, out var minTileX, out var minTileY, out var maxTileX, out var maxTileY))
                {
                    continue;
                }

                AddLightToTileRect(lightIndex, minTileX, minTileY, maxTileX, maxTileY, layout, includeListData, maxLightsPerTile);
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
            bool includeListData,
            int maxLightsPerTile)
        {
            if (!TryGetViewSphere(positionAndRange, cullingContext, out var viewSphere))
            {
                AddLightToTileRect(lightIndex, minTileX, minTileY, maxTileX, maxTileY, layout, includeListData, maxLightsPerTile);
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

                    AddLightToTile(lightIndex, tileX, tileY, layout, includeListData, maxLightsPerTile);
                }
            }
        }

        private void AddLightToTileRect(int lightIndex, int minTileX, int minTileY, int maxTileX, int maxTileY, BurtTileLightLayout layout, bool includeListData, int maxLightsPerTile)
        {
            for (var tileY = minTileY; tileY <= maxTileY; tileY++)
            {
                for (var tileX = minTileX; tileX <= maxTileX; tileX++)
                {
                    AddLightToTile(lightIndex, tileX, tileY, layout, includeListData, maxLightsPerTile);
                }
            }
        }

        private void AddLightToTile(int lightIndex, int tileX, int tileY, BurtTileLightLayout layout, bool includeListData, int maxLightsPerTile)
        {
            var tileIndex = tileY * layout.TileCountX + tileX;
            var currentCount = tileLightCountData[tileIndex];
            if (includeListData && currentCount < (uint)maxLightsPerTile)
            {
                tileLightListData[tileIndex * maxLightsPerTile + (int)currentCount] = (uint)lightIndex;
            }

            tileLightCountData[tileIndex] = currentCount + 1u;
        }

        private BurtTileLightStats FinalizeTileMetadata(BurtTileLightLayout layout, bool includeListData, int maxLightsPerTile)
        {
            var minCount = int.MaxValue;
            var maxCount = 0;
            var sumCount = 0L;
            var overflowTileCount = 0;
            var maxOverflowExtraCount = 0;

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

        private bool UploadBuffers(
            BurtRenderBufferHandle countBuffer,
            BurtRenderBufferHandle listBuffer,
            BurtRenderBufferHandle offsetBuffer,
            BurtTileLightLayout layout,
            bool includeListData,
            int maxLightsPerTile)
        {
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

        private static void UploadGlobals(
            BurtRenderGraphContext context,
            BurtRenderBufferHandle countBuffer,
            BurtRenderBufferHandle listBuffer,
            BurtRenderBufferHandle offsetBuffer,
            BurtTileLightLayout layout,
            BurtTileLightStats stats,
            bool uploaded,
            bool includeListData,
            int maxLightsPerTile)
        {
            var cmd = CommandBufferPool.Get("Burt Upload Tile Light Debug Globals");
            var tileListUsable = uploaded && includeListData;
            var additionalLightCount = context != null && context.Request != null && context.Request.LightingData != null
                ? context.Request.LightingData.AdditionalLightCount
                : 0;
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, tileListUsable ? 1f : 0f);
            cmd.SetGlobalVector(BurtTiledLightData.TileLightGridParamsId, new Vector4(layout.TileCountX, layout.TileCountY, layout.TileSize, maxLightsPerTile));
            cmd.SetGlobalVector(BurtTiledLightData.TileLightDebugStatsId, new Vector4(stats.MinCount, stats.MaxCount, stats.AverageCount, additionalLightCount));

            if (countBuffer.IsValid && countBuffer.HasBuffer)
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

            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void UploadDisabledGlobals(BurtRenderGraphContext context)
        {
            if (context == null)
            {
                return;
            }

            var cmd = CommandBufferPool.Get("Burt Disable Tile Light Globals");
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, 0f);
            cmd.SetGlobalVector(BurtTiledLightData.TileLightGridParamsId, Vector4.zero);
            cmd.SetGlobalVector(BurtTiledLightData.TileLightDebugStatsId, Vector4.zero);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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
            if (!IsFinite(cameraViewPosition.x) || !IsFinite(cameraViewPosition.y) || !IsFinite(cameraViewPosition.z) || radius <= 0.0001f)
            {
                return false;
            }

            viewSphere = new BurtViewSphere(new Vector3(cameraViewPosition.x, cameraViewPosition.y, -cameraViewPosition.z), radius);
            return true;
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

            var cmd = CommandBufferPool.Get(Name);
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
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        private static void ClearDiagnostic(BurtRenderGraphContext context, BurtRenderTargetHandle cameraColor, Color color)
        {
            var cmd = CommandBufferPool.Get("Burt Debug Tile Light List Missing Resource");
            cmd.SetRenderTarget(cameraColor.Identifier);
            BurtRenderTargetDescriptorUtility.SetCameraTargetViewport(cmd, context.Request != null ? context.Request.Camera : null);
            cmd.ClearRenderTarget(false, true, color);
            cmd.SetGlobalFloat(BurtTiledLightData.TileLightCountBufferEnabledId, 0f);
            context.ScriptableContext.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
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
}
