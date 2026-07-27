// BurtRP material shading-debug additional-lighting evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_LIGHTING_ADDITIONAL_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_LIGHTING_ADDITIONAL_INCLUDED

bool BurtTryEvaluateMaterialLightingAdditionalShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING))
    {
        debugColor = max(data.AdditionalDiffuseColor + data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_DIFFUSE))
    {
        debugColor = max(data.AdditionalDiffuseColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SPECULAR))
    {
        debugColor = max(data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED))
    {
        debugColor = max(data.AdditionalUnshadowedColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_ADDITIONAL_LIGHTING))
    {
#if BURT_ACTIVE_HAIR_SHADING_MODEL
        debugColor = max(data.AdditionalDiffuseColor + data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_HAIR_SHADING
        float3 hairAdditionalLighting = max(data.AdditionalDiffuseColor + data.AdditionalSpecularColor, float3(0.0f, 0.0f, 0.0f));
        debugColor = BurtIsActiveHairShadingModel(surfaceData.ShadingModelID) ? hairAdditionalLighting : float3(0.0f, 0.0f, 0.0f);
#else
        debugColor = float3(0.0f, 0.0f, 0.0f);
#endif
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_LIGHTING_ADDITIONAL_INCLUDED
