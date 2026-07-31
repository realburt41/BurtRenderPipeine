// PBR indirect-light composition and optional shading-model indirect hooks.
#ifndef BURT_LIGHTING_INDIRECT_PBR_INCLUDED
#define BURT_LIGHTING_INDIRECT_PBR_INCLUDED

float3 BurtEvaluateIndirectDiffusePBR(BurtPBRMaterialData MaterialData, float3 NormalWS, float EnergyPreservation)
{
    float3 DiffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(NormalWS);

    return MaterialData.DiffuseColor * DiffuseIrradiance * BurtGTAOMultiBounce(MaterialData.Occlusion, MaterialData.BaseColor) * saturate(EnergyPreservation);
}

float3 BurtEvaluateIndirectDiffusePBR(BurtSurfaceData SurfaceData, float3 NormalWS, float EnergyPreservation)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    return BurtEvaluateIndirectDiffusePBR(MaterialData, NormalWS, EnergyPreservation);
}

float3 GetIndirectSpecularEnergyCompensation(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtPBREnergyTerms EnergyTerms)
{
return EnergyTerms.IndirectSpecularEnergyCompensation;
}

float3 GetIndirectSpecularEnergyCompensation(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

float DirectSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(MaterialData, GeometryData);

    BurtPBREnergyTerms EnergyTerms = BurtPreparePBREnergyTerms(MaterialData, GeometryData, DirectSpecularPerceptualRoughness);

return GetIndirectSpecularEnergyCompensation(MaterialData, GeometryData, EnergyTerms);
}

float3 BurtEvaluateIndirectSpecularPBR(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, float3 IndirectSpecularEnergyCompensation)
{
float Roughness = MaterialData.PerceptualRoughness;

float3 ReflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(GeometryData, MaterialData.Anisotropy, Roughness);

    float3 SpecularRadiance = SampleIndirectSpecularRadiance(ReflectionDirectionWS, Roughness);

    float2 Dfg = GetSpecularDFGTerms(Roughness, GeometryData.NDotV);

float3 EnvBRDF = EvalSpecularDFG(MaterialData.F0, MaterialData.F90, Dfg);


    float SpecularOcclusion = GetIndirectSpecularOcclusion(GeometryData.NDotV, MaterialData.Occlusion, Roughness);

    return SpecularRadiance * EnvBRDF * IndirectSpecularEnergyCompensation * SpecularOcclusion;
}

float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS, float3 IndirectSpecularEnergyCompensation)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

    return BurtEvaluateIndirectSpecularPBR(MaterialData, GeometryData, IndirectSpecularEnergyCompensation);
}

float3 BurtEvaluateIndirectSpecularPBR(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS)
{
float3 IndirectSpecularEnergyCompensation = GetIndirectSpecularEnergyCompensation(SurfaceData, NormalWS, ViewDirectionWS);

    return BurtEvaluateIndirectSpecularPBR(SurfaceData, NormalWS, ViewDirectionWS, IndirectSpecularEnergyCompensation);
}

struct BurtIndirectPBRComponents

{
float3 Diffuse;

float3 Specular;

    float3 SpecularEnergyCompensation;

    float3 SubsurfaceIndirect;

    float3 SubsurfaceIndirectTransmission;
};

BurtIndirectPBRComponents BurtCreateZeroPBRIndirectComponents()
{
    BurtIndirectPBRComponents Components = (BurtIndirectPBRComponents)0;
    Components.SpecularEnergyCompensation = float3(1.0f, 1.0f, 1.0f);
    return Components;
}

#if BURT_ENABLE_SUBSURFACE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingSubsurface.hlsl"
#else
BurtIndirectPBRComponents BurtApplySubsurfaceIndirectTransmissionFromLight(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    BurtLight MainLight)
{
    return Components;
}

BurtIndirectPBRComponents BurtApplySubsurfaceIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData)
{
    return Components;
}
#endif

#if BURT_ENABLE_FABRIC_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_FABRIC))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingFabric.hlsl"
#else
BurtIndirectPBRComponents BurtApplyFabricIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData)
{
    return Components;
}
#endif

BurtPBRMaterialData BurtCreateClearCoatMaterialData(BurtPBRMaterialData BaseMaterialData);

#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingClearCoat.hlsl"
#else
BurtIndirectPBRComponents BurtApplyClearCoatIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    BurtPBRGeometryData ClearCoatGeometryData)
{
    return Components;
}
#endif


// Evaluates split indirect PBR lighting from prepared material/geometry data.
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtPBRGeometryData ClearCoatGeometryData, BurtPBREnergyTerms EnergyTerms)
{
    BurtIndirectPBRComponents Components;
    Components.Diffuse = BurtEvaluateIndirectDiffusePBR(MaterialData, GeometryData.NormalWS, EnergyTerms.EnergyPreservation);
    Components.SpecularEnergyCompensation = GetIndirectSpecularEnergyCompensation(MaterialData, GeometryData, EnergyTerms);
    Components.Specular = BurtEvaluateIndirectSpecularPBR(MaterialData, GeometryData, Components.SpecularEnergyCompensation);
    Components.SubsurfaceIndirect = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirectTransmission = float3(0.0f, 0.0f, 0.0f);

    Components = BurtApplyFabricIndirectPBRComponents(Components, MaterialData, GeometryData);
    Components = BurtApplySubsurfaceIndirectPBRComponents(Components, MaterialData, GeometryData);
    Components = BurtApplyClearCoatIndirectPBRComponents(Components, MaterialData, GeometryData, ClearCoatGeometryData);

    return Components;
}

BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtPBREnergyTerms EnergyTerms)
{
    return BurtEvaluateIndirectPBRComponents(MaterialData, GeometryData, GeometryData, EnergyTerms);
}


// Compatibility overload that prepares material/geometry/energy terms internally.
BurtIndirectPBRComponents BurtEvaluateIndirectPBRComponents(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS, float EnergyPreservation)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

    float DirectSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(MaterialData, GeometryData);
    BurtPBREnergyTerms EnergyTerms = BurtPreparePBREnergyTerms(MaterialData, GeometryData, DirectSpecularPerceptualRoughness);
    EnergyTerms.EnergyPreservation = EnergyPreservation;

    return BurtEvaluateIndirectPBRComponents(MaterialData, GeometryData, EnergyTerms);
}

float3 BurtEvaluateIndirectPBR(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

    float DirectSpecularPerceptualRoughness = GetDirectSpecularPerceptualRoughness(MaterialData, GeometryData);

    BurtPBREnergyTerms EnergyTerms = BurtPreparePBREnergyTerms(MaterialData, GeometryData, DirectSpecularPerceptualRoughness);

    BurtIndirectPBRComponents Components = BurtEvaluateIndirectPBRComponents(MaterialData, GeometryData, EnergyTerms);

    return Components.Diffuse + Components.Specular;
}

#endif
