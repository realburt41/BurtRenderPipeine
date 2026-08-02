// Per-object shadow atlas declarations, filtering, and transmission thickness.
#ifndef BURT_PER_OBJECT_SHADOWS_INCLUDED
#define BURT_PER_OBJECT_SHADOWS_INCLUDED

float4 _BurtPerObjectShadowRows0[BURT_PER_OBJECT_SHADOW_MAX_SLICES];
float4 _BurtPerObjectShadowRows1[BURT_PER_OBJECT_SHADOW_MAX_SLICES];
float4 _BurtPerObjectShadowRows2[BURT_PER_OBJECT_SHADOW_MAX_SLICES];
float4 _BurtPerObjectShadowRows3[BURT_PER_OBJECT_SHADOW_MAX_SLICES];
float4 _BurtPerObjectShadowAtlasRects[BURT_PER_OBJECT_SHADOW_MAX_SLICES];
float4 _BurtPerObjectShadowSliceParams[BURT_PER_OBJECT_SHADOW_MAX_SLICES];
float4 _BurtPerObjectShadowSliceDepthParams[BURT_PER_OBJECT_SHADOW_MAX_SLICES];
float4 _BurtPerObjectShadowParams;
float4 _BurtPerObjectShadowTexelSize;
int _BurtPerObjectShadowObjectIndex;

int BurtGetPerObjectShadowSliceCount()
{
    int sliceCount = (int)(_BurtPerObjectShadowParams.x + 0.5f);
    return min(max(sliceCount, 0), BURT_PER_OBJECT_SHADOW_MAX_SLICES);
}

int BurtDecodePerObjectShadowSliceIndex(int objectIndex)
{
    int sliceIndex = objectIndex - 1;
    return sliceIndex >= 0 && sliceIndex < BurtGetPerObjectShadowSliceCount() ? sliceIndex : -1;
}

float4 BurtTransformWorldToPerObjectShadowSlice(float4 positionWS, int sliceIndex)
{
    return float4(
        dot(_BurtPerObjectShadowRows0[sliceIndex], positionWS),
        dot(_BurtPerObjectShadowRows1[sliceIndex], positionWS),
        dot(_BurtPerObjectShadowRows2[sliceIndex], positionWS),
        dot(_BurtPerObjectShadowRows3[sliceIndex], positionWS));
}

float2 BurtGetPerObjectShadowAtlasTexelMargin(int sliceIndex)
{
    float4 atlasRect = _BurtPerObjectShadowAtlasRects[sliceIndex];
    float2 rectSize = max(atlasRect.zw - atlasRect.xy, max(_BurtPerObjectShadowTexelSize.xy, float2(0.0f, 0.0f)));
    return min(max(_BurtPerObjectShadowTexelSize.xy, float2(0.0f, 0.0f)), rectSize * 0.45f);
}

bool BurtIsInsidePerObjectShadowAtlas(float3 shadowCoord, int sliceIndex)
{
    float4 atlasRect = _BurtPerObjectShadowAtlasRects[sliceIndex];
    float2 texelMargin = BurtGetPerObjectShadowAtlasTexelMargin(sliceIndex);
    bool outsideShadowMap = shadowCoord.x <= atlasRect.x + texelMargin.x
        || shadowCoord.x >= atlasRect.z - texelMargin.x
        || shadowCoord.y <= atlasRect.y + texelMargin.y
        || shadowCoord.y >= atlasRect.w - texelMargin.y
        || shadowCoord.z <= 0.0f
        || shadowCoord.z >= 1.0f;

    return !outsideShadowMap;
}

float2 BurtClampPerObjectShadowUVToRect(float2 shadowUV, int sliceIndex)
{
    float4 atlasRect = _BurtPerObjectShadowAtlasRects[sliceIndex];
    float2 texelMargin = BurtGetPerObjectShadowAtlasTexelMargin(sliceIndex);
    return clamp(shadowUV, atlasRect.xy + texelMargin, atlasRect.zw - texelMargin);
}

float2 BurtPerObjectShadowAtlasUVToSliceUV(float2 atlasUV, int sliceIndex)
{
    float4 atlasRect = _BurtPerObjectShadowAtlasRects[sliceIndex];
    float2 rectSize = max(atlasRect.zw - atlasRect.xy, _BurtPerObjectShadowTexelSize.xy);
    return (atlasUV - atlasRect.xy) / rectSize;
}

float2 BurtPerObjectShadowSliceUVToAtlasUV(float2 sliceUV, int sliceIndex)
{
    float4 atlasRect = _BurtPerObjectShadowAtlasRects[sliceIndex];
    float2 rectSize = max(atlasRect.zw - atlasRect.xy, _BurtPerObjectShadowTexelSize.xy);
    return atlasRect.xy + sliceUV * rectSize;
}

