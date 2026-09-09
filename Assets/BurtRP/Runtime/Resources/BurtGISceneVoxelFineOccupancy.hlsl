#ifndef BURT_GI_SCENE_VOXEL_FINE_OCCUPANCY_INCLUDED
#define BURT_GI_SCENE_VOXEL_FINE_OCCUPANCY_INCLUDED

// Optional geometric refinement of ONE material/coarse voxel. This helper
// owns no resources and does not select radiance. A black fine hit is still
// occupied; callers must separately validate the associated material source.
// The mask contains 4^3 cells, x + 4*y + 16*z, low word first.
bool BurtGIFineOccupancyContains(uint2 mask, uint3 localCell)
{
    uint bit = localCell.x + localCell.y * 4u + localCell.z * 16u;
    return (((bit < 32u ? mask.x : mask.y) >> (bit & 31u)) & 1u) != 0u;
}

// Compact payload rank of an occupied bit in its 4^3 node. The caller must
// check occupancy and allocation validity before using the returned rank.
uint BurtGIFineOccupancyRank(uint2 mask, uint bit)
{
    if (bit < 32u)
        return countbits(mask.x & ((1u << bit) - 1u));
    return countbits(mask.x) + countbits(mask.y & ((1u << (bit - 32u)) - 1u));
}

// Parallel rays use half-open spatial ownership [min,max). No reciprocal of
// an artificial epsilon direction: that would turn parallel rays into hits.
bool BurtGIFineOccupancyRayBox(
    float3 origin, float3 direction, float3 boxMin, float3 boxMax,
    out float entryT, out float exitT)
{
    entryT = -3.402823466e+38;
    exitT = 3.402823466e+38;
    [unroll]
    for (uint axis = 0u; axis < 3u; ++axis)
    {
        if (direction[axis] == 0.0)
        {
            if (origin[axis] < boxMin[axis] || origin[axis] >= boxMax[axis])
                return false;
        }
        else
        {
            float a = (boxMin[axis] - origin[axis]) / direction[axis];
            float b = (boxMax[axis] - origin[axis]) / direction[axis];
            entryT = max(entryT, min(a, b));
            exitT = min(exitT, max(a, b));
        }
    }
    return exitT > entryT;
}

// Cell at the positive-t side of a boundary. In particular, a ray directed
// toward -X on x=k starts in k-1, not k. Zero directions retain [min,max).
int3 BurtGIFineOccupancyDirectionalCell(float3 gridPosition, float3 direction)
{
    return int3(
        direction.x < 0.0 ? ceil(gridPosition.x) - 1.0 : floor(gridPosition.x),
        direction.y < 0.0 ? ceil(gridPosition.y) - 1.0 : floor(gridPosition.y),
        direction.z < 0.0 ? ceil(gridPosition.z) - 1.0 : floor(gridPosition.z));
}

// Searches only the intersection of [startT,endT) with this coarse cell.
// A miss does NOT certify the rest of the ray: outer HDDA must continue at
// the coarse exit. intervalComplete=false must remain unresolved upstream.
// direction is normalized by the caller; returned hitT is an entry distance,
// without the forward epsilon formerly added to coarse hit positions.
bool BurtGIFineOccupancyTraceCell(
    uint2 mask, uint3 coarseCell, float3 volumeMin, float coarseSize,
    float3 origin, float3 direction, float startT, float endT,
    out float hitT, out uint3 fineCell, out bool intervalComplete,
    out uint visitedCells)
{
    hitT = endT;
    fineCell = 0u;
    intervalComplete = false;
    visitedCells = 0u;
    if (coarseSize <= 0.0 || endT <= startT || !any(direction != 0.0))
        return false;

    float3 coarseMin = volumeMin + (float3)coarseCell * coarseSize;
    float entryT, exitT;
    if (!BurtGIFineOccupancyRayBox(origin, direction, coarseMin,
                                 coarseMin + coarseSize, entryT, exitT))
    {
        intervalComplete = true;
        return false;
    }
    float t = max(startT, entryT);
    float stopT = min(endT, exitT);
    if (t >= stopT || (mask.x | mask.y) == 0u)
    {
        intervalComplete = true;
        return false;
    }

    float fineSize = coarseSize * 0.25;
    // Slab intersection has already established membership. Clamp only the
    // outer face's floating-point reconstruction, not a genuinely outside ray.
    float3 local = clamp((origin - coarseMin + direction * t) / fineSize, 0.0, 4.0);
    // A parallel in-box coordinate very close to an outer face can round to
    // exactly 4 after subtraction. The slab test above already proved a
    // positive-length in-box interval, so this cannot select an outside cell.
    int3 cell = clamp(BurtGIFineOccupancyDirectionalCell(local, direction), 0, 3);
    int3 stepCell = int3(sign(direction));
    float3 nextT = 3.402823466e+38;
    [unroll]
    for (uint axis = 0u; axis < 3u; ++axis)
    {
        if (direction[axis] != 0.0)
        {
            float boundary = coarseMin[axis] +
                (float)(cell[axis] + (stepCell[axis] > 0 ? 1 : 0)) * fineSize;
            nextT[axis] = (boundary - origin[axis]) / direction[axis];
        }
    }

    // At most ten positive-length cells can be visited in a 4^3 block.
    // Leave a finite safety budget, and never report budget exhaustion clear.
    [loop]
    for (uint iteration = 0u; iteration < 16u; ++iteration)
    {
        if (any(cell < 0) || any(cell >= 4))
        {
            intervalComplete = t >= stopT;
            return false;
        }
        float crossingT = min(nextT.x, min(nextT.y, nextT.z));
        float cellEnd = min(stopT, crossingT);
        if (cellEnd > t)
        {
            ++visitedCells;
            if (BurtGIFineOccupancyContains(mask, (uint3)cell))
            {
                hitT = t;
                fineCell = coarseCell * 4u + (uint3)cell;
                return true;
            }
        }
        if (crossingT >= stopT)
        {
            intervalComplete = true;
            return false;
        }
        // Advance all tied faces. Edge/corner-only contacts are not cells
        // with positive ray coverage and must not become opaque hits.
        [unroll]
        for (uint axis = 0u; axis < 3u; ++axis)
        {
            if (nextT[axis] <= crossingT)
            {
                cell[axis] += stepCell[axis];
                // Recompute from the integer boundary, rather than accumulating
                // tDelta error until the final face precedes the slab exit.
                float boundary = coarseMin[axis] +
                    (float)(cell[axis] + (stepCell[axis] > 0 ? 1 : 0)) * fineSize;
                nextT[axis] = (boundary - origin[axis]) / direction[axis];
            }
        }
        t = max(t, crossingT);
    }
    return false;
}

#endif
