// Forward-only XRender ClearCrystal port for transparent BurtRP materials.
#ifndef BURT_CLEAR_CRYSTAL_PASS_INCLUDED
#define BURT_CLEAR_CRYSTAL_PASS_INCLUDED

#include "UnityCG.cginc"

#define BURT_FORWARD_SINGLE_SHADING_MODEL 1
#define BURT_MATERIAL_SHADING_MODEL_DEFAULT_LIT 1

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtPreExposure.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtNormal.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLighting.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtTransparentAtmosphereFog.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtClearCrystalProperties.hlsl"

sampler2D _BurtOpaqueCameraColorTexture;
UNITY_DECLARE_DEPTH_TEXTURE(_BurtCameraDepthTexture);
float _BurtOpaqueCameraColorAvailable;
float4x4 _BurtTAACurrentViewProjection;
float4x4 _BurtTAACurrentNonJitteredViewProjection;
float4x4 _BurtTAAPreviousNonJitteredViewProjection;
float4x4 unity_MatrixPreviousM;
float4 _BurtTAATexelSize;

struct BurtClearCrystalAttributes
{
    float4 PositionOS : POSITION;
    float3 NormalOS : NORMAL;
    float4 TangentOS : TANGENT;
    float2 UV0 : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BurtClearCrystalVaryings
{
    float4 PositionCS : SV_POSITION;
    float4 ScreenPos : TEXCOORD0;
    float3 PositionWS : TEXCOORD1;
    float3 NormalWS : TEXCOORD2;
    float4 TangentWS : TEXCOORD3;
    float2 UV0 : TEXCOORD4;
#if defined(BURT_TRANSPARENT_VERTEX_FOG) && !defined(BURT_IGNORE_FOG)
    float4 TransparentFog : TEXCOORD5;
#endif
};

struct BurtClearCrystalMaterialData
{
    float4 BaseColor;
    float4 MaskMap;
    float4 TransmissionMask;
    float3 NormalWS;
    float3 GeometryNormalWS;
    float3 InternalColor;
    float3 EmissionColor;
    float Roughness;
};

struct BurtClearCrystalMotionVectorVaryings
{
    float4 PositionCS : SV_POSITION;
    float4 CurrentClipNoJitter : TEXCOORD0;
    float4 PreviousClipNoJitter : TEXCOORD1;
    float2 UV0 : TEXCOORD2;
    float SourceConfidence : TEXCOORD3;
};

BurtClearCrystalVaryings VertClearCrystal(BurtClearCrystalAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    BurtClearCrystalVaryings output;
    output.PositionCS = UnityObjectToClipPos(input.PositionOS);
    output.ScreenPos = ComputeScreenPos(output.PositionCS);
    output.PositionWS = mul(unity_ObjectToWorld, input.PositionOS).xyz;
#if defined(BURT_TRANSPARENT_VERTEX_FOG) && !defined(BURT_IGNORE_FOG)
    float2 transparentFogScreenUV = saturate(
        output.ScreenPos.xy / max(output.ScreenPos.w, BURT_EPSILON));
    output.TransparentFog = BurtEvaluateTransparentFog(
        transparentFogScreenUV,
        output.PositionWS);
#endif
    output.NormalWS = BurtSafeNormalize(UnityObjectToWorldNormal(input.NormalOS));
    output.TangentWS = BurtObjectToWorldTangent(input.TangentOS);
    output.UV0 = input.UV0;
    return output;
}

BurtClearCrystalMotionVectorVaryings VertClearCrystalMotionVector(BurtClearCrystalAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    float4 currentWorld = mul(unity_ObjectToWorld, input.PositionOS);
    float4 previousWorld = mul(unity_MatrixPreviousM, input.PositionOS);
    float3 objectDelta = previousWorld.xyz - currentWorld.xyz;

    BurtClearCrystalMotionVectorVaryings output;
    output.PositionCS = mul(_BurtTAACurrentViewProjection, currentWorld);
    output.CurrentClipNoJitter = mul(_BurtTAACurrentNonJitteredViewProjection, currentWorld);
    output.PreviousClipNoJitter = mul(_BurtTAAPreviousNonJitteredViewProjection, previousWorld);
    output.UV0 = input.UV0;
    output.SourceConfidence = step(1e-8f, dot(objectDelta, objectDelta));
    return output;
}

float BurtClearCrystalMotionVectorAlpha(float2 uv0)
{
    float2 baseUV = uv0 * _BaseMap_ST.xy + _BaseMap_ST.zw + _BaseColorFlowSpeed.xy * (_Time.y * 20.0f);
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, baseUV).a * _BaseColor.a;
}