float2 BurtGetPerObjectShadowSliceTexelSize(int sliceIndex)
{
    float4 atlasRect = _BurtPerObjectShadowAtlasRects[sliceIndex];
    float2 atlasSize = max(_BurtPerObjectShadowTexelSize.zw, float2(1.0f, 1.0f));
    float2 sliceSize = max((atlasRect.zw - atlasRect.xy) * atlasSize, float2(1.0f, 1.0f));
    return 1.0f / sliceSize;
}

float3 BurtApplyPerObjectShadowReceiverBias(float3 positionWS, float3 normalWS, int sliceIndex)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    float3 lightDirectionWS = BurtSafeNormalize(-_BurtMainLightDirection.xyz);
    float4 sliceParams = _BurtPerObjectShadowSliceParams[sliceIndex];
    float normalBias = max(sliceParams.z, 0.0f);
    if (normalBias <= 0.0f)
    {
        return positionWS;
    }

    float noL = saturate(dot(safeNormalWS, lightDirectionWS));
    float sinTheta = 1.0f - noL;
    float pcfBiasScale = (1.0f + ceil(0.5f * BURT_PER_OBJECT_SHADOW_PCF_SIZE)) * 0.5f;
    return positionWS + safeNormalWS * normalBias * pcfBiasScale * sinTheta;
}

float BurtSamplePerObjectShadowCompare(float3 projectedShadowCoord, int sliceIndex)
{
    projectedShadowCoord.xy = BurtClampPerObjectShadowUVToRect(projectedShadowCoord.xy, sliceIndex);
    return BURT_SAMPLE_SHADOW_CLAMP(_BurtPerObjectShadowAtlas, projectedShadowCoord);
}

float BurtSamplePerObjectShadowCompareSliceUV(float2 sliceUV, float receiverDepth, int sliceIndex)
{
    float2 sliceTexelSize = BurtGetPerObjectShadowSliceTexelSize(sliceIndex);
    sliceUV = clamp(sliceUV, sliceTexelSize, 1.0f - sliceTexelSize);
    float2 atlasUV = BurtPerObjectShadowSliceUVToAtlasUV(sliceUV, sliceIndex);
    return BURT_SAMPLE_SHADOW_CLAMP(_BurtPerObjectShadowAtlas, float3(atlasUV, receiverDepth));
}

float BurtSamplePerObjectShadowRawDepth(float2 shadowUV, int sliceIndex)
{
    float2 clampedUV = BurtClampPerObjectShadowUVToRect(shadowUV, sliceIndex);
    float2 shadowSize = max(_BurtPerObjectShadowTexelSize.zw, float2(1.0f, 1.0f));
    int2 pixelCoord = (int2)clamp(floor(clampedUV * shadowSize), float2(0.0f, 0.0f), shadowSize - 1.0f);
    return _BurtPerObjectShadowAtlas.Load(int3(pixelCoord, 0)).r;
}


float BurtSamplePerObjectShadowPCF(float3 projectedShadowCoord, int sliceIndex)
{
    float2 sliceTexelSize = BurtGetPerObjectShadowSliceTexelSize(sliceIndex);
    if (sliceTexelSize.x <= 0.0f || sliceTexelSize.y <= 0.0f)
    {
        return BurtSamplePerObjectShadowCompare(projectedShadowCoord, sliceIndex);
    }

    float2 sliceUV = BurtPerObjectShadowAtlasUVToSliceUV(projectedShadowCoord.xy, sliceIndex);
    float2 shadowMapSize = rcp(sliceTexelSize);
    float2 uvInTexels = sliceUV * shadowMapSize;
    float2 baseTexel = floor(uvInTexels + 0.5f);
    float2 st = uvInTexels + 0.5f - baseTexel;
    float2 baseUV = (baseTexel - 0.5f) * sliceTexelSize;

    float3 uw = st.x * float3(-3.0f, 0.0f, 3.0f) + float3(4.0f, 7.0f, 1.0f);
    float3 uFactor = st.x * float3(-2.0f, 1.0f, 1.0f) + float3(3.0f, 3.0f, 0.0f);
    float3 u = uFactor * rcp(uw) + float3(-2.0f, 0.0f, 2.0f);

    float3 vw = st.y * float3(-3.0f, 0.0f, 3.0f) + float3(4.0f, 7.0f, 1.0f);
    float3 vFactor = st.y * float3(-2.0f, 1.0f, 1.0f) + float3(3.0f, 3.0f, 0.0f);
    float3 v = vFactor * rcp(vw) + float3(-2.0f, 0.0f, 2.0f);

    float3 sumVec = float3(0.0f, 0.0f, 0.0f);
    UNITY_UNROLL
    for (int i = 0; i < 3; ++i)
    {
        float3 sampleValue = float3(
            BurtSamplePerObjectShadowCompareSliceUV(baseUV + float2(u.x, v[i]) * sliceTexelSize, projectedShadowCoord.z, sliceIndex),
            BurtSamplePerObjectShadowCompareSliceUV(baseUV + float2(u.y, v[i]) * sliceTexelSize, projectedShadowCoord.z, sliceIndex),
            BurtSamplePerObjectShadowCompareSliceUV(baseUV + float2(u.z, v[i]) * sliceTexelSize, projectedShadowCoord.z, sliceIndex));
        sumVec += vw[i] * uw * sampleValue;
    }

    return dot(sumVec, float3(1.0f, 1.0f, 1.0f)) * (1.0f / 144.0f);
}

