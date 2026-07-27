// BurtRP material shading-debug transmission mode evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_TRANSMISSION_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_TRANSMISSION_INCLUDED

#ifndef BURT_PI
#define BURT_PI (3.14159265359f)
#endif

bool BurtTryEvaluateMaterialTransmissionShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_PROFILE_ID))
    {
        float visibleProfileIndex = saturate(data.SubsurfaceProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f));
        debugColor = visibleProfileIndex.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION))
    {
        debugColor = saturate(data.SubsurfaceTransmission);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_DIRECT_TRANSMISSION))
    {
        debugColor = max(data.SubsurfaceDirectTransmission, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_BRDF))
    {
        debugColor = saturate(data.SubsurfaceTransmissionBRDF);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_SHADOW))
    {
        debugColor = data.SubsurfaceTransmissionShadow.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_PHASE))
    {
        float visiblePhase = saturate(data.SubsurfaceTransmissionPhase * (4.0f * BURT_PI));
        debugColor = visiblePhase.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_THICKNESS))
    {
        debugColor = saturate(data.SubsurfaceTransmissionThickness * 0.1f).xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_KERNEL_WEIGHT))
    {
        debugColor = saturate(data.SubsurfaceKernelWeight * 4.0f);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SUBSURFACE_INDIRECT))
    {
        debugColor = max(data.SubsurfaceIndirect, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    float foliageMask = saturate(max(data.FoliageMask, BurtIsFoliageShadingModel(surfaceData.ShadingModelID) ? 1.0f : 0.0f));
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION))
    {
        debugColor = saturate(data.FoliageTransmission) * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_DIRECT_TRANSMISSION))
    {
        debugColor = max(data.FoliageDirectTransmission, float3(0.0f, 0.0f, 0.0f)) * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_BRDF))
    {
        debugColor = saturate(data.FoliageTransmissionBRDF) * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_SHADOW))
    {
        debugColor = data.FoliageTransmissionShadow.xxx * foliageMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FOLIAGE_SPECULAR_BRDF))
    {
        debugColor = saturate(data.FoliageSpecularBRDF) * foliageMask;
        return true;
    }

    float grassMask = foliageMask * saturate(surfaceData.FoliageIsGrass);
    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION))
    {
        debugColor = saturate(data.FoliageTransmission) * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_DIRECT_TRANSMISSION))
    {
        debugColor = max(data.FoliageDirectTransmission, float3(0.0f, 0.0f, 0.0f)) * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_BRDF))
    {
        debugColor = saturate(data.FoliageTransmissionBRDF) * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_SHADOW))
    {
        debugColor = data.FoliageTransmissionShadow.xxx * grassMask;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GRASS_SPECULAR_BRDF))
    {
        debugColor = saturate(data.FoliageSpecularBRDF) * grassMask;
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_TRANSMISSION_INCLUDED
