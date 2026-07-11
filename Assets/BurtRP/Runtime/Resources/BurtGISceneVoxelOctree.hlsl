#ifndef BURT_GI_SCENE_VOXEL_OCTREE_INCLUDED
#define BURT_GI_SCENE_VOXEL_OCTREE_INCLUDED

bool BurtGIVoxelOctreeMaskContains(uint lowMask, uint highMask, uint bitIndex)
{
    uint word = bitIndex < 32u ? lowMask : highMask;
    return ((word >> (bitIndex & 31u)) & 1u) != 0u;
}

bool BurtGIVoxelOctreeContainsVoxel(uint3 voxelCoord)
{
    uint3 parentCoord = voxelCoord >> 4u;
    uint3 leafCoord = voxelCoord >> 2u;
    uint rootBit = parentCoord.x + parentCoord.y * 4u + parentCoord.z * 16u;
    if (!BurtGIVoxelOctreeMaskContains(
            _BurtGISceneVoxelOctreeRootLowTexture[uint3(0u, 0u, 0u)],
            _BurtGISceneVoxelOctreeRootHighTexture[uint3(0u, 0u, 0u)],
            rootBit))
    {
        return false;
    }

    uint3 parentChild = leafCoord & 3u;
    uint parentBit = parentChild.x + parentChild.y * 4u + parentChild.z * 16u;
    if (!BurtGIVoxelOctreeMaskContains(
            _BurtGISceneVoxelOctreeParentLowTexture[parentCoord],
            _BurtGISceneVoxelOctreeParentHighTexture[parentCoord],
            parentBit))
    {
        return false;
    }

    uint3 leafChild = voxelCoord & 3u;
    uint leafBit = leafChild.x + leafChild.y * 4u + leafChild.z * 16u;
    return BurtGIVoxelOctreeMaskContains(
        _BurtGISceneVoxelOctreeLeafLowTexture[leafCoord],
        _BurtGISceneVoxelOctreeLeafHighTexture[leafCoord],
        leafBit);
}

bool BurtGIVoxelOctreeRayTrace(float3 originWS, float3 directionWS, float maxDistance, out float hitDistance)
{
    hitDistance = maxDistance;
    if (_BurtGISceneVoxelOctreeValid <= 0.5 || maxDistance <= 0.0)
    {
        return false;
    }

    uint width;
    uint height;
    uint depth;
    _BurtGISceneVoxelGeometryReadTexture.GetDimensions(width, height, depth);
    uint3 volumeSize = max(uint3(width, height, depth), uint3(1u, 1u, 1u));
    float4 centerExtent = _BurtGISceneVoxelCenterExtent;
    float extent = max(centerExtent.w, 0.001);
    float3 boxMin = centerExtent.xyz - extent;
    float3 boxMax = centerExtent.xyz + extent;
    float3 direction = normalize(directionWS);
    float3 invDirection = rcp(max(abs(direction), 0.000001)) * sign(direction + 0.000001);
    float3 t0 = (boxMin - originWS) * invDirection;
    float3 t1 = (boxMax - originWS) * invDirection;
    float entryDistance = max(max(min(t0.x, t1.x), min(t0.y, t1.y)), min(t0.z, t1.z));
    float exitDistance = min(min(max(t0.x, t1.x), max(t0.y, t1.y)), max(t0.z, t1.z));
    float voxelSizeWS = extent * 2.0 / max((float)volumeSize.x, 1.0);
    float traceDistance = max(0.0, entryDistance) + voxelSizeWS * 0.001;
    float traceEnd = min(maxDistance, exitDistance);
    if (traceDistance >= traceEnd)
    {
        return false;
    }

    [loop]
    for (uint stepIndex = 0u; stepIndex < 96u && traceDistance < traceEnd; ++stepIndex)
    {
        float3 positionWS = originWS + direction * traceDistance;
        float3 localPosition = clamp(positionWS - boxMin, 0.0, extent * 2.0 - voxelSizeWS * 0.001);
        uint3 voxelCoord = min((uint3)(localPosition / voxelSizeWS), volumeSize - 1u);
        uint3 parentCoord = voxelCoord >> 4u;
        uint3 leafCoord = voxelCoord >> 2u;
        uint rootBit = parentCoord.x + parentCoord.y * 4u + parentCoord.z * 16u;
        bool rootOccupied = BurtGIVoxelOctreeMaskContains(
            _BurtGISceneVoxelOctreeRootLowTexture[uint3(0u, 0u, 0u)],
            _BurtGISceneVoxelOctreeRootHighTexture[uint3(0u, 0u, 0u)],
            rootBit);
        uint levelScale = 16u;
        bool occupied = rootOccupied;
        if (occupied)
        {
            uint3 parentChild = leafCoord & 3u;
            uint parentBit = parentChild.x + parentChild.y * 4u + parentChild.z * 16u;
            occupied = BurtGIVoxelOctreeMaskContains(
                _BurtGISceneVoxelOctreeParentLowTexture[parentCoord],
                _BurtGISceneVoxelOctreeParentHighTexture[parentCoord],
                parentBit);
            levelScale = 4u;
        }
        if (occupied)
        {
            uint3 leafChild = voxelCoord & 3u;
            uint leafBit = leafChild.x + leafChild.y * 4u + leafChild.z * 16u;
            occupied = BurtGIVoxelOctreeMaskContains(
                _BurtGISceneVoxelOctreeLeafLowTexture[leafCoord],
                _BurtGISceneVoxelOctreeLeafHighTexture[leafCoord],
                leafBit);
            levelScale = 1u;
        }
        if (occupied)
        {
            hitDistance = traceDistance;
            return true;
        }

        float cellSizeWS = voxelSizeWS * (float)levelScale;
        float3 cellIndex = floor(localPosition / cellSizeWS);
        float3 cellMin = boxMin + cellIndex * cellSizeWS;
        float3 cellMax = cellMin + cellSizeWS;
        float3 boundary = lerp(cellMin, cellMax, step(0.0, direction));
        float3 safeDirection = sign(direction + 0.000001) * max(abs(direction), 0.000001);
        float3 distanceToBoundary = max((boundary - positionWS) / safeDirection, voxelSizeWS * 0.001);
        traceDistance += min(min(distanceToBoundary.x, distanceToBoundary.y), distanceToBoundary.z);
    }

    return false;
}

