#ifndef BURT_GBUFFER_SUBSURFACE_INCLUDED
#define BURT_GBUFFER_SUBSURFACE_INCLUDED

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_SUBSURFACE;
    return BurtCreateGBufferData(SurfaceData, NormalWS, Emission);
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 GeometryNormalWS, float3 Emission)
{
    BurtGBufferData Data = BurtCreateSubsurfaceGBufferData(SurfaceData, NormalWS, Emission);
    Data.SubsurfaceGeometryNormalWS = BurtSafeNormalize(GeometryNormalWS);
    return Data;
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_SUBSURFACE;
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

BurtGBufferData BurtCreateSubsurfaceGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 GeometryNormalWS, float4 TangentWS, float3 Emission)
{
    BurtGBufferData Data = BurtCreateSubsurfaceGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
    Data.SubsurfaceGeometryNormalWS = BurtSafeNormalize(GeometryNormalWS);
    return Data;
}

bool BurtIsSubsurface3SGBuffer(BurtGBufferData GBufferData)
{
#if BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) &&
        BurtIsSubsurface3SPreIntegratedMode(GBufferData.SubsurfaceScatteringMode);
#else
    return false;
#endif
}

float3 BurtGetSubsurfaceGeometryNormalWS(BurtGBufferData GBufferData)
{
    return BurtSafeNormalize(GBufferData.SubsurfaceGeometryNormalWS);
}

float BurtGetSubsurfaceStrength(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return 1.0f;
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? 1.0f : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetSubsurfaceThickness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(GBufferData.SubsurfaceThickness);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.SubsurfaceThickness) : BURT_SUBSURFACE_DEFAULT_THICKNESS;
#else
    return BURT_SUBSURFACE_DEFAULT_THICKNESS;
#endif
}

float BurtGetSubsurfacePower(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfacePower(GBufferData.SubsurfacePower);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BurtClampSubsurfacePower(GBufferData.SubsurfacePower) : BURT_SUBSURFACE_DEFAULT_POWER;
#else
    return BURT_SUBSURFACE_DEFAULT_POWER;
#endif
}

float BurtGetSubsurfaceDistortion(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(GBufferData.SubsurfaceDistortion);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.SubsurfaceDistortion) : BURT_SUBSURFACE_DEFAULT_DISTORTION;
#else
    return BURT_SUBSURFACE_DEFAULT_DISTORTION;
#endif
}

float BurtGetSubsurfaceScatteringMode(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfaceScatteringMode(GBufferData.SubsurfaceScatteringMode);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BurtClampSubsurfaceScatteringMode(GBufferData.SubsurfaceScatteringMode) : BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
#else
    return BURT_SUBSURFACE_DEFAULT_SCATTERING_MODE;
#endif
}

float BurtGetSubsurfaceAmbient(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return saturate(GBufferData.SubsurfaceAmbient);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.SubsurfaceAmbient) : BURT_SUBSURFACE_DEFAULT_AMBIENT;
#else
    return BURT_SUBSURFACE_DEFAULT_AMBIENT;
#endif
}

float BurtGetSubsurfaceProfileIndex(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_SUBSURFACE_SHADING_MODEL
    return BurtClampSubsurfaceProfileIndex(GBufferData.SubsurfaceProfileIndex);
#elif BURT_ENABLE_SUBSURFACE_SHADING
    return BurtIsActiveSubsurfaceShadingModel(GBufferData.ShadingModelID) ? BurtClampSubsurfaceProfileIndex(GBufferData.SubsurfaceProfileIndex) : BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#else
    return BURT_SUBSURFACE_DEFAULT_PROFILE_INDEX;
#endif
}

#endif
