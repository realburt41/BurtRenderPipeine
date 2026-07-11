// BurtRP shading-debug shadow payload fill helpers.
#ifndef BURT_SHADING_DEBUG_SHADOW_INCLUDED
#define BURT_SHADING_DEBUG_SHADOW_INCLUDED

void BurtFillMainLightShadowShadingDebugData(
    float3 positionWS,
    float3 normalWS,
    out float3 shadowCascadeColor,
    out float shadowCascadeBlend,
    out float shadowDistanceFade,
    out float shadowPCSSRadius,
    out float shadowReceiverDepthDelta,
    out float mainLightShadowReceiverDepth,
    out float mainLightShadowRawDepth,
    out float mainLightShadowCompare,
    out float3 mainLightShadowProjectionValidity,
    out float shadowPCSSBlockerFraction)
{
    shadowCascadeColor = float3(0.0f, 0.0f, 0.0f);
    shadowCascadeBlend = 0.0f;
    shadowDistanceFade = 0.0f;
    shadowPCSSRadius = 0.0f;
    shadowReceiverDepthDelta = 0.0f;
    mainLightShadowReceiverDepth = 0.0f;
    mainLightShadowRawDepth = 0.0f;
    mainLightShadowCompare = 1.0f;
    mainLightShadowProjectionValidity = float3(0.0f, 0.0f, 0.0f);
    shadowPCSSBlockerFraction = 0.0f;

    if (!BurtIsShadingDebugEnabled())
    {
        return;
    }

    bool needsShadowDebug = BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA)
        || BurtNeedsMainLightShadowProjectionShadingDebug()
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION);
    if (!needsShadowDebug)
    {
        return;
    }

    shadowCascadeColor = BurtGetMainLightShadowCascadeDebugColor(positionWS);
    shadowCascadeBlend = BurtGetMainLightShadowCascadeBlendDebug(positionWS);
    shadowDistanceFade = BurtGetMainLightShadowDistanceFadeDebug(positionWS);
    shadowPCSSRadius = BurtGetMainLightShadowPCSSRadiusDebug(positionWS, normalWS);
    shadowReceiverDepthDelta = BurtGetMainLightShadowReceiverDepthDeltaDebug(positionWS, normalWS);
    BurtTryGetMainLightShadowProjectionDebug(positionWS, normalWS, mainLightShadowReceiverDepth, mainLightShadowRawDepth, mainLightShadowCompare);
    mainLightShadowProjectionValidity = BurtGetMainLightShadowProjectionValidityDebug(positionWS, normalWS);
    shadowPCSSBlockerFraction = BurtGetMainLightShadowPCSSBlockerFractionDebug(positionWS, normalWS);
}

void BurtFillPerObjectShadowShadingDebugData(
    float3 positionWS,
    float3 normalWS,
    int objectIndex,
    out float3 objectIndexColor,
    out float3 sliceColor,
    out float3 uvColor,
    out float3 depthColor,
    out float3 compareColor,
    out float3 transmissionDepthColor,
    out float3 transmissionThicknessColor)
{
    objectIndexColor = float3(0.0f, 0.0f, 0.0f);
    sliceColor = float3(0.0f, 0.0f, 0.0f);
    uvColor = float3(0.0f, 0.0f, 0.0f);
    depthColor = float3(0.0f, 0.0f, 0.0f);
    compareColor = float3(0.0f, 0.0f, 0.0f);
    transmissionDepthColor = float3(0.0f, 0.0f, 0.0f);
    transmissionThicknessColor = float3(0.0f, 0.0f, 0.0f);

    if (!BurtIsShadingDebugEnabled())
    {
        return;
    }

    bool needsProjectionDebug = BurtNeedsPerObjectShadowProjectionShadingDebug();
    bool needsTransmissionDebug = BurtNeedsPerObjectShadowTransmissionShadingDebug();
    if (!needsProjectionDebug && !needsTransmissionDebug)
    {
        return;
    }

    if (needsProjectionDebug)
    {
        BurtFillPerObjectShadowProjectionDebugData(
            positionWS,
            normalWS,
            objectIndex,
            objectIndexColor,
            sliceColor,
            uvColor,
            depthColor,
            compareColor);
    }

    if (needsTransmissionDebug)
    {
        BurtFillPerObjectShadowTransmissionDebugData(
            positionWS,
            objectIndex,
            transmissionDepthColor,
            transmissionThicknessColor);
    }
}

#endif // BURT_SHADING_DEBUG_SHADOW_INCLUDED
