// PBR shading core, direct-light evaluation, composition, and public PBR entry points.
#ifndef BURT_LIGHTING_PBR_CORE_INCLUDED
#define BURT_LIGHTING_PBR_CORE_INCLUDED

struct BurtPBRShadingCoreData

{
    BurtPBRMaterialData MaterialData;

    BurtPBRGeometryData GeometryData;

    BurtSpecularAATerms SpecularAATerms;

float DirectSpecularPerceptualRoughness;

    BurtPBREnergyTerms EnergyTerms;

    float3 ClearCoatNormalWS;

    BurtPBRGeometryData ClearCoatGeometryData;

    BurtSpecularAATerms ClearCoatSpecularAATerms;

    float ClearCoatDirectSpecularPerceptualRoughness;

    float3 ClearCoatDirectSpecularEnergyCompensation;
};

BurtPBRShadingCoreData BurtResetClearCoatTopLayerCoreData(BurtPBRShadingCoreData CoreData)
{
    CoreData.ClearCoatSpecularAATerms = CoreData.SpecularAATerms;
    CoreData.ClearCoatDirectSpecularPerceptualRoughness = CoreData.DirectSpecularPerceptualRoughness;
    CoreData.ClearCoatDirectSpecularEnergyCompensation = CoreData.EnergyTerms.DirectSpecularEnergyCompensation;
    return CoreData;
}

#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingPBRCoreClearCoat.hlsl"
#else
BurtPBRShadingCoreData BurtApplyClearCoatTopLayerCoreData(BurtPBRShadingCoreData CoreData)
{
    return BurtResetClearCoatTopLayerCoreData(CoreData);
}
#endif

// Prepare stage: shared material, geometry, Specular AA, and energy inputs.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData)
{
    BurtPBRShadingCoreData CoreData;

    CoreData.MaterialData = MaterialData;

    CoreData.GeometryData = GeometryData;

    CoreData.SpecularAATerms = BurtEvaluateSpecularAATerms(MaterialData, GeometryData);

    CoreData.DirectSpecularPerceptualRoughness = CoreData.SpecularAATerms.FilteredPerceptualRoughness;

    CoreData.EnergyTerms = BurtPreparePBREnergyTerms(MaterialData, GeometryData, CoreData.DirectSpecularPerceptualRoughness);
    CoreData.ClearCoatNormalWS = GeometryData.NormalWS;
    CoreData.ClearCoatGeometryData = GeometryData;
    CoreData = BurtApplyClearCoatTopLayerCoreData(CoreData);

    return CoreData;
}

// SurfaceData overload used by Forward paths.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ViewDirectionWS)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, ViewDirectionWS);

    return BurtPreparePBRShadingCoreData(MaterialData, GeometryData);
}

BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 ViewDirectionWS)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(SurfaceData);
    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(NormalWS, TangentWS, ViewDirectionWS);
    return BurtPreparePBRShadingCoreData(MaterialData, GeometryData);
}

BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ClearCoatNormalWS, float3 ViewDirectionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);
    CoreData.ClearCoatNormalWS = BurtSafeNormalize(ClearCoatNormalWS);
    CoreData.ClearCoatGeometryData = BurtPreparePBRGeometryData(CoreData.ClearCoatNormalWS, ViewDirectionWS);
    CoreData = BurtApplyClearCoatTopLayerCoreData(CoreData);
    return CoreData;
}

BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 ClearCoatNormalWS, float3 ViewDirectionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, TangentWS, ViewDirectionWS);
    CoreData.ClearCoatNormalWS = BurtSafeNormalize(ClearCoatNormalWS);
    CoreData.ClearCoatGeometryData = BurtPreparePBRGeometryData(CoreData.ClearCoatNormalWS, TangentWS, ViewDirectionWS);
    CoreData = BurtApplyClearCoatTopLayerCoreData(CoreData);
    return CoreData;
}

