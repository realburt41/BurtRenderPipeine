#ifndef BURT_DEFERRED_LIGHTING_PASS_INCLUDED
#define BURT_DEFERRED_LIGHTING_PASS_INCLUDED

#define BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL 1

// The production deferred shader compiles a single optional XRender-style
// shading-debug variant. Dedicated probe/shadow diagnostics remain lightweight
// standalone passes because they are not lighting-result selection.
#ifndef BURT_COMPILE_SHADING_DEBUG
#define BURT_COMPILE_SHADING_DEBUG 0
#endif

#define BURT_ENABLE_SHADING_DEBUG BURT_COMPILE_SHADING_DEBUG
#define BURT_LIGHTING_RESULT_INCLUDE_DEBUG_CHANNELS BURT_ENABLE_SHADING_DEBUG

// Deferred debug shaders select these before BurtLighting.hlsl is included so
// PBR composition can omit diagnostics that the active category never reads.
#if BURT_ENABLE_SHADING_DEBUG && defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_BRDF)
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_BRDF_DEBUG 1
#else
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_BRDF_DEBUG 0
#endif

#if BURT_ENABLE_SHADING_DEBUG && defined(BURT_DEFERRED_LIGHTING_DEBUG_CATEGORY_TRANSMISSION)
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG 1
#else
#define BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG 0
#endif

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#define BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingResult.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

Texture2D<float> _BurtScreenSpaceAmbientOcclusionTexture;
Texture2D<float> _BurtScreenSpaceShadowTexture;
Texture2D<float4> _BurtGIDiffuseIndirectTexture;
Texture2D<float4> _BurtGIBackfaceDiffuseIndirectTexture;
Texture2D<float4> _BurtGIRoughSpecularIndirectTexture;
float _BurtScreenSpaceAmbientOcclusionEnabled;
float _BurtScreenSpaceShadowEnabled;
float _BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled;
float4 _BurtGIApplyIndirectParams; // x=diffuse enabled, y=diffuse/transmission intensity, z=backface enabled, w=rough-specular enabled.
float4 _BurtGIApplyIndirectParams1; // x=XRender character diffuse intensity, y=reserved/legacy diffuse boost, z=XGI screen ratio, w=ratio speed/debug.
float4 _BurtGIShortRangeAOParams; // x=enabled, y=weight, z=slope tolerance scale, w=radius pixels.

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

float BurtResolveDeferredGIXGIScreenRatioMask(float2 ScreenUV)
{
    float ScreenRatio = saturate(_BurtGIApplyIndirectParams1.z);
    return ScreenUV.x <= ScreenRatio ? 1.0f : 0.0f;
}

float3 BurtSampleDeferredGIDiffuseIndirect(float2 ScreenUV)
{
    float3 ScreenSpaceDiffuse = 0.0f;
    if (_BurtGIApplyIndirectParams.x >= 0.5f && BurtResolveDeferredGIXGIScreenRatioMask(ScreenUV) > 0.5f)
    {
        ScreenSpaceDiffuse = max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIDiffuseIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
    }

    return ScreenSpaceDiffuse;
}

float3 BurtSampleDeferredGIBackfaceDiffuseIndirect(float2 ScreenUV)
{
    if (_BurtGIApplyIndirectParams.z < 0.5f || BurtResolveDeferredGIXGIScreenRatioMask(ScreenUV) <= 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIBackfaceDiffuseIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float3 BurtSampleDeferredGIRoughSpecularIndirect(float2 ScreenUV)
{
    if (_BurtGIApplyIndirectParams.w < 0.5f || BurtResolveDeferredGIXGIScreenRatioMask(ScreenUV) <= 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIRoughSpecularIndirectTexture, ScreenUV).rgb, 0.0f);
}

float BurtDeferredGIRoughSpecularSmoothReflectionFade(float PerceptualRoughness)
{
    const float SpecularReflectionsRoughnessMask = 0.6f;
    return saturate(PerceptualRoughness * (-2.0f / SpecularReflectionsRoughnessMask) + 2.0f);
}

float BurtResolveDeferredGIXGICharacterIntensity(BurtGBufferData GBufferData)
{
    bool IsCharacterLike =
        BurtIsActiveHairShadingModel(GBufferData.ShadingModelID) ||
        BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ||
        BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ||
        BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID);
    return IsCharacterLike ? max(_BurtGIApplyIndirectParams1.x, 0.0f) : 1.0f;
}

float3 BurtResolveDeferredGIXGIDiffuseColor(float3 DiffuseColor)
{
    return max(DiffuseColor, float3(0.0f, 0.0f, 0.0f));
}

float BurtResolveDeferredGIShortRangeAOWeight()
{
    return _BurtGIShortRangeAOParams.x >= 0.5f ? saturate(_BurtGIShortRangeAOParams.y) : 0.0f;
}

