#ifndef BURT_DEFERRED_LIGHTING_DEBUG_INCLUDED
#define BURT_DEFERRED_LIGHTING_DEBUG_INCLUDED
#if BURT_ENABLE_SHADING_DEBUG
#if !defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_LIGHTING) && !defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_BRDF) && !defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_SHADOW) && !defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_TRANSMISSION)
#define BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL 1
#endif

#ifndef BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL
#define BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL 0
#endif

#if defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_LIGHTING)
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_LIGHTING 1
#else
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_LIGHTING BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL
#endif

#if defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_BRDF)
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_BRDF 1
#else
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_BRDF BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL
#endif

#if defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_SHADOW)
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_SHADOW 1
#else
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_SHADOW BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL
#endif

#if defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_TRANSMISSION)
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_TRANSMISSION 1
#else
#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_TRANSMISSION BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL
#endif

#define BURT_DEFERRED_LIGHTING_DEBUG_FILL_GBUFFER BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_FULL

#if !BURT_DEFERRED_LIGHTING_DEBUG_FILL_SHADOW
#define BURT_SHADING_DEBUG_INCLUDE_SHADOW 0
#define BURT_SHADING_DEBUG_INCLUDE_ADDITIONAL_LIGHTS 0
#endif

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"
float BurtGetDeferredLightingDebugMaterialChannel(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return BurtGetHairScatter(GBufferData);
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return 0.0f;
#elif defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    return 1.0f;
#else
    return saturate(GBufferData.Metallic);
#endif
}

void BurtApplyDeferredLightingDebugBaseline(
    BurtGBufferData ShadingGBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV,
    inout BurtPBRShadingComponents DebugComponents)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    ShadingGBufferData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;

    BurtPBRShadingCoreData DebugCoreData = BurtPreparePBRShadingCoreData(ShadingGBufferData, ViewDirectionWS);
    BurtDirectPBRComponents MainDirectComponents = BurtEvaluatePBRDirectFromCore(DebugCoreData, MainLight);
    BurtDirectPBRComponents AdditionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(DebugCoreData, PositionWS, ShadowPositionWS, ScreenUV);
    BurtDirectPBRComponents DirectComponents = BurtAddPBRDirectComponents(MainDirectComponents, AdditionalDirectComponents);
    BurtIndirectPBRComponents IndirectComponents = BurtEvaluatePBRIndirectFromCore(DebugCoreData);
    DebugComponents = BurtComposePBRShadingComponentsWithAdditional(DebugCoreData, DirectComponents, IndirectComponents, AdditionalDirectComponents);
#endif
}

float3 BurtEvaluateDeferredLightingAdditionalUnshadowedDebug(
    BurtGBufferData GBufferData,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float2 ScreenUV)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    BurtPBRGeometryData HairGeometryData = BurtPrepareHairGeometryData(GBufferData, ViewDirectionWS);
    BurtHairDirectComponents HairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(GBufferData, HairGeometryData, PositionWS, ScreenUV);
    return HairAdditional.Diffuse + HairAdditional.Specular;
#else
    BurtPBRShadingCoreData CoreData = BurtPreparePBRShadingCoreData(GBufferData, ViewDirectionWS);
    BurtDirectPBRComponents Additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(CoreData, PositionWS, ScreenUV);
    return Additional.Diffuse + Additional.Specular;
