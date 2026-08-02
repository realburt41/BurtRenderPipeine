// Additional-light storage, tile/cluster light-list lookup, and additional-shadow diagnostics.
#ifndef BURT_LIGHTING_ADDITIONAL_LIGHTS_INCLUDED
#define BURT_LIGHTING_ADDITIONAL_LIGHTS_INCLUDED

// XRender-style punctual tile-bin specialization. Fullscreen and shadow bins
// keep the complete loop; non-shadow bins have a compile-time maximum and can
// omit shadow sampling because the CPU classifier routes every shadowed tile to
// the default keyword variant.
#if defined(BURT_PUNCTUAL_BIN_1_2)
    #define BURT_PUNCTUAL_LIGHT_LOOP_MAX 2
    #define BURT_PUNCTUAL_BIN_UNSHADOWED 1
#elif defined(BURT_PUNCTUAL_BIN_3_8)
    #define BURT_PUNCTUAL_LIGHT_LOOP_MAX 8
    #define BURT_PUNCTUAL_BIN_UNSHADOWED 1
#else
    #define BURT_PUNCTUAL_LIGHT_LOOP_MAX BURT_MAX_ADDITIONAL_LIGHTS
#endif

#if !defined(BURT_MAIN_LIGHT_DIRECTION_DECLARED)
#define BURT_MAIN_LIGHT_DIRECTION_DECLARED
float4 _BurtMainLightDirection;
#endif

float4 _BurtMainLightColor;
float4 _BurtMainLightColorOuterSpace;
float4 _BurtMainLightAtmosphereTransmittance;
float _BurtMainLightOcclusionFactor;

#if !defined(BURT_EXCLUDE_ADDITIONAL_LIGHTING)
#define BURT_MAX_ADDITIONAL_LIGHTS 8
float _BurtAdditionalLightCount;
float4 _BurtAdditionalLightPositionAndRange[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightColorAndType[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightDirectionAndSpot[BURT_MAX_ADDITIONAL_LIGHTS];
float4 _BurtAdditionalLightSpotParams[BURT_MAX_ADDITIONAL_LIGHTS];

#define BURT_ADDITIONAL_LIGHT_BUFFER_ROWS 4

#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
StructuredBuffer<float4> _BurtAdditionalLightBuffer;
float _BurtAdditionalLightBufferEnabled;
#endif


#define BURT_LIGHT_TYPE_DIRECTIONAL (0.0f)
#define BURT_LIGHT_TYPE_POINT (1.0f)
#define BURT_LIGHT_TYPE_SPOT (2.0f)
float BurtSampleAdditionalLightShadow(int lightIndex, float3 positionWS, float3 lightDirectionWS, float3 normalWS, float3 lightPositionWS);
#else
#define BURT_MAX_ADDITIONAL_LIGHTS 0
#endif


struct BurtLight
{
    float3 DirectionWS;

    float3 Color;

    float ShadowAttenuation;

    float TransmissionShadowAttenuation;

    float TransmissionThickness;
};

// Creates the current main light from BurtRP globals.
BurtLight BurtCreateMainLight(float shadowAttenuation, float transmissionShadowAttenuation, float transmissionThickness)
{
    BurtLight light;
    light.DirectionWS = BurtSafeNormalize(_BurtMainLightDirection.xyz);
    light.Color = _BurtMainLightColor.rgb;
    light.ShadowAttenuation = shadowAttenuation;
    light.TransmissionShadowAttenuation = transmissionShadowAttenuation;
    light.TransmissionThickness = transmissionThickness;
    return light;
}

BurtLight BurtCreateMainLight(float shadowAttenuation, float transmissionShadowAttenuation)
{
    return BurtCreateMainLight(shadowAttenuation, transmissionShadowAttenuation, -1.0f);
}

BurtLight BurtCreateMainLight(float shadowAttenuation)
{
    return BurtCreateMainLight(shadowAttenuation, shadowAttenuation);
}

#if !defined(BURT_EXCLUDE_ADDITIONAL_LIGHTING)
int BurtGetAdditionalLightCount()
{
    return min((int)round(max(_BurtAdditionalLightCount, 0.0f)), BURT_MAX_ADDITIONAL_LIGHTS);
}

bool BurtHasAdditionalLights()
{
    return BurtGetAdditionalLightCount() > 0;
}


#include "Assets/BurtRP/Runtime/Shaders/ShaderLibrary/Lighting/BurtLightingAdditionalLightList.hlsl"

float4 BurtReadAdditionalLightPositionAndRange(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS];
    }
#endif

    return _BurtAdditionalLightPositionAndRange[lightIndex];
}

float4 BurtReadAdditionalLightColorAndType(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 1];
    }
#endif

    return _BurtAdditionalLightColorAndType[lightIndex];
}

float4 BurtReadAdditionalLightDirectionAndSpot(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 2];
    }
#endif

    return _BurtAdditionalLightDirectionAndSpot[lightIndex];
}

float4 BurtReadAdditionalLightSpotParams(int lightIndex)
{
#if defined(BURT_USE_ADDITIONAL_LIGHT_BUFFER)
    if (_BurtAdditionalLightBufferEnabled > 0.5f)
    {
        return _BurtAdditionalLightBuffer[lightIndex * BURT_ADDITIONAL_LIGHT_BUFFER_ROWS + 3];
    }
#endif

    return _BurtAdditionalLightSpotParams[lightIndex];
}

