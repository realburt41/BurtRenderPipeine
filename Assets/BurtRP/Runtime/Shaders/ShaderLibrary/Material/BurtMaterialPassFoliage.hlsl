// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_FOLIAGE_INCLUDED
#define BURT_MATERIAL_PASS_FOLIAGE_INCLUDED

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
float4 _VegetationBoundsMin;
float4 _VegetationBoundsMax;
Texture2D _GlobalBaseColorMap;
float4 GlobalTexture_ST;

#if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS) && defined(BURT_GRASS_PROPERTIES_INCLUDED)
    #define BURT_MATERIAL_COMPILE_GRASS_FOLIAGE 1
#else
    #define BURT_MATERIAL_COMPILE_GRASS_FOLIAGE 0
#endif

float3 BurtApplyFoliageSaturation(float3 Color, float SaturationBoost)
{
    float Luminance = PerceivedLuminance(Color);
    return max(lerp(float3(Luminance, Luminance, Luminance), Color, 1.0f + saturate(SaturationBoost)), float3(0.0f, 0.0f, 0.0f));
}

float BurtMaterialPow3(float Value)
{
    return Value * Value * Value;
}

float BurtMaterialSafePow(float Value, float Power)
{
    return pow(max(Value, 0.0f), max(Power, BURT_EPSILON));
}

float BurtMaterialRangeRemap(float MinValue, float MaxValue, float Value)
{
    return saturate((Value - MinValue) / max(MaxValue - MinValue, BURT_EPSILON));
}

float3 BurtMaterialOverlayBlend(float3 BaseColor, float3 BlendColor)
{
    return lerp(
        2.0f * BaseColor * BlendColor,
        1.0f - 2.0f * (1.0f - BaseColor) * (1.0f - BlendColor),
        step(0.5f, BaseColor));
}

float BurtMaterialLinearStep(float Edge0, float Edge1, float Value)
{
    return saturate((Value - Edge0) / max(Edge1 - Edge0, BURT_EPSILON));
}

float4 BurtFetchGrassGroundColor(float3 PositionWS, float3 FallbackColor)
{
    if (abs(GlobalTexture_ST.z) <= BURT_EPSILON || abs(GlobalTexture_ST.w) <= BURT_EPSILON)
    {
        return float4(FallbackColor, 0.0f);
    }

    float2 UV = float2(
        (PositionWS.x - GlobalTexture_ST.x) / GlobalTexture_ST.z,
        (PositionWS.z - GlobalTexture_ST.y) / GlobalTexture_ST.w);
    return SAMPLE_TEXTURE2D_LOD(_GlobalBaseColorMap, sampler_LinearRepeat, UV, 0.0f);
}

float3 BurtResolveFoliageObjectUpWS()
{
    return BurtSafeNormalize(UnityObjectToWorldDir(float3(0.0f, 1.0f, 0.0f)));
}

#if !BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
float BurtEvaluateFoliageNormalizedHeight(float3 PositionOS, float3 PositionWS)
{
    float VegetationHeight = _VegetationBoundsMax.y - _VegetationBoundsMin.y;
    float Height = VegetationHeight > 0.0001f ? VegetationHeight : _TreeHeight;
    Height = max(Height, BURT_EPSILON);
    return saturate(PositionOS.y / Height);
}

float BurtResolveFoliageTintMode()
{
    float TintMode = _CustomEnum;
    if (TintMode < 0.5f && _FoliageTintMode > 0.5f)
    {
        TintMode = _FoliageTintMode;
    }

    return clamp(round(TintMode), 0.0f, 2.0f);
}

float BurtEvaluateFoliageTintValue()
{
    float TintMode = BurtResolveFoliageTintMode();
    if (TintMode > 1.5f)
    {
        return saturate(unity_ObjectToWorld._m30);
    }

    return saturate(_TintValue);
}
#endif

float4 BurtSampleFoliageNSRMap(float2 BaseMapUV)
{
    return BurtSampleNormalMap(BaseMapUV);
}

float4 BurtResolveFoliageSurfaceMap(float2 BaseMapUV, float4 FallbackMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
    return BurtSampleFoliageNSRMap(BaseMapUV);
#else
    return FallbackMap;
#endif
}

float3 BurtSampleFoliageNSRNormalWS(float2 NormalMapUV, float3 NormalWS, float4 TangentWS, float NormalScale, float Facing, float4 DoubleSidedNormalModeConstants)
{
    if (NormalScale <= 0.0f)
    {
        float3 NeutralNormalTS = BurtApplyDoubleSidedNormalMode(float3(0.0f, 0.0f, 1.0f), Facing, DoubleSidedNormalModeConstants);
        return BurtTransformTangentToWorld(NeutralNormalTS, NormalWS, TangentWS);
    }

    float4 NsrMap = BurtSampleFoliageNSRMap(NormalMapUV);
    float4 PackedNormal = float4(1.0f, NsrMap.g, 1.0f, NsrMap.r);
    float3 NormalTS = BurtUnpackNormalScale(PackedNormal, NormalScale);
    NormalTS = BurtApplyDoubleSidedNormalMode(NormalTS, Facing, DoubleSidedNormalModeConstants);
    return BurtTransformTangentToWorld(NormalTS, NormalWS, TangentWS);
}