bool BurtGIVoxelOctreeRayTraceResource(
    Texture3D<float4> geometryTexture,
    Texture3D<uint> leafLowTexture,
    Texture3D<uint> leafHighTexture,
    Texture3D<uint> parentLowTexture,
    Texture3D<uint> parentHighTexture,
    Texture3D<uint> rootLowTexture,
    Texture3D<uint> rootHighTexture,
    float4 centerExtent,
    float valid,
    float3 originWS,
    float3 directionWS,
    float maxDistance,
    out float hitDistance)
{
    hitDistance = maxDistance;
    if (valid <= 0.5 || maxDistance <= 0.0)
    {
        return false;
    }

    uint width;
    uint height;
    uint depth;
    geometryTexture.GetDimensions(width, height, depth);
    uint3 volumeSize = max(uint3(width, height, depth), uint3(1u, 1u, 1u));
    float extent = max(centerExtent.w, 0.001);
    float3 boxMin = centerExtent.xyz - extent;
    float3 boxMax = centerExtent.xyz + extent;
    float3 direction = normalize(directionWS);
    float3 invDirection = rcp(max(abs(direction), 0.000001)) * sign(direction + 0.000001);
    float3 t0 = (boxMin - originWS) * invDirection;
    float3 t1 = (boxMax - originWS) * invDirection;
    float entryDistance = max(max(min(t0.x, t1.x), min(t0.y, t1.y)), min(t0.z, t1.z));
    float exitDistance = min(min(max(t0.x, t1.x), max(t0.y, t1.y)), max(t0.z, t1.z));
    float voxelSizeWS = extent * 2.0 / max((float)volumeSize.x, 1.0);
    float traceDistance = max(0.0, entryDistance) + voxelSizeWS * 0.001;
    float traceEnd = min(maxDistance, exitDistance);
    if (traceDistance >= traceEnd)
    {
        return false;
    }

    [loop]
    for (uint stepIndex = 0u; stepIndex < 96u && traceDistance < traceEnd; ++stepIndex)
    {
        float3 positionWS = originWS + direction * traceDistance;
        float3 localPosition = clamp(positionWS - boxMin, 0.0, extent * 2.0 - voxelSizeWS * 0.001);
        uint3 voxelCoord = min((uint3)(localPosition / voxelSizeWS), volumeSize - 1u);
        uint3 parentCoord = voxelCoord >> 4u;
        uint3 leafCoord = voxelCoord >> 2u;
        uint rootBit = parentCoord.x + parentCoord.y * 4u + parentCoord.z * 16u;
        bool occupied = BurtGIVoxelOctreeMaskContains(
            rootLowTexture[uint3(0u, 0u, 0u)],
            rootHighTexture[uint3(0u, 0u, 0u)],
            rootBit);
        uint levelScale = 16u;
        if (occupied)
        {
            uint3 parentChild = leafCoord & 3u;
            uint parentBit = parentChild.x + parentChild.y * 4u + parentChild.z * 16u;
            occupied = BurtGIVoxelOctreeMaskContains(parentLowTexture[parentCoord], parentHighTexture[parentCoord], parentBit);
            levelScale = 4u;
        }
        if (occupied)
        {
            uint3 leafChild = voxelCoord & 3u;
            uint leafBit = leafChild.x + leafChild.y * 4u + leafChild.z * 16u;
            occupied = BurtGIVoxelOctreeMaskContains(leafLowTexture[leafCoord], leafHighTexture[leafCoord], leafBit);
            levelScale = 1u;
        }
        if (occupied)
        {
            hitDistance = traceDistance;
            return true;
        }

        float cellSizeWS = voxelSizeWS * (float)levelScale;
        float3 cellIndex = floor(localPosition / cellSizeWS);
        float3 cellMin = boxMin + cellIndex * cellSizeWS;
        float3 cellMax = cellMin + cellSizeWS;
        float3 boundary = lerp(cellMin, cellMax, step(0.0, direction));
        float3 safeDirection = sign(direction + 0.000001) * max(abs(direction), 0.000001);
        float3 distanceToBoundary = max((boundary - positionWS) / safeDirection, voxelSizeWS * 0.001);
        traceDistance += min(min(distanceToBoundary.x, distanceToBoundary.y), distanceToBoundary.z);
    }

    return false;
}

