// Split from BurtMaterialShadingModelPassCommon.hlsl.
#ifndef BURT_MATERIAL_PASS_GBUFFER_BRIDGE_INCLUDED
#define BURT_MATERIAL_PASS_GBUFFER_BRIDGE_INCLUDED

#if defined(BURT_GBUFFER_INCLUDED)

BurtGBufferData BurtCreateMaterialPassGBufferData_DefaultLit(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}

#if BURT_ENABLE_HAIR_SHADING
BurtGBufferData BurtCreateMaterialPassGBufferData_Hair(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateHairGBufferData(SurfaceData, ShadingDirectionWS, BaseNormalWS, GeometryNormalWS, EmissionColor);
}
#endif

#if BURT_ENABLE_CLEAR_COAT_SHADING
BurtGBufferData BurtCreateMaterialPassGBufferData_ClearCoat(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    float3 ClearCoatNormalWS = BurtSampleClearCoatNormalWS(NormalMapUV, GeometryNormalWS, TangentWS, _ClearCoatNormalScale, Facing, _DoubleSidedNormalModeConstants);
    return BurtCreateClearCoatGBufferData(SurfaceData, BaseNormalWS, TangentWS, ClearCoatNormalWS, EmissionColor);
}
#endif

#if BURT_ENABLE_SUBSURFACE_SHADING
BurtGBufferData BurtCreateMaterialPassGBufferData_Subsurface(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateSubsurfaceGBufferData(SurfaceData, BaseNormalWS, GeometryNormalWS, TangentWS, EmissionColor);
}
#endif

#if BURT_ENABLE_FOLIAGE_SHADING
BurtGBufferData BurtCreateMaterialPassGBufferData_Foliage(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateFoliageGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}
#endif

#if BURT_ENABLE_FABRIC_SHADING
BurtGBufferData BurtCreateMaterialPassGBufferData_Fabric(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateFabricGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}
#endif

#if BURT_ENABLE_FUR_SHADING
BurtGBufferData BurtCreateMaterialPassGBufferData_Fur(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateFurGBufferData(SurfaceData, BaseNormalWS, TangentWS, EmissionColor);
}
#endif

#if BURT_ENABLE_EYE_SHADING
BurtGBufferData BurtCreateMaterialPassGBufferData_Eye(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
    return BurtCreateEyeGBufferData(SurfaceData, BaseNormalWS, TangentWS, SurfaceData.EyeIrisNormalWS, SurfaceData.EyeCausticNormalWS, EmissionColor);
}
#endif

BurtGBufferData BurtCreateMaterialPassGBufferData(
    BurtSurfaceData SurfaceData,
    float2 NormalMapUV,
    float3 GeometryNormalWS,
    float3 BaseNormalWS,
    float4 TangentWS,
    float3 ShadingDirectionWS,
    float Facing,
    float3 EmissionColor)
{
#if defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_HAIR)
    return BurtCreateMaterialPassGBufferData_Hair(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_CLEAR_COAT)
    return BurtCreateMaterialPassGBufferData_ClearCoat(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_SUBSURFACE)
    return BurtCreateMaterialPassGBufferData_Subsurface(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FABRIC)
    return BurtCreateMaterialPassGBufferData_Fabric(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FOLIAGE)
    return BurtCreateMaterialPassGBufferData_Foliage(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_FUR)
    return BurtCreateMaterialPassGBufferData_Fur(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#elif defined(BURT_MATERIAL_SELECTED_SHADING_MODEL_EYE)
    return BurtCreateMaterialPassGBufferData_Eye(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#else
    return BurtCreateMaterialPassGBufferData_DefaultLit(SurfaceData, NormalMapUV, GeometryNormalWS, BaseNormalWS, TangentWS, ShadingDirectionWS, Facing, EmissionColor);
#endif
}

#endif // BURT_GBUFFER_INCLUDED

#endif // BURT_MATERIAL_PASS_GBUFFER_BRIDGE_INCLUDED
