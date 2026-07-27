// Subsurface-specific PBR composition helpers.
#ifndef BURT_LIGHTING_PBR_CORE_SUBSURFACE_INCLUDED
#define BURT_LIGHTING_PBR_CORE_SUBSURFACE_INCLUDED

void BurtResolveSubsurfaceDeferredPostprocessTransmission(
    BurtPBRMaterialData MaterialData,
    inout float3 ResolvedDirectDiffuse,
    inout float3 ResolvedDirectTransmission,
    inout float3 ResolvedIndirectDiffuse,
    inout float3 ResolvedIndirectTransmission)
{
#if defined(BURT_SUBSURFACE_DEFERRED_POSTPROCESS_INPUT)
    bool FoldSubsurfaceTransmissionIntoDiffuse =
        BurtGetSubsurfaceMaterialWeight(MaterialData) > 0.0001f &&
        !BurtIsSubsurface3SPreIntegratedMode(MaterialData.SubsurfaceScatteringMode);
    if (FoldSubsurfaceTransmissionIntoDiffuse)
    {
        ResolvedDirectDiffuse += ResolvedDirectTransmission;
        ResolvedIndirectDiffuse += ResolvedIndirectTransmission;
        ResolvedDirectTransmission = float3(0.0f, 0.0f, 0.0f);
        ResolvedIndirectTransmission = float3(0.0f, 0.0f, 0.0f);
    }
#endif
}

BurtPBRShadingComponents BurtApplySubsurfacePBRShadingComponents(
    BurtPBRShadingComponents Components,
    BurtPBRShadingCoreData CoreData,
    BurtDirectPBRComponents DirectComponents,
    BurtIndirectPBRComponents IndirectComponents)
{
#if BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG
    Components.SubsurfaceProfileIndex = CoreData.MaterialData.SubsurfaceProfileIndex;
    Components.SubsurfaceTransmission = float3(0.0f, 0.0f, 0.0f);
    Components.SubsurfaceDirectTransmission = DirectComponents.Transmission;
    Components.SubsurfaceTransmissionBRDF = DirectComponents.TransmissionBRDF;
    Components.SubsurfaceTransmissionLobe = DirectComponents.TransmissionLobe;
    Components.SubsurfaceTransmissionPhase = DirectComponents.TransmissionPhase;
    Components.SubsurfaceTransmissionShadow = DirectComponents.TransmissionShadow;
    Components.SubsurfaceTransmissionThickness = DirectComponents.TransmissionThickness;
    Components.SubsurfaceKernelWeight = float3(0.0f, 0.0f, 0.0f);
#endif
    Components.SubsurfaceIndirect = IndirectComponents.SubsurfaceIndirect;
    Components.SubsurfaceIndirectTransmission = IndirectComponents.SubsurfaceIndirectTransmission;

#if BURT_PBR_SHADING_COMPONENTS_INCLUDE_TRANSMISSION_DEBUG
    if (BurtGetSubsurfaceMaterialWeight(CoreData.MaterialData) > 0.0001f)
    {
        Components.SubsurfaceTransmission = max(DirectComponents.TransmissionThroughput, float3(0.0f, 0.0f, 0.0f));
        Components.SubsurfaceKernelWeight = BurtUseSubsurfaceProfileParamLut()
            ? max(BurtFetchSubsurfaceProfileParam(BURT_SUBSURFACE_PROFILE_PARAM_KERNEL0_OFFSET, CoreData.MaterialData.SubsurfaceProfileIndex).rgb, float3(0.0f, 0.0f, 0.0f))
            : float3(0.204f, 0.236f, 0.290f);
    }
#endif

    return Components;
}

#endif // BURT_LIGHTING_PBR_CORE_SUBSURFACE_INCLUDED
