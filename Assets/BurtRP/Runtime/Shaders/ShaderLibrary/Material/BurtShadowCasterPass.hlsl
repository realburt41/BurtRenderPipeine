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
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        float4 color : COLOR;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID

#if BURT_SHADOW_CASTER_ALPHA_CLIP
    float2 uv0 : TEXCOORD0;
#endif
};

struct ShadowVaryings
{
    float4 positionCS : SV_POSITION;

#if BURT_SHADOW_CASTER_ALPHA_CLIP
    float2 baseMapUV : TEXCOORD0;
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
    float4 positionOS = BurtApplyMultipassObjectShellOffset(input.positionOS, input.normalOS);
    #if defined(BURT_MATERIAL_SHADING_MODEL_TRUNK) || defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
        positionOS = BurtApplyTrunkVertexAnimationObjectSpace(positionOS, input.color, _Time.y);
    #endif

    ShadowVaryings output;
    float3 biasedPositionWS = ApplyBurtShadowCasterNormalBias(positionOS, input.normalOS);
    output.positionCS = mul(UNITY_MATRIX_VP, float4(biasedPositionWS, 1.0f));

#if !defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    #if UNITY_REVERSED_Z
        output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #else
        output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
    #endif
#endif

#if BURT_SHADOW_CASTER_ALPHA_CLIP
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        output.baseMapUV = input.uv0;
    #else
        output.baseMapUV = BurtTransformBaseMapUV(input.uv0, _BaseMap_ST);
    #endif
#endif

    return output;
}

float4 FragShadow(ShadowVaryings input) : SV_Target
{
#if BURT_SHADOW_CASTER_ALPHA_CLIP
    float4 baseColor = BurtSampleBaseMap(input.baseMapUV) * _BaseColor;
    #if defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
        BurtApplyAlphaClip(saturate(baseColor.a - saturate(_ShadowCutOff)), _AlphaClip, 0.0f);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) || defined(BURT_MATERIAL_SHADING_MODEL_FOLIAGE)
        float alpha = BURT_SAMPLE_TEXTURE2D_REPEAT(_AlphaMap, input.baseMapUV).r;
        BurtApplyAlphaClip(alpha, _AlphaClip, _Cutoff);
    #else
        BurtApplyAlphaClip(baseColor.a, _AlphaClip, _Cutoff);
    #endif
#endif

    return 0;
}

#endif // BURT_SHADOW_CASTER_PASS_INCLUDED
