#ifndef BURT_GI_SCENE_VOXEL_FINE_TRACE_INCLUDED
#define BURT_GI_SCENE_VOXEL_FINE_TRACE_INCLUDED

#include "BurtGISceneVoxelFineData.hlsl"
#include "BurtGISceneVoxelFineOccupancy.hlsl"

// The hierarchy has the existing 16/4/1 COARSE-cell layout, but its leaf
// occupancy MUST be built from OR(fineLow,fineHigh), not coarse material alpha.
// All four readiness flags describe the same bounds/resolution/build epoch.
// A stale/unavailable geometry or hierarchy is unresolved, never empty.
struct BurtGISceneVoxelFineTraceResult
{
    uint occupied;
    uint materialValid;
    uint lightingValid;
    uint rangeComplete;
    uint3 fineCoord;
    uint poolIndex;
    float distance;
    uint hierarchySteps;
    uint fineSteps;
    uint budgetExhausted;
    float4 radiance;
    float4 normalAndValidity;
};

// Readiness belongs to a generation/bounds epoch; shape checks additionally
// reject a wrongly bound level. Such a resource must never certify emptiness.
bool BurtGIFineTraceHierarchyDimensionsValid(
    Texture3D<uint> leafLow, Texture3D<uint> leafHigh,
    Texture3D<uint> parentLow, Texture3D<uint> parentHigh,
    Texture3D<uint> rootLow, Texture3D<uint> rootHigh, uint coarseResolution)
{
    uint width = 0u, height = 0u, depth = 0u;
    uint leafResolution = coarseResolution / 4u;
    uint parentResolution = coarseResolution / 16u;
    uint rootResolution = max(1u, coarseResolution / 64u);
    leafLow.GetDimensions(width, height, depth);
    if (any(uint3(width, height, depth) != leafResolution)) return false;
    leafHigh.GetDimensions(width, height, depth);
    if (any(uint3(width, height, depth) != leafResolution)) return false;
    parentLow.GetDimensions(width, height, depth);
    if (any(uint3(width, height, depth) != parentResolution)) return false;
    parentHigh.GetDimensions(width, height, depth);
    if (any(uint3(width, height, depth) != parentResolution)) return false;
    rootLow.GetDimensions(width, height, depth);
    if (any(uint3(width, height, depth) != rootResolution)) return false;
    rootHigh.GetDimensions(width, height, depth);
    return all(uint3(width, height, depth) == rootResolution);
}

bool BurtGIFineTraceMaskContains(uint low, uint high, uint bit)
{
    return (((bit < 32u ? low : high) >> (bit & 31u)) & 1u) != 0u;
}

// Classify the positive-t side of a boundary. Reconstructing position alone
// can round onto an adjacent cell after a long empty-space jump. Correct that
// candidate against boundary times computed from the ORIGINAL ray, without
// adding an epsilon that could jump over the first occupied fine cell.
int3 BurtGIFineTraceCoarseCellAtT(
    float3 origin, float3 direction, float3 volumeMin, float coarseSize,
    uint resolution, float t)
{
    float3 grid = (origin - volumeMin + direction * t) / coarseSize;
    int3 cell = clamp(BurtGIFineOccupancyDirectionalCell(grid, direction),
        0, (int)resolution - 1);
    [unroll]
    for (uint axis = 0u; axis < 3u; ++axis)
    {
        if (direction[axis] != 0.0)
        {
            float lo = volumeMin[axis] + (float)cell[axis] * coarseSize;
            float hi = volumeMin[axis] + (float)(cell[axis] + 1) * coarseSize;
            float a = (lo - origin[axis]) / direction[axis];
            float b = (hi - origin[axis]) / direction[axis];
            int stepCell = direction[axis] > 0.0 ? 1 : -1;
            if (t < min(a, b)) cell[axis] -= stepCell;
            else if (t >= max(a, b)) cell[axis] += stepCell;
        }
    }
    return cell;
}

