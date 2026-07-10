// ClearCoat-specific indirect lighting helpers.
#ifndef BURT_LIGHTING_CLEAR_COAT_INCLUDED
#define BURT_LIGHTING_CLEAR_COAT_INCLUDED

float3 BurtEvaluateClearCoatLayerTransmission(
    BurtPBRMaterialData BaseMaterialData,
    BurtPBRMaterialData ClearCoatMaterialData,
    BurtPBRGeometryData ClearCoatGeometryData)
{
    float2 ClearCoatDFG = GetSpecularDFGTerms(ClearCoatMaterialData.PerceptualRoughness, ClearCoatGeometryData.NDotV);
    float3 ClearCoatEnvFresnel = EvalSpecularDFG(ClearCoatMaterialData.F0, ClearCoatMaterialData.F90, ClearCoatDFG);
    return BurtClearCoatFresnelTransmission(ClearCoatEnvFresnel) * BurtSimpleClearCoatTransmittanceFromView(ClearCoatGeometryData.NDotV, BaseMaterialData.Metallic, BaseMaterialData.BaseColor);
}

float3 BurtEvaluateClearCoatLayerEnergyPreservation(
    BurtPBRMaterialData BaseMaterialData,
    BurtPBRMaterialData ClearCoatMaterialData,
    BurtPBRGeometryData ClearCoatGeometryData)
{
    float3 ClearCoatEnergyCompensation;
    float ClearCoatEnergyPreservation;
    GetSpecularEnergyTerms(ClearCoatMaterialData.F0, ClearCoatMaterialData.F90, ClearCoatMaterialData.PerceptualRoughness, ClearCoatGeometryData.NDotV, ClearCoatEnergyCompensation, ClearCoatEnergyPreservation);
    return ClearCoatEnergyPreservation * BurtSimpleClearCoatTransmittanceFromView(ClearCoatGeometryData.NDotV, BaseMaterialData.Metallic, BaseMaterialData.BaseColor);
}

float3 BurtClearCoatLayerCombine(
    BurtPBRMaterialData BaseMaterialData,
    BurtPBRGeometryData BaseGeometryData,
    float3 BaseIndirectSpecularEnergyCompensation,
    BurtPBRMaterialData ClearCoatMaterialData,
    BurtPBRGeometryData ClearCoatGeometryData,
    float ClearCoatMask)
{
    float BaseRoughness = BaseMaterialData.PerceptualRoughness;
    float3 BottomLayerReflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(BaseGeometryData, BaseMaterialData.Anisotropy, BaseRoughness);
    float3 BottomLayerRadiance = SampleIndirectSpecularRadiance(BottomLayerReflectionDirectionWS, BaseRoughness);
    float2 BottomLayerDFG = GetSpecularDFGTerms(BaseRoughness, BaseGeometryData.NDotV);
    float3 BottomLayerEnvBRDF = EvalSpecularDFG(BaseMaterialData.F0, BaseMaterialData.F90, BottomLayerDFG);
    float BottomLayerSpecularOcclusion = GetIndirectSpecularOcclusion(BaseGeometryData.NDotV, BaseMaterialData.Occlusion, BaseRoughness);
    float3 BottomLayerReflections = BottomLayerRadiance * BottomLayerEnvBRDF * BaseIndirectSpecularEnergyCompensation * BottomLayerSpecularOcclusion;

    float ClearCoatRoughness = ClearCoatMaterialData.PerceptualRoughness;
    float3 TopLayerReflectionDirectionWS = BurtGetIndirectSpecularReflectionDirectionWS(ClearCoatGeometryData, 0.0f, ClearCoatRoughness);
    float3 TopLayerRadiance = SampleIndirectSpecularRadiance(TopLayerReflectionDirectionWS, ClearCoatRoughness);
    float2 ClearCoatDFG = GetSpecularDFGTerms(ClearCoatRoughness, ClearCoatGeometryData.NDotV);
    float3 ClearCoatEnvBRDF = EvalSpecularDFG(ClearCoatMaterialData.F0, ClearCoatMaterialData.F90, ClearCoatDFG);
    float3 ClearCoatEnergyCompensation;
    float ClearCoatEnergyPreservation;
    GetSpecularEnergyTerms(ClearCoatMaterialData.F0, ClearCoatMaterialData.F90, ClearCoatRoughness, ClearCoatGeometryData.NDotV, ClearCoatEnergyCompensation, ClearCoatEnergyPreservation);
    float ClearCoatSpecularOcclusion = GetIndirectSpecularOcclusion(ClearCoatGeometryData.NDotV, ClearCoatMaterialData.Occlusion, ClearCoatRoughness);
    float3 TopLayerReflections = TopLayerRadiance * ClearCoatEnvBRDF * ClearCoatEnergyCompensation * ClearCoatSpecularOcclusion;

    float3 LayerTransmission = BurtEvaluateClearCoatLayerTransmission(BaseMaterialData, ClearCoatMaterialData, ClearCoatGeometryData);
    float3 TransmittedBottomLayerReflections = BottomLayerReflections * LayerTransmission;
    return lerp(BottomLayerReflections, TransmittedBottomLayerReflections, ClearCoatMask) + TopLayerReflections * ClearCoatMask;
}

float3 BurtEvaluateClearCoatLayerCombinedSpecular(
    BurtPBRMaterialData BaseMaterialData,
    BurtPBRGeometryData BaseGeometryData,
    float3 BaseIndirectSpecularEnergyCompensation,
    BurtPBRMaterialData ClearCoatMaterialData,
    BurtPBRGeometryData ClearCoatGeometryData,
    float ClearCoatMask)
{
    return BurtClearCoatLayerCombine(BaseMaterialData, BaseGeometryData, BaseIndirectSpecularEnergyCompensation, ClearCoatMaterialData, ClearCoatGeometryData, ClearCoatMask);
}

BurtIndirectPBRComponents BurtApplyClearCoatIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData,
    BurtPBRGeometryData ClearCoatGeometryData)
{
    float ClearCoatMask = saturate(MaterialData.ClearCoatMask);
    if (ClearCoatMask > 0.0001f)
    {
        BurtPBRMaterialData ClearCoatMaterialData = BurtCreateClearCoatMaterialData(MaterialData);
        float3 LayerTransmission = BurtEvaluateClearCoatLayerEnergyPreservation(MaterialData, ClearCoatMaterialData, ClearCoatGeometryData);

        Components.Diffuse = lerp(Components.Diffuse, Components.Diffuse * LayerTransmission, ClearCoatMask);
        Components.Specular = BurtEvaluateClearCoatLayerCombinedSpecular(
            MaterialData,
            GeometryData,
            Components.SpecularEnergyCompensation,
            ClearCoatMaterialData,
            ClearCoatGeometryData,
            ClearCoatMask);
    }

    return Components;
}

#endif
