// Shared DepthOnly entry points for opaque/cutout BurtRP materials.
#ifndef BURT_DEPTH_ONLY_PASS_INCLUDED
#define BURT_DEPTH_ONLY_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"

#ifndef BURT_DEPTH_ONLY_ALPHA_CLIP
#define BURT_DEPTH_ONLY_ALPHA_CLIP 1
#endif

struct DepthAttributes
{
    float4 positionOS : POSITION;

#if BURT_DEPTH_ONLY_ALPHA_CLIP
    float2 uv0 : TEXCOORD0;
#endif
};

struct DepthVaryings
{
    float4 positionCS : SV_POSITION;

#if BURT_DEPTH_ONLY_ALPHA_CLIP
    float2 baseMapUV : TEXCOORD0;
#endif
};

DepthVaryings VertDepth(DepthAttributes input)
{
    DepthVaryings output;
    output.positionCS = UnityObjectToClipPos(input.positionOS);

#if BURT_DEPTH_ONLY_ALPHA_CLIP
    output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);
#endif

    return output;
}

float4 FragDepth(DepthVaryings input) : SV_Target
{
#if BURT_DEPTH_ONLY_ALPHA_CLIP
    float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;
    BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);
#endif

    return 0;
}

#endif // BURT_DEPTH_ONLY_PASS_INCLUDED
