#ifndef BURT_DEFERRED_LIGHTING_PASS_INCLUDED
#define BURT_DEFERRED_LIGHTING_PASS_INCLUDED

#define BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL 1
#define BURT_USE_ADDITIONAL_LIGHT_BUFFER 1
#define BURT_USE_TILED_LIGHTING 1

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"

Texture2D<float> _BurtScreenSpaceAmbientOcclusionTexture;
float _BurtScreenSpaceAmbientOcclusionEnabled;
float _BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled;

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
    return dot(BurtApplyPreExposure(diffuseLighting), float3(0.2126f, 0.7152f, 0.0722f));
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

void BurtClipDeferredLightingPassShadingModel(float shadingModelID)
{
    float modelDelta = abs(BurtResolveSurfaceShadingModel(shadingModelID) - BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID);
    clip(0.5f - modelDelta);
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
#elif defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    BurtPBRMaterialData materialData = BurtPreparePBRMaterialData(gbufferData);
    float subsurfaceStrength = saturate(materialData.subsurfaceStrength);
    if (_BurtDeferredSubsurfaceDiffuseLuminanceOutputEnabled > 0.5f &&
        subsurfaceStrength > 0.0001f &&
        materialData.metallic < 0.5f)
    {
        materialData.diffuseColor = DiffuseColorFromBaseColor(float3(1.0f, 1.0f, 1.0f), materialData.metallic);
    }

    BurtPBRGeometryData geometryData = BurtPreparePBRGeometryData(gbufferData, viewDirectionWS);
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(materialData, geometryData);
    BurtDirectPBRComponents mainDirectComponents = BurtEvaluatePBRDirectFromCore(coreData, mainLight);
    BurtDirectPBRComponents additionalDirectComponents = BurtEvaluatePBRAdditionalDirectLightingFromCore(coreData, positionWS, shadowPositionWS, screenUV);
    BurtDirectPBRComponents directComponents = BurtAddPBRDirectComponents(mainDirectComponents, additionalDirectComponents);
    BurtIndirectPBRComponents indirectComponents = BurtEvaluatePBRIndirectFromCore(coreData);
    return BurtComposePBRShadingComponentsWithAdditional(coreData, directComponents, indirectComponents, additionalDirectComponents);
#else
    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
#endif
}

#if defined(BURT_ENABLE_SHADING_DEBUG)
float BurtGetDeferredLightingDebugMaterialChannel(BurtGBufferData gbufferData)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    return BurtGetHairScatter(gbufferData);
#elif defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    return saturate(gbufferData.subsurfaceStrength);
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
    shadingGBufferData.subsurfaceStrength = 0.0f;

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
    float3 strandDirectionWS = BurtSafeNormalize(BurtGetHairStrandDirectionWS(gbufferData));
    float3 hairNormalWS = BurtHairCreateViewFacingNormalWS(strandDirectionWS, viewDirectionWS);
    BurtPBRGeometryData hairGeometryData = BurtPreparePBRGeometryData(hairNormalWS, viewDirectionWS);
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

    BurtClipDeferredLightingPassShadingModel(BurtSampleDeferredShadingModelID(screenUV));

    BurtEncodedGBuffer encodedGBuffer = BurtSampleEncodedGBuffer(screenUV);
    BurtGBufferData gbufferData = BurtDecodeGBuffer(encodedGBuffer);
    gbufferData.shadingModelID = BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID;

    float rawDepth;
    float3 positionWS;
    float3 shadowPositionWS;
    float3 viewDirectionWS;
    BurtPrepareDeferredViewData(screenUV, rawDepth, positionWS, shadowPositionWS, viewDirectionWS);

    float shadowAttenuation = BurtSampleMainLightShadow(shadowPositionWS);
    BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);

    BurtGBufferData shadingGBufferData = gbufferData;
    float screenSpaceAmbientOcclusion = BurtSampleDeferredScreenSpaceAmbientOcclusion(screenUV);
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

    float3 finalColor = pbrComponents.lighting + gbufferData.emission;
    float3 finalPreExposedColor = BurtApplyPreExposure(finalColor);
    float outputAlpha = BurtEvaluateDeferredOutputAlpha(pbrComponents);

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (!BurtIsShadingDebugEnabled())
    {
        return float4(finalPreExposedColor, outputAlpha);
    }

    BurtSurfaceData debugSurfaceData;
    debugSurfaceData.baseColor = float4(gbufferData.baseColor, 1.0f);
    debugSurfaceData.alpha = 1.0f;
    debugSurfaceData.reflectance = gbufferData.reflectance;
    debugSurfaceData.smoothness = gbufferData.smoothness;
    debugSurfaceData.metallic = BurtGetDeferredLightingDebugMaterialChannel(gbufferData);
    debugSurfaceData.clearCoatMask = BurtGetClearCoatMask(gbufferData);
    debugSurfaceData.subsurfaceStrength = BurtGetSubsurfaceStrength(gbufferData);
    debugSurfaceData.subsurfaceThickness = BurtGetSubsurfaceThickness(gbufferData);
    debugSurfaceData.subsurfacePower = BurtGetSubsurfacePower(gbufferData);
    debugSurfaceData.subsurfaceDistortion = BurtGetSubsurfaceDistortion(gbufferData);
    debugSurfaceData.subsurfaceAmbient = BurtGetSubsurfaceAmbient(gbufferData);
    debugSurfaceData.subsurfaceTint = BurtGetSubsurfaceTint(gbufferData);
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
    debugData.additionalUnshadowedColor = BurtEvaluateDeferredLightingAdditionalUnshadowedDebug(shadingGBufferData, viewDirectionWS, positionWS, screenUV);
    debugData.indirectDiffuseColor = debugLightingComponents.indirectDiffuse;
    debugData.indirectSpecularColor = debugLightingComponents.indirectSpecular;
    debugData.shadowAttenuation = shadowAttenuation;
    debugData.additionalShadowAttenuation = BurtEvaluateAdditionalShadowAttenuationDebug(shadowPositionWS, deferredAONormalWS, screenUV);

    BurtFillAdditionalLightShadowProjectionDebugData(
        shadowPositionWS,
        deferredAONormalWS,
        screenUV,
        debugData.additionalShadowFaceColor,
        debugData.additionalShadowUVColor,
        debugData.additionalShadowDepthColor,
        debugData.additionalShadowDepthDeltaColor);

    BurtFillMainLightShadowShadingDebugData(
        shadowPositionWS,
        debugData.normalWS,
        debugData.shadowCascadeColor,
        debugData.shadowCascadeBlend,
        debugData.shadowDistanceFade,
        debugData.shadowPCSSRadius,
        debugData.shadowReceiverDepthDelta,
        debugData.shadowPCSSBlockerFraction);

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
