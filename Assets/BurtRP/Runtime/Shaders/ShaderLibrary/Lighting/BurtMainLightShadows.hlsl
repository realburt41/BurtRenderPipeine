// Main directional light shadows. Additional-light and debug code are separate dependencies.
#ifndef BURT_MAIN_LIGHT_SHADOWS_INCLUDED
#define BURT_MAIN_LIGHT_SHADOWS_INCLUDED

// Keep the texture-coupled comparison sampler here. The generic inline
// sampler_LinearClampCompare does not inherit the shadow texture's backend
// comparison state on D3D11, which can invert/reject every reversed-Z lookup.
// UNITY_DECLARE_SHADOWMAP still expands to Texture2D on D3D11, so the raw
// .Load diagnostics and PCSS blocker reads below remain available.
UNITY_DECLARE_SHADOWMAP(_BurtMainLightShadowMap);
Texture2D _BurtPerObjectShadowAtlas;

#define BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES 4

#if defined(BURT_MAIN_LIGHT_PCF_3)
    #define BURT_MAIN_LIGHT_SHADOW_SOFT_FILTER_RADIUS_TEXELS 2.0f
#elif defined(BURT_MAIN_LIGHT_PCF_7)
    #define BURT_MAIN_LIGHT_SHADOW_SOFT_FILTER_RADIUS_TEXELS 4.0f
#else
    #define BURT_MAIN_LIGHT_SHADOW_SOFT_FILTER_RADIUS_TEXELS 3.0f
#endif
#define BURT_PER_OBJECT_SHADOW_MAX_SLICES 8
#define BURT_PER_OBJECT_SHADOW_PCF_SIZE 5.0f

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
float4 _BurtMainLightShadowReceiverBiasParams;

#define BURT_MAIN_LIGHT_SHADOW_TRANSITION_TEXEL_FLOOR (8.0f)

struct BurtMainLightShadowInput
{
    float4 ShadowCoord;
    float Strength;
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
    // Reserve the complete selected XRender optimized-PCF footprint inside the
    // active cascade tile. Hard shadows only need the center comparison texel.
    return _BurtMainLightShadowSoftness > 0.5f ? BURT_MAIN_LIGHT_SHADOW_SOFT_FILTER_RADIUS_TEXELS : 1.0f;
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
    // XRender derives receiver normal bias from the unremapped [-1, 1] shadow projection.
    // BRP stores atlas UV matrices here, so the inverse atlas-UV texel is twice that value.
    return max(texelWorldSizeX, texelWorldSizeY) * 0.5f;
}

float BurtSmootherStep01(float value)
{
    value = saturate(value);
    return value * value * value * (value * (value * 6.0f - 15.0f) + 10.0f);
}

