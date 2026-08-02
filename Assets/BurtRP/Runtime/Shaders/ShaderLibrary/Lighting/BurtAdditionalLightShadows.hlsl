// Punctual/additional-light shadow atlas implementation.
#ifndef BURT_ADDITIONAL_LIGHT_SHADOWS_INCLUDED
#define BURT_ADDITIONAL_LIGHT_SHADOWS_INCLUDED

Texture2D _BurtAdditionalLightShadowAtlas;

#define BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT 8
#define BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES 24
#define BURT_ADDITIONAL_LIGHT_SHADOW_POINT_FACE_COUNT 6
#define BURT_ADDITIONAL_LIGHT_SHADOW_TYPE_POINT 1.0f
#define BURT_ADDITIONAL_LIGHT_SHADOW_TYPE_SPOT 2.0f

float4 _BurtAdditionalLightShadowData[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightShadowLightParams[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightShadowSliceAtlasRects[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES];
float4 _BurtAdditionalLightShadowSliceRows0[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES];
float4 _BurtAdditionalLightShadowSliceRows1[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES];
float4 _BurtAdditionalLightShadowSliceRows2[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES];
float4 _BurtAdditionalLightShadowSliceRows3[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES];
float4 _BurtAdditionalLightShadowParams;
float4 _BurtAdditionalLightShadowTexelSize;

float4 BurtTransformWorldToAdditionalLightShadowSlice(float4 positionWS, int sliceIndex)
{
    return float4(
        dot(_BurtAdditionalLightShadowSliceRows0[sliceIndex], positionWS),
        dot(_BurtAdditionalLightShadowSliceRows1[sliceIndex], positionWS),
        dot(_BurtAdditionalLightShadowSliceRows2[sliceIndex], positionWS),
        dot(_BurtAdditionalLightShadowSliceRows3[sliceIndex], positionWS));
}

int BurtSelectPointLightShadowFace(float3 directionWS)
{
    float3 absDirection = abs(directionWS);
    if (absDirection.z >= absDirection.x && absDirection.z >= absDirection.y)
    {
        return directionWS.z >= 0.0f ? 4 : 5;
    }

    if (absDirection.y >= absDirection.x)
    {
        return directionWS.y >= 0.0f ? 2 : 3;
    }

    return directionWS.x >= 0.0f ? 0 : 1;
}

bool BurtResolveAdditionalLightShadowSlice(
    int lightIndex,
    float3 positionWS,
    float3 lightPositionWS,
    float3 lightDirectionWS,
    out float4 shadowData,
    out int sliceIndex,
    out int sliceOffset,
    out bool isPointShadow)
{
    shadowData = float4(0.0f, 0.0f, 0.0f, 0.0f);
    sliceIndex = -1;
    sliceOffset = 0;
    isPointShadow = false;

    if (_BurtAdditionalLightShadowParams.x <= 0.5f)
    {
        return false;
    }

    if (lightIndex < 0 || lightIndex >= BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT)
    {
        return false;
    }

    shadowData = _BurtAdditionalLightShadowData[lightIndex];
    if (shadowData.x <= 0.5f || shadowData.y <= 0.0001f)
    {
        return false;
    }

    float4 lightParams = _BurtAdditionalLightShadowLightParams[lightIndex];
    int firstSliceIndex = (int)(lightParams.x + 0.5f);
    int sliceCount = (int)(lightParams.y + 0.5f);
    if (firstSliceIndex < 0 || firstSliceIndex >= BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES || sliceCount <= 0)
    {
        return false;
    }

    isPointShadow = lightParams.z > (BURT_ADDITIONAL_LIGHT_SHADOW_TYPE_POINT - 0.5f) && lightParams.z < (BURT_ADDITIONAL_LIGHT_SHADOW_TYPE_POINT + 0.5f);
    if (isPointShadow)
    {
        if (sliceCount < BURT_ADDITIONAL_LIGHT_SHADOW_POINT_FACE_COUNT)
        {
            return false;
        }

        float3 fromLightDirectionWS = -BurtSafeNormalize(lightDirectionWS);
        float3 fromLightVectorWS = positionWS - lightPositionWS;
        if (dot(fromLightVectorWS, fromLightVectorWS) > BURT_EPSILON)
        {
            fromLightDirectionWS = BurtSafeNormalize(fromLightVectorWS);
        }

        sliceOffset = BurtSelectPointLightShadowFace(fromLightDirectionWS);
    }

    sliceIndex = firstSliceIndex + min(sliceOffset, max(sliceCount - 1, 0));
    return sliceIndex >= 0 && sliceIndex < BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES;
}

bool BurtResolveAdditionalLightShadowSliceFromData(
    int lightIndex,
    float3 positionWS,
    float3 lightPositionWS,
    float3 lightDirectionWS,
    float4 shadowData,
    float4 lightParams,
    out int sliceIndex,
    out int sliceOffset,
    out bool isPointShadow)
{
    sliceIndex = -1;
    sliceOffset = 0;
    isPointShadow = false;

    if (lightIndex < 0 || lightIndex >= BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT)
    {
        return false;
    }

    if (shadowData.x <= 0.5f || shadowData.y <= 0.0001f)
    {
        return false;
    }

    int firstSliceIndex = (int)(lightParams.x + 0.5f);
    int sliceCount = (int)(lightParams.y + 0.5f);
    if (firstSliceIndex < 0 || firstSliceIndex >= BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES || sliceCount <= 0)
    {
        return false;
    }

    isPointShadow = lightParams.z > (BURT_ADDITIONAL_LIGHT_SHADOW_TYPE_POINT - 0.5f) && lightParams.z < (BURT_ADDITIONAL_LIGHT_SHADOW_TYPE_POINT + 0.5f);
    if (isPointShadow)
    {
        if (sliceCount < BURT_ADDITIONAL_LIGHT_SHADOW_POINT_FACE_COUNT)
        {
            return false;
        }

        float3 fromLightDirectionWS = -BurtSafeNormalize(lightDirectionWS);
        float3 fromLightVectorWS = positionWS - lightPositionWS;
        if (dot(fromLightVectorWS, fromLightVectorWS) > BURT_EPSILON)
        {
            fromLightDirectionWS = BurtSafeNormalize(fromLightVectorWS);
        }

        sliceOffset = BurtSelectPointLightShadowFace(fromLightDirectionWS);
    }

    sliceIndex = firstSliceIndex + min(sliceOffset, max(sliceCount - 1, 0));
    return sliceIndex >= 0 && sliceIndex < BURT_ADDITIONAL_LIGHT_SHADOW_MAX_SLICES;
}

float2 BurtGetAdditionalLightShadowAtlasTexelMargin(int sliceIndex, bool isPointShadow)
{
    float4 atlasRect = _BurtAdditionalLightShadowSliceAtlasRects[sliceIndex];
    float2 texelMargin = max(_BurtAdditionalLightShadowTexelSize.xy, float2(0.0f, 0.0f)) * (isPointShadow ? 3.0f : 1.0f);
    float2 rectSize = max(atlasRect.zw - atlasRect.xy, max(_BurtAdditionalLightShadowTexelSize.xy, float2(0.0f, 0.0f)));
    return min(texelMargin, rectSize * 0.45f);
}

bool BurtIsInsideAdditionalLightShadowAtlas(float3 shadowCoord, int sliceIndex, bool isPointShadow)
{
    float4 atlasRect = _BurtAdditionalLightShadowSliceAtlasRects[sliceIndex];
    float2 texelMargin = BurtGetAdditionalLightShadowAtlasTexelMargin(sliceIndex, isPointShadow);
    bool outsideShadowMap = shadowCoord.x <= atlasRect.x + texelMargin.x
        || shadowCoord.x >= atlasRect.z - texelMargin.x
        || shadowCoord.y <= atlasRect.y + texelMargin.y
        || shadowCoord.y >= atlasRect.w - texelMargin.y
        || shadowCoord.z <= 0.0f
        || shadowCoord.z >= 1.0f;

    return !outsideShadowMap;
}

float BurtApplyAdditionalLightReceiverBias(float projectedDepth)
{
    float receiverBias = max(_BurtAdditionalLightShadowParams.w, 0.0f);
    return saturate(projectedDepth + receiverBias);
}

float3 BurtApplyAdditionalLightReceiverNormalBias(int lightIndex, float3 positionWS, float3 normalWS)
{
    float receiverNormalBias = max(_BurtAdditionalLightShadowLightParams[lightIndex].w, 0.0f);
    if (receiverNormalBias <= 0.0f)
    {
        return positionWS;
    }

    return positionWS + BurtSafeNormalize(normalWS) * receiverNormalBias;
}

float3 BurtResolveAdditionalLightShadowSamplePositionWS(int lightIndex, float3 positionWS, float3 normalWS, float4 shadowData)
{
    if (shadowData.w <= 0.5f)
    {
        return positionWS;
    }

    return BurtApplyAdditionalLightReceiverNormalBias(lightIndex, positionWS, normalWS);
}

float2 BurtClampAdditionalLightShadowUVToRect(float2 shadowUV, int sliceIndex, bool isPointShadow)
{
    float4 atlasRect = _BurtAdditionalLightShadowSliceAtlasRects[sliceIndex];
    float2 texelMargin = BurtGetAdditionalLightShadowAtlasTexelMargin(sliceIndex, isPointShadow);
    return clamp(shadowUV, atlasRect.xy + texelMargin, atlasRect.zw - texelMargin);
}

bool BurtTryProjectAdditionalLightShadowSlice(float3 positionWS, int sliceIndex, out float4 shadowCoord)
{
    shadowCoord = BurtTransformWorldToAdditionalLightShadowSlice(float4(positionWS, 1.0f), sliceIndex);
    if (shadowCoord.w <= 0.00001f)
    {
        shadowCoord = float4(0.0f, 0.0f, 0.0f, 1.0f);
        return false;
    }

    return true;
}

float BurtSampleAdditionalLightShadowRawDepth(float2 shadowUV, int sliceIndex, bool isPointShadow)
{
    float2 clampedUV = BurtClampAdditionalLightShadowUVToRect(shadowUV, sliceIndex, isPointShadow);
    float2 shadowSize = max(_BurtAdditionalLightShadowTexelSize.zw, float2(1.0f, 1.0f));
    int2 pixelCoord = (int2)clamp(floor(clampedUV * shadowSize), float2(0.0f, 0.0f), shadowSize - 1.0f);
    return _BurtAdditionalLightShadowAtlas.Load(int3(pixelCoord, 0)).r;
}

float BurtConvertAdditionalLightShadowDepthToLightDistance(float depth)
{
#if defined(UNITY_REVERSED_Z)
    depth = 1.0f - depth;
#endif
    return saturate(depth);
}

float BurtSampleAdditionalLightShadowCompare(float4 shadowCoord, int sliceIndex, bool isPointShadow)
{
    float3 projectedShadowCoord = shadowCoord.xyz / shadowCoord.w;
    if (!BurtIsInsideAdditionalLightShadowAtlas(projectedShadowCoord, sliceIndex, isPointShadow))
    {
        return 1.0f;
    }

    projectedShadowCoord.xy = BurtClampAdditionalLightShadowUVToRect(projectedShadowCoord.xy, sliceIndex, isPointShadow);
    projectedShadowCoord.z = BurtApplyAdditionalLightReceiverBias(projectedShadowCoord.z);
    return BURT_SAMPLE_SHADOW_CLAMP(_BurtAdditionalLightShadowAtlas, projectedShadowCoord);
}

float BurtSampleAdditionalLightShadow(int lightIndex, float3 positionWS, float3 lightDirectionWS, float3 normalWS, float3 lightPositionWS)
{
    float4 shadowData;
    int sliceIndex;
    int sliceOffset;
    bool isPointShadow;
    if (!BurtResolveAdditionalLightShadowSlice(lightIndex, positionWS, lightPositionWS, lightDirectionWS, shadowData, sliceIndex, sliceOffset, isPointShadow))
    {
        return 1.0f;
    }

    float3 shadowPositionWS = BurtResolveAdditionalLightShadowSamplePositionWS(lightIndex, positionWS, normalWS, shadowData);
    if (isPointShadow)
    {
        float4 lightParams = _BurtAdditionalLightShadowLightParams[lightIndex];
        if (!BurtResolveAdditionalLightShadowSliceFromData(lightIndex, shadowPositionWS, lightPositionWS, lightDirectionWS, shadowData, lightParams, sliceIndex, sliceOffset, isPointShadow))
        {
            return 1.0f;
        }
    }

    float4 shadowCoord;
    if (!BurtTryProjectAdditionalLightShadowSlice(shadowPositionWS, sliceIndex, shadowCoord))
    {
        return 1.0f;
    }

    float rawShadow = BurtSampleAdditionalLightShadowCompare(shadowCoord, sliceIndex, isPointShadow);
    return BurtApplyShadowStrength(rawShadow, shadowData.y);
}

#endif // BURT_ADDITIONAL_LIGHT_SHADOWS_INCLUDED