void BurtGIFineTraceResolvePayload(
    Texture3D<uint> offsets,
    StructuredBuffer<uint> owners,
    StructuredBuffer<BurtGISceneVoxelFineMaterialData> materials,
    StructuredBuffer<BurtGISceneVoxelFineLightingData> lighting,
    uint2 mask, uint capacity, uint materialReady, uint lightingReady,
    inout BurtGISceneVoxelFineTraceResult result)
{
    // The geometric hit is already final. Missing storage/validity must not
    // turn a black/unknown opaque hit into a miss or substitute coarse RGB.
    if (capacity == 0u || materialReady == 0u) return;
    uint offset = offsets[result.fineCoord >> 2u];
    uint bit = BurtGIFineCellBit(result.fineCoord & 3u);
    uint rank = BurtGIFineMaskRank(mask, bit);
    if (offset == BURT_GI_FINE_INVALID_OFFSET || offset >= capacity ||
        rank >= capacity - offset) return;
    uint index = offset + rank;
    uint ownerCount = 0u, ownerStride = 0u, materialCount = 0u, materialStride = 0u;
    owners.GetDimensions(ownerCount, ownerStride);
    materials.GetDimensions(materialCount, materialStride);
    if (ownerStride != 4u || materialStride != 24u ||
        index >= ownerCount || index >= materialCount ||
        owners[index] == BURT_GI_FINE_INVALID_OFFSET) return;
    result.poolIndex = index;
    BurtGISceneVoxelFineMaterialData material = materials[index];
    float4 albedo = BurtGIFineUnpackHalf4(material.albedoAndValid);
    float4 geometry = BurtGIFineUnpackHalf4(material.normalAndValid);
    float4 emission = BurtGIFineUnpackHalf4(material.emissionAndValid);
    if (all(isfinite(geometry)) && geometry.a > 0.001)
        result.normalAndValidity = geometry;
    result.materialValid = all(isfinite(albedo)) && all(isfinite(geometry)) &&
        all(isfinite(emission)) && albedo.a > 0.001 && geometry.a > 0.001 &&
        emission.a > 0.001 ? 1u : 0u;
    if (result.materialValid == 0u || lightingReady == 0u) return;
    uint lightingCount = 0u, lightingStride = 0u;
    lighting.GetDimensions(lightingCount, lightingStride);
    if (lightingStride != 16u || index >= lightingCount) return;
    BurtGISceneVoxelFineLightingData value = lighting[index];
    float4 radiance = BurtGIFineUnpackHalf4(value.radianceAndValid);
    float4 normal = BurtGIFineUnpackHalf4(value.normalAndValid);
    if (!all(isfinite(radiance)) || !all(isfinite(normal)) ||
        radiance.a <= 0.001 || normal.a <= 0.001) return;
    result.lightingValid = 1u;
    result.radiance = radiance;
    result.normalAndValidity = normal;
}

