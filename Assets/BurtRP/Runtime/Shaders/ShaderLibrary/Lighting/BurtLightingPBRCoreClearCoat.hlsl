// ClearCoat-specific PBR core helpers.
#ifndef BURT_LIGHTING_PBR_CORE_CLEAR_COAT_INCLUDED
#define BURT_LIGHTING_PBR_CORE_CLEAR_COAT_INCLUDED

BurtPBRMaterialData BurtCreateClearCoatMaterialData(BurtPBRMaterialData BaseMaterialData)
{
    BurtPBRMaterialData ClearCoatMaterialData = BaseMaterialData;
    ClearCoatMaterialData.BaseColor = float3(1.0f, 1.0f, 1.0f);
    ClearCoatMaterialData.Metallic = 0.0f;
    ClearCoatMaterialData.Anisotropy = 0.0f;
    ClearCoatMaterialData.Reflectance = BURT_INPUT_DEFAULT_REFLECTANCE;
    ClearCoatMaterialData.DiffuseColor = float3(0.0f, 0.0f, 0.0f);
    ClearCoatMaterialData.F0 = float3(0.04f, 0.04f, 0.04f);
    ClearCoatMaterialData.F90 = float3(1.0f, 1.0f, 1.0f);
    ClearCoatMaterialData.PerceptualRoughness = ClampPerceptualRoughness(BaseMaterialData.ClearCoatRoughness);
    ClearCoatMaterialData.LinearRoughness = PerceptualRoughnessToLinearRoughness(ClearCoatMaterialData.PerceptualRoughness);
    ClearCoatMaterialData.A2 = LinearRoughnessToA2(ClearCoatMaterialData.LinearRoughness);
    ClearCoatMaterialData.ClearCoatMask = 0.0f;
    return ClearCoatMaterialData;
}

BurtPBRShadingCoreData BurtApplyClearCoatTopLayerCoreData(BurtPBRShadingCoreData CoreData)
{
    CoreData = BurtResetClearCoatTopLayerCoreData(CoreData);

    if (saturate(CoreData.MaterialData.ClearCoatMask) <= 0.0001f)
    {
        return CoreData;
    }

    BurtPBRMaterialData ClearCoatMaterialData = BurtCreateClearCoatMaterialData(CoreData.MaterialData);
    CoreData.ClearCoatSpecularAATerms = BurtEvaluateSpecularAATerms(ClearCoatMaterialData, CoreData.ClearCoatGeometryData);
    CoreData.ClearCoatDirectSpecularPerceptualRoughness = CoreData.ClearCoatSpecularAATerms.FilteredPerceptualRoughness;

    BurtPBREnergyTerms ClearCoatEnergyTerms = BurtPreparePBREnergyTerms(ClearCoatMaterialData, CoreData.ClearCoatGeometryData, CoreData.ClearCoatDirectSpecularPerceptualRoughness);
    CoreData.ClearCoatDirectSpecularEnergyCompensation = ClearCoatEnergyTerms.DirectSpecularEnergyCompensation;
    return CoreData;
}

