#ifndef BURT_GI_FINE_SCREEN_PROBE_SOURCE_INCLUDED
#define BURT_GI_FINE_SCREEN_PROBE_SOURCE_INCLUDED

#include "BurtGISceneVoxelFineTrace.hlsl"

// One immutable per-camera source snapshot. Bounds and params have six entries:
// params=(COARSE resolution N, logical capacity, readiness bits, step budget).
// ready bits: geometry=1, hierarchy=2, material=4, evaluated lighting=8.
// The C# binder validates owner generation/source epoch/bounds/lighting ticket
// before publishing SourceReady. No legacy dense texture is part of this ABI.
float4 _BurtGIScreenProbeFineBounds[6];
float4 _BurtGIScreenProbeFineParams[6];
uint _BurtGIScreenProbeFineLevelMask;
uint _BurtGIScreenProbeFineSourceReady;

#define BURT_GI_DECLARE_FINE_SCREEN_LEVEL(INDEX) \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##Low; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##High; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##Offsets; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##LeafLow; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##LeafHigh; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##ParentLow; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##ParentHigh; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##RootLow; \
Texture3D<uint> _BurtGIScreenProbeFine##INDEX##RootHigh; \
StructuredBuffer<uint> _BurtGIScreenProbeFine##INDEX##Owners; \
StructuredBuffer<BurtGISceneVoxelFineMaterialData> _BurtGIScreenProbeFine##INDEX##Materials; \
StructuredBuffer<BurtGISceneVoxelFineLightingData> _BurtGIScreenProbeFine##INDEX##Lighting;

BURT_GI_DECLARE_FINE_SCREEN_LEVEL(0)
#if defined(BURT_GI_SCREEN_PROBE_TRACE_VOXEL_OCTREE_CLIPMAPS)
BURT_GI_DECLARE_FINE_SCREEN_LEVEL(1)
BURT_GI_DECLARE_FINE_SCREEN_LEVEL(2)
BURT_GI_DECLARE_FINE_SCREEN_LEVEL(3)
BURT_GI_DECLARE_FINE_SCREEN_LEVEL(4)
BURT_GI_DECLARE_FINE_SCREEN_LEVEL(5)
#endif
#undef BURT_GI_DECLARE_FINE_SCREEN_LEVEL

bool BurtGIFineScreenProbeLevelSelected(uint level)
{
#if !defined(BURT_GI_SCREEN_PROBE_TRACE_VOXEL_OCTREE_CLIPMAPS)
    if (level != 0u) return false;
#endif
    return level < 6u && (_BurtGIScreenProbeFineLevelMask & (1u << level)) != 0u;
}

bool BurtGIFineScreenProbeMetadataValid(uint level)
{
    float4 p = _BurtGIScreenProbeFineParams[level];
    float4 b = _BurtGIScreenProbeFineBounds[level];
    return all(isfinite(p)) && all(isfinite(b)) && b.w > 0.0 &&
        (p.x == 16.0 || p.x == 32.0 || p.x == 64.0 || p.x == 128.0) &&
        p.y >= 0.0 && p.y <= 1048576.0 && p.y == floor(p.y) &&
        p.z >= 0.0 && p.z <= 15.0 && p.z == floor(p.z) &&
        (((uint)p.z & 3u) == 3u) && p.w >= 96.0 && p.w <= 512.0;
}

bool BurtGIFineScreenProbeSourceValid()
{
    if (_BurtGIScreenProbeFineSourceReady == 0u ||
        (_BurtGIScreenProbeFineLevelMask & 63u) == 0u ||
        (_BurtGIScreenProbeFineLevelMask & ~63u) != 0u) return false;
#if !defined(BURT_GI_SCREEN_PROBE_TRACE_VOXEL_OCTREE_CLIPMAPS)
    if (_BurtGIScreenProbeFineLevelMask != 1u) return false;
#endif
    [loop]
    for (uint level = 0u; level < 6u; ++level)
        if (BurtGIFineScreenProbeLevelSelected(level) && !BurtGIFineScreenProbeMetadataValid(level))
            return false;
    return true;
}

// A screen placement is not the known occupied cell center used by the fine
// lighting producer. Retain a geometric source bias only, at FINE resolution;
// never discard an extra coarse-cell interval as if it were certified self.
float BurtGIFineScreenProbeSourceCellSize(float3 sourcePositionWS)
{
    if (!BurtGIFineScreenProbeSourceValid()) return 0.0;
    [loop]
    for (uint level = 0u; level < 6u; ++level)
    {
        if (!BurtGIFineScreenProbeLevelSelected(level)) continue;
        float4 bounds = _BurtGIScreenProbeFineBounds[level];
        if (all(sourcePositionWS >= bounds.xyz - bounds.w) &&
            all(sourcePositionWS < bounds.xyz + bounds.w))
            return (2.0 * bounds.w) / (4.0 * _BurtGIScreenProbeFineParams[level].x);
    }
    return 0.0;
}

