// BurtRP runtime shader port of Assets/Res/Shader/MaterialModel/MM_CH_MultiPassFur.hlsl.
#ifndef BURT_MULTIPASS_FUR_PASS_INCLUDED
#define BURT_MULTIPASS_FUR_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#define BURT_FORWARD_SINGLE_SHADING_MODEL 1
#define BURT_MATERIAL_GBUFFER_SINGLE_SHADING_MODEL 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

sampler2D _FlowTex;
sampler2D _FlowDirectionMap;
sampler2D _EmissiveMap;

static const float BURT_MULTIPASS_FUR_REFERENCE_LAYER_COUNT = 16.0f;

struct BurtMultipassFurAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float4 tangentOS : TANGENT;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BurtMultipassFurVaryings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    float3 normalWS : TEXCOORD1;
    float4 tangentWS : TEXCOORD2;
    float2 uv0 : TEXCOORD3;
    float2 uv1 : TEXCOORD4;
    float layerIndex : TEXCOORD5;
    float3 geometricNormalWS : TEXCOORD6;
};

struct BurtMultipassFurGBufferOutput
{
    float4 gbuffer0 : SV_Target0;
    float4 gbuffer1 : SV_Target1;
    float4 gbuffer2 : SV_Target2;
    float4 gbuffer3 : SV_Target3;
    float4 gbuffer4 : SV_Target4;
};

float BurtMultipassFurLayerIndex()
{
    return (float)BurtGetCurrentInstanceID();
}

float BurtMultipassFurAttenuation(float layerIndex)
{
    float attenuation = _FurMaxCount > BURT_MULTIPASS_FUR_REFERENCE_LAYER_COUNT
        ? _FurAttenuation / max(_FurMaxCount / BURT_MULTIPASS_FUR_REFERENCE_LAYER_COUNT, 1.0f)
        : _FurAttenuation;
    return layerIndex * 0.1f * attenuation;
}

float2 BurtMultipassFurPanner(float2 uv, float2 speed)
{
    return uv + frac(speed * _Time.yy);
}

float3 BurtMultipassFurTangentToObject(float3 directionTS, float4 tangentOS, float3 normalOS)
{
    float3 tangent = BurtSafeNormalize(tangentOS.xyz);
    float3 normal = BurtSafeNormalize(normalOS);
    float3 bitangent = BurtSafeNormalize(cross(normal, tangent) * tangentOS.w);
    return BurtSafeNormalize(tangent * directionTS.x + bitangent * directionTS.y + normal * directionTS.z);
}

float4 BurtMultipassFurObjectToWorldTangent(float4 tangentOS)
{
    float3 tangentWS = BurtSafeNormalize(UnityObjectToWorldDir(tangentOS.xyz));
    return float4(tangentWS, tangentOS.w * unity_WorldTransformParams.w);
}

float3 BurtGetMultipassFurNormalWS(BurtMultipassFurVaryings input, float facing)
{
    float3 normalWS = input.normalWS;
    if (facing < 0.0f)
    {
        normalWS *= _DoubleSidedNormalModeConstants.z;
    }

    return BurtSafeNormalize(normalWS);
}

float3 BurtMultipassFurGravityVector(float furAtten)
{
    float gravity = -furAtten * 2.0f * _FurGravityIntensity;
    float axis = floor(_FurGravityDirection + 0.5f);
    if (axis < 0.5f)
    {
        return float3(gravity, 0.0f, 0.0f);
    }

    if (axis < 1.5f)
    {
        return float3(0.0f, gravity, 0.0f);
    }

    return float3(0.0f, 0.0f, gravity);
}

float3 BurtCalculateMultipassFurOffsetOS(BurtMultipassFurAttributes input, float layerIndex)
{
    float furAtten = BurtMultipassFurAttenuation(layerIndex);
    float3 furDirectionOS = BurtSafeNormalize(input.normalOS);
    float furLength = 0.05f;

#if defined(BURT_MULTIPASS_FUR_USE_DIRECTION_MAP)
    float2 directionUV = _FlowDirectionUV2 > 0.5f ? input.uv1 : input.uv0;
    float4 directionLength = tex2Dlod(_FlowDirectionMap, float4(directionUV, 0.0f, 0.0f));
    float3 directionTS = directionLength.xyz * 2.0f - 1.0f;
    float3 bentDirectionOS = BurtMultipassFurTangentToObject(directionTS, input.tangentOS, input.normalOS);
    furDirectionOS = BurtSafeNormalize(lerp(furDirectionOS, bentDirectionOS, _FlowDirectionIntensity));
    furLength = directionLength.a;
#endif

    float scale = max(max(abs(_FurScale.x), abs(_FurScale.y)), abs(_FurScale.z));
    scale = max(scale, 0.0001f);
    furLength = clamp(_FurSpacing * scale * 0.01f * furLength, 0.0f, _FurSpacingMax);

    float3 finalFur = BurtSafeNormalize(BurtMultipassFurGravityVector(furAtten) + furDirectionOS) * furAtten * furLength;
    float3 finalOffset = finalFur + BurtSafeNormalize(input.normalOS) * _FurExpand * 0.01f;
    return layerIndex > 0.0f ? finalOffset : float3(0.0f, 0.0f, 0.0f);
}

