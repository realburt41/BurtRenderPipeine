#ifndef BURT_SHADOWS_INCLUDED
#define BURT_SHADOWS_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

UNITY_DECLARE_SHADOWMAP(_BurtMainLightShadowMap);
UNITY_DECLARE_SHADOWMAP(_BurtAdditionalLightShadowAtlas);

#define BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES 4
#define BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT 32

float4x4 _BurtMainLightWorldToShadow;
float4 _BurtMainLightWorldToShadowRow0;
float4 _BurtMainLightWorldToShadowRow1;
float4 _BurtMainLightWorldToShadowRow2;
float4 _BurtMainLightWorldToShadowRow3;

float4x4 _BurtMainLightWorldToShadowMatrices[BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES];
float4 _BurtMainLightWorldToShadowRows0[BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES];
float4 _BurtMainLightWorldToShadowRows1[BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES];
float4 _BurtMainLightWorldToShadowRows2[BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES];
float4 _BurtMainLightWorldToShadowRows3[BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES];
float4 _BurtMainLightShadowCascadeSpheres[BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES];
float4 _BurtMainLightShadowCascadeAtlasRects[BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES];

// x: cascade count, y: blend ratio of current cascade sphere radius, z: final cascade fade distance in world units, w: tile resolution.
float4 _BurtMainLightShadowCascadeParams;
float _BurtMainLightShadowStrength;
float4 _BurtMainLightShadowTexelSize;
float _BurtMainLightShadowSampleBias;
float _BurtMainLightShadowSoftness;
float4 _BurtMainLightShadowCasterBiasParams;

