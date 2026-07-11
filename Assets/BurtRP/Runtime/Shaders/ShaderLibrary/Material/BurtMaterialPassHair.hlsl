// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_HAIR_INCLUDED
#define BURT_MATERIAL_PASS_HAIR_INCLUDED

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

#endif // BURT_MATERIAL_PASS_HAIR_INCLUDED
