#ifndef BURT_BRDF_CLEAR_COAT_INCLUDED
#define BURT_BRDF_CLEAR_COAT_INCLUDED

#if BURT_MODEL_HAS_CLEAR_COAT
float BurtRefractBlendClearCoatApprox(float voH)
{
    float safeVoH = saturate(voH);
    return (0.63f - 0.22f * safeVoH) * safeVoH - 0.745f;
}

float3 BurtClearCoatFresnelTransmission(float3 clearCoatFresnel)
{
    float3 transmission = saturate(1.0f - clearCoatFresnel);
    return transmission * transmission;
}

float3 BurtSimpleClearCoatTransmittance(float noL, float noV, float metallic, float3 baseColor)
{
    float3 transmittance = float3(1.0f, 1.0f, 1.0f);
    float clearCoatCoverage = saturate(metallic);
    if (clearCoatCoverage > 0.0001f)
    {
        const float layerThickness = 1.0f;
        float thinDistance = layerThickness * (rcp_safe(max(noV, 0.001f)) + rcp_safe(max(noL, 0.001f)));
        thinDistance = min(thinDistance, 4.0f);
        float3 transmittanceColor = max(baseColor * Fd_Lambert(), float3(0.001f, 0.001f, 0.001f));
        float3 extinctionCoefficient = -log(transmittanceColor) / (2.0f * layerThickness);
        float3 opticalDepth = extinctionCoefficient * max(thinDistance - 2.0f * layerThickness, 0.0f);
        transmittance = exp(-opticalDepth);
        transmittance = lerp(float3(1.0f, 1.0f, 1.0f), transmittance, clearCoatCoverage);
    }

    return transmittance;
}

float3 BurtSimpleClearCoatTransmittanceFromView(float noV, float metallic, float3 baseColor)
{
    return BurtSimpleClearCoatTransmittance(noV, noV, metallic, baseColor);
}
#endif

#endif // BURT_BRDF_CLEAR_COAT_INCLUDED

