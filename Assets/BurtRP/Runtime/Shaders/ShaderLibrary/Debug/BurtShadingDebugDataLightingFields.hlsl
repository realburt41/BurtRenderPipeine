#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_MAIN
    #if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DETAIL
    float3 DetailLightingColor;
    #endif
    #if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DIRECT
    float3 DirectDiffuseColor;
    float3 DirectSpecularColor;
    #endif
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_INDIRECT
    float3 IndirectDiffuseColor;
    float3 IndirectSpecularColor;
    float AmbientOcclusion;
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_GI_PROBE
    float3 GIProbeIrradiance;
    float GIProbeValidity;
    float GIProbeSkyVisibility;
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_ADDITIONAL
    float3 AdditionalDiffuseColor;
    float3 AdditionalSpecularColor;
    float3 AdditionalUnshadowedColor;
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_FINAL
    float3 EmissionColor;
    float3 FinalLightingColor;
#endif
