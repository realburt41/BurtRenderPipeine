// Shared material shading-model helpers used by GBuffer and Forward passes.
#ifndef BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
#define BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtHairDither.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtTrunkVertexAnimation.hlsl"

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtEyePass.hlsl"
#endif

#if !defined(SAMPLE_TEXTURE2D_BIAS)
    #define SAMPLE_TEXTURE2D_BIAS(textureName, samplerName, coord2, bias) textureName.SampleBias(samplerName, coord2, bias)
#endif

#if !defined(BURT_MAIN_LIGHT_DIRECTION_DECLARED)
#define BURT_MAIN_LIGHT_DIRECTION_DECLARED
float4 _BurtMainLightDirection;
#endif

float4 BurtEvaluateMaterialPassMaskMap(float2 UV0, float2 UV1)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtSampleMaskMap(UV1);
#else
    return BurtSampleMaskMap(BurtTransformMaskMapUV(UV0, _MaskMap_ST));
#endif
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
float BurtAvatarHairGradientFactor(float2 UV0, float3 PositionOS)
{
    float GradientFactor = UV0.y;
    float3 GradientDirection = BurtSafeNormalize(_GradientDirection.xyz);
    float PositionGradientFactor = dot(PositionOS + _GradientPosOffset.xyz, GradientDirection);
    GradientFactor = _RootGradientPosEnable > 0.5f ? PositionGradientFactor : GradientFactor;
    return _RootGradientReverse > 0.5f ? 1.0f - GradientFactor : GradientFactor;
}

float3 BurtAvatarHairApplyGradientMap(float3 BaseColor, float GradientMask)
{
    float GradientV = (_GradientRowIndex + 0.5f) * max(_GradientMap_TexelSize.y, BURT_EPSILON);
    float3 GradientColor = SAMPLE_TEXTURE2D(_GradientMap, sampler_LinearClamp, float2(saturate(GradientMask), saturate(GradientV))).rgb;
    float3 BlendSoftLight = (1.0f - 2.0f * GradientColor) * BaseColor * BaseColor + 2.0f * GradientColor * BaseColor;
    float3 BlendOverlay = lerp(
        2.0f * BaseColor * GradientColor,
        1.0f - 2.0f * (1.0f - BaseColor) * (1.0f - GradientColor),
        step(0.5f, BaseColor));

    float3 Result = BaseColor;
    Result += (BlendSoftLight * 1.05f - BaseColor) * _GradientSoftLight;
    Result += (BlendOverlay - BaseColor) * _GradientOverlay;
    return lerp(Result, GradientColor, _GradientReplace);
}

float3 BurtAvatarHairStructureFactor(float HairStructureMask)
{
    float HairShadowMask = saturate((1.0f - HairStructureMask) * _HairShadowIntensity);
    float3 HairBrightFactor = max(_HairBrightColor.rgb * _HairBrightIntensity, float3(0.0f, 0.0f, 0.0f));
    float3 HairShadowFactor = max(lerp(float3(0.0f, 0.0f, 0.0f), HairBrightFactor, HairStructureMask), float3(0.0f, 0.0f, 0.0f));
    return max(lerp(HairShadowFactor, _HairShadowColor.rgb, HairShadowMask), float3(0.0f, 0.0f, 0.0f));
}
#endif

float4 BurtEvaluateMaterialPassBaseColor(float2 UV0, float2 UV1, float3 PositionOS, float4 MaskMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float4 IDValue = SAMPLE_TEXTURE2D(_IDMap, sampler_LinearRepeat, UV0 * float2(_IDXTilling, 1.0f));
    float4 BaseMap = BurtSampleBaseMap(UV0);
    float GradientFactor = BurtAvatarHairGradientFactor(UV0, PositionOS);

    float RootGradient = saturate(smoothstep(_RootGradient.x, _RootGradient.y, GradientFactor));
    float4 BaseColor = _BaseColor;
    BaseColor.rgb = lerp(_BaseColor.rgb, _RootColor.rgb, RootGradient * _RootGradientEnable);
    BaseColor *= BaseMap;

    if (_GradientColorEnable > 0.5f)
    {
        BaseColor.rgb = BurtAvatarHairApplyGradientMap(BaseColor.rgb, IDValue.a);
    }

    float3 HairShadowFactor = BurtAvatarHairStructureFactor(saturate(IDValue.g));
    BaseColor.rgb = lerp(BaseColor.rgb, BaseColor.rgb * HairShadowFactor, _HairShadowPower);
    BaseColor.rgb = lerp(BaseColor.rgb * _AlbedoOcclusionColor.rgb, BaseColor.rgb, lerp(1.0f, MaskMap.g, _AlbedoOcclusion));
    return BaseColor;
#else
    return BurtSampleBaseMap(BurtTransformBaseMapUV(UV0, _BaseMap_ST)) * _BaseColor;
#endif
}

