// BurtRP runtime shader port of Assets/Res/Shader/MaterialModel/MM_CH_MultiPassFur.hlsl.
#ifndef BURT_MULTIPASS_FUR_PASS_INCLUDED
#define BURT_MULTIPASS_FUR_PASS_INCLUDED

#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#define BURT_FORWARD_SINGLE_SHADING_MODEL 1
#define BURT_MATERIAL_GBUFFER_SINGLE_SHADING_MODEL 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtGBuffer.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

Texture2D _FlowTex;
Texture2D _FlowDirectionMap;
Texture2D _EmissiveMap;
Texture2DArray _FlowDirectionMapSegmentArray;

float4x4 _BurtFurBlurCurrentNonJitteredViewProjection;
float4x4 _BurtFurBlurPreviousNonJitteredViewProjection;
float4x4 _BurtFurBlurPreviousObjectToWorld;
float4 _BurtFurBlurScreenSize;

static const float BURT_MULTIPASS_FUR_REFERENCE_LAYER_COUNT = 16.0f;
static const float BURT_TWO_PI = 6.28318530717958647692f;
static const float BURT_INV_TWO_PI = 0.15915494309189535f;

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
    float3 furDirectionWS : TEXCOORD7;
};

struct BurtMultipassFurGBufferOutput
{
    float4 gbuffer0 : SV_Target0;
    float4 gbuffer1 : SV_Target1;
    float4 gbuffer2 : SV_Target2;
    float4 gbuffer3 : SV_Target3;
    float4 gbuffer4 : SV_Target4;
    float4 objectIndex : SV_Target5;
};

struct BurtMultipassFurVelocityVaryings
{
    float4 positionCS : SV_POSITION;
    float4 currentClipNoJitter : TEXCOORD0;
    float4 previousClipNoJitter : TEXCOORD1;
    float2 uv0 : TEXCOORD2;
    float2 uv1 : TEXCOORD3;
    float layerIndex : TEXCOORD4;
    float velocityValid : TEXCOORD5;
};

float4 BurtEncodeMultipassFurPerObjectShadowObjectIndexTarget()
{
    return float4(saturate((float)max(_BurtPerObjectShadowObjectIndex, 0) / 255.0f), 0.0f, 0.0f, 1.0f);
}

float BurtMultipassFurLayerIndex()
{
    return (float)BurtGetCurrentInstanceID();
}

