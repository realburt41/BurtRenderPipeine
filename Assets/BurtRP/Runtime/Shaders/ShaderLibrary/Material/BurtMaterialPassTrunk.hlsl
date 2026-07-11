// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_TRUNK_INCLUDED
#define BURT_MATERIAL_PASS_TRUNK_INCLUDED

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_TRUNK)
float BurtEvaluateTrunkVertexAO(float4 VertexColor)
{
    return saturate((saturate(VertexColor.a) - _VertexAORemap.x) / max(_VertexAORemap.y - _VertexAORemap.x, BURT_EPSILON));
}

BurtSurfaceData BurtApplyTrunkXRenderSurfaceSemantics(BurtSurfaceData SurfaceData, float4 MaskMap, float4 VertexColor)
{
    float MapOcclusion = saturate(MaskMap.g);
    float VertexAO = BurtEvaluateTrunkVertexAO(VertexColor);

    SurfaceData.Metallic = 0.0f;
    SurfaceData.Anisotropy = 0.0f;
    SurfaceData.Reflectance = saturate(_Specular);
    SurfaceData.Smoothness = saturate(1.0f - saturate(MaskMap.a));
    SurfaceData.Occlusion = min(MapOcclusion, VertexAO);
    SurfaceData.Height = 0.5f;
    SurfaceData.ShadingModelID = BURT_SHADING_MODEL_DEFAULT_LIT;
    return SurfaceData;
}
#endif

#endif // BURT_MATERIAL_PASS_TRUNK_INCLUDED
