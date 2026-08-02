// Compact XRender-style selector for the production deferred debug variant.
// This file deliberately does not include BurtShadingDebug.hlsl: the generic
// forward/debug facade builds a large surface payload and dispatcher tree. Here
// every displayed value already exists in the production lighting result.
#ifndef BURT_DEFERRED_LIGHTING_DEBUG_INCLUDED
#define BURT_DEFERRED_LIGHTING_DEBUG_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugModes.hlsl"

#define BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DETAIL 1

#ifndef BURT_PI
#define BURT_PI (3.14159265359f)
#endif

bool BurtIsDeferredLightingDebugMode(float expectedMode)
{
    return abs(_BurtShadingDebugMode - expectedMode) < 0.5f;
}

void BurtApplyDeferredLightingDetailDebugOverride(inout BurtGBufferData gBufferData)
{
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        gBufferData.BaseColor = float3(0.18f, 0.18f, 0.18f);
    }
}

float4 BurtEvaluateDeferredLightingDebugOutput(
    BurtGBufferData gBufferData,
    BurtPBRShadingComponents components,
    BurtLightingResult lightingResult,
    float shadowAttenuation,
    float3 finalPreExposedColor,
    float outputAlpha)
{
    if (_BurtShadingDebugEnabled <= 0.5f)
    {
        return float4(finalPreExposedColor, outputAlpha);
    }

    // Lighting result: identical fields used by production composition.
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
        return float4(max(lightingResult.Lighting, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE))
        return float4(max(lightingResult.DirectDiffuse, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR))
        return float4(max(lightingResult.DirectSpecular, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_INDIRECT_LIGHTING))
        return float4(max(lightingResult.IndirectDiffuse + lightingResult.IndirectSpecularReflection, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_INDIRECT_DIFFUSE))
        return float4(max(lightingResult.IndirectDiffuse, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR))
        return float4(max(lightingResult.IndirectSpecularReflection, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_AMBIENT_OCCLUSION))
        return float4(lightingResult.AmbientOcclusion.xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_EMISSION))
        return float4(max(lightingResult.Emission, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING))
        return float4(max(lightingResult.FinalLighting, 0.0f), 1.0f);
    // Use the exact attenuation consumed by production deferred lighting. This
    // includes main CSM, per-object shadow, screen-space shadow and material
    // micro-shadow terms, instead of recomputing a divergent diagnostic value.
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SHADOW_ATTENUATION))
        return float4(saturate(shadowAttenuation).xxx, 1.0f);

    // BRDF diagnostics are captured while the production BRDF is evaluated.
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS))
        return float4(components.SpecularAARoughness.xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SPECULAR_ENERGY_COMPENSATION))
        return float4(saturate((components.SpecularEnergyCompensation - 1.0f) * 0.5f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENERGY_COMPENSATION))
        return float4(saturate((components.IndirectSpecularEnergyCompensation - 1.0f) * 0.5f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_ENERGY_PRESERVATION))
        return float4(components.EnergyPreservation.xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SPECULAR_OCCLUSION))
        return float4(components.SpecularOcclusion.xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_D))
        return float4(saturate(components.DirectBRDFD * 0.05f).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_VISIBILITY))
        return float4(saturate(components.DirectBRDFVisibility).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_BRDF_FRESNEL))
        return float4(saturate(components.DirectBRDFFresnel), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_LOBE))
        return float4(saturate(components.DirectDiffuseLobe).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_DIFFUSE_BRDF))
        return float4(saturate(components.DirectDiffuseBRDF), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_DIRECT_SPECULAR_BRDF))
        return float4(saturate(components.DirectSpecularBRDF), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SPECULAR_AA_NORMAL_VARIANCE))
        return float4(saturate(components.SpecularAANormalVariance * 100.0f).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SPECULAR_AA_ROUGHNESS_DELTA))
        return float4(saturate(components.SpecularAARoughnessDelta * 5.0f).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_DFG))
        return float4(saturate(float3(components.IndirectSpecularDFG, 0.0f)), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_INDIRECT_SPECULAR_ENV_BRDF))
        return float4(saturate(components.IndirectSpecularEnvBRDF), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_HEIGHT))
        return float4(0.5f, 0.5f, 0.5f, 1.0f);

    // Subsurface, foliage/grass and hair fields also come from the same model
    // evaluator. Non-matching shading models naturally leave these fields zero.
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_PROFILE_ID))
        return float4(saturate(components.SubsurfaceProfileIndex / 7.0f).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION))
        return float4(saturate(components.SubsurfaceTransmission), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_DIRECT_TRANSMISSION))
        return float4(max(components.SubsurfaceDirectTransmission, 0.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_BRDF))
        return float4(saturate(components.SubsurfaceTransmissionBRDF), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_SHADOW))
        return float4(components.SubsurfaceTransmissionShadow.xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_PHASE))
        return float4(saturate(components.SubsurfaceTransmissionPhase * (4.0f * BURT_PI)).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_TRANSMISSION_THICKNESS))
        return float4(saturate(components.SubsurfaceTransmissionThickness * 0.1f).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_KERNEL_WEIGHT))
        return float4(saturate(components.SubsurfaceKernelWeight * 4.0f), 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_SUBSURFACE_INDIRECT))
        return float4(max(components.SubsurfaceIndirect, 0.0f), 1.0f);

    float foliageMask = saturate(max(
        components.FoliageMask,
        BurtIsFoliageShadingModel(gBufferData.ShadingModelID) ? 1.0f : 0.0f));
    float grassMask = foliageMask * saturate(gBufferData.FoliageIsGrass);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION))
        return float4(saturate(components.FoliageTransmission) * foliageMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_FOLIAGE_DIRECT_TRANSMISSION))
        return float4(max(components.FoliageDirectTransmission, 0.0f) * foliageMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_BRDF))
        return float4(saturate(components.FoliageTransmissionBRDF) * foliageMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_FOLIAGE_TRANSMISSION_SHADOW))
        return float4(components.FoliageTransmissionShadow.xxx * foliageMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_FOLIAGE_SPECULAR_BRDF))
        return float4(saturate(components.FoliageSpecularBRDF) * foliageMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION))
        return float4(saturate(components.FoliageTransmission) * grassMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_GRASS_DIRECT_TRANSMISSION))
        return float4(max(components.FoliageDirectTransmission, 0.0f) * grassMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_BRDF))
        return float4(saturate(components.FoliageTransmissionBRDF) * grassMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_GRASS_TRANSMISSION_SHADOW))
        return float4(components.FoliageTransmissionShadow.xxx * grassMask, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_GRASS_SPECULAR_BRDF))
        return float4(saturate(components.FoliageSpecularBRDF) * grassMask, 1.0f);

    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_HAIR_PRIMARY_LOBE))
        return float4(saturate(components.HairPrimaryLobe * 0.05f).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_HAIR_SECONDARY_LOBE))
        return float4(saturate(components.HairSecondaryLobe * 0.25f).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_HAIR_TRANSMISSION_LOBE))
        return float4(saturate(components.HairTransmissionLobe).xxx, 1.0f);
    if (BurtIsDeferredLightingDebugMode(BURT_SHADING_DEBUG_MODE_HAIR_SCATTER))
        return float4(components.HairScatter.xxx, 1.0f);

    return float4(finalPreExposedColor, outputAlpha);
}

#endif // BURT_DEFERRED_LIGHTING_DEBUG_INCLUDED
