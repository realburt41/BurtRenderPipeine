// Shadow diagnostics kept out of the runtime sampling layer.
#ifndef BURT_SHADOWS_DEBUG_INCLUDED
#define BURT_SHADOWS_DEBUG_INCLUDED

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

float3 BurtGetMainLightShadowProjectionValidityDebug(float3 PositionWS, float3 NormalWS)
{
    int CascadeIndex = BurtSelectMainLightShadowCascade(PositionWS);
    if (CascadeIndex < 0)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float3 ReceiverPositionWS = BurtApplyMainLightShadowReceiverNormalBias(PositionWS, NormalWS, CascadeIndex);
    float3 ProjectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(ReceiverPositionWS, CascadeIndex);
    float4 AtlasRect = BurtGetMainLightShadowCascadeAtlasRect(CascadeIndex);
    float2 AtlasTexelMargin = BurtGetMainLightShadowBaseAtlasTexelMargin(CascadeIndex);
    bool InsideUV = ProjectedShadowCoord.x > AtlasRect.x + AtlasTexelMargin.x
        && ProjectedShadowCoord.x < AtlasRect.z - AtlasTexelMargin.x
        && ProjectedShadowCoord.y > AtlasRect.y + AtlasTexelMargin.y
        && ProjectedShadowCoord.y < AtlasRect.w - AtlasTexelMargin.y;
    bool InsideDepth = ProjectedShadowCoord.z > 0.0f && ProjectedShadowCoord.z < 1.0f;

    // White: valid. Red: atlas UV rejected. Blue: projected depth rejected. Magenta: both rejected.
    if (InsideUV && InsideDepth)
    {
        float receiverDepth = BurtApplyMainLightReceiverBias(ProjectedShadowCoord.z);
        float storedDepth = BurtSampleMainLightShadowRawDepthBilinear(ProjectedShadowCoord.xy, CascadeIndex);
        float manualVisibility = BurtCompareMainLightShadowDepth(storedDepth, receiverDepth);
        float hardwareVisibility = BurtSampleMainLightShadowCompare(float3(ProjectedShadowCoord.xy, receiverDepth), CascadeIndex);
        bool manualShadowed = manualVisibility < 0.5f;
        bool hardwareShadowed = hardwareVisibility < 0.5f;

        // White: both lit. Green: both shadowed. Yellow: manual-only shadow.
        // Cyan: hardware-only shadow. The rejected projection colors below stay unchanged.
        if (manualShadowed && hardwareShadowed)
        {
            return float3(0.0f, 1.0f, 0.0f);
        }

        if (manualShadowed)
        {
            return float3(1.0f, 1.0f, 0.0f);
        }

        if (hardwareShadowed)
        {
            return float3(0.0f, 1.0f, 1.0f);
        }

        return float3(1.0f, 1.0f, 1.0f);
    }

    if (!InsideUV && !InsideDepth)
    {
        return float3(1.0f, 0.0f, 1.0f);
    }

    return InsideUV ? float3(0.0f, 0.0f, 1.0f) : float3(1.0f, 0.0f, 0.0f);
}

bool BurtTryGetMainLightShadowProjectionDebug(float3 PositionWS, float3 NormalWS, out float ReceiverDepth, out float RawDepth, out float CompareVisibility)
{
    ReceiverDepth = 0.0f;
    RawDepth = 0.0f;
    CompareVisibility = 1.0f;

    int CascadeIndex = BurtSelectMainLightShadowCascade(PositionWS);
    if (CascadeIndex < 0)
    {
        return false;
    }

    float3 ReceiverPositionWS = BurtApplyMainLightShadowReceiverNormalBias(PositionWS, NormalWS, CascadeIndex);
    float3 ProjectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(ReceiverPositionWS, CascadeIndex);
    if (!BurtIsInsideMainLightShadowMap(ProjectedShadowCoord, CascadeIndex))
    {
        return false;
    }

    ReceiverDepth = BurtApplyMainLightReceiverBias(ProjectedShadowCoord.z);
    RawDepth = BurtSampleMainLightShadowRawDepthBilinear(ProjectedShadowCoord.xy, CascadeIndex);
    CompareVisibility = BurtSampleMainLightShadowCompare(float3(ProjectedShadowCoord.xy, ReceiverDepth), CascadeIndex);
    return true;
}

