// Lightweight shading-debug mode helpers shared by Forward's direct evaluator
// and Deferred's compatibility payload path.
#ifndef BURT_SHADING_DEBUG_COMMON_INCLUDED
#define BURT_SHADING_DEBUG_COMMON_INCLUDED

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugModes.hlsl"

bool BurtIsShadingDebugEnabled()
{
    return _BurtShadingDebugEnabled > 0.5f;
}

bool BurtIsSameShadingDebugMode(float mode, float expectedMode)
{
    return abs(mode - expectedMode) < 0.5f;
}

bool BurtNeedsAdditionalLightingUnshadowedShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_LIGHTING_UNSHADOWED);
}

bool BurtNeedsAdditionalShadowAttenuationShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_ATTENUATION);
}

bool BurtNeedsAdditionalShadowProjectionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_FACE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_UV)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_ADDITIONAL_SHADOW_DEPTH_DELTA);
}

bool BurtNeedsMainLightShadowProjectionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RECEIVER_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_RAW_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_COMPARE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_MAIN_LIGHT_SHADOW_PROJECTION_VALIDITY);
}

bool BurtNeedsPerObjectShadowProjectionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_OBJECT_INDEX)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_SLICE)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_UV)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_COMPARE);
}

bool BurtNeedsPerObjectShadowTransmissionShadingDebug()
{
    return BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_DEPTH)
        || BurtIsSameShadingDebugMode(_BurtShadingDebugMode, BURT_SHADING_DEBUG_MODE_PER_OBJECT_SHADOW_TRANSMISSION_THICKNESS);
}

float3 BurtEncodeNormalWSForDebug(float3 normalWS)
{
    float3 safeNormalWS = BurtSafeNormalize(normalWS);
    return safeNormalWS * 0.5f + 0.5f;
}

#endif // BURT_SHADING_DEBUG_COMMON_INCLUDED
