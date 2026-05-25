// Shared DepthOnly entry points for opaque/cutout BurtRP materials.
#ifndef BURT_DEPTH_ONLY_PASS_INCLUDED
#define BURT_DEPTH_ONLY_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"

#if !defined(BURT_DEPTH_ONLY_ALPHA_CLIP)
    #if defined(BURT_ALPHA_CLIP)
        #define BURT_DEPTH_ONLY_ALPHA_CLIP 1
    #else
        #define BURT_DEPTH_ONLY_ALPHA_CLIP 0
    #endif
#endif

struct DepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID

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
    UNITY_SETUP_INSTANCE_ID(input);
    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.positionOS, input.normalOS);

    DepthVaryings output;
    output.positionCS = UnityObjectToClipPos(positionOS);

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