float3 BurtGetPerObjectShadowSliceDebugColor(int sliceIndex)
{
    if (sliceIndex == 0) return float3(1.0f, 0.0f, 0.0f);
    if (sliceIndex == 1) return float3(0.0f, 1.0f, 0.0f);
    if (sliceIndex == 2) return float3(0.0f, 0.0f, 1.0f);
    if (sliceIndex == 3) return float3(1.0f, 1.0f, 0.0f);
    if (sliceIndex == 4) return float3(1.0f, 0.0f, 1.0f);
    if (sliceIndex == 5) return float3(0.0f, 1.0f, 1.0f);
    if (sliceIndex == 6) return float3(1.0f, 0.5f, 0.0f);
    if (sliceIndex == 7) return float3(0.5f, 0.0f, 1.0f);
    return float3(0.25f, 0.25f, 0.25f);
}

#define BURT_PER_OBJECT_SHADOW_TRANSMISSION_DEBUG_MAX_THICKNESS (10.0f)
void BurtFillPerObjectShadowProjectionDebugData(
    float3 positionWS,
    float3 normalWS,
    int objectIndex,
    out float3 objectIndexColor,
    out float3 sliceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 compareColor)
{
    objectIndexColor = float3(0.0f, 0.0f, 0.0f);
    sliceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    compareColor = float3(0.0f, 0.0f, 0.0f);

    int sliceCount = BurtGetPerObjectShadowSliceCount();
    objectIndex = max(objectIndex, 0);
    float objectIndexDebug = saturate((float)objectIndex / max((float)sliceCount, 1.0f));
    objectIndexColor = float3(objectIndexDebug, objectIndexDebug, objectIndexDebug);

    int sliceIndex = BurtDecodePerObjectShadowSliceIndex(objectIndex);
    if (sliceIndex < 0)
    {
        return;
    }

    float3 biasedPositionWS = BurtApplyPerObjectShadowReceiverBias(positionWS, normalWS, sliceIndex);
    float4 shadowCoord = BurtTransformWorldToPerObjectShadowSlice(float4(biasedPositionWS, 1.0f), sliceIndex);
    float safeW = abs(shadowCoord.w) > 0.00001f ? shadowCoord.w : (shadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 projectedShadowCoord = shadowCoord.xyz / safeW;
    bool insideAtlas = BurtIsInsidePerObjectShadowAtlas(projectedShadowCoord, sliceIndex);

    float4 sliceParams = _BurtPerObjectShadowSliceParams[sliceIndex];
    float receiverDepth = saturate(projectedShadowCoord.z + max(sliceParams.y, 0.0f));
    float rawDepth = BurtSamplePerObjectShadowRawDepth(projectedShadowCoord.xy, sliceIndex);
    float compareVisibility = insideAtlas ? BurtSamplePerObjectShadowCompare(float3(projectedShadowCoord.xy, receiverDepth), sliceIndex) : 1.0f;
    float insideWeight = insideAtlas ? 1.0f : 0.25f;

    sliceColor = BurtGetPerObjectShadowSliceDebugColor(sliceIndex) * insideWeight;
    uvColor = float3(saturate(BurtClampPerObjectShadowUVToRect(projectedShadowCoord.xy, sliceIndex)), insideAtlas ? 1.0f : 0.0f);
    depthColor = float3(saturate(receiverDepth), saturate(rawDepth), saturate(compareVisibility));
    compareColor = float3(compareVisibility, compareVisibility, compareVisibility);
}

void BurtFillPerObjectShadowTransmissionDebugData(
    float3 positionWS,
    int objectIndex,
    out float3 transmissionDepthColor,
    out float3 transmissionThicknessColor)
{
    transmissionDepthColor = float3(0.0f, 0.0f, 0.0f);
    transmissionThicknessColor = float3(0.0f, 0.0f, 0.0f);

    int sliceCount = BurtGetPerObjectShadowSliceCount();
    int safeObjectIndex = max(objectIndex, 0);
    float objectIndexDebug = saturate((float)safeObjectIndex / max((float)sliceCount, 1.0f));
    if (safeObjectIndex <= 0)
    {
        return;
    }

    int sliceIndex = BurtDecodePerObjectShadowSliceIndex(safeObjectIndex);
    if (sliceIndex < 0)
    {
        transmissionThicknessColor = float3(objectIndexDebug, 0.0f, 0.25f);
        return;
    }

    float4 depthParams = _BurtPerObjectShadowSliceDepthParams[sliceIndex];
    if (depthParams.w <= 0.5f || depthParams.x <= 0.0001f)
    {
        transmissionThicknessColor = float3(objectIndexDebug, 0.0f, 0.5f);
        return;
    }

    float4 shadowCoord = BurtTransformWorldToPerObjectShadowSlice(float4(positionWS, 1.0f), sliceIndex);
    float safeW = abs(shadowCoord.w) > 0.00001f ? shadowCoord.w : (shadowCoord.w < 0.0f ? -0.00001f : 0.00001f);
    float3 projectedShadowCoord = shadowCoord.xyz / safeW;
    float surfaceDepth = saturate(projectedShadowCoord.z);
    if (!BurtIsInsidePerObjectShadowAtlas(projectedShadowCoord, sliceIndex))
    {
        transmissionDepthColor = float3(surfaceDepth, 0.0f, 0.0f);
        transmissionThicknessColor = float3(objectIndexDebug, 0.0f, 0.75f);
        return;
    }

    float rawDepth = BurtSamplePerObjectShadowRawDepth(projectedShadowCoord.xy, sliceIndex);
    float depthDelta = max(saturate(rawDepth) - surfaceDepth, 0.0f);
    float thickness = clamp(saturate(depthDelta) * max(depthParams.x, 0.001f), 0.0f, BURT_PER_OBJECT_SHADOW_TRANSMISSION_DEBUG_MAX_THICKNESS);
    transmissionDepthColor = float3(surfaceDepth, saturate(rawDepth), saturate(depthDelta * 16.0f));
    transmissionThicknessColor = float3(
        objectIndexDebug,
        saturate(thickness / BURT_PER_OBJECT_SHADOW_TRANSMISSION_DEBUG_MAX_THICKNESS),
        1.0f);
}

float BurtGetMainLightShadowPCSSRadiusDebug(float3 PositionWS, float3 NormalWS)
{
    if (_BurtMainLightShadowPCSSParams.x <= 0.5f)
    {
        return 0.0f;
    }

    int CascadeIndex = BurtSelectMainLightShadowCascade(PositionWS);
    if (CascadeIndex < 0)
    {
        return 0.0f;
    }

    float3 ReceiverPositionWS = BurtApplyMainLightShadowReceiverNormalBias(PositionWS, NormalWS, CascadeIndex);
    float3 ProjectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(ReceiverPositionWS, CascadeIndex);
    if (!BurtIsInsideMainLightShadowMap(ProjectedShadowCoord, CascadeIndex))
    {
        return 0.0f;
    }

    ProjectedShadowCoord.z = BurtApplyMainLightReceiverBias(ProjectedShadowCoord.z);
    float Radius = BurtCalculateMainLightShadowPCSSRadiusTexels(ProjectedShadowCoord, CascadeIndex);
    float NormalizedRadius = saturate(Radius / max(_BurtMainLightShadowPCSSParams.w, 1.5f));

    // For debug readability, suppress the wide low-level halo on lit receivers and emphasize
    // where the current PCSS filter is actually contributing to visible shadowing.
    float ShadowedWeight = saturate((1.0f - BurtSampleMainLightShadowPCSS(ProjectedShadowCoord, CascadeIndex)) * 1.25f);
    return NormalizedRadius * ShadowedWeight;
}

float BurtGetMainLightShadowPCSSBlockerFractionDebug(float3 PositionWS, float3 NormalWS)
{
    if (_BurtMainLightShadowPCSSParams.x <= 0.5f)
    {
        return 0.0f;
    }

    int CascadeIndex = BurtSelectMainLightShadowCascade(PositionWS);
    if (CascadeIndex < 0)
    {
        return 0.0f;
    }

    float3 ReceiverPositionWS = BurtApplyMainLightShadowReceiverNormalBias(PositionWS, NormalWS, CascadeIndex);
    float3 ProjectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(ReceiverPositionWS, CascadeIndex);
    if (!BurtIsInsideMainLightShadowMap(ProjectedShadowCoord, CascadeIndex))
    {
        return 0.0f;
    }

    ProjectedShadowCoord.z = BurtApplyMainLightReceiverBias(ProjectedShadowCoord.z);

    float BlockerFraction = 0.0f;
    float AverageBlockerDistance = 0.0f;
    float AverageBlockerDepth = 0.0f;
    BurtTryEvaluateMainLightShadowPCSSBlockers(ProjectedShadowCoord, CascadeIndex, BlockerFraction, AverageBlockerDistance, AverageBlockerDepth);

    // Raw blocker fraction is too sparse to read on narrow silhouettes when only a few Poisson
    // taps land on the occluder. Use the center sample as a floor for debug visibility, then
    // gate by actual shadow visibility so lit receivers do not show false self-blockers.
    float CenterRawDepth = BurtSampleMainLightShadowRawDepthBilinear(ProjectedShadowCoord.xy, CascadeIndex);
    float CenterIsBlocker = BurtIsMainLightShadowRawBlocker(CenterRawDepth, ProjectedShadowCoord.z);
    float DisplayFraction = saturate(max(BlockerFraction, CenterIsBlocker * 0.35f) * 1.75f);
    float BlockedWeight = saturate(1.0f - BurtSampleMainLightShadowPCSS(ProjectedShadowCoord, CascadeIndex));
    return DisplayFraction * BlockedWeight;
}

float BurtGetMainLightShadowReceiverDepthDeltaDebug(float3 positionWS, float3 normalWS)
{
    int cascadeIndex = BurtSelectMainLightShadowCascade(positionWS);
    if (cascadeIndex < 0)
    {
        return 0.0f;
    }

    float3 receiverPositionWS = BurtApplyMainLightShadowReceiverNormalBias(positionWS, normalWS, cascadeIndex);
    float3 ProjectedShadowCoord = BurtProjectWorldToMainLightShadowCascade(receiverPositionWS, cascadeIndex);
    if (!BurtIsInsideMainLightShadowMap(ProjectedShadowCoord, cascadeIndex))
    {
        return 0.0f;
    }

    float ReceiverDepth = BurtApplyMainLightReceiverBias(ProjectedShadowCoord.z);
    float StoredDepthCenter = 0.0f;
    float StoredDepthSurface = 0.0f;
    float StoredDepthAverage = 0.0f;
    float StoredDepthSpan = 0.0f;
    float StoredDepthSurfaceCurvature = 0.0f;
    BurtSampleMainLightShadowDebugNeighborhood(ProjectedShadowCoord.xy, cascadeIndex, StoredDepthCenter, StoredDepthSurface, StoredDepthAverage, StoredDepthSpan, StoredDepthSurfaceCurvature);

    // Convert both receiver/stored depth into the same light-distance convention before
    // taking the difference; otherwise reversed-Z platforms turn this debug into a shadow mask.
    float ReceiverDistance = BurtConvertMainLightShadowDepthToLightDistance(ReceiverDepth);
    float StoredDistanceCenter = BurtConvertMainLightShadowDepthToLightDistance(StoredDepthCenter);
    float StoredDistanceSurface = BurtConvertMainLightShadowDepthToLightDistance(StoredDepthSurface);
    float StoredDistanceAverage = BurtConvertMainLightShadowDepthToLightDistance(StoredDepthAverage);
    float StoredDistanceSpan = StoredDepthSpan;
    float StoredDistanceCurvature = StoredDepthSurfaceCurvature;
    float SignedDistanceDelta = ReceiverDistance - StoredDistanceSurface;
    float NeighborhoodDisagreement = abs(StoredDistanceSurface - StoredDistanceAverage);
    float LocalPlaneResidual = abs(StoredDistanceCenter - StoredDistanceSurface);

    // This view is for receiver-vs-shadow-surface alignment, not for visualizing full cast-shadow
    // separation. Once the pixel is genuinely blocked by another surface, blend back toward mid-gray
    // so acne/bias pressure on lit receivers remains readable.
    float Visibility = BurtSampleMainLightShadowPCSS(float3(ProjectedShadowCoord.xy, ReceiverDepth), cascadeIndex);
    float LitWeight = saturate((Visibility - 0.05f) / 0.95f);

    // Keep the scale wide enough that small flat-surface bias differences stay around mid-gray.
    float DisplayScale = max(max(max(max(StoredDistanceSpan * 2.5f, NeighborhoodDisagreement * 3.0f), max(LocalPlaneResidual, StoredDistanceCurvature) * 6.0f), _BurtMainLightShadowSampleBias * 24.0f), 0.002f);
    float DeltaDebug = saturate(0.5f + SignedDistanceDelta / DisplayScale);
    return lerp(0.5f, DeltaDebug, LitWeight);
}

float3 BurtGetAdditionalLightShadowFaceDebugColor(int sliceOffset)
{
    if (sliceOffset == 0) return float3(1.0f, 0.0f, 0.0f);
    if (sliceOffset == 1) return float3(0.0f, 1.0f, 0.0f);
    if (sliceOffset == 2) return float3(0.0f, 0.0f, 1.0f);
    if (sliceOffset == 3) return float3(1.0f, 1.0f, 0.0f);
    if (sliceOffset == 4) return float3(1.0f, 0.0f, 1.0f);
    if (sliceOffset == 5) return float3(0.0f, 1.0f, 1.0f);
    return float3(0.25f, 0.25f, 0.25f);
}

bool BurtGetAdditionalLightShadowProjectionDebug(
    int lightIndex,
    float3 positionWS,
    float3 lightPositionWS,
    float3 lightDirectionWS,
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

    float4 shadowData;
    int sliceIndex;
    int sliceOffset;
    bool isPointShadow;
    if (!BurtResolveAdditionalLightShadowSlice(lightIndex, positionWS, lightPositionWS, lightDirectionWS, shadowData, sliceIndex, sliceOffset, isPointShadow))
    {
        return false;
    }

    float3 shadowPositionWS = BurtResolveAdditionalLightShadowSamplePositionWS(lightIndex, positionWS, normalWS, shadowData);
    if (isPointShadow)
    {
        float4 lightParams = _BurtAdditionalLightShadowLightParams[lightIndex];
        if (!BurtResolveAdditionalLightShadowSliceFromData(lightIndex, shadowPositionWS, lightPositionWS, lightDirectionWS, shadowData, lightParams, sliceIndex, sliceOffset, isPointShadow))
        {
            return false;
        }
    }

    float4 shadowCoord;
    if (!BurtTryProjectAdditionalLightShadowSlice(shadowPositionWS, sliceIndex, shadowCoord))
    {
        return false;
    }

    float3 projectedShadowCoord = shadowCoord.xyz / shadowCoord.w;
    bool insideAtlas = BurtIsInsideAdditionalLightShadowAtlas(projectedShadowCoord, sliceIndex, isPointShadow);
    float2 clampedUV = BurtClampAdditionalLightShadowUVToRect(projectedShadowCoord.xy, sliceIndex, isPointShadow);
    float rawDepth = BurtSampleAdditionalLightShadowRawDepth(projectedShadowCoord.xy, sliceIndex, isPointShadow);
    float receiverDepth = BurtApplyAdditionalLightReceiverBias(projectedShadowCoord.z);
    float compareVisibility = insideAtlas ? BurtSampleAdditionalLightShadowCompare(shadowCoord, sliceIndex, isPointShadow) : 1.0f;

    faceColor = isPointShadow ? BurtGetAdditionalLightShadowFaceDebugColor(sliceOffset) : float3(1.0f, 1.0f, 1.0f);
    uvColor = float3(saturate(clampedUV), insideAtlas ? 1.0f : 0.0f);
    float receiverDistance = BurtConvertAdditionalLightShadowDepthToLightDistance(receiverDepth);
    float storedDistance = BurtConvertAdditionalLightShadowDepthToLightDistance(rawDepth);
    depthColor = float3(saturate(receiverDepth), saturate(rawDepth), saturate(compareVisibility));
    depthDeltaColor = float3(saturate((receiverDistance - storedDistance) * 25.0f + 0.5f), saturate(receiverDepth), saturate(rawDepth));
    return true;
}

#endif