// GBufferData overload used by Deferred Lighting paths.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtGBufferData GBufferData, float3 ViewDirectionWS)
{
    BurtPBRMaterialData MaterialData = BurtPreparePBRMaterialData(GBufferData);

    BurtPBRGeometryData GeometryData = BurtPreparePBRGeometryData(GBufferData, ViewDirectionWS);

    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(MaterialData, GeometryData);
#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
    CoreData.ClearCoatNormalWS = BurtGetClearCoatNormalWS(GBufferData);
    CoreData.ClearCoatGeometryData = BurtPreparePBRGeometryData(CoreData.ClearCoatNormalWS, GBufferData.TangentWS, ViewDirectionWS);
    CoreData = BurtApplyClearCoatTopLayerCoreData(CoreData);
#endif
    return CoreData;
}

// EncodedGBuffer overload used after sampling MRT payloads.
BurtPBRShadingCoreData BurtPreparePBRShadingCoreData(BurtEncodedGBuffer EncodedGBuffer, float3 ViewDirectionWS)
{
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);

    return BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
}

struct BurtPBRShadingComponents

{
    float3 DiffuseColor;

    float3 F0;

float3 F90;

    float3 DirectDiffuse;

    float3 DirectSpecular;

float3 DirectTransmission;

float3 DirectLighting;

    float3 AdditionalDiffuse;

    float3 AdditionalSpecular;

float3 AdditionalLighting;

float3 IndirectDiffuse;

float3 IndirectSpecular;

float3 IndirectLighting;

    float3 Lighting;

    float PerceptualRoughness;

    float SpecularAARoughness;

    float SpecularAANormalVariance;

    float SpecularAARoughnessDelta;

    float3 SpecularEnergyCompensation;

    float3 IndirectSpecularEnergyCompensation;

    float EnergyPreservation;

    float SpecularOcclusion;

    float DirectBRDFD;

    float DirectBRDFVisibility;

    float3 DirectBRDFFresnel;

float DirectDiffuseLobe;

float3 DirectDiffuseBRDF;

float3 DirectSpecularBRDF;

float2 IndirectSpecularDFG;

    float3 IndirectSpecularEnvBRDF;

    float HairPrimaryLobe;

    float HairSecondaryLobe;

    float HairTransmissionLobe;

    float HairScatter;

    float ClearCoatMask;

    float SubsurfaceProfileIndex;

    float3 SubsurfaceTransmission;

    float3 SubsurfaceDirectTransmission;

    float3 SubsurfaceTransmissionBRDF;

    float SubsurfaceTransmissionLobe;

    float SubsurfaceTransmissionPhase;

    float SubsurfaceTransmissionShadow;

    float SubsurfaceTransmissionThickness;

    float3 SubsurfaceKernelWeight;

    float3 SubsurfaceIndirect;

    float3 SubsurfaceIndirectTransmission;

    float FoliageMask;

    float3 FoliageTransmission;

    float3 FoliageDirectTransmission;

    float3 FoliageTransmissionBRDF;

    float FoliageTransmissionShadow;

    float3 FoliageSpecularBRDF;
};

#if BURT_ENABLE_SUBSURFACE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingPBRCoreSubsurface.hlsl"
#else
void BurtResolveSubsurfaceDeferredPostprocessTransmission(
    BurtPBRMaterialData MaterialData,
    inout float3 ResolvedDirectDiffuse,
    inout float3 ResolvedDirectTransmission,
    inout float3 ResolvedIndirectDiffuse,
    inout float3 ResolvedIndirectTransmission)
{
}