BurtDirectPBRComponents BurtApplyClearCoatPBRDirectFromCore(BurtDirectPBRComponents Components, BurtPBRShadingCoreData CoreData, BurtLight Light)
{
    float ClearCoatMask = saturate(CoreData.MaterialData.ClearCoatMask);
    if (ClearCoatMask <= 0.0001f)
    {
        return Components;
    }

    BurtPBRGeometryData ClearCoatGeometryData = CoreData.ClearCoatGeometryData;
    BurtPBRGeometryData BaseGeometryData = CoreData.GeometryData;
    float3 N = ClearCoatGeometryData.NormalWS;
    float3 BaseN = BaseGeometryData.NormalWS;
    float3 V = ClearCoatGeometryData.ViewDirectionWS;
    float3 L = BurtSafeNormalize(Light.DirectionWS);
    float3 H = BurtSafeNormalize(L + V);
    float ClearCoatNdotL = saturate(dot(N, L));
    float ClearCoatNdotH = saturate(dot(N, H));
    float ClearCoatVdotH = saturate(dot(V, H));
    float ClearCoatNoV = ClearCoatGeometryData.NDotV;
    float ClearCoatRoughness = ClampPerceptualRoughness(CoreData.ClearCoatDirectSpecularPerceptualRoughness);
    float ClearCoatLinearRoughness = PerceptualRoughnessToLinearRoughness(ClearCoatRoughness);
    float ClearCoatA2 = LinearRoughnessToA2(ClearCoatLinearRoughness);
    float ClearCoatD = D_GGX(ClearCoatA2, ClearCoatNdotH);
    float ClearCoatVisibility = Vis_SmithJointApprox(ClearCoatLinearRoughness, ClearCoatNoV, ClearCoatNdotL);
    float3 ClearCoatFresnel = F_Schlick_UE(float3(BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0, BURT_CLEAR_COAT_F0), float3(1.0f, 1.0f, 1.0f), ClearCoatVdotH);
    float3 ClearCoatSpecularBRDF = ClearCoatD * ClearCoatVisibility * ClearCoatFresnel * CoreData.ClearCoatDirectSpecularEnergyCompensation;
    float3 ClearCoatSpecular = ClearCoatSpecularBRDF * Light.Color * ClearCoatNdotL * Light.ShadowAttenuation;

    float BaseNdotL = saturate(dot(BaseN, L));
    float BaseNdotV = BaseGeometryData.NDotV;
    float BaseNdotH = saturate(dot(BaseN, H));
    float BaseXdotH = dot(BaseGeometryData.TangentWS, H);
    float BaseYdotH = dot(BaseGeometryData.BitangentWS, H);
    float BaseXdotV = dot(BaseGeometryData.TangentWS, V);
    float BaseYdotV = dot(BaseGeometryData.BitangentWS, V);
    float BaseXdotL = dot(BaseGeometryData.TangentWS, L);
    float BaseYdotL = dot(BaseGeometryData.BitangentWS, L);
    float RefractionBlend = BurtRefractBlendClearCoatApprox(ClearCoatVdotH);
    float RefractionProjection = RefractionBlend * BaseNdotH;
    float RefractedNdotV = clamp(BURT_CLEAR_COAT_ETA * BaseNdotV - RefractionProjection, 0.001f, 1.0f);
    float RefractedNdotL = clamp(BURT_CLEAR_COAT_ETA * BaseNdotL - RefractionProjection, 0.001f, 1.0f);
    float RefractedVdotH = saturate(BURT_CLEAR_COAT_ETA * ClearCoatVdotH - RefractionBlend);
    float3 LayerTransmission = BurtClearCoatFresnelTransmission(ClearCoatFresnel) * BurtSimpleClearCoatTransmittance(RefractedNdotL, RefractedNdotV, CoreData.MaterialData.Metallic, CoreData.MaterialData.BaseColor);
    float BottomLayerLightNoL = ClearCoatNdotL;

    float BottomDiffuseLobe = SlabLobe_Diffuse(CoreData.MaterialData, RefractedNdotV, RefractedNdotL, RefractedVdotH);
    float3 RefractedDiffuseBRDF = CoreData.MaterialData.DiffuseColor * BottomDiffuseLobe * CoreData.EnergyTerms.EnergyPreservation * LayerTransmission;
    float3 RefractedDiffuse = RefractedDiffuseBRDF * Light.Color * BottomLayerLightNoL * Light.ShadowAttenuation;

    float BottomLinearRoughness = PerceptualRoughnessToLinearRoughness(CoreData.DirectSpecularPerceptualRoughness);
    float BottomAx;
    float BottomAy;
    GetAnisotropicRoughness(BottomLinearRoughness, CoreData.MaterialData.Anisotropy, BottomAx, BottomAy);
    float BottomD = D_GGX_Anisotropic(BottomAx, BottomAy, BaseNdotH, BaseXdotH, BaseYdotH);
    float BottomVisibility = Vis_SmithJointAnisotropic(BottomAx, BottomAy, RefractedNdotV, BottomLayerLightNoL, BaseXdotV, BaseXdotL, BaseYdotV, BaseYdotL);
    float3 BottomFresnel = F_Schlick_UE(CoreData.MaterialData.F0, CoreData.MaterialData.F90, RefractedVdotH);
    float3 RefractedSpecularBRDF = BottomD * BottomVisibility * BottomFresnel * CoreData.EnergyTerms.DirectSpecularEnergyCompensation * LayerTransmission;
    float3 RefractedSpecular = RefractedSpecularBRDF * Light.Color * BottomLayerLightNoL * Light.ShadowAttenuation;

    Components.Diffuse = lerp(Components.Diffuse, RefractedDiffuse, ClearCoatMask);
    Components.Specular = lerp(Components.Specular, RefractedSpecular, ClearCoatMask) + ClearCoatSpecular * ClearCoatMask;
    Components.BrdfTerms.DiffuseLobe = lerp(Components.BrdfTerms.DiffuseLobe, BottomDiffuseLobe, ClearCoatMask);
    Components.BrdfTerms.DiffuseBRDF = lerp(Components.BrdfTerms.DiffuseBRDF, RefractedDiffuseBRDF, ClearCoatMask);
    Components.BrdfTerms.SpecularBRDF = lerp(Components.BrdfTerms.SpecularBRDF, RefractedSpecularBRDF, ClearCoatMask) + ClearCoatSpecularBRDF * ClearCoatMask;
    Components.BrdfTerms.NDotL = lerp(Components.BrdfTerms.NDotL, ClearCoatNdotL, ClearCoatMask);
    Components.BrdfTerms.NDotV = lerp(Components.BrdfTerms.NDotV, ClearCoatNoV, ClearCoatMask);
    Components.BrdfTerms.NDotH = lerp(Components.BrdfTerms.NDotH, ClearCoatNdotH, ClearCoatMask);
    Components.BrdfTerms.VDotH = lerp(Components.BrdfTerms.VDotH, ClearCoatVdotH, ClearCoatMask);
    Components.BrdfTerms.PerceptualRoughness = lerp(Components.BrdfTerms.PerceptualRoughness, ClearCoatRoughness, ClearCoatMask);
    Components.BrdfTerms.LinearRoughness = lerp(Components.BrdfTerms.LinearRoughness, ClearCoatLinearRoughness, ClearCoatMask);
    Components.BrdfTerms.A2 = lerp(Components.BrdfTerms.A2, ClearCoatA2, ClearCoatMask);
    Components.BrdfTerms.D = lerp(Components.BrdfTerms.D, ClearCoatD, ClearCoatMask);
    Components.BrdfTerms.Visibility = lerp(Components.BrdfTerms.Visibility, ClearCoatVisibility, ClearCoatMask);
    Components.BrdfTerms.Fresnel = lerp(Components.BrdfTerms.Fresnel, ClearCoatFresnel, ClearCoatMask);
    return Components;
}