float4 BurtEvaluateMaterialPassBaseColor(float2 BaseMapUV, float3 PositionWS, float3 PositionOS, float4 VertexColor)
{
    float4 BaseMap = BurtSampleBaseMap(BaseMapUV);
    float4 BaseColor = BaseMap * _BaseColor;

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
        float CameraDistance = distance(_WorldSpaceCameraPos.xyz, PositionWS);
        float FadeDistance = 250.0f;
        float FadeDis = saturate((FadeDistance - CameraDistance) / (0.15f * FadeDistance));
        float HeightMask = BurtGrassHeightFromVertexColor(VertexColor);
        float3 PivotWS = BurtGrassPivotPosWSFromVertexColor(VertexColor, unity_ObjectToWorld);
        float2 NoiseUV = PivotWS.xz * _NoiseMap_ST.xy * 0.01f + _NoiseMap_ST.zw;
        float2 Noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_LinearRepeat, NoiseUV).rg;
        float SqHeightMask = HeightMask * HeightMask;
        float HeightX = lerp(1.0f - SqHeightMask, SqHeightMask, saturate(_Variation01Height));
        float HeightY = lerp(1.0f - SqHeightMask, SqHeightMask, saturate(_Variation02Height));
        Noise.x = saturate(Noise.x * HeightX) * max(_VariationIntensity01, 0.0f);
        Noise.y = saturate(Noise.y * HeightY) * max(_VariationIntensity02, 0.0f);

        float4 GroundColor = BurtFetchGrassGroundColor(PositionWS, _BaseColor.rgb);
        float GroundFadeIntensity = saturate(max(_GroundFadeIntensity, GroundColor.a));
        float3 GrassColor = lerp(_BaseColor.rgb, GroundColor.rgb, GroundFadeIntensity);
        GrassColor = lerp(GrassColor, BurtMaterialOverlayBlend(GrassColor, _Variation01.rgb), saturate(Noise.x - Noise.y));
        GrassColor = lerp(GrassColor, BurtMaterialOverlayBlend(GrassColor, _Variation02.rgb), saturate(Noise.y));
        GrassColor = lerp(GrassColor, BurtMaterialOverlayBlend(GrassColor, _BaseColorTip.rgb), BurtMaterialSafePow(HeightMask, _TipMaskPow));
        BaseColor.rgb = saturate(lerp(GrassColor, GroundColor.rgb, saturate(1.0f - FadeDis)));
    #else
        BaseColor.rgb = BaseMap.rgb;
        float TintMask = saturate(BaseMap.a);
        float HeightScale = BurtMaterialSafePow(BurtEvaluateFoliageNormalizedHeight(PositionOS, PositionWS), _TintHeightContrast);
        float AOScale = BurtMaterialRangeRemap(_TintAORemap.x, _TintAORemap.y, BurtMaterialPow3(saturate(VertexColor.a)));
        float LocalScale = lerp(AOScale, HeightScale, saturate(_TintAOHeightRatio)) * max(_TintScale, 0.0f);
        float TintMode = BurtResolveFoliageTintMode();

        if (TintMode < 0.5f)
        {
            BaseColor.rgb = lerp(BaseColor.rgb, BurtMaterialOverlayBlend(BaseColor.rgb, _LocalTintColor.rgb), saturate(LocalScale));
        }
        else
        {
            float TintValue = BurtEvaluateFoliageTintValue();
            float2 TintUV = float2(TintValue, 0.5f);
            float3 GlobalTintColor = SAMPLE_TEXTURE2D(_TintPalette, sampler_LinearClamp, TintUV).rgb;
            float3 LocalTintColor = SAMPLE_TEXTURE2D(_LocalTintPalette, sampler_LinearClamp, TintUV).rgb;
            float3 TintColor = lerp(GlobalTintColor, LocalTintColor, saturate(LocalScale));
            BaseColor.rgb = BaseMap.rgb * lerp(1.0f, 2.0f * TintColor, TintMask);
        }
    #endif
#endif

    return BaseColor;
}