float3 BurtResolveDeferredGIMaterialShortRangeAO(BurtGBufferData GBufferData)
{
    float Weight = BurtResolveDeferredGIShortRangeAOWeight();
    float3 MaterialAO = BurtGTAOMultiBounce(GBufferData.Occlusion, GBufferData.BaseColor);
    return lerp(float3(1.0f, 1.0f, 1.0f), MaterialAO, Weight);
}

float BurtDeferredGIBackfaceDiffuseBlend(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    float Thickness = saturate(BurtGetSubsurfaceThickness(GBufferData));
    float Ambient = saturate(BurtGetSubsurfaceAmbient(GBufferData));
    float Strength = saturate(BurtGetSubsurfaceStrength(GBufferData));
    return saturate(max(Strength, max(Thickness, Ambient)));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    if (BurtGetFoliageIsGrass(GBufferData) > 0.5f)
    {
        return 0.0f;
    }

    float Transmission = max(BurtGetFoliageTransmissionWeight(GBufferData), max(BurtGetFoliageThickness(GBufferData), BurtGetFoliageBackLight(GBufferData)));
    return saturate(Transmission);
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return saturate(max(BurtGetHairBackLight(GBufferData), BurtGetHairScatter(GBufferData) * 0.35f));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return 0.0f;
#else
    return 0.0f;
#endif
}

float3 BurtDeferredGIBackfaceTransmissionColor(BurtGBufferData GBufferData, BurtPBRShadingComponents Components)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    return max(GBufferData.BaseColor, float3(0.0f, 0.0f, 0.0f));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return max(BurtGetFoliageTransmissionColor(GBufferData), float3(0.0f, 0.0f, 0.0f));
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return max(Components.DiffuseColor, float3(0.0f, 0.0f, 0.0f));
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

void BurtApplyDeferredGIIndirect(float2 ScreenUV, BurtGBufferData GBufferData, float3 ViewDirectionWS, inout BurtPBRShadingComponents Components)
{
    float RawDepth = BurtSampleDeferredRawDepth(ScreenUV);
    float3 PositionWS = BurtReconstructDeferredPositionWS(ScreenUV, RawDepth);
    float3 ProbeVolumeIrradiance;
    if (BurtTrySampleGIProbeVolumeIrradiance(PositionWS, BurtGetDeferredSurfaceNormalWS(GBufferData), ViewDirectionWS, ProbeVolumeIrradiance))
    {
        Components.IndirectDiffuse = Components.DiffuseColor * ProbeVolumeIrradiance * BurtGTAOMultiBounce(GBufferData.Occlusion, GBufferData.BaseColor) * saturate(Components.EnergyPreservation);
    }

    float3 DiffuseIndirect = BurtSampleDeferredGIDiffuseIndirect(ScreenUV);
    float3 BackfaceDiffuseIndirect = BurtSampleDeferredGIBackfaceDiffuseIndirect(ScreenUV);
    float3 RoughSpecularIndirect = BurtSampleDeferredGIRoughSpecularIndirect(ScreenUV);
    // XRender parity: Translucency Volume is sampled only by MATERIAL_USE_TRANSPARENT.
    // Deferred lighting shades opaque receivers, so mixing the froxel volume here creates
    // low-frequency light patches on skin, hair and foliage.
    float3 MaterialShortRangeAO = BurtResolveDeferredGIMaterialShortRangeAO(GBufferData);
    float EnergyPreservation = saturate(Components.EnergyPreservation);
    float3 XGIDiffuseColor = BurtResolveDeferredGIXGIDiffuseColor(Components.DiffuseColor);
    DiffuseIndirect *= BurtResolveDeferredGIXGICharacterIntensity(GBufferData);
    DiffuseIndirect *= MaterialShortRangeAO;
    DiffuseIndirect *= XGIDiffuseColor * EnergyPreservation;
    float BackfaceDiffuseBlend = BurtDeferredGIBackfaceDiffuseBlend(GBufferData);
    float3 BackfaceTransmissionIndirect = BackfaceDiffuseIndirect * BackfaceDiffuseBlend;
    BackfaceTransmissionIndirect *= BurtDeferredGIBackfaceTransmissionColor(GBufferData, Components);
    BackfaceTransmissionIndirect *= MaterialShortRangeAO;
    BackfaceTransmissionIndirect *= EnergyPreservation;

    float3 SubsurfaceIndirectTransmission = max(Components.SubsurfaceIndirectTransmission, float3(0.0f, 0.0f, 0.0f));
    float3 SubsurfaceIndirectTransmissionForLighting = SubsurfaceIndirectTransmission;
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (BurtGetSubsurfaceStrength(GBufferData) > 0.0001f &&
        !BurtIsSubsurface3SPreIntegratedMode(BurtGetSubsurfaceScatteringMode(GBufferData)))
    {
        SubsurfaceIndirectTransmissionForLighting = float3(0.0f, 0.0f, 0.0f);
    }
#endif
    Components.SubsurfaceIndirectTransmission = SubsurfaceIndirectTransmission + BackfaceTransmissionIndirect;
    Components.IndirectDiffuse += DiffuseIndirect;
    if (_BurtGIApplyIndirectParams.w >= 0.5f && any(RoughSpecularIndirect > 0.0001f))
    {
        float SmoothReflectionFade = BurtDeferredGIRoughSpecularSmoothReflectionFade(GBufferData.PerceptualRoughness);
        Components.IndirectSpecular = lerp(RoughSpecularIndirect, Components.IndirectSpecular, SmoothReflectionFade);
    }
    Components.SubsurfaceIndirect = Components.IndirectDiffuse;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular + SubsurfaceIndirectTransmissionForLighting + BackfaceTransmissionIndirect;
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

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents(
    BurtGBufferData GBufferData,
    BurtLight MainLight,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 ShadowPositionWS,
    float2 ScreenUV)
{
#if defined(BURT_DEFERRED_LIGHTING_EXCLUDE_ADDITIONAL)
    // Base deferred stage mirrors XRender's DeferredLightingNoPunctual pass.
    // Keep main-light and indirect evaluation here; punctual/additional lights
    // are accumulated by BurtDeferredAdditionalLightingPass.
    #if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return BurtEvaluateHairShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
    #elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return BurtEvaluateFurShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
    #else
        #if defined(BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT) && BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
    }
        #endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
    #endif
#else
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return BurtEvaluateHairShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return BurtEvaluateFurShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
#else
    #if defined(BURT_DEFERRED_SHADING_MODEL_DEFAULT_LIT) && BURT_ENABLE_EYE_SHADING
    if (BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID))
    {
        return BurtEvaluateEyeShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
    }
    #endif

    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
