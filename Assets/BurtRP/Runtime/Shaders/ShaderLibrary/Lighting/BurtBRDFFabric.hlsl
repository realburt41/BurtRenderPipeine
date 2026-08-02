#ifndef BURT_BRDF_FABRIC_INCLUDED
#define BURT_BRDF_FABRIC_INCLUDED

#if BURT_MODEL_HAS_FABRIC
float BurtClothEnergyLookup(float roughness, float noV)
{
    float c = saturate(noV);
    float r = saturate(roughness);
    return (0.526422f / ((-0.227114f + r) * (-0.968835f + r) * ((5.38869f - 20.2835f * c) * r) - (-1.18761f - ((2.58744f - c) * c)))) + 0.0615456f;
}

float BurtComputeWrappedDiffuseLighting(float noL, float wrap)
{
    float safeWrap = saturate(wrap);
    float denominator = (1.0f + safeWrap) * (1.0f + safeWrap);
    return saturate((noL + safeWrap) / max(denominator, BURT_EPSILON));
}
#endif

#if BURT_MODEL_HAS_FABRIC
float D_Charlie(float linearRoughness, float noH)
{
    float invAlpha = rcp_safe(max(linearRoughness, 0.001f));
    float cos2h = noH * noH;
    float sin2h = max(1.0f - cos2h, 0.001f);
    return (2.0f + invAlpha) * pow(sin2h, invAlpha * 0.5f) * (0.5f * BURT_INV_PI);
}

float V_Neubelt(float noV, float noL)
{
    return rcp_safe(4.0f * (saturate(noL) + saturate(noV) - saturate(noL) * saturate(noV)));
}
#endif

#if BURT_ENABLE_FABRIC_SHADING
void BurtApplyFabricDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    if (materialData.FabricActive <= 0.0001f || materialData.FabricIsSilk > 0.5f)
    {
        return;
    }

    float fuzzWeight = saturate(materialData.FabricFuzzWeight);
    if (fuzzWeight <= 0.0001f)
    {
        return;
    }

    float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float3 h = BurtSafeNormalize(l + v);
    float noL = saturate(dot(n, l));
    float noV = saturate(geometryData.NDotV);
    float noH = saturate(dot(n, h));
    float loH = saturate(dot(l, h));
    float fuzzRoughness = ClampPerceptualRoughness(materialData.FabricFuzzRoughness);
    float fuzzLinearRoughness = PerceptualRoughnessToLinearRoughness(fuzzRoughness);
    float fuzzD = D_Charlie(fuzzLinearRoughness, noH);
    float fuzzVisibility = V_Neubelt(noV, noL);
    float3 fuzzFresnel = F_Schlick_UE(materialData.FabricFuzzColor, loH);
    float3 fuzzBRDF = fuzzD * fuzzVisibility * fuzzFresnel;
    float3 fuzzSpecular = fuzzBRDF * lightColor * noL * shadowAttenuation;
    float fuzzEnergyPreservation = saturate(1.0f - BurtClothEnergyLookup(fuzzRoughness, noV));
    float baseLayerWeight = lerp(1.0f, fuzzEnergyPreservation, fuzzWeight);

    components.Diffuse *= baseLayerWeight;
    components.Specular = components.Specular * baseLayerWeight + fuzzSpecular * fuzzWeight;
    components.EnergyPreservation = saturate(components.EnergyPreservation * baseLayerWeight);
    components.BrdfTerms.DiffuseBRDF *= baseLayerWeight;
    components.BrdfTerms.SpecularBRDF = components.BrdfTerms.SpecularBRDF * baseLayerWeight + fuzzBRDF * fuzzWeight;
    components.BrdfTerms.PerceptualRoughness = lerp(components.BrdfTerms.PerceptualRoughness, fuzzRoughness, fuzzWeight);
    components.BrdfTerms.LinearRoughness = lerp(components.BrdfTerms.LinearRoughness, fuzzLinearRoughness, fuzzWeight);
    components.BrdfTerms.A2 = lerp(components.BrdfTerms.A2, LinearRoughnessToA2(fuzzLinearRoughness), fuzzWeight);
    components.BrdfTerms.D = lerp(components.BrdfTerms.D, fuzzD, fuzzWeight);
    components.BrdfTerms.Visibility = lerp(components.BrdfTerms.Visibility, fuzzVisibility, fuzzWeight);
    components.BrdfTerms.Fresnel = lerp(components.BrdfTerms.Fresnel, fuzzFresnel, fuzzWeight);
}

void BurtApplySilkWrappedDiffuseDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    if (materialData.FabricActive <= 0.0001f || materialData.FabricIsSilk <= 0.5f)
    {
        return;
    }

    float3 l = BurtSafeNormalize(lightDirectionWS);
    float rawNoL = dot(geometryData.NormalWS, l);
    float wrappedNoL = BurtComputeWrappedDiffuseLighting(-rawNoL, cos(BURT_PI * (5.0f / 12.0f)));
    float3 wrappedDiffuse = components.BrdfTerms.DiffuseBRDF * lightColor * wrappedNoL * shadowAttenuation;

    components.Diffuse += wrappedDiffuse;
}
#endif

#endif // BURT_BRDF_FABRIC_INCLUDED

