#ifndef BURT_DEFERRED_LIGHTING_PASS_INCLUDED
#define BURT_DEFERRED_LIGHTING_PASS_INCLUDED

#define BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL 1
#define BURT_USE_ADDITIONAL_LIGHT_BUFFER 1
#define BURT_USE_TILED_LIGHTING 1

// Enable Shading Debug only in Editor.
#if defined(UNITY_EDITOR)
    #define BURT_ENABLE_SHADING_DEBUG 1
#else
    #define BURT_ENABLE_SHADING_DEBUG 0
#endif

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#define BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

// Include debug shader only in Editor.
#if BURT_ENABLE_SHADING_DEBUG
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"
#endif

Texture2D<float> _BurtScreenSpaceAmbientOcclusionTexture;
Texture2D<float> _BurtScreenSpaceShadowTexture;
Texture2D<float4> _BurtGIDiffuseIndirectTexture;
Texture2D<float4> _BurtGIBackfaceDiffuseIndirectTexture;
Texture2D<float4> _BurtGIRoughSpecularIndirectTexture;
Texture3D<float4> _BurtGITranslucencyVolume0;
Texture3D<float4> _BurtGITranslucencyVolume1;
float _BurtScreenSpaceAmbientOcclusionEnabled;
float _BurtScreenSpaceShadowEnabled;
float _BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled;
float4 _BurtGIApplyIndirectParams; // x=diffuse enabled, y=intensity, z=backface enabled, w=rough-specular enabled.
float4 _BurtGIShortRangeAOParams; // x=enabled, y=weight, z=slope tolerance scale, w=radius pixels.
float4 _BurtGITranslucencyVolumeParams; // x=enabled, y=intensity, z=grazing power, w=backface mix.
float4 _BurtGITranslucencyVolumeGridSize; // xyz=volume grid size, w=apply scale.
float4 _BurtGITranslucencyVolumeGridZParams; // x=log scale, y=log bias, z=slice scale.
float4 _BurtGITranslucencyVolumeParams0; // x=near, y=far, z=depth fade power, w=screen-probe blend.

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
    float3 ScreenSpaceDiffuse = 0.0f;
    if (_BurtGIApplyIndirectParams.x >= 0.5f)
    {
        ScreenSpaceDiffuse = max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIDiffuseIndirectTexture, ScreenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
    }

    return ScreenSpaceDiffuse;
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

float2 BurtDeferredGIShortRangeAODirection(int Index)
{
    float2 Direction = 0.0f;
    if (Index == 0) Direction = float2(1.0f, 0.0f);
    if (Index == 1) Direction = float2(-1.0f, 0.0f);
    if (Index == 2) Direction = float2(0.0f, 1.0f);
    if (Index == 3) Direction = float2(0.0f, -1.0f);
    if (Index == 4) Direction = float2(1.0f, 1.0f);
    if (Index == 5) Direction = float2(-1.0f, 1.0f);
    if (Index == 6) Direction = float2(1.0f, -1.0f);
    if (Index == 7) Direction = float2(-1.0f, -1.0f);
    return Direction;
}

float BurtResolveDeferredGIShortRangeAO(float2 ScreenUV, BurtGBufferData GBufferData)
{
    if (_BurtGIShortRangeAOParams.x < 0.5f || _BurtGIShortRangeAOParams.y <= 0.0001f)
    {
        return 1.0f;
    }

    float CenterRawDepth = BurtSampleDeferredRawDepth(ScreenUV);
    float CenterLinearDepth = max(LinearEyeDepth(CenterRawDepth), 0.0001f);
    float3 CenterNormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    float2 Texel = max(_BurtDeferredScreenSize.zw, float2(1.0f / 8192.0f, 1.0f / 8192.0f));
    float RadiusPixels = max(_BurtGIShortRangeAOParams.w, 0.5f);
    float SlopeToleranceScale = max(_BurtGIShortRangeAOParams.z, 0.05f);
    float DepthTolerance = max(CenterLinearDepth * 0.0125f * SlopeToleranceScale, 0.015f);
    float OcclusionSum = 0.0f;
    float WeightSum = 0.0f;

    [unroll(8)]
    for (int Index = 0; Index < 8; ++Index)
    {
        float2 Direction = BurtDeferredGIShortRangeAODirection(Index);
        float2 SampleUV = saturate(ScreenUV + Direction * Texel * RadiusPixels);
        float SampleRawDepth = BurtSampleDeferredRawDepth(SampleUV);
        float SampleLinearDepth = LinearEyeDepth(SampleRawDepth);
        float FrontDepth = CenterLinearDepth - SampleLinearDepth;
        float FrontOcclusion = saturate(FrontDepth / DepthTolerance);
        BurtGBufferData SampleGBufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(SampleUV));
        float NormalMismatch = saturate(1.0f - dot(CenterNormalWS, BurtGetDeferredSurfaceNormalWS(SampleGBufferData)));
        float RingWeight = Index < 4 ? 1.0f : 0.75f;
        float SampleOcclusion = FrontOcclusion * lerp(0.35f, 1.0f, sqrt(NormalMismatch));
        OcclusionSum += SampleOcclusion * RingWeight;
        WeightSum += RingWeight;
    }

    float Occlusion = saturate(OcclusionSum / max(WeightSum, 0.0001f));
    return saturate(1.0f - Occlusion * max(_BurtGIShortRangeAOParams.y, 0.0f));
}

