#ifndef BURT_GBUFFER_EYE_INCLUDED
#define BURT_GBUFFER_EYE_INCLUDED

BurtSurfaceData BurtApplyEyeGBufferSurfaceSemantics(BurtSurfaceData SurfaceData)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_EYE;
    SurfaceData.Metallic = 0.0f;
    SurfaceData.Anisotropy = 0.0f;
    return SurfaceData;
}

BurtGBufferData BurtCreateEyeGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 IrisNormalWS, float3 CausticNormalWS, float3 Emission)
{
    SurfaceData = BurtApplyEyeGBufferSurfaceSemantics(SurfaceData);
    SurfaceData.EyeIrisNormalWS = BurtSafeNormalize(IrisNormalWS);
    SurfaceData.EyeCausticNormalWS = BurtSafeNormalize(CausticNormalWS);
    BurtGBufferData Data = BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
    Data.EyeIrisMask = saturate(SurfaceData.EyeIrisMask);
    Data.EyeIrisNormalWS = BurtSafeNormalize(IrisNormalWS);
    Data.EyeCausticNormalWS = BurtSafeNormalize(CausticNormalWS);
    return Data;
}

BurtGBufferData BurtCreateEyeGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    return BurtCreateEyeGBufferData(SurfaceData, NormalWS, TangentWS, SurfaceData.EyeIrisNormalWS, SurfaceData.EyeCausticNormalWS, Emission);
}

float BurtGetEyeIrisMask(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_EYE_SHADING_MODEL
    return saturate(GBufferData.EyeIrisMask);
#elif BURT_ENABLE_EYE_SHADING
    return BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID) ? saturate(GBufferData.EyeIrisMask) : 0.0f;
#else
    return 0.0f;
#endif
}

float3 BurtGetEyeIrisNormalWS(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_EYE_SHADING_MODEL
    return BurtSafeNormalize(GBufferData.EyeIrisNormalWS);
#elif BURT_ENABLE_EYE_SHADING
    return BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID) ? BurtSafeNormalize(GBufferData.EyeIrisNormalWS) : GBufferData.NormalWS;
#else
    return GBufferData.NormalWS;
#endif
}

float3 BurtGetEyeCausticNormalWS(BurtGBufferData GBufferData)
{
#if BURT_ACTIVE_EYE_SHADING_MODEL
    return BurtSafeNormalize(GBufferData.EyeCausticNormalWS);
#elif BURT_ENABLE_EYE_SHADING
    return BurtIsActiveEyeShadingModel(GBufferData.ShadingModelID) ? BurtSafeNormalize(GBufferData.EyeCausticNormalWS) : GBufferData.NormalWS;
#else
    return GBufferData.NormalWS;
#endif
}

#endif
