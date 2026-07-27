// BurtRP material shading-debug BRDF mode evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_BRDF_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_BRDF_INCLUDED

bool BurtTryEvaluateMaterialBRDFShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS))
    {
        debugColor = data.SpecularAARoughness.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_ENERGY_COMPENSATION))
    {
        debugColor = saturate((data.SpecularEnergyCompensation - 1.0f) * 0.5f);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENERGY_COMPENSATION))
    {
        debugColor = saturate((data.IndirectSpecularEnergyCompensation - 1.0f) * 0.5f);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ENERGY_PRESERVATION))
    {
        debugColor = data.EnergyPreservation.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_OCCLUSION))
    {
        debugColor = data.SpecularOcclusion.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_D))
    {
        float visibleD = saturate(data.DirectBRDFD * 0.05f);
        debugColor = visibleD.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_VISIBILITY))
    {
        float visibleVisibility = saturate(data.DirectBRDFVisibility);
        debugColor = visibleVisibility.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_FRESNEL))
    {
        debugColor = saturate(data.DirectBRDFFresnel);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_LOBE))
    {
        float visibleDiffuseLobe = saturate(data.DirectDiffuseLobe);
        debugColor = visibleDiffuseLobe.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_BRDF))
    {
        debugColor = saturate(data.DirectDiffuseBRDF);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR_BRDF))
    {
        debugColor = saturate(data.DirectSpecularBRDF);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_NORMAL_VARIANCE))
    {
        float visibleVariance = saturate(data.SpecularAANormalVariance * 100.0f);
        debugColor = visibleVariance.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS_DELTA))
    {
        float visibleRoughnessDelta = saturate(data.SpecularAARoughnessDelta * 5.0f);
        debugColor = visibleRoughnessDelta.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_DFG))
    {
        debugColor = saturate(float3(data.IndirectSpecularDFG.x, data.IndirectSpecularDFG.y, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENV_BRDF))
    {
        debugColor = saturate(data.IndirectSpecularEnvBRDF);
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_BRDF_INCLUDED