float3 BurtApplyMainLightShadowReceiverNormalBias(float3 PositionWS, float3 NormalWS, int CascadeIndex)
{
    float WorldTexelSize = BurtGetMainLightShadowEstimatedWorldTexelSize(CascadeIndex);
    float NormalBiasFactor = max(_BurtMainLightShadowReceiverBiasParams.y, 0.0f);
    if (WorldTexelSize <= 0.0f || NormalBiasFactor <= 0.0f)
    {
        return PositionWS;
    }

    float3 SafeNormalWS = NormalWS;
    SafeNormalWS *= rsqrt(max(dot(SafeNormalWS, SafeNormalWS), 0.000001f));

    // XRender's shadow receiver bias uses light.forward, while BRP lighting stores surface-to-light.
    float3 SafeLightDirectionWS = -_BurtMainLightDirection.xyz;
    SafeLightDirectionWS *= rsqrt(max(dot(SafeLightDirectionWS, SafeLightDirectionWS), 0.000001f));

    float NoL = saturate(dot(SafeNormalWS, SafeLightDirectionWS));
    float SinTheta = sqrt(max(1.0f - NoL * NoL, 0.0f));
    return PositionWS + SafeNormalWS * SinTheta * WorldTexelSize * NormalBiasFactor;
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

void BurtGetMainLightShadowOptimizedPCFCoordinates(
    float2 shadowUV,
    int cascadeIndex,
    out float2 texelSize,
    out float2 baseUV,
    out float2 offsetFactor)
{
    texelSize = _BurtMainLightShadowTexelSize.xy;
    float2 shadowSize = max(_BurtMainLightShadowTexelSize.zw, float2(1.0f, 1.0f));
    float2 centerUV = BurtClampMainLightShadowUVToCascade(shadowUV, cascadeIndex);
    float2 coord = centerUV * shadowSize;
    float2 baseCoord = floor(coord + 0.5f);
    baseUV = (baseCoord - 0.5f) * texelSize;
    offsetFactor = coord + 0.5f - baseCoord;
}

float BurtSampleMainLightShadowOptimizedPCF3(float3 projectedShadowCoord, int cascadeIndex)
{
    // Exact XRender OptimizedPcfSample3Internal layout: the full 3x3 tent is
    // reconstructed from four bilinear comparison fetches.
    float2 texelSize;
    float2 baseUV;
    float2 offsetFactor;
    BurtGetMainLightShadowOptimizedPCFCoordinates(projectedShadowCoord.xy, cascadeIndex, texelSize, baseUV, offsetFactor);

    float4 sstt = float4(offsetFactor.xx, offsetFactor.yy);
    float4 uwvw = sstt * float4(-2.0f, 2.0f, -2.0f, 2.0f) + float4(3.0f, 1.0f, 3.0f, 1.0f);
    float4 uvFactor = sstt * float4(-1.0f, 1.0f, -1.0f, 1.0f) + float4(2.0f, 0.0f, 2.0f, 0.0f);
    float4 uvOffset = uvFactor * rcp(uwvw) + float4(-1.0f, 1.0f, -1.0f, 1.0f);

    float4 sampleVisibility = float4(
        BurtSampleMainLightShadowCompare(float3(baseUV + uvOffset.xz * texelSize, projectedShadowCoord.z), cascadeIndex),
        BurtSampleMainLightShadowCompare(float3(baseUV + uvOffset.yz * texelSize, projectedShadowCoord.z), cascadeIndex),
        BurtSampleMainLightShadowCompare(float3(baseUV + uvOffset.xw * texelSize, projectedShadowCoord.z), cascadeIndex),
        BurtSampleMainLightShadowCompare(float3(baseUV + uvOffset.yw * texelSize, projectedShadowCoord.z), cascadeIndex));
    float4 weightedVisibility = uwvw.xyxy * uwvw.zzww * sampleVisibility;
    return dot(weightedVisibility, float4(1.0f, 1.0f, 1.0f, 1.0f)) * (1.0f / 16.0f);
}

float BurtSampleMainLightShadowOptimizedPCF5(float3 projectedShadowCoord, int cascadeIndex)
{
    // Port of XRender OptimizedPcfSample5Internal. A separable 5x5 tent is
    // reconstructed with 3x3 bilinear comparison fetches, giving the complete
    // 25-texel footprint without exposing sparse Poisson taps.
    float2 texelSize;
    float2 baseUV;
    float2 offsetFactor;
    BurtGetMainLightShadowOptimizedPCFCoordinates(projectedShadowCoord.xy, cascadeIndex, texelSize, baseUV, offsetFactor);

    float3 horizontalWeights = offsetFactor.x * float3(-3.0f, 0.0f, 3.0f) + float3(4.0f, 7.0f, 1.0f);
    float3 horizontalFactors = offsetFactor.x * float3(-2.0f, 1.0f, 1.0f) + float3(3.0f, 3.0f, 0.0f);
    float3 horizontalOffsets = horizontalFactors * rcp(horizontalWeights) + float3(-2.0f, 0.0f, 2.0f);
    float3 verticalWeights = offsetFactor.y * float3(-3.0f, 0.0f, 3.0f) + float3(4.0f, 7.0f, 1.0f);
    float3 verticalFactors = offsetFactor.y * float3(-2.0f, 1.0f, 1.0f) + float3(3.0f, 3.0f, 0.0f);
    float3 verticalOffsets = verticalFactors * rcp(verticalWeights) + float3(-2.0f, 0.0f, 2.0f);

    float3 weightedRows = 0.0f;

    UNITY_UNROLL
    for (int rowIndex = 0; rowIndex < 3; rowIndex++)
    {
        float2 sampleUV0 = BurtClampMainLightShadowUVToCascade(baseUV + float2(horizontalOffsets.x, verticalOffsets[rowIndex]) * texelSize, cascadeIndex);
        float2 sampleUV1 = BurtClampMainLightShadowUVToCascade(baseUV + float2(horizontalOffsets.y, verticalOffsets[rowIndex]) * texelSize, cascadeIndex);
        float2 sampleUV2 = BurtClampMainLightShadowUVToCascade(baseUV + float2(horizontalOffsets.z, verticalOffsets[rowIndex]) * texelSize, cascadeIndex);
        float3 sampleVisibility = float3(
            BurtSampleMainLightShadowCompare(float3(sampleUV0, projectedShadowCoord.z), cascadeIndex),
            BurtSampleMainLightShadowCompare(float3(sampleUV1, projectedShadowCoord.z), cascadeIndex),
            BurtSampleMainLightShadowCompare(float3(sampleUV2, projectedShadowCoord.z), cascadeIndex));
        weightedRows += verticalWeights[rowIndex] * horizontalWeights * sampleVisibility;
    }

    return dot(weightedRows, float3(1.0f, 1.0f, 1.0f)) * (1.0f / 144.0f);
}

float BurtSampleMainLightShadowOptimizedPCF7(float3 projectedShadowCoord, int cascadeIndex)
{
    // Exact XRender OptimizedPcfSample7Internal layout: the full 7x7 tent is
    // reconstructed from sixteen bilinear comparison fetches.
    float2 texelSize;
    float2 baseUV;
    float2 offsetFactor;
    BurtGetMainLightShadowOptimizedPCFCoordinates(projectedShadowCoord.xy, cascadeIndex, texelSize, baseUV, offsetFactor);

    float4 horizontalWeights = offsetFactor.x * float4(5.0f, 11.0f, -11.0f, -5.0f) + float4(-6.0f, -28.0f, -17.0f, -1.0f);
    float4 horizontalFactors = offsetFactor.x * float4(4.0f, 4.0f, -7.0f, -1.0f) + float4(-5.0f, -16.0f, -5.0f, 0.0f);
    float4 horizontalOffsets = horizontalFactors * rcp(horizontalWeights) + float4(-3.0f, -1.0f, 1.0f, 3.0f);
    float4 verticalWeights = offsetFactor.y * float4(5.0f, 11.0f, -11.0f, -5.0f) + float4(-6.0f, -28.0f, -17.0f, -1.0f);
    float4 verticalFactors = offsetFactor.y * float4(4.0f, 4.0f, -7.0f, -1.0f) + float4(-5.0f, -16.0f, -5.0f, 0.0f);
    float4 verticalOffsets = verticalFactors * rcp(verticalWeights) + float4(-3.0f, -1.0f, 1.0f, 3.0f);

    float4 weightedRows = 0.0f;

    UNITY_UNROLL
    for (int rowIndex = 0; rowIndex < 4; rowIndex++)
    {
        float4 sampleVisibility = float4(
            BurtSampleMainLightShadowCompare(float3(baseUV + float2(horizontalOffsets.x, verticalOffsets[rowIndex]) * texelSize, projectedShadowCoord.z), cascadeIndex),
            BurtSampleMainLightShadowCompare(float3(baseUV + float2(horizontalOffsets.y, verticalOffsets[rowIndex]) * texelSize, projectedShadowCoord.z), cascadeIndex),
            BurtSampleMainLightShadowCompare(float3(baseUV + float2(horizontalOffsets.z, verticalOffsets[rowIndex]) * texelSize, projectedShadowCoord.z), cascadeIndex),
            BurtSampleMainLightShadowCompare(float3(baseUV + float2(horizontalOffsets.w, verticalOffsets[rowIndex]) * texelSize, projectedShadowCoord.z), cascadeIndex));
        weightedRows += verticalWeights[rowIndex] * horizontalWeights * sampleVisibility;
    }

    return dot(weightedRows, float4(1.0f, 1.0f, 1.0f, 1.0f)) * (1.0f / 2704.0f);
}

float BurtSampleMainLightShadowFiltered(float3 projectedShadowCoord, int cascadeIndex)
{
    if (_BurtMainLightShadowSoftness <= 0.5f)
    {
        return BurtSampleMainLightShadowCompare(projectedShadowCoord, cascadeIndex);
    }

    #if defined(BURT_MAIN_LIGHT_PCF_3)
        return BurtSampleMainLightShadowOptimizedPCF3(projectedShadowCoord, cascadeIndex);
    #elif defined(BURT_MAIN_LIGHT_PCF_7)
        return BurtSampleMainLightShadowOptimizedPCF7(projectedShadowCoord, cascadeIndex);
    #else
        return BurtSampleMainLightShadowOptimizedPCF5(projectedShadowCoord, cascadeIndex);
    #endif
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
    return BurtSampleMainLightShadowFiltered(projectedShadowCoord, cascadeIndex);
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

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtPerObjectShadows.hlsl"

float BurtSampleMainLightShadowRawVisibility(float3 PositionWS, float3 NormalWS, int CascadeIndex)
{
    float3 ReceiverPositionWS = BurtApplyMainLightShadowReceiverNormalBias(PositionWS, NormalWS, CascadeIndex);
    return BurtSampleMainLightShadowRawVisibility(BurtTransformWorldToMainLightShadowCascade(float4(ReceiverPositionWS, 1.0f), CascadeIndex), CascadeIndex);
}

float BurtSampleMainLightShadowWithoutPerObject(float3 PositionWS, float3 NormalWS)
{
    float MainVisibility = 1.0f;
    int CascadeIndex = _BurtMainLightShadowStrength > 0.0001f ? BurtSelectMainLightShadowCascade(PositionWS) : -1;
    if (CascadeIndex >= 0)
    {
        float RawShadow = BurtSampleMainLightShadowRawVisibility(PositionWS, NormalWS, CascadeIndex);

        float BlendWeight = BurtCalculateMainLightCascadeBlendWeight(PositionWS, CascadeIndex);
        if (BlendWeight > 0.0f)
        {
            int NextCascadeIndex = min(CascadeIndex + 1, BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES - 1);
            float NextRawShadow = BurtSampleMainLightShadowRawVisibility(PositionWS, NormalWS, NextCascadeIndex);
            RawShadow = lerp(RawShadow, NextRawShadow, BlendWeight);
        }

        RawShadow = lerp(RawShadow, 1.0f, BurtCalculateMainLightShadowFade(PositionWS, CascadeIndex));
        MainVisibility = BurtApplyShadowStrength(RawShadow, _BurtMainLightShadowStrength);
    }

    return MainVisibility;
}

float BurtSampleMainLightVolumetricShadowRaw(float3 PositionWS, int CascadeIndex)
{
    float4 ShadowCoord = BurtTransformWorldToMainLightShadowCascade(float4(PositionWS, 1.0f), CascadeIndex);
    float SafeW = abs(ShadowCoord.w) > 0.00001f ? ShadowCoord.w : (ShadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 ProjectedShadowCoord = ShadowCoord.xyz / SafeW;
    if (!BurtIsInsideMainLightShadowMap(ProjectedShadowCoord, CascadeIndex))
    {
        return 1.0f;
    }

    // Participating media has no surface normal. Use the receiver depth bias,
    // but skip the surface normal offset and selected optimized PCF path. The
    // hardware comparison sample remains bilinearly filtered and temporal
    // raymarch jitter integrates it over the volume.
    ProjectedShadowCoord.z = BurtApplyMainLightReceiverBias(ProjectedShadowCoord.z);
    return BurtSampleMainLightShadowCompare(ProjectedShadowCoord, CascadeIndex);
}

float BurtSampleMainLightVolumetricShadowFilteredRaw(float3 PositionWS, int CascadeIndex)
{
    float4 ShadowCoord = BurtTransformWorldToMainLightShadowCascade(float4(PositionWS, 1.0f), CascadeIndex);
    float SafeW = abs(ShadowCoord.w) > 0.00001f ? ShadowCoord.w : (ShadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 ProjectedShadowCoord = ShadowCoord.xyz / SafeW;
    if (!BurtIsInsideMainLightShadowMap(ProjectedShadowCoord, CascadeIndex))
    {
        return 1.0f;
    }

    ProjectedShadowCoord.z = BurtApplyMainLightReceiverBias(ProjectedShadowCoord.z);
    float2 TexelOffset = _BurtMainLightShadowTexelSize.xy * 1.5f;
    float Visibility = BurtSampleMainLightShadowCompare(ProjectedShadowCoord, CascadeIndex) * 4.0f;
    Visibility += BurtSampleMainLightShadowCompare(ProjectedShadowCoord + float3(TexelOffset.x, 0.0f, 0.0f), CascadeIndex);
    Visibility += BurtSampleMainLightShadowCompare(ProjectedShadowCoord - float3(TexelOffset.x, 0.0f, 0.0f), CascadeIndex);
    Visibility += BurtSampleMainLightShadowCompare(ProjectedShadowCoord + float3(0.0f, TexelOffset.y, 0.0f), CascadeIndex);
    Visibility += BurtSampleMainLightShadowCompare(ProjectedShadowCoord - float3(0.0f, TexelOffset.y, 0.0f), CascadeIndex);
    return Visibility * 0.125f;
}

float BurtSampleMainLightVolumetricShadow(float3 PositionWS)
{
    int CascadeIndex = _BurtMainLightShadowStrength > 0.0001f ? BurtSelectMainLightShadowCascade(PositionWS) : -1;
    if (CascadeIndex < 0)
    {
        return 1.0f;
    }

    float RawShadow = BurtSampleMainLightVolumetricShadowRaw(PositionWS, CascadeIndex);
    float BlendWeight = BurtCalculateMainLightCascadeBlendWeight(PositionWS, CascadeIndex);
    if (BlendWeight > 0.0f)
    {
        int NextCascadeIndex = min(CascadeIndex + 1, BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES - 1);
        float NextRawShadow = BurtSampleMainLightVolumetricShadowRaw(PositionWS, NextCascadeIndex);
        RawShadow = lerp(RawShadow, NextRawShadow, BlendWeight);
    }

    RawShadow = lerp(RawShadow, 1.0f, BurtCalculateMainLightShadowFade(PositionWS, CascadeIndex));
    return BurtApplyShadowStrength(RawShadow, _BurtMainLightShadowStrength);
}

float BurtSampleMainLightVolumetricShadowFiltered(float3 PositionWS)
{
    int CascadeIndex = _BurtMainLightShadowStrength > 0.0001f ? BurtSelectMainLightShadowCascade(PositionWS) : -1;
    if (CascadeIndex < 0)
    {
        return 1.0f;
    }

    float RawShadow = BurtSampleMainLightVolumetricShadowFilteredRaw(PositionWS, CascadeIndex);
    float BlendWeight = BurtCalculateMainLightCascadeBlendWeight(PositionWS, CascadeIndex);
    if (BlendWeight > 0.0f)
    {
        int NextCascadeIndex = min(CascadeIndex + 1, BURT_MAIN_LIGHT_SHADOW_MAX_CASCADES - 1);
        float NextRawShadow = BurtSampleMainLightVolumetricShadowFilteredRaw(PositionWS, NextCascadeIndex);
        RawShadow = lerp(RawShadow, NextRawShadow, BlendWeight);
    }

    RawShadow = lerp(RawShadow, 1.0f, BurtCalculateMainLightShadowFade(PositionWS, CascadeIndex));
    return BurtApplyShadowStrength(RawShadow, _BurtMainLightShadowStrength);
}

float BurtSampleMainLightShadowWithoutPerObject(float3 positionWS)
{
    return BurtSampleMainLightShadowWithoutPerObject(positionWS, _BurtMainLightDirection.xyz);
}

#if !defined(BURT_SHADOWS_EXCLUDE_TRANSMISSION)
float BurtSampleMainLightTransmissionShadow(float3 positionWS, float3 normalWS, int objectIndex, float transmissionThickness)
{
    int SliceIndex = BurtDecodePerObjectShadowSliceIndex(objectIndex);
    if (SliceIndex < 0 || transmissionThickness < 0.0f)
    {
        return 1.0f;
    }

    float SliceWorldTexelSize = max(_BurtPerObjectShadowSliceParams[SliceIndex].w, 0.0f);
    float TransmissionOffset = max(transmissionThickness, SliceWorldTexelSize * 2.0f);
    float3 TransmissionPositionWS = positionWS + BurtSafeNormalize(_BurtMainLightDirection.xyz) * TransmissionOffset;
    float MainVisibility = BurtSampleMainLightShadowWithoutPerObject(TransmissionPositionWS, normalWS);
    float PerObjectVisibility = BurtSamplePerObjectShadowExcludingSlice(positionWS, normalWS, SliceIndex);
    return min(MainVisibility, PerObjectVisibility);
}

float BurtSampleMainLightTransmissionShadow(float3 positionWS, float3 normalWS, int objectIndex)
{
    float transmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(positionWS, objectIndex, 0.0f);
    return BurtSampleMainLightTransmissionShadow(positionWS, normalWS, objectIndex, transmissionThickness);
}

float BurtSampleMainLightTransmissionShadow(float3 positionWS, float3 normalWS)
{
    return BurtSampleMainLightTransmissionShadow(positionWS, normalWS, _BurtPerObjectShadowObjectIndex);
}

#endif // !BURT_SHADOWS_EXCLUDE_TRANSMISSION

float BurtSampleMainLightShadow(float3 positionWS, float3 normalWS, int objectIndex)
{
    return min(BurtSampleMainLightShadowWithoutPerObject(positionWS, normalWS), BurtSamplePerObjectShadow(positionWS, normalWS, objectIndex));
}

float BurtSampleMainLightShadow(float3 positionWS, float3 normalWS)
{
    return BurtSampleMainLightShadow(positionWS, normalWS, _BurtPerObjectShadowObjectIndex);
}

float BurtSampleMainLightShadow(float3 positionWS)
{
    return BurtSampleMainLightShadow(positionWS, _BurtMainLightDirection.xyz);
}

float BurtSampleMainLightShadow(float4 shadowCoord)
{
    if (_BurtMainLightShadowStrength <= 0.0001f)
    {
        return 1.0f;
    }

    return BurtApplyShadowStrength(BurtSampleMainLightShadowRawVisibility(shadowCoord, 0), _BurtMainLightShadowStrength);
}

#endif // BURT_MAIN_LIGHT_SHADOWS_INCLUDED
