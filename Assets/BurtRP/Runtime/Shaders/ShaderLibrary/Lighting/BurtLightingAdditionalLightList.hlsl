// Tile/cluster light-list lookup for Burt additional lights.
#ifndef BURT_LIGHTING_ADDITIONAL_LIGHT_LIST_INCLUDED
#define BURT_LIGHTING_ADDITIONAL_LIGHT_LIST_INCLUDED
#if defined(BURT_USE_TILED_LIGHTING)
StructuredBuffer<uint> _BurtTileLightCountBuffer;
StructuredBuffer<uint> _BurtTileLightListBuffer;
StructuredBuffer<uint2> _BurtTileLightOffsetBuffer;
float4 _BurtTileLightGridParams;
float _BurtTileLightCountBufferEnabled;
StructuredBuffer<uint> _BurtClusterLightCountBuffer;
StructuredBuffer<uint> _BurtClusterLightListBuffer;
StructuredBuffer<uint2> _BurtClusterLightOffsetBuffer;
float4 _BurtClusterLightGridParams;
float4 _BurtClusterLightDepthParams;
float4 _BurtClusterLightWorldToViewZ;
float _BurtClusterLightBufferEnabled;
#endif
bool BurtUseTileLightList()
{
#if defined(BURT_USE_TILED_LIGHTING)
    int additionalLightCount = BurtGetAdditionalLightCount();
    return _BurtTileLightCountBufferEnabled > 0.5f &&
        additionalLightCount > 0 &&
        _BurtTileLightGridParams.x > 0.5f &&
        _BurtTileLightGridParams.y > 0.5f &&
        _BurtTileLightGridParams.w > 0.5f;
#else
    return false;
#endif
}

float2 BurtGetTileLightLookupUV(float2 screenUV)
{
    float2 tileUV = screenUV;
#if UNITY_UV_STARTS_AT_TOP
    tileUV.y = 1.0f - tileUV.y;
#endif
    return saturate(tileUV);
}

uint BurtGetTileLightIndex(float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    float2 tileUV = BurtGetTileLightLookupUV(screenUV);
    uint tileCountX = (uint)max((int)round(_BurtTileLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtTileLightGridParams.y), 1);
    uint tileX = (uint)floor(tileUV.x * (float)tileCountX);
    uint tileY = (uint)floor(tileUV.y * (float)tileCountY);
    tileX = min(tileX, tileCountX - 1u);
    tileY = min(tileY, tileCountY - 1u);
    return tileY * tileCountX + tileX;
#else
    return 0u;
#endif
}

uint2 BurtGetTileLightRange(float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseTileLightList())
    {
        return uint2(0u, 0u);
    }

    uint tileIndex = BurtGetTileLightIndex(screenUV);
    uint2 range = _BurtTileLightOffsetBuffer[tileIndex];
    uint maxLightsPerTile = (uint)max((int)round(_BurtTileLightGridParams.w), 1);
    uint tileCountX = (uint)max((int)round(_BurtTileLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtTileLightGridParams.y), 1);
    uint listCapacity = max(tileCountX * tileCountY * maxLightsPerTile, 1u);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint countBufferCount = min(_BurtTileLightCountBuffer[tileIndex], maxLightsPerTile);
    range.x = min(range.x, listCapacity - 1u);
    range.y = min(range.y, min(countBufferCount, min(maxLightsPerTile, additionalLightCount)));
    range.y = min(range.y, listCapacity - range.x);
    return range;
#else
    return uint2(0u, 0u);
#endif
}

bool BurtTileLightListOverflows(float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseTileLightList())
    {
        return false;
    }

    uint tileIndex = BurtGetTileLightIndex(screenUV);
    uint maxLightsPerTile = (uint)max((int)round(_BurtTileLightGridParams.w), 1);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint storedCapacity = min(maxLightsPerTile, additionalLightCount);
    return _BurtTileLightCountBuffer[tileIndex] > storedCapacity;
#else
    return false;
#endif
}

bool BurtUseClusterLightList()
{
#if defined(BURT_USE_TILED_LIGHTING)
    int additionalLightCount = BurtGetAdditionalLightCount();
    return _BurtClusterLightBufferEnabled > 0.5f &&
        additionalLightCount > 0 &&
        _BurtClusterLightGridParams.x > 0.5f &&
        _BurtClusterLightGridParams.y > 0.5f &&
        _BurtClusterLightGridParams.z > 0.5f &&
        _BurtClusterLightGridParams.w > 0.5f &&
        _BurtClusterLightDepthParams.z > 0.0f;
#else
    return false;
#endif
}

