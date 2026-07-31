#ifndef BURT_LIGHTING_RESULT_INCLUDED
#define BURT_LIGHTING_RESULT_INCLUDED

// Stable lighting output shared by production composition and shading debug.
// Keep this independent from BRDF-only diagnostic fields so debug display code
// reads the exact channels that are used to build the final production color.
struct BurtLightingResult
{
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
    float3 FinalLighting;
    float AmbientOcclusion;
};

BurtLightingResult BurtCreateLightingResult(
    BurtPBRShadingComponents Components,
    float3 Emission,
    float AmbientOcclusion)
{
    BurtLightingResult Result = (BurtLightingResult)0;
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
    return Result;
}

#endif
