#ifndef BURT_XGI_COMPAT_DEBUG_INCLUDED
#define BURT_XGI_COMPAT_DEBUG_INCLUDED

#include "UnityCG.cginc"

#if defined(BURT_XGI_COMPAT_ENABLE_PROBE_VOLUME_DEBUG)
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Core/BurtCommon.hlsl"
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingSkySH.hlsl"
    #include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingProbeVolume.hlsl"
#endif

float4 _BurtXGICompatTint;
int _BurtXGICompatDebugLayer;

#define BURT_XGI_COMPAT_PROBE_LAYER_VISIBILITY 0
#define BURT_XGI_COMPAT_PROBE_LAYER_BRICK_SIZE 1
#define BURT_XGI_COMPAT_PROBE_LAYER_VALIDITY 2
#define BURT_XGI_COMPAT_PROBE_LAYER_SKY_VISIBILITY 3
#define BURT_XGI_COMPAT_PROBE_LAYER_SH 4
#define BURT_XGI_COMPAT_PROBE_LAYER_SHL0 5
#define BURT_XGI_COMPAT_PROBE_LAYER_SHL0L1 6

UNITY_INSTANCING_BUFFER_START(BurtXGICompatProps)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BurtXGICompatInstanceColor)
    UNITY_DEFINE_INSTANCED_PROP(float4, _BurtXGICompatProbeAtlasIndex)
UNITY_INSTANCING_BUFFER_END(BurtXGICompatProps)

struct BurtXGICompatAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct BurtXGICompatVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    float2 uv : TEXCOORD1;
    float3 positionWS : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

BurtXGICompatVaryings BurtXGICompatVert(BurtXGICompatAttributes input)
{
    UNITY_SETUP_INSTANCE_ID(input);
    BurtXGICompatVaryings output;
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = UnityObjectToClipPos(input.positionOS.xyz);
    output.positionWS = mul(unity_ObjectToWorld, input.positionOS).xyz;
    output.normalWS = UnityObjectToWorldNormal(input.normalOS);
    output.uv = input.uv;
    return output;
}

#if defined(BURT_XGI_COMPAT_ENABLE_PROBE_VOLUME_DEBUG)
bool BurtXGICompatTryResolveProbeAtlasUV(float4 atlasIndex, out float3 poolUV)
{
    poolUV = 0.0;
    if (atlasIndex.w < -0.5 || _BurtGIProbeVolumeParams.w < 1.5)
    {
        return false;
    }

    float3 dimensions = max(_BurtGIProbeVolumeVirtualPhysicalPoolDimensions.xyz, float3(1.0, 1.0, 1.0));
    float3 texel = floor(atlasIndex.xyz + 0.5);
    if (any(texel < float3(0.0, 0.0, 0.0)) || any(texel >= dimensions))
    {
        return false;
    }

    poolUV = (texel + float3(0.5, 0.5, 0.5)) / dimensions;
    return true;
}