bool BurtGIVoxelOctreeRayTraceClipmaps(
    float3 originWS,
    float3 directionWS,
    float maxDistance,
    out float hitDistance,
    out uint hitClipmapLevel)
{
    hitDistance = maxDistance;
    hitClipmapLevel = 0u;
    bool hit = false;
    float candidateDistance;
    if (BurtGIVoxelOctreeRayTrace(originWS, directionWS, maxDistance, candidateDistance))
    {
        hit = true;
        hitDistance = candidateDistance;
    }

    if ((_BurtGISceneVoxelClipmapValidMask & 2u) != 0u &&
        BurtGIVoxelOctreeRayTraceResource(
            _BurtGISceneVoxelClipmap1GeometryReadTexture,
            _BurtGISceneVoxelClipmap1OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap1OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap1OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap1OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap1OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap1OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[1],
            1.0,
            originWS,
            directionWS,
            hitDistance,
            candidateDistance) && candidateDistance < hitDistance)
    {
        hit = true;
        hitDistance = candidateDistance;
        hitClipmapLevel = 1u;
    }

    if ((_BurtGISceneVoxelClipmapValidMask & 4u) != 0u &&
        BurtGIVoxelOctreeRayTraceResource(
            _BurtGISceneVoxelClipmap2GeometryReadTexture,
            _BurtGISceneVoxelClipmap2OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap2OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap2OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap2OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap2OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap2OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[2],
            1.0,
            originWS,
            directionWS,
            hitDistance,
            candidateDistance) && candidateDistance < hitDistance)
    {
        hit = true;
        hitDistance = candidateDistance;
        hitClipmapLevel = 2u;
    }

    if ((_BurtGISceneVoxelClipmapValidMask & 8u) != 0u &&
        BurtGIVoxelOctreeRayTraceResource(
            _BurtGISceneVoxelClipmap3GeometryReadTexture,
            _BurtGISceneVoxelClipmap3OctreeLeafLowTexture,
            _BurtGISceneVoxelClipmap3OctreeLeafHighTexture,
            _BurtGISceneVoxelClipmap3OctreeParentLowTexture,
            _BurtGISceneVoxelClipmap3OctreeParentHighTexture,
            _BurtGISceneVoxelClipmap3OctreeRootLowTexture,
            _BurtGISceneVoxelClipmap3OctreeRootHighTexture,
            _BurtGISceneVoxelClipmapCenterExtent[3],
            1.0,
            originWS,
            directionWS,
            hitDistance,
            candidateDistance) && candidateDistance < hitDistance)
    {
        hit = true;
        hitDistance = candidateDistance;
        hitClipmapLevel = 3u;
    }

    return hit;
}

#endif
