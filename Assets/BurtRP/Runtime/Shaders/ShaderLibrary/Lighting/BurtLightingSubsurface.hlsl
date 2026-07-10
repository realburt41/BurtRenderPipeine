// Subsurface-specific indirect lighting helpers.
#ifndef BURT_LIGHTING_SUBSURFACE_INCLUDED
#define BURT_LIGHTING_SUBSURFACE_INCLUDED

float3 BurtEvaluateSubsurface3SSH9(float curvature, float profileIndex, float3 normalWS)
{
    float3 SafeNormalWS = BurtSafeNormalize(normalWS);
    float3 zh0 = BurtSampleSubsurfaceSHLut(curvature, 0.0f, profileIndex);
    float3 zh1 = BurtSampleSubsurfaceSHLut(curvature, 1.0f, profileIndex);
    float3 zh2 = BurtSampleSubsurfaceSHLut(curvature, 2.0f, profileIndex);

    float4 shAr;
    float4 shAg;
    float4 shAb;
    float4 SHBr;
    float4 SHBg;
    float4 SHBb;
    float4 SHC;
    float3 SHNormalDirection = SafeNormalWS;

    if (_BurtSkyDiffuseSHEnabled > 0.5f)
    {
        SHNormalDirection = BurtRotateSkyReflectionDirection(SafeNormalWS);
        shAr = BurtSampleSkyDiffuseSHPacked(0.0f);
        shAg = BurtSampleSkyDiffuseSHPacked(1.0f);
        shAb = BurtSampleSkyDiffuseSHPacked(2.0f);
        SHBr = BurtSampleSkyDiffuseSHPacked(3.0f);
        SHBg = BurtSampleSkyDiffuseSHPacked(4.0f);
        SHBb = BurtSampleSkyDiffuseSHPacked(5.0f);
        SHC = BurtSampleSkyDiffuseSHPacked(6.0f);
    }
    else if (_BurtAmbientSHEnabled > 0.5f)
    {
        shAr = _BurtAmbientSHAr;
        shAg = _BurtAmbientSHAg;
        shAb = _BurtAmbientSHAb;
        SHBr = _BurtAmbientSHBr;
        SHBg = _BurtAmbientSHBg;
        SHBb = _BurtAmbientSHBb;
        SHC = _BurtAmbientSHC;
    }
    else
    {
        shAr = unity_SHAr;
        shAg = unity_SHAg;
        shAb = unity_SHAb;
        SHBr = unity_SHBr;
        SHBg = unity_SHBg;
        SHBb = unity_SHBb;
        SHC = unity_SHC;
    }

    shAr.xyz *= zh1.x;
    shAr.w *= zh0.x;
    shAg.xyz *= zh1.y;
    shAg.w *= zh0.y;
    shAb.xyz *= zh1.z;
    shAb.w *= zh0.z;

    float4 SHNormal = float4(SHNormalDirection, 1.0f);
    float3 LinearL0L1;
    LinearL0L1.r = dot(shAr, SHNormal);
    LinearL0L1.g = dot(shAg, SHNormal);
    LinearL0L1.b = dot(shAb, SHNormal);

    SHBr *= zh2.x;
    SHBg *= zh2.y;
    SHBb *= zh2.z;
    SHC.xyz *= zh2.xyz;

    float4 VB = SHNormal.xyzz * SHNormal.yzzx;
    float3 LinearL2;
    LinearL2.r = dot(SHBr, VB);
    LinearL2.g = dot(SHBg, VB);
    LinearL2.b = dot(SHBb, VB);

    float VC = SHNormalDirection.x * SHNormalDirection.x - SHNormalDirection.y * SHNormalDirection.y;
    float3 SHIrradiance = LinearL0L1 + LinearL2 + SHC.rgb * VC;
    if (_BurtSkyDiffuseSHEnabled > 0.5f)
    {
        SHIrradiance *= max(_BurtSkyDiffuseCubemapTint.rgb, float3(0.0f, 0.0f, 0.0f)) * max(_BurtSkyDiffuseCubemapIntensity, 0.0f);
    }

    SHIrradiance = BurtApplySkyLowerHemisphere(SHIrradiance, SafeNormalWS, _BurtSkyLowerHemisphereDiffuseColor);

#ifdef UNITY_COLORSPACE_GAMMA
    SHIrradiance = pow(max(SHIrradiance, float3(0.0f, 0.0f, 0.0f)), 1.0f / 2.2f);
#endif

    return max(SHIrradiance, float3(0.0f, 0.0f, 0.0f));
}