float BurtEvaluateMaterialPassOpacity(float Alpha, float2 BaseMapUV, float3 PositionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
        float AlphaMap = SAMPLE_TEXTURE2D_BIAS(_AlphaMap, sampler_LinearRepeat, BaseMapUV, -1.0f).r;
    #else
        float AlphaMap = SAMPLE_TEXTURE2D(_AlphaMap, sampler_LinearRepeat, BaseMapUV).r;
    #endif
    float DistanceToCamera = distance(_WorldSpaceCameraPos.xyz, PositionWS);
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
        float DistanceFactor = saturate(DistanceToCamera / 150.0f);
    #else
        float DistanceFactor = saturate((DistanceToCamera - 20.0f) / 200.0f);
    #endif
    return saturate(AlphaMap + AlphaMap * DistanceFactor * max(_AlphaIncrease, 0.0f));
#else
    return Alpha;
#endif
}

float3 BurtApplyFoliageMaterialNormalWS(float3 NormalWS, float3 PositionWS, float4 VertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
        float CameraDistance = distance(_WorldSpaceCameraPos.xyz, PositionWS);
        float FadeDistance = 250.0f;
        float FadeDis = saturate((FadeDistance - CameraDistance) / (0.15f * FadeDistance));
        float3 LightDirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
        float3 UpWardDir = BurtResolveFoliageObjectUpWS();
        float NoL = dot(LightDirectionWS, UpWardDir) * 0.5f + 0.5f;
        float NormalWeight = lerp(1.0f, (_TLNormalWeight - 1.0f) * NoL + 1.0f, FadeDis);
        UpWardDir.xz *= max(NormalWeight, 0.0f);
        if (_WindStrength > 0.001f && _WindNormalStrength > 0.001f)
        {
            float3 PivotWS = BurtGrassPivotPosWSFromVertexColor(VertexColor, unity_ObjectToWorld);
            float WindOffset = BurtGrassWindNoiseIntensity(PivotWS, _Time.y);
            float WindFactor = WindOffset * 2.0f - 1.0f;
            float3 WindDelta = BurtTrunkWindDirectionWS() * (WindFactor * _WindNormalStrength * _WindStrength * 0.03f);
            UpWardDir.xz += WindDelta.xz;
        }
        return BurtSafeNormalize(UpWardDir);
    #else
        #if defined(BURT_FOLIAGE_USE_BAKED_NORMALS)
            float3 NormalVS = mul((float3x3)UNITY_MATRIX_V, NormalWS);
            NormalVS = BurtSafeNormalize(float3(NormalVS.x, NormalVS.y, abs(NormalVS.z)));
            return BurtSafeNormalize(mul((float3x3)UNITY_MATRIX_I_V, NormalVS));
        #else
            return NormalWS;
        #endif
    #endif
#else
    return NormalWS;
#endif
}

BurtSurfaceData BurtApplyFoliageXRenderSurfaceSemantics(
    BurtSurfaceData SurfaceData,
    float4 BaseColor,
    float4 MaskMap,
    float ThickneSSScale,
    float RoughneSSScale,
    float ReflectanceScale,
    float3 SubsurfaceColor,
    float SubsurfaceColorSaturate,
    float TransmissionNdotL,
    float BackLight,
    float SpecularScale,
    float UseSpecularColor)
{
    float Thickness = saturate(MaskMap.b);
    float Roughness = saturate(MaskMap.a * RoughneSSScale);
    float TransmissionWeight = lerp(1.0f, 1.0f - Thickness, saturate(ThickneSSScale));
    float3 FoliageTransmissionColor = BurtApplyFoliageSaturation(BaseColor.rgb, SubsurfaceColorSaturate) * max(SubsurfaceColor, float3(0.0f, 0.0f, 0.0f));

    SurfaceData.Smoothness = saturate(1.0f - Roughness);
    SurfaceData.Reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    return BurtApplyFoliageSurfaceSemantics(
        SurfaceData,
        FoliageTransmissionColor,
        TransmissionWeight,
        Thickness,
        BackLight,
        TransmissionNdotL,
        saturate(SpecularScale),
        UseSpecularColor);
}

BurtSurfaceData BurtApplyGrassMaterialExtras(BurtSurfaceData SurfaceData, float3 PositionWS, float4 VertexColor)
{
#if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
    float HeightMask = BurtGrassHeightFromVertexColor(VertexColor);
    float CameraDistance = distance(_WorldSpaceCameraPos.xyz, PositionWS);
    float NearRange = BurtMaterialSafePow(1.0f - saturate(CameraDistance / max(_HeightAOFallOff, BURT_EPSILON)), 0.7f);
    float HeightOcclusion = saturate(HeightMask - saturate(_HeightAO) + 1.0f);
    SurfaceData.Occlusion = min(SurfaceData.Occlusion, lerp(1.0f, HeightOcclusion, NearRange));
#endif
    return SurfaceData;
}

