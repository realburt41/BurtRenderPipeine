// BurtRP material shading-debug lighting mode evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_LIGHTING_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_LIGHTING_INCLUDED

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_MAIN
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugMaterialLightingMain.hlsl"
#endif
#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_ADDITIONAL
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugMaterialLightingAdditional.hlsl"
#endif
#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_INDIRECT
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugMaterialLightingIndirect.hlsl"
#endif
#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_FINAL
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugMaterialLightingFinal.hlsl"
#endif

bool BurtTryEvaluateMaterialLightingShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_MAIN
    if (BurtTryEvaluateMaterialLightingMainShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_ADDITIONAL
    if (BurtTryEvaluateMaterialLightingAdditionalShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_INDIRECT
    if (BurtTryEvaluateMaterialLightingIndirectShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

#if BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING_FINAL
    if (BurtTryEvaluateMaterialLightingFinalShadingDebug(surfaceData, data, debugColor))
    {
        return true;
    }
#endif

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_LIGHTING_INCLUDED