float3 BurtEvaluateSubsurfaceIndirectSpecularDualLobe(
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    float3 FallbackIndirectSpecularEnergyCompensation)
{
    float SubsurfaceStrength = BurtGetSubsurfaceMaterialWeight(MaterialData);
    if (SubsurfaceStrength <= 0.0001f)
    {
        return BurtEvaluateIndirectSpecularPBR(MaterialData, GeometryData, FallbackIndirectSpecularEnergyCompensation);
    }

    float4 DualSpecular = BurtLoadSubsurfaceProfileDualSpecular(MaterialData.SubsurfaceProfileIndex);
    float LobeMix = saturate(DualSpecular.z);
    float Roughness0 = ClampPerceptualRoughness(MaterialData.PerceptualRoughness * max(DualSpecular.x * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));
    float Roughness1 = ClampPerceptualRoughness(MaterialData.PerceptualRoughness * max(DualSpecular.y * BURT_SUBSURFACE_MAX_DUAL_SPECULAR_ROUGHNESS, 0.01f));

    float3 Reflection0 = BurtGetIndirectSpecularReflectionDirectionWS(GeometryData, MaterialData.Anisotropy, Roughness0);
    float3 Reflection1 = BurtGetIndirectSpecularReflectionDirectionWS(GeometryData, MaterialData.Anisotropy, Roughness1);
    float3 Radiance0 = SampleIndirectSpecularRadiance(Reflection0, Roughness0);
    float3 Radiance1 = SampleIndirectSpecularRadiance(Reflection1, Roughness1);

    float2 Dfg0 = GetSpecularDFGTerms(Roughness0, GeometryData.NDotV);
    float2 Dfg1 = GetSpecularDFGTerms(Roughness1, GeometryData.NDotV);
    float3 EnvBRDF0 = EvalSpecularDFG(MaterialData.F0, MaterialData.F90, Dfg0);
    float3 EnvBRDF1 = EvalSpecularDFG(MaterialData.F0, MaterialData.F90, Dfg1);

    float3 Energy0;
    float Preservation0;
    float3 Energy1;
    float Preservation1;
    GetSpecularEnergyTerms(MaterialData.F0, MaterialData.F90, Roughness0, GeometryData.NDotV, Energy0, Preservation0);
    GetSpecularEnergyTerms(MaterialData.F0, MaterialData.F90, Roughness1, GeometryData.NDotV, Energy1, Preservation1);

    float SpecularOcclusion0 = GetIndirectSpecularOcclusion(GeometryData.NDotV, MaterialData.Occlusion, Roughness0);
    float SpecularOcclusion1 = GetIndirectSpecularOcclusion(GeometryData.NDotV, MaterialData.Occlusion, Roughness1);
    float3 Lobe0 = Radiance0 * EnvBRDF0 * Energy0 * SpecularOcclusion0;
    float3 Lobe1 = Radiance1 * EnvBRDF1 * Energy1 * SpecularOcclusion1;
    float3 DualLobeSpecular = lerp(Lobe0, Lobe1, LobeMix);
    float3 SingleLobeSpecular = BurtEvaluateIndirectSpecularPBR(MaterialData, GeometryData, FallbackIndirectSpecularEnergyCompensation);
    return lerp(SingleLobeSpecular, DualLobeSpecular, SubsurfaceStrength);
}

