// BurtRP material shading-debug category dispatcher.
#ifndef BURT_SHADING_DEBUG_MATERIAL_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_INCLUDED

#ifndef BURT_PI
#define BURT_PI (3.14159265359f)
#endif

bool BurtTryEvaluateMaterialShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (!BurtIsShadingDebugEnabled())
    {
        return false;
    }

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_CORE
    if (BurtTryEvaluateMaterialCoreShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_BRDF
    if (BurtTryEvaluateMaterialBRDFShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_TRANSMISSION
    if (BurtTryEvaluateMaterialTransmissionShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_HAIR
    if (BurtTryEvaluateMaterialHairShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING
    if (BurtTryEvaluateMaterialLightingShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_SHADOW
    if (BurtTryEvaluateMaterialShadowShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_INCLUDED
