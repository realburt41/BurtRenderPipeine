#ifndef BURT_DEFERRED_LIGHTING_PASS_INCLUDED
#define BURT_DEFERRED_LIGHTING_PASS_INCLUDED

#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    #define BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID BURT_SHADING_MODEL_HAIR
    #define BURT_ENABLE_HAIR_SHADING 1
#elif defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT)
    #define BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID BURT_SHADING_MODEL_CLEAR_COAT
    #define BURT_ENABLE_CLEAR_COAT_SHADING 1
#elif defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE)
    #define BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID BURT_SHADING_MODEL_SUBSURFACE
    #define BURT_ENABLE_SUBSURFACE_SHADING 1
#else
    #define BURT_DEFERRED_LIGHTING_SHADING_MODEL_ID BURT_SHADING_MODEL_DEFAULT_LIT
    #define BURT_ENABLE_DEFAULT_LIT_SHADING 1
#endif

#define BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL 1
#define BURT_USE_ADDITIONAL_LIGHT_BUFFER 1
#define BURT_USE_TILED_LIGHTING 1

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"

sampler2D _BurtScreenSpaceAmbientOcclusionTexture;
float _BurtScreenSpaceAmbientOcclusionEnabled;

float BurtSampleDeferredScreenSpaceAmbientOcclusion(float2 screenUV)
{
    if (_BurtScreenSpaceAmbientOcclusionEnabled < 0.5f)
    {
        return 1.0f;
    }

    float ao = tex2D(_BurtScreenSpaceAmbientOcclusionTexture, screenUV).r;
    return saturate(ao);
}

float BurtResolveDeferredScreenSpaceSpecularOcclusionScale(
    float noV,
    float perceptualRoughness,
    float screenSpaceAO)
{
    return GetIndirectSpecularOcclusion(noV, saturate(screenSpaceAO), perceptualRoughness);
}

BurtPBRShadingComponents BurtApplyDeferredScreenSpaceAmbientOcclusion(
    BurtPBRShadingComponents components,
    float noV,
    float perceptualRoughness,
    float screenSpaceAO)
{
    float ao = saturate(screenSpaceAO);
    float specularAO = BurtResolveDeferredScreenSpaceSpecularOcclusionScale(noV, perceptualRoughness, ao);
    components.indirectDiffuse *= ao;
    components.indirectSpecular *= specularAO;
    components.specularOcclusion *= specularAO;
    components.indirectLighting = components.indirectDiffuse + components.indirectSpecular;
    components.lighting = components.directLighting + components.indirectLighting;
    return components;
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
#else
    return BurtEvaluatePBRShadingComponentsFromGBuffer(gbufferData, mainLight, viewDirectionWS, positionWS, shadowPositionWS, screenUV);
#endif
}

void BurtPrepareDeferredLightingAmbientOcclusionInputs(
    BurtGBufferData gbufferData,
    float3 viewDirectionWS,
    out float3 normalWS,
    out float perceptualRoughness)
{
#if defined(BURT_DEFERRED_SHADING_MODEL_HAIR)
    float3 viewDirection = BurtSafeNormalize(viewDirectionWS);
    normalWS = BurtHairCreateViewFacingNormalWS(BurtGetHairStrandDirectionWS(gbufferData), viewDirection);
    perceptualRoughness = gbufferData.perceptualRoughness;
#elif defined(BURT_DEFERRED_SHADING_MODEL_CLEAR_COAT)
    float clearCoatMask = saturate(gbufferData.clearCoatMask);
    normalWS = BurtSafeNormalize(lerp(gbufferData.normalWS, gbufferData.clearCoatNormalWS, clearCoatMask));
    perceptualRoughness = saturate(lerp(gbufferData.perceptualRoughness, ClampPerceptualRoughness(gbufferData.clearCoatRoughness), clearCoatMask));
#else
    normalWS = gbufferData.normalWS;
    perceptualRoughness = gbufferData.perceptualRoughness;
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

    float screenSpaceAmbientOcclusion = BurtSampleDeferredScreenSpaceAmbientOcclusion(screenUV);
    float3 deferredAONormalWS;
    float deferredAORoughness;
    BurtPrepareDeferredLightingAmbientOcclusionInputs(shadingGBufferData, viewDirectionWS, deferredAONormalWS, deferredAORoughness);

    float3 deferredAOViewDirectionWS = BurtSafeNormalize(viewDirectionWS);
    float deferredNoV = saturate(dot(deferredAONormalWS, deferredAOViewDirectionWS));
    pbrComponents = BurtApplyDeferredScreenSpaceAmbientOcclusion(pbrComponents, deferredNoV, deferredAORoughness, screenSpaceAmbientOcclusion);

    float3 finalColor = pbrComponents.lighting + gbufferData.emission;

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (!BurtIsShadingDebugEnabled())
    {
        return float4(finalColor, 1.0f);
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

    BurtShadingDebugData debugData;
    debugData.normalWS = BurtGetGBufferDirectionWS(gbufferData);
    debugData.detailLightingColor = pbrComponents.lighting;
    debugData.directDiffuseColor = pbrComponents.directDiffuse;
    debugData.directSpecularColor = pbrComponents.directSpecular;
    debugData.additionalDiffuseColor = pbrComponents.additionalDiffuse;
    debugData.additionalSpecularColor = pbrComponents.additionalSpecular;
    debugData.additionalUnshadowedColor = BurtEvaluateDeferredLightingAdditionalUnshadowedDebug(shadingGBufferData, viewDirectionWS, positionWS, screenUV);
    debugData.indirectDiffuseColor = pbrComponents.indirectDiffuse;
    debugData.indirectSpecularColor = pbrComponents.indirectSpecular;
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

    debugData.ambientOcclusion = saturate(gbufferData.occlusion * screenSpaceAmbientOcclusion);
    debugData.emissionColor = gbufferData.emission;
    debugData.finalLightingColor = finalColor;
    debugData.reflectance = gbufferData.reflectance;
    debugData.perceptualRoughness = pbrComponents.perceptualRoughness;
    debugData.specularAARoughness = pbrComponents.specularAARoughness;
    debugData.specularEnergyCompensation = pbrComponents.specularEnergyCompensation;
    debugData.indirectSpecularEnergyCompensation = pbrComponents.indirectSpecularEnergyCompensation;
    debugData.energyPreservation = pbrComponents.energyPreservation;
    debugData.specularOcclusion = pbrComponents.specularOcclusion;
    debugData.diffuseColor = pbrComponents.diffuseColor;
    debugData.directBRDFD = pbrComponents.directBRDFD;
    debugData.directBRDFVisibility = pbrComponents.directBRDFVisibility;
    debugData.directBRDFFresnel = pbrComponents.directBRDFFresnel;
    debugData.directDiffuseLobe = pbrComponents.directDiffuseLobe;
    debugData.directDiffuseBRDF = pbrComponents.directDiffuseBRDF;
    debugData.directSpecularBRDF = pbrComponents.directSpecularBRDF;
    debugData.specularAANormalVariance = pbrComponents.specularAANormalVariance;
    debugData.specularAARoughnessDelta = pbrComponents.specularAARoughnessDelta;
    debugData.indirectSpecularDFG = pbrComponents.indirectSpecularDFG;
    debugData.indirectSpecularEnvBRDF = pbrComponents.indirectSpecularEnvBRDF;
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

    return float4(finalColor, 1.0f);
}

#endif
