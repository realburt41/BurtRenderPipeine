#ifndef BURT_GBUFFER_FUR_INCLUDED
#define BURT_GBUFFER_FUR_INCLUDED

BurtSurfaceData BurtApplyFurGBufferSurfaceSemantics(BurtSurfaceData SurfaceData)
{
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_FUR;
    SurfaceData.Metallic = 0.0f;
    return SurfaceData;
}

BurtGBufferData BurtCreateFurGBufferData(BurtSurfaceData SurfaceData, float3 NormalWS, float4 TangentWS, float3 Emission)
{
    SurfaceData = BurtApplyFurGBufferSurfaceSemantics(SurfaceData);
    return BurtCreateGBufferData(SurfaceData, NormalWS, TangentWS, Emission);
}

#endif
