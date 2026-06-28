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

float BurtSampleDeferredScreenSpaceAmbientOcclusion(float2 screenUV)
{
    if (_BurtScreenSpaceAmbientOcclusionEnabled < 0.5f)
    {
        return 1.0f;
    }

    int2 textureSize = max((int2)_BurtDeferredScreenSize.xy, int2(1, 1));
    int2 pixelCoord = clamp((int2)floor(screenUV * (float2)textureSize), int2(0, 0), textureSize - 1);
    float ao = _BurtScreenSpaceAmbientOcclusionTexture.Load(int3(pixelCoord, 0));
    return saturate(ao);
}

float BurtResolveDeferredMaterialScreenSpaceAmbientOcclusion(float2 screenUV, BurtGBufferData gbufferData)
{
    float screenSpaceAmbientOcclusion = BurtSampleDeferredScreenSpaceAmbientOcclusion(screenUV);
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    return saturate(lerp(1.0f, screenSpaceAmbientOcclusion, max(BurtGetFoliageScreenSpaceShadowIntensity(gbufferData), 0.0f)));
#else
    return screenSpaceAmbientOcclusion;
#endif
}

float BurtSampleDeferredScreenSpaceShadow(float2 screenUV)
{
    if (_BurtScreenSpaceShadowEnabled < 0.5f)
    {
        return 1.0f;
    }

    return saturate(BURT_SAMPLE_TEXTURE2D_LOD_CLAMP(_BurtScreenSpaceShadowTexture, screenUV, 0.0f));
}

float BurtResolveDeferredMaterialScreenSpaceShadow(float2 screenUV, BurtGBufferData gbufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_FOLIAGE)
    float screenSpaceShadow = BurtSampleDeferredScreenSpaceShadow(screenUV);
    return saturate(lerp(1.0f, screenSpaceShadow, max(BurtGetFoliageScreenSpaceShadowIntensity(gbufferData), 0.0f)));
#else
    return 1.0f;
#endif
}

float3 BurtSampleDeferredGIDiffuseIndirect(float2 screenUV)
{
    if (_BurtGIApplyIndirectParams.x < 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIDiffuseIndirectTexture, screenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float3 BurtSampleDeferredGIBackfaceDiffuseIndirect(float2 screenUV)
{
    if (_BurtGIApplyIndirectParams.z < 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIBackfaceDiffuseIndirectTexture, screenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float3 BurtSampleDeferredGIRoughSpecularIndirect(float2 screenUV)
{
    if (_BurtGIApplyIndirectParams.w < 0.5f)
    {
        return 0.0f;
    }

    return max(BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtGIRoughSpecularIndirectTexture, screenUV).rgb, 0.0f) * max(_BurtGIApplyIndirectParams.y, 0.0f);
}

float BurtDeferredGIBackfaceDiffuseBlend(BurtGBufferData gbufferData)
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

void BurtApplyDeferredGIIndirect(float2 screenUV, BurtGBufferData gbufferData, inout BurtPBRShadingComponents components)
{
    float3 diffuseIndirect = BurtSampleDeferredGIDiffuseIndirect(screenUV);
    float3 backfaceDiffuseIndirect = BurtSampleDeferredGIBackfaceDiffuseIndirect(screenUV);
    float3 roughSpecularIndirect = BurtSampleDeferredGIRoughSpecularIndirect(screenUV);
    diffuseIndirect = lerp(diffuseIndirect, backfaceDiffuseIndirect, BurtDeferredGIBackfaceDiffuseBlend(gbufferData));

    float roughSpecularBlend = smoothstep(0.35f, 0.92f, saturate(gbufferData.perceptualRoughness));
    float3 subsurfaceIndirectTransmission = max(components.subsurfaceIndirectTransmission, float3(0.0f, 0.0f, 0.0f));
    float3 subsurfaceIndirectTransmissionForLighting = subsurfaceIndirectTransmission;
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (BurtGetSubsurfaceStrength(gbufferData) > 0.0001f &&
        !BurtIsSubsurface3SPreIntegratedMode(BurtGetSubsurfaceScatteringMode(gbufferData)))
    {
        subsurfaceIndirectTransmissionForLighting = float3(0.0f, 0.0f, 0.0f);
    }
#endif
    components.subsurfaceIndirectTransmission = subsurfaceIndirectTransmission;
    components.indirectDiffuse += diffuseIndirect;
    components.indirectSpecular += roughSpecularIndirect * roughSpecularBlend;
    components.subsurfaceIndirect = components.indirectDiffuse;
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular + subsurfaceIndirectTransmissionForLighting;
    components.lighting = components.directLighting + components.indirectLighting;
}

float BurtEvaluateDeferredOutputAlpha(BurtPBRShadingComponents components)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    if (_BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled < 0.5f)
    {
        return 1.0f;
    }

    float3 diffuseLighting =
        components.directDiffuse +
        components.indirectDiffuse;
    return dot(BurtApplyPreExposure(diffuseLighting), float3(0.3f, 0.59f, 0.11f));
#else
    return 1.0f;
#endif
}

// 出处：XRender/Shaders/SlabShaderPass/SlabDeferredLightingPass.hlsl::Vert，通过 vertexID 生成全屏三角形。
Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = BurtGetFullScreenTriangleVertexPosition(input.vertexID);
    output.screenUV = BurtGetFullScreenTriangleTexCoord(input.vertexID);
    return output;
}

BurtPBRShadingComponents BurtEvaluateDeferredLightingShadingModelComponents(
    BurtGBufferData gbufferData,
    BurtLight mainLight,
    float3 viewDirectionWS,
    float3 positionWS,
    float3 shadowPositionWS,
    float2 screenUV)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return BurtEvaluateHairShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return BurtEvaluateFurShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
#else
    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
#endif
}

