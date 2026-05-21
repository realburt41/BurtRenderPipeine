// Shared material shading-model helpers used by GBuffer and Forward passes.
#ifndef BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
#define BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(float4 baseColor, float4 maskMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float hairReflectance = saturate(_Reflectance * _HairSpecularScale);
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, hairReflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    surfaceData.smoothness = saturate(surfaceData.smoothness - _HairRoughnessOffset);
    float hairShiftScale = saturate(_HairShiftScale * maskMap.b);
    return BurtApplyHairGBufferSurfaceSemantics(surfaceData, (_HairScatter + _HairScatterBoost) * maskMap.r, hairShiftScale);
#else
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, _Metallic, maskMap, _OcclusionStrength);
    surfaceData = BurtApplyAnisotropySurfaceSemantics(surfaceData, _Anisotropy);

    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
        surfaceData = BurtApplyClearCoatSurfaceSemantics(surfaceData, _ClearCoatMask, _ClearCoatRoughness);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
        surfaceData = BurtApplySubsurfaceSurfaceSemantics(surfaceData, _SubsurfaceStrength, _SubsurfaceThickness, _SubsurfacePower, _SubsurfaceDistortion, _SubsurfaceAmbient, _SubsurfaceTint.rgb, _SubsurfaceProfileIndex, _SubsurfaceScatteringMode);
    #endif

    return surfaceData;
#endif
}

float3 BurtGetMaterialPassNormalWS(float2 normalMapUV, float3 normalWS, float4 tangentWS, float facing)
{
    return BurtSampleNormalWS(normalMapUV, normalWS, tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);
}

float3 BurtGetMaterialPassShadingDirectionWS(float3 normalWS, float4 tangentWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float strandDirectionSign = lerp(1.0f, -1.0f, saturate(_HairTangentFlip));
    return BurtSafeNormalize(tangentWS.xyz * strandDirectionSign);
#else
    return normalWS;
#endif
}

float3 BurtGetMaterialPassDebugNormalWS(float3 normalWS, float3 shadingDirectionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return shadingDirectionWS;
#else
    return normalWS;
#endif
}

BurtGBufferData BurtCreateMaterialPassGBufferData(
    BurtSurfaceData surfaceData,
    float2 normalMapUV,
    float3 geometryNormalWS,
    float3 baseNormalWS,
    float4 tangentWS,
    float3 shadingDirectionWS,
    float facing,
    float3 emissionColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtCreateHairGBufferData(surfaceData, shadingDirectionWS, emissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
    float3 clearCoatNormalWS = BurtSampleClearCoatNormalWS(normalMapUV, geometryNormalWS, tangentWS, _ClearCoatNormalScale, facing, _DoubleSidedNormalModeConstants);
    return BurtCreateClearCoatGBufferData(surfaceData, baseNormalWS, tangentWS, clearCoatNormalWS, emissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    return BurtCreateSubsurfaceGBufferData(surfaceData, baseNormalWS, tangentWS, emissionColor);
#else
    return BurtCreateGBufferData(surfaceData, baseNormalWS, tangentWS, emissionColor);
#endif
}

#endif // BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
