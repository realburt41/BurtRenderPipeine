#ifndef BURT_BRDF_SUBSURFACE_INCLUDED
#define BURT_BRDF_SUBSURFACE_INCLUDED

#if BURT_ENABLE_SUBSURFACE_SHADING
Texture2DArray _BurtSubsurfacePreIntegratedLut;
float _BurtSubsurfacePreIntegratedLutEnabled;
Texture2DArray _BurtSubsurfaceSHLut;
float _BurtSubsurfaceSHLutEnabled;
float _BurtSubsurfaceProfileCount;
float4 _BurtSubsurfaceProfileDualSpeculars[8];
float4 _BurtSubsurfaceProfileTransmissions[8];
float4 _BurtSubsurfaceProfileTransmissionTints[8];
#if defined(SHADER_TARGET) && SHADER_TARGET < 35
    Texture2D _BurtSubsurfaceProfileParamLut;
    #define BURT_SUBSURFACE_PROFILE_PARAM_LUT_USE_LOAD 0
#else
    Texture2D<float4> _BurtSubsurfaceProfileParamLut;
    #define BURT_SUBSURFACE_PROFILE_PARAM_LUT_USE_LOAD 1
#endif
float _BurtSubsurfaceProfileParamLutEnabled;
float4 _BurtSubsurfaceProfileParamLutSize;
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
#define BURT_SUBSURFACE_PREINTEGRATED_LUT_SIZE (256.0f)
#define BURT_SUBSURFACE_PREINTEGRATED_LUT_INV_SIZE (1.0f / BURT_SUBSURFACE_PREINTEGRATED_LUT_SIZE)
#define BURT_SUBSURFACE_SH_LUT_WIDTH (512.0f)
#define BURT_SUBSURFACE_SH_LUT_INV_WIDTH (1.0f / BURT_SUBSURFACE_SH_LUT_WIDTH)
#define BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS (2.0f)
#define BURT_SUBSURFACE_EXTINCTION_DECODE_SCALE (100.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_DUAL_SPECULAR_OFFSET (4.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_OFFSET (5.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_OFFSET (6.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_SIZE (32.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_KERNEL0_OFFSET (38.0f)
#define BURT_SUBSURFACE_PROFILE_PARAM_TINT_OFFSET (2.0f)
#define BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE (10.0f)
float4 BurtFetchSubsurfaceProfileParam(float sampleIndex, float profileIndex)
{
#if BURT_SUBSURFACE_PROFILE_PARAM_LUT_USE_LOAD
    int width = max((int)_BurtSubsurfaceProfileParamLutSize.x, 1);
    int height = max((int)_BurtSubsurfaceProfileParamLutSize.y, 1);
    int sample = clamp((int)floor(sampleIndex + 0.5f), 0, width - 1);
    int profile = clamp((int)floor(BurtClampSubsurfaceProfileIndex(profileIndex) + 0.5f), 0, height - 1);
    return _BurtSubsurfaceProfileParamLut.Load(int3(sample, profile, 0));
#else
    float width = max(_BurtSubsurfaceProfileParamLutSize.x, 1.0f);
    float height = max(_BurtSubsurfaceProfileParamLutSize.y, 1.0f);
    float resolvedProfile = BurtClampSubsurfaceProfileIndex(profileIndex);
    float2 uv = (float2(clamp(sampleIndex, 0.0f, width - 1.0f), resolvedProfile) + 0.5f) / float2(width, height);
    return BURT_SAMPLE_TEXTURE2D_CLAMP(_BurtSubsurfaceProfileParamLut, uv);
#endif
}

bool BurtUseSubsurfaceProfileParamLut()
{
    return _BurtSubsurfaceProfileParamLutEnabled > 0.5f && _BurtSubsurfaceProfileParamLutSize.x > 1.0f && _BurtSubsurfaceProfileParamLutSize.y > 0.5f;
}

