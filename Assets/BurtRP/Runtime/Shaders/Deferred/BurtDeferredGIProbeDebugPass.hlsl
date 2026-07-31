#ifndef BURT_DEFERRED_GI_PROBE_DEBUG_PASS_INCLUDED
#define BURT_DEFERRED_GI_PROBE_DEBUG_PASS_INCLUDED

// Probe diagnostics are intentionally independent from deferred BRDF and
// lighting evaluation. This is the same stage-first rule XRender applies to
// probe selection/debug views: reconstruct the pixel, sample probe data once,
// and display the requested channel.
#include "UnityCG.cginc"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtInput.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Material/BurtShadingModelMacros.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Deferred/BurtDeferred.hlsl"
#define BURT_LIGHTING_SKY_SH_IRRADIANCE_ONLY 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingSkySH.hlsl"
#define BURT_GI_PROBE_VOLUME_DEBUG_ONLY 1
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingProbeVolume.hlsl"
#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Debug/BurtShadingDebugModes.hlsl"

struct Attributes
{
    uint VertexID : SV_VertexID;
};

struct Varyings
{
    float4 PositionCS : SV_POSITION;
    float2 ScreenUV : TEXCOORD0;
};

Varyings Vert(Attributes Input)
{
    Varyings Output;
    Output.PositionCS = BurtGetFullScreenTriangleVertexPosition(Input.VertexID);
    Output.ScreenUV = BurtGetFullScreenTriangleTexCoord(Input.VertexID);
    return Output;
}

float4 Frag(Varyings Input) : SV_Target
{
    float RawDepth;
    float3 PositionWS;
    float3 ViewDirectionWS;
    BurtPrepareDeferredViewData(Input.ScreenUV, RawDepth, PositionWS, ViewDirectionWS);

#if defined(UNITY_REVERSED_Z)
    if (RawDepth <= 0.0f)
#else
    if (RawDepth >= 1.0f)
#endif
    {
        return float4(0.0f, 0.0f, 0.0f, 1.0f);
    }

    BurtGBufferData GBufferData = BurtDecodeGBuffer(BurtSampleEncodedGBuffer(Input.ScreenUV));
    float3 NormalWS = BurtGetDeferredSurfaceNormalWS(GBufferData);

    float3 Irradiance;
    float Validity;
    float SkyVisibility;
    BurtTrySampleGIProbeVolumeDebugData(
        PositionWS,
        NormalWS,
        ViewDirectionWS,
        Irradiance,
        Validity,
        SkyVisibility);

    if (abs(_BurtShadingDebugMode - BURT_SHADING_DEBUG_MODE_GI_PROBE_IRRADIANCE) < 0.5f)
    {
        return float4(max(Irradiance, float3(0.0f, 0.0f, 0.0f)), 1.0f);
    }

    if (abs(_BurtShadingDebugMode - BURT_SHADING_DEBUG_MODE_GI_PROBE_VALIDITY) < 0.5f)
    {
        return float4(Validity.xxx, 1.0f);
    }

    return float4(SkyVisibility.xxx, 1.0f);
}

#endif
