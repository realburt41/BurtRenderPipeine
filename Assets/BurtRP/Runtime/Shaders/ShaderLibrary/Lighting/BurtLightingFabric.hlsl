// Fabric-specific indirect lighting helpers.
#ifndef BURT_LIGHTING_FABRIC_INCLUDED
#define BURT_LIGHTING_FABRIC_INCLUDED

float3 BurtEvaluateFabricIndirectFuzzPBR(BurtPBRMaterialData MaterialData, BurtPBRGeometryData GeometryData)
{
    if (MaterialData.FabricActive <= 0.0001f || MaterialData.FabricIsSilk > 0.5f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float FuzzWeight = saturate(MaterialData.FabricFuzzWeight);
    if (FuzzWeight <= 0.0001f)
    {
        return float3(0.0f, 0.0f, 0.0f);
    }

    float3 DiffuseIrradiance = BurtSampleIndirectDiffuseIrradiance(GeometryData.NormalWS);
    float FuzzReflectance = saturate(BurtClothEnergyLookup(MaterialData.FabricFuzzRoughness, GeometryData.NDotV));
    float3 AO = BurtGTAOMultiBounce(MaterialData.Occlusion, MaterialData.BaseColor);
    return DiffuseIrradiance * max(MaterialData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f)) * AO * FuzzReflectance;
}

BurtIndirectPBRComponents BurtApplyFabricIndirectPBRComponents(
    BurtIndirectPBRComponents Components,
    BurtPBRMaterialData MaterialData,
    BurtPBRGeometryData GeometryData)
{
    if (MaterialData.FabricActive > 0.0001f && MaterialData.FabricIsSilk <= 0.5f)
    {
        float FuzzWeight = saturate(MaterialData.FabricFuzzWeight);
        float FuzzEnergyPreservation = saturate(1.0f - BurtClothEnergyLookup(MaterialData.FabricFuzzRoughness, GeometryData.NDotV));
        float BaseLayerWeight = lerp(1.0f, FuzzEnergyPreservation, FuzzWeight);
        Components.Diffuse = Components.Diffuse * BaseLayerWeight + BurtEvaluateFabricIndirectFuzzPBR(MaterialData, GeometryData) * FuzzWeight;
    }

    return Components;
}

#endif