float4 _BurtAdditionalLightShadowData[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightShadowAtlasRects[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4x4 _BurtAdditionalLightWorldToShadowMatrices[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightWorldToShadowRows0[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightWorldToShadowRows1[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightWorldToShadowRows2[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightWorldToShadowRows3[BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT];
float4 _BurtAdditionalLightShadowParams;
float4 _BurtAdditionalLightShadowTexelSize;

// x: PCSS enabled, y: light size in texels, z: blocker search radius in texels, w: max filter radius in texels.
float4 _BurtMainLightShadowPCSSParams;
static const int BURT_MAIN_LIGHT_SHADOW_PCSS_SAMPLE_COUNT = 13;
static const float BURT_MAIN_LIGHT_SHADOW_CENTER_SAMPLE_WEIGHT = 2.0f;
static const float BURT_MAIN_LIGHT_SHADOW_MIN_PCSS_FILTER_RADIUS_TEXELS = 0.35f;
static const float BURT_MAIN_LIGHT_SHADOW_TRANSITION_TEXEL_FLOOR = 8.0f;

struct BurtMainLightShadowInput
{
    float4 shadowCoord;
    float strength;
};

int BurtGetMainLightShadowCascadeCount()
{
    int cascadeCount = (int)(_BurtMainLightShadowCascadeParams.x + 0.5f);
    return min(max(cascadeCount, 0), BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES);
}

float4 BurtGetMainLightShadowCascadeSphere(int cascadeIndex)
{
    if (cascadeIndex == 1)
    {
        return _BurtMainLightShadowCascadeSpheres[1];
    }

    if (cascadeIndex == 2)
    {
        return _BurtMainLightShadowCascadeSpheres[2];
    }

    if (cascadeIndex == 3)
    {
        return _BurtMainLightShadowCascadeSpheres[3];
    }

    return _BurtMainLightShadowCascadeSpheres[0];
}

float4 BurtGetMainLightShadowCascadeAtlasRect(int cascadeIndex)
{
    if (cascadeIndex == 1)
    {
        return _BurtMainLightShadowCascadeAtlasRects[1];
    }

    if (cascadeIndex == 2)
    {
        return _BurtMainLightShadowCascadeAtlasRects[2];
    }

    if (cascadeIndex == 3)
    {
        return _BurtMainLightShadowCascadeAtlasRects[3];
    }

    return _BurtMainLightShadowCascadeAtlasRects[0];
}

float4 BurtGetMainLightWorldToShadowRow0(int cascadeIndex)
{
    if (cascadeIndex == 1)
    {
        return _BurtMainLightWorldToShadowRows0[1];
    }

    if (cascadeIndex == 2)
    {
        return _BurtMainLightWorldToShadowRows0[2];
    }

    if (cascadeIndex == 3)
    {
        return _BurtMainLightWorldToShadowRows0[3];
    }

    return _BurtMainLightWorldToShadowRows0[0];
}

float4 BurtGetMainLightWorldToShadowRow1(int cascadeIndex)
{
    if (cascadeIndex == 1)
    {
        return _BurtMainLightWorldToShadowRows1[1];
    }

    if (cascadeIndex == 2)
    {
        return _BurtMainLightWorldToShadowRows1[2];
    }

    if (cascadeIndex == 3)
    {
        return _BurtMainLightWorldToShadowRows1[3];
    }

    return _BurtMainLightWorldToShadowRows1[0];
}

float BurtGetMainLightShadowMaxFilterRadiusTexels()
{
    float baseRadius = _BurtMainLightShadowSoftness > 0.5f ? 1.5f : 0.0f;
    if (_BurtMainLightShadowPCSSParams.x > 0.5f && _BurtMainLightShadowSoftness > 0.5f)
    {
        baseRadius = max(baseRadius, max(_BurtMainLightShadowPCSSParams.z, _BurtMainLightShadowPCSSParams.w));
    }

    return max(baseRadius, 1.0f);
}

float2 BurtGetMainLightShadowBaseAtlasTexelMargin(int cascadeIndex)
{
    float4 atlasRect = BurtGetMainLightShadowCascadeAtlasRect(cascadeIndex);
    float2 rectSize = max(atlasRect.zw - atlasRect.xy, _BurtMainLightShadowTexelSize.xy);
    return min(max(_BurtMainLightShadowTexelSize.xy, float2(0.0f, 0.0f)), rectSize * 0.45f);
}

float2 BurtGetMainLightShadowAtlasTexelMargin(int cascadeIndex)
{
    float4 atlasRect = BurtGetMainLightShadowCascadeAtlasRect(cascadeIndex);
    float2 rectSize = max(atlasRect.zw - atlasRect.xy, _BurtMainLightShadowTexelSize.xy);
    float2 requestedMargin = max(_BurtMainLightShadowTexelSize.xy * BurtGetMainLightShadowMaxFilterRadiusTexels(), _BurtMainLightShadowTexelSize.xy);
    return min(requestedMargin, rectSize * 0.45f);
}

float BurtGetMainLightShadowEstimatedWorldTexelSize(int cascadeIndex)
{
    float worldToAtlasScaleX = length(BurtGetMainLightWorldToShadowRow0(cascadeIndex).xyz);
    float worldToAtlasScaleY = length(BurtGetMainLightWorldToShadowRow1(cascadeIndex).xyz);
    float texelWorldSizeX = _BurtMainLightShadowTexelSize.x / max(worldToAtlasScaleX, 0.000001f);
    float texelWorldSizeY = _BurtMainLightShadowTexelSize.y / max(worldToAtlasScaleY, 0.000001f);
    return max(texelWorldSizeX, texelWorldSizeY);
}

float BurtSmootherStep01(float value)
{
    value = saturate(value);
    return value * value * value * (value * (value * 6.0f - 15.0f) + 10.0f);
}

float3 BurtApplyMainLightShadowCasterBiasEstimate(float3 positionWS, float3 normalWS, int cascadeIndex)
{
    float worldTexelSize = BurtGetMainLightShadowEstimatedWorldTexelSize(cascadeIndex);
    if (worldTexelSize <= 0.0f)
    {
        return positionWS;
    }

    float3 lightDirectionWS = _BurtMainLightDirection.xyz;
    lightDirectionWS *= rsqrt(max(dot(lightDirectionWS, lightDirectionWS), 0.000001f));

    float3 safeNormalWS = normalWS;
    safeNormalWS *= rsqrt(max(dot(safeNormalWS, safeNormalWS), 0.000001f));

    float depthBias = -max(_BurtMainLightShadowCasterBiasParams.x, 0.0f) * worldTexelSize;
    float normalBias = -max(_BurtMainLightShadowCasterBiasParams.y, 0.0f) * worldTexelSize;
    float normalBiasScale = (1.0f - saturate(dot(safeNormalWS, lightDirectionWS))) * normalBias;
    return positionWS + lightDirectionWS * depthBias + safeNormalWS * normalBiasScale;
}

float4 BurtTransformWorldToMainLightShadowCascade(float4 positionWS, int cascadeIndex)
{
    if (cascadeIndex == 1)
    {
        return float4(
            dot(_BurtMainLightWorldToShadowRows0[1], positionWS),
            dot(_BurtMainLightWorldToShadowRows1[1], positionWS),
            dot(_BurtMainLightWorldToShadowRows2[1], positionWS),
            dot(_BurtMainLightWorldToShadowRows3[1], positionWS));
    }

    if (cascadeIndex == 2)
    {
        return float4(
            dot(_BurtMainLightWorldToShadowRows0[2], positionWS),
            dot(_BurtMainLightWorldToShadowRows1[2], positionWS),
            dot(_BurtMainLightWorldToShadowRows2[2], positionWS),
            dot(_BurtMainLightWorldToShadowRows3[2], positionWS));
    }

    if (cascadeIndex == 3)
    {
        return float4(
            dot(_BurtMainLightWorldToShadowRows0[3], positionWS),
            dot(_BurtMainLightWorldToShadowRows1[3], positionWS),
            dot(_BurtMainLightWorldToShadowRows2[3], positionWS),
            dot(_BurtMainLightWorldToShadowRows3[3], positionWS));
    }

    return float4(
        dot(_BurtMainLightWorldToShadowRows0[0], positionWS),
        dot(_BurtMainLightWorldToShadowRows1[0], positionWS),
        dot(_BurtMainLightWorldToShadowRows2[0], positionWS),
        dot(_BurtMainLightWorldToShadowRows3[0], positionWS));
}

float4 BurtTransformWorldToMainLightShadow(float4 positionWS)
{
    return float4(
        dot(_BurtMainLightWorldToShadowRow0, positionWS),
        dot(_BurtMainLightWorldToShadowRow1, positionWS),
        dot(_BurtMainLightWorldToShadowRow2, positionWS),
        dot(_BurtMainLightWorldToShadowRow3, positionWS));
}

bool BurtIsInsideMainLightShadowMap(float3 projectedShadowCoord, int cascadeIndex)
{
    float4 atlasRect = BurtGetMainLightShadowCascadeAtlasRect(cascadeIndex);
    float2 atlasTexelMargin = BurtGetMainLightShadowBaseAtlasTexelMargin(cascadeIndex);
    bool outsideShadowMap = projectedShadowCoord.x <= atlasRect.x + atlasTexelMargin.x
        || projectedShadowCoord.x >= atlasRect.z - atlasTexelMargin.x
        || projectedShadowCoord.y <= atlasRect.y + atlasTexelMargin.y
        || projectedShadowCoord.y >= atlasRect.w - atlasTexelMargin.y
        || projectedShadowCoord.z <= 0.0f
        || projectedShadowCoord.z >= 1.0f;

    return !outsideShadowMap;
}

bool BurtIsInsideMainLightShadowMap(float3 projectedShadowCoord)
{
    return BurtIsInsideMainLightShadowMap(projectedShadowCoord, 0);
}

float2 BurtClampMainLightShadowUVToCascade(float2 shadowUV, int cascadeIndex)
{
    float4 atlasRect = BurtGetMainLightShadowCascadeAtlasRect(cascadeIndex);
    float2 atlasTexelMargin = BurtGetMainLightShadowAtlasTexelMargin(cascadeIndex);
    return clamp(shadowUV, atlasRect.xy + atlasTexelMargin, atlasRect.zw - atlasTexelMargin);
}

float BurtApplyMainLightReceiverBias(float projectedDepth)
{
    float receiverBias = max(0.0f, _BurtMainLightShadowSampleBias);
    return saturate(projectedDepth + receiverBias);
}

float BurtSampleMainLightShadowCompare(float3 projectedShadowCoord, int cascadeIndex)
{
    projectedShadowCoord.xy = BurtClampMainLightShadowUVToCascade(projectedShadowCoord.xy, cascadeIndex);
    return UNITY_SAMPLE_SHADOW(_BurtMainLightShadowMap, projectedShadowCoord);
}

float BurtSampleMainLightShadowCompare(float3 projectedShadowCoord)
{
    return BurtSampleMainLightShadowCompare(projectedShadowCoord, 0);
}

float BurtSampleMainLightShadowRawDepth(float2 shadowUV, int cascadeIndex)
{
    float2 clampedUV = BurtClampMainLightShadowUVToCascade(shadowUV, cascadeIndex);
    float2 shadowSize = max(_BurtMainLightShadowTexelSize.zw, float2(1.0f, 1.0f));
    int2 pixelCoord = (int2)clamp(floor(clampedUV * shadowSize), float2(0.0f, 0.0f), shadowSize - 1.0f);
    return _BurtMainLightShadowMap.Load(int3(pixelCoord, 0)).r;
}

float BurtSampleMainLightShadowRawDepthBilinear(float2 shadowUV, int cascadeIndex)
{
    float2 clampedUV = BurtClampMainLightShadowUVToCascade(shadowUV, cascadeIndex);
    float2 shadowSize = max(_BurtMainLightShadowTexelSize.zw, float2(1.0f, 1.0f));
    float2 pixelCoord = clampedUV * shadowSize - 0.5f;
    float2 pixelCoordFloor = floor(pixelCoord);
    float2 pixelCoordFrac = pixelCoord - pixelCoordFloor;

    int2 sampleCoord00 = (int2)clamp(pixelCoordFloor, float2(0.0f, 0.0f), shadowSize - 1.0f);
    int2 sampleCoord10 = (int2)clamp(pixelCoordFloor + float2(1.0f, 0.0f), float2(0.0f, 0.0f), shadowSize - 1.0f);
    int2 sampleCoord01 = (int2)clamp(pixelCoordFloor + float2(0.0f, 1.0f), float2(0.0f, 0.0f), shadowSize - 1.0f);
    int2 sampleCoord11 = (int2)clamp(pixelCoordFloor + float2(1.0f, 1.0f), float2(0.0f, 0.0f), shadowSize - 1.0f);

    float rawDepth00 = _BurtMainLightShadowMap.Load(int3(sampleCoord00, 0)).r;
    float rawDepth10 = _BurtMainLightShadowMap.Load(int3(sampleCoord10, 0)).r;
    float rawDepth01 = _BurtMainLightShadowMap.Load(int3(sampleCoord01, 0)).r;
    float rawDepth11 = _BurtMainLightShadowMap.Load(int3(sampleCoord11, 0)).r;

    float rawDepth0 = lerp(rawDepth00, rawDepth10, pixelCoordFrac.x);
    float rawDepth1 = lerp(rawDepth01, rawDepth11, pixelCoordFrac.x);
    return lerp(rawDepth0, rawDepth1, pixelCoordFrac.y);
}

float BurtSampleMainLightShadowRawDepth(float2 shadowUV)
{
    return BurtSampleMainLightShadowRawDepth(shadowUV, 0);
}

float BurtMainLightShadowInterleavedGradientNoise(float2 pixelPosition)
{
    return frac(52.9829189f * frac(dot(pixelPosition, float2(0.06711056f, 0.00583715f))));
}

float2 BurtRotateMainLightShadowDiskOffset(float2 offset, int rotationIndex)
{
    if (rotationIndex == 1)
    {
        return float2(-offset.y, offset.x);
    }

    if (rotationIndex == 2)
    {
        return -offset;
    }

    if (rotationIndex == 3)
    {
        return float2(offset.y, -offset.x);
    }

    return offset;
}

int BurtGetMainLightShadowSampleRotationIndex(float2 shadowUV, int cascadeIndex)
{
    float2 clampedUV = BurtClampMainLightShadowUVToCascade(shadowUV, cascadeIndex);
    float2 shadowSize = max(_BurtMainLightShadowTexelSize.zw, float2(1.0f, 1.0f));
    float2 coarseTexelCoord = floor(clampedUV * shadowSize * 0.25f);
    return min((int)(BurtMainLightShadowInterleavedGradientNoise(coarseTexelCoord) * 4.0f), 3);
}

float2 BurtGetMainLightShadowPoissonDiskOffset(int sampleIndex)
{
    if (sampleIndex == 1) return float2(-0.326212f, -0.405805f);
    if (sampleIndex == 2) return float2(-0.840144f, -0.073580f);
    if (sampleIndex == 3) return float2(-0.695914f,  0.457137f);
    if (sampleIndex == 4) return float2(-0.203345f,  0.620716f);
    if (sampleIndex == 5) return float2( 0.962340f, -0.194983f);
    if (sampleIndex == 6) return float2( 0.473434f, -0.480026f);
    if (sampleIndex == 7) return float2( 0.519456f,  0.767022f);
    if (sampleIndex == 8) return float2( 0.185461f, -0.893124f);
    if (sampleIndex == 9) return float2( 0.507431f,  0.064425f);
    if (sampleIndex == 10) return float2( 0.896420f,  0.412458f);
    if (sampleIndex == 11) return float2(-0.321940f, -0.932615f);
    if (sampleIndex == 12) return float2(-0.791559f, -0.597705f);
    return float2(0.0f, 0.0f);
}

float BurtGetMainLightShadowPoissonDiskWeight(float2 offset, int sampleIndex)
{
    return sampleIndex == 0 ? BURT_MAIN_LIGHT_SHADOW_CENTER_SAMPLE_WEIGHT : rcp(1.0f + dot(offset, offset));
}

void BurtSampleMainLightShadowDebugNeighborhood(float2 shadowUV, int cascadeIndex, out float centerDepth, out float surfaceDepth, out float averageDepth, out float depthSpan, out float surfaceCurvature)
{
    float2 clampedUV = BurtClampMainLightShadowUVToCascade(shadowUV, cascadeIndex);
    float2 texelSize = _BurtMainLightShadowTexelSize.xy;
    float2 shadowSize = max(_BurtMainLightShadowTexelSize.zw, float2(1.0f, 1.0f));
    float2 shadowPixelCoord = clampedUV * shadowSize;
    float2 texelOffsetFromCenter = shadowPixelCoord - (floor(shadowPixelCoord) + 0.5f);
    float depthMin = 1.0f;
    float depthMax = 0.0f;
    float weightedDepthSum = 0.0f;
    float weightSum = 0.0f;
    centerDepth = 0.0f;
    surfaceDepth = 0.0f;
    surfaceCurvature = 0.0f;
    float sampleDepths[9];

    float2 offsets[9] = {
        float2(-1.0f, -1.0f), float2( 0.0f, -1.0f), float2( 1.0f, -1.0f),
        float2(-1.0f,  0.0f), float2( 0.0f,  0.0f), float2( 1.0f,  0.0f),
        float2(-1.0f,  1.0f), float2( 0.0f,  1.0f), float2( 1.0f,  1.0f)
    };

    float weights[9] = {
        1.0f, 2.0f, 1.0f,
        2.0f, 4.0f, 2.0f,
        1.0f, 2.0f, 1.0f
    };

    UNITY_UNROLL
    for (int sampleIndex = 0; sampleIndex < 9; sampleIndex++)
    {
        float sampleDepth = BurtSampleMainLightShadowRawDepthBilinear(clampedUV + offsets[sampleIndex] * texelSize, cascadeIndex);
        sampleDepths[sampleIndex] = sampleDepth;
        if (sampleIndex == 4)
        {
            centerDepth = sampleDepth;
        }
        float sampleWeight = weights[sampleIndex];
        weightedDepthSum += sampleDepth * sampleWeight;
        weightSum += sampleWeight;
        depthMin = min(depthMin, sampleDepth);
        depthMax = max(depthMax, sampleDepth);
    }

    averageDepth = weightedDepthSum / max(weightSum, 1.0f);
    depthSpan = max(depthMax - depthMin, 0.0f);

    float leftDepth = sampleDepths[3];
    float rightDepth = sampleDepths[5];
    float downDepth = sampleDepths[1];
    float upDepth = sampleDepths[7];
    float2 depthGradient = 0.5f * float2(rightDepth - leftDepth, upDepth - downDepth);
    surfaceDepth = centerDepth + dot(depthGradient, texelOffsetFromCenter);
    surfaceCurvature = abs(centerDepth - (leftDepth + rightDepth + downDepth + upDepth) * 0.25f);
}

float BurtConvertMainLightShadowDepthToLightDistance(float depth)
{
    // UNITY_SAMPLE_SHADOW and raw .Load both operate in the platform's hardware shadow-depth
    // convention. Convert to 0=near light, 1=far light only when we need an actual distance
    // difference for debug/PCSS blocker estimation.
    #if defined(UNITY_REVERSED_Z)
        depth = 1.0f - depth;
    #endif
    return saturate(depth);
}

float BurtCalculateMainLightBlockerDistance(float storedDepth, float receiverDepth)
{
    float storedDistance = BurtConvertMainLightShadowDepthToLightDistance(storedDepth);
    float receiverDistance = BurtConvertMainLightShadowDepthToLightDistance(receiverDepth);
    return max(0.0f, receiverDistance - storedDistance);
}

float BurtIsMainLightShadowRawBlocker(float storedDepth, float receiverDepth)
{
    float blockerDistance = BurtCalculateMainLightBlockerDistance(storedDepth, receiverDepth);
    float minBlockerDistance = max(0.00001f, _BurtMainLightShadowSampleBias * 0.25f);
    return blockerDistance > minBlockerDistance ? 1.0f : 0.0f;
}

bool BurtTryEvaluateMainLightShadowPCSSBlockers(
    float3 projectedShadowCoord,
    int cascadeIndex,
    out float blockerFraction,
    out float averageBlockerDistance,
    out float averageBlockerDepth)
{
    blockerFraction = 0.0f;
    averageBlockerDistance = 0.0f;
    averageBlockerDepth = 0.0f;

    float searchRadius = min(max(_BurtMainLightShadowPCSSParams.z, 0.0f), max(_BurtMainLightShadowPCSSParams.w, 1.0f));
    if (searchRadius <= 0.0f)
    {
        return false;
    }

    float2 texelSize = _BurtMainLightShadowTexelSize.xy;
    float blockerDistanceSum = 0.0f;
    float blockerDepthSum = 0.0f;
    float blockerWeightSum = 0.0f;
    float sampleWeightSum = 0.0f;
    int sampleRotationIndex = BurtGetMainLightShadowSampleRotationIndex(projectedShadowCoord.xy, cascadeIndex);

    UNITY_UNROLL
    for (int blockerIndex = 0; blockerIndex < BURT_MAIN_LIGHT_SHADOW_PCSS_SAMPLE_COUNT; blockerIndex++)
    {
        float2 offset = BurtRotateMainLightShadowDiskOffset(BurtGetMainLightShadowPoissonDiskOffset(blockerIndex), sampleRotationIndex);
        float sampleWeight = BurtGetMainLightShadowPoissonDiskWeight(offset, blockerIndex);
        float2 sampleUV = projectedShadowCoord.xy + offset * searchRadius * texelSize;
        float rawDepth = BurtSampleMainLightShadowRawDepthBilinear(sampleUV, cascadeIndex);
        float blockerDistance = BurtCalculateMainLightBlockerDistance(rawDepth, projectedShadowCoord.z);
        float isBlocker = BurtIsMainLightShadowRawBlocker(rawDepth, projectedShadowCoord.z);
        float blockerWeight = sampleWeight * isBlocker;
        blockerDistanceSum += blockerDistance * blockerWeight;
        blockerDepthSum += BurtConvertMainLightShadowDepthToLightDistance(rawDepth) * blockerWeight;
        blockerWeightSum += blockerWeight;
        sampleWeightSum += sampleWeight;
    }

    blockerFraction = blockerWeightSum / max(sampleWeightSum, 0.0001f);
    if (blockerWeightSum <= 0.0f)
    {
        return false;
    }

    averageBlockerDistance = blockerDistanceSum / blockerWeightSum;
    averageBlockerDepth = blockerDepthSum / blockerWeightSum;
    return true;
}

float BurtSampleMainLightShadowPCF(float3 projectedShadowCoord, float radiusTexels, int cascadeIndex)
{
    float2 texelSize = _BurtMainLightShadowTexelSize.xy;
    float radius = max(radiusTexels, 0.0f);
    float visibility = 0.0f;
    float weightSum = 0.0f;
    int sampleRotationIndex = BurtGetMainLightShadowSampleRotationIndex(projectedShadowCoord.xy, cascadeIndex);

    UNITY_UNROLL
    for (int filterIndex = 0; filterIndex < BURT_MAIN_LIGHT_SHADOW_PCSS_SAMPLE_COUNT; filterIndex++)
    {
        float2 offset = BurtRotateMainLightShadowDiskOffset(BurtGetMainLightShadowPoissonDiskOffset(filterIndex), sampleRotationIndex);
        float sampleWeight = BurtGetMainLightShadowPoissonDiskWeight(offset, filterIndex);
        float sampleVisibility = BurtSampleMainLightShadowCompare(float3(projectedShadowCoord.xy + offset * radius * texelSize, projectedShadowCoord.z), cascadeIndex);
        visibility += sampleVisibility * sampleWeight;
        weightSum += sampleWeight;
    }

    return visibility / max(weightSum, 0.0001f);
}

float BurtSampleMainLightShadowPCF(float3 projectedShadowCoord, float radiusTexels)
{
    return BurtSampleMainLightShadowPCF(projectedShadowCoord, radiusTexels, 0);
}

float BurtCalculateMainLightShadowPCSSRadiusTexels(float3 projectedShadowCoord, int cascadeIndex)
{
    if (_BurtMainLightShadowPCSSParams.x <= 0.5f || _BurtMainLightShadowSoftness <= 0.5f)
    {
        return 0.0f;
    }

    float lightSize = max(_BurtMainLightShadowPCSSParams.y, 0.0f);
    if (lightSize <= 0.0f)
    {
        return 0.0f;
    }

    float blockerFraction = 0.0f;
    float averageBlockerDistance = 0.0f;
    float averageBlockerDepth = 0.0f;
    if (!BurtTryEvaluateMainLightShadowPCSSBlockers(projectedShadowCoord, cascadeIndex, blockerFraction, averageBlockerDistance, averageBlockerDepth))
    {
        return 0.0f;
    }

    // Directional cascades should not let the blocker-depth denominator blow the filter radius
    // all the way to max across the whole footprint. Compress the receiver/blocker ratio into a
    // bounded contact-hardening factor, then only open up toward the configured max radius when
    // the blocker search actually finds dense occluder coverage.
    float receiverToBlockerRatio = averageBlockerDistance / max(averageBlockerDepth, 0.001f);
    float contactHardening = receiverToBlockerRatio / (1.0f + receiverToBlockerRatio);
    contactHardening *= contactHardening;
    float maxFilterRadius = max(_BurtMainLightShadowPCSSParams.w, 1.0f);
    float blockerCoverage = saturate(blockerFraction);
    float penumbraTarget = lerp(lightSize, maxFilterRadius, blockerCoverage * blockerCoverage);
    float penumbraRadius = contactHardening * penumbraTarget;
    penumbraRadius = min(max(penumbraRadius, 0.0f), maxFilterRadius);

    return penumbraRadius;
}

float BurtSampleMainLightShadowPCSS(float3 projectedShadowCoord, int cascadeIndex)
{
    if (_BurtMainLightShadowSoftness <= 0.5f)
    {
        return BurtSampleMainLightShadowCompare(projectedShadowCoord, cascadeIndex);
    }

    if (_BurtMainLightShadowPCSSParams.x <= 0.5f)
    {
        return BurtSampleMainLightShadowPCF(projectedShadowCoord, 1.5f, cascadeIndex);
    }

    float radiusTexels = BurtCalculateMainLightShadowPCSSRadiusTexels(projectedShadowCoord, cascadeIndex);
    radiusTexels = min(max(radiusTexels, BURT_MAIN_LIGHT_SHADOW_MIN_PCSS_FILTER_RADIUS_TEXELS), max(_BurtMainLightShadowPCSSParams.w, BURT_MAIN_LIGHT_SHADOW_MIN_PCSS_FILTER_RADIUS_TEXELS));
    return BurtSampleMainLightShadowPCF(projectedShadowCoord, radiusTexels, cascadeIndex);
}

float BurtSampleMainLightShadowPCSS(float3 projectedShadowCoord)
{
    return BurtSampleMainLightShadowPCSS(projectedShadowCoord, 0);
}

float BurtApplyShadowStrength(float rawShadow, float strength)
{
    return lerp(1.0f, rawShadow, saturate(strength));
}

int BurtSelectMainLightShadowCascade(float3 positionWS)
{
    int cascadeCount = BurtGetMainLightShadowCascadeCount();

    UNITY_UNROLL
    for (int cascadeIndex = 0; cascadeIndex < BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES; cascadeIndex++)
    {
        if (cascadeIndex < cascadeCount)
        {
            float4 cascadeSphere = BurtGetMainLightShadowCascadeSphere(cascadeIndex);
            float3 toCenter = positionWS - cascadeSphere.xyz;
            if (cascadeSphere.w > 0.0f && dot(toCenter, toCenter) <= cascadeSphere.w)
            {
                return cascadeIndex;
            }
        }
    }

    return -1;
}

float BurtSampleMainLightShadowRawVisibility(float4 shadowCoord, int cascadeIndex)
{
    float safeW = abs(shadowCoord.w) > 0.00001f ? shadowCoord.w : (shadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 projectedShadowCoord = shadowCoord.xyz / safeW;

    if (!BurtIsInsideMainLightShadowMap(projectedShadowCoord, cascadeIndex))
    {
        return 1.0f;
    }

    projectedShadowCoord.z = BurtApplyMainLightReceiverBias(projectedShadowCoord.z);
    return BurtSampleMainLightShadowPCSS(projectedShadowCoord, cascadeIndex);
}

float BurtCalculateMainLightCascadeBlendWeight(float3 positionWS, int cascadeIndex)
{
    int cascadeCount = BurtGetMainLightShadowCascadeCount();
    if (cascadeIndex < 0 || cascadeIndex >= cascadeCount - 1)
    {
        return 0.0f;
    }

    float blendRatio = saturate(_BurtMainLightShadowCascadeParams.y);
    if (blendRatio <= 0.0f)
    {
        return 0.0f;
    }

    float4 cascadeSphere = BurtGetMainLightShadowCascadeSphere(cascadeIndex);
    float radius = sqrt(max(cascadeSphere.w, 0.0f));
    if (radius <= 0.0001f)
    {
        return 0.0f;
    }

    float distanceToCenter = length(positionWS - cascadeSphere.xyz);
    float texelBlendFloor = BurtGetMainLightShadowEstimatedWorldTexelSize(cascadeIndex) * BURT_MAIN_LIGHT_SHADOW_TRANSITION_TEXEL_FLOOR;
    float blendBand = max(max(radius * blendRatio, texelBlendFloor), 0.0001f);
    float blendStart = radius - blendBand;
    float blendWeight = saturate((distanceToCenter - blendStart) / blendBand);
    return BurtSmootherStep01(blendWeight);
}

float BurtCalculateMainLightShadowFade(float3 positionWS, int cascadeIndex)
{
    int cascadeCount = BurtGetMainLightShadowCascadeCount();
    if (cascadeIndex != cascadeCount - 1)
    {
        return 0.0f;
    }

    float fadeDistance = max(_BurtMainLightShadowCascadeParams.z, 0.0f);
    if (fadeDistance <= 0.0001f)
    {
        return 0.0f;
    }

    float4 cascadeSphere = BurtGetMainLightShadowCascadeSphere(cascadeIndex);
    float radius = sqrt(max(cascadeSphere.w, 0.0f));
    float distanceToCenter = length(positionWS - cascadeSphere.xyz);
    float texelFadeFloor = BurtGetMainLightShadowEstimatedWorldTexelSize(cascadeIndex) * BURT_MAIN_LIGHT_SHADOW_TRANSITION_TEXEL_FLOOR;
    float fadeBand = max(fadeDistance, texelFadeFloor);
    float fadeStart = max(0.0f, radius - fadeBand);
    float fadeWeight = saturate((distanceToCenter - fadeStart) / fadeBand);
    return BurtSmootherStep01(fadeWeight);
}

float3 BurtGetMainLightShadowCascadeDebugColorByIndex(int cascadeIndex)
{
    if (cascadeIndex == 1)
    {
        return float3(0.15f, 0.9f, 0.25f);
    }

    if (cascadeIndex == 2)
    {
        return float3(0.2f, 0.45f, 1.0f);
    }

    if (cascadeIndex == 3)
    {
        return float3(1.0f, 0.65f, 0.1f);
    }

    return float3(1.0f, 0.15f, 0.1f);
}

float3 BurtGetMainLightShadowCascadeDebugColor(float3 positionWS)
{
    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    return BurtGetMainLightShadowCascadeDebugColorByIndex(cascadeIndex);
}

float BurtGetMainLightShadowCascadeBlendDebug(float3 positionWS)
{
    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return 0.0f;
    }

    return BurtCalculateMainLightCascadeBlendWeight(positionWS, cascadeIndex);
}

float BurtGetMainLightShadowDistanceFadeDebug(float3 positionWS)
{
    int cascadeCount = BurtGetMainLightShadowCascadeCount();
    if (cascadeCount <= 0)
    {
        return 0.0f;
    }

    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return 1.0f;
    }

    return BurtCalculateMainLightShadowFade(positionWS, cascadeIndex);
}

float3 BurtProjectWorldToMainLightShadowCascade(float3 positionWS, int cascadeIndex)
{
    float4 shadowCoord = BurtTransformWorldToMainLightShadowCascade(float4(positionWS, 1.0f), cascadeIndex);
    float safeW = abs(shadowCoord.w) > 0.00001f ? shadowCoord.w : (shadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    return shadowCoord.xyz / safeW;
}

float BurtGetMainLightShadowPCSSRadiusDebug(float3 positionWS)
{
    if (_BurtMainLightShadowPCSSParams.x <= 0.5f)
    {
        return 0.0f;
    }

    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return 0.0f;
    }

    float3 projectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(positionWS, cascadeIndex);
    if (!BurtIsInsideMainLightShadowMap(projectedShadowCoord, cascadeIndex))
    {
        return 0.0f;
    }

    projectedShadowCoord.z = BurtApplyMainLightReceiverBias(projectedShadowCoord.z);
    float radius = BurtCalculateMainLightShadowPCSSRadiusTexels(projectedShadowCoord, cascadeIndex);
    float normalizedRadius = saturate(radius / max(_BurtMainLightShadowPCSSParams.w, 1.5f));

    // For debug readability, suppress the wide low-level halo on lit receivers and emphasize
    // where the current PCSS filter is actually contributing to visible shadowing.
    float shadowedWeight = saturate((1.0f - BurtSampleMainLightShadowPCSS(projectedShadowCoord, cascadeIndex)) * 1.25f);
    return normalizedRadius * shadowedWeight;
}

float BurtGetMainLightShadowPCSSBlockerFractionDebug(float3 positionWS)
{
    if (_BurtMainLightShadowPCSSParams.x <= 0.5f)
    {
        return 0.0f;
    }

    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return 0.0f;
    }

    float3 projectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(positionWS, cascadeIndex);
    if (!BurtIsInsideMainLightShadowMap(projectedShadowCoord, cascadeIndex))
    {
        return 0.0f;
    }

    projectedShadowCoord.z = BurtApplyMainLightReceiverBias(projectedShadowCoord.z);

    float blockerFraction = 0.0f;
    float averageBlockerDistance = 0.0f;
    float averageBlockerDepth = 0.0f;
    BurtTryEvaluateMainLightShadowPCSSBlockers(projectedShadowCoord, cascadeIndex, blockerFraction, averageBlockerDistance, averageBlockerDepth);

    // Raw blocker fraction is too sparse to read on narrow silhouettes when only a few Poisson
    // taps land on the occluder. Use the center sample as a floor for debug visibility, then
    // gate by actual shadow visibility so lit receivers do not show false self-blockers.
    float centerRawDepth = BurtSampleMainLightShadowRawDepthBilinear(projectedShadowCoord.xy, cascadeIndex);
    float centerIsBlocker = BurtIsMainLightShadowRawBlocker(centerRawDepth, projectedShadowCoord.z);
    float displayFraction = saturate(max(blockerFraction, centerIsBlocker * 0.35f) * 1.75f);
    float blockedWeight = saturate(1.0f - BurtSampleMainLightShadowPCSS(projectedShadowCoord, cascadeIndex));
    return displayFraction * blockedWeight;
}

float BurtGetMainLightShadowReceiverDepthDeltaDebug(float3 positionWS, float3 normalWS)
{
    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return 0.0f;
    }

    float3 projectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(positionWS, cascadeIndex);
    if (!BurtIsInsideMainLightShadowMap(projectedShadowCoord, cascadeIndex))
    {
        return 0.0f;
    }

    // The shadow map stores caster positions after BurtRP's depth/normal bias. Re-project an
    // estimated biased receiver so this debug reflects acne-vs-bias pressure instead of just
    // visualizing the caster bias offset as a full-white self-shadowing surface.
    float3 biasedPositionWS = BurtApplyMainLightShadowCasterBiasEstimate(positionWS, normalWS, cascadeIndex);
    float3 biasedProjectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(biasedPositionWS, cascadeIndex);
    float receiverDepth = biasedProjectedShadowCoord.z;
    float storedDepthCenter = 0.0f;
    float storedDepthSurface = 0.0f;
    float storedDepthAverage = 0.0f;
    float storedDepthSpan = 0.0f;
    float storedDepthSurfaceCurvature = 0.0f;
    BurtSampleMainLightShadowDebugNeighborhood(biasedProjectedShadowCoord.xy, cascadeIndex, storedDepthCenter, storedDepthSurface, storedDepthAverage, storedDepthSpan, storedDepthSurfaceCurvature);

    // Convert both receiver/stored depth into the same light-distance convention before
    // taking the difference; otherwise reversed-Z platforms turn this debug into a shadow mask.
    float receiverDistance = BurtConvertMainLightShadowDepthToLightDistance(receiverDepth);
    float storedDistanceCenter = BurtConvertMainLightShadowDepthToLightDistance(storedDepthCenter);
    float storedDistanceSurface = BurtConvertMainLightShadowDepthToLightDistance(storedDepthSurface);
    float storedDistanceAverage = BurtConvertMainLightShadowDepthToLightDistance(storedDepthAverage);
    float storedDistanceSpan = storedDepthSpan;
    float storedDistanceCurvature = storedDepthSurfaceCurvature;
    float signedDistanceDelta = receiverDistance - storedDistanceSurface;
    float neighborhoodDisagreement = abs(storedDistanceSurface - storedDistanceAverage);
    float localPlaneResidual = abs(storedDistanceCenter - storedDistanceSurface);

    // This view is for receiver-vs-shadow-surface alignment, not for visualizing full cast-shadow
    // separation. Once the pixel is genuinely blocked by another surface, blend back toward mid-gray
    // so acne/bias pressure on lit receivers remains readable.
    float visibility = BurtSampleMainLightShadowRawVisibility(BurtTransformWorldToMainLightShadowCascade(float4(positionWS, 1.0f), cascadeIndex), cascadeIndex);
    float litWeight = saturate((visibility - 0.05f) / 0.95f);

    // Keep the scale wide enough that small flat-surface bias differences stay around mid-gray.
    float displayScale = max(max(max(max(storedDistanceSpan * 2.5f, neighborhoodDisagreement * 3.0f), max(localPlaneResidual, storedDistanceCurvature) * 6.0f), _BurtMainLightShadowSampleBias * 24.0f), 0.002f);
    float deltaDebug = saturate(0.5f + signedDistanceDelta / displayScale);
    return lerp(0.5f, deltaDebug, litWeight);
}

float BurtSampleMainLightShadow(float3 positionWS)
{
    if (_BurtMainLightShadowStrength <= 0.0001f)
    {
        return 1.0f;
    }

    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return 1.0f;
    }

    float4 positionWS4 = float4(positionWS, 1.0f);
    float rawShadow = BurtSampleMainLightShadowRawVisibility(BurtTransformWorldToMainLightShadowCascade(positionWS4, cascadeIndex), cascadeIndex);

    float blendWeight = BurtCalculateMainLightCascadeBlendWeight(positionWS, cascadeIndex);
    if (blendWeight > 0.0f)
    {
        int nextCascadeIndex = min(cascadeIndex + 1, BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES - 1);
        float nextRawShadow = BurtSampleMainLightShadowRawVisibility(BurtTransformWorldToMainLightShadowCascade(positionWS4, nextCascadeIndex), nextCascadeIndex);
        rawShadow = lerp(rawShadow, nextRawShadow, blendWeight);
    }

    rawShadow = lerp(rawShadow, 1.0f, BurtCalculateMainLightShadowFade(positionWS, cascadeIndex));
    return BurtApplyShadowStrength(rawShadow, _BurtMainLightShadowStrength);
}

float BurtSampleMainLightShadow(float4 shadowCoord)
{
    if (_BurtMainLightShadowStrength <= 0.0001f)
    {
        return 1.0f;
    }

    return BurtApplyShadowStrength(BurtSampleMainLightShadowRawVisibility(shadowCoord, 0), _BurtMainLightShadowStrength);
}

float4 BurtTransformWorldToAdditionalLightShadow(float4 positionWS, int lightIndex)
{
    return float4(
        dot(_BurtAdditionalLightWorldToShadowRows0[lightIndex], positionWS),
        dot(_BurtAdditionalLightWorldToShadowRows1[lightIndex], positionWS),
        dot(_BurtAdditionalLightWorldToShadowRows2[lightIndex], positionWS),
        dot(_BurtAdditionalLightWorldToShadowRows3[lightIndex], positionWS));
}

bool BurtIsInsideAdditionalLightShadowAtlas(float3 projectedShadowCoord, int lightIndex)
{
    float4 atlasRect = _BurtAdditionalLightShadowAtlasRects[lightIndex];
    float2 texelMargin = max(_BurtAdditionalLightShadowTexelSize.xy, float2(0.0f, 0.0f));
    bool outsideShadowMap = projectedShadowCoord.x <= atlasRect.x + texelMargin.x
        || projectedShadowCoord.x >= atlasRect.z - texelMargin.x
        || projectedShadowCoord.y <= atlasRect.y + texelMargin.y
        || projectedShadowCoord.y >= atlasRect.w - texelMargin.y
        || projectedShadowCoord.z <= 0.0f
        || projectedShadowCoord.z >= 1.0f;

    return !outsideShadowMap;
}

float2 BurtClampAdditionalLightShadowUVToRect(float2 shadowUV, int lightIndex)
{
    float4 atlasRect = _BurtAdditionalLightShadowAtlasRects[lightIndex];
    float2 texelMargin = max(_BurtAdditionalLightShadowTexelSize.xy, float2(0.0f, 0.0f));
    return clamp(shadowUV, atlasRect.xy + texelMargin, atlasRect.zw - texelMargin);
}

float BurtSampleAdditionalLightShadow(int lightIndex, float3 positionWS)
{
    if (_BurtAdditionalLightShadowParams.w <= 0.5f)
    {
        return 1.0f;
    }

    if (lightIndex < 0 || lightIndex >= BURT_ADDITIONAL_LIGHT_SHADOW_MAX_COUNT)
    {
        return 1.0f;
    }

    float4 shadowData = _BurtAdditionalLightShadowData[lightIndex];
    if (shadowData.x <= 0.5f || shadowData.y <= 0.0001f)
    {
        return 1.0f;
    }

    float4 shadowCoord = BurtTransformWorldToAdditionalLightShadow(float4(positionWS, 1.0f), lightIndex);
    if (abs(shadowCoord.w) <= BURT_EPSILON)
    {
        return 1.0f;
    }

    float3 projectedShadowCoord = shadowCoord.xyz / shadowCoord.w;
    if (!BurtIsInsideAdditionalLightShadowAtlas(projectedShadowCoord, lightIndex))
    {
        return 1.0f;
    }

    projectedShadowCoord.xy = BurtClampAdditionalLightShadowUVToRect(projectedShadowCoord.xy, lightIndex);
    float rawShadow = UNITY_SAMPLE_SHADOW(_BurtAdditionalLightShadowAtlas, projectedShadowCoord);
    return BurtApplyShadowStrength(rawShadow, shadowData.y);
}

#define BURT_ADDITIONAL_LIGHT_SHADOWS_INCLUDED 1

#endif // BURT_SHADOWS_INCLUDED