float BurtEncodeMultipassFurDir(float2 furDirection)
{
    float angle = atan2(furDirection.y, furDirection.x);
    angle += angle < 0.0f ? BURT_TWO_PI : 0.0f;
    return angle * BURT_INV_TWO_PI;
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

float4 BurtSampleMultipassFurDirectionLength(float2 directionUV, int segmentIndex)
{
    if (_UseDirectionMapSegment > 0.5f)
    {
        return BURT_SAMPLE_TEXTURE2D_ARRAY_LOD_REPEAT(_FlowDirectionMapSegmentArray, directionUV, segmentIndex, 0.0f);
    }

    return _FlowDirectionMap.SampleLevel(sampler_LinearRepeat, directionUV, 0.0f);
}

void BurtCalculateMultipassFurDirectionLength(
    BurtMultipassFurAttributes input,
    float2 directionUV,
    int segmentIndex,
    float intensity,
    out float3 furDirectionOS,
    out float furLength)
{
    float4 directionLength = BurtSampleMultipassFurDirectionLength(directionUV, segmentIndex);
    float3 directionTS = directionLength.xyz * 2.0f - 1.0f;
    float3 bentDirectionOS = BurtMultipassFurTangentToObject(directionTS, input.tangentOS, input.normalOS);
    furDirectionOS = BurtSafeNormalize(lerp(BurtSafeNormalize(input.normalOS), bentDirectionOS, intensity));
    furLength = directionLength.a;
}

float4 BurtMultipassFurObjectToWorldTangent(float4 tangentOS)
{
    float3 tangentWS = BurtSafeNormalize(UnityObjectToWorldDir(tangentOS.xyz));
    return float4(tangentWS, tangentOS.w * unity_WorldTransformParams.w);
}

float3 BurtGetMultipassFurNormalWS(BurtMultipassFurVaryings input, float facing)
{
    return BurtSampleNormalWS(input.uv0, input.normalWS, input.tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);
}

float3 BurtGetMultipassFurGeometryNormalWS(BurtMultipassFurVaryings input, float facing)
{
    float3 normalWS = input.geometricNormalWS;
    if (facing < 0.0f)
    {
        normalWS *= _DoubleSidedNormalModeConstants.z;
    }

    return BurtSafeNormalize(normalWS);
}

float3 BurtGetMultipassFurShadingDirectionWS(BurtMultipassFurVaryings input)
{
    float3 furDirectionWS = BurtSafeNormalize(input.furDirectionWS);
    return dot(furDirectionWS, furDirectionWS) > BURT_EPSILON ? furDirectionWS : BurtSafeNormalize(input.tangentWS.xyz);
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

bool BurtShouldUseMultipassFurDirectionMap()
{
    return _UseDirectionMap > 0.5f || _UseDirectionMapSegment > 0.5f;
}

float3 BurtCalculateMultipassFurOffsetOS(BurtMultipassFurAttributes input, float layerIndex, out float3 furDirectionForShadingOS)
{
    float furAtten = BurtMultipassFurAttenuation(layerIndex);
    float3 furDirectionOS = BurtSafeNormalize(input.normalOS);
    float3 furDirectionOS1 = furDirectionOS;
    float3 furDirectionOS2 = furDirectionOS;
    float furLength = 0.05f;
    float furLength1 = 0.05f;
    float furLength2 = 0.05f;

    if (BurtShouldUseMultipassFurDirectionMap())
    {
        float2 directionUV = _FlowDirectionUV2 > 0.5f ? input.uv1 : input.uv0;
        if (_UseDirectionMapSegment > 0.5f)
        {
            BurtCalculateMultipassFurDirectionLength(input, directionUV, 0, _FlowDirectionIntensitySegment1, furDirectionOS, furLength);
            BurtCalculateMultipassFurDirectionLength(input, directionUV, 1, _FlowDirectionIntensitySegment2, furDirectionOS1, furLength1);
            BurtCalculateMultipassFurDirectionLength(input, directionUV, 2, _FlowDirectionIntensitySegment3, furDirectionOS2, furLength2);
        }
        else
        {
            BurtCalculateMultipassFurDirectionLength(input, directionUV, 0, _FlowDirectionIntensity, furDirectionOS, furLength);
        }
    }

    float scale = max(max(abs(_FurScale.x), abs(_FurScale.y)), abs(_FurScale.z));
    scale = max(scale, 0.0001f);
    furLength = clamp(_FurSpacing * scale * 0.01f * furLength, 0.0f, _FurSpacingMax);
    furLength1 = clamp(_FurSpacing * scale * 0.01f * furLength1, 0.0f, _FurSpacingMax);
    furLength2 = clamp(_FurSpacing * scale * 0.01f * furLength2, 0.0f, _FurSpacingMax);

    float3 finalFur = BurtSafeNormalize(BurtMultipassFurGravityVector(furAtten) + furDirectionOS) * furAtten * furLength;
    if (BurtShouldUseMultipassFurDirectionMap() && _UseDirectionMapSegment > 0.5f)
    {
        float totalLength = max(furLength + furLength1 + furLength2, 0.0001f);
        float segmentBoundary01 = furLength / totalLength;
        float segmentBoundary12 = (furLength + furLength1) / totalLength;
        float normalizedLayer = layerIndex / max(_FurMaxCount, 1.0f);
        float t0 = saturate(normalizedLayer / max(segmentBoundary01, 0.0001f));
        float t1 = saturate((normalizedLayer - segmentBoundary01) / max(segmentBoundary12 - segmentBoundary01, 0.0001f));
        float t2 = saturate((normalizedLayer - segmentBoundary12) / max(1.0f - segmentBoundary12, 0.0001f));
        finalFur = (
            BurtSafeNormalize(furDirectionOS) * furLength * t0 +
            BurtSafeNormalize(furDirectionOS1) * furLength1 * t1 +
            BurtSafeNormalize(furDirectionOS2) * furLength2 * t2) * furAtten;
        furDirectionOS = BurtSafeNormalize(
            BurtSafeNormalize(furDirectionOS) * max(furLength, BURT_EPSILON) +
            BurtSafeNormalize(furDirectionOS1) * max(furLength1, BURT_EPSILON) +
            BurtSafeNormalize(furDirectionOS2) * max(furLength2, BURT_EPSILON));
    }

    float3 finalOffset = finalFur + BurtSafeNormalize(input.normalOS) * _FurExpand * 0.01f;
    furDirectionForShadingOS = dot(finalFur, finalFur) > BURT_EPSILON ? BurtSafeNormalize(finalFur) : furDirectionOS;
    return layerIndex > 0.0f ? finalOffset : float3(0.0f, 0.0f, 0.0f);
}

BurtMultipassFurVaryings VertMultipassFur(BurtMultipassFurAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float layerIndex = BurtMultipassFurLayerIndex();
    float4 positionOS = input.positionOS;
    float3 furDirectionOS;
    positionOS.xyz += BurtCalculateMultipassFurOffsetOS(input, layerIndex, furDirectionOS);

    BurtMultipassFurVaryings output;
    output.positionCS = UnityObjectToClipPos(positionOS);
    output.positionWS = mul(unity_ObjectToWorld, positionOS).xyz;
    output.normalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.normalOS));
    output.geometricNormalWS = output.normalWS;
    output.tangentWS = BurtMultipassFurObjectToWorldTangent(input.tangentOS);
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    output.layerIndex = layerIndex;
    output.furDirectionWS = BurtSafeNormalize(UnityObjectToWorldDir(furDirectionOS));
    return output;
}

