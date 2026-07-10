#ifndef BURT_GBUFFER_FOLIAGE_INCLUDED
#define BURT_GBUFFER_FOLIAGE_INCLUDED

BurtGBufferData BurtCreateFoliageGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FOLIAGE;
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

float3 BurtGetFoliageTransmissionColor(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return max(GBufferData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? max(GBufferData.FoliageTransmissionColor, float3(0.0f, 0.0f, 0.0f)) : float3(0.0f, 0.0f, 0.0f);
#else
    return float3(0.0f, 0.0f, 0.0f);
#endif
}

float BurtGetFoliageTransmissionWeight(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return GBufferData.FoliageIsGrass > 0.5f ? max(GBufferData.FoliageTransmissionWeight, 0.0f) : saturate(GBufferData.FoliageTransmissionWeight);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID)
        ? (GBufferData.FoliageIsGrass > 0.5f ? max(GBufferData.FoliageTransmissionWeight, 0.0f) : saturate(GBufferData.FoliageTransmissionWeight))
        : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageThickness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageThickness);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageThickness) : 0.5f;
#else
    return 0.5f;
#endif
}

float BurtGetFoliageBackLight(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageBackLight);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageBackLight) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageTransmissionNdotL(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageTransmissionNdotL);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageTransmissionNdotL) : 0.5f;
#else
    return 0.5f;
#endif
}

float BurtGetFoliageSpecularScale(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageSpecularScale);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageSpecularScale) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageUseSpecularColor(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageUseSpecularColor);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageUseSpecularColor) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageIsGrass(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return saturate(GBufferData.FoliageIsGrass);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FoliageIsGrass) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFoliageScreenSpaceShadowIntensity(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FOLIAGE_SHADING_MODEL
    return max(GBufferData.FoliageScreenSpaceShadowIntensity, 0.0f);
#elif BURT_ENABLE_FOLIAGE_SHADING
    return BurtIsActiveFoliageShadingModel(GBufferData.ShadingModelID) ? max(GBufferData.FoliageScreenSpaceShadowIntensity, 0.0f) : 0.0f;
#else
    return 0.0f;
#endif
}

#endif
