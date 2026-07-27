// BurtRP material shading-debug hair mode evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_HAIR_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_HAIR_INCLUDED

bool BurtTryEvaluateMaterialHairShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_PRIMARY_LOBE))
    {
        float visiblePrimary = saturate(data.HairPrimaryLobe * 0.05f);
        debugColor = visiblePrimary.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SECONDARY_LOBE))
    {
        float visibleSecondary = saturate(data.HairSecondaryLobe * 0.25f);
        debugColor = visibleSecondary.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_TRANSMISSION_LOBE))
    {
        float visibleTransmission = saturate(data.HairTransmissionLobe);
        debugColor = visibleTransmission.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HAIR_SCATTER))
    {
        debugColor = data.HairScatter.xxx;
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_HAIR_INCLUDED