BurtPBRShadingComponents BurtApplySubsurfacePBRShadingComponents(
    BurtPBRShadingComponents Components,
    BurtPBRShadingCoreData CoreData,
    BurtDirectPBRComponents DirectComponents,
    BurtIndirectPBRComponents IndirectComponents)
{
    Components.SubsurfaceProfileIndex = CoreData.MaterialData.SubsurfaceProfileIndex;
    Components.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceDirectTransmission = DirectComponents.Transmission;
    Components.SubsurfaceTransmissionBRDF = DirectComponents.TransmissionBRDF;
    Components.SubsurfaceTransmissionLobe = DirectComponents.TransmissionLobe;
    Components.SubsurfaceTransmissionPhase = DirectComponents.TransmissionPhase;
    Components.SubsurfaceTransmissionShadow = DirectComponents.TransmissionShadow;
    Components.SubsurfaceTransmissionThickness = DirectComponents.TransmissionThickness;
    Components.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceIndirect = IndirectComponents.SubsurfaceIndirect;
    Components.SubsurfaceIndirectTransmission = IndirectComponents.SubsurfaceIndirectTransmission;
    return Components;
}
#endif

#if BURT_ENABLE_FOLIAGE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE))
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingPBRCoreFoliage.hlsl"
#else
BurtPBRShadingComponents BurtApplyFoliagePBRShadingComponents(
    BurtPBRShadingComponents Components,
    BurtPBRShadingCoreData CoreData,
    BurtDirectPBRComponents DirectComponents)
{
    Components.FoliageMask = 0.0f;
    Components.FoliageTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageDirectTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.FoliageTransmissionShadow = 1.0f;
    Components.FoliageSpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    return Components;
}
#endif

// Direct stage shared by main and additional lights.
BurtDirectPBRComponents BurtEvaluatePBRDirectFromCore(BurtPBRShadingCoreData CoreData, BurtLight Light)
{
    BurtDirectPBRComponents Components = BurtEvaluateDirectPBRComponents(
        CoreData.MaterialData,
        CoreData.GeometryData,
        CoreData.EnergyTerms,
        CoreData.DirectSpecularPerceptualRoughness,
        Light.Color,
        Light.DirectionWS,
        Light.ShadowAttenuation,
        Light.TransmissionShadowAttenuation,
        Light.TransmissionThickness);

#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
    Components = BurtApplyClearCoatPBRDirectFromCore(Components, CoreData, Light);
#endif
    return Components;
}

BurtDirectPBRComponents BurtCreateZeroPBRDirectComponents()
{
    BurtDirectPBRComponents Components;
    Components.Diffuse = float3(0.0f, 0.0f, 0.0f);
    Components.Specular = float3(0.0f, 0.0f, 0.0f);
    Components.Transmission = float3(0.0f, 0.0f, 0.0f);
    Components.EnergyPreservation = 1.0f;
    Components.TransmissionBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.TransmissionThroughput = float3(0.0f, 0.0f, 0.0f);
    Components.TransmissionLobe = 0.0f;
    Components.TransmissionPhase = 0.0f;
    Components.TransmissionShadow = 1.0f;
    Components.TransmissionThickness = 0.0f;
    Components.BrdfTerms.NDotL = 0.0f;
    Components.BrdfTerms.NDotV = 0.0f;
    Components.BrdfTerms.NDotH = 0.0f;
    Components.BrdfTerms.VDotH = 0.0f;
    Components.BrdfTerms.PerceptualRoughness = 1.0f;
    Components.BrdfTerms.LinearRoughness = 1.0f;
    Components.BrdfTerms.A2 = 1.0f;
    Components.BrdfTerms.D = 0.0f;
    Components.BrdfTerms.Visibility = 0.0f;
    Components.BrdfTerms.Fresnel = float3(0.0f, 0.0f, 0.0f);
    Components.BrdfTerms.DiffuseLobe = 0.0f;
    Components.BrdfTerms.DiffuseBRDF = float3(0.0f, 0.0f, 0.0f);
    Components.BrdfTerms.SpecularBRDF = float3(0.0f, 0.0f, 0.0f);
    return Components;
}

