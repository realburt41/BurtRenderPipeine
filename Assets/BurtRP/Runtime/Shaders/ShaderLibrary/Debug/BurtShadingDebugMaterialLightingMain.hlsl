// BurtRP material shading-debug main-light/detail lighting evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_LIGHTING_MAIN_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_LIGHTING_MAIN_INCLUDED

bool BurtTryEvaluateMaterialLightingMainShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    #if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DETAIL
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        debugColor = max(data.DetailLightingColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }
    #endif

    #if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DIRECT
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE))
    {
        debugColor = max(data.DirectDiffuseColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR))
    {
        debugColor = max(data.DirectSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }
    #endif

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_LIGHTING_MAIN_INCLUDED
