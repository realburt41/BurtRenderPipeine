// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_SUBSURFACE_INCLUDED
#define BURT_MATERIAL_PASS_SUBSURFACE_INCLUDED

#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
float BurtEvaluateSubsurfaceMaterialThickness(float2 BaseMapUV)
{
    return saturate(_SubsurfaceThickness * BurtSampleSubsurfaceThicknessMap(BaseMapUV));
}
#endif

#endif // BURT_MATERIAL_PASS_SUBSURFACE_INCLUDED
