// BurtRP material shading-debug final-lighting and emission evaluation.
#ifndef BURT_SHADING_DEBUG_MATERIAL_LIGHTING_FINAL_INCLUDED
#define BURT_SHADING_DEBUG_MATERIAL_LIGHTING_FINAL_INCLUDED

bool BurtTryEvaluateMaterialLightingFinalShadingDebug(BurtSurfaceData surfaceData, BurtShadingDebugData data, out float3 debugColor)
{
    debugColor = float3(0.0f, 0.0f, 0.0f);

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_EMISSION))
    {
        debugColor = max(data.EmissionColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    if (BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_FINAL_LIGHTING))
    {
        debugColor = max(data.FinalLightingColor, float3(0.0f, 0.0f, 0.0f));
        return true;
    }

    return false;
}

#endif // BURT_SHADING_DEBUG_MATERIAL_LIGHTING_FINAL_INCLUDED
