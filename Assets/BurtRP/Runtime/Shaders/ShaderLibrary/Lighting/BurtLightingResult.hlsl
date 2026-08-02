#ifndef BURT_LIGHTING_RESULT_INCLUDED
#define BURT_LIGHTING_RESULT_INCLUDED

// Stable lighting output shared by production composition and shading debug.
// XRender keeps diagnostic channels out of production result structs. Mirror
// that behavior here: normal deferred variants only carry the final value,
// while the explicitly requested debug variant retains channel breakdowns.
#ifndef BURT_LIGHTING_RESULT_INCLUDE_DEBUG_CHANNELS
#define BURT_LIGHTING_RESULT_INCLUDE_DEBUG_CHANNELS 0
#endif

struct BurtLightingResult
{
#if BURT_LIGHTING_RESULT_INCLUDE_DEBUG_CHANNELS
    float3 DirectDiffuse;
    float3 DirectSpecular;
    float3 DirectTransmission;
    float3 DirectLighting;

    float3 AdditionalDiffuse;
    float3 AdditionalSpecular;
    float3 AdditionalLighting;

    float3 IndirectDiffuse;
    float3 IndirectSpecularReflection;
    float3 IndirectSpecularTransmission;
    float3 IndirectLighting;

    float3 Emission;
    float3 Lighting;
#endif
    float3 FinalLighting;
#if BURT_LIGHTING_RESULT_INCLUDE_DEBUG_CHANNELS
    float AmbientOcclusion;
#endif
};

BurtLightingResult BurtCreateLightingResult(
    BurtPBRShadingComponents Components,
    float3 Emission,
    float AmbientOcclusion)
{
    BurtLightingResult Result = (BurtLightingResult)0;
#if BURT_LIGHTING_RESULT_INCLUDE_DEBUG_CHANNELS
    Result.DirectDiffuse = Components.DirectDiffuse;
    Result.DirectSpecular = Components.DirectSpecular;
    Result.DirectTransmission = Components.DirectTransmission;
    Result.DirectLighting = Components.DirectLighting;

    Result.AdditionalDiffuse = Components.AdditionalDiffuse;
    Result.AdditionalSpecular = Components.AdditionalSpecular;
    Result.AdditionalLighting = Components.AdditionalLighting;

    Result.IndirectDiffuse = Components.IndirectDiffuse;
    Result.IndirectSpecularReflection = Components.IndirectSpecular;
    Result.IndirectSpecularTransmission = Components.SubsurfaceIndirectTransmission;
    Result.IndirectLighting = Components.IndirectLighting;

    Result.Emission = Emission;
    Result.Lighting = Components.Lighting;
    Result.FinalLighting = Result.Lighting + Result.Emission;
    Result.AmbientOcclusion = AmbientOcclusion;
#else
    Result.FinalLighting = Components.Lighting + Emission;
#endif
    return Result;
}

#endif
