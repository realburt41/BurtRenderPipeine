// Shared forward pass for lit-style BurtRP materials. Material shaders select one lighting path with BURT_MATERIAL_SHADING_MODEL_*.
#ifndef BURT_FORWARD_PASS_INCLUDED
#define BURT_FORWARD_PASS_INCLUDED

#define BURT_FORWARD_SINGLE_SHADING_MODEL 1

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialShadingModelPassCommon.hlsl"

struct Attributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    float2 baseMapUV : TEXCOORD2;
    float4 tangentWS : TEXCOORD3;
    float3 positionWS : TEXCOORD4;
    float2 emissionMapUV : TEXCOORD5;
    float2 maskMapUV : TEXCOORD6;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    output.positionCS = UnityObjectToClipPos(input.positionOS);

    float4 positionWS = mul(unity_ObjectToWorld, input.positionOS);
    output.positionWS = positionWS.xyz;

    output.normalWS = normalize(UnityObjectToWorldNormal(input.normalOS));
    output.tangentWS = BurtObjectToWorldTangent(input.tangentOS);
    output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);
    output.emissionMapUV = BurtTransformEmissionMapUV(input.uv0, _EmissionMap_ST);
    output.maskMapUV = BurtTransformMaskMapUV(input.uv0, _MaskMap_ST);
    return output;
}

float3 BurtGetForwardNormalWS(Varyings input, float facing)
{
    return BurtGetMaterialPassNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, facing);
}

float3 BurtGetForwardShadingDirectionWS(Varyings input, float3 normalWS)
{
    return BurtGetMaterialPassShadingDirectionWS(normalWS, input.tangentWS);
}

float3 BurtGetForwardDebugNormalWS(float3 normalWS, float3 shadingDirectionWS)
{
    return BurtGetMaterialPassDebugNormalWS(normalWS, shadingDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateForwardShadingComponents(BurtSurfaceData surfaceData, BurtLight mainLight, Varyings input, float3 normalWS, float3 shadingDirectionWS, float3 viewDirectionWS, float facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtEvaluateHairShadingComponentsFromGBuffer(BurtCreateHairGBufferData(surfaceData, shadingDirectionWS, float3(0.0f, 0.0f, 0.0f)), mainLight, viewDirectionWS, input.positionWS);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
    float3 clearCoatNormalWS = BurtSampleClearCoatNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, _ClearCoatNormalScale, facing, _DoubleSidedNormalModeConstants);
    return BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, input.tangentWS, clearCoatNormalWS, viewDirectionWS, input.positionWS);
#else
    return BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, input.tangentWS, viewDirectionWS, input.positionWS);
#endif
}

