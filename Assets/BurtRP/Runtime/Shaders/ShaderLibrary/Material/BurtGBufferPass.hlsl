// Shared GBuffer pass wiring. Material shaders select one packing path with BURT_MATERIAL_SHADING_MODEL_*.
#ifndef BURT_GBUFFER_PASS_INCLUDED
#define BURT_GBUFFER_PASS_INCLUDED

#if !defined(BURT_MATERIAL_SHADING_MODEL_DEFAULT_LIT) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_CLEAR_COAT) && !defined(BURT_MATERIAL_SHADING_MODEL_SUBSURFACE)
    #define BURT_MATERIAL_SHADING_MODEL_DEFAULT_LIT 1
#endif

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"

struct GBufferAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
};

struct GBufferVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    float2 baseMapUV : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    float2 maskMapUV : TEXCOORD3;
    float2 emissionMapUV : TEXCOORD4;
};

struct GBufferFragmentOutput
{
    float4 gbuffer0 : SV_Target0;
    float4 gbuffer1 : SV_Target1;
    float4 gbuffer2 : SV_Target2;
    float4 gbuffer3 : SV_Target3;
    float4 gbuffer4 : SV_Target4;
};

GBufferVaryings VertGBuffer(GBufferAttributes input)
{
    GBufferVaryings output;
    output.positionCS = UnityObjectToClipPos(input.positionOS);
    output.normalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.normalOS));
    output.tangentWS = BurtObjectToWorldTangent(input.tangentOS);
    output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);
    output.maskMapUV = BurtTransformMaskMapUV(input.uv0, _MaskMap_ST);
    output.emissionMapUV = BurtTransformEmissionMapUV(input.uv0, _EmissionMap_ST);
    return output;
}

GBufferFragmentOutput BurtPackGBufferOutput(BurtEncodedGBuffer encodedGBuffer)
{
    GBufferFragmentOutput output;
    output.gbuffer0 = encodedGBuffer.gbuffer0;
    output.gbuffer1 = encodedGBuffer.gbuffer1;
    output.gbuffer2 = encodedGBuffer.gbuffer2;
    output.gbuffer3 = encodedGBuffer.gbuffer3;
    output.gbuffer4 = encodedGBuffer.gbuffer4;
    return output;
}

BurtSurfaceData BurtCreateGBufferSurfaceData(float4 baseColor, float4 maskMap)
{
#if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    float hairReflectance = saturate(_Reflectance * _HairSpecularScale);
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, hairReflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    surfaceData.smoothness = saturate(surfaceData.smoothness - _HairRoughnessOffset);
    float hairShiftScale = saturate(_HairShiftScale * maskMap.b);
    return BurtApplyHairGBufferSurfaceSemantics(surfaceData, (_HairScatter + _HairScatterBoost) * maskMap.r, hairShiftScale);
#else
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, _Metallic, maskMap, _OcclusionStrength);
    surfaceData = BurtApplyAnisotropySurfaceSemantics(surfaceData, _Anisotropy);

    #if defined(BURT_MATERIAL_SHADING_MODEL_CLEAR_COAT)
        surfaceData = BurtApplyClearCoatSurfaceSemantics(surfaceData, _ClearCoatMask, _ClearCoatRoughness);
    #elif defined(BURT_MATERIAL_SHADING_MODEL_SUBSURFACE)
        surfaceData = BurtApplySubsurfaceSurfaceSemantics(surfaceData, _SubsurfaceStrength, _SubsurfaceThickness, _SubsurfacePower, _SubsurfaceDistortion, _SubsurfaceAmbient, _SubsurfaceTint.rgb);
    #endif

    return surfaceData;
#endif
}

BurtGBufferData BurtCreateMaterialGBufferData(GBufferVaryings input, float facing, BurtSurfaceData surfaceData, float3 emissionColor)
{
#if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    float strandDirectionSign = lerp(1.0f, -1.0f, saturate(_HairTangentFlip));
    float3 strandDirectionWS = BurtSafeNormalize(input.tangentWS.xyz * strandDirectionSign);
    return BurtCreateHairGBufferData(surfaceData, strandDirectionWS, emissionColor);
#else
    float3 normalWS = BurtSampleNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);

    #if defined(BURT_MATERIAL_SHADING_MODEL_CLEAR_COAT)
        float3 clearCoatNormalWS = BurtSampleClearCoatNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, _ClearCoatNormalScale, facing, _DoubleSidedNormalModeConstants);
        return BurtCreateClearCoatGBufferData(surfaceData, normalWS, input.tangentWS, clearCoatNormalWS, emissionColor);
    #elif defined(BURT_MATERIAL_SHADING_MODEL_SUBSURFACE)
        return BurtCreateSubsurfaceGBufferData(surfaceData, normalWS, input.tangentWS, emissionColor);
    #else
        return BurtCreateGBufferData(surfaceData, normalWS, input.tangentWS, emissionColor);
    #endif
#endif
}

GBufferFragmentOutput FragGBuffer(GBufferVaryings input, fixed facing : VFACE)
{
    float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;
    BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);

    float4 maskMap = BurtSampleMaskMap(input.maskMapUV);
    BurtSurfaceData surfaceData = BurtCreateGBufferSurfaceData(baseColor, maskMap);
    float3 emissionColor = BurtEvaluateEmission(input.emissionMapUV, _EmissionColor.rgb);
    BurtGBufferData gbufferData = BurtCreateMaterialGBufferData(input, facing, surfaceData, emissionColor);

    return BurtPackGBufferOutput(BurtEncodeGBuffer(gbufferData));
}

#endif // BURT_GBUFFER_PASS_INCLUDED
