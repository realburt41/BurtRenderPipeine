#ifndef BURT_POST_PROCESS_LENS_FLARE_BRIDGE_INCLUDED
#define BURT_POST_PROCESS_LENS_FLARE_BRIDGE_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"

Texture2D _BurtCameraDepthTexture;
Texture2D _BurtHiZDepthTexture;
Texture2D _BurtLensFlareBokeh0Tex;
Texture2D _BurtLensFlareBokeh1Tex;
Texture2D _BurtLensFlareBokeh2Tex;
Texture2D _BurtLensFlareBokeh3Tex;
Texture2D _BurtLensFlareBokeh4Tex;
Texture2D _BurtLensFlareLineTex;

float4 _BurtMainLightDirection;
float4 _BurtMainLightColor;
float4x4 _BurtLensFlareViewProjection;
float4 _BurtLensFlareBokeh0ScaleAndPosition;
float4 _BurtLensFlareBokeh1ScaleAndPosition;
float4 _BurtLensFlareBokeh2ScaleAndPosition;
float4 _BurtLensFlareBokeh3ScaleAndPosition;
float4 _BurtLensFlareBokeh4ScaleAndPosition;
float4 _BurtLensFlareBokeh0Color;
float4 _BurtLensFlareBokeh1Color;
float4 _BurtLensFlareBokeh2Color;
float4 _BurtLensFlareBokeh3Color;
float4 _BurtLensFlareBokeh4Color;
float4 _BurtLensFlareLineParams;
float4 _BurtLensFlareTotalParams;
float4 _BurtLensFlareTintColor;
float4 _BurtLensFlareTextureFlags0;
float4 _BurtLensFlareTextureFlags1;
float4 _BurtLensFlareDepthParams;

#define BURT_LENS_FLARE_TWO_PI 6.28318530718

struct LensFlareAttributes
{
    uint vertexID : SV_VertexID;
};

struct LensFlareVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

LensFlareVaryings VertLensFlare(LensFlareAttributes input)
{
    LensFlareVaryings output;
    float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
    output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
    output.uv = uv;
    return output;
}

float BurtLensFlareLuminance(float3 color)
{
    return dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722));
}

bool BurtLensFlareGetSunUV(out float2 sunUV)
{
    float3 lightDirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
    float3 sunPositionWS = _WorldSpaceCameraPos.xyz + lightDirectionWS * 1000.0;
    float4 sunCS = mul(_BurtLensFlareViewProjection, float4(sunPositionWS, 1.0));
    if (sunCS.w <= 0.000001)
    {
        sunUV = 0.0;
        return false;
    }

    float3 sunNDC = sunCS.xyz / sunCS.w;
    sunUV = sunNDC.xy * 0.5 + 0.5;
    return sunNDC.z >= 0.0 && sunNDC.z <= 1.0 && all(saturate(sunUV) == sunUV);
}

float BurtLensFlareIntensityScaleBySunPos(float2 sunUV)
{
    float2 factor = saturate((1.0 - abs(sunUV - 0.5) * 1.95) * 6.0);
    return factor.x * factor.y;
}

float BurtLensFlareVisibleBySceneDepth(float2 sunUV)
{
    float2 uv = saturate(sunUV);
    if (_BurtLensFlareDepthParams.x > 0.5)
    {
        float hiZDepth = _BurtHiZDepthTexture.SampleLevel(sampler_PointClamp, uv, max(_BurtLensFlareDepthParams.y, 0.0)).r;
        #if defined(UNITY_REVERSED_Z)
            return hiZDepth <= 1e-6 ? 1.0 : 0.0;
        #else
            return hiZDepth >= 1.0 - 1e-6 ? 1.0 : 0.0;
        #endif
    }

    float rawDepth = _BurtCameraDepthTexture.SampleLevel(sampler_PointClamp, uv, 0.0).r;
    float eyeDepth = LinearEyeDepth(rawDepth);
    float farPlane = max(_ProjectionParams.z, 0.0001);
    return eyeDepth >= farPlane * 0.98 ? 1.0 : 0.0;
}

float3 BurtLensFlareProceduralBokeh(float2 uv, float3 tint)
{
    float2 centered = uv - 0.5;
    float radius = length(centered);
    float disk = pow(saturate(1.0 - radius * 2.0), 2.5);
    float ring = pow(saturate(1.0 - abs(radius - 0.28) * 9.0), 2.0);
    float streak = pow(saturate(1.0 - abs(centered.y) * 18.0), 2.0) * pow(saturate(1.0 - abs(centered.x) * 1.7), 1.5);
    return (disk + ring * 0.35 + streak * 0.12) * tint;
}

float3 BurtLensFlareSampleBokeh(Texture2D tex, float textureValid, float2 screenUV, float2 positionFactor, float2 sunUV, float2 aspectRatioScaler, float2 elementScale, float3 inputColor)
{
    float2 centeredUV = screenUV - lerp(1.0 - sunUV, sunUV, positionFactor);
    float2 recenterUV = centeredUV * aspectRatioScaler * elementScale * max(_BurtLensFlareTotalParams.y, 0.0) + 0.5;
    float inBounds = all(saturate(recenterUV) == recenterUV) ? 1.0 : 0.0;
    float3 textureValue = tex.SampleLevel(sampler_LinearClamp, saturate(recenterUV), 0.0).rgb;
    float3 proceduralValue = BurtLensFlareProceduralBokeh(recenterUV, 1.0);
    return lerp(proceduralValue, textureValue, textureValid) * inputColor * inBounds;
}