float2 BurtMultipassFurClipToUv(float4 clipPosition)
{
    float2 uv = clipPosition.xy / max(abs(clipPosition.w), 1e-6);
    uv = uv * 0.5f + 0.5f;
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1.0f - uv.y;
    #endif
    return uv;
}

BurtMultipassFurVelocityVaryings VertMultipassFurVelocity(BurtMultipassFurAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float layerIndex = BurtMultipassFurLayerIndex();
    float4 positionOS = input.positionOS;
    float3 furDirectionOS;
    positionOS.xyz += BurtCalculateMultipassFurOffsetOS(input, layerIndex, furDirectionOS);

    float4 currentWorld = mul(unity_ObjectToWorld, positionOS);
    float4 previousWorld = mul(_BurtFurBlurPreviousObjectToWorld, positionOS);

    BurtMultipassFurVelocityVaryings output;
    output.positionCS = UnityObjectToClipPos(positionOS);
    output.currentClipNoJitter = mul(_BurtFurBlurCurrentNonJitteredViewProjection, currentWorld);
    output.previousClipNoJitter = mul(_BurtFurBlurPreviousNonJitteredViewProjection, previousWorld);
    output.uv0 = input.uv0;
    output.uv1 = input.uv1;
    output.layerIndex = layerIndex;
    output.velocityValid = step(0.5f, layerIndex);
    return output;
}

float4 BurtSampleMultipassFurBase(BurtMultipassFurVaryings input)
{
    float2 baseUV = BurtMultipassFurPanner(input.uv0 * _BaseMapPanner.xy, _BaseMapPanner.zw);
    return BURT_SAMPLE_TEXTURE2D_REPEAT(_BaseMap, baseUV);
}

float4 BurtSampleMultipassFurMask(BurtMultipassFurVaryings input)
{
    float2 baseUV = BurtMultipassFurPanner(input.uv0 * _BaseMapPanner.xy, _BaseMapPanner.zw);
    return BURT_SAMPLE_TEXTURE2D_REPEAT(_MaskMap, baseUV);
}

float BurtMultipassFurFlowAlpha(BurtMultipassFurVaryings input, float furAtten)
{
    float2 flowUV = (_FlowTexUV2 > 0.5f ? input.uv1 : input.uv0) * _FlowTilling.xx * 0.1f;
    flowUV += BurtMultipassFurPanner(flowUV, _FlowPanner.xy);

    float flowValue = BURT_SAMPLE_TEXTURE2D_REPEAT(_FlowTex, flowUV).r;
    float furAlphaOffset = pow(max(furAtten * 2.0f, 0.0f), 0.8f + _FurTickness);
    furAlphaOffset = pow(max(furAlphaOffset, 0.0f), _FurTicknessCurve);

    float finalAlpha = saturate(flowValue - furAlphaOffset);
    return input.layerIndex == 0.0f ? 1.0f : finalAlpha;
}

float BurtMultipassFurHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031f);
    p3 += dot(p3, p3.yzx + 33.33f);
    return frac((p3.x + p3.y) * p3.z);
}

float BurtEvaluateMultipassFurDitheredAlpha(BurtMultipassFurVaryings input, float alpha, float baseAlpha)
{
    if (input.layerIndex == 0.0f)
    {
        return 1.0f;
    }

    float dither = BurtMultipassFurHash12(floor(input.uv0 * 8192.0f + input.layerIndex * float2(17.0f, 31.0f)));
    return (alpha > dither ? 1.0f : 0.0f) * baseAlpha;
}