float3 BurtSampleSubsurfacePreIntegratedLut(float rawNoL, float curvature, float profileIndex)
{
    float localInvSize = BURT_SUBSURFACE_PREINTEGRATED_LUT_INV_SIZE;
    int profileSlice = clamp((int)floor(BurtClampSubsurfaceProfileIndex(profileIndex) + 0.5f), 0, 7);
    float localU = saturate(rawNoL * 0.5f + 0.5f) * (1.0f - localInvSize) + 0.5f * localInvSize;
    float localV = saturate(curvature) * (1.0f - localInvSize) + 0.5f * localInvSize;
    return max(BURT_SAMPLE_TEXTURE2D_ARRAY_LOD_CLAMP(_BurtSubsurfacePreIntegratedLut, float2(localU, localV), profileSlice, 0.0f).rgb, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtSampleSubsurfaceSHLut(float curvature, float shBand, float profileIndex)
{
    float localU = saturate(curvature) * (1.0f - BURT_SUBSURFACE_SH_LUT_INV_WIDTH) + 0.5f * BURT_SUBSURFACE_SH_LUT_INV_WIDTH;
    float localV = (clamp(shBand, 0.0f, 2.0f) + 0.5f) / 3.0f;
    int profileSlice = clamp((int)floor(BurtClampSubsurfaceProfileIndex(profileIndex) + 0.5f), 0, 7);
    return max(BURT_SAMPLE_TEXTURE2D_ARRAY_LOD_CLAMP(_BurtSubsurfaceSHLut, float2(localU, localV), profileSlice, 0.0f).rgb, float3(0.0f, 0.0f, 0.0f));
}

float4 BurtLoadSubsurfaceProfileDualSpecular(float profileIndex)
{
    if (BurtUseSubsurfaceProfileParamLut())
    {
        return BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_DUAL_SPECULAR_OFFSET, profileIndex);
    }

    int index = _BurtSubsurfaceProfileCount > 0.5f
        ? clamp((int)floor(profileIndex + 0.5f), 0, 7)
        : 0;
    return _BurtSubsurfaceProfileDualSpeculars[index];
}

int BurtResolveSubsurfaceProfileArrayIndex(float profileIndex)
{
    return _BurtSubsurfaceProfileCount > 0.5f
        ? clamp((int)floor(profileIndex + 0.5f), 0, 7)
        : 0;
}

float4 BurtLoadSubsurfaceProfileTransmission(float profileIndex)
{
    if (BurtUseSubsurfaceProfileParamLut())
    {
        return BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_OFFSET, profileIndex);
    }

    return _BurtSubsurfaceProfileTransmissions[BurtResolveSubsurfaceProfileArrayIndex(profileIndex)];
}

float4 BurtLoadSubsurfaceProfileTransmissionTint(float profileIndex)
{
    if (BurtUseSubsurfaceProfileParamLut())
    {
        return BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TINT_OFFSET, profileIndex);
    }

    return _BurtSubsurfaceProfileTransmissionTints[BurtResolveSubsurfaceProfileArrayIndex(profileIndex)];
}

float BurtDecodeSubsurfaceProfileExtinctionScale(float encodedExtinctionScale)
{
    return max(encodedExtinctionScale * BURT_SUBSURFACE_EXTINCTION_DECODE_SCALE, 0.01f);
}

float BurtDecodeSubsurfaceProfileScatteringDistribution(float encodedScatteringDistribution)
{
    return clamp(encodedScatteringDistribution * 2.0f - 1.0f, -0.99f, 0.99f);
}

float BurtResolveSubsurfaceProfileThickness(float materialThickness)
{
    return lerp(0.1f, 1.0f, saturate(materialThickness));
}

float BurtResolveSubsurfaceProfileThickness(float materialThickness, float resolvedTransmissionThickness)
{
    return resolvedTransmissionThickness >= 0.0f
        ? clamp(resolvedTransmissionThickness * BurtResolveSubsurfaceProfileThickness(materialThickness), 0.0f, BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE)
        : BurtResolveSubsurfaceProfileThickness(materialThickness);
}

bool BurtHasResolvedSubsurfaceTransmissionThickness(float resolvedTransmissionThickness)
{
    return resolvedTransmissionThickness >= 0.0f;
}

float BurtResolveSubsurfaceTransmissionLightingThickness(
    float materialThickness,
    float resolvedTransmissionThickness,
    float nDotL)
{
    float thickness = BurtResolveSubsurfaceProfileThickness(materialThickness, resolvedTransmissionThickness);
    float threshold = lerp(0.1f, 0.01f, abs(nDotL));
    return clamp(max(thickness, threshold), 0.001f, BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE);
}