float2 BurtClearCrystalClipToUv(float4 clipPosition)
{
    float2 uv = clipPosition.xy / max(abs(clipPosition.w), 1.0e-6f) * 0.5f + 0.5f;
    #if UNITY_UV_STARTS_AT_TOP
        uv.y = 1.0f - uv.y;
    #endif
    return uv;
}

float4 FragClearCrystalMotionVector(BurtClearCrystalMotionVectorVaryings input) : SV_Target
{
    BurtApplyAlphaClip(BurtClearCrystalMotionVectorAlpha(input.UV0), _AlphaClip, _Cutoff);
    clip(input.SourceConfidence - 0.5f);

    float2 currentUv = BurtClearCrystalClipToUv(input.CurrentClipNoJitter);
    float2 previousUv = BurtClearCrystalClipToUv(input.PreviousClipNoJitter);
    float valid = step(1.0e-5f, input.PreviousClipNoJitter.w);
    valid *= step(0.0f, currentUv.x) * step(currentUv.x, 1.0f) * step(0.0f, currentUv.y) * step(currentUv.y, 1.0f);
    valid *= step(0.0f, previousUv.x) * step(previousUv.x, 1.0f) * step(0.0f, previousUv.y) * step(previousUv.y, 1.0f);

    float2 velocity = currentUv - previousUv;
    float2 velocityPixels = abs(velocity * _BurtTAATexelSize.zw);
    clip(max(velocityPixels.x, velocityPixels.y) - 0.02f);
    return float4(velocity * valid, 1.0f, 1.0f);
}

float4 FragClearCrystalResponsiveAAMask(BurtClearCrystalMotionVectorVaryings input) : SV_Target
{
    BurtApplyAlphaClip(BurtClearCrystalMotionVectorAlpha(input.UV0), _AlphaClip, _Cutoff);
    clip(_ResponsiveAA - 0.5f);
    return float4(1.0f, 0.0f, 0.0f, 0.0f);
}

float2 BurtClearCrystalRotateUV(float2 uv, float2 pivot, float angle)
{
    float sine;
    float cosine;
    sincos(angle, sine, cosine);
    float2 centeredUV = uv - pivot;
    return float2(
        centeredUV.x * cosine - centeredUV.y * sine,
        centeredUV.x * sine + centeredUV.y * cosine) + pivot;
}

float3 BurtClearCrystalBlendRNM(float3 baseNormalTS, float3 detailNormalTS)
{
    float3 baseTerm = float3(baseNormalTS.xy, baseNormalTS.z + 1.0f);
    float3 detailTerm = float3(-detailNormalTS.xy, detailNormalTS.z);
    return BurtSafeNormalize(baseTerm * (dot(baseTerm, detailTerm) / max(baseTerm.z, BURT_EPSILON)) - detailTerm);
}

float3 BurtClearCrystalSampleParallaxLayer(
    float2 uv,
    float4 tilingOffset,
    float2 flowSpeed,
    float3 channelMask,
    float strength,
    float3 layerColor,
    float baseColorBlend,
    float brightness,
    float3 globalTint,
    float3 viewDirection)
{
    float2 baseUV = uv * tilingOffset.xy + tilingOffset.zw + flowSpeed * (_Time.y * 20.0f);
    float2 viewOffset = strength * (viewDirection.xy / max(viewDirection.z, 0.001f));
    float intensity = dot(SAMPLE_TEXTURE2D(_ParallaxMap, sampler_LinearRepeat, baseUV + viewOffset).rgb, channelMask);
    float3 layer = intensity * layerColor;
    return lerp(layer, layer * globalTint, saturate(baseColorBlend)) * max(brightness, 0.0f);
}

