#ifndef BURT_EASY_FOG_PASS_INCLUDED
#define BURT_EASY_FOG_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEasyFogProperties.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtTransparentAtmosphereFog.hlsl"

UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);

struct BurtEasyFogAttributes
{
    float4 PositionOS : POSITION;
    float2 UV0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BurtEasyFogVaryings
{
    float4 PositionCS : SV_POSITION;
    float2 UV0 : TEXCOORD0;
    float4 ScreenPos : TEXCOORD1;
    float ViewDepth : TEXCOORD2;
    float CameraFade : TEXCOORD3;
    float3 PositionWS : TEXCOORD4;
#if defined(BURT_TRANSPARENT_VERTEX_FOG) && !defined(BURT_IGNORE_FOG)
    float4 TransparentFog : TEXCOORD5;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BurtEasyFogMotionVectorVaryings
{
    float4 PositionCS : SV_POSITION;
    float4 CurrentClipNoJitter : TEXCOORD0;
    float4 PreviousClipNoJitter : TEXCOORD1;
    float2 UV0 : TEXCOORD2;
    float SourceConfidence : TEXCOORD3;
};

float4x4 _BurtTAACurrentViewProjection;
float4x4 _BurtTAACurrentNonJitteredViewProjection;
float4x4 _BurtTAAPreviousNonJitteredViewProjection;
float4x4 unity_MatrixPreviousM;
float4 _BurtTAATexelSize;

float3 BurtEasyFogObjectCenterWS()
{
    return float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
}

float BurtEasyFogObjectScaleWeight()
{
    float scaleX = length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m01, unity_ObjectToWorld._m02));
    float scaleY = length(float3(unity_ObjectToWorld._m10, unity_ObjectToWorld._m11, unity_ObjectToWorld._m12));
    return (scaleX + scaleY) * 0.1f + 0.9f;
}

float BurtEasyFogCameraFade()
{
    float cameraFadeDistance = max(abs(_CameraFadingDistance), BURT_EPSILON);
    float cameraDistance = distance(BurtEasyFogObjectCenterWS(), _WorldSpaceCameraPos.xyz);
    float scaleWeight = max(BurtEasyFogObjectScaleWeight(), BURT_EPSILON);
    return saturate(((cameraDistance / scaleWeight) - cameraFadeDistance * 0.4f) / cameraFadeDistance);
}

float4 BurtEasyFogApplyBillboard(float4 positionOS)
{
    if (_EnableBillboard < 0.5f)
    {
        return positionOS;
    }

    float3 cameraOS = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1.0f)).xyz;
    float3 newZ = float3(cameraOS.x, 0.0f, cameraOS.z);
    newZ = dot(newZ, newZ) > BURT_EPSILON ? normalize(newZ) : float3(0.0f, 0.0f, 1.0f);

    float3 newX = normalize(cross(float3(0.0f, 1.0f, 0.0f), newZ));
    float3 newY = normalize(cross(newZ, newX));
    float3x3 billboardMatrix = float3x3(newX, newY, newZ);

    positionOS.xyz = mul(positionOS.xyz, billboardMatrix);
    return positionOS;
}

float BurtEasyFogSampleFlowOpacity(float2 uv0)
{
    float2 baseMapUV = uv0 * _OpacityMap_ST.xy + _OpacityMap_ST.zw;
    float2 flowMapUV = uv0 * _Flowmap_ST.xy + _Flowmap_ST.zw;
    float2 flowData = SAMPLE_TEXTURE2D(_Flowmap, sampler_LinearRepeat, flowMapUV).rg * 2.0f - 1.0f;
    flowData *= _FlowmapIntensity;

    float flowTime = (_Time.y * 20.0f) * _FlowmapSpeed;
    float phase1 = frac(flowTime);
    float phase2 = frac(flowTime + 0.5f);
    float blend = abs((phase1 - 0.5f) * 2.0f);

    float opacity1 = SAMPLE_TEXTURE2D(_OpacityMap, sampler_LinearClamp, baseMapUV + phase1 * flowData).r;
    float opacity2 = SAMPLE_TEXTURE2D(_OpacityMap, sampler_LinearClamp, baseMapUV + phase2 * flowData).r;
    return lerp(opacity1, opacity2, blend);
}

float BurtEasyFogDepthFade(float2 screenUV, float surfaceViewDepth)
{
    float rawSceneDepth = SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, screenUV);
    float sceneViewDepth = LinearEyeDepth(rawSceneDepth);
    float depthCompare = saturate((sceneViewDepth - surfaceViewDepth) * _DepthFadeDistance / 100.0f);
    return pow(depthCompare, max(_DepthFadePower, BURT_EPSILON));
}

