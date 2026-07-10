#ifndef BURT_GBUFFER_CLEAR_COAT_INCLUDED
#define BURT_GBUFFER_CLEAR_COAT_INCLUDED

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 ClearCoatNormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_CLEAR_COAT;
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
    Data.ClearCoatNormalWS = BurtSafeNormalize(ClearCoatNormalWS);
    return Data;
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 ClearCoatNormalWS, float3 Emission)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_CLEAR_COAT;
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, NormalWS, Emission);
    Data.ClearCoatNormalWS = BurtSafeNormalize(ClearCoatNormalWS);
    return Data;
}

BurtGBufferData BurtCreateClearCoatGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float3 Emission)
{
    return BurtCreateClearCoatGBufferData(SurfaceData, NormalWS, NormalWS, Emission);
}

float3 BurtGetClearCoatNormalWS(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return GBufferData.ClearCoatNormalWS;
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(GBufferData.ShadingModelID) ? GBufferData.ClearCoatNormalWS : GBufferData.NormalWS;
#else
    return GBufferData.NormalWS;
#endif
}

float BurtGetClearCoatMask(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return saturate(GBufferData.ClearCoatMask);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.ClearCoatMask) : 0.0f;
#else
    return 0.0f;
#endif
}

float BurtGetClearCoatRoughness(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_CLEAR_COAT_SHADING_MODEL
    return ClampPerceptualRoughness(GBufferData.ClearCoatRoughness);
#elif BURT_ENABLE_CLEAR_COAT_SHADING
    return BurtIsActiveClearCoatShadingModel(GBufferData.ShadingModelID) ? ClampPerceptualRoughness(GBufferData.ClearCoatRoughness) : 0.2f;
#else
    return 0.2f;
#endif
}

#endif