#endif
}
void BurtApplyDeferredLightingDetailDebugOverride(inout BurtGBufferData ShadingGBufferData)
{
    if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        ShadingGBufferData.BaseColor = float3(0.18f, 0.18f, 0.18f);
    }
}
float4 BurtEvaluateDeferredLightingDebugOutput(
    BurtGBufferData GBufferData,
    BurtGBufferData ShadingGBufferData,
    BurtLight MainLight,
    BurtPBRShadingComponents PBRComponents,
    float3 FinalColor,
    float3 FinalPreExposedColor,
    float OutputAlpha,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float3 ShadowNormalWS,
    int PerObjectShadowObjectIndex,
    float2 ScreenUV)
{
    if (!BurtIsShadingDebugEnabled())
    {
        return float4(FinalPreExposedColor, OutputAlpha);
    }

    BurtSurfaceData DebugSurfaceData = BurtCreateSurfaceData(float4(GBufferData.BaseColor, 1.0f));
    DebugSurfaceData.BaseColor = float4(GBufferData.BaseColor, 1.0f);
    DebugSurfaceData.Alpha = 1.0f;
    DebugSurfaceData.Reflectance = GBufferData.Reflectance;
    DebugSurfaceData.Smoothness = GBufferData.Smoothness;
    DebugSurfaceData.Metallic = BurtGetDeferredLightingDebugMaterialChannel(GBufferData);
    DebugSurfaceData.Anisotropy = GBufferData.Anisotropy;
    DebugSurfaceData.Height = 0.5f;
#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
    DebugSurfaceData.ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    DebugSurfaceData.ClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
#else
    DebugSurfaceData.ClearCoatMask = 0.0f;
    DebugSurfaceData.ClearCoatRoughness = 0.2f;
#endif
#if BURT_ENABLE_SUBSURFACE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE))
    DebugSurfaceData.SubsurfaceThickness = BurtGetSubsurfaceThickness(GBufferData);
    DebugSurfaceData.SubsurfacePower = BurtGetSubsurfacePower(GBufferData);
    DebugSurfaceData.SubsurfaceDistortion = BurtGetSubsurfaceDistortion(GBufferData);
    DebugSurfaceData.SubsurfaceAmbient = BurtGetSubsurfaceAmbient(GBufferData);
    DebugSurfaceData.SubsurfaceScatteringMode = BurtGetSubsurfaceScatteringMode(GBufferData);
    DebugSurfaceData.Subsurface3SCurvature = saturate(GBufferData.Subsurface3SCurvature);
    DebugSurfaceData.SubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(GBufferData);
#else
    DebugSurfaceData.SubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    DebugSurfaceData.SubsurfacePower = BURT_SUBSURFACE_DEFAULT_POWER;
    DebugSurfaceData.SubsurfaceDistortion = BURT_SUBSURFACE_DEFAULT_DISTORTION;
    DebugSurfaceData.SubsurfaceAmbient = BURT_SUBSURFACE_DEFAULT_AMBIENT;
    DebugSurfaceData.SubsurfaceScatteringMode = BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
    DebugSurfaceData.Subsurface3SCurvature = 1.0f - BURT_SUBSURFACE_DEFAULT_THICKNESS;
    DebugSurfaceData.SubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#endif
#if BURT_ENABLE_FABRIC_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_FABRIC))
    DebugSurfaceData.FabricIsSilk = BurtGetFabricIsSilk(GBufferData);
    DebugSurfaceData.FabricFuzzWeight = BurtGetFabricFuzzWeight(GBufferData);
    DebugSurfaceData.FabricFuzzRoughness = BurtGetFabricFuzzRoughness(GBufferData);
    DebugSurfaceData.FabricFuzzColor = BurtGetFabricFuzzColor(GBufferData);
#else
    DebugSurfaceData.FabricIsSilk = 0.0f;
    DebugSurfaceData.FabricFuzzWeight = 0.0f;
    DebugSurfaceData.FabricFuzzRoughness = 0.75f;
    DebugSurfaceData.FabricFuzzColor = float3(1.0f, 1.0f, 1.0f);
#endif
    DebugSurfaceData.Occlusion = GBufferData.Occlusion;
    DebugSurfaceData.ShadingModelID = GBufferData.ShadingModelID;

#if BURT_DEFERRED_LIGHTING_DEBUG_FILL_GBUFFER
    BurtPBRMaterialData DebugGBufferMaterialData = BurtPreparePBRMaterialData(GBufferData);
#endif
    BurtPBRShadingComponents DebugLightingComponents = PBRComponents;
#if BURT_DEFERRED_LIGHTING_DEBUG_FILL_LIGHTING
    BurtApplyDeferredLightingDebugBaseline(
        ShadingGBufferData,
        MainLight,
        ViewDirectionWS,
        PositionWS,
        ShadowPositionWS,
        ScreenUV,
        DebugLightingComponents);
#endif

    float3 DeferredAONormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    BurtShadingDebugData DebugData = BurtCreateDefaultShadingDebugData(DeferredAONormalWS);
    DebugData.NormalWS = DeferredAONormalWS;
#if BURT_DEFERRED_LIGHTING_DEBUG_FILL_LIGHTING
    DebugData.DetailLightingColor = DebugLightingComponents.Lighting;
    DebugData.DirectDiffuseColor = DebugLightingComponents.DirectDiffuse;
    DebugData.DirectSpecularColor = DebugLightingComponents.DirectSpecular;
    DebugData.AdditionalDiffuseColor = DebugLightingComponents.AdditionalDiffuse;
    DebugData.AdditionalSpecularColor = DebugLightingComponents.AdditionalSpecular;
    DebugData.AdditionalUnshadowedColor = BurtNeedsAdditionalLightingUnshadowedShadingDebug()
        ? BurtEvaluateDeferredLightingAdditionalUnshadowedDebug(ShadingGBufferData, ViewDirectionWS, PositionWS, ScreenUV)
        : float3(0.0f, 0.0f, 0.0f);
    DebugData.IndirectDiffuseColor = DebugLightingComponents.IndirectDiffuse;
    DebugData.IndirectSpecularColor = DebugLightingComponents.IndirectSpecular;
    DebugData.AmbientOcclusion = ShadingGBufferData.Occlusion;
    DebugData.EmissionColor = GBufferData.Emission;
    DebugData.FinalLightingColor = FinalColor;
#endif
#if BURT_DEFERRED_LIGHTING_DEBUG_FILL_SHADOW
    DebugData.ShadowAttenuation = BurtSampleMainLightShadowWithoutPerObject(ShadowPositionWS, ShadowNormalWS);
    DebugData.AdditionalShadowAttenuation = BurtNeedsAdditionalShadowAttenuationShadingDebug()
        ? BurtEvaluateAdditionalShadowAttenuationDebug(ShadowPositionWS, DeferredAONormalWS, ScreenUV)
        : 1.0f;

    if (BurtNeedsAdditionalShadowProjectionShadingDebug())
    {
        BurtFillAdditionalLightShadowProjectionDebugData(
            ShadowPositionWS,
            DeferredAONormalWS,
            ScreenUV,
            DebugData.AdditionalShadowFaceColor,
            DebugData.AdditionalShadowUVColor,
            DebugData.AdditionalShadowDepthColor,
            DebugData.AdditionalShadowDepthDeltaColor);
    }

    BurtFillMainLightShadowShadingDebugData(
        ShadowPositionWS,
        DebugData.NormalWS,
        DebugData.ShadowCascadeColor,
        DebugData.ShadowCascadeBlend,
        DebugData.ShadowDistanceFade,
        DebugData.ShadowPCSSRadius,
        DebugData.ShadowReceiverDepthDelta,
        DebugData.MainLightShadowReceiverDepth,
        DebugData.MainLightShadowRawDepth,
        DebugData.MainLightShadowCompare,
        DebugData.MainLightShadowProjectionValidity,
        DebugData.ShadowPCSSBlockerFraction);

    BurtFillPerObjectShadowShadingDebugData(
        ShadowPositionWS,
        ShadowNormalWS,
        PerObjectShadowObjectIndex,
        DebugData.PerObjectShadowObjectIndexColor,
        DebugData.PerObjectShadowSliceColor,
        DebugData.PerObjectShadowUVColor,
        DebugData.PerObjectShadowDepthColor,
        DebugData.PerObjectShadowCompareColor,
        DebugData.PerObjectShadowTransmissionDepthColor,
        DebugData.PerObjectShadowTransmissionThicknessColor);
#endif
#if BURT_DEFERRED_LIGHTING_DEBUG_FILL_BRDF
    DebugData.Reflectance = GBufferData.Reflectance;
    DebugData.PerceptualRoughness = DebugLightingComponents.PerceptualRoughness;
    DebugData.SpecularAARoughness = DebugLightingComponents.SpecularAARoughness;
    DebugData.SpecularEnergyCompensation = DebugLightingComponents.SpecularEnergyCompensation;
    DebugData.IndirectSpecularEnergyCompensation = DebugLightingComponents.IndirectSpecularEnergyCompensation;
    DebugData.EnergyPreservation = DebugLightingComponents.EnergyPreservation;
    DebugData.SpecularOcclusion = DebugLightingComponents.SpecularOcclusion;
    DebugData.DiffuseColor = DebugLightingComponents.DiffuseColor;
    DebugData.DirectBRDFD = DebugLightingComponents.DirectBRDFD;
    DebugData.DirectBRDFVisibility = DebugLightingComponents.DirectBRDFVisibility;
    DebugData.DirectBRDFFresnel = DebugLightingComponents.DirectBRDFFresnel;
    DebugData.DirectDiffuseLobe = DebugLightingComponents.DirectDiffuseLobe;
    DebugData.DirectDiffuseBRDF = DebugLightingComponents.DirectDiffuseBRDF;
    DebugData.DirectSpecularBRDF = DebugLightingComponents.DirectSpecularBRDF;
    DebugData.SpecularAANormalVariance = DebugLightingComponents.SpecularAANormalVariance;
    DebugData.SpecularAARoughnessDelta = DebugLightingComponents.SpecularAARoughnessDelta;
    DebugData.IndirectSpecularDFG = DebugLightingComponents.IndirectSpecularDFG;
    DebugData.IndirectSpecularEnvBRDF = DebugLightingComponents.IndirectSpecularEnvBRDF;
#endif
#if BURT_DEFERRED_LIGHTING_DEBUG_FILL_TRANSMISSION
    DebugData.SubsurfaceProfileIndex = PBRComponents.SubsurfaceProfileIndex;
    DebugData.SubsurfaceTransmission = PBRComponents.SubsurfaceTransmission;
    DebugData.SubsurfaceDirectTransmission = PBRComponents.SubsurfaceDirectTransmission;
    DebugData.SubsurfaceTransmissionBRDF = PBRComponents.SubsurfaceTransmissionBRDF;
    DebugData.SubsurfaceTransmissionShadow = PBRComponents.SubsurfaceTransmissionShadow;
    DebugData.SubsurfaceTransmissionPhase = PBRComponents.SubsurfaceTransmissionPhase;
    DebugData.SubsurfaceTransmissionThickness = PBRComponents.SubsurfaceTransmissionThickness;
    DebugData.SubsurfaceKernelWeight = PBRComponents.SubsurfaceKernelWeight;
    DebugData.SubsurfaceIndirect = PBRComponents.SubsurfaceIndirect;
    DebugData.FoliageMask = PBRComponents.FoliageMask;
    DebugData.FoliageTransmission = PBRComponents.FoliageTransmission;
    DebugData.FoliageDirectTransmission = PBRComponents.FoliageDirectTransmission;
    DebugData.FoliageTransmissionBRDF = PBRComponents.FoliageTransmissionBRDF;
    DebugData.FoliageTransmissionShadow = PBRComponents.FoliageTransmissionShadow;
    DebugData.FoliageSpecularBRDF = PBRComponents.FoliageSpecularBRDF;
    DebugData.HairPrimaryLobe = PBRComponents.HairPrimaryLobe;
    DebugData.HairSecondaryLobe = PBRComponents.HairSecondaryLobe;
    DebugData.HairTransmissionLobe = PBRComponents.HairTransmissionLobe;
    DebugData.HairScatter = PBRComponents.HairScatter;
#endif
#if BURT_DEFERRED_LIGHTING_DEBUG_FILL_GBUFFER
    DebugData.GBufferBaseColor = GBufferData.BaseColor;
    if (BurtIsSubsurfaceShadingModel(GBufferData.ShadingModelID)
        && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PRESKIN_POSITION))
    {
        DebugData.PreSkinPositionDebugColor = GBufferData.BaseColor;
        DebugData.PreSkinPositionAvailable = 1.0f;
    }
    DebugData.GBufferNormalWS = BurtGetGBufferDirectionWS(GBufferData);
    DebugData.GBufferMetallic = BurtGetDeferredLightingDebugMaterialChannel(GBufferData);
