// Lit transparent refraction distortion pass. Writes XRender-style distortion payload:
// xy = scaled screen UV offset, z = rough refraction, w = surface view-space depth.
#ifndef BURT_REFRACTION_DISTORTION_PASS_INCLUDED
#define BURT_REFRACTION_DISTORTION_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtMaterialShadingModelPassCommon.hlsl"

UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

struct RefractionDistortionAttributes
{
    float4 PositionOS : POSITION;
    float3 NormalOS : NORMAL;
    float4 TangentOS : TANGENT;
    float2 UV0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct RefractionDistortionVaryings
{
    float4 PositionCS : SV_POSITION;
    float4 ScreenPos : TEXCOORD0;
    float2 BaseMapUV : TEXCOORD1;
    float2 MaskMapUV : TEXCOORD2;
    float3 NormalWS : TEXCOORD3;
    float4 TangentWS : TEXCOORD4;
    float3 PositionWS : TEXCOORD5;
};

RefractionDistortionVaryings VertRefractionDistortion(RefractionDistortionAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.PositionOS, input.NormalOS);

    RefractionDistortionVaryings output;
    output.PositionCS = UnityObjectToClipPos(positionOS);
    output.ScreenPos = ComputeScreenPos(output.PositionCS);
    output.BaseMapUV = BurtTransformBaseMapUV(input.UV0, _BaseMap_ST);
    output.MaskMapUV = BurtTransformMaskMapUV(input.UV0, _MaskMap_ST);
    output.NormalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.NormalOS));
    output.TangentWS = BurtObjectToWorldTangent(input.TangentOS);
    output.PositionWS = mul(unity_ObjectToWorld, positionOS).xyz;
    return output;
}

float2 BurtRefractionDistortionScreenUV(RefractionDistortionVaryings input)
{
    return saturate(input.ScreenPos.xy / max(input.ScreenPos.w, BURT_EPSILON));
}

float BurtRefractionDistortionLinearEyeDepth(float3 positionWS)
{
    return max(-mul(UNITY_MATRIX_V, float4(positionWS, 1.0f)).z, 1.0e-4f);
}

float2 BurtComputeRefractionDistortionOffset(float3 normalWS)
{
    float3 normalVS = BurtSafeNormalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
    float aspect = max(_ScreenParams.x, 1.0f) / max(_ScreenParams.y, 1.0f);
    float2 fovFix = float2(UNITY_MATRIX_P._m00, aspect * UNITY_MATRIX_P._m00);
    return normalVS.xy * (_IOR - 1.0f) * fovFix * 0.00023f * max(_ScreenParams.x, 1.0f) * saturate(_Refraction);
}

float4 FragRefractionDistortion(RefractionDistortionVaryings input, fixed facing : VFACE) : SV_Target
{
    clip(_Surface - 0.5f);
    clip(_Refraction - 1.0e-4f);

    float4 baseColor = BurtSampleBaseMap(input.BaseMapUV) * _BaseColor;
    float alpha = BurtEvaluateMaterialPassOpacity(baseColor.a, input.BaseMapUV, input.PositionWS);
    BurtApplyMaterialPassAlphaClip(alpha, _AlphaClip, _Cutoff, input.PositionCS);
    clip(alpha - 1.0e-4f);
    baseColor.a = alpha;

    float4 maskMap = BurtSampleMaskMap(input.MaskMapUV);
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.PositionWS);
    float3 normalWS = BurtGetMaterialPassNormalWS(input.BaseMapUV, input.NormalWS, input.TangentWS, facing);
    BurtSurfaceData surfaceData = BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap, input.BaseMapUV, normalWS, viewDirectionWS, input.PositionWS);

    float2 screenUV = BurtRefractionDistortionScreenUV(input);
    float2 uvBorder = max(_ScreenParams.zw - 1.0f, float2(0.0f, 0.0f));
    float2 clampedUV = clamp(screenUV, uvBorder, 1.0f - uvBorder);
    float2 refractionOffset = BurtComputeRefractionDistortionOffset(normalWS);
    float2 depthProbeUV = clamp(clampedUV + refractionOffset, uvBorder, 1.0f - uvBorder);

    float surfaceDepth = BurtRefractionDistortionLinearEyeDepth(input.PositionWS);
    float sceneRawDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, depthProbeUV);
    float sceneDepth = LinearEyeDepth(sceneRawDepth);
    float depthFade = saturate(max(sceneDepth - surfaceDepth, 0.0f) * 100.0f);
    float roughRefraction = saturate((1.0f - surfaceData.Smoothness) - saturate(_RefractionStage));

    return float4(refractionOffset * depthFade * 4.0f, roughRefraction, surfaceDepth);
}

#endif // BURT_REFRACTION_DISTORTION_PASS_INCLUDED
