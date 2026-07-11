// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_SURFACE_INCLUDED
#define BURT_MATERIAL_PASS_SURFACE_INCLUDED

#if !defined(BURT_MATERIAL_COMPILE_GRASS_FOLIAGE)
#define BURT_MATERIAL_COMPILE_GRASS_FOLIAGE 0
#endif

#if !defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
float BurtEvaluateMaterialPassOpacity(float Alpha, float2 BaseMapUV, float3 PositionWS)
{
    return Alpha;
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
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
    return BurtCreateGrassXRenderSurfaceData(BaseColor, MaskMap);
    #else
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, _Reflectance, _Smoothness, 0.0f, MaskMap, _OcclusionStrength);
    return BurtApplyFoliageXRenderSurfaceSemantics(SurfaceData, BaseColor, MaskMap, _ThicknessScale, _RoughnessScale, _ReflectanceScale, _SubsurfaceColor.rgb, _SubsurfaceColorSaturate, _TransmissionNdotL, _FoliageBackLight, _ReflectanceScale, 1.0f);
    #endif
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
    BurtSurfaceData SurfaceData = BurtCreateSurfaceData(BaseColor, saturate(_Specular), 1.0f, 0.0f, float4(0.0f, MaskMap.g, 0.5f, 1.0f), 1.0f);
    return BurtApplyTrunkXRenderSurfaceSemantics(SurfaceData, MaskMap, float4(1.0f, 1.0f, 1.0f, 1.0f));
#elif defined(BURT_MATERIAL_SELECTED_INTERIOR_MAPPING)
    return BurtCreateInteriorMappingSurfaceData(BaseColor, MaskMap);
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
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
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
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
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
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
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
    #if BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
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
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE) && !BURT_MATERIAL_COMPILE_GRASS_FOLIAGE
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

#endif // BURT_MATERIAL_PASS_SURFACE_INCLUDED