float4 BurtEvaluateMaterialPassBaseColor(float2 UV0, float2 UV1, float3 PositionOS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtEvaluateMaterialPassBaseColor(UV0, UV1, PositionOS, BurtEvaluateMaterialPassMaskMap(UV0, UV1));
#else
    return BurtSampleBaseMap(BurtTransformBaseMapUV(UV0, _BaseMap_ST)) * _BaseColor;
#endif
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
float BurtEvaluateSubsurfaceMaterialThickness(float2 BaseMapUV)
{
    return saturate(_SubsurfaceThickness * BurtSampleSubsurfaceThicknessMap(BaseMapUV));
}
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
float2 BurtTransformFuzzMapUV(float2 UV0, float4 FuzzMapST)
{
    return UV0 * FuzzMapST.xy + FuzzMapST.zw;
}

float3 BurtEvaluateFabricFuzzColor(float2 UV0)
{
    float2 FuzzMapUV = BurtTransformFuzzMapUV(UV0, _FuzzMap_ST);
    return SAMPLE_TEXTURE2D(_FuzzMap, sampler_LinearRepeat, FuzzMapUV).rgb * _FuzzColor.rgb;
}

float BurtEvaluateFabricFuzzWeight(float2 UV0)
{
    return SAMPLE_TEXTURE2D(_FuzzMask, sampler_LinearRepeat, UV0).r * _FuzzAmount;
}

BurtSurfaceData BurtApplyFabricPassSurfaceSemantics(BurtSurfaceData SurfaceData, float4 MaskMap, float2 UV0, float NdotV)
{
#if defined(BURT_MATERIAL_SELECTED_FABRIC_IS_SILK)
    return BurtApplySilkSurfaceSemantics(SurfaceData, _Anisotropy, _FacingColor.rgb);
#else
    float FabricFuzzRoughness = ClampPerceptualRoughness(saturate(MaskMap.a) * _FuzzRoughness);
    return BurtApplyFabricSurfaceSemantics(SurfaceData, BurtEvaluateFabricFuzzWeight(UV0), BurtEvaluateFabricFuzzColor(UV0), FabricFuzzRoughness);
#endif
}

BurtSurfaceData BurtApplyFabricPassSurfaceSemantics(BurtSurfaceData SurfaceData, float4 MaskMap, float2 UV0)
{
    return BurtApplyFabricPassSurfaceSemantics(SurfaceData, MaskMap, UV0, 1.0f);
}
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
float4 _VegetationBoundsMin;
float4 _VegetationBoundsMax;
Texture2D _GlobalBaseColorMap;
float4 GlobalTexture_ST;

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

float4 BurtSampleFoliageNSRMap(float2 BaseMapUV)
{
    return BurtSampleNormalMap(BaseMapUV);
}

float4 BurtResolveFoliageSurfaceMap(float2 BaseMapUV, float4 FallbackMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
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
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
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
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
        float AlphaMap = SAMPLE_TEXTURE2D_BIAS(_AlphaMap, sampler_LinearRepeat, BaseMapUV, -1.0f).r;
    #else
        float AlphaMap = SAMPLE_TEXTURE2D(_AlphaMap, sampler_LinearRepeat, BaseMapUV).r;
    #endif
    float DistanceToCamera = distance(_WorldSpaceCameraPos.xyz, PositionWS);
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
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
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
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
#if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
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
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    float CameraDistance = distance(_WorldSpaceCameraPos.xyz, PositionWS);
    float FoliageScreenSpaceShadow = saturate(CameraDistance * 0.025f);
    SurfaceData.Occlusion = BurtMaterialRangeRemap(_VertexAORemap.x, _VertexAORemap.y, saturate(VertexColor.a));
    SurfaceData.FoliageScreenSpaceShadowIntensity = FoliageScreenSpaceShadow * FoliageScreenSpaceShadow;
#endif
    return SurfaceData;
}

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

BurtSurfaceData BurtApplyGrassXRenderSurfaceSemantics(
    BurtSurfaceData SurfaceData,
    float3 NormalWS,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float4 VertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
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

#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
float BurtEvaluateMaterialPassOpacity(float Alpha, float2 BaseMapUV, float3 PositionWS)
{
    return Alpha;
}
#endif

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
float BurtEvaluateTrunkVertexAO(float4 VertexColor)
{
    return saturate((saturate(VertexColor.a) - _VertexAORemap.x) / max(_VertexAORemap.y - _VertexAORemap.x, BURT_EPSILON));
}

BurtSurfaceData BurtApplyTrunkXRenderSurfaceSemantics(BurtSurfaceData SurfaceData, float4 MaskMap, float4 VertexColor)
{
    float MapOcclusion = saturate(MaskMap.g);
    float VertexAO = BurtEvaluateTrunkVertexAO(VertexColor);

    SurfaceData.Metallic = 0.0f;
    SurfaceData.Anisotropy = 0.0f;
    SurfaceData.Reflectance = saturate(_Specular);
    SurfaceData.Smoothness = saturate(1.0f - saturate(MaskMap.a));
    SurfaceData.Occlusion = min(MapOcclusion, VertexAO);
    SurfaceData.Height = 0.5f;
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    return SurfaceData;
}
#endif

float BurtEvaluateMaterialPassRegularOpacity(float Alpha, float Cutoff)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return Alpha - saturate(Cutoff);
#else
    return Alpha;
#endif
}

float BurtEvaluateMaterialPassRegularOpacity(float Alpha)
{
    return BurtEvaluateMaterialPassRegularOpacity(Alpha, _Cutoff);
}

float BurtEvaluateMaterialPassShadowOpacity(float Alpha)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return saturate(Alpha - saturate(_ShadowCutOff));
#else
    return Alpha;
#endif
}

