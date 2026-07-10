#ifndef BURT_POST_PROCESS_DIAPHRAGM_DOF_BRIDGE_INCLUDED
#define BURT_POST_PROCESS_DIAPHRAGM_DOF_BRIDGE_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

Texture2D _BurtPostProcessSourceTexture;
Texture2D _BurtCameraDepthTexture;
float4 _BurtPostProcessTexelSize;
float4 _BurtDiaphragmDOFParams0;
float4 _BurtDiaphragmDOFParams1;
float4 _BurtDiaphragmDOFParams2;

struct DiaphragmDOFAttributes
{
    uint vertexID : SV_VertexID;
};

struct DiaphragmDOFVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

DiaphragmDOFVaryings VertDiaphragmDOF(DiaphragmDOFAttributes input)
{
    DiaphragmDOFVaryings output;
    float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
    output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
    output.uv = uv;
    return output;
}

float BurtDiaphragmDofSignedCocPixels(float2 uv)
{
    float rawDepth = _BurtCameraDepthTexture.SampleLevel(sampler_PointClamp, saturate(uv), 0.0).r;
    float sceneDepth = max(LinearEyeDepth(rawDepth), 0.0001);
    float focusDistance = max(_BurtDiaphragmDOFParams0.x, 0.0001);
    float infinityCocRadius = max(_BurtDiaphragmDOFParams0.y, 0.0);
    float cocRadius = ((sceneDepth - focusDistance) / sceneDepth) * infinityCocRadius;
    float depthBlurAbsRadius = (1.0 - exp2(-sceneDepth * max(_BurtDiaphragmDOFParams1.x, 0.0))) * max(_BurtDiaphragmDOFParams1.y, 0.0);
    float signedCoc = max(abs(cocRadius), depthBlurAbsRadius);
    signedCoc = cocRadius < 0.0 ? -signedCoc : signedCoc;
    signedCoc = clamp(signedCoc, _BurtDiaphragmDOFParams0.z, _BurtDiaphragmDOFParams0.w);
    return signedCoc * max(_BurtPostProcessTexelSize.z, 1.0);
}

float2 BurtDiaphragmSampleOffset(int index)
{
    static const float2 offsets[24] =
    {
        float2(0.0000, 0.0000),
        float2(0.5000, 0.0000),
        float2(-0.2500, 0.4330),
        float2(-0.2500, -0.4330),
        float2(0.8660, 0.5000),
        float2(-0.8660, 0.5000),
        float2(0.0000, -1.0000),
        float2(0.0000, 1.0000),
        float2(0.8660, -0.5000),
        float2(-0.8660, -0.5000),
        float2(1.0000, 0.0000),
        float2(-1.0000, 0.0000),
        float2(0.3536, 0.3536),
        float2(-0.3536, 0.3536),
        float2(0.3536, -0.3536),
        float2(-0.3536, -0.3536),
        float2(0.7071, 0.7071),
        float2(-0.7071, 0.7071),
        float2(0.7071, -0.7071),
        float2(-0.7071, -0.7071),
        float2(0.2588, 0.9659),
        float2(-0.9659, 0.2588),
        float2(0.9659, -0.2588),
        float2(-0.2588, -0.9659)
    };

    return offsets[index];
}

float3 BurtDiaphragmDofVisualize(float signedCocPixels)
{
    float radius = saturate(abs(signedCocPixels) / max(_BurtDiaphragmDOFParams1.z, 1.0));
    float3 foreground = float3(0.1, 0.45, 1.0);
    float3 background = float3(1.0, 0.4, 0.1);
    return lerp(float3(0.05, 0.05, 0.05), signedCocPixels < 0.0 ? foreground : background, radius);
}

float4 FragDiaphragmDOF(DiaphragmDOFVaryings input) : SV_Target
{
    float2 uv = input.uv;
    float4 center = _BurtPostProcessSourceTexture.SampleLevel(sampler_LinearClamp, uv, 0.0);
    float signedCocPixels = BurtDiaphragmDofSignedCocPixels(uv);
    float radiusPixels = min(abs(signedCocPixels), max(_BurtDiaphragmDOFParams1.z, 0.0));

    if (_BurtDiaphragmDOFParams2.z > 0.5)
    {
        return float4(BurtDiaphragmDofVisualize(signedCocPixels), center.a);
    }

    if (radiusPixels < 0.35)
    {
        return center;
    }

    int sampleCount = _BurtDiaphragmDOFParams2.y > 0.5 ? 24 : 12;
    float squeeze = max(_BurtDiaphragmDOFParams2.x, 1.0);
    float2 texel = _BurtPostProcessTexelSize.xy;
    float3 sum = center.rgb;
    float weightSum = 1.0;

    [loop]
    for (int i = 1; i < 24; ++i)
    {
        if (i >= sampleCount)
        {
            break;
        }

        float2 diskOffset = BurtDiaphragmSampleOffset(i);
        diskOffset.x *= squeeze;
        float sampleDistance = length(diskOffset) * radiusPixels;
        float2 sampleUV = saturate(uv + diskOffset * radiusPixels * texel);
        float4 sampleColor = _BurtPostProcessSourceTexture.SampleLevel(sampler_LinearClamp, sampleUV, 0.0);
        float sampleCoc = BurtDiaphragmDofSignedCocPixels(sampleUV);
        float sampleRadius = min(abs(sampleCoc), max(_BurtDiaphragmDOFParams1.z, 0.0));
        float circleWeight = saturate((sampleRadius - sampleDistance + 1.0) * 0.5);
        float sameSideWeight = signedCocPixels < 0.0 ? (sampleCoc <= 0.0 ? 1.0 : 0.35) : (sampleCoc >= 0.0 ? 1.0 : 0.2);
        float weight = max(0.08, circleWeight) * sameSideWeight;
        sum += max(sampleColor.rgb, 0.0) * weight;
        weightSum += weight;
    }

    center.rgb = sum / max(weightSum, 0.0001);
    return center;
}

#endif
