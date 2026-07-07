#ifndef BURT_DEFERRED_LIGHTING_PASS_INCLUDED
#define BURT_DEFERRED_LIGHTING_PASS_INCLUDED

#define BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL 1
#define BURT_USE_ADDITIONAL_LIGHT_BUFFER 1
#define BURT_USE_TILED_LIGHTING 1
#define BURT_ENABLE_SHADING_DEBUG 1

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"

Texture2D<float> _BurtScreenSpaceAmbientOcclusionTexture;
Texture2D<float> _BurtScreenSpaceShadowTexture;
Texture2D<float4> _BurtGIDiffuseIndirectTexture;
Texture2D<float4> _BurtGIBackfaceDiffuseIndirectTexture;
Texture2D<float4> _BurtGIRoughSpecularIndirectTexture;
float _BurtScreenSpaceAmbientOcclusionEnabled;
float _BurtScreenSpaceShadowEnabled;
float _BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled;
float4 _BurtGIApplyIndirectParams; // x=diffuse enabled, y=intensity, z=backface enabled, w=rough-specular enabled.

float BurtSampleDeferredScreenSpaceAmbientOcclusion(float2 ScreenUV)
{
    if (_BurtScreenSpaceAmbientOcclusionEnabled < 0.5f)
    {
        return 1.0f;
    }

    int2 TextureSize = max((int2)_BurtDeferredScreenSize.xy, int2(1, 1));
    int2 PixelCoord = clamp((int2)floor(ScreenUV * (float2)TextureSize), int2(0, 0), TextureSize - 1);
    float AO = _BurtScreenSpaceAmbientOcclusionTexture.Load(int3(PixelCoord, 0));
    return saturate(AO);
}

float BurtResolveDeferredMaterialScreenSpaceAmbientOcclusion(float2 ScreenUV, BurtGBufferData GBufferData)
{
    float ScreenSpaceAmbientOcclusion = BurtSampleDeferredScreenSpaceAmbientOcclusion(ScreenUV);
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return saturate(lerp(1.0f, ScreenSpaceAmbientOcclusion, max(BurtGetFoliageScreenSpaceShadowIntensity(GBufferData), 0.0f)));
#else
    return ScreenSpaceAmbientOcclusion;
#endif
}

float BurtSampleDeferredScreenSpaceShadow(float2 ScreenUV)
{
    if (_BurtScreenSpaceShadowEnabled < 0.5f)
    {
        return 1.0f;
    }

    return saturate(BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtScreenSpaceShadowTexture, ScreenUV, 0.0f));
}

float BurtResolveDeferredMaterialScreenSpaceShadow(float2 ScreenUV, BurtGBufferData GBufferData)
{
    float ScreenSpaceShadow = BurtSampleDeferredScreenSpaceShadow(ScreenUV);
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return saturate(lerp(1.0f, ScreenSpaceShadow, max(BurtGetFoliageScreenSpaceShadowIntensity(GBufferData), 0.0f)));
#else
    return ScreenSpaceShadow;
#endif
}

float BurtResolveDeferredMaterialFoliageMicroShadow(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return saturate(GBufferData.Occlusion);
#else
    return 1.0f;
#endif
}