#if BURT_PBR_SHADING_COMPONENTS_INCLUDE_BRDF_DEBUG
void BurtApplyClearCoatPBRDebugComponents(
    BurtPBRShadingCoreData CoreData,
    inout float DebugSpecularAARoughness,
    inout float DebugSpecularAANormalVariance,
    inout float DebugSpecularAARoughnessDelta,
    inout float3 DebugDirectSpecularEnergyCompensation,
    inout float3 DebugIndirectSpecularEnergyCompensation,
    inout float DebugIndirectNoV,
    inout float DebugIndirectRoughness,
    inout float2 DebugIndirectDFG,
    inout float3 DebugIndirectEnvBRDF)
{
    float ClearCoatMask = saturate(CoreData.MaterialData.ClearCoatMask);
    if (ClearCoatMask <= 0.0001f)
    {
        return;
    }

    DebugSpecularAARoughness = lerp(DebugSpecularAARoughness, CoreData.ClearCoatDirectSpecularPerceptualRoughness, ClearCoatMask);
    DebugSpecularAANormalVariance = lerp(DebugSpecularAANormalVariance, CoreData.ClearCoatSpecularAATerms.NormalVariance, ClearCoatMask);
    DebugSpecularAARoughnessDelta = lerp(DebugSpecularAARoughnessDelta, CoreData.ClearCoatSpecularAATerms.RoughnessDelta, ClearCoatMask);

    BurtPBRMaterialData ClearCoatMaterialData = BurtCreateClearCoatMaterialData(CoreData.MaterialData);
    BurtPBREnergyTerms ClearCoatEnergyTerms = BurtPreparePBREnergyTerms(ClearCoatMaterialData, CoreData.ClearCoatGeometryData, CoreData.ClearCoatDirectSpecularPerceptualRoughness);
    float2 ClearCoatDFG = GetSpecularDFGTerms(ClearCoatMaterialData.PerceptualRoughness, CoreData.ClearCoatGeometryData.NDotV);
    float3 ClearCoatEnvBRDF = EvalSpecularDFG(ClearCoatMaterialData.F0, ClearCoatMaterialData.F90, ClearCoatDFG);
    float3 BottomLayerEnvBRDF = EvalSpecularDFG(CoreData.MaterialData.F0, CoreData.MaterialData.F90, DebugIndirectDFG);
    float3 ClearCoatLayerTransmission = BurtEvaluateClearCoatLayerTransmission(CoreData.MaterialData, ClearCoatMaterialData, CoreData.ClearCoatGeometryData);

    DebugDirectSpecularEnergyCompensation = lerp(DebugDirectSpecularEnergyCompensation, ClearCoatEnergyTerms.DirectSpecularEnergyCompensation, ClearCoatMask);
    DebugIndirectSpecularEnergyCompensation = lerp(DebugIndirectSpecularEnergyCompensation, ClearCoatEnergyTerms.IndirectSpecularEnergyCompensation, ClearCoatMask);
    DebugIndirectNoV = lerp(DebugIndirectNoV, CoreData.ClearCoatGeometryData.NDotV, ClearCoatMask);
    DebugIndirectRoughness = lerp(DebugIndirectRoughness, ClearCoatMaterialData.PerceptualRoughness, ClearCoatMask);
    DebugIndirectDFG = lerp(DebugIndirectDFG, ClearCoatDFG, ClearCoatMask);
    DebugIndirectEnvBRDF = lerp(DebugIndirectEnvBRDF, BottomLayerEnvBRDF * ClearCoatLayerTransmission + ClearCoatEnvBRDF, ClearCoatMask);
}
#endif

#endif // BURT_LIGHTING_PBR_CORE_CLEAR_COAT_INCLUDED