float BurtDeferredGIBackfaceDiffuseBlend(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    float Thickness = saturate(BurtGetSubsurfaceThickness(GBufferData));
    float Ambient = saturate(BurtGetSubsurfaceAmbient(GBufferData));
    float Strength = saturate(BurtGetSubsurfaceStrength(GBufferData));
    return saturate(max(Strength, max(Thickness, Ambient)));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    float Transmission = max(BurtGetFoliageTransmissionWeight(GBufferData), max(BurtGetFoliageThickness(GBufferData), BurtGetFoliageBackLight(GBufferData)));
    return saturate(Transmission);
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return 1.0f;
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return 0.0f;
#else
    return 0.0f;
#endif
}

float BurtDeferredGITranslucencyVolumeBlend(BurtGBufferData GBufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    float Thickness = saturate(BurtGetSubsurfaceThickness(GBufferData));
    float Ambient = saturate(BurtGetSubsurfaceAmbient(GBufferData));
    float Strength = saturate(BurtGetSubsurfaceStrength(GBufferData));
    return saturate(max(Strength, max(Thickness, Ambient)));
#elif defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    float Transmission = max(BurtGetFoliageTransmissionWeight(GBufferData), max(BurtGetFoliageThickness(GBufferData), BurtGetFoliageBackLight(GBufferData)));
    return saturate(Transmission);
#elif defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return 0.35f;
#else
    return 0.0f;
#endif
}

float3 BurtResolveDeferredGITranslucencyVolumeLite(float2 ScreenUV, BurtGBufferData GBufferData, float3 ViewDirectionWS)
{
    if (_BurtGITranslucencyVolumeParams.x < 0.5f || _BurtGIApplyIndirectParams.z < 0.5f)
    {
        return 0.0f;
    }

    float MaterialWeight = BurtDeferredGITranslucencyVolumeBlend(GBufferData);
    if (MaterialWeight <= 0.0001f)
    {
        return 0.0f;
    }

    float3 NormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    float NDotV = saturate(abs(dot(NormalWS, BurtSafeNormalize(ViewDirectionWS))));
    float GrazingWrap = pow(saturate(1.0f - NDotV), max(_BurtGITranslucencyVolumeParams.z, 0.05f));
    float ViewWeight = lerp(0.35f, 1.0f, GrazingWrap);
    float3 BackfaceRadiance = max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIBackfaceDiffuseIndirectTexture, ScreenUV).rgb, 0.0f);
    float RawDepth = BurtSampleDeferredRawDepth(ScreenUV);
    float LinearDepth = max(LinearEyeDepth(RawDepth), 0.0001f);
    float Slice = log2(LinearDepth * _BurtGITranslucencyVolumeGridZParams.x + _BurtGITranslucencyVolumeGridZParams.y) * _BurtGITranslucencyVolumeGridZParams.z / max(_BurtGITranslucencyVolumeGridSize.z, 1.0f);
    float3 VolumeUV = saturate(float3(ScreenUV, Slice));
    float4 Volume0 = _BurtGITranslucencyVolume0.SampleLevel(sampler_TriLinearClamp, VolumeUV, 0.0f);
    float4 Volume1 = _BurtGITranslucencyVolume1.SampleLevel(sampler_TriLinearClamp, VolumeUV, 0.0f);
    float VolumeConfidence = saturate(max(Volume0.a, Volume1.a));
    float3 NormalVS = BurtSafeNormalize(mul((float3x3)UNITY_MATRIX_V, NormalWS));
    float3 NormalizedAmbientColor = Volume0.rgb / (dot(Volume0.rgb, float3(0.2126f, 0.7152f, 0.0722f)) + 0.00001f);
    float3 DirectionalRadiance = max(dot(Volume1.rgb, NormalVS), 0.0f) * NormalizedAmbientColor;
    float3 VolumeRadiance = max(Volume0.rgb + DirectionalRadiance, 0.0f);
    float3 SourceRadiance = lerp(BackfaceRadiance * max(_BurtGITranslucencyVolumeParams.w, 0.0f), VolumeRadiance, VolumeConfidence);
    return SourceRadiance * MaterialWeight * ViewWeight * max(_BurtGIApplyIndirectParams.y, 0.0f) * max(_BurtGITranslucencyVolumeParams.y, 0.0f);
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
    float3 TranslucencyVolumeIndirect = BurtResolveDeferredGITranslucencyVolumeLite(ScreenUV, GBufferData, ViewDirectionWS);
    float ShortRangeAO = BurtResolveDeferredGIShortRangeAO(ScreenUV, GBufferData);
    DiffuseIndirect *= ShortRangeAO;
    BackfaceDiffuseIndirect *= lerp(1.0f, ShortRangeAO, 0.75f);
    RoughSpecularIndirect *= lerp(1.0f, ShortRangeAO, 0.35f);
    TranslucencyVolumeIndirect *= lerp(1.0f, ShortRangeAO, 0.5f);
    DiffuseIndirect += BackfaceDiffuseIndirect * BurtDeferredGIBackfaceDiffuseBlend(GBufferData);

    float3 SubsurfaceIndirectTransmission = max(Components.SubsurfaceIndirectTransmission, float3(0.0f, 0.0f, 0.0f));
    float3 SubsurfaceIndirectTransmissionForLighting = SubsurfaceIndirectTransmission;
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (BurtGetSubsurfaceStrength(GBufferData) > 0.0001f &&
        !BurtIsSubsurface3SPreIntegratedMode(BurtGetSubsurfaceScatteringMode(GBufferData)))
    {
        SubsurfaceIndirectTransmissionForLighting = float3(0.0f, 0.0f, 0.0f);
    }
