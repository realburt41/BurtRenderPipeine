// Shared ShadowCaster entry points for BurtRP materials.
#ifndef BURT_SHADOW_CASTER_PASS_INCLUDED
#define BURT_SHADOW_CASTER_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

#if !defined(BURT_SHADOW_CASTER_ALPHA_CLIP)
    #if defined(BURT_ALPHA_CLIP)
        #define BURT_SHADOW_CASTER_ALPHA_CLIP 1
    #else
        #define BURT_SHADOW_CASTER_ALPHA_CLIP 0
    #endif
#endif

#if !defined(BURT_SHADOW_CASTER_USES_BASE_MAP_UV)
    #if BURT_SHADOW_CASTER_ALPHA_CLIP || defined(BURT_MATERIAL_SHADING_MODEL_HAIR) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        #define BURT_SHADOW_CASTER_USES_BASE_MAP_UV 1
    #else
        #define BURT_SHADOW_CASTER_USES_BASE_MAP_UV 0
    #endif
#endif

float4 _BurtMainLightDirection;
float4 _BurtShadowCasterLightPosition;
float _BurtCastingPunctualLightShadow;
float3 _LightDirection;
float3 _LightPosition;
float4 _ShadowBias;
float _BurtMainLightShadowDepthBias;
float _BurtMainLightShadowNormalBias;

struct ShadowAttributes
{
    float4 PositionOS : POSITION;
    float3 NormalOS : NORMAL;
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
        float4 Color : COLOR;
    #endif
    #if BURT_SHADOW_CASTER_USES_BASE_MAP_UV
    float2 UV0 : TEXCOORD0;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct ShadowVaryings
{
    float4 PositionCS : SV_POSITION;
    #if BURT_SHADOW_CASTER_USES_BASE_MAP_UV
    float2 BaseMapUV : TEXCOORD0;
    #endif
};

float3 ApplyBurtShadowCasterNormalBias(float4 positionOS, float3 normalOS)
{
    float3 positionWS = mul(unity_ObjectToWorld, positionOS).xyz;
    float3 normalWS = UnityObjectToWorldNormal(normalOS);
    normalWS *= rsqrt(max(dot(normalWS, normalWS), 0.000001f));

    float3 lightDirectionWS = _LightDirection;
    if (dot(lightDirectionWS, lightDirectionWS) <= 0.000001f)
    {
        lightDirectionWS = _BurtMainLightDirection.xyz;
    }

#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    lightDirectionWS = _BurtShadowCasterLightPosition.xyz - positionWS;
    if (dot(lightDirectionWS, lightDirectionWS) <= 0.000001f)
    {
        lightDirectionWS = _LightPosition - positionWS;
    }
#else
    if (_BurtCastingPunctualLightShadow > 0.5f)
    {
        lightDirectionWS = _BurtShadowCasterLightPosition.xyz - positionWS;
    }
#endif

    lightDirectionWS *= rsqrt(max(dot(lightDirectionWS, lightDirectionWS), 0.000001f));

    float depthBias = _ShadowBias.x;
    float normalBias = _ShadowBias.y;
    if (abs(depthBias) <= 0.0000001f && abs(normalBias) <= 0.0000001f)
    {
        depthBias = _BurtMainLightShadowDepthBias;
        normalBias = _BurtMainLightShadowNormalBias;
    }

    float normalBiasScale = (1.0f - saturate(dot(normalWS, lightDirectionWS))) * normalBias;
    return positionWS + lightDirectionWS * depthBias + normalWS * normalBiasScale;
}

ShadowVaryings VertShadow(ShadowAttributes input)
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
    #if (defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)) && defined(BURT_FOLIAGE_USE_BAKED_NORMALS)
        positionOS.xyz *= 0.98f;
    #endif

    ShadowVaryings output;
    float3 biasedPositionWS = ApplyBurtShadowCasterNormalBias(positionOS, input.NormalOS);
    output.PositionCS = mul(UNITY_MATRIX_VP, float4(biasedPositionWS, 1.0f));

    #if BURT_SHADOW_CASTER_USES_BASE_MAP_UV
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        output.BaseMapUV = input.UV0;
    #else
        output.BaseMapUV = BurtTransformBaseMapUV(input.UV0, _BaseMap_ST);
    #endif
    #endif

    return output;
}

float4 FragShadow(ShadowVaryings input) : SV_Target
{
#if BURT_SHADOW_CASTER_ALPHA_CLIP
    float4 baseColor = BurtSampleBaseMap(input.BaseMapUV) * _BaseColor;
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        BurtApplyAlphaClip(saturate(baseColor.a - saturate(_ShadowCutOff)), _AlphaClip, 0.0f);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
            float alpha = SAMPLE_TEXTURE2D_BIAS(_AlphaMap, sampler_LinearRepeat, input.BaseMapUV, -1.0f).r;
        #else
            float alpha = SAMPLE_TEXTURE2D(_AlphaMap, sampler_LinearRepeat, input.BaseMapUV).r;
        #endif
        BurtApplyAlphaClip(alpha, _AlphaClip, _Cutoff);
    #else
        BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);
    #endif
#endif

    return 0;
}

#endif // BURT_SHADOW_CASTER_PASS_INCLUDED
