// Shared material shading-model helpers used by GBuffer and Forward passes.
#ifndef BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
#define BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairDither.hlsl"

float4 BurtEvaluateMaterialPassMaskMap(float2 uv0, float2 uv1)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtSampleMaskMap(uv1);
#else
    return BurtSampleMaskMap(BurtTransformMaskMapUV(uv0, _MaskMap_ST));
#endif
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
float BurtAvatarHairGradientFactor(float2 uv0, float3 positionOS)
{
    float gradientFactor = uv0.y;
    float3 gradientDirection = BurtSafeNormalize(_GradientDirection.xyz);
    float positionGradientFactor = dot(positionOS + _GradientPosOffset.xyz, gradientDirection);
    gradientFactor = _RootGradientPosEnable > 0.5f ? positionGradientFactor : gradientFactor;
    return _RootGradientReverse > 0.5f ? 1.0f - gradientFactor : gradientFactor;
}

float3 BurtAvatarHairApplyGradientMap(float3 baseColor, float gradientMask)
{
    float gradientV = (_GradientRowIndex + 0.5f) * max(_GradientMap_TexelSize.y, BURT_EPSILON);
    float3 gradientColor = BURT_SAMPLE_TEXTURE2D_CLAMP(_GradientMap, float2(saturate(gradientMask), saturate(gradientV))).rgb;
    float3 blendSoftLight = (1.0f - 2.0f * gradientColor) * baseColor * baseColor + 2.0f * gradientColor * baseColor;
    float3 blendOverlay = lerp(
        2.0f * baseColor * gradientColor,
        1.0f - 2.0f * (1.0f - baseColor) * (1.0f - gradientColor),
        step(0.5f, baseColor));

    float3 result = baseColor;
    result += (blendSoftLight * 1.05f - baseColor) * _GradientSoftLight;
    result += (blendOverlay - baseColor) * _GradientOverlay;
    return lerp(result, gradientColor, _GradientReplace);
}

float3 BurtAvatarHairStructureFactor(float hairStructureMask)
{
    float hairShadowMask = saturate((1.0f - hairStructureMask) * _HairShadowIntensity);
    float3 hairBrightFactor = max(_HairBrightColor.rgb * _HairBrightIntensity, float3(0.0f, 0.0f, 0.0f));
    float3 hairShadowFactor = max(lerp(float3(0.0f, 0.0f, 0.0f), hairBrightFactor, hairStructureMask), float3(0.0f, 0.0f, 0.0f));
    return max(lerp(hairShadowFactor, _HairShadowColor.rgb, hairShadowMask), float3(0.0f, 0.0f, 0.0f));
}
#endif

float4 BurtEvaluateMaterialPassBaseColor(float2 uv0, float2 uv1, float3 positionOS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float4 idValue = BURT_SAMPLE_TEXTURE2D_REPEAT(_IDMap, uv0 * float2(_IDXTilling, 1.0f));
    float4 baseMap = BurtSampleBaseMap(uv0);
    float4 maskMap = BurtEvaluateMaterialPassMaskMap(uv0, uv1);
    float gradientFactor = BurtAvatarHairGradientFactor(uv0, positionOS);

    float rootGradient = saturate(smoothstep(_RootGradient.x, _RootGradient.y, gradientFactor));
    float4 baseColor = _BaseColor;
    baseColor.rgb = lerp(_BaseColor.rgb, _RootColor.rgb, rootGradient * _RootGradientEnable);
    baseColor *= baseMap;

    if (_GradientColorEnable > 0.5f)
    {
        baseColor.rgb = BurtAvatarHairApplyGradientMap(baseColor.rgb, idValue.a);
    }

    float3 hairShadowFactor = BurtAvatarHairStructureFactor(saturate(idValue.g));
    baseColor.rgb = lerp(baseColor.rgb, baseColor.rgb * hairShadowFactor, _HairShadowPower);
    baseColor.rgb = lerp(baseColor.rgb * _AlbedoOcclusionColor.rgb, baseColor.rgb, lerp(1.0f, maskMap.g, _AlbedoOcclusion));
    return baseColor;
#else
    return BurtSampleBaseMap(BurtTransformBaseMapUV(uv0, _BaseMap_ST)) * _BaseColor;
#endif
}

float BurtEvaluateMaterialPassRegularOpacity(float alpha, float cutoff)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return alpha - saturate(cutoff);
#else
    return alpha;