void BurtApplyMaterialPassAlphaClip(float Alpha, float AlphaClip, float Cutoff)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtApplyAlphaClip(BurtEvaluateMaterialPassRegularOpacity(Alpha, Cutoff), AlphaClip, 0.0f);
#else
    BurtApplyAlphaClip(Alpha, AlphaClip, Cutoff);
#endif
}

void BurtApplyMaterialPassAlphaClip(float Alpha, float AlphaClip, float Cutoff, float4 PositionCS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    BurtApplyHairDitherAlphaClip(Alpha, AlphaClip, Cutoff, PositionCS);
#else
    BurtApplyAlphaClip(Alpha, AlphaClip, Cutoff);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(float4 BaseColor, float4 MaskMap)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float HairReflectance = saturate(_Reflectance * _HairSpecularScale);
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, HairReflectance, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    SurfaceData.Smoothness = saturate(SurfaceData.Smoothness - _HairRoughnessOffset);
    float HairShiftScale = saturate(_HairShiftScale * MaskMap.b);
    return BurtApplyHairGBufferSurfaceSemantics(SurfaceData, (_HairScatter + _HairScatterBoost) * MaskMap.r, HairShiftScale);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, BURT_SUBSURFACE_FIXED_REFLECTANCE, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    float Subsurface3SCurvature = saturate(MaskMap.b * _Subsurface3SCurvatureScale + _Subsurface3SCurvatureBias);
    return BurtApplySubsurfaceSurfaceSemantics(SurfaceData, _SubsurfaceThickness, _SubsurfacePower, _SubsurfaceDistortion, _SubsurfaceAmbient, Subsurface3SCurvature, _SubsurfaceProfileIndex, _SubsurfaceScatteringMode);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    return BurtCreateGrassXRenderSurfaceData(BaseColor, MaskMap);
    #else
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    return BurtApplyFoliageXRenderSurfaceSemantics(SurfaceData, BaseColor, MaskMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
    #endif
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, saturate(_Specular), 1.0f, 0.0f, float4(0.0f, MaskMap.g, 0.5f, 1.0f), 1.0f);
    return BurtApplyTrunkXRenderSurfaceSemantics(SurfaceData, MaskMap, float4(1.0f, 1.0f, 1.0f, 1.0f));
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    float EyeReflectance = saturate(_ScleraSpecular);
    float EyeSmoothness = saturate(1.0f - _ScleraRoughness);
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, EyeReflectance, EyeSmoothness, 0.0f, MaskMap, 1.0f);
    return BurtApplyEyeSurfaceSemantics(SurfaceData, 0.0f, float3(0.0f, 0.0f, 1.0f), float3(0.0f, 0.0f, 1.0f));
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    BurtSurfaceData SurfaceData = BurtCreateFabricSurfaceData(BaseColor, _Reflectance, _Roughness, _Metallic, MaskMap, _OcclusionStrength);
    return SurfaceData;
