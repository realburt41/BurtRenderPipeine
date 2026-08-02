// Production-excluded raw shadow neighborhood diagnostics.
#ifndef BURT_SHADOWS_DEBUG_SUPPORT_INCLUDED
#define BURT_SHADOWS_DEBUG_SUPPORT_INCLUDED

float BurtConvertMainLightShadowDepthToLightDistance(float depth)
{
#if defined(UNITY_REVERSED_Z)
    return 1.0f - depth;
#else
    return depth;
#endif
}

float BurtIsMainLightShadowRawBlocker(float storedDepth, float receiverDepth)
{
#if defined(UNITY_REVERSED_Z)
    return storedDepth > receiverDepth ? 1.0f : 0.0f;
#else
    return storedDepth < receiverDepth ? 1.0f : 0.0f;
#endif
}

float BurtLoadMainLightShadowRawDepth(int2 pixelCoord)
{
    int2 shadowSize = max((int2)_BurtMainLightShadowTexelSize.zw, int2(1, 1));
    pixelCoord = clamp(pixelCoord, int2(0, 0), shadowSize - 1);
    return _BurtMainLightShadowMap.Load(int3(pixelCoord, 0)).r;
}

float BurtSampleMainLightShadowRawDepthBilinear(float2 shadowUV, int cascadeIndex)
{
    float2 clampedUV = BurtClampMainLightShadowUVToCascade(shadowUV, cascadeIndex);
    float2 shadowSize = max(_BurtMainLightShadowTexelSize.zw, float2(1.0f, 1.0f));
    float2 pixelCoord = clampedUV * shadowSize - 0.5f;
    int2 pixel0 = (int2)floor(pixelCoord);
    float2 blend = frac(pixelCoord);

    float depth00 = BurtLoadMainLightShadowRawDepth(pixel0);
    float depth10 = BurtLoadMainLightShadowRawDepth(pixel0 + int2(1, 0));
    float depth01 = BurtLoadMainLightShadowRawDepth(pixel0 + int2(0, 1));
    float depth11 = BurtLoadMainLightShadowRawDepth(pixel0 + int2(1, 1));
    return lerp(lerp(depth00, depth10, blend.x), lerp(depth01, depth11, blend.x), blend.y);
}

float BurtCompareMainLightShadowDepth(float storedDepth, float receiverDepth)
{
    return 1.0f - BurtIsMainLightShadowRawBlocker(storedDepth, receiverDepth);
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

#endif // BURT_SHADOWS_DEBUG_SUPPORT_INCLUDED
