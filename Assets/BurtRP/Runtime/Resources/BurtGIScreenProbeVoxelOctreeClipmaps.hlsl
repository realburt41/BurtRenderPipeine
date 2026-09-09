#ifndef BURT_GI_SCREEN_PROBE_VOXEL_OCTREE_CLIPMAPS_INCLUDED
#define BURT_GI_SCREEN_PROBE_VOXEL_OCTREE_CLIPMAPS_INCLUDED

// ScreenProbe's multi-level kernel keeps the single-level kernel independent.
Texture3D<uint> _BurtGISceneVoxelClipmap1OctreeLeafLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap1OctreeLeafHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap1OctreeParentLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap1OctreeParentHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap1OctreeRootLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap1OctreeRootHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap2OctreeLeafLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap2OctreeLeafHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap2OctreeParentLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap2OctreeParentHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap2OctreeRootLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap2OctreeRootHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap3OctreeLeafLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap3OctreeLeafHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap3OctreeParentLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap3OctreeParentHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap3OctreeRootLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap3OctreeRootHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap4OctreeLeafLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap4OctreeLeafHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap4OctreeParentLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap4OctreeParentHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap4OctreeRootLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap4OctreeRootHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap5OctreeLeafLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap5OctreeLeafHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap5OctreeParentLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap5OctreeParentHighTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap5OctreeRootLowTexture;
Texture3D<uint> _BurtGISceneVoxelClipmap5OctreeRootHighTexture;

// Reuse the per-resource HDDA, not the generic nearest-of-overlapping-levels
// selector. XRender advances tMin to the fine volume's exit before the next level.
#define BURT_GI_SCENE_VOXEL_OCTREE_DISABLE_CLIPMAP_TRACE
#include "BurtGISceneVoxelOctree.hlsl"
#undef BURT_GI_SCENE_VOXEL_OCTREE_DISABLE_CLIPMAP_TRACE

float4 BurtGIScreenProbeVoxelOctreeBounds(uint level)
{
    return level == 0u ? _BurtGISceneVoxelCenterExtent : _BurtGISceneVoxelClipmapCenterExtent[level];
}

bool BurtGIScreenProbeVoxelOctreeLevelValid(uint level)
{
    return level == 0u ? _BurtGISceneVoxelOctreeValid > 0.5
        : (_BurtGISceneVoxelClipmapValidMask & (1u << level)) != 0u;
}

float BurtGIScreenProbeVoxelOctreeCellSize(uint level)
{
    uint width = 1u, height = 1u, depth = 1u;
    if (level == 0u) _BurtGISceneVoxelGeometryReadTexture.GetDimensions(width, height, depth);
    else if (level == 1u) _BurtGISceneVoxelClipmap1GeometryReadTexture.GetDimensions(width, height, depth);
    else if (level == 2u) _BurtGISceneVoxelClipmap2GeometryReadTexture.GetDimensions(width, height, depth);
    else if (level == 3u) _BurtGISceneVoxelClipmap3GeometryReadTexture.GetDimensions(width, height, depth);
    else if (level == 4u) _BurtGISceneVoxelClipmap4GeometryReadTexture.GetDimensions(width, height, depth);
    else if (level == 5u) _BurtGISceneVoxelClipmap5GeometryReadTexture.GetDimensions(width, height, depth);
    return max(2.0 * BurtGIScreenProbeVoxelOctreeBounds(level).w / max((float)width, 1.0), 0.001);
}

bool BurtGIScreenProbeVoxelOctreeTraceLevel(
    uint level, float3 origin, float3 direction, float length, out float distance, out bool rangeComplete)
{
    rangeComplete = false;
    distance = length;
    if (level == 0u)
        return BurtGIVoxelOctreeRayTraceWithCompletion(origin, direction, length, distance, rangeComplete);
    if (level == 1u)
        return BurtGIVoxelOctreeRayTraceResourceWithCompletion(
            _BurtGISceneVoxelClipmap1GeometryReadTexture,
            _BurtGISceneVoxelClipmap1OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap1OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap1OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap1OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap1OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap1OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[1], 1.0,
            origin, direction, length, distance, rangeComplete);
    if (level == 2u)
        return BurtGIVoxelOctreeRayTraceResourceWithCompletion(
            _BurtGISceneVoxelClipmap2GeometryReadTexture,
            _BurtGISceneVoxelClipmap2OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap2OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap2OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap2OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap2OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap2OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[2], 1.0,
            origin, direction, length, distance, rangeComplete);
    if (level == 3u)
        return BurtGIVoxelOctreeRayTraceResourceWithCompletion(
            _BurtGISceneVoxelClipmap3GeometryReadTexture,
            _BurtGISceneVoxelClipmap3OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap3OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap3OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap3OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap3OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap3OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[3], 1.0,
            origin, direction, length, distance, rangeComplete);
    if (level == 4u)
        return BurtGIVoxelOctreeRayTraceResourceWithCompletion(
            _BurtGISceneVoxelClipmap4GeometryReadTexture,
            _BurtGISceneVoxelClipmap4OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap4OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap4OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap4OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap4OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap4OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[4], 1.0,
            origin, direction, length, distance, rangeComplete);
    if (level == 5u)
        return BurtGIVoxelOctreeRayTraceResourceWithCompletion(
            _BurtGISceneVoxelClipmap5GeometryReadTexture,
            _BurtGISceneVoxelClipmap5OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap5OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap5OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap5OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap5OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap5OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[5], 1.0,
            origin, direction, length, distance, rangeComplete);
    return false;
}