BurtSurfaceData BurtApplyFoliageMaterialExtras(BurtSurfaceData SurfaceData, float3 PositionWS, float3 PositionOS, float4 VertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
    float CameraDistance = distance(_WorldSpaceCameraPos.xyz, PositionWS);
    float FoliageScreenSpaceShadow = saturate(CameraDistance * 0.025f);
    SurfaceData.Occlusion = BurtMaterialRangeRemap(_VertexAORemap.x, _VertexAORemap.y, saturate(VertexColor.a));
    SurfaceData.FoliageScreenSpaceShadowIntensity = FoliageScreenSpaceShadow * FoliageScreenSpaceShadow;
#endif
    return SurfaceData;
}

#if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
BurtSurfaceData BurtCreateGrassXRenderSurfaceData(float4 BaseColor, float4 MaskMap)
{
    float4 GrassMaskMap = float4(0.0f, MaskMap.g, 0.5f, 1.0f);
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, saturate(1.0f - _Roughness), 0.0f, GrassMaskMap, _OcclusionStrength);
    SurfaceData.Metallic = 0.0f;
    SurfaceData.Anisotropy = 0.0f;
    SurfaceData.Smoothness = saturate(1.0f - _Roughness);
    SurfaceData.Reflectance = saturate(_Reflectance);
    SurfaceData.FoliageTransmissionColor = max(BaseColor.rgb, float3(0.0f, 0.0f, 0.0f));
    SurfaceData.FoliageTransmissionWeight = 0.0f;
    SurfaceData.FoliageThickness = 0.0f;
    SurfaceData.FoliageBackLight = 0.0f;
    SurfaceData.FoliageTransmissionNdotL = 0.0f;
    SurfaceData.FoliageSpecularScale = saturate(_Specular);
    SurfaceData.FoliageUseSpecularColor = 0.0f;
    SurfaceData.FoliageScreenSpaceShadowIntensity = 0.0f;
    SurfaceData.FoliageIsGrass = 1.0f;
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FOLIAGE;
    return SurfaceData;
}
#endif

BurtSurfaceData BurtApplyGrassXRenderSurfaceSemantics(
    BurtSurfaceData SurfaceData,
    float3 NormalWS,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float4 VertexColor)
{
#if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
    float CameraDistance = distance(_WorldSpaceCameraPos.xyz, PositionWS);
    float FadeDistance = 250.0f;
    float FadeDis = saturate((FadeDistance - CameraDistance) / (0.15f * FadeDistance));
    float3 UpWardDir = BurtSafeNormalize(lerp(float3(0.0f, 1.0f, 0.0f), BurtResolveFoliageObjectUpWS(), 0.75f));
    float3 CameraVectorWS = BurtSafeNormalize(ViewDirectionWS);
    float3 LightDirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
    float NoV = dot(CameraVectorWS, UpWardDir);
    float VoL = dot(CameraVectorWS, LightDirectionWS);
    float NoVWeight = BurtMaterialLinearStep(0.5f + _FresnelExp, 1.0f, 1.0f - abs(NoV));
    float VoLWeight = saturate(1.0f - (VoL * 0.5f + 0.5f));
    float FresnelTerm = NoVWeight * VoLWeight;
    FresnelTerm *= saturate(CameraDistance * 0.04f);

    float GrassSSSIntensity = (_FresnelIntensity * FresnelTerm + _SSSIntensity) * FadeDis;
    float HeightMask = BurtGrassHeightFromVertexColor(VertexColor);
    float DisMask = saturate(1.0f - CameraDistance / max(_SSShadowDistance, BURT_EPSILON));
    float DisFalloff = 1.0f - (1.0f - DisMask) * (1.0f - DisMask);
    SurfaceData.Metallic = 0.0f;
    SurfaceData.Anisotropy = 0.0f;
    SurfaceData.FoliageTransmissionColor = max(SurfaceData.BaseColor.rgb, float3(0.0f, 0.0f, 0.0f));
    SurfaceData.FoliageTransmissionWeight = max(GrassSSSIntensity, 0.0f);
    SurfaceData.FoliageThickness = 0.0f;
    SurfaceData.FoliageBackLight = 0.0f;
    SurfaceData.FoliageTransmissionNdotL = 0.0f;
    SurfaceData.FoliageSpecularScale = saturate(lerp(0.5f, _Specular, FadeDis));
    SurfaceData.FoliageUseSpecularColor = 0.0f;
    SurfaceData.FoliageScreenSpaceShadowIntensity = max((1.0f - HeightMask) * _SSShadowIntensity * DisFalloff, 0.0f);
    SurfaceData.FoliageIsGrass = 1.0f;
    SurfaceData.Reflectance = saturate(lerp(0.5f, _Reflectance, FadeDis));
    SurfaceData.Smoothness = saturate(1.0f - _Roughness);
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FOLIAGE;
#endif
    return SurfaceData;
}
#endif

#endif // BURT_MATERIAL_PASS_FOLIAGE_INCLUDED