float3 BurtSampleSubsurfaceTransmissionProfileByThickness(float profileIndex, float profileThickness)
{
    float4 profileTransmission = BurtLoadSubsurfaceProfileTransmission(profileIndex);
    float extinctionScale = BurtDecodeSubsurfaceProfileExtinctionScale(profileTransmission.x);
    profileThickness = clamp(profileThickness, 0.0f, BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE);
    float3 fallback = exp2(-1.442695f * extinctionScale * profileThickness).xxx;
    if (!BurtUseSubsurfaceProfileParamLut())
    {
        return fallback;
    }

    float samplePosition = saturate(profileThickness / BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE) * (BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_SIZE - 1.0f);
    float lowerSample = floor(samplePosition);
    float upperSample = min(lowerSample + 1.0f, BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_SIZE - 1.0f);
    float sampleT = samplePosition - lowerSample;
    float3 lower = BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_OFFSET + lowerSample, profileIndex).rgb;
    float3 upper = BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_TRANSMISSION_PROFILE_OFFSET + upperSample, profileIndex).rgb;
    return max(lerp(lower, upper, sampleT), float3(0.0f, 0.0f, 0.0f));
}

float3 BurtSampleSubsurfaceTransmissionThroughputByThickness(float profileIndex, float profileThickness)
{
    float3 throughput = BurtSampleSubsurfaceTransmissionProfileByThickness(profileIndex, profileThickness);
    if (!BurtUseSubsurfaceProfileParamLut())
    {
        throughput *= max(BurtLoadSubsurfaceProfileTransmissionTint(profileIndex).rgb, float3(0.0f, 0.0f, 0.0f));
    }

    return max(throughput, float3(0.0f, 0.0f, 0.0f));
}

float BurtEvaluateSubsurfaceProfileIntensity(float3 profileColor)
{
    return max(dot(max(profileColor, float3(0.0f, 0.0f, 0.0f)), float3(0.3f, 0.59f, 0.11f)), 0.0f);
}

float BurtHenyeyGreensteinPhase(float anisotropy, float cosTheta)
{
    float g = clamp(anisotropy, -0.99f, 0.99f);
    float g2 = g * g;
    float denominator = pow(max(1.0f + g2 + 2.0f * g * cosTheta, 0.001f), 1.5f);
    return (1.0f - g2) / max(4.0f * BURT_PI * denominator, BURT_EPSILON);
}

#endif

#if BURT_MODEL_HAS_SUBSURFACE
float BurtGetSubsurfaceMaterialWeight(BurtPBRMaterialData materialData)
{
    return saturate(materialData.SubsurfaceActive);
}
#endif

#if BURT_MODEL_HAS_SUBSURFACE
float3 BurtEvaluateSubsurfaceTransmissionBRDFColor(
    BurtPBRMaterialData materialData,
    float3 transmissionThroughput,
    float transmissionPhase)
{
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    if (BurtIsSubsurface3SPreIntegratedMode(materialData.SubsurfaceScatteringMode))
    {
        return max(materialData.BaseColor * transmissionThroughput * transmissionPhase, float3(0.0f, 0.0f, 0.0f));
    }

    return max(transmissionThroughput * transmissionPhase, float3(0.0f, 0.0f, 0.0f));
#else
    return max(materialData.BaseColor * transmissionThroughput * transmissionPhase, float3(0.0f, 0.0f, 0.0f));
#endif
}

float BurtEvaluateSubsurfaceTransmissionWrapLobe(
    BurtPBRGeometryData geometryData,
    float3 lightDirectionWS,
    float scatteringDistribution)
{
    float3 n = geometryData.NormalWS;
    float3 v = geometryData.ViewDirectionWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float nDotL = dot(n, l);
    float opacity = saturate(1.0f - abs(scatteringDistribution));
    float inScatter = pow(saturate(dot(l, -v)), 12.0f) * lerp(3.0f, 0.1f, opacity);
    float wrappedDiffuse = pow(saturate(nDotL * (1.0f / 1.5f) + (0.5f / 1.5f)), 1.5f) * (2.5f / 1.5f);
    float normalContribution = lerp(1.0f, wrappedDiffuse, opacity);
    float backScatter = normalContribution * (0.5f * BURT_INV_PI);
    return max(lerp(backScatter, 1.0f, inScatter), 0.0f);
}