#if defined(BURT_ENABLE_SHADING_DEBUG)
float BurtGetDeferredLightingDebugMaterialChannel(BurtGBufferData gbufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return BurtGetHairScatter(gbufferData);
#elif defined(BURT_DEFERRED_SHADING_MODEL_FUR)
    return 0.0f;
#elif defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    return 1.0f;
#else
    return saturate(gbufferData.metallic);
#endif
}

void BurtApplyDeferredLightingDebugBaseline(
    BurtGBufferData shadingGBufferData,
    BurtLight mainLight,
    float3 viewDirectionWS,
    float3 positionWS,
    float3 shadowPositionWS,
    float2 screenUV,
    inout BurtPBRShadingComponents debugComponents)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    shadingGBufferData.shadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;

    BurtPBRShadingCoreData debugCoreData = BurtPreparePBRShadingCoreData(shadingGBufferData, viewDirectionWS);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(debugCoreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(debugCoreData, positionWS, shadowPositionWS, screenUV);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(debugCoreData);
    debugComponents = BurtComposePBRShadingComponentsWithAdditional(debugCoreData, directComponents, indirectComponents, additionalDirectComponents);
#endif
}

float3 BurtEvaluateDeferredLightingAdditionalUnshadowedDebug(
    BurtGBufferData gbufferData,
    float3 viewDirectionWS,
    float3 positionWS,
    float2 screenUV)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
    BurtPBRGeometryData hairGeometryData = BurtPrepareHairGeometryData(gbufferData, viewDirectionWS);
    BurtHairDirectComponents hairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(gbufferData, hairGeometryData, positionWS, screenUV);
    return hairAdditional.diffuse + hairAdditional.specular;
#else
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(gbufferData, viewDirectionWS);
    BurtDirectPBRComponents additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS, screenUV);
    return additional.diffuse + additional.specular;
#endif
}
#endif