float3 BurtClearCrystalTransformViewDirectionToTangentSpace(float3 viewDirectionWS, float3 normalWS, float4 tangentWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    float3 safeTangentWS = BurtSafeNormalize(tangentWS.xyz);
    safeTangentWS = BurtSafeNormalize(safeTangentWS - safeNormalWS * dot(safeNormalWS, safeTangentWS));
    float3 bitangentWS = cross(safeNormalWS, safeTangentWS) * tangentWS.w;
    return float3(
        dot(viewDirectionWS, safeTangentWS),
        dot(viewDirectionWS, bitangentWS),
        dot(viewDirectionWS, safeNormalWS));
}

float3 BurtClearCrystalGetNormalWS(BurtClearCrystalVaryings input, float facing, float2 baseUV)
{
    float3 baseNormalTS = BurtUnpackNormalScale(
        SAMPLE_TEXTURE2D(_NormalMap, sampler_LinearRepeat, baseUV),
        max(_NormalScale, 0.1f));
    float2 detailUV = BurtClearCrystalRotateUV(input.UV0, float2(0.5f, 0.5f), _DetailNormalRotate);
    detailUV = detailUV * _DetailNormalTiling.xy + _DetailNormalTiling.zw;
    float3 detailNormalTS = BurtUnpackNormalScale(
        SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_LinearRepeat, detailUV),
        max(_DetailNormalScale, 0.0f));
    float3 normalTS = BurtClearCrystalBlendRNM(baseNormalTS, detailNormalTS);
    normalTS = BurtApplyDoubleSidedNormalMode(normalTS, facing, _DoubleSidedNormalModeConstants);
    return BurtTransformTangentToWorld(normalTS, input.NormalWS, input.TangentWS);
}

