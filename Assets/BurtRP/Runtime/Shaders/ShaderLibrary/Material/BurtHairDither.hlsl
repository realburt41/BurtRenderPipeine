// XRender-style screen-space coverage dither used by AvatarHair opacity masking.
#ifndef BURT_HAIR_DITHER_INCLUDED
#define BURT_HAIR_DITHER_INCLUDED

float _BurtHairDitherFrameIndex;

float BurtGetMaterialCoverageAndClipping(float opacity, float3 svPosition, float taaIndex, float offset)
{
    float2 pos = svPosition.xy;
    float dither5 = frac((pos.x + pos.y * 2.0f - 1.5f + taaIndex) / 5.0f);
    float noise = frac(dot(float2(171.0f, 231.0f) / 71.0f, pos.xy));
    float dither = (dither5 * 5.0f + noise) * (1.0f / 6.0f);
    return opacity + dither - offset;
}

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR) || defined(BURT_MATERIAL_SHADING_MODEL_HAIR)
float BurtEvaluateHairDitheredOpacity(float alpha, float cutoff, float4 positionCS)
{
    float opacity = alpha - saturate(cutoff);
    return BurtGetMaterialCoverageAndClipping(opacity, float3(positionCS.xy, positionCS.z), _BurtHairDitherFrameIndex, _OpacityMaskOffset);
}

float BurtEvaluateHairDitheredOpacity(float alpha, float4 positionCS)
{
    return BurtEvaluateHairDitheredOpacity(alpha, _Cutoff, positionCS);
}

void BurtApplyHairDitherAlphaClip(float alpha, float alphaClip, float cutoff, float4 positionCS)
{
    BurtApplyAlphaClip(BurtEvaluateHairDitheredOpacity(alpha, cutoff, positionCS), alphaClip, 0.0f);
}

void BurtApplyHairDitherAlphaClip(float alpha, float alphaClip, float4 positionCS)
{
    BurtApplyHairDitherAlphaClip(alpha, alphaClip, _Cutoff, positionCS);
}
#endif

#endif // BURT_HAIR_DITHER_INCLUDED
