// Foliage-specific PBR composition helpers.
#ifndef BURT_LIGHTING_PBR_CORE_FOLIAGE_INCLUDED
#define BURT_LIGHTING_PBR_CORE_FOLIAGE_INCLUDED

BurtPBRShadingComponents BurtApplyFoliagePBRShadingComponents(
    BurtPBRShadingComponents Components,
    BurtPBRShadingCoreData CoreData,
    BurtDirectPBRComponents DirectComponents)
{
#if BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG
    float FoliageMask = CoreData.MaterialData.FoliageActive > 0.5f ? 1.0f : 0.0f;
    Components.FoliageMask = FoliageMask;
    Components.FoliageTransmission = max(DirectComponents.TransmissionThroughput, float3(0.0f, 0.0f, 0.0f)) * FoliageMask;
    Components.FoliageDirectTransmission = DirectComponents.Transmission * FoliageMask;
    Components.FoliageTransmissionBRDF = DirectComponents.TransmissionBRDF * FoliageMask;
    Components.FoliageTransmissionShadow = lerp(1.0f, DirectComponents.TransmissionShadow, FoliageMask);
    Components.FoliageSpecularBRDF = DirectComponents.BrdfTerms.SpecularBRDF * FoliageMask;
#endif
    return Components;
}

#endif // BURT_LIGHTING_PBR_CORE_FOLIAGE_INCLUDED