float3 BurtSampleDeferredGIDiffuseIndirect(float2 ScreenUV)
{
    if (_BurtGIApplyIndirectParams.x < 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIDiffuseIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float3 BurtSampleDeferredGIBackfaceDiffuseIndirect(float2 ScreenUV)
{
    if (_BurtGIApplyIndirectParams.z < 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIBackfaceDiffuseIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float3 BurtSampleDeferredGIRoughSpecularIndirect(float2 ScreenUV)
{
    if (_BurtGIApplyIndirectParams.w < 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIRoughSpecularIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float BurtDeferredGIBackfaceDiffuseBlend(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    return 0.0f;
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return 0.45f;
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return 0.0f;
#else
    return 0.0f;
#endif
}

void BurtApplyDeferredGIIndirect(float2 ScreenUV, BurtGBufferData GBufferData, inout BurtPBRShadingComponents Components)
{
    float3 DiffuseIndirect = BurtSampleDeferredGIDiffuseIndirect(ScreenUV);
    float3 BackfaceDiffuseIndirect = BurtSampleDeferredGIBackfaceDiffuseIndirect(ScreenUV);
    float3 RoughSpecularIndirect = BurtSampleDeferredGIRoughSpecularIndirect(ScreenUV);
    DiffuseIndirect = lerp(DiffuseIndirect, BackfaceDiffuseIndirect, BurtDeferredGIBackfaceDiffuseBlend(GBufferData));

    float RoughSpecularBlend = smoothstep(0.35f, 0.92f, saturate(GBufferData.PerceptualRoughness));
    float3 SubsurfaceIndirectTransmission = max(Components.SubsurfaceIndirectTransmission, float3(0.0f, 0.0f, 0.0f));
    float3 SubsurfaceIndirectTransmissionForLighting = SubsurfaceIndirectTransmission;
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (BurtGetSubsurfaceStrength(GBufferData) > 0.0001f &&
        !BurtIsSubsurface3SPreIntegratedMode(BurtGetSubsurfaceScatteringMode(GBufferData)))
    {
        SubsurfaceIndirectTransmissionForLighting = float3(0.0f, 0.0f, 0.0f);
    }
#endif
    Components.SubsurfaceIndirectTransmission = SubsurfaceIndirectTransmission;
    Components.IndirectDiffuse += DiffuseIndirect;
    Components.IndirectSpecular += RoughSpecularIndirect * RoughSpecularBlend;
    Components.SubsurfaceIndirect = Components.IndirectDiffuse;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular + SubsurfaceIndirectTransmissionForLighting;
    Components.Lighting = Components.DirectLighting + Components.IndirectLighting;
}

float BurtEvaluateDeferredOutputAlpha(BurtPBRShadingComponents Components)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (_BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled < 0.5f)
    {
        return 1.0f;
    }

    float3 DiffuseLighting =
        Components.DirectDiffuse +
        Components.IndirectDiffuse;
    return dot(BurtApplyPreExposure(DiffuseLighting), float3(0.3f, 0.59f, 0.11f));
#else
    return 1.0f;
#endif
}

// Fullscreen triangle vertex entry, matching XRender's SlabDeferredLightingPass::Vert.
Varyings Vert(Attributes Input)
{
    Varyings Output;
    Output.PositionCS = BurtGetFullScreenTriangleVertexPosition(Input.VertexID);
    Output.ScreenUV = BurtGetFullScreenTriangleTexCoord(Input.VertexID);
    return Output;
}

#define BURT_EVALUATE_DEFERRED_LIGHTING_SHADING_MODEL_COMPONENTS(ShadingModelName, GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV) \
    BURT_TOKEN_PASTE2(BurtEvaluateDeferredLightingShadingModelComponents_, ShadingModelName)(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV)

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_DefaultLit(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
#if BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
    }
#endif
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}

#if BURT_ENABLE_HAIR_SHADING
BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_Hair(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BurtEvaluateHairShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}
#endif

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_ClearCoat(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_Subsurface(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_Fabric(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_Foliage(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}

#if BURT_ENABLE_FUR_SHADING
BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_Fur(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BurtEvaluateFurShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}
#endif

#if BURT_ENABLE_EYE_SHADING
BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents_Eye(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}
#endif

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
    return BURT_EVALUATE_DEFERRED_LIGHTING_SHADING_MODEL_COMPONENTS(
        BURT_DEFERRED_SHADING_MODEL_NAME,
        GBufferData,
        MainLight,
        ViewDirectionWS,
        PositionWS,
        ShadowPositionWS,
        ScreenUV);
}

#if defined(BURT_ENABLE_SHADING_DEBUG)
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
#endif

float4 Frag(Varyings input) : SV_Target
{
    float2 ScreenUV = input.ScreenUV;

    BurtEncodedGBuffer EncodedGBuffer = BurtSampleEncodedGBuffer(ScreenUV);
    BurtGBufferData GBufferData = BurtDecodeGBuffer(EncodedGBuffer);
#if defined(BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT) && BURT_ENABLE_EYE_SHADING
    if (!BurtIsEyeShadingModel(GBufferData.ShadingModelID))
    {
        GBufferData.ShadingModelID = BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID;
    }
#else
    GBufferData.ShadingModelID = BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID;
#endif

    float RawDepth;
    float3 PositionWS;
    float3 ShadowPositionWS;
    float3 ViewDirectionWS;
    BurtPrepareDeferredViewData(ScreenUV, RawDepth, PositionWS, ShadowPositionWS, ViewDirectionWS);

    float3 ShadowNormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    GBufferData = BurtResolveHairDeferredGeometryData(GBufferData, ViewDirectionWS, PositionWS);
    ShadowNormalWS = BurtGetHairGeometryNormalWS(GBufferData);
#endif

    int PerObjectShadowObjectIndex = BurtSampleDeferredPerObjectShadowObjectIndex(ScreenUV);
    float ShadowAttenuation = BurtSampleMainLightShadow(ShadowPositionWS, ShadowNormalWS, PerObjectShadowObjectIndex);
    ShadowAttenuation *= BurtResolveDeferredMaterialScreenSpaceShadow(ScreenUV, GBufferData);
    ShadowAttenuation *= BurtResolveDeferredMaterialFoliageMicroShadow(GBufferData);
    float TransmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(PositionWS, PerObjectShadowObjectIndex, -1.0f);
    float TransmissionShadowAttenuation = BurtSampleMainLightTransmissionShadow(PositionWS, ShadowNormalWS, PerObjectShadowObjectIndex, TransmissionThickness);
    BurtLight MainLight = BurtCreateMainLight(ShadowAttenuation, TransmissionShadowAttenuation, TransmissionThickness);

    BurtGBufferData ShadingGBufferData = GBufferData;
    float ScreenSpaceAmbientOcclusion = BurtResolveDeferredMaterialScreenSpaceAmbientOcclusion(ScreenUV, ShadingGBufferData);
    ShadingGBufferData.Occlusion = min(saturate(ShadingGBufferData.Occlusion), ScreenSpaceAmbientOcclusion);

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        ShadingGBufferData.BaseColor = float3(0.18f, 0.18f, 0.18f);
    }
#endif

    BurtPBRShadingComponents PBRComponents = BurtEvaluateDeferredLightingShadingModelComponents(
        ShadingGBufferData,
        MainLight,
        ViewDirectionWS,
        PositionWS,
        ShadowPositionWS,
        ScreenUV);
    BurtApplyDeferredGIIndirect(ScreenUV, ShadingGBufferData, PBRComponents);

    float3 FinalColor = PBRComponents.Lighting + GBufferData.Emission;
    float3 FinalPreExposedColor = BurtApplyPreExposure(FinalColor);
    float OutputAlpha = BurtEvaluateDeferredOutputAlpha(PBRComponents);

#if defined(BURT_ENABLE_SHADING_DEBUG)
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
    DebugSurfaceData.ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    DebugSurfaceData.ClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
    DebugSurfaceData.SubsurfaceThickness = BurtGetSubsurfaceThickness(GBufferData);
    DebugSurfaceData.SubsurfacePower = BurtGetSubsurfacePower(GBufferData);
    DebugSurfaceData.SubsurfaceDistortion = BurtGetSubsurfaceDistortion(GBufferData);
    DebugSurfaceData.SubsurfaceAmbient = BurtGetSubsurfaceAmbient(GBufferData);
    DebugSurfaceData.SubsurfaceScatteringMode = BurtGetSubsurfaceScatteringMode(GBufferData);
    DebugSurfaceData.Subsurface3SCurvature = saturate(GBufferData.Subsurface3SCurvature);
    DebugSurfaceData.SubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(GBufferData);
    DebugSurfaceData.FabricIsSilk = BurtGetFabricIsSilk(GBufferData);
    DebugSurfaceData.FabricFuzzWeight = BurtGetFabricFuzzWeight(GBufferData);
    DebugSurfaceData.FabricFuzzRoughness = BurtGetFabricFuzzRoughness(GBufferData);
    DebugSurfaceData.FabricFuzzColor = BurtGetFabricFuzzColor(GBufferData);
    DebugSurfaceData.Occlusion = GBufferData.Occlusion;
    DebugSurfaceData.ShadingModelID = GBufferData.ShadingModelID;

    BurtPBRMaterialData DebugGBufferMaterialData = BurtPreparePBRMaterialData(GBufferData);
    BurtPBRShadingComponents DebugLightingComponents = PBRComponents;
    BurtApplyDeferredLightingDebugBaseline(
        ShadingGBufferData,
        MainLight,
        ViewDirectionWS,
        PositionWS,
        ShadowPositionWS,
        ScreenUV,
        DebugLightingComponents);

    float3 DeferredAONormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    BurtShadingDebugData DebugData = BurtCreateDefaultShadingDebugData(DeferredAONormalWS);
    DebugData.NormalWS = DeferredAONormalWS;
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
    DebugData.ShadowAttenuation = ShadowAttenuation;
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

    DebugData.AmbientOcclusion = ShadingGBufferData.Occlusion;
    DebugData.EmissionColor = GBufferData.Emission;
    DebugData.FinalLightingColor = FinalColor;
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
    DebugData.GBufferBaseColor = GBufferData.BaseColor;
    DebugData.GBufferNormalWS = BurtGetGBufferDirectionWS(GBufferData);
    DebugData.GBufferMetallic = BurtGetDeferredLightingDebugMaterialChannel(GBufferData);
    DebugData.GBufferClearCoatMask = BurtGetClearCoatMask(GBufferData);
    DebugData.GBufferClearCoatNormalWS = BurtGetClearCoatNormalWS(GBufferData);
    DebugData.GBufferClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
    DebugData.GBufferSubsurfaceStrength = BurtGetSubsurfaceStrength(GBufferData);
    DebugData.GBufferSubsurfaceThickness = BurtGetSubsurfaceThickness(GBufferData);
    DebugData.GBufferSubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(GBufferData);
    DebugData.GBufferAnisotropy = GBufferData.Anisotropy;
    DebugData.GBufferTangentWS = GBufferData.TangentWS;
    DebugData.GBufferSmoothness = GBufferData.Smoothness;
    DebugData.GBufferOcclusion = GBufferData.Occlusion;
    DebugData.GBufferReflectance = GBufferData.Reflectance;
    DebugData.GBufferRoughness = DebugGBufferMaterialData.PerceptualRoughness;
    DebugData.GBufferDiffuseColor = DebugGBufferMaterialData.DiffuseColor;

    float3 DebugColor;
    if (BurtTryEvaluateMaterialShadingDebug(DebugSurfaceData, DebugData, DebugColor))
    {
        return float4(DebugColor, 1.0f);
    }
#endif

    return float4(FinalPreExposedColor, OutputAlpha);
}

#endif