// Keeping traversal and payload calls together makes it impossible for this
// adapter to return a fine hit while loading a neighboring coarse RGB value.
#define BURT_GI_TRACE_FINE_SCREEN_LEVEL(INDEX) \
    if (level == INDEX) \
    { \
        BurtGIFineTraceGeometryIntervalNormalizedResourceWithCompletion( \
            _BurtGIScreenProbeFine##INDEX##Low, _BurtGIScreenProbeFine##INDEX##High, \
            _BurtGIScreenProbeFine##INDEX##LeafLow, _BurtGIScreenProbeFine##INDEX##LeafHigh, \
            _BurtGIScreenProbeFine##INDEX##ParentLow, _BurtGIScreenProbeFine##INDEX##ParentHigh, \
            _BurtGIScreenProbeFine##INDEX##RootLow, _BurtGIScreenProbeFine##INDEX##RootHigh, \
            (uint)p.x, ready & 1u, ready & 2u, _BurtGIScreenProbeFineBounds[INDEX], \
            origin, direction, startT, endT, (uint)p.w, result); \
        if (result.occupied != 0u) \
        { \
            uint3 coarseCell = result.fineCoord >> 2u; \
            uint2 mask = uint2(_BurtGIScreenProbeFine##INDEX##Low[coarseCell], \
                              _BurtGIScreenProbeFine##INDEX##High[coarseCell]); \
            BurtGIFineTraceResolvePayload(_BurtGIScreenProbeFine##INDEX##Offsets, \
                _BurtGIScreenProbeFine##INDEX##Owners, _BurtGIScreenProbeFine##INDEX##Materials, \
                _BurtGIScreenProbeFine##INDEX##Lighting, mask, (uint)p.y, \
                ready & 4u, ready & 8u, result); \
        } \
        return; \
    }

void BurtGIFineScreenProbeTraceLevel(uint level, float3 origin, float3 direction,
    float startT, float endT, out BurtGISceneVoxelFineTraceResult result)
{
    result = (BurtGISceneVoxelFineTraceResult)0;
    result.poolIndex = BURT_GI_FINE_INVALID_OFFSET;
    result.distance = endT;
    float4 p = _BurtGIScreenProbeFineParams[level];
    uint ready = (uint)p.z;
    BURT_GI_TRACE_FINE_SCREEN_LEVEL(0)
#if defined(BURT_GI_SCREEN_PROBE_TRACE_VOXEL_OCTREE_CLIPMAPS)
    BURT_GI_TRACE_FINE_SCREEN_LEVEL(1)
    BURT_GI_TRACE_FINE_SCREEN_LEVEL(2)
    BURT_GI_TRACE_FINE_SCREEN_LEVEL(3)
    BURT_GI_TRACE_FINE_SCREEN_LEVEL(4)
    BURT_GI_TRACE_FINE_SCREEN_LEVEL(5)
#endif
}
#undef BURT_GI_TRACE_FINE_SCREEN_LEVEL

void BurtGIFineScreenProbeTrace(float3 origin, float3 normalizedDirection,
    float maxDistance, out BurtGISceneVoxelFineTraceResult result)
{
    result = (BurtGISceneVoxelFineTraceResult)0;
    result.poolIndex = BURT_GI_FINE_INVALID_OFFSET;
    result.distance = maxDistance;
    if (!BurtGIFineScreenProbeSourceValid() || !all(isfinite(origin)) ||
        !all(isfinite(normalizedDirection)) || !isfinite(maxDistance) || maxDistance <= 0.0)
        return;
    float cursor = 0.0;
    [loop]
    for (uint level = 0u; level < 6u; ++level)
    {
        if (!BurtGIFineScreenProbeLevelSelected(level)) continue;
        float4 bounds = _BurtGIScreenProbeFineBounds[level];
        float entryT = 0.0, exitT = 0.0;
        if (!BurtGIFineOccupancyRayBox(origin, normalizedDirection,
                bounds.xyz - bounds.w, bounds.xyz + bounds.w, entryT, exitT)) continue;
        float endT = min(maxDistance, exitT);
        if (endT <= cursor) continue;
        // A containing later level may bridge this gap; it is not safe to
        // report a hit beyond an unqueried gap in a smaller, offset clipmap.
        if (entryT > cursor) continue;
        BurtGIFineScreenProbeTraceLevel(level, origin, normalizedDirection,
            cursor, endT, result);
        if (result.occupied != 0u) return; // Payload failure does not continue.
        if (result.rangeComplete == 0u) return; // Stale, budget or numeric ambiguity.
        cursor = endT;
        // Completion of a clipped interval is not completion of the whole ray.
        result.rangeComplete = 0u;
        if (cursor >= maxDistance)
        {
            result.rangeComplete = 1u;
            result.distance = maxDistance;
            return;
        }
    }
    result.rangeComplete = 0u;
    result.distance = maxDistance;
}

#endif
