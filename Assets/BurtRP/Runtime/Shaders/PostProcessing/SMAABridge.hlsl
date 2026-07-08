#ifndef BURT_POST_PROCESS_SMAA_BRIDGE_INCLUDED
#define BURT_POST_PROCESS_SMAA_BRIDGE_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"

#ifndef TEXTURE2D
    #define TEXTURE2D(textureName) Texture2D textureName
#endif

#ifndef mad
    #define mad(a, b, c) ((a) * (b) + (c))
#endif

Texture2D _BurtPostProcessSourceTexture;
Texture2D _BurtSMAAEdgeTexture;
Texture2D _BurtSMAABlendTexture;
Texture2D _BurtSMAAAreaTexture;
Texture2D _BurtSMAASearchTexture;
float4 _BurtPostProcessTexelSize;
float4 _BurtSMAAParams;

float2 ClampAndScaleUVForPoint(float2 uv)
{
    return min(uv, 1.0);
}

float2 ClampAndScaleUV(float2 uv, float2 texelSize, float numberOfTexels)
{
    float2 maxCoord = 1.0 - numberOfTexels * texelSize;
    return min(uv, maxCoord);
}

float2 ClampAndScaleUVForBilinear(float2 uv)
{
    return ClampAndScaleUV(uv, _BurtPostProcessTexelSize.xy, 0.5);
}

float3 PositivePow(float3 value, float power)
{
    return pow(abs(value), float3(power, power, power));
}

#define SMAA_HLSL_4_1
#define SMAA_RT_METRICS _BurtPostProcessTexelSize
#define SMAA_THRESHOLD max(_BurtSMAAParams.x, 0.0001)
#define SMAA_MAX_SEARCH_STEPS 16
#define SMAA_MAX_SEARCH_STEPS_DIAG 8
#define SMAA_CORNER_ROUNDING 25
#define SMAA_AREATEX_SELECT(sampleValue) sampleValue.rg
#define SMAA_SEARCHTEX_SELECT(sampleValue) sampleValue.a
#define GAMMA_FOR_EDGE_DETECTION (1.0 / 2.2)
#define LinearSampler sampler_LinearClamp
#define PointSampler sampler_PointClamp

#include "SubpixelMorphologicalAntiAliasing.hlsl"

struct SMAAAttributes
{
    uint vertexID : SV_VertexID;
};

float2 SMAAGetFullScreenTriangleUV(uint vertexID)
{
    return float2((vertexID << 1) & 2, vertexID & 2);
}

float4 SMAAGetFullScreenTrianglePosition(uint vertexID)
{
    float2 uv = SMAAGetFullScreenTriangleUV(vertexID);
    return float4(uv * 2.0 - 1.0, 0.0, 1.0);
}

struct SMAAVaryingsEdge
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 offsets[3] : TEXCOORD1;
};

SMAAVaryingsEdge VertSMAAEdge(SMAAAttributes input)
{
    SMAAVaryingsEdge output;
    output.positionCS = SMAAGetFullScreenTrianglePosition(input.vertexID);
    output.uv = SMAAGetFullScreenTriangleUV(input.vertexID);
    SMAAEdgeDetectionVS(output.uv, output.offsets);
    return output;
}

float4 FragSMAAEdge(SMAAVaryingsEdge input) : SV_Target
{
    return float4(SMAAColorEdgeDetectionPS(input.uv, input.offsets, _BurtPostProcessSourceTexture), 0.0, 0.0);
}

struct SMAAVaryingsBlend
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float2 pixCoord : TEXCOORD1;
    float4 offsets[3] : TEXCOORD2;
};

SMAAVaryingsBlend VertSMAABlend(SMAAAttributes input)
{
    SMAAVaryingsBlend output;
    output.positionCS = SMAAGetFullScreenTrianglePosition(input.vertexID);
    output.uv = SMAAGetFullScreenTriangleUV(input.vertexID);
    SMAABlendingWeightCalculationVS(output.uv, output.pixCoord, output.offsets);
    return output;
}

float4 FragSMAABlend(SMAAVaryingsBlend input) : SV_Target
{
    float4 subsampleIndices = 0.0;
    return SMAABlendingWeightCalculationPS(input.uv, input.pixCoord, input.offsets, _BurtSMAAEdgeTexture, _BurtSMAAAreaTexture, _BurtSMAASearchTexture, subsampleIndices);
}

struct SMAAVaryingsNeighbor
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 offset : TEXCOORD1;
};

SMAAVaryingsNeighbor VertSMAANeighbor(SMAAAttributes input)
{
    SMAAVaryingsNeighbor output;
    output.positionCS = SMAAGetFullScreenTrianglePosition(input.vertexID);
    output.uv = SMAAGetFullScreenTriangleUV(input.vertexID);
    SMAANeighborhoodBlendingVS(output.uv, output.offset);
    return output;
}

float4 FragSMAANeighbor(SMAAVaryingsNeighbor input) : SV_Target
{
    float4 color = SMAANeighborhoodBlendingPS(input.uv, input.offset, _BurtPostProcessSourceTexture, _BurtSMAABlendTexture);
    float blendStrength = saturate(_BurtSMAAParams.y);
    if (blendStrength < 1.0)
    {
        float4 source = SAMPLE_TEXTURE2D_LOD(_BurtPostProcessSourceTexture, sampler_PointClamp, ClampAndScaleUVForPoint(input.uv), 0);
        color = lerp(source, color, blendStrength);
    }

    return color;
}

#endif