float4 BurtResolveMultipassFurBaseColor(BurtMultipassFurVaryings input, out float4 baseMap, out float4 maskMap, out float furAtten)
{
    baseMap = BurtSampleMultipassFurBase(input);
    maskMap = BurtSampleMultipassFurMask(input);
    furAtten = BurtMultipassFurAttenuation(input.layerIndex);

    float4 baseColor = baseMap * _BaseColor;
    baseColor = lerp(baseColor * lerp(float4(1.0f, 1.0f, 1.0f, 1.0f), _DarkColor, baseColor.a), baseColor, furAtten);
    float baseAlpha = baseColor.a;
    float flowAlpha = BurtMultipassFurFlowAlpha(input, furAtten);
    baseColor.a = BurtEvaluateMultipassFurDitheredAlpha(input, flowAlpha, baseAlpha);
    return baseColor;
}

void BurtApplyMultipassFurClip(float alpha, float4 positionCS)
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
    surfaceData.hairSecondaryRoughness = ClampPerceptualRoughness(saturate(_Roughness));
    surfaceData.hairBackLight = saturate(_FurRimIntensity * 0.2f);
    surfaceData.hairShadowFillStrength = saturate(_FurRimIntensity * 0.1f);
    surfaceData.hairSpecularShift = 0.0f;
    surfaceData.hairSecondarySpecularShift = 0.0f;
    surfaceData.hairSpecularColor = float3(1.0f, 1.0f, 1.0f);
    surfaceData.hairSecondarySpecularColor = float3(1.0f, 1.0f, 1.0f);
    return BurtApplyHairGBufferSurfaceSemantics(surfaceData, saturate(furAtten), 1.0f);
}

