// BurtRP material shading-debug core and GBuffer mode evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_CORE_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_CORE_INCLUDED

bool BurtTryEvaluateMaterialCoreShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ALBEDO))
    {
        debugColor = max(surfaceData.BaseColor.rgb, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_NORMAL_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.NormalWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_SMOOTHNESS))
    {
        debugColor = surfaceData.Smoothness.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_METALLIC))
    {
        debugColor = surfaceData.Metallic.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_OCCLUSION))
    {
        debugColor = surfaceData.Occlusion.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_HEIGHT))
    {
        debugColor = surfaceData.Height.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PRESKIN_POSITION))
    {
        debugColor = data.PreSkinPositionAvailable > 0.5f
            ? saturate(data.PreSkinPositionDebugColor)
            : float3(0.0f, 0.0f, 0.0f);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_REFLECTANCE))
    {
        debugColor = data.Reflectance.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ROUGHNESS))
    {
        debugColor = data.PerceptualRoughness.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DIFFUSE_COLOR))
    {
        debugColor = saturate(data.DiffuseColor);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_BASE_COLOR))
    {
        debugColor = max(data.GBufferBaseColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_NORMAL_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.GBufferNormalWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_METALLIC))
    {
        debugColor = data.GBufferMetallic.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_MASK))
    {
        debugColor = data.GBufferClearCoatMask.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_NORMAL_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.GBufferClearCoatNormalWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_CLEAR_COAT_ROUGHNESS))
    {
        debugColor = data.GBufferClearCoatRoughness.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_STRENGTH))
    {
        debugColor = data.GBufferSubsurfaceStrength.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_THICKNESS))
    {
        debugColor = data.GBufferSubsurfaceThickness.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SUBSURFACE_PROFILE_INDEX))
    {
        float visibleProfileIndex = saturate(data.GBufferSubsurfaceProfileIndex / max(BURT_SHADING_DEBUG_SUBSURFACE_PROFILE_SLOT_COUNT - 1.0f, 1.0f));
        debugColor = visibleProfileIndex.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ANISOTROPY))
    {
        float encodedAnisotropy = saturate(data.GBufferAnisotropy * 0.5f + 0.5f);
        debugColor = encodedAnisotropy.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_TANGENT_WS))
    {
        debugColor = BurtEncodeNormalWSForDebug(data.GBufferTangentWS);
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_SMOOTHNESS))
    {
        debugColor = data.GBufferSmoothness.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_OCCLUSION))
    {
        debugColor = data.GBufferOcclusion.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_REFLECTANCE))
    {
        debugColor = data.GBufferReflectance.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_ROUGHNESS))
    {
        debugColor = data.GBufferRoughness.xxx;
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_GBUFFER_DIFFUSE_COLOR))
    {
        debugColor = saturate(data.GBufferDiffuseColor);
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_CORE_INCLUDED