bool BurtAdditionalLightUsesInverseSquaredFalloff(float packedFalloffAndNearCutoff)
{
    return packedFalloffAndNearCutoff >= 0.0f;
}

float BurtDecodeAdditionalLightVolumetricNearCutoff(float packedFalloffAndNearCutoff)
{
    return packedFalloffAndNearCutoff >= 0.0f
        ? packedFalloffAndNearCutoff
        : max(-packedFalloffAndNearCutoff - 1.0f, 0.0f);
}

float BurtEvaluateAdditionalLightDistanceAttenuation(
    float distanceSquared,
    float range,
    bool useInverseSquaredFalloff)
{
    float safeRange = max(range, 0.0001f);
    float normalizedDistanceSquared = distanceSquared / max(safeRange * safeRange, BURT_EPSILON);
    if (!useInverseSquaredFalloff)
    {
        return saturate(1.0f - sqrt(saturate(normalizedDistanceSquared)));
    }

    float smoothFactor = saturate(1.0f - normalizedDistanceSquared * normalizedDistanceSquared);
    return smoothFactor * smoothFactor * rcp(max(distanceSquared, 0.0001f));
}

BurtLight BurtCreateAdditionalLightInternal(int lightIndex, float3 positionWS, float3 normalWS, bool sampleShadow, float3 shadowPositionWS)
{
    BurtLight light;
    light.DirectionWS = float3(0.0f, 1.0f, 0.0f);
    light.Color = float3(0.0f, 0.0f, 0.0f);
    light.ShadowAttenuation = 1.0f;
    light.TransmissionShadowAttenuation = 1.0f;
    light.TransmissionThickness = -1.0f;

    float4 colorAndType = BurtReadAdditionalLightColorAndType(lightIndex);
    float lightType = colorAndType.w;

    if (lightType < 0.5f)
    {
        light.DirectionWS = BurtSafeNormalize(BurtReadAdditionalLightDirectionAndSpot(lightIndex).xyz);
        light.Color = max(colorAndType.rgb, float3(0.0f, 0.0f, 0.0f));
        return light;
    }

    float4 positionAndRange = BurtReadAdditionalLightPositionAndRange(lightIndex);
    float3 toLight = positionAndRange.xyz - positionWS;
    float distanceSquared = dot(toLight, toLight);
    light.DirectionWS = BurtSafeNormalize(toLight);

    float4 spotParams = BurtReadAdditionalLightSpotParams(lightIndex);
    float attenuation = BurtEvaluateAdditionalLightDistanceAttenuation(
        distanceSquared,
        positionAndRange.w,
        BurtAdditionalLightUsesInverseSquaredFalloff(spotParams.w));

    if (lightType > 1.5f)
    {
        float3 spotDirectionWS = BurtSafeNormalize(BurtReadAdditionalLightDirectionAndSpot(lightIndex).xyz);
        float3 fromLightDirectionWS = -light.DirectionWS;
        float spotCos = dot(fromLightDirectionWS, spotDirectionWS);
        float spotFade = saturate((spotCos - spotParams.y) * spotParams.z);
        attenuation *= spotFade * spotFade;
    }

    light.Color = max(colorAndType.rgb, float3(0.0f, 0.0f, 0.0f)) * attenuation;
    light.ShadowAttenuation = sampleShadow && lightType > 0.5f ? BurtSampleAdditionalLightShadow(lightIndex, shadowPositionWS, light.DirectionWS, normalWS, positionAndRange.xyz) : 1.0f;
    light.TransmissionShadowAttenuation = light.ShadowAttenuation;
    return light;
}

BurtLight BurtCreateAdditionalLightInternal(int lightIndex, float3 positionWS, float3 normalWS, bool sampleShadow)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, sampleShadow, positionWS);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, true);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS, float3 shadowPositionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, true, shadowPositionWS);
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, float3(0.0f, 0.0f, 0.0f), true);
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, normalWS, false);
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS)
{
    return BurtCreateAdditionalLightInternal(lightIndex, positionWS, float3(0.0f, 0.0f, 0.0f), false);
}
#else
int BurtGetAdditionalLightCount()
{
    return 0;
}

bool BurtHasAdditionalLights()
{
    return false;
}

BurtLight BurtCreateExcludedAdditionalLight()
{
    BurtLight light;
    light.DirectionWS = float3(0.0f, 1.0f, 0.0f);
    light.Color = float3(0.0f, 0.0f, 0.0f);
    light.ShadowAttenuation = 1.0f;
    light.TransmissionShadowAttenuation = 1.0f;
    light.TransmissionThickness = -1.0f;
    return light;
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateExcludedAdditionalLight();
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS, float3 normalWS, float3 shadowPositionWS)
{
    return BurtCreateExcludedAdditionalLight();
}

BurtLight BurtCreateAdditionalLight(int lightIndex, float3 positionWS)
{
    return BurtCreateExcludedAdditionalLight();
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS, float3 normalWS)
{
    return BurtCreateExcludedAdditionalLight();
}

BurtLight BurtCreateAdditionalLightUnshadowed(int lightIndex, float3 positionWS)
{
    return BurtCreateExcludedAdditionalLight();
}
#endif

#endif