float3 BurtClearCrystalSampleEmission(BurtClearCrystalVaryings input, float3 normalWS, float4 maskMap)
{
    float2 emissiveUV = input.UV0;
    if (_EmissiveUseViewSpaceUV > 0.5f)
    {
        float3 pivotWS = mul(unity_ObjectToWorld, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;
        float3 positionVS = mul(UNITY_MATRIX_V, float4(input.PositionWS, 1.0f)).xyz;
        float3 pivotVS = mul(UNITY_MATRIX_V, float4(pivotWS, 1.0f)).xyz;
        emissiveUV = (positionVS.xy - pivotVS.xy) * _EmissiveTillingPanner.xy + _EmissiveTillingPanner.zw;
        emissiveUV += normalWS.xy * _ViewSpaceUVNormalIntensity;
    }

    float3 emission = SAMPLE_TEXTURE2D(_EmissiveMap, sampler_LinearRepeat, emissiveUV).rgb * _EmissiveColor.rgb;
    return emission * (_EmissiveUseViewSpaceUV > 0.5f ? maskMap.b : 1.0f);
}

BurtClearCrystalMaterialData BurtBuildClearCrystalMaterialData(BurtClearCrystalVaryings input, float facing)
{
    BurtClearCrystalMaterialData data = (BurtClearCrystalMaterialData)0;
    float2 baseUV = input.UV0 * _BaseMap_ST.xy + _BaseMap_ST.zw + _BaseColorFlowSpeed.xy * (_Time.y * 20.0f);
    float4 globalTint = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, baseUV) * _BaseColor;
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.PositionWS);
    float3 geometryNormalWS = BurtSafeNormalize(input.NormalWS);
    float3 referenceAxis = abs(geometryNormalWS.y) < 0.999f ? float3(0.0f, 1.0f, 0.0f) : float3(1.0f, 0.0f, 0.0f);
    float3 stableTangentWS = BurtSafeNormalize(cross(referenceAxis, geometryNormalWS));
    float3 stableBitangentWS = cross(geometryNormalWS, stableTangentWS);
    float3 viewDirectionNS = float3(
        dot(viewDirectionWS, stableTangentWS),
        dot(viewDirectionWS, stableBitangentWS),
        dot(viewDirectionWS, geometryNormalWS));
    float3 pivotWS = mul(unity_ObjectToWorld, float4(0.0f, 0.0f, 0.0f, 1.0f)).xyz;
    float2 objectSpaceUV = (input.PositionWS - pivotWS).xz;
    float3 viewDirectionTS = BurtClearCrystalTransformViewDirectionToTangentSpace(viewDirectionWS, geometryNormalWS, input.TangentWS);

    float3 layer3 = BurtClearCrystalSampleParallaxLayer(
        _UseObjectSpaceParallax3 > 0.5f ? objectSpaceUV : input.UV0,
        _ParallaxTilingOffset3,
        _ParallaxFlowSpeed3.xy,
        float3(0.0f, 0.0f, 1.0f),
        _ParallaxStrength.z,
        _ParallaxColor3.rgb,
        _ParallaxBaseColorBlend3,
        _ParallaxBrightness3,
        globalTint.rgb,
        _UseObjectSpaceParallax3 > 0.5f ? viewDirectionNS : viewDirectionTS);
    float3 layer2 = BurtClearCrystalSampleParallaxLayer(
        _UseObjectSpaceParallax2 > 0.5f ? objectSpaceUV : input.UV0,
        _ParallaxTilingOffset2,
        _ParallaxFlowSpeed2.xy,
        float3(0.0f, 1.0f, 0.0f),
        _ParallaxStrength.y,
        _ParallaxColor2.rgb,
        _ParallaxBaseColorBlend2,
        _ParallaxBrightness2,
        globalTint.rgb,
        _UseObjectSpaceParallax2 > 0.5f ? viewDirectionNS : viewDirectionTS);
    float3 layer1 = BurtClearCrystalSampleParallaxLayer(
        _UseObjectSpaceParallax1 > 0.5f ? objectSpaceUV : input.UV0,
        _ParallaxTilingOffset1,
        _ParallaxFlowSpeed1.xy,
        float3(1.0f, 0.0f, 0.0f),
        _ParallaxStrength.x,
        _ParallaxColor1.rgb,
        _ParallaxBaseColorBlend1,
        _ParallaxBrightness1,
        globalTint.rgb,
        _UseObjectSpaceParallax1 > 0.5f ? viewDirectionNS : viewDirectionTS);

    data.MaskMap = SAMPLE_TEXTURE2D(_MaskMap, sampler_LinearRepeat, baseUV);
    data.TransmissionMask = SAMPLE_TEXTURE2D(_TransmissionColorMap, sampler_LinearRepeat, input.UV0);
    data.InternalColor = layer3;
    data.BaseColor = float4(layer3, globalTint.a);
    data.GeometryNormalWS = geometryNormalWS;
    data.NormalWS = BurtClearCrystalGetNormalWS(input, facing, baseUV);
    data.EmissionColor = layer1 + layer2 + BurtClearCrystalSampleEmission(input, data.NormalWS, data.MaskMap);
    data.Roughness = saturate(data.MaskMap.a * _Roughness);
    return data;
}

float BurtClearCrystalHenyeyGreenstein(float phaseAnisotropy, float cosine)
{
    float g = clamp(phaseAnisotropy, -0.99f, 0.99f);
    float denominator = max(1.0f + g * g + 2.0f * g * cosine, 1.0e-4f);
    return (1.0f - g * g) / (4.0f * UNITY_PI * denominator * sqrt(denominator));
}