float3 BurtEvaluateSubsurfaceIndirectProfile(
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    float ResolvedTransmissionThickness)
{
    float SubsurfaceStrength = BurtGetSubsurfaceMaterialWeight(MaterialData);
    if (SubsurfaceStrength <= 0.0001f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    if (BurtIsSubsurface3SPreIntegratedMode(MaterialData.SubsurfaceScatteringMode))
    {
        float Curvature = saturate(MaterialData.Subsurface3SCurvature);
        float3 ReferenceSHIrradiance = BurtEvaluateSubsurface3SSH9(Curvature, MaterialData.SubsurfaceProfileIndex, GeometryData.NormalWS);
        float3 LutScatter = BurtSampleSubsurfacePreIntegratedLut(1.0f, Curvature, MaterialData.SubsurfaceProfileIndex);
        float3 FallbackIrradiance = lerp(float3(1.0f, 1.0f, 1.0f), LutScatter, saturate(_BurtSubsurfacePreIntegratedLutEnabled)) * BurtSampleIndirectDiffuseIrradiance(GeometryData.NormalWS);
        float3 SubsurfaceIrradiance = lerp(FallbackIrradiance, ReferenceSHIrradiance, saturate(_BurtSubsurfaceSHLutEnabled) * BURT_SUBSURFACE_3S_SH_IRRADIANCE_WEIGHT);
        return MaterialData.DiffuseColor * SubsurfaceIrradiance * BurtGTAOMultiBounce(MaterialData.Occlusion, MaterialData.BaseColor) * SubsurfaceStrength;
    }

    float MaterialThickness = saturate(MaterialData.SubsurfaceThickness);
    float TransmissionThickness = BurtResolveSubsurfaceProfileThickness(MaterialData.SubsurfaceThickness, ResolvedTransmissionThickness);
    float NormalizedThickness = saturate(TransmissionThickness / BURT_SUBSURFACE_MAX_TRANSMISSION_PROFILE_DISTANCE);
    float AmbientWrap = saturate(MaterialData.SubsurfaceAmbient);
    float3 FrontIrradiance = BurtSampleIndirectDiffuseIrradiance(GeometryData.NormalWS);
    float3 BackIrradiance = BurtSampleIndirectDiffuseIrradiance(-GeometryData.NormalWS);
    float3 TangentIrradiance = BurtSampleIndirectDiffuseIrradiance(GeometryData.TangentWS);
    float3 BitangentIrradiance = BurtSampleIndirectDiffuseIrradiance(GeometryData.BitangentWS);
    float3 SideIrradiance = (TangentIrradiance + BitangentIrradiance) * 0.5f;
    float3 WrappedIrradiance = lerp(FrontIrradiance, BackIrradiance, AmbientWrap);
    WrappedIrradiance = lerp(WrappedIrradiance, SideIrradiance, saturate(MaterialThickness * 0.35f));

    float4 ProfileTransmission = BurtLoadSubsurfaceProfileTransmission(MaterialData.SubsurfaceProfileIndex);
    float4 ProfileTint = BurtLoadSubsurfaceProfileTransmissionTint(MaterialData.SubsurfaceProfileIndex);
    float3 ProfileTransmittance = BurtSampleSubsurfaceTransmissionProfileByThickness(MaterialData.SubsurfaceProfileIndex, TransmissionThickness);
    float ExtinctionScale = BurtDecodeSubsurfaceProfileExtinctionScale(ProfileTransmission.x);
    float NormalWrap = saturate(ProfileTransmission.y);
    float ScatteringDistribution = saturate(abs(BurtDecodeSubsurfaceProfileScatteringDistribution(ProfileTransmission.z)));
    float MeanFreePathVisibility = saturate(rsqrt(ExtinctionScale));
    float ThicknessVisibility = lerp(0.18f, 1.0f, max(MaterialThickness, NormalizedThickness));
    float WrapVisibility = lerp(0.55f, 1.15f, max(AmbientWrap, NormalWrap));
    float ScatterVisibility = lerp(0.75f, 1.25f, ScatteringDistribution);
    float TransmissionIntensity = BurtEvaluateSubsurfaceProfileIntensity(ProfileTransmittance);
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    float3 SubsurfaceDiffuse = WrappedIrradiance * TransmissionIntensity;
#else
    float3 Tint = max(ProfileTint.rgb, float3(0.0f, 0.0f, 0.0f));
    float3 SubsurfaceDiffuse = MaterialData.BaseColor * Tint * TransmissionIntensity * WrappedIrradiance;
#endif
    return SubsurfaceDiffuse * MaterialData.Occlusion * SubsurfaceStrength * ThicknessVisibility * WrapVisibility * ScatterVisibility * lerp(0.55f, 1.0f, MeanFreePathVisibility);
}

float3 BurtEvaluateSubsurfaceIndirectProfile(
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData)
{
    return BurtEvaluateSubsurfaceIndirectProfile(MaterialData, GeometryData, -1.0f);
}

float3 BurtEvaluateSubsurfaceIndirectTransmission(
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    float ResolvedTransmissionThickness)
{
    float SubsurfaceStrength = BurtGetSubsurfaceMaterialWeight(MaterialData);
    if (SubsurfaceStrength <= 0.0001f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    if (!BurtHasResolvedSubsurfaceTransmissionThickness(ResolvedTransmissionThickness))
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    if (BurtIsSubsurface3SPreIntegratedMode(MaterialData.SubsurfaceScatteringMode))
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float TransmissionThickness = BurtResolveSubsurfaceProfileThickness(MaterialData.SubsurfaceThickness, ResolvedTransmissionThickness);
    float3 BackIrradiance = BurtSampleIndirectDiffuseIrradiance(-GeometryData.NormalWS);
    float3 TangentIrradiance = BurtSampleIndirectDiffuseIrradiance(GeometryData.TangentWS);
    float3 BitangentIrradiance = BurtSampleIndirectDiffuseIrradiance(GeometryData.BitangentWS);
    float3 SideIrradiance = (TangentIrradiance + BitangentIrradiance) * 0.5f;
    float SideBlend = saturate(MaterialData.SubsurfaceAmbient * 0.12f);
    float3 XgiTransmissionIrradiance = lerp(BackIrradiance, SideIrradiance, SideBlend);
    float3 ProfileThroughput = BurtSampleSubsurfaceTransmissionThroughputByThickness(MaterialData.SubsurfaceProfileIndex, TransmissionThickness);
    float3 AO = BurtGTAOMultiBounce(MaterialData.Occlusion, MaterialData.BaseColor);

#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    float3 SubsurfaceColor = ProfileThroughput;
#else
    float3 SubsurfaceColor = MaterialData.BaseColor * ProfileThroughput;
#endif
    return max(XgiTransmissionIrradiance * SubsurfaceColor * AO * SubsurfaceStrength, float3(0.0f, 0.0f, 0.0f));
}

BurtIndirectPBRComponents BurtApplySubsurfaceIndirectTransmissionFromLight(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    BurtLight MainLight)
{
#if BURT_ENABLE_SUBSURFACE_SHADING && (!defined(BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS) || defined(BURT_DEFERRED_SHADING_MODEL_SUBSURFACE))
    if (!BurtIsSubsurface3SPreIntegratedMode(MaterialData.SubsurfaceScatteringMode))
    {
        Components.SubsurfaceIndirectTransmission = BurtEvaluateSubsurfaceIndirectTransmission(MaterialData, GeometryData, MainLight.TransmissionThickness);
        Components.SubsurfaceIndirect = Components.Diffuse;
    }
#endif
    return Components;
}

BurtIndirectPBRComponents BurtApplySubsurfaceIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData)
{
    bool IsSubsurface3S = BurtIsSubsurface3SPreIntegratedMode(MaterialData.SubsurfaceScatteringMode);

#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    if (IsSubsurface3S)
    {
        Components.SubsurfaceIndirect = BurtEvaluateSubsurfaceIndirectProfile(MaterialData, GeometryData, -1.0f);
        Components.Diffuse = lerp(Components.Diffuse, Components.SubsurfaceIndirect, BurtGetSubsurfaceMaterialWeight(MaterialData));
    }
    else
    {
        Components.SubsurfaceIndirectTransmission = BurtEvaluateSubsurfaceIndirectTransmission(MaterialData, GeometryData, -1.0f);
        Components.SubsurfaceIndirect = Components.Diffuse;
    }
#else
    Components.SubsurfaceIndirectTransmission = BurtEvaluateSubsurfaceIndirectTransmission(MaterialData, GeometryData, -1.0f);
    Components.SubsurfaceIndirect = BurtEvaluateSubsurfaceIndirectProfile(MaterialData, GeometryData, -1.0f);
    Components.Diffuse = lerp(Components.Diffuse, Components.SubsurfaceIndirect, BurtGetSubsurfaceMaterialWeight(MaterialData));
#endif

    if (!IsSubsurface3S)
    {
        Components.Specular = BurtEvaluateSubsurfaceIndirectSpecularDualLobe(MaterialData, GeometryData, Components.SpecularEnergyCompensation);
    }

    return Components;
}

#endif