// A miss has rangeComplete=1 only when ALL [minDistance,maxDistance) was inside
// this ready volume and traversed. On a hit [minDistance,first opaque hit] was
// covered; the interval beyond an opaque hit is irrelevant and untraversed.
// An outside-volume part of the requested interval remains incomplete even if
// an in-volume hit exists. Distances always refer to the ORIGINAL originWS.
// minDistance can skip a caller-proven starting fine cell; it must be that
// cell's exact exit, not a normal bias/epsilon that can skip an adjacent blocker.
// Local TraceCell's intervalComplete is only consulted on a MISS: on HIT that
// helper deliberately leaves it false because the rest of its cell was not
// traversed. occupied and lightingValid are independent (valid black is a hit).
// This is the ONE traversal core. Eight geometry SRVs, no payload resources.
void BurtGIFineTraceGeometryIntervalCore(
    Texture3D<uint> fineLow,
    Texture3D<uint> fineHigh,
    Texture3D<uint> leafLow,
    Texture3D<uint> leafHigh,
    Texture3D<uint> parentLow,
    Texture3D<uint> parentHigh,
    Texture3D<uint> rootLow,
    Texture3D<uint> rootHigh,
    uint coarseResolution, uint geometryReady, uint hierarchyReady,
    float4 centerExtent, float3 originWS, float3 directionWS,
    float minDistance, float maxDistance, uint stepBudget, bool directionIsNormalized,
    out BurtGISceneVoxelFineTraceResult result)
{
    // An explicit out aggregate avoids FXC's early struct-return X4000 path.
    result = (BurtGISceneVoxelFineTraceResult)0;
    result.distance = isfinite(maxDistance) && maxDistance > 0.0 ? maxDistance : 0.0;
    result.poolIndex = BURT_GI_FINE_INVALID_OFFSET;
    if (geometryReady == 0u || hierarchyReady == 0u ||
        (coarseResolution != 16u && coarseResolution != 32u &&
         coarseResolution != 64u && coarseResolution != 128u) ||
        !all(isfinite(centerExtent)) || centerExtent.w <= 0.0 ||
        !all(isfinite(originWS)) || !all(isfinite(directionWS)) ||
        (asuint(minDistance) & 0x7f800000u) == 0x7f800000u || minDistance < 0.0 ||
        !isfinite(maxDistance) || maxDistance <= minDistance) return;
    float lengthSq = dot(directionWS, directionWS);
    if (!isfinite(lengthSq) || lengthSq <= 0.0) return;
    uint width = 0u, height = 0u, depth = 0u;
    fineLow.GetDimensions(width, height, depth);
    if (any(uint3(width, height, depth) != coarseResolution)) return;
    fineHigh.GetDimensions(width, height, depth);
    if (any(uint3(width, height, depth) != coarseResolution)) return;
    if (!BurtGIFineTraceHierarchyDimensionsValid(leafLow, leafHigh,
        parentLow, parentHigh, rootLow, rootHigh, coarseResolution)) return;

    // A caller-computed self-cell exit belongs to that EXACT direction. A
    // second normalization can move minDistance back inside the starting cell.
    float3 direction = directionIsNormalized ? directionWS : directionWS * rsqrt(lengthSq);
    float3 volumeMin = centerExtent.xyz - centerExtent.w;
    float3 volumeMax = centerExtent.xyz + centerExtent.w;
    float coarseSize = (centerExtent.w * 2.0) / (float)coarseResolution;
    if (!all(isfinite(volumeMin)) || !all(isfinite(volumeMax)) ||
        !isfinite(coarseSize) || coarseSize <= 0.0) return;
    float entryT = 0.0, exitT = 0.0;
    if (!BurtGIFineOccupancyRayBox(originWS, direction, volumeMin, volumeMax,
                                 entryT, exitT)) return;
    float t = max(minDistance, entryT);
    float stopT = min(maxDistance, exitT);
    if (t >= stopT) return;
    // Preserve at least the existing coarse traversal's 96-step budget.
    // 512 bounds the worst-case 3*N-2 coarse intersections at N<=128.
    uint budget = clamp(stepBudget, 96u, 512u);
    [loop]
    for (uint iteration = 0u; iteration < 512u; ++iteration)
    {
        if (iteration >= budget)
        {
            result.budgetExhausted = 1u;
            return;
        }
        int3 signedCell = BurtGIFineTraceCoarseCellAtT(originWS, direction,
            volumeMin, coarseSize, coarseResolution, t);
        if (any(signedCell < 0) || any(signedCell >= (int)coarseResolution))
            return;
        uint3 cell = (uint3)signedCell;
        ++result.hierarchySteps;
        uint3 parent = cell >> 4u;
        uint3 leaf = cell >> 2u;
        uint3 root = parent >> 2u;
        uint bit = BurtGIFineCellBit(parent & 3u);
        bool occupied = BurtGIFineTraceMaskContains(rootLow[root], rootHigh[root], bit);
        uint scale = 16u;
        if (occupied)
        {
            bit = BurtGIFineCellBit(leaf & 3u);
            occupied = BurtGIFineTraceMaskContains(parentLow[parent], parentHigh[parent], bit);
            scale = 4u;
        }
        if (occupied)
        {
            bit = BurtGIFineCellBit(cell & 3u);
            occupied = BurtGIFineTraceMaskContains(leafLow[leaf], leafHigh[leaf], bit);
            scale = 1u;
        }
        uint3 blockMinCell = (cell / scale) * scale;
        uint3 blockMaxCell = min(blockMinCell + scale, coarseResolution);
        float3 crossing = 3.402823466e+38;
        [unroll]
        for (uint axis = 0u; axis < 3u; ++axis)
        {
            if (direction[axis] != 0.0)
            {
                uint boundaryCell = direction[axis] > 0.0 ? blockMaxCell[axis] : blockMinCell[axis];
                float boundary = volumeMin[axis] + (float)boundaryCell * coarseSize;
                crossing[axis] = (boundary - originWS[axis]) / direction[axis];
            }
        }
        float crossingT = min(crossing.x, min(crossing.y, crossing.z));
        float cellStopT = min(stopT, crossingT);
        if (cellStopT <= t) return; // Numeric ambiguity is not an empty proof.
        if (occupied)
        {
            uint2 mask = uint2(fineLow[cell], fineHigh[cell]);
            float hitT = cellStopT;
            uint3 fineCell = 0u;
            bool intervalComplete = false;
            uint visited = 0u;
            bool hit = BurtGIFineOccupancyTraceCell(mask, cell, volumeMin,
                coarseSize, originWS, direction, t, cellStopT,
                hitT, fineCell, intervalComplete, visited);
            result.fineSteps += visited;
            if (hit)
            {
                result.occupied = 1u;
                result.distance = hitT;
                result.fineCoord = fineCell;
                result.rangeComplete = entryT <= minDistance ? 1u : 0u;
                return;
            }
            if (!intervalComplete) return;
        }
        if (cellStopT >= stopT)
        {
            result.rangeComplete = entryT <= minDistance && exitT >= maxDistance ? 1u : 0u;
            return;
        }
        t = crossingT;
    }
    result.budgetExhausted = 1u;
    return;
}