float2 BurtLensFlareRotate(float2 inputUV, float rotationAngle)
{
    float sinRot = sin(rotationAngle * BURT_LENS_FLARE_TWO_PI);
    float cosRot = cos(rotationAngle * BURT_LENS_FLARE_TWO_PI);
    return float2(dot(inputUV, float2(cosRot, -sinRot)), dot(inputUV, float2(sinRot, cosRot)));
}

float BurtLensFlareVectorToRadiusValue(float2 inputVector)
{
    return frac(atan2(inputVector.y, inputVector.x) / BURT_LENS_FLARE_TWO_PI);
}

float3 BurtLensFlareLine(float2 screenUV, float2 sunUV, float2 aspectRatioScaler)
{
    float2 vectorToCenter = (sunUV - 0.5) * aspectRatioScaler;
    float vectorLength = max(length(vectorToCenter), 0.001);
    float angle = BurtLensFlareVectorToRadiusValue(vectorToCenter);
    float2 scaleUV = aspectRatioScaler * (screenUV - sunUV);
    float2 lineUV = BurtLensFlareRotate(scaleUV, -angle - 0.25);
    lineUV *= float2(max(_BurtLensFlareLineParams.z, 0.001), 1.0 / max(vectorLength * _BurtLensFlareLineParams.y, 0.001));
    float curve = _BurtLensFlareLineParams.w >= 0.0 ? max(_BurtLensFlareLineParams.w, 0.001) : min(_BurtLensFlareLineParams.w, -0.001);
    lineUV -= float2(0.0, pow(max(0.0, cos(abs(lineUV.x) * BURT_LENS_FLARE_TWO_PI)), 0.7) / curve);
    lineUV += float2(0.5, 0.0);

    float textureValid = _BurtLensFlareTextureFlags1.y;
    float3 textureLine = _BurtLensFlareLineTex.SampleLevel(sampler_LinearClamp, saturate(float2(lineUV.x, 1.0 - lineUV.y)), 0.0).rgb;
    textureLine *= _BurtLensFlareLineTex.SampleLevel(sampler_LinearClamp, saturate(float2(lineUV.x, 1.0 - lineUV.y) + float2(0.0, vectorLength * 0.2)), 0.0).rgb;

    float proceduralMask = pow(saturate(1.0 - abs(lineUV.x - 0.5) * 2.0), 3.0) * pow(saturate(1.0 - abs(lineUV.y) * 1.4), 2.0);
    float3 lineValue = lerp(proceduralMask.xxx, textureLine, textureValid);
    float finalIntensity = pow(saturate(1.0 - abs(vectorLength - 0.35)), 10.0) * _BurtLensFlareLineParams.x;
    return finalIntensity * lineValue;
}

float3 BurtLensFlareColor(float2 screenUV)
{
    float2 sunUV;
    bool isSunInScreen = BurtLensFlareGetSunUV(sunUV);
    float sunFade = BurtLensFlareIntensityScaleBySunPos(sunUV);
    float visible = BurtLensFlareVisibleBySceneDepth(sunUV);
    if (!isSunInScreen || sunFade <= 0.0 || visible <= 0.0)
    {
        return 0.0;
    }

    float2 aspectRatioScaler = float2(max(_ScreenParams.x, 1.0) / max(_ScreenParams.y, 1.0), 1.0);
    float3 color = BurtLensFlareLine(screenUV, sunUV, aspectRatioScaler);
    color += BurtLensFlareSampleBokeh(_BurtLensFlareBokeh0Tex, _BurtLensFlareTextureFlags0.x, screenUV, _BurtLensFlareBokeh0ScaleAndPosition.zw, sunUV, aspectRatioScaler, _BurtLensFlareBokeh0ScaleAndPosition.xy, _BurtLensFlareBokeh0Color.rgb);
    color += BurtLensFlareSampleBokeh(_BurtLensFlareBokeh1Tex, _BurtLensFlareTextureFlags0.y, screenUV, _BurtLensFlareBokeh1ScaleAndPosition.zw, sunUV, aspectRatioScaler, _BurtLensFlareBokeh1ScaleAndPosition.xy, _BurtLensFlareBokeh1Color.rgb);
    color += BurtLensFlareSampleBokeh(_BurtLensFlareBokeh2Tex, _BurtLensFlareTextureFlags0.z, screenUV, _BurtLensFlareBokeh2ScaleAndPosition.zw, sunUV, aspectRatioScaler, _BurtLensFlareBokeh2ScaleAndPosition.xy, _BurtLensFlareBokeh2Color.rgb);
    color += BurtLensFlareSampleBokeh(_BurtLensFlareBokeh3Tex, _BurtLensFlareTextureFlags0.w, screenUV, _BurtLensFlareBokeh3ScaleAndPosition.zw, sunUV, aspectRatioScaler, _BurtLensFlareBokeh3ScaleAndPosition.xy, _BurtLensFlareBokeh3Color.rgb);
    color += BurtLensFlareSampleBokeh(_BurtLensFlareBokeh4Tex, _BurtLensFlareTextureFlags1.x, screenUV, _BurtLensFlareBokeh4ScaleAndPosition.zw, sunUV, aspectRatioScaler, _BurtLensFlareBokeh4ScaleAndPosition.xy, _BurtLensFlareBokeh4Color.rgb);
    color *= sunFade * _BurtLensFlareTintColor.rgb * _BurtLensFlareTotalParams.x * BurtLensFlareLuminance(_BurtMainLightColor.rgb) * 0.5;
    return color;
}

float4 FragLensFlare(LensFlareVaryings input) : SV_Target
{
    return float4(BurtApplyPreExposure(max(BurtLensFlareColor(input.uv), 0.0)), 0.0);
}

#endif