#else
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, _Smoothness, _Metallic, MaskMap, _OcclusionStrength);

    #if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
        SurfaceData = BurtApplyAnisotropySurfaceSemantics(SurfaceData, _Anisotropy);
        SurfaceData = BurtApplyClearCoatSurfaceSemantics(SurfaceData, _ClearCoatMask, _ClearCoatRoughness);
    #else
        SurfaceData = BurtApplyAnisotropySurfaceSemantics(SurfaceData, _Anisotropy);
    #endif

    return SurfaceData;
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(float4 BaseColor, float4 MaskMap, float2 UV0)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, BURT_SUBSURFACE_FIXED_REFLECTANCE, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    float SubsurfaceThickness = saturate(_SubsurfaceThickness);
    if (!BurtIsSubsurface3SPreIntegratedMode(_SubsurfaceScatteringMode))
    {
        SubsurfaceThickness = BurtEvaluateSubsurfaceMaterialThickness(UV0);
    }
    float Subsurface3SCurvature = saturate(MaskMap.b * _Subsurface3SCurvatureScale + _Subsurface3SCurvatureBias);
    return BurtApplySubsurfaceSurfaceSemantics(SurfaceData, SubsurfaceThickness, _SubsurfacePower, _SubsurfaceDistortion, _SubsurfaceAmbient, Subsurface3SCurvature, _SubsurfaceProfileIndex, _SubsurfaceScatteringMode);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    BurtSurfaceData SurfaceData = BurtCreateFabricSurfaceData(BaseColor, _Reflectance, _Roughness, _Metallic, MaskMap, _OcclusionStrength);
    return BurtApplyFabricPassSurfaceSemantics(SurfaceData, MaskMap, UV0);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    return BurtCreateGrassXRenderSurfaceData(BaseColor, MaskMap);
    #else
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    float4 FoliageMap = BurtResolveFoliageSurfaceMap(UV0, MaskMap);
    return BurtApplyFoliageXRenderSurfaceSemantics(SurfaceData, BaseColor, FoliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
    #endif
#else
    return BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(float4 BaseColor, float4 MaskMap, float2 UV0, float3 NormalWS, float3 ViewDirectionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    return BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, UV0);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    BurtSurfaceData SurfaceData = BurtCreateFabricSurfaceData(BaseColor, _Reflectance, _Roughness, _Metallic, MaskMap, _OcclusionStrength);
    float NdotV = saturate(dot(BurtSafeNormalize(NormalWS), BurtSafeNormalize(ViewDirectionWS)));
    return BurtApplyFabricPassSurfaceSemantics(SurfaceData, MaskMap, UV0, NdotV);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    return BurtCreateGrassXRenderSurfaceData(BaseColor, MaskMap);
    #else
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    float4 FoliageMap = BurtResolveFoliageSurfaceMap(UV0, MaskMap);
    return BurtApplyFoliageXRenderSurfaceSemantics(SurfaceData, BaseColor, FoliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
    #endif
#else
    return BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(
    float4 BaseColor,
    float4 MaskMap,
    float2 UV0,
    float2 UV1,
    float3 PositionOS,
    float3 GeometryNormalWS,
    float4 TangentWS,
    float3 ViewDirectionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float4 IDValue = SAMPLE_TEXTURE2D(_IDMap, sampler_LinearRepeat, UV0 * float2(_IDXTilling, 1.0f));
    float NdotV = saturate(dot(BurtSafeNormalize(GeometryNormalWS), ViewDirectionWS));
    float EdgeRoughness = lerp(0.0f, _RoughParameter.z, saturate(pow(1.0f - NdotV, _EdgeRoughRimPower)));
    float2 Roughness = saturate(_RoughParameter.xy + EdgeRoughness.xx);
    float Scatter = _ScatterUseFullRange > 0.33f ? _ScatterFull : _Scatter;
    Scatter = max(Scatter, (_HairScatter + _HairScatterBoost) * MaskMap.r);

    float3 HairShadowFactor = BurtAvatarHairStructureFactor(saturate(IDValue.g));
    float Reflectance = MaskMap.r * _Reflectance * _HairSpecularScale;
    Reflectance = lerp(Reflectance, Reflectance * PerceivedLuminance(HairShadowFactor), _HairShadowPower);

    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, Reflectance, 1.0f - Roughness.x, 0.0f, float4(MaskMap.r, MaskMap.g, MaskMap.b, 1.0f), _Occlusion);
    SurfaceData.Occlusion = lerp(1.0f, MaskMap.g, _Occlusion);
    SurfaceData.Height = saturate(MaskMap.b);
    SurfaceData.HairSecondaryRoughness = Roughness.y;
    SurfaceData.HairBackLight = (_BackLightIntensity - BaseColor.a * _BackLightIntensity) *
        lerp(1.0f, pow(saturate(1.0f - NdotV), rcp(max(BURT_EPSILON, _BackLightMaskRange))), _BackLightMask);
    SurfaceData.HairShadowFillStrength = _HairShadowFillStrength;
    SurfaceData.HairSpecularShift = _SpecularShift * 1.98f + 1.36f;
    SurfaceData.HairSecondarySpecularShift = _SecondarySpecularShift * 3.33f + 1.56f;
    SurfaceData.HairSpecularColor = _SpecularColor.rgb;
    SurfaceData.HairSecondarySpecularColor = _SpecularSecondColor.rgb;
    return BurtApplyHairGBufferSurfaceSemantics(SurfaceData, Scatter, 1.0f);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC) && !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) && !defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
    return BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, UV0, GeometryNormalWS, ViewDirectionWS);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    return BurtCreateGrassXRenderSurfaceData(BaseColor, MaskMap);
    #else
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    float4 FoliageMap = BurtResolveFoliageSurfaceMap(UV0, MaskMap);
    return BurtApplyFoliageXRenderSurfaceSemantics(SurfaceData, BaseColor, FoliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
    #endif
#else
    return BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(
    float4 BaseColor,
    float4 MaskMap,
    float2 UV0,
    float3 NormalWS,
    float3 ViewDirectionWS,
    float3 PositionWS,
    float3 PositionOS,
    float4 VertexColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    #if defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    BurtSurfaceData SurfaceData = BurtCreateGrassXRenderSurfaceData(BaseColor, MaskMap);
    SurfaceData = BurtApplyGrassMaterialExtras(SurfaceData, PositionWS, VertexColor);
    return BurtApplyGrassXRenderSurfaceSemantics(SurfaceData, NormalWS, ViewDirectionWS, PositionWS, VertexColor);
    #else
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    float4 FoliageMap = BurtResolveFoliageSurfaceMap(UV0, MaskMap);
    SurfaceData = BurtApplyFoliageXRenderSurfaceSemantics(SurfaceData, BaseColor, FoliageMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
    SurfaceData = BurtApplyFoliageMaterialExtras(SurfaceData, PositionWS, PositionOS, VertexColor);
    return SurfaceData;
    #endif
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, saturate(_Specular), 1.0f, 0.0f, float4(0.0f, MaskMap.g, 0.5f, 1.0f), 1.0f);
    return BurtApplyTrunkXRenderSurfaceSemantics(SurfaceData, MaskMap, VertexColor);
#else
    return BurtCreateMaterialShadingModelSurfaceData(BaseColor, MaskMap, UV0, NormalWS, ViewDirectionWS);
#endif
}

BurtSurfaceData BurtCreateMaterialShadingModelSurfaceData(
    float4 BaseColor,
    float4 MaskMap,
    float2 UV0,
    float3 NormalWS,
    float3 ViewDirectionWS,
    float3 PositionWS)
{
    return BurtCreateMaterialShadingModelSurfaceData(
        BaseColor,
        MaskMap,
        UV0,
        NormalWS,
        ViewDirectionWS,
        PositionWS,
        float3(0.0f, 0.0f, 0.0f),
        float4(1.0f, 1.0f, 1.0f, 1.0f));
}

float3 BurtGetMaterialPassNormalWS(float2 NormalMapUV, float3 NormalWS, float4 TangentWS, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !defined(BURT_MATERIAL_SELECTED_FOLIAGE_IS_GRASS)
    return BurtSampleFoliageNSRNormalWS(NormalMapUV, NormalWS, TangentWS, _NormalScale, Facing, _DoubleSidedNormalModeConstants);
#else
    return BurtSampleNormalWS(NormalMapUV, NormalWS, TangentWS, _NormalScale, Facing, _DoubleSidedNormalModeConstants);
#endif
}

float3 BurtGetMaterialPassGeometryNormalWS(float3 NormalWS, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtSafeNormalize(NormalWS);
#else
    return NormalWS;
#endif
}

float3 BurtGetMaterialPassShadingDirectionWS(float3 NormalWS, float4 TangentWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float StrandDirectionSign = lerp(1.0f, -1.0f, saturate(_HairTangentFlip));
    return BurtSafeNormalize(TangentWS.xyz * StrandDirectionSign);
#else
    return NormalWS;
#endif
}

float3 BurtGetMaterialPassShadingDirectionWS(float2 UV0, float3 NormalWS, float4 TangentWS, float Facing)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    float IDValue = SAMPLE_TEXTURE2D(_IDMap, sampler_LinearRepeat, UV0 * float2(_IDXTilling, 1.0f)).r;
    float3 HairTangentTS = lerp(_TangentA.xyz, _TangentB.xyz, IDValue) * _IDIntensity;
    HairTangentTS = BurtSafeNormalize(HairTangentTS + float3(0.0f, 1.0f, 0.0f));

    float Angle = _HairRotate * 6.28318530718f;
    float SinAngle;
    float CosAngle;
    sincos(Angle, SinAngle, CosAngle);
    float3 RotatedTangentTS = float3(
        HairTangentTS.x * CosAngle - HairTangentTS.y * SinAngle,
        HairTangentTS.x * SinAngle + HairTangentTS.y * CosAngle,
        HairTangentTS.z);
    HairTangentTS = BurtSafeNormalize(RotatedTangentTS + HairTangentTS);
    HairTangentTS = BurtApplyDoubleSidedNormalMode(HairTangentTS, Facing, _DoubleSidedNormalModeConstants);
    float StrandDirectionSign = lerp(1.0f, -1.0f, saturate(_HairTangentFlip));
    return BurtTransformTangentToWorld(HairTangentTS, NormalWS, TangentWS) * StrandDirectionSign;
#else
    return BurtGetMaterialPassShadingDirectionWS(NormalWS, TangentWS);
#endif
}