float3 BurtClearCrystalEvaluateTransmissionFromLight(
    BurtLight light,
    float3 normalWS,
    float3 viewDirectionWS,
    float3 extinction,
    float thickness,
    float transmissionWeight,
    float phaseAnisotropy)
{
    if (transmissionWeight <= 1.0e-4f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float3 refractedView = refract(viewDirectionWS, -normalWS, rcp(max(_IOR, 1.0f)));
    if (dot(refractedView, refractedView) <= BURT_EPSILON)
    {
        refractedView = -viewDirectionWS;
    }

    float3 transmittance = exp(-extinction * max(thickness, 0.0f));
    float phase = BurtClearCrystalHenyeyGreenstein(phaseAnisotropy, dot(light.DirectionWS, refractedView));
    return light.Color * light.TransmissionShadowAttenuation * transmittance * phase * transmissionWeight;
}

float3 BurtClearCrystalEvaluateTransmission(
    BurtClearCrystalMaterialData materialData,
    BurtLight mainLight,
    float3 positionWS,
    float3 viewDirectionWS,
    float resolvedThickness)
{
    float transmissionWeight = saturate(materialData.TransmissionMask.g * _Weight);
    float3 transmittanceColor = saturate(materialData.InternalColor * _TransmissionColor.rgb);
    float3 extinction = -log(max(transmittanceColor, float3(1.0e-4f, 1.0e-4f, 1.0e-4f))) / max(_MFPScale, 1.0e-3f);
    float rim = 1.0f - saturate(dot(materialData.GeometryNormalWS, viewDirectionWS));
    float rimFade = saturate(pow(rim, 2.8f) * 2.0f);
    float phaseAnisotropy = lerp(_PhaseAniso, 0.0f, rimFade);

    float3 transmission = BurtClearCrystalEvaluateTransmissionFromLight(
        mainLight,
        materialData.GeometryNormalWS,
        viewDirectionWS,
        extinction,
        resolvedThickness,
        transmissionWeight,
        phaseAnisotropy);

    int additionalLightCount = BurtGetAdditionalLightCount();
    for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
    {
        BurtLight additionalLight = BurtCreateAdditionalLight(lightIndex, positionWS, materialData.GeometryNormalWS);
        transmission += BurtClearCrystalEvaluateTransmissionFromLight(
            additionalLight,
            materialData.GeometryNormalWS,
            viewDirectionWS,
            extinction,
            materialData.TransmissionMask.r * _Thickness,
            transmissionWeight,
            phaseAnisotropy);
    }

    float3 indirectTransmission = BurtSampleIndirectDiffuseIrradiance(-materialData.GeometryNormalWS);
    indirectTransmission *= exp(-extinction * max(materialData.TransmissionMask.r * _Thickness, 0.0f));
    return transmission + indirectTransmission * transmissionWeight * (1.0f / UNITY_PI);
}

float2 BurtClearCrystalScreenUV(BurtClearCrystalVaryings input)
{
    return saturate(input.ScreenPos.xy / max(input.ScreenPos.w, BURT_EPSILON));
}

float BurtClearCrystalLinearEyeDepth(float3 positionWS)
{
    return max(-mul(UNITY_MATRIX_V, float4(positionWS, 1.0f)).z, 1.0e-4f);
}

float2 BurtClearCrystalRefractionOffset(float3 normalWS)
{
    float3 normalVS = BurtSafeNormalize(mul((float3x3)UNITY_MATRIX_V, normalWS));
    float aspect = max(_ScreenParams.x, 1.0f) / max(_ScreenParams.y, 1.0f);
    float2 fieldOfViewFix = float2(UNITY_MATRIX_P._m00, aspect * UNITY_MATRIX_P._m00);
    return normalVS.xy * (_IOR - 1.0f) * fieldOfViewFix * 0.00023f * max(_ScreenParams.x, 1.0f) * saturate(_Refraction);
}

float3 BurtClearCrystalSampleOpaqueColor(float2 screenUV)
{
    return BurtRemovePreExposure(tex2D(_BurtOpaqueCameraColorTexture, screenUV).rgb);
}

float3 BurtClearCrystalSampleRoughRefraction(float2 screenUV, float roughness, float thickness, float sceneDepth)
{
    float radiusPixels = saturate(roughness) * max(thickness, 0.0f) * 16.0f / max(sceneDepth, 0.01f);
    if (radiusPixels <= 1.0e-3f)
    {
        return BurtClearCrystalSampleOpaqueColor(screenUV);
    }

    float2 radiusUV = rcp(max(_ScreenParams.xy, float2(1.0f, 1.0f))) * min(radiusPixels, 32.0f);
    float3 color = BurtClearCrystalSampleOpaqueColor(screenUV) * 0.25f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV + float2(radiusUV.x, 0.0f))) * 0.125f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV - float2(radiusUV.x, 0.0f))) * 0.125f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV + float2(0.0f, radiusUV.y))) * 0.125f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV - float2(0.0f, radiusUV.y))) * 0.125f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV + radiusUV)) * 0.0625f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV - radiusUV)) * 0.0625f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV + float2(radiusUV.x, -radiusUV.y))) * 0.0625f;
    color += BurtClearCrystalSampleOpaqueColor(saturate(screenUV + float2(-radiusUV.x, radiusUV.y))) * 0.0625f;
    return color;
}