float4 Frag(Varyings input) : SV_Target
{
    float2 screenUV = input.screenUV;

    BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
    gbufferData.shadingModelID = BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID;

    float rawDepth;
    float3 positionWS;
    float3 shadowPositionWS;
    float3 viewDirectionWS;
    BurtPrepareDeferredViewData(screenUV, rawDepth, positionWS, shadowPositionWS, viewDirectionWS);

    float3 shadowNormalWS = BurtGetGBufferDirectionWS(gbufferData);
#if BURT_ACTIVE_HAIR_SHADING_MODEL
    gbufferData = BurtResolveHairDeferredGeometryData(gbufferData, viewDirectionWS, positionWS);
    shadowNormalWS = BurtGetHairGeometryNormalWS(gbufferData);
#endif

    int perObjectShadowObjectIndex = BurtSampleDeferredPerObjectShadowObjectIndex(screenUV);
    float shadowAttenuation = BurtSampleMainLightShadow(shadowPositionWS, shadowNormalWS, perObjectShadowObjectIndex);
    shadowAttenuation *= BurtResolveDeferredMaterialScreenSpaceShadow(screenUV, gbufferData);
    float transmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(positionWS, perObjectShadowObjectIndex, -1.0f);
    float transmissionShadowAttenuation = BurtSampleMainLightTransmissionShadow(positionWS, shadowNormalWS, perObjectShadowObjectIndex, transmissionThickness);
    BurtLight mainLight = BurtCreateMainLight(shadowAttenuation, transmissionShadowAttenuation, transmissionThickness);

    BurtGBufferData shadingGBufferData = gbufferData;
    float screenSpaceAmbientOcclusion = BurtResolveDeferredMaterialScreenSpaceAmbientOcclusion(screenUV, shadingGBufferData);
    shadingGBufferData.occlusion = min(saturate(shadingGBufferData.occlusion), screenSpaceAmbientOcclusion);

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        shadingGBufferData.baseColor = float3(0.18f, 0.18f, 0.18f);
    }
