// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_FABRIC_INCLUDED
#define BURT_MATERIAL_PASS_FABRIC_INCLUDED

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

#endif // BURT_MATERIAL_PASS_FABRIC_INCLUDED
