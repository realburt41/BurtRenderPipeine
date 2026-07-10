// Additional-light storage, tile/cluster light-list lookup, and additional-shadow diagnostics.
#ifndef BURT_LIGHTING_ADDITIONAL_LIGHTS_INCLUDED
#define BURT_LIGHTING_ADDITIONAL_LIGHTS_INCLUDED

#if !defined(BURT_MAIN_LIGHT_DIRECTION_DECLARED)
#define BURT_MAIN_LIGHT_DIRECTION_DECLARED
float4 _BurtMainLightDirection;
#endif

float4 _BurtMainLightColor;


#define BURT_MAX_ADDITIONAL_LIGHTS 8
float _BurtAdditionalLightCount;
float4 _BurtAdditionalLightPositionAndRange[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightColorAndType[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightDirectionAndSpot[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightSpotParams[BURT_MAX_ADDITIONAL_LIGHTS];

#define BURT_ADDITIONAL_LIGHT_BUFFER_ROWS 4

#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
StructuredBuffer<float4> _BurtAdditionalLightBuffer;
float _BurtAdditionalLightBufferEnabled;
#endif

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

#define BURT_LIGHT_TYPE_DIRECTIONAL (0.0f)
#define BURT_LIGHT_TYPE_POINT (1.0f)
#define BURT_LIGHT_TYPE_SPOT (2.0f)
float BurtSampleAdditionalLightShadow(int lightIndex, float3 positionWS, float3 lightDirectionWS, float3 normalWS, float3 lightPositionWS);
bool BurtGetAdditionalLightShadowProjectionDebug(
    int lightIndex,
    float3 positionWS,
    float3 lightPositionWS,
    float3 lightDirectionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor);

struct BurtLight
{
    float3 DirectionWS;

    float3 Color;

    float ShadowAttenuation;

    float TransmissionShadowAttenuation;

    float TransmissionThickness;
};

// Creates the current main light from BurtRP globals.
BurtLight BurtCreateMainLight(float shadowAttenuation, float transmissionShadowAttenuation, float transmissionThickness)
{
    BurtLight light;
    light.DirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
    light.Color = _BurtMainLightColor.rgb;
    light.ShadowAttenuation = shadowAttenuation;
    light.TransmissionShadowAttenuation = transmissionShadowAttenuation;
    light.TransmissionThickness = transmissionThickness;
    return light;
}

BurtLight BurtCreateMainLight(float shadowAttenuation, float transmissionShadowAttenuation)
{
    return BurtCreateMainLight(shadowAttenuation, transmissionShadowAttenuation, -1.0f);
}

BurtLight BurtCreateMainLight(float shadowAttenuation)
{
    return BurtCreateMainLight(shadowAttenuation, shadowAttenuation);
}

int BurtGetAdditionalLightCount()
{
    return min((int)round(max(_BurtAdditionalLightCount, 0.0f)), BURT_MAX_ADDITIONAL_LIGHTS);
}

bool BurtHasAdditionalLights()
{
    return BurtGetAdditionalLightCount() > 0;
}

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

float4 BurtReadAdditionalLightPositionAndRange(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS];
    }
#endif

    return _BurtAdditionalLightPositionAndRange[lightIndex];
}

float4 BurtReadAdditionalLightColorAndType(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 1];
    }
#endif

    return _BurtAdditionalLightColorAndType[lightIndex];
}

float4 BurtReadAdditionalLightDirectionAndSpot(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 2];
    }
#endif

    return _BurtAdditionalLightDirectionAndSpot[lightIndex];
}

float4 BurtReadAdditionalLightSpotParams(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 3];
    }
#endif

    return _BurtAdditionalLightSpotParams[lightIndex];
}

float BurtEvaluateAdditionalLightDistanceAttenuation(float distanceSquared, float range)
{
    float safeRange = max(range, 0.0001f);
    float rangeFade = saturate(1.0f - distanceSquared / max(safeRange * safeRange, BURT_EPSILON));
    return rangeFade * rangeFade * rcp(max(distanceSquared, 0.25f));
}