// Normalizes exactly once, retaining the original full-query convention.
void BurtGIFineTraceGeometryIntervalResourceWithCompletion(
    Texture3D<uint> fineLow, Texture3D<uint> fineHigh,
    Texture3D<uint> leafLow, Texture3D<uint> leafHigh,
    Texture3D<uint> parentLow, Texture3D<uint> parentHigh,
    Texture3D<uint> rootLow, Texture3D<uint> rootHigh,
    uint coarseResolution, uint geometryReady, uint hierarchyReady,
    float4 centerExtent, float3 originWS, float3 directionWS,
    float minDistance, float maxDistance, uint stepBudget,
    out BurtGISceneVoxelFineTraceResult result)
{
    BurtGIFineTraceGeometryIntervalCore(
        fineLow, fineHigh, leafLow, leafHigh, parentLow, parentHigh, rootLow, rootHigh,
        coarseResolution, geometryReady, hierarchyReady, centerExtent,
        originWS, directionWS, minDistance, maxDistance, stepBudget, false, result);
}

// The caller must supply a normalized direction and compute min/max from it.
// Finite/nonzero checks still apply, but this entry does NOT normalize again.
// This preserves the exact boundary parameter used to omit a known self cell.
void BurtGIFineTraceGeometryIntervalNormalizedResourceWithCompletion(
    Texture3D<uint> fineLow, Texture3D<uint> fineHigh,
    Texture3D<uint> leafLow, Texture3D<uint> leafHigh,
    Texture3D<uint> parentLow, Texture3D<uint> parentHigh,
    Texture3D<uint> rootLow, Texture3D<uint> rootHigh,
    uint coarseResolution, uint geometryReady, uint hierarchyReady,
    float4 centerExtent, float3 originWS, float3 normalizedDirectionWS,
    float minDistance, float maxDistance, uint stepBudget,
    out BurtGISceneVoxelFineTraceResult result)
{
    BurtGIFineTraceGeometryIntervalCore(
        fineLow, fineHigh, leafLow, leafHigh, parentLow, parentHigh, rootLow, rootHigh,
        coarseResolution, geometryReady, hierarchyReady, centerExtent,
        originWS, normalizedDirectionWS, minDistance, maxDistance, stepBudget, true, result);
}

// Plain geometry query preserves the original [0,maxDistance) convention.
// Payload fields stay zero/invalid, including for a black opaque hit.
void BurtGIFineTraceGeometryResourceWithCompletion(
    Texture3D<uint> fineLow, Texture3D<uint> fineHigh,
    Texture3D<uint> leafLow, Texture3D<uint> leafHigh,
    Texture3D<uint> parentLow, Texture3D<uint> parentHigh,
    Texture3D<uint> rootLow, Texture3D<uint> rootHigh,
    uint coarseResolution, uint geometryReady, uint hierarchyReady,
    float4 centerExtent, float3 originWS, float3 directionWS,
    float maxDistance, uint stepBudget, out BurtGISceneVoxelFineTraceResult result)
{
    BurtGIFineTraceGeometryIntervalResourceWithCompletion(
        fineLow, fineHigh, leafLow, leafHigh, parentLow, parentHigh, rootLow, rootHigh,
        coarseResolution, geometryReady, hierarchyReady, centerExtent,
        originWS, directionWS, 0.0, maxDistance, stepBudget, result);
}

// Compatibility wrapper: same API and results as 445. Geometry does not
// depend on allocation or light energy; only the final occupied cell's payload
// is read. Lighting's geometry-only caller never binds these extra four SRVs.
void BurtGIFineTraceResourceWithCompletion(
    Texture3D<uint> fineLow, Texture3D<uint> fineHigh, Texture3D<uint> offsets,
    Texture3D<uint> leafLow, Texture3D<uint> leafHigh,
    Texture3D<uint> parentLow, Texture3D<uint> parentHigh,
    Texture3D<uint> rootLow, Texture3D<uint> rootHigh,
    StructuredBuffer<uint> owners,
    StructuredBuffer<BurtGISceneVoxelFineMaterialData> materials,
    StructuredBuffer<BurtGISceneVoxelFineLightingData> lighting,
    uint coarseResolution, uint capacity,
    uint geometryReady, uint hierarchyReady, uint materialReady, uint lightingReady,
    float4 centerExtent, float3 originWS, float3 directionWS,
    float maxDistance, uint stepBudget, out BurtGISceneVoxelFineTraceResult result)
{
    BurtGIFineTraceGeometryResourceWithCompletion(
        fineLow, fineHigh, leafLow, leafHigh, parentLow, parentHigh, rootLow, rootHigh,
        coarseResolution, geometryReady, hierarchyReady, centerExtent,
        originWS, directionWS, maxDistance, stepBudget, result);
    if (result.occupied != 0u)
    {
        uint3 coarseCell = result.fineCoord >> 2u;
        uint2 mask = uint2(fineLow[coarseCell], fineHigh[coarseCell]);
        BurtGIFineTraceResolvePayload(offsets, owners, materials, lighting,
            mask, capacity, materialReady, lightingReady, result);
    }
}

#endif