bool BurtXGICompatTryEvaluateAtlasIrradiance(float3 poolUV, float3 normalWS, bool includeL1, bool includeL2, bool includeSky, out float3 irradiance)
{
    irradiance = 0.0;
    float4 l0L1Rx = _BurtGIProbeVolumeVirtualL0L1Rx.SampleLevel(sampler_LinearClamp, poolUV, 0.0);
    float3 l0 = l0L1Rx.rgb;
    float3 l1R = 0.0;
    float3 l1G = 0.0;
    float3 l1B = 0.0;
    bool hasL1 = includeL1 && _BurtGIProbeVolumeVirtualBufferCounts.w > 0.5;
    if (hasL1)
    {
        float4 l1GL1Ry = _BurtGIProbeVolumeVirtualL1GL1Ry.SampleLevel(sampler_LinearClamp, poolUV, 0.0);
        float4 l1BL1Rz = _BurtGIProbeVolumeVirtualL1BL1Rz.SampleLevel(sampler_LinearClamp, poolUV, 0.0);
        l1R = (float3(l0L1Rx.a, l1GL1Ry.a, l1BL1Rz.a) - float3(0.5, 0.5, 0.5)) * (4.0 * l0.r);
        l1G = (l1GL1Ry.rgb - float3(0.5, 0.5, 0.5)) * (4.0 * l0.g);
        l1B = (l1BL1Rz.rgb - float3(0.5, 0.5, 0.5)) * (4.0 * l0.b);
    }

    bool hasL2 = hasL1 && includeL2 && _BurtGIProbeVolumeVirtualBiasL2.z > 0.5;
    float4 l2R = 0.0;
    float4 l2G = 0.0;
    float4 l2B = 0.0;
    float3 l2C = 0.0;
    if (hasL2)
    {
        l2R = (_BurtGIProbeVolumeVirtualL20.SampleLevel(sampler_LinearClamp, poolUV, 0.0) - float4(0.5, 0.5, 0.5, 0.5)) * (7.1554176 * l0.r);
        l2G = (_BurtGIProbeVolumeVirtualL21.SampleLevel(sampler_LinearClamp, poolUV, 0.0) - float4(0.5, 0.5, 0.5, 0.5)) * (7.1554176 * l0.g);
        l2B = (_BurtGIProbeVolumeVirtualL22.SampleLevel(sampler_LinearClamp, poolUV, 0.0) - float4(0.5, 0.5, 0.5, 0.5)) * (7.1554176 * l0.b);
        l2C = (_BurtGIProbeVolumeVirtualL23.SampleLevel(sampler_LinearClamp, poolUV, 0.0).rgb - float3(0.5, 0.5, 0.5)) * (7.1554176 * l0);
    }

    const float shBasis0 = 0.28209479177387814;
    const float shBasis1 = 0.4886025119029199;
    const float shBasis2 = 1.092548430592079;
    const float shBasis3 = 0.31539156525252;
    const float shBasis4 = 0.5462742152960395;
    float3 xgiAxis = normalWS.zxy;
    float3 xgiAxisSquared = xgiAxis * xgiAxis;
    float4 basisL0L1 = float4(
        shBasis0,
        -shBasis1 * xgiAxis.y,
        shBasis1 * xgiAxis.z,
        -shBasis1 * xgiAxis.x);
    if (!hasL1)
    {
        basisL0L1.yzw = float3(0.0, 0.0, 0.0);
    }

    irradiance = float3(
        dot(float4(l0.r, l1R.x, l1R.y, l1R.z), basisL0L1),
        dot(float4(l0.g, l1G.x, l1G.y, l1G.z), basisL0L1),
        dot(float4(l0.b, l1B.x, l1B.y, l1B.z), basisL0L1));
    if (hasL2)
    {
        float4 basisL2 = float4(
            shBasis2 * xgiAxis.x * xgiAxis.y,
            -shBasis2 * xgiAxis.y * xgiAxis.z,
            shBasis3 * (3.0 * xgiAxisSquared.z - 1.0),
            -shBasis2 * xgiAxis.x * xgiAxis.z);
        float basisL2C = shBasis4 * (xgiAxisSquared.x - xgiAxisSquared.y);
        irradiance += float3(
            dot(l2R, basisL2) + l2C.x * basisL2C,
            dot(l2G, basisL2) + l2C.y * basisL2C,
            dot(l2B, basisL2) + l2C.z * basisL2C);
    }

    irradiance *= max(_BurtGIProbeVolumeVirtualMainLightSHParams.rgb, float3(0.0, 0.0, 0.0));
    if (includeSky)
    {
        float skyVisibility = 1.0;
        if (_BurtGIProbeVolumeVirtualBiasL2.w > 0.5)
        {
            float4 skyVisibilityL0L1 = _BurtGIProbeVolumeVirtualSkyVisibilityL0L1.SampleLevel(sampler_LinearClamp, poolUV, 0.0);
            float4 skyVisibilityBasis = float4(
                shBasis0,
                shBasis1 * normalWS.x,
                shBasis1 * normalWS.y,
                shBasis1 * normalWS.z);
            skyVisibility = saturate(lerp(1.0, dot(skyVisibilityBasis, skyVisibilityL0L1), _BurtGIProbeVolumeVirtualSkyVisibilityParams.w));
        }

        irradiance += max(BurtSampleIndirectDiffuseIrradiance(normalWS) * skyVisibility * max(_BurtGIProbeVolumeVirtualSkyVisibilityParams.rgb, float3(0.0, 0.0, 0.0)), float3(0.0, 0.0, 0.0));
    }

    irradiance = max(irradiance * _BurtGIProbeVolumeParams.y, float3(0.0, 0.0, 0.0));
    return true;
}