BurtDirectPBRComponents BurtAddPBRDirectComponents(BurtDirectPBRComponents BaseComponents, BurtDirectPBRComponents AddedComponents)
{
    BaseComponents.Diffuse += AddedComponents.Diffuse;
    BaseComponents.Specular += AddedComponents.Specular;
    BaseComponents.Transmission += AddedComponents.Transmission;
    BaseComponents.TransmissionBRDF += AddedComponents.TransmissionBRDF;
    BaseComponents.TransmissionThroughput = max(BaseComponents.TransmissionThroughput, AddedComponents.TransmissionThroughput);
    BaseComponents.TransmissionLobe += AddedComponents.TransmissionLobe;
    BaseComponents.TransmissionPhase = max(BaseComponents.TransmissionPhase, AddedComponents.TransmissionPhase);
    BaseComponents.TransmissionShadow = min(BaseComponents.TransmissionShadow, AddedComponents.TransmissionShadow);
    BaseComponents.TransmissionThickness = max(BaseComponents.TransmissionThickness, AddedComponents.TransmissionThickness);
    return BaseComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, float3 PositionWS)
{
    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, PositionWS, CoreData.GeometryData.NormalWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluatePBRDirectFromCore(CoreData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, float3 PositionWS, float3 ShadowPositionWS)
{
    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight(LightIndex, PositionWS, CoreData.GeometryData.NormalWS, ShadowPositionWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluatePBRDirectFromCore(CoreData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, float3 PositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroPBRDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
    }

    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, PositionWS, CoreData.GeometryData.NormalWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluatePBRDirectFromCore(CoreData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
#endif
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingFromCore(BurtPBRShadingCoreData CoreData, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroPBRDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ShadowPositionWS);
    }

    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLight((int)StoredLightIndex, PositionWS, CoreData.GeometryData.NormalWS, ShadowPositionWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluatePBRDirectFromCore(CoreData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ShadowPositionWS);
#endif
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(BurtPBRShadingCoreData CoreData, float3 PositionWS)
{
    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();
    if (AdditionalLightCount <= 0)
    {
        return AdditionalDirectComponents;
    }

    [loop]
    for (int LightIndex = 0; LightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LightIndex++)
    {
        if (LightIndex >= AdditionalLightCount)
        {
            break;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLightUnshadowed(LightIndex, PositionWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluatePBRDirectFromCore(CoreData, AdditionalLight));
    }

    return AdditionalDirectComponents;
}

BurtDirectPBRComponents BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(BurtPBRShadingCoreData CoreData, float3 PositionWS, float2 ScreenUV)
{
    if (!BurtHasAdditionalLights())
    {
        return BurtCreateZeroPBRDirectComponents();
    }

#if defined(BURT_USE_TILED_LIGHTING)
    uint2 Range = uint2(0u, 0u);
    uint UseClusterLightList = 0u;
    if (!BurtTryGetAdditionalLightListRange(ScreenUV, PositionWS, Range, UseClusterLightList))
    {
        return BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS);
    }

    BurtDirectPBRComponents AdditionalDirectComponents = BurtCreateZeroPBRDirectComponents();
    int AdditionalLightCount = BurtGetAdditionalLightCount();

    [loop]
    for (uint LocalLightIndex = 0u; LocalLightIndex < BURT_MAX_ADDITIONAL_LIGHTS; LocalLightIndex++)
    {
        if (LocalLightIndex >= Range.y)
        {
            break;
        }

        uint StoredLightIndex = BurtReadAdditionalLightListIndex(Range.x + LocalLightIndex, UseClusterLightList);
        if (StoredLightIndex >= (uint)AdditionalLightCount)
        {
            continue;
        }

        BurtLight AdditionalLight = BurtCreateAdditionalLightUnshadowed((int)StoredLightIndex, PositionWS);
        AdditionalDirectComponents = BurtAddPBRDirectComponents(AdditionalDirectComponents, BurtEvaluatePBRDirectFromCore(CoreData, AdditionalLight));
    }

    return AdditionalDirectComponents;
#else
    return BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS);
#endif
}

BurtDirectPBRComponents BurtEvaluatePBRDirectLightingFromCore(BurtPBRShadingCoreData CoreData, BurtLight MainLight, float3 PositionWS)
{
    BurtDirectPBRComponents DirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    return BurtAddPBRDirectComponents(DirectComponents, BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS));
}

BurtDirectPBRComponents BurtEvaluatePBRDirectLightingFromCore(BurtPBRShadingCoreData CoreData, BurtLight MainLight, float3 PositionWS, float2 ScreenUV)
{
    BurtDirectPBRComponents DirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    return BurtAddPBRDirectComponents(DirectComponents, BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ScreenUV));
}

BurtDirectPBRComponents BurtEvaluatePBRDirectLightingFromCore(BurtPBRShadingCoreData CoreData, BurtLight MainLight, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    BurtDirectPBRComponents DirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    return BurtAddPBRDirectComponents(DirectComponents, BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ShadowPositionWS, ScreenUV));
}