void BurtClearCrystalApplyRefraction(
    BurtClearCrystalVaryings input,
    BurtClearCrystalMaterialData materialData,
    inout float3 finalColor,
    inout float outputAlpha)
{
    if (_Refraction <= 1.0e-4f || _BurtOpaqueCameraColorAvailable < 0.5f || outputAlpha <= 1.0e-4f)
    {
        return;
    }

    float2 screenUV = BurtClearCrystalScreenUV(input);
    float2 uvBorder = max(_ScreenParams.zw - 1.0f, float2(0.0f, 0.0f));
    float2 clampedUV = clamp(screenUV, uvBorder, 1.0f - uvBorder);
    float2 offset = BurtClearCrystalRefractionOffset(materialData.NormalWS);
    float2 depthProbeUV = clamp(clampedUV + offset, uvBorder, 1.0f - uvBorder);
    float surfaceDepth = BurtClearCrystalLinearEyeDepth(input.PositionWS);
    float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, depthProbeUV));
    float thickness = max(sceneDepth - surfaceDepth, 0.0f);
    float depthFade = saturate(thickness * 100.0f);
    if (depthFade <= 1.0e-4f)
    {
        return;
    }

    float2 refractedUV = clamp(clampedUV + offset * depthFade, uvBorder, 1.0f - uvBorder);
    float refractedDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, refractedUV));
    float refractedThickness = max(refractedDepth - surfaceDepth, 0.0f);
    float roughRefraction = saturate(materialData.Roughness * (1.0f - saturate(_RoughnessRefractionWeight)));
    float3 refractedColor = BurtClearCrystalSampleRoughRefraction(refractedUV, roughRefraction, refractedThickness, refractedDepth);
    float3 composite = lerp(refractedColor, finalColor, saturate(outputAlpha));
    float refractionBlend = saturate(_Refraction) * depthFade;
    finalColor = lerp(finalColor, composite, refractionBlend);
    outputAlpha = lerp(outputAlpha, 1.0f, refractionBlend);
}

