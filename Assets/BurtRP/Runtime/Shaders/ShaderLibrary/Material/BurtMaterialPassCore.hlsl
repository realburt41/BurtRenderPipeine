// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_CORE_INCLUDED
#define BURT_MATERIAL_PASS_CORE_INCLUDED

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

#endif // BURT_MATERIAL_PASS_CORE_INCLUDED
