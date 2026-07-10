#ifndef BURT_LIGHTING_FUR_INCLUDED
#define BURT_LIGHTING_FUR_INCLUDED

BurtPBRShadingComponents BurtEvaluateFurShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS)
{
    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateFurShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS)
{
    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS, PositionWS);
}

BurtPBRShadingComponents BurtEvaluateFurShadingComponents(BurtSurfaceData SurfaceData, BurtLight MainLight, float3 NormalWS, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    return BurtEvaluatePBRShadingComponents(SurfaceData, MainLight, NormalWS, ViewDirectionWS, PositionWS, ScreenUV);
}

BurtPBRShadingComponents BurtEvaluateFurShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS);
}

BurtPBRShadingComponents BurtEvaluateFurShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS);
}

BurtPBRShadingComponents BurtEvaluateFurShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float2 ScreenUV)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ScreenUV);
}

BurtPBRShadingComponents BurtEvaluateFurShadingComponentsFromGBuffer(BurtGBufferData GBufferData, BurtLight MainLight, float3 ViewDirectionWS, float3 PositionWS, float3 ShadowPositionWS, float2 ScreenUV)
{
    return BurtEvaluatePBRShadingComponentsFromGBuffer(GBufferData, MainLight, ViewDirectionWS, PositionWS, ShadowPositionWS, ScreenUV);
}

#endif