#if defined(BURT_ENABLE_SHADING_DEBUG)
BurtGBufferData BurtCreateForwardDebugGBufferData(BurtSurfaceData surfaceData, Varyings input, float3 normalWS, float3 shadingDirectionWS, float facing)
{
    return BurtCreateMaterialPassGBufferData(surfaceData, input.baseMapUV, input.normalWS, normalWS, input.tangentWS, shadingDirectionWS, facing, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtEvaluateForwardAdditionalUnshadowedDebug(BurtSurfaceData surfaceData, float3 normalWS, float3 shadingDirectionWS, float3 viewDirectionWS, float3 positionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtGBufferData hairGBufferData = BurtCreateHairGBufferData(surfaceData, shadingDirectionWS, float3(0.0f, 0.0f, 0.0f));
    float3 hairNormalWS = BurtHairCreateViewFacingNormalWS(shadingDirectionWS, viewDirectionWS);
    BurtPBRGeometryData hairGeometryData = BurtPreparePBRGeometryData(hairNormalWS, viewDirectionWS);
    BurtHairDirectComponents hairAdditional = BurtEvaluateHairAdditionalDirectLightingUnshadowedComponents(hairGBufferData, hairGeometryData, positionWS);
    return hairAdditional.diffuse + hairAdditional.specular;
#else
    BurtPBRShadingCoreData coreData = BurtPreparePBRShadingCoreData(surfaceData, normalWS, viewDirectionWS);
    BurtDirectPBRComponents additional = BurtEvaluatePBRAdditionalDirectLightingUnshadowedFromCore(coreData, positionWS);
    return additional.diffuse + additional.specular;
#endif
}

void BurtFillForwardShadingDebugData(
    inout BurtShadingDebugData debugData,
    BurtSurfaceData surfaceData,
    BurtSurfaceData shadingSurfaceData,
    BurtPBRShadingComponents pbrComponents,
    BurtGBufferData debugDecodedGBufferData,
    BurtPBRMaterialData debugGBufferMaterialData,
    float3 normalWS,
    float3 shadingDirectionWS,
    float3 viewDirectionWS,
    float3 positionWS,
    float shadowAttenuation,
    float3 emissionColor,
    float3 finalColor)
{
    debugData.normalWS = normalWS;
    debugData.detailLightingColor = pbrComponents.lighting;
    debugData.directDiffuseColor = pbrComponents.directDiffuse;
    debugData.directSpecularColor = pbrComponents.directSpecular;
    debugData.additionalDiffuseColor = pbrComponents.additionalDiffuse;
    debugData.additionalSpecularColor = pbrComponents.additionalSpecular;
    debugData.additionalUnshadowedColor = BurtEvaluateForwardAdditionalUnshadowedDebug(shadingSurfaceData, normalWS, shadingDirectionWS, viewDirectionWS, positionWS);
    debugData.indirectDiffuseColor = pbrComponents.indirectDiffuse;
    debugData.indirectSpecularColor = pbrComponents.indirectSpecular;
    debugData.shadowAttenuation = shadowAttenuation;
    debugData.additionalShadowAttenuation = BurtEvaluateAdditionalShadowAttenuationDebug(positionWS, normalWS);
    BurtFillAdditionalLightShadowProjectionDebugData(
        positionWS,
        normalWS,
        debugData.additionalShadowFaceColor,
        debugData.additionalShadowUVColor,
        debugData.additionalShadowDepthColor,
        debugData.additionalShadowDepthDeltaColor);

    BurtFillMainLightShadowShadingDebugData(
        positionWS,
        debugData.normalWS,
        debugData.shadowCascadeColor,
        debugData.shadowCascadeBlend,
        debugData.shadowDistanceFade,
        debugData.shadowPCSSRadius,
        debugData.shadowReceiverDepthDelta,
        debugData.shadowPCSSBlockerFraction);

    debugData.ambientOcclusion = surfaceData.occlusion;
    debugData.emissionColor = emissionColor;
    debugData.finalLightingColor = finalColor;
    debugData.reflectance = surfaceData.reflectance;
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
    debugData.subsurfaceProfileIndex = pbrComponents.subsurfaceProfileIndex;
    debugData.subsurfaceTransmission = pbrComponents.subsurfaceTransmission;
    debugData.subsurfaceKernelWeight = pbrComponents.subsurfaceKernelWeight;
    debugData.subsurfaceIndirect = pbrComponents.subsurfaceIndirect;
    debugData.hairPrimaryLobe = pbrComponents.hairPrimaryLobe;
    debugData.hairSecondaryLobe = pbrComponents.hairSecondaryLobe;
    debugData.hairTransmissionLobe = pbrComponents.hairTransmissionLobe;
    debugData.hairScatter = pbrComponents.hairScatter;
    debugData.gbufferBaseColor = debugDecodedGBufferData.baseColor;
    debugData.gbufferNormalWS = BurtGetForwardDebugNormalWS(BurtGetDefaultLitNormalWS(debugDecodedGBufferData), BurtGetHairStrandDirectionWS(debugDecodedGBufferData));
    debugData.gbufferMetallic = BurtGetGBufferMaterialChannel(debugDecodedGBufferData);
    debugData.gbufferClearCoatMask = BurtGetClearCoatMask(debugDecodedGBufferData);
    debugData.gbufferClearCoatNormalWS = BurtGetClearCoatNormalWS(debugDecodedGBufferData);
    debugData.gbufferClearCoatRoughness = BurtGetClearCoatRoughness(debugDecodedGBufferData);
    debugData.gbufferSubsurfaceStrength = BurtGetSubsurfaceStrength(debugDecodedGBufferData);
    debugData.gbufferSubsurfaceThickness = BurtGetSubsurfaceThickness(debugDecodedGBufferData);
    debugData.gbufferSubsurfaceProfileIndex = BurtGetSubsurfaceProfileIndex(debugDecodedGBufferData);
    debugData.gbufferAnisotropy = debugDecodedGBufferData.anisotropy;
    debugData.gbufferTangentWS = debugDecodedGBufferData.tangentWS;
    debugData.gbufferSmoothness = debugDecodedGBufferData.smoothness;
    debugData.gbufferOcclusion = debugDecodedGBufferData.occlusion;
    debugData.gbufferReflectance = debugDecodedGBufferData.reflectance;
    debugData.gbufferRoughness = debugGBufferMaterialData.perceptualRoughness;
    debugData.gbufferDiffuseColor = debugGBufferMaterialData.diffuseColor;
}
#endif

float4 Frag(Varyings input, fixed facing : VFACE) : SV_Target
{
    float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;
    BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

    float3 normalWS = BurtGetForwardNormalWS(input, facing);
    float3 shadingDirectionWS = BurtGetForwardShadingDirectionWS(input, normalWS);
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    float4 maskMap = BurtSampleMaskMap(input.maskMapUV);
    BurtSurfaceData surfaceData = BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap);
    float shadowAttenuation = BurtSampleMainLightShadow(input.positionWS);
    BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);
    BurtSurfaceData shadingSurfaceData = surfaceData;

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (BurtIsShadingDebugEnabled() && BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_DETAIL_LIGHTING))
    {
        shadingSurfaceData.baseColor.rgb = float3(0.18f, 0.18f, 0.18f);
    }