BurtMultipassFurVaryings VertMultipassFur(BurtMultipassFurAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float layerIndex = BurtMultipassFurLayerIndex();
    float4 positionOS = input.positionOS;
    positionOS.xyz += BurtCalculateMultipassFurOffsetOS(input, layerIndex);

    BurtMultipassFurVaryings output;
    output.positionCS = UnityObjectToClipPos(positionOS);
    output.positionWS = mul(unity_ObjectToWorld, positionOS).xyz;
    output.normalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.normalOS));
    output.geometricNormalWS = output.normalWS;
    output.tangentWS = BurtMultipassFurObjectToWorldTangent(input.tangentOS);
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    output.layerIndex = layerIndex;
    return output;
}

float4 BurtSampleMultipassFurBase(BurtMultipassFurVaryings input)
{
    float2 baseUV = BurtMultipassFurPanner(input.uv0 * _BaseMapPanner.xy, _BaseMapPanner.zw);
    return tex2D(_BaseMap, baseUV);
}

float4 BurtSampleMultipassFurMask(BurtMultipassFurVaryings input)
{
    float2 baseUV = BurtMultipassFurPanner(input.uv0 * _BaseMapPanner.xy, _BaseMapPanner.zw);
    return tex2D(_MaskMap, baseUV);
}

float BurtMultipassFurFlowAlpha(BurtMultipassFurVaryings input, float furAtten, float baseAlpha)
{
    float2 flowUV = (_FlowTexUV2 > 0.5f ? input.uv1 : input.uv0) * _FlowTilling.xx * 0.1f;
    flowUV += BurtMultipassFurPanner(flowUV, _FlowPanner.xy);

    float flowValue = tex2D(_FlowTex, flowUV).r;
    float furAlphaOffset = pow(max(furAtten * 2.0f, 0.0f), 0.8f + _FurTickness);
    furAlphaOffset = pow(max(furAlphaOffset, 0.0f), _FurTicknessCurve);

    float finalAlpha = saturate(flowValue - furAlphaOffset);
    return input.layerIndex == 0.0f ? 1.0f : finalAlpha * baseAlpha;
}

float4 BurtResolveMultipassFurBaseColor(BurtMultipassFurVaryings input, out float4 baseMap, out float4 maskMap, out float furAtten)
{
    baseMap = BurtSampleMultipassFurBase(input);
    maskMap = BurtSampleMultipassFurMask(input);
    furAtten = BurtMultipassFurAttenuation(input.layerIndex);

    float4 baseColor = baseMap * _BaseColor;
    baseColor = lerp(baseColor * lerp(float4(1.0f, 1.0f, 1.0f, 1.0f), _DarkColor, baseColor.a), baseColor, furAtten);
    baseColor.a = BurtMultipassFurFlowAlpha(input, furAtten, baseColor.a);
    return baseColor;
}

void BurtApplyMultipassFurClip(float alpha)
{
    BurtApplyAlphaClip(alpha, _AlphaClip, _Cutoff);
}

BurtSurfaceData BurtCreateMultipassFurSurfaceData(float4 baseColor, float4 baseMap, float4 maskMap, float furAtten)
{
    float roughnessMask = saturate(maskMap.a);
    float smoothness = saturate(1.0f - saturate(_Roughness) * roughnessMask);
    float furMask = baseMap.a > 0.5f ? 1.0f : 0.0f;
    float reflectance = lerp(_Reflectance, saturate(_Reflectance * furAtten), furMask);
    float anisotropy = lerp(0.0f, clamp(_Anisotropy * furAtten, -1.0f, 1.0f), furMask);

    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, reflectance, smoothness, 0.0f);
    surfaceData.occlusion = BurtResolveOcclusion(maskMap, _Occlusion);
    surfaceData.anisotropy = anisotropy;
    surfaceData.height = saturate(maskMap.b);
    return surfaceData;
}

