#ifndef BURT_GBUFFER_FABRIC_INCLUDED
#define BURT_GBUFFER_FABRIC_INCLUDED

BurtGBufferData BurtCreateFabricGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FABRIC;
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

float BurtGetFabricFuzzWeight(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return saturate(GBufferData.FabricFuzzWeight);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FabricFuzzWeight) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetFabricFuzzRoughness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return ClampPerceptualRoughness(GBufferData.FabricFuzzRoughness);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? ClampPerceptualRoughness(GBufferData.FabricFuzzRoughness) : 0.75f;
#else
    return 0.75f;
#endif
}

float3 BurtGetFabricFuzzColor(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return max(GBufferData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f));
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? max(GBufferData.FabricFuzzColor, float3(0.0f, 0.0f, 0.0f)) : float3(1.0f, 1.0f, 1.0f);
#else
    return float3(1.0f, 1.0f, 1.0f);
#endif
}

float BurtGetFabricIsSilk(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_FABRIC_SHADING_MODEL
    return saturate(GBufferData.FabricIsSilk);
#elif BURT_ENABLE_FABRIC_SHADING
    return BurtIsActiveFabricShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.FabricIsSilk) : 0.0f;
#else
    return 0.0f;
#endif
}

#endif