#endif
    Components.SubsurfaceIndirectTransmission = SubsurfaceIndirectTransmission + TranslucencyVolumeIndirect;
    Components.IndirectDiffuse += DiffuseIndirect;
    Components.IndirectSpecular += RoughSpecularIndirect;
    Components.SubsurfaceIndirect = Components.IndirectDiffuse;
    Components.IndirectLighting = Components.IndirectDiffuse + Components.IndirectSpecular + SubsurfaceIndirectTransmissionForLighting + TranslucencyVolumeIndirect;
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
}

#if BURT_ENABLE_SHADING_DEBUG
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
    float TransmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(ShadowPositionWS, PerObjectShadowObjectIndex, -1.0f);
    float TransmissionShadowAttenuation = BurtSampleMainLightTransmissionShadow(ShadowPositionWS, ShadowNormalWS, PerObjectShadowObjectIndex, TransmissionThickness);
    BurtLight MainLight = BurtCreateMainLight(ShadowAttenuation, TransmissionShadowAttenuation, TransmissionThickness);

    BurtGBufferData ShadingGBufferData = GBufferData;
    float ScreenSpaceAmbientOcclusion = BurtResolveDeferredMaterialScreenSpaceAmbientOcclusion(ScreenUV, ShadingGBufferData);
    ShadingGBufferData.Occlusion = min(saturate(ShadingGBufferData.Occlusion), ScreenSpaceAmbientOcclusion);

#if BURT_ENABLE_SHADING_DEBUG
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
    BurtApplyDeferredGIIndirect(ScreenUV, ShadingGBufferData, ViewDirectionWS, PBRComponents);

    float3 FinalColor = PBRComponents.Lighting + GBufferData.Emission;
    float3 FinalPreExposedColor = BurtApplyPreExposure(FinalColor);
    float OutputAlpha = BurtEvaluateDeferredOutputAlpha(PBRComponents);

#if BURT_ENABLE_SHADING_DEBUG
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
#if BURT_ENABLE_CLEAR_COAT_SHADING
    DebugSurfaceData.ClearCoatMask = BurtGetClearCoatMask(GBufferData);
    DebugSurfaceData.ClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
#else
    DebugSurfaceData.ClearCoatMask = 0.0f;
    DebugSurfaceData.ClearCoatRoughness = 0.2f;
#endif
#if BURT_ENABLE_SUBSURFACE_SHADING
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
#if BURT_ENABLE_FABRIC_SHADING
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
#if BURT_ENABLE_CLEAR_COAT_SHADING
    DebugData.GBufferClearCoatMask = BurtGetClearCoatMask(GBufferData);
    DebugData.GBufferClearCoatNormalWS = BurtGetClearCoatNormalWS(GBufferData);
    DebugData.GBufferClearCoatRoughness = BurtGetClearCoatRoughness(GBufferData);
#else
    DebugData.GBufferClearCoatMask = 0.0f;
    DebugData.GBufferClearCoatNormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);
    DebugData.GBufferClearCoatRoughness = 0.2f;
#endif
#if BURT_ENABLE_SUBSURFACE_SHADING
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

    float3 DebugColor;
    if (BurtTryEvaluateMaterialShadingDebug(DebugSurfaceData, DebugData, DebugColor))
    {
        return float4(DebugColor, 1.0f);
    }
#endif

    return float4(FinalPreExposedColor, OutputAlpha);
}

#endif