float3 BurtEvaluateMultipassFurEmission(BurtMultipassFurVaryings input, float4 maskMap)
{
    if (max(max(_EmissiveColor.r, _EmissiveColor.g), _EmissiveColor.b) <= 0.0f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

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

    return BURT_SAMPLE_TEXTURE2D_REPEAT(_EmissiveMap, emissionUV).rgb * _EmissiveColor.rgb * viewSpaceMask;
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
    output.objectIndex = BurtEncodeMultipassFurPerObjectShadowObjectIndexTarget();
    return output;
}

BurtMultipassFurGBufferOutput FragMultipassFurGBuffer(BurtMultipassFurVaryings input, fixed facing : VFACE)
{
    float4 baseMap;
    float4 maskMap;
    float furAtten;
    float4 baseColor = BurtResolveMultipassFurBaseColor(input, baseMap, maskMap, furAtten);
    BurtApplyMultipassFurClip(baseColor.a, input.positionCS);

    BurtSurfaceData surfaceData = BurtCreateMultipassFurSurfaceData(baseColor, baseMap, maskMap, furAtten);
    float3 emissionColor = BurtEvaluateMultipassFurEmission(input, maskMap);
    float3 normalWS = BurtGetMultipassFurNormalWS(input, facing);
    float3 geometryNormalWS = BurtGetMultipassFurGeometryNormalWS(input, facing);
    float3 shadingDirectionWS = BurtGetMultipassFurShadingDirectionWS(input);
    BurtGBufferData gbufferData = BurtCreateHairGBufferData(surfaceData, shadingDirectionWS, normalWS, geometryNormalWS, emissionColor);
    return BurtPackMultipassFurGBuffer(BurtEncodeGBuffer(gbufferData));
}

float4 FragMultipassFurForward(BurtMultipassFurVaryings input, fixed facing : VFACE) : SV_Target
{
    float4 baseMap;
    float4 maskMap;
    float furAtten;
    float4 baseColor = BurtResolveMultipassFurBaseColor(input, baseMap, maskMap, furAtten);
    BurtApplyMultipassFurClip(baseColor.a, input.positionCS);

    float3 normalWS = BurtGetMultipassFurNormalWS(input, facing);
    float3 geometryNormalWS = BurtGetMultipassFurGeometryNormalWS(input, facing);
    float3 shadingDirectionWS = BurtGetMultipassFurShadingDirectionWS(input);
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    BurtSurfaceData surfaceData = BurtCreateMultipassFurSurfaceData(baseColor, baseMap, maskMap, furAtten);
    float shadowAttenuation = BurtSampleMainLightShadow(input.positionWS, normalWS);
    BurtLight mainLight = BurtCreateMainLight(shadowAttenuation);
    BurtGBufferData hairGBufferData = BurtCreateHairGBufferData(surfaceData, shadingDirectionWS, normalWS, geometryNormalWS, float3(0.0f, 0.0f, 0.0f));
    BurtPBRShadingComponents pbrComponents = BurtEvaluateHairShadingComponentsFromGBuffer(hairGBufferData, mainLight, viewDirectionWS, input.positionWS);
    float3 emissionColor = BurtEvaluateMultipassFurEmission(input, maskMap);
    float3 finalColor = pbrComponents.lighting + emissionColor + BurtEvaluateMultipassFurRim(input, viewDirectionWS);

    return float4(BurtApplyPreExposure(finalColor), surfaceData.alpha);
}

float4 FragMultipassFurDepth(BurtMultipassFurVaryings input) : SV_Target
{
    float4 baseMap;
    float4 maskMap;
    float furAtten;
    float4 baseColor = BurtResolveMultipassFurBaseColor(input, baseMap, maskMap, furAtten);
    BurtApplyMultipassFurClip(baseColor.a, input.positionCS);
    return 0;
}

float4 FragMultipassFurBlurProperty(BurtMultipassFurVaryings input) : SV_Target
{
    float4 baseMap;
    float4 maskMap;
    float furAtten;
    float4 baseColor = BurtResolveMultipassFurBaseColor(input, baseMap, maskMap, furAtten);
    BurtApplyMultipassFurClip(baseColor.a, input.positionCS);

    float theta = 0.0f;
    if (_FurBlurEnabled > 0.5f && input.layerIndex > 0.5f)
    {
        float3 positionVS = mul(UNITY_MATRIX_V, float4(input.positionWS, 1.0f)).xyz;
        float distanceToCamera = length(positionVS);
        float3 directionVS = mul((float3x3)UNITY_MATRIX_V, BurtGetMultipassFurShadingDirectionWS(input));
        float2 directionSS = directionVS.xy;
        float directionLengthSquared = dot(directionSS, directionSS);
        if (distanceToCamera < max(_FurBlurDistance, 0.0f) && directionLengthSquared > BURT_EPSILON)
        {
            theta = BurtEncodeMultipassFurDir(directionSS * rsqrt(directionLengthSquared));
        }
    }

    return float4(theta, input.positionCS.z, 0.0f, 1.0f);
}

float4 FragMultipassFurBlurVelocity(BurtMultipassFurVelocityVaryings input) : SV_Target
{
    float4 baseMap = BURT_SAMPLE_TEXTURE2D_REPEAT(_BaseMap, BurtMultipassFurPanner(input.uv0 * _BaseMapPanner.xy, _BaseMapPanner.zw));
    float furAtten = BurtMultipassFurAttenuation(input.layerIndex);
    float4 baseColor = baseMap * _BaseColor;
    baseColor = lerp(baseColor * lerp(float4(1.0f, 1.0f, 1.0f, 1.0f), _DarkColor, baseColor.a), baseColor, furAtten);
    float flowUVSource = _FlowTexUV2 > 0.5f ? 1.0f : 0.0f;
    float2 flowUV = lerp(input.uv0, input.uv1, flowUVSource) * _FlowTilling.xx * 0.1f;
    flowUV += BurtMultipassFurPanner(flowUV, _FlowPanner.xy);
    float flowValue = BURT_SAMPLE_TEXTURE2D_REPEAT(_FlowTex, flowUV).r;
    float furAlphaOffset = pow(max(furAtten * 2.0f, 0.0f), 0.8f + _FurTickness);
    furAlphaOffset = pow(max(furAlphaOffset, 0.0f), _FurTicknessCurve);
    float flowAlpha = input.layerIndex == 0.0f ? 1.0f : saturate(flowValue - furAlphaOffset);
    float dither = BurtMultipassFurHash12(floor(input.uv0 * 8192.0f + input.layerIndex * float2(17.0f, 31.0f)));
    baseColor.a = (input.layerIndex == 0.0f ? 1.0f : (flowAlpha > dither ? 1.0f : 0.0f)) * baseColor.a;
    BurtApplyMultipassFurClip(baseColor.a, input.positionCS);

    clip(_FurBlurEnabled - 0.5f);
    clip(input.velocityValid - 0.5f);

    float valid = step(1e-5f, input.currentClipNoJitter.w) * step(1e-5f, input.previousClipNoJitter.w);
    float2 currentUv = BurtMultipassFurClipToUv(input.currentClipNoJitter);
    float2 previousUv = BurtMultipassFurClipToUv(input.previousClipNoJitter);
    valid *= step(0.0f, currentUv.x) * step(currentUv.x, 1.0f) * step(0.0f, currentUv.y) * step(currentUv.y, 1.0f);
    valid *= step(0.0f, previousUv.x) * step(previousUv.x, 1.0f) * step(0.0f, previousUv.y) * step(previousUv.y, 1.0f);

    float2 velocity = previousUv - currentUv;
    float2 velocityPixels = abs(velocity * _BurtFurBlurScreenSize.xy);
    float keepVelocity = step(0.02f, max(velocityPixels.x, velocityPixels.y));
    return float4(velocity * valid * keepVelocity, valid, 1.0f);
}

#endif // BURT_MULTIPASS_FUR_PASS_INCLUDED
