#ifndef BURT_POST_PROCESS_FXAA_BRIDGE_INCLUDED
#define BURT_POST_PROCESS_FXAA_BRIDGE_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#define FXAA_QUALITY__PRESET 39
#define FXAA_PC 1
#define FXAA_HLSL_5 1

#include "Fxaa3_11.hlsl"

Texture2D _BurtPostProcessSourceTexture;
float4 _BurtPostProcessTexelSize;
float4 _BurtFXAAParams;

struct FXAAAttributes
{
    uint vertexID : SV_VertexID;
};

struct FXAAVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

FXAAVaryings VertFXAA(FXAAAttributes input)
{
    FXAAVaryings output;
    float2 uv = float2((input.vertexID << 1) & 2, input.vertexID & 2);
    output.positionCS = float4(uv * 2.0 - 1.0, 0.0, 1.0);
    output.uv = uv;
    return output;
}

float4 FragFXAA(FXAAVaryings input) : SV_Target
{
    FxaaTex textureAndSampler;
    textureAndSampler.tex = _BurtPostProcessSourceTexture;
    textureAndSampler.smpl = sampler_LinearClamp;
    textureAndSampler.UVMinMax = float4(0.0, 0.0, 1.0, 1.0);

    float2 rcpFrame = _BurtPostProcessTexelSize.xy;
    float4 texCorners = float4(input.uv - 0.5 * rcpFrame, input.uv + 0.5 * rcpFrame);
    float4 consoleRcpFrameOpt = float4(-0.5 * rcpFrame.x, -0.5 * rcpFrame.y, 0.5 * rcpFrame.x, 0.5 * rcpFrame.y);
    float4 consoleRcpFrameOpt2 = float4(-2.5 * rcpFrame.x, -2.5 * rcpFrame.y, 2.5 * rcpFrame.x, 2.5 * rcpFrame.y);

    return FxaaPixelShader(
        input.uv,
        texCorners,
        textureAndSampler,
        textureAndSampler,
        textureAndSampler,
        rcpFrame,
        consoleRcpFrameOpt,
        consoleRcpFrameOpt2,
        float4(0.0, 0.0, 0.0, 0.0),
        saturate(_BurtFXAAParams.x),
        max(_BurtFXAAParams.y, 0.0),
        max(_BurtFXAAParams.z, 0.0),
        8.0,
        0.125,
        0.05,
        float4(0.0, 0.0, 0.0, 0.0));
}

#endif