#endif
}

float BurtEvaluateMaterialPassRegularOpacity(float alpha)
{
    return BurtEvaluateMaterialPassRegularOpacity(alpha, _Cutoff);
}

float BurtEvaluateMaterialPassShadowOpacity(float alpha)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return saturate(alpha - saturate(_ShadowCutOff));
#else
    return alpha;
#endif
}

void BurtApplyMaterialPassAlphaClip(float alpha, float alphaClip, float cutoff)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtApplyAlphaClip(BurtEvaluateMaterialPassRegularOpacity(alpha, cutoff), alphaClip, 0.0f);
#else
    BurtApplyAlphaClip(alpha, alphaClip, cutoff);
#endif
}

void BurtApplyMaterialPassAlphaClip(float alpha, float alphaClip, float cutoff, float4 positionCS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtApplyHairDitherAlphaClip(alpha, alphaClip, cutoff, positionCS);
#else
    BurtApplyAlphaClip(alpha, alphaClip, cutoff);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(float4 baseColor, float4 maskMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float hairReflectance = saturate(_Reflectance * _HairSpecularScale);
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, hairReflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    surfaceData.smoothness = saturate(surfaceData.smoothness - _HairRoughnessOffset);
    float hairShiftScale = saturate(_HairShiftScale * maskMap.b);
    return BurtApplyHairGBufferSurfaceSemantics(surfaceData, (_HairScatter + _HairScatterBoost) * maskMap.r, hairShiftScale);
#else
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, _Metallic, maskMap, _OcclusionStrength);

    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
        surfaceData = BurtApplyAnisotropySurfaceSemantics(surfaceData, _Anisotropy);
        surfaceData = BurtApplyClearCoatSurfaceSemantics(surfaceData, _ClearCoatMask, _ClearCoatRoughness);
    #elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
        float subsurface3SCurvature = saturate(maskMap.g * _Subsurface3SCurvatureScale + _Subsurface3SCurvatureBias);
        surfaceData = BurtApplySubsurfaceSurfaceSemantics(surfaceData, _SubsurfaceThickness, _SubsurfacePower, _SubsurfaceDistortion, _SubsurfaceAmbient, subsurface3SCurvature, _SubsurfaceProfileIndex, _SubsurfaceScatteringMode);
    #else
        surfaceData = BurtApplyAnisotropySurfaceSemantics(surfaceData, _Anisotropy);
    #endif

    return surfaceData;
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(
    float4 baseColor,
    float4 maskMap,
    float2 uv0,
    float2 uv1,
    float3 positionOS,
    float3 geometryNormalWS,
    float4 tangentWS,
    float3 viewDirectionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float4 idValue = BURT_SAMPLE_TEXTURE2D_REPEAT(_IDMap, uv0 * float2(_IDXTilling, 1.0f));
    float nDotV = saturate(dot(BurtSafeNormalize(geometryNormalWS), viewDirectionWS));
    float edgeRoughness = lerp(0.0f, _RoughParameter.z, saturate(pow(1.0f - nDotV, _EdgeRoughRimPower)));
    float2 roughness = saturate(_RoughParameter.xy + edgeRoughness.xx);
    float scatter = _ScatterUseFullRange > 0.33f ? _ScatterFull : _Scatter;
    scatter = max(scatter, (_HairScatter + _HairScatterBoost) * maskMap.r);

    float3 hairShadowFactor = BurtAvatarHairStructureFactor(saturate(idValue.g));
    float reflectance = maskMap.r * _Reflectance * _HairSpecularScale;
    reflectance = lerp(reflectance, reflectance * PerceivedLuminance(hairShadowFactor), _HairShadowPower);

    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, reflectance, 1.0f - roughness.x, 0.0f, float4(maskMap.r, maskMap.g, maskMap.b, 1.0f), _Occlusion);
    surfaceData.occlusion = lerp(1.0f, maskMap.g, _Occlusion);
    surfaceData.height = saturate(maskMap.b);
    surfaceData.hairSecondaryRoughness = roughness.y;
    surfaceData.hairBackLight = (_BackLightIntensity - baseColor.a * _BackLightIntensity) *
        lerp(1.0f, pow(saturate(1.0f - nDotV), rcp(max(BURT_EPSILON, _BackLightMaskRange))), _BackLightMask);
    surfaceData.hairShadowFillStrength = _HairShadowFillStrength;
    surfaceData.hairSpecularShift = _SpecularShift * 1.98f + 1.36f;
    surfaceData.hairSecondarySpecularShift = _SecondarySpecularShift * 3.33f + 1.56f;
    surfaceData.hairSpecularColor = _SpecularColor.rgb;
    surfaceData.hairSecondarySpecularColor = _SpecularSecondColor.rgb;
    return BurtApplyHairGBufferSurfaceSemantics(surfaceData, scatter, 1.0f);
#else
    return BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap);
