// BurtRP material shading-debug indirect lighting and GI probe evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_LIGHTING_INDIRECT_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_LIGHTING_INDIRECT_INCLUDED

bool BurtTryEvaluateMaterialLightingIndirectShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING))
    {
        debugColor = max(data.IndirectDiffuseColor + data.IndirectSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_DIFFUSE))
    {
        debugColor = max(data.IndirectDiffuseColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR))
    {
        debugColor = max(data.IndirectSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_IRRADIANCE))
    {
        debugColor = max(data.GIProbeIrradiance, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_VALIDITY))
    {
        debugColor = data.GIProbeValidity.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GI_PROBE_SKY_VISIBILITY))
    {
        debugColor = data.GIProbeSkyVisibility.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_AMBIENT_OCCLUSION))
    {
        debugColor = data.AmbientOcclusion.xxx;
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_LIGHTING_INDIRECT_INCLUDED