#endif

    BurtPBRShadingComponents pbrComponents = BurtEvaluateDeferredLightingShadingModelComponents(
        shadingGBufferData,
        mainLight,
        viewDirectionWS,
        positionWS,
        shadowPositionWS,
        screenUV);
    BurtApplyDeferredGIIndirect(screenUV, shadingGBufferData, pbrComponents);

    float3 finalColor = pbrComponents.lighting + gbufferData.emission;
    float3 finalPreExposedColor = BurtApplyPreExposure(finalColor);
    float outputAlpha = BurtEvaluateDeferredOutputAlpha(pbrComponents);

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (!BurtIsShadingDebugEnabled())
    {
        return float4(finalPreExposedColor, outputAlpha);
    }

    BurtSurfaceData debugSurfaceData = BurtCreateSurfaceData(float4(gbufferData.baseColor, 1.0f));
    debugSurfaceData.baseColor = float4(gbufferData.baseColor, 1.0f);
    debugSurfaceData.alpha = 1.0f;
    debugSurfaceData.reflectance = gbufferData.reflectance;
    debugSurfaceData.smoothness = gbufferData.smoothness;
    debugSurfaceData.metallic = BurtGetDeferredLightingDebugMaterialChannel(gbufferData);
    debugSurfaceData.anisotropy = gbufferData.anisotropy;
    debugSurfaceData.height = 0.5f;
    debugSurfaceData.clearCoatMask = BurtGetClearCoatMask(gbufferData);
    debugSurfaceData.clearCoatRoughness = BurtGetClearCoatRoughness(gbufferData);
    debugSurfaceData.subsurfaceThickness = BurtGetSubsurfaceThickness(gbufferData);
    debugSurfaceData.subsurfacePower = BurtGetSubsurfacePower(gbufferData);
    debugSurfaceData.subsurfaceDistortion = BurtGetSubsurfaceDistortion(gbufferData);
    debugSurfaceData.subsurfaceAmbient = BurtGetSubsurfaceAmbient(gbufferData);
    debugSurfaceData.subsurfaceScatteringMode = BurtGetSubsurfaceScatteringMode(gbufferData);
    debugSurfaceData.subsurface3SCurvature = saturate(gbufferData.subsurface3SCurvature);
    debugSurfaceData.subsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(gbufferData);
    debugSurfaceData.fabricIsSilk = BurtGetFabricIsSilk(gbufferData);
    debugSurfaceData.fabricFuzzWeight = BurtGetFabricFuzzWeight(gbufferData);
    debugSurfaceData.fabricFuzzRoughness = BurtGetFabricFuzzRoughness(gbufferData);
    debugSurfaceData.fabricFuzzColor = BurtGetFabricFuzzColor(gbufferData);
    debugSurfaceData.occlusion = gbufferData.occlusion;
    debugSurfaceData.shadingModelID = gbufferData.shadingModelID;

    BurtPBRMaterialData debugGBufferMaterialData = BurtPreparePBRMaterialData(gbufferData);
    BurtPBRShadingComponents debugLightingComponents = pbrComponents;
    BurtApplyDeferredLightingDebugBaseline(
        shadingGBufferData,
        mainLight,
        viewDirectionWS,
        positionWS,
        shadowPositionWS,
        screenUV,
        debugLightingComponents);

    float3 deferredAONormalWS = BurtGetGBufferDirectionWS(gbufferData);
    BurtShadingDebugData debugData = BurtCreateDefaultShadingDebugData(BurtGetGBufferDirectionWS(gbufferData));
    debugData.normalWS = deferredAONormalWS;
    debugData.detailLightingColor = debugLightingComponents.lighting;
    debugData.directDiffuseColor = debugLightingComponents.directDiffuse;
    debugData.directSpecularColor = debugLightingComponents.directSpecular;
    debugData.additionalDiffuseColor = debugLightingComponents.additionalDiffuse;
    debugData.additionalSpecularColor = debugLightingComponents.additionalSpecular;
    debugData.additionalUnshadowedColor = BurtNeedsAdditionalLightingUnshadowedShadingDebug()
        ? BurtEvaluateDeferredLightingAdditionalUnshadowedDebug(shadingGBufferData, viewDirectionWS, positionWS, screenUV)
        : float3(0.0f, 0.0f, 0.0f);
    debugData.indirectDiffuseColor = debugLightingComponents.indirectDiffuse;
    debugData.indirectSpecularColor = debugLightingComponents.indirectSpecular;
    debugData.shadowAttenuation = shadowAttenuation;
    debugData.additionalShadowAttenuation = BurtNeedsAdditionalShadowAttenuationShadingDebug()
        ? BurtEvaluateAdditionalShadowAttenuationDebug(shadowPositionWS, deferredAONormalWS, screenUV)
        : 1.0f;

    if (BurtNeedsAdditionalShadowProjectionShadingDebug())
    {
        BurtFillAdditionalLightShadowProjectionDebugData(
            shadowPositionWS,
            deferredAONormalWS,
            screenUV,
            debugData.additionalShadowFaceColor,
            debugData.additionalShadowUVColor,
            debugData.additionalShadowDepthColor,
            debugData.additionalShadowDepthDeltaColor);
    }

    BurtFillMainLightShadowShadingDebugData(
        shadowPositionWS,
        debugData.normalWS,
        debugData.shadowCascadeColor,
        debugData.shadowCascadeBlend,
        debugData.shadowDistanceFade,
        debugData.shadowPCSSRadius,
        debugData.shadowReceiverDepthDelta,
        debugData.shadowPCSSBlockerFraction);

    BurtFillPerObjectShadowShadingDebugData(
        shadowPositionWS,
        shadowNormalWS,
        perObjectShadowObjectIndex,
        debugData.perObjectShadowObjectIndexColor,
        debugData.perObjectShadowSliceColor,
        debugData.perObjectShadowUVColor,
        debugData.perObjectShadowDepthColor,
        debugData.perObjectShadowCompareColor,
        debugData.perObjectShadowTransmissionDepthColor,
        debugData.perObjectShadowTransmissionThicknessColor);

    debugData.ambientOcclusion = shadingGBufferData.occlusion;
    debugData.emissionColor = gbufferData.emission;
    debugData.finalLightingColor = finalColor;
    debugData.reflectance = gbufferData.reflectance;
    debugData.perceptualRoughness = debugLightingComponents.perceptualRoughness;
    debugData.specularAARoughness = debugLightingComponents.specularAARoughness;
    debugData.specularEnergyCompensation = debugLightingComponents.specularEnergyCompensation;
    debugData.indirectSpecularEnergyCompensation = debugLightingComponents.indirectSpecularEnergyCompensation;
    debugData.energyPreservation = debugLightingComponents.energyPreservation;
    debugData.specularOcclusion = debugLightingComponents.specularOcclusion;
    debugData.diffuseColor = debugLightingComponents.diffuseColor;
    debugData.directBRDFD = debugLightingComponents.directBRDFD;
    debugData.directBRDFVisibility = debugLightingComponents.directBRDFVisibility;
    debugData.directBRDFFresnel = debugLightingComponents.directBRDFFresnel;
    debugData.directDiffuseLobe = debugLightingComponents.directDiffuseLobe;
    debugData.directDiffuseBRDF = debugLightingComponents.directDiffuseBRDF;
    debugData.directSpecularBRDF = debugLightingComponents.directSpecularBRDF;
    debugData.specularAANormalVariance = debugLightingComponents.specularAANormalVariance;
    debugData.specularAARoughnessDelta = debugLightingComponents.specularAARoughnessDelta;
    debugData.indirectSpecularDFG = debugLightingComponents.indirectSpecularDFG;
    debugData.indirectSpecularEnvBRDF = debugLightingComponents.indirectSpecularEnvBRDF;
    debugData.subsurfaceProfileIndex = pbrComponents.subsurfaceProfileIndex;
    debugData.subsurfaceTransmission = pbrComponents.subsurfaceTransmission;
    debugData.subsurfaceDirectTransmission = pbrComponents.subsurfaceDirectTransmission;
    debugData.subsurfaceTransmissionBRDF = pbrComponents.subsurfaceTransmissionBRDF;
    debugData.subsurfaceTransmissionShadow = pbrComponents.subsurfaceTransmissionShadow;
    debugData.subsurfaceTransmissionPhase = pbrComponents.subsurfaceTransmissionPhase;
    debugData.subsurfaceTransmissionThickness = pbrComponents.subsurfaceTransmissionThickness;
    debugData.subsurfaceKernelWeight = pbrComponents.subsurfaceKernelWeight;
    debugData.subsurfaceIndirect = pbrComponents.subsurfaceIndirect;
    debugData.hairPrimaryLobe = pbrComponents.hairPrimaryLobe;
    debugData.hairSecondaryLobe = pbrComponents.hairSecondaryLobe;
    debugData.hairTransmissionLobe = pbrComponents.hairTransmissionLobe;
    debugData.hairScatter = pbrComponents.hairScatter;
    debugData.gbufferBaseColor = gbufferData.baseColor;
    debugData.gbufferNormalWS = BurtGetGBufferDirectionWS(gbufferData);
    debugData.gbufferMetallic = BurtGetDeferredLightingDebugMaterialChannel(gbufferData);
    debugData.gbufferClearCoatMask = BurtGetClearCoatMask(gbufferData);
    debugData.gbufferClearCoatNormalWS = BurtGetClearCoatNormalWS(gbufferData);
    debugData.gbufferClearCoatRoughness = BurtGetClearCoatRoughness(gbufferData);
    debugData.gbufferSubsurfaceStrength = BurtGetSubsurfaceStrength(gbufferData);
    debugData.gbufferSubsurfaceThickness = BurtGetSubsurfaceThickness(gbufferData);
    debugData.gbufferSubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(gbufferData);
    debugData.gbufferAnisotropy = gbufferData.anisotropy;
    debugData.gbufferTangentWS = gbufferData.tangentWS;
    debugData.gbufferSmoothness = gbufferData.smoothness;
    debugData.gbufferOcclusion = gbufferData.occlusion;
    debugData.gbufferReflectance = gbufferData.reflectance;
    debugData.gbufferRoughness = debugGBufferMaterialData.perceptualRoughness;
    debugData.gbufferDiffuseColor = debugGBufferMaterialData.diffuseColor;

    float3 debugColor;
    if (BurtTryEvaluateMaterialShadingDebug(debugSurfaceData, debugData, debugColor))
    {
        return float4(debugColor, 1.0f);
    }
#endif

    return float4(finalPreExposedColor, outputAlpha);
}

#endif