float4 FragClearCrystal(BurtClearCrystalVaryings input, fixed facing : VFACE) : SV_Target
{
    BurtClearCrystalMaterialData materialData = BurtBuildClearCrystalMaterialData(input, facing);
    BurtApplyAlphaClip(materialData.BaseColor.a, _AlphaClip, _Cutoff);

    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.PositionWS);
    float reflectance = _Reflectance * (facing < 0.0f ? 0.5f : 1.0f);
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(
        materialData.BaseColor,
        reflectance,
        1.0f - materialData.Roughness,
        saturate(materialData.MaskMap.r * _Metallic));
    surfaceData.Occlusion = lerp(1.0f, materialData.MaskMap.g, saturate(_Occlusion));
    surfaceData.Height = materialData.MaskMap.b;

    float fallbackThickness = materialData.TransmissionMask.r * _Thickness;
    float shadowAttenuation = BurtSampleMainLightShadow(input.PositionWS, materialData.NormalWS, _BurtPerObjectShadowObjectIndex);
    float transmissionThickness = BurtResolvePerObjectShadowTransmissionThickness(input.PositionWS, fallbackThickness);
    float transmissionShadow = BurtSampleMainLightTransmissionShadow(
        input.PositionWS,
        materialData.NormalWS,
        _BurtPerObjectShadowObjectIndex,
        transmissionThickness);
    BurtLight mainLight = BurtCreateMainLight(shadowAttenuation, transmissionShadow, transmissionThickness);
    BurtPBRShadingComponents pbr = BurtEvaluatePBRShadingComponents(
        surfaceData,
        mainLight,
        materialData.NormalWS,
        viewDirectionWS,
        input.PositionWS);
    float3 transmission = BurtClearCrystalEvaluateTransmission(
        materialData,
        mainLight,
        input.PositionWS,
        viewDirectionWS,
        transmissionThickness);
    float3 finalColor = pbr.Lighting + materialData.EmissionColor + transmission;
    float outputAlpha = surfaceData.Alpha;
    BurtClearCrystalApplyRefraction(input, materialData, finalColor, outputAlpha);
#if !defined(BURT_IGNORE_FOG)
#if defined(BURT_TRANSPARENT_VERTEX_FOG)
    finalColor = BurtBlendTransparentFog(finalColor, input.TransparentFog);
#else
    finalColor = BurtApplyTransparentFog(
        finalColor,
        BurtClearCrystalScreenUV(input),
        input.PositionWS);
#endif
#endif
    return float4(BurtApplyPreExposure(finalColor), outputAlpha);
}

float4 FragClearCrystalDistortion(BurtClearCrystalVaryings input, fixed facing : VFACE) : SV_Target
{
    clip(_Refraction - 1.0e-4f);
    BurtClearCrystalMaterialData materialData = BurtBuildClearCrystalMaterialData(input, facing);
    BurtApplyAlphaClip(materialData.BaseColor.a, _AlphaClip, _Cutoff);

    float2 screenUV = BurtClearCrystalScreenUV(input);
    float2 uvBorder = max(_ScreenParams.zw - 1.0f, float2(0.0f, 0.0f));
    float2 offset = BurtClearCrystalRefractionOffset(materialData.NormalWS);
    float surfaceDepth = BurtClearCrystalLinearEyeDepth(input.PositionWS);
    float sceneDepth = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_BurtCameraDepthTexture, clamp(screenUV + offset, uvBorder, 1.0f - uvBorder)));
    float depthFade = saturate((sceneDepth - surfaceDepth) * 100.0f);
    float roughRefraction = saturate(materialData.Roughness * (1.0f - saturate(_RoughnessRefractionWeight)));
    return float4(offset * depthFade * 4.0f, roughRefraction, surfaceDepth);
}

struct BurtClearCrystalShadowVaryings
{
    float4 PositionCS : SV_POSITION;
    float2 UV0 : TEXCOORD0;
};

BurtClearCrystalShadowVaryings VertClearCrystalShadow(BurtClearCrystalAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);

    BurtClearCrystalShadowVaryings output;
    output.PositionCS = UnityObjectToClipPos(input.PositionOS);
    output.UV0 = input.UV0;
    return output;
}

float4 FragClearCrystalShadow(BurtClearCrystalShadowVaryings input) : SV_Target
{
    float2 baseUV = input.UV0 * _BaseMap_ST.xy + _BaseMap_ST.zw + _BaseColorFlowSpeed.xy * (_Time.y * 20.0f);
    float shadowAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_LinearRepeat, baseUV).a * _BaseColor.a * _ShadowIntensity;
    BurtApplyAlphaClip(shadowAlpha, _AlphaClip, _Cutoff);
    return 0.0f;
}

#endif // BURT_CLEAR_CRYSTAL_PASS_INCLUDED
