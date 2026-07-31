// BurtRP shading-debug per-pixel payload and common helpers.
#ifndef BURT_SHADING_DEBUG_DATA_INCLUDED
#define BURT_SHADING_DEBUG_DATA_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreSkinPosition.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugCommon.hlsl"

struct BurtShadingDebugData
{
#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_CORE
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugDataCoreFields.hlsl"
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugDataLightingFields.hlsl"
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_SHADOW
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugDataShadowFields.hlsl"
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_BRDF
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugDataBRDFFields.hlsl"
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_TRANSMISSION
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugDataTransmissionFields.hlsl"
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_HAIR
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugDataHairFields.hlsl"
#endif
};

BurtShadingDebugData BurtCreateDefaultShadingDebugData(float3 normalWS)
{
    BurtShadingDebugData data = (BurtShadingDebugData)0;
    float3 safeNormalWS = BurtSafeNormalize(normalWS);

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_CORE
    data.NormalWS = safeNormalWS;
    data.PreSkinPositionOS = float3(0.0f, 0.0f, 0.0f);
    data.PreSkinPositionDebugColor = float3(0.0f, 0.0f, 0.0f);
    data.PreSkinPositionAvailable = 0.0f;
    data.Reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    data.PerceptualRoughness = 0.5f;
    data.DiffuseColor = float3(0.0f, 0.0f, 0.0f);

    data.GBufferBaseColor = float3(0.0f, 0.0f, 0.0f);
    data.GBufferNormalWS = safeNormalWS;
    data.GBufferMetallic = 0.0f;
    data.GBufferClearCoatMask = 0.0f;
    data.GBufferClearCoatNormalWS = safeNormalWS;
    data.GBufferClearCoatRoughness = 0.0f;
    data.GBufferSubsurfaceStrength = 0.0f;
    data.GBufferSubsurfaceThickness = 0.0f;
    data.GBufferSubsurfaceProfileIndex = 0.0f;
    data.GBufferAnisotropy = 0.0f;
    data.GBufferTangentWS = BurtSafeNormalize(abs(safeNormalWS.y) < 0.999f ? cross(float3(0.0f, 1.0f, 0.0f), safeNormalWS) : cross(float3(1.0f, 0.0f, 0.0f), safeNormalWS));
    data.GBufferSmoothness = 0.5f;
    data.GBufferOcclusion = 1.0f;
    data.GBufferReflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    data.GBufferRoughness = 0.5f;
    data.GBufferDiffuseColor = float3(0.0f, 0.0f, 0.0f);
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_MAIN
    #if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DETAIL
    data.DetailLightingColor = float3(0.0f, 0.0f, 0.0f);
    #endif
    #if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DIRECT
    data.DirectDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.DirectSpecularColor = float3(0.0f, 0.0f, 0.0f);
    #endif
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_INDIRECT
    data.IndirectDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.IndirectSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.AmbientOcclusion = 1.0f;
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_GI_PROBE
    data.GIProbeIrradiance = float3(0.0f, 0.0f, 0.0f);
    data.GIProbeValidity = 0.0f;
    data.GIProbeSkyVisibility = 0.0f;
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_ADDITIONAL
    data.AdditionalDiffuseColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalSpecularColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalUnshadowedColor = float3(0.0f, 0.0f, 0.0f);
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_SHADOW
    data.ShadowAttenuation = 1.0f;
    data.AdditionalShadowAttenuation = 1.0f;
    data.AdditionalShadowFaceColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalShadowUVColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalShadowDepthColor = float3(0.0f, 0.0f, 0.0f);
    data.AdditionalShadowDepthDeltaColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowObjectIndexColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowSliceColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowUVColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowDepthColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowCompareColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowTransmissionDepthColor = float3(0.0f, 0.0f, 0.0f);
    data.PerObjectShadowTransmissionThicknessColor = float3(0.0f, 0.0f, 0.0f);
    data.ShadowCascadeColor = float3(0.0f, 0.0f, 0.0f);
    data.ShadowCascadeBlend = 0.0f;
    data.ShadowDistanceFade = 0.0f;
    data.ShadowPCSSRadius = 0.0f;
    data.ShadowReceiverDepthDelta = 0.0f;
    data.MainLightShadowReceiverDepth = 0.0f;
    data.MainLightShadowRawDepth = 0.0f;
    data.MainLightShadowCompare = 1.0f;
    data.MainLightShadowProjectionValidity = float3(0.0f, 0.0f, 0.0f);
    data.ShadowPCSSBlockerFraction = 0.0f;
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_FINAL
    data.EmissionColor = float3(0.0f, 0.0f, 0.0f);
    data.FinalLightingColor = float3(0.0f, 0.0f, 0.0f);
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_BRDF
    data.SpecularAARoughness = 0.5f;
    data.SpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    data.IndirectSpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    data.EnergyPreservation = 1.0f;
    data.SpecularOcclusion = 1.0f;
    data.DirectBRDFD = 0.0f;
    data.DirectBRDFVisibility = 0.0f;
    data.DirectBRDFFresnel = float3(0.0f, 0.0f, 0.0f);
    data.DirectDiffuseLobe = 0.0f;
    data.DirectDiffuseBRDF = float3(0.0f, 0.0f, 0.0f);
    data.DirectSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    data.SpecularAANormalVariance = 0.0f;
    data.SpecularAARoughnessDelta = 0.0f;
    data.IndirectSpecularDFG = float2(0.0f, 0.0f);
    data.IndirectSpecularEnvBRDF = float3(0.0f, 0.0f, 0.0f);
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_HAIR
    data.HairPrimaryLobe = 0.0f;
    data.HairSecondaryLobe = 0.0f;
    data.HairTransmissionLobe = 0.0f;
    data.HairScatter = 0.0f;
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_TRANSMISSION
    data.SubsurfaceProfileIndex = 0.0f;
    data.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceTransmissionShadow = 1.0f;
    data.SubsurfaceTransmissionPhase = 0.0f;
    data.SubsurfaceTransmissionThickness = 0.0f;
    data.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    data.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);

    data.FoliageMask = 0.0f;
    data.FoliageTransmission = float3(0.0f, 0.0f, 0.0f);
    data.FoliageDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    data.FoliageTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    data.FoliageTransmissionShadow = 1.0f;
    data.FoliageSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
#endif

    return data;
}

#endif // BURT_SHADING_DEBUG_DATA_INCLUDED