// Indirect stage: evaluates SH Diffuse and reflection-probe/sky Specular.
BurtIndirectPBRComponents BurtEvaluatePBRIndirectFromCore(BurtPBRShadingCoreData CoreData)
{
    BurtIndirectPBRComponents Components = BurtEvaluateIndirectPBRComponents(CoreData.MaterialData, CoreData.GeometryData, CoreData.ClearCoatGeometryData, CoreData.EnergyTerms);
    return Components;
}

BurtIndirectPBRComponents BurtEvaluatePBRIndirectFromCore(BurtPBRShadingCoreData CoreData, BurtLight MainLight)
{
    BurtIndirectPBRComponents Components = BurtEvaluatePBRIndirectFromCore(CoreData);
    return BurtApplySubsurfaceIndirectTransmissionFromLight(Components, CoreData.MaterialData, CoreData.GeometryData, MainLight);
}

// Compose stage used by rendering and debug views.
BurtPBRShadingComponents BurtComposePBRShadingComponents(BurtPBRShadingCoreData CoreData, BurtDirectPBRComponents DirectComponents, BurtIndirectPBRComponents IndirectComponents)
{
    BurtPBRShadingComponents Components;

    Components.PerceptualRoughness = CoreData.MaterialData.PerceptualRoughness;
    Components.DiffuseColor = CoreData.MaterialData.DiffuseColor;
    Components.F0 = CoreData.MaterialData.F0;
    Components.F90 = CoreData.MaterialData.F90;

    float DebugSpecularAARoughness = CoreData.DirectSpecularPerceptualRoughness;
    float DebugSpecularAANormalVariance = CoreData.SpecularAATerms.NormalVariance;
    float DebugSpecularAARoughnessDelta = CoreData.SpecularAATerms.RoughnessDelta;

    float3 DebugDirectSpecularEnergyCompensation = CoreData.EnergyTerms.DirectSpecularEnergyCompensation;
    float3 DebugIndirectSpecularEnergyCompensation = CoreData.EnergyTerms.IndirectSpecularEnergyCompensation;

    Components.EnergyPreservation = CoreData.EnergyTerms.EnergyPreservation;
    float DebugIndirectNoV = CoreData.GeometryData.NDotV;
    float DebugIndirectRoughness = CoreData.MaterialData.PerceptualRoughness;
    float2 DebugIndirectDFG = GetSpecularDFGTerms(DebugIndirectRoughness, DebugIndirectNoV);
    float3 DebugIndirectEnvBRDF = EvalSpecularDFG(CoreData.MaterialData.F0, CoreData.MaterialData.F90, DebugIndirectDFG);
#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
    BurtApplyClearCoatPBRDebugComponents(
        CoreData,
        DebugSpecularAARoughness,
        DebugSpecularAANormalVariance,
        DebugSpecularAARoughnessDelta,
        DebugDirectSpecularEnergyCompensation,
        DebugIndirectSpecularEnergyCompensation,
        DebugIndirectNoV,
        DebugIndirectRoughness,
        DebugIndirectDFG,
        DebugIndirectEnvBRDF);
#endif

    Components.SpecularAARoughness = DebugSpecularAARoughness;
    Components.SpecularAANormalVariance = DebugSpecularAANormalVariance;
    Components.SpecularAARoughnessDelta = DebugSpecularAARoughnessDelta;

    Components.SpecularEnergyCompensation = DebugDirectSpecularEnergyCompensation;
    Components.IndirectSpecularEnergyCompensation = DebugIndirectSpecularEnergyCompensation;
    float3 ResolvedDirectDiffuse = DirectComponents.Diffuse;
    float3 ResolvedDirectTransmission = DirectComponents.Transmission;
    float3 ResolvedIndirectDiffuse = IndirectComponents.Diffuse;
    float3 ResolvedIndirectTransmission = IndirectComponents.SubsurfaceIndirectTransmission;
    BurtResolveSubsurfaceDeferredPostprocessTransmission(
        CoreData.MaterialData,
        ResolvedDirectDiffuse,
        ResolvedDirectTransmission,
        ResolvedIndirectDiffuse,
        ResolvedIndirectTransmission);

    Components.DirectBRDFD = DirectComponents.BrdfTerms.D;
    Components.DirectBRDFVisibility = DirectComponents.BrdfTerms.Visibility;
    Components.DirectBRDFFresnel = DirectComponents.BrdfTerms.Fresnel;
    Components.DirectDiffuseLobe = DirectComponents.BrdfTerms.DiffuseLobe;
    Components.DirectDiffuseBRDF = DirectComponents.BrdfTerms.DiffuseBRDF;
    Components.DirectSpecularBRDF = DirectComponents.BrdfTerms.SpecularBRDF;
    Components.DirectDiffuse = ResolvedDirectDiffuse;
    Components.DirectSpecular = DirectComponents.Specular;
    Components.DirectTransmission = ResolvedDirectTransmission;
    Components.DirectLighting = Components.DirectDiffuse + Components.DirectSpecular + Components.DirectTransmission;
    Components.AdditionalDiffuse = float3(0.0f, 0.0f, 0.0f);
    Components.AdditionalSpecular = float3(0.0f, 0.0f, 0.0f);
    Components.AdditionalLighting = float3(0.0f, 0.0f, 0.0f);

    Components.IndirectDiffuse = ResolvedIndirectDiffuse;
    Components.IndirectSpecular = IndirectComponents.Specular;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular + ResolvedIndirectTransmission;
    Components.Lighting = Components.DirectLighting + Components.IndirectLighting;
    Components.SpecularOcclusion = GetIndirectSpecularOcclusion(DebugIndirectNoV, CoreData.MaterialData.Occlusion, DebugIndirectRoughness);
    Components.IndirectSpecularDFG = DebugIndirectDFG;
    Components.IndirectSpecularEnvBRDF = DebugIndirectEnvBRDF;
    Components.HairPrimaryLobe = 0.0f;
    Components.HairSecondaryLobe = 0.0f;
    Components.HairTransmissionLobe = 0.0f;
    Components.HairScatter = 0.0f;
    Components.ClearCoatMask = CoreData.MaterialData.ClearCoatMask;
    Components = BurtApplySubsurfacePBRShadingComponents(Components, CoreData, DirectComponents, IndirectComponents);
    Components = BurtApplyFoliagePBRShadingComponents(Components, CoreData, DirectComponents);

return Components;
}

