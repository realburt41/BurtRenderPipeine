// Shared material shading-model helpers used by GBuffer and Forward passes.
#ifndef BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
#define BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairDither.hlsl"

#if !defined(BURT_MAIN_LIGHT_DIRECTION_DECLARED)
#define BURT_MAIN_LIGHT_DIRECTION_DECLARED
float4 _BurtMainLightDirection;
#endif

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

float4 BurtEvaluateMaterialPassBaseColor(float2 uv0, float2 uv1, float3 positionOS, float4 maskMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float4 idValue = BURT_SAMPLE_TEXTURE2D_REPEAT(_IDMap, uv0 * float2(_IDXTilling, 1.0f));
    float4 baseMap = BurtSampleBaseMap(uv0);
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

float4 BurtEvaluateMaterialPassBaseColor(float2 uv0, float2 uv1, float3 positionOS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtEvaluateMaterialPassBaseColor(uv0, uv1, positionOS, BurtEvaluateMaterialPassMaskMap(uv0, uv1));
#else
    return BurtSampleBaseMap(BurtTransformBaseMapUV(uv0, _BaseMap_ST)) * _BaseColor;
#endif
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
float2 BurtTransformFuzzMapUV(float2 uv0, float4 fuzzMapST)
{
    return uv0 * fuzzMapST.xy + fuzzMapST.zw;
}

float3 BurtEvaluateFabricFuzzColor(float2 uv0)
{
    float2 fuzzMapUV = BurtTransformFuzzMapUV(uv0, _FuzzMap_ST);
    return BURT_SAMPLE_TEXTURE2D_REPEAT(_FuzzMap, fuzzMapUV).rgb * _FuzzColor.rgb;
}

float BurtEvaluateFabricFuzzWeight(float2 uv0)
{
    return BURT_SAMPLE_TEXTURE2D_REPEAT(_FuzzMask, uv0).r * _FuzzAmount;
}

BurtSurfaceData BurtApplyFabricPassSurfaceSemantics(BurtSurfaceData surfaceData, float4 maskMap, float2 uv0, float nDotV)
{
#if defined(BURT_MATERIAL_SELECTED_FABRIC_IS_SILK)
    return BurtApplySilkSurfaceSemantics(surfaceData, _Anisotropy, _FacingColor.rgb);
#else
    float fabricFuzzRoughness = ClampPerceptualRoughness(saturate(maskMap.a) * _FuzzRoughness);
    return BurtApplyFabricSurfaceSemantics(surfaceData, BurtEvaluateFabricFuzzWeight(uv0), BurtEvaluateFabricFuzzColor(uv0), fabricFuzzRoughness);
#endif
}

BurtSurfaceData BurtApplyFabricPassSurfaceSemantics(BurtSurfaceData surfaceData, float4 maskMap, float2 uv0)
{
    return BurtApplyFabricPassSurfaceSemantics(surfaceData, maskMap, uv0, 1.0f);
}
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
float4 _VegetationBoundsMin;
float4 _VegetationBoundsMax;

float3 BurtApplyFoliageSaturation(float3 color, float saturationBoost)
{
    float luminance = PerceivedLuminance(color);
    return max(lerp(float3(luminance, luminance, luminance), color, 1.0f + saturate(saturationBoost)), float3(0.0f, 0.0f, 0.0f));
}

float BurtMaterialPow3(float value)
{
    return value * value * value;
}

float BurtMaterialSafePow(float value, float power)
{
    return pow(max(value, 0.0f), max(power, BURT_EPSILON));
}

float BurtMaterialRangeRemap(float minValue, float maxValue, float value)
{
    return saturate((value - minValue) / max(maxValue - minValue, BURT_EPSILON));
}

float3 BurtMaterialOverlayBlend(float3 baseColor, float3 blendColor)
{
    return lerp(
        2.0f * baseColor * blendColor,
        1.0f - 2.0f * (1.0f - baseColor) * (1.0f - blendColor),
        step(0.5f, baseColor));
}

float BurtMaterialLinearStep(float edge0, float edge1, float value)
{
    return saturate((value - edge0) / max(edge1 - edge0, BURT_EPSILON));
}

float3 BurtResolveFoliageObjectUpWS()
{
    return BurtSafeNormalize(UnityObjectToWorldDir(float3(0.0f, 1.0f, 0.0f)));
}

float BurtEvaluateFoliageNormalizedHeight(float3 positionOS, float3 positionWS)
{
    float vegetationHeight = _VegetationBoundsMax.y - _VegetationBoundsMin.y;
    float height = vegetationHeight > 0.0001f ? vegetationHeight : _TreeHeight;
    height = max(height, BURT_EPSILON);
    return saturate(positionOS.y / height);
}

float BurtResolveFoliageTintMode()
{
    float tintMode = _CustomEnum;
    if (tintMode < 0.5f && _FoliageTintMode > 0.5f)
    {
        tintMode = _FoliageTintMode;
    }

    return clamp(round(tintMode), 0.0f, 2.0f);
}

float BurtEvaluateFoliageTintValue()
{
    float tintMode = BurtResolveFoliageTintMode();
    if (tintMode > 1.5f)
    {
        return saturate(unity_ObjectToWorld._m30);
    }

    return saturate(_TintValue);
}

float4 BurtSampleFoliageNSRMap(float2 baseMapUV)
{
    return BurtSampleNormalMap(baseMapUV);
}

float4 BurtResolveFoliageSurfaceMap(float2 baseMapUV, float4 fallbackMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    return BurtSampleFoliageNSRMap(baseMapUV);
#else
    return fallbackMap;
#endif
}

float3 BurtSampleFoliageNSRNormalWS(float2 normalMapUV, float3 normalWS, float4 tangentWS, float normalScale, float facing, float4 doubleSidedNormalModeConstants)
{
    if (normalScale <= 0.0f)
    {
        float3 neutralNormalTS = BurtApplyDoubleSidedNormalMode(float3(0.0f, 0.0f, 1.0f), facing, doubleSidedNormalModeConstants);
        return BurtTransformTangentToWorld(neutralNormalTS, normalWS, tangentWS);
    }

    float4 nsrMap = BurtSampleFoliageNSRMap(normalMapUV);
    float4 packedNormal = float4(1.0f, nsrMap.g, 1.0f, nsrMap.r);
    float3 normalTS = BurtUnpackNormalScale(packedNormal, normalScale);
    normalTS = BurtApplyDoubleSidedNormalMode(normalTS, facing, doubleSidedNormalModeConstants);
    return BurtTransformTangentToWorld(normalTS, normalWS, tangentWS);
}

float4 BurtEvaluateMaterialPassBaseColor(float2 baseMapUV, float3 positionWS, float3 positionOS, float4 vertexColor)
{
    float4 baseMap = BurtSampleBaseMap(baseMapUV);
    float4 baseColor = baseMap * _BaseColor;

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float heightMask = saturate(vertexColor.r);
        float2 noiseUV = positionWS.xz * _NoiseMap_ST.xy * 0.01f + _NoiseMap_ST.zw;
        float2 noise = BURT_SAMPLE_TEXTURE2D_REPEAT(_NoiseMap, noiseUV).rg;
        float sqHeightMask = heightMask * heightMask;
        float heightX = lerp(1.0f - sqHeightMask, sqHeightMask, saturate(_Variation01Height));
        float heightY = lerp(1.0f - sqHeightMask, sqHeightMask, saturate(_Variation02Height));
        noise.x = saturate(noise.x * heightX) * max(_VariationIntensity01, 0.0f);
        noise.y = saturate(noise.y * heightY) * max(_VariationIntensity02, 0.0f);

        float3 grassColor = baseColor.rgb;
        grassColor = lerp(grassColor, BurtMaterialOverlayBlend(grassColor, _Variation01.rgb), saturate(noise.x - noise.y));
        grassColor = lerp(grassColor, BurtMaterialOverlayBlend(grassColor, _Variation02.rgb), saturate(noise.y));
        grassColor = lerp(grassColor, BurtMaterialOverlayBlend(grassColor, _BaseColorTip.rgb), BurtMaterialSafePow(heightMask, _TipMaskPow));
        baseColor.rgb = saturate(lerp(grassColor, grassColor * 0.85f, saturate(_GroundFadeIntensity)));
    #else
        baseColor.rgb = baseMap.rgb;
        float tintMask = saturate(baseMap.a);
        float heightScale = BurtMaterialSafePow(BurtEvaluateFoliageNormalizedHeight(positionOS, positionWS), _TintHeightContrast);
        float aoScale = BurtMaterialRangeRemap(_TintAORemap.x, _TintAORemap.y, BurtMaterialPow3(saturate(vertexColor.a)));
        float localScale = lerp(aoScale, heightScale, saturate(_TintAOHeightRatio)) * max(_TintScale, 0.0f);
        float tintMode = BurtResolveFoliageTintMode();

        if (tintMode < 0.5f)
        {
            baseColor.rgb = lerp(baseColor.rgb, BurtMaterialOverlayBlend(baseColor.rgb, _LocalTintColor.rgb), saturate(localScale));
        }
        else
        {
            float tintValue = BurtEvaluateFoliageTintValue();
            float2 tintUV = float2(tintValue, 0.5f);
            float3 globalTintColor = BURT_SAMPLE_TEXTURE2D_CLAMP(_TintPalette, tintUV).rgb;
            float3 localTintColor = BURT_SAMPLE_TEXTURE2D_CLAMP(_LocalTintPalette, tintUV).rgb;
            float3 tintColor = lerp(globalTintColor, localTintColor, saturate(localScale));
            baseColor.rgb = baseMap.rgb * lerp(1.0f, 2.0f * tintColor, tintMask);
        }
    #endif
#endif

    return baseColor;
}

float BurtEvaluateMaterialPassOpacity(float alpha, float2 baseMapUV, float3 positionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    float alphaMap = BURT_SAMPLE_TEXTURE2D_REPEAT(_AlphaMap, baseMapUV).r;
    float distanceToCamera = distance(_WorldSpaceCameraPos.xyz, positionWS);
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float distanceFactor = saturate(distanceToCamera / 150.0f);
    #else
        float distanceFactor = saturate((distanceToCamera - 20.0f) / 200.0f);
    #endif
    return saturate(alphaMap + alphaMap * distanceFactor * max(_AlphaIncrease, 0.0f));
#else
    return alpha;
#endif
}

float3 BurtApplyFoliageMaterialNormalWS(float3 normalWS, float3 positionWS, float4 vertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float cameraDistance = distance(_WorldSpaceCameraPos.xyz, positionWS);
        float fadeDistance = 250.0f;
        float fadeDis = saturate((fadeDistance - cameraDistance) / (0.15f * fadeDistance));
        float3 lightDirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
        float3 upWardDir = BurtResolveFoliageObjectUpWS();
        float noL = dot(lightDirectionWS, upWardDir) * 0.5f + 0.5f;
        float normalWeight = lerp(1.0f, (_TLNormalWeight - 1.0f) * noL + 1.0f, fadeDis);
        upWardDir.xz *= max(normalWeight, 0.0f);
        return BurtSafeNormalize(upWardDir);
    #else
        #if defined(BURT_FOLIAGE_USE_BAKED_NORMALS)
            float3 normalVS = mul((float3x3)UNITY_MATRIX_V, normalWS);
            normalVS = BurtSafeNormalize(float3(normalVS.x, normalVS.y, abs(normalVS.z)));
            return BurtSafeNormalize(mul((float3x3)UNITY_MATRIX_I_V, normalVS));
        #else
            return normalWS;
        #endif
    #endif
#else
    return normalWS;
#endif
}

BurtSurfaceData BurtApplyFoliageXRenderSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float4 baseColor,
    float4 maskMap,
    float thicknessScale,
    float roughnessScale,
    float reflectanceScale,
    float3 subsurfaceColor,
    float subsurfaceColorSaturate,
    float transmissionNdotL,
    float backLight,
    float specularScale,
    float useSpecularColor)
{
    float thickness = saturate(maskMap.b);
    float roughness = saturate(maskMap.a * roughnessScale);
    float transmissionWeight = lerp(1.0f, 1.0f - thickness, saturate(thicknessScale));
    float3 foliageTransmissionColor = BurtApplyFoliageSaturation(baseColor.rgb, subsurfaceColorSaturate) * max(subsurfaceColor, float3(0.0f, 0.0f, 0.0f));

    surfaceData.smoothness = saturate(1.0f - roughness);
    surfaceData.reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    return BurtApplyFoliageSurfaceSemantics(
        surfaceData,
        foliageTransmissionColor,
        transmissionWeight,
        thickness,
        backLight,
        transmissionNdotL,
        saturate(specularScale),
        useSpecularColor);
}

BurtSurfaceData BurtApplyFoliageMaterialExtras(BurtSurfaceData surfaceData, float3 positionWS, float3 positionOS, float4 vertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float heightMask = saturate(vertexColor.r);
        float cameraDistance = distance(_WorldSpaceCameraPos.xyz, positionWS);
        float nearRange = BurtMaterialSafePow(1.0f - saturate(cameraDistance / max(_HeightAOFallOff, BURT_EPSILON)), 0.7f);
        float heightOcclusion = saturate(heightMask - saturate(_HeightAO) + 1.0f);
        surfaceData.occlusion = min(surfaceData.occlusion, lerp(1.0f, heightOcclusion, nearRange));
    #else
        float cameraDistance = distance(_WorldSpaceCameraPos.xyz, positionWS);
        float foliageScreenSpaceShadow = saturate(cameraDistance * 0.025f);
        surfaceData.occlusion = BurtMaterialRangeRemap(_VertexAORemap.x, _VertexAORemap.y, saturate(vertexColor.a));
        surfaceData.foliageScreenSpaceShadowIntensity = foliageScreenSpaceShadow * foliageScreenSpaceShadow;
    #endif
#endif
    return surfaceData;
}

BurtSurfaceData BurtApplyGrassXRenderSurfaceSemantics(
    BurtSurfaceData surfaceData,
    float3 normalWS,
    float3 viewDirectionWS,
    float3 positionWS,
    float4 vertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    float cameraDistance = distance(_WorldSpaceCameraPos.xyz, positionWS);
    float fadeDistance = 250.0f;
    float fadeDis = saturate((fadeDistance - cameraDistance) / (0.15f * fadeDistance));
    float3 upWardDir = BurtSafeNormalize(lerp(float3(0.0f, 1.0f, 0.0f), BurtResolveFoliageObjectUpWS(), 0.75f));
    float3 cameraVectorWS = BurtSafeNormalize(viewDirectionWS);
    float3 lightDirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
    float noV = dot(cameraVectorWS, upWardDir);
    float voL = dot(cameraVectorWS, lightDirectionWS);
    float noVWeight = BurtMaterialLinearStep(0.5f + _FresnelExp, 1.0f, 1.0f - abs(noV));
    float voLWeight = saturate(1.0f - (voL * 0.5f + 0.5f));
    float fresnelTerm = noVWeight * voLWeight;
    fresnelTerm *= saturate(cameraDistance * 0.04f);

    float grassSSSIntensity = (_FresnelIntensity * fresnelTerm + _SSSIntensity) * fadeDis;
    float heightMask = saturate(vertexColor.r);
    float disMask = saturate(1.0f - cameraDistance / max(_SSShadowDistance, BURT_EPSILON));
    float disFalloff = 1.0f - (1.0f - disMask) * (1.0f - disMask);
    surfaceData.foliageTransmissionWeight = max(grassSSSIntensity, 0.0f);
    surfaceData.foliageSpecularScale = saturate(lerp(0.5f, _Specular, fadeDis));
    surfaceData.foliageUseSpecularColor = 0.0f;
    surfaceData.foliageScreenSpaceShadowIntensity = max((1.0f - heightMask) * _SSShadowIntensity * disFalloff, 0.0f);
    surfaceData.foliageIsGrass = 1.0f;
    surfaceData.reflectance = saturate(lerp(0.5f, _Reflectance, fadeDis));
    surfaceData.smoothness = saturate(1.0f - _Roughness);
#endif
    return surfaceData;
}
#endif

#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
float BurtEvaluateMaterialPassOpacity(float alpha, float2 baseMapUV, float3 positionWS)
{
    return alpha;
}
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
float BurtEvaluateTrunkVertexAO(float4 vertexColor)
{
    return saturate((saturate(vertexColor.a) - _VertexAORemap.x) / max(_VertexAORemap.y - _VertexAORemap.x, BURT_EPSILON));
}

BurtSurfaceData BurtApplyTrunkXRenderSurfaceSemantics(BurtSurfaceData surfaceData, float4 maskMap, float4 vertexColor)
{
    float mapOcclusion = saturate(maskMap.g);
    float vertexAO = BurtEvaluateTrunkVertexAO(vertexColor);

    surfaceData.metallic = 0.0f;
    surfaceData.anisotropy = 0.0f;
    surfaceData.reflectance = saturate(_Specular);
    surfaceData.smoothness = saturate(1.0f - saturate(maskMap.a));
    surfaceData.occlusion = min(mapOcclusion, vertexAO);
    surfaceData.height = 0.5f;
    surfaceData.shadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    return surfaceData;
}
#endif

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
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, BURT_SUBSURFACE_FIXED_REFLECTANCE, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    float subsurface3SCurvature = saturate(maskMap.g * _Subsurface3SCurvatureScale + _Subsurface3SCurvatureBias);
    return BurtApplySubsurfaceSurfaceSemantics(surfaceData, _SubsurfaceThickness, _SubsurfacePower, _SubsurfaceDistortion, _SubsurfaceAmbient, subsurface3SCurvature, _SubsurfaceProfileIndex, _SubsurfaceScatteringMode);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    return BurtApplyFoliageXRenderSurfaceSemantics(surfaceData, baseColor, maskMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, saturate(_Specular), 1.0f, 0.0f, float4(0.0f, maskMap.g, 0.5f, 1.0f), 1.0f);
    return BurtApplyTrunkXRenderSurfaceSemantics(surfaceData, maskMap, float4(1.0f, 1.0f, 1.0f, 1.0f));
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    BurtSurfaceData surfaceData = BurtCreateFabricSurfaceData(baseColor, _Reflectance, _Roughness, _Metallic, maskMap, _OcclusionStrength);
    return surfaceData;
#else
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, _Metallic, maskMap, _OcclusionStrength);

    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
        surfaceData = BurtApplyAnisotropySurfaceSemantics(surfaceData, _Anisotropy);
        surfaceData = BurtApplyClearCoatSurfaceSemantics(surfaceData, _ClearCoatMask, _ClearCoatRoughness);
    #else
        surfaceData = BurtApplyAnisotropySurfaceSemantics(surfaceData, _Anisotropy);
    #endif

    return surfaceData;
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(float4 baseColor, float4 maskMap, float2 uv0)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    BurtSurfaceData surfaceData = BurtCreateFabricSurfaceData(baseColor, _Reflectance, _Roughness, _Metallic, maskMap, _OcclusionStrength);
    return BurtApplyFabricPassSurfaceSemantics(surfaceData, maskMap, uv0);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    float4 foliageMap = BurtResolveFoliageSurfaceMap(uv0, maskMap);
    return BurtApplyFoliageXRenderSurfaceSemantics(surfaceData, baseColor, foliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
#else
    return BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(float4 baseColor, float4 maskMap, float2 uv0, float3 normalWS, float3 viewDirectionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    BurtSurfaceData surfaceData = BurtCreateFabricSurfaceData(baseColor, _Reflectance, _Roughness, _Metallic, maskMap, _OcclusionStrength);
    float nDotV = saturate(dot(BurtSafeNormalize(normalWS), BurtSafeNormalize(viewDirectionWS)));
    return BurtApplyFabricPassSurfaceSemantics(surfaceData, maskMap, uv0, nDotV);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    float4 foliageMap = BurtResolveFoliageSurfaceMap(uv0, maskMap);
    return BurtApplyFoliageXRenderSurfaceSemantics(surfaceData, baseColor, foliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
#else
    return BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap);
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
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    return BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap, uv0, geometryNormalWS, viewDirectionWS);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    float4 foliageMap = BurtResolveFoliageSurfaceMap(uv0, maskMap);
    return BurtApplyFoliageXRenderSurfaceSemantics(surfaceData, baseColor, foliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
#else
    return BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(
    float4 baseColor,
    float4 maskMap,
    float2 uv0,
    float3 normalWS,
    float3 viewDirectionWS,
    float3 positionWS,
    float3 positionOS,
    float4 vertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, _Reflectance, _Smoothness, 0.0f, maskMap, _OcclusionStrength);
    float4 foliageMap = BurtResolveFoliageSurfaceMap(uv0, maskMap);
    surfaceData = BurtApplyFoliageXRenderSurfaceSemantics(surfaceData, baseColor, foliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
    surfaceData = BurtApplyFoliageMaterialExtras(surfaceData, positionWS, positionOS, vertexColor);
    return BurtApplyGrassXRenderSurfaceSemantics(surfaceData, normalWS, viewDirectionWS, positionWS, vertexColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    BurtSurfaceData surfaceData = BurtCreateSurfaceData(baseColor, saturate(_Specular), 1.0f, 0.0f, float4(0.0f, maskMap.g, 0.5f, 1.0f), 1.0f);
    return BurtApplyTrunkXRenderSurfaceSemantics(surfaceData, maskMap, vertexColor);
#else
    return BurtCreateMaterialShadingModelSurfaceData(baseColor, maskMap, uv0, normalWS, viewDirectionWS);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(
    float4 baseColor,
    float4 maskMap,
    float2 uv0,
    float3 normalWS,
    float3 viewDirectionWS,
    float3 positionWS)
{
    return BurtCreateMaterialShadingModelSurfaceData(
        baseColor,
        maskMap,
        uv0,
        normalWS,
        viewDirectionWS,
        positionWS,
        float3(0.0f, 0.0f, 0.0f),
        float4(1.0f, 1.0f, 1.0f, 1.0f));
}

float3 BurtGetMaterialPassNormalWS(float2 normalMapUV, float3 normalWS, float4 tangentWS, float facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    return BurtSampleFoliageNSRNormalWS(normalMapUV, normalWS, tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);
#else
    return BurtSampleNormalWS(normalMapUV, normalWS, tangentWS, _NormalScale, facing, _DoubleSidedNormalModeConstants);
#endif
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
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    return BurtCreateFoliageGBufferData(surfaceData, baseNormalWS, tangentWS, emissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    return BurtCreateFabricGBufferData(surfaceData, baseNormalWS, tangentWS, emissionColor);
#else
    return BurtCreateGBufferData(surfaceData, baseNormalWS, tangentWS, emissionColor);
#endif
}

#endif // BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