float BurtResolvePerObjectShadowTransmissionThickness(float3 positionWS, int objectIndex, float fallbackThickness)
{
    float fallback = fallbackThickness >= 0.0f ? lerp(0.1f, 1.0f, saturate(fallbackThickness)) : -1.0f;
    int sliceIndex = BurtDecodePerObjectShadowSliceIndex(objectIndex);
    if (sliceIndex < 0)
    {
        return fallback;
    }

    float4 depthParams = _BurtPerObjectShadowSliceDepthParams[sliceIndex];
    if (depthParams.w <= 0.5f || depthParams.x <= 0.0001f)
    {
        return fallback;
    }

    float4 shadowCoord = BurtTransformWorldToPerObjectShadowSlice(float4(positionWS, 1.0f), sliceIndex);
    float safeW = abs(shadowCoord.w) > 0.00001f ? shadowCoord.w : (shadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 projectedShadowCoord = shadowCoord.xyz / safeW;
    if (!BurtIsInsidePerObjectShadowAtlas(projectedShadowCoord, sliceIndex))
    {
        return fallback;
    }

    float rawDepth = BurtSamplePerObjectShadowRawDepth(projectedShadowCoord.xy, sliceIndex);
    float depthDelta = max(saturate(rawDepth) - saturate(projectedShadowCoord.z), 0.0f);
    float thickness = saturate(depthDelta) * max(depthParams.x, 0.001f);
    return clamp(thickness, 0.0f, 10.0f);
}

float BurtResolvePerObjectShadowTransmissionThickness(float3 positionWS, float fallbackThickness)
{
    return BurtResolvePerObjectShadowTransmissionThickness(positionWS, _BurtPerObjectShadowObjectIndex, fallbackThickness);
}

float BurtSamplePerObjectShadowSlice(float3 positionWS, float3 normalWS, int sliceIndex)
{
    float3 biasedPositionWS = BurtApplyPerObjectShadowReceiverBias(positionWS, normalWS, sliceIndex);
    float4 shadowCoord = BurtTransformWorldToPerObjectShadowSlice(float4(biasedPositionWS, 1.0f), sliceIndex);
    float safeW = abs(shadowCoord.w) > 0.00001f ? shadowCoord.w : (shadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 projectedShadowCoord = shadowCoord.xyz / safeW;
    if (!BurtIsInsidePerObjectShadowAtlas(projectedShadowCoord, sliceIndex))
    {
        return 1.0f;
    }

    float4 sliceParams = _BurtPerObjectShadowSliceParams[sliceIndex];
    projectedShadowCoord.z = saturate(projectedShadowCoord.z + max(sliceParams.y, 0.0f));
    float rawShadow = BurtSamplePerObjectShadowPCF(projectedShadowCoord, sliceIndex);
    return BurtApplyShadowStrength(rawShadow, sliceParams.x);
}

float BurtSamplePerObjectShadow(float3 positionWS, float3 normalWS)
{
    int sliceCount = BurtGetPerObjectShadowSliceCount();
    if (sliceCount <= 0)
    {
        return 1.0f;
    }

    float visibility = 1.0f;
    UNITY_UNROLL
    for (int sliceIndex = 0; sliceIndex < BURT_PER_OBJECT_SHADOW_MAX_SLICES; sliceIndex++)
    {
        if (sliceIndex < sliceCount)
        {
            visibility = min(visibility, BurtSamplePerObjectShadowSlice(positionWS, normalWS, sliceIndex));
        }
    }

    return visibility;
}

float BurtSamplePerObjectShadowExcludingSlice(float3 positionWS, float3 normalWS, int excludedSliceIndex)
{
    int sliceCount = BurtGetPerObjectShadowSliceCount();
    if (sliceCount <= 0)
    {
        return 1.0f;
    }

    float visibility = 1.0f;
    UNITY_UNROLL
    for (int sliceIndex = 0; sliceIndex < BURT_PER_OBJECT_SHADOW_MAX_SLICES; sliceIndex++)
    {
        if (sliceIndex < sliceCount && sliceIndex != excludedSliceIndex)
        {
            visibility = min(visibility, BurtSamplePerObjectShadowSlice(positionWS, normalWS, sliceIndex));
        }
    }

    return visibility;
}

float BurtSamplePerObjectShadow(float3 positionWS, float3 normalWS, int objectIndex)
{
    return BurtSamplePerObjectShadow(positionWS, normalWS);
}

float BurtSamplePerObjectShadow(float3 positionWS)
{
    return BurtSamplePerObjectShadow(positionWS, _BurtMainLightDirection.xyz);
}

#endif // BURT_PER_OBJECT_SHADOWS_INCLUDED
