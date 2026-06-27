// Shared DepthOnly entry points for opaque/cutout BurtRP materials.
#ifndef BURT_DEPTH_ONLY_PASS_INCLUDED
#define BURT_DEPTH_ONLY_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairDither.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

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
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        float4 color : COLOR;
    #endif
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
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        float3 positionWS : TEXCOORD1;
    #endif
#endif
};

DepthVaryings VertDepth(DepthAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.positionOS, input.normalOS);
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        positionOS = BurtApplyTrunkVertexAnimationObjectSpace(positionOS, input.color, _Time.y);
    #endif

    DepthVaryings output;
    output.positionCS = UnityObjectToClipPos(positionOS);

#if BURT_DEPTH_ONLY_ALPHA_CLIP
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        output.baseMapUV = input.uv0;
    #else
        output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);
    #endif
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        output.positionWS = mul(unity_ObjectToWorld, positionOS).xyz;
    #endif
#endif

    return output;
}

float4 FragDepth(DepthVaryings input) : SV_Target
{
#if BURT_DEPTH_ONLY_ALPHA_CLIP
    float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        BurtApplyHairDitherAlphaClip(baseColor.a, _AlphaClip, _Cutoff, input.positionCS);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        float alphaMap = BURT_SAMPLE_TEXTURE2D_REPEAT(_AlphaMap, input.baseMapUV).r;
        float distanceToCamera = distance(_WorldSpaceCameraPos.xyz, input.positionWS);
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
            float distanceFactor = saturate(distanceToCamera / 150.0f);
        #else
            float distanceFactor = saturate((distanceToCamera - 20.0f) / 200.0f);
        #endif
        float alpha = saturate(alphaMap + alphaMap * distanceFactor * max(_AlphaIncrease, 0.0f));
        BurtApplyAlphaClip(alpha, _AlphaClip, _Cutoff);
    #else
        BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);
    #endif
#endif

    return 0;
}

#endif // BURT_DEPTH_ONLY_PASS_INCLUDED
