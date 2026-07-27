#ifndef BURT_HEXA_LIGHTING_PASS_INCLUDED
#define BURT_HEXA_LIGHTING_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHexaLightingProperties.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingHexa.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtTransparentAtmosphereFog.hlsl"

struct BurtHexaAttributes
{
    float4 PositionOS : POSITION;
    float3 NormalOS : NORMAL;
    float4 TangentOS : TANGENT;
    float2 UV0 : TEXCOORD0;
    float4 Color : COLOR;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BurtHexaVaryings
{
    float4 PositionCS : SV_POSITION;
    float3 PositionWS : TEXCOORD0;
    float3 NormalWS : TEXCOORD1;
    float4 TangentWS : TEXCOORD2;
    float2 UV0 : TEXCOORD3;
    float4 Color : TEXCOORD4;
    float4 ScreenPos : TEXCOORD5;
    UNITY_VERTEX_OUTPUT_STEREO
};

struct BurtHexaMotionVectorVaryings
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

float2 BurtHexaFlipbookUV(float2 uv, float columns, float rows, float currentTile)
{
    currentTile = floor(currentTile);
    currentTile = fmod(currentTile, columns * rows);
    float2 tileScale = rcp(float2(columns, rows));
    float tileY = abs(rows - (floor(currentTile * tileScale.x) + 1.0f));
    float tileX = abs(currentTile - columns * floor(currentTile * tileScale.x));
    return (uv + float2(tileX, tileY)) * tileScale;
}

float4 BurtHexaSampleFlipbookMotionVectors(Texture2D flipbookTexture, float2 uv, float currentTile)
{
    float columns = max(_Rows, 1.0f);
    float rows = max(_Columns, 1.0f);
    float timeFactor = frac(currentTile);
    float2 frameUV0 = BurtHexaFlipbookUV(uv, columns, rows, currentTile);
    float2 frameUV1 = BurtHexaFlipbookUV(uv, columns, rows, currentTile + 1.0f);
    float4 motionUVs = float4(
        SAMPLE_TEXTURE2D(_MotionVectorMap, sampler_LinearRepeat, frameUV0).xy,
        SAMPLE_TEXTURE2D(_MotionVectorMap, sampler_LinearRepeat, frameUV1).xy);

    motionUVs = motionUVs * 2.0f - 1.0f;
    motionUVs.xy = frameUV0 - motionUVs.xy * timeFactor * _MotionVectorScale;
    motionUVs.zw = frameUV1 + motionUVs.zw * (1.0f - timeFactor) * _MotionVectorScale;

    float4 frame0 = flipbookTexture.Sample(sampler_LinearRepeat, motionUVs.xy);
    float4 frame1 = flipbookTexture.Sample(sampler_LinearRepeat, motionUVs.zw);
    return lerp(frame0, frame1, timeFactor);
}

BurtHexaVaryings Vert(BurtHexaAttributes input)
{
    BurtHexaVaryings output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_OUTPUT(BurtHexaVaryings, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.PositionCS = UnityObjectToClipPos(input.PositionOS);
    output.PositionWS = mul(unity_ObjectToWorld, input.PositionOS).xyz;
    output.NormalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.NormalOS));
    output.TangentWS = float4(
        BurtSafeNormalize(UnityObjectToWorldDir(input.TangentOS.xyz)),
        input.TangentOS.w * unity_WorldTransformParams.w);
    output.UV0 = input.UV0;
    output.Color = input.Color;
    output.ScreenPos = ComputeScreenPos(output.PositionCS);
    return output;
}

float4 Frag(BurtHexaVaryings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float currentTile = _Time.y * 20.0f * _PlaySpeed;
    float4 positiveAxesSample = BurtHexaSampleFlipbookMotionVectors(_PositiveAxesLightmap, input.UV0, currentTile);
    float4 negativeAxesSample = BurtHexaSampleFlipbookMotionVectors(_NegativeAxesLightmap, input.UV0, currentTile);

    BurtHexaLightingData data;
    data.BaseColor = max(_BasicColor.rgb * input.Color.rgb, float3(0.0f, 0.0f, 0.0f));
    data.ScatteringFactorRTBk = pow(max(positiveAxesSample.rgb, float3(0.0f, 0.0f, 0.0f)), max(_Density, 0.05f)) * 0.31830988618f;
    data.ScatteringFactorLBtF = pow(max(negativeAxesSample.rgb, float3(0.0f, 0.0f, 0.0f)), max(_Density, 0.05f)) * 0.31830988618f;

    float shadowAttenuation = BurtSampleMainLightShadow(input.PositionWS, input.NormalWS);
    BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);
    float3 lighting = BurtEvaluateHexaLighting(data, mainLight, input.PositionWS, input.NormalWS, input.TangentWS);
    float alpha = saturate(positiveAxesSample.a * _OverallAlpha);
    float2 screenUV = saturate(input.ScreenPos.xy / max(input.ScreenPos.w, BURT_EPSILON));
    float3 premultipliedLighting = BurtApplyPremultipliedTransparentFog(
        lighting * alpha,
        alpha,
        screenUV,
        input.PositionWS);
    return float4(BurtApplyPreExposure(premultipliedLighting), alpha);
}

BurtHexaMotionVectorVaryings VertMotionVector(BurtHexaAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float4 currentWorld = mul(unity_ObjectToWorld, input.PositionOS);
    float4 previousWorld = mul(unity_MatrixPreviousM, input.PositionOS);
    float3 objectDelta = previousWorld.xyz - currentWorld.xyz;

    BurtHexaMotionVectorVaryings output;
    output.PositionCS = mul(_BurtTAACurrentViewProjection, currentWorld);
    output.CurrentClipNoJitter = mul(_BurtTAACurrentNonJitteredViewProjection, currentWorld);
    output.PreviousClipNoJitter = mul(_BurtTAAPreviousNonJitteredViewProjection, previousWorld);
    output.UV0 = input.UV0;
    output.SourceConfidence = step(1e-8f, dot(objectDelta, objectDelta));
    return output;
}

float4 FragMotionVector(BurtHexaMotionVectorVaryings input) : SV_Target
{
    float currentTile = _Time.y * 20.0f * _PlaySpeed;
    float alpha = saturate(BurtHexaSampleFlipbookMotionVectors(_PositiveAxesLightmap, input.UV0, currentTile).a * _OverallAlpha);
    clip(alpha - 1.0f / 255.0f);
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

float4 FragResponsiveAAMask(BurtHexaMotionVectorVaryings input) : SV_Target
{
    float currentTile = _Time.y * 20.0f * _PlaySpeed;
    float alpha = saturate(BurtHexaSampleFlipbookMotionVectors(_PositiveAxesLightmap, input.UV0, currentTile).a * _OverallAlpha);
    clip(alpha - 1.0f / 255.0f);
    clip(_ResponsiveAA - 0.5f);
    return float4(1.0f, 0.0f, 0.0f, 0.0f);
}

#endif // BURT_HEXA_LIGHTING_PASS_INCLUDED