#endif

    BurtPBRShadingComponents pbrComponents = BurtEvaluateForwardShadingComponents(shadingSurfaceData, mainLight, input, normalWS, shadingDirectionWS, viewDirectionWS, facing);
    float3 emissionColor = BurtEvaluateEmission(input.emissionMapUV, _EmissionColor.rgb);
    float3 finalColor = pbrComponents.lighting + emissionColor;

#if defined(BURT_ENABLE_SHADING_DEBUG)
    if (!BurtIsShadingDebugEnabled())
    {
        return float4(BurtApplyPreExposure(finalColor), surfaceData.alpha);
    }

    BurtGBufferData debugGBufferSourceData = BurtCreateForwardDebugGBufferData(surfaceData, input, normalWS, shadingDirectionWS, facing);
    BurtEncodedGBuffer debugEncodedGBuffer = BurtEncodeGBuffer(debugGBufferSourceData);
    BurtGBufferData debugDecodedGBufferData = BurtDecodeGBuffer(debugEncodedGBuffer);
    BurtPBRMaterialData debugGBufferMaterialData = BurtPreparePBRMaterialData(debugDecodedGBufferData);
    BurtShadingDebugData debugData = BurtCreateDefaultShadingDebugData(normalWS);
    BurtFillForwardShadingDebugData(
        debugData,
        surfaceData,
        shadingSurfaceData,
        pbrComponents,
        debugDecodedGBufferData,
        debugGBufferMaterialData,
        normalWS,
        shadingDirectionWS,
        viewDirectionWS,
        input.positionWS,
        shadowAttenuation,
        emissionColor,
        finalColor);

    float3 debugColor;
    if (BurtTryEvaluateMaterialShadingDebug(surfaceData, debugData, debugColor))
    {
        return float4(debugColor, surfaceData.alpha);
    }
#endif

    return float4(BurtApplyPreExposure(finalColor), surfaceData.alpha);
}

#endif // BURT_FORWARD_PASS_INCLUDED