float3 BurtEvaluateMultipassFurEmission(BurtMultipassFurVaryings input, float4 maskMap)
{
    float2 emissionUV = input.uv0;
    float viewSpaceMask = 1.0f;

    if (_EmissiveUseViewSpaceUV > 0.5f)
    {
        float3 pivotVS = mul(UNITY_MATRIX_V, mul(unity_ObjectToWorld, float4(0.0f, 0.0f, 0.0f, 1.0f))).xyz;
        float3 positionVS = mul(UNITY_MATRIX_V, float4(input.positionWS, 1.0f)).xyz - pivotVS;
        emissionUV = BurtMultipassFurPanner(positionVS.xy * _EmissiveTillingPanner.xy, _EmissiveTillingPanner.zw);
        emissionUV += input.geometricNormalWS.xy * _ViewSpaceUVNormalIntensity;
        viewSpaceMask = saturate(maskMap.b);
    }
    else
    {
        emissionUV = input.uv0;
    }

    return tex2D(_EmissiveMap, emissionUV).rgb * _EmissiveColor.rgb * viewSpaceMask;
}

float3 BurtEvaluateMultipassFurRim(BurtMultipassFurVaryings input, float3 viewDirectionWS)
{
    float ndotv = saturate(dot(input.geometricNormalWS, viewDirectionWS));
    return saturate(pow(1.0f - ndotv, _FurRimPower) * _FurRimIntensity).xxx;
}

BurtMultipassFurGBufferOutput BurtPackMultipassFurGBuffer(BurtEncodedGBuffer encodedGBuffer)
{
    BurtMultipassFurGBufferOutput output;
    output.gbuffer0 = encodedGBuffer.gbuffer0;
    output.gbuffer1 = encodedGBuffer.gbuffer1;
    output.gbuffer2 = encodedGBuffer.gbuffer2;
    output.gbuffer3 = encodedGBuffer.gbuffer3;
    output.gbuffer4 = encodedGBuffer.gbuffer4;
    return output;
}

BurtMultipassFurGBufferOutput FragMultipassFurGBuffer(BurtMultipassFurVaryings input, fixed facing : VFACE)
{
    float4 baseMap;
    float4 maskMap;
    float furAtten;
    float4 baseColor = BurtResolveMultipassFurBaseColor(input, baseMap, maskMap, furAtten);
    BurtApplyMultipassFurClip(baseColor.a);

    BurtSurfaceData surfaceData = BurtCreateMultipassFurSurfaceData(baseColor, baseMap, maskMap, furAtten);
    float3 emissionColor = BurtEvaluateMultipassFurEmission(input, maskMap);
    float3 normalWS = BurtGetMultipassFurNormalWS(input, facing);
    BurtGBufferData gbufferData = BurtCreateGBufferData(surfaceData, normalWS, input.tangentWS, emissionColor);
    return BurtPackMultipassFurGBuffer(BurtEncodeGBuffer(gbufferData));
}

float4 FragMultipassFurForward(BurtMultipassFurVaryings input, fixed facing : VFACE) : SV_Target
{
    float4 baseMap;
    float4 maskMap;
    float furAtten;
    float4 baseColor = BurtResolveMultipassFurBaseColor(input, baseMap, maskMap, furAtten);
    BurtApplyMultipassFurClip(baseColor.a);

    float3 normalWS = BurtGetMultipassFurNormalWS(input, facing);
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    BurtSurfaceData surfaceData = BurtCreateMultipassFurSurfaceData(baseColor, baseMap, maskMap, furAtten);
    float shadowAttenuation = BurtSampleMainLightShadow(input.positionWS, normalWS);
    BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);
    BurtPBRShadingComponents pbrComponents = BurtEvaluatePBRShadingComponents(surfaceData, mainLight, normalWS, input.tangentWS, viewDirectionWS, input.positionWS);
    float3 emissionColor = BurtEvaluateMultipassFurEmission(input, maskMap);
    float3 finalColor = pbrComponents.lighting + emissionColor + BurtEvaluateMultipassFurRim(input, viewDirectionWS);

    return float4(BurtApplyPreExposure(finalColor) * surfaceData.alpha, surfaceData.alpha);
}

float4 FragMultipassFurDepth(BurtMultipassFurVaryings input) : SV_Target
{
    float4 baseMap;
    float4 maskMap;
    float furAtten;
    float4 baseColor = BurtResolveMultipassFurBaseColor(input, baseMap, maskMap, furAtten);
    BurtApplyMultipassFurClip(baseColor.a);
    return 0;
}

#endif // BURT_MULTIPASS_FUR_PASS_INCLUDED
