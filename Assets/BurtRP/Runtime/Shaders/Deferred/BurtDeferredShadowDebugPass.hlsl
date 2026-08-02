// Lightweight deferred shadow diagnostics.
// This pass deliberately avoids the PBR, indirect-lighting, GI, and material-model evaluators.
#ifndef BURT_DEFERRED_SHADOW_DEBUG_PASS_INCLUDED
#define BURT_DEFERRED_SHADOW_DEBUG_PASS_INCLUDED

#define BURT_DEFERRED_LIGHTING_SINGLE_SHADING_MODEL 1
#define BURT_USE_ADDITIONAL_LIGHT_BUFFER 1
#define BURT_USE_TILED_LIGHTING 1
#define BURT_ENABLE_SHADING_DEBUG 1
#define BURT_DEFERRED_LIGHTING_PRUNE_MODEL_HELPERS 1
#define BURT_INCLUDE_SHADOW_DEBUG 1

#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingAdditionalLights.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtShadows.hlsl"

#define BURT_SHADING_DEBUG_MATERIAL_INCLUDE_CORE 0
#define BURT_SHADING_DEBUG_MATERIAL_INCLUDE_BRDF 0
#define BURT_SHADING_DEBUG_MATERIAL_INCLUDE_LIGHTING 0
#define BURT_SHADING_DEBUG_MATERIAL_INCLUDE_TRANSMISSION 0
#define BURT_SHADING_DEBUG_MATERIAL_INCLUDE_HAIR 0
#define BURT_SHADING_DEBUG_MATERIAL_INCLUDE_SHADOW 1
#define BURT_SHADING_DEBUG_INCLUDE_SHADOW 1
#define BURT_SHADING_DEBUG_INCLUDE_ADDITIONAL_LIGHTS 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebug.hlsl"

Varyings Vert(Attributes input)
{
    Varyings output;
    output.PositionCS = BurtGetFullScreenTriangleVertexPosition(input.VertexID);
    output.ScreenUV = BurtGetFullScreenTriangleTexCoord(input.VertexID);
    return output;
}

float3 BurtGetDeferredShadowDebugNormalWS(
    BurtGBufferData gBufferData,
    float3 positionWS,
    float3 viewDirectionWS)
{
    if (BurtIsHairShadingModel(gBufferData.ShadingModelID))
    {
        float3 geometricNormalWS = cross(ddx(positionWS), ddy(positionWS));
        float normalLengthSquared = dot(geometricNormalWS, geometricNormalWS);
        if (normalLengthSquared <= 0.0001f)
        {
            return BurtSafeNormalize(viewDirectionWS);
        }

        geometricNormalWS *= rsqrt(normalLengthSquared);
        return dot(geometricNormalWS, viewDirectionWS) < 0.0f ? -geometricNormalWS : geometricNormalWS;
    }

    return BurtGetDeferredSurfaceNormalWS(gBufferData);
}

float4 Frag(Varyings input) : SV_Target
{
    float rawDepth;
    float3 positionWS;
    float3 shadowPositionWS;
    float3 viewDirectionWS;
    BurtPrepareDeferredViewData(
        input.ScreenUV,
        rawDepth,
        positionWS,
        shadowPositionWS,
        viewDirectionWS);

    BurtGBufferData gBufferData = BurtSampleDeferredGBufferData(input.ScreenUV);
    float3 shadowNormalWS = BurtGetDeferredShadowDebugNormalWS(
        gBufferData,
        positionWS,
        viewDirectionWS);
    int perObjectShadowObjectIndex = BurtSampleDeferredPerObjectShadowObjectIndex(input.ScreenUV);

    BurtShadingDebugData debugData = BurtCreateDefaultShadingDebugData(shadowNormalWS);
    debugData.ShadowAttenuation = BurtSampleMainLightShadowWithoutPerObject(shadowPositionWS, shadowNormalWS);
    debugData.AdditionalShadowAttenuation = BurtNeedsAdditionalShadowAttenuationShadingDebug()
        ? BurtEvaluateAdditionalShadowAttenuationDebug(shadowPositionWS, shadowNormalWS, input.ScreenUV)
        : 1.0f;

    if (BurtNeedsAdditionalShadowProjectionShadingDebug())
    {
        BurtFillAdditionalLightShadowProjectionDebugData(
            shadowPositionWS,
            shadowNormalWS,
            input.ScreenUV,
            debugData.AdditionalShadowFaceColor,
            debugData.AdditionalShadowUVColor,
            debugData.AdditionalShadowDepthColor,
            debugData.AdditionalShadowDepthDeltaColor);
    }

    BurtFillMainLightShadowShadingDebugData(
        shadowPositionWS,
        shadowNormalWS,
        debugData.ShadowCascadeColor,
        debugData.ShadowCascadeBlend,
        debugData.ShadowDistanceFade,
        debugData.ShadowPCSSRadius,
        debugData.ShadowReceiverDepthDelta,
        debugData.MainLightShadowReceiverDepth,
        debugData.MainLightShadowRawDepth,
        debugData.MainLightShadowCompare,
        debugData.MainLightShadowProjectionValidity,
        debugData.ShadowPCSSBlockerFraction);

    BurtFillPerObjectShadowShadingDebugData(
        shadowPositionWS,
        shadowNormalWS,
        perObjectShadowObjectIndex,
        debugData.PerObjectShadowObjectIndexColor,
        debugData.PerObjectShadowSliceColor,
        debugData.PerObjectShadowUVColor,
        debugData.PerObjectShadowDepthColor,
        debugData.PerObjectShadowCompareColor,
        debugData.PerObjectShadowTransmissionDepthColor,
        debugData.PerObjectShadowTransmissionThicknessColor);

    BurtSurfaceData surfaceData = (BurtSurfaceData)0;
    float3 debugColor;
    if (BurtTryEvaluateMaterialShadingDebug(surfaceData, debugData, debugColor))
    {
        return float4(debugColor, 1.0f);
    }

    return float4(0.0f, 0.0f, 0.0f, 1.0f);
}

#endif // BURT_DEFERRED_SHADOW_DEBUG_PASS_INCLUDED