bool BurtXGICompatTrySampleProbeAtlasDebugColor(float4 atlasIndex, float3 normalWS, out float3 color)
{
    color = 0.0;
    float3 poolUV;
    if (!BurtXGICompatTryResolveProbeAtlasUV(atlasIndex, poolUV))
    {
        return false;
    }

    if (_BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_VALIDITY)
    {
        float validity = _BurtGIProbeVolumeVirtualBufferCounts.z > 0.5
            ? saturate(_BurtGIProbeVolumeVirtualValidity.SampleLevel(sampler_LinearClamp, poolUV, 0.0))
            : 1.0;
        color = lerp(float3(0.95, 0.08, 0.05), float3(0.18, 1.0, 0.24), validity);
        return true;
    }

    if (_BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_VISIBILITY ||
        _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SKY_VISIBILITY)
    {
        float skyVisibility = 1.0;
        if (_BurtGIProbeVolumeVirtualBiasL2.w > 0.5)
        {
            const float shBasis0 = 0.28209479177387814;
            const float shBasis1 = 0.4886025119029199;
            float4 skyVisibilityL0L1 = _BurtGIProbeVolumeVirtualSkyVisibilityL0L1.SampleLevel(sampler_LinearClamp, poolUV, 0.0);
            float4 skyVisibilityBasis = float4(shBasis0, shBasis1 * normalWS.x, shBasis1 * normalWS.y, shBasis1 * normalWS.z);
            skyVisibility = saturate(lerp(1.0, dot(skyVisibilityBasis, skyVisibilityL0L1), _BurtGIProbeVolumeVirtualSkyVisibilityParams.w));
        }

        color = max(float3(skyVisibility, skyVisibility, skyVisibility), float3(0.08, 0.08, 0.0));
        return true;
    }

    if (_BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SH ||
        _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SHL0 ||
        _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SHL0L1)
    {
        bool includeL1 = _BurtXGICompatDebugLayer != BURT_XGI_COMPAT_PROBE_LAYER_SHL0;
        bool includeL2 = _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SH;
        bool includeSky = _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SH;
        return BurtXGICompatTryEvaluateAtlasIrradiance(poolUV, normalWS, includeL1, includeL2, includeSky, color);
    }

    return false;
}

bool BurtXGICompatTryResolveProbeVolumeDebugColor(BurtXGICompatVaryings input, out float3 color)
{
    color = 0.0;
    if (_BurtXGICompatDebugLayer < 0)
    {
        return false;
    }

    float3 normalWS = BurtSafeNormalize(input.normalWS);
    float3 viewDirectionWS = BurtSafeNormalize(_WorldSpaceCameraPos.xyz - input.positionWS);
    float4 atlasIndex = UNITY_ACCESS_INSTANCED_PROP(BurtXGICompatProps, _BurtXGICompatProbeAtlasIndex);
    if (BurtXGICompatTrySampleProbeAtlasDebugColor(atlasIndex, normalWS, color))
    {
        return true;
    }

    if (_BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_VISIBILITY ||
        _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SKY_VISIBILITY ||
        _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_VALIDITY)
    {
        float validity;
        float skyVisibility;
        if (!BurtTrySampleGIProbeVolumeDebugChannels(input.positionWS, normalWS, viewDirectionWS, validity, skyVisibility))
        {
            return false;
        }

        if (_BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_VALIDITY)
        {
            color = lerp(float3(0.95, 0.08, 0.05), float3(0.18, 1.0, 0.24), saturate(validity));
            return true;
        }

        color = max(float3(skyVisibility, skyVisibility, skyVisibility), float3(0.08, 0.08, 0.0));
        return true;
    }

    if (_BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SH ||
        _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SHL0 ||
        _BurtXGICompatDebugLayer == BURT_XGI_COMPAT_PROBE_LAYER_SHL0L1)
    {
        float3 irradiance;
        if (!BurtTrySampleGIProbeVolumeIrradiance(input.positionWS, normalWS, viewDirectionWS, irradiance))
        {
            return false;
        }

        color = max(irradiance, 0.0);
        return true;
    }

    return false;
}
#endif

float4 BurtXGICompatFrag(BurtXGICompatVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    float4 instanceColor = UNITY_ACCESS_INSTANCED_PROP(BurtXGICompatProps, _BurtXGICompatInstanceColor);
    float4 tint = _BurtXGICompatTint * (instanceColor.a > 0.0001 ? instanceColor : float4(1.0, 1.0, 1.0, 1.0));
    float3 normalColor = normalize(input.normalWS + 0.0001) * 0.5 + 0.5;
    float grid = max(step(0.96, frac(input.uv.x * 16.0)), step(0.96, frac(input.uv.y * 16.0)));
    float3 color = saturate(tint.rgb * (0.72 + grid * 0.18) + normalColor * 0.1);
#if defined(BURT_XGI_COMPAT_ENABLE_PROBE_VOLUME_DEBUG)
    float3 probeVolumeDebugColor;
    if (BurtXGICompatTryResolveProbeVolumeDebugColor(input, probeVolumeDebugColor))
    {
        color = max(probeVolumeDebugColor, 0.0);
    }
#endif
    return float4(color, tint.a);
}

#endif
