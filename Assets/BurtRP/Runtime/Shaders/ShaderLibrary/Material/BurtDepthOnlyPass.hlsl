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

#if !defined(BURT_DEPTH_ONLY_USES_BASE_MAP_UV)
    #if BURT_DEPTH_ONLY_ALPHA_CLIP || defined(BURT_MATERIAL_SHADING_MODEL_HAIR) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        #define BURT_DEPTH_ONLY_USES_BASE_MAP_UV 1
    #else
        #define BURT_DEPTH_ONLY_USES_BASE_MAP_UV 0
    #endif
#endif

struct DepthAttributes
{
    float4 PositionOS : POSITION;
    float3 NormalOS : NORMAL;
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        float4 Color : COLOR;
    #endif
    #if BURT_DEPTH_ONLY_USES_BASE_MAP_UV
    float2 UV0 : TEXCOORD0;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct DepthVaryings
{
    float4 PositionCS : SV_POSITION;
    #if BURT_DEPTH_ONLY_USES_BASE_MAP_UV
    float2 BaseMapUV : TEXCOORD0;
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        float3 PositionWS : TEXCOORD1;
    #endif
    #endif
};

DepthVaryings VertDepth(DepthAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.PositionOS, input.NormalOS);
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        positionOS = BurtApplyTrunkVertexAnimationObjectSpace(positionOS, input.Color, _Time.y);
    #elif defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        positionOS = BurtApplyGrassVertexAnimationObjectSpace(positionOS, input.NormalOS, input.Color, _Time.y);
        #else
        positionOS = BurtApplyFoliageVertexAnimationObjectSpace(positionOS, input.Color, _Time.y);
        #endif
    #endif

    DepthVaryings output;
    output.PositionCS = UnityObjectToClipPos(positionOS);

    #if BURT_DEPTH_ONLY_USES_BASE_MAP_UV
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        output.BaseMapUV = input.UV0;
    #else
        output.BaseMapUV = BurtTransformBaseMapUV(input.UV0, _BaseMap_ST);
    #endif
    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        output.PositionWS = mul(unity_ObjectToWorld, positionOS).xyz;
    #endif
    #endif

    return output;
}

float4 FragDepth(DepthVaryings input) : SV_Target
{
#if BURT_DEPTH_ONLY_ALPHA_CLIP
    float4 baseColor = BurtSampleBaseMap(input.BaseMapUV) * _BaseColor;
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        BurtApplyHairDitherAlphaClip(baseColor.a, _AlphaClip, _Cutoff, input.PositionCS);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
            float alphaMap = SAMPLE_TEXTURE2D_BIAS(_AlphaMap, sampler_LinearRepeat, input.BaseMapUV, -1.0f).r;
        #else
            float alphaMap = SAMPLE_TEXTURE2D(_AlphaMap, sampler_LinearRepeat, input.BaseMapUV).r;
        #endif
        float distanceToCamera = distance(_WorldSpaceCameraPos.xyz, input.PositionWS);
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