void BurtEvaluateSubsurfaceProfileTransmissionTerms(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightDirectionWS,
    float resolvedTransmissionThickness,
    float transmissionShadowAttenuation,
    out float3 transmissionBRDF,
    out float3 transmissionThroughput,
    out float transmissionLobe,
    out float transmissionPhase,
    out float transmissionThickness)
{
    float4 profileTransmission = BurtLoadSubsurfaceProfileTransmission(materialData.SubsurfaceProfileIndex);
    float nDotL = dot(geometryData.NormalWS, BurtSafeNormalize(lightDirectionWS));
    bool hasResolvedTransmissionThickness = BurtHasResolvedSubsurfaceTransmissionThickness(resolvedTransmissionThickness);
    transmissionThickness = hasResolvedTransmissionThickness
        ? BurtResolveSubsurfaceTransmissionLightingThickness(materialData.SubsurfaceThickness, resolvedTransmissionThickness, nDotL)
        : BurtResolveSubsurfaceProfileThickness(materialData.SubsurfaceThickness);
    transmissionThroughput = BurtSampleSubsurfaceTransmissionThroughputByThickness(
        materialData.SubsurfaceProfileIndex,
        transmissionThickness);
    float oneOverIOR = clamp(profileTransmission.w, 0.01f, 1.0f);
    float scatteringDistribution = BurtDecodeSubsurfaceProfileScatteringDistribution(profileTransmission.z);
    if (hasResolvedTransmissionThickness)
    {
        float3 refractedView = refract(geometryData.ViewDirectionWS, -geometryData.NormalWS, oneOverIOR);
        refractedView = dot(refractedView, refractedView) > BURT_EPSILON
            ? BurtSafeNormalize(refractedView)
            : BurtSafeNormalize(geometryData.ViewDirectionWS);
        transmissionPhase = BurtHenyeyGreensteinPhase(scatteringDistribution, dot(BurtSafeNormalize(lightDirectionWS), refractedView));
    }
    else
    {
        transmissionPhase = BurtEvaluateSubsurfaceTransmissionWrapLobe(geometryData, lightDirectionWS, scatteringDistribution);
    }

    transmissionBRDF = BurtEvaluateSubsurfaceTransmissionBRDFColor(materialData, transmissionThroughput, transmissionPhase);
    transmissionLobe = BurtEvaluateSubsurfaceProfileIntensity(transmissionBRDF);
}

void BurtEvaluateSubsurfaceProfileTransmissionTerms(
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightDirectionWS,
    out float3 transmissionBRDF,
    out float3 transmissionThroughput,
    out float transmissionLobe,
    out float transmissionPhase,
    out float transmissionThickness)
{
    BurtEvaluateSubsurfaceProfileTransmissionTerms(
        materialData,
        geometryData,
        lightDirectionWS,
        -1.0f,
        1.0f,
        transmissionBRDF,
        transmissionThroughput,
        transmissionLobe,
        transmissionPhase,
        transmissionThickness);
}

void BurtApplySubsurfaceDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation,
    float transmissionShadowAttenuation,
    float resolvedTransmissionThickness)
{
    float subsurfaceStrength = BurtGetSubsurfaceMaterialWeight(materialData);
    if (subsurfaceStrength <= 0.0001f)
    {
        return;
    }

    float3 n = geometryData.NormalWS;
    float3 l = BurtSafeNormalize(lightDirectionWS);
    float rawNoL = dot(n, l);
    float Curvature = saturate(materialData.Subsurface3SCurvature);
    if (BurtIsSubsurface3SPreIntegratedMode(materialData.SubsurfaceScatteringMode))
    {
        float3 PreintegratedLutScatter = BurtSampleSubsurfacePreIntegratedLut(rawNoL, Curvature, materialData.SubsurfaceProfileIndex);
        float PreintegratedLutWeight = saturate(_BurtSubsurfacePreIntegratedLutEnabled);
        float3 PreintegratedDiffuseBRDF = materialData.DiffuseColor * PreintegratedLutScatter * BURT_INV_PI;
        float3 PreintegratedDiffuse = PreintegratedDiffuseBRDF * lightColor * shadowAttenuation;
        float TargetDiffuseLobe = BurtEvaluateSubsurfaceProfileIntensity(PreintegratedLutScatter) * BURT_INV_PI;
        float3 ResolvedDiffuse = lerp(components.Diffuse, PreintegratedDiffuse, PreintegratedLutWeight);
        float3 ResolvedDiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, PreintegratedDiffuseBRDF, PreintegratedLutWeight);
        float ResolvedDiffuseLobe = lerp(components.BrdfTerms.DiffuseLobe, TargetDiffuseLobe, PreintegratedLutWeight);

        components.Diffuse = lerp(components.Diffuse, ResolvedDiffuse, subsurfaceStrength);
        components.BrdfTerms.DiffuseLobe = lerp(components.BrdfTerms.DiffuseLobe, ResolvedDiffuseLobe, subsurfaceStrength);
        components.BrdfTerms.DiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, ResolvedDiffuseBRDF, subsurfaceStrength);
        return;
    }

    float3 v = geometryData.ViewDirectionWS;
    float wrappedNoL = saturate((rawNoL + materialData.SubsurfaceDistortion) / (1.0f + materialData.SubsurfaceDistortion));
    float3 h = BurtSafeNormalize(l + v);
    float vDotH = saturate(dot(v, h));
    float wrappedDiffuseLobe = SlabLobe_Diffuse(materialData, geometryData.NDotV, wrappedNoL, vDotH);

    float3 profileTransmissionBRDF;
    float3 profileTransmissionThroughput;
    float profileTransmissionLobe;
    float profileTransmissionPhase;
    float profileTransmissionThickness;
    BurtEvaluateSubsurfaceProfileTransmissionTerms(
        materialData,
        geometryData,
        l,
        resolvedTransmissionThickness,
        transmissionShadowAttenuation,
        profileTransmissionBRDF,
        profileTransmissionThroughput,
        profileTransmissionLobe,
        profileTransmissionPhase,
        profileTransmissionThickness);
    float transmissionShadow = saturate(transmissionShadowAttenuation);
    float transmissionVisibility = BurtHasResolvedSubsurfaceTransmissionThickness(resolvedTransmissionThickness) ? transmissionShadow : 1.0f;
    float3 profileTransmission = profileTransmissionBRDF * lightColor * transmissionVisibility;
    components.Transmission = lerp(components.Transmission, profileTransmission, subsurfaceStrength);
    components.TransmissionBRDF = lerp(components.TransmissionBRDF, profileTransmissionBRDF, subsurfaceStrength);
    components.TransmissionThroughput = lerp(components.TransmissionThroughput, profileTransmissionThroughput, subsurfaceStrength);
    components.TransmissionLobe = lerp(components.TransmissionLobe, profileTransmissionLobe, subsurfaceStrength);
    components.TransmissionPhase = lerp(components.TransmissionPhase, profileTransmissionPhase, subsurfaceStrength);
    components.TransmissionShadow = lerp(components.TransmissionShadow, transmissionShadow, subsurfaceStrength);
    components.TransmissionThickness = lerp(components.TransmissionThickness, profileTransmissionThickness, subsurfaceStrength);
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    float3 diffuseTint = float3(1.0f, 1.0f, 1.0f);
#else
    float3 diffuseTint = max(BurtLoadSubsurfaceProfileTransmissionTint(materialData.SubsurfaceProfileIndex).rgb, float3(0.0f, 0.0f, 0.0f));