// Parallel rays outside an axis slab miss; parallel rays inside do not restrict
// the interval. Avoid reciprocal/sign ambiguity at zero direction components.
bool BurtGIScreenProbeVoxelOctreeInterval(
    float3 origin, float3 direction, float4 bounds,
    out float entry, out float exitDistance)
{
    float3 local = origin - bounds.xyz;
    bool3 parallel = abs(direction) < 1e-8;
    if (any(parallel & (abs(local) > bounds.www)))
    {
        entry = 0.0;
        exitDistance = 0.0;
        return false;
    }
    float3 inv = rcp(lerp(direction, 1.0, parallel));
    float3 a = (-bounds.www - local) * inv;
    float3 b = (bounds.www - local) * inv;
    float3 nearT = lerp(min(a, b), -1e20, parallel);
    float3 farT = lerp(max(a, b), 1e20, parallel);
    entry = max(max(nearT.x, nearT.y), nearT.z);
    exitDistance = min(min(farT.x, farT.y), farT.z);
    return exitDistance > max(entry, 0.0);
}

bool BurtGIScreenProbeVoxelOctreeTraceClipmapsWithCompletion(
    float3 origin, float3 direction, float maxDistance,
    out float hitDistance, out uint hitLevel, out bool rangeComplete)
{
    rangeComplete = false;
    hitDistance = maxDistance;
    hitLevel = 0u;
    uint firstLevel = 0u;
    [unroll]
    for (uint candidateLevel = 0u; candidateLevel < 6u; ++candidateLevel)
    {
        float4 bounds = BurtGIScreenProbeVoxelOctreeBounds(candidateLevel);
        if (BurtGIScreenProbeVoxelOctreeLevelValid(candidateLevel) &&
            all(abs(origin - bounds.xyz) <= bounds.www))
        {
            firstLevel = candidateLevel;
            break;
        }
    }

    float tMin = 0.0;
    bool intervalsComplete = true;
    [loop]
    for (uint level = firstLevel; level < 6u && tMin < maxDistance; ++level)
    {
        if (!BurtGIScreenProbeVoxelOctreeLevelValid(level)) continue;
        float entry, exitDistance;
        if (!BurtGIScreenProbeVoxelOctreeInterval(origin, direction,
                BurtGIScreenProbeVoxelOctreeBounds(level), entry, exitDistance)) continue;
        float start = max(tMin, max(entry, 0.0));
        float end = min(maxDistance, exitDistance);
        if (start >= end) continue;
        float distance;
        bool intervalComplete;
        intervalsComplete = intervalsComplete && start <= tMin;
        if (BurtGIScreenProbeVoxelOctreeTraceLevel(level,
                origin + direction * start, direction, end - start, distance, intervalComplete))
        {
            hitDistance = start + distance;
            hitLevel = level;
            return true; // Black occupied hits also stop the fine-to-coarse chain.
        }
        // Do not re-read overlapping coarse occupancy in an already traced fine
        // region: that can reintroduce coarse self-occlusion and false blockers.
        intervalsComplete = intervalsComplete && intervalComplete;
        tMin = end;
    }
    rangeComplete = maxDistance > 0.0 && tMin >= maxDistance && intervalsComplete;
    return false;
}

bool BurtGIScreenProbeVoxelOctreeTraceClipmaps(
    float3 origin, float3 direction, float maxDistance,
    out float hitDistance, out uint hitLevel)
{
    bool rangeComplete;
    return BurtGIScreenProbeVoxelOctreeTraceClipmapsWithCompletion(origin, direction, maxDistance, hitDistance, hitLevel, rangeComplete);
}
#endif
