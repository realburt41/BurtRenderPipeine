// BurtRP material shading-debug shadow mode evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_SHADOW_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_SHADOW_INCLUDED

bool BurtTryEvaluateMaterialShadowShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_ATTENUATION))
    {
        debugColor = data.ShadowAttenuation.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RECEIVER_DEPTH))
    {
        debugColor = data.MainLightShadowReceiverDepth.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RAW_DEPTH))
    {
        debugColor = data.MainLightShadowRawDepth.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_COMPARE))
    {
        debugColor = data.MainLightShadowCompare.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_PROJECTION_VALIDITY))
    {
        debugColor = data.MainLightShadowProjectionValidity;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_ATTENUATION))
    {
        debugColor = data.AdditionalShadowAttenuation.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE))
    {
        debugColor = saturate(data.AdditionalShadowFaceColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV))
    {
        debugColor = saturate(data.AdditionalShadowUVColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH))
    {
        debugColor = saturate(data.AdditionalShadowDepthColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH_DELTA))
    {
        debugColor = saturate(data.AdditionalShadowDepthDeltaColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_OBJECT_INDEX))
    {
        debugColor = saturate(data.PerObjectShadowObjectIndexColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_SLICE))
    {
        debugColor = saturate(data.PerObjectShadowSliceColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_UV))
    {
        debugColor = saturate(data.PerObjectShadowUVColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_DEPTH))
    {
        debugColor = saturate(data.PerObjectShadowDepthColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_COMPARE))
    {
        debugColor = saturate(data.PerObjectShadowCompareColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_DEPTH))
    {
        debugColor = saturate(data.PerObjectShadowTransmissionDepthColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_THICKNESS))
    {
        debugColor = saturate(data.PerObjectShadowTransmissionThicknessColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_INDEX))
    {
        debugColor = saturate(data.ShadowCascadeColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_CASCADE_BLEND))
    {
        debugColor = data.ShadowCascadeBlend.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_DISTANCE_FADE))
    {
        debugColor = data.ShadowDistanceFade.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_RADIUS))
    {
        debugColor = data.ShadowPCSSRadius.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_RECEIVER_DEPTH_DELTA))
    {
        debugColor = data.ShadowReceiverDepthDelta.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SHADOW_PCSS_BLOCKER_FRACTION))
    {
        debugColor = data.ShadowPCSSBlockerFraction.xxx;
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_SHADOW_INCLUDED