#endif
    float4 dualSpecular = BurtLoadSubsurfaceProfileDualSpecular(materialData.SubsurfaceProfileIndex);
    float lobeMix = saturate(dualSpecular.z);
    float lobe0Roughness = ClampPerceptualRoughness(materialData.PerceptualRoughness * max(dualSpecular.x * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));
    float lobe1Roughness = ClampPerceptualRoughness(materialData.PerceptualRoughness * max(dualSpecular.y * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));
    float averageRoughness = ClampPerceptualRoughness(lerp(lobe0Roughness, lobe1Roughness, lobeMix));
    float averageLinearRoughness = PerceptualRoughnessToLinearRoughness(averageRoughness);
    float3 dualEnergyCompensation;
    float dualEnergyPreservation;
    GetSpecularEnergyTerms(materialData.F0, materialData.F90, averageRoughness, components.BrdfTerms.NDotV, dualEnergyCompensation, dualEnergyPreservation);
    float3 LutScatter = BurtSampleSubsurfacePreIntegratedLut(rawNoL, Curvature, materialData.SubsurfaceProfileIndex);
    float LutWeight = saturate(_BurtSubsurfacePreIntegratedLutEnabled);
    float ScatterIntensity = BurtEvaluateSubsurfaceProfileIntensity(LutScatter);
    float3 wrappedDiffuseBRDF = materialData.DiffuseColor * diffuseTint * lerp(wrappedDiffuseLobe, ScatterIntensity * BURT_INV_PI, LutWeight) * dualEnergyPreservation;
    float3 wrappedDiffuse = wrappedDiffuseBRDF * lightColor * lerp(wrappedNoL * shadowAttenuation, shadowAttenuation, LutWeight);

    profileTransmissionBRDF *= dualEnergyPreservation;
    profileTransmission *= dualEnergyPreservation;
    components.Transmission *= dualEnergyPreservation;
    components.TransmissionBRDF *= dualEnergyPreservation;
    profileTransmissionLobe *= dualEnergyPreservation;
    components.TransmissionLobe *= dualEnergyPreservation;

    float lobe0A2 = LinearRoughnessToA2(PerceptualRoughnessToLinearRoughness(lobe0Roughness));
    float lobe1A2 = LinearRoughnessToA2(PerceptualRoughnessToLinearRoughness(lobe1Roughness));
    float lobe0D = D_GGX(lobe0A2, components.BrdfTerms.NDotH);
    float lobe1D = D_GGX(lobe1A2, components.BrdfTerms.NDotH);
    float dualD = lerp(lobe0D, lobe1D, lobeMix);
    float dualVisibility = Vis_SmithJointApprox(averageLinearRoughness, components.BrdfTerms.NDotV, components.BrdfTerms.NDotL);
    float3 dualSpecularBRDF = dualD * dualVisibility * components.BrdfTerms.Fresnel * dualEnergyCompensation;
    float lightVisibility = components.BrdfTerms.NDotL * shadowAttenuation;
    float3 dualSpecularContribution = dualSpecularBRDF * lightColor * lightVisibility;

    float3 targetDiffuse = wrappedDiffuse;
    float targetDiffuseLobe = wrappedDiffuseLobe;
    float3 targetDiffuseBRDF = wrappedDiffuseBRDF;
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    if (!BurtIsSubsurface3SPreIntegratedMode(materialData.SubsurfaceScatteringMode))
    {
        // 5S/4S match XRender's post-process SSS path: direct diffuse stays as
        // ordinary diffuse, while profile diffusion/tint is applied by the
        // screen-space subsurface pass. Profile transmission is a direct-light
        // lobe and stays in the deferred subsurface input.
        targetDiffuse = components.Diffuse;
        targetDiffuseLobe = components.BrdfTerms.DiffuseLobe;
        targetDiffuseBRDF = components.BrdfTerms.DiffuseBRDF;
    }
#endif
    components.Diffuse = lerp(components.Diffuse, targetDiffuse, subsurfaceStrength);
    components.BrdfTerms.DiffuseLobe = lerp(components.BrdfTerms.DiffuseLobe, targetDiffuseLobe, subsurfaceStrength);
    components.BrdfTerms.DiffuseBRDF = lerp(components.BrdfTerms.DiffuseBRDF, targetDiffuseBRDF, subsurfaceStrength);
    components.Specular = lerp(components.Specular, dualSpecularContribution, subsurfaceStrength);
    components.EnergyPreservation = lerp(components.EnergyPreservation, dualEnergyPreservation, subsurfaceStrength);
    components.BrdfTerms.SpecularBRDF = lerp(components.BrdfTerms.SpecularBRDF, dualSpecularBRDF, subsurfaceStrength);
    components.BrdfTerms.PerceptualRoughness = lerp(components.BrdfTerms.PerceptualRoughness, averageRoughness, subsurfaceStrength);
    components.BrdfTerms.LinearRoughness = lerp(components.BrdfTerms.LinearRoughness, averageLinearRoughness, subsurfaceStrength);
    components.BrdfTerms.A2 = lerp(components.BrdfTerms.A2, LinearRoughnessToA2(averageLinearRoughness), subsurfaceStrength);
    components.BrdfTerms.D = lerp(components.BrdfTerms.D, dualD, subsurfaceStrength);
    components.BrdfTerms.Visibility = lerp(components.BrdfTerms.Visibility, dualVisibility, subsurfaceStrength);
}

void BurtApplySubsurfaceDirectPBR(
    inout BurtDirectPBRComponents components,
    BurtPBRMaterialData materialData,
    BurtPBRGeometryData geometryData,
    float3 lightColor,
    float3 lightDirectionWS,
    float shadowAttenuation)
{
    BurtApplySubsurfaceDirectPBR(
        components,
        materialData,
        geometryData,
        lightColor,
        lightDirectionWS,
        shadowAttenuation,
        shadowAttenuation,
        -1.0f);
}
#endif

#endif // BURT_BRDF_SUBSURFACE_INCLUDED