BurtEasyFogVaryings BurtEasyFogVert(BurtEasyFogAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    BurtEasyFogVaryings output;
    UNITY_INITIALIZE_OUTPUT(BurtEasyFogVaryings, output);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float4 positionOS = BurtEasyFogApplyBillboard(input.PositionOS);
    float4 positionWS = mul(unity_ObjectToWorld, positionOS);
    output.PositionCS = mul(UNITY_MATRIX_VP, positionWS);
    output.UV0 = input.UV0;
    output.ScreenPos = ComputeScreenPos(output.PositionCS);
    output.ViewDepth = max(-mul(UNITY_MATRIX_V, positionWS).z, 0.0f);
    output.CameraFade = BurtEasyFogCameraFade();
    output.PositionWS = positionWS.xyz;
#if defined(BURT_TRANSPARENT_VERTEX_FOG) && !defined(BURT_IGNORE_FOG)
    float2 transparentFogScreenUV = saturate(
        output.ScreenPos.xy / max(output.ScreenPos.w, BURT_EPSILON));
    output.TransparentFog = BurtEvaluateTransparentFog(
        transparentFogScreenUV,
        output.PositionWS);
#endif
    return output;
}

float4 BurtEasyFogFrag(BurtEasyFogVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

    float2 screenUV = input.ScreenPos.xy / max(input.ScreenPos.w, BURT_EPSILON);
    screenUV = saturate(screenUV);

    float opacity = BurtEasyFogSampleFlowOpacity(input.UV0);
    opacity *= BurtEasyFogDepthFade(screenUV, input.ViewDepth);
    opacity *= _FogIntensity;
    opacity = saturate(opacity * saturate(input.CameraFade));

    clip(opacity - 0.0001f);

    float3 fogColor = _BaseColorTink.rgb * max(_EmissiveIntensity, 0.0f);
#if !defined(BURT_IGNORE_FOG)
#if defined(BURT_TRANSPARENT_VERTEX_FOG)
    float3 premultipliedFogColor = BurtBlendPremultipliedTransparentFog(
        fogColor * opacity,
        opacity,
        input.TransparentFog);
#else
    float3 premultipliedFogColor = BurtApplyPremultipliedTransparentFog(
        fogColor * opacity,
        opacity,
        screenUV,
        input.PositionWS);
#endif
#else
    float3 premultipliedFogColor = fogColor * opacity;
#endif
    return float4(BurtApplyPreExposure(premultipliedFogColor), opacity);
}

BurtEasyFogMotionVectorVaryings BurtEasyFogMotionVectorVert(BurtEasyFogAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float4 positionOS = BurtEasyFogApplyBillboard(input.PositionOS);
    float4 currentWorld = mul(unity_ObjectToWorld, positionOS);
    float4 previousWorld = mul(unity_MatrixPreviousM, positionOS);
    float3 objectDelta = previousWorld.xyz - currentWorld.xyz;

    BurtEasyFogMotionVectorVaryings output;
    output.PositionCS = mul(_BurtTAACurrentViewProjection, currentWorld);
    output.CurrentClipNoJitter = mul(_BurtTAACurrentNonJitteredViewProjection, currentWorld);
    output.PreviousClipNoJitter = mul(_BurtTAAPreviousNonJitteredViewProjection, previousWorld);
    output.UV0 = input.UV0;
    output.SourceConfidence = step(1e-8f, dot(objectDelta, objectDelta));
    return output;
}

float4 BurtEasyFogMotionVectorFrag(BurtEasyFogMotionVectorVaryings input) : SV_Target
{
    float opacity = saturate(BurtEasyFogSampleFlowOpacity(input.UV0) * _FogIntensity);
    clip(opacity - 0.0001f);
    clip(input.SourceConfidence - 0.5f);

    float2 currentUv = input.CurrentClipNoJitter.xy / max(abs(input.CurrentClipNoJitter.w), 1e-6f) * 0.5f + 0.5f;
    float2 previousUv = input.PreviousClipNoJitter.xy / max(abs(input.PreviousClipNoJitter.w), 1e-6f) * 0.5f + 0.5f;
#if UNITY_UV_STARTS_AT_TOP
    currentUv.y = 1.0f - currentUv.y;
    previousUv.y = 1.0f - previousUv.y;
#endif
    float valid = step(1e-5f, input.PreviousClipNoJitter.w);
    valid *= step(0.0f, currentUv.x) * step(currentUv.x, 1.0f) * step(0.0f, currentUv.y) * step(currentUv.y, 1.0f);
    valid *= step(0.0f, previousUv.x) * step(previousUv.x, 1.0f) * step(0.0f, previousUv.y) * step(previousUv.y, 1.0f);

    float2 velocity = currentUv - previousUv;
    float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
    clip(max(velocityPixels.x, velocityPixels.y) - 0.02f);
    return float4(velocity * valid, 1.0f, 1.0f);
}

float4 BurtEasyFogResponsiveAAMaskFrag(BurtEasyFogMotionVectorVaryings input) : SV_Target
{
    float opacity = saturate(BurtEasyFogSampleFlowOpacity(input.UV0) * _FogIntensity);
    clip(opacity - 0.0001f);
    clip(_ResponsiveAA - 0.5f);
    return float4(1.0f, 0.0f, 0.0f, 0.0f);
}

#endif // BURT_EASY_FOG_PASS_INCLUDED