BurtPBRShadingComponents BurtComposePBRShadingComponentsWithAdditional(
    BurtPBRShadingCoreData CoreData,
    BurtDirectPBRComponents DirectComponents,
    BurtIndirectPBRComponents IndirectComponents,
    BurtDirectPBRComponents AdditionalDirectComponents)
{
    BurtPBRShadingComponents Components = BurtComposePBRShadingComponents(CoreData, DirectComponents, IndirectComponents);
    Components.AdditionalDiffuse = AdditionalDirectComponents.Diffuse;
    Components.AdditionalSpecular = AdditionalDirectComponents.Specular;
    Components.AdditionalLighting = Components.AdditionalDiffuse + Components.AdditionalSpecular;
    return Components;
}

// Evaluates full PBR shading from prepared material and geometry data.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtLight MainLight)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(MaterialData, GeometryData);

    BurtDirectPBRComponents DirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);

    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);

    return BurtComposePBRShadingComponents(CoreData, DirectComponents, IndirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtLight MainLight, float3 PositionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(MaterialData, GeometryData);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData, BurtLight MainLight, float3 PositionWS, float2 ScreenUV)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(MaterialData, GeometryData);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ScreenUV);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

// Evaluates full PBR shading from SurfaceData.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);

    BurtDirectPBRComponents DirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);

    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);

    return BurtComposePBRShadingComponents(CoreData, DirectComponents, IndirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    float3 ProbeVolumeDiffuse;
    if (BurtTryEvaluateGIProbeVolumeIndirectDiffuse(CoreData.MaterialData, PositionWS, NormalWS, ViewDirectionWS, CoreData.EnergyTerms.EnergyPreservation, ProbeVolumeDiffuse))
    {
        IndirectComponents.Diffuse = ProbeVolumeDiffuse;
    }
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float4 TangentWS, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, TangentWS, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    float3 ProbeVolumeDiffuse;
    if (BurtTryEvaluateGIProbeVolumeIndirectDiffuse(CoreData.MaterialData, PositionWS, NormalWS, ViewDirectionWS, CoreData.EnergyTerms.EnergyPreservation, ProbeVolumeDiffuse))
    {
        IndirectComponents.Diffuse = ProbeVolumeDiffuse;
    }
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ClearCoatNormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ClearCoatNormalWS, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    float3 ProbeVolumeDiffuse;
    if (BurtTryEvaluateGIProbeVolumeIndirectDiffuse(CoreData.MaterialData, PositionWS, NormalWS, ViewDirectionWS, CoreData.EnergyTerms.EnergyPreservation, ProbeVolumeDiffuse))
    {
        IndirectComponents.Diffuse = ProbeVolumeDiffuse;
    }
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float4 TangentWS, float3 ClearCoatNormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, TangentWS, ClearCoatNormalWS, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    float3 ProbeVolumeDiffuse;
    if (BurtTryEvaluateGIProbeVolumeIndirectDiffuse(CoreData.MaterialData, PositionWS, NormalWS, ViewDirectionWS, CoreData.EnergyTerms.EnergyPreservation, ProbeVolumeDiffuse))
    {
        IndirectComponents.Diffuse = ProbeVolumeDiffuse;
    }
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(SurfaceData, NormalWS, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ScreenUV);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

// Evaluates full PBR shading from decoded GBuffer data.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);

    BurtDirectPBRComponents DirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);

    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);

    return BurtComposePBRShadingComponents(CoreData, DirectComponents, IndirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ScreenUV);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(CoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(CoreData, PositionWS, ShadowPositionWS, ScreenUV);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(CoreData, MainLight);
    return BurtComposePBRShadingComponentsWithAdditional(CoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
}

// Evaluates full PBR shading from encoded GBuffer MRT payloads.
BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtEncodedGBuffer EncodedGBuffer, BurtLight MainLight, float3 ViewDirectionWS)
{
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);

    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtEncodedGBuffer EncodedGBuffer, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS)
{
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS);
}

BurtPBRShadingComponents BurtEvaluatePBRShadingComponentsFromGBuffer(BurtEncodedGBuffer EncodedGBuffer, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
}


#endif