float3 BurtGetMaterialPassShadingDirectionWS(float2 UV0, float3 NormalWS, float4 TangentWS)
{
    return BurtGetMaterialPassShadingDirectionWS(UV0, NormalWS, TangentWS, 1.0f);
}

float3 BurtGetMaterialPassDebugNormalWS(float3 NormalWS, float3 ShadingDirectionWS)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return ShadingDirectionWS;
#else
    return NormalWS;
#endif
}

#define BURT_CREATE_MATERIAL_PASS_GBUFFER_DATA(ShadingModelName, SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor) \
    BURT_TOKEN_PASTE2(BurtCreateMaterialPassGBufferData_, ShadingModelName)(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor)

BurtGBufferData BurtCreateMaterialPassGBufferData_DefaultLit(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}

BurtGBufferData BurtCreateMaterialPassGBufferData_Hair(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateHairGBufferData(SurfaceData, ShadingDirectionWS, BaseNormalWS, GeometryNormalWS, EmissionColor);
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
BurtGBufferData BurtCreateMaterialPassGBufferData_ClearCoat(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    float3 ClearCoatNormalWS = BurtSampleClearCoatNormalWS(NormalMapUV, GeometryNormalWS, TangentWS, _ClearCoatNormalScale, Facing, _DoubleSidedNormalModeConstants);
    return BurtCreateClearCoatGBufferData(SurfaceData, BaseNormalWS, TangentWS, ClearCoatNormalWS, EmissionColor);
}
#endif

BurtGBufferData BurtCreateMaterialPassGBufferData_Subsurface(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateSubsurfaceGBufferData(SurfaceData, BaseNormalWS, GeometryNormalWS, TangentWS, EmissionColor);
}

BurtGBufferData BurtCreateMaterialPassGBufferData_Foliage(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateFoliageGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}

BurtGBufferData BurtCreateMaterialPassGBufferData_Fabric(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateFabricGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}

BurtGBufferData BurtCreateMaterialPassGBufferData_Fur(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateFurGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}

BurtGBufferData BurtCreateMaterialPassGBufferData_Eye(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateEyeGBufferData(SurfaceData, BaseNormalWS, TangentWS, SurfaceData.EyeIrisNormalWS, SurfaceData.EyeCausticNormalWS, EmissionColor);
}

BurtGBufferData BurtCreateMaterialPassGBufferData(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BURT_CREATE_MATERIAL_PASS_GBUFFER_DATA(
        BURT_MATERIAL_SELECTED_SHADING_MODEL_NAME,
        SurfaceData,
        NormalMapUV,
        GeometryNormalWS,
        BaseNormalWS,
        TangentWS,
        ShadingDirectionWS,
        Facing,
        EmissionColor);
}

#endif // BURT_MATERIAL_SHADING_MODEL_PASS_COMMON_INCLUDED