BurtLight BurtCreateAdditionalLightInternal(int lightIndex, float3 positionWS, float3 normalWS, bool sampleShadow, float3 shadowPositionWS)
{
    BurtLight light;
    light.DirectionWS = float3(0.0f, 1.0f, 0.0f);
    light.Color = float3(0.0f, 0.0f, 0.0f);
    light.ShadowAttenuation = 1.0f;
    light.TransmissionShadowAttenuation = 1.0f;
    light.TransmissionThickness = -1.0f;

    float4 colorAndType = BurtReadAdditionalLightColorAndType(lightIndex);
    float lightType = colorAndType.w;

    if (lightType < 0.5f)
    {
        light.DirectionWS = BurtSafeNormalize(BurtReadAdditionalLightDirectionAndSpot(lightIndex).xyz);
        light.Color = max(colorAndType.rgb, float3(0.0f, 0.0f, 0.0f));
        return light;
    }

    float4 positionAndRange = BurtReadAdditionalLightPositionAndRange(lightIndex);
    float3 toLight = positionAndRange.xyz - positionWS;
    float distanceSquared = dot(toLight, toLight);
    light.DirectionWS = BurtSafeNormalize(toLight);

    float attenuation = BurtEvaluateAdditionalLightDistanceAttenuation(distanceSquared, positionAndRange.w);

    if (lightType > 1.5f)
    {
        float3 spotDirectionWS = BurtSafeNormalize(BurtReadAdditionalLightDirectionAndSpot(lightIndex).xyz);
        float3 fromLightDirectionWS = -light.DirectionWS;
        float spotCos = dot(fromLightDirectionWS, spotDirectionWS);
        float3 spotParams = BurtReadAdditionalLightSpotParams(lightIndex).xyz;
        float spotFade = saturate((spotCos - spotParams.y) * spotParams.z);
        attenuation *= spotFade * spotFade;
    }

    light.Color = max(colorAndType.rgb, float3(0.0f, 0.0f, 0.0f)) * attenuation;
    light.ShadowAttenuation = sampleShadow && lightType > 0.5f ? BurtSampleAdditionalLightShadow(lightIndex, shadowPositionWS, light.DirectionWS, normalWS, positionAndRange.xyz) : 1.0f;
    light.TransmissionShadowAttenuation = light.ShadowAttenuation;
    return light;
}

BurtLight BurtCreateAdditionalLightInternal(int lightIndex, float3 positionWS, float3 normalWS, bool sampleShadow)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, sampleShadow, positionWS);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, true);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS, float3 shadowPositionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, true, shadowPositionWS);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, float3(0.0f, 0.0f, 0.0f), true);
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, false);
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, float3(0.0f, 0.0f, 0.0f), false);
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float3 normalWS)
{
    float Attenuation = 1.0f;
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, positionWS, normalWS);
        if (dot(AdditionalLight.Color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
        {
            continue;
        }

        Attenuation = min(Attenuation, saturate(AdditionalLight.ShadowAttenuation));
    }

    return Attenuation;
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS)
{
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, float3(0.0f, 0.0f, 0.0f));
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float3 normalWS, float2 screenUV)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, Range, UseClusterLightList))
    {
        return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, normalWS);
    }

    float Attenuation = 1.0f;
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, positionWS, normalWS);
        if (dot(AdditionalLight.Color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
        {
            continue;
        }

        Attenuation = min(Attenuation, saturate(AdditionalLight.ShadowAttenuation));
    }

    return Attenuation;
#else
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, normalWS);
#endif
}

float BurtEvaluateAdditionalShadowAttenuationDebug(float3 positionWS, float2 screenUV)
{
    return BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, float3(0.0f, 0.0f, 0.0f), screenUV);
}

bool BurtTryFillAdditionalLightShadowProjectionDebugForLight(
    int lightIndex,
    float3 positionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    float4 PositionAndRange = BurtReadAdditionalLightPositionAndRange(lightIndex);
    BurtLight AdditionalLight = BurtCreateAdditionalLightUnshadowed(lightIndex, positionWS, normalWS);
    if (dot(AdditionalLight.Color, float3(0.2126f, 0.7152f, 0.0722f)) <= 0.0001f)
    {
        return false;
    }

    return BurtGetAdditionalLightShadowProjectionDebug(
        lightIndex,
        positionWS,
        PositionAndRange.xyz,
        AdditionalLight.DirectionWS,
        normalWS,
        faceColor,
        uvColor,
        depthColor,
        depthDeltaColor);
}

void BurtFillAdditionalLightShadowProjectionDebugData(
    float3 positionWS,
    float3 normalWS,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    int AdditionalLightCount = BurtGetAdditionalLightCount();
    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        if (BurtTryFillAdditionalLightShadowProjectionDebugForLight(LightIndex, positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor))
        {
            return;
        }
    }
}

void BurtFillAdditionalLightShadowProjectionDebugData(
    float3 positionWS,
    float3 normalWS,
    float2 screenUV,
    out float3 faceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 depthDeltaColor)
{
#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(screenUV, positionWS, Range, UseClusterLightList))
    {
        BurtFillAdditionalLightShadowProjectionDebugData(positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor);
        return;
    }

    faceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    depthDeltaColor = float3(0.0f, 0.0f, 0.0f);

    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        if (BurtTryFillAdditionalLightShadowProjectionDebugForLight((int)StoredLightIndex, positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor))
        {
            return;
        }
    }
#else
    BurtFillAdditionalLightShadowProjectionDebugData(positionWS, normalWS, faceColor, uvColor, depthColor, depthDeltaColor);
#endif
}

// Returns the ambient color uploaded by BurtRP.

#endif
