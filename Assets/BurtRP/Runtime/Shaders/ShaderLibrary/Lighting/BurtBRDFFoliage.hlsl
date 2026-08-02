#ifndef BURT_BRDF_FOLIAGE_INCLUDED
#define BURT_BRDF_FOLIAGE_INCLUDED

#if BURT_MODEL_HAS_FOLIAGE
float3 BurtTransmittanceToExtinction(float3 transmittanceColor, float thicknessInMeters)
{
    return -log(clamp(transmittanceColor, BURT_PARTICIPATING_MEDIA_MIN_TRANSMITTANCE, 1.0f)) / max(BURT_PARTICIPATING_MEDIA_MIN_MFP_METER, thicknessInMeters);
}

float3 BurtTransmittanceToMeanFreePath(float3 transmittanceColor, float thicknessInMeters)
{
    return 1.0f / max(BURT_PARTICIPATING_MEDIA_MIN_EXTINCTION, BurtTransmittanceToExtinction(transmittanceColor, thicknessInMeters));
}

float3 BurtExtinctionToTransmittance(float3 extinction, float thicknessInMeters)
{
    return exp(-extinction * thicknessInMeters);
}

float3 BurtIsotropicMediumSlabTransmittance(float3 extinctionCoef, float thicknessInMeters, float noV)
{
    float3 safeExtinction = max(float3(0.000001f, 0.000001f, 0.000001f), extinctionCoef);
    float pathLength = thicknessInMeters / max(0.0001f, abs(noV));
    return BurtExtinctionToTransmittance(safeExtinction, pathLength);
}

float3 BurtEvaluateFoliageSlabSubsurfaceColor(float3 foliageTransmissionColor)
{
    float3 meanFreePath = BurtTransmittanceToMeanFreePath(foliageTransmissionColor, BURT_VOLUME_DEFAULT_THICKNESS_M);
    float3 minMeanFreePath = float3(
        BURT_PARTICIPATING_MEDIA_MIN_MFP_METER,
        BURT_PARTICIPATING_MEDIA_MIN_MFP_METER,
        BURT_PARTICIPATING_MEDIA_MIN_MFP_METER);
    float3 extinction = 1.0f / max(minMeanFreePath, meanFreePath);
    return BurtIsotropicMediumSlabTransmittance(extinction, BURT_VOLUME_DEFAULT_THICKNESS_M, 1.0f);
}
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
void BurtApplyFoliageDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation)
{
    if (materialData.FoliageActive <= 0.0001f)
    {
        return;
    }

    float transmissionWeight = materialData.FoliageIsGrass > 0.5f
        ? max(materialData.FoliageTransmissionWeight, 0.0f)
        : saturate(materialData.FoliageTransmissionWeight);
    if (transmissionWeight <= 0.0001f)
    {
        return;
    }

    float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float foliageTransmissionShadowAttenuation = saturate(shadowAttenuation);
    if (materialData.FoliageIsGrass > 0.5f)
    {
        float voL = dot(v, l);
        float phase = (-voL) * 0.5f + 0.5f;
        float transLightVoL = saturate(Pow5(phase));
        float average = (materialData.BaseColor.r + materialData.BaseColor.g + materialData.BaseColor.b) * 0.3333f;
        float3 sssColor = saturate((materialData.BaseColor - average.xxx) * 0.35f + materialData.BaseColor);
        float3 sssLight = sssColor * transLightVoL * transmissionWeight * lightColor * foliageTransmissionShadowAttenuation;
        float sssBlend = 0.35f * saturate(transmissionWeight * 10.0f);
        float3 baseDiffuse = components.Diffuse;
        float3 blendedDiffuse = lerp(baseDiffuse, sssLight, sssBlend);
        float3 addedTransmission = max(blendedDiffuse - baseDiffuse, float3(0.0f, 0.0f, 0.0f));

        components.Transmission += addedTransmission;
        components.TransmissionBRDF += sssColor * transLightVoL * transmissionWeight;
        components.TransmissionThroughput = max(components.TransmissionThroughput, sssColor);
        components.TransmissionLobe = max(components.TransmissionLobe, transLightVoL * transmissionWeight);
        components.TransmissionShadow = foliageTransmissionShadowAttenuation;
        components.TransmissionThickness = max(components.TransmissionThickness, saturate(materialData.FoliageThickness));
        components.Diffuse = blendedDiffuse;
        components.BrdfTerms.DiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, sssColor * transLightVoL * transmissionWeight, sssBlend);
        components.BrdfTerms.DiffuseLobe = max(components.BrdfTerms.DiffuseLobe, transLightVoL * transmissionWeight);
        return;
    }

    float voL = dot(v, l);
    float noL = dot(n, l);
    float wrap = saturate(materialData.FoliageTransmissionNdotL);
    float wrapNoL = saturate((-noL + wrap) / max((1.0f + wrap) * (1.0f + wrap), BURT_EPSILON));
    float scatter = D_GGX(0.36f, saturate(-voL));
    float lobe = max(scatter * wrapNoL, 0.0f);
    float3 foliageTransmissionColor = max(materialData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
    float3 foliageSlabSubsurfaceColor = BurtEvaluateFoliageSlabSubsurfaceColor(foliageTransmissionColor);
    float3 transmissionBRDF = foliageSlabSubsurfaceColor * transmissionWeight * lobe;
    float3 transmission = transmissionBRDF * lightColor * foliageTransmissionShadowAttenuation;

    components.Transmission += transmission;
    components.TransmissionBRDF += transmissionBRDF;
    components.TransmissionThroughput = max(components.TransmissionThroughput, foliageSlabSubsurfaceColor);
    components.TransmissionLobe = max(components.TransmissionLobe, lobe);
    components.TransmissionShadow = foliageTransmissionShadowAttenuation;
    components.TransmissionThickness = max(components.TransmissionThickness, saturate(materialData.FoliageThickness));
    components.Diffuse += transmission;
    components.BrdfTerms.DiffuseBRDF += transmissionBRDF;
    components.BrdfTerms.DiffuseLobe = max(components.BrdfTerms.DiffuseLobe, lobe * transmissionWeight);
}

void BurtApplyFoliageDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation,
    float resolvedTransmissionThickness)
{
    BurtApplyFoliageDirectPBR(
        components,
        materialData,
        geometryData,
        lightColor,
        lightDirectionWS,
        shadowAttenuation,
        transmissionShadowAttenuation);
}
#endif

#endif // BURT_BRDF_FOLIAGE_INCLUDED