uint BurtGetClusterLightIndex(float2 screenUV, float3 positionWS)
{
#if defined(BURT_USE_TILED_LIGHTING)
    float2 tileUV = BurtGetTileLightLookupUV(screenUV);
    uint tileCountX = (uint)max((int)round(_BurtClusterLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtClusterLightGridParams.y), 1);
    uint depthSliceCount = (uint)max((int)round(_BurtClusterLightGridParams.z), 1);
    uint tileX = (uint)floor(tileUV.x * (float)tileCountX);
    uint tileY = (uint)floor(tileUV.y * (float)tileCountY);
    tileX = min(tileX, tileCountX - 1u);
    tileY = min(tileY, tileCountY - 1u);

    float viewDepth = max(dot(float4(positionWS, 1.0f), _BurtClusterLightWorldToViewZ), _BurtClusterLightDepthParams.x);
    float depth01 = saturate(log(viewDepth / max(_BurtClusterLightDepthParams.x, 0.0001f)) * _BurtClusterLightDepthParams.z);
    uint depthSlice = (uint)floor(depth01 * (float)depthSliceCount);
    depthSlice = min(depthSlice, depthSliceCount - 1u);
    return depthSlice * tileCountX * tileCountY + tileY * tileCountX + tileX;
#else
    return 0u;
#endif
}

uint2 BurtGetClusterLightRange(float2 screenUV, float3 positionWS)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseClusterLightList())
    {
        return uint2(0u, 0u);
    }

    uint clusterIndex = BurtGetClusterLightIndex(screenUV, positionWS);
    uint2 range = _BurtClusterLightOffsetBuffer[clusterIndex];
    uint tileCountX = (uint)max((int)round(_BurtClusterLightGridParams.x), 1);
    uint tileCountY = (uint)max((int)round(_BurtClusterLightGridParams.y), 1);
    uint depthSliceCount = (uint)max((int)round(_BurtClusterLightGridParams.z), 1);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint maxLightsPerCluster = (uint)max((int)round(_BurtClusterLightGridParams.w), 1);
    uint maxPackedListCapacity = max(tileCountX * tileCountY * depthSliceCount * min(maxLightsPerCluster, additionalLightCount), 1u);
    uint countBufferCount = min(_BurtClusterLightCountBuffer[clusterIndex], maxLightsPerCluster);
    range.x = min(range.x, maxPackedListCapacity - 1u);
    range.y = min(range.y, min(countBufferCount, min(maxLightsPerCluster, additionalLightCount)));
    range.y = min(range.y, maxPackedListCapacity - range.x);
    return range;
#else
    return uint2(0u, 0u);
#endif
}

bool BurtClusterLightListOverflows(float2 screenUV, float3 positionWS)
{
#if defined(BURT_USE_TILED_LIGHTING)
    if (!BurtUseClusterLightList())
    {
        return false;
    }

    uint clusterIndex = BurtGetClusterLightIndex(screenUV, positionWS);
    uint maxLightsPerCluster = (uint)max((int)round(_BurtClusterLightGridParams.w), 1);
    uint additionalLightCount = (uint)BurtGetAdditionalLightCount();
    uint storedCapacity = min(maxLightsPerCluster, additionalLightCount);
    return _BurtClusterLightCountBuffer[clusterIndex] > storedCapacity;
#else
    return false;
#endif
}

bool BurtTryGetAdditionalLightListRange(float2 screenUV, float3 positionWS, out uint2 range, out uint useClusterLightList)
{
    range = uint2(0u, 0u);
    useClusterLightList = 0u;
#if defined(BURT_USE_TILED_LIGHTING)
    if (BurtUseClusterLightList() && !BurtClusterLightListOverflows(screenUV, positionWS))
    {
        range = BurtGetClusterLightRange(screenUV, positionWS);
        useClusterLightList = 1u;
        return true;
    }

    if (BurtUseTileLightList() && !BurtTileLightListOverflows(screenUV))
    {
        range = BurtGetTileLightRange(screenUV);
        return true;
    }
#endif

    return false;
}

uint BurtReadAdditionalLightListIndex(uint listIndex, uint useClusterLightList)
{
#if defined(BURT_USE_TILED_LIGHTING)
    return useClusterLightList > 0u ? _BurtClusterLightListBuffer[listIndex] : _BurtTileLightListBuffer[listIndex];
#else
    return 0u;
#endif
}
#endif