#endif
#endif
}

#if BURT_ENABLE_SHADING_DEBUG
#include "Assets/BurtRP/Runtime/Shaders/Deferred/BurtDeferredLightingDebug.hlsl"
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
    float TransmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(ShadowPositionWS, PerObjectShadowObjectIndex, -1.0f);
    float TransmissionShadowAttenuation = BurtSampleMainLightTransmissionShadow(ShadowPositionWS, ShadowNormalWS, PerObjectShadowObjectIndex, TransmissionThickness);
    BurtLight MainLight = BurtCreateMainLight(ShadowAttenuation, TransmissionShadowAttenuation, TransmissionThickness);

    BurtGBufferData ShadingGBufferData = GBufferData;
    float ScreenSpaceAmbientOcclusion = BurtResolveDeferredMaterialScreenSpaceAmbientOcclusion(ScreenUV, ShadingGBufferData);
    ShadingGBufferData.Occlusion = min(saturate(ShadingGBufferData.Occlusion), ScreenSpaceAmbientOcclusion);

#if BURT_ENABLE_SHADING_DEBUG && BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_DETAIL
    BurtApplyDeferredLightingDetailDebugOverride(ShadingGBufferData);
#endif

    BurtPBRShadingComponents PBRComponents = (BurtPBRShadingComponents)0;
    BurtLightingResult LightingResult = (BurtLightingResult)0;
    float3 FinalColor = float3(0.0f, 0.0f, 0.0f);
    float3 FinalPreExposedColor = float3(0.0f, 0.0f, 0.0f);
    float OutputAlpha = 1.0f;

    PBRComponents = BurtEvaluateDeferredLightingShadingModelComponents(
        ShadingGBufferData,
        MainLight,
        ViewDirectionWS,
        PositionWS,
        ShadowPositionWS,
        ScreenUV);
    BurtApplyDeferredGIIndirect(ScreenUV, ShadingGBufferData, ViewDirectionWS, PBRComponents);

    LightingResult = BurtCreateLightingResult(
        PBRComponents,
        GBufferData.Emission,
        ShadingGBufferData.Occlusion);
    FinalColor = LightingResult.FinalLighting;
    FinalPreExposedColor = BurtApplyPreExposure(FinalColor);
    OutputAlpha = BurtEvaluateDeferredOutputAlpha(PBRComponents);

#if BURT_ENABLE_SHADING_DEBUG
    return BurtEvaluateDeferredLightingDebugOutput(
        GBufferData,
        PBRComponents,
        LightingResult,
        FinalPreExposedColor,
        OutputAlpha);
#endif

    return float4(FinalPreExposedColor, OutputAlpha);
}

#endif