#if BURT_ENABLE_CLEAR_COAT_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT))
    DebugData.GBufferClearCoatMask = BurtGetClearCoatMask(GBufferData);
    DebugData.GBufferClearCoatNormalWS = BurtGetClearCoatNormalWS(GBufferData);
    DebugData.GBufferClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
#else
    DebugData.GBufferClearCoatMask = 0.0f;
    DebugData.GBufferClearCoatNormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    DebugData.GBufferClearCoatRoughness = 0.2f;
#endif
#if BURT_ENABLE_SUBSURFACE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE))
    DebugData.GBufferSubsurfaceStrength = BurtGetSubsurfaceStrength(GBufferData);
    DebugData.GBufferSubsurfaceThickness = BurtGetSubsurfaceThickness(GBufferData);
    DebugData.GBufferSubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(GBufferData);
#else
    DebugData.GBufferSubsurfaceStrength = 0.0f;
    DebugData.GBufferSubsurfaceThickness = BURT_SUBSURFACE_DEFAULT_THICKNESS;
    DebugData.GBufferSubsurfaceProfileIndex = BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#endif
    DebugData.GBufferAnisotropy = GBufferData.Anisotropy;
    DebugData.GBufferTangentWS = GBufferData.TangentWS;
    DebugData.GBufferSmoothness = GBufferData.Smoothness;
    DebugData.GBufferOcclusion = GBufferData.Occlusion;
    DebugData.GBufferReflectance = GBufferData.Reflectance;
    DebugData.GBufferRoughness = DebugGBufferMaterialData.PerceptualRoughness;
    DebugData.GBufferDiffuseColor = DebugGBufferMaterialData.DiffuseColor;
#endif

    float3 DebugColor;
    if (BurtTryEvaluateMaterialShadingDebug(DebugSurfaceData, DebugData, DebugColor))
    {
        return float4(DebugColor, 1.0f);
    }
    return float4(FinalPreExposedColor, OutputAlpha);
}
#endif
#endif