#endif
}

float3 BurtGetMaterialPassNormalWS(float2 normalMapUV, float3 normalWS, float4 tangentWS, float facing)
{
    return BurtSampleNormalWS(normalMapUV, normalWS, tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);
}

float3 BurtGetMaterialPassGeometryNormalWS(float3 normalWS, float facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtSafeNormalize(normalWS);
#else
    return normalWS;
#endif
}

float3 BurtGetMaterialPassShadingDirectionWS(float3 normalWS, float4 tangentWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float strandDirectionSign = lerp(1.0f, -1.0f, saturate(_HairTangentFlip));
    return BurtSafeNormalize(tangentWS.xyz * strandDirectionSign);
#else
    return normalWS;
#endif
}

float3 BurtGetMaterialPassShadingDirectionWS(float2 uv0, float3 normalWS, float4 tangentWS, float facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float idValue = BURT_SAMPLE_TEXTURE2D_REPEAT(_IDMap, uv0 * float2(_IDXTilling, 1.0f)).r;
    float3 hairTangentTS = lerp(_TangentA.xyz, _TangentB.xyz, idValue) * _IDIntensity;
    hairTangentTS = BurtSafeNormalize(hairTangentTS + float3(0.0f, 1.0f, 0.0f));

    float angle = _HairRotate * 6.28318530718f;
    float sinAngle;
    float cosAngle;
    sincos(angle, sinAngle, cosAngle);
    float3 rotatedTangentTS = float3(
        hairTangentTS.x * cosAngle - hairTangentTS.y * sinAngle,
        hairTangentTS.x * sinAngle + hairTangentTS.y * cosAngle,
        hairTangentTS.z);
    hairTangentTS = BurtSafeNormalize(rotatedTangentTS + hairTangentTS);
    hairTangentTS = BurtApplyDoubleSidedNormalMode(hairTangentTS, facing, _DoubleSidedNormalModeConstants);
    float strandDirectionSign = lerp(1.0f, -1.0f, saturate(_HairTangentFlip));
    return BurtTransformTangentToWorld(hairTangentTS, normalWS, tangentWS) * strandDirectionSign;
#else
    return BurtGetMaterialPassShadingDirectionWS(normalWS, tangentWS);
#endif
}

float3 BurtGetMaterialPassShadingDirectionWS(float2 uv0, float3 normalWS, float4 tangentWS)
{
    return BurtGetMaterialPassShadingDirectionWS(uv0, normalWS, tangentWS, 1.0f);
}

float3 BurtGetMaterialPassDebugNormalWS(float3 normalWS, float3 shadingDirectionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return shadingDirectionWS;
#else
    return normalWS;
#endif
}

BurtGBufferData BurtCreateMaterialPassGBufferData(
    BurtSurfaceData surfaceData,
    float2 normalMapUV,
    float3 geometryNormalWS,
    float3 baseNormalWS,
    float4 tangentWS,
    float3 shadingDirectionWS,
    float facing,
    float3 emissionColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtCreateHairGBufferData(surfaceData, shadingDirectionWS, baseNormalWS, geometryNormalWS, emissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
    float3 clearCoatNormalWS = BurtSampleClearCoatNormalWS(normalMapUV, geometryNormalWS, tangentWS, _ClearCoatNormalScale, facing, _DoubleSidedNormalModeConstants);
    return BurtCreateClearCoatGBufferData(surfaceData, baseNormalWS, tangentWS, clearCoatNormalWS, emissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    return BurtCreateSubsurfaceGBufferData(surfaceData, baseNormalWS, tangentWS, emissionColor);
#else
    return BurtCreateGBufferData(surfaceData, baseNormalWS, tangentWS, emissionColor);
#endif
}

#endif // BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
