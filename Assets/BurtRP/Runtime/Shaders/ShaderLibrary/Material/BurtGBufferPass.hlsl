// Shared GBuffer pass wiring. Material shaders select one packing path with BURT_MATERIAL_SHADING_MODEL_*.
#ifndef BURT_GBUFFER_PASS_INCLUDED
#define BURT_GBUFFER_PASS_INCLUDED

#define BURT_MATERIAL_GBUFFER_SINGLE_SHADING_MODEL 1

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEmission.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialShadingModelPassCommon.hlsl"

struct GBufferAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct GBufferVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    float2 baseMapUV : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    float2 maskMapUV : TEXCOORD3;
    float2 emissionMapUV : TEXCOORD4;
    float2 uv0 : TEXCOORD5;
    float2 uv1 : TEXCOORD6;
    float3 positionOS : TEXCOORD7;
    float3 positionWS : TEXCOORD8;
};

struct GBufferFragmentOutput
{
    float4 gbuffer0 : SV_Target0;
    float4 gbuffer1 : SV_Target1;
    float4 gbuffer2 : SV_Target2;
    float4 gbuffer3 : SV_Target3;
    float4 gbuffer4 : SV_Target4;
};

struct SubsurfaceForwardFragmentOutput
{
    float4 gbuffer0 : SV_Target0;
    float4 gbuffer2 : SV_Target1;
};

static const float BURT_SUBSURFACE_PROFILE_TYPE_BURLEY = 64.0f;
static const float BURT_SUBSURFACE_PROFILE_TYPE_SEPARABLE = 128.0f;

float BurtEncodeSubsurfaceProfileIDAndTypeForScreenSpacePass(BurtSurfaceData surfaceData)
{
    if (saturate(surfaceData.subsurfaceStrength) <= 0.0f)
    {
        return 0.0f;
    }

    float profileID = BurtClampSubsurfaceProfileIndex(surfaceData.subsurfaceProfileIndex);
    float profileType = abs(BurtClampSubsurfaceScatteringMode(surfaceData.subsurfaceScatteringMode) - BURT_SUBSURFACE_SCATTERING_MODE_4S_SEPARABLE) < 0.5f
        ? BURT_SUBSURFACE_PROFILE_TYPE_SEPARABLE
        : BURT_SUBSURFACE_PROFILE_TYPE_BURLEY;
    return (profileType + profileID) / 255.0f;
}

GBufferVaryings VertGBuffer(GBufferAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.positionOS, input.normalOS);

    GBufferVaryings output;
    output.positionCS = UnityObjectToClipPos(positionOS);
    output.normalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.normalOS));
    output.tangentWS = BurtObjectToWorldTangent(input.tangentOS);
    output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);
    output.maskMapUV = BurtTransformMaskMapUV(input.uv0, _MaskMap_ST);
    output.emissionMapUV = BurtTransformEmissionMapUV(input.uv0, _EmissionMap_ST);
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    output.positionOS = positionOS.xyz;
    output.positionWS = mul(unity_ObjectToWorld, positionOS).xyz;
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

BurtGBufferData BurtCreateMaterialGBufferData(GBufferVaryings input, float facing, BurtSurfaceData surfaceData, float3 emissionColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float2 hairNormalMapUV = input.uv0 * float2(_IDXTilling, 1.0f);
    float3 baseNormalWS = BurtGetMaterialPassNormalWS(hairNormalMapUV, input.normalWS, input.tangentWS, facing);
    float3 geometryNormalWS = BurtGetMaterialPassGeometryNormalWS(input.normalWS, facing);
    float3 shadingDirectionWS = BurtGetMaterialPassShadingDirectionWS(input.uv0, input.normalWS, input.tangentWS, facing);
    return BurtCreateMaterialPassGBufferData(surfaceData, hairNormalMapUV, geometryNormalWS, baseNormalWS, input.tangentWS, shadingDirectionWS, facing, emissionColor);
#else
    float3 baseNormalWS = BurtGetMaterialPassNormalWS(input.baseMapUV, input.normalWS, input.tangentWS, facing);
    float3 shadingDirectionWS = BurtGetMaterialPassShadingDirectionWS(baseNormalWS, input.tangentWS);
    return BurtCreateMaterialPassGBufferData(surfaceData, input.baseMapUV, input.normalWS, baseNormalWS, input.tangentWS, shadingDirectionWS, facing, emissionColor);
#endif
}

GBufferFragmentOutput FragGBuffer(GBufferVaryings input, fixed facing : VFACE)
{
    float4 baseColor = BurtEvaluateMaterialPassBaseColor(input.uv0, input.uv1, input.positionOS);
    BurtApplyMaterialPassAlphaClip(baseColor.a, _AlphaClip, _Cutoff, input.positionCS);

    float4 maskMap = BurtEvaluateMaterialPassMaskMap(input.uv0, input.uv1);
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    BurtSurfaceData surfaceData = BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap, input.uv0, input.uv1, input.positionOS, input.normalWS, input.tangentWS, viewDirectionWS);
    float3 emissionColor = BurtEvaluateEmission(input.emissionMapUV, _EmissionColor.rgb);
    BurtGBufferData gbufferData = BurtCreateMaterialGBufferData(input, facing, surfaceData, emissionColor);

    return BurtPackGBufferOutput(BurtEncodeGBuffer(gbufferData));
}

SubsurfaceForwardFragmentOutput FragSubsurfaceForward(GBufferVaryings input, fixed facing : VFACE)
{
    float4 baseColor = BurtEvaluateMaterialPassBaseColor(input.uv0, input.uv1, input.positionOS);
    BurtApplyMaterialPassAlphaClip(baseColor.a, _AlphaClip, _Cutoff, input.positionCS);

    float4 maskMap = BurtEvaluateMaterialPassMaskMap(input.uv0, input.uv1);
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    BurtSurfaceData surfaceData = BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap, input.uv0, input.uv1, input.positionOS, input.normalWS, input.tangentWS, viewDirectionWS);
    float3 emissionColor = BurtEvaluateEmission(input.emissionMapUV, _EmissionColor.rgb);

    SubsurfaceForwardFragmentOutput output;
    output.gbuffer0 = float4(saturate(surfaceData.baseColor.rgb), BurtEncodeSubsurfaceProfileIDAndTypeForScreenSpacePass(surfaceData));
    output.gbuffer2 = float4(max(emissionColor, float3(0.0f, 0.0f, 0.0f)), saturate(surfaceData.reflectance));
    return output;
}

#endif // BURT_GBUFFER_PASS_INCLUDED
